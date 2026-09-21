using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using UnityEngine;
using World;
using world.generation;

namespace world.persistence
{
    public interface IWorldStorage
    {
        WorldDescriptor ReadWorldDescriptor(string worldId);
        void UpdateWorldVersion(string worldId, SaveVersion version);
        bool TryLoadPlayer(WorldLoadAuthorization authorization, out PlayerSnapshot snapshot);
        bool TryLoadChunk(WorldLoadAuthorization authorization, ChunkCoord coord, out ChunkSnapshot snapshot);
        void SavePlayer(WorldLoadAuthorization authorization, PlayerSnapshot snapshot);
        void SaveChunk(WorldLoadAuthorization authorization, ChunkSnapshot snapshot);
    }

    public interface IWorldClockStorage
    {
        void SaveClock(string worldId, double elapsedSeconds);
    }

    public sealed class FileWorldStorage : IWorldStorage, IWorldClockStorage
    {
        public const string DescriptorMagic = "UNITYCRAFT_WORLD";
        private const uint PlayerMagic = 0x52504355; // UCPR
        private const uint ChunkMagic = 0x48434355; // UCCH

        private readonly string _savesRoot;

        public FileWorldStorage(string persistentDataPath)
        {
            if (string.IsNullOrWhiteSpace(persistentDataPath)) throw new ArgumentException("A persistent data path is required.", nameof(persistentDataPath));
            _savesRoot = Path.Combine(persistentDataPath, "saves");
        }

        public IReadOnlyList<string> ListWorldIds()
        {
            if (!Directory.Exists(_savesRoot)) return Array.Empty<string>();
            string[] directories = Directory.GetDirectories(_savesRoot);
            List<string> result = new(directories.Length);
            foreach (string directory in directories) result.Add(Path.GetFileName(directory));
            result.Sort(StringComparer.Ordinal);
            return result;
        }

        public WorldDescriptor CreateWorld(string worldId, string displayName, string worldSeed = null)
        {
            ValidateWorldId(worldId);
            displayName = displayName?.Trim();
            if (string.IsNullOrWhiteSpace(displayName)) throw new ArgumentException("A display name is required.", nameof(displayName));
            string descriptorPath = DescriptorPath(worldId);
            if (File.Exists(descriptorPath)) return ReadWorldDescriptor(worldId);

            string now = DateTime.UtcNow.ToString("O");
            WorldDescriptor descriptor = new()
            {
                worldId = worldId,
                displayName = displayName,
                schemaVersion = SaveVersionPolicy.Current.Schema,
                contentVersion = SaveVersionPolicy.Current.Content,
                createdUtc = now,
                lastSavedUtc = now,
                worldSeed = WorldGenerationSettings.NormalizeOrCreateSeed(worldSeed)
            };
            WriteDescriptor(descriptor);
            return descriptor;
        }

        public WorldDescriptor ReadWorldDescriptor(string worldId)
        {
            ValidateWorldId(worldId);
            string path = DescriptorPath(worldId);
            if (!File.Exists(path)) throw new FileNotFoundException($"World '{worldId}' does not exist.", path);
            try
            {
                WorldDescriptor descriptor = JsonUtility.FromJson<WorldDescriptor>(File.ReadAllText(path, Encoding.UTF8));
                if (descriptor == null || descriptor.magic != DescriptorMagic || descriptor.worldId != worldId ||
                    string.IsNullOrWhiteSpace(descriptor.displayName) || descriptor.schemaVersion <= 0 || descriptor.contentVersion <= 0)
                    throw new CorruptSaveException($"World descriptor '{path}' is invalid.");

                if (string.IsNullOrWhiteSpace(descriptor.worldSeed))
                {
                    if ((descriptor.schemaVersion != SaveVersionPolicy.Current.Schema &&
                         descriptor.schemaVersion != SaveVersionPolicy.LegacySchema) || descriptor.contentVersion > 1)
                        throw new CorruptSaveException($"World descriptor '{path}' does not contain a generation seed.");

                    // Content version 1 predated seeded world descriptors. Assign the seed once and
                    // persist it without changing the displayed last-save time.
                    descriptor.worldSeed = WorldGenerationSettings.NormalizeOrCreateSeed(null);
                    descriptor.contentVersion = SaveVersionPolicy.Current.Content;
                    WriteDescriptor(descriptor);
                }
                return descriptor;
            }
            catch (SaveDataException) { throw; }
            catch (Exception exception)
            {
                throw new CorruptSaveException($"Could not read world descriptor '{path}'.", exception);
            }
        }

