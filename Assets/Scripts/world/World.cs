using System;
using System.Collections.Generic;
using Stopwatch = System.Diagnostics.Stopwatch;
using JetBrains.Annotations;
using player;
using UnityEngine;
using UnityEngine.SceneManagement;
using world.blocks;
using World.blocks;
using world.generation;
using world.persistence;
using render.screens;
using settings;
using Render;

namespace World
{
    public class World : MonoBehaviour
    {
        public Material material;
        public Material transparentMaterial;
        public Material waterMaterial;
        public Material waterMobileMaterial;
        public readonly Dictionary<ChunkCoord, Chunk> ChunkMap = new();
        
        public Transform player;
        public static World Instance;
        public WorldSaveCoordinator Persistence { get; private set; }
        private IWorldStorage Storage { get; set; }
        private WorldLoadAuthorization LoadAuthorization { get; set; }

        public Material ActiveWaterMaterial => QualitySettings.names[QualitySettings.GetQualityLevel()] == "PC" || waterMobileMaterial == null
            ? waterMaterial
            : waterMobileMaterial;
        
        private ChunkCoord _playerLastChunkCoord;
        internal int FluidTick { get; private set; }
        [SerializeField, Min(0)] private int maximumFluidUpdatesPerFixedTick;
        private readonly FluidScheduler _fluidScheduler = new();
        private readonly BlockUpdateScheduler _blockUpdateScheduler = new();
        private readonly LeafDistanceCache _leafDistanceCache = new();
        private FallingBlockSystem _fallingBlocks;
        private int _randomTickSeed;
        private readonly Queue<ImmediateBlockUpdate> _immediateBlockUpdates = new();
        private readonly HashSet<ImmediateBlockUpdate> _queuedImmediateBlockUpdates = new();
        private bool _processingImmediateBlockUpdates;
        private Player _playerComponent;
        private float _nextAutosaveTime;
        private bool _persistenceReady;
        private bool _shuttingDown;
        private bool _gameplayReady;
        private HashSet<ChunkCoord> _initialChunks;
        private HashSet<ChunkCoord> _desiredChunks = new();
        private GameplayMenuController _menus;
        private Exception _lastPersistenceError;
        [SerializeField, Min(0.1f)] private float renderRebuildBudgetMilliseconds = 4f;
        private readonly SortedDictionary<int, Queue<RenderWork>> _renderQueue = new();
        private readonly object _renderQueueLock = new();
        private int _queuedRenderCount;
        private int _playerSection;

        private static readonly Vector3Int[] BlockUpdateDirections =
        {
            Vector3Int.zero, Vector3Int.left, Vector3Int.right, Vector3Int.up,
            Vector3Int.down, Vector3Int.forward, Vector3Int.back
        };

        private readonly struct ImmediateBlockUpdate : IEquatable<ImmediateBlockUpdate>
        {
            public readonly Vector3Int Position;
            public readonly ushort BlockId;
            public readonly ushort StateId;

            public ImmediateBlockUpdate(Vector3Int position, ushort blockId, ushort stateId)
            {
                Position = position;
                BlockId = blockId;
                StateId = stateId;
            }

            public bool Equals(ImmediateBlockUpdate other) => Position.Equals(other.Position) &&
                                                               BlockId == other.BlockId && StateId == other.StateId;
            public override bool Equals(object obj) => obj is ImmediateBlockUpdate other && Equals(other);
            public override int GetHashCode() => HashCode.Combine(Position, BlockId, StateId);
        }

        private readonly struct RenderWork
        {
            public readonly Chunk Chunk;
            public readonly ChunkRenderObject RenderObject;

            public RenderWork(Chunk chunk, ChunkRenderObject renderObject)
            {
                Chunk = chunk;
                RenderObject = renderObject;
            }
        }

