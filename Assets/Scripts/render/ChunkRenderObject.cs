using System;
using System.Collections.Generic;
using render;
using UnityEngine;
using World;
using world.blocks;
using World.blocks;

namespace Render
{
    [Flags]
    public enum ChunkRenderDirtyFlags
    {
        None = 0,
        Blocks = 1 << 0,
        Water = 1 << 1,
        All = Blocks | Water
    }

    public class ChunkRenderObject
    {
        // Builds run serially on the main thread. One reusable builder avoids per-rebuild
        // list allocation without retaining a second full copy of every section mesh.
        private static readonly MeshBuilder SharedMeshBuilder = new();

        private readonly Chunk _owner;
        private MeshRenderer _opaqueRenderer;
        private MeshFilter _meshFilter;
        private MeshCollider _meshCollider;
        private RenderObjectProperty _renderObjectProperty;
        private GameObject _chunkObject;
        private Mesh _opaqueMesh;
        private Mesh _colliderMesh;

        private MeshRenderer _transparentRenderer;
        private MeshFilter _transparentMeshFilter;
        private GameObject _transparentObject;
        private Mesh _transparentMesh;

        private MeshRenderer _waterRenderer;
        private MeshFilter _waterMeshFilter;
        private GameObject _waterObject;
        private Mesh _waterMesh;

        private readonly int _heightIndex;
        private readonly Vector3Int _chunkPosition;

        private readonly List<int> _triangleCoordinate = new();
        private readonly List<int> _triangleFace = new();
        private readonly object _dirtyLock = new();
        private ChunkRenderDirtyFlags _dirtyFlags = ChunkRenderDirtyFlags.All;
        private bool _queued;
        private bool _finalized;
        private bool _destroyed;
        private bool _active;
        private bool _hasBlockGeometry;
        private bool _hasColliderGeometry;
        private bool _hasWaterGeometry;

        public const int TopFace = 0;
        public const int BottomFace = 1;
        public const int FrontFace = 2;
        public const int BackFace = 3;
        public const int LeftFace = 4;
        public const int RightFace = 5;

        public const int SideFaceBegin = FrontFace;
        public const int SideFaceEnd = RightFace;

        private const int ChunkSize = Chunk.ChunkSize;

        public ChunkRenderObject(Chunk owner, ChunkCoord coord, int index)
        {
            _owner = owner;
            SectionIndex = index;
            _heightIndex = index * ChunkSize;
            _chunkPosition = new Vector3Int(coord.X * ChunkSize, index * ChunkSize, coord.Z * ChunkSize);
        }

        internal int SectionIndex { get; }
        public bool Dirty
        {
            get
            {
                lock (_dirtyLock) return _dirtyFlags != ChunkRenderDirtyFlags.None;
            }
        }

        public void FinalizeGeneration()
        {
            if (_finalized || _destroyed) return;
            // A finalized section is intentionally data-only until a rebuild produces geometry.
            // Most sections never need every render channel, so eagerly constructing four meshes
            // and their components would make scene cost proportional to loaded section count.
            _active = _owner.IsActive;
            _finalized = true;
            MarkDirty(ChunkRenderDirtyFlags.All);
        }

        private Mesh CreatePersistentMesh(string channel)
        {
            Mesh mesh = new() { name = $"{SectionName} {channel}" };
            mesh.MarkDynamic();
            return mesh;
        }

        private string SectionName =>
            $"Chunk @{_chunkPosition.x / ChunkSize},{_chunkPosition.z / ChunkSize} height {_heightIndex / ChunkSize}";

        private void EnsureRootObject()
        {
            if (_chunkObject != null) return;

            _chunkObject = new GameObject(SectionName);
            // Keep a newly allocated hierarchy invisible until every requested channel has been
            // uploaded. This also prevents a transient active object during an inactive rebuild.
            _chunkObject.SetActive(false);
            _chunkObject.transform.position = _chunkPosition;
            _chunkObject.transform.SetParent(World.World.Instance.transform);
        }

