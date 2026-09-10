using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
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
                AddQuad(vertices[0], vertices[1], vertices[2], vertices[3], reverse);
            }

            public virtual void AddQuad(Vector3 first, Vector3 second, Vector3 third, Vector3 fourth,
                bool reverse = false)
            {
                int vertexIndex = VertexCount;
                Vertices.Add(first);
                Vertices.Add(second);
                Vertices.Add(third);
                Vertices.Add(fourth);

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

            public virtual void Clear()
            {
                Vertices.Clear();
                Triangles.Clear();
            }

            public void UploadTo(Mesh mesh)
            {
                PrepareMesh(mesh, VertexCount);
                if (IsEmpty) return;
                mesh.SetVertices(Vertices);
                mesh.SetTriangles(Triangles, 0, true);
            }

            protected static void PrepareMesh(Mesh mesh, int vertexCount)
            {
                mesh.Clear(false);
                IndexFormat requiredFormat = vertexCount > ushort.MaxValue ? IndexFormat.UInt32 : IndexFormat.UInt16;
                if (mesh.indexFormat != requiredFormat) mesh.indexFormat = requiredFormat;
            }
        }

        public sealed class TexturedMeshHolder : MeshHolder
        {
            public readonly List<Vector2> Uvs = new();
            public readonly List<Vector4> TextureIndices = new();
            public readonly List<Vector3> Normals = new();

            public void AddQuad(Vector3[] vertices, Vector2[] uvs, Vector4 texture, bool reverse = false)
            {
                AddQuad(vertices[0], vertices[1], vertices[2], vertices[3],
                    uvs[0], uvs[1], uvs[2], uvs[3], texture, reverse);
            }

            public void AddQuad(Vector3 first, Vector3 second, Vector3 third, Vector3 fourth,
                Vector2 firstUv, Vector2 secondUv, Vector2 thirdUv, Vector2 fourthUv,
                Vector4 texture, bool reverse = false)
            {
                base.AddQuad(first, second, third, fourth, reverse);
                Uvs.Add(firstUv);
                Uvs.Add(secondUv);
                Uvs.Add(thirdUv);
                Uvs.Add(fourthUv);

                Vector3 normal = Vector3.Cross(second - first, third - first).normalized;
                if (reverse) normal = -normal;
                for (int i = 0; i < 4; i++)
                {
                    TextureIndices.Add(texture);
                    Normals.Add(normal);
                }
            }

            public override void Clear()
            {
                base.Clear();
                Uvs.Clear();
                TextureIndices.Clear();
                Normals.Clear();
            }

            public new void UploadTo(Mesh mesh) => UploadTo(mesh, true);

            public void UploadTo(Mesh mesh, bool useProvidedFlatNormals)
            {
                PrepareMesh(mesh, Vertices.Count);
                if (IsEmpty) return;
                mesh.SetVertices(Vertices);
                if (useProvidedFlatNormals) mesh.SetNormals(Normals);
                mesh.SetUVs(0, Uvs);
                mesh.SetUVs(1, TextureIndices);
                mesh.SetTriangles(Triangles, 0, true);
                // Fluid surface quads can be non-planar. Preserve RecalculateNormals' two-triangle
                // smoothing for those meshes while block faces use the cheaper supplied flat normals.
                if (!useProvidedFlatNormals) mesh.RecalculateNormals();
            }
        }

        public readonly TexturedMeshHolder OpaqueMesh = new();
        public readonly TexturedMeshHolder TransparentMesh = new();
        public readonly TexturedMeshHolder WaterMesh = new();
        public readonly MeshHolder ColliderMesh = new();
        public readonly MeshHolder WaterSourceColliderMesh = new();
        public readonly List<int> TriangleCoordinate = new();
        public readonly List<int> TriangleFace = new();
        public readonly List<int> WaterSourceTriangleCoordinate = new();

        public void Clear()
        {
            OpaqueMesh.Clear();
            TransparentMesh.Clear();
            WaterMesh.Clear();
            ColliderMesh.Clear();
            WaterSourceColliderMesh.Clear();
            TriangleCoordinate.Clear();
            TriangleFace.Clear();
            WaterSourceTriangleCoordinate.Clear();
        }

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
            Vector3 first = model.VerticesLookup[model.TrianglesLookup[face, 0]] + position;
            Vector3 second = model.VerticesLookup[model.TrianglesLookup[face, 1]] + position;
            Vector3 third = model.VerticesLookup[model.TrianglesLookup[face, 2]] + position;
            Vector3 fourth = model.VerticesLookup[model.TrianglesLookup[face, 3]] + position;

            AddQuad(first, second, third, fourth, uvs[0], uvs[1], uvs[2], uvs[3], texture, targets);

            if ((targets & MeshTargets.Collider) == 0) return;

            int serialized = ((int)position.x << 16) | ((int)position.y << 8) | (int)position.z;
            TriangleCoordinate.Add(serialized);
            TriangleFace.Add(face);
        }

        public void AddWaterSourceColliderFace(int face, Vector3 position)
        {
            Vector3 first = DefaultModel.VerticesLookup[DefaultModel.TrianglesLookup[face, 0]] + position;
            Vector3 second = DefaultModel.VerticesLookup[DefaultModel.TrianglesLookup[face, 1]] + position;
            Vector3 third = DefaultModel.VerticesLookup[DefaultModel.TrianglesLookup[face, 2]] + position;
            Vector3 fourth = DefaultModel.VerticesLookup[DefaultModel.TrianglesLookup[face, 3]] + position;
            WaterSourceColliderMesh.AddQuad(first, second, third, fourth);

            int serialized = ((int)position.x << 16) | ((int)position.y << 8) | (int)position.z;
            WaterSourceTriangleCoordinate.Add(serialized);
        }

        public void AddQuad(Vector3[] vertices, Vector2[] uvs, Vector4 texture, MeshTargets targets, bool reverse = false)
        {
            AddQuad(vertices[0], vertices[1], vertices[2], vertices[3],
                uvs[0], uvs[1], uvs[2], uvs[3], texture, targets, reverse);
        }

        public void AddQuad(Vector3 first, Vector3 second, Vector3 third, Vector3 fourth,
            Vector2 firstUv, Vector2 secondUv, Vector2 thirdUv, Vector2 fourthUv,
            Vector4 texture, MeshTargets targets, bool reverse = false)
        {
            if ((targets & MeshTargets.Opaque) != 0)
                OpaqueMesh.AddQuad(first, second, third, fourth, firstUv, secondUv, thirdUv, fourthUv, texture, reverse);
            if ((targets & MeshTargets.Transparent) != 0)
                TransparentMesh.AddQuad(first, second, third, fourth, firstUv, secondUv, thirdUv, fourthUv, texture, reverse);
            if ((targets & MeshTargets.Water) != 0)
                WaterMesh.AddQuad(first, second, third, fourth, firstUv, secondUv, thirdUv, fourthUv, texture, reverse);
            if ((targets & MeshTargets.Collider) != 0)
                ColliderMesh.AddQuad(first, second, third, fourth, reverse);
        }

        private static MeshTargets DefaultTargets(Block block)
        {
            MeshTargets targets = block.Transparent ? MeshTargets.Transparent : MeshTargets.Opaque;
            if (block.Collide) targets |= MeshTargets.Collider;
            return targets;
        }
    }
}
