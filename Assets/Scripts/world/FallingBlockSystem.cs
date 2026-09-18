using System;
using System.Collections.Generic;
using render;
using UnityEngine;
using world.blocks;
using world.persistence;
using Render;
using World.blocks;

namespace World
{
    internal sealed class FallingBlockSystem
    {
        internal const float TerminalVelocity = 70f;
        internal const float VoidDespawnHeight = -64f;

        private readonly World _world;
        private readonly Dictionary<ChunkCoord, List<FallingBlockEntity>> _byChunk = new();
        private readonly Stack<FallingBlockEntity> _pool = new();
        private readonly List<FallingBlockEntity> _all = new();
        private readonly Dictionary<long, Mesh> _meshes = new();

        internal FallingBlockSystem(World world) => _world = world;

        internal bool HasActive(ChunkCoord coord) => _byChunk.TryGetValue(coord, out List<FallingBlockEntity> list) && list.Count > 0;

        internal bool Intersects(Bounds bounds)
        {
            int minChunkX = ChunkCoord.ToChunkCoord(Mathf.FloorToInt(bounds.min.x), 0).X;
            int maxChunkX = ChunkCoord.ToChunkCoord(Mathf.FloorToInt(bounds.max.x), 0).X;
            int minChunkZ = ChunkCoord.ToChunkCoord(0, Mathf.FloorToInt(bounds.min.z)).Z;
            int maxChunkZ = ChunkCoord.ToChunkCoord(0, Mathf.FloorToInt(bounds.max.z)).Z;

            for (int chunkX = minChunkX; chunkX <= maxChunkX; chunkX++)
            for (int chunkZ = minChunkZ; chunkZ <= maxChunkZ; chunkZ++)
            {
                if (!_byChunk.TryGetValue(new ChunkCoord(chunkX, chunkZ), out List<FallingBlockEntity> entities))
                    continue;
                foreach (var t in entities)
                    if (t.Intersects(bounds)) return true;
            }
            return false;
        }

        internal void Spawn(BlockState state, Vector3 velocity)
        {
            ushort stateId = state.Block.EncodeStateCompact(state.Data);
            ChunkCoord owner = ChunkCoord.ToChunkCoord(state.Position.x, state.Position.z);
            Spawn(owner, state.Block, stateId, state.Position, velocity);
        }

        internal void Restore(Chunk owner, FallingBlockSnapshot snapshot)
        {
            if (!Blocks.TryGetById(snapshot.BlockId, out Block block) || block is not GravityBlock ||
                (uint)snapshot.StateId > ushort.MaxValue)
                throw new System.IO.InvalidDataException("A saved falling block has an unsupported block or state ID.");
            block.DecodeStateCached((ushort)snapshot.StateId);
            Spawn(owner.ChunkPosition, block, (ushort)snapshot.StateId, snapshot.Position, snapshot.Velocity);
        }

        internal FallingBlockSnapshot[] Capture(Chunk chunk)
        {
            if (!_byChunk.TryGetValue(chunk.ChunkPosition, out List<FallingBlockEntity> list) || list.Count == 0)
                return Array.Empty<FallingBlockSnapshot>();
            FallingBlockSnapshot[] result = new FallingBlockSnapshot[list.Count];
            for (int i = 0; i < list.Count; i++) result[i] = list[i].CreateSnapshot();
            Array.Sort(result, (left, right) => left.Y.CompareTo(right.Y));
            return result;
        }

        internal void MarkMoved(FallingBlockEntity entity)
        {
            _world.GetChunk(entity.OwnerCoord)?.MarkPersistenceDirty();
        }

        internal bool CanSimulate(ChunkCoord coord)
        {
            Chunk chunk = _world.GetChunk(coord);
            return chunk is { IsActive: true, IsRenderReady: true };
        }

