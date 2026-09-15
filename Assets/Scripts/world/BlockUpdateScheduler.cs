using System;
using System.Collections.Generic;
using UnityEngine;
using world.persistence;
using World.blocks;

namespace World
{
    /// <summary>Global, deduplicated delayed block-update queue.</summary>
    internal sealed class BlockUpdateScheduler
    {
        private readonly Dictionary<int, List<BlockCellKey>> _buckets = new();
        private readonly Stack<List<BlockCellKey>> _bucketPool = new();
        private readonly Dictionary<BlockCellKey, PendingUpdate> _pending = new();
        private readonly Dictionary<ChunkIdentity, HashSet<BlockCellKey>> _chunkKeys = new();
        private readonly HashSet<ChunkIdentity> _suspended = new();
        private readonly Dictionary<ChunkIdentity, List<BlockCellKey>> _pausedDue = new();
        private int _lastAdvancedTick;
        private int PendingCount => _pending.Count;

        internal bool HasPending(Chunk chunk)
        {
            return chunk != null && _chunkKeys.TryGetValue(
                new ChunkIdentity(chunk.ChunkPosition, chunk.FluidGeneration), out HashSet<BlockCellKey> keys) &&
                   keys.Count > 0;
        }

        internal void Schedule(Chunk chunk, Vector3Int localPosition, ushort expectedBlockId,
            ushort expectedStateId, int delay, bool markPersistenceDirty = true)
        {
            if (chunk == null || delay < 1 || localPosition.y < 0 || localPosition.y >= Chunk.ChunkHeight) return;
            BlockCellKey key = new(chunk.ChunkPosition, chunk.FluidGeneration, localPosition);
            int dueTick = Math.Max(_lastAdvancedTick + 1, _lastAdvancedTick + delay);
            PendingUpdate next = new(dueTick, expectedBlockId, expectedStateId);

            if (_pending.TryGetValue(key, out PendingUpdate existing))
            {
                if (existing.ExpectedBlockId == expectedBlockId && existing.ExpectedStateId == expectedStateId &&
                    existing.DueTick <= dueTick) return;
                _pending[key] = next;
            }
            else
            {
                _pending.Add(key, next);
                ChunkIdentity identity = key.Identity;
                if (!_chunkKeys.TryGetValue(identity, out HashSet<BlockCellKey> keys))
                {
                    keys = new HashSet<BlockCellKey>();
                    _chunkKeys.Add(identity, keys);
                }
                keys.Add(key);
            }
            AddToBucket(dueTick, key);
            if (markPersistenceDirty) chunk.MarkPersistenceDirty();
        }

        internal void Restore(Chunk chunk, ScheduledBlockUpdateSnapshot snapshot)
        {
            if ((uint)snapshot.BlockId > ushort.MaxValue || (uint)snapshot.StateId > ushort.MaxValue)
                throw new System.IO.InvalidDataException("A restored scheduled block update exceeds compact ID limits.");
            Schedule(chunk, new Vector3Int(snapshot.X, snapshot.Y, snapshot.Z), (ushort)snapshot.BlockId,
                (ushort)snapshot.StateId, snapshot.RemainingTicks, false);
        }

        internal void Advance(int tick, World world)
        {
            _lastAdvancedTick = tick;
            if (!_buckets.Remove(tick, out List<BlockCellKey> due)) return;
            due.Sort();
            foreach (var key in due)
            {
                if (!_pending.TryGetValue(key, out PendingUpdate pending) || pending.DueTick != tick) continue;
                if (_suspended.Contains(key.Identity))
                {
                    if (!_pausedDue.TryGetValue(key.Identity, out List<BlockCellKey> paused))
                    {
                        paused = new List<BlockCellKey>();
                        _pausedDue.Add(key.Identity, paused);
                    }
                    paused.Add(key);
                    continue;
                }

                Complete(key);
                Chunk chunk = world.ResolveActiveBlockChunk(key.Coord, key.Generation);
                if (chunk == null) continue;
                chunk.MarkPersistenceDirty();
                chunk.GetCellUnchecked(key.LocalPosition.x, key.LocalPosition.y, key.LocalPosition.z,
                    out Block block, out ushort stateId);
                if (block.BlockId != pending.ExpectedBlockId || stateId != pending.ExpectedStateId) continue;
                Vector3Int worldPosition = new(key.Coord.X * Chunk.ChunkSize + key.LocalPosition.x,
                    key.LocalPosition.y, key.Coord.Z * Chunk.ChunkSize + key.LocalPosition.z);
                block.OnBlockUpdate(world,
                    block.AsState(worldPosition, block.DecodeStateCached(stateId)));
            }
            due.Clear();
            _bucketPool.Push(due);
        }

        internal ScheduledBlockUpdateSnapshot[] Capture(Chunk chunk)
        {
            ChunkIdentity identity = new(chunk.ChunkPosition, chunk.FluidGeneration);
            if (!_chunkKeys.TryGetValue(identity, out HashSet<BlockCellKey> keys) || keys.Count == 0)
                return Array.Empty<ScheduledBlockUpdateSnapshot>();
            ScheduledBlockUpdateSnapshot[] result = new ScheduledBlockUpdateSnapshot[keys.Count];
            int index = 0;
            foreach (BlockCellKey key in keys)
            {
                PendingUpdate pending = _pending[key];
                result[index++] = new ScheduledBlockUpdateSnapshot(key.LocalPosition.x, key.LocalPosition.y,
                    key.LocalPosition.z, pending.ExpectedBlockId, pending.ExpectedStateId,
                    Math.Max(1, pending.DueTick - _lastAdvancedTick));
            }
            Array.Sort(result, CompareSnapshots);
            return result;
        }

