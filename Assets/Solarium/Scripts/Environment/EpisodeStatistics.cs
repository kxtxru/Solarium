using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Unity.MLAgents;
using UnityEngine;

namespace Solarium
{
    public enum RewardComponent
    {
        Survival,
        Food,
        GoldenFood,
        Healing,
        SupplyPickup,
        SupplyDeposit,
        Damage,
        Sprint,
        Stuck,
        WallCollision,
        Mud,
        Death
    }

    [Serializable]
    public struct EpisodeRecord
    {
        public int episode;
        public int seed;
        public float difficulty;
        public float survivalTime;
        public int foodCollected;
        public int healingUsed;
        public int suppliesDeposited;
        public float damageTaken;
        public float energySpent;
        public float episodeReward;
        public string terminationReason;

        public string ToCsv()
        {
            CultureInfo c = CultureInfo.InvariantCulture;
            string safeReason = (terminationReason ?? "unknown").Replace("\"", "\"\"");
            return string.Join(",",
                episode.ToString(c),
                seed.ToString(c),
                difficulty.ToString("0.###", c),
                survivalTime.ToString("0.###", c),
                foodCollected.ToString(c),
                healingUsed.ToString(c),
                suppliesDeposited.ToString(c),
                damageTaken.ToString("0.###", c),
                energySpent.ToString("0.###", c),
                episodeReward.ToString("0.#####", c),
                $"\"{safeReason}\"");
        }
    }

    public sealed class EpisodeStatistics : MonoBehaviour
    {
        private static readonly object FileLock = new();
        private static readonly string Header =
            "episode,seed,difficulty,survival_time,food_collected,healing_used,supplies_deposited,damage_taken,energy_spent,episode_reward,termination_reason";

        [SerializeField, Min(5)] private int movingAverageWindow = 50;
        [SerializeField] private bool writeCsv = true;
        private readonly Queue<EpisodeRecord> recent = new();
        private string csvPath;

        public IReadOnlyCollection<EpisodeRecord> Recent => recent;
        public float BestSurvival { get; private set; }
        public float AverageSurvival { get; private set; }
        public float AverageFood { get; private set; }
        public float RecentDeathRate { get; private set; }
        public int TotalDeaths { get; private set; }
        public string CsvPath => csvPath;

        private void Awake()
        {
            csvPath = Path.Combine(
                Application.persistentDataPath,
                "SolariumStats",
                "episodes.csv");
        }

        public void Record(EpisodeRecord record)
        {
            recent.Enqueue(record);
            while (recent.Count > Mathf.Max(5, movingAverageWindow))
                recent.Dequeue();

            float survival = 0f;
            float food = 0f;
            int deaths = 0;
            foreach (EpisodeRecord item in recent)
            {
                survival += item.survivalTime;
                food += item.foodCollected;
                if (IsDeath(item.terminationReason))
                    deaths++;
            }
            int count = Mathf.Max(1, recent.Count);
            AverageSurvival = survival / count;
            AverageFood = food / count;
            RecentDeathRate = (float)deaths / count;
            BestSurvival = Mathf.Max(BestSurvival, record.survivalTime);
            if (IsDeath(record.terminationReason))
                TotalDeaths++;

            if (Academy.IsInitialized)
            {
                StatsRecorder stats = Academy.Instance.StatsRecorder;
                stats.Add("Solarium/Survival Time", record.survivalTime);
                stats.Add("Solarium/Food Collected", record.foodCollected);
                stats.Add("Solarium/Healing Used", record.healingUsed);
                stats.Add("Solarium/Supplies Deposited", record.suppliesDeposited);
                stats.Add("Solarium/Damage Taken", record.damageTaken);
                stats.Add("Solarium/Death", IsDeath(record.terminationReason) ? 1f : 0f);
                stats.Add("Solarium/Lava Death", record.terminationReason == "lava" ? 1f : 0f);
            }

            if (writeCsv)
                AppendCsv(record);
        }

        private static bool IsDeath(string reason) =>
            reason == "health_depleted" || reason == "energy_depleted" || reason == "lava";

        private void AppendCsv(EpisodeRecord record)
        {
            lock (FileLock)
            {
                string directory = Path.GetDirectoryName(csvPath);
                if (!Directory.Exists(directory))
                    Directory.CreateDirectory(directory);
                bool needsHeader = !File.Exists(csvPath);
                using var writer = new StreamWriter(csvPath, true, new UTF8Encoding(false));
                if (needsHeader)
                    writer.WriteLine(Header);
                writer.WriteLine(record.ToCsv());
            }
        }
    }
}
