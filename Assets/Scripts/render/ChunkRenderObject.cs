using System.Collections.Generic;
using render;
using UnityEngine;
using World;
using world.blocks;
using World.blocks;

namespace Render
{
    public class ChunkRenderObject
    {
        private MeshFilter _meshFilter;
        private MeshCollider _meshCollider;
        private GameObject _chunkObject;
        
        private MeshFilter _transparentMesh;
        private GameObject _transparentObject;

        private MeshFilter _waterMesh;
        private GameObject _waterObject;

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

        public const int SideFaceBegin = FrontFace;
        public const int SideFaceEnd = RightFace;

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

            _transparentObject = new GameObject
            {
                transform =
                {
                    position = _chunkPosition,
                    parent = _chunkObject.transform  
                },
                name = "Transparent Render"
            };
            
            var meshRenderer2 = _transparentObject.AddComponent<MeshRenderer>();
            meshRenderer2.material = World.World.Instance.transparentMaterial;
            
            _transparentMesh = _transparentObject.AddComponent<MeshFilter>();

            _waterObject = new GameObject
            {
                transform =
                {
                    position = _chunkPosition,
                    parent = _chunkObject.transform
                },
                name = "Water Render"
            };

            var waterRenderer = _waterObject.AddComponent<MeshRenderer>();
            waterRenderer.material = World.World.Instance.ActiveWaterMaterial;
            _waterMesh = _waterObject.AddComponent<MeshFilter>();
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
                        BlockState block = chunk.GetBlock(position);
                        if (!block.IsAir) block.Block.Render(block, chunk, meshBuilder, position, localPosition);
                        if (!chunk.GetFluid(position).IsEmpty) Water.Render(chunk, meshBuilder, position, localPosition);
                    }
                }
            }

            bool targetState = !(meshBuilder.OpaqueMesh.IsEmpty && meshBuilder.TransparentMesh.IsEmpty && meshBuilder.WaterMesh.IsEmpty);
            if (targetState != Active) Active = targetState;

            _triangleCoordinate = meshBuilder.TriangleCoordinate;
            _triangleFace = meshBuilder.TriangleFace;

            Mesh renderMesh = new Mesh
            {
                vertices = meshBuilder.OpaqueMesh.Vertices.ToArray(),
                triangles = meshBuilder.OpaqueMesh.Triangles.ToArray(),
                uv = meshBuilder.OpaqueMesh.Uvs.ToArray()
            };
            renderMesh.SetUVs(1, meshBuilder.OpaqueMesh.TextureIndices.ToArray());
            
            renderMesh.RecalculateNormals();
            _meshFilter.mesh = renderMesh;
            
            Mesh colliderMesh = new Mesh
            {
                vertices = meshBuilder.ColliderMesh.Vertices.ToArray(),
                triangles = meshBuilder.ColliderMesh.Triangles.ToArray()
            };
            
            colliderMesh.RecalculateNormals();
            _meshCollider.sharedMesh = colliderMesh;

            Mesh transparentMesh = new Mesh
            {
                vertices = meshBuilder.TransparentMesh.Vertices.ToArray(),
                triangles = meshBuilder.TransparentMesh.Triangles.ToArray(),
                uv = meshBuilder.TransparentMesh.Uvs.ToArray()
            };
            transparentMesh.SetUVs(1, meshBuilder.TransparentMesh.TextureIndices.ToArray());
            
            transparentMesh.RecalculateNormals();
            _transparentMesh.mesh = transparentMesh;

            Mesh waterMesh = new Mesh
            {
                vertices = meshBuilder.WaterMesh.Vertices.ToArray(),
                triangles = meshBuilder.WaterMesh.Triangles.ToArray(),
                uv = meshBuilder.WaterMesh.Uvs.ToArray()
            };
            waterMesh.SetUVs(1, meshBuilder.WaterMesh.TextureIndices.ToArray());

            waterMesh.RecalculateNormals();
            _waterMesh.mesh = waterMesh;
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
