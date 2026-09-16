using System;
using System.IO;
using player;
using render;
using Render;
using UnityEngine;
using World;
using World.blocks;

namespace world.blocks
{
    public enum StairHalf
    {
        Bottom,
        Top
    }

    public enum StairFacing
    {
        North,
        East,
        South,
        West
    }

    public enum StairShape
    {
        Straight,
        OuterLeft,
        OuterRight,
        InnerLeft,
        InnerRight
    }

    // A corner's physical mesh does not retain which of its two possible source directions formed it.
    // These 24 canonical states therefore encode each distinct mesh exactly once: 12 per vertical half.
    public enum StairState
    {
        BottomNorthStraight = 0,
        BottomEastStraight = 1,
        BottomSouthStraight = 2,
        BottomWestStraight = 3,
        BottomNorthWestOuter = 4,
        BottomNorthEastOuter = 5,
        BottomSouthWestOuter = 6,
        BottomSouthEastOuter = 7,
        BottomNorthLeftInner = 8,
        BottomNorthRightInner = 9,
        BottomSouthLeftInner = 10,
        BottomSouthRightInner = 11,
        TopNorthStraight = 12,
        TopEastStraight = 13,
        TopSouthStraight = 14,
        TopWestStraight = 15,
        TopNorthWestOuter = 16,
        TopNorthEastOuter = 17,
        TopSouthWestOuter = 18,
        TopSouthEastOuter = 19,
        TopNorthLeftInner = 20,
        TopNorthRightInner = 21,
        TopSouthLeftInner = 22,
        TopSouthRightInner = 23
    }

    /// <summary>
    /// A reusable plank-textured stair. A stair is represented as a 2x2x2 set of half-block cells;
    /// the canonical state records only that visible occupancy, not the placement history that made a corner.
    /// </summary>
    public record Stair(int Id, int Texture = 90) : Block(Id, BlockProperty.Default(Texture).SetSolid(false), StairState.BottomNorthStraight)
    {
        private const int StatesPerHalf = 12;
        private const int StateCount = StatesPerHalf * 2;
        private const byte NorthStraightMask = 0b1100;
        private const byte EastStraightMask = 0b1010;
        private const byte SouthStraightMask = 0b0011;
        private const byte WestStraightMask = 0b0101;

        // Bits are x + z * 2. The upper/lower partial layer for each canonical state is listed here.
        private static readonly byte[] HighLayerMasks =
        {
            NorthStraightMask, EastStraightMask, SouthStraightMask, WestStraightMask,
            0b0100, 0b1000, 0b0001, 0b0010,
            0b1101, 0b1110, 0b1011, 0b0111
        };

        private static readonly sbyte[] StateByHighLayerMask = CreateStateByHighLayerMask();
        private static readonly Vector3Int[] FaceDirections =
        {
            Vector3Int.up, Vector3Int.down, Vector3Int.forward, Vector3Int.back, Vector3Int.left, Vector3Int.right
        };
        private static readonly int[,] Triangles =
        {
            { 3, 7, 2, 6 }, // top
            { 1, 5, 0, 4 }, // bottom
            { 5, 6, 4, 7 }, // front
            { 0, 3, 1, 2 }, // back
            { 4, 7, 0, 3 }, // left
            { 1, 2, 5, 6 }  // right
        };
        private static readonly CellGeometry[] Cells = CreateCells();

        public override int? BlockUpdateDelayTicks => 0;

        public override bool IsSolid(BlockState state, int face) => CoversFace(GetState(state.Data), face);

        internal override bool IsSolidCompact(ushort stateId, int face) =>
            stateId < StateCount && CoversFace((StairState)stateId, face);

        public override (int max, int min) GetFlowingAmountLimit(BlockState state, int face)
        {
            StairState stair = GetState(state.Data);
            // A top-half stair fills its top boundary regardless of its corner shape.
            if (face == ChunkRenderObject.TopFace && Half(stair) == StairHalf.Top) return (0, 10);
            if (CoversFace(stair, face)) return (0, 10);
            return Half(stair) == StairHalf.Top ? (4, 0) : (9, 5);
        }

