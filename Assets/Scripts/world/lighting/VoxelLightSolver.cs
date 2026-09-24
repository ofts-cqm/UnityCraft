using System;
using System.Collections.Generic;
using UnityEngine;
using World;
using world.blocks;

namespace lighting
{
    public sealed class LightInput
    {
        public readonly ushort[] Blocks, States;
        public readonly byte[] Fluids;
        public LightInput(ushort[] blocks, ushort[] states, byte[] fluids)
        { Blocks = blocks; States = states; Fluids = fluids; }
    }

    /// <summary>Immutable after publication. The solver owns all mutable copies.</summary>
    public sealed class LightColumn
    {
        public readonly byte[] Sky = new byte[ChunkData.CellCount];
        public readonly byte[] Local = new byte[ChunkData.CellCount];
        public readonly byte[] Occupancy;
        public readonly byte[] Absorption;
        public readonly byte[] Emission;
        public LightColumn()
        {
            Occupancy = new byte[ChunkData.CellCount];
            Absorption = new byte[ChunkData.CellCount];
            Emission = new byte[ChunkData.CellCount];
        }
        internal LightColumn(LightColumn optics)
        {
            // Optical arrays are immutable in the worker. Share them across published light
            // generations to avoid copying three additional buffers on every propagation update.
            Occupancy = optics.Occupancy;
            Absorption = optics.Absorption;
            Emission = optics.Emission;
        }
    }

    /// <summary>
    /// Rebuild a bounded dirty region from sources, rather than leaving stale light behind after
    /// removal. A 16-block halo exceeds the maximum 14-block lateral propagation distance.
    /// No Unity object APIs are used here, so large solves run entirely on the lighting worker.
    /// </summary>
    public static class VoxelLightSolver
    {
        private static readonly int[] Dx = { 0, 0, 0, 0, -1, 1 };
        private static readonly int[] Dy = { 1, -1, 0, 0, 0, 0 };
        private static readonly int[] Dz = { 0, 0, 1, -1, 0, 0 };
        private static readonly byte[,] OpenFaces = BuildOpenFaces();
        public static int Index(int x, int y, int z) => y * 256 + z * 16 + x;

        public static byte Occupancy(World.blocks.Block block, ushort state)
        {
            if (block.IsAir || block.AllowsLightPassThrough) return 0;
            if (block is Stair)
            {
                byte mask = 0;
                for (int n = 0; n < 8; n++)
                    if (Stair.Occupies((StairState)state, n & 1, n >> 2, (n >> 1) & 1)) mask |= (byte)(1 << n);
                return mask;
            }
            if (block is Slab)
            {
                SlabPart part = (SlabPart)block.DecodeState(state);
                byte mask = 0;
                for (int n = 0; n < 8; n++)
                {
                    int x = n & 1, y = n >> 2, z = (n >> 1) & 1;
                    bool filled = part switch
                    {
                        SlabPart.Bottom => y == 0, SlabPart.Top => y == 1,
                        SlabPart.East => x == 1, SlabPart.West => x == 0,
                        SlabPart.North => z == 1, SlabPart.South => z == 0, _ => true
                    };
                    if (filled) mask |= (byte)(1 << n);
                }
                return mask;
            }
            return 255;
        }

        private static byte[,] BuildOpenFaces()
        {
            var table = new byte[256, 6];
            for (int mask = 0; mask < 256; mask++)
            for (int n = 0; n < 8; n++)
            {
                if ((mask & (1 << n)) != 0) continue;
                int x = n & 1, y = n >> 2, z = (n >> 1) & 1;
                table[mask, y == 1 ? 0 : 1] |= (byte)(1 << (x + z * 2));
                table[mask, z == 1 ? 2 : 3] |= (byte)(1 << (x + y * 2));
                table[mask, x == 0 ? 4 : 5] |= (byte)(1 << (z + y * 2));
            }
            return table;
        }

        public static bool Connects(byte from, byte to, int face) =>
            (OpenFaces[from, face] & OpenFaces[to, face ^ 1]) != 0;

        public static LightColumn CreateColumn(LightInput input)
        {
            var column = new LightColumn();
            var palette = new Dictionary<uint, (byte mask, byte absorption, byte emission)>();
            for (int i = 0; i < ChunkData.CellCount; i++)
            {
                uint key = input.Blocks[i] | ((uint)input.States[i] << 16);
                if (!palette.TryGetValue(key, out var optics))
                {
                    var block = Blocks.GetByCompactId(input.Blocks[i]);
                    optics = (Occupancy(block, input.States[i]),
                        0,
                        (byte)Math.Min(15, (int)block.LightEmission(input.States[i])));
                    palette.Add(key, optics);
                }
                column.Occupancy[i] = optics.mask;
                column.Absorption[i] = (byte)Math.Max(optics.absorption, input.Fluids[i] == 0 ? 0 : 2);
                column.Emission[i] = optics.emission;
            }
            return column;
        }

