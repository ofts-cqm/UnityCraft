using Render;
using UnityEngine;

namespace World
{
    
    // IsSolid: if is solid, then neighboring faces will not be rendered, otherwise see IsTransparent. 
    // IsTransparent: if is transparent, neighboring faces will be rendered if and only if they are the same block. 
    // If is not transparent, then neighboring faces will always be rendered
    public abstract record Block(int BlockId, bool IsSolid, bool IsTransparent, bool Collide)
    {
        private static int _registered;

        protected Block(bool isSolid = true, bool isTransparent = false, bool collide = true) : this(_registered++, isSolid, isTransparent, collide)
        {
            Blocks.BlockList.Add(this);
        }

        public abstract Vector2Int GetTextureIndex(int face);
        
        public bool IsAir => BlockId == 0;
    }

    public record SimpleBlock(bool IsSolid, bool IsTransparent, bool Collide, Vector2Int TextureUv) : Block(IsSolid, IsTransparent)
    {
        public SimpleBlock(Vector2Int TextureUv, bool isSolid = true, bool isTransparent = false, bool collide = true) : this(isSolid, isTransparent, collide, TextureUv) { }
        
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
