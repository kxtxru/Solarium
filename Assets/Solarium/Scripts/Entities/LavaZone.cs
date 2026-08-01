using UnityEngine;

namespace Solarium
{
    public sealed class LavaZone : MonoBehaviour
    {
        private SolariumEnvironment environment;

        public void Initialize(SolariumEnvironment owner) => environment = owner;

        private void OnTriggerEnter2D(Collider2D other)
        {
            SolAgent agent = other.GetComponentInParent<SolAgent>();
            if (environment == null
                || agent == null
                || agent != environment.Agent
                || !agent.Vitals.IsAlive)
                return;

            agent.Vitals.Kill("lava");
        }
    }
}
