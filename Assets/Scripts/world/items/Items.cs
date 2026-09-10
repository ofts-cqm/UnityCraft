using System.Collections.Generic;
using System.IO;
using render.ui;
using world.blocks;
using World.blocks;

namespace world.items
{
    public static class Items
    {
        private static readonly Dictionary<int, Item> ItemsById = new();
        public static readonly List<ItemStack> BuildingBlockList = new();
        public static readonly List<ItemStack> NatureBlockList = new();
        public static readonly List<ItemStack> ColoredBlockList = new();
        public static readonly List<ItemStack> ToolItemList = new();

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
        public static readonly Item OakPlanks;
        public static readonly Item OakSlab;
        public static readonly Item WaterBucket;
        public static readonly Item Bucket;

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

        private static Item RegisterColoredItem(this Item item)
        {
            ColoredBlockList.Add(ItemStack.CreativeStack(item));
            return item;
        }

        private static Item RegisterToolItem(this Item item)
        {
            ToolItemList.Add(ItemStack.CreativeStack(item));
            return item;
        }

        private static void FinalizeItemInventory(List<ItemStack> items)
        {
            while (items.Count < 45 || items.Count % 9 != 0) items.Add(ItemStack.EmptyStack(true));
        }

        static Items()
        {
            SpriteBaker.PrepareBaking();
            
            Air = new BlockItem(0, Blocks.Air);
            GrassBlock = new BlockItem(1, Blocks.GrassBlock).RegisterNatureItem();
            Dirt = new BlockItem(2, Blocks.Dirt).RegisterNatureItem();
            Stone = new BlockItem(3, Blocks.Stone).RegisterNatureItem();
            Sand = new BlockItem(4, Blocks.Sand).RegisterNatureItem();
            OakLog = new BlockItem(5, Blocks.OakLog).RegisterBuildingItem();
            OakLeave = new BlockItem(6, Blocks.OakLeave).RegisterNatureItem();
            Gravel = new BlockItem(7, Blocks.Gravel).RegisterNatureItem();
            WhiteStainedGlass = new BlockItem(8, Blocks.WhiteStainedGlass).RegisterColoredItem();
            LightGrayStainedGlass = new BlockItem(9, Blocks.LightGrayStainedGlass).RegisterColoredItem();
            GrayStainedGlass = new BlockItem(10, Blocks.GrayStainedGlass).RegisterColoredItem();
            BlackStainedGlass = new BlockItem(11, Blocks.BlackStainedGlass).RegisterColoredItem();
            BrownStainedGlass = new BlockItem(12, Blocks.BrownStainedGlass).RegisterColoredItem();
            RedStainedGlass = new BlockItem(13, Blocks.RedStainedGlass).RegisterColoredItem();
            OrangeStainedGlass = new BlockItem(14, Blocks.OrangeStainedGlass).RegisterColoredItem();
            YellowStainedGlass = new BlockItem(15, Blocks.YellowStainedGlass).RegisterColoredItem();
            LimeStainedGlass = new BlockItem(16, Blocks.LimeStainedGlass).RegisterColoredItem();
            GreenStainedGlass = new BlockItem(17, Blocks.GreenStainedGlass).RegisterColoredItem();
            CyanStainedGlass = new BlockItem(18, Blocks.CyanStainedGlass).RegisterColoredItem();
            LightBlueStainedGlass = new BlockItem(19, Blocks.LightBlueStainedGlass).RegisterColoredItem();
            BlueStainedGlass = new BlockItem(20, Blocks.BlueStainedGlass).RegisterColoredItem();
            PurpleStainedGlass = new BlockItem(21, Blocks.PurpleStainedGlass).RegisterColoredItem();
            MagentaStainedGlass = new BlockItem(22, Blocks.MagentaStainedGlass).RegisterColoredItem();
            PinkStainedGlass = new BlockItem(23, Blocks.PinkStainedGlass).RegisterColoredItem();
            OakPlanks = new BlockItem(24, Blocks.OakPlanks).RegisterBuildingItem();
            OakSlab = new BlockItem(25, Blocks.OakSlab).RegisterBuildingItem();
            WaterBucket = new WaterBucket(26).RegisterToolItem();
            Bucket = new Bucket(27).RegisterToolItem();
            
            FinalizeItemInventory(NatureBlockList);
            FinalizeItemInventory(BuildingBlockList);
            FinalizeItemInventory(ColoredBlockList);
            FinalizeItemInventory(ToolItemList);
            
            SpriteBaker.FinalizeBaking();
        }

        internal static void Register(Item item)
        {
            if (!ItemsById.TryAdd(item.ItemId, item)) throw new InvalidDataException($"Duplicate item ID {item.ItemId}.");
        }

        public static bool TryGetById(int itemId, out Item item)
        {
            _ = Air; // Ensure the registry's static constructor has completed.
            return ItemsById.TryGetValue(itemId, out item);
        }
    }
}
