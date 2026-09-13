using render.ui;
using UnityEngine;
using world.blocks;
using World.blocks;

namespace world.items
{
    public class BlockItem : Item
    {
        private Block Block { get; }

        private readonly int _playerLayer = LayerMask.GetMask("Ignore Raycast");

        public BlockItem(int itemId, string name, Block block) : base(itemId, name)
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
                Vector3 placementHalfExtents = half * 0.9f;
                if (!Physics.CheckBox(center, placementHalfExtents, Quaternion.identity, _playerLayer) &&
                    !world.IntersectsFallingBlock(center, placementHalfExtents))
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
