using System;
using System.IO;
using player;
using render;
using Render;
using UnityEngine;
using UnityEngine.InputSystem;
using World;
using World.blocks;

namespace world.blocks
{
    public enum SlabPart
    {
        Bottom,
        Top,
        Both,
        East,
        West,
        North,
        South
    }

    public record Slab(int Id) : Block(Id, BlockProperty.Default(90).SetSolid(false), SlabPart.Bottom)
    {
        private static readonly int[,] Triangles =
        {
            { 3, 7, 2, 6 }, // top
            { 1, 5, 0, 4 }, // bottom
            { 5, 6, 4, 7 }, // front
            { 0, 3, 1, 2 }, // back
            { 4, 7, 0, 3 }, // left
            { 1, 2, 5, 6 }  // right
        };

        private static readonly Vector2[] DefaultUvs =
        {
            new(0, 0), new(0, 1), new(1, 0), new(1, 1)
        };

        private static readonly MeshBuilder.CubicModel TopModel = CreateModel(new Vector3(0, .5f, 0), Vector3.one,
            new[] { new Vector2(0, .5f), new Vector2(0, 1), new Vector2(1, .5f), new Vector2(1, 1) });
        private static readonly MeshBuilder.CubicModel BottomModel = CreateModel(Vector3.zero, new Vector3(1, .5f, 1),
            new[] { new Vector2(0, 0), new Vector2(0, .5f), new Vector2(1, 0), new Vector2(1, .5f) });
        private static readonly MeshBuilder.CubicModel EastModel = CreateModel(new Vector3(.5f, 0, 0), Vector3.one, DefaultUvs);
        private static readonly MeshBuilder.CubicModel WestModel = CreateModel(Vector3.zero, new Vector3(.5f, 1, 1), DefaultUvs);
        private static readonly MeshBuilder.CubicModel NorthModel = CreateModel(new Vector3(0, 0, .5f), Vector3.one, DefaultUvs);
        private static readonly MeshBuilder.CubicModel SouthModel = CreateModel(Vector3.zero, new Vector3(1, 1, .5f), DefaultUvs);

        private static MeshBuilder.CubicModel CreateModel(Vector3 min, Vector3 max, Vector2[] uvs)
        {
            return new MeshBuilder.CubicModel(new[]
            {
                new Vector3(min.x, min.y, min.z), new Vector3(max.x, min.y, min.z),
                new Vector3(max.x, max.y, min.z), new Vector3(min.x, max.y, min.z),
                new Vector3(min.x, min.y, max.z), new Vector3(max.x, min.y, max.z),
                new Vector3(max.x, max.y, max.z), new Vector3(min.x, max.y, max.z)
            }, Triangles, uvs);
        }

        public override bool IsSolid(BlockState state, int face)
        {
            SlabPart part = GetPart(state.Data);
            if (part == SlabPart.Both) return true;
            return part switch
            {
                SlabPart.Bottom => face == ChunkRenderObject.BottomFace,
                SlabPart.Top => face == ChunkRenderObject.TopFace,
                SlabPart.East => face == ChunkRenderObject.RightFace,
                SlabPart.West => face == ChunkRenderObject.LeftFace,
                SlabPart.North => face == ChunkRenderObject.FrontFace,
                SlabPart.South => face == ChunkRenderObject.BackFace,
                _ => false
            };
        }

        internal override bool IsSolidCompact(ushort stateId, int face)
        {
            // Slab solidity depends on its orientation. Keep compact face queries exact while the
            // common stateless blocks use Block's allocation-free property lookup.
            return IsSolid(AsState(Vector3Int.zero, DecodeStateCached(stateId)), face);
        }

        public override (int max, int min) GetFlowingAmountLimit(BlockState state, int face)
        {
            SlabPart part = GetPart(state.Data);
            if (part == SlabPart.Both) return base.GetFlowingAmountLimit(state, face);
            if (part == SlabPart.Top)
            {
                if (face is >= ChunkRenderObject.SideFaceBegin and <= ChunkRenderObject.SideFaceEnd) return (4, 0);
                return face == ChunkRenderObject.TopFace ? (0, 10) : (10, 0);
            }
            if (part == SlabPart.Bottom)
            {
                if (face is >= ChunkRenderObject.SideFaceBegin and <= ChunkRenderObject.SideFaceEnd) return (9, 5);
                return face == ChunkRenderObject.TopFace ? (10, 0) : (0, 10);
            }

            int blockedFace = part switch
            {
                SlabPart.East => ChunkRenderObject.RightFace,
                SlabPart.West => ChunkRenderObject.LeftFace,
                SlabPart.North => ChunkRenderObject.FrontFace,
                SlabPart.South => ChunkRenderObject.BackFace,
                _ => throw new ArgumentOutOfRangeException()
            };
            int oppositeFace = Opposite(blockedFace);
            if (face == blockedFace) return (0, 10);
            if (face == oppositeFace) return (10, 0);
            return (4, 0);
        }

