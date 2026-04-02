using System.Collections;
using DuckovProto.Combat;
using DuckovProto.Feedback;
using DuckovProto.Spells.Data;
using UnityEngine;

namespace DuckovProto.Spells.Executors
{
    public sealed class ProjectileExecutor
    {
        private const float ProjectileRadius = 0.14f;
        private static readonly RaycastHit[] HitBuffer = new RaycastHit[32];

        public void Execute(MonoBehaviour host, SpellDefinitionSO spell, CastContext ctx)
        {
            if (host == null || spell == null || ctx == null || ctx.caster == null)
            {
                return;
            }

            host.StartCoroutine(ExecuteRoutine(spell, ctx));
        }

        private IEnumerator ExecuteRoutine(SpellDefinitionSO spell, CastContext ctx)
        {
            float speed = Mathf.Max(1f, spell.Speed);
            float range = Mathf.Max(1f, spell.Range);
            float lifetime = spell.Duration > 0f
                ? spell.Duration
                : Mathf.Clamp((range / speed) + 0.5f, 0.25f, 4f);
            int remainingBounces = Mathf.Max(0, spell.BounceCount);
            float damage = Mathf.Max(0f, spell.Damage);

            Vector3 direction = ctx.ResolveAimDirection(0.92f);
            Vector3 position = ctx.ResolveCastOrigin(0.92f) + (direction * 0.65f);
            GameObject visual = CreateWaterProjectileVisual(position);

            float elapsed = 0f;
            float traveled = 0f;

            while (elapsed < lifetime && traveled < range)
            {
                float dt = Time.deltaTime;
                elapsed += dt;
                float stepDistance = Mathf.Max(0.01f, speed * dt);

                if (TryFindHit(position, direction, stepDistance, ctx, out RaycastHit hit, out bool enemyHit, out bool wallHit))
                {
                    traveled += Mathf.Max(0f, hit.distance);
                    position = hit.point;

                    if (enemyHit)
                    {
                        if (TryApplyDamage(hit.collider, damage))
                        {
                            FeedbackUtils.SpawnParticleBurst(VfxPreset.Ice, position + Vector3.up * 0.03f, 14, 1f, 0.95f, 1f, 0.8f, 0.7f);
                            FeedbackUtils.SpawnImpactPuff(position + Vector3.up * 0.02f, 0.12f);
                        }

                        break;
                    }

                    if (wallHit)
                    {
                        FeedbackUtils.SpawnImpactPuff(position + Vector3.up * 0.02f, 0.1f);

                        if (remainingBounces > 0)
                        {
                            remainingBounces--;
                            Vector3 normal = hit.normal.sqrMagnitude > 0.0001f ? hit.normal.normalized : -direction;
                            direction = Vector3.Reflect(direction, normal).normalized;
                            position += direction * 0.08f;
                            continue;
                        }

                        break;
                    }
                }
                else
                {
                    position += direction * stepDistance;
                    traveled += stepDistance;
                }

                if (visual != null)
                {
                    visual.transform.position = position;
                    visual.transform.rotation = Quaternion.LookRotation(direction, Vector3.up);
                }

                yield return null;
            }

            if (visual != null)
            {
                Object.Destroy(visual);
            }
        }

        private static bool TryFindHit(
            Vector3 origin,
            Vector3 direction,
            float maxDistance,
            CastContext ctx,
            out RaycastHit bestHit,
            out bool enemyHit,
            out bool wallHit)
        {
            bestHit = default;
            enemyHit = false;
            wallHit = false;

            int mask = ctx.enemyMask.value | ctx.wallMask.value;
            if (mask == 0)
            {
                return false;
            }

            int hitCount = Physics.SphereCastNonAlloc(
                origin,
                ProjectileRadius,
                direction,
                HitBuffer,
                maxDistance,
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
                if (d < 0f || d > maxDistance + 0.001f)
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
                    wallHit = isWall;
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

            Health health = hitCollider.GetComponentInParent<Health>();
            if (health == null || health.IsDead)
            {
                return false;
            }

            health.TakeDamage(damage);
            return true;
        }

        private static GameObject CreateWaterProjectileVisual(Vector3 startPosition)
        {
            GameObject projectile = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            projectile.name = "Spell_WaterRicochet";
            projectile.transform.position = startPosition;
            projectile.transform.localScale = Vector3.one * 0.24f;
            projectile.layer = LayerMask.NameToLayer("Projectile");

            Collider col = projectile.GetComponent<Collider>();
            if (col != null)
            {
                Object.Destroy(col);
            }

            Renderer renderer = projectile.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.material = new Material(Shader.Find("Sprites/Default"));
                renderer.material.color = new Color(0.5f, 0.82f, 1f, 0.95f);
            }

            TrailRenderer trail = projectile.AddComponent<TrailRenderer>();
            trail.time = 0.12f;
            trail.startWidth = 0.08f;
            trail.endWidth = 0.015f;
            trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            trail.receiveShadows = false;
            trail.material = new Material(Shader.Find("Sprites/Default"));
            trail.startColor = new Color(0.72f, 0.92f, 1f, 0.85f);
            trail.endColor = new Color(0.72f, 0.92f, 1f, 0f);

            return projectile;
        }
    }
}
