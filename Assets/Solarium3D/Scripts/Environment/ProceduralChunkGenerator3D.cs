using System;
using System.Collections.Generic;
using UnityEngine;

namespace Solarium.ThreeD
{
    [DisallowMultipleComponent]
    public sealed class ProceduralChunkGenerator3D : MonoBehaviour
    {
        private readonly SpawnValidator validator = new();
        private readonly Dictionary<string, Food3D> activeFoods = new();
        private SolariumEnvironment3D environment;
        private WorldObjectPool3D pool;
        private InfiniteWorld3D world;
        private System.Random random;
        private Transform chunkRoot;
        private ChunkCoord3D chunkCoord;
        private ChunkState3D chunkState;
        private Vector2 chunkCenter;
        private float chunkSize;

        public void Initialize(SolariumEnvironment3D owner, WorldObjectPool3D objectPool, InfiniteWorld3D infiniteWorld)
        {
            environment = owner;
            pool = objectPool;
            world = infiniteWorld;
        }

        public void BuildChunk(
            ChunkCoord3D coord,
            ChunkState3D state,
            Transform parent,
            Vector2 physicalCenter,
            float size)
        {
            chunkCoord = coord;
            chunkState = state;
            chunkRoot = parent;
            chunkCenter = physicalCenter;
            chunkSize = size;
            random = new System.Random(InfiniteWorldMath3D.ChunkSeed(world.WorldSeed, coord));
            state.biome = InfiniteWorldMath3D.BiomeFor(world.WorldSeed, coord);
            validator.Reset(physicalCenter, new Vector2(size, size), 0.8f);

            CreateGround(state.biome);
            CreateFoods(state.biome);
            CreateHealing(state.biome);
            CreateRations(state.biome);
            if (InfiniteWorldMath3D.IsSanctuaryChunk(world.WorldSeed, coord))
                CreateShelter();
            CreateObstacles(state.biome);
            CreateTerrain(state.biome);
            CreateEnemies(state.biome);
            CreateDecorations(state.biome);
        }

        public void ReleaseChunk(ChunkCoord3D coord, Transform root)
        {
            foreach (Food3D food in root.GetComponentsInChildren<Food3D>(true))
            {
                activeFoods.Remove(Key(coord, food.EntityId));
                environment.Unregister(food);
            }
            foreach (HealingZone3D healing in root.GetComponentsInChildren<HealingZone3D>(true))
                environment.Unregister(healing);
            foreach (SupplyShard3D ration in root.GetComponentsInChildren<SupplyShard3D>(true))
                environment.Unregister(ration);
            foreach (ShelterZone3D shelter in root.GetComponentsInChildren<ShelterZone3D>(true))
                environment.Unregister(shelter);
            foreach (EnemyController3D enemy in root.GetComponentsInChildren<EnemyController3D>(true))
                environment.Unregister(enemy);
            pool.ReturnEpisodeChildren(root);
        }

        public void TickRespawns(float worldTime)
        {
            foreach (KeyValuePair<string, Food3D> pair in activeFoods)
            {
                Food3D food = pair.Value;
                if (food == null || food.gameObject.activeSelf)
                    continue;
                ChunkEntityState3D state = world.GetEntityState(food.ChunkCoord, food.EntityId);
                if (state.readyAtWorldTime <= 0f || worldTime < state.readyAtWorldTime)
                    continue;
                state.available = true;
                state.readyAtWorldTime = 0f;
                food.gameObject.SetActive(true);
            }
        }

        private void CreateGround(Biome3D biome)
        {
            GameObject go = Rent("chunk_ground", BuildGround, "Ground", chunkCenter, -0.16f,
                new Vector3(chunkSize, 0.3f, chunkSize));
            Color tint = biome switch
            {
                Biome3D.Meadow => new Color(0.19f, 0.38f, 0.22f),
                Biome3D.Forest => new Color(0.10f, 0.27f, 0.17f),
                Biome3D.Wetland => new Color(0.14f, 0.30f, 0.28f),
                _ => new Color(0.29f, 0.17f, 0.12f)
            };
            var block = new MaterialPropertyBlock();
            block.SetColor("_BaseColor", tint);
            foreach (MeshRenderer renderer in go.GetComponentsInChildren<MeshRenderer>(true))
                renderer.SetPropertyBlock(block);
            go.SetActive(true);
        }

        private void CreateFoods(Biome3D biome)
        {
            int regularCount = biome switch
            {
                Biome3D.Meadow => 4,
                Biome3D.Forest => 3,
                Biome3D.Wetland => 2,
                _ => 1
            };
            for (int i = 0; i < regularCount; i++)
                CreateFood($"food_{i}", false, i);
            if (InfiniteWorldMath3D.ChunkSeed(world.WorldSeed ^ 0x16B7, chunkCoord) % 5 == 0)
                CreateFood("golden_food_0", true, regularCount + 7);
        }

