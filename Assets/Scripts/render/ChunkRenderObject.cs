using System.Collections.Generic;
using render;
using UnityEngine;
using World;

namespace Render
{
    public class ChunkRenderObject
    {
        private MeshFilter _meshFilter;
        private MeshCollider _meshCollider;
        private GameObject _chunkObject;

        private readonly int _heightIndex;
        private int _vertIndex;
        private int _colliderVertIndex;
        private readonly List<Vector3> _vertices = new();
        private readonly List<int> _triangles = new();
        private readonly List<Vector3> _colliderVertices = new();
        private readonly List<int> _colliderTriangles = new();
        private readonly List<int> _triangleCoordinate = new();
        private readonly List<int> _triangleFace = new();
        private readonly List<Vector2> _uvs = new();
        private readonly Vector3Int _chunkPosition;

        public const int TopFace = 0;
        public const int BottomFace = 1;
        public const int FrontFace = 2;
        public const int BackFace = 3;
        public const int LeftFace = 4;
        public const int RightFace = 5;

        private const int TextureWidth = 16;
        private const int TextureHeight = 1;

        private const int ChunkSize = Chunk.ChunkSize;
        
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

        public ChunkRenderObject(World.World world, ChunkCoord coord, int index)
        {
            _heightIndex = index * 16;
            _chunkPosition = new Vector3Int(coord.X * ChunkSize, index * 16, coord.Z * ChunkSize);
        }

        public void FinalizeGeneration()
        {
            _chunkObject = new GameObject
            {
                transform =
                {
                    position = _chunkPosition
                },
                name = $"Chunk @{_chunkPosition.x / ChunkSize},{_chunkPosition.z / ChunkSize} height {_heightIndex / ChunkSize}"
            };
            
            var meshRenderer1 = _chunkObject.AddComponent<MeshRenderer>();
            meshRenderer1.material = World.World.Instance.material;
            
            _meshFilter = _chunkObject.AddComponent<MeshFilter>();
            _meshCollider = _chunkObject.AddComponent<MeshCollider>();
            _chunkObject.AddComponent<RenderObjectProperty>().RenderObject = this;
            _chunkObject.transform.SetParent(World.World.Instance.transform);
        }
        
        public bool Active {
            get => _chunkObject.activeSelf;
            set
            {
                _chunkObject.SetActive(value);
                if (value)
                {
                    Dirty = true;
                }
            }
        }

        public void DestroyObject()
        {
            Object.Destroy(_chunkObject);
        }

        public bool Dirty { get; set; } = true;

        private static bool ShouldRender(Block thisBlock, Block adjacentBlock)
        {
            if (adjacentBlock.IsSolid) return false;
            if (adjacentBlock.IsTransparent) return adjacentBlock.BlockId != thisBlock.BlockId;
            return true;
        }

        public void RerenderChunk(Chunk chunk)
        {
            Dirty = false;
            _vertices.Clear();
            _triangles.Clear();
            _colliderVertices.Clear();
            _colliderTriangles.Clear();
            _triangleCoordinate.Clear();
            _triangleFace.Clear();
            _uvs.Clear();
            _vertIndex = 0;
            _colliderVertIndex = 0;
            
            for (int i = 0; i < ChunkSize; i++)
            {
                for (int j = 0; j < ChunkSize; j++)
                {
                    for (int k = 0; k < ChunkSize; k++)
                    {
                        Vector3Int position = new Vector3Int(i, j + _heightIndex, k);
                        Vector3 localPosition = new Vector3(i, j, k);
                        Block block = chunk.GetBlock(position);
                        if (block.IsAir) continue;
                        
                        if (ShouldRender(block, chunk.GetBlock(position + Vector3Int.left))) AddFace(LeftFace, localPosition, block);
                        if (ShouldRender(block, chunk.GetBlock(position + Vector3Int.right))) AddFace(RightFace, localPosition, block);
                        if (ShouldRender(block, chunk.GetBlock(position + Vector3Int.up))) AddFace(TopFace, localPosition, block);
                        if (ShouldRender(block, chunk.GetBlock(position + Vector3Int.down))) AddFace(BottomFace, localPosition, block);
                        if (ShouldRender(block, chunk.GetBlock(position + Vector3Int.forward))) AddFace(FrontFace, localPosition, block);
                        if (ShouldRender(block, chunk.GetBlock(position + Vector3Int.back))) AddFace(BackFace, localPosition, block);
                    }
                }
            }

            //if (_vertIndex == 0) Active = false;
            if (_vertIndex == 0 && Active) Active = false;
            else if (_vertIndex != 0 && !Active) Active = true;

            _meshFilter.mesh = GetMesh();
            // Todo: fix water collider
            _meshCollider.sharedMesh = GetCollideMesh();
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

            if (block.Collide)
            {
                for (int i = 0; i < 4; i++)
                {
                    _colliderVertices.Add(VerticesLookup[TrianglesLookup[face, i]] + position);
                }
            
                _colliderTriangles.Add(_colliderVertIndex);
                _colliderTriangles.Add(_colliderVertIndex + 1);
                _colliderTriangles.Add(_colliderVertIndex + 2);
                _colliderTriangles.Add(_colliderVertIndex + 2);
                _colliderTriangles.Add(_colliderVertIndex + 1);
                _colliderTriangles.Add(_colliderVertIndex + 3);
                _colliderVertIndex+= 4;
                
                int serialized = ((int)position.x << 16) | ((int)position.y << 8) | ((int)position.z);
                _triangleCoordinate.Add(serialized);
                _triangleFace.Add(face);
            }
        }

        public Vector3Int GetBlockPositionOfTriangle(int index)
        {
            int serialized = _triangleCoordinate[index / 2];
            Vector3Int des = new Vector3Int((serialized >> 16) & 0xFF, (serialized >> 8) & 0xFF, serialized & 0xFF) + _chunkPosition;
            return des;
        }

        public int GetTriangleFacing(int index)
        {
            return _triangleFace[index / 2];
        }

        private Mesh GetMesh()
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

        private Mesh GetCollideMesh()
        {
            Mesh mesh = new Mesh
            {
                vertices = _colliderVertices.ToArray(),
                triangles = _colliderTriangles.ToArray()
            };
            
            mesh.RecalculateNormals();

            return mesh;
        }
    }
}
