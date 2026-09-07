using System;
using World;

namespace world.persistence
{
    public readonly struct SaveVersion : IEquatable<SaveVersion>
    {
        public readonly int Schema;
        public readonly int Content;

        public SaveVersion(int schema, int content)
        {
            Schema = schema;
            Content = content;
        }

        public bool Equals(SaveVersion other) => Schema == other.Schema && Content == other.Content;
        public override bool Equals(object obj) => obj is SaveVersion other && Equals(other);
        public override int GetHashCode() => (Schema * 397) ^ Content;
        public override string ToString() => $"{Schema}.{Content}";
    }

    public enum VersionCompatibility
    {
        Compatible,
        NewerContentRequiresConfirmation,
        IncompatibleSchema
    }

    public static class SaveVersionPolicy
    {
        public static readonly SaveVersion Current = new(1, 1);

        public static VersionCompatibility Validate(SaveVersion savedVersion)
        {
            if (savedVersion.Schema != Current.Schema) return VersionCompatibility.IncompatibleSchema;
            return savedVersion.Content <= Current.Content
                ? VersionCompatibility.Compatible
                : VersionCompatibility.NewerContentRequiresConfirmation;
        }

        public static WorldLoadAuthorization Authorize(WorldDescriptor descriptor, bool acceptNewerContentRisk = false)
        {
            VersionCompatibility compatibility = Validate(descriptor.Version);
            if (compatibility == VersionCompatibility.IncompatibleSchema)
                throw new IncompatibleSaveException($"World '{descriptor.DisplayName}' uses schema {descriptor.SchemaVersion}; this game requires schema {Current.Schema}.");
            if (compatibility == VersionCompatibility.NewerContentRequiresConfirmation && !acceptNewerContentRisk)
                throw new NewerContentConfirmationRequiredException(descriptor);

            return new WorldLoadAuthorization(descriptor.WorldId, descriptor.Version,
                compatibility == VersionCompatibility.NewerContentRequiresConfirmation);
        }
    }

    [Serializable]
    public sealed class WorldDescriptor
    {
        public string magic = FileWorldStorage.DescriptorMagic;
        public string worldId;
        public string displayName;
        public int schemaVersion;
        public int contentVersion;
        public string createdUtc;
        public string lastSavedUtc;

        public SaveVersion Version => new(schemaVersion, contentVersion);
        public string WorldId => worldId;
        public string DisplayName => displayName;
        public int SchemaVersion => schemaVersion;
        public int ContentVersion => contentVersion;
    }

    public sealed class WorldLoadAuthorization
    {
        public string WorldId { get; }
        public SaveVersion SaveVersion { get; }
        public bool AcceptedNewerContentRisk { get; }

        internal WorldLoadAuthorization(string worldId, SaveVersion saveVersion, bool acceptedNewerContentRisk)
        {
            WorldId = worldId;
            SaveVersion = saveVersion;
            AcceptedNewerContentRisk = acceptedNewerContentRisk;
        }
    }

    /// <summary>
    /// Future world-selection UI can set an authorization before loading the gameplay scene.
    /// With no selection, the gameplay scene opens the built-in "My World" placeholder.
    /// </summary>
    public static class WorldSession
    {
        public const string DefaultWorldId = "my-world";
        public const string DefaultWorldName = "My World";
        public static WorldLoadAuthorization SelectedWorld { get; private set; }

        public static void Select(WorldLoadAuthorization authorization)
        {
            SelectedWorld = authorization ?? throw new ArgumentNullException(nameof(authorization));
        }

        public static void ClearSelection() => SelectedWorld = null;
    }

    public readonly struct InventorySlotSnapshot
    {
        public readonly int ItemId;
        public readonly int Count;
        public readonly bool Infinite;

        public InventorySlotSnapshot(int itemId, int count, bool infinite)
        {
            ItemId = itemId;
            Count = count;
            Infinite = infinite;
        }
    }

    public sealed class PlayerSnapshot
    {
        public const int InventorySize = 36;
        public readonly float X;
        public readonly float Y;
        public readonly float Z;
        public readonly InventorySlotSnapshot[] Inventory;

        public PlayerSnapshot(float x, float y, float z, InventorySlotSnapshot[] inventory)
        {
            if (inventory == null || inventory.Length != InventorySize)
                throw new ArgumentException($"A player snapshot must contain exactly {InventorySize} inventory slots.", nameof(inventory));
            X = x;
            Y = y;
            Z = z;
            Inventory = inventory;
        }
    }

    public sealed class ChunkSnapshot
    {
        public static int CellCount => Chunk.ChunkSize * Chunk.ChunkHeight * Chunk.ChunkSize;
        public readonly ChunkCoord Coord;
        public readonly int[] BlockIds;
        public readonly int[] StateIds;
        public readonly byte[] FluidAmounts;
        public readonly long Revision;

        public ChunkSnapshot(ChunkCoord coord, int[] blockIds, int[] stateIds, byte[] fluidAmounts, long revision = 0)
        {
            if (blockIds == null || blockIds.Length != CellCount) throw new ArgumentException("Invalid block array length.", nameof(blockIds));
            if (stateIds == null || stateIds.Length != CellCount) throw new ArgumentException("Invalid state array length.", nameof(stateIds));
            if (fluidAmounts == null || fluidAmounts.Length != CellCount) throw new ArgumentException("Invalid fluid array length.", nameof(fluidAmounts));
            Coord = coord;
            BlockIds = blockIds;
            StateIds = stateIds;
            FluidAmounts = fluidAmounts;
            Revision = revision;
        }

        public static int Index(int x, int y, int z) => (x * Chunk.ChunkHeight + y) * Chunk.ChunkSize + z;
    }

    public class SaveDataException : Exception
    {
        public SaveDataException(string message) : base(message) { }
        public SaveDataException(string message, Exception innerException) : base(message, innerException) { }
    }

    public sealed class CorruptSaveException : SaveDataException
    {
        public CorruptSaveException(string message) : base(message) { }
        public CorruptSaveException(string message, Exception innerException) : base(message, innerException) { }
    }

    public sealed class IncompatibleSaveException : SaveDataException
    {
        public IncompatibleSaveException(string message) : base(message) { }
    }

    public sealed class NewerContentConfirmationRequiredException : SaveDataException
    {
        public WorldDescriptor Descriptor { get; }
        public NewerContentConfirmationRequiredException(WorldDescriptor descriptor)
            : base($"World '{descriptor.DisplayName}' contains newer content ({descriptor.Version}) and requires confirmation before loading.")
        {
            Descriptor = descriptor;
        }
    }
}
