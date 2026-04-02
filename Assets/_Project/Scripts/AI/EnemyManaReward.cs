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

            return PlayerRuntimeLocator.TryGetMana(out playerMana);
        }
    }
}
