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
        
        public override bool OnUse(World.World world, ItemUseContext context)
        {
            if (!context.BlockPosition.HasValue) return false;

            Vector3Int rawPosition = context.BlockPosition.Value;
            Vector3Int finalPosition = OffsetBlock(rawPosition, context.BlockFace);
            
            object data = Block.GetStateToPlace(context.BlockFace, rawPosition, ref finalPosition);
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
