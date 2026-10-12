using World;
using world.blocks;
using World.blocks;

namespace world.generation
{
    public static class StructureGenerator
    {
        private static PerlinNoise _structureNoise;
        private static PerlinNoise _replaceNoise;
        private static int _structureSeed;

        public static void Initialize(WorldGenerationSettings settings)
        {
            _structureSeed = settings.StructureSeed;
            _structureNoise = new PerlinNoise(settings.StructureSeed, 0.5f, new[] { 1 });
            _replaceNoise = new PerlinNoise(settings.StructureReplaceSeed, 0.5f, new[] { 1 });
        }

        private static readonly Block[,,] OakTreeStructure = {
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
                { Blocks.Void, Blocks.Void, Blocks.Log.Oak, Blocks.Void, Blocks.Void },
                { Blocks.Void, Blocks.Void, Blocks.Log.Oak, Blocks.Void, Blocks.Void },
                { Blocks.OakLeave, Blocks.OakLeave, Blocks.Log.Oak, Blocks.OakLeave, Blocks.OakLeave },
                { Blocks.OakLeave, Blocks.OakLeave, Blocks.Log.Oak, Blocks.OakLeave, Blocks.OakLeave },
                { Blocks.Void, Blocks.OakLeave, Blocks.Log.Oak, Blocks.OakLeave, Blocks.Void },
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

        private static readonly Block[,,] BirchTreeStructure = CreateBirch();
        // Profiles run from top to bottom. Even widths keep the crown centered on a 2x2 trunk.
        private static readonly Block[,,] SpruceTreeStructure = CreateLayeredTree(
            Blocks.Log.Spruce, Blocks.SpruceLeaves,
            new[] { 2, 4, 4, 2, 4, 6, 2, 6, 8, 2, 8, 10, 2, 10, 12, 10, 6, 2, 2, 2, 2, 2 }, 2);
        private static readonly Block[,,] JungleTreeStructure = CreateLayeredTree(
            Blocks.Log.Jungle, Blocks.JungleLeaves,
            new[] { 6, 10, 12, 12, 6, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2 }, 1);
        private static readonly Block[][,,] AcaciaTreeStructures =
            { CreateAcacia(0), CreateAcacia(1), CreateAcacia(2), CreateAcacia(3) };

        private static Block[,,] EmptyStructure(int width, int height)
        {
            var result = new Block[width, height, width];
            for (int x = 0; x < width; x++)
            for (int y = 0; y < height; y++)
            for (int z = 0; z < width; z++) result[x, y, z] = Blocks.Void;
            return result;
        }

        private static Block[,,] CreateBirch()
        {
            var result = EmptyStructure(5, 8);
            result[2, 0, 2] = result[2, 1, 2] = Blocks.Log.Birch;
            for (int x = 0; x < 5; x++)
            for (int y = 0; y < 6; y++)
            for (int z = 0; z < 5; z++)
            {
                Block block = OakTreeStructure[x, y, z];
                result[x, y + 2, z] = block == Blocks.Log.Oak ? Blocks.Log.Birch :
                    block == Blocks.OakLeave ? Blocks.BirchLeaves : block;
            }
            return result;
        }

        private static Block[,,] CreateLayeredTree(Block log, Block leaves, int[] widths, int leafOnlyTopLayers)
        {
            var result = EmptyStructure(12, widths.Length);
            for (int row = 0; row < widths.Length; row++)
            {
                int y = widths.Length - 1 - row;
                int start = (12 - widths[row]) / 2;
                // A width of two below the tip denotes bare trunk in the supplied profile.
                if (widths[row] > 2 || row < leafOnlyTopLayers)
                    for (int x = start; x < start + widths[row]; x++)
                    for (int z = start; z < start + widths[row]; z++) result[x, y, z] = leaves;
                if (row >= leafOnlyTopLayers)
                    for (int x = 5; x <= 6; x++)
                    for (int z = 5; z <= 6; z++) result[x, y, z] = log;
            }
            return result;
        }

        private static Block[,,] CreateAcacia(int direction)
        {
            var result = EmptyStructure(11, 7);
            int dx = direction == 0 ? 1 : direction == 1 ? -1 : 0;
            int dz = direction == 2 ? 1 : direction == 3 ? -1 : 0;
            for (int y = 0; y < 6; y++)
            {
                int offset = y < 3 ? 0 : y == 3 ? 1 : 2;
                result[5 + dx * offset, y, 5 + dz * offset] = Blocks.Log.Acacia;
            }
            for (int y = 5; y <= 6; y++)
            {
                int radius = y == 5 ? 3 : 1;
                for (int x = -radius; x <= radius; x++)
                for (int z = -radius; z <= radius; z++)
                {
                    if (y == 5 && x == 0 && z == 0) continue;
                    result[5 + dx * 2 + x, y, 5 + dz * 2 + z] = Blocks.AcaciaLeaves;
                }
            }
            return result;
        }

        private const int MaximumTreeReach = 6;

        private const int OakTreeHorizontalRadius = 2;

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
            for (int originX = targetMinX - MaximumTreeReach;
                 originX < targetMinX + Chunk.ChunkSize + MaximumTreeReach;
                 originX++)
            {
                for (int originZ = targetMinZ - MaximumTreeReach;
                     originZ < targetMinZ + Chunk.ChunkSize + MaximumTreeReach;
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

                    if (!TrySelectTree(originX, originZ, height, biome, out Block[,,] structure,
                            out Block leaves, out int offset)) continue;
                    PlaceStructure(structure, originX - offset, height + 1,
                        originZ - offset, coord, context, leaves);
                }
            }
        }

