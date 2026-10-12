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
        private const float HumidityFrequency = 0.003f;
        
        private static PerlinNoise _oceanNoise;
        private static PerlinNoise _plainNoise;
        private static PerlinNoise _hillNoise;
        private static PerlinNoise _continentalityNoise;
        private static PerlinNoise _temperatureNoise;
        private static PerlinNoise _humidityNoise;

        private const float OceanPlainBound = -0.15f;
        private const float PlainHillBound = 0.1f;
        private const float HillMountainBound = 0.30f;
        private const float TransitionPeriod = 0.08f;
        
        public void Initialize(WorldGenerationSettings settings)
        {
            _oceanNoise = new PerlinNoise(settings.OceanSeed, OceanFrequency, new[] { 4, 3, 2, 1 });
            _plainNoise = new PerlinNoise(settings.FeatureSeed, PlainFrequency, new[] { 10, 2, 2, 1, 1 });
            _hillNoise = new PerlinNoise(settings.HeightSeed, HillFrequency, new[] { 10, 5, 3, 1 });
            _continentalityNoise = new PerlinNoise(settings.ContinentalSeed, ContinentalityFrequency, new[] { 4, 1, 2 });
            _temperatureNoise = new PerlinNoise(settings.TemperatureSeed, TemperatureFrequency, new[] { 5, 3, 2, 4 });
            _humidityNoise = new PerlinNoise(settings.HumiditySeed, HumidityFrequency, new[] { 5, 3, 2, 4 });
        }

        private float SampleOcean(int worldX, int worldZ)
        {
            return _oceanNoise.At(worldX, worldZ) * 20 + 18;
        }

        private float SamplePlain(int worldX, int worldZ)
        {
            return _plainNoise.At(worldX, worldZ) * 30 + 38;
        }

        private float SampleHill(int worldX, int worldZ)
        {
            return _hillNoise.At(worldX, worldZ) * 50 + 60;
        }

        private float SampleMountain(int worldX, int worldZ)
        {
            float noise = _hillNoise.At(worldX, worldZ);
            const float root = 1 / 2f;
            noise = noise > 0 ? Mathf.Pow(noise, root) : -Mathf.Pow(-noise, root);
            return noise * 60 + 80;
        }

        private float BlendNoise(float noiseA, float noiseB, float bound, float current)
        {
            float factor = (current - bound + TransitionPeriod) / TransitionPeriod / 2;
            return noiseA * (1 - factor) + noiseB * factor;
        }

        private float SampleLevel(int worldX, int worldZ, float continentality)
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

        private int ClampNoise(float noise)
        {
            return (int)Mathf.Clamp(noise, 0, 128);
        }

        private static ChunkGenerator.BiomeEnum GetPlainBiome(float temperature, float humidity)
        {
            return temperature switch
            {
                > 0.2f => humidity switch
                {
                    > 0.1f => ChunkGenerator.BiomeEnum.Jungle,
                    > -0f => ChunkGenerator.BiomeEnum.Plain,
                    > -0.15f => ChunkGenerator.BiomeEnum.Savanna,
                    _ => ChunkGenerator.BiomeEnum.Desert
                },
                < ChunkGenerator.SnowyTemperature => ChunkGenerator.BiomeEnum.Plain,
                _ => humidity switch
                {
                    > 0f => ChunkGenerator.BiomeEnum.Plain,
                    > -0.1f => ChunkGenerator.BiomeEnum.Forest,
                    _ => ChunkGenerator.BiomeEnum.BirchForest
                }
            };
        }

        private static ChunkGenerator.BiomeEnum GetHillBiome(float temperature, float humidity)
        {
            return temperature switch
            {
                > 0.2f => humidity switch
                {
                    > 0.1f => ChunkGenerator.BiomeEnum.Forest,
                    > -0f => ChunkGenerator.BiomeEnum.Plain,
                    > -0.15f => ChunkGenerator.BiomeEnum.Savanna,
                    _ => ChunkGenerator.BiomeEnum.DesertHill
                },
                < ChunkGenerator.SnowyTemperature => humidity > 0 ? ChunkGenerator.BiomeEnum.Taiga : ChunkGenerator.BiomeEnum.Mountain,
                _ => humidity switch
                {
                    > 0.1f => ChunkGenerator.BiomeEnum.Forest,
                    > 0f => ChunkGenerator.BiomeEnum.Taiga,
                    _ => ChunkGenerator.BiomeEnum.Mountain
                }
            };
        }

        public void SampleColumn(int worldX, int worldZ, out int height, out ChunkGenerator.BiomeEnum biome, 
            out float continental, out float temperature, out float humidity)
        {
            temperature = _temperatureNoise.At(worldX, worldZ);
            continental = _continentalityNoise.At(worldX, worldZ);
            humidity = _humidityNoise.At(worldX, worldZ);

            biome = continental switch
            {
                < OceanPlainBound => ChunkGenerator.BiomeEnum.Ocean,
                < PlainHillBound => GetPlainBiome(temperature, humidity),
                < HillMountainBound => GetHillBiome(temperature, humidity),
                _ => temperature < 0.1f
                    ? ChunkGenerator.BiomeEnum.FrostMountain
                    : ChunkGenerator.BiomeEnum.Mountain
            };

            height = ClampNoise(SampleLevel(worldX, worldZ, continental));
        }

        public int GetSeaLevel() => 32;
    }
}
