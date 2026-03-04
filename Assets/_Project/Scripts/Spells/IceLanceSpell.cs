using DuckovProto.Combat;
using DuckovProto.Feedback;
using UnityEngine;

namespace DuckovProto.Spells
{
    public sealed class IceLanceSpell : MonoBehaviour
    {
        [SerializeField] private GameObject projectilePrefab;
        [SerializeField] private float speed = 30f;
        [SerializeField] private float lifetime = 2f;
        [SerializeField] private float spawnForwardOffset = 0.8f;
        [SerializeField] private float spawnUpOffset = 0.2f;
        [SerializeField] private int projectileLayer = 9;
        [SerializeField] private float damage = 3f;
        [SerializeField] private LayerMask damageMask = 1 << 8;
        [SerializeField] private float maxRange = 26f;
        [SerializeField] private float speedRampTime = 0.05f;
        [SerializeField] private float wobbleAmp = 0.03f;
        [SerializeField] private float wobbleFreq = 8f;
        [SerializeField] private float wobbleNoise = 0.2f;
        [SerializeField] private float pulseScale = 0.04f;
        [SerializeField] private LayerMask obstacleMask;
        [SerializeField] private MagicProjectileMotion.WallHitBehavior wallHitBehavior = MagicProjectileMotion.WallHitBehavior.StopAndExpire;
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
                SpawnMuzzleVfx(spawnPosition, direction);
            }

            GameObject projectile = projectilePrefab != null
                ? Instantiate(projectilePrefab, spawnPosition, Quaternion.LookRotation(direction, Vector3.up))
                : CreateRuntimeProjectile(spawnPosition, direction);

            projectile.layer = projectileLayer;

            Rigidbody rb = projectile.GetComponent<Rigidbody>();
            if (rb == null)
            {
                rb = projectile.AddComponent<Rigidbody>();
            }

            rb.useGravity = false;
            rb.isKinematic = false;
            rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.linearVelocity = Vector3.zero;

            Collider projectileCollider = projectile.GetComponent<Collider>();
            if (projectileCollider == null)
            {
                projectileCollider = projectile.AddComponent<CapsuleCollider>();
            }

            if (projectileCollider is CapsuleCollider capsule)
            {
                // Ice lance uses a long forward silhouette; keep hit volume tight to that shape.
                capsule.direction = 2; // Z axis
                capsule.center = new Vector3(0f, 0f, 0.03f);
                capsule.radius = 0.08f;
                capsule.height = 0.72f;
            }

            projectileCollider.isTrigger = true;

            ProjectileDamage projectileDamage = projectile.GetComponent<ProjectileDamage>();
            if (projectileDamage == null)
            {
                projectileDamage = projectile.AddComponent<ProjectileDamage>();
            }

            projectileDamage.Configure(
                damage,
                damageMask,
                ProjectileDamage.ImpactVfxStyle.Ice,
                0.03f,
                0.1f);

            MagicProjectileMotion motion = projectile.GetComponent<MagicProjectileMotion>();
            if (motion == null)
            {
                motion = projectile.AddComponent<MagicProjectileMotion>();
            }

            motion.Configure(
                wobbleAmp,
                wobbleFreq,
                wobbleNoise,
                speedRampTime,
                pulseScale,
                ResolveObstacleMask(obstacleMask),
                wallHitBehavior);
            motion.Init(direction, speed, maxRange);
            Destroy(projectile, lifetime);
        }

        private static GameObject CreateRuntimeProjectile(Vector3 position, Vector3 direction)
        {
            GameObject projectile = new GameObject("IceLanceProjectile");
            projectile.name = "IceLanceProjectile";
            projectile.transform.position = position;
            projectile.transform.rotation = Quaternion.LookRotation(direction, Vector3.up);

            ProjectileVisualFactory.CreateIceLanceVisual(projectile);

            TrailRenderer trail = projectile.AddComponent<TrailRenderer>();
            trail.time = 0.12f;
            trail.startWidth = 0.06f;
            trail.endWidth = 0.01f;
            trail.minVertexDistance = 0.03f;
            trail.material = new Material(Shader.Find("Sprites/Default"));
            trail.startColor = new Color(0.7f, 0.95f, 1f, 0.9f);
            trail.endColor = new Color(0.7f, 0.95f, 1f, 0f);

            GameObject fx = new GameObject("IceTrailFx");
            fx.transform.SetParent(projectile.transform, false);
            ParticleSystem ps = fx.AddComponent<ParticleSystem>();
            ParticleSystem.MainModule main = ps.main;
            main.playOnAwake = true;
            main.loop = true;
            main.startLifetime = 0.08f;
            main.startSpeed = 0.2f;
            main.startSize = 0.05f;
            main.startColor = new Color(0.75f, 0.95f, 1f, 0.9f);
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.maxParticles = 64;

            ParticleSystem.EmissionModule emission = ps.emission;
            emission.rateOverTime = 18f;

            ParticleSystem.ShapeModule shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 8f;
            shape.radius = 0.015f;

            return projectile;
        }

        private static void SpawnMuzzleVfx(Vector3 pos, Vector3 direction)
        {
            FeedbackUtils.SpawnParticleBurst(VfxPreset.Ice, pos + direction * 0.15f, 11, 1f, 0.8f, 1.25f, 0.8f, 0.65f);
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
}