        public override void Render(BlockState state, IBlockProvider chunk, MeshBuilder builder, Vector3Int position,
            Vector3 localPosition)
        {
            SlabPart part = GetPart(state.Data);
            MeshBuilder.CubicModel model = ModelFor(part);

            AddFaceIfVisible(chunk.GetBlock(position + Vector3Int.left), ChunkRenderObject.RightFace, ChunkRenderObject.LeftFace, model);
            AddFaceIfVisible(chunk.GetBlock(position + Vector3Int.right), ChunkRenderObject.LeftFace, ChunkRenderObject.RightFace, model);
            AddFaceIfVisible(chunk.GetBlock(position + Vector3Int.up), ChunkRenderObject.BottomFace, ChunkRenderObject.TopFace, model);
            AddFaceIfVisible(chunk.GetBlock(position + Vector3Int.down), ChunkRenderObject.TopFace, ChunkRenderObject.BottomFace, model);
            AddFaceIfVisible(chunk.GetBlock(position + Vector3Int.forward), ChunkRenderObject.BackFace, ChunkRenderObject.FrontFace, model);
            AddFaceIfVisible(chunk.GetBlock(position + Vector3Int.back), ChunkRenderObject.FrontFace, ChunkRenderObject.BackFace, model);

            void AddFaceIfVisible(BlockState neighbor, int neighborFace, int face, MeshBuilder.CubicModel faceModel)
            {
                if (!ShouldRenderSlab(neighbor, neighborFace, part)) return;
                builder.AddFace(face, localPosition, this, faceModel, FaceUvs(face, faceModel),
                    new Vector4(TextureIndex(face), 1, 1, 0),
                    MeshBuilder.MeshTargets.Opaque | MeshBuilder.MeshTargets.Collider);
            }
        }

        private static Vector2[] FaceUvs(int face, MeshBuilder.CubicModel model)
        {
            Vector2[] uvs = new Vector2[4];
            for (int i = 0; i < 4; i++)
            {
                Vector3 vertex = model.VerticesLookup[model.TrianglesLookup[face, i]];
                uvs[i] = face switch
                {
                    ChunkRenderObject.TopFace => new Vector2(vertex.x, vertex.z),
                    ChunkRenderObject.BottomFace => new Vector2(1 - vertex.x, vertex.z),
                    ChunkRenderObject.FrontFace => new Vector2(1 - vertex.x, vertex.y),
                    ChunkRenderObject.BackFace => new Vector2(vertex.x, vertex.y),
                    ChunkRenderObject.LeftFace => new Vector2(1 - vertex.z, vertex.y),
                    ChunkRenderObject.RightFace => new Vector2(vertex.z, vertex.y),
                    _ => Vector2.zero
                };
            }
            return uvs;
        }

        public override object GetStateToPlace(int face, Vector3Int original, ref Vector3Int position)
        {
            BlockState clicked = World.World.Instance.GetBlock(original);
            if (clicked.Block.BlockId == BlockId)
            {
                position = original;
                return SlabPart.Both;
            }

            if (IsShiftPressed()) return GetVerticalStateToPlace(face, position);

            BlockState target = World.World.Instance.GetBlock(position);
            if (target.Block.BlockId == BlockId) return SlabPart.Both;

            float impactY = Player.Instance.ImpactPoint.y - Mathf.Floor(Player.Instance.ImpactPoint.y);
            return impactY > .5f ? SlabPart.Top : SlabPart.Bottom;
        }

        public override bool CanPlace(BlockState existing, object placementData)
        {
            if (existing.IsAir) return true;
            if (existing.Block.BlockId != BlockId) return false;
            SlabPart current = GetPart(existing.Data);
            SlabPart placement = GetPart(placementData);
            return current != SlabPart.Both && placement == SlabPart.Both;
        }

        private object GetVerticalStateToPlace(int face, Vector3Int position)
        {
            SlabPart desired = face switch
            {
                ChunkRenderObject.RightFace => SlabPart.West,
                ChunkRenderObject.LeftFace => SlabPart.East,
                ChunkRenderObject.FrontFace => SlabPart.South,
                ChunkRenderObject.BackFace => SlabPart.North,
                _ => FacingPart(Player.Instance != null && Player.Instance.cameraTransform != null
                    ? Player.Instance.cameraTransform.forward
                    : Vector3.forward)
            };

            BlockState target = World.World.Instance.GetBlock(position);
            if (target.Block.BlockId == BlockId) return SlabPart.Both;
            return desired;
        }

