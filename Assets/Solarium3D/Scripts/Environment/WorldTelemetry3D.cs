using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

namespace Solarium.ThreeD
{
    [DisallowMultipleComponent]
    public sealed class WorldTelemetry3D : MonoBehaviour
    {
        private static readonly object FileLock = new();
        private const string Header =
            "episode,seed,survival_time,food_collected,rations_collected,rations_deposited,rations_consumed,rations_available,chunks_discovered,chunks_revisited,total_chunks,distance_from_origin,episode_reward,termination_reason";
        private string csvPath;

        public string CsvPath => csvPath;

        private void Awake()
        {
            csvPath = Path.Combine(
                Application.isBatchMode ? Application.temporaryCachePath : Application.persistentDataPath,
                "SolariumStats",
                "infinite-episodes.csv");
        }

        public void Record(SolariumEnvironment3D environment, EpisodeRecord episode)
        {
            if (environment == null || environment.InfiniteWorld == null || !environment.InfiniteWorld.IsInfinite)
                return;
            CultureInfo culture = CultureInfo.InvariantCulture;
            string safeReason = (episode.terminationReason ?? "unknown").Replace("\"", "\"\"");
            string row = string.Join(",",
                episode.episode.ToString(culture),
                episode.seed.ToString(culture),
                episode.survivalTime.ToString("0.###", culture),
                episode.foodCollected.ToString(culture),
                environment.RationsCollected.ToString(culture),
                environment.SuppliesDeposited.ToString(culture),
                environment.RationsConsumed.ToString(culture),
                environment.RationsAvailable.ToString(culture),
                environment.InfiniteWorld.EpisodeNewChunks.ToString(culture),
                environment.InfiniteWorld.EpisodeRevisitedChunks.ToString(culture),
                environment.ChunksDiscovered.ToString(culture),
                environment.DistanceFromWorldOrigin.ToString("0.###", culture),
                episode.episodeReward.ToString("0.#####", culture),
                $"\"{safeReason}\"");

            lock (FileLock)
            {
                string directory = Path.GetDirectoryName(csvPath);
                if (!Directory.Exists(directory))
                    Directory.CreateDirectory(directory);
                bool needsHeader = !File.Exists(csvPath);
                using var writer = new StreamWriter(csvPath, true, new UTF8Encoding(false));
                if (needsHeader)
                    writer.WriteLine(Header);
                writer.WriteLine(row);
            }
        }
    }
}
