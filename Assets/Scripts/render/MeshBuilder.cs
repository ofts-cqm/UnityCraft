using System.Collections.Generic;
using UnityEngine;
using World.blocks;

namespace render
{
    public class MeshBuilder
    {
        public int VertIndex;
        public int TransparentVertIndex;
        private int _colliderVertIndex;
        public readonly List<Vector3> Vertices = new();
        public readonly List<int> Triangles = new();
        public readonly List<Vector3> ColliderVertices = new();
        public readonly List<int> ColliderTriangles = new();
        public readonly List<Vector3> TransparentVertices = new();
        public readonly List<int> TransparentTriangles = new();
        public readonly List<int> TriangleCoordinate = new();
        public readonly List<int> TriangleFace = new();
        public readonly List<Vector2> Uvs = new();
        public readonly List<Vector4> TextureIndices = new();
        public readonly List<Vector2> TransparentUvs = new();
        public readonly List<Vector4> TransparentTextureIndices = new();

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
            AddFace(face, position, block, DefaultModel, new Vector4(block.TextureIndex(face), 1, 1, 0), block.Transparent);
        }
        
        public void AddFace(int face, Vector3 position, Block block, CubicModel model, Vector4 texture, bool transparent)
        {
            if (transparent)
            {
                for (int i = 0; i < 4; i++)
                {
                    TransparentVertices.Add(model.VerticesLookup[model.TrianglesLookup[face, i]] + position);
                    TransparentUvs.Add(model.UvsLookup[i]);
                    TransparentTextureIndices.Add(texture);
                }

                TransparentTriangles.Add(TransparentVertIndex);
                TransparentTriangles.Add(TransparentVertIndex + 1);
                TransparentTriangles.Add(TransparentVertIndex + 2);
                TransparentTriangles.Add(TransparentVertIndex + 2);
                TransparentTriangles.Add(TransparentVertIndex + 1);
                TransparentTriangles.Add(TransparentVertIndex + 3);
                TransparentVertIndex += 4;
            }
            else
            {
                for (int i = 0; i < 4; i++)
                {
                    Vertices.Add(model.VerticesLookup[model.TrianglesLookup[face, i]] + position);
                    Uvs.Add(model.UvsLookup[i]);
                    TextureIndices.Add(texture);
                }

                Triangles.Add(VertIndex);
                Triangles.Add(VertIndex + 1);
                Triangles.Add(VertIndex + 2);
                Triangles.Add(VertIndex + 2);
                Triangles.Add(VertIndex + 1);
                Triangles.Add(VertIndex + 3);
                VertIndex += 4;
            }

            if (block.Collide)
            {
                for (int i = 0; i < 4; i++)
                {
                    ColliderVertices.Add(model.VerticesLookup[model.TrianglesLookup[face, i]] + position);
                }
            
                ColliderTriangles.Add(_colliderVertIndex);
                ColliderTriangles.Add(_colliderVertIndex + 1);
                ColliderTriangles.Add(_colliderVertIndex + 2);
                ColliderTriangles.Add(_colliderVertIndex + 2);
                ColliderTriangles.Add(_colliderVertIndex + 1);
                ColliderTriangles.Add(_colliderVertIndex + 3);
                _colliderVertIndex+= 4;
                
                int serialized = ((int)position.x << 16) | ((int)position.y << 8) | ((int)position.z);
                TriangleCoordinate.Add(serialized);
                TriangleFace.Add(face);
            }
        }
    }
}