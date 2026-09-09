using System;
using System.Diagnostics;
using Render;
using world.blocks;
using World.blocks;

namespace World
{
    /// <summary>
    /// Compact, section-contiguous storage for one chunk. The three cell buffers consume exactly
    /// 160 KiB (ushort block + ushort state + byte fluid per cell). All writes go through this
    /// type so section occupancy and border metadata cannot drift from the cell data.
    /// </summary>
    public sealed class ChunkData
    {
        public const int SectionCount = Chunk.ChunkHeight / Chunk.ChunkSize;
        public const int CellsPerSection = Chunk.ChunkSize * Chunk.ChunkSize * Chunk.ChunkSize;
        public const int CellCount = Chunk.ChunkSize * Chunk.ChunkHeight * Chunk.ChunkSize;
        public const int CellPayloadBytes = CellCount * (sizeof(ushort) + sizeof(ushort) + sizeof(byte));

        private const int FaceCount = 6;
        private const int BorderWordsPerFace = 4; // 16 * 16 bits

        private readonly ushort[] _blockIds = new ushort[CellCount];
        private readonly ushort[] _stateIds = new ushort[CellCount];
        private readonly byte[] _fluidAmounts = new byte[CellCount];

        private readonly ushort[] _nonAirCounts = new ushort[SectionCount];
        private readonly ushort[] _collidableCounts = new ushort[SectionCount];
        private readonly ushort[] _opaqueCounts = new ushort[SectionCount];
        private readonly ushort[] _transparentCounts = new ushort[SectionCount];
        private readonly ushort[] _fluidCounts = new ushort[SectionCount];
        private readonly ushort[] _flowingFluidCounts = new ushort[SectionCount];
        private readonly ulong[] _nonAirBorders = new ulong[SectionCount * FaceCount * BorderWordsPerFace];
        private readonly ulong[] _fluidBorders = new ulong[SectionCount * FaceCount * BorderWordsPerFace];

        /// <summary>
        /// Converts local coordinates to the live-memory layout. Each 16-high section occupies one
        /// contiguous 4096-cell range, unlike the legacy save-file order in ChunkSnapshot.Index.
        /// </summary>
        public static int Index(int x, int y, int z)
        {
            if ((uint)x >= Chunk.ChunkSize || (uint)y >= Chunk.ChunkHeight || (uint)z >= Chunk.ChunkSize)
                throw new ArgumentOutOfRangeException($"Chunk-local cell ({x}, {y}, {z}) is outside the chunk.");
            return IndexUnchecked(x, y, z);
        }

        internal static int IndexUnchecked(int x, int y, int z)
        {
            int section = y >> 4;
            int localY = y & (Chunk.ChunkSize - 1);
            return section * CellsPerSection + localY * Chunk.ChunkSize * Chunk.ChunkSize +
                   z * Chunk.ChunkSize + x;
        }

        public ushort GetBlockId(int x, int y, int z) => _blockIds[Index(x, y, z)];
        public ushort GetStateId(int x, int y, int z) => _stateIds[Index(x, y, z)];
        public byte GetFluidRaw(int x, int y, int z) => _fluidAmounts[Index(x, y, z)];

        internal ushort GetBlockIdUnchecked(int x, int y, int z) => _blockIds[IndexUnchecked(x, y, z)];
        internal ushort GetStateIdUnchecked(int x, int y, int z) => _stateIds[IndexUnchecked(x, y, z)];
        internal byte GetFluidRawUnchecked(int x, int y, int z) => _fluidAmounts[IndexUnchecked(x, y, z)];

        internal void GetCellIdsUnchecked(int x, int y, int z, out ushort blockId, out ushort stateId)
        {
            int index = IndexUnchecked(x, y, z);
            blockId = _blockIds[index];
            stateId = _stateIds[index];
        }

        public ushort NonAirCount(int section) => _nonAirCounts[CheckedSection(section)];
        public ushort CollidableCount(int section) => _collidableCounts[CheckedSection(section)];
        public ushort OpaqueCount(int section) => _opaqueCounts[CheckedSection(section)];
        public ushort TransparentCount(int section) => _transparentCounts[CheckedSection(section)];
        public ushort FluidCount(int section) => _fluidCounts[CheckedSection(section)];
        public ushort FlowingFluidCount(int section) => _flowingFluidCounts[CheckedSection(section)];

        public ulong GetNonAirBorderWord(int section, int face, int word) =>
            _nonAirBorders[BorderIndex(CheckedSection(section), face, word)];

        public ulong GetFluidBorderWord(int section, int face, int word) =>
            _fluidBorders[BorderIndex(CheckedSection(section), face, word)];

        internal void SetBlock(int x, int y, int z, Block block, int stateId)
        {
            if ((uint)block.BlockId > ushort.MaxValue)
                throw new InvalidOperationException($"Block ID {block.BlockId} cannot be stored in compact chunk data.");
            if ((uint)stateId > ushort.MaxValue)
                throw new InvalidOperationException($"State ID {stateId} cannot be stored in compact chunk data.");

            int index = IndexUnchecked(x, y, z);
            ushort previousId = _blockIds[index];
            Block previous = Blocks.GetByCompactId(previousId);
            UpdateBlockMetadata(x, y, z, previous, block);
            _blockIds[index] = (ushort)block.BlockId;
            _stateIds[index] = (ushort)stateId;
        }

