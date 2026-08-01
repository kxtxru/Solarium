using UnityEngine;

namespace Solarium.ThreeD
{
    [RequireComponent(typeof(Rigidbody), typeof(CapsuleCollider))]
    public sealed class EnemyController3D : MonoBehaviour
    {
        private SolariumEnvironment3D environment;
        private Rigidbody body;
        private bool patroller;
        private bool chasing;
        private Vector2 patrolOrigin;
        private Vector2 patrolTarget;
        private float patrolPhase;
        private float retreatUntil;

        public Vector2 Velocity => body == null ? Vector2.zero : Planar3D.ToPlanarDirection(body.linearVelocity);
        public float MaxSpeed => environment == null
            ? 0f
            : environment.Settings.enemySpeed * environment.CurrentConfig.enemySpeedMultiplier;

        public void Initialize(SolariumEnvironment3D owner, bool isPatroller, Vector2 origin)
        {
            if (environment != null && environment.InfiniteWorld != null)
                environment.InfiniteWorld.OriginShifted -= HandleOriginShift;
            environment = owner;
            patroller = isPatroller;
            chasing = false;
            patrolOrigin = origin;
            patrolPhase = Mathf.Abs(owner.CurrentSeed % 997) * 0.017f + origin.sqrMagnitude;
            patrolTarget = origin + Vector2.right * 2f;
            retreatUntil = 0f;
            body = GetComponent<Rigidbody>();
            body.useGravity = false;
            body.constraints = RigidbodyConstraints.FreezePositionY | RigidbodyConstraints.FreezeRotation;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.linearVelocity = Vector3.zero;
            if (environment.InfiniteWorld != null)
                environment.InfiniteWorld.OriginShifted += HandleOriginShift;
        }

        private void FixedUpdate()
        {
            if (body == null || environment == null || environment.Agent == null || !environment.Agent.Vitals.IsAlive)
            {
                if (body != null) body.linearVelocity = Vector3.zero;
                return;
            }

            Vector2 position = Planar3D.ToPlanar(transform.position);
            Vector2 agentPosition = Planar3D.ToPlanar(environment.Agent.transform.position);
            Vector2 toAgent = agentPosition - position;
            if (!patroller && environment.Agent.IsSheltered)
            {
                chasing = false;
                body.linearVelocity = Planar3D.ToWorldDirection(-toAgent.normalized * MaxSpeed * 0.75f);
                return;
            }

            float detection = patroller
                ? environment.Settings.enemyDetectionRange * 0.7f
                : environment.Settings.enemyDetectionRange;
            bool retreating = Time.time < retreatUntil;
            if (!retreating)
            {
                if (!chasing && toAgent.magnitude <= detection) chasing = true;
                else if (chasing && toAgent.magnitude >= environment.Settings.enemyLoseRange) chasing = false;
            }

            Vector2 desired;
            float speed;
            if (retreating)
            {
                desired = -toAgent.normalized;
                speed = MaxSpeed * 1.15f;
            }
            else if (chasing)
            {
                if (!patroller && IsAgentNearArenaEdge())
                {
                    float turn = ((environment.CurrentSeed + name.GetHashCode()) & 1) == 0 ? 1f : -1f;
                    desired = new Vector2(-toAgent.y, toAgent.x).normalized * turn;
                    speed = MaxSpeed * 0.8f;
                }
                else
                {
                    desired = toAgent.normalized;
                    speed = MaxSpeed * (patroller ? 0.9f : 1f);
                }
            }
            else if (patroller)
            {
                float angle = Time.fixedTime * 0.5f + patrolPhase;
                patrolTarget = patrolOrigin + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * 2f;
                desired = (patrolTarget - position).normalized;
                speed = MaxSpeed * 0.55f;
            }
            else
            {
                desired = Vector2.zero;
                speed = 0f;
            }

            Vector3 worldDirection = Planar3D.ToWorldDirection(desired);
            Vector3 origin = transform.position + worldDirection * 0.5f;
            if (desired.sqrMagnitude > 0f
                && Physics.Raycast(origin, worldDirection, out RaycastHit hit, 0.9f, ~0, QueryTriggerInteraction.Ignore)
                && hit.collider.GetComponentInParent<ArenaSolid3D>() != null)
            {
                float turn = ((environment.CurrentSeed + name.GetHashCode()) & 1) == 0 ? 1f : -1f;
                desired = new Vector2(-desired.y, desired.x) * turn;
            }
            body.linearVelocity = Planar3D.ToWorldDirection(desired * speed);
        }

        private bool IsAgentNearArenaEdge()
        {
            if (environment.InfiniteWorld != null && environment.InfiniteWorld.IsInfinite)
                return false;
            Vector2 relative = Planar3D.ToPlanar(environment.Agent.transform.position - environment.transform.position);
            Vector2 half = environment.CurrentConfig.arenaSize * 0.5f;
            const float margin = 1.5f;
            return Mathf.Abs(relative.x) >= half.x - margin || Mathf.Abs(relative.y) >= half.y - margin;
        }

        private void OnCollisionStay(Collision collision)
        {
            SolAgent3D agent = collision.collider.GetComponentInParent<SolAgent3D>();
            if (agent == null || environment == null || agent != environment.Agent)
                return;
            float applied = agent.Vitals.TakeDamage(
                environment.Settings.enemyDamage * environment.CurrentConfig.enemyDamageMultiplier,
                Time.time);
            if (applied <= 0f || !agent.Vitals.IsAlive)
                return;
            Vector2 away = Planar3D.ToPlanar(agent.transform.position - transform.position);
            retreatUntil = Time.time + environment.Settings.enemyRetreatSeconds;
            chasing = false;
            agent.ApplyKnockback(away, environment.Settings.enemyKnockbackSpeed, environment.Settings.enemyKnockbackSeconds);
        }

        private void HandleOriginShift(Vector3 shift)
        {
            Vector2 planar = Planar3D.ToPlanarDirection(shift);
            patrolOrigin += planar;
            patrolTarget += planar;
        }

        private void OnDisable()
        {
            if (environment != null && environment.InfiniteWorld != null)
                environment.InfiniteWorld.OriginShifted -= HandleOriginShift;
            if (body != null)
                body.linearVelocity = Vector3.zero;
        }
    }
}