        private void EnsureOpaqueChannel()
        {
            EnsureRootObject();
            if (_opaqueRenderer == null)
            {
                _opaqueRenderer = _chunkObject.AddComponent<MeshRenderer>();
                _opaqueRenderer.sharedMaterial = World.World.Instance.material;
            }
            if (_meshFilter == null) _meshFilter = _chunkObject.AddComponent<MeshFilter>();
            if (_opaqueMesh == null) _opaqueMesh = CreatePersistentMesh("Opaque");
            _meshFilter.sharedMesh = _opaqueMesh;
        }

        private void EnsureColliderChannel()
        {
            EnsureRootObject();
            if (_meshCollider == null) _meshCollider = _chunkObject.AddComponent<MeshCollider>();
            if (_renderObjectProperty == null)
            {
                _renderObjectProperty = _chunkObject.AddComponent<RenderObjectProperty>();
                _renderObjectProperty.RenderObject = this;
            }
            if (_colliderMesh == null) _colliderMesh = CreatePersistentMesh("Collider");
        }

        private void EnsureTransparentChannel()
        {
            EnsureRootObject();
            if (_transparentObject == null)
            {
                _transparentObject = new GameObject("Transparent Render");
                _transparentObject.SetActive(false);
                _transparentObject.transform.SetParent(_chunkObject.transform, false);
                _transparentRenderer = _transparentObject.AddComponent<MeshRenderer>();
                _transparentRenderer.sharedMaterial = World.World.Instance.transparentMaterial;
                _transparentMeshFilter = _transparentObject.AddComponent<MeshFilter>();
            }
            if (_transparentMesh == null) _transparentMesh = CreatePersistentMesh("Transparent");
            _transparentMeshFilter.sharedMesh = _transparentMesh;
            _transparentObject.SetActive(true);
        }

        private void EnsureWaterChannel()
        {
            EnsureRootObject();
            if (_waterObject == null)
            {
                _waterObject = new GameObject("Water Render");
                _waterObject.SetActive(false);
                _waterObject.transform.SetParent(_chunkObject.transform, false);
                _waterRenderer = _waterObject.AddComponent<MeshRenderer>();
                _waterRenderer.sharedMaterial = World.World.Instance.ActiveWaterMaterial;
                _waterMeshFilter = _waterObject.AddComponent<MeshFilter>();
            }
            if (_waterMesh == null) _waterMesh = CreatePersistentMesh("Water");
            _waterMeshFilter.sharedMesh = _waterMesh;
            _waterObject.SetActive(true);
        }

        public bool Active
        {
            get => _finalized && !_destroyed && _active;
            set
            {
                if (!_finalized || _destroyed) return;
                _active = value;
                if (value)
                {
                    if (_chunkObject != null) _chunkObject.SetActive(HasAllocatedChannel);
                    MarkDirty(ChunkRenderDirtyFlags.All);
                }
                else
                {
                    // The inactive cache retains compact cell data only. Render/collision state is
                    // regenerated if the chunk returns to view, avoiding thousands of hidden Unity objects.
                    _hasBlockGeometry = false;
                    _hasColliderGeometry = false;
                    _hasWaterGeometry = false;
                    _triangleCoordinate.Clear();
                    _triangleFace.Clear();
                    lock (_dirtyLock) _queued = false;
                    ReleaseOpaqueChannel();
                    ReleaseColliderChannel();
                    ReleaseTransparentChannel();
                    ReleaseWaterChannel();
                    ReleaseRootObject();
                }
            }
        }

        public void DestroyObject()
        {
            lock (_dirtyLock)
            {
                if (_destroyed) return;
                _destroyed = true;
                _queued = false;
                _dirtyFlags = ChunkRenderDirtyFlags.None;
            }

            _active = false;
            _hasBlockGeometry = false;
            _hasColliderGeometry = false;
            _hasWaterGeometry = false;
            ReleaseOpaqueChannel();
            ReleaseColliderChannel();
            ReleaseTransparentChannel();
            ReleaseWaterChannel();
            ReleaseRootObject();
        }

