using System;
using UnityEngine;
using Render;

namespace World
{
    public class Chunk
    {
        private readonly World _world;
        public readonly ChunkCoord ChunkPosition;
        
        public const int ChunkSize = 16;
        private const int ChunkHeight = 128;
        private const int ChunkSectionCount = ChunkHeight / ChunkSize;

        private readonly ChunkRenderObject[] _renderObjects = new ChunkRenderObject[8];
        private readonly Block[,,] _blockData = new Block[ChunkSize, ChunkHeight, ChunkSize];

        public Chunk(ChunkCoord coord, World world)
        {
            ChunkPosition = coord;
            
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
                return _world.GetBlock(ChunkPosition.X * ChunkSize + x, y, ChunkPosition.Z * ChunkSize + z); 
            
            return _blockData[x, y, z];
        }

        public void SetBlock(int x, int y, int z, Block block)
        {
            _blockData[x, y, z] = block;
            _renderObjects[y / 16].Dirty = true;
            
            if (x == 0) _world.GetChunk(ChunkPosition.Left())?.SetDirty(y);
            if (x == ChunkSize - 1) _world.GetChunk(ChunkPosition.Right())?.SetDirty(y);
            if (z == 0) _world.GetChunk(ChunkPosition.Up())?.SetDirty(y);
            if (z == ChunkSize - 1) _world.GetChunk(ChunkPosition.Down())?.SetDirty(y);
        }

        public void SetBlock(Vector3Int position, Block block)
        {
            SetBlock(position.x, position.y, position.z, block);
        }

        private void SetDirty(int y)
        {
            _renderObjects[y / 16].Dirty = true;
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
            foreach (ChunkRenderObject obj in _renderObjects) if (obj.Dirty) obj.RerenderChunk(this);
        }

        public void MarkDirty()
        {
            foreach (ChunkRenderObject obj in _renderObjects) obj.Dirty = true;
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
            return new ChunkCoord(
                (x > 0 ? x : x - 15) / Chunk.ChunkSize,
                (z > 0 ? z : z - 15) / Chunk.ChunkSize
                );
        }

        public bool Equals(ChunkCoord other)
        {
            return X == other.X && Z == other.Z;
        }

        public override bool Equals(object obj)
        {
            return obj is ChunkCoord other && X == other.X && Z == other.Z;
        }
        
        public ChunkCoord Left() => new(X - 1, Z);
        public ChunkCoord Right() => new(X + 1, Z);
        public ChunkCoord Up() => new(X, Z - 1);
        public ChunkCoord Down() => new(X, Z + 1);

        public override int GetHashCode() => X << 16 | Z;
    }
}