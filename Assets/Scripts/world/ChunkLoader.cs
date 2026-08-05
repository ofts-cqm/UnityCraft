using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using Unity.VisualScripting;

namespace World
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

        private const int MaxInactiveChunks = 20;
        public static bool SyncLoading = false;
        
        public static void LoadChunk(ChunkCoord coord)
        {
            // MAIN THREAD ONLY.
            // No lock is needed for chunks, inactiveNodes, or pendingLoads.

            // Case 1: The chunk is already cached.
            if (InactiveMap.TryGetValue(coord, out Chunk chunk))
            {
                // Unity API: must happen on the main thread.
                chunk.Active = true;

                // Remove it from the inactive-cache tracking.
                InactiveChunks.Remove(coord); 
                InactiveMap.Remove(coord);
                InactiveNodes.Remove(coord);

                // Loading this neighbor may change visible border faces.
                World.Instance.GetChunk(coord.Left())?.MarkDirty();
                World.Instance.GetChunk(coord.Right())?.MarkDirty();
                World.Instance.GetChunk(coord.Up())?.MarkDirty();
                World.Instance.GetChunk(coord.Down())?.MarkDirty();
                return;
            }

            // Case 2: It is already being generated or loaded.
            if (PendingLoad.Contains(coord)) return;
            
            // Case 3: Enqueue Loading Request
            // If Sync Loading is required
            if(SyncLoading)
            {
                Chunk newChunk = new Chunk(coord, World.Instance);
                newChunk.FinalizeLoading();
                World.Instance.ChunkMap.Add(coord, newChunk);
                
                World.Instance.GetChunk(coord.Left())?.MarkDirty();
                World.Instance.GetChunk(coord.Right())?.MarkDirty();
                World.Instance.GetChunk(coord.Up())?.MarkDirty();
                World.Instance.GetChunk(coord.Down())?.MarkDirty();
                return;
            }
            
            PendingLoad.Add(coord);

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
            if (!World.Instance.ChunkMap.Remove(coord, out Chunk chunk)) return;

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
        
        public static void WorkerLoop()
        {
            while (true)
            {
                ChunkCoord request;

                // LOCK REQUIRED:
                // Workers and the main thread share loadQueue.
                lock (LoadQueueLock)
                {
                    while (LoadQueue.Count == 0) Monitor.Wait(LoadQueueLock);

                    request = LoadQueue.Dequeue();
                }

                Chunk chunk = new Chunk(request, World.Instance);
                CompletedLoads.Enqueue(chunk);
            }
        }
        
        // MAIN THREAD ONLY.
        public static void ProcessCompletedLoads()
        {
            int installedThisFrame = 0;
            const int maxInstallationsPerFrame = 2;

            while (installedThisFrame < maxInstallationsPerFrame &&
                   CompletedLoads.TryDequeue(out Chunk chunk))
            {
                ChunkCoord coord = chunk.ChunkPosition;
                
                // The old request may have been cancelled and removed.
                if (!PendingLoad.Contains(coord)) continue;

                PendingLoad.Remove(coord);
                
                // Unity API starts here, on the main thread.
                chunk.FinalizeLoading();
                World.Instance.ChunkMap.Add(coord, chunk);

                World.Instance.GetChunk(coord.Left())?.MarkDirty();
                World.Instance.GetChunk(coord.Right())?.MarkDirty();
                World.Instance.GetChunk(coord.Up())?.MarkDirty();
                World.Instance.GetChunk(coord.Down())?.MarkDirty();

                installedThisFrame++;
            }
        }
    }
}