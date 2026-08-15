using System.Collections.Generic;
using UnityEngine;
using World.blocks;

namespace world.blocks
{
    public static class Blocks
    {
        public static readonly List<Block> BlockList = new();

        public static readonly Block Air;
        public static readonly Block Void;
        public static readonly Block GrassBlock;
        public static readonly Block Dirt;
        public static readonly Block Stone;
        public static readonly Block Water;
        public static readonly Block Sand;
        public static readonly Block OakLog;
        public static readonly Block OakLeave;

        static Blocks()
        {
            Air = new SimpleBlock(new Vector2Int(0, 0), false, false);
            Void = new SimpleBlock(new Vector2Int(0, 0));
            GrassBlock = new PillarBlock(
                new Vector2Int(0, 0), 
                new Vector2Int(2, 0), 
                new Vector2Int(1, 0)
            );
            Dirt = new SimpleBlock(new Vector2Int(2, 0));
            Stone = new SimpleBlock(new Vector2Int(3, 0));
            Water = new Water();
            Sand = new SimpleBlock(new Vector2Int(5, 0));
            OakLog = new PillarBlock(
                new Vector2Int(7, 0),
                new Vector2Int(6, 0),
                new Vector2Int(7, 0)
            );
            OakLeave = new SimpleBlock(new Vector2Int(8, 0));
        }
    }
}