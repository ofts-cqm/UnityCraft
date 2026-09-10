using System.Collections.Generic;
using System.IO;
using render.ui;
using world.blocks;

namespace world.items
{
    public static class Items
    {
        private static readonly Dictionary<int, Item> ItemsById = new();
        public static readonly List<ItemStack> BuildingBlockList = new();
        public static readonly List<ItemStack> NatureBlockList = new();
        public static readonly List<ItemStack> ColoredBlockList = new();
        public static readonly List<ItemStack> ToolItemList = new();
        public static readonly SortedSet<ItemStack> AllItemsList = new();

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
        public static readonly Item SearchIcon;
        public static readonly Item BackpackIcon;

        private static Item RegisterBuildingItem(this Item item)
        {
            ItemStack itemStack = ItemStack.CreativeStack(item);
            AllItemsList.Add(itemStack);
            BuildingBlockList.Add(itemStack);
            return item;
        }

        private static Item RegisterNatureItem(this Item item)
        {
            ItemStack itemStack = ItemStack.CreativeStack(item);
            AllItemsList.Add(itemStack);
            NatureBlockList.Add(itemStack);
            return item;
        }

        private static Item RegisterColoredItem(this Item item)
        {
            ItemStack itemStack = ItemStack.CreativeStack(item);
            AllItemsList.Add(itemStack);
            ColoredBlockList.Add(itemStack);
            return item;
        }

        private static Item RegisterToolItem(this Item item)
        {
            ItemStack itemStack = ItemStack.CreativeStack(item);
            AllItemsList.Add(itemStack);
            ToolItemList.Add(itemStack);
            return item;
        }

        private static void FinalizeItemInventory(List<ItemStack> items)
        {
            while (items.Count < 45 || items.Count % 9 != 0) items.Add(ItemStack.EmptyStack(true));
        }

        static Items()
        {
            SpriteBaker.PrepareBaking();
            
            Air = new BlockItem(0, "", Blocks.Air);
            GrassBlock = new BlockItem(1, "Grass Block", Blocks.GrassBlock).RegisterNatureItem();
            Dirt = new BlockItem(2, "Dirt", Blocks.Dirt).RegisterNatureItem();
            Stone = new BlockItem(3, "Stone", Blocks.Stone).RegisterNatureItem();
            Sand = new BlockItem(4, "Sand", Blocks.Sand).RegisterNatureItem();
            OakLog = new BlockItem(5, "Oak Log", Blocks.OakLog).RegisterBuildingItem();
            OakLeave = new BlockItem(6, "Oak Leave", Blocks.OakLeave).RegisterNatureItem();
            Gravel = new BlockItem(7, "Gravel", Blocks.Gravel).RegisterNatureItem();
            WhiteStainedGlass = new BlockItem(8, "White Stained Glass", Blocks.WhiteStainedGlass).RegisterColoredItem();
            LightGrayStainedGlass = new BlockItem(9, "Light Gray Stained Glass", Blocks.LightGrayStainedGlass).RegisterColoredItem();
            GrayStainedGlass = new BlockItem(10, "Gray Stained Glass", Blocks.GrayStainedGlass).RegisterColoredItem();
            BlackStainedGlass = new BlockItem(11, "Black Stained Glass", Blocks.BlackStainedGlass).RegisterColoredItem();
            BrownStainedGlass = new BlockItem(12, "Brown Stained Glass", Blocks.BrownStainedGlass).RegisterColoredItem();
            RedStainedGlass = new BlockItem(13, "Red Stained Glass", Blocks.RedStainedGlass).RegisterColoredItem();
            OrangeStainedGlass = new BlockItem(14, "Orange Stained Glass", Blocks.OrangeStainedGlass).RegisterColoredItem();
            YellowStainedGlass = new BlockItem(15, "Yellow Stained Glass", Blocks.YellowStainedGlass).RegisterColoredItem();
            LimeStainedGlass = new BlockItem(16, "Lime Stained Glass", Blocks.LimeStainedGlass).RegisterColoredItem();
            GreenStainedGlass = new BlockItem(17, "Green Stained Glass", Blocks.GreenStainedGlass).RegisterColoredItem();
            CyanStainedGlass = new BlockItem(18, "Cyan Stained Glass", Blocks.CyanStainedGlass).RegisterColoredItem();
            LightBlueStainedGlass = new BlockItem(19, "Light Blue Stained Glass", Blocks.LightBlueStainedGlass).RegisterColoredItem();
            BlueStainedGlass = new BlockItem(20, "Blue Stained Glass", Blocks.BlueStainedGlass).RegisterColoredItem();
            PurpleStainedGlass = new BlockItem(21, "Purple Stained Glass", Blocks.PurpleStainedGlass).RegisterColoredItem();
            MagentaStainedGlass = new BlockItem(22, "Magenta Stained Glass", Blocks.MagentaStainedGlass).RegisterColoredItem();
            PinkStainedGlass = new BlockItem(23, "Pink Stained Glass", Blocks.PinkStainedGlass).RegisterColoredItem();
            OakPlanks = new BlockItem(24, "Oak Planks", Blocks.OakPlanks).RegisterBuildingItem();
            OakSlab = new BlockItem(25, "Oak Slab", Blocks.OakSlab).RegisterBuildingItem();
            WaterBucket = new WaterBucket(26).RegisterToolItem();
            Bucket = new Bucket(27).RegisterToolItem();
            SearchIcon = new SimpleItem(-1, "", "items/search");
            BackpackIcon = new SimpleItem(-1, "", "items/backpack");
            
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
