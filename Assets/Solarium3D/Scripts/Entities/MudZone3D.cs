using UnityEngine;

namespace Solarium.ThreeD
{
    public sealed class MudZone3D : MonoBehaviour
    {
        private SolariumEnvironment3D environment;
        public void Initialize(SolariumEnvironment3D owner) => environment = owner;

        private void OnTriggerEnter(Collider other)
        {
            SolAgent3D agent = other.GetComponentInParent<SolAgent3D>();
            if (environment != null && agent == environment.Agent)
                agent.EnterMud(this);
        }

        private void OnTriggerExit(Collider other)
        {
            SolAgent3D agent = other.GetComponentInParent<SolAgent3D>();
            if (environment != null && agent == environment.Agent)
                agent.ExitMud(this);
        }

        private void OnDisable()
        {
            if (environment != null && environment.Agent != null)
                environment.Agent.ExitMud(this);
        }
    }
}
