using UnityEngine;

namespace world.items
{
    public abstract class Item
    {
        public int MaxStack { get; }
        public int ItemId { get; }
        
        protected Item(int itemId, int maxStack = 64)
        {
            ItemId = itemId;
            MaxStack = maxStack;
            Items.Register(this);
        }
        
        public abstract bool OnUse(World.World world, Vector3Int position, int face);
        
        public abstract bool OnDestroy(World.World world, Vector3Int position, int face);
        
        public abstract Sprite Sprite { get; }
    }
}
