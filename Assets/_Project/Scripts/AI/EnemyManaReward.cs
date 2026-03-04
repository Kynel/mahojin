using DuckovProto.Combat;
using DuckovProto.Player;
using UnityEngine;

namespace DuckovProto.AI
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Health))]
    public sealed class EnemyManaReward : MonoBehaviour
    {
        [SerializeField] private float killManaRestore = 10f;
        [SerializeField] private Mana playerMana;
        [SerializeField] private string playerTag = "Player";

        private Health health;

        private void Awake()
        {
            health = GetComponent<Health>();
        }

        private void OnEnable()
        {
            if (health != null)
            {
                health.OnDied += OnDied;
            }
        }

        private void OnDisable()
        {
            if (health != null)
            {
                health.OnDied -= OnDied;
            }
        }

        private void OnDied()
        {
            if (killManaRestore <= 0f)
            {
                return;
            }

            if (!ResolvePlayerMana())
            {
                return;
            }

            playerMana.Restore(killManaRestore);
        }

        private bool ResolvePlayerMana()
        {
            if (playerMana != null)
            {
                return true;
            }

            GameObject player = GameObject.FindWithTag(playerTag);
            if (player != null)
            {
                playerMana = player.GetComponent<Mana>();
                if (playerMana != null)
                {
                    return true;
                }

                PlayerVitals vitals = player.GetComponent<PlayerVitals>();
                if (vitals != null)
                {
                    playerMana = vitals.ManaComponent;
                    if (playerMana != null)
                    {
                        return true;
                    }
                }
            }

            PlayerVitals fallbackVitals = FindFirstObjectByType<PlayerVitals>();
            if (fallbackVitals != null)
            {
                playerMana = fallbackVitals.ManaComponent;
            }

            return playerMana != null;
        }
    }
}
