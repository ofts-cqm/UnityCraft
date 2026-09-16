using world.blocks;

namespace world.items
{
    public class WoodBlockItem
    {
        public readonly Item Acacia;
        public readonly Item Bamboo;
        public readonly Item Birch;
        public readonly Item Cherry;
        public readonly Item DarkOak;
        public readonly Item Jungle;
        public readonly Item Mangrove;
        public readonly Item Oak;
        public readonly Item PaleOak;
        public readonly Item Spruce;
        
        private static Item Register(Item item)
        {
            ItemStack itemStack = ItemStack.CreativeStack(item);
            Items.AllItemsList.Add(itemStack);
            Items.BuildingBlockList.Add(itemStack);
            return item;
        }

        public WoodBlockItem(int id, string name, WoodBlocks items, int legacyId = -1)
        {
            Acacia    = Register(new BlockItem(id++, $"Acacia {name}", items.Acacia)); 
            Bamboo    = Register(new BlockItem(id++, $"Bamboo {name}", items.Bamboo));
            Birch     = Register(new BlockItem(id++, $"Birch {name}", items.Birch));
            Cherry    = Register(new BlockItem(id++, $"Cherry {name}", items.Cherry));
            DarkOak   = Register(new BlockItem(id++, $"DarkOak {name}", items.DarkOak));
            Jungle    = Register(new BlockItem(id++, $"Jungle {name}", items.Jungle));
            Mangrove  = Register(new BlockItem(id++, $"Mangrove {name}", items.Mangrove));
            Oak       = Register(new BlockItem(legacyId == -1 ? id++ : legacyId, $"Oak {name}", items.Oak));
            PaleOak   = Register(new BlockItem(id++, $"Pale Oak {name}", items.PaleOak));
            Spruce    = Register(new BlockItem(id, $"Spruce {name}", items.Spruce));
        }
    }
}