        private bool ShouldRenderSlab(BlockState neighbor, int neighborFace, SlabPart part)
        {
            int face = Opposite(neighborFace);
            if (!TouchesBoundary(part, face)) return true;
            if (neighbor.Block is Slab)
            {
                SlabPart neighborPart = GetPart(neighbor.Data);
                if (!TouchesBoundary(neighborPart, neighborFace)) return true;
                if (part == neighborPart) return false;
            }
            if (neighbor.Block.Transparent) return neighbor.Block.BlockId != BlockId;
            return !neighbor.Block.IsSolid(neighbor, neighborFace);
        }

        private static SlabPart GetPart(object state) => state is SlabPart part ? part : SlabPart.Bottom;

        private static MeshBuilder.CubicModel ModelFor(SlabPart part) => part switch
        {
            SlabPart.Top => TopModel,
            SlabPart.Bottom => BottomModel,
            SlabPart.Both => MeshBuilder.DefaultModel,
            SlabPart.East => EastModel,
            SlabPart.West => WestModel,
            SlabPart.North => NorthModel,
            SlabPart.South => SouthModel,
            _ => throw new ArgumentOutOfRangeException(nameof(part), part, null)
        };

        private static bool TouchesBoundary(SlabPart part, int face)
        {
            return part == SlabPart.Both || part switch
            {
                SlabPart.Top => face == ChunkRenderObject.TopFace,
                SlabPart.Bottom => face == ChunkRenderObject.BottomFace,
                SlabPart.East => face == ChunkRenderObject.RightFace,
                SlabPart.West => face == ChunkRenderObject.LeftFace,
                SlabPart.North => face == ChunkRenderObject.FrontFace,
                SlabPart.South => face == ChunkRenderObject.BackFace,
                _ => false
            };
        }

        private static SlabPart FacingPart(Vector3 direction)
        {
            if (Mathf.Abs(direction.x) > Mathf.Abs(direction.z))
                return direction.x >= 0 ? SlabPart.East : SlabPart.West;
            return direction.z >= 0 ? SlabPart.North : SlabPart.South;
        }

        private static bool IsShiftPressed()
        {
            Keyboard keyboard = Keyboard.current;
            return keyboard != null && (keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed);
        }

        private static int Opposite(int face) => face switch
        {
            ChunkRenderObject.LeftFace => ChunkRenderObject.RightFace,
            ChunkRenderObject.RightFace => ChunkRenderObject.LeftFace,
            ChunkRenderObject.FrontFace => ChunkRenderObject.BackFace,
            ChunkRenderObject.BackFace => ChunkRenderObject.FrontFace,
            ChunkRenderObject.TopFace => ChunkRenderObject.BottomFace,
            ChunkRenderObject.BottomFace => ChunkRenderObject.TopFace,
            _ => face
        };

        public override int EncodeState(object state)
        {
            return state is SlabPart part ? part switch
            {
                SlabPart.Bottom => 0,
                SlabPart.Top => 1,
                SlabPart.Both => 2,
                SlabPart.East => 3,
                SlabPart.West => 4,
                SlabPart.North => 5,
                SlabPart.South => 6,
                _ => throw new InvalidDataException($"Unknown slab state {state}.")
            } : throw new InvalidDataException("Slab state is missing or invalid.");
        }

        public override object DecodeState(int stateId)
        {
            return stateId switch
            {
                0 => SlabPart.Bottom,
                1 => SlabPart.Top,
                2 => SlabPart.Both,
                3 => SlabPart.East,
                4 => SlabPart.West,
                5 => SlabPart.North,
                6 => SlabPart.South,
                _ => throw new InvalidDataException($"Unknown slab state ID {stateId}.")
            };
        }

        public override (Vector3 half, Vector3 center) GetBoundingBox(Vector3Int position, object state)
        {
            SlabPart part = GetPart(state);
            Vector3 center = position + new Vector3(.5f, .5f, .5f);
            return part switch
            {
                SlabPart.Top => (new Vector3(.5f, .25f, .5f), center + Vector3.up * .25f),
                SlabPart.Bottom => (new Vector3(.5f, .25f, .5f), center - Vector3.up * .25f),
                SlabPart.East => (new Vector3(.25f, .5f, .5f), center + Vector3.right * .25f),
                SlabPart.West => (new Vector3(.25f, .5f, .5f), center - Vector3.right * .25f),
                SlabPart.North => (new Vector3(.5f, .5f, .25f), center + Vector3.forward * .25f),
                SlabPart.South => (new Vector3(.5f, .5f, .25f), center - Vector3.forward * .25f),
                _ => (new Vector3(.5f, .5f, .5f), center)
            };
        }
    }
}
