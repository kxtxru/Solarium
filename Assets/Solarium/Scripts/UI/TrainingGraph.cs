using System.Collections.Generic;
using UnityEngine;

namespace Solarium
{
    public sealed class TrainingGraph : MonoBehaviour
    {
        [SerializeField] private SolariumEnvironment environment;
        [SerializeField] private bool visible = true;
        private Texture2D lineTexture;

        public void Initialize(SolariumEnvironment owner) => environment = owner;

        private void Awake()
        {
            lineTexture = new Texture2D(1, 1, TextureFormat.RGBA32, false)
            {
                hideFlags = HideFlags.HideAndDontSave
            };
            lineTexture.SetPixel(0, 0, Color.white);
            lineTexture.Apply();
        }

        private void OnGUI()
        {
            if (!visible || environment == null || environment.Statistics.Recent.Count < 2)
                return;

            Rect panel = new(Screen.width - 390f, 14f, 376f, 205f);
            GUI.Box(panel, "Rendimiento reciente");
            Rect graph = new(panel.x + 12f, panel.y + 28f, panel.width - 24f, 130f);
            GUI.Box(graph, GUIContent.none);

            var records = new List<EpisodeRecord>(environment.Statistics.Recent);
            float maxSurvival = Mathf.Max(1f, environment.Statistics.BestSurvival);
            for (int i = 1; i < records.Count; i++)
            {
                Vector2 from = new(
                    graph.x + (i - 1f) / (records.Count - 1f) * graph.width,
                    graph.yMax - records[i - 1].survivalTime / maxSurvival * graph.height);
                Vector2 to = new(
                    graph.x + i / (records.Count - 1f) * graph.width,
                    graph.yMax - records[i].survivalTime / maxSurvival * graph.height);
                DrawLine(from, to, new Color(1f, 0.75f, 0.15f), 2f);
            }

            GUI.Label(
                new Rect(panel.x + 12f, panel.y + 163f, panel.width - 24f, 38f),
                $"Media supervivencia: {environment.Statistics.AverageSurvival:0.0}s   " +
                $"Mejor: {environment.Statistics.BestSurvival:0.0}s\n" +
                $"Comida media: {environment.Statistics.AverageFood:0.00}   " +
                $"Muertes: {environment.Statistics.RecentDeathRate:P0}");
        }

        private void DrawLine(Vector2 from, Vector2 to, Color color, float width)
        {
            Matrix4x4 previous = GUI.matrix;
            Color previousColor = GUI.color;
            Vector2 delta = to - from;
            float angle = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;
            GUI.color = color;
            GUIUtility.RotateAroundPivot(angle, from);
            GUI.DrawTexture(new Rect(from.x, from.y - width * 0.5f, delta.magnitude, width), lineTexture);
            GUI.matrix = previous;
            GUI.color = previousColor;
        }

        private void OnDestroy()
        {
            if (lineTexture != null)
                Destroy(lineTexture);
        }
    }
}
