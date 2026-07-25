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
                    position = new Vector3(coord.X * ChunkSize, 0f, coord.Z * ChunkSize)
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
                return _world.GetBlock(_chunkPosition.X * ChunkSize + x, y, _chunkPosition.Z * ChunkSize + z); 
            
            return _blockData[x, y, z];
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
        }

        public void UpdateChunkRender()
        {
            RenderChunk();
            _world.GetChunk(new ChunkCoord(_chunkPosition.X + 1, _chunkPosition.Z))?.RenderChunk();
            _world.GetChunk(new ChunkCoord(_chunkPosition.X - 1, _chunkPosition.Z))?.RenderChunk();
            _world.GetChunk(new ChunkCoord(_chunkPosition.X, _chunkPosition.Z + 1))?.RenderChunk();
            _world.GetChunk(new ChunkCoord(_chunkPosition.X, _chunkPosition.Z - 1))?.RenderChunk();
        }
        
        private void RenderChunk() => _meshFilter.mesh = _renderObject.LoadChunk(this);
    }
    
    public readonly struct ChunkCoord : IEquatable<ChunkCoord>
    {
        public readonly int X;
        public readonly int Z;

        public ChunkCoord(int x, int z)
        {
            X = x;
            Z = z;
        }

        public ChunkCoord(Vector3Int coord)
        {
            X = coord.x / Chunk.ChunkSize;
            if (coord.x < 0) X--;
            Z = coord.z / Chunk.ChunkSize;
            if (coord.z < 0) Z--;
        }
        
        public ChunkCoord(Vector3 coord)
        {
            X = (int)coord.x / Chunk.ChunkSize;
            if (coord.x < 0) X--;
            Z = (int)coord.z / Chunk.ChunkSize;
            if (coord.z < 0) Z--;
        }

        public static ChunkCoord ToChunkCoord(int x, int z)
        {
            int X = x / Chunk.ChunkSize;
            if (x < 0) X--;
            int Z = z / Chunk.ChunkSize;
            if (z < 0) Z--;
            return new ChunkCoord(X, Z);
        }

        public bool Equals(ChunkCoord other)
        {
            return X == other.X && Z == other.Z;
        }

        public override bool Equals(object obj)
        {
            return obj is ChunkCoord other && X == other.X && Z == other.Z;
        }

        public override int GetHashCode() => X << 16 | Z;
    }
}