        private void CreateFood(string id, bool golden, int fallbackIndex)
        {
            float radius = golden ? 0.48f : 0.36f;
            Vector2 position = FindResourcePoint(radius, 0.35f, id, fallbackIndex);
            GameObject go = Rent(golden ? "golden_food" : "food",
                () => BuildTrigger(golden ? "Golden Food" : "Food", 0.42f, golden ? VisualKind3D.GoldenFood : VisualKind3D.Food),
                golden ? "GoldenFood" : "Food", position, 0.48f, Vector3.one);
            Food3D food = go.GetComponent<Food3D>() ?? go.AddComponent<Food3D>();
            food.InitializeStreamed(environment, golden, chunkCoord, id);
            ChunkEntityState3D state = world.GetEntityState(chunkCoord, id);
            if (!state.available && state.readyAtWorldTime > 0f && world.WorldTime >= state.readyAtWorldTime)
            {
                state.available = true;
                state.readyAtWorldTime = 0f;
            }
            go.SetActive(state.available);
            activeFoods[Key(chunkCoord, id)] = food;
            environment.Register(food);
        }

        private void CreateHealing(Biome3D biome)
        {
            if (biome == Biome3D.Volcanic || biome == Biome3D.Meadow || Next01() < 0.65f)
            {
                const string id = "healing_0";
                Vector2 position = FindResourcePoint(0.6f, 0.4f, id, 13);
                GameObject go = Rent("healing", () => BuildTrigger("Healing", 0.58f, VisualKind3D.Healing),
                    "Healing", position, 0.48f, Vector3.one);
                HealingZone3D healing = go.GetComponent<HealingZone3D>() ?? go.AddComponent<HealingZone3D>();
                healing.InitializeStreamed(environment, world, chunkCoord, id);
                go.SetActive(true);
                environment.Register(healing);
            }
        }

        private void CreateRations(Biome3D biome)
        {
            int count = biome == Biome3D.Meadow ? 2 : 1;
            for (int i = 0; i < count; i++)
            {
                string id = $"ration_{i}";
                Vector2 position = FindResourcePoint(0.34f, 0.45f, id, 19 + i);
                GameObject go = Rent("supply", () => BuildTrigger("Ration", 0.36f, VisualKind3D.Supply),
                    $"Ration_{i}", position, 0.42f, Vector3.one);
                SupplyShard3D ration = go.GetComponent<SupplyShard3D>() ?? go.AddComponent<SupplyShard3D>();
                ration.InitializeStreamed(environment, world, chunkCoord, id);
                ChunkEntityState3D state = world.GetEntityState(chunkCoord, id);
                go.SetActive(state.available);
                environment.Register(ration);
            }
        }

        private void CreateShelter()
        {
            const string id = "shelter_0";
            Vector2 position = FindResourcePoint(0.9f, 0.8f, id, 31);
            GameObject go = Rent("shelter", () => BuildTrigger("Shelter", 0.8f, VisualKind3D.Shelter),
                "Shelter", position, 0.55f, Vector3.one);
            ShelterZone3D shelter = go.GetComponent<ShelterZone3D>() ?? go.AddComponent<ShelterZone3D>();
            shelter.InitializeStreamed(environment, world, chunkCoord, id);
            if (environment.VisualsEnabled)
            {
                ReserveIndicator3D indicator = go.GetComponent<ReserveIndicator3D>() ?? go.AddComponent<ReserveIndicator3D>();
                indicator.Initialize(environment.Palette);
                shelter.BindIndicator(indicator);
            }
            go.SetActive(true);
            environment.Register(shelter);
        }

        private void CreateObstacles(Biome3D biome)
        {
            int count = biome switch
            {
                Biome3D.Meadow => 1,
                Biome3D.Forest => 6,
                Biome3D.Wetland => 2,
                _ => 4
            };
            for (int i = 0; i < count; i++)
            {
                Vector2 size = new(Next(1.1f, 2.8f), Next(1f, 2.4f));
                if (!TryFindBlockingRect(size, 0.55f, "obstacle", out Vector2 position))
                    continue;
                CreateSolid($"Obstacle_{i}", "obstacle", position, size, 1.1f, VisualKind3D.Obstacle);
            }
        }

