using System.Collections.Concurrent;
using System.Collections.Generic;
using System;
using System.Threading;
using World;

namespace world.generation
{
    public static class ChunkLoader
    {
        // main thread
        private static readonly HashSet<ChunkCoord> PendingLoad = new();
        private static readonly LinkedList<ChunkCoord> InactiveChunks = new();
        private static readonly Dictionary<ChunkCoord, Chunk> InactiveMap = new();
        private static readonly Dictionary<ChunkCoord, LinkedListNode<ChunkCoord>> InactiveNodes = new();
        
        // shared with worker thread
        private static readonly Queue<ChunkCoord> LoadQueue = new();
        private static readonly object LoadQueueLock = new();
        private static readonly ConcurrentQueue<Chunk> CompletedLoads = new();
        private static readonly ConcurrentDictionary<ChunkCoord, Chunk> CompletedMap = new();
        private static readonly ConcurrentQueue<(ChunkCoord coord, Exception error)> FailedLoads = new();
        private static Thread _worker;
        private static bool _stopping;

        private const int MaxInactiveChunks = 20;
        
        public static bool TryGetInactiveChunk(ChunkCoord coord, out Chunk chunk) => InactiveMap.TryGetValue(coord, out chunk);
        public static bool TryGetQueuedChunk(ChunkCoord coord, out Chunk chunk) => CompletedMap.TryGetValue(coord, out chunk);

        public static void StartWorker()
        {
            lock (LoadQueueLock)
            {
                if (_worker is { IsAlive: true }) return;
                _stopping = false;
                _worker = new Thread(WorkerLoop) { IsBackground = true, Name = "UnityCraft Chunk Loader" };
                _worker.Start();
            }
        }

        public static void StopWorker()
        {
            Thread worker;
            lock (LoadQueueLock)
            {
                _stopping = true;
                Monitor.PulseAll(LoadQueueLock);
                worker = _worker;
            }
            worker?.Join();
            lock (LoadQueueLock)
            {
                LoadQueue.Clear();
                _worker = null;
            }
            PendingLoad.Clear();
            CompletedMap.Clear();
            while (CompletedLoads.TryDequeue(out _)) { }
            while (FailedLoads.TryDequeue(out _)) { }
            foreach (Chunk inactiveChunk in InactiveMap.Values) inactiveChunk.DestroyChunk();
            InactiveChunks.Clear();
            InactiveMap.Clear();
            InactiveNodes.Clear();
        }
        
        public static void LoadChunk(ChunkCoord coord)
        {
            // MAIN THREAD ONLY.
            // No lock is needed for chunks, inactiveNodes, or pendingLoads.

            // Case 1: The chunk is already cached.
            if (InactiveMap.TryGetValue(coord, out Chunk chunk))
            {
                // Unity API: must happen on the main thread.
                chunk.Active = true;

                // Remove it from the inactive-cache tracking and add to the world
                InactiveChunks.Remove(InactiveNodes[coord]); 
                InactiveMap.Remove(coord);
                InactiveNodes.Remove(coord);
                World.World.Instance.ChunkMap[coord] = chunk;

                // Loading this neighbor may change visible border faces.
                MarkExistingNeighborBorders(coord);
                WakeFluidBorders(chunk);
                return;
            }

            // Case 2: It is already being generated or loaded.
            if (!PendingLoad.Add(coord)) return;
            
            // Case 3: Enqueue loading request. Initial world loading also uses this path so
            // the loading screen can remain responsive while the full view is prepared.

            // LOCK REQUIRED:
            // The main thread writes to loadQueue while worker threads read it.
            lock (LoadQueueLock)
            {
                LoadQueue.Enqueue(coord);
                Monitor.Pulse(LoadQueueLock);
            }
        }
        
