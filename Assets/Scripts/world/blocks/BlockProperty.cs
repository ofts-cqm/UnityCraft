using UnityEngine;

namespace world.blocks
{
    public record BlockProperty(bool[] IsSolid, Vector2Int[] UVs, bool Collide, bool ReplaceTerrain)
    {
        public static BlockProperty Default(Vector2Int uv) => Default(new[] { uv, uv, uv, uv, uv, uv });

        public static BlockProperty Pillar(Vector2Int top, Vector2Int bottom, Vector2Int side) =>
            Default(new[] { top, bottom, side, side, side, side });
        
        private static BlockProperty Default(Vector2Int[] uv) => new(
            new[] { true, true, true, true, true, true }, 
            uv,
            true, 
            true);

        public BlockProperty SetSolid(bool solid) => this with { IsSolid = new[] { solid, solid, solid, solid, solid, solid } };
    }
}