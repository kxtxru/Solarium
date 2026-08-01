using System;
using System.Collections.Generic;
using UnityEngine;

namespace Solarium.ThreeD
{
    public sealed class ProceduralSpawner3D : MonoBehaviour
    {
        private readonly SpawnValidator validator = new();
        private readonly List<SpawnFootprint> layout = new();
        private readonly Collider[] overlaps = new Collider[64];
        private SolariumEnvironment3D environment;
        private WorldObjectPool3D pool;
        private Transform episodeRoot;
        private System.Random random;

        public IReadOnlyList<SpawnFootprint> LastLayout => layout;
        public int PoolCreatedCount => pool == null ? 0 : pool.CreatedCount;

        public void Initialize(SolariumEnvironment3D owner, WorldObjectPool3D objectPool)
        {
            environment = owner;
            pool = objectPool;
            EnsureEpisodeRoot();
        }

        public Vector2 Generate(EpisodeConfig config, int seed)
        {
            Cleanup();
            EnsureEpisodeRoot();
            episodeRoot.name = $"Episode_{environment.EpisodeNumber}_{seed}";
            random = new System.Random(seed);
            layout.Clear();

            Vector2 center = Planar3D.ToPlanar(transform.position);
            float margin = environment.Settings.wallThickness + 0.75f;
            validator.Reset(center, config.arenaSize, margin);

            CreateGround(center, config.arenaSize);
            CreateBoundaries(center, config.arenaSize);
            Vector2 solPosition = FindAndReserveCircle(0.55f, environment.Settings.spawnClearance, "sol", center);
            if (validator.Occupied.Count > 0)
                layout.Add(validator.Occupied[^1]);

            GenerateObstacles(config, center);
            GenerateTerrain(config, center);
            GenerateShelters(config, center);
            GenerateFoods(config, center);

            if (environment.Foods.Count == 0
                || !SpawnValidator.HasGridPath(
                    solPosition,
                    Planar3D.ToPlanar(environment.Foods[0].transform.position),
                    center,
                    config.arenaSize,
                    layout))
            {
                RemoveBlockingGeometry();
            }

            GenerateHealing(config, center);
            GenerateSupplies(config, center);
            GenerateEnemies(config, center);
            GenerateHazards(config, center);
            GenerateDecorations(config, center);
            Physics.SyncTransforms();
            return solPosition;
        }

        public bool TryRespawnFood(Food3D food)
        {
            if (food == null || random == null)
                return false;
            Vector2 center = Planar3D.ToPlanar(transform.position);
            Vector2 half = environment.CurrentConfig.arenaSize * 0.5f - Vector2.one;
            for (int attempt = 0; attempt < environment.Settings.spawnAttempts; attempt++)
            {
                Vector2 candidate = new(
                    Mathf.Lerp(center.x - half.x, center.x + half.x, (float)random.NextDouble()),
                    Mathf.Lerp(center.y - half.y, center.y + half.y, (float)random.NextDouble()));
                if (TryPlaceRespawn(food, candidate))
                    return true;
            }

            const float cell = 1.1f;
            int width = Mathf.Max(1, Mathf.FloorToInt(half.x * 2f / cell));
            int height = Mathf.Max(1, Mathf.FloorToInt(half.y * 2f / cell));
            int total = width * height;
            int start = Math.Abs(environment.CurrentSeed + environment.FoodCollected * 31) % total;
            for (int offset = 0; offset < total; offset++)
            {
                int index = (start + offset) % total;
                int x = index % width;
                int y = index / width;
                Vector2 candidate = new(
                    center.x - half.x + (x + 0.5f) * cell,
                    center.y - half.y + (y + 0.5f) * cell);
                if (TryPlaceRespawn(food, candidate))
                    return true;
            }
            return false;
        }

        public void Cleanup()
        {
            if (episodeRoot != null && pool != null)
                pool.ReturnEpisodeChildren(episodeRoot);
        }

