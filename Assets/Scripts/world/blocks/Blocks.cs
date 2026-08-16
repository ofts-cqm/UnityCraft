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
        public static readonly Block Gravel;

        static Blocks()
        {
            Air = new Block(BlockProperty.Default(0).SetSolid(false) with { Collide = false });
            Void = new Block(BlockProperty.Default(0));
            GrassBlock = new Block(BlockProperty.Pillar(0, 2, 1));
            Dirt = new Block(BlockProperty.Default(2));
            Stone = new Block(BlockProperty.Default(3));
            Water = new Water();
            Sand = new Block(BlockProperty.Default(5));
            OakLog = new Block(BlockProperty.Pillar(6, 7, 6));
            OakLeave = new Block(BlockProperty.Default(8) with { ReplaceTerrain = false });
            Gravel = new Block(BlockProperty.Default(9));
        }
    }
}