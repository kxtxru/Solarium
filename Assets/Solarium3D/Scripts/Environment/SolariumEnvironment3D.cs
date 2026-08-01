using System;
using System.Collections.Generic;
using UnityEngine;

namespace Solarium.ThreeD
{
    public enum FeedbackKind3D
    {
        Food,
        GoldenFood,
        Healing,
        SupplyPickup,
        SupplyDeposit,
        Damage,
        Death
    }

    [DisallowMultipleComponent]
    [RequireComponent(typeof(ProceduralSpawner3D), typeof(DifficultyController), typeof(EpisodeStatistics))]
    [RequireComponent(typeof(WorldObjectPool3D))]
    public sealed class SolariumEnvironment3D : MonoBehaviour
    {
        private struct FoodRespawn
        {
            public Food3D food;
            public float remaining;
        }

        [SerializeField] private SolariumTrainingSettings settings;
        [SerializeField] private SolariumPalette3D palette;
        [SerializeField] private SolAgent3D agent;
        [SerializeField] private bool visualsEnabled = true;
        [Header("Seed")]
        [SerializeField] private int baseSeed = 12345;
        [SerializeField] private bool useFixedSeed;
        [SerializeField] private int fixedSeed = 12345;

        private readonly List<Food3D> foods = new();
        private readonly List<HealingZone3D> healingZones = new();
        private readonly List<EnemyController3D> enemies = new();
        private readonly List<SupplyShard3D> supplies = new();
        private readonly List<ShelterZone3D> shelters = new();
        private readonly List<FoodRespawn> foodRespawns = new();
        private readonly Dictionary<RewardComponent, float> rewardBreakdown = new();
        private ProceduralSpawner3D spawner;
        private DifficultyController difficulty;
        private EpisodeStatistics statistics;
        private bool endingEpisode;
        private int healingUsed;
        private int suppliesDeposited;
        private float damageTaken;
        private float energySpent;

        public SolariumTrainingSettings Settings => settings;
        public SolariumPalette3D Palette => palette;
        public SolAgent3D Agent => agent;
        public bool VisualsEnabled => visualsEnabled;
        public EpisodeConfig CurrentConfig { get; private set; }
        public int EpisodeNumber { get; private set; }
        public int CurrentSeed { get; private set; }
        public float SurvivalTime { get; private set; }
        public bool IsEpisodeActive => EpisodeNumber > 0 && !endingEpisode;
        public int FoodCollected { get; private set; }
        public int SuppliesDeposited => suppliesDeposited;
        public float DamageTaken => damageTaken;
        public string LastTerminationReason { get; private set; } = "none";
        public DifficultyController Difficulty => difficulty;
        public EpisodeStatistics Statistics => statistics;
        public IReadOnlyList<Food3D> Foods => foods;
        public IReadOnlyList<HealingZone3D> HealingZones => healingZones;
        public IReadOnlyList<EnemyController3D> Enemies => enemies;
        public IReadOnlyList<SupplyShard3D> Supplies => supplies;
        public IReadOnlyList<ShelterZone3D> Shelters => shelters;
        public IReadOnlyDictionary<RewardComponent, float> RewardBreakdown => rewardBreakdown;

        public event Action<EpisodeRecord> EpisodeCompleted;
        public event Action EpisodeStarted;
        public event Action<Vector3, FeedbackKind3D> FeedbackRequested;

        private void Awake()
        {
            if (settings == null)
                settings = SolariumTrainingSettings.CreateRuntimeDefaults();
            spawner = GetComponent<ProceduralSpawner3D>();
            difficulty = GetComponent<DifficultyController>();
            statistics = GetComponent<EpisodeStatistics>();
            if (agent == null)
                agent = GetComponentInChildren<SolAgent3D>(true);
            spawner.Initialize(this, GetComponent<WorldObjectPool3D>());
            if (agent != null)
                agent.Bind(this, settings.rayCount);
        }

        public void Configure(
            SolariumTrainingSettings trainingSettings,
            SolariumPalette3D visualPalette,
            SolAgent3D solAgent,
            int seed,
            bool fixedEpisodeSeed,
            bool enableVisuals)
        {
            settings = trainingSettings;
            palette = visualPalette;
            agent = solAgent;
            baseSeed = seed;
            fixedSeed = seed;
            useFixedSeed = fixedEpisodeSeed;
            visualsEnabled = enableVisuals;
        }

