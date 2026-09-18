using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using UnityEngine;
using World;

namespace lighting
{
    /// <summary>
    /// Main-thread facade around a worker-owned light world. Inputs are copied, output buffers are
    /// immutable, and revision checks prevent old chunks or meshes receiving asynchronous results.
    /// </summary>
    public sealed class WorldLighting : IDisposable
    {
        private sealed class Update
        {
            public long Revision;
            public LightInput Input;
        }
        public sealed class MeshBinding
        {
            internal Mesh Mesh;
            internal Vector3[] Vertices, Normals;
            internal Vector3 Offset;
            internal Color32[] Colors;
            internal Dictionary<VertexLightKey, int> VertexIndices;
            internal MeshLightSnapshot Applied;
            internal Dictionary<ChunkCoord, long> Dependencies;
            internal volatile bool Retired;
        }
        internal readonly struct VertexLightKey : IEquatable<VertexLightKey>
        {
            private readonly Vector3 _position, _normal;
            internal VertexLightKey(Vector3 position, Vector3 normal) { _position = position; _normal = normal; }
            public bool Equals(VertexLightKey other) => _position.Equals(other._position) && _normal.Equals(other._normal);
            public override bool Equals(object obj) => obj is VertexLightKey other && Equals(other);
            // Hash components separately: Vector3's shift/XOR hash collides heavily on voxel grids.
            public override int GetHashCode() => HashCode.Combine(_position.x, _position.y, _position.z,
                _normal.x, _normal.y, _normal.z);
        }
        // Immutable once published. Build the lookup on the worker, not during a geometry rebuild.
        internal sealed class MeshLightSnapshot
        {
            internal readonly Dictionary<VertexLightKey, int> Indices;
            internal readonly Color32[] Colors;

            internal MeshLightSnapshot(Dictionary<VertexLightKey, int> indices, Color32[] colors)
            { Indices = indices; Colors = colors; }
        }
        private readonly World.World _world;
        private readonly object _gate = new();
        private readonly Thread _worker;
        private readonly HashSet<ChunkCoord> _dirty = new();
        private readonly Dictionary<ChunkCoord, long> _versions = new();
        private readonly Dictionary<ChunkCoord, Update> _updates = new();
        private readonly Dictionary<ChunkCoord, LightColumn> _published = new();
        private readonly HashSet<MeshBinding> _awaitingFirstUpload = new();
        private readonly Dictionary<ChunkCoord, int> _pendingUploads = new();
        private readonly List<MeshBinding> _newMeshes = new();
        private readonly HashSet<MeshBinding> _readyMeshes = new();
        private readonly Dictionary<ChunkCoord, (LightColumn column, Dictionary<ChunkCoord, long> deps)> _readyColumns = new();
        private readonly Dictionary<int, (Vector3Int position, byte strength)> _sources = new();
        private readonly HashSet<ChunkCoord> _sourceDirty = new();
        private readonly Dictionary<ChunkCoord, long> _sourceVersions = new();
        private int _nextSource;
        private bool _sourcesChanged;
        private long _nextVersion;
        private volatile bool _stopping;
        private volatile Exception _failure;
        public double LastSolveMilliseconds { get; private set; }
        public string DiagnosticStatus
        {
            get { lock (_gate) return $"published={_published.Count}, snapshots={_dirty.Count + _updates.Count}, firstUploads={_awaitingFirstUpload.Count}, readyUploads={_readyMeshes.Count}, solveMs={LastSolveMilliseconds:F1}"; }
        }

        public WorldLighting(World.World world)
        {
            _world = world;
            _worker = new Thread(WorkerLoop) { IsBackground = true, Name = "UnityCraft Lighting" };
            _worker.Start();
        }

        public void Invalidate(ChunkCoord coord)
        {
            if (_stopping || !_dirty.Add(coord)) return;
            _versions[coord] = ++_nextVersion;
        }

