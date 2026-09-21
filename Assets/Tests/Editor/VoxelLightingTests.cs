using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading;
using lighting;
using NUnit.Framework;
using render;
using UnityEngine;
using UnityEngine.Rendering;
using World;
using world.blocks;
using world.lighting;
using world.persistence;

namespace Tests.Editor
{
    public sealed class VoxelLightingTests
    {
        private static readonly ChunkCoord Origin = new(0, 0);
        private static LightColumn Solid()
        {
            var column = new LightColumn();
            for (int i = 0; i < column.Occupancy.Length; i++) column.Occupancy[i] = 255;
            return column;
        }
        private static void Air(LightColumn column, int x, int y, int z) => column.Occupancy[VoxelLightSolver.Index(x, y, z)] = 0;
        private static Dictionary<ChunkCoord, LightColumn> Solve(LightColumn column,
            Dictionary<Vector3Int, byte> sources = null) => VoxelLightSolver.Solve(
            new Dictionary<ChunkCoord, LightColumn> { [Origin] = column }, new HashSet<ChunkCoord> { Origin },
            sources ?? new Dictionary<Vector3Int, byte>());

        [Test]
        public void SealedRoomHasNoSkyOrLocalLight()
        {
            var c = Solid();
            for (int y = 5; y < 10; y++) for (int x = 4; x < 12; x++) for (int z = 4; z < 12; z++) Air(c, x, y, z);
            var result = Solve(c)[Origin];
            Assert.That(result.Sky, Is.All.Zero);
            Assert.That(result.Local, Is.All.Zero);
        }

        [Test]
        public void OpenShaftStaysBrightAndDaylightExpiresAlongTunnel()
        {
            var left = Solid(); var right = Solid();
            for (int y = 5; y < 128; y++) Air(left, 1, y, 8);
            for (int x = 1; x < 16; x++) Air(left, x, 5, 8);
            for (int x = 0; x < 16; x++) Air(right, x, 5, 8);
            var rc = new ChunkCoord(1, 0);
            var result = VoxelLightSolver.Solve(new() { [Origin] = left, [rc] = right }, new() { Origin, rc }, new Dictionary<Vector3Int, byte>());
            Assert.AreEqual(15, result[Origin].Sky[VoxelLightSolver.Index(1, 5, 8)]);
            Assert.AreEqual(1, result[Origin].Sky[VoxelLightSolver.Index(15, 5, 8)]);
            Assert.AreEqual(0, result[rc].Sky[VoxelLightSolver.Index(0, 5, 8)]);
        }

        [Test]
        public void ClosingShaftRemovesPreviouslyPropagatedSky()
        {
            var c = Solid();
            for (int y = 4; y < 128; y++) Air(c, 8, y, 8);
            var result = Solve(c);
            result[Origin].Occupancy[VoxelLightSolver.Index(8, 120, 8)] = 255;
            result = VoxelLightSolver.Solve(result, new() { Origin }, new Dictionary<Vector3Int, byte>());
            Assert.AreEqual(0, result[Origin].Sky[VoxelLightSolver.Index(8, 4, 8)]);
        }

        [Test]
        public void LocalLightCrossesChunkBoundaryAndRemovalDoesNotLeaveGhosts()
        {
            var left = Solid(); var right = Solid();
            for (int x = 0; x < 16; x++) { Air(left, x, 8, 8); Air(right, x, 8, 8); }
            var rc = new ChunkCoord(1, 0);
            var columns = new Dictionary<ChunkCoord, LightColumn> { [Origin] = left, [rc] = right };
            var sources = new Dictionary<Vector3Int, byte> { [new Vector3Int(15, 8, 8)] = 15, [new Vector3Int(20, 8, 8)] = 10 };
            var result = VoxelLightSolver.Solve(columns, new() { Origin }, sources);
            Assert.AreEqual(14, result[rc].Local[VoxelLightSolver.Index(0, 8, 8)]);
            sources.Remove(new Vector3Int(15, 8, 8));
            result = VoxelLightSolver.Solve(result, new() { Origin }, sources);
            Assert.AreEqual(6, result[rc].Local[VoxelLightSolver.Index(0, 8, 8)]);
            sources.Clear();
            result = VoxelLightSolver.Solve(result, new() { rc }, sources);
            Assert.That(result[Origin].Local, Is.All.Zero);
            Assert.That(result[rc].Local, Is.All.Zero);
        }

        [Test]
        public void MissingNeighborDoesNotSupplySky()
        {
            var c = Solid();
            for (int x = 0; x < 16; x++) Air(c, x, 8, 8);
            Assert.That(Solve(c)[Origin].Sky, Is.All.Zero);
        }

