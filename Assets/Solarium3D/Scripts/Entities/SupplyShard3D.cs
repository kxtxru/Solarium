using UnityEngine;

namespace Solarium.ThreeD
{
    public sealed class SupplyShard3D : MonoBehaviour
    {
        private SolariumEnvironment3D environment;
        private InfiniteWorld3D world;
        private ChunkCoord3D chunkCoord;
        private string entityId;
        public bool IsAvailable => gameObject.activeInHierarchy;

        public void Initialize(SolariumEnvironment3D owner)
        {
            environment = owner;
            world = null;
            entityId = string.Empty;
        }

        public void InitializeStreamed(
            SolariumEnvironment3D owner,
            InfiniteWorld3D infiniteWorld,
            ChunkCoord3D coord,
            string id)
        {
            environment = owner;
            world = infiniteWorld;
            chunkCoord = coord;
            entityId = id;
        }

        public bool TryPickUp(SolAgent3D agent)
        {
            if (environment == null || agent == null || !IsAvailable || !agent.PickUpSupply())
                return false;
            world?.TakeRation(chunkCoord, entityId);
            gameObject.SetActive(false);
            environment.RegisterRationCollected();
            return true;
        }
    }
}
