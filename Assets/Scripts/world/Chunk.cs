using System;
using UnityEngine;
using Render;
using world.blocks;
using World.blocks;

namespace World
{
    public class Chunk
    {
        private readonly World _world;
        public readonly ChunkCoord ChunkPosition;
        
        public const int ChunkSize = 16;
        public const int ChunkHeight = 128;
        private const int ChunkSectionCount = ChunkHeight / ChunkSize;

        private readonly ChunkRenderObject[] _renderObjects = new ChunkRenderObject[8];
        private readonly Block[,,] _blockData;

        public Chunk(ChunkCoord coord, World world)
        {
            ChunkPosition = coord;
            
            _world = world;
            _blockData = ChunkGenerator.GenerateChunk(coord);
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
            if (y % 16 == 0 && y != 0) _renderObjects[y / 16 - 1].Dirty = true;
            if (y % 16 == 15 && y != ChunkHeight - 1) _renderObjects[y / 16 + 1].Dirty = true;
        }

        public void SetBlock(Vector3Int position, Block block)
        {
            SetBlock(position.x, position.y, position.z, block);
        }

        private void SetDirty(int y)
        {
            _renderObjects[y / 16].Dirty = true;
        }

        public void FinalizeLoading()
        {
            foreach (var obj in _renderObjects) obj.FinalizeGeneration();
        }
        
        public void DestroyChunk()
        {
            foreach (ChunkRenderObject obj in _renderObjects)
            {
                obj.DestroyObject();
            }
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
            X = (int)(coord.x > 0 ? coord.x : coord.x - 15) / Chunk.ChunkSize;
            Z = (int)(coord.z > 0 ? coord.z : coord.z - 15) / Chunk.ChunkSize;
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