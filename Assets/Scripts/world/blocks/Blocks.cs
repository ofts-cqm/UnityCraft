using System.Collections.Generic;
using UnityEngine;
using World.blocks;

namespace world.blocks
{
    public static class Blocks
    {
        public static readonly List<Block> BlockList = new();
        
        public static readonly Block Air = new SimpleBlock(new Vector2Int(0, 0), false, false);
        public static readonly Block Void = new SimpleBlock(new Vector2Int(0, 0));
        public static readonly Block GrassBlock = new PillarBlock(
            new Vector2Int(0, 0), 
            new Vector2Int(2, 0), 
            new Vector2Int(1, 0)
        );
        public static readonly Block Dirt = new SimpleBlock(new Vector2Int(2, 0));
        public static readonly Block Stone = new SimpleBlock(new Vector2Int(3, 0));
        public static readonly Block Water = new Water();
    }
}