using UnityEngine;

namespace Solarium
{
    public sealed class AgentDebugView : MonoBehaviour
    {
        [SerializeField] private bool showDebug;
        private SolAgent agent;
        private Rigidbody2D body;
        private SolObservations observations;

        public bool ShowDebug
        {
            get => showDebug;
            set
            {
                showDebug = value;
                if (observations != null)
                    observations.DrawDebugSensors = value;
            }
        }

        private void Awake()
        {
            agent = GetComponent<SolAgent>();
            body = GetComponent<Rigidbody2D>();
            observations = GetComponent<SolObservations>();
            observations.DrawDebugSensors = showDebug;
        }

        private void OnGUI()
        {
            if (!showDebug || agent == null)
                return;
            GUILayout.BeginArea(new Rect(14f, Screen.height - 135f, 410f, 120f), GUI.skin.box);
            GUILayout.Label($"Última acción: mover {agent.LastMovement} · sprint {agent.IsSprinting}");
            GUILayout.Label($"Velocidad: {(body == null ? Vector2.zero : body.linearVelocity)}");
            GUILayout.Label($"Terreno: {(agent.IsInMud ? "barro" : "normal")} · observaciones: {SolObservations.ObservationSizeFor(observations.RayCount)}");
            if (GUILayout.Button("Ocultar sensores"))
                ShowDebug = false;
            GUILayout.EndArea();
        }

        private void OnDrawGizmosSelected()
        {
            if (!showDebug || agent == null)
                return;
            Gizmos.color = Color.cyan;
            Gizmos.DrawRay(transform.position, (Vector3)agent.LastMovement * 2f);
        }
    }
}
