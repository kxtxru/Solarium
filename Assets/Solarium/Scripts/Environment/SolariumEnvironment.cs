using System;
using System.Collections.Generic;
using UnityEngine;

namespace Solarium
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(ProceduralSpawner), typeof(DifficultyController), typeof(EpisodeStatistics))]
    public sealed class SolariumEnvironment : MonoBehaviour
    {
        private struct FoodRespawn
        {
            public Food food;
            public float remaining;
        }

        [SerializeField] private SolariumTrainingSettings settings;
        [SerializeField] private SolAgent agent;
        [Header("Seed")]
        [SerializeField] private int baseSeed = 12345;
        [SerializeField] private bool useFixedSeed;
        [SerializeField] private int fixedSeed = 12345;

        private readonly List<Food> foods = new();
        private readonly List<HealingZone> healingZones = new();
        private readonly List<EnemyController> enemies = new();
        private readonly List<SupplyShard> supplies = new();
        private readonly List<ShelterZone> shelters = new();
        private readonly List<FoodRespawn> foodRespawns = new();
        private readonly Dictionary<RewardComponent, float> rewardBreakdown = new();
        private ProceduralSpawner spawner;
        private DifficultyController difficulty;
        private EpisodeStatistics statistics;
        private bool episodeRunning;
        private bool endingEpisode;
        private int healingUsed;
        private int suppliesDeposited;
        private float damageTaken;
        private float energySpent;

        public SolariumTrainingSettings Settings => settings;
        public SolAgent Agent => agent;
        public EpisodeConfig CurrentConfig { get; private set; }
        public int EpisodeNumber { get; private set; }
        public int CurrentSeed { get; private set; }
        public float SurvivalTime { get; private set; }
        public int FoodCollected { get; private set; }
        public float DamageTaken => damageTaken;
        public string LastTerminationReason { get; private set; } = "none";
        public DifficultyController Difficulty => difficulty;
        public EpisodeStatistics Statistics => statistics;
        public IReadOnlyList<Food> Foods => foods;
        public IReadOnlyList<HealingZone> HealingZones => healingZones;
        public IReadOnlyList<EnemyController> Enemies => enemies;
        public IReadOnlyList<SupplyShard> Supplies => supplies;
        public IReadOnlyList<ShelterZone> Shelters => shelters;
        public int SuppliesDeposited => suppliesDeposited;
        public IReadOnlyDictionary<RewardComponent, float> RewardBreakdown => rewardBreakdown;

        public event Action<EpisodeRecord> EpisodeCompleted;

        private void Awake()
        {
            if (settings == null)
                settings = SolariumTrainingSettings.CreateRuntimeDefaults();
            spawner = GetComponent<ProceduralSpawner>();
            difficulty = GetComponent<DifficultyController>();
            statistics = GetComponent<EpisodeStatistics>();
            if (agent == null)
                agent = GetComponentInChildren<SolAgent>(true);
            spawner.Initialize(this);
            if (agent != null)
                agent.Bind(this, settings.rayCount);
        }

        public void Configure(
            SolariumTrainingSettings trainingSettings,
            SolAgent solAgent,
            int seed,
            bool fixedEpisodeSeed)
        {
            settings = trainingSettings;
            agent = solAgent;
            baseSeed = seed;
            fixedSeed = seed;
            useFixedSeed = fixedEpisodeSeed;
        }

        public void BeginEpisode()
        {
            if (agent == null)
            {
                Debug.LogError("Solarium environment has no SolAgent assigned.", this);
                return;
            }

            endingEpisode = false;
            episodeRunning = false;
            EpisodeNumber++;
            CurrentSeed = useFixedSeed
                ? fixedSeed
                : unchecked(baseSeed + EpisodeNumber * 7919);
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
            episodeRunning = true;
        }

        public void EndEpisode(string reason)
        {
            if (!episodeRunning || endingEpisode)
                return;

            endingEpisode = true;
            episodeRunning = false;
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
            EpisodeCompleted?.Invoke(record);
            agent.EndEpisode();
        }

        public void OnAgentFixedStep(float spent, bool sprinting, float stillTime)
        {
            if (!episodeRunning)
                return;

            SurvivalTime += Time.fixedDeltaTime;
            energySpent += spent;
            agent.AddTrackedReward(
                settings.survivalRewardPerSecond * Time.fixedDeltaTime,
                RewardComponent.Survival);

            if (sprinting)
            {
                agent.AddTrackedReward(
                    settings.sprintPenaltyPerSecond * Time.fixedDeltaTime,
                    RewardComponent.Sprint);
            }

            if (stillTime >= settings.stuckWindowSeconds)
            {
                agent.AddTrackedReward(
                    settings.stuckPenaltyPerSecond * Time.fixedDeltaTime,
                    RewardComponent.Stuck);
            }

            if (agent.IsInMud)
            {
                agent.AddTrackedReward(
                    settings.mudPenaltyPerSecond * Time.fixedDeltaTime,
                    RewardComponent.Mud);
            }

            TickFoodRespawns();
        }

        public void CollectFood(Food food)
        {
            if (!episodeRunning || food == null || !food.gameObject.activeSelf)
                return;

            float energy = food.IsGolden ? settings.goldenFoodEnergy : settings.normalFoodEnergy;
            agent.Vitals.AddEnergy(energy);
            FoodCollected++;
            agent.AddTrackedReward(
                food.IsGolden ? settings.goldenFoodReward : settings.foodReward,
                food.IsGolden ? RewardComponent.GoldenFood : RewardComponent.Food);
            food.gameObject.SetActive(false);
            foodRespawns.Add(new FoodRespawn
            {
                food = food,
                remaining = settings.foodRespawnSeconds * (food.IsGolden ? 2.5f : 1f)
            });
        }

        public bool UseHealing()
        {
            if (!episodeRunning)
                return false;

            float healed = agent.Vitals.Heal(settings.healingAmount);
            if (healed <= 0f)
                return false;

            healingUsed++;
            float normalized = healed / Mathf.Max(1f, agent.Vitals.maxHealth);
            agent.AddTrackedReward(settings.healingReward * normalized, RewardComponent.Healing);
            return true;
        }

        public void RegisterDamage(float amount) => damageTaken += Mathf.Max(0f, amount);

        public void Register(Food food) => foods.Add(food);
        public void Register(HealingZone healing) => healingZones.Add(healing);
        public void Register(EnemyController enemy) => enemies.Add(enemy);
        public void Register(SupplyShard supply) => supplies.Add(supply);
        public void Register(ShelterZone shelter) => shelters.Add(shelter);

        public bool TryInteract(SolAgent actor)
        {
            if (!episodeRunning || actor == null || actor != agent)
                return false;

            float range = settings.interactionRange;
            if (actor.IsCarryingSupply)
            {
                ShelterZone shelter = FindNearest(shelters, actor.transform.position, range);
                if (shelter == null || !shelter.Deposit(actor))
                    return false;

                suppliesDeposited++;
                actor.AddTrackedReward(settings.supplyDepositReward, RewardComponent.SupplyDeposit);
                return true;
            }

            ShelterZone reserve = FindNearest(shelters, actor.transform.position, range);
            if (reserve != null && reserve.TryUseReserve(actor))
                return true;

            SupplyShard supply = FindNearest(supplies, actor.transform.position, range);
            if (supply == null || !supply.TryPickUp(actor))
                return false;

            actor.AddTrackedReward(settings.supplyPickupReward, RewardComponent.SupplyPickup);
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
            foreach (EnemyController enemy in enemies)
            {
                if (enemy == null || !enemy.gameObject.activeInHierarchy)
                    continue;
                nearest = Mathf.Min(nearest, Vector2.Distance(position, enemy.transform.position));
            }
            return nearest;
        }

        public void RestartCurrentSeed()
        {
            fixedSeed = CurrentSeed;
            useFixedSeed = true;
            EndEpisode("manual_restart");
        }

        public void RestartWithSeed(int seed)
        {
            fixedSeed = seed;
            useFixedSeed = true;
            EndEpisode("manual_restart");
        }

        public void UseSequenceSeeds()
        {
            useFixedSeed = false;
            EndEpisode("manual_restart");
        }

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

        private static T FindNearest<T>(IReadOnlyList<T> candidates, Vector2 position, float range)
            where T : Component
        {
            T nearest = null;
            float best = range;
            foreach (T candidate in candidates)
            {
                if (candidate == null || !candidate.gameObject.activeInHierarchy)
                    continue;
                float distance = Vector2.Distance(position, candidate.transform.position);
                if (distance > best)
                    continue;
                nearest = candidate;
                best = distance;
            }
            return nearest;
        }

        private void OnDestroy()
        {
            spawner?.Cleanup();
            if (settings != null && settings.hideFlags != HideFlags.None)
                Destroy(settings);
        }
    }
}