        [Test]
        public void LeavesAndWaterAttenuateVerticalSkylight()
        {
            var c = Solid();
            for (int y = 5; y < 128; y++) Air(c, 8, y, 8);
            c.Absorption[VoxelLightSolver.Index(8, 126, 8)] = 1;
            c.Absorption[VoxelLightSolver.Index(8, 125, 8)] = 2;
            var result = Solve(c)[Origin];
            Assert.AreEqual(14, result.Sky[VoxelLightSolver.Index(8, 126, 8)]);
            Assert.AreEqual(12, result.Sky[VoxelLightSolver.Index(8, 125, 8)]);
            Assert.AreEqual(12, result.Sky[VoxelLightSolver.Index(8, 5, 8)]);
        }

        [Test]
        public void PartialBlockOpeningsRespectMatchingQuadrants()
        {
            var slab = Blocks.WoodSlab.Oak;
            byte bottom = VoxelLightSolver.Occupancy(slab, (ushort)slab.EncodeState(SlabPart.Bottom));
            byte top = VoxelLightSolver.Occupancy(slab, (ushort)slab.EncodeState(SlabPart.Top));
            Assert.AreEqual(0x0f, bottom);
            Assert.AreEqual(0xf0, top);
            Assert.IsTrue(VoxelLightSolver.Connects(bottom, bottom, 5));
            Assert.IsFalse(VoxelLightSolver.Connects(bottom, top, 5));
            Assert.IsFalse(VoxelLightSolver.Connects(bottom, 0, 1));
            for (ushort state = 0; state < 24; state++)
            {
                byte mask = VoxelLightSolver.Occupancy(Blocks.WoodStairs.Oak, state);
                for (int bit = 0; bit < 8; bit++)
                    Assert.AreEqual(Stair.Occupies((StairState)state, bit & 1, bit >> 2, (bit >> 1) & 1), (mask & (1 << bit)) != 0);
            }
            Assert.AreEqual(0, VoxelLightSolver.Occupancy(Blocks.StainedGlass.White, 0));
        }

        [Test]
        public void OpaqueEmissiveBlockEmitsButWallStopsPropagation()
        {
            var c = Solid();
            for (int x = 4; x < 8; x++) Air(c, x, 8, 8);
            Air(c, 9, 8, 8);
            c.Emission[VoxelLightSolver.Index(3, 8, 8)] = 15;
            var result = Solve(c)[Origin];
            Assert.AreEqual(14, result.Local[VoxelLightSolver.Index(4, 8, 8)]);
            Assert.AreEqual(0, result.Local[VoxelLightSolver.Index(9, 8, 8)]);
        }

        [Test]
        public void SolverCanCancelMassiveWork()
        {
            Assert.IsNull(VoxelLightSolver.Solve(new() { [Origin] = new LightColumn() }, new() { Origin },
                new Dictionary<Vector3Int, byte>(), () => true));
        }

        [TestCase(true)]
        [TestCase(false)]
        public void LightStreamUploadPreservesGeometryAndTextureIndices(bool flatNormals)
        {
            var builder = new MeshBuilder();
            builder.AddFace(0, Vector3.zero, Blocks.Stone, MeshBuilder.MeshTargets.Opaque);
            var mesh = new Mesh();
            try
            {
                builder.OpaqueMesh.UploadTo(mesh, flatNormals);
                Assert.AreEqual(1, mesh.GetVertexAttributeStream(VertexAttribute.Color));
                Vector3[] before = mesh.vertices;
                CollectionAssert.AreEqual(builder.OpaqueMesh.Vertices, before);
                var expectedBounds = new Bounds(new Vector3(.5f, 1, .5f), new Vector3(1, flatNormals ? 0 : .5f, 1));
                Assert.AreEqual(expectedBounds, mesh.bounds);
                var uvBefore = new List<Vector4>(); mesh.GetUVs(1, uvBefore);
                var colors = new Color32[mesh.vertexCount];
                for (int i = 0; i < colors.Length; i++) colors[i] = new Color32(127, 34, 0, 255);
                mesh.SetVertexBufferData(colors, 0, 0, colors.Length, 1, MeshUpdateFlags.DontRecalculateBounds);
                CollectionAssert.AreEqual(before, mesh.vertices);
                var uvAfter = new List<Vector4>(); mesh.GetUVs(1, uvAfter);
                CollectionAssert.AreEqual(uvBefore, uvAfter);
                CollectionAssert.AreEqual(colors, mesh.colors32);
                Assert.AreEqual(expectedBounds, mesh.bounds);
            }
            finally { UnityEngine.Object.DestroyImmediate(mesh); }
        }

        [Test]
        public void CycleHasCorrectHorizonAndNightTiming()
        {
            Assert.AreEqual(0, DaylightCycle.SolarElevation(0), .0001);
            Assert.AreEqual(1, DaylightCycle.SolarElevation(300), .0001);
            Assert.AreEqual(0, DaylightCycle.SolarElevation(600), .0001);
            Assert.AreEqual(-1, DaylightCycle.SolarElevation(900), .0001);
            Assert.AreEqual(0, DaylightCycle.SolarElevation(1200), .0001);
        }