        internal void TrySettle(FallingBlockEntity entity)
        {
            int x = entity.ColumnX;
            int z = entity.ColumnZ;
            // The rigidbody position is the cube's lower corner. Flooring its center is stable
            // across contact offsets and partial-height colliders such as slabs.
            int y = Mathf.FloorToInt(entity.PhysicsPosition.y + 0.5f);
            if (y < 0 || y >= Chunk.ChunkHeight) return;

            Vector3Int target = new(x, y, z);
            if (!_world.TryGetLoadedBlock(target + Vector3Int.down, out BlockState below) || !below.Block.Collide) return;

            while (y < Chunk.ChunkHeight && _world.TryGetLoadedBlock(target, out BlockState occupied) && occupied.Block.Collide)
            {
                y++;
                target.y = y;
            }
            if (y >= Chunk.ChunkHeight || !_world.TryGetLoadedBlock(target, out _)) return;

            Block block = entity.Block;
            object data = block.DecodeStateCached(entity.StateId);
            _world.SetBlock(target, block, data);
            Retire(entity);
        }

        internal void Retire(FallingBlockEntity entity)
        {
            ChunkCoord coord = entity.OwnerCoord;
            if (_byChunk.TryGetValue(coord, out List<FallingBlockEntity> list))
            {
                list.Remove(entity);
                if (list.Count == 0) _byChunk.Remove(coord);
            }
            _world.GetChunk(coord)?.MarkPersistenceDirty();
            entity.Deactivate();
            _pool.Push(entity);
            _world.OnFallingBlockReleased(coord);
        }

        internal void DestroyAll()
        {
            foreach (FallingBlockEntity entity in _all)
                if (entity != null) DestroyRuntimeObject(entity.gameObject);
            foreach (Mesh mesh in _meshes.Values)
                if (mesh != null) DestroyRuntimeObject(mesh);
            _all.Clear();
            _pool.Clear();
            _byChunk.Clear();
            _meshes.Clear();
        }

        private static void DestroyRuntimeObject(UnityEngine.Object target)
        {
            if (Application.isPlaying) UnityEngine.Object.Destroy(target);
            else UnityEngine.Object.DestroyImmediate(target);
        }

        private void Spawn(ChunkCoord owner, Block block, ushort stateId, Vector3 position, Vector3 velocity)
        {
            FallingBlockEntity entity = _pool.Count > 0 ? _pool.Pop() : CreateEntity();
            entity.Activate(this, owner, block, stateId, position, velocity, MeshFor(block, stateId), _world.material,
                _world.PlayerController);
            if (!_byChunk.TryGetValue(owner, out List<FallingBlockEntity> list))
            {
                list = new List<FallingBlockEntity>();
                _byChunk.Add(owner, list);
            }
            list.Add(entity);
            _world.GetChunk(owner)?.MarkPersistenceDirty();
        }

        private FallingBlockEntity CreateEntity()
        {
            GameObject gameObject = new("Falling Block");
            gameObject.AddComponent<MeshFilter>();
            gameObject.AddComponent<MeshRenderer>();
            gameObject.AddComponent<BoxCollider>();
            gameObject.AddComponent<Rigidbody>();
            FallingBlockEntity entity = gameObject.AddComponent<FallingBlockEntity>();
            gameObject.transform.SetParent(_world.transform);
            _all.Add(entity);
            return entity;
        }

        private Mesh MeshFor(Block block, ushort stateId)
        {
            long key = ((long)block.BlockId << 32) | stateId;
            if (_meshes.TryGetValue(key, out Mesh mesh)) return mesh;
            MeshBuilder builder = new();
            for (int face = ChunkRenderObject.TopFace; face <= ChunkRenderObject.RightFace; face++)
                builder.AddFace(face, Vector3.zero, block, MeshBuilder.MeshTargets.Opaque);
            mesh = new Mesh { name = $"Falling {block.BlockId}:{stateId}" };
            builder.OpaqueMesh.UploadTo(mesh);
            _meshes.Add(key, mesh);
            return mesh;
        }
    }

    internal sealed class FallingBlockEntity : MonoBehaviour
    {
        private FallingBlockSystem _system;
        private Rigidbody _body;
        private MeshFilter _filter;
        private MeshRenderer _renderer;
        private BoxCollider _collider;
        private bool _active;
        private bool _waitingForColliderRefresh;
        private Vector3 _pendingVelocity;
        private MaterialPropertyBlock _lightProperties;

        private void LateUpdate()
        {
            if (!_active || _renderer == null || World.Instance?.Lighting == null) return;
            Vector2 light = World.Instance.Lighting.Sample(transform.position);
            _lightProperties ??= new MaterialPropertyBlock();
            _lightProperties.SetVector("_VoxelDynamicLight", new Vector4(light.x, light.y, 0, 1));
            _renderer.SetPropertyBlock(_lightProperties);
        }

