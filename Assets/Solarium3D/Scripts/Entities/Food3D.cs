using UnityEngine;

namespace Solarium.ThreeD
{
    public sealed class Food3D : MonoBehaviour
    {
        private SolariumEnvironment3D environment;
        public bool IsGolden { get; private set; }

        public void Initialize(SolariumEnvironment3D owner, bool golden)
        {
            environment = owner;
            IsGolden = golden;
        }

        private void OnTriggerEnter(Collider other)
        {
            SolAgent3D agent = other.GetComponentInParent<SolAgent3D>();
            if (agent != null && environment != null && agent == environment.Agent && gameObject.activeSelf)
                environment.CollectFood(this);
        }
    }
}
