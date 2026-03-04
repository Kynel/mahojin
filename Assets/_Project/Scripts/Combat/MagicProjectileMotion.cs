using System;
using DuckovProto.Feedback;
using UnityEngine;

namespace DuckovProto.Combat
{
    [RequireComponent(typeof(Collider))]
    public sealed class MagicProjectileMotion : MonoBehaviour
    {
        public enum WallHitBehavior
        {
            StopAndExpire,
            Destroy,
            Stick,
            Ignore
        }

        [Header("Motion")]
        [SerializeField] private float wobbleAmp = 0.12f;
        [SerializeField] private float wobbleFreq = 4f;
        [SerializeField] private float wobbleNoise = 0.35f;
        [SerializeField] private float speedRampTime = 0.1f;
        [SerializeField] private float pulseScale = 0.08f;

        [Header("Collision")]
        [SerializeField] private LayerMask obstacleMask;
        [SerializeField] private WallHitBehavior wallHitBehavior = WallHitBehavior.StopAndExpire;
        [SerializeField] private float expireDelay = 0.3f;
        [SerializeField] private bool enableExpireVfx = true;

        private Rigidbody rb;
        private Collider projectileCollider;
        private Vector3 startPos;
        private Vector3 baseScale;
        private Vector3 direction;
        private Vector3 rightAxis;
        private float speed;
        private float maxRange;
        private float elapsed;
        private float traveledDistance;
        private float seedA;
        private float seedB;
        private bool initialized;
        private bool expiring;

        public event Action<MagicProjectileMotion> OnMaxRangeReached;
        public event Action<MagicProjectileMotion, Collider> OnObstacleHit;

        private void Awake()
        {
            rb = GetComponent<Rigidbody>();
            projectileCollider = GetComponent<Collider>();
            baseScale = transform.localScale;
            seedA = UnityEngine.Random.Range(0f, 100f);
            seedB = UnityEngine.Random.Range(0f, 100f);
        }

        public void Configure(
            float wobbleAmpValue,
            float wobbleFreqValue,
            float wobbleNoiseValue,
            float speedRampTimeValue,
            float pulseScaleValue,
            LayerMask obstacleMaskValue,
            WallHitBehavior wallBehaviorValue)
        {
            wobbleAmp = Mathf.Max(0f, wobbleAmpValue);
            wobbleFreq = Mathf.Max(0f, wobbleFreqValue);
            wobbleNoise = Mathf.Clamp01(wobbleNoiseValue);
            speedRampTime = Mathf.Max(0f, speedRampTimeValue);
            pulseScale = Mathf.Max(0f, pulseScaleValue);
            obstacleMask = obstacleMaskValue;
            wallHitBehavior = wallBehaviorValue;
        }

        public void Init(Vector3 directionValue, float speedValue, float maxRangeValue)
        {
            direction = directionValue.sqrMagnitude > 0.0001f
                ? directionValue.normalized
                : transform.forward;
            speed = Mathf.Max(0f, speedValue);
            maxRange = Mathf.Max(0.1f, maxRangeValue);

            if (rb == null)
            {
                rb = GetComponent<Rigidbody>();
            }

            if (rb != null)
            {
                rb.useGravity = false;
                rb.isKinematic = false;
                rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
                rb.interpolation = RigidbodyInterpolation.Interpolate;
                rb.linearVelocity = Vector3.zero;
            }

            startPos = rb != null ? rb.position : transform.position;
            rightAxis = Vector3.Cross(Vector3.up, direction);
            if (rightAxis.sqrMagnitude <= 0.0001f)
            {
                rightAxis = Vector3.Cross(Vector3.forward, direction);
            }
            rightAxis.Normalize();
            elapsed = 0f;
            traveledDistance = 0f;
            expiring = false;
            initialized = true;
        }

        private void Update()
        {
            if (!initialized || expiring || pulseScale <= 0f)
            {
                return;
            }

            float p = 1f + (Mathf.Sin((Time.time + seedA) * (wobbleFreq + 1.5f)) * pulseScale);
            transform.localScale = baseScale * p;
        }

        private void FixedUpdate()
        {
            if (!initialized || expiring)
            {
                return;
            }

            elapsed += Time.fixedDeltaTime;
            float ramp01 = speedRampTime <= 0f
                ? 1f
                : Mathf.Clamp01(elapsed / Mathf.Max(0.0001f, speedRampTime));
            float currentSpeed = speed * ramp01;

            traveledDistance += currentSpeed * Time.fixedDeltaTime;
            if (traveledDistance >= maxRange)
            {
                traveledDistance = maxRange;
                MoveToComputedPosition();
                HandleMaxRangeReached();
                return;
            }

            MoveToComputedPosition();
        }

