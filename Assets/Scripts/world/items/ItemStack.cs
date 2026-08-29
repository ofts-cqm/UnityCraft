using System;
using UnityEngine;

namespace world.items
{
    public struct ItemStack
    {
        public Item Item { get; set; }

        private int _stack;
        public int Stack
        {
            get => _stack;
            private set
            {
                _stack = Infinite ? _stack : Math.Clamp(value, 0, Item.MaxStack);
                if (_stack == 0 && Item.ItemId != Items.Air.ItemId) Item = Items.Air;
            }
        }

        public bool Infinite { get; }

        public ItemStack(Item item, int stack, bool infinite = false)
        {
            Item = item;
            _stack = stack;
            Infinite = infinite;
        }

        public bool IsEmpty => Item.ItemId == Items.Air.ItemId;
        
        public bool OnUse(World.World world, Vector3Int position, int face) => Item.OnUse(world, position, face);
        
        public bool OnDestroy(World.World world, Vector3Int position, int face) => Item.OnDestroy(world, position, face);
        
        public static ItemStack CreativeStack(Item item) => new(item, 1, true);
        public static ItemStack EmptyStack(bool infinite = false) => new(Items.Air, 0, infinite);

        public bool CanStack(ItemStack other)
        {
            if (IsEmpty || other.IsEmpty) return false;
            return Item.ItemId == other.Item.ItemId && Stack < Item.MaxStack;
        }

        public ItemStack StackItem(ItemStack other)
        {
            if (Infinite)
            {
                other.Stack = 0;
                return other;
            }
            int delta = Math.Min(other.Stack, Item.MaxStack - Stack);

            if (Stack == 0) Item = other.Item;
            Stack += delta;
            other.Stack -= delta;
            return other;
        }

        public void Increase() => Stack++;
        
        public void Decrease() => Stack--;

        public ItemStack Half()
        {
            ItemStack newStack = new ItemStack(Item, (Stack + 1) / 2);
            Stack /= 2;
            return newStack;
        }
        
        public ItemStack Copy(){
            return new(Item, Stack);
        }

        public ItemStack Max()
        {
            return new(Item, Item.MaxStack);
        }
    }
}