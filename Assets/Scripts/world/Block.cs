using Render;
using UnityEngine;

namespace World
{
    public abstract record Block(int BlockId, bool IsSolid)
    {
        private static int _registered;
        static readonly Vector3 BlockSize = new (1f, 1f, 1f);

        protected Block(bool isSolid = true) : this(_registered++, isSolid)
        {
            Blocks.BlockList.Add(this);
        }

        public abstract Vector2Int GetTextureIndex(int face);
        
        public bool IsAir => BlockId == 0;
    }

    public record SimpleBlock(bool IsSolid, Vector2Int TextureUv) : Block(IsSolid)
    {
        public SimpleBlock(Vector2Int TextureUv, bool isSolid = true) : this(isSolid, TextureUv) { }
        
        public override Vector2Int GetTextureIndex(int face) => TextureUv;
    }

    public record PillarBlock(Vector2Int TopUv, Vector2Int BottomUv, Vector2Int SideUv) : Block
    {
        public override Vector2Int GetTextureIndex(int face)
        {
            return face switch
            {
                ChunkRenderObject.TopFace => TopUv,
                ChunkRenderObject.BottomFace => BottomUv,
                _ => SideUv
            };
        }
    }
}

namespace System.Runtime.CompilerServices
{
    internal static class IsExternalInit {}
}
