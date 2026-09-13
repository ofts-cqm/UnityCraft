using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using world.blocks;
using world.persistence;
using World;
using World.blocks;

namespace Tests.Editor
{
    public sealed class BlockUpdateBehaviorTests
    {
        private GameObject _worldObject;
        private World.World _world;
        private Chunk _chunk;

        [SetUp]
        public void SetUp()
        {
            _worldObject = new GameObject("Block update test world");
            _worldObject.SetActive(false);
            _world = _worldObject.AddComponent<World.World>();
            _chunk = CreateEmptyChunk(_world);
            _world.ChunkMap.Add(_chunk.ChunkPosition, _chunk);
            SetField(_chunk, "_active", true);
            World.World.Instance = _world;
        }

        [TearDown]
        public void TearDown()
        {
            if (_worldObject != null) UnityEngine.Object.DestroyImmediate(_worldObject);
            World.World.Instance = null;
        }

        [Test]
        public void GrassDecaysUnderSolidBottomFaceOrWater()
        {
            Vector3Int position = new(8, 64, 8);
            _world.SetBlock(position, Blocks.GrassBlock);
            _world.SetBlock(position + Vector3Int.up, Blocks.Stone);

            Blocks.GrassBlock.OnRandomTick(_world, _world.GetBlock(position));
            Assert.AreEqual(Blocks.Dirt.BlockId, _world.GetBlock(position).Block.BlockId);

            _world.SetBlock(position, Blocks.GrassBlock);
            _world.SetBlock(position + Vector3Int.up, Blocks.Air);
            _world.SetFluid(position + Vector3Int.up, FluidState.Source);
            Blocks.GrassBlock.OnRandomTick(_world, _world.GetBlock(position));
            Assert.AreEqual(Blocks.Dirt.BlockId, _world.GetBlock(position).Block.BlockId);
        }

        [Test]
        public void ExposedDirtSpreadsFromExposedGrassInItsTwentySixNeighbors()
        {
            Vector3Int dirt = new(8, 64, 8);
            Vector3Int diagonalGrass = dirt + new Vector3Int(1, 1, 1);
            _world.SetBlock(dirt, Blocks.Dirt);
            _world.SetBlock(diagonalGrass, Blocks.GrassBlock);

            Blocks.Dirt.OnRandomTick(_world, _world.GetBlock(dirt));

            Assert.AreEqual(Blocks.GrassBlock.BlockId, _world.GetBlock(dirt).Block.BlockId);
        }

        [Test]
        public void LeavesUseFourStepOrthogonalPathsAndDeferAtUnloadedBorders()
        {
            Vector3Int connected = new(7, 80, 8);
            for (int x = 7; x <= 10; x++) _world.SetBlock(new Vector3Int(x, 80, 8), Blocks.OakLeave);
            _world.SetBlock(new Vector3Int(11, 80, 8), Blocks.OakLog);
            Blocks.OakLeave.OnRandomTick(_world, _world.GetBlock(connected));
            Assert.AreEqual(Blocks.OakLeave.BlockId, _world.GetBlock(connected).Block.BlockId);

            Vector3Int tooFar = new(7, 90, 8);
            for (int x = 7; x <= 11; x++) _world.SetBlock(new Vector3Int(x, 90, 8), Blocks.OakLeave);
            _world.SetBlock(new Vector3Int(12, 90, 8), Blocks.OakLog);
            Blocks.OakLeave.OnRandomTick(_world, _world.GetBlock(tooFar));
            Assert.AreEqual(Blocks.Air.BlockId, _world.GetBlock(tooFar).Block.BlockId);

            Vector3Int border = new(15, 100, 8);
            _world.SetBlock(border, Blocks.OakLeave);
            Blocks.OakLeave.OnRandomTick(_world, _world.GetBlock(border));
            Assert.AreEqual(Blocks.OakLeave.BlockId, _world.GetBlock(border).Block.BlockId,
                "An unavailable neighboring chunk must leave distance unknown rather than decay the leaf.");
        }

