using UnityEngine;

namespace Solarium.ThreeD
{
    public sealed class HealingZone3D : MonoBehaviour
    {
        private SolariumEnvironment3D environment;
        private InfiniteWorld3D world;
        private ChunkCoord3D chunkCoord;
        private string entityId;
        private float availableAt;
        public bool IsAvailable => world != null ? world.IsReady(chunkCoord, entityId) : Time.time >= availableAt;

        public void Initialize(SolariumEnvironment3D owner)
        {
            environment = owner;
            world = null;
            availableAt = 0f;
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
            availableAt = 0f;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!IsAvailable || environment == null)
                return;
            SolAgent3D agent = other.GetComponentInParent<SolAgent3D>();
            if (agent != environment.Agent || !environment.UseHealing())
                return;
            if (world != null)
                world.SetCooldown(chunkCoord, entityId, environment.Settings.healingCooldown);
            else
                availableAt = Time.time + environment.Settings.healingCooldown;
        }
    }
}