        [Test]
        public void TerrainMaterialsKeepGlassTransparentAndOpaqueBlocksShadowCasting()
        {
            var opaque = Resources.Load<Material>("VoxelMaterial");
            var glass = Resources.Load<Material>("TransparentVoxelMaterial");
            Assert.AreEqual("UnityCraft/Voxel Terrain", opaque.shader.name);
            Assert.AreEqual(opaque.shader, glass.shader);
            Assert.AreEqual((float)BlendMode.SrcAlpha, glass.GetFloat("_SrcBlend"));
            Assert.AreEqual((float)BlendMode.OneMinusSrcAlpha, glass.GetFloat("_DstBlend"));
            Assert.AreEqual(0, glass.GetFloat("_ZWrite"));
            Assert.Less(glass.GetFloat("_Cutoff"), 1f / 255);
            Assert.AreEqual(3000, glass.renderQueue);
            Assert.AreEqual(1, opaque.GetFloat("_CastVoxelShadows"));
            Assert.AreEqual(0, glass.GetFloat("_CastVoxelShadows"));
        }

        [Test]
        public void ClockIsOptionalAndSurvivesDescriptorVersionWrites()
        {
            string path = Path.Combine(Path.GetTempPath(), "UnityCraftLightingTests-" + Guid.NewGuid().ToString("N"));
            try
            {
                var storage = new FileWorldStorage(path);
                var descriptor = storage.CreateWorld("clock", "Clock", "123");
                Assert.IsNull(descriptor.daylight);
                storage.SaveClock("clock", 987.25);
                storage.UpdateWorldVersion("clock", SaveVersionPolicy.Current);
                Assert.AreEqual(987.25, storage.ReadWorldDescriptor("clock").daylight.elapsedSeconds);
                Assert.AreEqual(2, storage.ReadWorldDescriptor("clock").schemaVersion);
                Assert.Throws<ArgumentOutOfRangeException>(() => storage.SaveClock("clock", double.NaN));
            }
            finally { if (Directory.Exists(path)) Directory.Delete(path, true); }
        }

