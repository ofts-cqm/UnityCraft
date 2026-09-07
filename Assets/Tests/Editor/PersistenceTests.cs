using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using world.blocks;
using world.persistence;
using World;

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
            for (int stateId = 0; stateId <= 2; stateId++)
                Assert.AreEqual(stateId, Blocks.OakSlab.EncodeState(Blocks.OakSlab.DecodeState(stateId)));
            Assert.Throws<InvalidDataException>(() => Blocks.OakSlab.DecodeState(3));
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
                lastSavedUtc = DateTime.UtcNow.ToString("O")
            };
        }
    }
}