        public override void Render(BlockState state, IBlockProvider chunk, MeshBuilder builder, Vector3Int position,
            Vector3 localPosition)
        {
            StairState stair = GetState(state.Data);
            const MeshBuilder.MeshTargets targets = MeshBuilder.MeshTargets.Opaque | MeshBuilder.MeshTargets.Collider;

            for (int cell = 0; cell < Cells.Length; cell++)
            {
                int x = cell & 1;
                int z = (cell >> 1) & 1;
                int y = cell >> 2;
                if (!Occupies(stair, x, y, z)) continue;

                for (int face = ChunkRenderObject.TopFace; face <= ChunkRenderObject.RightFace; face++)
                {
                    Vector3Int direction = FaceDirections[face];
                    int nextX = x + direction.x;
                    int nextY = y + direction.y;
                    int nextZ = z + direction.z;
                    if (IsCell(nextX, nextY, nextZ) && Occupies(stair, nextX, nextY, nextZ)) continue;

                    if (!IsCell(nextX, nextY, nextZ))
                    {
                        BlockState neighbor = chunk.GetBlock(position + direction);
                        if (!ShouldRenderBoundary(neighbor, Opposite(face), x, y, z)) continue;
                    }

                    CellGeometry geometry = Cells[cell];
                    builder.AddFace(face, localPosition, this, geometry.Model, geometry.FaceUvs[face],
                        new Vector4(TextureIndex(face), 1, 1, 0), targets);
                }
            }
        }

        public override object GetStateToPlace(int face, Vector3Int original, ref Vector3Int position)
        {
            float impactY = Player.Instance != null
                ? Player.Instance.ImpactPoint.y - Mathf.Floor(Player.Instance.ImpactPoint.y)
                : .5f;
            StairHalf half = impactY > .5f ? StairHalf.Top : StairHalf.Bottom;
            return Create(half, HighStepFacingFromPlayer(), StairShape.Straight);
        }

        public override bool CanPlace(BlockState existing, object placementData) => existing.IsAir;

        public override void OnBlockUpdate(World.World world, BlockState state)
        {
            StairState current = GetState(state.Data);
            // Corners are deliberately stable. A removed or later-placed neighbor must not restore or reshape one.
            if (Shape(current) != StairShape.Straight) return;

            StairFacing facing = Facing(current);
            byte ownMask = HighMask(current);
            StairState next;
            // Facing is the high/back direction. A perpendicular neighbor there makes an outer corner; the
            // matching neighbor on the low/front side makes an inner corner.
            if (TryConnection(world, state.Position, current, Direction(facing), ownMask, true, out next) ||
                TryConnection(world, state.Position, current, -Direction(facing), ownMask, false, out next))
                world.SetBlock(state.Position, this, next);
        }

        public override int EncodeState(object state)
        {
            if (state is not StairState stair || (int)stair < 0 || (int)stair >= StateCount)
                throw new InvalidDataException($"Unknown stair state {state}.");
            return (int)stair;
        }

        public override object DecodeState(int stateId)
        {
            if ((uint)stateId >= StateCount) throw new InvalidDataException($"Unknown stair state ID {stateId}.");
            return (StairState)stateId;
        }

        public override (Vector3 half, Vector3 center) GetBoundingBox(Vector3Int position, object state) =>
            (Vector3.one * .5f, position + Vector3.one * .5f);

        public static StairState Create(StairHalf half, StairFacing facing, StairShape shape)
        {
            byte mask = shape switch
            {
                StairShape.Straight => StraightMask(facing),
                StairShape.OuterLeft => OuterMask(facing, true),
                StairShape.OuterRight => OuterMask(facing, false),
                StairShape.InnerLeft => InnerMask(facing, true),
                StairShape.InnerRight => InnerMask(facing, false),
                _ => throw new ArgumentOutOfRangeException(nameof(shape), shape, null)
            };
            return FromMask(half, mask);
        }

        public static StairHalf Half(StairState state) => (int)state >= StatesPerHalf ? StairHalf.Top : StairHalf.Bottom;

        public static StairShape Shape(StairState state)
        {
            int local = LocalState(state);
            return local switch
            {
                < 4 => StairShape.Straight,
                4 or 7 => StairShape.OuterLeft,
                5 or 6 => StairShape.OuterRight,
                8 or 10 => StairShape.InnerLeft,
                _ => StairShape.InnerRight
            };
        }

        public static StairFacing Facing(StairState state)
        {
            int local = LocalState(state);
            if (local < 4) return (StairFacing)local;
            if (local < 8) return local is 4 or 5 ? StairFacing.North : StairFacing.South;
            return local is 8 or 9 ? StairFacing.North : StairFacing.South;
        }

