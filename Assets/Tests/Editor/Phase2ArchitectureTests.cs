using System;
using System.Reflection;
using NUnit.Framework;
using Render;
using World;
using World.blocks;
using world.blocks;
using world.generation;
using world.persistence;
using UnityEngine;

namespace Tests.Editor
{
    public sealed class Phase2ArchitectureTests
    {
        private static readonly MethodInfo SetBlockMethod = typeof(ChunkData).GetMethod("SetBlock",
            BindingFlags.Instance | BindingFlags.NonPublic, null,
            new[] { typeof(int), typeof(int), typeof(int), typeof(Block), typeof(int) }, null);

        private static readonly MethodInfo SetFluidRawMethod = typeof(ChunkData).GetMethod("SetFluidRaw",
            BindingFlags.Instance | BindingFlags.NonPublic, null,
            new[] { typeof(int), typeof(int), typeof(int), typeof(byte) }, null);

        [Test]
        public void CompactIndexIsBijectiveAndKeepsEachSectionContiguous()
        {
            bool[] visited = new bool[ChunkData.CellCount];
            for (int y = 0; y < Chunk.ChunkHeight; y++)
            for (int z = 0; z < Chunk.ChunkSize; z++)
            for (int x = 0; x < Chunk.ChunkSize; x++)
            {
                int index = ChunkData.Index(x, y, z);
                Assert.That(index, Is.InRange(0, ChunkData.CellCount - 1));
                Assert.IsFalse(visited[index], $"Index {index} is shared by more than one cell.");
                visited[index] = true;
            }

            Assert.IsTrue(Array.TrueForAll(visited, value => value));
            for (int section = 0; section < ChunkData.SectionCount; section++)
            {
                int first = ChunkData.Index(0, section * Chunk.ChunkSize, 0);
                int last = ChunkData.Index(Chunk.ChunkSize - 1,
                    section * Chunk.ChunkSize + Chunk.ChunkSize - 1, Chunk.ChunkSize - 1);
                Assert.AreEqual(section * ChunkData.CellsPerSection, first);
                Assert.AreEqual(first + ChunkData.CellsPerSection - 1, last);
            }
        }

