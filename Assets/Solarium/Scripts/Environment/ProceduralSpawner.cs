using System;
using System.Collections.Generic;
using UnityEngine;

namespace Solarium
{
    public sealed class ProceduralSpawner : MonoBehaviour
    {
        private readonly SpawnValidator validator = new();
        private readonly List<SpawnFootprint> layout = new();
        private SolariumEnvironment environment;
        private Transform episodeRoot;
        private System.Random random;

        public IReadOnlyList<SpawnFootprint> LastLayout => layout;

        public void Initialize(SolariumEnvironment owner) => environment = owner;

        public Vector2 Generate(EpisodeConfig config, int seed)
        {
            Cleanup();
            random = new System.Random(seed);
            layout.Clear();

            Vector2 center = transform.position;
            float margin = environment.Settings.wallThickness + 0.75f;
            validator.Reset(center, config.arenaSize, margin);
            episodeRoot = new GameObject($"Episode_{environment.EpisodeNumber}_{seed}").transform;
            episodeRoot.SetParent(transform, true);

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
                    environment.Foods[0].transform.position,
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
            Physics2D.SyncTransforms();
            return solPosition;
        }

        public bool TryRespawnFood(Food food)
        {
            if (food == null || random == null)
                return false;

            Vector2 center = transform.position;
            Vector2 half = environment.CurrentConfig.arenaSize * 0.5f - Vector2.one;
            for (int attempt = 0; attempt < environment.Settings.spawnAttempts; attempt++)
            {
                Vector2 candidate = new(
                    Mathf.Lerp(center.x - half.x, center.x + half.x, (float)random.NextDouble()),
                    Mathf.Lerp(center.y - half.y, center.y + half.y, (float)random.NextDouble()));
                Collider2D[] overlaps = Physics2D.OverlapCircleAll(candidate, 0.55f);
                bool blocked = false;
                foreach (Collider2D overlap in overlaps)
                {
                    if (overlap == null || overlap.gameObject == food.gameObject)
                        continue;
                    if (overlap.GetComponentInParent<SolariumEnvironment>() == environment)
                    {
                        blocked = true;
                        break;
                    }
                }
                if (blocked)
                    continue;
                food.transform.position = candidate;
                food.gameObject.SetActive(true);
                return true;
            }
            return false;
        }

        public void Cleanup()
        {
            if (episodeRoot == null)
                return;
            episodeRoot.gameObject.SetActive(false);
            if (Application.isPlaying)
                Destroy(episodeRoot.gameObject);
            else
                DestroyImmediate(episodeRoot.gameObject);
            episodeRoot = null;
        }

        private void GenerateObstacles(EpisodeConfig config, Vector2 center)
        {
            for (int i = 0; i < config.obstacleCount; i++)
            {
                Vector2 size = new(Next(1.2f, 3f), Next(1f, 2.5f));
                if (!TryFindRect(size, 0.5f, "obstacle", center, out Vector2 position))
                    continue;
                layout.Add(validator.Occupied[^1]);
                CreateSolid($"Obstacle_{i}", position, size, new Color(0.32f, 0.35f, 0.42f));
            }
        }

        private void GenerateFoods(EpisodeConfig config, Vector2 center)
        {
            int total = Mathf.Max(1, config.foodCount) + config.goldenFoodCount;
            for (int i = 0; i < total; i++)
            {
                bool golden = i >= config.foodCount;
                if (!TryFindCircle(golden ? 0.48f : 0.36f, 0.35f, golden ? "golden_food" : "food", center, out Vector2 position))
                    continue;
                layout.Add(validator.Occupied[^1]);
                Food food = CreateFood(i, position, golden);
                environment.Register(food);
            }

            if (environment.Foods.Count == 0)
            {
                Vector2 fallback = center + Vector2.right * 2f;
                Food food = CreateFood(0, fallback, false);
                environment.Register(food);
                layout.Add(new SpawnFootprint { center = fallback, halfExtents = Vector2.one * 0.5f, kind = "food" });
            }
        }

