using System;
using System.Collections.Generic;
using JetBrains.Annotations;
using UnityEngine;

namespace World
{
    public class World : MonoBehaviour
    {
        public Material material;
        public const int ViewDistance = 2;
        
        private readonly Dictionary<ChunkCoord, Chunk> _chunkMap = new();
        
        public Transform player;
        
        private ChunkCoord _playerLastChunkCoord;
        
        // Start is called once before the first execution of Update after the MonoBehaviour is created
        private void Start()
        {
            CheckViewDistance();
        }
        
        private void Update() {
            //if (!new ChunkCoord(player.transform.position).Equals(_playerLastChunkCoord))
            //    CheckViewDistance();
        }

        // TODO: Investigate why chunk border is wrong
        private void CheckViewDistance()
        {
            HashSet<ChunkCoord> previouslyActiveChunks = new HashSet<ChunkCoord>(_chunkMap.Keys);
            List<ChunkCoord> loadQueue = new List<ChunkCoord>();
            ChunkCoord centerCoord = new ChunkCoord(player.transform.position);
            _playerLastChunkCoord = centerCoord;

            for (int x = centerCoord.X - ViewDistance; x < centerCoord.X + ViewDistance; x++) {
                for (int z = centerCoord.Z - ViewDistance; z < centerCoord.Z + ViewDistance; z++) {
                    ChunkCoord thisChunk = new ChunkCoord(x, z);

                    if (!_chunkMap.ContainsKey(thisChunk)) loadQueue.Add(thisChunk);
                    previouslyActiveChunks.Remove(thisChunk);
                }
            }

            foreach (ChunkCoord coord in previouslyActiveChunks)
            {
                _chunkMap[coord].IsActive = false;
                _chunkMap.Remove(coord);
            }
            
            foreach (ChunkCoord coord in loadQueue)
            {
                LoadChunk(coord);
            }
        }

        private void LoadChunk(ChunkCoord coord)
        {
            Chunk chunk = new Chunk(coord, this);
            _chunkMap.Add(coord, chunk);
            chunk.UpdateChunkRender();
        }
        
        [CanBeNull] public Chunk GetChunk(ChunkCoord coord) => _chunkMap.GetValueOrDefault(coord);

        public Block GetBlock(int x, int y, int z)
        {
            return _chunkMap.TryGetValue(ChunkCoord.ToChunkCoord(x, z), out Chunk chunk) ? chunk.GetBlock(ToCoordInChunk(x, y, z)) : Blocks.Void;
        }

        public bool IsInBlock(float x, float y, float z)
        {
            return !GetBlock((int)Math.Floor(x), (int)Math.Floor(y), (int)Math.Floor(z))
                .IsAir;
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
