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
        public static readonly Block Barrel;

        static Blocks()
        {
            Air = new Block(BlockProperty.Default(0).SetSolid(false) with { Collide = false });
            Void = new Block(BlockProperty.Default(0));
            GrassBlock = new Block(BlockProperty.Pillar(0, 2, 1));
            Dirt = new Block(BlockProperty.Default(2));
            Stone = new Block(BlockProperty.Default(3));
            Water = new Water();
            Sand = new Block(BlockProperty.Default(5));
            OakLog = new Block(BlockProperty.Pillar(7, 7, 6));
            OakLeave = new Block(BlockProperty.Default(8).SetSolid(false) with { ReplaceTerrain = false });
            Gravel = new Block(BlockProperty.Default(9));
            WhiteStainedGlass = new Block(BlockProperty.Default(48).SetTransparent(true));
            LightGrayStainedGlass = new Block(BlockProperty.Default(49).SetTransparent(true));
            GrayStainedGlass = new Block(BlockProperty.Default(50).SetTransparent(true));
            BlackStainedGlass = new Block(BlockProperty.Default(51).SetTransparent(true));
            BrownStainedGlass = new Block(BlockProperty.Default(52).SetTransparent(true));
            RedStainedGlass = new Block(BlockProperty.Default(53).SetTransparent(true));
            OrangeStainedGlass = new Block(BlockProperty.Default(54).SetTransparent(true));
            YellowStainedGlass = new Block(BlockProperty.Default(55).SetTransparent(true));
            LimeStainedGlass = new Block(BlockProperty.Default(56).SetTransparent(true));
            GreenStainedGlass = new Block(BlockProperty.Default(57).SetTransparent(true));
            CyanStainedGlass = new Block(BlockProperty.Default(58).SetTransparent(true));
            LightBlueStainedGlass = new Block(BlockProperty.Default(59).SetTransparent(true));
            BlueStainedGlass = new Block(BlockProperty.Default(60).SetTransparent(true));
            PurpleStainedGlass = new Block(BlockProperty.Default(61).SetTransparent(true));
            MagentaStainedGlass = new Block(BlockProperty.Default(62).SetTransparent(true));
            PinkStainedGlass = new Block(BlockProperty.Default(63).SetTransparent(true));
            Barrel = new Block(BlockProperty.Pillar(13, 15, 14));
        }
    }
}