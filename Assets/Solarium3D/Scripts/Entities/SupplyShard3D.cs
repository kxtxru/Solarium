using UnityEngine;

namespace Solarium.ThreeD
{
    public sealed class SupplyShard3D : MonoBehaviour
    {
        private SolariumEnvironment3D environment;
        public bool IsAvailable => gameObject.activeInHierarchy;
        public void Initialize(SolariumEnvironment3D owner) => environment = owner;

        public bool TryPickUp(SolAgent3D agent)
        {
            if (environment == null || agent == null || !IsAvailable || !agent.PickUpSupply())
                return false;
            gameObject.SetActive(false);
            return true;
        }
    }
}
