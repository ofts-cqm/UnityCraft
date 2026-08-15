using render;
using Render;
using UnityEngine;

namespace World.blocks
{
    public record Water() : SimpleBlock(new Vector2Int(4, 0), false, false)
    {
        private static readonly MeshBuilder.CubicModel WaterModel = new(
            new Vector3[]
            {
                new(0.0f, 0.0f, 0.0f),
                new(1.0f, 0.0f, 0.0f),
                new(1.0f, .875f, 0.0f),
                new(0.0f, .875f, 0.0f),
                new(0.0f, 0.0f, 1f),
                new(1.0f, 0.0f, 1f),
                new(1.0f, .875f, 1f),
                new(0.0f, .875f, 1f)
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
                new(0, .125f),
                new(0, 1),
                new(1, .125f),
                new(1, 1)
            }
        );
        
        public override void Render(Chunk chunk, MeshBuilder builder, Vector3Int position, Vector3 localPosition)
        {
            MeshBuilder.CubicModel model = chunk.GetBlock(position + Vector3Int.up).BlockId == BlockId ? MeshBuilder.DefaultModel : WaterModel;
            if (chunk.GetBlock(position + Vector3Int.left).BlockId != BlockId) builder.AddFace(ChunkRenderObject.LeftFace, localPosition, this, model);
            if (chunk.GetBlock(position + Vector3Int.right).BlockId != BlockId) builder.AddFace(ChunkRenderObject.RightFace, localPosition, this, model);
            if (chunk.GetBlock(position + Vector3Int.up).BlockId != BlockId) builder.AddFace(ChunkRenderObject.TopFace, localPosition, this, model);
            if (chunk.GetBlock(position + Vector3Int.down).BlockId != BlockId) builder.AddFace(ChunkRenderObject.BottomFace, localPosition, this, model);
            if (chunk.GetBlock(position + Vector3Int.forward).BlockId != BlockId) builder.AddFace(ChunkRenderObject.FrontFace, localPosition, this, model);
            if (chunk.GetBlock(position + Vector3Int.back).BlockId != BlockId) builder.AddFace(ChunkRenderObject.BackFace, localPosition, this, model);
        }
    }
}