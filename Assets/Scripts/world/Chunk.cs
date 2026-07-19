using System;
using UnityEngine;
using Render;

namespace World
{
    public class Chunk : MonoBehaviour
    {
        public MeshFilter meshFilter;
        public MeshRenderer meshRenderer;
        
        public const int ChunkSize = 16;
        public const int ChunkHeight = 128;
        
        private readonly ChunkRenderObject _renderObject = new();
        private Block[,,] _blockData = new Block[ChunkSize, ChunkHeight, ChunkSize];

        public Block GetBlock(Vector3Int position) => GetBlock(position.x, position.y, position.z);
        
        public Block GetBlock(int x, int y, int z)
        {
            if (x < 0 || y < 0 || z < 0 || x >= ChunkSize || y >= ChunkHeight || z >= ChunkSize) return Blocks.Air;
            return _blockData[x, y, z] ?? Blocks.Air;
        }

        public void ForEachBlock(Action<Block, Vector3Int> action)
        {
            for (int i = 0; i < ChunkSize; i++)
            {
                for (int j = 0; j < ChunkHeight; j++)
                {
                    for (int k = 0; k < ChunkSize; k++)
                    {
                        action.Invoke(GetBlock(i, j, k), new Vector3Int(i, j, k));
                    }
                }
            }
        }

        private void initializeBlockList()
        {
            for (int i = 0; i < ChunkSize; i++)
            {
                for (int j = 0; j < ChunkHeight; j++)
                {
                    for (int k = 0; k < ChunkSize; k++)
                    {
                        _blockData[i, j, k] = Blocks.Air;
                    }
                }
            }
        }

        // Start is called once before the first execution of Update after the MonoBehaviour is created
        void Start()
        {
            initializeBlockList();
            
            _blockData[0, 0, 0] = Blocks.Stone;
            _blockData[0, 0, 1] = Blocks.Dirt;
            _blockData[0, 0, 2] = Blocks.Stone;
            _blockData[1, 0, 0] = Blocks.Dirt;
            _blockData[1, 0, 1] = Blocks.GrassBlock;
            _blockData[1, 0, 2] = Blocks.Dirt;
            _blockData[2, 0, 0] = Blocks.Stone;
            _blockData[2, 0, 1] = Blocks.Dirt;
            _blockData[2, 0, 2] = Blocks.Stone;
            _blockData[3, 0, 3] = Blocks.GrassBlock;
            
            meshFilter.mesh = _renderObject.LoadChunk(this);
        }
    }
}