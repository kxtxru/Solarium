using UnityEngine;

namespace Solarium
{
    public sealed class SupplyShard : MonoBehaviour
    {
        private SolariumEnvironment environment;

        public bool IsAvailable => gameObject.activeInHierarchy;

        public void Initialize(SolariumEnvironment owner) => environment = owner;

        public bool TryPickUp(SolAgent agent)
        {
            if (environment == null || agent == null || !IsAvailable || !agent.PickUpSupply())
                return false;

            gameObject.SetActive(false);
            return true;
        }
    }
}
