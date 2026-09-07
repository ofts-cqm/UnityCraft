using System;
using System.Collections.Generic;
using JetBrains.Annotations;
using player;
using UnityEngine;
using world.blocks;
using World.blocks;
using world.generation;
using world.persistence;

namespace World
{
    public class World : MonoBehaviour
    {
        public Material material;
        public Material transparentMaterial;
        public Material waterMaterial;
        public Material waterMobileMaterial;
        private const int ViewDistance = 8;
        private const float AutosaveIntervalSeconds = 30f;
        
        public readonly Dictionary<ChunkCoord, Chunk> ChunkMap = new();
        
        public Transform player;
        public static World Instance;
        public WorldSaveCoordinator Persistence { get; private set; }
        public IWorldStorage Storage { get; private set; }
        public WorldLoadAuthorization LoadAuthorization { get; private set; }

        public Material ActiveWaterMaterial => QualitySettings.names[QualitySettings.GetQualityLevel()] == "PC" || waterMobileMaterial == null
            ? waterMaterial
            : waterMobileMaterial;
        
        private ChunkCoord _playerLastChunkCoord;
        internal int FluidTick { get; private set; }
        private Player _playerComponent;
        private float _nextAutosaveTime;
        private bool _persistenceReady;
        private bool _shuttingDown;

        private void Awake()
        {
            Instance = this;
            try
            {
                Storage = new FileWorldStorage(Application.persistentDataPath);
                LoadAuthorization = WorldSession.SelectedWorld;
                if (LoadAuthorization == null)
                {
                    WorldDescriptor descriptor = Storage.CreateWorld(WorldSession.DefaultWorldId, WorldSession.DefaultWorldName);
                    LoadAuthorization = SaveVersionPolicy.Authorize(descriptor);
                }

                _playerComponent = player.GetComponent<Player>();
                if (_playerComponent == null) throw new InvalidOperationException("The World player transform has no Player component.");
                if (Storage.TryLoadPlayer(LoadAuthorization, out PlayerSnapshot savedPlayer))
                    _playerComponent.ApplyPersistenceSnapshot(savedPlayer);

                Persistence = new WorldSaveCoordinator(Storage, LoadAuthorization);
                _persistenceReady = true;
                _nextAutosaveTime = Time.unscaledTime + AutosaveIntervalSeconds;
            }
            catch (Exception exception)
            {
                Debug.LogError($"Could not open saved world: {exception}");
                enabled = false;
                Player.PauseGame();
            }
        }
        
        // Start is called once before the first execution of Update after the MonoBehaviour is created
        private void Start()
        {
            if (!_persistenceReady) return;
            try
            {
                ChunkLoader.StartWorker();
                ChunkLoader.SyncLoading = true;
                CheckViewDistance();
            }
            catch (Exception exception)
            {
                Debug.LogError($"Could not load the initial world area: {exception}");
                AbortWorldLoad();
            }
            finally { ChunkLoader.SyncLoading = false; }
        }
        
        private void Update() {
            ProcessSaveCompletions();
            if (!new ChunkCoord(player.transform.position).Equals(_playerLastChunkCoord))
                CheckViewDistance();
            ChunkLoader.ProcessCompletedLoads();
            foreach (Chunk chunk in ChunkMap.Values) chunk.UpdateDirtyRenderObjects();
            if (Time.unscaledTime >= _nextAutosaveTime)
            {
                RequestSave();
                _nextAutosaveTime = Time.unscaledTime + AutosaveIntervalSeconds;
            }
        }

        private void FixedUpdate()
        {
            FluidTick++;
            foreach (Chunk chunk in ChunkMap.Values) chunk.TickFluid(FluidTick);
        }

        private void CheckViewDistance()
        {
            HashSet<ChunkCoord> previouslyActiveChunks = new HashSet<ChunkCoord>(ChunkMap.Keys);
            List<ChunkCoord> loadQueue = new List<ChunkCoord>();
            ChunkCoord centerCoord = new ChunkCoord(player.transform.position);
            _playerLastChunkCoord = centerCoord;

            for (int x = centerCoord.X - ViewDistance; x < centerCoord.X + ViewDistance + 1; x++) {
                for (int z = centerCoord.Z - ViewDistance; z < centerCoord.Z + ViewDistance + 1; z++) {
                    ChunkCoord thisChunk = new ChunkCoord(x, z);

                    if (!ChunkMap.ContainsKey(thisChunk)) loadQueue.Add(thisChunk);
                    previouslyActiveChunks.Remove(thisChunk);
                }
            }

            foreach (ChunkCoord coord in previouslyActiveChunks) ChunkLoader.UnloadChunk(coord);
            
            foreach (ChunkCoord coord in loadQueue) ChunkLoader.LoadChunk(coord);
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
                if (completion.Error != null) Debug.LogError($"Could not save world data: {completion.Error}");
            }
        }

        internal void HandleChunkLoadFailure(ChunkCoord coord, Exception exception)
        {
            Debug.LogError($"Could not load chunk ({coord.X}, {coord.Z}): {exception}");
            AbortWorldLoad();
        }

        private void AbortWorldLoad()
        {
            if (_shuttingDown) return;
            enabled = false;
            _shuttingDown = true;
            ChunkLoader.StopWorker();
            Persistence?.FlushAndStop();
            ProcessSaveCompletions();
            _persistenceReady = false;
            Player.PauseGame();
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
            ShutdownPersistence();
            if (Instance == this) Instance = null;
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
