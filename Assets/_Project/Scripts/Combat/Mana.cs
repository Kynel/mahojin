using UnityEngine;

namespace DuckovProto.Combat
{
    [DisallowMultipleComponent]
    public sealed class Mana : MonoBehaviour
    {
        [SerializeField] private float maxMana = 100f;
        [SerializeField] private float currentMana = 100f;
        [SerializeField] private float regenPerSecond = 12f;
        [SerializeField] private float regenDelayAfterSpend = 0.25f;

        private float regenResumeTime;

        public float CurrentMana => currentMana;
        public float MaxMana => maxMana;
        public float RegenPerSecond => regenPerSecond;
        public float Normalized => maxMana <= 0f ? 0f : currentMana / maxMana;

        private void Awake()
        {
            maxMana = Mathf.Max(1f, maxMana);
            currentMana = Mathf.Clamp(currentMana, 0f, maxMana);
            regenResumeTime = Time.time;
        }

        private void OnValidate()
        {
            maxMana = Mathf.Max(1f, maxMana);
            currentMana = Mathf.Clamp(currentMana, 0f, maxMana);
            regenPerSecond = Mathf.Max(0f, regenPerSecond);
            regenDelayAfterSpend = Mathf.Max(0f, regenDelayAfterSpend);
        }

        private void Update()
        {
            if (currentMana >= maxMana || regenPerSecond <= 0f)
            {
                return;
            }

            if (Time.time < regenResumeTime)
            {
                return;
            }

            currentMana = Mathf.Min(maxMana, currentMana + regenPerSecond * Time.deltaTime);
        }

        public bool TrySpend(float amount)
        {
            if (amount <= 0f)
            {
                return true;
            }

            if (currentMana + 0.0001f < amount)
            {
                return false;
            }

            currentMana = Mathf.Max(0f, currentMana - amount);
            regenResumeTime = Time.time + regenDelayAfterSpend;
            return true;
        }

        public void Restore(float amount)
        {
            if (amount <= 0f)
            {
                return;
            }

            currentMana = Mathf.Clamp(currentMana + amount, 0f, maxMana);
        }

        public void RestoreToFull()
        {
            currentMana = maxMana;
            regenResumeTime = Time.time;
        }
    }
}
