using JetBrains.Annotations;
using UnityEngine;
using World.blocks;

namespace world.blocks
{
    public record BlockState(Vector3Int Position, Block Block, object Data)
    {
        public BlockState(int x, int y, int z, Block block, [CanBeNull] object data = null)
        : this(new Vector3Int(x, y, z), block, data ?? block.DefaultState)
        {
        }
        public bool IsAir => Block.IsAir;
    }
}