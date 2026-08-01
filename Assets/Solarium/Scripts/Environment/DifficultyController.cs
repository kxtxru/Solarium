using Unity.MLAgents;
using UnityEngine;

namespace Solarium
{
    public sealed class DifficultyController : MonoBehaviour
    {
        [SerializeField] private bool useManualDifficulty;
        [SerializeField, Range(0f, 1f)] private float manualDifficulty;
        [SerializeField, Range(1f, 2f)] private float arenaSizeMultiplier = 1f;
        [SerializeField] private string environmentParameterName = "solarium_difficulty";

        public bool UseManualDifficulty
        {
            get => useManualDifficulty;
            set => useManualDifficulty = value;
        }

        public float ManualDifficulty
        {
            get => manualDifficulty;
            set => manualDifficulty = Mathf.Clamp01(value);
        }

        public float ArenaSizeMultiplier
        {
            get => arenaSizeMultiplier;
            set => arenaSizeMultiplier = Mathf.Clamp(value, 1f, 2f);
        }

        public float CurrentDifficulty
        {
            get
            {
                if (useManualDifficulty || !Academy.IsInitialized)
                    return Mathf.Clamp01(manualDifficulty);

                return Mathf.Clamp01(
                    Academy.Instance.EnvironmentParameters.GetWithDefault(
                        environmentParameterName, manualDifficulty));
            }
        }

        public EpisodeConfig Resolve()
        {
            float d = CurrentDifficulty;
            EpisodeConfig result = EpisodeConfig.SafeFallback(d);

            result.arenaSize = Vector2.Lerp(new Vector2(12f, 9f), new Vector2(22f, 16f), d)
                * arenaSizeMultiplier;
            result.foodCount = Mathf.RoundToInt(Mathf.Lerp(7f, 3f, d));
            result.goldenFoodCount = d >= 0.65f ? 1 : 0;
            result.obstacleCount = d < 0.2f ? 0 : Mathf.RoundToInt(Mathf.Lerp(1f, 7f, d));
            // Keep lethal pressure in every lesson. Otherwise the first policy
            // only learns resource collection in a harmless world.
            result.hunterCount = 1 + Mathf.RoundToInt(Mathf.Lerp(0f, 2f, d));
            result.patrollerCount = d < 0.55f ? 0 : Mathf.RoundToInt(Mathf.Lerp(1f, 2f, (d - 0.55f) / 0.45f));
            result.healingCount = d < 0.35f || d > 0.8f ? 1 : 2;
            result.hazardCount = d < 0.75f ? 0 : Mathf.RoundToInt(Mathf.Lerp(1f, 3f, (d - 0.75f) / 0.25f));
            result.mudCount = 1 + Mathf.RoundToInt(Mathf.Lerp(0f, 2f, d));
            result.lavaCount = 1 + Mathf.RoundToInt(Mathf.Lerp(0f, 2f, d));
            result.supplyCount = 1 + Mathf.RoundToInt(Mathf.Lerp(0f, 2f, d));
            result.shelterCount = 1;
            result.visionRange = Mathf.Lerp(10f, 6f, d);
            result.energyDrainMultiplier = Mathf.Lerp(0.65f, 1.25f, d);
            result.enemySpeedMultiplier = Mathf.Lerp(0.88f, 1.12f, d);
            result.enemyDamageMultiplier = Mathf.Lerp(0.65f, 1f, d);
            return result;
        }

        public static bool IsWithinLimits(EpisodeConfig config)
        {
            return config.difficulty is >= 0f and <= 1f
                && config.arenaSize.x >= 10f && config.arenaSize.y >= 8f
                && config.foodCount >= 1
                && config.hunterCount >= 1
                && config.mudCount >= 1
                && config.lavaCount >= 1
                && config.supplyCount >= 1
                && config.shelterCount >= 1
                && config.visionRange > 0f
                && config.energyDrainMultiplier > 0f
                && config.enemySpeedMultiplier > 0f
                && config.enemyDamageMultiplier > 0f;
        }
    }
}