        // Hash thinning preserves oak's candidate distribution while retaining the requested
        // fraction for sparse species. Changing the noise threshold would not yield that ratio.
        private static uint OriginHash(int x, int z, uint salt)
        {
            unchecked
            {
                uint value = (uint)x * 0x9E3779B9u ^ (uint)z * 0x85EBCA6Bu ^ (uint)_structureSeed ^ salt;
                value ^= value >> 16;
                value *= 0x7FEB352Du;
                value ^= value >> 15;
                value *= 0x846CA68Bu;
                return value ^ (value >> 16);
            }
        }

        private static bool TrySelectTree(int x, int z, int height, ChunkGenerator.BiomeEnum biome,
            out Block[,,] structure, out Block leaves, out int offset)
        {
            structure = null;
            leaves = null;
            offset = OakTreeHorizontalRadius;
            int trunkWidth = 1;
            int density = 100;
            switch (biome)
            {
                case ChunkGenerator.BiomeEnum.Forest:
                    structure = OakTreeStructure; leaves = Blocks.OakLeave; break;
                case ChunkGenerator.BiomeEnum.BirchForest:
                    structure = BirchTreeStructure; leaves = Blocks.BirchLeaves; break;
                case ChunkGenerator.BiomeEnum.Taiga:
                    structure = SpruceTreeStructure; leaves = Blocks.SpruceLeaves;
                    offset = 5; trunkWidth = 2; density = 70; break;
                case ChunkGenerator.BiomeEnum.Jungle:
                    structure = JungleTreeStructure; leaves = Blocks.JungleLeaves;
                    offset = 5; trunkWidth = 2; break;
                case ChunkGenerator.BiomeEnum.Savanna:
                    structure = AcaciaTreeStructures[(int)(OriginHash(x, z, 0xA511E9B3u) % 4)];
                    leaves = Blocks.AcaciaLeaves; offset = 5; density = 30; break;
                default: return false;
            }
            if (_structureNoise.At(x, z) >= -0.45f ||
                (density < 100 && OriginHash(x, z, 0x63D83595u) % 100 >= density) ||
                height < 0 || height + structure.GetLength(1) >= Chunk.ChunkHeight) return false;

            for (int dx = 0; dx < trunkWidth; dx++)
            for (int dz = 0; dz < trunkWidth; dz++)
            {
                ChunkGenerator.SampleColumn(x + dx, z + dz, out int groundHeight, out var groundBiome);
                Block support = ChunkGenerator.SampleTerrainBlock(height, groundHeight, groundBiome);
                if (support != Blocks.Dirt && support != Blocks.GrassBlock) return false;
            }
            return true;
        }

        internal static bool TryGetTreeOrigin(int worldX, int worldZ, out int height)
        {
            ChunkGenerator.SampleColumn(worldX, worldZ, out height, out ChunkGenerator.BiomeEnum biome);
            return TrySelectTree(worldX, worldZ, height, biome, out _, out _, out _);
        }
    }
}