        private void Awake()
        {
            GameSettings.EnsureLoaded();
            Instance = this;
            GameSettings.Applied += OnSettingsApplied;
            try
            {
                _playerComponent = player.GetComponent<Player>();
                if (_playerComponent == null) throw new InvalidOperationException("The World player transform has no Player component.");
                _playerComponent.SetGameplayReady(false);
                _menus = GameplayMenuController.Create(this);

                Storage = new FileWorldStorage(Application.persistentDataPath);
                LoadAuthorization = WorldSession.SelectedWorld;
                if (LoadAuthorization == null) throw new InvalidOperationException("Select a world before opening gameplay.");

                WorldDescriptor descriptor = Storage.ReadWorldDescriptor(LoadAuthorization.WorldId);
                WorldGenerationSettings generationSettings = WorldGenerationSettings.FromSeed(descriptor.worldSeed);
                ChunkGenerator.Initialize(generationSettings);
                _randomTickSeed = generationSettings.FeatureSeed ^ generationSettings.StructureSeed;
                _fallingBlocks = new FallingBlockSystem(this);

                if (Storage.TryLoadPlayer(LoadAuthorization, out PlayerSnapshot savedPlayer))
                    _playerComponent.ApplyPersistenceSnapshot(savedPlayer);

                Persistence = new WorldSaveCoordinator(Storage, LoadAuthorization);
                _persistenceReady = true;
                _nextAutosaveTime = Time.unscaledTime + GameSettings.AutosaveInterval;
            }
            catch (Exception exception)
            {
                Debug.LogError($"Could not open saved world: {exception}");
                AbortWorldLoad(exception.Message);
            }
        }
        
        // Start is called once before the first execution of Update after the MonoBehaviour is created
        private void Start()
        {
            if (!_persistenceReady) return;
            try
            {
                ChunkLoader.StartWorker();
                BeginInitialLoad();
            }
            catch (Exception exception)
            {
                Debug.LogError($"Could not load the initial world area: {exception}");
                AbortWorldLoad(exception.Message);
            }
        }
        
        private void Update() {
            if (!_persistenceReady || _shuttingDown) return;
            _playerSection = Mathf.Clamp(Mathf.FloorToInt(player.position.y / Chunk.ChunkSize), 0, 7);
            ProcessSaveCompletions();
            ChunkLoader.ProcessCompletedLoads();
            if (_shuttingDown) return;
            ProcessRenderQueue();

            if (!_gameplayReady)
            {
                int loaded = 0;
                foreach (ChunkCoord coord in _initialChunks)
                    if (ChunkMap.TryGetValue(coord, out Chunk chunk) && chunk.IsRenderReady) loaded++;
                _menus.SetLoadingProgress(loaded, _initialChunks.Count);
                if (loaded == _initialChunks.Count) CompleteInitialLoad();
                return;
            }

            if (!new ChunkCoord(player.transform.position).Equals(_playerLastChunkCoord)) CheckViewDistance();
            if (Time.unscaledTime >= _nextAutosaveTime)
            {
                RequestSave();
                _nextAutosaveTime = Time.unscaledTime + GameSettings.AutosaveInterval;
            }
        }

        private void FixedUpdate()
        {
            if (!_gameplayReady || _shuttingDown) return;
            FluidTick++;
            _blockUpdateScheduler.Advance(FluidTick, this);
            ProcessRandomTicks();
            _fluidScheduler.Advance(FluidTick, maximumFluidUpdatesPerFixedTick, this);
        }

        private void BeginInitialLoad()
        {
            ChunkCoord centerCoord = new(player.transform.position);
            _playerLastChunkCoord = centerCoord;
            _initialChunks = new HashSet<ChunkCoord>();
            int viewDistance = GameSettings.ViewDistance;
            for (int x = centerCoord.X - viewDistance; x <= centerCoord.X + viewDistance; x++)
            for (int z = centerCoord.Z - viewDistance; z <= centerCoord.Z + viewDistance; z++)
            {
                ChunkCoord coord = new(x, z);
                _initialChunks.Add(coord);
                ChunkLoader.LoadChunk(coord);
            }
            _desiredChunks = new HashSet<ChunkCoord>(_initialChunks);
            _menus.SetLoadingProgress(0, _initialChunks.Count);
        }

