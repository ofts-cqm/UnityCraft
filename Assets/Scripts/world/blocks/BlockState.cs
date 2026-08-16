using UnityEngine;
using World.blocks;

namespace world.blocks
{
    public class BlockState
    {
        public Vector3Int Position { get; set; }
        public Block Block { get; set; }

        public BlockState(int x, int y, int z, Block block)
        {
            Position = new Vector3Int(x, y, z);
            Block = block;
        }
    }
}