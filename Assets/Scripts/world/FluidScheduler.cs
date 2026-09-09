using System;
using System.Collections.Generic;
using UnityEngine;
using World.blocks;

namespace World
{
    /// <summary>
    /// One due-tick queue for the whole world. It stores value keys rather than Chunk references,
    /// deduplicates cells, and does no collection creation or world traversal on an empty tick.
    /// </summary>
    internal sealed class FluidScheduler
    {
        private readonly object _gate = new();
        private readonly Dictionary<int, List<FluidCellKey>> _buckets = new();
        private readonly Stack<List<FluidCellKey>> _bucketPool = new();
        private readonly Dictionary<FluidCellKey, PendingTick> _pending = new();
        private readonly Dictionary<ChunkIdentity, HashSet<FluidCellKey>> _chunkKeys = new();
        private readonly HashSet<ChunkIdentity> _suspended = new();
        private readonly Dictionary<ChunkIdentity, List<FluidCellKey>> _pausedDue = new();
        private int _lastAdvancedTick;

        internal int PendingCount
        {
            get { lock (_gate) return _pending.Count; }
        }

        internal void Schedule(Chunk chunk, Vector3Int localPosition, byte expectedRawAmount, int dueTick)
        {
            if (chunk == null || expectedRawAmount == 0 || localPosition.y < 0 ||
                localPosition.y >= Chunk.ChunkHeight) return;
            if ((uint)localPosition.x >= Chunk.ChunkSize || (uint)localPosition.z >= Chunk.ChunkSize)
                throw new ArgumentOutOfRangeException(nameof(localPosition));

            FluidCellKey key = new(chunk.ChunkPosition, chunk.FluidGeneration, localPosition);
            lock (_gate)
            {
                dueTick = Math.Max(dueTick, _lastAdvancedTick + 1);
                if (_pending.TryGetValue(key, out PendingTick existing))
                {
                    // A state mutation supersedes an event for an older amount even when its due
                    // time is later. Equivalent events keep the earliest scheduled execution.
                    if (existing.ExpectedRawAmount == expectedRawAmount && existing.DueTick <= dueTick) return;
                    _pending[key] = new PendingTick(dueTick, expectedRawAmount);
                }
                else
                {
                    _pending.Add(key, new PendingTick(dueTick, expectedRawAmount));
                    ChunkIdentity identity = key.Identity;
                    if (!_chunkKeys.TryGetValue(identity, out HashSet<FluidCellKey> keys))
                    {
                        keys = new HashSet<FluidCellKey>();
                        _chunkKeys.Add(identity, keys);
                    }
                    keys.Add(key);
                }
                AddToBucket(dueTick, key);
            }
        }

        internal int Advance(int tick, int maximumUpdates, World world)
        {
            return AdvanceCore(tick, maximumUpdates, world, null);
        }

        // Test seam for the scheduler's pure data-structure behavior. Production uses the World
        // overload above, avoiding a per-FixedUpdate method-group allocation.
        internal int Advance(int tick, int maximumUpdates, Func<ChunkCoord, int, Chunk> resolveActiveChunk)
        {
            return AdvanceCore(tick, maximumUpdates, null, resolveActiveChunk);
        }

        private int AdvanceCore(int tick, int maximumUpdates, World world,
            Func<ChunkCoord, int, Chunk> resolveActiveChunk)
        {
            List<FluidCellKey> due;
            lock (_gate)
            {
                _lastAdvancedTick = tick;
                if (!_buckets.TryGetValue(tick, out due)) return 0;
                _buckets.Remove(tick);
            }

            // Coordinates define conflict order explicitly. This replaces the old implicit
            // Dictionary/list ordering and makes fluid outcomes reproducible across runs.
            due.Sort();
            int updates = 0;
            for (int i = 0; i < due.Count; i++)
            {
                FluidCellKey key = due[i];
                PendingTick pending;
                lock (_gate)
                {
                    if (!_pending.TryGetValue(key, out pending) || pending.DueTick != tick) continue;
                    if (_suspended.Contains(key.Identity))
                    {
                        if (!_pausedDue.TryGetValue(key.Identity, out List<FluidCellKey> paused))
                        {
                            paused = new List<FluidCellKey>();
                            _pausedDue.Add(key.Identity, paused);
                        }
                        paused.Add(key);
                        continue;
                    }
                    if (maximumUpdates > 0 && updates >= maximumUpdates)
                    {
                        PendingTick deferred = new(tick + 1, pending.ExpectedRawAmount);
                        _pending[key] = deferred;
                        AddToBucket(deferred.DueTick, key);
                        continue;
                    }
                    Complete(key);
                }

                Chunk chunk = world != null
                    ? world.ResolveActiveFluidChunk(key.Coord, key.Generation)
                    : resolveActiveChunk(key.Coord, key.Generation);
                if (chunk == null) continue;
                FluidState state = chunk.GetFluid(key.LocalPosition);
                if (state.RawAmount != pending.ExpectedRawAmount || state.IsEmpty) continue;
                updates++;
                Water.Tick(chunk, key.LocalPosition);
            }
            lock (_gate)
            {
                due.Clear();
                _bucketPool.Push(due);
            }
            return updates;
        }

