using System;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace world.generation
{
    public readonly struct WorldGenerationSettings : IEquatable<WorldGenerationSettings>
    {
        private string WorldSeed { get; }
        public int ContinentalSeed { get; }
        public int HeightSeed { get; }
        public int FeatureSeed { get; }
        public int TemperatureSeed { get; }
        public int StructureSeed { get; }
        public int StructureReplaceSeed { get; }

        private WorldGenerationSettings(string worldSeed, ulong root)
        {
            WorldSeed = worldSeed;
            ulong state = root;
            ContinentalSeed = NextSeed(ref state);
            HeightSeed = NextSeed(ref state);
            FeatureSeed = NextSeed(ref state);
            TemperatureSeed = NextSeed(ref state);
            StructureSeed = NextSeed(ref state);
            StructureReplaceSeed = NextSeed(ref state);
        }

        public static WorldGenerationSettings FromSeed(string seed)
        {
            if (string.IsNullOrWhiteSpace(seed)) throw new ArgumentException("A stored world seed is required.", nameof(seed));
            string normalized = seed.Trim();
            ulong root;
            if (long.TryParse(normalized, NumberStyles.Integer, CultureInfo.InvariantCulture, out long numeric))
                root = unchecked((ulong)numeric);
            else
            {
                using SHA256 sha = SHA256.Create();
                byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(normalized));
                root = 0;
                for (int i = 0; i < sizeof(ulong); i++) root |= (ulong)hash[i] << (i * 8);
            }
            return new WorldGenerationSettings(normalized, root);
        }

        public static string NormalizeOrCreateSeed(string seed)
        {
            if (!string.IsNullOrWhiteSpace(seed)) return seed.Trim();
            byte[] bytes = new byte[sizeof(long)];
            using (RandomNumberGenerator random = RandomNumberGenerator.Create()) random.GetBytes(bytes);
            return BitConverter.ToInt64(bytes, 0).ToString(CultureInfo.InvariantCulture);
        }

        private static int NextSeed(ref ulong state)
        {
            state += 0x9E3779B97F4A7C15UL;
            ulong value = state;
            value = (value ^ (value >> 30)) * 0xBF58476D1CE4E5B9UL;
            value = (value ^ (value >> 27)) * 0x94D049BB133111EBUL;
            value ^= value >> 31;
            return unchecked((int)(value ^ (value >> 32)));
        }

        public bool Equals(WorldGenerationSettings other)
        {
            return WorldSeed == other.WorldSeed && ContinentalSeed == other.ContinentalSeed &&
                   HeightSeed == other.HeightSeed && FeatureSeed == other.FeatureSeed &&
                   TemperatureSeed == other.TemperatureSeed && StructureSeed == other.StructureSeed &&
                   StructureReplaceSeed == other.StructureReplaceSeed;
        }

        public override bool Equals(object obj) => obj is WorldGenerationSettings other && Equals(other);
        public override int GetHashCode() => WorldSeed?.GetHashCode() ?? 0;
    }
}
