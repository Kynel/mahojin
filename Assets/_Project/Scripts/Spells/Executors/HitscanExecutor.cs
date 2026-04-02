using DuckovProto.Combat;
using DuckovProto.Feedback;
using DuckovProto.Spells.Data;
using UnityEngine;

namespace DuckovProto.Spells.Executors
{
    public sealed class HitscanExecutor
    {
        private const float RailRadius = 0.2f;
        private static readonly RaycastHit[] HitBuffer = new RaycastHit[32];

        public void Execute(MonoBehaviour host, SpellDefinitionSO spell, CastContext ctx)
        {
            if (spell == null || ctx == null || ctx.caster == null)
            {
                return;
            }

            float range = Mathf.Max(1f, spell.Range > 0f ? spell.Range : 45f);
            float damage = Mathf.Max(0f, spell.Damage);

            Vector3 origin = ctx.ResolveCastOrigin(0.95f);
            Vector3 direction = ctx.ResolveAimDirection();
            Vector3 endPoint = origin + direction * range;

            bool damaged = false;
            if (TryFindFirstHit(origin, direction, range, ctx, out RaycastHit hit, out bool enemyHit))
            {
                endPoint = hit.point;
                if (enemyHit)
                {
                    damaged = TryApplyDamage(hit.collider, damage);
                }
            }

            FeedbackUtils.SpawnLightningLine(origin, endPoint, 0.1f, 0.06f);
            FeedbackUtils.SpawnParticleBurst(VfxPreset.Lightning, endPoint + Vector3.up * 0.03f, 12, 1.15f, 1f, 1f, 0.85f, 0.65f);
            if (damaged)
            {
                FeedbackUtils.CameraShake(0.06f, 0.07f);
            }
        }

        private static bool TryFindFirstHit(
            Vector3 origin,
            Vector3 direction,
            float range,
            CastContext ctx,
            out RaycastHit bestHit,
            out bool enemyHit)
        {
            bestHit = default;
            enemyHit = false;

            int mask = ctx.enemyMask.value | ctx.wallMask.value;
            if (mask == 0)
            {
                return false;
            }

            int hitCount = Physics.SphereCastNonAlloc(
                origin,
                RailRadius,
                direction,
                HitBuffer,
                range,
                mask,
                QueryTriggerInteraction.Ignore);
            if (hitCount <= 0)
            {
                return false;
            }

            bool found = false;
            float nearestDistance = float.MaxValue;
            for (int i = 0; i < hitCount; i++)
            {
                RaycastHit hit = HitBuffer[i];
                Collider col = hit.collider;
                if (col == null)
                {
                    continue;
                }

                if (ctx.caster != null && col.transform.IsChildOf(ctx.caster))
                {
                    continue;
                }

                float d = hit.distance;
                if (d < 0f || d > range + 0.001f)
                {
                    continue;
                }

                int layerMask = 1 << col.gameObject.layer;
                bool isEnemy = (ctx.enemyMask.value & layerMask) != 0;
                bool isWall = (ctx.wallMask.value & layerMask) != 0;
                if (!isEnemy && !isWall)
                {
                    continue;
                }

                if (!found || d < nearestDistance)
                {
                    found = true;
                    nearestDistance = d;
                    bestHit = hit;
                    enemyHit = isEnemy;
                }
            }

            return found;
        }

        private static bool TryApplyDamage(Collider hitCollider, float damage)
        {
            if (hitCollider == null || damage <= 0f)
            {
                return false;
            }

            Health health = HealthCache.GetHealth(hitCollider);
            if (health == null || health.IsDead)
            {
                return false;
            }

            health.TakeDamage(damage);
            return true;
        }
    }
}
