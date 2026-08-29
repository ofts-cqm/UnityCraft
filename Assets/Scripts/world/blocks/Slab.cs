using System;
using player;
using render;
using Render;
using UnityEngine;
using World;
using World.blocks;

namespace world.blocks
{
    enum Parts
    {
        Top,
        Bottom,
        Both
    }
    
    public record Slab() : Block(BlockProperty.Default(12).SetSolid(false), Parts.Bottom)
    {
        private static readonly MeshBuilder.CubicModel TopModel = new(
            new Vector3[]
            {
                new(0.0f, 0.5f, 0.0f),
                new(1.0f, 0.5f, 0.0f),
                new(1.0f, 1.0f, 0.0f),
                new(0.0f, 1.0f, 0.0f),
                new(0.0f, 0.5f, 1f),
                new(1.0f, 0.5f, 1f),
                new(1.0f, 1.0f, 1f),
                new(0.0f, 1.0f, 1f)
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
                new(0, 0.5f),
                new(0, 1),
                new(1, 0.5f),
                new(1, 1)
            }
        );
        
        private static readonly MeshBuilder.CubicModel BottomModel = new(
            new Vector3[]
            {
                new(0.0f, 0.0f, 0.0f),
                new(1.0f, 0.0f, 0.0f),
                new(1.0f, 0.5f, 0.0f),
                new(0.0f, 0.5f, 0.0f),
                new(0.0f, 0.0f, 1f),
                new(1.0f, 0.0f, 1f),
                new(1.0f, 0.5f, 1f),
                new(0.0f, 0.5f, 1f)
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
                new(0, 0.5f),
                new(1, 0),
                new(1, 0.5f)
            }
        );

        private static readonly MeshBuilder.CubicModel BottomModelSurface =
            BottomModel with { UvsLookup = MeshBuilder.DefaultModel.UvsLookup };
        private static readonly MeshBuilder.CubicModel TopModelSurface =
            TopModel with { UvsLookup = MeshBuilder.DefaultModel.UvsLookup };

        private bool ShouldRenderSlab(BlockState block, int face, Parts state)
        {
            switch (state)
            {
                case Parts.Top when face == ChunkRenderObject.BottomFace:
                case Parts.Bottom when face == ChunkRenderObject.TopFace:
                    return true;
                default:
                    if (block.Block is Slab && state == (Parts)block.Data) return false;
                    break;
            }
            
            if (block.Block.Transparent) return block.Block.BlockId != BlockId;
            return !block.Block.IsSolid(face);
        }
        
        public override void Render(BlockState state, IBlockProvider chunk, MeshBuilder builder, Vector3Int position,
            Vector3 localPosition)
        {
            Parts parts = state.Data is Parts property ? property : Parts.Bottom;
            MeshBuilder.CubicModel model = parts switch
            {
                Parts.Top => TopModel,
                Parts.Bottom => BottomModel,
                Parts.Both => MeshBuilder.DefaultModel,
                _ => throw new ArgumentOutOfRangeException()
            };
            MeshBuilder.CubicModel surfaceModel = parts switch
            {
                Parts.Top => TopModelSurface,
                Parts.Bottom => BottomModelSurface,
                Parts.Both => MeshBuilder.DefaultModel,
                _ => throw new ArgumentOutOfRangeException()
            };
            
            if (ShouldRenderSlab(chunk.GetBlock(position + Vector3Int.left), ChunkRenderObject.RightFace, parts)) builder.AddFace(ChunkRenderObject.LeftFace, localPosition, this, model);
            if (ShouldRenderSlab(chunk.GetBlock(position + Vector3Int.right), ChunkRenderObject.LeftFace, parts)) builder.AddFace(ChunkRenderObject.RightFace, localPosition, this, model);
            if (ShouldRenderSlab(chunk.GetBlock(position + Vector3Int.up), ChunkRenderObject.BottomFace, parts)) builder.AddFace(ChunkRenderObject.TopFace, localPosition, this, surfaceModel);
            if (ShouldRenderSlab(chunk.GetBlock(position + Vector3Int.down), ChunkRenderObject.TopFace, parts)) builder.AddFace(ChunkRenderObject.BottomFace, localPosition, this, surfaceModel);
            if (ShouldRenderSlab(chunk.GetBlock(position + Vector3Int.forward), ChunkRenderObject.BackFace, parts)) builder.AddFace( ChunkRenderObject.FrontFace, localPosition, this, model);
            if (ShouldRenderSlab(chunk.GetBlock(position + Vector3Int.back), ChunkRenderObject.FrontFace, parts)) builder.AddFace(ChunkRenderObject.BackFace, localPosition, this, model);
        }

        public override object GetStateToPlace(int face, Vector3Int original, ref Vector3Int position)
        {
            if (face == ChunkRenderObject.TopFace)
            {
                BlockState state = World.World.Instance.GetBlock(original);
                if (state.Block is Slab && state.Data is Parts.Bottom)
                {
                    position = original;
                    return Parts.Both;
                }
            }else if (face == ChunkRenderObject.BottomFace)
            {
                BlockState state = World.World.Instance.GetBlock(original);
                if (state.Block is Slab && state.Data is Parts.Top)
                {
                    position = original;
                    return Parts.Both;
                }
            }
            
            BlockState state2 = World.World.Instance.GetBlock(position);
            if (state2.Block is Slab) return Parts.Both;

            float impactY = Player.Instance.ImpactPoint.y;
            impactY -= (int) impactY;
            return impactY > 0.5f ? Parts.Top : Parts.Bottom;
        }
        
        public override (Vector3 half, Vector3 center) GetBoundingBox(Vector3Int position, object state)
        {
            switch (state)
            {
                case Parts.Top:
                {
                    Vector3 half = new Vector3(0.5f, 0.25f, 0.5f);
                    Vector3 center = position + half;
                    center.y += 0.5f;
                    return (half, center);
                }
                case Parts.Bottom:
                {
                    Vector3 half = new Vector3(0.5f, 0.25f, 0.5f);
                    Vector3 center = position + half;
                    return (half, center);
                }
                default:
                {
                    Vector3 half = new Vector3(0.5f, 0.5f, 0.5f);
                    Vector3 center = position + half;
                    return (half, center);
                }
            }
        }
    }
}