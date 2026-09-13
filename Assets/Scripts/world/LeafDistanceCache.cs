using UnityEngine;
using world.blocks;

namespace World
{
    internal sealed class LeafDistanceCache
    {
        private const byte Unknown = 0;
        internal const byte Disconnected = 5;
        private const int MaximumDistance = 4;

        private static readonly Vector3Int[] Directions =
        {
            Vector3Int.left, Vector3Int.right, Vector3Int.up, Vector3Int.down,
            Vector3Int.forward, Vector3Int.back
        };

        // A Manhattan-radius-four search contains only 129 cells. Reuse buffers so a random leaf
        // tick performs no managed allocation.
        private readonly Vector3Int[] _positions = new Vector3Int[160];
        private readonly byte[] _depths = new byte[160];

        internal byte Resolve(World world, Vector3Int position)
        {
            if (!world.TryGetLoadedBlock(position, out BlockState origin) ||
                origin.Block.BlockId != Blocks.OakLeave.BlockId) return Disconnected;

            Chunk chunk = world.GetChunk(ChunkCoord.ToChunkCoord(position.x, position.z));
            Vector3Int local = World.ToCoordInChunk(position);
            byte cached = chunk.Data.GetLeafDistanceUnchecked(local.x, local.y, local.z);
            if (cached != Unknown) return cached;

            int head = 0;
            int tail = 1;
            bool touchesUnavailableData = false;
            _positions[0] = position;
            _depths[0] = 0;

            while (head < tail)
            {
                Vector3Int current = _positions[head];
                int depth = _depths[head++];
                foreach (var t in Directions)
                {
                    Vector3Int neighborPosition = current + t;
                    if (!world.TryGetLoadedBlock(neighborPosition, out BlockState neighbor))
                    {
                        touchesUnavailableData = true;
                        continue;
                    }
                    if (neighbor.Block.BlockId == Blocks.OakLog.BlockId)
                    {
                        byte result = (byte)(depth + 1);
                        chunk.Data.SetLeafDistanceUnchecked(local.x, local.y, local.z, result);
                        return result;
                    }
                    if (depth + 1 >= MaximumDistance ||
                        neighbor.Block.BlockId != Blocks.OakLeave.BlockId ||
                        Contains(neighborPosition, tail)) continue;
                    _positions[tail] = neighborPosition;
                    _depths[tail] = (byte)(depth + 1);
                    tail++;
                }
            }

            byte resolved = touchesUnavailableData ? Unknown : Disconnected;
            chunk.Data.SetLeafDistanceUnchecked(local.x, local.y, local.z, resolved);
            return resolved;
        }

        internal void InvalidateAround(World world, Vector3Int position)
        {
            for (int y = -MaximumDistance; y <= MaximumDistance; y++)
            for (int z = -MaximumDistance; z <= MaximumDistance; z++)
            for (int x = -MaximumDistance; x <= MaximumDistance; x++)
            {
                if (Mathf.Abs(x) + Mathf.Abs(y) + Mathf.Abs(z) > MaximumDistance) continue;
                Vector3Int candidate = position + new Vector3Int(x, y, z);
                if (!world.TryGetLoadedBlock(candidate, out BlockState block) ||
                    block.Block.BlockId != Blocks.OakLeave.BlockId) continue;
                SetUnknown(world, candidate);
            }
        }

        internal void OnChunkTopologyChanged(World world, ChunkCoord coord)
        {
            world.GetChunk(coord)?.Data.ClearLeafDistances();
            InvalidateNeighborBand(world, coord.Left(), true, Chunk.ChunkSize - MaximumDistance);
            InvalidateNeighborBand(world, coord.Right(), true, 0);
            InvalidateNeighborBand(world, coord.Up(), false, Chunk.ChunkSize - MaximumDistance);
            InvalidateNeighborBand(world, coord.Down(), false, 0);
        }

        private static void InvalidateNeighborBand(World world, ChunkCoord coord, bool xBand, int start)
        {
            Chunk chunk = world.GetChunk(coord);
            if (chunk == null) return;
            for (int y = 0; y < Chunk.ChunkHeight; y++)
            for (int across = 0; across < Chunk.ChunkSize; across++)
            for (int depth = 0; depth < MaximumDistance; depth++)
            {
                int x = xBand ? start + depth : across;
                int z = xBand ? across : start + depth;
                if (chunk.Data.GetBlockIdUnchecked(x, y, z) == Blocks.OakLeave.BlockId)
                    chunk.Data.SetLeafDistanceUnchecked(x, y, z, Unknown);
            }
        }

        private void SetUnknown(World world, Vector3Int position)
        {
            Chunk chunk = world.GetChunk(ChunkCoord.ToChunkCoord(position.x, position.z));
            if (chunk == null || position.y < 0 || position.y >= Chunk.ChunkHeight) return;
            Vector3Int local = World.ToCoordInChunk(position);
            chunk.Data.SetLeafDistanceUnchecked(local.x, local.y, local.z, Unknown);
        }

        private bool Contains(Vector3Int position, int length)
        {
            for (int i = 0; i < length; i++) if (_positions[i] == position) return true;
            return false;
        }
    }
}
