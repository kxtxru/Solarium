using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Solarium.ThreeD
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(WorldObjectPool3D), typeof(ProceduralChunkGenerator3D))]
    public sealed class InfiniteWorld3D : MonoBehaviour
    {
        private sealed class LoadedChunk
        {
            public ChunkCoord3D coord;
            public Transform root;
        }

        [SerializeField] private WorldMode3D mode = WorldMode3D.BoundedArena;
        [SerializeField, Min(8f)] private float chunkSize = InfiniteWorldMath3D.DefaultChunkSize;
        [SerializeField, Range(1, 2)] private int activeRadius = 1;
        [SerializeField] private int configuredSeed = 12345;
        [SerializeField, Min(0.1f)] private float saveDebounceSeconds = 0.75f;

        private readonly Dictionary<ChunkCoord3D, LoadedChunk> loaded = new();
        private readonly Dictionary<ChunkCoord3D, ChunkState3D> states = new();
        private readonly Stack<Transform> availableRoots = new();
        private readonly HashSet<ChunkCoord3D> visitedThisEpisode = new();
        private readonly HashSet<ChunkCoord3D> leftThisEpisode = new();
        private readonly List<ChunkCoord3D> coordinateBuffer = new();
        private SolariumEnvironment3D environment;
        private ProceduralChunkGenerator3D generator;
        private WorldSaveData3D saveData;
        private ChunkCoord3D originChunk;
        private ChunkCoord3D currentChunk;
        private Vector2 physicalAnchor;
        private bool initialized;
        private bool dirty;
        private float nextSaveTime;
        private string savePathOverride;

        public WorldMode3D Mode => mode;
        public bool IsInfinite => mode != WorldMode3D.BoundedArena;
        public bool IsPersistent => mode == WorldMode3D.InfinitePersistent;
        public float ChunkSize => chunkSize;
        public int ActiveChunkCount => loaded.Count;
        public int WorldSeed => saveData == null ? configuredSeed : saveData.worldSeed;
        public float WorldTime => saveData == null ? 0f : saveData.elapsedWorldTime;
        public int DiscoveredChunkCount { get; private set; }
        public int EpisodeNewChunks { get; private set; }
        public int EpisodeRevisitedChunks { get; private set; }
        public ChunkCoord3D CurrentChunk => currentChunk;
        public string SavePath => savePathOverride ?? Path.Combine(
            Application.persistentDataPath, "Solarium3D", "world-v1.json");

        public event Action<Vector3> OriginShifted;
        public event Action WorldStateChanged;

        public void Configure(WorldMode3D value, int seed)
        {
            mode = value;
            configuredSeed = seed;
        }

        public void Initialize(SolariumEnvironment3D owner)
        {
            environment = owner;
            generator = GetComponent<ProceduralChunkGenerator3D>();
            generator.Initialize(owner, GetComponent<WorldObjectPool3D>(), this);
            physicalAnchor = Planar3D.ToPlanar(transform.position);
            initialized = true;
        }

        public Vector2 BeginEpisode(int episodeSeed)
        {
            if (!initialized)
                Initialize(GetComponent<SolariumEnvironment3D>());
            if (!IsInfinite)
                return physicalAnchor;

            visitedThisEpisode.Clear();
            leftThisEpisode.Clear();
            EpisodeNewChunks = 0;
            EpisodeRevisitedChunks = 0;

            if (saveData == null || mode == WorldMode3D.InfiniteTraining)
            {
                if (mode == WorldMode3D.InfinitePersistent)
                    LoadOrCreatePersistentWorld();
                else
                    CreateWorld(episodeSeed);
            }

            ChunkCoord3D spawnChunk = saveData.hasLastShelter
                ? new ChunkCoord3D(saveData.lastShelterChunkX, saveData.lastShelterChunkY)
                : new ChunkCoord3D(0, 0);
            Vector2 spawnLocal = saveData.hasLastShelter
                ? new Vector2(saveData.lastShelterLocalX, saveData.lastShelterLocalY)
                : Vector2.zero;

            UnloadAll();
            originChunk = spawnChunk;
            currentChunk = spawnChunk;
            LoadWindow(currentChunk);
            EnterChunk(currentChunk, false);
            return physicalAnchor + spawnLocal;
        }

        public void Tick()
        {
            if (!IsInfinite || saveData == null || environment == null || environment.Agent == null)
                return;

            saveData.elapsedWorldTime += Time.fixedDeltaTime;
            Vector2 logical = GetLogicalPosition(environment.Agent.transform.position);
            ChunkCoord3D next = InfiniteWorldMath3D.LogicalToChunk(logical, chunkSize);
            if (next != currentChunk)
            {
                leftThisEpisode.Add(currentChunk);
                ShiftOrigin(next);
                currentChunk = next;
                LoadWindow(currentChunk);
                EnterChunk(currentChunk, true);
                MarkDirty();
            }

            generator.TickRespawns(saveData.elapsedWorldTime);
        }

        public Vector2 GetLogicalPosition(Vector3 physicalPosition)
        {
            Vector2 local = Planar3D.ToPlanar(physicalPosition) - physicalAnchor;
            return InfiniteWorldMath3D.ChunkCenter(originChunk, chunkSize) + local;
        }

        public Vector2 PhysicalCenter(ChunkCoord3D coord)
        {
            ChunkCoord3D relative = coord - originChunk;
            return physicalAnchor + new Vector2(relative.x * chunkSize, relative.y * chunkSize);
        }

        public ChunkState3D GetOrCreateState(ChunkCoord3D coord)
        {
            if (states.TryGetValue(coord, out ChunkState3D existing))
                return existing;
            var created = new ChunkState3D
            {
                x = coord.x,
                y = coord.y,
                biome = InfiniteWorldMath3D.BiomeFor(WorldSeed, coord)
            };
            states[coord] = created;
            saveData.chunks.Add(created);
            return created;
        }

        public ChunkEntityState3D GetEntityState(ChunkCoord3D coord, string entityId, bool initiallyAvailable = true) =>
            GetOrCreateState(coord).GetOrCreateEntity(entityId, initiallyAvailable);

        public void ConsumeFood(Food3D food, float respawnSeconds)
        {
            ChunkEntityState3D state = GetEntityState(food.ChunkCoord, food.EntityId);
            state.available = false;
            state.readyAtWorldTime = WorldTime + Mathf.Max(0.1f, respawnSeconds);
            food.gameObject.SetActive(false);
            MarkDirty();
        }

        public bool IsReady(ChunkCoord3D coord, string entityId)
        {
            ChunkEntityState3D state = GetEntityState(coord, entityId);
            return state.available || (state.readyAtWorldTime > 0f && WorldTime >= state.readyAtWorldTime);
        }

        public void SetCooldown(ChunkCoord3D coord, string entityId, float seconds)
        {
            ChunkEntityState3D state = GetEntityState(coord, entityId);
            state.available = false;
            state.readyAtWorldTime = WorldTime + Mathf.Max(0.1f, seconds);
            MarkDirty();
        }

        public void TakeRation(ChunkCoord3D coord, string entityId)
        {
            ChunkEntityState3D state = GetEntityState(coord, entityId);
            state.available = false;
            state.readyAtWorldTime = 0f;
            MarkDirty();
        }

        public void VisitShelter(ShelterZone3D shelter)
        {
            if (shelter == null || !shelter.IsStreamed)
                return;
            ChunkEntityState3D state = GetEntityState(shelter.ChunkCoord, shelter.EntityId);
            state.visited = true;
            saveData.hasLastShelter = true;
            saveData.lastShelterChunkX = shelter.ChunkCoord.x;
            saveData.lastShelterChunkY = shelter.ChunkCoord.y;
            saveData.lastShelterEntityId = shelter.EntityId;
            Vector2 local = Planar3D.ToPlanar(shelter.transform.position) - PhysicalCenter(shelter.ChunkCoord);
            saveData.lastShelterLocalX = local.x;
            saveData.lastShelterLocalY = local.y;
            MarkDirty(true);
        }

        public void NotifyReserveChanged()
        {
            MarkDirty(true);
        }

        public int TotalStoredRations()
        {
            int total = 0;
            foreach (ChunkState3D chunk in states.Values)
            {
                if (chunk.entities == null)
                    continue;
                foreach (ChunkEntityState3D entity in chunk.entities)
                    total += entity == null ? 0 : Mathf.Max(0, entity.storedRations);
            }
            return total;
        }

        public void EndEpisode()
        {
            if (IsPersistent)
                SaveNow();
            if (mode == WorldMode3D.InfiniteTraining)
            {
                UnloadAll();
                states.Clear();
                saveData = null;
            }
        }

        public void CreateNewPersistentWorld(int seed)
        {
            if (!IsPersistent)
                return;
            UnloadAll();
            CreateWorld(seed);
            SaveNow();
            environment?.EndEpisode("manual_restart");
        }

        public string SerializeForTests() => JsonUtility.ToJson(saveData, true);

        public void LoadJsonForTests(string json)
        {
            saveData = JsonUtility.FromJson<WorldSaveData3D>(json);
            RebuildStateIndex();
        }

        public void SetSavePathForTests(string path) => savePathOverride = path;

        private void CreateWorld(int seed)
        {
            states.Clear();
            saveData = new WorldSaveData3D
            {
                worldSeed = seed == 0 ? 12345 : seed,
                elapsedWorldTime = 0f,
                chunks = new List<ChunkState3D>()
            };
            DiscoveredChunkCount = 0;
            dirty = true;
        }

        private void LoadOrCreatePersistentWorld()
        {
            if (!File.Exists(SavePath))
            {
                CreateWorld(configuredSeed);
                SaveNow();
                return;
            }

            try
            {
                string json = File.ReadAllText(SavePath);
                WorldSaveData3D loadedSave = JsonUtility.FromJson<WorldSaveData3D>(json);
                if (loadedSave == null || loadedSave.schemaVersion != WorldSaveData3D.CurrentSchemaVersion)
                    throw new InvalidDataException("Unsupported Solarium world save schema.");
                saveData = loadedSave;
                saveData.chunks ??= new List<ChunkState3D>();
                RebuildStateIndex();
            }
            catch (Exception exception)
            {
                string corrupt = SavePath + $".corrupt-{DateTime.UtcNow:yyyyMMdd-HHmmss}";
                try { File.Move(SavePath, corrupt); }
                catch (Exception moveException) { Debug.LogWarning(moveException.Message, this); }
                Debug.LogWarning($"Solarium world save was corrupt and was preserved at {corrupt}: {exception.Message}", this);
                CreateWorld(configuredSeed);
                SaveNow();
            }
        }

        private void RebuildStateIndex()
        {
            states.Clear();
            DiscoveredChunkCount = 0;
            if (saveData?.chunks == null)
                return;
            foreach (ChunkState3D chunk in saveData.chunks)
            {
                if (chunk == null)
                    continue;
                chunk.entities ??= new List<ChunkEntityState3D>();
                states[chunk.Coord] = chunk;
                if (chunk.visited)
                    DiscoveredChunkCount++;
            }
        }

        private void LoadWindow(ChunkCoord3D center)
        {
            coordinateBuffer.Clear();
            foreach (ChunkCoord3D coord in loaded.Keys)
            {
                if (Mathf.Abs(coord.x - center.x) > activeRadius || Mathf.Abs(coord.y - center.y) > activeRadius)
                    coordinateBuffer.Add(coord);
            }
            foreach (ChunkCoord3D coord in coordinateBuffer)
                UnloadChunk(coord);

            for (int y = -activeRadius; y <= activeRadius; y++)
            for (int x = -activeRadius; x <= activeRadius; x++)
            {
                ChunkCoord3D coord = center + new ChunkCoord3D(x, y);
                if (loaded.ContainsKey(coord))
                    continue;
                LoadChunk(coord);
            }
            Physics.SyncTransforms();
        }

        private void LoadChunk(ChunkCoord3D coord)
        {
            Transform root = availableRoots.Count > 0 ? availableRoots.Pop() : new GameObject("World Chunk").transform;
            root.name = $"Chunk_{coord.x}_{coord.y}";
            root.SetParent(transform, true);
            root.position = Planar3D.ToWorld(PhysicalCenter(coord));
            root.gameObject.SetActive(true);
            ChunkState3D state = GetOrCreateState(coord);
            generator.BuildChunk(coord, state, root, PhysicalCenter(coord), chunkSize);
            loaded[coord] = new LoadedChunk { coord = coord, root = root };
        }

        private void UnloadChunk(ChunkCoord3D coord)
        {
            if (!loaded.TryGetValue(coord, out LoadedChunk chunk))
                return;
            generator.ReleaseChunk(coord, chunk.root);
            chunk.root.gameObject.SetActive(false);
            availableRoots.Push(chunk.root);
            loaded.Remove(coord);
        }

        private void UnloadAll()
        {
            coordinateBuffer.Clear();
            coordinateBuffer.AddRange(loaded.Keys);
            foreach (ChunkCoord3D coord in coordinateBuffer)
                UnloadChunk(coord);
        }

        private void ShiftOrigin(ChunkCoord3D nextOrigin)
        {
            ChunkCoord3D chunks = nextOrigin - originChunk;
            Vector3 appliedShift = new(-chunks.x * chunkSize, 0f, -chunks.y * chunkSize);
            foreach (LoadedChunk chunk in loaded.Values)
                chunk.root.position += appliedShift;
            Rigidbody body = environment.Agent.GetComponent<Rigidbody>();
            body.position += appliedShift;
            environment.Agent.transform.position += appliedShift;
            originChunk = nextOrigin;
            OriginShifted?.Invoke(appliedShift);
        }

        private void EnterChunk(ChunkCoord3D coord, bool reward)
        {
            ChunkState3D state = GetOrCreateState(coord);
            bool firstEver = !state.visited;
            state.visited = true;
            if (firstEver)
            {
                DiscoveredChunkCount++;
                EpisodeNewChunks++;
                if (reward)
                    environment.Agent.AddTrackedReward(environment.Settings.chunkDiscoveryReward, RewardComponent.ChunkDiscovery);
            }
            if (!visitedThisEpisode.Add(coord) && leftThisEpisode.Contains(coord))
                EpisodeRevisitedChunks++;
            WorldStateChanged?.Invoke();
        }

        private void MarkDirty(bool saveImmediately = false)
        {
            dirty = true;
            nextSaveTime = Time.unscaledTime + saveDebounceSeconds;
            WorldStateChanged?.Invoke();
            if (saveImmediately && IsPersistent)
                SaveNow();
        }

        private void Update()
        {
            if (dirty && IsPersistent && Time.unscaledTime >= nextSaveTime)
                SaveNow();
        }

        public void SaveNow()
        {
            if (!IsPersistent || saveData == null)
                return;
            try
            {
                string directory = Path.GetDirectoryName(SavePath);
                if (!string.IsNullOrEmpty(directory))
                    Directory.CreateDirectory(directory);
                string temporary = SavePath + ".tmp";
                File.WriteAllText(temporary, JsonUtility.ToJson(saveData, true));
                if (File.Exists(SavePath))
                    File.Replace(temporary, SavePath, SavePath + ".bak", true);
                else
                    File.Move(temporary, SavePath);
                dirty = false;
            }
            catch (Exception exception)
            {
                Debug.LogError($"Could not save Solarium world: {exception.Message}", this);
            }
        }

        private void OnApplicationQuit() => SaveNow();
        private void OnDestroy() => SaveNow();
    }
}