        private void CompleteInitialLoad()
        {
            _gameplayReady = true;
            _nextAutosaveTime = Time.unscaledTime + GameSettings.AutosaveInterval;
            _menus.CompleteLoading();
            _playerComponent.SetGameplayReady(true);
        }

        private void CheckViewDistance()
        {
            ChunkCoord centerCoord = new ChunkCoord(player.transform.position);
            _playerLastChunkCoord = centerCoord;
            int viewDistance = GameSettings.ViewDistance;
            HashSet<ChunkCoord> desiredChunks = new();

            for (int x = centerCoord.X - viewDistance; x < centerCoord.X + viewDistance + 1; x++) {
                for (int z = centerCoord.Z - viewDistance; z < centerCoord.Z + viewDistance + 1; z++) {
                    ChunkCoord thisChunk = new ChunkCoord(x, z);
                    desiredChunks.Add(thisChunk);
                }
            }

            // Cancel obsolete pending requests as well as unload chunks that have already arrived.
            foreach (ChunkCoord coord in _desiredChunks)
                if (!desiredChunks.Contains(coord)) ChunkLoader.UnloadChunk(coord);
            foreach (ChunkCoord coord in new List<ChunkCoord>(ChunkMap.Keys))
                if (!desiredChunks.Contains(coord)) ChunkLoader.UnloadChunk(coord);
            foreach (ChunkCoord coord in desiredChunks)
                if (!ChunkMap.ContainsKey(coord)) ChunkLoader.LoadChunk(coord);
            _desiredChunks = desiredChunks;
        }

        public void RequestSave()
        {
            if (!_persistenceReady || _shuttingDown || Persistence == null) return;
            if (_playerComponent != null) Persistence.QueuePlayer(_playerComponent.CreatePersistenceSnapshot());
            foreach (Chunk chunk in ChunkMap.Values) QueueChunkSave(chunk);
        }

        internal void QueueChunkSave(Chunk chunk)
        {
            if (!_persistenceReady || _shuttingDown || Persistence == null || chunk == null) return;
            if (chunk.TryCreatePersistenceSnapshot(out ChunkSnapshot snapshot)) Persistence.QueueChunk(chunk, snapshot);
        }

        private void ProcessSaveCompletions()
        {
            if (Persistence == null) return;
            while (Persistence.TryDequeueCompletion(out WorldSaveCoordinator.SaveCompletion completion))
            {
                if (completion.Chunk != null) completion.Chunk.CompletePersistenceSave(completion.Revision, completion.Error == null);
                if (completion.Error != null)
                {
                    _lastPersistenceError = completion.Error;
                    Debug.LogError($"Could not save world data: {completion.Error}");
                }
            }
        }

        internal void HandleChunkLoadFailure(ChunkCoord coord, Exception exception)
        {
            Debug.LogError($"Could not load chunk ({coord.X}, {coord.Z}): {exception}");
            AbortWorldLoad($"Chunk ({coord.X}, {coord.Z}) could not be loaded. {exception.Message}");
        }

        private void AbortWorldLoad(string message)
        {
            if (_shuttingDown) return;
            enabled = false;
            _shuttingDown = true;
            ChunkLoader.StopWorker();
            Persistence?.FlushAndStop();
            ProcessSaveCompletions();
            _persistenceReady = false;
            WorldSession.ReportError(message);
            WorldSession.ClearSelection();
            Time.timeScale = 1f;
            SceneManager.LoadScene(GameScenes.WorldSelection);
        }

        public void SaveAndQuitToWorldSelection()
        {
            if (_shuttingDown) return;
            string error = null;
            try
            {
                ShutdownPersistence();
                if (_lastPersistenceError != null) error = $"The world could not be fully saved. {_lastPersistenceError.Message}";
            }
            catch (Exception exception)
            {
                error = $"The world could not be fully saved. {exception.Message}";
                Debug.LogError(error);
            }
            WorldSession.ClearSelection();
            if (error != null) WorldSession.ReportError(error);
            Time.timeScale = 1f;
            SceneManager.LoadScene(GameScenes.WorldSelection);
        }