        private bool TryConnection(World.World world, Vector3Int position, StairState current, Vector3Int direction,
            byte ownMask, bool outer, out StairState result)
        {
            result = default;
            if (!world.TryGetLoadedBlock(position + direction, out BlockState neighbor) || neighbor.Block is not Stair)
                return false;

            StairState adjacent = GetState(neighbor.Data);
            if (Half(adjacent) != Half(current) || Shape(adjacent) != StairShape.Straight ||
                SameAxis(Facing(adjacent), Facing(current))) return false;

            // A curved stair has two possible source facings but only one canonical visual state. It must therefore
            // never be used as a connection source: doing so can select a model unrelated to its placement layout.
            // A straight neighbor retains an unambiguous high-step direction.
            byte mask = outer ? (byte)(ownMask & HighMask(adjacent)) : (byte)(ownMask | HighMask(adjacent));
            if ((uint)mask >= StateByHighLayerMask.Length) return false;
            int local = StateByHighLayerMask[mask];
            if (local < 0 || (outer ? !IsOuter(Shape((StairState)local)) : !IsInner(Shape((StairState)local)))) return false;

            result = FromMask(Half(current), mask);
            return true;
        }

        private bool ShouldRenderBoundary(BlockState neighbor, int neighborFace, int x, int y, int z)
        {
            if (neighbor.Block is Stair stair)
            {
                int neighborX = x;
                int neighborY = y;
                int neighborZ = z;
                switch (neighborFace)
                {
                    case ChunkRenderObject.LeftFace: neighborX = 0; break;
                    case ChunkRenderObject.RightFace: neighborX = 1; break;
                    case ChunkRenderObject.BottomFace: neighborY = 0; break;
                    case ChunkRenderObject.TopFace: neighborY = 1; break;
                    case ChunkRenderObject.BackFace: neighborZ = 0; break;
                    case ChunkRenderObject.FrontFace: neighborZ = 1; break;
                }
                return !Occupies(GetState(neighbor.Data), neighborX, neighborY, neighborZ);
            }

            return neighbor.Block.Transparent ? neighbor.Block.BlockId != BlockId : !neighbor.Block.IsSolid(neighbor, neighborFace);
        }

        private static bool CoversFace(StairState state, int face)
        {
            for (int first = 0; first < 2; first++)
            for (int second = 0; second < 2; second++)
            {
                bool occupied = face switch
                {
                    ChunkRenderObject.TopFace => Occupies(state, first, 1, second),
                    ChunkRenderObject.BottomFace => Occupies(state, first, 0, second),
                    ChunkRenderObject.FrontFace => Occupies(state, first, second, 1),
                    ChunkRenderObject.BackFace => Occupies(state, first, second, 0),
                    ChunkRenderObject.LeftFace => Occupies(state, 0, first, second),
                    ChunkRenderObject.RightFace => Occupies(state, 1, first, second),
                    _ => throw new ArgumentOutOfRangeException(nameof(face), face, null)
                };
                if (!occupied) return false;
            }
            return true;
        }

        private static bool Occupies(StairState state, int x, int y, int z)
        {
            if (!IsCell(x, y, z)) return false;
            bool baseLayer = Half(state) == StairHalf.Bottom ? y == 0 : y == 1;
            return baseLayer || (HighMask(state) & (1 << (x + z * 2))) != 0;
        }

        private static StairState GetState(object state) => state is StairState stair && (int)stair is >= 0 and < StateCount
            ? stair
            : StairState.BottomNorthStraight;

        private static byte HighMask(StairState state) => HighLayerMasks[LocalState(state)];

        private static int LocalState(StairState state)
        {
            int value = (int)state;
            return value >= StatesPerHalf ? value - StatesPerHalf : value;
        }

        private static StairState FromMask(StairHalf half, byte mask)
        {
            int local = StateByHighLayerMask[mask];
            if (local < 0) throw new ArgumentOutOfRangeException(nameof(mask), mask, "Not a stair shape.");
            return (StairState)(local + (half == StairHalf.Top ? StatesPerHalf : 0));
        }

        private static byte StraightMask(StairFacing facing) => facing switch
        {
            StairFacing.North => NorthStraightMask,
            StairFacing.East => EastStraightMask,
            StairFacing.South => SouthStraightMask,
            StairFacing.West => WestStraightMask,
            _ => throw new ArgumentOutOfRangeException(nameof(facing), facing, null)
        };

        private static byte OuterMask(StairFacing facing, bool left)
        {
            Vector3Int direction = Direction(facing);
            Vector3Int side = Direction(left ? TurnLeft(facing) : TurnRight(facing));
            return (byte)(1 << ((direction.x + side.x > 0 ? 1 : 0) + (direction.z + side.z > 0 ? 2 : 0)));
        }

        private static byte InnerMask(StairFacing facing, bool left)
        {
            byte straight = StraightMask(facing);
            Vector3Int front = -Direction(facing);
            Vector3Int side = Direction(left ? TurnLeft(facing) : TurnRight(facing));
            int bit = (front.x + side.x > 0 ? 1 : 0) + (front.z + side.z > 0 ? 2 : 0);
            return (byte)(straight | (1 << bit));
        }