        private bool TryPlaceRespawn(Food3D food, Vector2 candidate)
        {
            Vector3 world = Planar3D.ToWorld(candidate, 0.48f);
            int count = Physics.OverlapSphereNonAlloc(world, 0.5f, overlaps, ~0, QueryTriggerInteraction.Collide);
            for (int i = 0; i < count; i++)
            {
                Collider overlap = overlaps[i];
                if (overlap == null
                    || overlap.gameObject == food.gameObject
                    || overlap.GetComponentInParent<ArenaGround3D>() != null)
                    continue;
                if (overlap.GetComponentInParent<SolariumEnvironment3D>() == environment)
                    return false;
            }
            food.transform.position = world;
            food.gameObject.SetActive(true);
            return true;
        }

        private void GenerateObstacles(EpisodeConfig config, Vector2 center)
        {
            for (int i = 0; i < config.obstacleCount; i++)
            {
                Vector2 size = new(Next(1.2f, 3f), Next(1f, 2.5f));
                if (!TryFindRect(size, 0.5f, "obstacle", center, out Vector2 position))
                    continue;
                layout.Add(validator.Occupied[^1]);
                CreateSolid($"Obstacle_{i}", "obstacle", position, size, 1.1f, VisualKind3D.Obstacle);
            }
        }

        private void GenerateFoods(EpisodeConfig config, Vector2 center)
        {
            int greenCount = Mathf.Max(1, config.foodCount);
            int total = greenCount + config.goldenFoodCount;
            for (int i = 0; i < total; i++)
            {
                bool golden = i >= greenCount;
                float radius = golden ? 0.48f : 0.36f;
                if (!TryFindCircle(radius, 0.35f, golden ? "golden_food" : "food", center, out Vector2 position))
                    position = FindEmergencyFreeCell(radius, 0.35f, golden ? "golden_food" : "food", center, i);
                layout.Add(validator.Occupied[^1]);
                Food3D food = CreateFood(i, position, golden);
                environment.Register(food);
            }
        }

        private void GenerateHealing(EpisodeConfig config, Vector2 center)
        {
            for (int i = 0; i < config.healingCount; i++)
            {
                if (!TryFindCircle(0.6f, 0.4f, "healing", center, out Vector2 position))
                    position = FindEmergencyFreeCell(0.6f, 0.4f, "healing", center, i);
                layout.Add(validator.Occupied[^1]);
                GameObject go = RentObject("healing", () => BuildTrigger("Healing", 0.58f, VisualKind3D.Healing),
                    $"Healing_{i}", position, 0.48f, Vector3.one);
                HealingZone3D zone = go.GetComponent<HealingZone3D>() ?? go.AddComponent<HealingZone3D>();
                zone.Initialize(environment);
                go.SetActive(true);
                environment.Register(zone);
            }
        }

        private void GenerateShelters(EpisodeConfig config, Vector2 center)
        {
            for (int i = 0; i < config.shelterCount; i++)
            {
                if (!TryFindCircle(0.9f, 0.8f, "shelter", center, out Vector2 position))
                    position = FindEmergencyFreeCell(0.9f, 0.8f, "shelter", center, i);
                layout.Add(validator.Occupied[^1]);
                GameObject go = RentObject("shelter", () => BuildTrigger("Shelter", 0.8f, VisualKind3D.Shelter),
                    $"Shelter_{i}", position, 0.55f, Vector3.one);
                ShelterZone3D shelter = go.GetComponent<ShelterZone3D>() ?? go.AddComponent<ShelterZone3D>();
                shelter.Initialize(environment);
                go.SetActive(true);
                environment.Register(shelter);
            }
        }

        private void GenerateSupplies(EpisodeConfig config, Vector2 center)
        {
            for (int i = 0; i < config.supplyCount; i++)
            {
                if (!TryFindCircle(0.32f, 0.45f, "supply", center, out Vector2 position))
                    position = FindEmergencyFreeCell(0.32f, 0.45f, "supply", center, i);
                layout.Add(validator.Occupied[^1]);
                GameObject go = RentObject("supply", () => BuildTrigger("Supply", 0.35f, VisualKind3D.Supply),
                    $"Supply_{i}", position, 0.42f, Vector3.one);
                SupplyShard3D supply = go.GetComponent<SupplyShard3D>() ?? go.AddComponent<SupplyShard3D>();
                supply.Initialize(environment);
                go.SetActive(true);
                environment.Register(supply);
            }
        }