        private void OnApplicationPause(bool paused)
        {
            if (!paused || !_persistenceReady || _shuttingDown) return;
            RequestSave();
            Persistence.Flush();
            ProcessSaveCompletions();
        }

        private void OnApplicationQuit() => ShutdownPersistence();

        private void OnDestroy()
        {
            GameSettings.Applied -= OnSettingsApplied;
            ShutdownPersistence();
            foreach (Chunk chunk in ChunkMap.Values) chunk.DestroyChunk();
            ChunkMap.Clear();
            _fluidScheduler.Clear();
            _blockUpdateScheduler.Clear();
            _fallingBlocks?.DestroyAll();
            lock (_renderQueueLock)
            {
                _renderQueue.Clear();
                _queuedRenderCount = 0;
            }
            if (Instance == this) Instance = null;
        }

        private void OnSettingsApplied()
        {
            _nextAutosaveTime = Time.unscaledTime + GameSettings.AutosaveInterval;
            if (_gameplayReady && !_shuttingDown) CheckViewDistance();
        }

        private void ShutdownPersistence()
        {
            if (!_persistenceReady || _shuttingDown) return;
            RequestSave();
            _shuttingDown = true;
            ChunkLoader.StopWorker();
            Persistence.FlushAndStop();
            ProcessSaveCompletions();
            _persistenceReady = false;
        }

        internal void EnqueueRenderObject(Chunk chunk, ChunkRenderObject renderObject)
        {
            if (!renderObject.TryReserveQueueEntry()) return;

            int horizontalDistance = Math.Max(Math.Abs(chunk.ChunkPosition.X - _playerLastChunkCoord.X),
                Math.Abs(chunk.ChunkPosition.Z - _playerLastChunkCoord.Z));
            int priority = horizontalDistance * 16 + Math.Abs(renderObject.SectionIndex - _playerSection);
            lock (_renderQueueLock)
            {
                if (!_renderQueue.TryGetValue(priority, out Queue<RenderWork> bucket))
                {
                    bucket = new Queue<RenderWork>();
                    _renderQueue.Add(priority, bucket);
                }
                bucket.Enqueue(new RenderWork(chunk, renderObject));
                _queuedRenderCount++;
            }
        }

        private void ProcessRenderQueue()
        {
            long started = Stopwatch.GetTimestamp();
            double budgetSeconds = Math.Max(0.1f, renderRebuildBudgetMilliseconds) / 1000.0;
            bool processedAny = false;
            while ((!processedAny || (double)(Stopwatch.GetTimestamp() - started) / Stopwatch.Frequency < budgetSeconds) &&
                   TryDequeueRenderWork(out RenderWork work))
            {
                processedAny = true;
                work.RenderObject.ReleaseQueueEntry();
                if (!work.Chunk.IsActive || !work.RenderObject.Dirty) continue;
                work.RenderObject.RerenderChunk();
            }
        }

        private bool TryDequeueRenderWork(out RenderWork work)
        {
            lock (_renderQueueLock)
            {
                if (_queuedRenderCount == 0)
                {
                    work = default;
                    return false;
                }

                int priority = 0;
                Queue<RenderWork> bucket = null;
                foreach (KeyValuePair<int, Queue<RenderWork>> pair in _renderQueue)
                {
                    priority = pair.Key;
                    bucket = pair.Value;
                    break;
                }

                work = bucket.Dequeue();
                _queuedRenderCount--;
                if (bucket.Count == 0) _renderQueue.Remove(priority);
                return true;
            }
        }
        
        [CanBeNull] public Chunk GetChunk(ChunkCoord coord) => ChunkMap.GetValueOrDefault(coord);

        public BlockState GetBlock(int x, int y, int z)
        {
            if (!ChunkMap.TryGetValue(ChunkCoord.ToChunkCoord(x, z), out Chunk chunk))
                return Blocks.Void.AsState(x, y, z);
            BlockState local = chunk.GetBlock(ToCoordInChunk(x, y, z));
            return local.Block.AsState(x, y, z, local.Data);
        }

