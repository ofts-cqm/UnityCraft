using System;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using Render;
using render;
using UnityEngine;
using world.blocks;
using world.persistence;
using World;
using World.blocks;

namespace Tests.Editor
{
    public sealed class StairTests
    {
        private GameObject _worldObject;
        private World.World _world;

        [SetUp]
        public void SetUp()
        {
            _worldObject = new GameObject("stair test world");
            _worldObject.SetActive(false);
            _world = _worldObject.AddComponent<World.World>();
            Chunk chunk = CreateEmptyChunk(_world);
            _world.ChunkMap.Add(chunk.ChunkPosition, chunk);
            SetField(chunk, "_active", true);
            World.World.Instance = _world;
        }

        [TearDown]
        public void TearDown()
        {
            if (_worldObject != null) UnityEngine.Object.DestroyImmediate(_worldObject);
            World.World.Instance = null;
        }

        [Test]
        public void StairStatesAreCompactStableAndCanonicalByVisibleGeometry()
        {
            for (int stateId = 0; stateId < 24; stateId++)
                Assert.AreEqual(stateId, Blocks.OakStairs.EncodeState(Blocks.OakStairs.DecodeState(stateId)));
            Assert.Throws<InvalidDataException>(() => Blocks.OakStairs.DecodeState(24));

            Assert.AreEqual(StairState.BottomNorthWestOuter,
                Stair.Create(StairHalf.Bottom, StairFacing.North, StairShape.OuterLeft));
            Assert.AreEqual(Stair.Create(StairHalf.Bottom, StairFacing.North, StairShape.OuterLeft),
                Stair.Create(StairHalf.Bottom, StairFacing.West, StairShape.OuterRight),
                "Both placement histories produce the same north-west outer-corner mesh.");
            Assert.AreEqual(Stair.Create(StairHalf.Bottom, StairFacing.North, StairShape.InnerLeft),
                Stair.Create(StairHalf.Bottom, StairFacing.West, StairShape.InnerRight),
                "Both placement histories produce the same inner-corner mesh.");
        }

        [Test]
        public void StairFluidLimitsUseOpenEdgesAndClosedFaces()
        {
            BlockState lower = Blocks.OakStairs.AsState(Vector3Int.zero, StairState.BottomNorthStraight);
            Assert.AreEqual((0, 10), Blocks.OakStairs.GetFlowingAmountLimit(lower, ChunkRenderObject.FrontFace));
            Assert.AreEqual((9, 5), Blocks.OakStairs.GetFlowingAmountLimit(lower, ChunkRenderObject.BackFace));
            Assert.AreEqual((0, 10), Blocks.OakStairs.GetFlowingAmountLimit(lower, ChunkRenderObject.BottomFace));

            BlockState upper = Blocks.OakStairs.AsState(Vector3Int.zero, StairState.TopNorthStraight);
            Assert.AreEqual((0, 10), Blocks.OakStairs.GetFlowingAmountLimit(upper, ChunkRenderObject.TopFace));
            Assert.AreEqual((4, 0), Blocks.OakStairs.GetFlowingAmountLimit(upper, ChunkRenderObject.BackFace));
        }

        [Test]
        public void StairMeshUsesHalfCellGeometryAndUnscaledPlankUvs()
        {
            MeshBuilder builder = new();
            Blocks.OakStairs.Render(Blocks.OakStairs.AsState(Vector3Int.zero, StairState.BottomNorthWestOuter),
                new AirBlockProvider(), builder, Vector3Int.zero, Vector3.zero);

            Assert.IsFalse(builder.OpaqueMesh.IsEmpty);
            Assert.AreEqual(builder.OpaqueMesh.Vertices.Count, builder.OpaqueMesh.Uvs.Count);
            Assert.AreEqual(builder.OpaqueMesh.Vertices.Count, builder.ColliderMesh.Vertices.Count);
            bool sawHalfCoordinate = false;
            foreach (Vector3 vertex in builder.OpaqueMesh.Vertices)
            {
                Assert.That(vertex.x, Is.InRange(0f, 1f));
                Assert.That(vertex.y, Is.InRange(0f, 1f));
                Assert.That(vertex.z, Is.InRange(0f, 1f));
                sawHalfCoordinate |= Mathf.Approximately(vertex.x, .5f) || Mathf.Approximately(vertex.y, .5f) ||
                                     Mathf.Approximately(vertex.z, .5f);
            }
            Assert.IsTrue(sawHalfCoordinate);
            foreach (Vector2 uv in builder.OpaqueMesh.Uvs)
            {
                Assert.That(uv.x, Is.InRange(0f, 1f));
                Assert.That(uv.y, Is.InRange(0f, 1f));
            }
            Assert.AreEqual(90, builder.OpaqueMesh.TextureIndices[0].x);
        }