        private void GenerateEnemies(EpisodeConfig config, Vector2 center)
        {
            int total = config.hunterCount + config.patrollerCount;
            for (int i = 0; i < total; i++)
            {
                bool patroller = i >= config.hunterCount;
                string kind = patroller ? "patroller" : "hunter";
                if (!TryFindCircle(0.55f, 0.75f, kind, center, out Vector2 position))
                    position = FindEmergencyFreeCell(0.55f, 0.75f, kind, center, i);
                layout.Add(validator.Occupied[^1]);
                GameObject go = RentObject(kind, () => BuildEnemy(patroller),
                    $"{(patroller ? "Patroller" : "Hunter")}_{i}", position, 0.55f, Vector3.one);
                EnemyController3D enemy = go.GetComponent<EnemyController3D>();
                enemy.Initialize(environment, patroller, position);
                go.SetActive(true);
                environment.Register(enemy);
            }
        }

        private void GenerateHazards(EpisodeConfig config, Vector2 center)
        {
            for (int i = 0; i < config.hazardCount; i++)
            {
                Vector2 size = new(Next(1.2f, 2.2f), Next(1.2f, 2.2f));
                if (!TryFindRect(size, 0.4f, "hazard", center, out Vector2 position))
                    continue;
                layout.Add(validator.Occupied[^1]);
                GameObject go = RentObject("hazard", () => BuildPatch("Hazard", VisualKind3D.Hazard),
                    $"Hazard_{i}", position, 0.08f, new Vector3(size.x, 1f, size.y));
                Hazard3D hazard = go.GetComponent<Hazard3D>() ?? go.AddComponent<Hazard3D>();
                hazard.Initialize(environment);
                go.SetActive(true);
            }
        }

        private void GenerateTerrain(EpisodeConfig config, Vector2 center)
        {
            for (int i = 0; i < config.mudCount; i++)
            {
                Vector2 size = new(Next(1.5f, 2.8f), Next(1.3f, 2.4f));
                if (!TryFindRect(size, 0.35f, "mud", center, out Vector2 position))
                    continue;
                layout.Add(validator.Occupied[^1]);
                GameObject go = RentObject("mud", () => BuildPatch("Mud", VisualKind3D.Mud),
                    $"Mud_{i}", position, 0.06f, new Vector3(size.x, 1f, size.y));
                MudZone3D mud = go.GetComponent<MudZone3D>() ?? go.AddComponent<MudZone3D>();
                mud.Initialize(environment);
                go.SetActive(true);
            }

            for (int i = 0; i < config.lavaCount; i++)
            {
                Vector2 size = new(Next(1.1f, 1.8f), Next(1.1f, 1.8f));
                if (!TryFindRect(size, 0.55f, "lava", center, out Vector2 position))
                    continue;
                layout.Add(validator.Occupied[^1]);
                GameObject go = RentObject("lava", () => BuildPatch("Lava", VisualKind3D.Lava),
                    $"Lava_{i}", position, 0.09f, new Vector3(size.x, 1f, size.y));
                LavaZone3D lava = go.GetComponent<LavaZone3D>() ?? go.AddComponent<LavaZone3D>();
                lava.Initialize(environment);
                go.SetActive(true);
            }
        }

        private void GenerateDecorations(EpisodeConfig config, Vector2 center)
        {
            if (!environment.VisualsEnabled)
                return;
            int count = Mathf.RoundToInt(Mathf.Lerp(18f, 34f, config.difficulty));
            for (int i = 0; i < count; i++)
            {
                float radius = i % 5 == 0 ? 0.28f : 0.16f;
                if (!TryFindCircle(radius, 0.08f, "decoration", center, out Vector2 position))
                    continue;
                VisualKind3D kind = i % 5 == 0 ? VisualKind3D.Stone : VisualKind3D.Foliage;
                string key = kind == VisualKind3D.Stone ? "decor_stone" : "decor_foliage";
                GameObject go = RentObject(key, () => BuildDecoration(kind), $"Decoration_{i}",
                    position, kind == VisualKind3D.Stone ? 0.2f : 0.3f,
                    Vector3.one * (kind == VisualKind3D.Stone ? Next(0.35f, 0.7f) : Next(0.3f, 0.75f)));
                go.transform.rotation = Quaternion.Euler(0f, Next(0f, 360f), 0f);
                go.SetActive(true);
            }
        }

