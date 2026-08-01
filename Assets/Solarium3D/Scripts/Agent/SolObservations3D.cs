using Unity.MLAgents.Sensors;
using UnityEngine;

namespace Solarium.ThreeD
{
    public sealed class SolObservations3D : MonoBehaviour
    {
        public const int InternalObservationCount = 26;
        public const int ValuesPerRay = 14;
        public const int ReservedRayFeatureCount = 4;

        [SerializeField, Min(1)] private int rayCount = 12;
        [SerializeField, Min(0.5f)] private float visionRange = 8f;
        [SerializeField, Min(0.05f)] private float nearWallDistance = 0.7f;
        [SerializeField] private bool drawDebugSensors;

        private readonly RaycastHit[] rayHits = new RaycastHit[32];
        private SolariumEnvironment3D environment;
        private SolVitals vitals;
        private Rigidbody body;

        public int RayCount => rayCount;
        public float VisionRange => visionRange;
        public bool DrawDebugSensors { get => drawDebugSensors; set => drawDebugSensors = value; }

        public static int ObservationSizeFor(int rays) =>
            InternalObservationCount + Mathf.Max(1, rays) * ValuesPerRay;

        public void Initialize(SolariumEnvironment3D owner, SolVitals agentVitals, Rigidbody agentBody, int rays)
        {
            environment = owner;
            vitals = agentVitals;
            body = agentBody;
            rayCount = Mathf.Max(1, rays);
        }

        public void SetVisionRange(float value) => visionRange = Mathf.Max(0.5f, value);

        public void Write(VectorSensor sensor, bool sprinting)
        {
            Vector2 velocity = body == null ? Vector2.zero : Planar3D.ToPlanarDirection(body.linearVelocity);
            float maxSpeed = vitals == null
                ? 1f
                : Mathf.Max(0.1f, vitals.movementSpeed * vitals.sprintMultiplier);

            sensor.AddObservation(vitals == null ? 0f : vitals.NormalizedHealth);
            sensor.AddObservation(vitals == null ? 0f : vitals.NormalizedEnergy);
            sensor.AddObservation(Mathf.Clamp(velocity.x / maxSpeed, -1f, 1f));
            sensor.AddObservation(Mathf.Clamp(velocity.y / maxSpeed, -1f, 1f));
            sensor.AddObservation(IsNearWall() ? 1f : 0f);
            sensor.AddObservation(vitals != null && vitals.IsReceivingDamage ? 1f : 0f);
            sensor.AddObservation(sprinting ? 1f : 0f);
            sensor.AddObservation(environment != null && environment.Agent.IsInMud ? 1f : 0f);
            sensor.AddObservation(environment != null && environment.Agent.IsCarryingSupply ? 1f : 0f);

            AddNearestFood(sensor);
            AddNearestHealing(sensor);
            AddNearestEnemy(sensor);
            AddNearestSupply(sensor);
            AddNearestShelter(sensor);
            AddRays(sensor);
        }

        public bool IsNearWall()
        {
            Vector3 origin = transform.position;
            Vector2[] directions = { Vector2.right, Vector2.up, Vector2.left, Vector2.down };
            for (int i = 0; i < directions.Length; i++)
            {
                if (TryGetRayHit(origin, Planar3D.ToWorldDirection(directions[i]), nearWallDistance, out RaycastHit hit)
                    && hit.collider.GetComponentInParent<ArenaSolid3D>() != null)
                    return true;
            }
            return false;
        }

        private void AddNearestFood(VectorSensor sensor)
        {
            Transform nearest = null;
            float distance = visionRange;
            if (environment != null)
            {
                foreach (Food3D food in environment.Foods)
                {
                    if (food == null || !food.gameObject.activeInHierarchy)
                        continue;
                    float candidate = PlanarDistance(transform.position, food.transform.position);
                    if (candidate < distance) { distance = candidate; nearest = food.transform; }
                }
            }
            AddDirectionDistance(sensor, nearest, distance);
        }

