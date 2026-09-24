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
        public static readonly WoodBlockItem Log;
        public static readonly Item OakLeave;
        public static readonly Item Gravel;
        public static readonly ColoredBlockItem StainedGlass;
        public static readonly WoodBlockItem Planks;
        public static readonly WoodBlockItem WoodSlab;
        public static readonly WoodBlockItem WoodStairs;
        public static readonly Item WaterBucket;
        public static readonly Item Bucket;
        public static readonly Item SearchIcon;
        public static readonly Item BackpackIcon;
        public static readonly Item Torch;

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
            Log = new WoodBlockItem(38, "Log", Blocks.Log, 5);
            OakLeave = new BlockItem(6, "Oak Leave", Blocks.OakLeave).RegisterNatureItem();
            Gravel = new BlockItem(7, "Gravel", Blocks.Gravel).RegisterNatureItem();
            StainedGlass = new ColoredBlockItem(8, "Stained Glass", Blocks.StainedGlass);
            Planks = new WoodBlockItem(47, "Planks", Blocks.Planks, 24);
            WoodSlab = new WoodBlockItem(56, "Slab", Blocks.WoodSlab, 25);
            WoodStairs = new WoodBlockItem(29, "Stairs", Blocks.WoodStairs, 28);
            WaterBucket = new WaterBucket(26).RegisterToolItem();
            Bucket = new Bucket(27).RegisterToolItem();
            SearchIcon = new SimpleItem(-1, "", "items/search");
            BackpackIcon = new SimpleItem(-1, "", "items/backpack");
            Torch = new BlockItem(65, "Torch", Blocks.Torch).RegisterToolItem();
            
            FinalizeItemInventory(NatureBlockList);
            FinalizeItemInventory(BuildingBlockList);
            FinalizeItemInventory(ColoredBlockList);
            FinalizeItemInventory(ToolItemList);
            
            SpriteBaker.FinalizeBaking();
        }

        internal static void Register(Item item)
        {
            if (!ItemsById.TryAdd(item.ItemId, item)) 
                throw new InvalidDataException($"Duplicate item ID {item.ItemId} when adding {item.Name} (previous instance is {ItemsById[item.ItemId].Name})");
        }

        public static bool TryGetById(int itemId, out Item item)
        {
            _ = Air; // Ensure the registry's static constructor has completed.
            return ItemsById.TryGetValue(itemId, out item);
        }
    }
}
