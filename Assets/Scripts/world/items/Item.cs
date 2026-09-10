using Render;
using UnityEngine;
using world.blocks;

namespace world.items
{
    public readonly struct ItemUseContext
    {
        public readonly Ray AimRay;
        public readonly float MaxDistance;
        public readonly Vector3Int? BlockPosition;
        public readonly int BlockFace;

        public ItemUseContext(Ray aimRay, float maxDistance, Vector3Int? blockPosition, int blockFace)
        {
            AimRay = aimRay;
            MaxDistance = maxDistance;
            BlockPosition = blockPosition;
            BlockFace = blockFace;
        }
    }

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
        
        public abstract bool OnUse(World.World world, ItemUseContext context);
        
        public bool OnDestroy(World.World world, Vector3Int position, int face)
        {
            world.SetBlock(position, Blocks.Air);
            return true;
        }
        
        public abstract Sprite Sprite { get; }

        protected static Vector3Int OffsetBlock(Vector3Int rawPosition, int face)
        {
            return face switch
            {
                ChunkRenderObject.TopFace => rawPosition + Vector3Int.up,
                ChunkRenderObject.BottomFace => rawPosition + Vector3Int.down,
                ChunkRenderObject.LeftFace => rawPosition + Vector3Int.left,
                ChunkRenderObject.RightFace => rawPosition + Vector3Int.right,
                ChunkRenderObject.FrontFace => rawPosition + Vector3Int.forward,
                ChunkRenderObject.BackFace => rawPosition + Vector3Int.back,
                _ => rawPosition
            };
        }
    }
}