        private void AddNearestHealing(VectorSensor sensor)
        {
            Transform nearest = null;
            float distance = visionRange;
            if (environment != null)
            {
                foreach (HealingZone3D healing in environment.HealingZones)
                {
                    if (healing == null || !healing.IsAvailable)
                        continue;
                    float candidate = PlanarDistance(transform.position, healing.transform.position);
                    if (candidate < distance) { distance = candidate; nearest = healing.transform; }
                }
            }
            AddDirectionDistance(sensor, nearest, distance);
        }

        private void AddNearestEnemy(VectorSensor sensor)
        {
            EnemyController3D nearest = null;
            float distance = visionRange;
            if (environment != null)
            {
                foreach (EnemyController3D enemy in environment.Enemies)
                {
                    if (enemy == null || !enemy.gameObject.activeInHierarchy)
                        continue;
                    float candidate = PlanarDistance(transform.position, enemy.transform.position);
                    if (candidate < distance) { distance = candidate; nearest = enemy; }
                }
            }

            if (nearest == null)
            {
                sensor.AddObservation(Vector2.zero);
                sensor.AddObservation(1f);
                sensor.AddObservation(Vector2.zero);
                return;
            }

            Vector2 offset = Planar3D.ToPlanar(nearest.transform.position - transform.position);
            sensor.AddObservation(offset.normalized);
            sensor.AddObservation(Mathf.Clamp01(distance / visionRange));
            Vector2 ownVelocity = body == null ? Vector2.zero : Planar3D.ToPlanarDirection(body.linearVelocity);
            Vector2 relative = nearest.Velocity - ownVelocity;
            float scale = Mathf.Max(1f, nearest.MaxSpeed + (vitals == null ? 0f : vitals.movementSpeed));
            sensor.AddObservation(Vector2.ClampMagnitude(relative / scale, 1f));
        }

        private void AddNearestSupply(VectorSensor sensor)
        {
            Transform nearest = null;
            float distance = visionRange;
            if (environment != null)
            {
                foreach (SupplyShard3D supply in environment.Supplies)
                {
                    if (supply == null || !supply.IsAvailable)
                        continue;
                    float candidate = PlanarDistance(transform.position, supply.transform.position);
                    if (candidate < distance) { distance = candidate; nearest = supply.transform; }
                }
            }
            AddDirectionDistance(sensor, nearest, distance);
        }

        private void AddNearestShelter(VectorSensor sensor)
        {
            Transform nearest = null;
            float distance = visionRange;
            if (environment != null)
            {
                foreach (ShelterZone3D shelter in environment.Shelters)
                {
                    if (shelter == null || !shelter.gameObject.activeInHierarchy)
                        continue;
                    float candidate = PlanarDistance(transform.position, shelter.transform.position);
                    if (candidate < distance) { distance = candidate; nearest = shelter.transform; }
                }
            }
            AddDirectionDistance(sensor, nearest, distance);
        }

        private void AddDirectionDistance(VectorSensor sensor, Transform target, float distance)
        {
            if (target == null)
            {
                sensor.AddObservation(Vector2.zero);
                sensor.AddObservation(1f);
                return;
            }
            Vector2 offset = Planar3D.ToPlanar(target.position - transform.position);
            sensor.AddObservation(offset.normalized);
            sensor.AddObservation(Mathf.Clamp01(distance / visionRange));
        }