        public bool IsReady(ChunkCoord coord)
        {
            if (!_published.ContainsKey(coord) || _dirty.Contains(coord)) return false;
            return _pendingUploads.GetValueOrDefault(coord) == 0;
        }
        public Vector2 Sample(Vector3 position) => VoxelLightSolver.Sample(_published, position);

        public int RegisterSource(Vector3Int position, byte strength)
        {
            if (_stopping) throw new ObjectDisposedException(nameof(WorldLighting));
            int id = ++_nextSource;
            UpdateSource(id, position, strength);
            return id;
        }

        public void UpdateSource(int id, Vector3Int position, byte strength)
        {
            if (_stopping) throw new ObjectDisposedException(nameof(WorldLighting));
            if (id <= 0 || id > _nextSource) throw new ArgumentOutOfRangeException(nameof(id));
            if (strength > 15) throw new ArgumentOutOfRangeException(nameof(strength));
            lock (_gate)
            {
                if (_sources.TryGetValue(id, out var old))
                {
                    if (old.position == position && old.strength == strength) return;
                    StampSource(old.position);
                }
                _sources[id] = (position, strength);
                StampSource(position);
                _sourcesChanged = true;
                Monitor.Pulse(_gate);
            }
        }

        private void StampSource(Vector3Int position)
        {
            var coord = Coord(position);
            _sourceDirty.Add(coord);
            _versions[coord] = ++_nextVersion;
            // A source revision must not certify geometry the worker has not received yet.
            // The pending chunk snapshot will carry this revision when Pump copies it.
            if (!_dirty.Contains(coord)) _sourceVersions[coord] = _nextVersion;
        }
        public void RemoveSource(int id)
        {
            if (_stopping) return;
            lock (_gate)
            {
                if (!_sources.Remove(id, out var old)) return;
                StampSource(old.position);
                _sourcesChanged = true;
                Monitor.Pulse(_gate);
            }
        }

        private static ChunkCoord Coord(Vector3Int p) => new(p.x >> 4, p.z >> 4);

        public MeshBinding Bind(Mesh mesh, List<Vector3> vertices, List<Vector3> normals, Vector3 offset,
            MeshBinding previous = null)
        {
            // Geometry edits change vertex order/count. Copying colors by index, or clearing them,
            // would briefly shade unrelated faces incorrectly while the worker catches up.
            Retire(previous);
            var applied = previous != null && previous.Offset == offset ? previous.Applied : null;
            var binding = new MeshBinding
            {
                Mesh = mesh, Vertices = vertices.ToArray(), Normals = normals.ToArray(), Offset = offset,
                Applied = applied
            };
            var initial = new Color32[vertices.Count];
            var coord = new ChunkCoord(offset);
            // Newly streamed chunks can have no light field yet. Their first valid upload is
            // already asynchronous; do not perform thousands of guaranteed-missing lookups.
            bool hasField = _published.ContainsKey(coord);
            for (int i = 0; (applied != null || hasField) && i < initial.Length; i++)
            {
                if (applied != null && applied.Indices.TryGetValue(new VertexLightKey(vertices[i], normals[i]), out int index))
                    initial[i] = applied.Colors[index];
                else
                {
                    // A newly exposed face has no cached smooth sample. One cheap lookup in the
                    // last published field supplies its provisional light; no propagation runs here.
                    Vector2 light = Sample(vertices[i] + offset + normals[i] * .02f);
                    initial[i] = new Color32((byte)Mathf.RoundToInt(light.x * 255),
                        (byte)Mathf.RoundToInt(light.y * 255), 0, 255);
                }
            }
            mesh.SetVertexBufferData(initial, 0, 0, initial.Length, 1,
                UnityEngine.Rendering.MeshUpdateFlags.DontRecalculateBounds);
            _awaitingFirstUpload.Add(binding);
            _pendingUploads[coord] = _pendingUploads.GetValueOrDefault(coord) + 1;
            lock (_gate) { _newMeshes.Add(binding); Monitor.Pulse(_gate); }
            return binding;
        }