        private void OnTriggerEnter(Collider other)
        {
            TryHandleObstacleHit(other);
        }

        private void OnCollisionEnter(Collision collision)
        {
            TryHandleObstacleHit(collision.collider);
        }

        private void TryHandleObstacleHit(Collider other)
        {
            if (!initialized || expiring || other == null)
            {
                return;
            }

            int hitLayer = 1 << other.gameObject.layer;
            if ((obstacleMask.value & hitLayer) == 0)
            {
                return;
            }

            OnObstacleHit?.Invoke(this, other);
            Vector3 hitPoint = other.ClosestPoint(transform.position);
            HandleWallHit(hitPoint);
        }

        private void MoveToComputedPosition()
        {
            Vector3 linearPos = startPos + direction * traveledDistance;
            float wobblePhase = (elapsed + seedA) * wobbleFreq;
            float wobbleSin = Mathf.Sin(wobblePhase * Mathf.PI * 2f);
            float wobbleCos = Mathf.Cos((wobblePhase + seedB) * Mathf.PI);
            float wobble = (wobbleSin * 0.75f + wobbleCos * 0.25f) * wobbleAmp;
            wobble *= (1f - wobbleNoise) + (Mathf.PerlinNoise(seedA + elapsed, seedB) * wobbleNoise);

            Vector3 nextPos = linearPos + rightAxis * wobble;
            if (rb != null)
            {
                nextPos.y = rb.position.y;
                rb.MovePosition(nextPos);
                rb.MoveRotation(Quaternion.LookRotation(direction, Vector3.up));
            }
            else
            {
                transform.position = nextPos;
                transform.rotation = Quaternion.LookRotation(direction, Vector3.up);
            }
        }

        private void HandleMaxRangeReached()
        {
            OnMaxRangeReached?.Invoke(this);
            HandleWallHit(transform.position);
        }

        private void HandleWallHit(Vector3 hitPoint)
        {
            switch (wallHitBehavior)
            {
                case WallHitBehavior.Ignore:
                    return;

                case WallHitBehavior.Destroy:
                    Destroy(gameObject);
                    return;

                case WallHitBehavior.Stick:
                    EnterExpireState(false, hitPoint);
                    if (rb != null)
                    {
                        rb.isKinematic = true;
                    }
                    return;

                default:
                    EnterExpireState(true, hitPoint);
                    return;
            }
        }

        private void EnterExpireState(bool destroyAfterDelay, Vector3 hitPoint)
        {
            if (expiring)
            {
                return;
            }

            expiring = true;
            initialized = false;
            transform.localScale = baseScale;

            if (rb != null)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }

            if (projectileCollider != null)
            {
                projectileCollider.enabled = false;
            }

            if (enableExpireVfx)
            {
                FeedbackUtils.SpawnImpactPuff(hitPoint + Vector3.up * 0.03f, 0.12f);
            }

            if (destroyAfterDelay)
            {
                StartCoroutine(FadeAndDestroyRoutine(Mathf.Clamp(expireDelay, 0.2f, 0.4f)));
            }
        }

        private System.Collections.IEnumerator FadeAndDestroyRoutine(float duration)
        {
            Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
            TrailRenderer[] trails = GetComponentsInChildren<TrailRenderer>(true);
            float[] trailStart = new float[trails.Length];
            float[] trailEnd = new float[trails.Length];
            for (int i = 0; i < trails.Length; i++)
            {
                trailStart[i] = trails[i].startWidth;
                trailEnd[i] = trails[i].endWidth;
            }

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / Mathf.Max(0.0001f, duration));
                float a = 1f - t;
                transform.localScale = Vector3.Lerp(baseScale, baseScale * 0.45f, t);

                for (int i = 0; i < renderers.Length; i++)
                {
                    Renderer r = renderers[i];
                    if (r == null || r.material == null)
                    {
                        continue;
                    }

                    Color c = r.material.color;
                    c.a = a;
                    r.material.color = c;
                }

                for (int i = 0; i < trails.Length; i++)
                {
                    if (trails[i] == null)
                    {
                        continue;
                    }

                    trails[i].startWidth = Mathf.Lerp(trailStart[i], 0f, t);
                    trails[i].endWidth = Mathf.Lerp(trailEnd[i], 0f, t);
                }

                yield return null;
            }

            Destroy(gameObject);
        }
    }
}
