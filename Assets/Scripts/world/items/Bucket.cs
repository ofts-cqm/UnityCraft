using UnityEngine;
using render;
using World.blocks;

namespace world.items
{
    public class Bucket : Item
    {
        public Bucket(int itemId) : base(itemId)
        {
            Sprite = Resources.Load<Sprite>("items/bucket");
        }

        public override bool OnUse(World.World world, ItemUseContext context)
        {
            // Physics raycasts do not report a collider containing their origin, so a camera already
            // inside a source collects that cell directly. Flowing cells intentionally fall through.
            Vector3Int position = Vector3Int.FloorToInt(context.AimRay.origin);
            if (!world.GetFluid(position).IsSource)
            {
                if (!Physics.Raycast(context.AimRay, out RaycastHit hit, context.MaxDistance,
                        LayerMask.GetMask("Water"),
                        QueryTriggerInteraction.Ignore) ||
                    !hit.collider.TryGetComponent(out WaterSourceColliderProperty colliderProperty))
                    return false;

                position = colliderProperty.RenderObject.GetWaterSourcePositionOfTriangle(hit.triangleIndex);
            }

            // The render queue may not have rebuilt a removed source's collider yet.
            if (!world.GetFluid(position).IsSource) return false;
            world.SetFluid(position, default);
            return true;
        }

        public override Sprite Sprite { get; }
    }
}
