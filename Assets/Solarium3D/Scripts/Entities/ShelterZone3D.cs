using UnityEngine;

namespace Solarium.ThreeD
{
    public sealed class ShelterZone3D : MonoBehaviour
    {
        private SolariumEnvironment3D environment;
        private InfiniteWorld3D world;
        private ChunkEntityState3D streamedState;
        private ReserveIndicator3D indicator;
        private int localStoredSupplies;

        public int StoredSupplies => streamedState == null ? localStoredSupplies : streamedState.storedRations;
        public float NormalizedReserve => Mathf.Clamp01(StoredSupplies / 4f);
        public bool IsStreamed => world != null;
        public ChunkCoord3D ChunkCoord { get; private set; }
        public string EntityId { get; private set; }
        public event System.Action<int> ReserveChanged;

        public void Initialize(SolariumEnvironment3D owner)
        {
            environment = owner;
            world = null;
            streamedState = null;
            localStoredSupplies = 0;
            EntityId = string.Empty;
            NotifyReserveChanged();
        }

        public void InitializeStreamed(
            SolariumEnvironment3D owner,
            InfiniteWorld3D infiniteWorld,
            ChunkCoord3D coord,
            string id)
        {
            environment = owner;
            world = infiniteWorld;
            ChunkCoord = coord;
            EntityId = id;
            streamedState = world.GetEntityState(coord, id);
            NotifyReserveChanged();
        }

        public void BindIndicator(ReserveIndicator3D value)
        {
            indicator = value;
            indicator?.SetCount(StoredSupplies);
        }

        public bool Deposit(SolAgent3D agent)
        {
            if (environment == null || agent == null || !agent.DepositSupply())
                return false;
            SetStoredSupplies(StoredSupplies + 1);
            environment.RegisterRationDeposited(this);
            return true;
        }

        public bool CanUseReserve(SolAgent3D agent)
        {
            if (environment == null || agent == null || StoredSupplies <= 0)
                return false;
            float missing = Mathf.Max(0f, agent.Vitals.maxEnergy - agent.Vitals.CurrentEnergy);
            return missing >= Mathf.Max(1f, environment.Settings.shelterMinimumEnergyDeficit);
        }

        public bool TryUseReserve(SolAgent3D agent)
        {
            if (!CanUseReserve(agent))
                return false;
            float restored = agent.Vitals.AddEnergy(environment.Settings.shelterReserveEnergy);
            if (restored <= 0f)
                return false;
            SetStoredSupplies(StoredSupplies - 1);
            environment.RegisterRationConsumed(this, restored);
            return true;
        }

        private void SetStoredSupplies(int value)
        {
            int clamped = Mathf.Max(0, value);
            if (streamedState == null)
                localStoredSupplies = clamped;
            else
                streamedState.storedRations = clamped;
            NotifyReserveChanged();
            world?.NotifyReserveChanged();
        }

        private void NotifyReserveChanged()
        {
            indicator?.SetCount(StoredSupplies);
            ReserveChanged?.Invoke(StoredSupplies);
        }

        private void OnTriggerEnter(Collider other)
        {
            SolAgent3D agent = other.GetComponentInParent<SolAgent3D>();
            if (environment != null && agent == environment.Agent)
            {
                agent.EnterShelter(this);
                environment.RegisterShelterVisited(this);
            }
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
