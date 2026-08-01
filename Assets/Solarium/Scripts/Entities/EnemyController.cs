using UnityEngine;

namespace Solarium
{
    public sealed class EnemyController : MonoBehaviour
    {
        private SolariumEnvironment environment;
        private Rigidbody2D body;
        private bool patroller;
        private bool chasing;
        private Vector2 patrolOrigin;
        private Vector2 patrolTarget;
        private float patrolPhase;
        private float retreatUntil;

        public Vector2 Velocity => body == null ? Vector2.zero : body.linearVelocity;
        public float MaxSpeed => environment == null
            ? 0f
            : environment.Settings.enemySpeed * environment.CurrentConfig.enemySpeedMultiplier;

        public void Initialize(SolariumEnvironment owner, bool isPatroller, Vector2 origin)
        {
            environment = owner;
            patroller = isPatroller;
            patrolOrigin = origin;
            patrolPhase = Mathf.Abs(owner.CurrentSeed % 997) * 0.017f + origin.sqrMagnitude;
            patrolTarget = origin + Vector2.right * 2f;
            body = GetComponent<Rigidbody2D>();
        }

        private void FixedUpdate()
        {
            if (environment == null || environment.Agent == null || !environment.Agent.Vitals.IsAlive)
            {
                if (body != null) body.linearVelocity = Vector2.zero;
                return;
            }

            Vector2 toAgent = environment.Agent.transform.position - transform.position;
            if (!patroller && environment.Agent.IsSheltered)
            {
                chasing = false;
                body.linearVelocity = -toAgent.normalized * MaxSpeed * 0.75f;
                return;
            }

            float detection = patroller
                ? environment.Settings.enemyDetectionRange * 0.7f
                : environment.Settings.enemyDetectionRange;
            bool retreating = Time.time < retreatUntil;
            if (!retreating)
            {
                if (!chasing && toAgent.magnitude <= detection)
                    chasing = true;
                else if (chasing && toAgent.magnitude >= environment.Settings.enemyLoseRange)
                    chasing = false;
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
                    // Do not pin the agent against a wall. Circling here preserves a
                    // meaningful escape choice while keeping the hunter threatening.
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
                desired = (patrolTarget - (Vector2)transform.position).normalized;
                speed = MaxSpeed * 0.55f;
            }
            else
            {
                desired = Vector2.zero;
                speed = 0f;
            }

            Vector2 origin = (Vector2)transform.position + desired * 0.5f;
            RaycastHit2D wallHit = Physics2D.Raycast(origin, desired, 0.9f);
            if (wallHit.collider != null && wallHit.collider.GetComponentInParent<ArenaSolid>() != null)
            {
                float turn = ((environment.CurrentSeed + name.GetHashCode()) & 1) == 0 ? 1f : -1f;
                desired = new Vector2(-desired.y, desired.x) * turn;
            }
            body.linearVelocity = desired * speed;
        }

        private bool IsAgentNearArenaEdge()
        {
            Vector2 relative = environment.Agent.transform.position - environment.transform.position;
            Vector2 half = environment.CurrentConfig.arenaSize * 0.5f;
            const float escapeMargin = 1.5f;
            return Mathf.Abs(relative.x) >= half.x - escapeMargin
                || Mathf.Abs(relative.y) >= half.y - escapeMargin;
        }

        private void OnCollisionStay2D(Collision2D collision)
        {
            SolAgent agent = collision.collider.GetComponentInParent<SolAgent>();
            if (agent == null || agent != environment.Agent)
                return;

            float applied = agent.Vitals.TakeDamage(
                environment.Settings.enemyDamage * environment.CurrentConfig.enemyDamageMultiplier,
                Time.time);
            if (applied <= 0f || !agent.Vitals.IsAlive)
                return;

            Vector2 awayFromEnemy = agent.transform.position - transform.position;
            retreatUntil = Time.time + environment.Settings.enemyRetreatSeconds;
            chasing = false;
            agent.ApplyKnockback(
                awayFromEnemy,
                environment.Settings.enemyKnockbackSpeed,
                environment.Settings.enemyKnockbackSeconds);
        }

        private void OnDrawGizmosSelected()
        {
            if (environment == null)
                return;
            Gizmos.color = new Color(1f, 0.2f, 0.2f, 0.2f);
            Gizmos.DrawWireSphere(transform.position, environment.Settings.enemyDetectionRange);
        }
    }
}