        internal ChunkCoord OwnerCoord { get; private set; }
        internal Block Block { get; private set; }
        internal ushort StateId { get; private set; }
        internal int ColumnX { get; private set; }
        internal int ColumnZ { get; private set; }
        internal Vector3 PhysicsPosition => _body.position;

        private void Awake() => EnsureComponents();

        private void EnsureComponents()
        {
            if (_filter != null) return;
            _filter = gameObject.GetComponent<MeshFilter>();
            if (_filter == null) _filter = gameObject.AddComponent<MeshFilter>();
            _renderer = gameObject.GetComponent<MeshRenderer>();
            if (_renderer == null) _renderer = gameObject.AddComponent<MeshRenderer>();
            _collider = gameObject.GetComponent<BoxCollider>();
            if (_collider == null) _collider = gameObject.AddComponent<BoxCollider>();
            _collider.center = Vector3.one * 0.5f;
            _collider.size = Vector3.one * 0.98f;
            _body = gameObject.GetComponent<Rigidbody>();
            if (_body == null) _body = gameObject.AddComponent<Rigidbody>();
            _body.constraints = RigidbodyConstraints.FreezePositionX | RigidbodyConstraints.FreezePositionZ |
                                RigidbodyConstraints.FreezeRotation;
            _body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            _body.interpolation = RigidbodyInterpolation.Interpolate;
        }

        internal void Activate(FallingBlockSystem system, ChunkCoord owner, Block block, ushort stateId,
            Vector3 position, Vector3 velocity, Mesh mesh, Material material, CharacterController player)
        {
            EnsureComponents();
            _system = system;
            OwnerCoord = owner;
            Block = block;
            StateId = stateId;
            ColumnX = Mathf.RoundToInt(position.x);
            ColumnZ = Mathf.RoundToInt(position.z);
            transform.position = position;
            _body.position = position;
            transform.rotation = Quaternion.identity;
            _filter.sharedMesh = mesh;
            _renderer.sharedMaterial = material;
            gameObject.SetActive(true);
            // The source voxel is removed immediately after spawning. Wait until that dirty chunk
            // collider is rebuilt, otherwise the entity can collide with the stale source cube.
            _pendingVelocity = velocity;
            _waitingForColliderRefresh = true;
            _body.isKinematic = true;
            if (player != null) Physics.IgnoreCollision(_collider, player, true);
            _active = true;
        }

        internal FallingBlockSnapshot CreateSnapshot() =>
            new(Block.BlockId, StateId, _waitingForColliderRefresh ? transform.position : _body.position,
                _waitingForColliderRefresh ? _pendingVelocity : _body.linearVelocity);

        internal bool Intersects(Bounds bounds)
        {
            if (!_active) return false;
            Vector3 position = _waitingForColliderRefresh ? transform.position : _body.position;
            Bounds colliderBounds = new(position + Vector3.one * 0.5f, Vector3.one * 0.98f);
            return colliderBounds.Intersects(bounds);
        }

        internal void Deactivate()
        {
            _active = false;
            _waitingForColliderRefresh = false;
            _body.linearVelocity = Vector3.zero;
            _body.isKinematic = true;
            gameObject.SetActive(false);
        }

        private void FixedUpdate()
        {
            if (!_active) return;
            if (_waitingForColliderRefresh)
            {
                if (!_system.CanSimulate(OwnerCoord)) return;
                _body.isKinematic = false;
                _body.linearVelocity = _pendingVelocity;
                _body.WakeUp();
                _waitingForColliderRefresh = false;
            }
            Vector3 velocity = _body.linearVelocity;
            if (velocity.y < -FallingBlockSystem.TerminalVelocity)
            {
                velocity.y = -FallingBlockSystem.TerminalVelocity;
                _body.linearVelocity = velocity;
            }
            if (_body.position.y < FallingBlockSystem.VoidDespawnHeight)
            {
                _system.Retire(this);
                return;
            }
            _system.MarkMoved(this);
        }

        private void OnCollisionStay(Collision collision)
        {
            if (!_active || collision.gameObject.layer != LayerMask.NameToLayer("Blocks")) return;
            _system.TrySettle(this);
        }
    }
}
