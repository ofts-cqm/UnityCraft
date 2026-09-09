using UnityEngine;
using World;
using world.blocks;
using World.blocks;

namespace world.generation
{
    public static class ChunkGenerator
    {
        private const float FirstLevelFrequency = 0.001f;
        private const float SecondLevelFrequency = 0.002f;
        private const float FeatureFrequency = 0.01f;
        private const float TemperatureFrequency = 0.002f;
        private const float Root = 1 / 5f;
        internal const int SeaLevel = 64;

        private static PerlinNoise _continentalNoise;
        private static PerlinNoise _heightNoise;
        private static PerlinNoise _featureNoise;
        private static PerlinNoise _temperatureNoise;
        private static bool _initialized;

        public enum BiomeEnum
        {
            Ocean,
            Plain,
            Desert,
            Mountain,
            Forest
        }

        public sealed class ChunkGenerationContext
        {
            public ChunkData Data { get; }
            public int[,] HeightMap { get; }
            public float[,] Height { get; }
            public float[,] Continental { get; }
            public float[,] Temperature { get; }
            public BiomeEnum[,] Biome { get; }

            internal ChunkGenerationContext(ChunkData data, int[,] heightMap, float[,] height,
                float[,] continental, float[,] temperature, BiomeEnum[,] biome)
            {
                Data = data;
                HeightMap = heightMap;
                Height = height;
                Continental = continental;
                Temperature = temperature;
                Biome = biome;
            }
        }

        public static void Initialize(WorldGenerationSettings settings)
        {
            _continentalNoise = new PerlinNoise(settings.ContinentalSeed, FirstLevelFrequency, new[] { 3, 1, 0, 0, 1 });
            _heightNoise = new PerlinNoise(settings.HeightSeed, SecondLevelFrequency, new[] { 3, 1, 0, 0, 1 });
            _featureNoise = new PerlinNoise(settings.FeatureSeed, FeatureFrequency, new[] { 1, 1, 4, 2 });
            _temperatureNoise = new PerlinNoise(settings.TemperatureSeed, TemperatureFrequency, new[] { 5, 3, 0, 0, 1, 1, 1 });
            StructureGenerator.Initialize(settings);
            _initialized = true;
        }

        private static float BiasNoise(float noise)
        {
            return noise > 0 ? Mathf.Pow(noise, Root) : -Mathf.Pow(-noise, Root);
        }

        /// <summary>
        /// Generates directly into the compact buffers used by the live chunk. No full-volume
        /// reference array or copy/translation pass is created.
        /// </summary>
        public static ChunkData GenerateChunk(ChunkCoord chunk)
        {
            if (!_initialized)
                throw new System.InvalidOperationException("ChunkGenerator must be initialized with the selected world's generation settings.");

            ChunkGenerationContext context = GenerateNoise(chunk.X, chunk.Z);
            GenerateFromHeightMap(context);
            StructureGenerator.GenerateTrees(chunk, context);
            return context.Data;
        }

        private static ChunkGenerationContext GenerateNoise(int chunkX, int chunkZ)
        {
            float[,] height = new float[Chunk.ChunkSize, Chunk.ChunkSize];
            int[,] heightMap = new int[Chunk.ChunkSize, Chunk.ChunkSize];
            float[,] continentalMap = new float[Chunk.ChunkSize, Chunk.ChunkSize];
            float[,] temperatureMap = new float[Chunk.ChunkSize, Chunk.ChunkSize];
            BiomeEnum[,] biomeMap = new BiomeEnum[Chunk.ChunkSize, Chunk.ChunkSize];
            int worldX = chunkX * Chunk.ChunkSize;
            int worldZ = chunkZ * Chunk.ChunkSize;

            for (int x = 0; x < Chunk.ChunkSize; x++)
            for (int z = 0; z < Chunk.ChunkSize; z++)
            {
                SampleColumn(worldX + x, worldZ + z, out int columnHeight, out BiomeEnum biome,
                    out float preciseHeight, out float continental, out float temperature);
                height[x, z] = preciseHeight;
                heightMap[x, z] = columnHeight;
                continentalMap[x, z] = continental;
                temperatureMap[x, z] = temperature;
                biomeMap[x, z] = biome;
            }

            return new ChunkGenerationContext(new ChunkData(), heightMap, height, continentalMap,
                temperatureMap, biomeMap);
        }

        internal static void SampleColumn(int worldX, int worldZ, out int height, out BiomeEnum biome)
        {
            SampleColumn(worldX, worldZ, out height, out biome, out _, out _, out _);
        }

