using System;
using System.Collections.Generic;
using System.IO;
using World.blocks;

namespace world.blocks
{
    public static class Blocks
    {
        private record AirBlock() : Block(0, BlockProperty.Default(0).SetSolid(false) with { Collide = false })
        {
            public override (int max, int min) GetFlowingAmountLimit(BlockState state, int face) => (10, 0);
        }

        public static readonly List<Block> BlockList = new();
        private static readonly Dictionary<int, Block> BlocksById = new();
        private static Block[] _blocksByCompactId = new Block[32];

        public static readonly Block Air;
        public static readonly Block Void;
        public static readonly Block GrassBlock;
        public static readonly Block Dirt;
        public static readonly Block Stone;
        // Generation-only marker; Chunk converts it to source fluid before the chunk is visible.
        internal static readonly Block GenerationWater;
        public static readonly Block Sand;
        public static readonly WoodBlocks Log;
        public static readonly Block OakLeave;
        public static readonly Block Gravel;
        public static readonly ColoredBlocks StainedGlass;
        public static readonly WoodBlocks Planks;
        public static readonly WoodBlocks WoodSlab;
        public static readonly WoodBlocks WoodStairs;
        public static readonly Torch Torch;

        static Blocks()
        {
            Air = new AirBlock();
            Void = new Block(1, BlockProperty.Default(0));
            GrassBlock = new GrassBlock(2, BlockProperty.Pillar(3, 0, 2));
            Dirt = new DirtBlock(3, BlockProperty.Default(0));
            Stone = new Block(4, BlockProperty.Default(7));
            GenerationWater = new Block(5, BlockProperty.Default(32).SetSolid(false) with { Collide = false });
            Sand = new GravityBlock(6, BlockProperty.Default(5));
            Log = new WoodBlocks(38, new Log(38, BlockProperty.Pillar(64, 64, 65)), 7);
            OakLeave = new LeavesBlock(8, BlockProperty.Default(108) with { ReplaceTerrain = false, IsSolid = false });
            Gravel = new GravityBlock(9, BlockProperty.Default(4));
            StainedGlass = new ColoredBlocks(10, new BatchableBlock(10, BlockProperty.Default(16).SetTransparent(true)));
            Planks = new WoodBlocks(47, new BatchableBlock(47, BlockProperty.Default(66)), 26);
            WoodSlab = new WoodBlocks(56, new Slab(56, BlockProperty.Default(66).SetSolid(false)), 27);
            WoodStairs = new WoodBlocks(29, new Stair(29, 66), 28);
            Torch = new Torch(65);
        }

        internal static void Register(Block block)
        {
            if ((uint)block.BlockId > ushort.MaxValue)
                throw new InvalidDataException($"Block ID {block.BlockId} exceeds the compact chunk format limit.");
            if (!BlocksById.TryAdd(block.BlockId, block)) 
                throw new InvalidDataException($"Duplicate block ID {block.BlockId}.");
            if (block.BlockId >= _blocksByCompactId.Length)
            {
                int length = _blocksByCompactId.Length;
                while (length <= block.BlockId) length *= 2;
                Array.Resize(ref _blocksByCompactId, length);
            }
            _blocksByCompactId[block.BlockId] = block;
            BlockList.Add(block);
        }

        internal static Block GetByCompactId(ushort blockId)
        {
            _ = Air;
            if (blockId >= _blocksByCompactId.Length || _blocksByCompactId[blockId] == null)
                throw new InvalidDataException($"Unknown compact block ID {blockId}.");
            return _blocksByCompactId[blockId];
        }

        public static bool TryGetById(int blockId, out Block block)
        {
            _ = Air; // Ensure the registry's static constructor has completed.
            return BlocksById.TryGetValue(blockId, out block);
        }
    }
}
