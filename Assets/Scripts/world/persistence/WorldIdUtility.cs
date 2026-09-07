using System;
using System.Collections.Generic;
using System.Text;

namespace world.persistence
{
    public static class WorldIdUtility
    {
        public static string FromDisplayName(string displayName)
        {
            if (string.IsNullOrWhiteSpace(displayName)) return "world";

            StringBuilder builder = new();
            bool pendingSeparator = false;
            foreach (char raw in displayName.Trim())
            {
                char c = char.ToLowerInvariant(raw);
                bool asciiLetter = c is >= 'a' and <= 'z';
                bool digit = c is >= '0' and <= '9';
                if (asciiLetter || digit)
                {
                    if (pendingSeparator && builder.Length > 0) builder.Append('-');
                    builder.Append(c);
                    pendingSeparator = false;
                }
                else pendingSeparator = true;
            }
            return builder.Length == 0 ? "world" : builder.ToString();
        }

        public static string CreateUnique(string displayName, IEnumerable<string> existingWorldIds)
        {
            string baseId = FromDisplayName(displayName);
            HashSet<string> existing = new(existingWorldIds ?? Array.Empty<string>(), StringComparer.OrdinalIgnoreCase);
            if (!existing.Contains(baseId)) return baseId;

            for (int suffix = 2; suffix < int.MaxValue; suffix++)
            {
                string candidate = $"{baseId}-{suffix}";
                if (!existing.Contains(candidate)) return candidate;
            }
            throw new InvalidOperationException("Could not allocate a unique world ID.");
        }
    }
}