        [Test]
        public void SupportedGravityBlockConsumesItsScheduledUpdateAfterTwoTicks()
        {
            Vector3Int position = new(8, 64, 8);
            _world.SetBlock(position + Vector3Int.down, Blocks.Stone);
            _world.SetBlock(position, Blocks.Sand);
            object scheduler = GetField(_world, "_blockUpdateScheduler");
            Assert.AreEqual(1, PendingCount(scheduler));

            SetField(_world, "_gameplayReady", true);
            Invoke(_world, "FixedUpdate");
            Assert.AreEqual(Blocks.Sand.BlockId, _world.GetBlock(position).Block.BlockId);
            Assert.AreEqual(1, PendingCount(scheduler));

            Invoke(_world, "FixedUpdate");
            Assert.AreEqual(Blocks.Sand.BlockId, _world.GetBlock(position).Block.BlockId);
            Assert.AreEqual(0, PendingCount(scheduler));

            _world.SetBlock(position + Vector3Int.left, Blocks.Stone);
            Assert.AreEqual(1, PendingCount(scheduler), "A changed horizontal neighbor must update the sand.");
            Invoke(_world, "FixedUpdate");
            Invoke(_world, "FixedUpdate");
            Assert.AreEqual(0, PendingCount(scheduler));
            _world.SetBlock(position + Vector3Int.left, Blocks.Stone);
            Assert.AreEqual(0, PendingCount(scheduler), "An equivalent write must not create an update.");
        }

        [Test]
        public void UnsupportedGravityBlockBecomesAPersistableFallingEntityAfterTwoTicks()
        {
            Type fallingSystemType = typeof(Chunk).Assembly.GetType("World.FallingBlockSystem", true);
            object fallingSystem = Activator.CreateInstance(fallingSystemType,
                BindingFlags.Instance | BindingFlags.NonPublic, null, new object[] { _world }, null);
            SetField(_world, "_fallingBlocks", fallingSystem);
            SetField(_world, "_gameplayReady", true);
            Vector3Int position = new(8, 64, 8);
            _world.SetBlock(position, Blocks.Gravel);

            Invoke(_world, "FixedUpdate");
            Assert.AreEqual(Blocks.Gravel.BlockId, _world.GetBlock(position).Block.BlockId);
            Invoke(_world, "FixedUpdate");

            Assert.AreEqual(Blocks.Air.BlockId, _world.GetBlock(position).Block.BlockId);
            MethodInfo capture = fallingSystemType.GetMethod("Capture",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(capture);
            FallingBlockSnapshot[] snapshots = (FallingBlockSnapshot[])capture.Invoke(fallingSystem,
                new object[] { _chunk });
            Assert.AreEqual(1, snapshots.Length);
            Assert.AreEqual(Blocks.Gravel.BlockId, snapshots[0].BlockId);
            Assert.AreEqual((Vector3)position, snapshots[0].Position);
            Assert.AreEqual(Vector3.zero, snapshots[0].Velocity);

            MethodInfo intersectsFallingBlock = typeof(World.World).GetMethod("IntersectsFallingBlock",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(intersectsFallingBlock);
            Assert.IsTrue((bool)intersectsFallingBlock.Invoke(_world,
                new object[] { (Vector3)position + Vector3.one * 0.5f, Vector3.one * 0.45f }));
            Assert.IsFalse((bool)intersectsFallingBlock.Invoke(_world,
                new object[] { (Vector3)position + Vector3.right * 2f + Vector3.one * 0.5f, Vector3.one * 0.45f }));

            IList entities = (IList)GetField(fallingSystem, "_all");
            Component entity = (Component)entities[0];
            entity.transform.SetParent(null);
            SetField(entity, "_waitingForColliderRefresh", false);
            Rigidbody body = entity.GetComponent<Rigidbody>();
            body.isKinematic = false;
            body.linearVelocity = new Vector3(0, -100, 0);
            Invoke(entity, "FixedUpdate");
            Assert.AreEqual(-70f, body.linearVelocity.y);

            SetField(_world, "_gameplayReady", false);
            Vector3Int support = new(8, 50, 8);
            _world.SetBlock(support, Blocks.Stone);
            body.position = new Vector3(8, 51.01f, 8);
            MethodInfo settle = fallingSystemType.GetMethod("TrySettle",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(settle);
            settle.Invoke(fallingSystem, new object[] { entity });
            Assert.AreEqual(Blocks.Gravel.BlockId,
                _world.GetBlock(support + Vector3Int.up).Block.BlockId);
            Assert.IsFalse((bool)intersectsFallingBlock.Invoke(_world,
                new object[] { (Vector3)position + Vector3.one * 0.5f, Vector3.one * 0.45f }),
                "A pooled falling entity must stop blocking placement after it settles.");
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

        private static int PendingCount(object scheduler)
        {
            PropertyInfo property = scheduler.GetType().GetProperty("PendingCount",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(property);
            return (int)property.GetValue(scheduler);
        }

        private static object GetField(object target, string name)
        {
            FieldInfo field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field);
            return field.GetValue(target);
        }

        private static void SetField(object target, string name, object value)
        {
            FieldInfo field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field);
            field.SetValue(target, value);
        }

        private static void Invoke(object target, string name)
        {
            MethodInfo method = target.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(method);
            method.Invoke(target, Array.Empty<object>());
        }
    }
}