        private void GenerateHealing(EpisodeConfig config, Vector2 center)
        {
            for (int i = 0; i < config.healingCount; i++)
            {
                bool reserved = TryFindCircle(0.6f, 0.4f, "healing", center, out Vector2 position);
                if (!reserved)
                {
                    position = EmergencyHealingPosition(config, center, i);
                    Debug.LogWarning($"Solarium could not reserve healing {i}; using emergency position {position}.", this);
                }
                else
                {
                    layout.Add(validator.Occupied[^1]);
                }
                GameObject go = CreateBase($"Healing_{i}", position, Vector2.one * 1.3f, SimpleShape.Cross, new Color(0.1f, 0.8f, 1f), 3);
                var collider = go.AddComponent<CircleCollider2D>();
                collider.isTrigger = true;
                collider.radius = 0.55f;
                HealingZone zone = go.AddComponent<HealingZone>();
                zone.Initialize(environment);
                environment.Register(zone);
            }
        }

        private void GenerateShelters(EpisodeConfig config, Vector2 center)
        {
            for (int i = 0; i < config.shelterCount; i++)
            {
                bool reserved = TryFindCircle(0.9f, 0.8f, "shelter", center, out Vector2 position);
                if (!reserved)
                {
                    position = EmergencyHealingPosition(config, center, i);
                    Debug.LogWarning($"Solarium could not reserve shelter {i}; using emergency position {position}.", this);
                }
                else
                {
                    layout.Add(validator.Occupied[^1]);
                }

                GameObject go = CreateBase(
                    $"Shelter_{i}",
                    position,
                    Vector2.one * 1.6f,
                    SimpleShape.Diamond,
                    new Color(0.2f, 0.95f, 0.85f),
                    2);
                var collider = go.AddComponent<CircleCollider2D>();
                collider.isTrigger = true;
                collider.radius = 0.7f;
                ShelterZone shelter = go.AddComponent<ShelterZone>();
                shelter.Initialize(environment);
                environment.Register(shelter);
            }
        }

        private void GenerateSupplies(EpisodeConfig config, Vector2 center)
        {
            for (int i = 0; i < config.supplyCount; i++)
            {
                bool reserved = TryFindCircle(0.32f, 0.45f, "supply", center, out Vector2 position);
                if (!reserved)
                {
                    position = EmergencySupplyPosition(config, center, i);
                    Debug.LogWarning($"Solarium could not reserve supply {i}; using emergency position {position}.", this);
                }
                else
                {
                    layout.Add(validator.Occupied[^1]);
                }

                GameObject go = CreateBase(
                    $"Supply_{i}",
                    position,
                    Vector2.one * 0.65f,
                    SimpleShape.Diamond,
                    new Color(0.95f, 0.35f, 1f),
                    2);
                var collider = go.AddComponent<CircleCollider2D>();
                collider.isTrigger = true;
                collider.radius = 0.35f;
                SupplyShard supply = go.AddComponent<SupplyShard>();
                supply.Initialize(environment);
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
                bool reserved = TryFindCircle(0.55f, 0.75f, kind, center, out Vector2 position);
                if (!reserved)
                {
                    // An enemy must never be silently omitted. The normal reservation can
                    // fail in crowded procedural layouts, so use a visible emergency spawn.
                    position = EmergencyEnemyPosition(config, center, i);
                    Debug.LogWarning($"Solarium could not reserve {kind} {i}; using emergency position {position}.", this);
                }
                else
                {
                    layout.Add(validator.Occupied[^1]);
                }
                GameObject go = CreateBase(
                    $"{(patroller ? "Patroller" : "Hunter")}_{i}",
                    position,
                    Vector2.one,
                    SimpleShape.Triangle,
                    patroller ? new Color(1f, 0.45f, 0.15f) : new Color(0.92f, 0.12f, 0.18f),
                    2);
                var collider = go.AddComponent<CircleCollider2D>();
                collider.radius = 0.42f;
                var body = go.AddComponent<Rigidbody2D>();
                body.gravityScale = 0f;
                body.freezeRotation = true;
                EnemyController enemy = go.AddComponent<EnemyController>();
                enemy.Initialize(environment, patroller, position);
                environment.Register(enemy);
            }
        }

