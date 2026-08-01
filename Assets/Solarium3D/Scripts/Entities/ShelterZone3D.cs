using UnityEngine;

namespace Solarium.ThreeD
{
    public sealed class ShelterZone3D : MonoBehaviour
    {
        private SolariumEnvironment3D environment;
        public int StoredSupplies { get; private set; }

        public void Initialize(SolariumEnvironment3D owner)
        {
            environment = owner;
            StoredSupplies = 0;
        }

        public bool Deposit(SolAgent3D agent)
        {
            if (environment == null || agent == null || !agent.DepositSupply())
                return false;
            StoredSupplies++;
            return true;
        }

        public bool TryUseReserve(SolAgent3D agent)
        {
            if (environment == null || agent == null || StoredSupplies <= 0 || agent.Vitals.NormalizedEnergy >= 0.98f)
                return false;
            StoredSupplies--;
            agent.Vitals.AddEnergy(environment.Settings.shelterReserveEnergy);
            return true;
        }

        private void OnTriggerEnter(Collider other)
        {
            SolAgent3D agent = other.GetComponentInParent<SolAgent3D>();
            if (environment != null && agent == environment.Agent)
                agent.EnterShelter(this);
        }

        private void OnTriggerExit(Collider other)
        {
            SolAgent3D agent = other.GetComponentInParent<SolAgent3D>();
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
