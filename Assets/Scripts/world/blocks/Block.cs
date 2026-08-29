using JetBrains.Annotations;
using render;
using Render;
using UnityEngine;
using world.blocks;

namespace World.blocks
{
    public record Block(int BlockId, BlockProperty Property)
    {
        private static int _registered;

        public Block(BlockProperty property) : this(_registered++, property)
        {
            Blocks.BlockList.Add(this);
        }

        private bool ShouldRender(BlockState block, int face)
        {
            if (block.Block.Transparent) return block.Block.BlockId != BlockId;
            return !block.Block.IsSolid(face);
        }

        public virtual void Render(BlockState state, IBlockProvider chunk, MeshBuilder builder, Vector3Int position, Vector3 localPosition)
        {
            if (ShouldRender(chunk.GetBlock(position + Vector3Int.left), ChunkRenderObject.RightFace)) 
                builder.AddFace(ChunkRenderObject.LeftFace, localPosition, this);
            if (ShouldRender(chunk.GetBlock(position + Vector3Int.right), ChunkRenderObject.LeftFace)) 
                builder.AddFace(ChunkRenderObject.RightFace, localPosition, this);
            if (ShouldRender(chunk.GetBlock(position + Vector3Int.up), ChunkRenderObject.BottomFace)) 
                builder.AddFace(ChunkRenderObject.TopFace, localPosition, this);
            if (ShouldRender(chunk.GetBlock(position + Vector3Int.down), ChunkRenderObject.TopFace)) 
                builder.AddFace(ChunkRenderObject.BottomFace, localPosition, this);
            if (ShouldRender(chunk.GetBlock(position + Vector3Int.forward), ChunkRenderObject.BackFace)) 
                builder.AddFace(ChunkRenderObject.FrontFace, localPosition, this);
            if (ShouldRender(chunk.GetBlock(position + Vector3Int.back), ChunkRenderObject.FrontFace)) 
                builder.AddFace(ChunkRenderObject.BackFace, localPosition, this);
        }

        public int TextureIndex(int face) => Property.Texture[face];
        public bool IsAir => BlockId == 0;
        public bool Collide => Property.Collide;
        public bool ReplaceTerrain => Property.ReplaceTerrain;
        public bool IsSolid(int face) => Property.IsSolid[face];
        public bool Transparent => Property.Transparent;
        public bool IsAirOrVoid => BlockId == Blocks.Air.BlockId || BlockId == Blocks.Void.BlockId;
        public BlockState AsState(Vector3Int position, [CanBeNull] object data = null) => new(position, this, data);
        public BlockState AsState(int x, int y, int z, [CanBeNull] object data = null) => new(new(x, y, z), this, data);
    }
}

namespace System.Runtime.CompilerServices
{
    internal static class IsExternalInit {}
}