        public void UpdateWorldVersion(string worldId, SaveVersion version)
        {
            WorldDescriptor descriptor = ReadWorldDescriptor(worldId);
            descriptor.schemaVersion = version.Schema;
            descriptor.contentVersion = version.Content;
            descriptor.lastSavedUtc = DateTime.UtcNow.ToString("O");
            WriteDescriptor(descriptor);
        }

        public void SaveClock(string worldId, double elapsedSeconds)
        {
            if (double.IsNaN(elapsedSeconds) || double.IsInfinity(elapsedSeconds) || elapsedSeconds < 0)
                throw new ArgumentOutOfRangeException(nameof(elapsedSeconds));
            WorldDescriptor descriptor = ReadWorldDescriptor(worldId);
            descriptor.daylight = new DaylightSnapshot { elapsedSeconds = elapsedSeconds };
            descriptor.lastSavedUtc = DateTime.UtcNow.ToString("O");
            WriteDescriptor(descriptor);
        }

        public bool TryLoadPlayer(WorldLoadAuthorization authorization, out PlayerSnapshot snapshot)
        {
            EnsureAuthorization(authorization);
            string path = PlayerPath(authorization.WorldId);
            snapshot = null;
            if (!File.Exists(path)) return false;
            try
            {
                using FileStream stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read);
                using BinaryReader reader = new(stream, Encoding.UTF8, false);
                ReadAndValidateHeader(reader, PlayerMagic, authorization, path);
                float x = reader.ReadSingle();
                float y = reader.ReadSingle();
                float z = reader.ReadSingle();
                if (!IsFinite(x) || !IsFinite(y) || !IsFinite(z)) throw new InvalidDataException("Player position is not finite.");
                int slotCount = reader.ReadInt32();
                if (slotCount != PlayerSnapshot.InventorySize) throw new InvalidDataException($"Expected {PlayerSnapshot.InventorySize} inventory slots, got {slotCount}.");
                InventorySlotSnapshot[] inventory = new InventorySlotSnapshot[slotCount];
                for (int i = 0; i < inventory.Length; i++)
                {
                    int itemId = reader.ReadInt32();
                    int count = reader.ReadInt32();
                    bool infinite = reader.ReadBoolean();
                    if (itemId < 0 || count < 0) throw new InvalidDataException($"Inventory slot {i} has invalid values.");
                    inventory[i] = new InventorySlotSnapshot(itemId, count, infinite);
                }
                if (stream.Position != stream.Length) throw new InvalidDataException("Player file contains trailing data.");
                snapshot = new PlayerSnapshot(x, y, z, inventory);
                return true;
            }
            catch (Exception exception) when (!(exception is SaveDataException))
            {
                throw new CorruptSaveException($"Could not read player save '{path}'.", exception);
            }
        }

