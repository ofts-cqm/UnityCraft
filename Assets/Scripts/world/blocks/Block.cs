using JetBrains.Annotations;
using render;
using Render;
using UnityEngine;
using world.blocks;

namespace World.blocks
{
    public record Block
    {
        public int BlockId { get; }
        private BlockProperty Property { get; }
        public object DefaultState { get; }

        public Block(int blockId, BlockProperty property, [CanBeNull] object defaultState = null)
        {
            BlockId = blockId;
            Property = property;
            DefaultState = defaultState ?? new object();
            Blocks.Register(this);
        }

        private bool ShouldRender(BlockState block, int face)
        {
            if (block.Block.Transparent) return block.Block.BlockId != BlockId;
            return !block.Block.IsSolid(block, face);
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

        public virtual object GetStateToPlace(int face, Vector3Int original, ref Vector3Int position)
        {
            return DefaultState;
        }

        /// <summary>
        /// Converts runtime block state to the stable numeric representation used by save files.
        /// Stateful block types must override both state conversion methods.
        /// </summary>
        public virtual int EncodeState(object state) => 0;

        public virtual object DecodeState(int stateId)
        {
            if (stateId != 0) throw new System.IO.InvalidDataException($"Block {BlockId} does not define state {stateId}.");
            return DefaultState;
        }

        public virtual (Vector3 half, Vector3 center) GetBoundingBox(Vector3Int position, object state)
        {
            Vector3 half = new Vector3(0.5f, 0.5f, 0.5f);
            Vector3 center = position + half;
            return (half, center);
        }

        public int TextureIndex(int face) => Property.Texture[face];
        public bool IsAir => BlockId == 0;
        public bool Collide => Property.Collide;
        public bool ReplaceTerrain => Property.ReplaceTerrain;
        public virtual bool IsSolid(BlockState state, int face) => Property.IsSolid;
        // Blocks are dry by default. Blocks that can hold or transmit fluid opt in explicitly.
        public virtual (int max, int min) GetFlowingAmountLimit(BlockState state, int face) => (0, 10);
        public bool Transparent => Property.Transparent;
        public bool IsAirOrVoid => BlockId == Blocks.Air.BlockId || BlockId == Blocks.Void.BlockId;
        public BlockState AsState(Vector3Int position, [CanBeNull] object data = null) => new(position, this, data ?? DefaultState);
        public BlockState AsState(int x, int y, int z, [CanBeNull] object data = null) => new(new(x, y, z), this, data ?? DefaultState);
    }
}

namespace System.Runtime.CompilerServices
{
    internal static class IsExternalInit {}
}
