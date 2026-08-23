using UnityEngine;

namespace world.items
{
    public abstract class Item
    {
        private static int _registered;
        public int ItemId { get; init; }
        
        protected Item()
        {
            Items.ItemList.Add(this);
            ItemId = _registered++;
        }
        
        public abstract bool OnUse(World.World world, Vector3Int position, int face);
        
        public abstract bool OnDestroy(World.World world, Vector3Int position, int face);
        
        public abstract Sprite Sprite { get; }
    }
}