using System.Collections.Generic;
using System.Threading;
using JetBrains.Annotations;
using UnityEngine;

namespace World
{
    public class World : MonoBehaviour
    {
        public Material material;
        private const int ViewDistance = 8;
        
        public readonly Dictionary<ChunkCoord, Chunk> ChunkMap = new();
        
        public Transform player;
        public static World Instance;
        
        private ChunkCoord _playerLastChunkCoord;
        
        // Start is called once before the first execution of Update after the MonoBehaviour is created
        private void Start()
        {
            Instance = this;
            new Thread(ChunkLoader.WorkerLoop).Start();
            ChunkLoader.SyncLoading = true;
            CheckViewDistance();
            ChunkLoader.SyncLoading = false;
        }
        
        private void Update() {
            if (!new ChunkCoord(player.transform.position).Equals(_playerLastChunkCoord))
                CheckViewDistance();
            ChunkLoader.ProcessCompletedLoads();
            foreach (Chunk chunk in ChunkMap.Values) chunk.UpdateDirtyRenderObjects();
        }

        private void CheckViewDistance()
        {
            HashSet<ChunkCoord> previouslyActiveChunks = new HashSet<ChunkCoord>(ChunkMap.Keys);
            List<ChunkCoord> loadQueue = new List<ChunkCoord>();
            ChunkCoord centerCoord = new ChunkCoord(player.transform.position);
            _playerLastChunkCoord = centerCoord;

            for (int x = centerCoord.X - ViewDistance; x < centerCoord.X + ViewDistance + 1; x++) {
                for (int z = centerCoord.Z - ViewDistance; z < centerCoord.Z + ViewDistance + 1; z++) {
                    ChunkCoord thisChunk = new ChunkCoord(x, z);

                    if (!ChunkMap.ContainsKey(thisChunk)) loadQueue.Add(thisChunk);
                    previouslyActiveChunks.Remove(thisChunk);
                }
            }

            foreach (ChunkCoord coord in previouslyActiveChunks) ChunkLoader.UnloadChunk(coord);
            
            foreach (ChunkCoord coord in loadQueue) ChunkLoader.LoadChunk(coord);
        }
        
        [CanBeNull] public Chunk GetChunk(ChunkCoord coord) => ChunkMap.GetValueOrDefault(coord);

        public Block GetBlock(int x, int y, int z)
        {
            return ChunkMap.TryGetValue(ChunkCoord.ToChunkCoord(x, z), out Chunk chunk) ? chunk.GetBlock(ToCoordInChunk(x, y, z)) : Blocks.Void;
        }

        public Block GetBlock(Vector3Int position)
        {
            return GetBlock(position.x, position.y, position.z);
        }

        public void SetBlock(int x, int y, int z, Block block)
        {
            if (ChunkMap.TryGetValue(ChunkCoord.ToChunkCoord(x, z), out Chunk chunk))
            {
                x %= Chunk.ChunkSize;
                if (x < 0) x += Chunk.ChunkSize;
                z %= Chunk.ChunkSize;
                if (z < 0) z += Chunk.ChunkSize;
                chunk.SetBlock(x, y, z, block);
            }
        }

        public void SetBlock(Vector3Int position, Block block)
        {
            if (ChunkMap.TryGetValue(new ChunkCoord(position), out Chunk chunk))
            {
                chunk.SetBlock(ToCoordInChunk(position.x, position.y, position.z), block);
            }
        }

        private static Vector3Int ToCoordInChunk(int x0, int y0, int z0)
        {
            int x = x0 % Chunk.ChunkSize;
            if (x < 0) x += Chunk.ChunkSize;
            int z = z0 % Chunk.ChunkSize;
            if (z < 0) z += Chunk.ChunkSize;
            return new Vector3Int(x, y0, z);
        }
    }
}
