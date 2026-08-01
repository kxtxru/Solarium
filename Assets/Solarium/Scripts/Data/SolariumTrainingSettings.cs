using UnityEngine;

namespace Solarium
{
    [CreateAssetMenu(menuName = "Solarium/Training Settings", fileName = "SolariumTrainingSettings")]
    public sealed class SolariumTrainingSettings : ScriptableObject
    {
        [Header("Episode")]
        [Min(1)] public int rayCount = 12;
        [Min(1f)] public float spawnClearance = 1.25f;
        [Min(1)] public int spawnAttempts = 40;
        [Min(0.02f)] public float wallThickness = 0.5f;
        [Min(0.1f)] public float foodRespawnSeconds = 5f;

        [Header("Rewards")]
        [Tooltip("Tiny per-second pressure to survive. Keep below resource rewards.")]
        public float survivalRewardPerSecond = 0.001f;
        [Tooltip("Positive resource signal, kept small enough not to hide danger.")]
        public float foodReward = 0.25f;
        public float goldenFoodReward = 0.75f;
        [Tooltip("Applied per normalized health restored; zero at full health.")]
        public float healingReward = 1.25f;
        [Tooltip("Applied per normalized health lost.")]
        public float damagePenalty = -1f;
        [Tooltip("Strong enough that deliberately restarting is unattractive.")]
        public float deathPenalty = -3f;
        [Tooltip("Per second. Increase if the policy sprints constantly.")]
        public float sprintPenaltyPerSecond = -0.002f;
        [Tooltip("Per second after the agent has failed to change position.")]
        public float stuckPenaltyPerSecond = -0.02f;
        [Tooltip("Applied at a limited rate while pushing into a wall or obstacle.")]
        public float wallCollisionPenalty = -0.03f;
        [Min(0.1f)] public float wallPenaltyInterval = 0.5f;
        [Tooltip("Small per-second cost while crossing mud.")]
        public float mudPenaltyPerSecond = -0.003f;
        [Tooltip("Small signal for carrying a shard; depositing it is the meaningful reward.")]
        public float supplyPickupReward = 0.05f;
        public float supplyDepositReward = 0.45f;
        [Min(0.25f)] public float stuckWindowSeconds = 2.5f;
        [Min(0.01f)] public float stuckMinimumDisplacement = 0.3f;

        [Header("Entities")]
        public float normalFoodEnergy = 28f;
        public float goldenFoodEnergy = 55f;
        public float healingAmount = 32f;
        public float healingCooldown = 6f;
        public float enemyDamage = 16f;
        public float enemySpeed = 2.25f;
        public float enemyDetectionRange = 6f;
        public float enemyLoseRange = 8f;
        [Min(0f)] public float enemyRetreatSeconds = 1.4f;
        [Min(0f)] public float enemyKnockbackSpeed = 4.5f;
        [Min(0f)] public float enemyKnockbackSeconds = 0.2f;
        public float hazardDamagePerSecond = 5f;
        public float hazardEnergyPerSecond = 4f;
        [Range(0.1f, 1f)] public float mudSpeedMultiplier = 0.42f;
        public float shelterReserveEnergy = 38f;
        [Min(0.1f)] public float interactionRange = 0.9f;
        [Min(0.05f)] public float interactionCooldown = 0.3f;

        public static SolariumTrainingSettings CreateRuntimeDefaults()
        {
            return CreateInstance<SolariumTrainingSettings>();
        }
    }
}