        internal void Suspend(Chunk chunk)
        {
            if (chunk != null) _suspended.Add(new ChunkIdentity(chunk.ChunkPosition, chunk.FluidGeneration));
        }

        internal void Resume(Chunk chunk)
        {
            if (chunk == null) return;
            ChunkIdentity identity = new(chunk.ChunkPosition, chunk.FluidGeneration);
            _suspended.Remove(identity);
            if (!_pausedDue.Remove(identity, out List<BlockCellKey> paused)) return;
            int dueTick = _lastAdvancedTick + 1;
            foreach (var key in paused)
            {
                if (!_pending.TryGetValue(key, out PendingUpdate pending)) continue;
                _pending[key] = new PendingUpdate(dueTick, pending.ExpectedBlockId, pending.ExpectedStateId);
                AddToBucket(dueTick, key);
            }
        }

        internal void Cancel(Chunk chunk)
        {
            if (chunk == null) return;
            ChunkIdentity identity = new(chunk.ChunkPosition, chunk.FluidGeneration);
            _suspended.Remove(identity);
            _pausedDue.Remove(identity);
            if (!_chunkKeys.Remove(identity, out HashSet<BlockCellKey> keys)) return;
            foreach (BlockCellKey key in keys) _pending.Remove(key);
        }

        internal void Clear()
        {
            _buckets.Clear();
            _pending.Clear();
            _chunkKeys.Clear();
            _suspended.Clear();
            _pausedDue.Clear();
            _bucketPool.Clear();
            _lastAdvancedTick = 0;
        }

        private void AddToBucket(int tick, BlockCellKey key)
        {
            if (!_buckets.TryGetValue(tick, out List<BlockCellKey> bucket))
            {
                bucket = _bucketPool.Count == 0 ? new List<BlockCellKey>() : _bucketPool.Pop();
                _buckets.Add(tick, bucket);
            }
            bucket.Add(key);
        }

        private void Complete(BlockCellKey key)
        {
            _pending.Remove(key);
            if (!_chunkKeys.TryGetValue(key.Identity, out HashSet<BlockCellKey> keys)) return;
            keys.Remove(key);
            if (keys.Count == 0) _chunkKeys.Remove(key.Identity);
        }

        private static int CompareSnapshots(ScheduledBlockUpdateSnapshot left, ScheduledBlockUpdateSnapshot right)
        {
            int comparison = left.Y.CompareTo(right.Y);
            if (comparison != 0) return comparison;
            comparison = left.Z.CompareTo(right.Z);
            return comparison != 0 ? comparison : left.X.CompareTo(right.X);
        }

        private readonly struct PendingUpdate
        {
            public readonly int DueTick;
            public readonly ushort ExpectedBlockId;
            public readonly ushort ExpectedStateId;

            public PendingUpdate(int dueTick, ushort expectedBlockId, ushort expectedStateId)
            {
                DueTick = dueTick;
                ExpectedBlockId = expectedBlockId;
                ExpectedStateId = expectedStateId;
            }
        }

        private readonly struct ChunkIdentity : IEquatable<ChunkIdentity>
        {
            private readonly ChunkCoord _coord;
            private readonly int _generation;
            public ChunkIdentity(ChunkCoord coord, int generation) { _coord = coord; _generation = generation; }
            public bool Equals(ChunkIdentity other) => _coord.Equals(other._coord) && _generation == other._generation;
            public override bool Equals(object obj) => obj is ChunkIdentity other && Equals(other);
            public override int GetHashCode() => unchecked((_coord.GetHashCode() * 397) ^ _generation);
        }

        private readonly struct BlockCellKey : IEquatable<BlockCellKey>, IComparable<BlockCellKey>
        {
            public readonly ChunkCoord Coord;
            public readonly int Generation;
            public readonly Vector3Int LocalPosition;
            public ChunkIdentity Identity => new(Coord, Generation);

            public BlockCellKey(ChunkCoord coord, int generation, Vector3Int localPosition)
            {
                Coord = coord;
                Generation = generation;
                LocalPosition = localPosition;
            }

            public int CompareTo(BlockCellKey other)
            {
                int comparison = Coord.X.CompareTo(other.Coord.X);
                if (comparison != 0) return comparison;
                comparison = Coord.Z.CompareTo(other.Coord.Z);
                if (comparison != 0) return comparison;
                comparison = LocalPosition.y.CompareTo(other.LocalPosition.y);
                if (comparison != 0) return comparison;
                comparison = LocalPosition.z.CompareTo(other.LocalPosition.z);
                return comparison != 0 ? comparison : LocalPosition.x.CompareTo(other.LocalPosition.x);
            }

            public bool Equals(BlockCellKey other) => Coord.Equals(other.Coord) && Generation == other.Generation &&
                                                      LocalPosition.Equals(other.LocalPosition);
            public override bool Equals(object obj) => obj is BlockCellKey other && Equals(other);
            public override int GetHashCode() => HashCode.Combine(Coord, Generation, LocalPosition);
        }
    }
}
