using System.Collections;
using DuckovProto.Feedback;
using UnityEngine;

namespace DuckovProto.Spells
{
    public sealed class BlinkSpell : MonoBehaviour
    {
        [SerializeField] private float blinkDistance = 5f;
        [SerializeField] private float groundRayStartHeight = 8f;
        [SerializeField] private float groundRayDistance = 20f;
        [SerializeField] private float groundOffset = 0f;
        [SerializeField] private LayerMask groundMask = 1 << 7;
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

            direction.y = 0f;
            if (direction.sqrMagnitude <= 0.0001f)
            {
                direction = Vector3.forward;
            }

            direction.Normalize();
            Vector3 desired = caster.position + (direction * blinkDistance);
            Vector3 rayStart = desired + (Vector3.up * groundRayStartHeight);
            Vector3 startPosition = caster.position;

            if (!Physics.Raycast(rayStart, Vector3.down, out RaycastHit hit, groundRayDistance, groundMask))
            {
                Debug.Log("Blink canceled: no ground hit", this);
                return;
            }

            Vector3 destination = hit.point + (Vector3.up * groundOffset);
            Rigidbody rb = caster.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.position = destination;
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }
            else
            {
                caster.position = destination;
            }

            if (enableVfx)
            {
                SpawnBlinkBurst(startPosition + Vector3.up * 0.05f, direction);
                SpawnBlinkBurst(destination + Vector3.up * 0.05f, direction);
                FeedbackUtils.CameraShake(0.05f, 0.04f);
            }

            Debug.Log($"Blink to {destination}", this);
        }

        private void SpawnBlinkBurst(Vector3 pos, Vector3 direction)
        {
            FeedbackUtils.SpawnParticleBurst(VfxPreset.Arcane, pos, 16, 1.1f, 1f, 1f, 0.35f, 0.7f);
            FeedbackUtils.SpawnParticleBurst(VfxPreset.Arcane, pos, 8, 1.35f, 0.75f, 1f, 0.25f, 0.55f);
            FeedbackUtils.SpawnImpactPuff(pos, 0.14f);
            FeedbackUtils.SpawnRing(pos, 0.9f, 0.12f, 0.01f, VfxPreset.Arcane, 1f);
            StartCoroutine(SpawnAfterImages(pos, direction));
        }

        private static IEnumerator SpawnAfterImages(Vector3 origin, Vector3 direction)
        {
            Vector3 flatDir = direction;
            flatDir.y = 0f;
            if (flatDir.sqrMagnitude < 0.001f)
            {
                flatDir = Vector3.forward;
            }

            flatDir.Normalize();
            for (int i = 0; i < 3; i++)
            {
                GameObject orb = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                orb.name = "BlinkAfterImage";
                orb.transform.position = origin + flatDir * (0.12f + i * 0.16f) + Vector3.up * 0.04f;
                orb.transform.localScale = Vector3.one * 0.08f;

                Collider col = orb.GetComponent<Collider>();
                if (col != null)
                {
                    Destroy(col);
                }

                Renderer renderer = orb.GetComponent<Renderer>();
                if (renderer != null)
                {
                    renderer.material = new Material(Shader.Find("Sprites/Default"));
                    renderer.material.color = new Color(0.9f, 0.85f, 1f, 0.75f);
                }

                Destroy(orb, 0.12f);
            }

            yield return null;
        }
    }
}
