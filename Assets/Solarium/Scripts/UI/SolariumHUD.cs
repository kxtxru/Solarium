using System.Globalization;
using Unity.MLAgents.Policies;
using UnityEngine;

namespace Solarium
{
    public sealed class SolariumHUD : MonoBehaviour
    {
        [SerializeField] private SolariumEnvironment environment;
        [SerializeField] private bool showControls = true;
        private string seedText = "12345";
        private GUIStyle titleStyle;
        private GUIStyle labelStyle;

        public void Initialize(SolariumEnvironment owner) => environment = owner;

        private void OnGUI()
        {
            if (environment == null || environment.Agent == null)
                return;
            EnsureStyles();

            GUILayout.BeginArea(new Rect(14f, 14f, 310f, Screen.height - 28f), GUI.skin.box);
            GUILayout.Label("SOLARIUM", titleStyle);
            SolAgent agent = environment.Agent;
            SolVitals vitals = agent.Vitals;
            Label("Episodio", environment.EpisodeNumber.ToString(CultureInfo.InvariantCulture));
            Label("Seed", environment.CurrentSeed.ToString(CultureInfo.InvariantCulture));
            Label("Supervivencia", $"{environment.SurvivalTime:0.0} s");
            Label("Récord", $"{environment.Statistics.BestSurvival:0.0} s");
            DrawBar("Vida", vitals.NormalizedHealth, new Color(0.95f, 0.25f, 0.3f));
            DrawBar("Energía", vitals.NormalizedEnergy, new Color(0.25f, 0.9f, 0.4f));
            Label("Comida", environment.FoodCollected.ToString(CultureInfo.InvariantCulture));
            Label("Carga", agent.IsCarryingSupply ? "Fragmento" : "Vacía");
            Label("Reserva", environment.SuppliesDeposited.ToString(CultureInfo.InvariantCulture));
            int expectedEnemies = environment.CurrentConfig.hunterCount + environment.CurrentConfig.patrollerCount;
            Label("Enemigos", $"{environment.Enemies.Count} / {expectedEnemies}");
            Label("Daño", environment.DamageTaken.ToString("0.0", CultureInfo.InvariantCulture));
            Label("Muertes", environment.Statistics.TotalDeaths.ToString(CultureInfo.InvariantCulture));
            Label("Fin anterior", environment.LastTerminationReason);
            Label("Terreno", agent.IsInMud ? "Barro (lento)" : "Normal");
            Label("Recompensa", agent.GetCumulativeReward().ToString("0.000", CultureInfo.InvariantCulture));
            Label("Estado", agent.DebugState);

            GUILayout.Space(6f);
            GUILayout.Label($"Dificultad: {environment.Difficulty.CurrentDifficulty:0.00}", labelStyle);
            float selected = GUILayout.HorizontalSlider(environment.Difficulty.ManualDifficulty, 0f, 1f);
            if (!Mathf.Approximately(selected, environment.Difficulty.ManualDifficulty))
            {
                environment.Difficulty.ManualDifficulty = selected;
                environment.Difficulty.UseManualDifficulty = true;
            }

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("x0.5")) Time.timeScale = 0.5f;
            if (GUILayout.Button("x1")) Time.timeScale = 1f;
            if (GUILayout.Button("x5")) Time.timeScale = 5f;
            if (GUILayout.Button("x20")) Time.timeScale = 20f;
            GUILayout.EndHorizontal();

            BehaviorParameters behavior = agent.GetComponent<BehaviorParameters>();
            string control = behavior.BehaviorType switch
            {
                BehaviorType.InferenceOnly => "Modelo ONNX",
                BehaviorType.HeuristicOnly => "Manual",
                _ => "Trainer / automático"
            };
            Label("Control", control);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Manual"))
                behavior.BehaviorType = BehaviorType.HeuristicOnly;
            if (GUILayout.Button("Modelo ONNX") && behavior.Model != null)
                behavior.BehaviorType = BehaviorType.InferenceOnly;
            if (GUILayout.Button("Trainer"))
                behavior.BehaviorType = BehaviorType.Default;
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            seedText = GUILayout.TextField(seedText);
            if (GUILayout.Button("Aplicar seed", GUILayout.Width(100f))
                && int.TryParse(seedText, out int seed))
                environment.RestartWithSeed(seed);
            GUILayout.EndHorizontal();
            if (GUILayout.Button("Reiniciar misma seed"))
                environment.RestartCurrentSeed();
            if (GUILayout.Button("Volver a seeds variables"))
                environment.UseSequenceSeeds();

            if (showControls)
            {
                GUILayout.Space(8f);
                GUILayout.Label("WASD/flechas: mover · Shift: sprint · E: interactuar", labelStyle);
                GUILayout.Label($"CSV: {environment.Statistics.CsvPath}", GUI.skin.label);
            }
            GUILayout.EndArea();
        }

        private void DrawBar(string title, float normalized, Color color)
        {
            Rect rect = GUILayoutUtility.GetRect(280f, 20f);
            GUI.Box(rect, GUIContent.none);
            Rect fill = new(rect.x + 2f, rect.y + 2f, (rect.width - 4f) * Mathf.Clamp01(normalized), rect.height - 4f);
            Color previous = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(fill, Texture2D.whiteTexture);
            GUI.color = previous;
            GUI.Label(rect, $"{title}: {normalized * 100f:0}%");
        }

        private void Label(string name, string value)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(name, labelStyle, GUILayout.Width(120f));
            GUILayout.Label(value, labelStyle);
            GUILayout.EndHorizontal();
        }

        private void EnsureStyles()
        {
            titleStyle ??= new GUIStyle(GUI.skin.label)
            {
                fontSize = 22,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            labelStyle ??= new GUIStyle(GUI.skin.label) { fontSize = 13 };
        }
    }
}
