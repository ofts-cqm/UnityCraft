using System.Collections.Generic;
using UnityEngine;
using world.blocks;
using World.blocks;

namespace World
{
    public static class ChunkGenerator
    {
        private const float FirstLevelFrequency = 0.001f;//0.01f;
        private const float SecondLevelFrequency = 0.002f;//0.02f;
        private const float FeatureFrequency = 0.01f;
        private const float TemperatureFrequency = 0.002f;
        private const float Root = 1/5f;
        private const int SeaLevel = 64;

        private static readonly PerlinNoise ContinentalNoise = new(114514, FirstLevelFrequency, new[]{ 3, 1, 0, 0, 1 });
        private static readonly PerlinNoise HeightNoise = new(1919810, SecondLevelFrequency, new[] { 3, 1, 0, 0, 1 });
        private static readonly PerlinNoise FeatureNoise = new(6767, FeatureFrequency, new[] { 1, 1, 4, 2 });
        private static readonly PerlinNoise TemperatureNoise = new(7676, TemperatureFrequency, new[] { 5, 3, 0, 0, 1, 1, 1 });
        
        private static readonly Dictionary<ChunkCoord, List<BlockState>> _outOfBoundsStates = new();

        private enum BiomeEnum
        {
            Ocean,
            Plain,
            Desert,
            Mountain,
            Forest
        }
        
        private record ChunkGenerationContext(Block[,,] Blocks, float[,] Height, float[,] Continental, float[,] Temperature, BiomeEnum[,] Biome);

        private static float BiasNoise(float noise)
        {
            return noise > 0 ? Mathf.Pow(noise, Root) : -Mathf.Pow(-noise, Root);
        }

        public static Block[,,] GenerateChunk(ChunkCoord chunk)
        {
            ChunkGenerationContext context = GenerateNoise(chunk.X, chunk.Z);
            GenerateFromHeightMap(context);
            PlaceOutOfBoundBlocks(context.Blocks, chunk);
            return context.Blocks;
        }
        
        private static ChunkGenerationContext GenerateNoise(int x, int z)
        {
            float[,] heightMap = new float[Chunk.ChunkSize, Chunk.ChunkSize];
            float[,] levelMap =  new float[Chunk.ChunkSize, Chunk.ChunkSize];
            float[,] temperatureMap = new float[Chunk.ChunkSize, Chunk.ChunkSize];
            x *= 16;
            z *= 16;

            for (int i = 0; i < Chunk.ChunkSize; i++)
            {
                for (int j = 0; j < Chunk.ChunkSize; j++)
                {
                    float firstLevel = BiasNoise(ContinentalNoise.At(x + i, z + j)) / 2;
                    float secondLevel = BiasNoise(HeightNoise.At(x + i, z + j)) / 4;
                    float preliminaryHeight = firstLevel + secondLevel;
                    float featureLevel = FeatureNoise.At(x + i, z + j);
                    featureLevel *= Mathf.Clamp(preliminaryHeight / 2 + 0.5f, 0, 1) * 0.25f;

                    levelMap[i, j] = preliminaryHeight;
                    heightMap[i, j] = preliminaryHeight + featureLevel;
                    
                    temperatureMap[i, j] = TemperatureNoise.At(x + i, z + j);
                }
            }
            
            return new ChunkGenerationContext(
                new Block[Chunk.ChunkSize, Chunk.ChunkHeight, Chunk.ChunkSize], 
                heightMap, 
                levelMap, 
                temperatureMap, 
                new BiomeEnum[Chunk.ChunkSize, Chunk.ChunkSize]
            );
        }
        
        private static void GenerateFromHeightMap(ChunkGenerationContext context)
        {
            float[,] heightMap = context.Height;
            Block[,,] blocks = context.Blocks;
            BiomeEnum[,] biomeMap = context.Biome;
            
            for (int i = 0; i < Chunk.ChunkSize; i++)
            {
                for (int k = 0; k < Chunk.ChunkSize; k++)
                {
                    if (context.Continental[i, k] < 0) biomeMap[i, k] = BiomeEnum.Ocean;
                    else if (context.Continental[i, k] < 0.5f)
                        biomeMap[i, k] = context.Temperature[i, k] > 0 ? BiomeEnum.Plain : BiomeEnum.Desert;
                    else biomeMap[i, k] = context.Temperature[i, k] > 0 ? BiomeEnum.Forest : BiomeEnum.Mountain;
                    
                    int height = (int)(heightMap[i, k] * 40) + 60;
                    for (int j = 0; j < Chunk.ChunkHeight; j++)
                    {
                        blocks[i, j, k] = biomeMap[i, k] switch
                        {
                            BiomeEnum.Forest => PlacePlainBlocks(j, height),
                            BiomeEnum.Desert=> PlaceDesertBlocks(j, height),
                            BiomeEnum.Mountain => PlaceMountainBlocks(j, height),
                            BiomeEnum.Ocean => PlaceOceanBlocks(j, height),
                            _ => PlacePlainBlocks(j, height)
                        };
                    }
                }
            }
        }

        private static Block PlaceOceanBlocks(int y, int height)
        {
            if (y < height && y > height - 3 && y > SeaLevel - 3) return Blocks.Sand;
            if (y < height) return Blocks.Stone;
            if (y == height) return Blocks.Sand;
            return y < SeaLevel ? Blocks.Water : Blocks.Air;
        }

        private static Block PlacePlainBlocks(int y, int height)
        {
            if (y < height - 3) return Blocks.Stone;
            if (y < height) return Blocks.Dirt;
            if (y == height) return Blocks.GrassBlock;
            return y < SeaLevel ? Blocks.Water : Blocks.Air;
        }

        private static Block PlaceMountainBlocks(int y, int height)
        {
            if (y <= height) return Blocks.Stone;
            return y < SeaLevel ? Blocks.Water : Blocks.Air;
        }

        private static Block PlaceDesertBlocks(int y, int height)
        {
            if (y < height - 3) return Blocks.Stone;
            if (y <= height) return Blocks.Sand;
            return y < SeaLevel ? Blocks.Water : Blocks.Air;
        }

        private static void PlaceOutOfBoundBlocks(Block[,,] blocks, ChunkCoord chunk)
        {
            foreach (BlockState blockState in _outOfBoundsStates.GetValueOrDefault(chunk, new List<BlockState>()))
            {
                int x = blockState.Position.x % Chunk.ChunkSize;
                if (x < 0) x += Chunk.ChunkSize;
                int z =  blockState.Position.z % Chunk.ChunkSize;
                if (z < 0) z += Chunk.ChunkSize;
                blocks[x, blockState.Position.y, z] = blockState.Block;
            }
        }
    }
}