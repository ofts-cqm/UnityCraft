namespace world.blocks
{
    public record BlockProperty(bool IsSolid, int[] Texture, bool Collide, bool ReplaceTerrain, bool Transparent)
    {
        public static BlockProperty Default(int texture) => Default(new[] { texture, texture, texture, texture, texture, texture });

        public static BlockProperty Pillar(int top, int bottom, int side) =>
            Default(new[] { top, bottom, side, side, side, side });
        
        private static BlockProperty Default(int[] uv) => new(
            true,
            uv,
            true, 
            true, 
            false
        );

        public BlockProperty SetSolid(bool solid) => this with { IsSolid = solid };
        
        public BlockProperty SetTransparent(bool transparent) 
            => this with { Transparent = transparent, IsSolid = !transparent };

        public BlockProperty SetTexture(int texture) => this with
        {
            Texture = new[] { texture, texture, texture, texture, texture, texture }
        };

        public BlockProperty OffsetTexture(int offset)
        {
            int[] texture = new int[6];
            for (int i = 0; i < 6; i++)
            {
                texture[i] = Texture[i] + offset;
            }

            return this with { Texture = texture };
        }
    }
}