using System;
using System.Collections.Generic;
using System.Linq;
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
            Water = 1 << 3,
            Selection = 1 << 4
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

            public void AddQuad(Vector3 first, Vector3 second, Vector3 third, Vector3 fourth,
                bool reverse = false, bool doubleSide = false)
            {
                int vertexIndex = VertexCount;
                Vertices.Add(first);
                Vertices.Add(second);
                Vertices.Add(third);
                Vertices.Add(fourth);

                if (reverse || doubleSide)
                {
                    Triangles.Add(vertexIndex + 2);
                    Triangles.Add(vertexIndex + 1);
                    Triangles.Add(vertexIndex);
                    Triangles.Add(vertexIndex + 3);
                    Triangles.Add(vertexIndex + 1);
                    Triangles.Add(vertexIndex + 2);
                }
                
                if (!reverse || doubleSide)
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
            private struct GeometryVertex
            {
                public Vector3 Position, Normal;
                public Vector2 Uv;
                public Vector4 Texture;
            }
            private readonly List<GeometryVertex> _geometry = new();
            private static readonly VertexAttributeDescriptor[] Layout =
            {
                new(VertexAttribute.Position, VertexAttributeFormat.Float32, 3, 0),
                new(VertexAttribute.Normal, VertexAttributeFormat.Float32, 3, 0),
                new(VertexAttribute.TexCoord0, VertexAttributeFormat.Float32, 2, 0),
                new(VertexAttribute.TexCoord1, VertexAttributeFormat.Float32, 4, 0),
                new(VertexAttribute.Color, VertexAttributeFormat.UNorm8, 4, 1)
            };
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
                Vector4 texture, bool reverse = false, bool doubleSide = false)
            {
                base.AddQuad(first, second, third, fourth, reverse, doubleSide);
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
                // Geometry and lighting have independent GPU streams. A light upload is four bytes
                // per vertex and cannot rebuild geometry, bounds, or the physics mesh.
                _geometry.Clear();
                Vector3 minimum = Vertices[0], maximum = Vertices[0];
                for (int i = 0; i < Vertices.Count; i++)
                {
                    _geometry.Add(new GeometryVertex { Position = Vertices[i], Normal = Normals[i], Uv = Uvs[i], Texture = TextureIndices[i] });
                    minimum = Vector3.Min(minimum, Vertices[i]);
                    maximum = Vector3.Max(maximum, Vertices[i]);
                }
                mesh.SetVertexBufferParams(Vertices.Count, Layout);
                mesh.SetVertexBufferData(_geometry, 0, 0, _geometry.Count, 0);
                mesh.SetTriangles(Triangles, 0, true);
                // SetTriangles does not populate bounds for this explicit multi-stream layout.
                // Keep culling correct, including water's existing shader displacement.
                mesh.bounds = new Bounds((minimum + maximum) * .5f,
                    maximum - minimum + (useProvidedFlatNormals ? Vector3.zero : Vector3.up * .5f));
                // Fluid surface quads can be non-planar. Preserve RecalculateNormals' two-triangle
                // smoothing for those meshes while block faces use the cheaper supplied flat normals.
                if (!useProvidedFlatNormals) mesh.RecalculateNormals();
            }
        }

        public readonly TexturedMeshHolder OpaqueMesh = new();
        public readonly TexturedMeshHolder TransparentMesh = new();
        public readonly TexturedMeshHolder WaterMesh = new();
        public readonly MeshHolder ColliderMesh = new();
        public readonly MeshHolder SelectionMesh = new();
        public readonly MeshHolder WaterSourceColliderMesh = new();
        public readonly List<int> TriangleCoordinate = new();
        public readonly List<int> TriangleFace = new();
        public readonly List<int> SelectionTriangleCoordinate = new();
        public readonly List<int> SelectionTriangleFace = new();
        public readonly List<int> WaterSourceTriangleCoordinate = new();

        public void Clear()
        {
            OpaqueMesh.Clear();
            TransparentMesh.Clear();
            WaterMesh.Clear();
            ColliderMesh.Clear();
            SelectionMesh.Clear();
            WaterSourceColliderMesh.Clear();
            TriangleCoordinate.Clear();
            TriangleFace.Clear();
            SelectionTriangleCoordinate.Clear();
            SelectionTriangleFace.Clear();
            WaterSourceTriangleCoordinate.Clear();
        }

        public record CubicModel(Vector3[] VerticesLookup, int[,] TrianglesLookup, Vector2[] UvsLookup)
        {
            public CubicModel Rotate(Matrix4x4 matrix)
            {
                return this with { VerticesLookup = VerticesLookup.Select(matrix.MultiplyPoint3x4).ToArray() };
            }
        }

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

        public void AddFace(int face, Vector3 position, Block block, CubicModel model, bool doubleSided = false)
        {
            AddFace(face, position, block, model, new Vector4(block.TextureIndex(face), 1, 1, 0), DefaultTargets(block), doubleSided);
        }

        public void AddFace(int face, Vector3 position, Block block, MeshTargets targets)
        {
            AddFace(face, position, block, DefaultModel, new Vector4(block.TextureIndex(face), 1, 1, 0), targets);
        }

        public void AddFace(int face, Vector3 position, Block block, CubicModel model, MeshTargets targets)
        {
            AddFace(face, position, block, model, new Vector4(block.TextureIndex(face), 1, 1, 0), targets);
        }

        public void AddFace(int face, Vector3 position, Block block, CubicModel model, Vector4 texture, MeshTargets targets, bool doubleSide = false)
        {
            AddFace(face, position, block, model, model.UvsLookup, texture, targets, doubleSide);
        }

        public void AddFace(int face, Vector3 position, Block block, CubicModel model, Vector2[] uvs, Vector4 texture,
            MeshTargets targets, bool doubleSide = false)
        {
            // Every rendered non-colliding block remains targetable. The chunk renderer never calls
            // AddFace for air, and water uses its separate fluid mesh rather than this block path.
            if (!block.Collide)
            {
                targets &= ~MeshTargets.Collider;
                if ((targets & (MeshTargets.Opaque | MeshTargets.Transparent)) != 0)
                    targets |= MeshTargets.Selection;
            }

            Vector3 first = model.VerticesLookup[model.TrianglesLookup[face, 0]] + position;
            Vector3 second = model.VerticesLookup[model.TrianglesLookup[face, 1]] + position;
            Vector3 third = model.VerticesLookup[model.TrianglesLookup[face, 2]] + position;
            Vector3 fourth = model.VerticesLookup[model.TrianglesLookup[face, 3]] + position;

            AddQuad(first, second, third, fourth, uvs[0], uvs[1], uvs[2], uvs[3], texture, targets, doubleSide:doubleSide);
            
            if ((targets & (MeshTargets.Collider | MeshTargets.Selection)) == 0) return;

            int serialized = ((int)position.x << 16) | ((int)position.y << 8) | (int)position.z;
            if ((targets & MeshTargets.Collider) != 0)
            {
                TriangleCoordinate.Add(serialized);
                TriangleFace.Add(face);
            }
            if ((targets & MeshTargets.Selection) != 0)
            {
                SelectionTriangleCoordinate.Add(serialized);
                SelectionTriangleFace.Add(face);
            }
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
            Vector4 texture, MeshTargets targets, bool reverse = false, bool doubleSide = false)
        {
            if ((targets & MeshTargets.Opaque) != 0)
                OpaqueMesh.AddQuad(first, second, third, fourth, firstUv, secondUv, thirdUv, fourthUv, texture, reverse, doubleSide);
            if ((targets & MeshTargets.Transparent) != 0)
                TransparentMesh.AddQuad(first, second, third, fourth, firstUv, secondUv, thirdUv, fourthUv, texture, reverse, doubleSide);
            if ((targets & MeshTargets.Water) != 0)
                WaterMesh.AddQuad(first, second, third, fourth, firstUv, secondUv, thirdUv, fourthUv, texture, reverse);
            if ((targets & MeshTargets.Collider) != 0)
                ColliderMesh.AddQuad(first, second, third, fourth, reverse);
            if ((targets & MeshTargets.Selection) != 0)
                SelectionMesh.AddQuad(first, second, third, fourth, reverse);
        }

        private static MeshTargets DefaultTargets(Block block)
        {
            MeshTargets targets = block.Transparent ? MeshTargets.Transparent : MeshTargets.Opaque;
            if (block.Collide) targets |= MeshTargets.Collider;
            return targets;
        }
    }
}
