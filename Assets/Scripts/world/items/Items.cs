using System.Collections.Generic;
using render.ui;
using world.blocks;
using World.blocks;

namespace world.items
{
    public static class Items
    {
        public static readonly List<Item> ItemList = new();
        public static readonly List<ItemStack> BuildingBlockList = new();
        public static readonly List<ItemStack> NatureBlockList = new();

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

        private static Item RegisterBuildingItem(this Item item)
        {
            BuildingBlockList.Add(ItemStack.CreativeStack(item));
            return item;
        }

        private static Item RegisterNatureItem(this Item item)
        {
            NatureBlockList.Add(ItemStack.CreativeStack(item));
            return item;
        }

        static Items()
        {
            SpriteBaker.PrepareBaking();
            
            Air = new BlockItem(Blocks.Air);
            GrassBlock = new BlockItem(Blocks.GrassBlock).RegisterNatureItem();
            Dirt = new BlockItem(Blocks.Dirt).RegisterNatureItem();
            Stone = new BlockItem(Blocks.Stone).RegisterNatureItem();
            Sand = new BlockItem(Blocks.Sand).RegisterNatureItem();
            OakLog = new BlockItem(Blocks.OakLog).RegisterBuildingItem();
            OakLeave = new BlockItem(Blocks.OakLeave).RegisterNatureItem();
            Gravel = new BlockItem(Blocks.Gravel).RegisterNatureItem();
            WhiteStainedGlass = new BlockItem(Blocks.WhiteStainedGlass).RegisterBuildingItem();
            LightGrayStainedGlass = new BlockItem(Blocks.LightGrayStainedGlass).RegisterBuildingItem();
            GrayStainedGlass = new BlockItem(Blocks.GrayStainedGlass).RegisterBuildingItem();
            BlackStainedGlass = new BlockItem(Blocks.BlackStainedGlass).RegisterBuildingItem();
            BrownStainedGlass = new BlockItem(Blocks.BrownStainedGlass).RegisterBuildingItem();
            RedStainedGlass = new BlockItem(Blocks.RedStainedGlass).RegisterBuildingItem();
            OrangeStainedGlass = new BlockItem(Blocks.OrangeStainedGlass).RegisterBuildingItem();
            YellowStainedGlass = new BlockItem(Blocks.YellowStainedGlass).RegisterBuildingItem();
            LimeStainedGlass = new BlockItem(Blocks.LimeStainedGlass).RegisterBuildingItem();
            GreenStainedGlass = new BlockItem(Blocks.GreenStainedGlass).RegisterBuildingItem();
            CyanStainedGlass = new BlockItem(Blocks.CyanStainedGlass).RegisterBuildingItem();
            LightBlueStainedGlass = new BlockItem(Blocks.LightBlueStainedGlass).RegisterBuildingItem();
            BlueStainedGlass = new BlockItem(Blocks.BlueStainedGlass).RegisterBuildingItem();
            PurpleStainedGlass = new BlockItem(Blocks.PurpleStainedGlass).RegisterBuildingItem();
            MagentaStainedGlass = new BlockItem(Blocks.MagentaStainedGlass).RegisterBuildingItem();
            PinkStainedGlass = new BlockItem(Blocks.PinkStainedGlass).RegisterBuildingItem();
            
            while (BuildingBlockList.Count < 45 || BuildingBlockList.Count % 9 != 0) BuildingBlockList.Add(ItemStack.EmptyStack());
            while (NatureBlockList.Count < 45 || NatureBlockList.Count % 9 != 0) NatureBlockList.Add(ItemStack.EmptyStack());
            
            SpriteBaker.FinalizeBaking();
        }
    }
}