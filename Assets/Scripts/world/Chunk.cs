using System;
using System.Collections.Generic;
using JetBrains.Annotations;
using UnityEngine;
using Render;
using world.blocks;
using World.blocks;
using world.generation;

namespace World
{
    public class Chunk : IBlockProvider
    {
        private readonly World _world;
        public readonly ChunkCoord ChunkPosition;
        
        public const int ChunkSize = 16;
        public const int ChunkHeight = 128;
        private const int ChunkSectionCount = ChunkHeight / ChunkSize;

        private readonly ChunkRenderObject[] _renderObjects = new ChunkRenderObject[8];
        private readonly Block[,,] _blockData;
        private readonly object[,,] _stateData;
        private readonly FluidState[,,] _fluidData;
        private readonly List<ScheduledFluidTick> _scheduledFluidTicks = new();

        private readonly struct ScheduledFluidTick
        {
            public readonly Vector3Int Position;
            public readonly byte ExpectedRawAmount;
            public readonly int DueTick;
            public ScheduledFluidTick(Vector3Int position, byte expectedRawAmount, int dueTick) { Position = position; ExpectedRawAmount = expectedRawAmount; DueTick = dueTick; }
        }

        public Chunk(ChunkCoord coord, World world)
        {
            ChunkPosition = coord;
            
            _world = world;
            _blockData = ChunkGenerator.GenerateChunk(coord);
            _stateData = new object[ChunkSize, ChunkHeight, ChunkSize];
            _fluidData = new FluidState[ChunkSize, ChunkHeight, ChunkSize];
            for (int x = 0; x < ChunkSize; x++) for (int y = 0; y < ChunkHeight; y++) for (int z = 0; z < ChunkSize; z++)
            {
                if (_blockData[x, y, z].BlockId != Blocks.GenerationWater.BlockId) continue;
                _blockData[x, y, z] = Blocks.Air;
                _fluidData[x, y, z] = FluidState.Source;
            }
            for (int i = 0; i < ChunkSectionCount; i++) _renderObjects[i] = new ChunkRenderObject(world, coord, i);
        }
        
        public bool Active {
            get => _renderObjects[0].Active;
            set {
                foreach (var obj in _renderObjects) obj.Active = value;            
            }
        }

        public BlockState GetBlock(Vector3Int position) => GetBlock(position.x, position.y, position.z);

        public BlockState GetBlock(int x, int y, int z)
        {
            if (y < 0 || y >= ChunkHeight) return Blocks.Air.AsState(x, y, z);
            
            if (x < 0 || x >= ChunkSize || z < 0 || z >= ChunkSize) 
                return _world.GetBlock(ChunkPosition.X * ChunkSize + x, y, ChunkPosition.Z * ChunkSize + z); 
            
            return _blockData[x, y, z].AsState(x, y, z, _stateData[x, y, z]);
        }

        public void SetBlock(int x, int y, int z, Block block, [CanBeNull] object state = null)
        {
            _blockData[x, y, z] = block;
            _stateData[x, y, z] = state;
            _renderObjects[y / 16].Dirty = true;
            
            if (x == 0) _world.GetChunk(ChunkPosition.Left())?.SetDirty(y);
            if (x == ChunkSize - 1) _world.GetChunk(ChunkPosition.Right())?.SetDirty(y);
            if (z == 0) _world.GetChunk(ChunkPosition.Up())?.SetDirty(y);
            if (z == ChunkSize - 1) _world.GetChunk(ChunkPosition.Down())?.SetDirty(y);
            if (y % 16 == 0 && y != 0) _renderObjects[y / 16 - 1].Dirty = true;
            if (y % 16 == 15 && y != ChunkHeight - 1) _renderObjects[y / 16 + 1].Dirty = true;
            ScheduleFluidNeighbors(new Vector3Int(x, y, z));
        }

        public void SetBlock(Vector3Int position, Block block, [CanBeNull] object state = null)
        {
            SetBlock(position.x, position.y, position.z, block, state);
        }

        private void SetDirty(int y)
        {
            _renderObjects[y / 16].Dirty = true;
        }

        public FluidState GetFluid(Vector3Int position) => GetFluid(position.x, position.y, position.z);

        public FluidState GetFluid(int x, int y, int z)
        {
            if (y < 0 || y >= ChunkHeight) return default;
            if (x < 0 || x >= ChunkSize || z < 0 || z >= ChunkSize)
                return _world.GetFluid(ChunkPosition.X * ChunkSize + x, y, ChunkPosition.Z * ChunkSize + z);
            return _fluidData[x, y, z];
        }