        [Test]
        public void WorkerPublishesLatestSourcesAndRejectsUnloadedChunkResults()
        {
            var root = new GameObject("Lighting worker test");
            root.SetActive(false); // Do not initialize the gameplay scene/save lifecycle.
            var world = root.AddComponent<World.World>();
            world.player = root.transform;
            var ids = new int[ChunkSnapshot.CellCount];
            for (int i = 0; i < ids.Length; i++) ids[i] = Blocks.Stone.BlockId;
            for (int x = 1; x < 15; x++) ids[ChunkSnapshot.Index(x, 8, 8)] = 0;
            var snapshot = new ChunkSnapshot(Origin, ids, new int[ids.Length], new byte[ids.Length]);
            var constructor = typeof(Chunk).GetConstructor(BindingFlags.NonPublic | BindingFlags.Instance, null,
                new[] { typeof(ChunkCoord), typeof(World.World), typeof(ChunkSnapshot) }, null);
            var chunk = (Chunk)constructor.Invoke(new object[] { Origin, world, snapshot });
            world.ChunkMap.Add(Origin, chunk);
            try
            {
                using var lighting = new WorldLighting(world);
                lighting.Invalidate(Origin);
                var p = new Vector3Int(8, 8, 8);
                int source = lighting.RegisterSource(p, 15);
                WaitFor(() => lighting.Sample((Vector3)p + Vector3.one * .5f).y == 1, lighting);
                lighting.UpdateSource(source, p, 4);
                WaitFor(() => Mathf.Abs(lighting.Sample((Vector3)p + Vector3.one * .5f).y - 4 / 15f) < .001f, lighting);
                lighting.RemoveSource(source);
                WaitFor(() => lighting.Sample((Vector3)p + Vector3.one * .5f).y == 0, lighting);
                lighting.RegisterSource(p, 15);
                world.ChunkMap.Remove(Origin);
                lighting.Invalidate(Origin);
                for (int i = 0; i < 20; i++) { lighting.Pump(10); Thread.Sleep(1); }
                Assert.AreEqual(Vector2.zero, lighting.Sample((Vector3)p + Vector3.one * .5f));
                world.ChunkMap.Add(Origin, chunk);
                lighting.Invalidate(Origin);
                WaitFor(() => lighting.Sample((Vector3)p + Vector3.one * .5f).y == 1, lighting);
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        private static void WaitFor(Func<bool> condition, WorldLighting lighting)
        {
            var watch = System.Diagnostics.Stopwatch.StartNew();
            while (!condition() && watch.ElapsedMilliseconds < 3000) { lighting.Pump(10); Thread.Sleep(1); }
            Assert.IsTrue(condition(), "Lighting worker did not publish the expected current result.");
        }

        [TestCase(true)]
        [TestCase(false)]
        public void RemeshPreservesAcceptedLightingAcrossReorderedVerticesAndRapidEdits(bool flatNormals)
        {
            var root = new GameObject("Lighting remesh test");
            root.SetActive(false);
            var world = root.AddComponent<World.World>();
            world.player = root.transform;
            var ids = new int[ChunkSnapshot.CellCount];
            var snapshot = new ChunkSnapshot(Origin, ids, new int[ids.Length], new byte[ids.Length]);
            var constructor = typeof(Chunk).GetConstructor(BindingFlags.NonPublic | BindingFlags.Instance, null,
                new[] { typeof(ChunkCoord), typeof(World.World), typeof(ChunkSnapshot) }, null);
            world.ChunkMap.Add(Origin, (Chunk)constructor.Invoke(new object[] { Origin, world, snapshot }));
            var mesh = new Mesh();
            try
            {
                using var lighting = new WorldLighting(world);
                lighting.Invalidate(Origin);
                int source = lighting.RegisterSource(new Vector3Int(8, 9, 8), 15);
                var builder = new MeshBuilder();
                foreach (int x in new[] { 2, 8, 11 })
                    builder.AddFace(0, new Vector3(x, 8, 8), Blocks.Stone, MeshBuilder.MeshTargets.Opaque);
                var data = builder.OpaqueMesh;
                data.UploadTo(mesh, flatNormals);
                var binding = lighting.Bind(mesh, data.Vertices, data.Normals, Vector3.zero);
                // Force the worker to see the source/mesh before Pump supplies the dirty chunk.
                // That incomplete result must not count as a valid first light upload.
                var gate = typeof(WorldLighting).GetField("_gate", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(lighting);
                var ready = (HashSet<WorldLighting.MeshBinding>)typeof(WorldLighting)
                    .GetField("_readyMeshes", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(lighting);
                Assert.IsTrue(SpinWait.SpinUntil(() => { lock (gate) return ready.Count > 0; }, 3000));
                lock (gate)
                {
                    lighting.Pump(10);
                    var awaiting = (HashSet<WorldLighting.MeshBinding>)typeof(WorldLighting)
                        .GetField("_awaitingFirstUpload", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(lighting);
                    Assert.IsTrue(awaiting.Contains(binding), "Source revision certified an unsupplied chunk snapshot.");
                }
                WaitFor(() => lighting.IsReady(Origin), lighting);
                var accepted = new Dictionary<(Vector3, Vector3), Color32>();
                var colors = mesh.colors32;
                for (int i = 0; i < colors.Length; i++)
                {
                    Assert.Greater(colors[i].r, 0);
                    Assert.Greater(colors[i].g, 0);
                    accepted[(data.Vertices[i], data.Normals[i])] = colors[i];
                }

                // Reorder retained faces, remove one and add another. Then edit again before
                // pumping any result: both remeshes must inherit the last accepted snapshot.
                foreach (int[] order in new[] { new[] { 11, 4, 2, 6 }, new[] { 2, 4, 11 } })
                {
                    builder.Clear();
                    foreach (int x in order)
                        builder.AddFace(0, new Vector3(x, 8, 8), Blocks.Stone, MeshBuilder.MeshTargets.Opaque);
                    data.UploadTo(mesh, flatNormals);
                    binding = lighting.Bind(mesh, data.Vertices, data.Normals, Vector3.zero, binding);
                    colors = mesh.colors32;
                    for (int i = 0; i < colors.Length; i++)
                    {
                        if (accepted.TryGetValue((data.Vertices[i], data.Normals[i]), out var old))
                            Assert.AreEqual(old, colors[i], "Unchanged face flashed during remesh.");
                        else
                            Assert.Greater(colors[i].r, 0, "New face should use the published field.");
                    }
                    Assert.IsFalse(lighting.IsReady(Origin), "Provisional colors are not an accepted upload.");
                }

                // Cached colors must not become permanent, and retired jobs must never upload
                // colors for the old vertex order to the shared mesh.
                lighting.RemoveSource(source);
                WaitFor(() =>
                {
                    if (!lighting.IsReady(Origin)) return false;
                    foreach (var color in mesh.colors32) if (color.g != 0) return false;
                    return true;
                }, lighting);
                foreach (var color in mesh.colors32) Assert.Greater(color.r, 0);
                CollectionAssert.AreEqual(data.Vertices, mesh.vertices);
                lighting.Retire(binding);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(mesh);
                UnityEngine.Object.DestroyImmediate(root);
            }
        }
    }
}
