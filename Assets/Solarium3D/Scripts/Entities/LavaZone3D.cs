using UnityEngine;

namespace Solarium.ThreeD
{
    public sealed class LavaZone3D : MonoBehaviour
    {
        private SolariumEnvironment3D environment;
        public void Initialize(SolariumEnvironment3D owner) => environment = owner;

        private void OnTriggerEnter(Collider other)
        {
            SolAgent3D agent = other.GetComponentInParent<SolAgent3D>();
            if (environment != null && agent == environment.Agent && agent.Vitals.IsAlive)
                agent.Vitals.Kill("lava");
        }
    }
}
