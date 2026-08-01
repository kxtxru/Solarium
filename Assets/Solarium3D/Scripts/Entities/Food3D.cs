using UnityEngine;

namespace Solarium.ThreeD
{
    public sealed class Food3D : MonoBehaviour
    {
        private SolariumEnvironment3D environment;
        public bool IsGolden { get; private set; }
        public bool IsStreamed { get; private set; }
        public ChunkCoord3D ChunkCoord { get; private set; }
        public string EntityId { get; private set; }

        public void Initialize(SolariumEnvironment3D owner, bool golden)
        {
            environment = owner;
            IsGolden = golden;
            IsStreamed = false;
            EntityId = string.Empty;
        }

        public void InitializeStreamed(
            SolariumEnvironment3D owner,
            bool golden,
            ChunkCoord3D coord,
            string entityId)
        {
            environment = owner;
            IsGolden = golden;
            IsStreamed = true;
            ChunkCoord = coord;
            EntityId = entityId;
        }

        private void OnTriggerEnter(Collider other)
        {
            SolAgent3D agent = other.GetComponentInParent<SolAgent3D>();
            if (agent != null && environment != null && agent == environment.Agent && gameObject.activeSelf)
                environment.CollectFood(this);
        }
    }
}
