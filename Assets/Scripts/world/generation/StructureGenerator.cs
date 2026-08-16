using World;
using world.blocks;
using World.blocks;

namespace world.generation
{
    public static class StructureGenerator
    {
        private static readonly PerlinNoise StructureNoise = new(12345, 0.5f, new[]{ 1 });
        private static readonly PerlinNoise ReplaceNoise = new(54321, 0.5f, new[]{ 1 });

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

        private static void PlaceStructure(Block[,,] structure, int x, int z, int centerX, int centerZ, ChunkCoord coord,
            ChunkGenerator.ChunkGenerationContext context, Block replaceBlock)
        {
            int xStart = coord.X * Chunk.ChunkSize + x;
            int yStart = context.HeightMap[centerX, centerZ] + 1;
            int zStart = coord.Z * Chunk.ChunkSize + z;
            
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
                            if (ReplaceNoise.At(xStart + i, zStart + k) < -0.02f && (((yStart + j) ^ 91) & 7) == 1) continue;
                        }
                        
                        ChunkGenerator.PlaceStructureBlock(new BlockState(xStart + i, yStart + j, zStart + k, structure[i, j, k]), coord, context);
                    }
                }
            }
        }
        
        public static void GenerateTrees(ChunkCoord coord, ChunkGenerator.ChunkGenerationContext context)
        {
            int xStart = coord.X * Chunk.ChunkSize;
            int zStart = coord.Z * Chunk.ChunkSize;
            for (int i = 0; i < 16; i++)
            {
                for (int j = 0; j < 16; j++)
                {
                    if (context.Biome[i, j] != ChunkGenerator.BiomeEnum.Forest) continue;
                    if (StructureNoise.At(xStart + i, zStart + j) < -0.45f) PlaceStructure(TreeStructure, i - 2, j - 2, i, j, coord, context, Blocks.OakLeave);
                }
            }
        }
    }
}