using DuckovProto.Combat;
using DuckovProto.Feedback;
using UnityEngine;

namespace DuckovProto.Spells
{
    public sealed class FireboltSpell : MonoBehaviour
    {
        [SerializeField] private GameObject projectilePrefab;
        [SerializeField] private float speed = 22f;
        [SerializeField] private float lifetime = 2.5f;
        [SerializeField] private float spawnForwardOffset = 0.8f;
        [SerializeField] private float spawnUpOffset = 0.2f;
        [SerializeField] private int projectileLayer = 9;
        [SerializeField] private float damage = 5f;
        [SerializeField] private LayerMask damageMask = 1 << 8;
        [SerializeField] private float maxRange = 18f;
        [SerializeField] private float speedRampTime = 0.12f;
        [SerializeField] private float wobbleAmp = 0.18f;
        [SerializeField] private float wobbleFreq = 2.9f;
        [SerializeField] private float wobbleNoise = 0.35f;
        [SerializeField] private float pulseScale = 0.10f;
        [SerializeField] private LayerMask obstacleMask;
        [SerializeField] private MagicProjectileMotion.WallHitBehavior wallHitBehavior = MagicProjectileMotion.WallHitBehavior.StopAndExpire;
        [SerializeField] private bool enableVfx = true;

        public void Cast(Transform caster)
        {
            Vector3 fallbackDirection = caster != null ? caster.forward : transform.forward;
            Cast(caster, fallbackDirection);
        }

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
                SpawnMuzzleVfx(caster.position + Vector3.up * 0.2f, direction);
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
                projectileCollider = projectile.AddComponent<SphereCollider>();
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
                ProjectileDamage.ImpactVfxStyle.Fire,
                0.04f,
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
            GameObject projectile = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            projectile.name = "FireboltProjectile";
            projectile.transform.position = position;
            projectile.transform.rotation = Quaternion.LookRotation(direction, Vector3.up);
            projectile.transform.localScale = Vector3.one * 0.32f;

            ProjectileVisualFactory.CreateFireballVisual(projectile);

            TrailRenderer trail = projectile.AddComponent<TrailRenderer>();
            trail.time = 0.4f;
            trail.startWidth = 0.16f;
            trail.endWidth = 0.04f;
            trail.minVertexDistance = 0.05f;
            trail.material = new Material(Shader.Find("Sprites/Default"));
            trail.startColor = new Color(1f, 0.7f, 0.2f, 0.95f);
            trail.endColor = new Color(1f, 0.2f, 0.05f, 0f);

            GameObject flameFx = new GameObject("FireTrailFx");
            flameFx.transform.SetParent(projectile.transform, false);
            ParticleSystem ps = flameFx.AddComponent<ParticleSystem>();
            ParticleSystem.MainModule main = ps.main;
            main.playOnAwake = true;
            main.loop = true;
            main.startLifetime = 0.16f;
            main.startSpeed = 0.3f;
            main.startSize = 0.13f;
            main.startColor = new Color(1f, 0.6f, 0.1f, 0.85f);
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.maxParticles = 80;

            ParticleSystem.EmissionModule emission = ps.emission;
            emission.rateOverTime = 14f;

            ParticleSystem.ShapeModule shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.06f;

            ParticleSystemRenderer renderer = ps.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            return projectile;
        }

        private static void SpawnMuzzleVfx(Vector3 pos, Vector3 direction)
        {
            FeedbackUtils.SpawnParticleBurst(VfxPreset.Fire, pos + direction * 0.4f, 24, 1.25f, 1.35f, 1.45f, 0.8f, 1f);
            FeedbackUtils.SpawnParticleBurst(VfxPreset.Fire, pos + direction * 0.2f, 12, 0.7f, 1f, 0.55f, 2f, 0.9f);
            SpawnDirectionalConeBurst(pos + direction * 0.32f, direction);
            FeedbackUtils.SpawnRing(pos, 1.15f, 0.18f, 0.02f, VfxPreset.Fire, 1.15f);
        }

        private static void SpawnDirectionalConeBurst(Vector3 position, Vector3 direction)
        {
            GameObject go = new GameObject("FireConeBurst");
            go.transform.position = position;
            go.transform.rotation = Quaternion.LookRotation(direction, Vector3.up);
            ParticleSystem ps = go.AddComponent<ParticleSystem>();

            var main = ps.main;
            main.playOnAwake = false;
            main.loop = false;
            main.startLifetime = 0.08f;
            main.startSpeed = 9f;
            main.startSize = 0.12f;
            main.startColor = new Color(1f, 0.9f, 0.6f, 0.95f);
            main.maxParticles = 32;
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            var emission = ps.emission;
            emission.enabled = false;

            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 12f;
            shape.radius = 0.04f;
            shape.length = 0.2f;

            ps.Emit(18);
            ps.Play();
            Destroy(go, 0.2f);
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
