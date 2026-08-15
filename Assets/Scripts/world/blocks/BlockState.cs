using UnityEngine;
using World.blocks;

namespace world.blocks
{
    public class BlockState
    {
        public Vector3Int Position { get; set; }
        public Block Block { get; set; }
    }
}