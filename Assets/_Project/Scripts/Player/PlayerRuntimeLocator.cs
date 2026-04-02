using DuckovProto.Combat;
using UnityEngine;

namespace DuckovProto.Player
{
    public static class PlayerRuntimeLocator
    {
        private static Transform cachedTransform;
        private static PlayerVitals cachedVitals;
        private static Health cachedHealth;
        private static Mana cachedMana;

        public static bool TryGetTransform(out Transform playerTransform)
        {
            Resolve();
            playerTransform = cachedTransform;
            return playerTransform != null;
        }

        public static bool TryGetVitals(out PlayerVitals vitals)
        {
            Resolve();
            vitals = cachedVitals;
            return vitals != null;
        }

        public static bool TryGetHealth(out Health health)
        {
            Resolve();
            health = cachedHealth;
            return health != null;
        }

        public static bool TryGetMana(out Mana mana)
        {
            Resolve();
            mana = cachedMana;
            return mana != null;
        }

        private static void Resolve()
        {
            if (cachedTransform != null)
            {
                bool healthValid = cachedHealth != null;
                bool manaValid = cachedMana != null;
                bool vitalsValid = cachedVitals != null;
                if (healthValid || manaValid || vitalsValid)
                {
                    return;
                }
            }

            cachedTransform = null;
            cachedVitals = null;
            cachedHealth = null;
            cachedMana = null;

            GameObject player = GameObject.FindWithTag("Player");
            if (player != null)
            {
                CacheFromTransform(player.transform);
            }

            if (cachedTransform == null)
            {
                PlayerVitals fallbackVitals = Object.FindFirstObjectByType<PlayerVitals>();
                if (fallbackVitals != null)
                {
                    CacheFromTransform(fallbackVitals.transform);
                }
            }
        }

        private static void CacheFromTransform(Transform playerTransform)
        {
            if (playerTransform == null)
            {
                return;
            }

            cachedTransform = playerTransform;
            cachedVitals = playerTransform.GetComponent<PlayerVitals>();
            cachedHealth = playerTransform.GetComponent<Health>();
            if (cachedHealth == null)
            {
                cachedHealth = playerTransform.GetComponentInParent<Health>();
            }

            if (cachedVitals != null && cachedHealth == null)
            {
                cachedHealth = cachedVitals.HealthComponent;
            }

            cachedMana = playerTransform.GetComponent<Mana>();
            if (cachedMana == null)
            {
                cachedMana = playerTransform.GetComponentInParent<Mana>();
            }

            if (cachedVitals != null && cachedMana == null)
            {
                cachedMana = cachedVitals.ManaComponent;
            }
        }
    }
}
