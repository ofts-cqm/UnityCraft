using System;
using UnityEngine;

namespace world.generation
{
    public class LegacyNoiseProvider: INoiseProvider
    {
        private const float Root = 1 / 5f;
        private const float FirstLevelFrequency = 0.001f;
        private const float SecondLevelFrequency = 0.002f;
        private const float FeatureFrequency = 0.01f;
        private const float TemperatureFrequency = 0.002f;
        
        private static PerlinNoise _continentalNoise;
        private static PerlinNoise _heightNoise;
        private static PerlinNoise _featureNoise;
        private static PerlinNoise _temperatureNoise;
        
        private static float BiasNoise(float noise)
        {
            return noise > 0 ? Mathf.Pow(noise, Root) : -Mathf.Pow(-noise, Root);
        }

        public void SampleColumn(int worldX, int worldZ, out int height, out ChunkGenerator.BiomeEnum biome,
            out float continental, out float temperature, out float humidity)
        {
            humidity = float.NaN;
            float firstLevel = BiasNoise(_continentalNoise.At(worldX, worldZ)) / 2;
            float secondLevel = BiasNoise(_heightNoise.At(worldX, worldZ)) / 4;
            continental = firstLevel + secondLevel;
            float featureLevel = _featureNoise.At(worldX, worldZ);
            featureLevel *= Mathf.Clamp(continental / 2 + 0.5f, 0, 1) * 0.25f;
            float preciseHeight = continental + featureLevel;
            temperature = _temperatureNoise.At(worldX, worldZ);
            height = Math.Clamp((int)(preciseHeight * 40) + 60, 0, 128);
            biome = continental < 0
                ? ChunkGenerator.BiomeEnum.Ocean
                : continental < 0.5f
                    ? temperature > 0 ? ChunkGenerator.BiomeEnum.Plain : ChunkGenerator.BiomeEnum.Desert
                    : temperature > 0 ? ChunkGenerator.BiomeEnum.Forest : ChunkGenerator.BiomeEnum.Mountain;
        }

        public int GetSeaLevel() => 64;

        public void Initialize(WorldGenerationSettings settings)
        {
            _continentalNoise = new PerlinNoise(settings.ContinentalSeed, FirstLevelFrequency, new[] { 3, 1, 0, 0, 1 });
            _heightNoise = new PerlinNoise(settings.HeightSeed, SecondLevelFrequency, new[] { 3, 1, 0, 0, 1 });
            _featureNoise = new PerlinNoise(settings.FeatureSeed, FeatureFrequency, new[] { 1, 1, 4, 2 });
            _temperatureNoise = new PerlinNoise(settings.TemperatureSeed, TemperatureFrequency, new[] { 5, 3, 0, 0, 1, 1, 1 });
        }
    }
}