        [Test]
        public void StraightStairsUpgradeForNeighborsAndCornersDoNotRevert()
        {
            Vector3Int outer = new(8, 64, 8);
            _world.SetBlock(outer, Blocks.OakStairs, StairState.BottomNorthStraight);
            _world.SetBlock(outer + Vector3Int.forward, Blocks.OakStairs, StairState.BottomEastStraight);

            Assert.AreEqual(StairState.BottomNorthEastOuter, _world.GetBlock(outer).Data);
            _world.SetBlock(outer + Vector3Int.forward, Blocks.Air);
            Assert.AreEqual(StairState.BottomNorthEastOuter, _world.GetBlock(outer).Data,
                "Removing the neighbor must leave the existing corner unchanged.");

            Vector3Int inner = new(10, 64, 8);
            _world.SetBlock(inner, Blocks.OakStairs, StairState.BottomNorthStraight);
            _world.SetBlock(inner + Vector3Int.back, Blocks.OakStairs, StairState.BottomEastStraight);
            Assert.AreEqual(StairState.BottomNorthRightInner, _world.GetBlock(inner).Data);

            Vector3Int mixedHalf = new(12, 64, 8);
            _world.SetBlock(mixedHalf, Blocks.OakStairs, StairState.BottomNorthStraight);
            _world.SetBlock(mixedHalf + Vector3Int.forward, Blocks.OakStairs, StairState.TopEastStraight);
            Assert.AreEqual(StairState.BottomNorthStraight, _world.GetBlock(mixedHalf).Data,
                "Only same-half stairs may form a corner.");

            Vector3Int canonicalNeighbor = new(14, 64, 8);
            _world.SetBlock(canonicalNeighbor, Blocks.OakStairs, StairState.BottomNorthStraight);
            _world.SetBlock(canonicalNeighbor + Vector3Int.forward, Blocks.OakStairs,
                StairState.BottomNorthEastOuter);
            Assert.AreEqual(StairState.BottomNorthStraight, _world.GetBlock(canonicalNeighbor).Data,
                "A canonical corner cannot be a source for another shape because its original facing is not stored.");

            Vector3Int canonicalInnerNeighbor = new(6, 64, 10);
            _world.SetBlock(canonicalInnerNeighbor, Blocks.OakStairs, StairState.BottomNorthStraight);
            _world.SetBlock(canonicalInnerNeighbor + Vector3Int.back, Blocks.OakStairs,
                StairState.BottomNorthRightInner);
            Assert.AreEqual(StairState.BottomNorthStraight, _world.GetBlock(canonicalInnerNeighbor).Data);
        }

        private static Chunk CreateEmptyChunk(World.World world)
        {
            ChunkSnapshot snapshot = new(new ChunkCoord(0, 0), new int[ChunkSnapshot.CellCount],
                new int[ChunkSnapshot.CellCount], new byte[ChunkSnapshot.CellCount]);
            ConstructorInfo constructor = typeof(Chunk).GetConstructor(BindingFlags.Instance | BindingFlags.NonPublic,
                null, new[] { typeof(ChunkCoord), typeof(World.World), typeof(ChunkSnapshot) }, null);
            Assert.IsNotNull(constructor);
            return (Chunk)constructor.Invoke(new object[] { snapshot.Coord, world, snapshot });
        }

        private static void SetField(object target, string name, object value)
        {
            FieldInfo field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field);
            field.SetValue(target, value);
        }

        private sealed class AirBlockProvider : IBlockProvider
        {
            public BlockState GetBlock(Vector3Int position) => Blocks.Air.AsState(position);
            public BlockState GetBlock(int x, int y, int z) => Blocks.Air.AsState(x, y, z);
        }
    }
}
