using System;

namespace world.generation
{
    public class PerlinNoise
    {
        private readonly int[] _permutation = new int[512];
        private readonly float _frequency;
        private readonly int[] _octaves;
        private readonly float _totalWeight;

        public PerlinNoise(int seed, float frequency, int[] octaves)
        {
            if (frequency <= 0f)
                throw new ArgumentOutOfRangeException(
                    nameof(frequency),
                    "Frequency must be greater than zero."
                );

            if (octaves == null || octaves.Length == 0)
                throw new ArgumentException(
                    "At least one octave must be provided.",
                    nameof(octaves)
                );

            _frequency = frequency;
            _octaves = (int[])octaves.Clone();

            float weightSum = 0f;

            foreach (int weight in _octaves)
            {
                if (weight < 0)
                    throw new ArgumentException(
                        "Octave weights cannot be negative.",
                        nameof(octaves)
                    );

                weightSum += weight;
            }

            if (weightSum == 0f)
                throw new ArgumentException(
                    "At least one octave must have a non-zero weight.",
                    nameof(octaves)
                );

            _totalWeight = weightSum;

            BuildPermutation(seed);
        }

        /// <summary>
        /// Samples the noise at the given coordinate.
        /// Returns approximately -1 to +1.
        /// </summary>
        public float At(float x, float y)
        {
            float result = 0f;
            float octaveFrequency = _frequency;

            for (int octave = 0; octave < _octaves.Length; octave++)
            {
                int weight = _octaves[octave];

                if (weight != 0)
                {
                    float value = Sample(
                        x * octaveFrequency,
                        y * octaveFrequency
                    );

                    result += value * weight;
                }

                octaveFrequency *= 2f;
            }

            return result / _totalWeight;
        }

        private void BuildPermutation(int seed)
        {
            int[] values = new int[256];

            for (int i = 0; i < 256; i++)
                values[i] = i;

            // We use our own small deterministic RNG rather than System.Random,
            // so the same seed produces the same noise regardless of runtime.
            uint state = unchecked((uint)seed);

            if (state == 0)
                state = 0x6D2B79F5u;

            // Fisher-Yates shuffle.
            for (int i = 255; i > 0; i--)
            {
                uint random = NextRandom(ref state);
                int j = (int)(random % (uint)(i + 1));

                (values[i], values[j]) = (values[j], values[i]);
            }

            for (int i = 0; i < 512; i++)
                _permutation[i] = values[i & 255];
        }

        private static uint NextRandom(ref uint state)
        {
            // xorshift32
            state ^= state << 13;
            state ^= state >> 17;
            state ^= state << 5;
            return state;
        }

        private float Sample(float x, float y)
        {
            // IMPORTANT:
            // Math.Floor is used rather than (int)x.
            //
            // Casting -0.5f to int gives 0, whereas Floor(-0.5) gives -1.
            // Correct flooring is important for negative-coordinate noise.
            int x0 = FastFloor(x);
            int y0 = FastFloor(y);

            float localX = x - x0;
            float localY = y - y0;

            int xi = x0 & 255;
            int yi = y0 & 255;

            int xi1 = (xi + 1) & 255;
            int yi1 = (yi + 1) & 255;

            float u = Fade(localX);
            float v = Fade(localY);

            // Hash each of the four lattice corners.
            int aa = _permutation[_permutation[xi] + yi];
            int ba = _permutation[_permutation[xi1] + yi];
            int ab = _permutation[_permutation[xi] + yi1];
            int bb = _permutation[_permutation[xi1] + yi1];

            float bottomLeft  = Gradient(aa, localX,      localY);
            float bottomRight = Gradient(ba, localX - 1, localY);
            float topLeft     = Gradient(ab, localX,      localY - 1);
            float topRight    = Gradient(bb, localX - 1, localY - 1);

            float bottom = Lerp(bottomLeft, bottomRight, u);
            float top    = Lerp(topLeft, topRight, u);

            return Lerp(bottom, top, v);
        }

        private static int FastFloor(float value)
        {
            int i = (int)value;
            return value < i ? i - 1 : i;
        }

        private static float Fade(float t)
        {
            // Ken Perlin's improved fade curve:
            // 6t^5 - 15t^4 + 10t^3
            return t * t * t * (t * (t * 6f - 15f) + 10f);
        }

        private static float Lerp(float a, float b, float t)
        {
            return a + t * (b - a);
        }

        private static float Gradient(int hash, float x, float y)
        {
            // Eight evenly distributed 2D gradient directions.
            //
            // Diagonals are normalized so they have the same magnitude
            // as the axis-aligned gradients.
            const float diagonal = 0.7071067811865475f; // 1 / sqrt(2)

            switch (hash & 7)
            {
                case 0: return  x;
                case 1: return -x;
                case 2: return  y;
                case 3: return -y;

                case 4: return diagonal * ( x + y);
                case 5: return diagonal * (-x + y);
                case 6: return diagonal * ( x - y);
                default:return diagonal * (-x - y);
            }
        }
    }
}