using UnityEngine;

namespace Solarium
{
    public sealed class Hazard : MonoBehaviour
    {
        private SolariumEnvironment environment;

        public void Initialize(SolariumEnvironment owner) => environment = owner;

        private void OnTriggerStay2D(Collider2D other)
        {
            SolAgent agent = other.GetComponentInParent<SolAgent>();
            if (agent == null || agent != environment.Agent)
                return;
            int episode = environment.EpisodeNumber;
            agent.Vitals.ConsumeEnergy(environment.Settings.hazardEnergyPerSecond * Time.fixedDeltaTime);
            if (episode != environment.EpisodeNumber)
                return;
            float damagePulse = environment.Settings.hazardDamagePerSecond
                * Mathf.Max(agent.Vitals.damageCooldown, Time.fixedDeltaTime);
            agent.Vitals.TakeDamage(damagePulse, Time.time);
        }
    }
}