        internal void SetFluidRaw(int x, int y, int z, byte rawAmount)
        {
            int index = IndexUnchecked(x, y, z);
            byte previous = _fluidAmounts[index];
            if (previous == rawAmount) return;

            int section = y >> 4;
            if (previous == 0 && rawAmount != 0) _fluidCounts[section]++;
            else if (previous != 0 && rawAmount == 0) _fluidCounts[section]--;
            bool previousFlowing = previous != 0 && previous != FluidState.Source.RawAmount;
            bool nextFlowing = rawAmount != 0 && rawAmount != FluidState.Source.RawAmount;
            UpdateCount(_flowingFluidCounts, section, previousFlowing, nextFlowing);

            if (previous == 0 || rawAmount == 0)
                UpdateBorderBits(_fluidBorders, x, y, z, rawAmount != 0);
            _fluidAmounts[index] = rawAmount;
        }

        private void UpdateBlockMetadata(int x, int y, int z, Block previous, Block next)
        {
            int section = y >> 4;
            bool previousNonAir = !previous.IsAir;
            bool nextNonAir = !next.IsAir;
            UpdateCount(_nonAirCounts, section, previousNonAir, nextNonAir);
            UpdateCount(_collidableCounts, section, previousNonAir && previous.Collide, nextNonAir && next.Collide);
            UpdateCount(_opaqueCounts, section, previousNonAir && !previous.Transparent,
                nextNonAir && !next.Transparent);
            UpdateCount(_transparentCounts, section, previousNonAir && previous.Transparent,
                nextNonAir && next.Transparent);
            if (previousNonAir != nextNonAir) UpdateBorderBits(_nonAirBorders, x, y, z, nextNonAir);
        }

        private static void UpdateCount(ushort[] counts, int section, bool previous, bool next)
        {
            if (previous == next) return;
            if (next) counts[section]++;
            else counts[section]--;
        }

        private static void UpdateBorderBits(ulong[] borders, int x, int y, int z, bool set)
        {
            int section = y >> 4;
            int localY = y & (Chunk.ChunkSize - 1);
            if (x == 0) SetBorderBit(borders, section, ChunkRenderObject.LeftFace, localY * 16 + z, set);
            if (x == Chunk.ChunkSize - 1) SetBorderBit(borders, section, ChunkRenderObject.RightFace, localY * 16 + z, set);
            if (z == Chunk.ChunkSize - 1) SetBorderBit(borders, section, ChunkRenderObject.FrontFace, localY * 16 + x, set);
            if (z == 0) SetBorderBit(borders, section, ChunkRenderObject.BackFace, localY * 16 + x, set);
            if (localY == Chunk.ChunkSize - 1) SetBorderBit(borders, section, ChunkRenderObject.TopFace, z * 16 + x, set);
            if (localY == 0) SetBorderBit(borders, section, ChunkRenderObject.BottomFace, z * 16 + x, set);
        }

        private static void SetBorderBit(ulong[] borders, int section, int face, int bit, bool set)
        {
            int index = BorderIndex(section, face, bit >> 6);
            ulong mask = 1UL << (bit & 63);
            if (set) borders[index] |= mask;
            else borders[index] &= ~mask;
        }

        private static int BorderIndex(int section, int face, int word)
        {
            if ((uint)face >= FaceCount) throw new ArgumentOutOfRangeException(nameof(face));
            if ((uint)word >= BorderWordsPerFace) throw new ArgumentOutOfRangeException(nameof(word));
            return (section * FaceCount + face) * BorderWordsPerFace + word;
        }

        private static int CheckedSection(int section)
        {
            if ((uint)section >= SectionCount) throw new ArgumentOutOfRangeException(nameof(section));
            return section;
        }

        [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
        public void ValidateMetadata()
        {
            ushort[] nonAir = new ushort[SectionCount];
            ushort[] collidable = new ushort[SectionCount];
            ushort[] opaque = new ushort[SectionCount];
            ushort[] transparent = new ushort[SectionCount];
            ushort[] fluid = new ushort[SectionCount];
            ushort[] flowingFluid = new ushort[SectionCount];
            ulong[] nonAirBorders = new ulong[_nonAirBorders.Length];
            ulong[] fluidBorders = new ulong[_fluidBorders.Length];

            for (int y = 0; y < Chunk.ChunkHeight; y++)
            for (int z = 0; z < Chunk.ChunkSize; z++)
            for (int x = 0; x < Chunk.ChunkSize; x++)
            {
                int index = IndexUnchecked(x, y, z);
                int section = y >> 4;
                Block block = Blocks.GetByCompactId(_blockIds[index]);
                if (!block.IsAir)
                {
                    nonAir[section]++;
                    if (block.Collide) collidable[section]++;
                    if (block.Transparent) transparent[section]++;
                    else opaque[section]++;
                    UpdateBorderBits(nonAirBorders, x, y, z, true);
                }
                if (_fluidAmounts[index] != 0)
                {
                    fluid[section]++;
                    if (_fluidAmounts[index] != FluidState.Source.RawAmount) flowingFluid[section]++;
                    UpdateBorderBits(fluidBorders, x, y, z, true);
                }
            }

            if (!Equal(nonAir, _nonAirCounts) || !Equal(collidable, _collidableCounts) ||
                !Equal(opaque, _opaqueCounts) || !Equal(transparent, _transparentCounts) ||
                !Equal(fluid, _fluidCounts) || !Equal(flowingFluid, _flowingFluidCounts) ||
                !Equal(nonAirBorders, _nonAirBorders) ||
                !Equal(fluidBorders, _fluidBorders))
                throw new InvalidOperationException("Chunk occupancy or border metadata does not match its cell data.");
        }

        private static bool Equal<T>(T[] left, T[] right) where T : IEquatable<T>
        {
            if (left.Length != right.Length) return false;
            for (int i = 0; i < left.Length; i++) if (!left[i].Equals(right[i])) return false;
            return true;
        }
    }
}