        private Food3D CreateFood(int index, Vector2 position, bool golden)
        {
            string key = golden ? "golden_food" : "food";
            VisualKind3D kind = golden ? VisualKind3D.GoldenFood : VisualKind3D.Food;
            GameObject go = RentObject(key, () => BuildTrigger(golden ? "Golden Food" : "Food", 0.42f, kind),
                $"{(golden ? "GoldenFood" : "Food")}_{index}", position, 0.48f, Vector3.one);
            Food3D food = go.GetComponent<Food3D>() ?? go.AddComponent<Food3D>();
            food.Initialize(environment, golden);
            go.SetActive(true);
            return food;
        }

        private void CreateGround(Vector2 center, Vector2 size)
        {
            GameObject go = RentObject("ground", BuildGround, "Ground", center, -0.16f,
                new Vector3(size.x + 2f, 0.3f, size.y + 2f));
            go.SetActive(true);
        }

        private void CreateBoundaries(Vector2 center, Vector2 size)
        {
            float t = Mathf.Max(0.35f, environment.Settings.wallThickness);
            CreateSolid("Wall_Top", "wall", center + Vector2.up * size.y * 0.5f,
                new Vector2(size.x + t, t), 0.8f, VisualKind3D.Wall);
            CreateSolid("Wall_Bottom", "wall", center + Vector2.down * size.y * 0.5f,
                new Vector2(size.x + t, t), 0.8f, VisualKind3D.Wall);
            CreateSolid("Wall_Left", "wall", center + Vector2.left * size.x * 0.5f,
                new Vector2(t, size.y + t), 0.8f, VisualKind3D.Wall);
            CreateSolid("Wall_Right", "wall", center + Vector2.right * size.x * 0.5f,
                new Vector2(t, size.y + t), 0.8f, VisualKind3D.Wall);
        }

