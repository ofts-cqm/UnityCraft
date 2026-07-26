using System;
using UnityEngine;
using Render;

namespace World
{
    public class Chunk
    {
        private readonly World _world;
        private readonly ChunkCoord _chunkPosition;
        
        public const int ChunkSize = 16;
        private const int ChunkHeight = 128;
        private const int ChunkSectionCount = ChunkHeight / ChunkSize;

        private readonly ChunkRenderObject[] _renderObjects = new ChunkRenderObject[8];
        private readonly Block[,,] _blockData = new Block[ChunkSize, ChunkHeight, ChunkSize];

        public Chunk(ChunkCoord coord, World world)
        {
            _chunkPosition = coord;
            
            _world = world;
            GenerateChunk();
            for (int i = 0; i < ChunkSectionCount; i++) _renderObjects[i] = new ChunkRenderObject(world, coord, i);
        }
        
        public bool Active {
            get => _renderObjects[0].Active;
            set {
                foreach (var obj in _renderObjects) obj.Active = value;            
            }
        }

        public Block GetBlock(Vector3Int position) => GetBlock(position.x, position.y, position.z);

        private Block GetBlock(int x, int y, int z)
        {
            if (y < 0 || y >= ChunkHeight) return Blocks.Air;
            
            if (x < 0 || x >= ChunkSize || z < 0 || z >= ChunkSize) 
                return _world.GetBlock(_chunkPosition.X * ChunkSize + x, y, _chunkPosition.Z * ChunkSize + z); 
            
            return _blockData[x, y, z];
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
                    
                    _blockData[i, 20, j] = ((i ^ j) & 1) == 1 ? Blocks.GrassBlock : Blocks.Stone;
                    
                    for (int k = 21; k < ChunkHeight; k++)
                    {
                        _blockData[i, k, j] = Blocks.Air;
                    }
                    _blockData[0, 21, 0] = Blocks.GrassBlock;
                    _blockData[0, 22, 0] = Blocks.GrassBlock;
                }
            }
        }

        void GenerateChunk()
        {
            InitializeBlockList();
        }

        public void UpdateDirtyRenderObjects()
        {
            for (int i = 0; i < ChunkSectionCount; i++)
            {
                if (_renderObjects[i].Dirty) UpdateChunkRender(i);
            }
        }

        private void UpdateChunkRender(int index)
        {
            RenderChunk(index);
            _world.GetChunk(new ChunkCoord(_chunkPosition.X + 1, _chunkPosition.Z))?.RenderChunk(index);
            _world.GetChunk(new ChunkCoord(_chunkPosition.X - 1, _chunkPosition.Z))?.RenderChunk(index);
            _world.GetChunk(new ChunkCoord(_chunkPosition.X, _chunkPosition.Z + 1))?.RenderChunk(index);
            _world.GetChunk(new ChunkCoord(_chunkPosition.X, _chunkPosition.Z - 1))?.RenderChunk(index);
        }

        private void RenderChunk(int index)
        {
            _renderObjects[index].RerenderChunk(this);
        }
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