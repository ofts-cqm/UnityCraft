using UnityEngine;
using World.blocks;

namespace world.items
{
    public class RawTextureBlockItem: BlockItem
    {
        public RawTextureBlockItem(int itemId, string name, string textureName, Block block) : base(itemId, name, block)
        {
            Sprite = Resources.Load<Sprite>(textureName);
        }

        public override Sprite Sprite { get; }
    }
}