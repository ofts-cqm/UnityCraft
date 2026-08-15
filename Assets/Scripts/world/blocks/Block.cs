using render;
using Render;
using UnityEngine;
using world.blocks;

namespace World.blocks
{
    public abstract record Block(int BlockId, bool IsSolid,  bool Collide)
    {
        private static int _registered;

        protected Block(bool isSolid = true, bool collide = true) : this(_registered++, isSolid, collide)
        {
            Blocks.BlockList.Add(this);
        }

        public virtual void Render(Chunk chunk, MeshBuilder builder, Vector3Int position, Vector3 localPosition)
        {
            if (!chunk.GetBlock(position + Vector3Int.left).IsSolid) builder.AddFace(ChunkRenderObject.LeftFace, localPosition, this, MeshBuilder.DefaultModel);
            if (!chunk.GetBlock(position + Vector3Int.right).IsSolid) builder.AddFace(ChunkRenderObject.RightFace, localPosition, this, MeshBuilder.DefaultModel);
            if (!chunk.GetBlock(position + Vector3Int.up).IsSolid) builder.AddFace(ChunkRenderObject.TopFace, localPosition, this, MeshBuilder.DefaultModel);
            if (!chunk.GetBlock(position + Vector3Int.down).IsSolid) builder.AddFace(ChunkRenderObject.BottomFace, localPosition, this, MeshBuilder.DefaultModel);
            if (!chunk.GetBlock(position + Vector3Int.forward).IsSolid) builder.AddFace(ChunkRenderObject.FrontFace, localPosition, this, MeshBuilder.DefaultModel);
            if (!chunk.GetBlock(position + Vector3Int.back).IsSolid) builder.AddFace(ChunkRenderObject.BackFace, localPosition, this, MeshBuilder.DefaultModel);
        }

        public abstract Vector2Int GetTextureIndex(int face);
        
        public bool IsAir => BlockId == 0;
    }

    public record SimpleBlock(bool IsSolid, bool Collide, Vector2Int TextureUv) : Block(IsSolid, Collide)
    {
        public SimpleBlock(Vector2Int TextureUv, bool isSolid = true, bool collide = true) : this(isSolid, collide, TextureUv) { }
        
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
