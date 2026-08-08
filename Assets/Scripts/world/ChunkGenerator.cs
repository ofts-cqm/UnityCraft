using System;
using UnityEngine;

namespace World
{
    public static class ChunkGenerator
    {
        private const float FirstLevelFrequency = 0.001f;//0.01f;
        private const float SecondLevelFrequency = 0.002f;//0.02f;
        private const float FeatureFrequency = 0.05f;
        private const float Root = 1/5f;

        private static float LevelNoise(int x, int z, float frequency)
        {
            float noise = Mathf.PerlinNoise(x * frequency, z * frequency) / 2 +
                Mathf.PerlinNoise(x * frequency * 2, z * frequency * 2) / 2 - 0.5f;
            return (noise > 0 ? Mathf.Pow(noise, Root) : -Mathf.Pow(-noise, Root)) + 0.5f;
        }
        
        public static float[,] GetHeightMap(int x, int z)
        {
            float[,] heightMap = new float[Chunk.ChunkSize, Chunk.ChunkSize];
            x *= 16;
            z *= 16;

            for (int i = 0; i < Chunk.ChunkSize; i++)
            {
                for (int j = 0; j < Chunk.ChunkSize; j++)
                {
                    float firstLevel = LevelNoise(x + i, z + j, FirstLevelFrequency) / 2;
                    float secondLevel = LevelNoise(x + i, z + j, SecondLevelFrequency) / 4;
                    float preliminaryHeight = firstLevel + secondLevel;
                    float featureLevel = Mathf.PerlinNoise((x + i) * FeatureFrequency, (z + j) * FeatureFrequency);
                    featureLevel *= Mathf.Clamp(preliminaryHeight, 0, 1) * 0.3f;
                    heightMap[i, j] = preliminaryHeight + featureLevel;
                }
            }
            
            return heightMap;
        }
        
        public static Block[,,] GenerateFromHeightMap(float[,] heightMap)
        {
            Block[,,] blocks = new Block[Chunk.ChunkSize, Chunk.ChunkHeight, Chunk.ChunkSize];
            for (int i = 0; i < Chunk.ChunkSize; i++)
            {
                for (int k = 0; k < Chunk.ChunkSize; k++)
                {
                    int height = (int)(heightMap[i, k] * 64) + 32;
                    for (int j = 0; j < Chunk.ChunkHeight; j++)
                    {
                        if (j < height - 3) blocks[i, j, k] = Blocks.Stone;
                        else if (j < height) blocks[i, j, k] = Blocks.Dirt;
                        else if (j == height) blocks[i, j, k] = Blocks.GrassBlock;
                        else if (j < 64 && height < 64)  blocks[i, j, k] = Blocks.Water;
                        else blocks[i, j, k] = Blocks.Air;
                    }
                }
            }
            return blocks;
        }
    }
}