        public static Dictionary<ChunkCoord, LightColumn> Solve(
            Dictionary<ChunkCoord, LightColumn> existing, HashSet<ChunkCoord> dirty,
            IReadOnlyDictionary<Vector3Int, byte> sources, Func<bool> cancelled = null)
        {
            var region = new Dictionary<ChunkCoord, LightColumn>();
            foreach (ChunkCoord c in dirty)
                for (int z = -1; z <= 1; z++) for (int x = -1; x <= 1; x++)
                {
                    var coord = new ChunkCoord(c.X + x, c.Z + z);
                    if (region.ContainsKey(coord) || !existing.TryGetValue(coord, out var old)) continue;
                    var next = new LightColumn(old);
                    region.Add(coord, next);
                }
            var queue = new Queue<(ChunkCoord coord, int index)>();
            foreach (var pair in region)
            {
                if (cancelled?.Invoke() == true) return null;
                var c = pair.Value;
                for (int z = 0; z < 16; z++) for (int x = 0; x < 16; x++)
                {
                    // Four vertical half-block rays prevent misaligned slab gaps admitting full skylight.
                    int a = 15, b = 15, d = 15, e = 15;
                    for (int y = 127; y >= 0; y--)
                    {
                        int i = Index(x, y, z), mask = c.Occupancy[i], loss = c.Absorption[i];
                        a = (mask & 0x11) == 0 ? Math.Max(0, a - loss) : 0;
                        b = (mask & 0x22) == 0 ? Math.Max(0, b - loss) : 0;
                        d = (mask & 0x44) == 0 ? Math.Max(0, d - loss) : 0;
                        e = (mask & 0x88) == 0 ? Math.Max(0, e - loss) : 0;
                        c.Sky[i] = (byte)Math.Max(Math.Max(a, b), Math.Max(d, e));
                        c.Local[i] = c.Emission[i];
                    }
                }
            }
            foreach (var source in sources)
            {
                Vector3Int p = source.Key;
                if (p.y < 0 || p.y >= 128) continue;
                if (region.TryGetValue(new ChunkCoord(p.x >> 4, p.z >> 4), out var c))
                {
                    int i = Index(p.x & 15, p.y, p.z & 15);
                    c.Local[i] = Math.Max(c.Local[i], source.Value);
                }
            }
            // Seed retained outside boundaries. The dirty halo is larger than any changed light's reach.
            foreach (var pair in region)
            {
                if (cancelled?.Invoke() == true) return null;
                var c = pair.Value;
                for (int y = 0; y < 128; y++) for (int z = 0; z < 16; z++) for (int x = 0; x < 16; x++)
                {
                    int i = Index(x, y, z);
                    if (x == 0 || x == 15 || z == 0 || z == 15)
                        for (int face = 2; face < 6; face++)
                        {
                            Neighbor(pair.Key, x, y, z, face, out var nc, out int ni);
                            if (nc.Equals(pair.Key) || region.ContainsKey(nc) || !existing.TryGetValue(nc, out var n)) continue;
                            if (!Connects(n.Occupancy[ni], c.Occupancy[i], face ^ 1)) continue;
                            int loss = Math.Max(1, (int)c.Absorption[i]);
                            c.Sky[i] = (byte)Math.Max(c.Sky[i], n.Sky[ni] - loss);
                            c.Local[i] = (byte)Math.Max(c.Local[i], n.Local[ni] - loss);
                        }
                    if (c.Sky[i] <= 1 && c.Local[i] <= 1) continue;
                    // Only the light frontier needs a queue entry. Enqueuing every sky-lit air
                    // cell allocated a world-sized queue even when almost no propagation was needed.
                    for (int face = 0; face < 6; face++)
                    {
                        if (y + Dy[face] < 0 || y + Dy[face] >= 128) continue;
                        Neighbor(pair.Key, x, y, z, face, out var nc, out int ni);
                        LightColumn n = c;
                        if (!nc.Equals(pair.Key) && !region.TryGetValue(nc, out n)) continue;
                        int loss = Math.Max(1, (int)n.Absorption[ni]);
                        bool passage = Connects(c.Occupancy[i], n.Occupancy[ni], face);
                        bool emitter = c.Local[i] != 0 && c.Local[i] == c.Emission[i] && OpenFaces[n.Occupancy[ni], face ^ 1] != 0;
                        if ((passage && c.Sky[i] - loss > n.Sky[ni]) ||
                            ((passage || emitter) && c.Local[i] - loss > n.Local[ni]))
                        { queue.Enqueue((pair.Key, i)); break; }
                    }
                }
            }
            int iterations = 0;
            while (queue.Count != 0)
            {
                if ((++iterations & 4095) == 0 && cancelled?.Invoke() == true) return null;
                var cell = queue.Dequeue();
                var c = region[cell.coord];
                int i = cell.index, x = i & 15, z = (i >> 4) & 15, y = i >> 8;
                for (int face = 0; face < 6; face++)
                {
                    if (y + Dy[face] < 0 || y + Dy[face] >= 128) continue;
                    Neighbor(cell.coord, x, y, z, face, out var nc, out int ni);
                    LightColumn n = c;
                    if (!nc.Equals(cell.coord) && !region.TryGetValue(nc, out n)) continue;
                    bool passage = Connects(c.Occupancy[i], n.Occupancy[ni], face);
                    // An opaque emissive block emits outward, but never transmits another source through itself.
                    bool emitter = c.Local[i] != 0 && c.Local[i] == c.Emission[i];
                    if (!passage && !(emitter && OpenFaces[n.Occupancy[ni], face ^ 1] != 0)) continue;
                    int loss = Math.Max(1, (int)n.Absorption[ni]);
                    int sky = passage ? c.Sky[i] - loss : 0, local = c.Local[i] - loss;
                    if (sky <= n.Sky[ni] && local <= n.Local[ni]) continue;
                    n.Sky[ni] = (byte)Math.Max(n.Sky[ni], sky);
                    n.Local[ni] = (byte)Math.Max(n.Local[ni], local);
                    queue.Enqueue((nc, ni));
                }
            }
            return region;
        }

