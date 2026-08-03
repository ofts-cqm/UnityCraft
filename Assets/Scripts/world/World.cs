using System.Collections.Generic;
using JetBrains.Annotations;
using UnityEngine;

namespace World
{
    public class World : MonoBehaviour
    {
        public Material material;
        private const int ViewDistance = 2;
        
        private readonly Dictionary<ChunkCoord, Chunk> _chunkMap = new();
        private readonly Dictionary<ChunkCoord, Chunk> _inactiveChunks = new();
        
        public Transform player;
        
        private ChunkCoord _playerLastChunkCoord;
        
        // Start is called once before the first execution of Update after the MonoBehaviour is created
        private void Start()
        {
            CheckViewDistance();
        }
        
        private void Update() {
            if (!new ChunkCoord(player.transform.position).Equals(_playerLastChunkCoord))
                CheckViewDistance();
            foreach (Chunk chunk in _chunkMap.Values) chunk.UpdateDirtyRenderObjects();
        }

        // TODO: Investigate why chunk border is wrong
        private void CheckViewDistance()
        {
            HashSet<ChunkCoord> previouslyActiveChunks = new HashSet<ChunkCoord>(_chunkMap.Keys);
            List<ChunkCoord> loadQueue = new List<ChunkCoord>();
            ChunkCoord centerCoord = new ChunkCoord(player.transform.position);
            _playerLastChunkCoord = centerCoord;

            for (int x = centerCoord.X - ViewDistance; x < centerCoord.X + ViewDistance + 1; x++) {
                for (int z = centerCoord.Z - ViewDistance; z < centerCoord.Z + ViewDistance + 1; z++) {
                    ChunkCoord thisChunk = new ChunkCoord(x, z);

                    if (!_chunkMap.ContainsKey(thisChunk)) loadQueue.Add(thisChunk);
                    previouslyActiveChunks.Remove(thisChunk);
                }
            }

            foreach (ChunkCoord coord in previouslyActiveChunks)
            {
                _inactiveChunks.Add(coord, _chunkMap[coord]);
                _chunkMap[coord].Active = false;
                _chunkMap.Remove(coord);
            }
            
            foreach (ChunkCoord coord in loadQueue) LoadChunk(coord);
        }

        private void LoadChunk(ChunkCoord coord)
        {
            if (_inactiveChunks.TryGetValue(coord, out Chunk chunk))
            {
                chunk.Active = true;
                _inactiveChunks.Remove(coord);
            }
            else chunk = new Chunk(coord, this);
            _chunkMap.Add(coord, chunk);
            GetChunk(coord.Left())?.MarkDirty();
            GetChunk(coord.Right())?.MarkDirty();
            GetChunk(coord.Up())?.MarkDirty();
            GetChunk(coord.Down())?.MarkDirty();
        }
        
        [CanBeNull] public Chunk GetChunk(ChunkCoord coord) => _chunkMap.GetValueOrDefault(coord);

        public Block GetBlock(int x, int y, int z)
        {
            return _chunkMap.TryGetValue(ChunkCoord.ToChunkCoord(x, z), out Chunk chunk) ? chunk.GetBlock(ToCoordInChunk(x, y, z)) : Blocks.Void;
        }

        public void SetBlock(int x, int y, int z, Block block)
        {
            if (_chunkMap.TryGetValue(ChunkCoord.ToChunkCoord(x, z), out Chunk chunk))
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
            if (_chunkMap.TryGetValue(new ChunkCoord(position), out Chunk chunk))
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