        public void BeginEpisode()
        {
            if (agent == null)
            {
                Debug.LogError("Solarium 3D environment has no agent assigned.", this);
                return;
            }

            endingEpisode = false;
            EpisodeNumber++;
            CurrentSeed = useFixedSeed ? fixedSeed : unchecked(baseSeed + EpisodeNumber * 7919);
            CurrentConfig = difficulty.Resolve();

            foods.Clear();
            healingZones.Clear();
            enemies.Clear();
            supplies.Clear();
            shelters.Clear();
            foodRespawns.Clear();
            rewardBreakdown.Clear();
            SurvivalTime = 0f;
            FoodCollected = 0;
            healingUsed = 0;
            suppliesDeposited = 0;
            damageTaken = 0f;
            energySpent = 0f;

            Vector2 spawn = spawner.Generate(CurrentConfig, CurrentSeed);
            agent.PrepareForEpisode(spawn);
            agent.SetVisionRange(CurrentConfig.visionRange);
            EpisodeStarted?.Invoke();
        }

        public void EndEpisode(string reason)
        {
            if (!IsEpisodeActive)
                return;
            endingEpisode = true;
            LastTerminationReason = reason;
            var record = new EpisodeRecord
            {
                episode = EpisodeNumber,
                seed = CurrentSeed,
                difficulty = CurrentConfig.difficulty,
                survivalTime = SurvivalTime,
                foodCollected = FoodCollected,
                healingUsed = healingUsed,
                suppliesDeposited = suppliesDeposited,
                damageTaken = damageTaken,
                energySpent = energySpent,
                episodeReward = agent.GetCumulativeReward(),
                terminationReason = reason
            };
            statistics.Record(record);
            FeedbackRequested?.Invoke(agent.transform.position, FeedbackKind3D.Death);
            EpisodeCompleted?.Invoke(record);
            agent.EndEpisode();
        }

        public void OnAgentFixedStep(float spent, bool sprinting, float stillTime)
        {
            if (!IsEpisodeActive)
                return;
            SurvivalTime += Time.fixedDeltaTime;
            energySpent += spent;
            agent.AddTrackedReward(settings.survivalRewardPerSecond * Time.fixedDeltaTime, RewardComponent.Survival);
            if (sprinting)
                agent.AddTrackedReward(settings.sprintPenaltyPerSecond * Time.fixedDeltaTime, RewardComponent.Sprint);
            if (stillTime >= settings.stuckWindowSeconds)
                agent.AddTrackedReward(settings.stuckPenaltyPerSecond * Time.fixedDeltaTime, RewardComponent.Stuck);
            if (agent.IsInMud)
                agent.AddTrackedReward(settings.mudPenaltyPerSecond * Time.fixedDeltaTime, RewardComponent.Mud);
            TickFoodRespawns();
        }

        public void CollectFood(Food3D food)
        {
            if (!IsEpisodeActive || food == null || !food.gameObject.activeSelf)
                return;
            bool golden = food.IsGolden;
            agent.Vitals.AddEnergy(golden ? settings.goldenFoodEnergy : settings.normalFoodEnergy);
            FoodCollected++;
            agent.AddTrackedReward(golden ? settings.goldenFoodReward : settings.foodReward,
                golden ? RewardComponent.GoldenFood : RewardComponent.Food);
            FeedbackRequested?.Invoke(food.transform.position, golden ? FeedbackKind3D.GoldenFood : FeedbackKind3D.Food);
            food.gameObject.SetActive(false);
            foodRespawns.Add(new FoodRespawn
            {
                food = food,
                remaining = settings.foodRespawnSeconds * (golden ? 2.5f : 1f)
            });
        }

        public bool UseHealing()
        {
            if (!IsEpisodeActive)
                return false;
            float healed = agent.Vitals.Heal(settings.healingAmount);
            if (healed <= 0f)
                return false;
            healingUsed++;
            agent.AddTrackedReward(settings.healingReward * (healed / Mathf.Max(1f, agent.Vitals.maxHealth)), RewardComponent.Healing);
            FeedbackRequested?.Invoke(agent.transform.position, FeedbackKind3D.Healing);
            return true;
        }

