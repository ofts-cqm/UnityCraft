using Render;
using render.ui;
using UnityEngine;
using world.blocks;
using World.blocks;

namespace world.items
{
    public class BlockItem : Item
    {
        private Block Block { get; }

        private readonly int _allLayer = LayerMask.GetMask("Ignore Raycast");

        public BlockItem(int itemId, Block block) : base(itemId)
        {
            Block = block;
            Sprite = SpriteBaker.BakeToSprite(Block);
        }
        
        public override bool OnUse(World.World world, Vector3Int position, int face)
        {
            Vector3Int rawPosition = position;
            Vector3Int finalPosition = OffsetBlock(rawPosition, face);
            
            object data = Block.GetStateToPlace(face, rawPosition, ref finalPosition);
            BlockState posBlock = world.GetBlock(finalPosition);
            
            if ((posBlock.IsAir || posBlock.Block.BlockId == Block.BlockId) && !Block.IsAir && Block.CanPlace(posBlock, data))
            {
                (Vector3 half, Vector3 center) = Block.GetBoundingBox(finalPosition, data);
                if (!Physics.CheckBox(center, half * 0.9f, new Quaternion(), _allLayer))
                {
                    world.SetBlock(finalPosition, Block, data);
                    return true;
                }
            }

            return false;
        }

        public override Sprite Sprite { get; }
    }
}