        public static void UnloadChunk(ChunkCoord coord)
        {
            // MAIN THREAD ONLY.
            // No explicit lock is needed.

            // First cancel background loading if the chunk is not loaded yet.
            if (PendingLoad.Contains(coord)) 
            {
                PendingLoad.Remove(coord);
                return;
            }

            // Remove the chunk from the chunk map
            if (!World.World.Instance.ChunkMap.Remove(coord, out Chunk chunk)) return;

            MarkExistingNeighborBorders(coord);

            World.World.Instance.QueueChunkSave(chunk);

            // It might already be inactive.
            if (!chunk.Active) return;

            // Unity API: main thread only.
            chunk.Active = false;

            // Add this chunk to the newest end of the inactive list.
            LinkedListNode<ChunkCoord> newNode = InactiveChunks.AddLast(coord);
            InactiveNodes.Add(coord, newNode);
            InactiveMap.Add(coord, chunk);

            // Keep at most MaxInactiveChunks cached.
            while (InactiveChunks.Count > MaxInactiveChunks)
            {
                ChunkCoord oldestCoord = InactiveChunks.First.Value;

                InactiveChunks.RemoveFirst();
                InactiveNodes.Remove(oldestCoord);
                if (!InactiveMap.Remove(oldestCoord, out Chunk oldestChunk)) continue;
                
                // Unity API: main thread only.
                oldestChunk.DestroyChunk();
            }
        }

        private static void WorkerLoop()
        {
            while (true)
            {
                ChunkCoord request;

                // LOCK REQUIRED:
                // Workers and the main thread share loadQueue.
                lock (LoadQueueLock)
                {
                    while (LoadQueue.Count == 0 && !_stopping) Monitor.Wait(LoadQueueLock);
                    if (_stopping) return;

                    request = LoadQueue.Dequeue();
                }

                try
                {
                    Chunk chunk = new Chunk(request, World.World.Instance);
                    CompletedLoads.Enqueue(chunk);
                    CompletedMap[request] = chunk;
                }
                catch (Exception exception)
                {
                    FailedLoads.Enqueue((request, exception));
                }
            }
        }
        
        // MAIN THREAD ONLY.
        public static void ProcessCompletedLoads()
        {
            while (FailedLoads.TryDequeue(out var failure))
            {
                PendingLoad.Remove(failure.coord);
                World.World.Instance.HandleChunkLoadFailure(failure.coord, failure.error);
            }

            int installedThisFrame = 0;
            const int maxInstallationsPerFrame = 2;

            while (installedThisFrame < maxInstallationsPerFrame &&
                   CompletedLoads.TryDequeue(out Chunk chunk))
            {
                ChunkCoord coord = chunk.ChunkPosition;
                CompletedMap.Remove(coord, out _);
                
                // The old request may have been cancelled and removed.
                if (!PendingLoad.Contains(coord)) continue;

                PendingLoad.Remove(coord);
                
                // Unity API starts here, on the main thread.
                chunk.FinalizeLoading();
                World.World.Instance.ChunkMap.Add(coord, chunk);

                MarkExistingNeighborBorders(coord);
                WakeFluidBorders(chunk);

                installedThisFrame++;
            }
        }

        private static void WakeFluidBorders(Chunk chunk)
        {
            chunk.ScheduleBorderFluidTicks();
            World.World.Instance.GetChunk(chunk.ChunkPosition.Left())?.ScheduleBorderFluidTicks();
            World.World.Instance.GetChunk(chunk.ChunkPosition.Right())?.ScheduleBorderFluidTicks();
            World.World.Instance.GetChunk(chunk.ChunkPosition.Up())?.ScheduleBorderFluidTicks();
            World.World.Instance.GetChunk(chunk.ChunkPosition.Down())?.ScheduleBorderFluidTicks();
        }

        private static void MarkExistingNeighborBorders(ChunkCoord coord)
        {
            World.World.Instance.GetChunk(coord.Left())?.MarkBorderDirty(Render.ChunkRenderObject.RightFace);
            World.World.Instance.GetChunk(coord.Right())?.MarkBorderDirty(Render.ChunkRenderObject.LeftFace);
            World.World.Instance.GetChunk(coord.Up())?.MarkBorderDirty(Render.ChunkRenderObject.FrontFace);
            World.World.Instance.GetChunk(coord.Down())?.MarkBorderDirty(Render.ChunkRenderObject.BackFace);
        }
    }
}
