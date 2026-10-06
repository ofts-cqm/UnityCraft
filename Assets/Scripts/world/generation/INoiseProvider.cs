namespace world.generation
{
    public interface INoiseProvider
    {
        void Initialize(WorldGenerationSettings settings);

        void SampleColumn(int worldX, int worldZ, out int height, out ChunkGenerator.BiomeEnum biome,
            out float continental, out float temperature);

        int GetSeaLevel();
    }
}