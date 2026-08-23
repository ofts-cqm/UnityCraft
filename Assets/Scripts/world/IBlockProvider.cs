using UnityEngine;
using World.blocks;

namespace World
{
    public interface IBlockProvider
    {
        public Block GetBlock(Vector3Int position);

        public Block GetBlock(int x, int y, int z);
    }
}