        private void CreateTerrain(Biome3D biome)
        {
            int mud = biome == Biome3D.Wetland ? 4 : biome == Biome3D.Forest ? 1 : 0;
            int lava = biome == Biome3D.Volcanic ? 3 : 0;
            int hazards = biome == Biome3D.Volcanic ? 2 : biome == Biome3D.Wetland ? 1 : 0;
            for (int i = 0; i < mud; i++)
                CreatePatch(i, "mud", VisualKind3D.Mud, new Vector2(1.5f, 2.8f), new Vector2(1.3f, 2.4f));
            for (int i = 0; i < lava; i++)
                CreatePatch(i, "lava", VisualKind3D.Lava, new Vector2(1.1f, 1.8f), new Vector2(1.1f, 1.8f));
            for (int i = 0; i < hazards; i++)
                CreatePatch(i, "hazard", VisualKind3D.Hazard, new Vector2(1.2f, 2.2f), new Vector2(1.2f, 2.2f));
        }

        private void CreatePatch(int index, string kind, VisualKind3D visual, Vector2 xRange, Vector2 yRange)
        {
            Vector2 size = new(Next(xRange.x, xRange.y), Next(yRange.x, yRange.y));
            if (!TryFindBlockingRect(size, 0.55f, kind, out Vector2 position))
                return;
            GameObject go = Rent(kind, () => BuildPatch(kind, visual), $"{kind}_{index}", position, 0.08f,
                new Vector3(size.x, 1f, size.y));
            if (kind == "mud") (go.GetComponent<MudZone3D>() ?? go.AddComponent<MudZone3D>()).Initialize(environment);
            else if (kind == "lava") (go.GetComponent<LavaZone3D>() ?? go.AddComponent<LavaZone3D>()).Initialize(environment);
            else (go.GetComponent<Hazard3D>() ?? go.AddComponent<Hazard3D>()).Initialize(environment);
            go.SetActive(true);
        }

        private void CreateEnemies(Biome3D biome)
        {
            int hunters = biome == Biome3D.Volcanic ? 2 : 1;
            int patrollers = biome == Biome3D.Forest || biome == Biome3D.Wetland ? 1 : 0;
            int total = hunters + patrollers;
            for (int i = 0; i < total; i++)
            {
                bool patroller = i >= hunters;
                string kind = patroller ? "patroller" : "hunter";
                Vector2 position = FindResourcePoint(0.55f, 0.8f, $"{kind}_{i}", 41 + i);
                GameObject go = Rent(kind, () => BuildEnemy(patroller), $"{kind}_{i}", position, 0.55f, Vector3.one);
                EnemyController3D enemy = go.GetComponent<EnemyController3D>();
                enemy.Initialize(environment, patroller, position);
                go.SetActive(true);
                environment.Register(enemy);
            }
        }

        private void CreateDecorations(Biome3D biome)
        {
            if (!environment.VisualsEnabled)
                return;
            int count = biome == Biome3D.Forest ? 30 : 18;
            for (int i = 0; i < count; i++)
            {
                Vector2 local = new(Next(-chunkSize * 0.47f, chunkSize * 0.47f), Next(-chunkSize * 0.47f, chunkSize * 0.47f));
                if (InSafeCorridor(local, 0.25f))
                    continue;
                VisualKind3D visual = i % 5 == 0 || biome == Biome3D.Volcanic ? VisualKind3D.Stone : VisualKind3D.Foliage;
                string key = visual == VisualKind3D.Stone ? "decor_stone" : "decor_foliage";
                float scale = visual == VisualKind3D.Stone ? Next(0.35f, 0.7f) : Next(0.3f, 0.75f);
                GameObject go = Rent(key, () => BuildDecoration(visual), $"Decoration_{i}", chunkCenter + local,
                    visual == VisualKind3D.Stone ? 0.2f : 0.3f, Vector3.one * scale);
                go.transform.rotation = Quaternion.Euler(0f, Next(0f, 360f), 0f);
                go.SetActive(true);
            }
        }

        private Vector2 FindResourcePoint(float radius, float clearance, string kind, int fallbackIndex)
        {
            for (int attempt = 0; attempt < environment.Settings.spawnAttempts; attempt++)
            {
                Vector2 local = new(Next(-chunkSize * 0.43f, chunkSize * 0.43f), Next(-chunkSize * 0.43f, chunkSize * 0.43f));
                Vector2 candidate = chunkCenter + local;
                if (validator.TryReserveCircle(candidate, radius, clearance, kind))
                    return candidate;
            }

            Vector2[] safeFallbacks =
            {
                new(-7f, -7f), new(7f, 7f), new(-7f, 7f), new(7f, -7f),
                new(-9f, -4f), new(9f, 4f), new(-4f, 9f), new(4f, -9f)
            };
            for (int offset = 0; offset < safeFallbacks.Length; offset++)
            {
                Vector2 candidate = chunkCenter + safeFallbacks[(fallbackIndex + offset) % safeFallbacks.Length];
                if (validator.TryReserveCircle(candidate, radius, clearance, kind))
                    return candidate;
            }
            return chunkCenter + new Vector2(5f, 5f);
        }

