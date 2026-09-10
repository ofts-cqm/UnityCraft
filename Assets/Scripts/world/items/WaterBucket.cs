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

        public override bool OnUse(World.World world, Vector3Int position, int face)
        {
            Vector3Int rawPosition = position;
            Vector3Int finalPosition = OffsetBlock(rawPosition, face);
            
            world.SetFluid(finalPosition, FluidState.Source);
            return true;
        }

        public override Sprite Sprite { get; }
    }
}