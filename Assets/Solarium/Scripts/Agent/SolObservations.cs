using Unity.MLAgents.Sensors;
using UnityEngine;

namespace Solarium
{
    public sealed class SolObservations : MonoBehaviour
    {
        // Keep this contract stable. New world objects should describe themselves
        // through the generic ray properties below before adding another input.
        public const int InternalObservationCount = 26;
        public const int ValuesPerRay = 14;
        public const int ReservedRayFeatureCount = 4;

        [SerializeField, Min(1)] private int rayCount = 12;
        [SerializeField, Min(0.5f)] private float visionRange = 8f;
        [SerializeField, Min(0.05f)] private float nearWallDistance = 0.7f;
        [SerializeField] private bool drawDebugSensors;

        private readonly RaycastHit2D[] rayHits = new RaycastHit2D[12];
        private ContactFilter2D rayFilter;
        private SolariumEnvironment environment;
        private SolVitals vitals;
        private Rigidbody2D body;

        public int RayCount => rayCount;
        public float VisionRange => visionRange;
        public bool DrawDebugSensors
        {
            get => drawDebugSensors;
            set => drawDebugSensors = value;
        }

        public static int ObservationSizeFor(int rays) =>
            InternalObservationCount + Mathf.Max(1, rays) * ValuesPerRay;

        public void Initialize(SolariumEnvironment owner, SolVitals agentVitals, Rigidbody2D agentBody, int rays)
        {
            environment = owner;
            vitals = agentVitals;
            body = agentBody;
            rayCount = Mathf.Max(1, rays);
            rayFilter = new ContactFilter2D();
            rayFilter.SetLayerMask(Physics2D.AllLayers);
            rayFilter.useTriggers = true;
        }

        public void SetVisionRange(float value) => visionRange = Mathf.Max(0.5f, value);

        public void Write(VectorSensor sensor, bool sprinting)
        {
            Vector2 velocity = body == null ? Vector2.zero : body.linearVelocity;
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
            sensor.AddObservation(
                environment != null && environment.Agent != null && environment.Agent.IsInMud
                    ? 1f
                    : 0f);
            sensor.AddObservation(
                environment != null && environment.Agent != null && environment.Agent.IsCarryingSupply
                    ? 1f
                    : 0f);

            AddNearestFood(sensor);
            AddNearestHealing(sensor);
            AddNearestEnemy(sensor);
            AddNearestSupply(sensor);
            AddNearestShelter(sensor);
            AddRays(sensor);
        }

        public bool IsNearWall()
        {
            Vector2 origin = transform.position;
            for (int i = 0; i < 4; i++)
            {
                Vector2 direction = i switch
                {
                    0 => Vector2.right,
                    1 => Vector2.up,
                    2 => Vector2.left,
                    _ => Vector2.down
                };
                if (TryGetRayHit(origin, direction, nearWallDistance, out RaycastHit2D hit)
                    && hit.collider.GetComponentInParent<ArenaSolid>() != null)
                    return true;
            }
            return false;
        }

        public float[] CaptureNormalized(bool sprinting)
        {
            var collector = new ArrayObservationWriter(ObservationSizeFor(rayCount));
            Write(collector.Sensor, sprinting);
            return collector.Values;
        }