        public void RegisterDamage(float amount)
        {
            damageTaken += Mathf.Max(0f, amount);
            FeedbackRequested?.Invoke(agent.transform.position, FeedbackKind3D.Damage);
        }

        public void Register(Food3D food) => foods.Add(food);
        public void Register(HealingZone3D healing) => healingZones.Add(healing);
        public void Register(EnemyController3D enemy) => enemies.Add(enemy);
        public void Register(SupplyShard3D supply) => supplies.Add(supply);
        public void Register(ShelterZone3D shelter) => shelters.Add(shelter);

        public bool TryInteract(SolAgent3D actor)
        {
            if (!IsEpisodeActive || actor == null || actor != agent)
                return false;
            float range = settings.interactionRange;
            Vector2 position = Planar3D.ToPlanar(actor.transform.position);
            if (actor.IsCarryingSupply)
            {
                ShelterZone3D shelter = FindNearest(shelters, position, range);
                if (shelter == null || !shelter.Deposit(actor))
                    return false;
                suppliesDeposited++;
                actor.AddTrackedReward(settings.supplyDepositReward, RewardComponent.SupplyDeposit);
                FeedbackRequested?.Invoke(shelter.transform.position, FeedbackKind3D.SupplyDeposit);
                return true;
            }

            ShelterZone3D reserve = FindNearest(shelters, position, range);
            if (reserve != null && reserve.TryUseReserve(actor))
                return true;
            SupplyShard3D supply = FindNearest(supplies, position, range);
            if (supply == null || !supply.TryPickUp(actor))
                return false;
            actor.AddTrackedReward(settings.supplyPickupReward, RewardComponent.SupplyPickup);
            FeedbackRequested?.Invoke(actor.transform.position, FeedbackKind3D.SupplyPickup);
            return true;
        }

        public void RecordReward(RewardComponent component, float amount)
        {
            rewardBreakdown.TryGetValue(component, out float current);
            rewardBreakdown[component] = current + amount;
        }

        public float GetNearestEnemyDistance(Vector2 position)
        {
            float nearest = float.PositiveInfinity;
            foreach (EnemyController3D enemy in enemies)
            {
                if (enemy != null && enemy.gameObject.activeInHierarchy)
                    nearest = Mathf.Min(nearest, Vector2.Distance(position, Planar3D.ToPlanar(enemy.transform.position)));
            }
            return nearest;
        }

        public void RestartCurrentSeed() { fixedSeed = CurrentSeed; useFixedSeed = true; EndEpisode("manual_restart"); }
        public void RestartWithSeed(int seed) { fixedSeed = seed; useFixedSeed = true; EndEpisode("manual_restart"); }
        public void UseSequenceSeeds() { useFixedSeed = false; EndEpisode("manual_restart"); }

        private void TickFoodRespawns()
        {
            for (int i = foodRespawns.Count - 1; i >= 0; i--)
            {
                FoodRespawn respawn = foodRespawns[i];
                respawn.remaining -= Time.fixedDeltaTime;
                if (respawn.remaining > 0f)
                {
                    foodRespawns[i] = respawn;
                    continue;
                }
                if (!spawner.TryRespawnFood(respawn.food))
                {
                    respawn.remaining = 0.5f;
                    foodRespawns[i] = respawn;
                    continue;
                }
                foodRespawns.RemoveAt(i);
            }
        }

        private static T FindNearest<T>(IReadOnlyList<T> candidates, Vector2 position, float range) where T : Component
        {
            T nearest = null;
            float best = range;
            foreach (T candidate in candidates)
            {
                if (candidate == null || !candidate.gameObject.activeInHierarchy)
                    continue;
                float distance = Vector2.Distance(position, Planar3D.ToPlanar(candidate.transform.position));
                if (distance > best)
                    continue;
                nearest = candidate;
                best = distance;
            }
            return nearest;
        }

        private void OnDestroy()
        {
            if (settings != null && settings.hideFlags != HideFlags.None)
                Destroy(settings);
        }
    }
}
