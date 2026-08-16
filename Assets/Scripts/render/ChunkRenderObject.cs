using System.Collections.Generic;
using render;
using UnityEngine;
using World;
using World.blocks;

namespace Render
{
    public class ChunkRenderObject
    {
        private MeshFilter _meshFilter;
        private MeshCollider _meshCollider;
        private GameObject _chunkObject;

        private readonly int _heightIndex;
        private readonly Vector3Int _chunkPosition;
        
        private List<int> _triangleCoordinate = new();
        private List<int> _triangleFace = new();

        public const int TopFace = 0;
        public const int BottomFace = 1;
        public const int FrontFace = 2;
        public const int BackFace = 3;
        public const int LeftFace = 4;
        public const int RightFace = 5;

        private const int ChunkSize = Chunk.ChunkSize;

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

        public void RerenderChunk(Chunk chunk)
        {
            Dirty = false;
            MeshBuilder meshBuilder = new MeshBuilder();
            
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
                        
                        block.Render(chunk, meshBuilder, position, localPosition);
                    }
                }
            }

            //if (_vertIndex == 0) Active = false;
            if (meshBuilder.VertIndex == 0 && Active) Active = false;
            else if (meshBuilder.VertIndex != 0 && !Active) Active = true;

            _triangleCoordinate = meshBuilder.TriangleCoordinate;
            _triangleFace = meshBuilder.TriangleFace;

            Mesh renderMesh = new Mesh
            {
                vertices = meshBuilder.Vertices.ToArray(),
                triangles = meshBuilder.Triangles.ToArray(),
                uv = meshBuilder.Uvs.ToArray(),
                uv2 = meshBuilder.TextureIndices.ToArray()
            };
            
            renderMesh.RecalculateNormals();
            _meshFilter.mesh = renderMesh;
            
            Mesh colliderMesh = new Mesh
            {
                vertices = meshBuilder.ColliderVertices.ToArray(),
                triangles = meshBuilder.ColliderTriangles.ToArray()
            };
            
            colliderMesh.RecalculateNormals();
            _meshCollider.sharedMesh = colliderMesh;
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
    }
}
