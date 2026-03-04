using DuckovProto.Combat;
using DuckovProto.Player;
using UnityEngine;

namespace DuckovProto.AI
{
    [DisallowMultipleComponent]
    public sealed class EnemyContactDamage : MonoBehaviour
    {
        [SerializeField] private float dps = 12f;
        [SerializeField] private float tickInterval = 0.25f;
        [SerializeField] private float contactRange = 1.35f;
        [SerializeField] private Transform rangeOrigin;
        [SerializeField] private string playerTag = "Player";

        private Transform targetTransform;
        private Health targetHealth;
        private float nextTickTime;

        private void OnValidate()
        {
            dps = Mathf.Max(0f, dps);
            tickInterval = Mathf.Max(0.01f, tickInterval);
            contactRange = Mathf.Max(0.1f, contactRange);
        }

        private void Update()
        {
            if (Time.time < nextTickTime)
            {
                return;
            }

            if (!ResolveTarget())
            {
                nextTickTime = Time.time + tickInterval;
                return;
            }

            Vector3 from = rangeOrigin != null ? rangeOrigin.position : transform.position;
            Vector3 to = targetTransform.position;
            from.y = 0f;
            to.y = 0f;

            if ((to - from).sqrMagnitude > contactRange * contactRange)
            {
                nextTickTime = Time.time + tickInterval;
                return;
            }

            targetHealth.TakeDamage(dps * tickInterval);
            nextTickTime = Time.time + tickInterval;
        }

        private bool ResolveTarget()
        {
            if (targetTransform != null && targetHealth != null)
            {
                return true;
            }

            GameObject player = GameObject.FindWithTag(playerTag);
            if (player == null)
            {
                PlayerVitals vitalsFallback = FindFirstObjectByType<PlayerVitals>();
                if (vitalsFallback == null)
                {
                    return false;
                }

                targetTransform = vitalsFallback.transform;
                targetHealth = vitalsFallback.HealthComponent;
                return targetHealth != null;
            }

            targetTransform = player.transform;
            targetHealth = player.GetComponent<Health>();
            if (targetHealth == null)
            {
                targetHealth = player.GetComponentInParent<Health>();
            }

            if (targetHealth == null)
            {
                PlayerVitals vitals = player.GetComponent<PlayerVitals>();
                if (vitals != null)
                {
                    targetHealth = vitals.HealthComponent;
                }
            }

            return targetHealth != null;
        }
    }
}
