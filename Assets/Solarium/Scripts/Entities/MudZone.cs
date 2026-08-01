using UnityEngine;

namespace Solarium
{
    public sealed class MudZone : MonoBehaviour
    {
        private SolariumEnvironment environment;

        public void Initialize(SolariumEnvironment owner) => environment = owner;

        private void OnTriggerEnter2D(Collider2D other)
        {
            SolAgent agent = other.GetComponentInParent<SolAgent>();
            if (environment != null && agent != null && agent == environment.Agent)
                agent.EnterMud(this);
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            SolAgent agent = other.GetComponentInParent<SolAgent>();
            if (environment != null && agent != null && agent == environment.Agent)
                agent.ExitMud(this);
        }

        private void OnDisable()
        {
            if (environment != null && environment.Agent != null)
                environment.Agent.ExitMud(this);
        }
    }
}
