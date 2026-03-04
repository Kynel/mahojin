using System.Collections;
using System.Collections.Generic;
using DuckovProto.Combat;
using DuckovProto.Feedback;
using UnityEngine;

namespace DuckovProto.Spells
{
    public sealed class ChainLightningSpell : MonoBehaviour
    {
        [SerializeField] private float speed = 25f;
        [SerializeField] private float lifetime = 1.5f;
        [SerializeField] private float spawnForwardOffset = 0.8f;
        [SerializeField] private float spawnUpOffset = 0.2f;
        [SerializeField] private int projectileLayer = 9;
        [SerializeField] private float maxRange = 22f;
        [SerializeField] private float speedRampTime = 0.08f;
        [SerializeField] private float wobbleAmp = 0.12f;
        [SerializeField] private float wobbleFreq = 8f;
        [SerializeField] private float wobbleNoise = 0.35f;
        [SerializeField] private float pulseScale = 0.06f;
        [SerializeField] private LayerMask obstacleMask;
        [SerializeField] private MagicProjectileMotion.WallHitBehavior wallHitBehavior = MagicProjectileMotion.WallHitBehavior.StopAndExpire;
        [SerializeField] private LayerMask enemyMask = 1 << 8;
        [SerializeField] private float chainRadius = 10f;
        [SerializeField] private int chainCount = 3;
        [SerializeField] private float chainDamage = 2f;
        [SerializeField] private float chainDelay = 0.05f;
        [SerializeField] private bool enableVfx = true;

        public void Cast(Transform caster, Vector3 direction)
        {
            if (caster == null)
            {
                caster = transform;
            }

            if (direction.sqrMagnitude <= 0.0001f)
            {
                direction = caster.forward;
            }

            direction = direction.normalized;
            Vector3 spawnPosition = caster.position + (direction * spawnForwardOffset) + (Vector3.up * spawnUpOffset);

            if (enableVfx)
            {
                FeedbackUtils.SpawnParticleBurst(VfxPreset.Lightning, spawnPosition, 16, 1.1f, 1f, 1.15f, 0.75f, 1f);
                FeedbackUtils.SpawnRing(caster.position, 0.9f, 0.14f, 0.02f, VfxPreset.Lightning, 1f);
            }

            GameObject projectile = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            projectile.name = "ChainBoltProjectile";
            projectile.transform.position = spawnPosition;
            projectile.transform.rotation = Quaternion.LookRotation(direction, Vector3.up);
            projectile.layer = projectileLayer;
            ProjectileVisualFactory.CreateLightningOrbVisual(projectile);

            Collider col = projectile.GetComponent<Collider>();
            col.isTrigger = true;

            Rigidbody rb = projectile.AddComponent<Rigidbody>();
            rb.useGravity = false;
            rb.isKinematic = false;
            rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.linearVelocity = Vector3.zero;

            TrailRenderer trail = projectile.AddComponent<TrailRenderer>();
            trail.time = 0.14f;
            trail.startWidth = 0.06f;
            trail.endWidth = 0f;
            trail.material = new Material(Shader.Find("Sprites/Default"));
            trail.startColor = new Color(1f, 0.95f, 0.2f, 0.95f);
            trail.endColor = new Color(1f, 1f, 1f, 0f);

            ChainBoltProjectile bolt = projectile.AddComponent<ChainBoltProjectile>();
            bolt.Initialize(this, enemyMask, lifetime);

            MagicProjectileMotion motion = projectile.AddComponent<MagicProjectileMotion>();
            motion.Configure(
                wobbleAmp,
                wobbleFreq,
                wobbleNoise,
                speedRampTime,
                pulseScale,
                ResolveObstacleMask(obstacleMask),
                wallHitBehavior);
            motion.Init(direction, speed, maxRange);
        }

        internal void OnChainBoltHit(Health firstTarget, Vector3 hitPoint)
        {
            if (firstTarget == null)
            {
                return;
            }

            StartCoroutine(ChainRoutine(firstTarget, hitPoint));
        }

        private IEnumerator ChainRoutine(Health firstTarget, Vector3 startPoint)
        {
            HashSet<Health> hit = new HashSet<Health>();
            Health current = firstTarget;
            Vector3 from = startPoint;

            for (int i = 0; i < chainCount && current != null; i++)
            {
                if (!hit.Contains(current))
                {
                    hit.Add(current);
                    current.TakeDamage(chainDamage);

                    if (enableVfx)
                    {
                        Vector3 targetPos = current.transform.position + Vector3.up * 0.3f;
                        FeedbackUtils.SpawnLightningLine(from, targetPos, 0.08f, 0.06f);
                        FeedbackUtils.SpawnParticleBurst(VfxPreset.Lightning, targetPos, 14, 1.1f, 0.95f, 1.1f, 0.75f, 0.7f);
                        FeedbackUtils.SpawnParticleBurst(VfxPreset.Lightning, targetPos + Vector3.up * 0.02f, 6, 1.25f, 0.65f, 0.9f, 0.45f, 0.45f);
                        FeedbackUtils.SpawnImpactPuff(targetPos, 0.12f);
                        from = targetPos;
                    }
                }

                if (i >= chainCount - 1)
                {
                    break;
                }

                Health next = FindNearestTarget(current.transform.position, hit);
                current = next;

                if (current != null && chainDelay > 0f)
                {
                    yield return new WaitForSeconds(chainDelay);
                }
            }
        }

        private Health FindNearestTarget(Vector3 center, HashSet<Health> excluded)
        {
            Collider[] colliders = Physics.OverlapSphere(center, chainRadius, enemyMask);
            float bestSqr = float.MaxValue;
            Health best = null;

            for (int i = 0; i < colliders.Length; i++)
            {
                Health health = colliders[i].GetComponentInParent<Health>();
                if (health == null || excluded.Contains(health))
                {
                    continue;
                }

                float sqr = (health.transform.position - center).sqrMagnitude;
                if (sqr < bestSqr)
                {
                    bestSqr = sqr;
                    best = health;
                }
            }

            return best;
        }

        private static LayerMask ResolveObstacleMask(LayerMask configuredMask)
        {
            if (configuredMask.value != 0)
            {
                return configuredMask;
            }

            int wall = LayerMask.NameToLayer("Wall");
            if (wall < 0)
            {
                return 0;
            }

            return 1 << wall;
        }
    }

    public sealed class ChainBoltProjectile : MonoBehaviour
    {
        private ChainLightningSpell owner;
        private LayerMask enemyMask;
        private bool resolved;

        public void Initialize(ChainLightningSpell spell, LayerMask mask, float lifetime)
        {
            owner = spell;
            enemyMask = mask;
            Destroy(gameObject, lifetime);
        }

        private void OnTriggerEnter(Collider other)
        {
            TryResolve(other, other != null ? other.ClosestPoint(transform.position) : transform.position);
        }

        private void OnCollisionEnter(Collision collision)
        {
            Vector3 hitPoint = transform.position;
            if (collision.contactCount > 0)
            {
                hitPoint = collision.GetContact(0).point;
            }

            TryResolve(collision.collider, hitPoint);
        }

        private void TryResolve(Collider other, Vector3 hitPoint)
        {
            if (resolved || other == null)
            {
                return;
            }

            int layerMask = 1 << other.gameObject.layer;
            if ((enemyMask.value & layerMask) == 0)
            {
                return;
            }

            Health health = other.GetComponentInParent<Health>();
            if (health == null || owner == null)
            {
                return;
            }

            resolved = true;
            owner.OnChainBoltHit(health, hitPoint);
            Destroy(gameObject);
        }
    }
}
