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
        private bool _hasBlockGeometry;
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

            _chunkObject = new GameObject
            {
                transform =
                {
                    position = _chunkPosition
                },
                name = $"Chunk @{_chunkPosition.x / ChunkSize},{_chunkPosition.z / ChunkSize} height {_heightIndex / ChunkSize}"
            };

            _opaqueRenderer = _chunkObject.AddComponent<MeshRenderer>();
            _opaqueRenderer.sharedMaterial = World.World.Instance.material;

            _meshFilter = _chunkObject.AddComponent<MeshFilter>();
            _meshCollider = _chunkObject.AddComponent<MeshCollider>();
            _chunkObject.AddComponent<RenderObjectProperty>().RenderObject = this;
            _chunkObject.transform.SetParent(World.World.Instance.transform);

            _transparentObject = new GameObject
            {
                transform =
                {
                    position = _chunkPosition,
                    parent = _chunkObject.transform
                },
                name = "Transparent Render"
            };

            _transparentRenderer = _transparentObject.AddComponent<MeshRenderer>();
            _transparentRenderer.sharedMaterial = World.World.Instance.transparentMaterial;
            _transparentMeshFilter = _transparentObject.AddComponent<MeshFilter>();

            _waterObject = new GameObject
            {
                transform =
                {
                    position = _chunkPosition,
                    parent = _chunkObject.transform
                },
                name = "Water Render"
            };

            _waterRenderer = _waterObject.AddComponent<MeshRenderer>();
            _waterRenderer.sharedMaterial = World.World.Instance.ActiveWaterMaterial;
            _waterMeshFilter = _waterObject.AddComponent<MeshFilter>();

            _opaqueMesh = CreatePersistentMesh("Opaque");
            _colliderMesh = CreatePersistentMesh("Collider");
            _transparentMesh = CreatePersistentMesh("Transparent");
            _waterMesh = CreatePersistentMesh("Water");
            _meshFilter.sharedMesh = _opaqueMesh;
            _transparentMeshFilter.sharedMesh = _transparentMesh;
            _waterMeshFilter.sharedMesh = _waterMesh;

            _opaqueRenderer.enabled = false;
            _transparentRenderer.enabled = false;
            _waterRenderer.enabled = false;
            _finalized = true;
            MarkDirty(ChunkRenderDirtyFlags.All);
        }

        private Mesh CreatePersistentMesh(string channel)
        {
            Mesh mesh = new() { name = $"{_chunkObject.name} {channel}" };
            mesh.MarkDynamic();
            return mesh;
        }

        public bool Active
        {
            get => _finalized && _chunkObject.activeSelf;
            set
            {
                if (!_finalized || _destroyed) return;
                _chunkObject.SetActive(value);
                if (value) MarkDirty(ChunkRenderDirtyFlags.All);
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

            if (_meshFilter != null) _meshFilter.sharedMesh = null;
            if (_transparentMeshFilter != null) _transparentMeshFilter.sharedMesh = null;
            if (_waterMeshFilter != null) _waterMeshFilter.sharedMesh = null;
            if (_meshCollider != null) _meshCollider.sharedMesh = null;
            DestroyMesh(_opaqueMesh);
            DestroyMesh(_colliderMesh);
            DestroyMesh(_transparentMesh);
            DestroyMesh(_waterMesh);
            if (_chunkObject != null) UnityEngine.Object.Destroy(_chunkObject);
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
            for (int i = 0; i < ChunkSize; i++)
            {
                for (int j = 0; j < ChunkSize; j++)
                {
                    for (int k = 0; k < ChunkSize; k++)
                    {
                        Vector3Int position = new(i, j + _heightIndex, k);
                        Vector3 localPosition = new(i, j, k);
                        if (rebuildBlocks)
                        {
                            BlockState block = _owner.GetBlock(position);
                            if (!block.IsAir) block.Block.Render(block, _owner, SharedMeshBuilder, position, localPosition);
                        }
                        if (rebuildWater && !_owner.GetFluid(position).IsEmpty)
                            Water.Render(_owner, SharedMeshBuilder, position, localPosition);
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
                SharedMeshBuilder.WaterMesh.UploadTo(_waterMesh, false);
                _waterRenderer.enabled = !SharedMeshBuilder.WaterMesh.IsEmpty;
                _hasWaterGeometry = !SharedMeshBuilder.WaterMesh.IsEmpty;
            }

            _chunkObject.SetActive(_owner.IsActive && (_hasBlockGeometry || _hasWaterGeometry));
            SharedMeshBuilder.Clear();
        }

        private void UploadBlockMeshes(MeshBuilder builder)
        {
            builder.OpaqueMesh.UploadTo(_opaqueMesh);
            _opaqueRenderer.enabled = !builder.OpaqueMesh.IsEmpty;

            _meshCollider.sharedMesh = null;
            builder.ColliderMesh.UploadTo(_colliderMesh);
            if (!builder.ColliderMesh.IsEmpty) _meshCollider.sharedMesh = _colliderMesh;

            builder.TransparentMesh.UploadTo(_transparentMesh);
            _transparentRenderer.enabled = !builder.TransparentMesh.IsEmpty;
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