        private static Vector2 EmergencyEnemyPosition(EpisodeConfig config, Vector2 center, int index)
        {
            Vector2[] directions = { Vector2.right, Vector2.up, Vector2.left, Vector2.down };
            Vector2 half = config.arenaSize * 0.5f;
            float distance = Mathf.Max(1.5f, Mathf.Min(half.x, half.y) * 0.55f);
            return center + directions[index % directions.Length] * distance;
        }

        private static Vector2 EmergencyHealingPosition(EpisodeConfig config, Vector2 center, int index)
        {
            Vector2[] directions =
            {
                new Vector2(1f, 1f).normalized,
                new Vector2(-1f, 1f).normalized,
                new Vector2(-1f, -1f).normalized,
                new Vector2(1f, -1f).normalized
            };
            Vector2 half = config.arenaSize * 0.5f;
            float distance = Mathf.Max(1.5f, Mathf.Min(half.x, half.y) * 0.4f);
            return center + directions[index % directions.Length] * distance;
        }

        private static Vector2 EmergencySupplyPosition(EpisodeConfig config, Vector2 center, int index)
        {
            Vector2[] directions = { Vector2.right, Vector2.up, Vector2.left, Vector2.down };
            Vector2 half = config.arenaSize * 0.5f;
            float distance = Mathf.Max(1f, Mathf.Min(half.x, half.y) * 0.25f);
            return center + directions[index % directions.Length] * distance;
        }

        private void GenerateHazards(EpisodeConfig config, Vector2 center)
        {
            for (int i = 0; i < config.hazardCount; i++)
            {
                Vector2 size = new(Next(1.2f, 2.2f), Next(1.2f, 2.2f));
                if (!TryFindRect(size, 0.4f, "hazard", center, out Vector2 position))
                    continue;
                layout.Add(validator.Occupied[^1]);
                GameObject go = CreateBase($"Hazard_{i}", position, size, SimpleShape.Circle, new Color(0.58f, 0.16f, 0.72f, 0.72f), -1);
                var collider = go.AddComponent<BoxCollider2D>();
                collider.isTrigger = true;
                Hazard hazard = go.AddComponent<Hazard>();
                hazard.Initialize(environment);
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
                GameObject go = CreateBase(
                    $"Mud_{i}",
                    position,
                    size,
                    SimpleShape.Circle,
                    new Color(0.34f, 0.19f, 0.08f, 0.88f),
                    -2);
                var collider = go.AddComponent<CircleCollider2D>();
                collider.isTrigger = true;
                collider.radius = 0.5f;
                go.AddComponent<MudZone>().Initialize(environment);
            }

            for (int i = 0; i < config.lavaCount; i++)
            {
                Vector2 size = new(Next(1.1f, 1.8f), Next(1.1f, 1.8f));
                if (!TryFindRect(size, 0.55f, "lava", center, out Vector2 position))
                    continue;
                layout.Add(validator.Occupied[^1]);
                GameObject go = CreateBase(
                    $"Lava_{i}",
                    position,
                    size,
                    SimpleShape.Diamond,
                    new Color(1f, 0.2f, 0.03f, 0.95f),
                    -1);
                var collider = go.AddComponent<CircleCollider2D>();
                collider.isTrigger = true;
                collider.radius = 0.5f;
                go.AddComponent<LavaZone>().Initialize(environment);
            }
        }

