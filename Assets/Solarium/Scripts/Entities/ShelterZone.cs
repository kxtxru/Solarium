using UnityEngine;

namespace Solarium
{
    public sealed class ShelterZone : MonoBehaviour
    {
        private SolariumEnvironment environment;

        public int StoredSupplies { get; private set; }

        public void Initialize(SolariumEnvironment owner) => environment = owner;

        public bool Deposit(SolAgent agent)
        {
            if (environment == null || agent == null || !agent.DepositSupply())
                return false;
            StoredSupplies++;
            return true;
        }

        public bool TryUseReserve(SolAgent agent)
        {
            if (environment == null || agent == null || StoredSupplies <= 0)
                return false;
            if (agent.Vitals.NormalizedEnergy >= 0.98f)
                return false;

            StoredSupplies--;
            agent.Vitals.AddEnergy(environment.Settings.shelterReserveEnergy);
            return true;
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            SolAgent agent = other.GetComponentInParent<SolAgent>();
            if (environment != null && agent == environment.Agent)
                agent.EnterShelter(this);
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            SolAgent agent = other.GetComponentInParent<SolAgent>();
            if (environment != null && agent == environment.Agent)
                agent.ExitShelter(this);
        }

        private void OnDisable()
        {
            if (environment != null && environment.Agent != null)
                environment.Agent.ExitShelter(this);
        }
    }
}
