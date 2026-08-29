using UnityEngine;
using world.blocks;
using World.blocks;

namespace World
{
    public interface IBlockProvider
    {
        public BlockState GetBlock(Vector3Int position);

        public BlockState GetBlock(int x, int y, int z);
    }
}