        private void CreateSolid(string name, string key, Vector2 position, Vector2 size, float height, VisualKind3D kind)
        {
            GameObject go = RentObject(key, () => BuildSolid(name, kind), name, position, height * 0.5f,
                new Vector3(size.x, height, size.y));
            go.SetActive(true);
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

        private GameObject BuildSolid(string name, VisualKind3D kind)
        {
            var root = new GameObject(name);
            root.layer = SolariumLayers3D.Gameplay;
            root.AddComponent<BoxCollider>();
            root.AddComponent<ArenaSolid3D>();
            if (environment.VisualsEnabled)
                LowPolyFactory3D.BuildSolid(root.transform, environment.Palette, kind);
            return root;
        }

        private GameObject BuildTrigger(string name, float radius, VisualKind3D kind)
        {
            var root = new GameObject(name);
            root.layer = SolariumLayers3D.Gameplay;
            var collider = root.AddComponent<SphereCollider>();
            collider.isTrigger = true;
            collider.radius = radius;
            if (environment.VisualsEnabled)
                LowPolyFactory3D.BuildResource(root.transform, environment.Palette, kind);
            return root;
        }

        private GameObject BuildPatch(string name, VisualKind3D kind)
        {
            var root = new GameObject(name);
            root.layer = SolariumLayers3D.Gameplay;
            var collider = root.AddComponent<BoxCollider>();
            collider.isTrigger = true;
            collider.size = new Vector3(1f, 1f, 1f);
            collider.center = new Vector3(0f, 0.3f, 0f);
            if (environment.VisualsEnabled)
                LowPolyFactory3D.BuildPatch(root.transform, environment.Palette, kind);
            return root;
        }

        private GameObject BuildEnemy(bool patroller)
        {
            var root = new GameObject(patroller ? "Patroller" : "Hunter");
            root.layer = SolariumLayers3D.Gameplay;
            var collider = root.AddComponent<CapsuleCollider>();
            collider.radius = 0.42f;
            collider.height = 1.05f;
            root.AddComponent<Rigidbody>();
            root.AddComponent<EnemyController3D>();
            if (environment.VisualsEnabled)
                LowPolyFactory3D.BuildEnemy(root.transform, environment.Palette, patroller);
            return root;
        }

        private GameObject BuildDecoration(VisualKind3D kind)
        {
            var root = new GameObject(kind.ToString());
            root.layer = SolariumLayers3D.Visual;
            if (environment.VisualsEnabled)
                LowPolyFactory3D.BuildDecoration(root.transform, environment.Palette, kind);
            return root;
        }

        private GameObject RentObject(
            string key,
            Func<GameObject> factory,
            string name,
            Vector2 position,
            float height,
            Vector3 scale)
        {
            GameObject go = pool.Rent(key, factory, episodeRoot);
            go.name = name;
            go.transform.position = Planar3D.ToWorld(position, height);
            go.transform.rotation = Quaternion.identity;
            go.transform.localScale = scale;
            return go;
        }

        private Vector2 FindAndReserveCircle(float radius, float clearance, string kind, Vector2 center) =>
            TryFindCircle(radius, clearance, kind, center, out Vector2 found)
                ? found
                : FindEmergencyFreeCell(radius, clearance, kind, center, 0);

        private bool TryFindCircle(float radius, float clearance, string kind, Vector2 center, out Vector2 found)
        {
            for (int attempt = 0; attempt < environment.Settings.spawnAttempts; attempt++)
            {
                Vector2 candidate = NextPoint(center, environment.CurrentConfig.arenaSize, radius + clearance + 0.5f);
                if (!validator.TryReserveCircle(candidate, radius, clearance, kind))
                    continue;
                found = candidate;
                return true;
            }
            found = default;
            return false;
        }

        private bool TryFindRect(Vector2 size, float clearance, string kind, Vector2 center, out Vector2 found)
        {
            float margin = Mathf.Max(size.x, size.y) * 0.5f + clearance + 0.5f;
            for (int attempt = 0; attempt < environment.Settings.spawnAttempts; attempt++)
            {
                Vector2 candidate = NextPoint(center, environment.CurrentConfig.arenaSize, margin);
                if (!validator.TryReserveRect(candidate, size, clearance, kind))
                    continue;
                found = candidate;
                return true;
            }
            found = default;
            return false;
        }

        private Vector2 FindEmergencyFreeCell(float radius, float clearance, string kind, Vector2 center, int offset)
        {
            Vector2 arena = environment.CurrentConfig.arenaSize;
            const float cell = 1f;
            int width = Mathf.Max(1, Mathf.FloorToInt((arena.x - 2f) / cell));
            int height = Mathf.Max(1, Mathf.FloorToInt((arena.y - 2f) / cell));
            int total = width * height;
            int start = Math.Abs(environment.CurrentSeed + offset * 97) % total;
            Vector2 min = center - new Vector2(width, height) * cell * 0.5f;
            for (int i = 0; i < total; i++)
            {
                int index = (start + i) % total;
                Vector2 candidate = min + new Vector2(index % width + 0.5f, index / width + 0.5f) * cell;
                if (validator.TryReserveCircle(candidate, radius, clearance, kind))
                    return candidate;
            }
            Vector2 fallback = center + new Vector2((offset % 3) - 1f, (offset / 3 % 3) - 1f);
            validator.TryReserveCircle(fallback, Mathf.Min(radius, 0.2f), 0f, kind);
            return fallback;
        }

        private Vector2 NextPoint(Vector2 center, Vector2 arenaSize, float margin)
        {
            Vector2 half = arenaSize * 0.5f - Vector2.one * margin;
            half.x = Mathf.Max(0.1f, half.x);
            half.y = Mathf.Max(0.1f, half.y);
            return new Vector2(center.x + Next(-half.x, half.x), center.y + Next(-half.y, half.y));
        }

        private float Next(float min, float max) => Mathf.Lerp(min, max, (float)random.NextDouble());

        private void RemoveBlockingGeometry()
        {
            for (int i = episodeRoot.childCount - 1; i >= 0; i--)
            {
                Transform child = episodeRoot.GetChild(i);
                if (child.GetComponent<ArenaSolid3D>() == null && child.GetComponent<LavaZone3D>() == null)
                    continue;
                if (child.name.StartsWith("Wall_", StringComparison.Ordinal))
                    continue;
                child.gameObject.SetActive(false);
            }
            layout.RemoveAll(item => item.kind == "obstacle" || item.kind == "lava");
        }

        private void EnsureEpisodeRoot()
        {
            if (episodeRoot != null)
                return;
            var root = new GameObject("Episode World");
            root.transform.SetParent(transform, true);
            episodeRoot = root.transform;
        }
    }
}