        public void Retire(MeshBinding binding)
        {
            if (binding == null) return;
            lock (_gate) { binding.Retired = true; _readyMeshes.Remove(binding); }
            CompleteFirstUpload(binding);
        }

        private void CompleteFirstUpload(MeshBinding binding)
        {
            if (!_awaitingFirstUpload.Remove(binding)) return;
            var coord = new ChunkCoord(binding.Offset);
            int remaining = _pendingUploads[coord] - 1;
            if (remaining == 0) _pendingUploads.Remove(coord);
            else _pendingUploads[coord] = remaining;
        }

        /// <summary>Returns time consumed so uploads share, rather than extend, the render budget.</summary>
        public double Pump(double budgetMilliseconds)
        {
            long started = Stopwatch.GetTimestamp();
            if (_failure != null) throw new InvalidOperationException("Voxel lighting worker failed.", _failure);
            // At most two bounded 160 KiB copies per frame; never scan every cell on this thread.
            int copied = 0;
            while (_dirty.Count > 0 && copied < 2 && ElapsedMilliseconds(started) < budgetMilliseconds)
            {
                ChunkCoord coord = default;
                float best = float.MaxValue;
                Vector3 player = _world.player.position;
                foreach (var candidate in _dirty)
                {
                    float dx = candidate.X * 16 - player.x, dz = candidate.Z * 16 - player.z;
                    float distance = dx * dx + dz * dz;
                    if (distance < best) { coord = candidate; best = distance; }
                }
                _dirty.Remove(coord);
                _world.ChunkMap.TryGetValue(coord, out var chunk);
                var update = new Update { Revision = _versions[coord], Input = chunk?.Data.CaptureLighting() };
                if (chunk == null) _published.Remove(coord);
                lock (_gate) { _updates[coord] = update; Monitor.Pulse(_gate); }
                copied++;
            }
            lock (_gate)
            {
                // Only references are published here. Propagation and color-array construction are worker work.
                foreach (var result in _readyColumns)
                    if (Valid(result.Value.deps)) _published[result.Key] = result.Value.column;
                _readyColumns.Clear();
                while (_readyMeshes.Count > 0 && ElapsedMilliseconds(started) < budgetMilliseconds)
                {
                    MeshBinding binding = null;
                    foreach (var next in _readyMeshes) { binding = next; break; }
                    _readyMeshes.Remove(binding);
                    if (!binding.Retired && binding.Mesh != null && Valid(binding.Dependencies))
                    {
                        binding.Mesh.SetVertexBufferData(binding.Colors, 0, 0, binding.Colors.Length, 1,
                            UnityEngine.Rendering.MeshUpdateFlags.DontRecalculateBounds);
                        binding.Applied = new MeshLightSnapshot(binding.VertexIndices, binding.Colors);
                        CompleteFirstUpload(binding);
                    }
                    binding.Colors = null;
                }
                Monitor.Pulse(_gate);
            }
            return ElapsedMilliseconds(started);
        }

        private static double ElapsedMilliseconds(long started) =>
            (Stopwatch.GetTimestamp() - started) * 1000.0 / Stopwatch.Frequency;

        private bool Valid(Dictionary<ChunkCoord, long> dependencies)
        {
            foreach (var pair in dependencies)
                if (_dirty.Contains(pair.Key) || _versions.GetValueOrDefault(pair.Key) != pair.Value) return false;
            return true;
        }

        private static Dictionary<ChunkCoord, long> Dependencies(ChunkCoord coord, Dictionary<ChunkCoord, long> versions)
        {
            var result = new Dictionary<ChunkCoord, long>();
            // Halo includes diagonal light sources and all neighbors used for vertex interpolation.
            for (int x = -1; x <= 1; x++) for (int z = -1; z <= 1; z++)
            {
                var c = new ChunkCoord(coord.X + x, coord.Z + z);
                result[c] = versions.GetValueOrDefault(c);
            }
            return result;
        }

