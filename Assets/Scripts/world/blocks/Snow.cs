using render;
using Render;
using UnityEngine;
using World;
using World.blocks;

namespace world.blocks
{
    public record Snow : Block
    {
        public Snow(int blockId) : base(blockId, BlockProperty.Default(13) with
        {
            AllowsLightPassThrough = true,
            Collide = false,
            IsSolid = false,
            ReplaceByPlace = true
        }) { }
        
        static readonly MeshBuilder.CubicModel SnowTopBottom = new(
            new Vector3[]
            {
                new(0.0f, 0.0f,  0.0f),
                new(1.0f, 0.0f,  0.0f),
                new(1.0f, .125f, 0.0f),
                new(0.0f, .125f, 0.0f),
                new(0.0f, 0.0f,  1.0f),
                new(1.0f, 0.0f,  1.0f),
                new(1.0f, .125f, 1.0f),
                new(0.0f, .125f, 1.0f)
            }, new[,]
            {
                { 3, 7, 2, 6 }, // top
                { 1, 5, 0, 4 }, // bottom
                { 5, 6, 4, 7 }, // front
                { 0, 3, 1, 2 }, // back
                { 4, 7, 0, 3 }, // left
                { 1, 2, 5, 6 } // right
            }, new Vector2[]
            {
                new(0, 0),
                new(0, 1),
                new(1, 0),
                new(1, 1)
            }
        );
        
        private static readonly MeshBuilder.CubicModel SnowModel = new(
            new Vector3[]
            {
                new(0.0f, 0.0f,  0.0f),
                new(1.0f, 0.0f,  0.0f),
                new(1.0f, .125f, 0.0f),
                new(0.0f, .125f, 0.0f),
                new(0.0f, 0.0f,  1.0f),
                new(1.0f, 0.0f,  1.0f),
                new(1.0f, .125f, 1.0f),
                new(0.0f, .125f, 1.0f)
            }, new[,]
            {
                { 3, 7, 2, 6 }, // top
                { 1, 5, 0, 4 }, // bottom
                { 5, 6, 4, 7 }, // front
                { 0, 3, 1, 2 }, // back
                { 4, 7, 0, 3 }, // left
                { 1, 2, 5, 6 } // right
            }, new Vector2[]
            {
                new(0, .875f),
                new(0, 1),
                new(1, .875f),
                new(1, 1)
            }
        );

        public override void Render(BlockState state, IBlockProvider chunk, MeshBuilder builder, Vector3Int position, Vector3 localPosition)
        {
            if (ShouldRender(chunk, position + Vector3Int.left, ChunkRenderObject.RightFace))
                builder.AddFace(ChunkRenderObject.LeftFace, localPosition, this, SnowModel);
            if (ShouldRender(chunk, position + Vector3Int.right, ChunkRenderObject.LeftFace))
                builder.AddFace(ChunkRenderObject.RightFace, localPosition, this, SnowModel);
            builder.AddFace(ChunkRenderObject.TopFace, localPosition, this, SnowTopBottom);
            if (ShouldRender(chunk, position + Vector3Int.down, ChunkRenderObject.TopFace))
                builder.AddFace(ChunkRenderObject.BottomFace, localPosition, this, SnowTopBottom);
            if (ShouldRender(chunk, position + Vector3Int.forward, ChunkRenderObject.BackFace))
                builder.AddFace(ChunkRenderObject.FrontFace, localPosition, this, SnowModel);
            if (ShouldRender(chunk, position + Vector3Int.back, ChunkRenderObject.FrontFace))
                builder.AddFace(ChunkRenderObject.BackFace, localPosition, this, SnowModel);
        }

        public override bool IsSolid(BlockState state, int face)
        {
            return face == ChunkRenderObject.BottomFace;
        }
    }
}