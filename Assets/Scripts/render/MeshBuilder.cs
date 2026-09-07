using System;
using System.Collections.Generic;
using UnityEngine;
using World.blocks;

namespace render
{
    public class MeshBuilder
    {
        [Flags]
        public enum MeshTargets
        {
            Opaque = 1 << 0,
            Transparent = 1 << 1,
            Collider = 1 << 2,
            Water = 1 << 3
        }

        public class MeshHolder
        {
            public readonly List<Vector3> Vertices = new();
            public readonly List<int> Triangles = new();

            private int VertexCount => Vertices.Count;
            public bool IsEmpty => VertexCount == 0;

            public void AddQuad(Vector3[] vertices, bool reverse = false)
            {
                int vertexIndex = VertexCount;
                for (int i = 0; i < 4; i++) Vertices.Add(vertices[i]);

                if (reverse)
                {
                    Triangles.Add(vertexIndex + 2);
                    Triangles.Add(vertexIndex + 1);
                    Triangles.Add(vertexIndex);
                    Triangles.Add(vertexIndex + 3);
                    Triangles.Add(vertexIndex + 1);
                    Triangles.Add(vertexIndex + 2);
                }
                else
                {
                    Triangles.Add(vertexIndex);
                    Triangles.Add(vertexIndex + 1);
                    Triangles.Add(vertexIndex + 2);
                    Triangles.Add(vertexIndex + 2);
                    Triangles.Add(vertexIndex + 1);
                    Triangles.Add(vertexIndex + 3);
                }
            }
        }

        public sealed class TexturedMeshHolder : MeshHolder
        {
            public readonly List<Vector2> Uvs = new();
            public readonly List<Vector4> TextureIndices = new();

            public void AddQuad(Vector3[] vertices, Vector2[] uvs, Vector4 texture, bool reverse = false)
            {
                base.AddQuad(vertices, reverse);
                for (int i = 0; i < 4; i++)
                {
                    Uvs.Add(uvs[i]);
                    TextureIndices.Add(texture);
                }
            }
        }

        public readonly TexturedMeshHolder OpaqueMesh = new();
        public readonly TexturedMeshHolder TransparentMesh = new();
        public readonly TexturedMeshHolder WaterMesh = new();
        public readonly MeshHolder ColliderMesh = new();
        public readonly List<int> TriangleCoordinate = new();
        public readonly List<int> TriangleFace = new();

        public record CubicModel(Vector3[] VerticesLookup, int[,] TrianglesLookup, Vector2[] UvsLookup);

        public static readonly CubicModel DefaultModel = new(
            new Vector3[]
            {
                new(0.0f, 0.0f, 0.0f),
                new(1.0f, 0.0f, 0.0f),
                new(1.0f, 1.0f, 0.0f),
                new(0.0f, 1.0f, 0.0f),
                new(0.0f, 0.0f, 1.0f),
                new(1.0f, 0.0f, 1.0f),
                new(1.0f, 1.0f, 1.0f),
                new(0.0f, 1.0f, 1.0f)
            }, new[,]
            {
                { 3, 7, 2, 6 }, // top
                { 1, 5, 0, 4 }, // bottom
                { 5, 6, 4, 7 }, // front
                { 0, 3, 1, 2 }, // back
                { 4, 7, 0, 3 }, // left
                { 1, 2, 5, 6 } // right
            }, new Vector2[]
            {
                new(0, 0),
                new(0, 1),
                new(1, 0),
                new(1, 1)
            }
        );

        public void AddFace(int face, Vector3 position, Block block)
        {
            AddFace(face, position, block, DefaultModel, new Vector4(block.TextureIndex(face), 1, 1, 0), DefaultTargets(block));
        }

        public void AddFace(int face, Vector3 position, Block block, CubicModel model)
        {
            AddFace(face, position, block, model, new Vector4(block.TextureIndex(face), 1, 1, 0), DefaultTargets(block));
        }

        public void AddFace(int face, Vector3 position, Block block, MeshTargets targets)
        {
            AddFace(face, position, block, DefaultModel, new Vector4(block.TextureIndex(face), 1, 1, 0), targets);
        }

        public void AddFace(int face, Vector3 position, Block block, CubicModel model, MeshTargets targets)
        {
            AddFace(face, position, block, model, new Vector4(block.TextureIndex(face), 1, 1, 0), targets);
        }

        public void AddFace(int face, Vector3 position, Block block, CubicModel model, Vector4 texture, MeshTargets targets)
        {
            AddFace(face, position, block, model, model.UvsLookup, texture, targets);
        }

        public void AddFace(int face, Vector3 position, Block block, CubicModel model, Vector2[] uvs, Vector4 texture,
            MeshTargets targets)
        {
            Vector3[] vertices = new Vector3[4];
            for (int i = 0; i < 4; i++) vertices[i] = model.VerticesLookup[model.TrianglesLookup[face, i]] + position;

            AddQuad(vertices, uvs, texture, targets);

            if ((targets & MeshTargets.Collider) == 0) return;

            int serialized = ((int)position.x << 16) | ((int)position.y << 8) | (int)position.z;
            TriangleCoordinate.Add(serialized);
            TriangleFace.Add(face);
        }

        public void AddQuad(Vector3[] vertices, Vector2[] uvs, Vector4 texture, MeshTargets targets, bool reverse = false)
        {
            if ((targets & MeshTargets.Opaque) != 0) OpaqueMesh.AddQuad(vertices, uvs, texture, reverse);
            if ((targets & MeshTargets.Transparent) != 0) TransparentMesh.AddQuad(vertices, uvs, texture, reverse);
            if ((targets & MeshTargets.Water) != 0) WaterMesh.AddQuad(vertices, uvs, texture, reverse);
            if ((targets & MeshTargets.Collider) != 0) ColliderMesh.AddQuad(vertices, reverse);
        }

        private static MeshTargets DefaultTargets(Block block)
        {
            MeshTargets targets = block.Transparent ? MeshTargets.Transparent : MeshTargets.Opaque;
            if (block.Collide) targets |= MeshTargets.Collider;
            return targets;
        }
    }
}
