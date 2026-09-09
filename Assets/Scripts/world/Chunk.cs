using System;
using System.IO;
using System.Threading;
using JetBrains.Annotations;
using UnityEngine;
using Render;
using world.blocks;
using World.blocks;
using world.generation;
using world.persistence;

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
        private readonly ChunkData _data;
        private static int _nextFluidGeneration;
        internal readonly int FluidGeneration;
        private long _persistenceRevision;
        private long _lastSavedRevision;
        private long _lastQueuedRevision;
        private volatile bool _active;
        private readonly bool _loadedFromSnapshot;

        public Chunk(ChunkCoord coord, World world)
            : this(coord, world, LoadOrGenerate(coord, world))
        {
        }

        internal Chunk(ChunkCoord coord, World world, ChunkSnapshot snapshot)
        {
            ChunkPosition = coord;
            _world = world;
            _loadedFromSnapshot = snapshot != null;
            FluidGeneration = Interlocked.Increment(ref _nextFluidGeneration);

            if (snapshot == null) _data = ChunkGenerator.GenerateChunk(coord);
            else
            {
                _data = new ChunkData();
                try { Hydrate(snapshot); }
                catch (CorruptSaveException) { throw; }
                catch (Exception exception)
                {
                    throw new CorruptSaveException($"Chunk ({coord.X}, {coord.Z}) contains invalid data.", exception);
                }
            }

            for (int i = 0; i < ChunkSectionCount; i++) _renderObjects[i] = new ChunkRenderObject(this, coord, i);
        }

        private static ChunkSnapshot LoadOrGenerate(ChunkCoord coord, World world)
        {
            return world.Persistence != null && world.Persistence.TryLoadChunk(coord, out ChunkSnapshot snapshot)
                ? snapshot
                : null;
        }

        private void Hydrate(ChunkSnapshot snapshot)
        {
            if (!snapshot.Coord.Equals(ChunkPosition)) throw new InvalidDataException("Chunk snapshot coordinates do not match the requested chunk.");
            bool replacedUnknownContent = false;
            for (int x = 0; x < ChunkSize; x++) for (int y = 0; y < ChunkHeight; y++) for (int z = 0; z < ChunkSize; z++)
            {
                int index = ChunkSnapshot.Index(x, y, z);
                if (!Blocks.TryGetById(snapshot.BlockIds[index], out Block block))
                {
                    // A player may explicitly admit the risk of loading newer content.
                    // Unknown blocks are intentionally replaced with Air.
                    block = Blocks.Air;
                    _data.SetBlock(x, y, z, block, 0);
                    replacedUnknownContent = true;
                }
                else
                {
                    if (block.BlockId == Blocks.GenerationWater.BlockId)
                        throw new InvalidDataException("A save contains the generation-only water marker.");
                    if ((uint)snapshot.StateIds[index] > ushort.MaxValue)
                        throw new InvalidDataException($"State ID {snapshot.StateIds[index]} is outside the compact state range.");
                    // Decode once while hydrating to retain the save validation contract. Runtime
                    // reads use the per-block decoded-state cache rather than storing one object per cell.
                    block.DecodeStateCached((ushort)snapshot.StateIds[index]);
                    _data.SetBlock(x, y, z, block, snapshot.StateIds[index]);
                }
                _data.SetFluidRaw(x, y, z, snapshot.FluidAmounts[index]);
            }
            // Once the player has accepted the load, the next save makes Air replacement explicit.
            if (replacedUnknownContent) _persistenceRevision = 1;
        }

        private void ScheduleRestoredFluidTicks()
        {
            for (int section = 0; section < ChunkSectionCount; section++)
            {
                if (_data.FlowingFluidCount(section) == 0) continue;
                int firstY = section * ChunkSize;
                for (int y = firstY; y < firstY + ChunkSize; y++)
                for (int z = 0; z < ChunkSize; z++)
                for (int x = 0; x < ChunkSize; x++)
                    if (_data.GetFluidRawUnchecked(x, y, z) is not 0 and not 8)
                        ScheduleFluidTick(new Vector3Int(x, y, z), Water.TickDelay);
            }
        }

        public ChunkData Data => _data;
        
        public bool Active {
            get => _active;
            set {
                if (_active == value) return;
                _active = value;
                foreach (var obj in _renderObjects) obj.Active = value;            
                if (value) _world?.ResumeFluidTicks(this);
                else _world?.SuspendFluidTicks(this);
            }
        }

        internal bool IsActive => _active;
        internal bool IsRenderReady
        {
            get
            {
                foreach (ChunkRenderObject obj in _renderObjects)
                    if (obj.Dirty) return false;
                return true;
            }
        }

        public BlockState GetBlock(Vector3Int position) => GetBlock(position.x, position.y, position.z);

        public bool ShouldRenderFace(Vector3Int neighborPosition, int neighborFace, int renderedBlockId)
        {
            int x = neighborPosition.x;
            int y = neighborPosition.y;
            int z = neighborPosition.z;
            if (y < 0 || y >= ChunkHeight) return true;
            if (x < 0 || x >= ChunkSize || z < 0 || z >= ChunkSize)
            {
                BlockState neighbor = _world.GetBlock(
                    ChunkPosition.X * ChunkSize + x, y, ChunkPosition.Z * ChunkSize + z);
                return neighbor.Block.Transparent
                    ? neighbor.Block.BlockId != renderedBlockId
                    : !neighbor.Block.IsSolid(neighbor, neighborFace);
            }

            _data.GetCellIdsUnchecked(x, y, z, out ushort blockId, out ushort stateId);
            Block block = Blocks.GetByCompactId(blockId);
            return block.Transparent ? block.BlockId != renderedBlockId : !block.IsSolidCompact(stateId, neighborFace);
        }

        public BlockState GetBlock(int x, int y, int z)
        {
            if (y < 0 || y >= ChunkHeight) return Blocks.Air.AsState(x, y, z);
            
            if (x < 0 || x >= ChunkSize || z < 0 || z >= ChunkSize) 
                return _world.GetBlock(ChunkPosition.X * ChunkSize + x, y, ChunkPosition.Z * ChunkSize + z); 
            
            _data.GetCellIdsUnchecked(x, y, z, out ushort blockId, out ushort stateId);
            Block block = Blocks.GetByCompactId(blockId);
            return block.AsState(x, y, z, block.DecodeStateCached(stateId));
        }

        internal void GetCellUnchecked(int x, int y, int z, out Block block, out ushort stateId)
        {
            _data.GetCellIdsUnchecked(x, y, z, out ushort blockId, out stateId);
            block = Blocks.GetByCompactId(blockId);
        }

        public void SetBlock(int x, int y, int z, Block block, [CanBeNull] object state = null)
        {
            if ((uint)x >= ChunkSize || (uint)y >= ChunkHeight || (uint)z >= ChunkSize)
                throw new ArgumentOutOfRangeException($"Chunk-local cell ({x}, {y}, {z}) is outside the chunk.");
            _data.GetCellIdsUnchecked(x, y, z, out ushort existingBlockId, out ushort existingStateId);
            Block existingBlock = Blocks.GetByCompactId(existingBlockId);
            object existingState = existingBlock.DecodeStateCached(existingStateId);
            object nextState = state ?? block.DefaultState;
            ushort nextStateId = block.EncodeStateCompact(nextState);
            if (existingBlock.BlockId == block.BlockId &&
                existingStateId == nextStateId)
            {
                // A repeated assignment is normally a true no-op. A malformed/restored cell can still
                // contain fluid inside an incompatible block; repairing that invariant is a real change.
                FluidState unchangedBlockFluid = FluidState.FromRaw(_data.GetFluidRawUnchecked(x, y, z));
                BlockState unchangedBlock = existingBlock.AsState(x, y, z, existingState);
                if (unchangedBlockFluid.IsEmpty || CanContainFluid(unchangedBlock)) return;
                _data.SetFluidRaw(x, y, z, 0);
                _persistenceRevision++;
                MarkFluidDirty(x, y, z);
                ScheduleFluidNeighbors(new Vector3Int(x, y, z));
                return;
            }

            FluidState existingFluid = FluidState.FromRaw(_data.GetFluidRawUnchecked(x, y, z));
            _data.SetBlock(x, y, z, block, nextStateId);
            _persistenceRevision++;
            _renderObjects[y / ChunkSize].MarkDirty(ChunkRenderDirtyFlags.All);
            
            if (x == 0) _world.GetChunk(ChunkPosition.Left())?.SetDirty(y, ChunkRenderDirtyFlags.All);
            if (x == ChunkSize - 1) _world.GetChunk(ChunkPosition.Right())?.SetDirty(y, ChunkRenderDirtyFlags.All);
            if (z == 0) _world.GetChunk(ChunkPosition.Up())?.SetDirty(y, ChunkRenderDirtyFlags.All);
            if (z == ChunkSize - 1) _world.GetChunk(ChunkPosition.Down())?.SetDirty(y, ChunkRenderDirtyFlags.All);
            if (y % ChunkSize == 0 && y != 0) _renderObjects[y / ChunkSize - 1].MarkDirty(ChunkRenderDirtyFlags.All);
            if (y % ChunkSize == ChunkSize - 1 && y != ChunkHeight - 1) _renderObjects[y / ChunkSize + 1].MarkDirty(ChunkRenderDirtyFlags.All);
            if (!existingFluid.IsEmpty && !CanContainFluid(GetBlock(x, y, z)))
            {
                _data.SetFluidRaw(x, y, z, 0);
                MarkFluidDirty(x, y, z);
            }
            ScheduleFluidNeighbors(new Vector3Int(x, y, z));
        }

        public void SetBlock(Vector3Int position, Block block, [CanBeNull] object state = null)
        {
            SetBlock(position.x, position.y, position.z, block, state);
        }

        private void SetDirty(int y, ChunkRenderDirtyFlags flags)
        {
            _renderObjects[y / ChunkSize].MarkDirty(flags);
        }

        public FluidState GetFluid(Vector3Int position) => GetFluid(position.x, position.y, position.z);

        public FluidState GetFluid(int x, int y, int z)
        {
            if (y < 0 || y >= ChunkHeight) return default;
            if (x < 0 || x >= ChunkSize || z < 0 || z >= ChunkSize)
                return _world.GetFluid(ChunkPosition.X * ChunkSize + x, y, ChunkPosition.Z * ChunkSize + z);
            return FluidState.FromRaw(_data.GetFluidRawUnchecked(x, y, z));
        }

        public void SetFluid(Vector3Int position, FluidState state, bool schedule = true) => SetFluid(position.x, position.y, position.z, state, schedule);

        public void SetFluid(int x, int y, int z, FluidState state, bool schedule = true)
        {
            if (y < 0 || y >= ChunkHeight) return;
            if (x < 0 || x >= ChunkSize || z < 0 || z >= ChunkSize) { _world.SetFluid(ChunkPosition.X * ChunkSize + x, y, ChunkPosition.Z * ChunkSize + z, state); return; }
            if (_data.GetFluidRawUnchecked(x, y, z) == state.RawAmount) return;
            _data.SetFluidRaw(x, y, z, state.RawAmount);
            _persistenceRevision++;
            MarkFluidDirty(x, y, z);
            if (schedule)
            {
                if (!state.IsEmpty) ScheduleFluidTick(new Vector3Int(x, y, z), Water.TickDelay);
                ScheduleFluidNeighbors(new Vector3Int(x, y, z));
            }
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
            _world?.ScheduleFluidTick(this, position, state.RawAmount, delay);
        }

        internal void ScheduleFluidNeighbors(Vector3Int position)
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

        private static bool CanContainFluid(BlockState state)
        {
            for (int face = ChunkRenderObject.TopFace; face <= ChunkRenderObject.RightFace; face++)
                if (state.Block.GetFlowingAmountLimit(state, face).max > 0) return true;
            return false;
        }

        private void MarkFluidDirty(int x, int y, int z)
        {
            _renderObjects[y / ChunkSize].MarkDirty(ChunkRenderDirtyFlags.Water);
            if (x == 0) _world.GetChunk(ChunkPosition.Left())?.SetDirty(y, ChunkRenderDirtyFlags.Water);
            if (x == ChunkSize - 1) _world.GetChunk(ChunkPosition.Right())?.SetDirty(y, ChunkRenderDirtyFlags.Water);
            if (z == 0) _world.GetChunk(ChunkPosition.Up())?.SetDirty(y, ChunkRenderDirtyFlags.Water);
            if (z == ChunkSize - 1) _world.GetChunk(ChunkPosition.Down())?.SetDirty(y, ChunkRenderDirtyFlags.Water);
            if (y % ChunkSize == 0 && y != 0) _renderObjects[y / ChunkSize - 1].MarkDirty(ChunkRenderDirtyFlags.Water);
            if (y % ChunkSize == ChunkSize - 1 && y != ChunkHeight - 1) _renderObjects[y / ChunkSize + 1].MarkDirty(ChunkRenderDirtyFlags.Water);
        }

        public void FinalizeLoading()
        {
            _active = true;
            foreach (var obj in _renderObjects) obj.FinalizeGeneration();
            _world?.ResumeFluidTicks(this);
            if (_loadedFromSnapshot) ScheduleRestoredFluidTicks();
        }

        internal void ScheduleBorderFluidTicks()
        {
            for (int section = 0; section < ChunkSectionCount; section++)
            {
                if (_data.FluidCount(section) == 0) continue;
                ScheduleFluidBorder(section, ChunkRenderObject.LeftFace);
                ScheduleFluidBorder(section, ChunkRenderObject.RightFace);
                ScheduleFluidBorder(section, ChunkRenderObject.FrontFace);
                ScheduleFluidBorder(section, ChunkRenderObject.BackFace);
            }
        }

        private void ScheduleFluidBorder(int section, int face)
        {
            for (int wordIndex = 0; wordIndex < 4; wordIndex++)
            {
                ulong word = _data.GetFluidBorderWord(section, face, wordIndex);
                if (word == 0) continue;
                for (int bitInWord = 0; bitInWord < 64; bitInWord++)
                {
                    ulong mask = 1UL << bitInWord;
                    if ((word & mask) == 0) continue;
                    int bit = wordIndex * 64 + bitInWord;
                    int localY = bit / ChunkSize;
                    int across = bit % ChunkSize;
                    int x = face == ChunkRenderObject.LeftFace ? 0 :
                        face == ChunkRenderObject.RightFace ? ChunkSize - 1 : across;
                    int z = face == ChunkRenderObject.BackFace ? 0 :
                        face == ChunkRenderObject.FrontFace ? ChunkSize - 1 : across;
                    ScheduleFluidTick(new Vector3Int(x, section * ChunkSize + localY, z), Water.TickDelay);
                }
            }
        }
        
        public void DestroyChunk()
        {
            _active = false;
            _world?.CancelFluidTicks(this);
            foreach (ChunkRenderObject obj in _renderObjects)
            {
                obj.DestroyObject();
            }
        }

        internal void EnqueueRenderObject(ChunkRenderObject renderObject)
        {
            if (_active) _world.EnqueueRenderObject(this, renderObject);
        }

        public void MarkDirty()
        {
            foreach (ChunkRenderObject obj in _renderObjects) obj.MarkDirty(ChunkRenderDirtyFlags.All);
        }

        internal void MarkBorderDirty(int face)
        {
            for (int section = 0; section < ChunkSectionCount; section++)
            {
                bool blocks = false;
                bool water = false;
                for (int word = 0; word < 4 && !(blocks && water); word++)
                {
                    blocks |= _data.GetNonAirBorderWord(section, face, word) != 0;
                    water |= _data.GetFluidBorderWord(section, face, word) != 0;
                }

                ChunkRenderDirtyFlags flags = (blocks ? ChunkRenderDirtyFlags.Blocks : ChunkRenderDirtyFlags.None) |
                                              (water ? ChunkRenderDirtyFlags.Water : ChunkRenderDirtyFlags.None);
                _renderObjects[section].MarkDirty(flags);
            }
        }

        public bool TryCreatePersistenceSnapshot(out ChunkSnapshot snapshot)
        {
            if (_persistenceRevision <= _lastQueuedRevision)
            {
                snapshot = null;
                return false;
            }

            int[] blocks = new int[ChunkSnapshot.CellCount];
            int[] states = new int[ChunkSnapshot.CellCount];
            byte[] fluids = new byte[ChunkSnapshot.CellCount];
            for (int x = 0; x < ChunkSize; x++) for (int y = 0; y < ChunkHeight; y++) for (int z = 0; z < ChunkSize; z++)
            {
                int index = ChunkSnapshot.Index(x, y, z);
                blocks[index] = _data.GetBlockIdUnchecked(x, y, z);
                states[index] = _data.GetStateIdUnchecked(x, y, z);
                fluids[index] = _data.GetFluidRawUnchecked(x, y, z);
            }
            _lastQueuedRevision = _persistenceRevision;
            snapshot = new ChunkSnapshot(ChunkPosition, blocks, states, fluids, _persistenceRevision);
            return true;
        }

        internal void CompletePersistenceSave(long revision, bool succeeded)
        {
            if (succeeded) _lastSavedRevision = Math.Max(_lastSavedRevision, revision);
            else if (_lastQueuedRevision <= revision) _lastQueuedRevision = _lastSavedRevision;
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

        public override int GetHashCode() => unchecked((X * 397) ^ Z);
    }
}
