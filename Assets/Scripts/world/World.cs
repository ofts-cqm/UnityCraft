using System.Collections.Generic;
using UnityEngine;

namespace World
{
    public class World : MonoBehaviour
    {
        public Material material;
        public const int ViewDistance = 4;
        
        private readonly Dictionary<ChunkCoord, Chunk> chunkMap = new();
        
        public Transform player;
        
        private ChunkCoord _playerLastChunkCoord;
        
        // Start is called once before the first execution of Update after the MonoBehaviour is created
        private void Start()
        {
            LoadChunk(new ChunkCoord(0, 0));
            LoadChunk(new ChunkCoord(0, 1));
            LoadChunk(new ChunkCoord(1, 0));
            LoadChunk(new ChunkCoord(1, 1));
        }
        
        private void Update() {
            if (!new ChunkCoord(player.transform.position).Equals(_playerLastChunkCoord))
                CheckViewDistance();
        }

        // TODO: Investigate why chunk border is wrong
        private void CheckViewDistance()
        {
            HashSet<ChunkCoord> previouslyActiveChunks = new HashSet<ChunkCoord>(chunkMap.Keys);
            ChunkCoord chunkCoord = new ChunkCoord(player.transform.position);
            _playerLastChunkCoord = chunkCoord;

            for (int x = chunkCoord.x - ViewDistance / 2; x < chunkCoord.x + ViewDistance / 2; x++) {
                for (int z = chunkCoord.z - ViewDistance / 2; z < chunkCoord.z + ViewDistance / 2; z++) {
                    ChunkCoord thisChunk = new ChunkCoord(x, z);

                    if (!chunkMap.ContainsKey(thisChunk))
                    {
                        LoadChunk(thisChunk);
                    }

                    previouslyActiveChunks.Remove(chunkCoord);
                }
            }

            //foreach (ChunkCoord coord in previouslyActiveChunks)
            //{
            //    chunkMap[coord].isActive = false;
            //    chunkMap.Remove(coord);
            //}
        }

        private void LoadChunk(ChunkCoord coord)
        {
            chunkMap.Add(coord, new Chunk(coord, this));
        }
        
        public Block GetBlock(int x, int y, int z) => GetBlock(new Vector3Int(x, y, z));

        public Block GetBlock(Vector3Int position)
        {
            //Console.WriteLine("World: getting block " + position);
            ChunkCoord coord = new ChunkCoord(position);
            return chunkMap.TryGetValue(coord, out Chunk chunk) ? chunk.GetBlock(ToCoordInChunk(position)) : Blocks.Air;
        }

        public Vector3Int ToCoordInChunk(Vector3Int position)
        {
            int x = position.x % Chunk.ChunkSize;
            if (x < 0) x += Chunk.ChunkSize;
            int z = position.z % Chunk.ChunkSize;
            if (z < 0) z += Chunk.ChunkSize;
            return new Vector3Int(x, position.y, z);
        }
    }
}
