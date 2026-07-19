using UnityEngine;

namespace World
{
    public static class Blocks
    {
        public static readonly Block Air = new SimpleBlock(new Vector2Int(0, 0), false);
        public static readonly Block GrassBlock = new PillarBlock(
            new Vector2Int(0, 0), 
            new Vector2Int(2, 0), 
            new Vector2Int(1, 0)
        );
        public static readonly Block Dirt = new SimpleBlock(new Vector2Int(2, 0));
        public static readonly Block Stone = new SimpleBlock(new Vector2Int(3, 0));
    }
}