        public void SetFluid(Vector3Int position, FluidState state, bool schedule = true) => SetFluid(position.x, position.y, position.z, state, schedule);

        public void SetFluid(int x, int y, int z, FluidState state, bool schedule = true)
        {
            if (y < 0 || y >= ChunkHeight) return;
            if (x < 0 || x >= ChunkSize || z < 0 || z >= ChunkSize) { _world.SetFluid(ChunkPosition.X * ChunkSize + x, y, ChunkPosition.Z * ChunkSize + z, state); return; }
            if (_fluidData[x, y, z].RawAmount == state.RawAmount) return;
            _fluidData[x, y, z] = state;
            MarkFluidDirty(x, y, z);
            if (schedule && !state.IsEmpty) ScheduleFluidTick(new Vector3Int(x, y, z), Water.TickDelay);
        }

        public void ScheduleFluidTick(Vector3Int position, int delay)
        {
            if (position.x < 0 || position.x >= ChunkSize || position.z < 0 || position.z >= ChunkSize)
            {
                _world.ScheduleFluidTick(new Vector3Int(ChunkPosition.X * ChunkSize + position.x, position.y, ChunkPosition.Z * ChunkSize + position.z), delay);
                return;
            }
            FluidState state = GetFluid(position);
            if (state.IsEmpty) return;
            _scheduledFluidTicks.Add(new ScheduledFluidTick(position, state.RawAmount, _world.FluidTick + delay));
        }

        internal void TickFluid(int tick)
        {
            List<ScheduledFluidTick> due = new();
            for (int i = _scheduledFluidTicks.Count - 1; i >= 0; i--) if (_scheduledFluidTicks[i].DueTick <= tick)
            {
                due.Add(_scheduledFluidTicks[i]);
                _scheduledFluidTicks.RemoveAt(i);
            }
            foreach (ScheduledFluidTick scheduled in due)
                if (GetFluid(scheduled.Position).RawAmount == scheduled.ExpectedRawAmount && scheduled.ExpectedRawAmount != 0) Water.Tick(this, scheduled.Position);
        }

        private void ScheduleFluidNeighbors(Vector3Int position)
        {
            ScheduleFluidIfPresent(position);
            ScheduleFluidIfPresent(position + Vector3Int.left); ScheduleFluidIfPresent(position + Vector3Int.right);
            ScheduleFluidIfPresent(position + Vector3Int.up); ScheduleFluidIfPresent(position + Vector3Int.down);
            ScheduleFluidIfPresent(position + Vector3Int.forward); ScheduleFluidIfPresent(position + Vector3Int.back);
        }

        private void ScheduleFluidIfPresent(Vector3Int position)
        {
            if (position.x < 0 || position.x >= ChunkSize || position.z < 0 || position.z >= ChunkSize)
            {
                _world.ScheduleFluidTick(new Vector3Int(ChunkPosition.X * ChunkSize + position.x, position.y, ChunkPosition.Z * ChunkSize + position.z), Water.TickDelay);
                return;
            }
            if (!GetFluid(position).IsEmpty) ScheduleFluidTick(position, Water.TickDelay);
        }

        private void MarkFluidDirty(int x, int y, int z)
        {
            _renderObjects[y / ChunkSize].Dirty = true;
            if (x == 0) _world.GetChunk(ChunkPosition.Left())?.SetDirty(y);
            if (x == ChunkSize - 1) _world.GetChunk(ChunkPosition.Right())?.SetDirty(y);
            if (z == 0) _world.GetChunk(ChunkPosition.Up())?.SetDirty(y);
            if (z == ChunkSize - 1) _world.GetChunk(ChunkPosition.Down())?.SetDirty(y);
            if (y % ChunkSize == 0 && y != 0) _renderObjects[y / ChunkSize - 1].Dirty = true;
            if (y % ChunkSize == ChunkSize - 1 && y != ChunkHeight - 1) _renderObjects[y / ChunkSize + 1].Dirty = true;
        }

        public void FinalizeLoading()
        {
            foreach (var obj in _renderObjects) obj.FinalizeGeneration();
        }

        internal void ScheduleBorderFluidTicks()
        {
            for (int y = 0; y < ChunkHeight; y++) for (int i = 0; i < ChunkSize; i++)
            {
                ScheduleFluidIfPresent(new Vector3Int(0, y, i)); ScheduleFluidIfPresent(new Vector3Int(ChunkSize - 1, y, i));
                ScheduleFluidIfPresent(new Vector3Int(i, y, 0)); ScheduleFluidIfPresent(new Vector3Int(i, y, ChunkSize - 1));
            }
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