        public BlockState GetBlock(Vector3Int position)
        {
            return GetBlock(position.x, position.y, position.z);
        }

        public FluidState GetFluid(int x, int y, int z)
        {
            return ChunkMap.TryGetValue(ChunkCoord.ToChunkCoord(x, z), out Chunk chunk)
                ? chunk.GetFluid(ToCoordInChunk(x, y, z))
                : default;
        }

        public FluidState GetFluid(Vector3Int position) => GetFluid(position.x, position.y, position.z);

        public Vector3 GetFluidFlow(Vector3Int position)
        {
            return ChunkMap.TryGetValue(ChunkCoord.ToChunkCoord(position.x, position.z), out Chunk chunk)
                ? Water.GetFlow(chunk, ToCoordInChunk(position.x, position.y, position.z))
                : Vector3.zero;
        }

        public void SetFluid(int x, int y, int z, FluidState state)
        {
            if (ChunkMap.TryGetValue(ChunkCoord.ToChunkCoord(x, z), out Chunk chunk)) chunk.SetFluid(ToCoordInChunk(x, y, z), state);
        }

        public void SetFluid(Vector3Int position, FluidState state) => SetFluid(position.x, position.y, position.z, state);

        internal void ScheduleFluidTick(Vector3Int position, int delay)
        {
            if (position.y < 0 || position.y >= Chunk.ChunkHeight) return;
            if (ChunkMap.TryGetValue(ChunkCoord.ToChunkCoord(position.x, position.z), out Chunk chunk))
                chunk.ScheduleFluidTick(ToCoordInChunk(position.x, position.y, position.z), delay);
        }

        internal void ScheduleFluidTick(Chunk chunk, Vector3Int localPosition, byte expectedRawAmount, int delay)
        {
            _fluidScheduler.Schedule(chunk, localPosition, expectedRawAmount, FluidTick + Math.Max(0, delay));
        }

        internal void SuspendFluidTicks(Chunk chunk) => _fluidScheduler.Suspend(chunk);
        internal void ResumeFluidTicks(Chunk chunk) => _fluidScheduler.Resume(chunk);
        internal void CancelFluidTicks(Chunk chunk) => _fluidScheduler.Cancel(chunk);

        internal Chunk ResolveActiveFluidChunk(ChunkCoord coord, int generation)
        {
            return ChunkMap.TryGetValue(coord, out Chunk chunk) && chunk.FluidGeneration == generation && chunk.IsActive
                ? chunk
                : null;
        }

        internal void SuspendBlockUpdates(Chunk chunk) => _blockUpdateScheduler.Suspend(chunk);
        internal void ResumeBlockUpdates(Chunk chunk) => _blockUpdateScheduler.Resume(chunk);
        internal void CancelBlockUpdates(Chunk chunk) => _blockUpdateScheduler.Cancel(chunk);

        internal Chunk ResolveActiveBlockChunk(ChunkCoord coord, int generation)
        {
            return ChunkMap.TryGetValue(coord, out Chunk chunk) && chunk.FluidGeneration == generation && chunk.IsActive
                ? chunk
                : null;
        }

        internal ScheduledBlockUpdateSnapshot[] CaptureScheduledBlockUpdates(Chunk chunk) =>
            _blockUpdateScheduler.Capture(chunk);

        internal FallingBlockSnapshot[] CaptureFallingBlocks(Chunk chunk) =>
            _fallingBlocks?.Capture(chunk) ?? Array.Empty<FallingBlockSnapshot>();

        internal bool IntersectsFallingBlock(Vector3 center, Vector3 halfExtents) =>
            _fallingBlocks != null && _fallingBlocks.Intersects(new Bounds(center, halfExtents * 2f));

        internal bool HasPersistentRuntimeState(Chunk chunk) =>
            _blockUpdateScheduler.HasPending(chunk) || _fallingBlocks != null && _fallingBlocks.HasActive(chunk.ChunkPosition);

