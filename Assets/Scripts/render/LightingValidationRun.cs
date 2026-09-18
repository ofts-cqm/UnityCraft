#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;
using World;
using World.blocks;
using world.blocks;
using world.persistence;
using settings;

namespace render
{
    /// <summary>
    /// Opt-in deterministic Player validation. Reflection at the new-system boundary lets the same
    /// harness run against the original checkout for comparable frame-time measurements.
    /// </summary>
    public sealed class LightingValidationRun : MonoBehaviour
    {
        private string _output;
        private readonly List<float> _frames = new();
        private bool _measure;
        private static readonly BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            string[] args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, "--voxel-lighting-validation");
            if (index < 0) return;
            Application.runInBackground = true;
            var run = new GameObject("Lighting Validation").AddComponent<LightingValidationRun>();
            DontDestroyOnLoad(run);
            run._output = index + 1 < args.Length ? args[index + 1] : Path.Combine(Application.temporaryCachePath, "lighting-validation");
            Directory.CreateDirectory(run._output);
            run.StartCoroutine(run.Run());
        }

        private void Update() { if (_measure) _frames.Add(Time.unscaledDeltaTime * 1000); }

        private IEnumerator Run()
        {
            QualitySettings.SetQualityLevel(Array.IndexOf(QualitySettings.names, "PC"), true);
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = -1;
            Screen.SetResolution(1280, 720, false);
            GameSettings.EnsureLoaded();
            // Private setter affects this diagnostic process only; do not overwrite user preferences.
            typeof(GameSettings).GetProperty("ViewDistance").SetValue(null, 4);
            var storage = new FileWorldStorage(Application.persistentDataPath);
            string id = "lighting-validation-" + DateTime.UtcNow.Ticks.ToString(CultureInfo.InvariantCulture);
            var descriptor = storage.CreateWorld(id, "Lighting validation", "482719");
            var auth = SaveVersionPolicy.Authorize(descriptor);
            for (int cx = -4; cx <= 4; cx++) for (int cz = -4; cz <= 4; cz++)
            {
                int[] blocks = new int[ChunkSnapshot.CellCount];
                byte[] fluids = new byte[blocks.Length];
                for (int x = 0; x < 16; x++) for (int z = 0; z < 16; z++) for (int y = 0; y < 64; y++)
                {
                    int wx = cx * 16 + x, wz = cz * 16 + z;
                    bool ground = y <= 49;
                    bool room = wx >= 1 && wx <= 14 && wz >= 1 && wz <= 14 && y >= 50 && y <= 59 &&
                        (wx == 1 || wx == 14 || wz == 1 || wz == 14 || y == 59);
                    bool tower = wx >= 22 && wx <= 25 && wz >= 8 && wz <= 11 && y < 61;
                    bool shaft = wx >= -8 && wx <= -6 && wz >= 4 && wz <= 6 && y >= 12;
                    bool cave = wx >= -7 && wx <= 32 && wz >= 4 && wz <= 6 && y >= 12 && y <= 15;
                    if ((ground || room || tower) && !shaft && !cave)
                        blocks[ChunkSnapshot.Index(x, y, z)] = y == 49 ? Blocks.GrassBlock.BlockId : Blocks.Stone.BlockId;
                    if (y == 50 && wx >= 3 && wx <= 6 && wz >= 3 && wz <= 6)
                        fluids[ChunkSnapshot.Index(x, y, z)] = FluidState.Source.RawAmount;
                }
                storage.SaveChunk(auth, new ChunkSnapshot(new ChunkCoord(cx, cz), blocks, new int[blocks.Length], fluids));
            }
            storage.SavePlayer(auth, new PlayerSnapshot(8, 68, 8, new InventorySlotSnapshot[PlayerSnapshot.InventorySize]));
            WorldSession.Select(auth);
            SceneManager.LoadScene(GameScenes.Gameplay);
            yield return null;
            var world = World.World.Instance;
            float deadline = Time.realtimeSinceStartup + 180;
            float nextStatus = 0;
            while (!(bool)typeof(World.World).GetField("_gameplayReady", Private).GetValue(world))
            {
                if (Time.realtimeSinceStartup > nextStatus)
                {
                    object service = typeof(World.World).GetProperty("Lighting")?.GetValue(world);
                    Debug.Log($"LIGHTING_LOADING chunks={world.ChunkMap.Count} {service?.GetType().GetProperty("DiagnosticStatus")?.GetValue(service)}");
                    nextStatus = Time.realtimeSinceStartup + 5;
                }
                if (Time.realtimeSinceStartup > deadline) { Debug.LogError("Lighting validation loading timeout"); Application.Quit(2); yield break; }
                yield return null;
            }
            var player = world.player.GetComponent<player.Player>();
            player.enabled = false;
            player.characterController.enabled = false;
            var camera = player.camera;
            camera.transform.SetParent(null, true);
            object cycle = typeof(World.World).GetProperty("Daylight")?.GetValue(world);
            Action<double> setTime = seconds => cycle?.GetType().GetProperty("ElapsedSeconds").SetValue(cycle, seconds);
            setTime(300);
            camera.transform.position = new Vector3(8, 68, -24);
            camera.transform.LookAt(new Vector3(8, 49, 12));
            yield return new WaitForSecondsRealtime(5);
            ScreenCapture.CaptureScreenshot(Path.Combine(_output, "noon.png"));
            yield return null;
            object initialLight = typeof(World.World).GetProperty("Lighting")?.GetValue(world);
            Debug.Log($"LIGHTING_ROOF {initialLight?.GetType().GetMethod("Sample")?.Invoke(initialLight, new object[] { new Vector3(8, 60.1f, 8) })}");
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "--remesh-flash-check") >= 0)
            {
                yield return CheckRemeshFlash(world);
                world.SaveAndQuitToWorldSelection();
                yield return null;
                Application.Quit();
                yield break;
            }
            _frames.Clear(); _measure = true;
            float start = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - start < 15)
            {
                float angle = (Time.realtimeSinceStartup - start) * .15f;
                camera.transform.position = new Vector3(8 + Mathf.Sin(angle) * 30, 68, 8 - Mathf.Cos(angle) * 30);
                camera.transform.LookAt(new Vector3(8, 50, 8));
                yield return null;
            }
            _measure = false;
            SaveFrames("orbit");
            camera.transform.position = new Vector3(5, 54, 5);
            camera.transform.LookAt(new Vector3(13, 53, 13));
            var geometry = new List<string>();
            foreach (var filter in FindObjectsByType<MeshFilter>())
            {
                if (!filter.name.StartsWith("Chunk @")) continue;
                var renderer = filter.GetComponent<MeshRenderer>();
                var colors = filter.sharedMesh.colors32;
                int red = 0;
                foreach (var color in colors) red += color.r;
                geometry.Add($"{filter.name}: local={filter.sharedMesh.bounds} world={renderer.bounds} vertices={filter.sharedMesh.vertexCount} position={filter.transform.position} meanSky={(colors.Length > 0 ? red / colors.Length : -1)}");
            }
            File.WriteAllLines(Path.Combine(_output, "geometry.txt"), geometry);
            yield return new WaitForSecondsRealtime(1);
            ScreenCapture.CaptureScreenshot(Path.Combine(_output, "sealed-room.png"));
            yield return null;
            camera.transform.position = new Vector3(24, 13.5f, 5.5f);
            camera.transform.LookAt(new Vector3(31, 13.5f, 5));
            yield return new WaitForSecondsRealtime(1);
            ScreenCapture.CaptureScreenshot(Path.Combine(_output, "deep-cave.png"));
            yield return null;
            object lighting = typeof(World.World).GetProperty("Lighting")?.GetValue(world);
            if (lighting != null)
            {
                int emitter = (int)lighting.GetType().GetMethod("RegisterSource").Invoke(lighting, new object[] { new Vector3Int(28, 13, 5), (byte)15 });
                yield return new WaitForSecondsRealtime(2);
                ScreenCapture.CaptureScreenshot(Path.Combine(_output, "cave-local-light.png"));
                yield return null;
                lighting.GetType().GetMethod("RemoveSource").Invoke(lighting, new object[] { emitter });
                yield return new WaitForSecondsRealtime(2);
                ScreenCapture.CaptureScreenshot(Path.Combine(_output, "cave-light-removed.png"));
                yield return null;
            }
            camera.transform.position = new Vector3(8, 68, -24);
            camera.transform.LookAt(new Vector3(8, 49, 12));
            foreach (var phase in new[] { ("morning-shadows", 150d), ("evening-shadows", 450d), ("sunrise", 5d), ("sunset", 595d), ("night", 900d) })
            {
                setTime(phase.Item2);
                yield return new WaitForSecondsRealtime(.2f);
                ScreenCapture.CaptureScreenshot(Path.Combine(_output, phase.Item1 + ".png"));
                yield return new WaitForSecondsRealtime(.2f);
            }
            foreach (var phase in new[] { ("sunrise-east", 5d, Vector3.right), ("sunset-west", 595d, Vector3.left) })
            {
                setTime(phase.Item2);
                camera.transform.LookAt(camera.transform.position + phase.Item3 * 100);
                yield return new WaitForSecondsRealtime(.2f);
                ScreenCapture.CaptureScreenshot(Path.Combine(_output, phase.Item1 + ".png"));
                yield return new WaitForSecondsRealtime(.2f);
            }
            camera.transform.LookAt(new Vector3(8, 49, 12));
            setTime(300);
            yield return new WaitForSecondsRealtime(1);
            _frames.Clear(); _measure = true;
            // A roof edit affects thousands of light values across multiple sections.
            for (int x = -16; x < 16; x++) for (int z = -16; z < 16; z++) world.SetBlock(x, 75, z, Blocks.Stone);
            yield return new WaitForSecondsRealtime(8);
            for (int x = -16; x < 16; x++) for (int z = -16; z < 16; z++) world.SetBlock(x, 75, z, Blocks.Air);
            yield return new WaitForSecondsRealtime(8);
            _measure = false;
            SaveFrames("roof-edits");
            _frames.Clear(); _measure = true;
            start = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - start < 20)
            {
                float progress = (Time.realtimeSinceStartup - start) / 20;
                world.player.position = new Vector3(8 + 128 * progress, 90, 8);
                camera.transform.position = world.player.position + new Vector3(0, 8, -12);
                camera.transform.LookAt(world.player.position + new Vector3(8, -20, 8));
                yield return null;
            }
            _measure = false;
            SaveFrames("streaming");
            if (lighting != null)
            {
                var ids = new List<int>();
                int center = Mathf.FloorToInt(world.player.position.x);
                setTime(900);
                for (int x = center - 16; x <= center + 16; x += 4)
                for (int z = -16; z <= 16; z += 4)
                    ids.Add((int)lighting.GetType().GetMethod("RegisterSource").Invoke(lighting,
                        new object[] { new Vector3Int(x, 72, z), (byte)15 }));
                yield return new WaitForSecondsRealtime(4);
                ScreenCapture.CaptureScreenshot(Path.Combine(_output, "many-local-lights.png"));
                yield return null;
                foreach (int emitterId in ids) lighting.GetType().GetMethod("RemoveSource").Invoke(lighting, new object[] { emitterId });
                yield return new WaitForSecondsRealtime(2);
            }
            Debug.Log("LIGHTING_VALIDATION_COMPLETE " + _output);
            world.SaveAndQuitToWorldSelection();
            yield return null;
            Application.Quit();
        }

        private IEnumerator CheckRemeshFlash(World.World world)
        {
            // Inspect the actual uploaded stream after each rendered frame, including frames before
            // the async solve completes. Settled screenshots alone cannot catch a 0.1-second flash.
            Application.targetFrameRate = 120;
            var target = GameObject.Find("Chunk @0,0 height 3").GetComponent<MeshFilter>();
            var mesh = target.sharedMesh;
            var positions = mesh.vertices;
            var normals = mesh.normals;
            var colors = mesh.colors32;
            var probes = new Dictionary<(Vector3, Vector3), Color32>();
            for (int i = 0; i < positions.Length; i++)
                if (positions[i].y == 12 && positions[i].x >= 2 && positions[i].x <= 4 &&
                    positions[i].z >= 2 && positions[i].z <= 4 && normals[i] == Vector3.up && colors[i].r > 0)
                    probes[(positions[i], normals[i])] = colors[i];
            if (probes.Count == 0) throw new InvalidOperationException("Remesh fixture has no lit roof probes.");
            int checkedFrames = 0, changedMeshes = 0;
            for (int edit = 0; edit < 10; edit++)
            {
                int oldCount = mesh.vertexCount;
                world.SetBlock(8, 59, 8, edit % 2 == 0 ? Blocks.Air : Blocks.Stone);
                bool sawRemesh = false;
                float until = Time.realtimeSinceStartup + .25f;
                do
                {
                    yield return new WaitForEndOfFrame();
                    positions = mesh.vertices; normals = mesh.normals; colors = mesh.colors32;
                    sawRemesh |= positions.Length != oldCount;
                    int matched = 0;
                    for (int i = 0; i < positions.Length; i++)
                    {
                        if (!probes.TryGetValue((positions[i], normals[i]), out var expected)) continue;
                        matched++;
                        if (!colors[i].Equals(expected))
                        {
                            Debug.LogError($"LIGHTING_REMESH_FLASH edit={edit} vertex={positions[i]} expected={expected} actual={colors[i]}");
                            Application.Quit(3);
                            yield break;
                        }
                    }
                    if (matched == 0) throw new InvalidOperationException("Remesh fixture lost its roof probes.");
                    checkedFrames++;
                    if (sawRemesh && edit < 2 && Time.realtimeSinceStartup < until - .15f)
                        ScreenCapture.CaptureScreenshot(Path.Combine(_output, $"remesh-{edit}-{checkedFrames}.png"));
                } while (Time.realtimeSinceStartup < until);
                if (sawRemesh) changedMeshes++;
            }
            if (changedMeshes != 10) throw new InvalidOperationException($"Only {changedMeshes}/10 edits rebuilt the mesh.");
            Debug.Log($"LIGHTING_REMESH_PASS edits={changedMeshes} frames={checkedFrames} probes={probes.Count}");
        }

        private void SaveFrames(string name)
        {
            var lines = new string[_frames.Count];
            for (int i = 0; i < lines.Length; i++) lines[i] = _frames[i].ToString("R", CultureInfo.InvariantCulture);
            File.WriteAllLines(Path.Combine(_output, name + "-frames-ms.txt"), lines);
            _frames.Sort();
            if (_frames.Count > 0)
                Debug.Log($"LIGHTING_PERF {name}: n={_frames.Count} median={_frames[_frames.Count / 2]:F3} p99={_frames[(int)((_frames.Count - 1) * .99)]:F3} max={_frames[_frames.Count - 1]:F3}");
        }
    }
}
#endif
