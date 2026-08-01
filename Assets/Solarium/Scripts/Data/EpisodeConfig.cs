using System;
using UnityEngine;

namespace Solarium
{
    [Serializable]
    public struct EpisodeConfig
    {
        [Range(0f, 1f)] public float difficulty;
        public Vector2 arenaSize;
        public int foodCount;
        public int goldenFoodCount;
        public int obstacleCount;
        public int hunterCount;
        public int patrollerCount;
        public int healingCount;
        public int hazardCount;
        public int mudCount;
        public int lavaCount;
        public int supplyCount;
        public int shelterCount;
        public float visionRange;
        public float energyDrainMultiplier;
        public float enemySpeedMultiplier;
        public float enemyDamageMultiplier;

        public static EpisodeConfig SafeFallback(float difficulty)
        {
            return new EpisodeConfig
            {
                difficulty = Mathf.Clamp01(difficulty),
                arenaSize = new Vector2(14f, 10f),
                foodCount = 5,
                goldenFoodCount = 0,
                obstacleCount = 0,
                hunterCount = 1,
                patrollerCount = 0,
                healingCount = 1,
                hazardCount = 0,
                mudCount = 1,
                lavaCount = 1,
                supplyCount = 1,
                shelterCount = 1,
                visionRange = 8f,
                energyDrainMultiplier = 0.7f,
                enemySpeedMultiplier = 0.88f,
                enemyDamageMultiplier = 0.65f
            };
        }
    }
}
