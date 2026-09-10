using UnityEngine;

namespace world.items
{
    public class SimpleItem : Item
    {
        public SimpleItem(int itemId, string name, string sprite, int maxStack = 64) : base(itemId, name, maxStack)
        {
            Sprite = Resources.Load<Sprite>(sprite);
        }

        public override bool OnUse(World.World world, ItemUseContext context)
        {
            return false;
        }

        public override Sprite Sprite { get; }
    }
}