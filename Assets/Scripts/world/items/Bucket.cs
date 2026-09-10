using UnityEngine;
using World.blocks;

namespace world.items
{
    public class Bucket : Item
    {
        public Bucket(int itemId) : base(itemId)
        {
            Sprite = Resources.Load<Sprite>("items/bucket");
        }

        public override bool OnUse(World.World world, Vector3Int position, int face)
        {
            // raycast to find the collider
            // position = raycast and find position
            FluidState fluidState = world.GetFluid(position);
            
            if (!fluidState.IsSource) return false;
            world.SetFluid(position, FluidState.FromRaw(0));
            return true;
        }

        public override Sprite Sprite { get; }
    }
}