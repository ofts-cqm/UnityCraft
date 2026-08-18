using render;
using Render;
using UnityEngine;
using world.blocks;

namespace World.blocks
{
    public record Water() : Block(BlockProperty.Default(16).SetSolid(false) with { Collide = false })
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
        
        private static bool ShouldRender(Block other, int face)
        {
            if (other.BlockId == Blocks.Water.BlockId) return false;
            if (face == ChunkRenderObject.BottomFace) return true;
            if (other.IsSolid(face)) return false;
            return true;
        }

        private void AddFace(MeshBuilder builder, int face, Vector3 position, MeshBuilder.CubicModel model)
        {
            builder.AddFace(face, position, this, model, new Vector4(TextureIndex(face), 8, 32, 1), true);
            
            for (int i = 0; i < 4; i++)
            {
                builder.TransparentVertices.Add(model.VerticesLookup[model.TrianglesLookup[face, i]] + position);

                builder.TransparentUvs.Add(model.UvsLookup[i]);
                builder.TransparentTextureIndices.Add(new Vector4(TextureIndex(face), 8, 32, 1));
            }
            
            builder.TransparentTriangles.Add(builder.TransparentVertIndex + 2);
            builder.TransparentTriangles.Add(builder.TransparentVertIndex + 1);
            builder.TransparentTriangles.Add(builder.TransparentVertIndex);
            builder.TransparentTriangles.Add(builder.TransparentVertIndex + 3);
            builder.TransparentTriangles.Add(builder.TransparentVertIndex + 1);
            builder.TransparentTriangles.Add(builder.TransparentVertIndex + 2);
            builder.TransparentVertIndex+= 4;
        }
        
        public override void Render(Chunk chunk, MeshBuilder builder, Vector3Int position, Vector3 localPosition)
        {
            MeshBuilder.CubicModel model = chunk.GetBlock(position + Vector3Int.up).IsAir ? WaterModel : MeshBuilder.DefaultModel;
            if (ShouldRender(chunk.GetBlock(position + Vector3Int.left), ChunkRenderObject.RightFace)) AddFace(builder, ChunkRenderObject.LeftFace, localPosition, model);
            if (ShouldRender(chunk.GetBlock(position + Vector3Int.right), ChunkRenderObject.LeftFace)) AddFace(builder, ChunkRenderObject.RightFace, localPosition, model);
            if (ShouldRender(chunk.GetBlock(position + Vector3Int.up), ChunkRenderObject.BottomFace)) AddFace(builder, ChunkRenderObject.TopFace, localPosition, model);
            if (ShouldRender(chunk.GetBlock(position + Vector3Int.down), ChunkRenderObject.TopFace)) AddFace(builder, ChunkRenderObject.BottomFace, localPosition, model);
            if (ShouldRender(chunk.GetBlock(position + Vector3Int.forward), ChunkRenderObject.BackFace)) AddFace(builder, ChunkRenderObject.FrontFace, localPosition, model);
            if (ShouldRender(chunk.GetBlock(position + Vector3Int.back), ChunkRenderObject.FrontFace)) AddFace(builder, ChunkRenderObject.BackFace, localPosition, model);
        }
    }
}