        private Food CreateFood(int index, Vector2 position, bool golden)
        {
            GameObject go = CreateBase(
                $"{(golden ? "GoldenFood" : "Food")}_{index}",
                position,
                Vector2.one * (golden ? 0.9f : 0.65f),
                golden ? SimpleShape.Diamond : SimpleShape.Circle,
                golden ? new Color(1f, 0.76f, 0.05f) : new Color(0.2f, 0.9f, 0.35f),
                1);
            var collider = go.AddComponent<CircleCollider2D>();
            collider.isTrigger = true;
            collider.radius = 0.5f;
            Food food = go.AddComponent<Food>();
            food.Initialize(environment, golden);
            return food;
        }

        private void CreateBoundaries(Vector2 center, Vector2 size)
        {
            float thickness = environment.Settings.wallThickness;
            CreateSolid("Wall_Top", center + Vector2.up * size.y * 0.5f, new Vector2(size.x + thickness, thickness), Color.white);
            CreateSolid("Wall_Bottom", center + Vector2.down * size.y * 0.5f, new Vector2(size.x + thickness, thickness), Color.white);
            CreateSolid("Wall_Left", center + Vector2.left * size.x * 0.5f, new Vector2(thickness, size.y + thickness), Color.white);
            CreateSolid("Wall_Right", center + Vector2.right * size.x * 0.5f, new Vector2(thickness, size.y + thickness), Color.white);
        }

        private GameObject CreateSolid(string name, Vector2 position, Vector2 size, Color color)
        {
            GameObject go = CreateBase(name, position, size, SimpleShape.Square, color, 0);
            go.AddComponent<BoxCollider2D>();
            go.AddComponent<ArenaSolid>();
            return go;
        }

        private GameObject CreateBase(
            string name,
            Vector2 position,
            Vector2 scale,
            SimpleShape shape,
            Color color,
            int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(episodeRoot, true);
            go.transform.position = position;
            go.transform.localScale = new Vector3(scale.x, scale.y, 1f);
            go.AddComponent<SpriteRenderer>();
            go.AddComponent<SimpleShapeRenderer>().Configure(shape, color, order);
            return go;
        }

        private Vector2 FindAndReserveCircle(float radius, float clearance, string kind, Vector2 center)
        {
            return TryFindCircle(radius, clearance, kind, center, out Vector2 found) ? found : center;
        }

        private bool TryFindCircle(float radius, float clearance, string kind, Vector2 center, out Vector2 found)
        {
            for (int attempt = 0; attempt < environment.Settings.spawnAttempts; attempt++)
            {
                Vector2 candidate = NextPoint(center, environment.CurrentConfig.arenaSize, radius + clearance + 0.5f);
                if (validator.TryReserveCircle(candidate, radius, clearance, kind))
                {
                    found = candidate;
                    return true;
                }
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
                if (validator.TryReserveRect(candidate, size, clearance, kind))
                {
                    found = candidate;
                    return true;
                }
            }
            found = default;
            return false;
        }

        private Vector2 NextPoint(Vector2 center, Vector2 arenaSize, float margin)
        {
            Vector2 half = arenaSize * 0.5f - Vector2.one * margin;
            half.x = Mathf.Max(0.1f, half.x);
            half.y = Mathf.Max(0.1f, half.y);
            return new Vector2(
                center.x + Next(-half.x, half.x),
                center.y + Next(-half.y, half.y));
        }

        private float Next(float min, float max) =>
            Mathf.Lerp(min, max, (float)random.NextDouble());

        private void RemoveBlockingGeometry()
        {
            for (int i = episodeRoot.childCount - 1; i >= 0; i--)
            {
                Transform child = episodeRoot.GetChild(i);
                if (!child.name.StartsWith("Obstacle_", StringComparison.Ordinal)
                    && !child.name.StartsWith("Lava_", StringComparison.Ordinal))
                    continue;
                child.gameObject.SetActive(false);
                if (Application.isPlaying)
                    Destroy(child.gameObject);
                else
                    DestroyImmediate(child.gameObject);
            }
            layout.RemoveAll(item => item.kind == "obstacle" || item.kind == "lava");
        }
    }
}