        private void AddNearestFood(VectorSensor sensor)
        {
            Transform nearest = null;
            float distance = visionRange;
            if (environment != null)
            {
                foreach (Food food in environment.Foods)
                {
                    if (food == null || !food.gameObject.activeInHierarchy)
                        continue;
                    float candidate = Vector2.Distance(transform.position, food.transform.position);
                    if (candidate < distance)
                    {
                        distance = candidate;
                        nearest = food.transform;
                    }
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
                foreach (HealingZone healing in environment.HealingZones)
                {
                    if (healing == null || !healing.IsAvailable)
                        continue;
                    float candidate = Vector2.Distance(transform.position, healing.transform.position);
                    if (candidate < distance)
                    {
                        distance = candidate;
                        nearest = healing.transform;
                    }
                }
            }
            AddDirectionDistance(sensor, nearest, distance);
        }

        private void AddNearestEnemy(VectorSensor sensor)
        {
            EnemyController nearest = null;
            float distance = visionRange;
            if (environment != null)
            {
                foreach (EnemyController enemy in environment.Enemies)
                {
                    if (enemy == null || !enemy.gameObject.activeInHierarchy)
                        continue;
                    float candidate = Vector2.Distance(transform.position, enemy.transform.position);
                    if (candidate < distance)
                    {
                        distance = candidate;
                        nearest = enemy;
                    }
                }
            }

            if (nearest == null)
            {
                sensor.AddObservation(Vector2.zero);
                sensor.AddObservation(1f);
                sensor.AddObservation(Vector2.zero);
                return;
            }

            Vector2 offset = nearest.transform.position - transform.position;
            sensor.AddObservation(offset.normalized);
            sensor.AddObservation(Mathf.Clamp01(distance / visionRange));
            Vector2 ownVelocity = body == null ? Vector2.zero : body.linearVelocity;
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
                foreach (SupplyShard supply in environment.Supplies)
                {
                    if (supply == null || !supply.IsAvailable)
                        continue;
                    float candidate = Vector2.Distance(transform.position, supply.transform.position);
                    if (candidate < distance)
                    {
                        distance = candidate;
                        nearest = supply.transform;
                    }
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
                foreach (ShelterZone shelter in environment.Shelters)
                {
                    if (shelter == null || !shelter.gameObject.activeInHierarchy)
                        continue;
                    float candidate = Vector2.Distance(transform.position, shelter.transform.position);
                    if (candidate < distance)
                    {
                        distance = candidate;
                        nearest = shelter.transform;
                    }
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
            Vector2 offset = target.position - transform.position;
            sensor.AddObservation(offset.normalized);
            sensor.AddObservation(Mathf.Clamp01(distance / visionRange));
        }

        private void AddRays(VectorSensor sensor)
        {
            Vector2 origin = transform.position;
            for (int i = 0; i < rayCount; i++)
            {
                float angle = i * Mathf.PI * 2f / rayCount;
                Vector2 direction = new(Mathf.Cos(angle), Mathf.Sin(angle));
                float normalizedDistance = 1f;
                Collider2D detected = null;

                if (TryGetRayHit(origin, direction, visionRange, out RaycastHit2D hit))
                {
                    normalizedDistance = Mathf.Clamp01(hit.distance / visionRange);
                    detected = hit.collider;
                }

                sensor.AddObservation(normalizedDistance);
                AddRayProperties(sensor, detected);
            }
        }

        private void AddRayProperties(VectorSensor sensor, Collider2D collider)
        {
            bool solid = collider != null && collider.GetComponentInParent<ArenaSolid>() != null;
            Food food = collider == null ? null : collider.GetComponentInParent<Food>();
            HealingZone healing = collider == null ? null : collider.GetComponentInParent<HealingZone>();
            EnemyController enemy = collider == null ? null : collider.GetComponentInParent<EnemyController>();
            Hazard hazard = collider == null ? null : collider.GetComponentInParent<Hazard>();
            MudZone mud = collider == null ? null : collider.GetComponentInParent<MudZone>();
            LavaZone lava = collider == null ? null : collider.GetComponentInParent<LavaZone>();
            SupplyShard supply = collider == null ? null : collider.GetComponentInParent<SupplyShard>();
            ShelterZone shelter = collider == null ? null : collider.GetComponentInParent<ShelterZone>();

            float resourceValue = food != null ? food.IsGolden ? 1f : 0.6f : supply != null ? 0.35f : 0f;
            float healingAvailable = healing != null && healing.IsAvailable ? 1f : 0f;
            float hostile = enemy == null ? 0f : 1f;
            float damage = DamageSignal(enemy, hazard, lava);
            float slow = mud == null || environment == null
                ? 0f
                : 1f - environment.Settings.mudSpeedMultiplier;
            float lethal = lava == null ? 0f : 1f;
            float moving = enemy == null ? 0f : 1f;
            float environmentalHazard = hazard != null || mud != null || lava != null ? 1f : 0f;

            // distance is written by AddRays. The remaining 13 values are fixed:
            // solid, resource value, healing, hostile, damage, slow, lethal,
            // moving, environmental hazard, supply, shelter, then two future-use channels.
            sensor.AddObservation(solid ? 1f : 0f);
            sensor.AddObservation(resourceValue);
            sensor.AddObservation(healingAvailable);
            sensor.AddObservation(hostile);
            sensor.AddObservation(damage);
            sensor.AddObservation(Mathf.Clamp01(slow));
            sensor.AddObservation(lethal);
            sensor.AddObservation(moving);
            sensor.AddObservation(environmentalHazard);
            sensor.AddObservation(supply != null && supply.IsAvailable ? 1f : 0f);
            sensor.AddObservation(shelter != null ? 1f : 0f);
            for (int i = 2; i < ReservedRayFeatureCount; i++)
                sensor.AddObservation(0f);
        }

        private float DamageSignal(EnemyController enemy, Hazard hazard, LavaZone lava)
        {
            if (lava != null)
                return 1f;

            float maxHealth = vitals == null ? 100f : Mathf.Max(1f, vitals.maxHealth);
            if (enemy != null && environment != null)
            {
                float damage = environment.Settings.enemyDamage
                    * environment.CurrentConfig.enemyDamageMultiplier;
                return Mathf.Clamp01(damage / maxHealth);
            }

            if (hazard != null && environment != null)
                return Mathf.Clamp01(environment.Settings.hazardDamagePerSecond / maxHealth);

            return 0f;
        }

        private bool TryGetRayHit(Vector2 origin, Vector2 direction, float distance, out RaycastHit2D nearest)
        {
            int count = Physics2D.Raycast(origin, direction, rayFilter, rayHits, distance);
            float bestDistance = float.PositiveInfinity;
            nearest = default;
            bool found = false;
            for (int i = 0; i < count; i++)
            {
                Collider2D collider = rayHits[i].collider;
                if (collider == null || collider.transform == transform || collider.transform.IsChildOf(transform))
                    continue;
                if (rayHits[i].distance < bestDistance)
                {
                    bestDistance = rayHits[i].distance;
                    nearest = rayHits[i];
                    found = true;
                }
            }
            return found;
        }

        private void OnDrawGizmosSelected()
        {
            if (!drawDebugSensors || rayCount <= 0)
                return;

            Gizmos.color = new Color(1f, 0.85f, 0.2f, 0.7f);
            for (int i = 0; i < rayCount; i++)
            {
                float angle = i * Mathf.PI * 2f / rayCount;
                Vector3 direction = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle)) * visionRange;
                Gizmos.DrawRay(transform.position, direction);
            }
            Gizmos.color = new Color(1f, 1f, 1f, 0.15f);
            Gizmos.DrawWireSphere(transform.position, visionRange);
        }

        private sealed class ArrayObservationWriter
        {
            public ArrayObservationWriter(int size)
            {
                Values = new float[size];
                Sensor = new VectorSensor(size);
            }

            public VectorSensor Sensor { get; }
            public float[] Values { get; }
        }
    }
}
