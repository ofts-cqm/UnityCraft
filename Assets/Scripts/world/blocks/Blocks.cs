using System.Collections.Generic;
using UnityEngine;
using World.blocks;

namespace world.blocks
{
    public static class Blocks
    {
        public static readonly List<Block> BlockList = new();

        public static readonly Block Air;
        public static readonly Block Void;
        public static readonly Block GrassBlock;
        public static readonly Block Dirt;
        public static readonly Block Stone;
        public static readonly Block Water;
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
            Air = new Block(BlockProperty.Default(0).SetSolid(false) with { Collide = false });
            Void = new Block(BlockProperty.Default(0));
            GrassBlock = new Block(BlockProperty.Pillar(3, 0, 2));
            Dirt = new Block(BlockProperty.Default(0));
            Stone = new Block(BlockProperty.Default(7));
            Water = new Water();
            Sand = new Block(BlockProperty.Default(5));
            OakLog = new Block(BlockProperty.Pillar(88, 88, 89));
            OakLeave = new Block(BlockProperty.Default(108).SetSolid(false) with { ReplaceTerrain = false, Transparent = false, IsSolid = false });
            Gravel = new Block(BlockProperty.Default(4));
            WhiteStainedGlass = new Block(BlockProperty.Default(16).SetTransparent(true));
            LightGrayStainedGlass = new Block(BlockProperty.Default(17).SetTransparent(true));
            GrayStainedGlass = new Block(BlockProperty.Default(18).SetTransparent(true));
            BlackStainedGlass = new Block(BlockProperty.Default(19).SetTransparent(true));
            BrownStainedGlass = new Block(BlockProperty.Default(20).SetTransparent(true));
            RedStainedGlass = new Block(BlockProperty.Default(21).SetTransparent(true));
            OrangeStainedGlass = new Block(BlockProperty.Default(22).SetTransparent(true));
            YellowStainedGlass = new Block(BlockProperty.Default(23).SetTransparent(true));
            LimeStainedGlass = new Block(BlockProperty.Default(24).SetTransparent(true));
            GreenStainedGlass = new Block(BlockProperty.Default(25).SetTransparent(true));
            CyanStainedGlass = new Block(BlockProperty.Default(26).SetTransparent(true));
            LightBlueStainedGlass = new Block(BlockProperty.Default(27).SetTransparent(true));
            BlueStainedGlass = new Block(BlockProperty.Default(28).SetTransparent(true));
            PurpleStainedGlass = new Block(BlockProperty.Default(29).SetTransparent(true));
            MagentaStainedGlass = new Block(BlockProperty.Default(30).SetTransparent(true));
            PinkStainedGlass = new Block(BlockProperty.Default(31).SetTransparent(true));
            OakPlanks = new Block(BlockProperty.Default(90));
            OakSlab = new Slab();
        }
    }
}