        private bool TryFindBlockingRect(Vector2 size, float clearance, string kind, out Vector2 found)
        {
            for (int attempt = 0; attempt < environment.Settings.spawnAttempts; attempt++)
            {
                Vector2 local = new(Next(-chunkSize * 0.42f, chunkSize * 0.42f), Next(-chunkSize * 0.42f, chunkSize * 0.42f));
                if (InSafeCorridor(local, Mathf.Max(size.x, size.y) * 0.5f + clearance))
                    continue;
                Vector2 candidate = chunkCenter + local;
                if (!validator.TryReserveRect(candidate, size, clearance, kind))
                    continue;
                found = candidate;
                return true;
            }
            found = default;
            return false;
        }

        private static bool InSafeCorridor(Vector2 local, float extent)
        {
            const float corridorHalfWidth = 1.25f;
            return Mathf.Abs(local.x) <= corridorHalfWidth + extent || Mathf.Abs(local.y) <= corridorHalfWidth + extent;
        }

        private GameObject Rent(string key, Func<GameObject> factory, string name, Vector2 position, float height, Vector3 scale)
        {
            GameObject go = pool.Rent(key, factory, chunkRoot);
            go.name = name;
            go.transform.position = Planar3D.ToWorld(position, height);
            go.transform.rotation = Quaternion.identity;
            go.transform.localScale = scale;
            return go;
        }

        private GameObject BuildGround()
        {
            var root = new GameObject("Ground");
            root.layer = SolariumLayers3D.Ground;
            root.AddComponent<BoxCollider>();
            root.AddComponent<ArenaGround3D>();
            if (environment.VisualsEnabled)
                LowPolyFactory3D.BuildSolid(root.transform, environment.Palette, VisualKind3D.Ground);
            return root;
        }

        private GameObject BuildTrigger(string name, float radius, VisualKind3D visual)
        {
            var root = new GameObject(name);
            root.layer = SolariumLayers3D.Gameplay;
            SphereCollider collider = root.AddComponent<SphereCollider>();
            collider.isTrigger = true;
            collider.radius = radius;
            if (environment.VisualsEnabled)
                LowPolyFactory3D.BuildResource(root.transform, environment.Palette, visual);
            return root;
        }

        private GameObject BuildSolid(string name, VisualKind3D visual)
        {
            var root = new GameObject(name);
            root.layer = SolariumLayers3D.Gameplay;
            root.AddComponent<BoxCollider>();
            root.AddComponent<ArenaSolid3D>();
            if (environment.VisualsEnabled)
                LowPolyFactory3D.BuildSolid(root.transform, environment.Palette, visual);
            return root;
        }

        private void CreateSolid(string name, string key, Vector2 position, Vector2 size, float height, VisualKind3D visual)
        {
            GameObject go = Rent(key, () => BuildSolid(name, visual), name, position, height * 0.5f,
                new Vector3(size.x, height, size.y));
            go.SetActive(true);
        }

        private GameObject BuildPatch(string name, VisualKind3D visual)
        {
            var root = new GameObject(name);
            root.layer = SolariumLayers3D.Gameplay;
            BoxCollider collider = root.AddComponent<BoxCollider>();
            collider.isTrigger = true;
            collider.size = Vector3.one;
            collider.center = new Vector3(0f, 0.3f, 0f);
            if (environment.VisualsEnabled)
                LowPolyFactory3D.BuildPatch(root.transform, environment.Palette, visual);
            return root;
        }

        private GameObject BuildEnemy(bool patroller)
        {
            var root = new GameObject(patroller ? "Patroller" : "Hunter");
            root.layer = SolariumLayers3D.Gameplay;
            CapsuleCollider collider = root.AddComponent<CapsuleCollider>();
            collider.radius = 0.42f;
            collider.height = 1.05f;
            root.AddComponent<Rigidbody>();
            root.AddComponent<EnemyController3D>();
            if (environment.VisualsEnabled)
                LowPolyFactory3D.BuildEnemy(root.transform, environment.Palette, patroller);
            return root;
        }

        private GameObject BuildDecoration(VisualKind3D visual)
        {
            var root = new GameObject(visual.ToString());
            root.layer = SolariumLayers3D.Visual;
            if (environment.VisualsEnabled)
                LowPolyFactory3D.BuildDecoration(root.transform, environment.Palette, visual);
            return root;
        }

        private float Next(float min, float max) => Mathf.Lerp(min, max, Next01());
        private float Next01() => (float)random.NextDouble();
        private static string Key(ChunkCoord3D coord, string id) => $"{coord.x}:{coord.y}:{id}";
    }
}