        private static void SampleColumn(int worldX, int worldZ, out int height, out BiomeEnum biome,
            out float preciseHeight, out float continental, out float temperature)
        {
            float firstLevel = BiasNoise(_continentalNoise.At(worldX, worldZ)) / 2;
            float secondLevel = BiasNoise(_heightNoise.At(worldX, worldZ)) / 4;
            continental = firstLevel + secondLevel;
            float featureLevel = _featureNoise.At(worldX, worldZ);
            featureLevel *= Mathf.Clamp(continental / 2 + 0.5f, 0, 1) * 0.25f;
            preciseHeight = continental + featureLevel;
            temperature = _temperatureNoise.At(worldX, worldZ);
            height = (int)(preciseHeight * 40) + 60;
            biome = continental < 0
                ? BiomeEnum.Ocean
                : continental < 0.5f
                    ? temperature > 0 ? BiomeEnum.Plain : BiomeEnum.Desert
                    : temperature > 0 ? BiomeEnum.Forest : BiomeEnum.Mountain;
        }

        private static void GenerateFromHeightMap(ChunkGenerationContext context)
        {
            for (int x = 0; x < Chunk.ChunkSize; x++)
            for (int z = 0; z < Chunk.ChunkSize; z++)
            {
                int height = context.HeightMap[x, z];
                int lastWrittenY = Mathf.Min(Chunk.ChunkHeight - 1, Mathf.Max(height, SeaLevel - 1));
                for (int y = 0; y <= lastWrittenY; y++)
                {
                    Block block = context.Biome[x, z] switch
                    {
                        BiomeEnum.Forest => PlacePlainBlocks(y, height),
                        BiomeEnum.Desert => PlaceDesertBlocks(y, height),
                        BiomeEnum.Mountain => PlaceMountainBlocks(y, height),
                        BiomeEnum.Ocean => PlaceOceanBlocks(y, height),
                        _ => PlacePlainBlocks(y, height)
                    };

                    if (block.BlockId == Blocks.GenerationWater.BlockId)
                        context.Data.SetFluidRaw(x, y, z, FluidState.Source.RawAmount);
                    else if (!block.IsAir)
                        context.Data.SetBlock(x, y, z, block, block.EncodeStateCompact(block.DefaultState));
                }
            }
        }

        private static Block PlaceOceanBlocks(int y, int height)
        {
            if (y < height && y > height - 3 && y > SeaLevel - 3) return Blocks.Gravel;
            if (y < height) return Blocks.Stone;
            if (y == height) return Blocks.Gravel;
            return y < SeaLevel ? Blocks.GenerationWater : Blocks.Air;
        }

        private static Block PlacePlainBlocks(int y, int height)
        {
            if (y < height - 3) return Blocks.Stone;
            if (y < height) return Blocks.Dirt;
            if (y == height) return Blocks.GrassBlock;
            return y < SeaLevel ? Blocks.GenerationWater : Blocks.Air;
        }

        private static Block PlaceMountainBlocks(int y, int height)
        {
            if (y <= height) return Blocks.Stone;
            return y < SeaLevel ? Blocks.GenerationWater : Blocks.Air;
        }

        private static Block PlaceDesertBlocks(int y, int height)
        {
            if (y < height - 3) return Blocks.Stone;
            if (y <= height) return Blocks.Sand;
            return y < SeaLevel ? Blocks.GenerationWater : Blocks.Air;
        }

        internal static void PlaceStructureBlock(BlockState blockState, ChunkCoord targetCoord,
            ChunkGenerationContext context)
        {
            if (blockState.Block.BlockId == Blocks.Void.BlockId || blockState.Position.y < 0 ||
                blockState.Position.y >= Chunk.ChunkHeight) return;
            if (!ChunkCoord.ToChunkCoord(blockState.Position.x, blockState.Position.z).Equals(targetCoord)) return;

            int x = ModChunk(blockState.Position.x);
            int z = ModChunk(blockState.Position.z);
            Block existing = Blocks.GetByCompactId(context.Data.GetBlockId(x, blockState.Position.y, z));
            bool generatedWater = existing.IsAir && context.Data.GetFluidRaw(x, blockState.Position.y, z) != 0;
            if ((!existing.IsAir || generatedWater) && !blockState.Block.ReplaceTerrain) return;
            context.Data.SetBlock(x, blockState.Position.y, z, blockState.Block,
                blockState.Block.EncodeStateCompact(blockState.Data));
            if (generatedWater) context.Data.SetFluidRaw(x, blockState.Position.y, z, 0);
        }

        private static int ModChunk(int coordinate)
        {
            int result = coordinate % Chunk.ChunkSize;
            return result < 0 ? result + Chunk.ChunkSize : result;
        }
    }
}
