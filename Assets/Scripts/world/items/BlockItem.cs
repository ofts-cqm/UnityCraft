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

        public BlockItem(Block block)
        {
            Block = block;
            Sprite = SpriteBaker.BakeToSprite(Block);
        }
        
        public override bool OnUse(World.World world, Vector3Int position, int face)
        {
            Vector3Int rawPosition = position;
            Vector3Int finalPosition = face switch
            {
                ChunkRenderObject.TopFace => rawPosition + Vector3Int.up,
                ChunkRenderObject.BottomFace => rawPosition + Vector3Int.down,
                ChunkRenderObject.LeftFace => rawPosition + Vector3Int.left,
                ChunkRenderObject.RightFace => rawPosition + Vector3Int.right,
                ChunkRenderObject.FrontFace => rawPosition + Vector3Int.forward,
                ChunkRenderObject.BackFace => rawPosition + Vector3Int.back,
                _ => rawPosition
            };
            
            object data = Block.GetStateToPlace(face, rawPosition, ref finalPosition);
            BlockState posBlock = world.GetBlock(finalPosition);
            
            if ((posBlock.IsAir || posBlock.Block.BlockId == Block.BlockId) && !Block.IsAir)
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

        public override bool OnDestroy(World.World world, Vector3Int position, int face)
        {
            world.SetBlock(position, Blocks.Air);
            return true;
        }

        public override Sprite Sprite { get; }
    }
}