        private static StairFacing HighStepFacingFromPlayer()
        {
            if (Player.Instance?.cameraTransform == null) return StairFacing.North;
            // The player looks toward the stair's front/low step, so its high step belongs behind it in the
            // horizontal look direction. This is deliberately the opposite of a "riser faces player" convention.
            Vector3 look = Player.Instance.cameraTransform.forward;
            if (Mathf.Abs(look.x) > Mathf.Abs(look.z)) return look.x >= 0 ? StairFacing.East : StairFacing.West;
            return look.z >= 0 ? StairFacing.North : StairFacing.South;
        }

        private static Vector3Int Direction(StairFacing facing) => facing switch
        {
            StairFacing.North => Vector3Int.forward,
            StairFacing.East => Vector3Int.right,
            StairFacing.South => Vector3Int.back,
            StairFacing.West => Vector3Int.left,
            _ => throw new ArgumentOutOfRangeException(nameof(facing), facing, null)
        };

        private static StairFacing TurnLeft(StairFacing facing) => facing switch
        {
            StairFacing.North => StairFacing.West,
            StairFacing.West => StairFacing.South,
            StairFacing.South => StairFacing.East,
            StairFacing.East => StairFacing.North,
            _ => throw new ArgumentOutOfRangeException(nameof(facing), facing, null)
        };

        private static StairFacing TurnRight(StairFacing facing) => facing switch
        {
            StairFacing.North => StairFacing.East,
            StairFacing.East => StairFacing.South,
            StairFacing.South => StairFacing.West,
            StairFacing.West => StairFacing.North,
            _ => throw new ArgumentOutOfRangeException(nameof(facing), facing, null)
        };

        private static bool IsOuter(StairShape shape) => shape is StairShape.OuterLeft or StairShape.OuterRight;

        private static bool IsInner(StairShape shape) => shape is StairShape.InnerLeft or StairShape.InnerRight;

        private static bool SameAxis(StairFacing first, StairFacing second) => ((int)first & 1) == ((int)second & 1);

        private static int Opposite(int face) => face switch
        {
            ChunkRenderObject.TopFace => ChunkRenderObject.BottomFace,
            ChunkRenderObject.BottomFace => ChunkRenderObject.TopFace,
            ChunkRenderObject.FrontFace => ChunkRenderObject.BackFace,
            ChunkRenderObject.BackFace => ChunkRenderObject.FrontFace,
            ChunkRenderObject.LeftFace => ChunkRenderObject.RightFace,
            ChunkRenderObject.RightFace => ChunkRenderObject.LeftFace,
            _ => throw new ArgumentOutOfRangeException(nameof(face), face, null)
        };

        private static bool IsCell(int x, int y, int z) => (uint)x < 2 && (uint)y < 2 && (uint)z < 2;

        private static sbyte[] CreateStateByHighLayerMask()
        {
            sbyte[] result = new sbyte[16];
            for (int i = 0; i < result.Length; i++) result[i] = -1;
            for (sbyte state = 0; state < StatesPerHalf; state++) result[HighLayerMasks[state]] = state;
            return result;
        }

        private static CellGeometry[] CreateCells()
        {
            CellGeometry[] cells = new CellGeometry[8];
            for (int cell = 0; cell < cells.Length; cell++)
            {
                int x = cell & 1;
                int z = (cell >> 1) & 1;
                int y = cell >> 2;
                Vector3 min = new(x * .5f, y * .5f, z * .5f);
                Vector3 max = min + Vector3.one * .5f;
                MeshBuilder.CubicModel model = new(new[]
                {
                    new Vector3(min.x, min.y, min.z), new Vector3(max.x, min.y, min.z),
                    new Vector3(max.x, max.y, min.z), new Vector3(min.x, max.y, min.z),
                    new Vector3(min.x, min.y, max.z), new Vector3(max.x, min.y, max.z),
                    new Vector3(max.x, max.y, max.z), new Vector3(min.x, max.y, max.z)
                }, Triangles, Array.Empty<Vector2>());
                Vector2[][] faceUvs = new Vector2[6][];
                for (int face = 0; face < faceUvs.Length; face++) faceUvs[face] = FaceUvs(face, model);
                cells[cell] = new CellGeometry(model, faceUvs);
            }
            return cells;
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
                    _ => throw new ArgumentOutOfRangeException(nameof(face), face, null)
                };
            }
            return uvs;
        }

        private sealed class CellGeometry
        {
            public readonly MeshBuilder.CubicModel Model;
            public readonly Vector2[][] FaceUvs;

            public CellGeometry(MeshBuilder.CubicModel model, Vector2[][] faceUvs)
            {
                Model = model;
                FaceUvs = faceUvs;
            }
        }
    }
}
