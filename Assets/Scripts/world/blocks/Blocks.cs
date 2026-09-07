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

        public static readonly Block Air;
        public static readonly Block Void;
        public static readonly Block GrassBlock;
        public static readonly Block Dirt;
        public static readonly Block Stone;
        // Generation-only marker; Chunk converts it to source fluid before the chunk is visible.
        internal static readonly Block GenerationWater;
        public static readonly Block Sand;
        public static readonly Block OakLog;
        public static readonly Block OakLeave;
        public static readonly Block Gravel;
        public static readonly Block WhiteStainedGlass;
        public static readonly Block LightGrayStainedGlass;
        public static readonly Block GrayStainedGlass;
        public static readonly Block BlackStainedGlass;
        public static readonly Block BrownStainedGlass;
        public static readonly Block RedStainedGlass;
        public static readonly Block OrangeStainedGlass;
        public static readonly Block YellowStainedGlass;
        public static readonly Block LimeStainedGlass;
        public static readonly Block GreenStainedGlass;
        public static readonly Block CyanStainedGlass;
        public static readonly Block LightBlueStainedGlass;
        public static readonly Block BlueStainedGlass;
        public static readonly Block PurpleStainedGlass;
        public static readonly Block MagentaStainedGlass;
        public static readonly Block PinkStainedGlass;
        public static readonly Block OakPlanks;
        public static readonly Block OakSlab;

        static Blocks()
        {
            Air = new AirBlock();
            Void = new Block(1, BlockProperty.Default(0));
            GrassBlock = new Block(2, BlockProperty.Pillar(3, 0, 2));
            Dirt = new Block(3, BlockProperty.Default(0));
            Stone = new Block(4, BlockProperty.Default(7));
            GenerationWater = new Block(5, BlockProperty.Default(32).SetSolid(false) with { Collide = false });
            Sand = new Block(6, BlockProperty.Default(5));
            OakLog = new Log(7);
            OakLeave = new Block(8, BlockProperty.Default(108) with { ReplaceTerrain = false, IsSolid = false });
            Gravel = new Block(9, BlockProperty.Default(4));
            WhiteStainedGlass = new Block(10, BlockProperty.Default(16).SetTransparent(true));
            LightGrayStainedGlass = new Block(11, BlockProperty.Default(17).SetTransparent(true));
            GrayStainedGlass = new Block(12, BlockProperty.Default(18).SetTransparent(true));
            BlackStainedGlass = new Block(13, BlockProperty.Default(19).SetTransparent(true));
            BrownStainedGlass = new Block(14, BlockProperty.Default(20).SetTransparent(true));
            RedStainedGlass = new Block(15, BlockProperty.Default(21).SetTransparent(true));
            OrangeStainedGlass = new Block(16, BlockProperty.Default(22).SetTransparent(true));
            YellowStainedGlass = new Block(17, BlockProperty.Default(23).SetTransparent(true));
            LimeStainedGlass = new Block(18, BlockProperty.Default(24).SetTransparent(true));
            GreenStainedGlass = new Block(19, BlockProperty.Default(25).SetTransparent(true));
            CyanStainedGlass = new Block(20, BlockProperty.Default(26).SetTransparent(true));
            LightBlueStainedGlass = new Block(21, BlockProperty.Default(27).SetTransparent(true));
            BlueStainedGlass = new Block(22, BlockProperty.Default(28).SetTransparent(true));
            PurpleStainedGlass = new Block(23, BlockProperty.Default(29).SetTransparent(true));
            MagentaStainedGlass = new Block(24, BlockProperty.Default(30).SetTransparent(true));
            PinkStainedGlass = new Block(25, BlockProperty.Default(31).SetTransparent(true));
            OakPlanks = new Block(26, BlockProperty.Default(90));
            OakSlab = new Slab(27);
        }

        internal static void Register(Block block)
        {
            if (!BlocksById.TryAdd(block.BlockId, block)) throw new InvalidDataException($"Duplicate block ID {block.BlockId}.");
            BlockList.Add(block);
        }

        public static bool TryGetById(int blockId, out Block block)
        {
            _ = Air; // Ensure the registry's static constructor has completed.
            return BlocksById.TryGetValue(blockId, out block);
        }
    }
}
