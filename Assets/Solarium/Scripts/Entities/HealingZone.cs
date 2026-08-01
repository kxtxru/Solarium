using UnityEngine;

namespace Solarium
{
    public sealed class HealingZone : MonoBehaviour
    {
        private SolariumEnvironment environment;
        private float availableAt;

        public bool IsAvailable => Time.time >= availableAt;

        public void Initialize(SolariumEnvironment owner) => environment = owner;

        private void Update()
        {
            SpriteRenderer renderer = GetComponent<SpriteRenderer>();
            if (renderer != null)
                renderer.enabled = IsAvailable;
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (!IsAvailable)
                return;
            SolAgent agent = other.GetComponentInParent<SolAgent>();
            if (agent == null || agent != environment.Agent)
                return;
            if (environment.UseHealing())
                availableAt = Time.time + environment.Settings.healingCooldown;
        }
    }
}
