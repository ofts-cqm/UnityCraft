using System.IO;
using render;
using Render;
using UnityEngine;
using World;
using World.blocks;

namespace world.blocks
{
    public enum TorchBase
    {
        Bottom,
        East,
        West,
        North,
        South,
        Rejected
    }
    
    public record Torch: Block
    {
        public Torch(int blockId) : base(blockId, BlockProperty.Pillar(11, 12, 10)
            .SetAllowsLightPassThrough(true) with { IsSolid = false, Collide = false, ReplaceTerrain = true, Transparent = false, AllowsLightPassThrough = true }, TorchBase.Bottom)
        {
        }

        private static readonly MeshBuilder.CubicModel TorchModel = new(
            new Vector3[]
            {
                new(0.0f, 0.9375f, 0.0f),
                new(0.0f, 0.9375f, 1.0f),
                new(1.0f, 0.9375f, 0.0f),
                new(1.0f, 0.9375f, 1.0f),
                
                new(1.0f, 0.0f, 0.0f),
                new(1.0f, 0.0f, 1.0f),
                new(0.0f, 0.0f, 0.0f),
                new(0.0f, 0.0f, 1.0f),
                
                new(0.0f, 0.0f, 0.4375f),
                new(0.0f, 1.0f, 0.4375f),
                new(1.0f, 0.0f, 0.4375f),
                new(1.0f, 1.0f, 0.4375f),
                
                new(1.0f, 0.0f, 0.5625f),
                new(1.0f, 1.0f, 0.5625f),
                new(0.0f, 0.0f, 0.5625f),
                new(0.0f, 1.0f, 0.5625f),
                
                new(0.4375f, 0.0f, 1.0f),
                new(0.4375f, 1.0f, 1.0f),
                new(0.4375f, 0.0f, 0.0f),
                new(0.4375f, 1.0f, 0.0f),
                
                new(0.5625f, 0.0f, 0.0f),
                new(0.5625f, 1.0f, 0.0f),
                new(0.5625f, 0.0f, 1.0f),
                new(0.5625f, 1.0f, 1.0f),
            }, new[,]
            {
                { 0, 1, 2, 3 }, // top
                { 4, 5, 6, 7 }, // bottom
                { 8, 9, 10, 11 }, // front
                { 12, 13, 14, 15 }, // back
                { 16, 17, 18, 19 }, // left
                { 20, 21, 22, 23 } // right
            }, new Vector2[]
            {
                new(0, 0),
                new(0, 1),
                new(1, 0),
                new(1, 1)
            }
        );
        
        private static readonly Matrix4x4 OffsetMatrix = Matrix4x4.Translate(new Vector3(0.5f, 0.8f, 0.5f));
        private static readonly Matrix4x4 ReverseMatrix = Matrix4x4.Translate(new Vector3(-0.5f, -0.8f, -0.5f));

        private static readonly MeshBuilder.CubicModel[] ModelLookup =
        {
            TorchModel,
            TorchModel.Rotate(OffsetMatrix * Matrix4x4.Rotate(Quaternion.Euler(0, 0, 30f)) * ReverseMatrix),
            TorchModel.Rotate(OffsetMatrix * Matrix4x4.Rotate(Quaternion.Euler(0, 0, -30f)) * ReverseMatrix),
            TorchModel.Rotate(OffsetMatrix * Matrix4x4.Rotate(Quaternion.Euler(30, 0, 0)) * ReverseMatrix),
            TorchModel.Rotate(OffsetMatrix * Matrix4x4.Rotate(Quaternion.Euler(-30, 0, 0)) * ReverseMatrix)
        };

        public override byte LightEmission(ushort stateId) => 15;

        public override bool CanPlace(BlockState existing, object placementData)
        {
            if (placementData is TorchBase.Rejected) return false;
            return existing.Block.Property.ReplaceByPlace;
        }

        public override void Render(BlockState state, IBlockProvider chunk, MeshBuilder builder, Vector3Int position, Vector3 localPosition)
        {
            MeshBuilder.CubicModel model = ModelLookup[(int)(state.Data as TorchBase? ?? TorchBase.Bottom)];
            builder.AddFace(ChunkRenderObject.TopFace,  localPosition, this, model, true);
            builder.AddFace(ChunkRenderObject.BottomFace,  localPosition, this, model, true);
            builder.AddFace(ChunkRenderObject.LeftFace,  localPosition, this, model, true);
            builder.AddFace(ChunkRenderObject.RightFace,  localPosition, this, model, true);
            builder.AddFace(ChunkRenderObject.FrontFace,  localPosition, this, model, true);
            builder.AddFace(ChunkRenderObject.BackFace,  localPosition, this, model, true);
        }

        public override object GetStateToPlace(int face, Vector3Int original, ref Vector3Int position)
        {
            if (!World.World.Instance.GetBlock(original).IsSolid(face)) return TorchBase.Rejected;
            
            return face switch
            {
                ChunkRenderObject.LeftFace => TorchBase.East,
                ChunkRenderObject.RightFace => TorchBase.West,
                ChunkRenderObject.FrontFace => TorchBase.North,
                ChunkRenderObject.BackFace => TorchBase.South,
                _ => TorchBase.Bottom
            };
        }

        public override int EncodeState(object state)
        {
            return state is TorchBase torch ? torch switch
            {
                TorchBase.Bottom => 0,
                TorchBase.East => 1,
                TorchBase.West => 2,
                TorchBase.North => 3,
                TorchBase.South => 4,
                _ => throw new InvalidDataException($"Unknown torch base {state}.")
            } : throw new InvalidDataException("Torch base is missing or invalid.");
        }

        public override object DecodeState(int stateId)
        {
            return stateId switch
            {
                0 => TorchBase.Bottom,
                1 => TorchBase.East,
                2 => TorchBase.West,
                3 => TorchBase.North,
                4 => TorchBase.South,
                _ => throw new InvalidDataException($"Unknown torche base ID {stateId}.")
            };
        }
    }
}
