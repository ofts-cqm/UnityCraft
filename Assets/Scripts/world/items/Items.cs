using System.Collections.Generic;
using render.ui;
using world.blocks;
using World.blocks;

namespace world.items
{
    public static class Items
    {
        public static readonly List<Item> ItemList = new();

        public static readonly Item Air;
        public static readonly Item GrassBlock;
        public static readonly Item Dirt;
        public static readonly Item Stone;
        public static readonly Item Sand;
        public static readonly Item OakLog;
        public static readonly Item OakLeave;
        public static readonly Item Gravel;
        public static readonly Item WhiteStainedGlass;
        public static readonly Item LightGrayStainedGlass;
        public static readonly Item GrayStainedGlass;
        public static readonly Item BlackStainedGlass;
        public static readonly Item BrownStainedGlass;
        public static readonly Item RedStainedGlass;
        public static readonly Item OrangeStainedGlass;
        public static readonly Item YellowStainedGlass;
        public static readonly Item LimeStainedGlass;
        public static readonly Item GreenStainedGlass;
        public static readonly Item CyanStainedGlass;
        public static readonly Item LightBlueStainedGlass;
        public static readonly Item BlueStainedGlass;
        public static readonly Item PurpleStainedGlass;
        public static readonly Item MagentaStainedGlass;
        public static readonly Item PinkStainedGlass;

        static Items()
        {
            SpriteBaker.PrepareBaking();
            
            Air = new BlockItem(Blocks.Air);
            GrassBlock = new BlockItem(Blocks.GrassBlock);
            Dirt = new BlockItem(Blocks.Dirt);
            Stone = new BlockItem(Blocks.Stone);
            Sand = new BlockItem(Blocks.Sand);
            OakLog = new BlockItem(Blocks.OakLog);
            OakLeave = new BlockItem(Blocks.OakLeave);
            Gravel = new BlockItem(Blocks.Gravel);
            WhiteStainedGlass = new BlockItem(Blocks.WhiteStainedGlass);
            LightGrayStainedGlass = new BlockItem(Blocks.LightGrayStainedGlass);
            GrayStainedGlass = new BlockItem(Blocks.GrayStainedGlass);
            BlackStainedGlass = new BlockItem(Blocks.BlackStainedGlass);
            BrownStainedGlass = new BlockItem(Blocks.BrownStainedGlass);
            RedStainedGlass = new BlockItem(Blocks.RedStainedGlass);
            OrangeStainedGlass = new BlockItem(Blocks.OrangeStainedGlass);
            YellowStainedGlass = new BlockItem(Blocks.YellowStainedGlass);
            LimeStainedGlass = new BlockItem(Blocks.LimeStainedGlass);
            GreenStainedGlass = new BlockItem(Blocks.GreenStainedGlass);
            CyanStainedGlass = new BlockItem(Blocks.CyanStainedGlass);
            LightBlueStainedGlass = new BlockItem(Blocks.LightBlueStainedGlass);
            BlueStainedGlass = new BlockItem(Blocks.BlueStainedGlass);
            PurpleStainedGlass = new BlockItem(Blocks.PurpleStainedGlass);
            MagentaStainedGlass = new BlockItem(Blocks.MagentaStainedGlass);
            PinkStainedGlass = new BlockItem(Blocks.PinkStainedGlass);
            
            SpriteBaker.FinalizeBaking();
        }
    }
}