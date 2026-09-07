using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using World;

namespace world.persistence
{
    /// <summary>
    /// Serializes all writes on one background thread. Chunk snapshots are immutable copies,
    /// so the worker never touches Unity objects or live chunk arrays.
    /// </summary>
    public sealed class WorldSaveCoordinator : IDisposable
    {
        private enum WorkKind { Player, Chunk }

        public readonly struct SaveCompletion
        {
            public readonly Chunk Chunk;
            public readonly long Revision;
            public readonly Exception Error;

            public SaveCompletion(Chunk chunk, long revision, Exception error)
            {
                Chunk = chunk;
                Revision = revision;
                Error = error;
            }
        }

        private sealed class PendingChunk
        {
            public readonly ChunkSnapshot Snapshot;
            public readonly Chunk Owner;
            public PendingChunk(ChunkSnapshot snapshot, Chunk owner) { Snapshot = snapshot; Owner = owner; }
        }

        private readonly IWorldStorage _storage;
        private readonly WorldLoadAuthorization _authorization;
        private readonly object _gate = new();
        private readonly Queue<(WorkKind kind, ChunkCoord coord)> _queue = new();
        private readonly Dictionary<ChunkCoord, PendingChunk> _latestChunks = new();
        private readonly HashSet<ChunkCoord> _queuedChunks = new();
        private readonly ConcurrentQueue<SaveCompletion> _completions = new();
        private readonly Thread _worker;
        private PlayerSnapshot _latestPlayer;
        private bool _playerQueued;
        private bool _stopping;
        private bool _disposed;
        private bool _descriptorUpdated;
        private bool _working;

        public WorldSaveCoordinator(IWorldStorage storage, WorldLoadAuthorization authorization)
        {
            _storage = storage ?? throw new ArgumentNullException(nameof(storage));
            _authorization = authorization ?? throw new ArgumentNullException(nameof(authorization));
            _worker = new Thread(WorkerLoop) { IsBackground = true, Name = "UnityCraft Save Worker" };
            _worker.Start();
        }

        public bool TryLoadChunk(ChunkCoord coord, out ChunkSnapshot snapshot)
        {
            lock (_gate)
            {
                if (_latestChunks.TryGetValue(coord, out PendingChunk pending))
                {
                    snapshot = pending.Snapshot;
                    return true;
                }
            }
            return _storage.TryLoadChunk(_authorization, coord, out snapshot);
        }

        public void QueuePlayer(PlayerSnapshot snapshot)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            lock (_gate)
            {
                ThrowIfStopping();
                _latestPlayer = snapshot;
                if (!_playerQueued)
                {
                    _playerQueued = true;
                    _queue.Enqueue((WorkKind.Player, default));
                }
                Monitor.Pulse(_gate);
            }
        }

        public void QueueChunk(Chunk chunk, ChunkSnapshot snapshot)
        {
            if (chunk == null) throw new ArgumentNullException(nameof(chunk));
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            lock (_gate)
            {
                ThrowIfStopping();
                _latestChunks[snapshot.Coord] = new PendingChunk(snapshot, chunk);
                if (_queuedChunks.Add(snapshot.Coord)) _queue.Enqueue((WorkKind.Chunk, snapshot.Coord));
                Monitor.Pulse(_gate);
            }
        }

        public bool TryDequeueCompletion(out SaveCompletion completion) => _completions.TryDequeue(out completion);

        public void Flush()
        {
            lock (_gate)
            {
                ThrowIfStopping();
                while (_queue.Count > 0 || _working) Monitor.Wait(_gate);
            }
        }

        public void FlushAndStop()
        {
            lock (_gate)
            {
                if (_disposed) return;
                _stopping = true;
                Monitor.PulseAll(_gate);
            }
            _worker.Join();
            _disposed = true;
        }

        public void Dispose() => FlushAndStop();

        private void WorkerLoop()
        {
            while (true)
            {
                (WorkKind kind, ChunkCoord coord) work;
                lock (_gate)
                {
                    while (_queue.Count == 0 && !_stopping) Monitor.Wait(_gate);
                    if (_queue.Count == 0 && _stopping) return;
                    work = _queue.Dequeue();
                    _working = true;
                }

                try
                {
                    if (work.kind == WorkKind.Player) SaveLatestPlayer();
                    else SaveLatestChunk(work.coord);
                }
                finally
                {
                    lock (_gate)
                    {
                        _working = false;
                        Monitor.PulseAll(_gate);
                    }
                }
            }
        }

        private void SaveLatestPlayer()
        {
            PlayerSnapshot snapshot;
            lock (_gate) snapshot = _latestPlayer;
            Exception error = null;
            try
            {
                EnsureDescriptorUpdated();
                _storage.SavePlayer(_authorization, snapshot);
                _storage.UpdateWorldVersion(_authorization.WorldId, DescriptorSaveVersion());
            }
            catch (Exception exception) { error = exception; }

            lock (_gate)
            {
                if (ReferenceEquals(snapshot, _latestPlayer))
                {
                    _latestPlayer = null;
                    _playerQueued = false;
                }
                else _queue.Enqueue((WorkKind.Player, default));
            }
            if (error != null) _completions.Enqueue(new SaveCompletion(null, 0, error));
        }

        private void SaveLatestChunk(ChunkCoord coord)
        {
            PendingChunk pending;
            lock (_gate) pending = _latestChunks[coord];
            Exception error = null;
            try
            {
                EnsureDescriptorUpdated();
                _storage.SaveChunk(_authorization, pending.Snapshot);
            }
            catch (Exception exception) { error = exception; }

            lock (_gate)
            {
                if (ReferenceEquals(pending, _latestChunks[coord]))
                {
                    _latestChunks.Remove(coord);
                    _queuedChunks.Remove(coord);
                }
                else _queue.Enqueue((WorkKind.Chunk, coord));
            }
            _completions.Enqueue(new SaveCompletion(pending.Owner, pending.Snapshot.Revision, error));
        }

        private void EnsureDescriptorUpdated()
        {
            if (_descriptorUpdated) return;
            _storage.UpdateWorldVersion(_authorization.WorldId, DescriptorSaveVersion());
            _descriptorUpdated = true;
        }

        private SaveVersion DescriptorSaveVersion()
        {
            // Do not claim a risky newer-content world has been downgraded while untouched
            // chunk files from that newer version may still exist.
            int content = Math.Max(_authorization.SaveVersion.Content, SaveVersionPolicy.Current.Content);
            return new SaveVersion(SaveVersionPolicy.Current.Schema, content);
        }

        private void ThrowIfStopping()
        {
            if (_stopping || _disposed) throw new ObjectDisposedException(nameof(WorldSaveCoordinator));
        }
    }
}
