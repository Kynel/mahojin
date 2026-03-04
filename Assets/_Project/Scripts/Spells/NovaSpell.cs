using System.Collections;
using System.Collections.Generic;
using DuckovProto.Combat;
using DuckovProto.Feedback;
using UnityEngine;

namespace DuckovProto.Spells
{
    public sealed class NovaSpell : MonoBehaviour
    {
        [SerializeField] private float radius = 3f;
        [SerializeField] private float damage = 4f;
        [SerializeField] private LayerMask enemyMask = 1 << 8;
        [SerializeField] private float visualDuration = 0.2f;
        [SerializeField] private bool enableVfx = true;

        public void Cast(Transform caster)
        {
            if (caster == null)
            {
                caster = transform;
            }

            Collider[] hits = Physics.OverlapSphere(caster.position, radius, enemyMask);
            HashSet<Health> unique = new HashSet<Health>();
            int puffCount = 0;

            for (int i = 0; i < hits.Length; i++)
            {
                Health health = hits[i].GetComponentInParent<Health>();
                if (health == null || unique.Contains(health))
                {
                    continue;
                }

                unique.Add(health);
                health.TakeDamage(damage);
                if (enableVfx && puffCount < 5)
                {
                    FeedbackUtils.SpawnImpactPuff(health.transform.position + Vector3.up * 0.2f, 0.16f);
                    puffCount++;
                }
            }

            if (enableVfx)
            {
                SpawnVisual(caster.position);
            }
            Debug.Log($"Nova hit {unique.Count} enemies", this);
        }

        private void SpawnVisual(Vector3 position)
        {
            FeedbackUtils.SpawnRing(position, radius, 0.3f, 0.03f, VfxPreset.Arcane, 1.2f);
            FeedbackUtils.SpawnParticleBurst(VfxPreset.Arcane, position + Vector3.up * 0.05f, 26, 1.3f, 1.2f, 0.95f, 0.95f, 1.3f);
            FeedbackUtils.SpawnParticleBurst(VfxPreset.Arcane, position + Vector3.up * 0.02f, 14, 0.8f, 1.3f, 0.65f, 1.2f, 1.1f);
            StartCoroutine(AnimateRangeRing(position));
        }

        private IEnumerator AnimateRangeRing(Vector3 center)
        {
            GameObject go = new GameObject("NovaRangeRing");
            LineRenderer lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace = true;
            lr.loop = true;
            lr.positionCount = 56;
            lr.widthMultiplier = 0.045f;
            lr.material = new Material(Shader.Find("Sprites/Default"));
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.receiveShadows = false;

            float expandTime = 0.15f;
            float fadeTime = Mathf.Max(0.15f, visualDuration - expandTime);
            float total = expandTime + fadeTime;
            float elapsed = 0f;

            while (elapsed < total)
            {
                elapsed += Time.deltaTime;
                float r;
                float alpha;
                if (elapsed <= expandTime)
                {
                    float t = Mathf.Clamp01(elapsed / expandTime);
                    r = Mathf.Lerp(radius * 0.45f, radius, t);
                    alpha = 0.9f;
                }
                else
                {
                    float t = Mathf.Clamp01((elapsed - expandTime) / fadeTime);
                    r = radius;
                    alpha = 1f - t;
                }

                Color c = new Color(0.8f, 0.9f, 1f, alpha);
                lr.startColor = c;
                lr.endColor = c;

                for (int i = 0; i < lr.positionCount; i++)
                {
                    float a = ((float)i / lr.positionCount) * Mathf.PI * 2f;
                    Vector3 p = center + new Vector3(Mathf.Cos(a) * r, 0.03f, Mathf.Sin(a) * r);
                    lr.SetPosition(i, p);
                }

                yield return null;
            }

            Destroy(go);
        }
    }
}
