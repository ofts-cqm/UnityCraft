using UnityEngine;

namespace world.items
{
    public abstract class Item
    {
        private static int _registered;
        public int MaxStack { get; }
        public int ItemId { get; }
        
        protected Item(int maxStack = 64)
        {
            Items.ItemList.Add(this);
            ItemId = _registered++;
            MaxStack = maxStack;
        }
        
        public abstract bool OnUse(World.World world, Vector3Int position, int face);
        
        public abstract bool OnDestroy(World.World world, Vector3Int position, int face);
        
        public abstract Sprite Sprite { get; }
    }
}