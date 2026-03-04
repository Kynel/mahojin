using System.Collections;
using DuckovProto.Combat;
using UnityEngine;

namespace DuckovProto.Player
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Health))]
    [RequireComponent(typeof(Mana))]
    public sealed class PlayerVitals : MonoBehaviour
    {
        [SerializeField] private Health health;
        [SerializeField] private Mana mana;
        [SerializeField] private Transform respawnPoint;
        [SerializeField] private float respawnDelay = 1f;
        [SerializeField] private bool logGameOver = true;

        private Rigidbody rb;
        private Vector3 initialPosition;
        private Quaternion initialRotation;
        private bool isRespawning;

        public Health HealthComponent => health;
        public Mana ManaComponent => mana;

        public float HP => health != null ? health.CurrentHealth : 0f;
        public float MaxHP => health != null ? health.MaxHealth : 0f;
        public float Mana => mana != null ? mana.CurrentMana : 0f;
        public float MaxMana => mana != null ? mana.MaxMana : 0f;
        public float ManaRegenPerSecond => mana != null ? mana.RegenPerSecond : 0f;

        private void Awake()
        {
            if (health == null)
            {
                health = GetComponent<Health>();
            }

            if (mana == null)
            {
                mana = GetComponent<Mana>();
            }

            rb = GetComponent<Rigidbody>();
            initialPosition = transform.position;
            initialRotation = transform.rotation;
        }

        private void OnValidate()
        {
            respawnDelay = Mathf.Max(0f, respawnDelay);
        }

        private void OnEnable()
        {
            if (health != null)
            {
                health.OnDied += OnPlayerDied;
            }
        }

        private void OnDisable()
        {
            if (health != null)
            {
                health.OnDied -= OnPlayerDied;
            }
        }

        public void ResetToFull()
        {
            if (health != null)
            {
                health.RestoreToFull();
            }

            if (mana != null)
            {
                mana.RestoreToFull();
            }
        }

        private void OnPlayerDied()
        {
            if (isRespawning)
            {
                return;
            }

            StartCoroutine(RespawnRoutine());
        }

        private IEnumerator RespawnRoutine()
        {
            isRespawning = true;
            if (logGameOver)
            {
                Debug.Log("Game Over", this);
            }

            yield return new WaitForSeconds(Mathf.Max(0f, respawnDelay));

            Vector3 targetPosition = respawnPoint != null ? respawnPoint.position : initialPosition;
            Quaternion targetRotation = respawnPoint != null ? respawnPoint.rotation : initialRotation;
            transform.SetPositionAndRotation(targetPosition, targetRotation);

            if (rb != null)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }

            ResetToFull();
            isRespawning = false;
        }
    }
}
