using World;
using world.blocks;
using World.blocks;

namespace world.generation
{
    public static class StructureGenerator
    {
        private static PerlinNoise _structureNoise;
        private static PerlinNoise _replaceNoise;

        public static void Initialize(WorldGenerationSettings settings)
        {
            _structureNoise = new PerlinNoise(settings.StructureSeed, 0.5f, new[] { 1 });
            _replaceNoise = new PerlinNoise(settings.StructureReplaceSeed, 0.5f, new[] { 1 });
        }

        private static readonly Block[,,] TreeStructure = {
            {
                { Blocks.Void, Blocks.Void, Blocks.Void, Blocks.Void, Blocks.Void },
                { Blocks.Void, Blocks.Void, Blocks.Void, Blocks.Void, Blocks.Void },
                { Blocks.OakLeave, Blocks.OakLeave, Blocks.OakLeave, Blocks.OakLeave, Blocks.OakLeave },
                { Blocks.OakLeave, Blocks.OakLeave, Blocks.OakLeave, Blocks.OakLeave, Blocks.OakLeave },
                { Blocks.Void, Blocks.Void, Blocks.Void, Blocks.Void, Blocks.Void },
                { Blocks.Void, Blocks.Void, Blocks.Void, Blocks.Void, Blocks.Void },
            },
            {
                { Blocks.Void, Blocks.Void, Blocks.Void, Blocks.Void, Blocks.Void },
                { Blocks.Void, Blocks.Void, Blocks.Void, Blocks.Void, Blocks.Void },
                { Blocks.OakLeave, Blocks.OakLeave, Blocks.OakLeave, Blocks.OakLeave, Blocks.OakLeave },
                { Blocks.OakLeave, Blocks.OakLeave, Blocks.OakLeave, Blocks.OakLeave, Blocks.OakLeave },
                { Blocks.Void, Blocks.OakLeave, Blocks.OakLeave, Blocks.OakLeave, Blocks.Void },
                { Blocks.Void, Blocks.Void, Blocks.OakLeave, Blocks.Void, Blocks.Void },
            },
            {
                { Blocks.Void, Blocks.Void, Blocks.OakLog, Blocks.Void, Blocks.Void },
                { Blocks.Void, Blocks.Void, Blocks.OakLog, Blocks.Void, Blocks.Void },
                { Blocks.OakLeave, Blocks.OakLeave, Blocks.OakLog, Blocks.OakLeave, Blocks.OakLeave },
                { Blocks.OakLeave, Blocks.OakLeave, Blocks.OakLog, Blocks.OakLeave, Blocks.OakLeave },
                { Blocks.Void, Blocks.OakLeave, Blocks.OakLog, Blocks.OakLeave, Blocks.Void },
                { Blocks.Void, Blocks.OakLeave, Blocks.OakLeave, Blocks.OakLeave, Blocks.Void },
            },
            {
                { Blocks.Void, Blocks.Void, Blocks.Void, Blocks.Void, Blocks.Void },
                { Blocks.Void, Blocks.Void, Blocks.Void, Blocks.Void, Blocks.Void },
                { Blocks.OakLeave, Blocks.OakLeave, Blocks.OakLeave, Blocks.OakLeave, Blocks.OakLeave },
                { Blocks.OakLeave, Blocks.OakLeave, Blocks.OakLeave, Blocks.OakLeave, Blocks.OakLeave },
                { Blocks.Void, Blocks.OakLeave, Blocks.OakLeave, Blocks.OakLeave, Blocks.Void },
                { Blocks.Void, Blocks.Void, Blocks.OakLeave, Blocks.Void, Blocks.Void },
            },
            {
                { Blocks.Void, Blocks.Void, Blocks.Void, Blocks.Void, Blocks.Void },
                { Blocks.Void, Blocks.Void, Blocks.Void, Blocks.Void, Blocks.Void },
                { Blocks.OakLeave, Blocks.OakLeave, Blocks.OakLeave, Blocks.OakLeave, Blocks.OakLeave },
                { Blocks.OakLeave, Blocks.OakLeave, Blocks.OakLeave, Blocks.OakLeave, Blocks.OakLeave },
                { Blocks.Void, Blocks.Void, Blocks.Void, Blocks.Void, Blocks.Void },
                { Blocks.Void, Blocks.Void, Blocks.Void, Blocks.Void, Blocks.Void },
            }
        };