        private void WorkerLoop()
        {
            var columns = new Dictionary<ChunkCoord, LightColumn>();
            var versions = new Dictionary<ChunkCoord, long>();
            var meshes = new List<MeshBinding>();
            var sources = new Dictionary<Vector3Int, byte>();
            try
            {
                while (!_stopping)
                {
                    Dictionary<ChunkCoord, Update> changes;
                    List<MeshBinding> added;
                    HashSet<ChunkCoord> dirty;
                    Dictionary<ChunkCoord, long> sourceVersions;
                    lock (_gate)
                    {
                        while (!_stopping && _updates.Count == 0 && _newMeshes.Count == 0 && !_sourcesChanged)
                            Monitor.Wait(_gate);
                        if (_stopping) return;
                        changes = new Dictionary<ChunkCoord, Update>(_updates); _updates.Clear();
                        added = new List<MeshBinding>(_newMeshes); _newMeshes.Clear();
                        dirty = new HashSet<ChunkCoord>(_sourceDirty); _sourceDirty.Clear();
                        sourceVersions = new Dictionary<ChunkCoord, long>(_sourceVersions); _sourceVersions.Clear();
                        if (_sourcesChanged)
                        {
                            sources.Clear();
                            foreach (var source in _sources.Values)
                                sources[source.position] = Math.Max(sources.GetValueOrDefault(source.position), source.strength);
                            _sourcesChanged = false;
                        }
                    }
                    foreach (var pair in changes)
                    {
                        versions[pair.Key] = Math.Max(versions.GetValueOrDefault(pair.Key), pair.Value.Revision);
                        if (pair.Value.Input == null) columns.Remove(pair.Key);
                        else columns[pair.Key] = VoxelLightSolver.CreateColumn(pair.Value.Input);
                        dirty.Add(pair.Key);
                    }
                    foreach (var stamp in sourceVersions)
                        versions[stamp.Key] = Math.Max(versions.GetValueOrDefault(stamp.Key), stamp.Value);
                    var watch = Stopwatch.StartNew();
                    var solved = VoxelLightSolver.Solve(columns, dirty, sources, () => _stopping);
                    if (solved == null) return;
                    LastSolveMilliseconds = watch.Elapsed.TotalMilliseconds;
                    foreach (var pair in solved) columns[pair.Key] = pair.Value;
                    lock (_gate)
                    {
                        foreach (var pair in solved)
                            _readyColumns[pair.Key] = (pair.Value, Dependencies(pair.Key, versions));
                    }
                    meshes.RemoveAll(binding => binding.Retired);
                    meshes.AddRange(added);
                    foreach (var binding in meshes)
                    {
                        if (_stopping) return;
                        if (binding.Retired) continue;
                        var coord = new ChunkCoord(Mathf.FloorToInt(binding.Offset.x) >> 4, Mathf.FloorToInt(binding.Offset.z) >> 4);
                        if (!solved.ContainsKey(coord) && !added.Contains(binding)) continue;
                        if (binding.VertexIndices == null)
                        {
                            var indices = new Dictionary<VertexLightKey, int>(binding.Vertices.Length);
                            for (int i = 0; i < binding.Vertices.Length; i++)
                                indices[new VertexLightKey(binding.Vertices[i], binding.Normals[i])] = i;
                            binding.VertexIndices = indices;
                        }
                        var colors = VoxelLightSolver.SampleVertices(columns, binding.Vertices, binding.Normals, binding.Offset);
                        lock (_gate)
                        {
                            if (binding.Retired) continue;
                            binding.Colors = colors;
                            binding.Dependencies = Dependencies(coord, versions);
                            _readyMeshes.Add(binding);
                        }
                    }
                }
            }
            catch (Exception exception) { _failure = exception; }
        }

        public void Dispose()
        {
            lock (_gate) { _stopping = true; Monitor.PulseAll(_gate); }
            _worker.Join();
            _published.Clear();
            _awaitingFirstUpload.Clear();
            _pendingUploads.Clear();
            lock (_gate) { _readyMeshes.Clear(); _readyColumns.Clear(); _newMeshes.Clear(); _updates.Clear(); }
        }
    }
}