        internal void RestoreChunkRuntimeState(Chunk chunk, ScheduledBlockUpdateSnapshot[] updates,
            FallingBlockSnapshot[] fallingBlocks)
        {
            foreach (var t in updates)
                _blockUpdateScheduler.Restore(chunk, t);

            foreach (var t in fallingBlocks)
                _fallingBlocks.Restore(chunk, t);
        }

        public void SetBlock(int x, int y, int z, Block block, [CanBeNull] object state = null)
        {
            if (ChunkMap.TryGetValue(ChunkCoord.ToChunkCoord(x, z), out Chunk chunk))
            {
                x %= Chunk.ChunkSize;
                if (x < 0) x += Chunk.ChunkSize;
                z %= Chunk.ChunkSize;
                if (z < 0) z += Chunk.ChunkSize;
                chunk.SetBlock(x, y, z, block, state);
            }
        }

        public void SetBlock(Vector3Int position, Block block, [CanBeNull] object state = null)
        {
            if (ChunkMap.TryGetValue(new ChunkCoord(position), out Chunk chunk))
            {
                chunk.SetBlock(ToCoordInChunk(position.x, position.y, position.z), block, state);
            }
        }

        // Todo: make no longer restricted to oak. 
        internal void NotifyBlockChanged(Vector3Int position, Block previous, Block next)
        {
            bool leafTopologyChanged = previous.BlockId == Blocks.Log.Oak.BlockId ||
                                       previous.BlockId == Blocks.OakLeave.BlockId ||
                                       next.BlockId == Blocks.Log.Oak.BlockId ||
                                       next.BlockId == Blocks.OakLeave.BlockId;
            if (leafTopologyChanged) _leafDistanceCache.InvalidateAround(this, position);

            foreach (var t in BlockUpdateDirections)
                QueueBlockUpdate(position + t);

            DrainImmediateBlockUpdates();
        }

        private void QueueBlockUpdate(Vector3Int position)
        {
            if (!TryGetLoadedCell(position, out Chunk chunk, out Vector3Int local, out Block block,
                    out ushort stateId)) return;
            int? delay = block.BlockUpdateDelayTicks;
            if (!delay.HasValue) return;
            if (delay.Value < 0)
                throw new InvalidOperationException($"Block {block.BlockId} declared a negative block-update delay.");
            if (delay.Value > 0)
            {
                _blockUpdateScheduler.Schedule(chunk, local, (ushort)block.BlockId, stateId, delay.Value);
                return;
            }

            ImmediateBlockUpdate update = new(position, (ushort)block.BlockId, stateId);
            if (_queuedImmediateBlockUpdates.Add(update)) _immediateBlockUpdates.Enqueue(update);
        }

        private void DrainImmediateBlockUpdates()
        {
            if (_processingImmediateBlockUpdates) return;
            _processingImmediateBlockUpdates = true;
            try
            {
                while (_immediateBlockUpdates.Count > 0)
                {
                    ImmediateBlockUpdate update = _immediateBlockUpdates.Dequeue();
                    _queuedImmediateBlockUpdates.Remove(update);
                    if (!TryGetLoadedCell(update.Position, out _, out _, out Block block, out ushort stateId) ||
                        block.BlockId != update.BlockId || stateId != update.StateId) continue;
                    block.OnBlockUpdate(this,
                        block.AsState(update.Position, block.DecodeStateCached(stateId)));
                }
            }
            finally { _processingImmediateBlockUpdates = false; }
        }

        internal bool TryGetLoadedBlock(Vector3Int position, out BlockState state)
        {
            if (position.y < 0 || position.y >= Chunk.ChunkHeight)
            {
                state = Blocks.Air.AsState(position);
                return true;
            }
            if (!TryGetLoadedCell(position, out _, out _, out Block block, out ushort stateId))
            {
                state = default;
                return false;
            }
            state = block.AsState(position, block.DecodeStateCached(stateId));
            return true;
        }

