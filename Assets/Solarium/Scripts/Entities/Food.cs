using UnityEngine;

namespace Solarium
{
    public sealed class Food : MonoBehaviour
    {
        private SolariumEnvironment environment;

        public bool IsGolden { get; private set; }

        public void Initialize(SolariumEnvironment owner, bool golden)
        {
            environment = owner;
            IsGolden = golden;
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            SolAgent agent = other.GetComponentInParent<SolAgent>();
            if (agent == null || agent != environment.Agent || !gameObject.activeSelf)
                return;
            environment.CollectFood(this);
        }
    }
}
