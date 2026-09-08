using System;
using System.Collections.Generic;
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
        private Player _playerComponent;
        private float _nextAutosaveTime;
        private bool _persistenceReady;
        private bool _shuttingDown;
        private bool _gameplayReady;
        private HashSet<ChunkCoord> _initialChunks;
        private HashSet<ChunkCoord> _desiredChunks = new();
        private GameplayMenuController _menus;
        private Exception _lastPersistenceError;

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
                ChunkGenerator.Initialize(WorldGenerationSettings.FromSeed(descriptor.worldSeed));

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
            ProcessSaveCompletions();
            ChunkLoader.ProcessCompletedLoads();
            if (_shuttingDown) return;
            foreach (Chunk chunk in ChunkMap.Values) chunk.UpdateDirtyRenderObjects();

            if (!_gameplayReady)
            {
                int loaded = 0;
                foreach (ChunkCoord coord in _initialChunks)
                    if (ChunkMap.ContainsKey(coord)) loaded++;
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
            foreach (Chunk chunk in ChunkMap.Values) chunk.TickFluid(FluidTick);
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
        
        [CanBeNull] public Chunk GetChunk(ChunkCoord coord) => ChunkMap.GetValueOrDefault(coord);

        public BlockState GetBlock(int x, int y, int z)
        {
            return ChunkMap.TryGetValue(ChunkCoord.ToChunkCoord(x, z), out Chunk chunk)
                ? chunk.GetBlock(ToCoordInChunk(x, y, z))
                : Blocks.Void.AsState(x, y, z);
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
            if (ChunkMap.TryGetValue(ChunkCoord.ToChunkCoord(position.x, position.z), out Chunk chunk))
                chunk.ScheduleFluidTick(ToCoordInChunk(position.x, position.y, position.z), delay);
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

        private static Vector3Int ToCoordInChunk(int x0, int y0, int z0)
        {
            int x = x0 % Chunk.ChunkSize;
            if (x < 0) x += Chunk.ChunkSize;
            int z = z0 % Chunk.ChunkSize;
            if (z < 0) z += Chunk.ChunkSize;
            return new Vector3Int(x, y0, z);
        }
    }
}