        public bool TryLoadChunk(WorldLoadAuthorization authorization, ChunkCoord coord, out ChunkSnapshot snapshot)
        {
            EnsureAuthorization(authorization);
            string path = ChunkPath(authorization.WorldId, coord);
            snapshot = null;
            if (!File.Exists(path)) return false;
            try
            {
                using FileStream stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read);
                using BinaryReader header = new(stream, Encoding.UTF8, true);
                SaveVersion fileVersion = ReadAndValidateHeader(header, ChunkMagic, authorization, path);
                int xCoord = header.ReadInt32();
                int zCoord = header.ReadInt32();
                int chunkSize = header.ReadInt32();
                int chunkHeight = header.ReadInt32();
                if (xCoord != coord.X || zCoord != coord.Z) throw new InvalidDataException("Chunk coordinates do not match its file name.");
                if (chunkSize != Chunk.ChunkSize || chunkHeight != Chunk.ChunkHeight) throw new InvalidDataException("Chunk dimensions do not match this schema.");

                int[] blocks = new int[ChunkSnapshot.CellCount];
                int[] states = new int[ChunkSnapshot.CellCount];
                byte[] fluids = new byte[ChunkSnapshot.CellCount];
                using GZipStream gzip = new(stream, CompressionMode.Decompress, true);
                using BinaryReader payload = new(gzip, Encoding.UTF8, true);
                for (int i = 0; i < ChunkSnapshot.CellCount; i++)
                {
                    blocks[i] = payload.ReadInt32();
                    states[i] = payload.ReadInt32();
                    fluids[i] = payload.ReadByte();
                    if (blocks[i] < 0 || states[i] < 0 || !IsValidFluid(fluids[i]))
                        throw new InvalidDataException($"Chunk cell {i} contains invalid data.");
                }
                ScheduledBlockUpdateSnapshot[] scheduled = Array.Empty<ScheduledBlockUpdateSnapshot>();
                FallingBlockSnapshot[] falling = Array.Empty<FallingBlockSnapshot>();
                if (fileVersion.Schema >= 2)
                {
                    int scheduledCount = payload.ReadInt32();
                    if ((uint)scheduledCount > ChunkSnapshot.CellCount)
                        throw new InvalidDataException("Chunk scheduled-update count is invalid.");
                    scheduled = new ScheduledBlockUpdateSnapshot[scheduledCount];
                    for (int i = 0; i < scheduled.Length; i++)
                    {
                        int x = payload.ReadInt32();
                        int y = payload.ReadInt32();
                        int z = payload.ReadInt32();
                        int blockId = payload.ReadInt32();
                        int stateId = payload.ReadInt32();
                        int remainingTicks = payload.ReadInt32();
                        if ((uint)x >= Chunk.ChunkSize || (uint)y >= Chunk.ChunkHeight ||
                            (uint)z >= Chunk.ChunkSize || blockId < 0 || stateId < 0 || remainingTicks < 1)
                            throw new InvalidDataException($"Scheduled block update {i} is invalid.");
                        scheduled[i] = new ScheduledBlockUpdateSnapshot(x, y, z, blockId, stateId, remainingTicks);
                    }

                    int fallingCount = payload.ReadInt32();
                    if ((uint)fallingCount > ChunkSnapshot.CellCount)
                        throw new InvalidDataException("Chunk falling-block count is invalid.");
                    falling = new FallingBlockSnapshot[fallingCount];
                    for (int i = 0; i < falling.Length; i++)
                    {
                        int blockId = payload.ReadInt32();
                        int stateId = payload.ReadInt32();
                        Vector3 position = new(payload.ReadSingle(), payload.ReadSingle(), payload.ReadSingle());
                        Vector3 velocity = new(payload.ReadSingle(), payload.ReadSingle(), payload.ReadSingle());
                        if (blockId < 0 || stateId < 0 || !IsFinite(position.x) || !IsFinite(position.y) ||
                            !IsFinite(position.z) || !IsFinite(velocity.x) || !IsFinite(velocity.y) ||
                            !IsFinite(velocity.z))
                            throw new InvalidDataException($"Falling block {i} is invalid.");
                        falling[i] = new FallingBlockSnapshot(blockId, stateId, position, velocity);
                    }
                }
                if (gzip.ReadByte() != -1) throw new InvalidDataException("Chunk payload contains trailing data.");
                snapshot = new ChunkSnapshot(coord, blocks, states, fluids, 0, scheduled, falling);
                return true;
            }
            catch (Exception exception) when (!(exception is SaveDataException))
            {
                throw new CorruptSaveException($"Could not read chunk save '{path}'.", exception);
            }
        }

        public void SavePlayer(WorldLoadAuthorization authorization, PlayerSnapshot snapshot)
        {
            EnsureAuthorization(authorization);
            string path = PlayerPath(authorization.WorldId);
            AtomicWrite(path, stream =>
            {
                using BinaryWriter writer = new(stream, Encoding.UTF8, true);
                WriteHeader(writer, PlayerMagic);
                writer.Write(snapshot.X);
                writer.Write(snapshot.Y);
                writer.Write(snapshot.Z);
                writer.Write(snapshot.Inventory.Length);
                foreach (InventorySlotSnapshot slot in snapshot.Inventory)
                {
                    writer.Write(slot.ItemId);
                    writer.Write(slot.Count);
                    writer.Write(slot.Infinite);
                }
            });
        }

        public void SaveChunk(WorldLoadAuthorization authorization, ChunkSnapshot snapshot)
        {
            EnsureAuthorization(authorization);
            string path = ChunkPath(authorization.WorldId, snapshot.Coord);
            AtomicWrite(path, stream =>
            {
                using BinaryWriter header = new(stream, Encoding.UTF8, true);
                WriteHeader(header, ChunkMagic);
                header.Write(snapshot.Coord.X);
                header.Write(snapshot.Coord.Z);
                header.Write(Chunk.ChunkSize);
                header.Write(Chunk.ChunkHeight);
                header.Flush();
                using GZipStream gzip = new(stream, System.IO.Compression.CompressionLevel.Fastest, true);
                using BinaryWriter payload = new(gzip, Encoding.UTF8, true);
                for (int i = 0; i < ChunkSnapshot.CellCount; i++)
                {
                    payload.Write(snapshot.BlockIds[i]);
                    payload.Write(snapshot.StateIds[i]);
                    payload.Write(snapshot.FluidAmounts[i]);
                }
                payload.Write(snapshot.ScheduledBlockUpdates.Length);
                foreach (ScheduledBlockUpdateSnapshot update in snapshot.ScheduledBlockUpdates)
                {
                    payload.Write(update.X);
                    payload.Write(update.Y);
                    payload.Write(update.Z);
                    payload.Write(update.BlockId);
                    payload.Write(update.StateId);
                    payload.Write(update.RemainingTicks);
                }
                payload.Write(snapshot.FallingBlocks.Length);
                foreach (FallingBlockSnapshot falling in snapshot.FallingBlocks)
                {
                    payload.Write(falling.BlockId);
                    payload.Write(falling.StateId);
                    payload.Write(falling.X);
                    payload.Write(falling.Y);
                    payload.Write(falling.Z);
                    payload.Write(falling.VelocityX);
                    payload.Write(falling.VelocityY);
                    payload.Write(falling.VelocityZ);
                }
            });
        }

        private void WriteDescriptor(WorldDescriptor descriptor)
        {
            string json = JsonUtility.ToJson(descriptor, true);
            AtomicWrite(DescriptorPath(descriptor.WorldId), stream =>
            {
                byte[] bytes = new UTF8Encoding(false).GetBytes(json);
                stream.Write(bytes, 0, bytes.Length);
            });
        }

        private static SaveVersion ReadAndValidateHeader(BinaryReader reader, uint expectedMagic,
            WorldLoadAuthorization authorization, string path)
        {
            if (reader.ReadUInt32() != expectedMagic) throw new InvalidDataException($"'{path}' has the wrong file type.");
            SaveVersion fileVersion = new(reader.ReadInt32(), reader.ReadInt32());
            int authorizedContent = Math.Max(authorization.SaveVersion.Content, SaveVersionPolicy.Current.Content);
            bool fileSchemaSupported = fileVersion.Schema == SaveVersionPolicy.Current.Schema ||
                                       fileVersion.Schema == SaveVersionPolicy.LegacySchema;
            bool authorizedSchemaSupported = authorization.SaveVersion.Schema == SaveVersionPolicy.Current.Schema ||
                                             authorization.SaveVersion.Schema == SaveVersionPolicy.LegacySchema;
            bool schemaMatches = fileSchemaSupported && authorizedSchemaSupported;
            if (!schemaMatches || fileVersion.Content > authorizedContent)
                throw new InvalidDataException($"'{path}' version {fileVersion} is inconsistent with the authorized world version {authorization.SaveVersion}.");
            return fileVersion;
        }

        private static void WriteHeader(BinaryWriter writer, uint magic)
        {
            writer.Write(magic);
            writer.Write(SaveVersionPolicy.Current.Schema);
            writer.Write(SaveVersionPolicy.Current.Content);
        }

        private static void AtomicWrite(string path, Action<FileStream> write)
        {
            string directory = Path.GetDirectoryName(path);
            if (string.IsNullOrEmpty(directory)) throw new InvalidOperationException($"Cannot determine the directory for '{path}'.");
            Directory.CreateDirectory(directory);
            string temporaryPath = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                using (FileStream stream = new(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None,
                           8192, FileOptions.WriteThrough))
                {
                    write(stream);
                    stream.Flush(true);
                }
                if (File.Exists(path)) File.Replace(temporaryPath, path, null);
                else File.Move(temporaryPath, path);
            }
            finally
            {
                if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
            }
        }

        private string DescriptorPath(string worldId) => Path.Combine(WorldPath(worldId), "world.json");
        private string PlayerPath(string worldId) => Path.Combine(WorldPath(worldId), "player.dat");
        private string ChunkPath(string worldId, ChunkCoord coord) => Path.Combine(WorldPath(worldId), "chunks", $"{coord.X}_{coord.Z}.chunk");
        private string WorldPath(string worldId)
        {
            ValidateWorldId(worldId);
            return Path.Combine(_savesRoot, worldId);
        }

        private static void EnsureAuthorization(WorldLoadAuthorization authorization)
        {
            if (authorization == null) throw new ArgumentNullException(nameof(authorization));
            ValidateWorldId(authorization.WorldId);
        }

        private static void ValidateWorldId(string worldId)
        {
            if (string.IsNullOrWhiteSpace(worldId)) throw new ArgumentException("World ID cannot be empty.", nameof(worldId));
            foreach (char c in worldId)
                if (!char.IsLetterOrDigit(c) && c != '-' && c != '_')
                    throw new ArgumentException("World ID may contain only letters, digits, '-' and '_'.", nameof(worldId));
        }

        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        private static bool IsValidFluid(byte value) => value <= 8 || value == 10;
    }
}