        private static void DestroyMesh(Mesh mesh)
        {
            if (mesh != null) UnityEngine.Object.Destroy(mesh);
        }

        public void MarkDirty(ChunkRenderDirtyFlags flags)
        {
            if (flags == ChunkRenderDirtyFlags.None) return;
            bool enqueue;
            lock (_dirtyLock)
            {
                if (_destroyed) return;
                _dirtyFlags |= flags;
                enqueue = _finalized;
            }
            if (enqueue) _owner.EnqueueRenderObject(this);
        }

        internal bool TryReserveQueueEntry()
        {
            lock (_dirtyLock)
            {
                if (_queued || _destroyed || _dirtyFlags == ChunkRenderDirtyFlags.None) return false;
                _queued = true;
                return true;
            }
        }

        internal void ReleaseQueueEntry()
        {
            lock (_dirtyLock) _queued = false;
        }

        internal void RerenderChunk()
        {
            if (!_finalized) return;

            ChunkRenderDirtyFlags rebuilding;
            lock (_dirtyLock)
            {
                if (_destroyed || _dirtyFlags == ChunkRenderDirtyFlags.None) return;
                rebuilding = _dirtyFlags;
                _dirtyFlags &= ~rebuilding;
            }
            SharedMeshBuilder.Clear();

            bool rebuildBlocks = (rebuilding & ChunkRenderDirtyFlags.Blocks) != 0;
            bool rebuildWater = (rebuilding & ChunkRenderDirtyFlags.Water) != 0;
            bool scanBlocks = rebuildBlocks && _owner.Data.NonAirCount(SectionIndex) != 0;
            bool scanWater = rebuildWater && _owner.Data.FluidCount(SectionIndex) != 0;
            if (scanBlocks || scanWater)
            {
                // Match ChunkData's section-contiguous y/z/x layout. Numeric IDs reject air/fluid
                // before a BlockState is materialized, and decoded state objects are cached per block.
                for (int j = 0; j < ChunkSize; j++)
                {
                    for (int k = 0; k < ChunkSize; k++)
                    {
                        for (int i = 0; i < ChunkSize; i++)
                        {
                            Vector3Int position = new(i, j + _heightIndex, k);
                            Vector3 localPosition = new(i, j, k);
                            if (scanBlocks)
                            {
                                _owner.GetCellUnchecked(i, position.y, k, out Block block, out ushort stateId);
                                if (!block.IsAir)
                                {
                                    BlockState blockState = block.AsState(position, block.DecodeStateCached(stateId));
                                    block.Render(blockState, _owner, SharedMeshBuilder, position, localPosition);
                                }
                            }
                            if (scanWater && _owner.Data.GetFluidRawUnchecked(i, position.y, k) != 0)
                                Water.Render(_owner, SharedMeshBuilder, position, localPosition);
                        }
                    }
                }
            }

            if (rebuildBlocks)
            {
                UploadBlockMeshes(SharedMeshBuilder);
                _triangleCoordinate.Clear();
                _triangleCoordinate.AddRange(SharedMeshBuilder.TriangleCoordinate);
                _triangleFace.Clear();
                _triangleFace.AddRange(SharedMeshBuilder.TriangleFace);
                _hasBlockGeometry = !(SharedMeshBuilder.OpaqueMesh.IsEmpty && SharedMeshBuilder.TransparentMesh.IsEmpty);
            }

            if (rebuildWater)
            {
                UploadWaterMesh(SharedMeshBuilder);
            }

            RefreshRootActivity();
            SharedMeshBuilder.Clear();
        }

