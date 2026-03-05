using System.Collections;
using System.Collections.Generic;
using DuckovProto.Combat;
using DuckovProto.Feedback;
using DuckovProto.Spells.Data;
using UnityEngine;

namespace DuckovProto.Spells.Executors
{
    public sealed class ChannelExecutor
    {
        private readonly Dictionary<int, Coroutine> activeByHost = new Dictionary<int, Coroutine>();
        private readonly Collider[] overlapBuffer = new Collider[96];
        private readonly HashSet<Health> tickDamagedHealth = new HashSet<Health>();

        public void Execute(MonoBehaviour host, SpellDefinitionSO spell, CastContext ctx)
        {
            if (host == null || spell == null || ctx == null || ctx.caster == null)
            {
                return;
            }

            int hostKey = host.GetInstanceID();
            if (activeByHost.TryGetValue(hostKey, out Coroutine running) && running != null)
            {
                host.StopCoroutine(running);
                activeByHost.Remove(hostKey);
            }

            Coroutine routine = host.StartCoroutine(ChannelRoutine(host, hostKey, spell, ctx));
            activeByHost[hostKey] = routine;
        }

        private IEnumerator ChannelRoutine(MonoBehaviour host, int hostKey, SpellDefinitionSO spell, CastContext ctx)
        {
            float duration = spell.Duration > 0f ? spell.Duration : 2f;
            float tickInterval = spell.TickInterval > 0f ? spell.TickInterval : 1f;
            int tickCount = spell.TickCount > 0 ? spell.TickCount : 3;
            float damagePerTick = Mathf.Max(0f, spell.Damage);
            float range = Mathf.Max(1f, spell.Range);

            if (spell.RequireAimLockDuringCast)
            {
                if (ctx.aimLock == null || !ctx.aimLock.HasLock)
                {
                    ctx.ReportState("Channel canceled: aim lock required");
                    activeByHost.Remove(hostKey);
                    yield break;
                }

                ctx.aimLock.EnsureLockUntil(duration + 0.2f);
            }

            ctx.ReportState($"Channel: {spell.DisplayName}");

            float elapsed = 0f;
            float nextTickTime = 0f;
            int emittedTicks = 0;
            float loopVfxTimer = 0f;

            while (elapsed <= duration + 0.001f)
            {
                if (spell.RequireAimLockDuringCast)
                {
                    if (ctx.aimLock == null || !ctx.aimLock.HasLock)
                    {
                        ctx.ReportState("Channel canceled: aim lock lost");
                        break;
                    }

                    float remaining = Mathf.Max(0f, duration - elapsed);
                    ctx.aimLock.EnsureLockUntil(remaining + 0.2f);
                }

                Vector3 direction = ctx.ResolveAimDirection();
                Vector3 origin = ctx.ResolveCastOrigin(0.95f);

                loopVfxTimer -= Time.deltaTime;
                if (loopVfxTimer <= 0f)
                {
                    SpawnLoopVfx(origin, direction, range);
                    loopVfxTimer = 0.12f;
                }

                if (emittedTicks < tickCount && elapsed >= nextTickTime)
                {
                    int hits = ApplyFlameTickDamage(origin, direction, range, damagePerTick, ctx.enemyMask, ctx.caster);
                    SpawnTickVfx(origin, direction, range, hits);

                    emittedTicks++;
                    nextTickTime += Mathf.Max(0.05f, tickInterval);
                }

                elapsed += Time.deltaTime;
                yield return null;
            }

            if (activeByHost.TryGetValue(hostKey, out Coroutine current) && current != null)
            {
                activeByHost.Remove(hostKey);
            }
        }

        private int ApplyFlameTickDamage(
            Vector3 origin,
            Vector3 direction,
            float range,
            float damage,
            LayerMask enemyMask,
            Transform caster)
        {
            if (damage <= 0f || enemyMask.value == 0)
            {
                return 0;
            }

            tickDamagedHealth.Clear();
            int hitCountTotal = 0;

            const int samples = 6;
            for (int i = 1; i <= samples; i++)
            {
                float t = i / (float)samples;
                Vector3 center = origin + direction * (range * t);
                float radius = Mathf.Lerp(0.45f, 1.35f, t);
                int overlapCount = Physics.OverlapSphereNonAlloc(
                    center,
                    radius,
                    overlapBuffer,
                    enemyMask,
                    QueryTriggerInteraction.Collide);

                for (int n = 0; n < overlapCount; n++)
                {
                    Collider col = overlapBuffer[n];
                    if (col == null)
                    {
                        continue;
                    }

                    if (caster != null && col.transform.IsChildOf(caster))
                    {
                        continue;
                    }

                    Health health = col.GetComponentInParent<Health>();
                    if (health == null || health.IsDead || tickDamagedHealth.Contains(health))
                    {
                        continue;
                    }

                    health.TakeDamage(damage);
                    tickDamagedHealth.Add(health);
                    hitCountTotal++;
                }
            }

            return hitCountTotal;
        }

        private static void SpawnLoopVfx(Vector3 origin, Vector3 direction, float range)
        {
            Vector3 p = origin + direction * Mathf.Min(2f, range * 0.4f);
            FeedbackUtils.SpawnParticleBurst(VfxPreset.Fire, p, 7, 0.78f, 0.55f, 0.8f, 0.55f, 0.45f);
        }

        private static void SpawnTickVfx(Vector3 origin, Vector3 direction, float range, int hitCount)
        {
            Vector3 p = origin + direction * Mathf.Min(range, Mathf.Max(2f, range * 0.65f));
            FeedbackUtils.SpawnParticleBurst(VfxPreset.Fire, p, 12, 1.05f, 0.95f, 1.05f, 0.7f, 0.78f);
            FeedbackUtils.SpawnImpactPuff(p + Vector3.up * 0.04f, 0.1f);

            if (hitCount > 0)
            {
                FeedbackUtils.CameraShake(0.05f, 0.05f);
            }
        }
    }
}
