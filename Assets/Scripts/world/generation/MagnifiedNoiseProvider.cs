using UnityEngine;

namespace world.generation
{
    public class MagnifiedNoiseProvider : INoiseProvider
    {
        private const float OceanFrequency = 0.01f;
        private const float PlainFrequency = 0.003f;
        private const float HillFrequency = 0.01f;
        private const float TemperatureFrequency = 0.002f;
        private const float ContinentalityFrequency = 0.003f;
        
        private static PerlinNoise _oceanNoise;
        private static PerlinNoise _plainNoise;
        private static PerlinNoise _hillNoise;
        private static PerlinNoise _continentalityNoise;
        private static PerlinNoise _temperatureNoise;

        private const float OceanPlainBound = -0.15f;
        private const float PlainHillBound = 0.1f;
        private const float HillMountainBound = 0.35f;
        private const float TransitionPeriod = 0.08f;
        
        public void Initialize(WorldGenerationSettings settings)
        {
            _oceanNoise = new PerlinNoise(settings.ContinentalSeed, OceanFrequency, new[] { 4, 3, 2, 1 });
            _plainNoise = new PerlinNoise(settings.FeatureSeed, PlainFrequency, new[] { 10, 2, 2, 1, 1 });
            _hillNoise = new PerlinNoise(settings.HeightSeed, HillFrequency, new[] { 10, 5, 3, 1 });
            _continentalityNoise = new PerlinNoise(settings.ContinentalSeed, ContinentalityFrequency, new[] { 4, 1, 2 });
            _temperatureNoise = new PerlinNoise(settings.TemperatureSeed, TemperatureFrequency, new[] { 5, 3, 2, 4 });
        }

        private int SampleOcean(int worldX, int worldZ)
        {
            return (int)(_oceanNoise.At(worldX, worldZ) * 20) + 18;
        }

        private int SamplePlain(int worldX, int worldZ)
        {
            return (int)(_plainNoise.At(worldX, worldZ) * 30) + 38;
        }

        private int SampleHill(int worldX, int worldZ)
        {
            return (int)(_hillNoise.At(worldX, worldZ) * 50) + 60;
        }

        private int SampleMountain(int worldX, int worldZ)
        {
            float noise = _hillNoise.At(worldX, worldZ);
            const float root = 1 / 2f;
            noise = noise > 0 ? Mathf.Pow(noise, root) : -Mathf.Pow(-noise, root);
            return (int)(noise * 60) + 80;
        }

        private int BlendNoise(int noiseA, int noiseB, float bound, float current)
        {
            float factor = (current - bound + TransitionPeriod) / TransitionPeriod / 2;
            return (int)(noiseA * (1 - factor) + noiseB * factor);
        }

        private int SampleLevel(int worldX, int worldZ, float continentality)
        {
            if (continentality < OceanPlainBound - TransitionPeriod) 
                return SampleOcean(worldX, worldZ);
            if (continentality < OceanPlainBound + TransitionPeriod)
                return BlendNoise(SampleOcean(worldX, worldZ), SamplePlain(worldX, worldZ), OceanPlainBound,
                    continentality);
            if (continentality < PlainHillBound - TransitionPeriod)
                return SamplePlain(worldX, worldZ);
            if (continentality < PlainHillBound + TransitionPeriod)
                return BlendNoise(SamplePlain(worldX, worldZ), SampleHill(worldX, worldZ), PlainHillBound,
                    continentality);
            if (continentality < HillMountainBound - TransitionPeriod)
                return SampleHill(worldX, worldZ);
            if (continentality < HillMountainBound + TransitionPeriod)
                return BlendNoise(SampleHill(worldX, worldZ), SampleMountain(worldX, worldZ), HillMountainBound,
                    continentality);
            return SampleMountain(worldX, worldZ);
        }

        public void SampleColumn(int worldX, int worldZ, out int height, out ChunkGenerator.BiomeEnum biome, 
            out float continental, out float temperature)
        {
            biome = ChunkGenerator.BiomeEnum.Plain;
            temperature = _temperatureNoise.At(worldX, worldZ);
            continental = _continentalityNoise.At(worldX, worldZ);

            if (continental < OceanPlainBound) biome = ChunkGenerator.BiomeEnum.Ocean;
            else if (continental < PlainHillBound)
                biome = temperature > 0.2f ? ChunkGenerator.BiomeEnum.Desert : ChunkGenerator.BiomeEnum.Plain;
            else if (continental < HillMountainBound)
                biome = temperature > -0.2f ? ChunkGenerator.BiomeEnum.Forest : ChunkGenerator.BiomeEnum.Mountain;
            else biome = ChunkGenerator.BiomeEnum.Mountain;
            
            height = SampleLevel(worldX, worldZ, continental);
        }

        public int GetSeaLevel() => 32;
    }
}