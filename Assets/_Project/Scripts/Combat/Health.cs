using System;
using System.Collections;
using UnityEngine;

namespace DuckovProto.Combat
{
    public sealed class Health : MonoBehaviour
    {
        public event Action OnDied;

        [SerializeField] private float maxHealth = 10f;
        [SerializeField] private bool destroyOnDeath = true;
        [SerializeField] private float hitScaleMultiplier = 1.15f;
        [SerializeField] private float hitScaleDuration = 0.06f;

        private float currentHealth;
        private Vector3 baseScale;
        private Coroutine hitRoutine;

        public float CurrentHealth => currentHealth;
        public float MaxHealth => maxHealth;
        public bool IsDead => currentHealth <= 0f;

        private void Awake()
        {
            currentHealth = maxHealth;
            baseScale = transform.localScale;
        }

        public void TakeDamage(float amount)
        {
            if (amount <= 0f || currentHealth <= 0f)
            {
                return;
            }

            currentHealth = Mathf.Max(0f, currentHealth - amount);
            PlayHitFeedback();
            if (currentHealth <= 0f)
            {
                OnDied?.Invoke();
                if (destroyOnDeath)
                {
                    Destroy(gameObject);
                }
            }
        }

        public void RestoreToFull()
        {
            currentHealth = maxHealth;
            if (hitRoutine != null)
            {
                StopCoroutine(hitRoutine);
                hitRoutine = null;
            }

            transform.localScale = baseScale;
        }

        public void Restore(float amount)
        {
            if (amount <= 0f)
            {
                return;
            }

            currentHealth = Mathf.Clamp(currentHealth + amount, 0f, maxHealth);
        }

        private void PlayHitFeedback()
        {
            if (hitScaleDuration <= 0f || hitScaleMultiplier <= 1f)
            {
                return;
            }

            if (hitRoutine != null)
            {
                StopCoroutine(hitRoutine);
            }

            hitRoutine = StartCoroutine(HitScaleRoutine());
        }

        private IEnumerator HitScaleRoutine()
        {
            transform.localScale = baseScale * hitScaleMultiplier;
            yield return new WaitForSeconds(hitScaleDuration);
            transform.localScale = baseScale;
            hitRoutine = null;
        }
    }
}