        private void AddRays(VectorSensor sensor)
        {
            Vector3 origin = transform.position;
            for (int i = 0; i < rayCount; i++)
            {
                float angle = i * Mathf.PI * 2f / rayCount;
                Vector3 direction = new(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                float normalizedDistance = 1f;
                Collider detected = null;
                if (TryGetRayHit(origin, direction, visionRange, out RaycastHit hit))
                {
                    normalizedDistance = Mathf.Clamp01(hit.distance / visionRange);
                    detected = hit.collider;
                }
                sensor.AddObservation(normalizedDistance);
                AddRayProperties(sensor, detected);
            }
        }

        private void AddRayProperties(VectorSensor sensor, Collider collider)
        {
            bool solid = collider != null && collider.GetComponentInParent<ArenaSolid3D>() != null;
            Food3D food = collider == null ? null : collider.GetComponentInParent<Food3D>();
            HealingZone3D healing = collider == null ? null : collider.GetComponentInParent<HealingZone3D>();
            EnemyController3D enemy = collider == null ? null : collider.GetComponentInParent<EnemyController3D>();
            Hazard3D hazard = collider == null ? null : collider.GetComponentInParent<Hazard3D>();
            MudZone3D mud = collider == null ? null : collider.GetComponentInParent<MudZone3D>();
            LavaZone3D lava = collider == null ? null : collider.GetComponentInParent<LavaZone3D>();
            SupplyShard3D supply = collider == null ? null : collider.GetComponentInParent<SupplyShard3D>();
            ShelterZone3D shelter = collider == null ? null : collider.GetComponentInParent<ShelterZone3D>();

            float resourceValue = food != null ? food.IsGolden ? 1f : 0.6f : supply != null ? 0.35f : 0f;
            float slow = mud == null || environment == null ? 0f : 1f - environment.Settings.mudSpeedMultiplier;
            sensor.AddObservation(solid ? 1f : 0f);
            sensor.AddObservation(resourceValue);
            sensor.AddObservation(healing != null && healing.IsAvailable ? 1f : 0f);
            sensor.AddObservation(enemy != null ? 1f : 0f);
            sensor.AddObservation(DamageSignal(enemy, hazard, lava));
            sensor.AddObservation(Mathf.Clamp01(slow));
            sensor.AddObservation(lava != null ? 1f : 0f);
            sensor.AddObservation(enemy != null ? 1f : 0f);
            sensor.AddObservation(hazard != null || mud != null || lava != null ? 1f : 0f);
            sensor.AddObservation(supply != null && supply.IsAvailable ? 1f : 0f);
            sensor.AddObservation(shelter != null ? 1f : 0f);
            sensor.AddObservation(shelter == null ? 0f : shelter.NormalizedReserve);
            sensor.AddObservation(0f);
        }

        private float DamageSignal(EnemyController3D enemy, Hazard3D hazard, LavaZone3D lava)
        {
            if (lava != null)
                return 1f;
            float maxHealth = vitals == null ? 100f : Mathf.Max(1f, vitals.maxHealth);
            if (enemy != null && environment != null)
                return Mathf.Clamp01(environment.Settings.enemyDamage * environment.CurrentConfig.enemyDamageMultiplier / maxHealth);
            if (hazard != null && environment != null)
                return Mathf.Clamp01(environment.Settings.hazardDamagePerSecond / maxHealth);
            return 0f;
        }

        private bool TryGetRayHit(Vector3 origin, Vector3 direction, float distance, out RaycastHit nearest)
        {
            int count = Physics.RaycastNonAlloc(
                origin,
                direction,
                rayHits,
                distance,
                SolariumLayers3D.SensorMask,
                QueryTriggerInteraction.Collide);
            float bestDistance = float.PositiveInfinity;
            nearest = default;
            bool found = false;
            for (int i = 0; i < count; i++)
            {
                Collider collider = rayHits[i].collider;
                if (collider == null
                    || collider.transform == transform
                    || collider.transform.IsChildOf(transform)
                    || collider.GetComponentInParent<SolariumEnvironment3D>() != environment)
                    continue;
                if (rayHits[i].distance >= bestDistance)
                    continue;
                bestDistance = rayHits[i].distance;
                nearest = rayHits[i];
                found = true;
            }
            return found;
        }

        private static float PlanarDistance(Vector3 a, Vector3 b) =>
            Vector2.Distance(Planar3D.ToPlanar(a), Planar3D.ToPlanar(b));

        private void OnDrawGizmosSelected()
        {
            if (!drawDebugSensors || rayCount <= 0)
                return;
            Gizmos.color = new Color(1f, 0.85f, 0.2f, 0.7f);
            for (int i = 0; i < rayCount; i++)
            {
                float angle = i * Mathf.PI * 2f / rayCount;
                Gizmos.DrawRay(transform.position, new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * visionRange);
            }
            Gizmos.color = new Color(1f, 1f, 1f, 0.15f);
            Gizmos.DrawWireSphere(transform.position, visionRange);
        }
    }
}
