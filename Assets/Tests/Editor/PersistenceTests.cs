using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Render;
using render;
using world.blocks;
using world.persistence;
using world.generation;
using World;
using UnityEngine;

namespace Tests.Editor
{
    public sealed class PersistenceTests
    {
        private string _temporaryRoot;
        private FileWorldStorage _storage;
        private WorldLoadAuthorization _authorization;

        [SetUp]
        public void SetUp()
        {
            _temporaryRoot = Path.Combine(Path.GetTempPath(), "UnityCraftTests", Guid.NewGuid().ToString("N"));
            _storage = new FileWorldStorage(_temporaryRoot);
            WorldDescriptor descriptor = _storage.CreateWorld("test-world", "Test World");
            _authorization = SaveVersionPolicy.Authorize(descriptor);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_temporaryRoot)) Directory.Delete(_temporaryRoot, true);
        }

        [Test]
        public void VersionPolicySeparatesSchemaAndContentCompatibility()
        {
            Assert.AreEqual(VersionCompatibility.Compatible,
                SaveVersionPolicy.Validate(SaveVersionPolicy.Current));
            Assert.AreEqual(VersionCompatibility.Compatible,
                SaveVersionPolicy.Validate(new SaveVersion(SaveVersionPolicy.Current.Schema, SaveVersionPolicy.Current.Content - 1)));
            Assert.AreEqual(VersionCompatibility.NewerContentRequiresConfirmation,
                SaveVersionPolicy.Validate(new SaveVersion(SaveVersionPolicy.Current.Schema, SaveVersionPolicy.Current.Content + 1)));
            Assert.AreEqual(VersionCompatibility.IncompatibleSchema,
                SaveVersionPolicy.Validate(new SaveVersion(SaveVersionPolicy.Current.Schema + 1, SaveVersionPolicy.Current.Content)));
        }

        [Test]
        public void NewerContentNeedsExplicitAuthorizationButSchemaMismatchCannotBeAuthorized()
        {
            WorldDescriptor newerContent = Descriptor(SaveVersionPolicy.Current.Schema, SaveVersionPolicy.Current.Content + 1);
            Assert.Throws<NewerContentConfirmationRequiredException>(() => SaveVersionPolicy.Authorize(newerContent));
            WorldLoadAuthorization accepted = SaveVersionPolicy.Authorize(newerContent, true);
            Assert.IsTrue(accepted.AcceptedNewerContentRisk);

            WorldDescriptor otherSchema = Descriptor(SaveVersionPolicy.Current.Schema + 1, SaveVersionPolicy.Current.Content);
            Assert.Throws<IncompatibleSaveException>(() => SaveVersionPolicy.Authorize(otherSchema, true));
        }

        [Test]
        public void WorldCreationPersistsProvidedAndRandomSeeds()
        {
            WorldDescriptor provided = _storage.CreateWorld("seeded-world", "Seeded World", "Glacier Village");
            Assert.AreEqual("Glacier Village", provided.worldSeed);
            Assert.AreEqual(SaveVersionPolicy.Current.Content, provided.contentVersion);
            Assert.AreEqual("Glacier Village", _storage.ReadWorldDescriptor("seeded-world").worldSeed);

            WorldDescriptor random = _storage.CreateWorld("random-world", "Random World", "   ");
            Assert.IsFalse(string.IsNullOrWhiteSpace(random.worldSeed));
            Assert.IsTrue(long.TryParse(random.worldSeed, out _));
        }

        [Test]
        public void SeedExpansionIsStableAndSeparatesNoiseSources()
        {
            WorldGenerationSettings first = WorldGenerationSettings.FromSeed("same seed");
            WorldGenerationSettings second = WorldGenerationSettings.FromSeed("same seed");
            WorldGenerationSettings different = WorldGenerationSettings.FromSeed("different seed");

            Assert.AreEqual(first, second);
            Assert.AreNotEqual(first, different);
            int[] derived =
            {
                first.ContinentalSeed, first.HeightSeed, first.FeatureSeed, first.TemperatureSeed,
                first.StructureSeed, first.StructureReplaceSeed
            };
            Assert.AreEqual(derived.Length, derived.Distinct().Count());
        }

        [Test]
        public void LegacyDescriptorReceivesOnePersistentSeedAndContentUpgrade()
        {
            const string worldId = "legacy-world";
            string worldDirectory = Path.Combine(_temporaryRoot, "saves", worldId);
            Directory.CreateDirectory(worldDirectory);
            WorldDescriptor legacy = new()
            {
                worldId = worldId,
                displayName = "Legacy World",
                schemaVersion = SaveVersionPolicy.Current.Schema,
                contentVersion = 1,
                createdUtc = "2024-01-01T00:00:00.0000000Z",
                lastSavedUtc = "2024-02-01T00:00:00.0000000Z"
            };
            File.WriteAllText(Path.Combine(worldDirectory, "world.json"), JsonUtility.ToJson(legacy, true));

            WorldDescriptor migrated = _storage.ReadWorldDescriptor(worldId);
            WorldDescriptor reread = _storage.ReadWorldDescriptor(worldId);
            Assert.AreEqual(SaveVersionPolicy.Current.Content, migrated.contentVersion);
            Assert.IsFalse(string.IsNullOrWhiteSpace(migrated.worldSeed));
            Assert.AreEqual(migrated.worldSeed, reread.worldSeed);
            Assert.AreEqual(legacy.createdUtc, migrated.createdUtc);
            Assert.AreEqual(legacy.lastSavedUtc, migrated.lastSavedUtc);
        }

        [Test]
        public void WorldIdsAreCleanAndCollisionSafe()
        {
            Assert.AreEqual("my-cool-world", WorldIdUtility.FromDisplayName("  My Cool_World!!  "));
            Assert.AreEqual("world", WorldIdUtility.FromDisplayName("世界"));
            Assert.AreEqual("my-world-3", WorldIdUtility.CreateUnique("My World", new[] { "my-world", "my-world-2" }));
        }

        [Test]
        public void PlayerSnapshotRoundTripsAllInventoryFields()
        {
            InventorySlotSnapshot[] inventory = Enumerable.Range(0, PlayerSnapshot.InventorySize)
                .Select(i => new InventorySlotSnapshot(i, i * 2, i % 3 == 0)).ToArray();
            PlayerSnapshot expected = new(-12.5f, 72.25f, 991.75f, inventory);

            _storage.SavePlayer(_authorization, expected);

            Assert.IsTrue(_storage.TryLoadPlayer(_authorization, out PlayerSnapshot actual));
            Assert.AreEqual(expected.X, actual.X);
            Assert.AreEqual(expected.Y, actual.Y);
            Assert.AreEqual(expected.Z, actual.Z);
            for (int i = 0; i < inventory.Length; i++)
            {
                Assert.AreEqual(inventory[i].ItemId, actual.Inventory[i].ItemId);
                Assert.AreEqual(inventory[i].Count, actual.Inventory[i].Count);
                Assert.AreEqual(inventory[i].Infinite, actual.Inventory[i].Infinite);
            }
        }

        [Test]
        public void SaveCoordinatorFlushesQueuedPlayerData()
        {
            InventorySlotSnapshot[] inventory = Enumerable.Repeat(new InventorySlotSnapshot(0, 0, false),
                PlayerSnapshot.InventorySize).ToArray();
            PlayerSnapshot expected = new(3, 70, -8, inventory);

            using (WorldSaveCoordinator coordinator = new(_storage, _authorization))
            {
                coordinator.QueuePlayer(expected);
                coordinator.Flush();
                Assert.IsFalse(coordinator.TryDequeueCompletion(out WorldSaveCoordinator.SaveCompletion completion) &&
                               completion.Error != null, completion.Error?.ToString());
            }

            Assert.IsTrue(_storage.TryLoadPlayer(_authorization, out PlayerSnapshot actual));
            Assert.AreEqual(expected.X, actual.X);
            Assert.AreEqual(expected.Y, actual.Y);
            Assert.AreEqual(expected.Z, actual.Z);
        }

        [Test]
        public void CompressedChunkRoundTripsIdsStatesFluidsAndNegativeCoordinates()
        {
            int[] blocks = new int[ChunkSnapshot.CellCount];
            int[] states = new int[ChunkSnapshot.CellCount];
            byte[] fluids = new byte[ChunkSnapshot.CellCount];
            int first = ChunkSnapshot.Index(0, 0, 0);
            int middle = ChunkSnapshot.Index(7, 63, 11);
            int last = ChunkSnapshot.Index(Chunk.ChunkSize - 1, Chunk.ChunkHeight - 1, Chunk.ChunkSize - 1);
            blocks[first] = 27; states[first] = 2; fluids[first] = 10;
            blocks[middle] = 14; states[middle] = 0; fluids[middle] = 8;
            blocks[last] = 4; states[last] = 0; fluids[last] = 3;
            ChunkCoord coord = new(-17, 23);

            _storage.SaveChunk(_authorization, new ChunkSnapshot(coord, blocks, states, fluids, 42));

            Assert.IsTrue(_storage.TryLoadChunk(_authorization, coord, out ChunkSnapshot actual));
            CollectionAssert.AreEqual(blocks, actual.BlockIds);
            CollectionAssert.AreEqual(states, actual.StateIds);
            CollectionAssert.AreEqual(fluids, actual.FluidAmounts);
        }

        [Test]
        public void TrailingPlayerDataIsReportedAsCorrupt()
        {
            InventorySlotSnapshot[] inventory = Enumerable.Repeat(new InventorySlotSnapshot(0, 0, false),
                PlayerSnapshot.InventorySize).ToArray();
            _storage.SavePlayer(_authorization, new PlayerSnapshot(0, 64, 0, inventory));
            string path = Path.Combine(_temporaryRoot, "saves", "test-world", "player.dat");
            File.AppendAllText(path, "unexpected");

            Assert.Throws<CorruptSaveException>(() => _storage.TryLoadPlayer(_authorization, out _));
        }

        [Test]
        public void SlabStateCodecHasStableIdsAndRejectsUnknownValues()
        {
            for (int stateId = 0; stateId <= 6; stateId++)
                Assert.AreEqual(stateId, Blocks.OakSlab.EncodeState(Blocks.OakSlab.DecodeState(stateId)));
            Assert.Throws<InvalidDataException>(() => Blocks.OakSlab.DecodeState(7));
        }

        [Test]
        public void OakLogStateCodecHasStableIdsAndRejectsUnknownValues()
        {
            for (int stateId = 0; stateId <= 2; stateId++)
                Assert.AreEqual(stateId, Blocks.OakLog.EncodeState(Blocks.OakLog.DecodeState(stateId)));
            Assert.Throws<InvalidDataException>(() => Blocks.OakLog.DecodeState(3));
        }

        [Test]
        public void OakLogPlacementMapsSideFacesToTheirAxes()
        {
            Vector3Int position = Vector3Int.zero;
            Assert.AreEqual(LogAxis.X, Blocks.OakLog.GetStateToPlace(ChunkRenderObject.LeftFace, Vector3Int.zero, ref position));
            Assert.AreEqual(LogAxis.X, Blocks.OakLog.GetStateToPlace(ChunkRenderObject.RightFace, Vector3Int.zero, ref position));
            Assert.AreEqual(LogAxis.Z, Blocks.OakLog.GetStateToPlace(ChunkRenderObject.FrontFace, Vector3Int.zero, ref position));
            Assert.AreEqual(LogAxis.Z, Blocks.OakLog.GetStateToPlace(ChunkRenderObject.BackFace, Vector3Int.zero, ref position));
            Assert.AreEqual(LogAxis.Y, Blocks.OakLog.GetStateToPlace(ChunkRenderObject.TopFace, Vector3Int.zero, ref position));
        }

        [Test]
        public void VerticalSlabBoundsOccupyTheirNamedHalf()
        {
            (Vector3 half, Vector3 center) = Blocks.OakSlab.GetBoundingBox(Vector3Int.zero, SlabPart.East);
            Assert.AreEqual(new Vector3(.25f, .5f, .5f), half);
            Assert.AreEqual(new Vector3(.75f, .5f, .5f), center);

            (half, center) = Blocks.OakSlab.GetBoundingBox(Vector3Int.zero, SlabPart.North);
            Assert.AreEqual(new Vector3(.5f, .5f, .25f), half);
            Assert.AreEqual(new Vector3(.5f, .5f, .75f), center);
        }

        [Test]
        public void EastVerticalSlabUsesHalfWidthUvsOnFrontAndBackFaces()
        {
            MeshBuilder builder = new();
            Blocks.OakSlab.Render(Blocks.OakSlab.AsState(Vector3Int.zero, SlabPart.East), new AirBlockProvider(), builder,
                Vector3Int.zero, Vector3.zero);

            CollectionAssert.AreEqual(new[]
            {
                new Vector2(0, 0), new Vector2(0, 1), new Vector2(.5f, 0), new Vector2(.5f, 1)
            }, builder.OpaqueMesh.Uvs.GetRange(16, 4));
        }

        [Test]
        public void XAxisLogRotatesBarkUvsToFollowItsLength()
        {
            MeshBuilder builder = new();
            Blocks.OakLog.Render(Blocks.OakLog.AsState(Vector3Int.zero, LogAxis.X), new AirBlockProvider(), builder,
                Vector3Int.zero, Vector3.zero);

            CollectionAssert.AreEqual(new[]
            {
                new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 0), new Vector2(1, 0)
            }, builder.OpaqueMesh.Uvs.GetRange(16, 4));
        }

        [Test]
        public void SlabCompletionOnlyAcceptsTheHoldingSlabBlockId()
        {
            Assert.IsTrue(Blocks.OakSlab.CanPlace(Blocks.Air.AsState(Vector3Int.zero), SlabPart.Bottom));
            Assert.IsTrue(Blocks.OakSlab.CanPlace(Blocks.OakSlab.AsState(Vector3Int.zero, SlabPart.East), SlabPart.Both));
            Assert.IsFalse(Blocks.OakSlab.CanPlace(Blocks.OakPlanks.AsState(Vector3Int.zero), SlabPart.Both));
        }

        [Test]
        public void BlockIdsAreUnique()
        {
            Assert.AreEqual(Blocks.BlockList.Count, Blocks.BlockList.Select(block => block.BlockId).Distinct().Count());
        }

        private static WorldDescriptor Descriptor(int schema, int content)
        {
            return new WorldDescriptor
            {
                worldId = "version-test",
                displayName = "Version Test",
                schemaVersion = schema,
                contentVersion = content,
                createdUtc = DateTime.UtcNow.ToString("O"),
                lastSavedUtc = DateTime.UtcNow.ToString("O"),
                worldSeed = "version-test-seed"
            };
        }

        private sealed class AirBlockProvider : IBlockProvider
        {
            public BlockState GetBlock(Vector3Int position) => Blocks.Air.AsState(position);
            public BlockState GetBlock(int x, int y, int z) => Blocks.Air.AsState(x, y, z);
        }
    }
}
