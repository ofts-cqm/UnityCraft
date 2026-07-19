using System.Collections.Generic;
using UnityEngine;
using World;

namespace Render
{
    public class ChunkRenderObject
    {
        private int _vertIndex;
        private readonly List<Vector3> _vertices = new();
        private readonly List<int> _triangles = new();
        private readonly List<Vector2> _uvs = new();

        public const int TopFace = 0;
        public const int BottomFace = 1;
        public const int FrontFace = 2;
        public const int BackFace = 3;
        public const int LeftFace = 4;
        public const int RightFace = 5;

        private const int TextureWidth = 16;
        private const int TextureHeight = 1;
        
        private static readonly Vector3[] VerticesLookup = {
            new(0.0f, 0.0f, 0.0f),
            new(1.0f, 0.0f, 0.0f),
            new(1.0f, 1.0f, 0.0f),
            new(0.0f, 1.0f, 0.0f),
            new(0.0f, 0.0f, 1.0f),
            new(1.0f, 0.0f, 1.0f),
            new(1.0f, 1.0f, 1.0f),
            new(0.0f, 1.0f, 1.0f)
        };

        private static readonly int[,] TrianglesLookup = {
            { 3, 7, 2, 6 }, // top
            { 1, 5, 0, 4 }, // bottom 
            { 5, 6, 4, 7 }, // front
            { 0, 3, 1, 2 }, // back
            { 4, 7, 0, 3 }, // left
            { 1, 2, 5, 6 } // right
        };
        
        private static readonly Vector2Int[] UvsLookup = {
            new(0, 0),
            new(0, 1),
            new(1, 0),
            new(1, 1)
        };

        public Mesh LoadChunk(Chunk chunk)
        {
            _vertices.Clear();
            _triangles.Clear();
            _uvs.Clear();
            
            chunk.ForEachBlock((block, position) =>
            {
                if (block.IsAir) return;
                        
                if (!chunk.GetBlock(position + Vector3Int.left).IsSolid) AddFace(LeftFace, position, block);
                if (!chunk.GetBlock(position + Vector3Int.right).IsSolid) AddFace(RightFace, position, block);
                if (!chunk.GetBlock(position + Vector3Int.up).IsSolid) AddFace(TopFace, position, block);
                if (!chunk.GetBlock(position + Vector3Int.down).IsSolid) AddFace(BottomFace, position, block);
                if (!chunk.GetBlock(position + Vector3Int.forward).IsSolid) AddFace(FrontFace, position, block);
                if (!chunk.GetBlock(position + Vector3Int.back).IsSolid) AddFace(BackFace, position, block);
            });

            return GetMesh();
        }

        private void AddFace(int face, Vector3 position, Block block)
        {
            for (int i = 0; i < 4; i++)
            {
                _vertices.Add(VerticesLookup[TrianglesLookup[face, i]] + position);

                Vector2Int textureUv = UvsLookup[i] + block.GetTextureIndex(face);
                _uvs.Add(new Vector2(textureUv.x / (float) TextureWidth, textureUv.y / (float )TextureHeight));
            }
            
            _triangles.Add(_vertIndex);
            _triangles.Add(_vertIndex + 1);
            _triangles.Add(_vertIndex + 2);
            _triangles.Add(_vertIndex + 2);
            _triangles.Add(_vertIndex + 1);
            _triangles.Add(_vertIndex + 3);
            
            _vertIndex+= 4;
        }

        public Mesh GetMesh()
        {
            Mesh mesh = new Mesh
            {
                vertices = _vertices.ToArray(),
                triangles = _triangles.ToArray(),
                uv = _uvs.ToArray()
            };
            
            mesh.RecalculateNormals();

            return mesh;
        }
    }
}
