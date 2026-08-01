using UnityEngine;

namespace Solarium.ThreeD
{
    public sealed class Hazard3D : MonoBehaviour
    {
        private SolariumEnvironment3D environment;
        public void Initialize(SolariumEnvironment3D owner) => environment = owner;

        private void OnTriggerStay(Collider other)
        {
            SolAgent3D agent = other.GetComponentInParent<SolAgent3D>();
            if (agent == null || environment == null || agent != environment.Agent)
                return;
            int episode = environment.EpisodeNumber;
            agent.Vitals.ConsumeEnergy(environment.Settings.hazardEnergyPerSecond * Time.fixedDeltaTime);
            if (episode != environment.EpisodeNumber)
                return;
            float pulse = environment.Settings.hazardDamagePerSecond
                * Mathf.Max(agent.Vitals.damageCooldown, Time.fixedDeltaTime);
            agent.Vitals.TakeDamage(pulse, Time.time);
        }
    }
}