        private void UploadBlockMeshes(MeshBuilder builder)
        {
            if (builder.OpaqueMesh.IsEmpty) ReleaseOpaqueChannel();
            else
            {
                EnsureOpaqueChannel();
                builder.OpaqueMesh.UploadTo(_opaqueMesh);
                _opaqueRenderer.enabled = true;
            }

            if (builder.ColliderMesh.IsEmpty) ReleaseColliderChannel();
            else
            {
                EnsureColliderChannel();
                _meshCollider.sharedMesh = null;
                builder.ColliderMesh.UploadTo(_colliderMesh);
                _meshCollider.sharedMesh = _colliderMesh;
            }
            _hasColliderGeometry = !builder.ColliderMesh.IsEmpty;

            if (builder.TransparentMesh.IsEmpty) ReleaseTransparentChannel();
            else
            {
                EnsureTransparentChannel();
                builder.TransparentMesh.UploadTo(_transparentMesh);
                _transparentRenderer.enabled = true;
            }
        }

        private void UploadWaterMesh(MeshBuilder builder)
        {
            if (builder.WaterMesh.IsEmpty)
            {
                ReleaseWaterChannel();
                _hasWaterGeometry = false;
                return;
            }

            EnsureWaterChannel();
            builder.WaterMesh.UploadTo(_waterMesh, false);
            _waterRenderer.enabled = true;
            _hasWaterGeometry = true;
        }

        private void ReleaseOpaqueChannel()
        {
            if (_opaqueRenderer != null)
            {
                _opaqueRenderer.enabled = false;
                UnityEngine.Object.Destroy(_opaqueRenderer);
                _opaqueRenderer = null;
            }
            if (_meshFilter != null)
            {
                _meshFilter.sharedMesh = null;
                UnityEngine.Object.Destroy(_meshFilter);
                _meshFilter = null;
            }
            DestroyMesh(_opaqueMesh);
            _opaqueMesh = null;
        }

        private void ReleaseColliderChannel()
        {
            if (_meshCollider != null)
            {
                _meshCollider.sharedMesh = null;
                UnityEngine.Object.Destroy(_meshCollider);
                _meshCollider = null;
            }
            if (_renderObjectProperty != null)
            {
                UnityEngine.Object.Destroy(_renderObjectProperty);
                _renderObjectProperty = null;
            }
            DestroyMesh(_colliderMesh);
            _colliderMesh = null;
        }

        private void ReleaseTransparentChannel()
        {
            if (_transparentMeshFilter != null) _transparentMeshFilter.sharedMesh = null;
            if (_transparentRenderer != null) _transparentRenderer.enabled = false;
            if (_transparentObject != null)
            {
                _transparentObject.SetActive(false);
                UnityEngine.Object.Destroy(_transparentObject);
            }
            DestroyMesh(_transparentMesh);
            _transparentObject = null;
            _transparentRenderer = null;
            _transparentMeshFilter = null;
            _transparentMesh = null;
        }

        private void ReleaseWaterChannel()
        {
            if (_waterMeshFilter != null) _waterMeshFilter.sharedMesh = null;
            if (_waterRenderer != null) _waterRenderer.enabled = false;
            if (_waterObject != null)
            {
                _waterObject.SetActive(false);
                UnityEngine.Object.Destroy(_waterObject);
            }
            DestroyMesh(_waterMesh);
            _waterObject = null;
            _waterRenderer = null;
            _waterMeshFilter = null;
            _waterMesh = null;
        }

        private bool HasAllocatedChannel =>
            _opaqueMesh != null || _colliderMesh != null || _transparentMesh != null || _waterMesh != null;

        private void RefreshRootActivity()
        {
            if (!HasAllocatedChannel)
            {
                ReleaseRootObject();
                return;
            }

            _chunkObject.SetActive(_active && (_hasBlockGeometry || _hasColliderGeometry || _hasWaterGeometry));
        }

        private void ReleaseRootObject()
        {
            if (_chunkObject == null) return;
            _chunkObject.SetActive(false);
            UnityEngine.Object.Destroy(_chunkObject);
            _chunkObject = null;
        }

        public Vector3Int GetBlockPositionOfTriangle(int index)
        {
            int serialized = _triangleCoordinate[index / 2];
            return new Vector3Int((serialized >> 16) & 0xFF, (serialized >> 8) & 0xFF, serialized & 0xFF) +
                   _chunkPosition;
        }

        public int GetTriangleFacing(int index)
        {
            return _triangleFace[index / 2];
        }
    }
}
