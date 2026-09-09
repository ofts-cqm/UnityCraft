using System;
using System.IO;
using render;
using Render;
using UnityEngine;
using World;
using World.blocks;

namespace world.blocks
{
    public enum LogAxis
    {
        X,
        Y,
        Z
    }

    public record Log(int Id) : Block(Id, BlockProperty.Pillar(88, 88, 89), LogAxis.Y)
    {
        public override object GetStateToPlace(int face, Vector3Int original, ref Vector3Int position)
        {
            return face switch
            {
                ChunkRenderObject.LeftFace or ChunkRenderObject.RightFace => LogAxis.X,
                ChunkRenderObject.FrontFace or ChunkRenderObject.BackFace => LogAxis.Z,
                _ => LogAxis.Y
            };
        }

        public override void Render(BlockState state, IBlockProvider chunk, MeshBuilder builder, Vector3Int position,
            Vector3 localPosition)
        {
            LogAxis axis = state.Data is LogAxis value ? value : LogAxis.Y;
            AddFaceIfVisible(ShouldRender(chunk, position + Vector3Int.left, ChunkRenderObject.RightFace),
                ChunkRenderObject.LeftFace);
            AddFaceIfVisible(ShouldRender(chunk, position + Vector3Int.right, ChunkRenderObject.LeftFace),
                ChunkRenderObject.RightFace);
            AddFaceIfVisible(ShouldRender(chunk, position + Vector3Int.up, ChunkRenderObject.BottomFace),
                ChunkRenderObject.TopFace);
            AddFaceIfVisible(ShouldRender(chunk, position + Vector3Int.down, ChunkRenderObject.TopFace),
                ChunkRenderObject.BottomFace);
            AddFaceIfVisible(ShouldRender(chunk, position + Vector3Int.forward, ChunkRenderObject.BackFace),
                ChunkRenderObject.FrontFace);
            AddFaceIfVisible(ShouldRender(chunk, position + Vector3Int.back, ChunkRenderObject.FrontFace),
                ChunkRenderObject.BackFace);

            void AddFaceIfVisible(bool visible, int face)
            {
                if (!visible) return;
                bool endGrain = axis switch
                {
                    LogAxis.X => face is ChunkRenderObject.LeftFace or ChunkRenderObject.RightFace,
                    LogAxis.Z => face is ChunkRenderObject.FrontFace or ChunkRenderObject.BackFace,
                    _ => face is ChunkRenderObject.TopFace or ChunkRenderObject.BottomFace
                };
                int texture = endGrain ? TextureIndex(ChunkRenderObject.TopFace) : TextureIndex(ChunkRenderObject.FrontFace);
                builder.AddFace(face, localPosition, this, MeshBuilder.DefaultModel, FaceUvs(axis, face),
                    new Vector4(texture, 1, 1, 0), MeshBuilder.MeshTargets.Opaque | MeshBuilder.MeshTargets.Collider);
            }
        }

        private static Vector2[] FaceUvs(LogAxis axis, int face)
        {
            Vector2[] uvs = new Vector2[4];
            for (int i = 0; i < 4; i++)
            {
                Vector3 vertex = MeshBuilder.DefaultModel.VerticesLookup[MeshBuilder.DefaultModel.TrianglesLookup[face, i]];
                uvs[i] = axis switch
                {
                    // The bark texture's vertical axis follows the length of the log.
                    LogAxis.X when face is ChunkRenderObject.TopFace or ChunkRenderObject.BottomFace => new Vector2(vertex.z, vertex.x),
                    LogAxis.X when face is ChunkRenderObject.FrontFace or ChunkRenderObject.BackFace => new Vector2(vertex.y, vertex.x),
                    LogAxis.Z when face is ChunkRenderObject.TopFace or ChunkRenderObject.BottomFace => new Vector2(vertex.x, vertex.z),
                    LogAxis.Z when face is ChunkRenderObject.LeftFace or ChunkRenderObject.RightFace => new Vector2(vertex.y, vertex.z),
                    _ => DefaultFaceUv(face, vertex)
                };
            }
            return uvs;
        }

        private static Vector2 DefaultFaceUv(int face, Vector3 vertex) => face switch
        {
            ChunkRenderObject.TopFace => new Vector2(vertex.x, vertex.z),
            ChunkRenderObject.BottomFace => new Vector2(1 - vertex.x, vertex.z),
            ChunkRenderObject.FrontFace => new Vector2(1 - vertex.x, vertex.y),
            ChunkRenderObject.BackFace => new Vector2(vertex.x, vertex.y),
            ChunkRenderObject.LeftFace => new Vector2(1 - vertex.z, vertex.y),
            ChunkRenderObject.RightFace => new Vector2(vertex.z, vertex.y),
            _ => Vector2.zero
        };

        public override int EncodeState(object state)
        {
            return state is LogAxis axis ? axis switch
            {
                LogAxis.Y => 0,
                LogAxis.X => 1,
                LogAxis.Z => 2,
                _ => throw new InvalidDataException($"Unknown log axis {state}.")
            } : throw new InvalidDataException("Log axis is missing or invalid.");
        }

        public override object DecodeState(int stateId)
        {
            return stateId switch
            {
                0 => LogAxis.Y,
                1 => LogAxis.X,
                2 => LogAxis.Z,
                _ => throw new InvalidDataException($"Unknown log axis ID {stateId}.")
            };
        }
    }
}