        private const int TreeHorizontalRadius = 2;

        private static void PlaceStructure(Block[,,] structure, int xStart, int yStart, int zStart,
            ChunkCoord targetCoord, ChunkGenerator.ChunkGenerationContext context, Block replaceBlock)
        {
            int xLength = structure.GetLength(0);
            int yLength = structure.GetLength(1);
            int zLength = structure.GetLength(2);
            
            for (int i = 0; i < xLength; i++)
            {
                for (int j = 0; j < yLength; j++)
                {
                    for (int k = 0; k < zLength; k++)
                    {
                        // conditionally omit block to create randomness
                        // the block must be the targeted replace block and must have at least one face facing air or boundary
                        if (structure[i, j, k].BlockId == replaceBlock.BlockId
                            && (i == 0 || i == xLength - 1 || j == 0 || j == yLength - 1 || k == 0 || k == zLength - 1
                            || structure[i - 1, j, k].IsAirOrVoid || structure[i + 1, j, k].IsAirOrVoid
                            || structure[i, j - 1, k].IsAirOrVoid || structure[i, j + 1, k].IsAirOrVoid
                            || structure[i, j, k - 1].IsAirOrVoid || structure[i, j, k + 1].IsAirOrVoid
                        ))
                        {
                            // use a noise to detect if replace or not
                            // we do not have y so we use a simple xor to randomize and see if last three digit is 001
                            if (_replaceNoise.At(xStart + i, zStart + k) < -0.02f && (((yStart + j) ^ 91) & 7) == 1) continue;
                        }
                        
                        ChunkGenerator.PlaceStructureBlock(
                            new BlockState(xStart + i, yStart + j, zStart + k, structure[i, j, k]),
                            targetCoord, context);
                    }
                }
            }
        }
        
        public static void GenerateTrees(ChunkCoord coord, ChunkGenerator.ChunkGenerationContext context)
        {
            int targetMinX = coord.X * Chunk.ChunkSize;
            int targetMinZ = coord.Z * Chunk.ChunkSize;

            // Evaluate every tree origin capable of intersecting this chunk. This produces the
            // same terrain/structure result regardless of chunk load order and removes all
            // background-thread access to live World/Chunk objects.
            for (int originX = targetMinX - TreeHorizontalRadius;
                 originX < targetMinX + Chunk.ChunkSize + TreeHorizontalRadius;
                 originX++)
            {
                for (int originZ = targetMinZ - TreeHorizontalRadius;
                     originZ < targetMinZ + Chunk.ChunkSize + TreeHorizontalRadius;
                     originZ++)
                {
                    bool localOrigin = originX >= targetMinX && originX < targetMinX + Chunk.ChunkSize &&
                                       originZ >= targetMinZ && originZ < targetMinZ + Chunk.ChunkSize;
                    int height;
                    ChunkGenerator.BiomeEnum biome;
                    if (localOrigin)
                    {
                        int localX = originX - targetMinX;
                        int localZ = originZ - targetMinZ;
                        height = context.HeightMap[localX, localZ];
                        biome = context.Biome[localX, localZ];
                    }
                    else ChunkGenerator.SampleColumn(originX, originZ, out height, out biome);

                    if (biome != ChunkGenerator.BiomeEnum.Forest ||
                        _structureNoise.At(originX, originZ) >= -0.45f) continue;
                    PlaceStructure(TreeStructure, originX - TreeHorizontalRadius, height + 1,
                        originZ - TreeHorizontalRadius, coord, context, Blocks.OakLeave);
                }
            }
        }

        internal static bool TryGetTreeOrigin(int worldX, int worldZ, out int height)
        {
            ChunkGenerator.SampleColumn(worldX, worldZ, out height, out ChunkGenerator.BiomeEnum biome);
            return biome == ChunkGenerator.BiomeEnum.Forest && _structureNoise.At(worldX, worldZ) < -0.45f;
        }
    }
}
