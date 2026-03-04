using DuckovProto.Feedback;
using UnityEngine;

namespace DuckovProto.Combat
{
    public sealed class ProjectileDamage : MonoBehaviour
    {
        public enum ImpactVfxStyle
        {
            Default,
            Fire,
            Ice,
            Lightning
        }

        [SerializeField] private float damage = 5f;
        [SerializeField] private LayerMask hitMask = ~0;
        [SerializeField] private bool enableFeedback = true;
        [SerializeField] private float hitStopDuration = 0.035f;
        [SerializeField] private float hitStopCooldown = 0.12f;
        [SerializeField] private ImpactVfxStyle impactVfxStyle = ImpactVfxStyle.Default;

        private static float lastHitStopRealtime = -999f;

        public void Configure(float damageAmount, LayerMask mask)
        {
            damage = damageAmount;
            hitMask = mask;
        }

        public void Configure(
            float damageAmount,
            LayerMask mask,
            ImpactVfxStyle style,
            float hitStopDurationValue = 0.035f,
            float hitStopCooldownValue = 0.12f)
        {
            damage = damageAmount;
            hitMask = mask;
            impactVfxStyle = style;
            hitStopDuration = hitStopDurationValue;
            hitStopCooldown = hitStopCooldownValue;
        }

        private void OnCollisionEnter(Collision collision)
        {
            Vector3 hitPoint = transform.position;
            if (collision.contactCount > 0)
            {
                hitPoint = collision.GetContact(0).point;
            }

            TryDamage(collision.collider, hitPoint);
        }

        private void OnTriggerEnter(Collider other)
        {
            Vector3 hitPoint = other.ClosestPoint(transform.position);
            TryDamage(other, hitPoint);
        }

        private void TryDamage(Collider other, Vector3 hitPoint)
        {
            if (other == null)
            {
                return;
            }

            int otherLayerMask = 1 << other.gameObject.layer;
            if ((hitMask.value & otherLayerMask) == 0)
            {
                return;
            }

            Health health = other.GetComponentInParent<Health>();
            if (health != null)
            {
                health.TakeDamage(damage);
                if (enableFeedback)
                {
                    PlayImpactVfx(hitPoint);
                    TryHitStop();
                }
            }

            Destroy(gameObject);
        }

        private void TryHitStop()
        {
            float now = Time.unscaledTime;
            if (now - lastHitStopRealtime < hitStopCooldown)
            {
                return;
            }

            lastHitStopRealtime = now;
            FeedbackUtils.HitStop(hitStopDuration);
        }

        private void PlayImpactVfx(Vector3 hitPoint)
        {
            switch (impactVfxStyle)
            {
                case ImpactVfxStyle.Fire:
                    FeedbackUtils.SpawnParticleBurst(VfxPreset.Fire, hitPoint, 20, 1.2f, 1.1f, 1.15f, 1f, 1.1f);
                    FeedbackUtils.SpawnParticleBurst(VfxPreset.Fire, hitPoint + Vector3.up * 0.04f, 8, 0.7f, 0.95f, 0.55f, 2.2f, 1f);
                    FeedbackUtils.SpawnRing(hitPoint, 0.95f, 0.18f, 0.02f, VfxPreset.Fire, 1.2f);
                    FeedbackUtils.SpawnParticleBurst(VfxPreset.Fire, hitPoint + Vector3.up * 0.03f, 7, 0.5f, 0.45f, 0.35f, 3.6f, 0.6f);
                    FeedbackUtils.CameraShake(0.11f, 0.11f);
                    break;

                case ImpactVfxStyle.Ice:
                    FeedbackUtils.SpawnParticleBurst(VfxPreset.Ice, hitPoint, 12, 1.05f, 0.95f, 1.2f, 1f, 0.75f);
                    FeedbackUtils.SpawnParticleBurst(VfxPreset.Ice, hitPoint + Vector3.up * 0.03f, Random.Range(6, 13), 0.9f, 0.55f, 1.35f, 0.9f, 0.75f);
                    FeedbackUtils.SpawnImpactPuff(hitPoint, 0.16f);
                    StartCoroutine(SpawnIceMicroSpikes(hitPoint));
                    break;

                case ImpactVfxStyle.Lightning:
                    FeedbackUtils.SpawnParticleBurst(VfxPreset.Lightning, hitPoint, 14, 1.15f, 1f, 1.2f, 0.95f, 0.85f);
                    FeedbackUtils.SpawnParticleBurst(VfxPreset.Lightning, hitPoint + Vector3.up * 0.02f, 6, 0.85f, 0.65f, 0.9f, 0.65f, 0.6f);
                    FeedbackUtils.SpawnImpactPuff(hitPoint, 0.14f);
                    break;

                default:
                    FeedbackUtils.SpawnImpactPuff(hitPoint, 0.2f);
                    break;
            }
        }

        private System.Collections.IEnumerator SpawnIceMicroSpikes(Vector3 hitPoint)
        {
            const int count = 3;
            GameObject[] spikes = new GameObject[count];
            Vector3[] dirs = new Vector3[count];
            float[] scales = new float[count];

            for (int i = 0; i < count; i++)
            {
                Vector2 d2 = Random.insideUnitCircle.normalized;
                if (d2.sqrMagnitude < 0.001f)
                {
                    d2 = Vector2.right;
                }

                Vector3 dir = new Vector3(d2.x, Random.Range(0.2f, 0.45f), d2.y).normalized;
                dirs[i] = dir;
                scales[i] = Random.Range(0.08f, 0.13f);

                GameObject spike = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                spike.name = "IceHitSpike";
                spike.transform.position = hitPoint + Vector3.up * 0.04f;
                spike.transform.localScale = new Vector3(scales[i] * 0.22f, scales[i], scales[i] * 0.22f);
                spike.transform.rotation = Quaternion.FromToRotation(Vector3.up, dir);
                Collider col = spike.GetComponent<Collider>();
                if (col != null)
                {
                    Destroy(col);
                }

                Renderer renderer = spike.GetComponent<Renderer>();
                if (renderer != null)
                {
                    renderer.material = new Material(Shader.Find("Sprites/Default"));
                    renderer.material.color = new Color(0.82f, 0.96f, 1f, 0.9f);
                }

                spikes[i] = spike;
            }

            float duration = 0.12f;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                for (int i = 0; i < spikes.Length; i++)
                {
                    if (spikes[i] == null)
                    {
                        continue;
                    }

                    spikes[i].transform.position = hitPoint + Vector3.up * 0.04f + (dirs[i] * (0.22f * t));
                    Vector3 s = spikes[i].transform.localScale;
                    s.y = Mathf.Lerp(scales[i], 0f, t);
                    spikes[i].transform.localScale = s;
                    Renderer renderer = spikes[i].GetComponent<Renderer>();
                    if (renderer != null && renderer.material != null)
                    {
                        Color c = renderer.material.color;
                        c.a = 1f - t;
                        renderer.material.color = c;
                    }
                }

                yield return null;
            }

            for (int i = 0; i < spikes.Length; i++)
            {
                if (spikes[i] != null)
                {
                    Destroy(spikes[i]);
                }
            }
        }
    }
}
