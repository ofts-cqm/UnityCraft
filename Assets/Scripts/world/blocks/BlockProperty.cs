using UnityEngine;

namespace world.blocks
{
    public record BlockProperty(bool[] IsSolid, int[] Texture, bool Collide, bool ReplaceTerrain, bool Transparent)
    {
        public static BlockProperty Default(int texture) => Default(new[] { texture, texture, texture, texture, texture, texture });

        public static BlockProperty Pillar(int top, int bottom, int side) =>
            Default(new[] { top, bottom, side, side, side, side });
        
        private static BlockProperty Default(int[] uv) => new(
            new[] { true, true, true, true, true, true }, 
            uv,
            true, 
            true, 
            false
        );

        public BlockProperty SetSolid(bool solid) => this with { IsSolid = new[] { solid, solid, solid, solid, solid, solid } };
        
        public BlockProperty SetTransparent(bool transparent) 
            => this with { Transparent = transparent, IsSolid = new[] { !transparent, !transparent, !transparent, !transparent, !transparent, !transparent } };
    }
}