        internal void Suspend(Chunk chunk)
        {
            if (chunk == null) return;
            lock (_gate) _suspended.Add(new ChunkIdentity(chunk.ChunkPosition, chunk.FluidGeneration));
        }

        internal void Resume(Chunk chunk)
        {
            if (chunk == null) return;
            ChunkIdentity identity = new(chunk.ChunkPosition, chunk.FluidGeneration);
            lock (_gate)
            {
                _suspended.Remove(identity);
                if (!_pausedDue.Remove(identity, out List<FluidCellKey> paused)) return;
                int dueTick = _lastAdvancedTick + 1;
                for (int i = 0; i < paused.Count; i++)
                {
                    FluidCellKey key = paused[i];
                    if (!_pending.TryGetValue(key, out PendingTick pending)) continue;
                    _pending[key] = new PendingTick(dueTick, pending.ExpectedRawAmount);
                    AddToBucket(dueTick, key);
                }
            }
        }

        internal void Cancel(Chunk chunk)
        {
            if (chunk == null) return;
            ChunkIdentity identity = new(chunk.ChunkPosition, chunk.FluidGeneration);
            lock (_gate)
            {
                _suspended.Remove(identity);
                _pausedDue.Remove(identity);
                if (!_chunkKeys.Remove(identity, out HashSet<FluidCellKey> keys)) return;
                foreach (FluidCellKey key in keys) _pending.Remove(key);
                // Bucket records are intentionally left as cheap stale values and disappear at
                // their due tick. Removing arbitrary list entries would make unload much costlier.
            }
        }

        internal void Clear()
        {
            lock (_gate)
            {
                _buckets.Clear();
                _pending.Clear();
                _chunkKeys.Clear();
                _suspended.Clear();
                _pausedDue.Clear();
                _bucketPool.Clear();
                _lastAdvancedTick = 0;
            }
        }

        private void AddToBucket(int dueTick, FluidCellKey key)
        {
            if (!_buckets.TryGetValue(dueTick, out List<FluidCellKey> bucket))
            {
                bucket = _bucketPool.Count == 0 ? new List<FluidCellKey>() : _bucketPool.Pop();
                _buckets.Add(dueTick, bucket);
            }
            bucket.Add(key);
        }

        // Must be called under _gate.
        private void Complete(FluidCellKey key)
        {
            _pending.Remove(key);
            ChunkIdentity identity = key.Identity;
            if (!_chunkKeys.TryGetValue(identity, out HashSet<FluidCellKey> keys)) return;
            keys.Remove(key);
            if (keys.Count == 0) _chunkKeys.Remove(identity);
        }

        private readonly struct PendingTick
        {
            public readonly int DueTick;
            public readonly byte ExpectedRawAmount;

            public PendingTick(int dueTick, byte expectedRawAmount)
            {
                DueTick = dueTick;
                ExpectedRawAmount = expectedRawAmount;
            }
        }

        private readonly struct ChunkIdentity : IEquatable<ChunkIdentity>
        {
            public readonly ChunkCoord Coord;
            public readonly int Generation;

            public ChunkIdentity(ChunkCoord coord, int generation)
            {
                Coord = coord;
                Generation = generation;
            }

            public bool Equals(ChunkIdentity other) => Coord.Equals(other.Coord) && Generation == other.Generation;
            public override bool Equals(object obj) => obj is ChunkIdentity other && Equals(other);
            public override int GetHashCode() => unchecked((Coord.GetHashCode() * 397) ^ Generation);
        }

        private readonly struct FluidCellKey : IEquatable<FluidCellKey>, IComparable<FluidCellKey>
        {
            public readonly ChunkCoord Coord;
            public readonly int Generation;
            public readonly Vector3Int LocalPosition;
            public ChunkIdentity Identity => new(Coord, Generation);

            public FluidCellKey(ChunkCoord coord, int generation, Vector3Int localPosition)
            {
                Coord = coord;
                Generation = generation;
                LocalPosition = localPosition;
            }

            public int CompareTo(FluidCellKey other)
            {
                int comparison = Coord.X.CompareTo(other.Coord.X);
                if (comparison != 0) return comparison;
                comparison = Coord.Z.CompareTo(other.Coord.Z);
                if (comparison != 0) return comparison;
                comparison = LocalPosition.y.CompareTo(other.LocalPosition.y);
                if (comparison != 0) return comparison;
                comparison = LocalPosition.z.CompareTo(other.LocalPosition.z);
                if (comparison != 0) return comparison;
                comparison = LocalPosition.x.CompareTo(other.LocalPosition.x);
                return comparison != 0 ? comparison : Generation.CompareTo(other.Generation);
            }

            public bool Equals(FluidCellKey other) => Coord.Equals(other.Coord) && Generation == other.Generation &&
                                                      LocalPosition.Equals(other.LocalPosition);
            public override bool Equals(object obj) => obj is FluidCellKey other && Equals(other);
            public override int GetHashCode() => HashCode.Combine(Coord, Generation, LocalPosition);
        }
    }
}