        private static void Neighbor(ChunkCoord coord, int x, int y, int z, int face, out ChunkCoord next, out int index)
        {
            x += Dx[face]; y += Dy[face]; z += Dz[face];
            next = new ChunkCoord(coord.X + (x >> 4), coord.Z + (z >> 4));
            index = Index(x & 15, y, z & 15);
        }

        public static Vector2 Sample(IReadOnlyDictionary<ChunkCoord, LightColumn> columns, Vector3 position)
        {
            int x = Mathf.FloorToInt(position.x), y = Mathf.FloorToInt(position.y), z = Mathf.FloorToInt(position.z);
            if (y >= 128) return new Vector2(1, 0);
            if (y < 0 || !columns.TryGetValue(new ChunkCoord(x >> 4, z >> 4), out var c)) return Vector2.zero;
            int i = Index(x & 15, y, z & 15);
            int bit = (position.x - x >= .5f ? 1 : 0) + (position.z - z >= .5f ? 2 : 0) + (position.y - y >= .5f ? 4 : 0);
            if ((c.Occupancy[i] & (1 << bit)) != 0) return Vector2.zero;
            return new Vector2(c.Sky[i] / 15f, c.Local[i] / 15f);
        }

        public static Color32[] SampleVertices(IReadOnlyDictionary<ChunkCoord, LightColumn> columns,
            Vector3[] vertices, Vector3[] normals, Vector3 offset)
        {
            var colors = new Color32[vertices.Length];
            for (int i = 0; i < vertices.Length; i++)
            {
                Vector3 n = normals[i];
                Vector3 p = vertices[i] + offset + n * .02f;
                Vector3 u = Mathf.Abs(n.y) > .5f ? Vector3.right : Vector3.up;
                Vector3 v = Vector3.Cross(n, u).normalized;
                Vector2 center = Sample(columns, p), sum = center;
                // Samples stay close to the vertex. A blocked side excludes its diagonal, avoiding
                // the classic bright corner leaking through two touching opaque blocks.
                for (int a = -1; a <= 1; a += 2) for (int b = -1; b <= 1; b += 2)
                {
                    Vector2 sideA = Sample(columns, p + u * (.04f * a));
                    Vector2 sideB = Sample(columns, p + v * (.04f * b));
                    Vector2 corner = Sample(columns, p + u * (.04f * a) + v * (.04f * b));
                    if (sideA == Vector2.zero && sideB == Vector2.zero) corner = Vector2.zero;
                    sum += corner;
                }
                sum /= 5;
                colors[i] = new Color32((byte)Mathf.RoundToInt(sum.x * 255), (byte)Mathf.RoundToInt(sum.y * 255), 0, 255);
            }
            return colors;
        }
    }
}
