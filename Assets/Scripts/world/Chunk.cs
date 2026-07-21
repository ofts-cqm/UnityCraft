using System;
using UnityEngine;
using Render;

namespace World
{
    public class Chunk
    {
        private readonly MeshFilter _meshFilter;
        private readonly MeshRenderer meshRenderer;
        private readonly GameObject chunkObject;
        private readonly World _world;
        private readonly ChunkCoord _chunkPosition;
        
        public const int ChunkSize = 16;
        private const int ChunkHeight = 128;
        
        private readonly ChunkRenderObject _renderObject = new();
        private readonly Block[,,] _blockData = new Block[ChunkSize, ChunkHeight, ChunkSize];

        public Chunk(ChunkCoord coord, World world)
        {
            _chunkPosition = coord;
            chunkObject = new GameObject
            {
                transform =
                {
                    position = new Vector3(coord.x * ChunkSize, 0f, coord.z * ChunkSize)
                }
            };
            
            meshRenderer = chunkObject.AddComponent<MeshRenderer>();
            _meshFilter = chunkObject.AddComponent<MeshFilter>();
            _world = world;
            chunkObject.transform.SetParent(world.transform);
            meshRenderer.material = world.material;
            GenerateChunk();
        }
        
        public bool isActive {

            get => chunkObject.activeSelf;
            set => chunkObject.SetActive(value);
        }

        public Block GetBlock(Vector3Int position) => GetBlock(position.x, position.y, position.z);
        
        public Block GetBlock(int x, int y, int z)
        {
            if (y < 0 || y >= ChunkHeight) return Blocks.Air;
            
            if (x < 0 || x >= ChunkSize || z < 0 || z >= ChunkSize) 
                return _world.GetBlock(_chunkPosition.x * ChunkSize + x, y, _chunkPosition.z * ChunkSize + z); 
            
            return _blockData[x, y, z] ?? Blocks.Air;
        }

        public void ForEachBlock(Action<Block, Vector3Int> action)
        {
            for (int i = 0; i < ChunkSize; i++)
            {
                for (int j = 0; j < ChunkHeight; j++)
                {
                    for (int k = 0; k < ChunkSize; k++)
                    {
                        action.Invoke(GetBlock(i, j, k), new Vector3Int(i, j, k));
                    }
                }
            }
        }

        private void InitializeBlockList()
        {
            for (int i = 0; i < ChunkSize; i++)
            {
                for (int j = 0; j < ChunkSize; j++)
                {
                    for (int k = 0; k < 16; k++)
                    {
                        _blockData[i, k, j] = Blocks.Stone;
                    }
                    
                    for (int k = 16; k < 20; k++)
                    {
                        _blockData[i, k, j] = Blocks.Dirt;
                    }
                    
                    _blockData[i, 20, j] = Blocks.GrassBlock;
                    
                    for (int k = 21; k < ChunkHeight; k++)
                    {
                        _blockData[i, k, j] = Blocks.Air;
                    }

                }
            }
        }

        void GenerateChunk()
        {
            InitializeBlockList();
            
            _meshFilter.mesh = _renderObject.LoadChunk(this);
        }
    }
    
    public struct ChunkCoord : IEquatable<ChunkCoord>
    {
        public int x;
        public int z;

        public ChunkCoord(int x, int z)
        {
            this.x = x;
            this.z = z;
        }

        public ChunkCoord(Vector3Int coord)
        {
            x = coord.x / Chunk.ChunkSize;
            z = coord.z / Chunk.ChunkSize;
        }
        
        public ChunkCoord(Vector3 coord)
        {
            x = (int)coord.x / Chunk.ChunkSize;
            z = (int)coord.z / Chunk.ChunkSize;
        }

        public bool Equals(ChunkCoord other)
        {
            return x == other.x && z == other.z;
        }

        public override bool Equals(object obj)
        {
            return obj is ChunkCoord other && Equals(other);
        }

        public override int GetHashCode() => x << 16 | z;
    }
}