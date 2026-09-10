using UnityEngine;
using World.blocks;

namespace world.items
{
    public class WaterBucket : Item
    {
        public WaterBucket(int id) : base(id)
        {
            Sprite = Resources.Load<Sprite>("items/water_bucket");
        }

        public override bool OnUse(World.World world, ItemUseContext context)
        {
            if (!context.BlockPosition.HasValue) return false;

            Vector3Int rawPosition = context.BlockPosition.Value;
            Vector3Int finalPosition = OffsetBlock(rawPosition, context.BlockFace);
            
            world.SetFluid(finalPosition, FluidState.Source);
            return true;
        }

        public override Sprite Sprite { get; }
    }
}
