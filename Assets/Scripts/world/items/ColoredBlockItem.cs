using world.blocks;

namespace world.items
{
    public class ColoredBlockItem
    {
        public readonly Item White;
        public readonly Item LightGray;
        public readonly Item Gray;
        public readonly Item Black;
        public readonly Item Brown;
        public readonly Item Red;
        public readonly Item Orange;
        public readonly Item Yellow;
        public readonly Item Lime;
        public readonly Item Green;
        public readonly Item Cyan;
        public readonly Item LightBlue;
        public readonly Item Blue;
        public readonly Item Purple;
        public readonly Item Magenta;
        public readonly Item Pink;
        
        private static Item Register(Item item)
        {
            ItemStack itemStack = ItemStack.CreativeStack(item);
            Items.AllItemsList.Add(itemStack);
            Items.ColoredBlockList.Add(itemStack);
            return item;
        }

        public ColoredBlockItem(int id, string name, ColoredBlocks blocks)
        {
            White     = Register(new BlockItem(id + 0, $"White {name}", blocks.White));
            LightGray = Register(new BlockItem(id + 1, $"Light Gray {name}", blocks.LightGray));
            Gray      = Register(new BlockItem(id + 2, $"Gray {name}", blocks.Gray));
            Black     = Register(new BlockItem(id + 3, $"Black {name}", blocks.Black));
            Brown     = Register(new BlockItem(id + 4, $"Brown {name}", blocks.Brown));
            Red       = Register(new BlockItem(id + 5, $"Red {name}", blocks.Red));
            Orange    = Register(new BlockItem(id + 6, $"Orange {name}", blocks.Orange));
            Yellow    = Register(new BlockItem(id + 7, $"Yellow {name}", blocks.Yellow));
            Lime      = Register(new BlockItem(id + 8, $"Lime {name}", blocks.Lime));
            Green     = Register(new BlockItem(id + 9, $"Green {name}", blocks.Green));
            Cyan      = Register(new BlockItem(id + 10, $"Cyan {name}", blocks.Cyan));
            LightBlue = Register(new BlockItem(id + 11, $"Light Blue {name}", blocks.LightBlue));
            Blue      = Register(new BlockItem(id + 12, $"Blue {name}", blocks.Blue));
            Purple    = Register(new BlockItem(id + 13, $"Purple {name}", blocks.Purple));
            Magenta   = Register(new BlockItem(id + 14, $"Magenta {name}", blocks.Magenta));
            Pink      = Register(new BlockItem(id + 15, $"Pink {name}", blocks.Pink));
        }
    }
}