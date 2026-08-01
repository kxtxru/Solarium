using UnityEngine;

namespace Solarium.ThreeD
{
    public sealed class HealingZone3D : MonoBehaviour
    {
        private SolariumEnvironment3D environment;
        private float availableAt;
        public bool IsAvailable => Time.time >= availableAt;

        public void Initialize(SolariumEnvironment3D owner)
        {
            environment = owner;
            availableAt = 0f;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!IsAvailable || environment == null)
                return;
            SolAgent3D agent = other.GetComponentInParent<SolAgent3D>();
            if (agent == environment.Agent && environment.UseHealing())
                availableAt = Time.time + environment.Settings.healingCooldown;
        }
    }
}
