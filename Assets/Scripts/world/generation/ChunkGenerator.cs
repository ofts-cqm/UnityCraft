using UnityEngine;
using World;
using world.blocks;
using World.blocks;

namespace world.generation
{
    public enum NoiseProviderKind { Legacy, Magnified }

    public static class ChunkGenerator
    {
        private static int _seaLevel = 32;
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
            public float[,] Continental { get; }
            public float[,] Temperature { get; }
            public BiomeEnum[,] Biome { get; }

            internal ChunkGenerationContext(ChunkData data, int[,] heightMap, 
                float[,] continental, float[,] temperature, BiomeEnum[,] biome)
            {
                Data = data;
                HeightMap = heightMap;
                Continental = continental;
                Temperature = temperature;
                Biome = biome;
            }
        }

        private static INoiseProvider CurrentProvider = new MagnifiedNoiseProvider();

        /// <summary>
        /// Generates directly into the compact buffers used by the live chunk. No full-volume
        /// reference array or copy/translation pass is created.
        /// </summary>
        public static ChunkData GenerateChunk(ChunkCoord chunk)
        {
            ChunkGenerationContext context = GenerateNoise(chunk.X, chunk.Z);
            GenerateFromHeightMap(context);
            StructureGenerator.GenerateTrees(chunk, context);
            return context.Data;
        }
        
        public static ChunkGenerationContext GenerateNoise(int chunkX, int chunkZ)
        {
            if (!_initialized)
                throw new System.InvalidOperationException("ChunkGenerator must be initialized with the selected world's generation settings.");
            
            int[,] heightMap = new int[Chunk.ChunkSize, Chunk.ChunkSize];
            float[,] continentalMap = new float[Chunk.ChunkSize, Chunk.ChunkSize];
            float[,] temperatureMap = new float[Chunk.ChunkSize, Chunk.ChunkSize];
            BiomeEnum[,] biomeMap = new BiomeEnum[Chunk.ChunkSize, Chunk.ChunkSize];
            int worldX = chunkX * Chunk.ChunkSize;
            int worldZ = chunkZ * Chunk.ChunkSize;

            for (int x = 0; x < Chunk.ChunkSize; x++)
            for (int z = 0; z < Chunk.ChunkSize; z++)
            {
                CurrentProvider.SampleColumn(worldX + x, worldZ + z, out int columnHeight, out BiomeEnum biome,
                    out float continental, out float temperature);
                heightMap[x, z] = columnHeight;
                continentalMap[x, z] = continental;
                temperatureMap[x, z] = temperature;
                biomeMap[x, z] = biome;
            }

            return new ChunkGenerationContext(new ChunkData(), heightMap, continentalMap,
                temperatureMap, biomeMap);
        }

        private static void GenerateFromHeightMap(ChunkGenerationContext context)
        {
            for (int x = 0; x < Chunk.ChunkSize; x++)
            for (int z = 0; z < Chunk.ChunkSize; z++)
            {
                int height = context.HeightMap[x, z];
                int lastWrittenY = Mathf.Min(Chunk.ChunkHeight - 1, Mathf.Max(height, _seaLevel - 1));
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
            if (y < height && y > height - 3 && y > _seaLevel - 3) return Blocks.Gravel;
            if (y < height) return Blocks.Stone;
            if (y == height) return Blocks.Gravel;
            return y < _seaLevel ? Blocks.GenerationWater : Blocks.Air;
        }

        private static Block PlacePlainBlocks(int y, int height)
        {
            if (y < height - 3) return Blocks.Stone;
            if (y < height) return Blocks.Dirt;
            if (y == height) return y < _seaLevel ? Blocks.Sand : Blocks.GrassBlock;
            return y < _seaLevel ? Blocks.GenerationWater : Blocks.Air;
        }

        private static Block PlaceMountainBlocks(int y, int height)
        {
            if (y <= height) return Blocks.Stone;
            return y < _seaLevel ? Blocks.GenerationWater : Blocks.Air;
        }

        private static Block PlaceDesertBlocks(int y, int height)
        {
            if (y < height - 3) return Blocks.Stone;
            if (y <= height) return Blocks.Sand;
            return y < _seaLevel ? Blocks.GenerationWater : Blocks.Air;
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

        public static void Initialize(WorldGenerationSettings settings, NoiseProviderKind provider = NoiseProviderKind.Magnified)
        {
            CurrentProvider = provider switch
            {
                NoiseProviderKind.Legacy => new LegacyNoiseProvider(),
                NoiseProviderKind.Magnified => new MagnifiedNoiseProvider(),
                _ => throw new System.ArgumentOutOfRangeException(nameof(provider))
            };
            _seaLevel = CurrentProvider.GetSeaLevel();
            CurrentProvider.Initialize(settings);
            StructureGenerator.Initialize(settings);
            _initialized = true;
        }
        
        internal static void SampleColumn(int worldX, int worldZ, out int height, out BiomeEnum biome)
        {
            CurrentProvider.SampleColumn(worldX, worldZ, out height, out biome, out _, out _);
        }

        public static void SampleClimate(int worldX, int worldZ, out float continentality, out float temperature)
        {
            if (!_initialized)
                throw new System.InvalidOperationException("ChunkGenerator must be initialized with the selected world's generation settings.");
            CurrentProvider.SampleColumn(worldX, worldZ, out _, out _, out continentality, out temperature);
        }

        private static int ModChunk(int coordinate)
        {
            int result = coordinate % Chunk.ChunkSize;
            return result < 0 ? result + Chunk.ChunkSize : result;
        }
    }
}