        private bool TryGetLoadedCell(Vector3Int position, out Chunk chunk, out Vector3Int local,
            out Block block, out ushort stateId)
        {
            local = ToCoordInChunk(position);
            chunk = null;
            if (position.y < 0 || position.y >= Chunk.ChunkHeight ||
                !ChunkMap.TryGetValue(ChunkCoord.ToChunkCoord(position.x, position.z), out chunk))
            {
                block = null;
                stateId = 0;
                return false;
            }
            chunk.GetCellUnchecked(local.x, local.y, local.z, out block, out stateId);
            return true;
        }

        internal bool IsGrassBlocked(Vector3Int position)
        {
            BlockState block = GetBlock(position);
            return !GetFluid(position).IsEmpty || block.Block.IsSolid(block, ChunkRenderObject.BottomFace);
        }

        internal byte ResolveLeafDistance(Vector3Int position) => _leafDistanceCache.Resolve(this, position);

        internal void OnChunkTopologyChanged(ChunkCoord coord) => _leafDistanceCache.OnChunkTopologyChanged(this, coord);

        internal void TryStartFallingBlock(BlockState scheduledState)
        {
            if (!TryGetLoadedBlock(scheduledState.Position, out BlockState current) ||
                current.Block.BlockId != scheduledState.Block.BlockId ||
                current.Block.EncodeStateCompact(current.Data) != scheduledState.Block.EncodeStateCompact(scheduledState.Data)) return;
            BlockState below = GetBlock(scheduledState.Position + Vector3Int.down);
            if (below.Block.Collide) return;
            _fallingBlocks.Spawn(current, Vector3.zero);
            SetBlock(scheduledState.Position, Blocks.Air);
        }

        internal bool ShouldPinChunk(ChunkCoord coord) => _fallingBlocks != null && _fallingBlocks.HasActive(coord);

        internal CharacterController PlayerController => _playerComponent != null ? _playerComponent.characterController : null;

        internal void OnFallingBlockReleased(ChunkCoord coord)
        {
            if (_gameplayReady && !_desiredChunks.Contains(coord) && ChunkMap.ContainsKey(coord))
                ChunkLoader.UnloadChunk(coord);
        }

        private void ProcessRandomTicks()
        {
            foreach (Chunk chunk in ChunkMap.Values)
            {
                if (!chunk.IsActive) continue;
                for (int section = 0; section < ChunkData.SectionCount; section++)
                {
                    uint hash = RandomTickHash(_randomTickSeed, FluidTick, chunk.ChunkPosition, section);
                    int index = (int)(hash & (ChunkData.CellsPerSection - 1));
                    int x = index & 15;
                    int z = (index >> 4) & 15;
                    int y = section * Chunk.ChunkSize + ((index >> 8) & 15);
                    chunk.GetCellUnchecked(x, y, z, out Block block, out ushort stateId);
                    if (!block.ReceivesRandomTicks) continue;
                    Vector3Int position = new(chunk.ChunkPosition.X * Chunk.ChunkSize + x, y,
                        chunk.ChunkPosition.Z * Chunk.ChunkSize + z);
                    block.OnRandomTick(this, block.AsState(position, block.DecodeStateCached(stateId)));
                }
            }
        }

        private static uint RandomTickHash(int seed, int tick, ChunkCoord coord, int section)
        {
            unchecked
            {
                uint value = (uint)seed;
                value ^= (uint)tick * 0x9E3779B9u;
                value ^= (uint)coord.X * 0x85EBCA6Bu;
                value ^= (uint)coord.Z * 0xC2B2AE35u;
                value ^= (uint)section * 0x27D4EB2Fu;
                value ^= value >> 16;
                value *= 0x7FEB352Du;
                value ^= value >> 15;
                value *= 0x846CA68Bu;
                return value ^ (value >> 16);
            }
        }

        internal static Vector3Int ToCoordInChunk(Vector3Int position) =>
            ToCoordInChunk(position.x, position.y, position.z);

        internal static Vector3Int ToCoordInChunk(int x0, int y0, int z0)
        {
            int x = x0 % Chunk.ChunkSize;
            if (x < 0) x += Chunk.ChunkSize;
            int z = z0 % Chunk.ChunkSize;
            if (z < 0) z += Chunk.ChunkSize;
            return new Vector3Int(x, y0, z);
        }
    }
}