        [TestCase(-1, 0, 0)]
        [TestCase(16, 0, 0)]
        [TestCase(0, -1, 0)]
        [TestCase(0, 128, 0)]
        [TestCase(0, 0, -1)]
        [TestCase(0, 0, 16)]
        public void CompactIndexRejectsOutOfBoundsCoordinates(int x, int y, int z)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => ChunkData.Index(x, y, z));
        }

        [Test]
        public void CompactCellPayloadIsExactlyFiveBytesPerCell()
        {
            Assert.AreEqual(ChunkData.CellCount * 5, ChunkData.CellPayloadBytes);
            Assert.AreEqual(160 * 1024, ChunkData.CellPayloadBytes);
        }

        [Test]
        public void MetadataTracksBlockCategoriesAndCornerFaceBits()
        {
            Assert.IsNotNull(SetBlockMethod, "ChunkData must retain one authoritative mutation path.");
            ChunkData data = new();

            SetBlock(data, 0, 0, 0, Blocks.Stone, 0);
            SetBlock(data, 15, 15, 15, Blocks.WhiteStainedGlass, 0);
            SetBlock(data, 8, 8, 8, Blocks.OakLeave, 0);

            Assert.AreEqual(3, data.NonAirCount(0));
            Assert.AreEqual(3, data.CollidableCount(0));
            Assert.AreEqual(2, data.OpaqueCount(0));
            Assert.AreEqual(1, data.TransparentCount(0));

            Assert.AreEqual(1UL, data.GetNonAirBorderWord(0, ChunkRenderObject.LeftFace, 0));
            Assert.AreEqual(1UL, data.GetNonAirBorderWord(0, ChunkRenderObject.BackFace, 0));
            Assert.AreEqual(1UL, data.GetNonAirBorderWord(0, ChunkRenderObject.BottomFace, 0));
            Assert.AreEqual(1UL << 63, data.GetNonAirBorderWord(0, ChunkRenderObject.RightFace, 3));
            Assert.AreEqual(1UL << 63, data.GetNonAirBorderWord(0, ChunkRenderObject.FrontFace, 3));
            Assert.AreEqual(1UL << 63, data.GetNonAirBorderWord(0, ChunkRenderObject.TopFace, 3));

            SetBlock(data, 0, 0, 0, Blocks.Air, 0);
            SetBlock(data, 15, 15, 15, Blocks.Stone, 0);

            Assert.AreEqual(2, data.NonAirCount(0));
            Assert.AreEqual(2, data.CollidableCount(0));
            Assert.AreEqual(2, data.OpaqueCount(0));
            Assert.AreEqual(0, data.TransparentCount(0));
            Assert.AreEqual(0UL, data.GetNonAirBorderWord(0, ChunkRenderObject.LeftFace, 0));
            Assert.AreEqual(1UL << 63, data.GetNonAirBorderWord(0, ChunkRenderObject.RightFace, 3),
                "Replacing one non-air border block with another must retain its occupancy bit.");
            data.ValidateMetadata();
        }

        [Test]
        public void MetadataTracksFluidCountAndAllFacesAcrossSections()
        {
            Assert.IsNotNull(SetFluidRawMethod, "ChunkData must retain one authoritative fluid mutation path.");
            ChunkData data = new();

            SetFluidRaw(data, 0, 16, 15, 8);
            SetFluidRaw(data, 15, 31, 0, 10);
            SetFluidRaw(data, 7, 24, 7, 4);

            Assert.AreEqual(3, data.FluidCount(1));
            Assert.AreEqual(2, data.FlowingFluidCount(1));
            Assert.AreEqual(1UL << 15, data.GetFluidBorderWord(1, ChunkRenderObject.LeftFace, 0));
            Assert.AreEqual(1UL << 48, data.GetFluidBorderWord(1, ChunkRenderObject.RightFace, 3));
            Assert.AreEqual(1UL, data.GetFluidBorderWord(1, ChunkRenderObject.FrontFace, 0));
            Assert.AreEqual(1UL << 63, data.GetFluidBorderWord(1, ChunkRenderObject.BackFace, 3));
            Assert.AreEqual(1UL << 48, data.GetFluidBorderWord(1, ChunkRenderObject.BottomFace, 3));
            Assert.AreEqual(1UL << 15, data.GetFluidBorderWord(1, ChunkRenderObject.TopFace, 0));

            SetFluidRaw(data, 0, 16, 15, 0);
            SetFluidRaw(data, 15, 31, 0, 0);
            Assert.AreEqual(1, data.FluidCount(1));
            Assert.AreEqual(1, data.FlowingFluidCount(1));
            Assert.AreEqual(0UL, data.GetFluidBorderWord(1, ChunkRenderObject.LeftFace, 0));
            Assert.AreEqual(0UL, data.GetFluidBorderWord(1, ChunkRenderObject.RightFace, 3));
            data.ValidateMetadata();
        }

        [Test]
        public void SnapshotIndexRetainsTheLegacySaveLayout()
        {
            Assert.AreEqual(0, ChunkSnapshot.Index(0, 0, 0));
            Assert.AreEqual(1, ChunkSnapshot.Index(0, 0, 1));
            Assert.AreEqual(Chunk.ChunkSize, ChunkSnapshot.Index(0, 1, 0));
            Assert.AreEqual(Chunk.ChunkHeight * Chunk.ChunkSize,
                ChunkSnapshot.Index(1, 0, 0));
            Assert.AreNotEqual(ChunkData.Index(1, 0, 0), ChunkSnapshot.Index(1, 0, 0),
                "Runtime layout may be section-local, but the persisted layout must remain legacy-compatible.");
        }

        [Test]
        public void HydrationAndSnapshotPreserveLegacyCellOrderStatesAndFluid()
        {
            int[] blocks = new int[ChunkSnapshot.CellCount];
            int[] states = new int[ChunkSnapshot.CellCount];
            byte[] fluids = new byte[ChunkSnapshot.CellCount];
            SetSnapshotCell(blocks, states, fluids, 0, 0, 0, Blocks.Stone, 0, 0);
            SetSnapshotCell(blocks, states, fluids, 15, 15, 15, Blocks.WhiteStainedGlass, 0, 0);
            SetSnapshotCell(blocks, states, fluids, 0, 16, 15, Blocks.OakSlab,
                Blocks.OakSlab.EncodeState(SlabPart.South), 4);
            SetSnapshotCell(blocks, states, fluids, 15, 31, 0, Blocks.OakLog,
                Blocks.OakLog.EncodeState(LogAxis.Z), 10);

            ChunkSnapshot source = new(new ChunkCoord(-3, 5), blocks, states, fluids, 17);
            Chunk chunk = CreateChunk(source);

            Assert.AreEqual(Blocks.Stone.BlockId, chunk.GetBlock(0, 0, 0).Block.BlockId);
            Assert.AreEqual(SlabPart.South, chunk.GetBlock(0, 16, 15).Data);
            Assert.AreEqual(LogAxis.Z, chunk.GetBlock(15, 31, 0).Data);
            Assert.AreEqual(4, chunk.GetFluid(0, 16, 15).Amount);
            Assert.IsTrue(chunk.GetFluid(15, 31, 0).IsFalling);
            Assert.AreEqual(Blocks.OakSlab.BlockId, chunk.Data.GetBlockId(0, 16, 15));
            Assert.AreEqual(Blocks.OakSlab.EncodeState(SlabPart.South), chunk.Data.GetStateId(0, 16, 15));

            int changedIndex = ChunkSnapshot.Index(8, 64, 8);
            blocks[changedIndex] = Blocks.OakSlab.BlockId;
            states[changedIndex] = Blocks.OakSlab.EncodeState(SlabPart.Top);
            chunk.SetBlock(8, 64, 8, Blocks.OakSlab, SlabPart.Top);

            Assert.IsTrue(chunk.TryCreatePersistenceSnapshot(out ChunkSnapshot roundTrip));
            Assert.AreEqual(source.Coord, roundTrip.Coord);
            CollectionAssert.AreEqual(blocks, roundTrip.BlockIds);
            CollectionAssert.AreEqual(states, roundTrip.StateIds);
            CollectionAssert.AreEqual(fluids, roundTrip.FluidAmounts);
            chunk.Data.ValidateMetadata();
        }

        [TestCase(0, 0, 3933162501179815464UL, 11661, 4723)]
        [TestCase(3, -5, 1697462888878982897UL, 18402, 107)]
        [TestCase(-7, 2, 10963678588166659157UL, 18658, 0)]
        public void DirectGenerationMatchesThePreCompactionGoldenWorld(int chunkX, int chunkZ,
            ulong expectedHash, int expectedNonAir, int expectedFluid)
        {
            ChunkGenerator.Initialize(WorldGenerationSettings.FromSeed("phase-2-generation-golden"));
            ChunkData data = ChunkGenerator.GenerateChunk(new ChunkCoord(chunkX, chunkZ));
            ulong hash = 14695981039346656037UL;
            int nonAir = 0;
            int fluid = 0;
            for (int x = 0; x < Chunk.ChunkSize; x++)
            for (int y = 0; y < Chunk.ChunkHeight; y++)
            for (int z = 0; z < Chunk.ChunkSize; z++)
            {
                ushort blockId = data.GetBlockId(x, y, z);
                ushort stateId = data.GetStateId(x, y, z);
                byte rawFluid = data.GetFluidRaw(x, y, z);
                if (blockId != Blocks.Air.BlockId) nonAir++;
                if (rawFluid != 0) fluid++;
                hash = Mix(hash, (byte)blockId);
                hash = Mix(hash, (byte)(blockId >> 8));
                hash = Mix(hash, (byte)stateId);
                hash = Mix(hash, (byte)(stateId >> 8));
                hash = Mix(hash, rawFluid);
            }

            Assert.AreEqual(expectedHash, hash);
            Assert.AreEqual(expectedNonAir, nonAir);
            Assert.AreEqual(expectedFluid, fluid);
            data.ValidateMetadata();
        }

        [Test]
        public void CrossBorderTreeGenerationIsIndependentOfChunkOrder()
        {
            const string seed = "phase-2-cross-border-tree-order";
            ChunkGenerator.Initialize(WorldGenerationSettings.FromSeed(seed));
            MethodInfo treeOrigin = typeof(StructureGenerator).GetMethod("TryGetTreeOrigin",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.IsNotNull(treeOrigin);

            for (int chunkX = -64; chunkX <= 64; chunkX++)
            for (int chunkZ = -64; chunkZ <= 64; chunkZ++)
            {
                ChunkCoord originCoord = new(chunkX, chunkZ);
                for (int localX = Chunk.ChunkSize - 2; localX < Chunk.ChunkSize; localX++)
                for (int localZ = 2; localZ < Chunk.ChunkSize - 2; localZ++)
                {
                    object[] arguments = { chunkX * Chunk.ChunkSize + localX,
                        chunkZ * Chunk.ChunkSize + localZ, 0 };
                    if (!(bool)treeOrigin.Invoke(null, arguments)) continue;
                    int y = (int)arguments[2] + 1;

                    ChunkCoord destinationCoord = originCoord.Right();
                    ChunkData destinationAfterOrigin = ChunkGenerator.GenerateChunk(destinationCoord);
                    if (!ContainsCrossBorderCanopy(destinationAfterOrigin, localX, localZ, y)) continue;
                    ulong expected = HashData(destinationAfterOrigin);

                    ChunkGenerator.Initialize(WorldGenerationSettings.FromSeed(seed));
                    ChunkData destinationBeforeOrigin = ChunkGenerator.GenerateChunk(destinationCoord);
                    _ = ChunkGenerator.GenerateChunk(originCoord);

                    Assert.AreEqual(expected, HashData(destinationBeforeOrigin),
                        $"Tree crossing from ({originCoord.X},{originCoord.Z}) changed destination " +
                        $"({destinationCoord.X},{destinationCoord.Z}) with generation order.");
                    return;
                }
            }

            Assert.Fail("The fixture search did not find a generated tree whose canopy crosses a right chunk border.");
        }

        [Test]
        public void RandomPublicMutationsKeepCompactMetadataInSync()
        {
            Chunk chunk = CreateEmptyChunk();
            System.Random random = new(170218);
            Block[] choices =
            {
                Blocks.Air, Blocks.Stone, Blocks.WhiteStainedGlass, Blocks.OakLeave, Blocks.OakSlab
            };
            for (int operation = 0; operation < 4000; operation++)
            {
                int x = random.Next(1, Chunk.ChunkSize - 1);
                int y = random.Next(0, Chunk.ChunkHeight);
                int z = random.Next(1, Chunk.ChunkSize - 1);
                if ((operation & 1) == 0)
                {
                    Block block = choices[random.Next(choices.Length)];
                    object state = block.BlockId == Blocks.OakSlab.BlockId
                        ? (SlabPart)random.Next(0, 7)
                        : block.DefaultState;
                    chunk.SetBlock(x, y, z, block, state);
                }
                else
                {
                    byte rawAmount = (byte)random.Next(0, 11);
                    chunk.SetFluid(x, y, z, new FluidState { Amount = rawAmount }, false);
                }
            }

            chunk.Data.ValidateMetadata();
            for (int section = 0; section < ChunkData.SectionCount; section++)
            {
                int nonAir = 0, collidable = 0, opaque = 0, transparent = 0, fluid = 0;
                int minY = section * Chunk.ChunkSize;
                for (int y = minY; y < minY + Chunk.ChunkSize; y++)
                for (int z = 0; z < Chunk.ChunkSize; z++)
                for (int x = 0; x < Chunk.ChunkSize; x++)
                {
                    Assert.IsTrue(Blocks.TryGetById(chunk.Data.GetBlockId(x, y, z), out Block block));
                    if (!block.IsAir)
                    {
                        nonAir++;
                        if (block.Collide) collidable++;
                        if (block.Transparent) transparent++;
                        else opaque++;
                    }
                    if (chunk.Data.GetFluidRaw(x, y, z) != 0) fluid++;
                }
                Assert.AreEqual(nonAir, chunk.Data.NonAirCount(section));
                Assert.AreEqual(collidable, chunk.Data.CollidableCount(section));
                Assert.AreEqual(opaque, chunk.Data.OpaqueCount(section));
                Assert.AreEqual(transparent, chunk.Data.TransparentCount(section));
                Assert.AreEqual(fluid, chunk.Data.FluidCount(section));
            }
        }

        [Test]
        public void CompactFaceQueriesMatchMaterializedBlockStateSemantics()
        {
            Chunk chunk = CreateEmptyChunk();
            Vector3Int position = new(8, 64, 8);
            Block[] renderedBlocks = { Blocks.Stone, Blocks.WhiteStainedGlass };

            foreach (Block block in Blocks.BlockList)
            {
                object[] states = block.BlockId == Blocks.OakSlab.BlockId
                    ? new object[] { SlabPart.Bottom, SlabPart.Top, SlabPart.Both, SlabPart.East,
                        SlabPart.West, SlabPart.North, SlabPart.South }
                    : new[] { block.DefaultState };
                foreach (object state in states)
                {
                    chunk.SetBlock(position.x, position.y, position.z, block, state);
                    BlockState materialized = chunk.GetBlock(position);
                    foreach (Block rendered in renderedBlocks)
                    for (int face = ChunkRenderObject.TopFace; face <= ChunkRenderObject.RightFace; face++)
                    {
                        bool expected = materialized.Block.Transparent
                            ? materialized.Block.BlockId != rendered.BlockId
                            : !materialized.Block.IsSolid(materialized, face);
                        Assert.AreEqual(expected,
                            chunk.ShouldRenderFace(position, face, rendered.BlockId),
                            $"Compact visibility differed for neighbor block {block.BlockId}, state {state}, face {face}.");
                    }
                }
            }
        }

        [Test]
        public void RegisteredDefaultStatesUseCompactStateZero()
        {
            foreach (Block block in Blocks.BlockList)
                Assert.AreEqual(0, block.EncodeState(block.DefaultState),
                    $"Block {block.BlockId} must encode its canonical default as state zero.");
        }

        [Test]
        public void FluidSchedulerDeduplicatesAndKeepsTheEarliestEquivalentTick()
        {
            Vector3Int position = new(8, 64, 8);
            Chunk chunk = CreateEnclosedFluidChunk((position, (byte)8));
            object scheduler = CreateFluidScheduler();

            Schedule(scheduler, chunk, position, 8, 5);
            Schedule(scheduler, chunk, position, 8, 8);
            Schedule(scheduler, chunk, position, 8, 3);

            Assert.AreEqual(1, PendingCount(scheduler));
            Assert.AreEqual(0, Advance(scheduler, 1, 0, chunk));
            Assert.AreEqual(0, Advance(scheduler, 2, 0, chunk));
            Assert.AreEqual(1, Advance(scheduler, 3, 0, chunk));
            Assert.AreEqual(0, PendingCount(scheduler));
        }

        [Test]
        public void FluidSchedulerRejectsStaleAmountAndHonorsSuspendResumeAndCancel()
        {
            Vector3Int position = new(8, 64, 8);
            Chunk chunk = CreateEnclosedFluidChunk((position, (byte)8));
            object scheduler = CreateFluidScheduler();

            Schedule(scheduler, chunk, position, 8, 1);
            chunk.SetFluid(position, new FluidState { Amount = 7 }, false);
            Assert.AreEqual(0, Advance(scheduler, 1, 0, chunk));
            Assert.AreEqual(7, chunk.GetFluid(position).Amount);
            Assert.AreEqual(0, PendingCount(scheduler));

            Schedule(scheduler, chunk, position, 7, 2);
            SchedulerMethod("Suspend", typeof(Chunk)).Invoke(scheduler, new object[] { chunk });
            Assert.AreEqual(0, Advance(scheduler, 2, 0, chunk));
            Assert.AreEqual(1, PendingCount(scheduler));
            SchedulerMethod("Resume", typeof(Chunk)).Invoke(scheduler, new object[] { chunk });
            Assert.AreEqual(1, Advance(scheduler, 3, 0, chunk));
            Assert.IsTrue(chunk.GetFluid(position).IsEmpty);

            chunk.SetFluid(position, FluidState.Source, false);
            Schedule(scheduler, chunk, position, 8, 4);
            SchedulerMethod("Cancel", typeof(Chunk)).Invoke(scheduler, new object[] { chunk });
            Assert.AreEqual(0, PendingCount(scheduler));
            Assert.AreEqual(0, Advance(scheduler, 4, 0, chunk));
        }

        [Test]
        public void FluidSchedulerBudgetDefersCellsInDeterministicCoordinateOrder()
        {
            Vector3Int earlier = new(2, 40, 2);
            Vector3Int later = new(10, 40, 10);
            Chunk chunk = CreateEnclosedFluidChunk((earlier, (byte)4), (later, (byte)4));
            object scheduler = CreateFluidScheduler();

            Schedule(scheduler, chunk, later, 4, 1);
            Schedule(scheduler, chunk, earlier, 4, 1);

            Assert.AreEqual(1, Advance(scheduler, 1, 1, chunk));
            Assert.IsTrue(chunk.GetFluid(earlier).IsEmpty);
            Assert.AreEqual(4, chunk.GetFluid(later).Amount);
            Assert.AreEqual(1, PendingCount(scheduler));
            Assert.AreEqual(1, Advance(scheduler, 2, 1, chunk));
            Assert.IsTrue(chunk.GetFluid(later).IsEmpty);
            Assert.AreEqual(0, PendingCount(scheduler));
        }

        [Test]
        public void GlobalFluidSchedulerKeepsIdenticalLocalCellsIndependentAcrossChunks()
        {
            Vector3Int position = new(8, 64, 8);
            Chunk left = CreateEnclosedFluidChunk(new ChunkCoord(-1, 0), (position, (byte)4));
            Chunk right = CreateEnclosedFluidChunk(new ChunkCoord(0, 0), (position, (byte)4));
            object scheduler = CreateFluidScheduler();

            Schedule(scheduler, right, position, 4, 1);
            Schedule(scheduler, left, position, 4, 1);

            Assert.AreEqual(2, PendingCount(scheduler));
            Assert.AreEqual(2, Advance(scheduler, 1, 0, left, right));
            Assert.IsTrue(left.GetFluid(position).IsEmpty);
            Assert.IsTrue(right.GetFluid(position).IsEmpty);
            Assert.AreEqual(0, PendingCount(scheduler));
        }

        [Test]
        public void StaleFluidEntryCannotRunAgainstReloadedChunkGeneration()
        {
            Vector3Int position = new(8, 64, 8);
            Chunk unloaded = CreateEnclosedFluidChunk(new ChunkCoord(3, -2), (position, (byte)4));
            Chunk reloaded = CreateEnclosedFluidChunk(new ChunkCoord(3, -2), (position, (byte)4));
            object scheduler = CreateFluidScheduler();

            Schedule(scheduler, unloaded, position, 4, 1);

            Assert.AreEqual(0, Advance(scheduler, 1, 0, reloaded));
            Assert.AreEqual(4, reloaded.GetFluid(position).Amount);
            Assert.AreEqual(0, PendingCount(scheduler));
        }

        [Test]
        public void ScheduledSourceRemainsASourceWhenItCannotSpread()
        {
            Vector3Int position = new(8, 64, 8);
            Chunk chunk = CreateEnclosedFluidChunk((position, (byte)8));
            object scheduler = CreateFluidScheduler();

            Schedule(scheduler, chunk, position, 8, 1);

            Assert.AreEqual(1, Advance(scheduler, 1, 0, chunk));
            Assert.IsTrue(chunk.GetFluid(position).IsSource);
            Assert.AreEqual(0, PendingCount(scheduler));
        }

        private static void SetBlock(ChunkData data, int x, int y, int z, Block block, int stateId)
        {
            SetBlockMethod.Invoke(data, new object[] { x, y, z, block, stateId });
        }

        private static void SetFluidRaw(ChunkData data, int x, int y, int z, byte rawAmount)
        {
            SetFluidRawMethod.Invoke(data, new object[] { x, y, z, rawAmount });
        }

        private static Chunk CreateChunk(ChunkSnapshot snapshot)
        {
            ConstructorInfo constructor = typeof(Chunk).GetConstructor(BindingFlags.Instance | BindingFlags.NonPublic,
                null, new[] { typeof(ChunkCoord), typeof(World.World), typeof(ChunkSnapshot) }, null);
            Assert.IsNotNull(constructor);
            return (Chunk)constructor.Invoke(new object[] { snapshot.Coord, null, snapshot });
        }

        private static Chunk CreateEmptyChunk()
        {
            return CreateChunk(new ChunkSnapshot(new ChunkCoord(0, 0), new int[ChunkSnapshot.CellCount],
                new int[ChunkSnapshot.CellCount], new byte[ChunkSnapshot.CellCount]));
        }

        private static Chunk CreateEnclosedFluidChunk(params (Vector3Int position, byte rawAmount)[] cells)
        {
            return CreateEnclosedFluidChunk(new ChunkCoord(0, 0), cells);
        }

        private static Chunk CreateEnclosedFluidChunk(ChunkCoord coord,
            params (Vector3Int position, byte rawAmount)[] cells)
        {
            int[] blocks = new int[ChunkSnapshot.CellCount];
            int[] states = new int[ChunkSnapshot.CellCount];
            byte[] fluids = new byte[ChunkSnapshot.CellCount];
            Vector3Int[] directions =
            {
                Vector3Int.left, Vector3Int.right, Vector3Int.up, Vector3Int.down,
                Vector3Int.forward, Vector3Int.back
            };
            foreach ((Vector3Int position, byte rawAmount) in cells)
            {
                fluids[ChunkSnapshot.Index(position.x, position.y, position.z)] = rawAmount;
                foreach (Vector3Int direction in directions)
                {
                    Vector3Int neighbor = position + direction;
                    blocks[ChunkSnapshot.Index(neighbor.x, neighbor.y, neighbor.z)] = Blocks.Stone.BlockId;
                }
            }
            return CreateChunk(new ChunkSnapshot(coord, blocks, states, fluids));
        }

        private static Type FluidSchedulerType =>
            typeof(Chunk).Assembly.GetType("World.FluidScheduler", true);

        private static object CreateFluidScheduler() => Activator.CreateInstance(FluidSchedulerType, true);

        private static MethodInfo SchedulerMethod(string name, params Type[] argumentTypes)
        {
            MethodInfo method = FluidSchedulerType.GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic,
                null, argumentTypes, null);
            Assert.IsNotNull(method, $"Expected FluidScheduler.{name} to remain testable.");
            return method;
        }

        private static int PendingCount(object scheduler)
        {
            PropertyInfo property = FluidSchedulerType.GetProperty("PendingCount",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(property);
            return (int)property.GetValue(scheduler);
        }

        private static void Schedule(object scheduler, Chunk chunk, Vector3Int position, byte expectedRawAmount,
            int dueTick)
        {
            SchedulerMethod("Schedule", typeof(Chunk), typeof(Vector3Int), typeof(byte), typeof(int))
                .Invoke(scheduler, new object[] { chunk, position, expectedRawAmount, dueTick });
        }

        private static int Advance(object scheduler, int tick, int maximumUpdates, params Chunk[] chunks)
        {
            FieldInfo generationField = typeof(Chunk).GetField("FluidGeneration",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(generationField);
            Func<ChunkCoord, int, Chunk> resolver = (coord, generation) =>
            {
                foreach (Chunk chunk in chunks)
                    if (coord.Equals(chunk.ChunkPosition) && (int)generationField.GetValue(chunk) == generation)
                        return chunk;
                return null;
            };
            return (int)SchedulerMethod("Advance", typeof(int), typeof(int),
                    typeof(Func<ChunkCoord, int, Chunk>))
                .Invoke(scheduler, new object[] { tick, maximumUpdates, resolver });
        }

        private static void SetSnapshotCell(int[] blockIds, int[] stateIds, byte[] fluidAmounts,
            int x, int y, int z, Block block, int stateId, byte fluidRaw)
        {
            int index = ChunkSnapshot.Index(x, y, z);
            blockIds[index] = block.BlockId;
            stateIds[index] = stateId;
            fluidAmounts[index] = fluidRaw;
        }

        private static ulong Mix(ulong hash, byte value)
        {
            unchecked { return (hash ^ value) * 1099511628211UL; }
        }

        private static bool ContainsCrossBorderCanopy(ChunkData destination, int originLocalX, int originLocalZ,
            int treeBaseY)
        {
            int maximumDestinationX = originLocalX == Chunk.ChunkSize - 1 ? 1 : 0;
            for (int x = 0; x <= maximumDestinationX; x++)
            for (int z = originLocalZ - 2; z <= originLocalZ + 2; z++)
            for (int y = treeBaseY + 2; y <= treeBaseY + 5; y++)
                if (destination.GetBlockId(x, y, z) == Blocks.OakLeave.BlockId) return true;
            return false;
        }

        private static ulong HashData(ChunkData data)
        {
            ulong hash = 14695981039346656037UL;
            for (int x = 0; x < Chunk.ChunkSize; x++)
            for (int y = 0; y < Chunk.ChunkHeight; y++)
            for (int z = 0; z < Chunk.ChunkSize; z++)
            {
                ushort blockId = data.GetBlockId(x, y, z);
                ushort stateId = data.GetStateId(x, y, z);
                hash = Mix(hash, (byte)blockId);
                hash = Mix(hash, (byte)(blockId >> 8));
                hash = Mix(hash, (byte)stateId);
                hash = Mix(hash, (byte)(stateId >> 8));
                hash = Mix(hash, data.GetFluidRaw(x, y, z));
            }
            return hash;
        }
    }
}
