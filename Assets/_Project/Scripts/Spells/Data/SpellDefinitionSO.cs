using UnityEngine;

namespace DuckovProto.Spells.Data
{
    public enum SpellCastType
    {
        Projectile,
        Hitscan,
        Channel,
    }

    [CreateAssetMenu(menuName = "DuckovProto/Spells/Spell Definition", fileName = "SpellDefinition_")]
    public sealed class SpellDefinitionSO : ScriptableObject
    {
        [SerializeField] private string id = "spell_id";
        [SerializeField] private string displayName = "Spell";
        [SerializeField] private SpellCastType castType = SpellCastType.Projectile;
        [SerializeField] private float manaCost = 10f;
        [SerializeField] private float cooldown = 0.5f;
        [SerializeField] private float range = 20f;
        [SerializeField] private float damage = 8f;
        [SerializeField] private float speed = 20f;
        [SerializeField] private int bounceCount;
        [SerializeField] private float duration = 1f;
        [SerializeField] private float tickInterval = 0.2f;
        [SerializeField] private int tickCount = 1;
        [SerializeField] private bool requireAimLockDuringCast;

        public string Id => id;
        public string DisplayName => displayName;
        public SpellCastType CastType => castType;
        public float ManaCost => Mathf.Max(0f, manaCost);
        public float Cooldown => Mathf.Max(0f, cooldown);
        public float Range => Mathf.Max(0f, range);
        public float Damage => Mathf.Max(0f, damage);
        public float Speed => Mathf.Max(0f, speed);
        public int BounceCount => Mathf.Max(0, bounceCount);
        public float Duration => Mathf.Max(0f, duration);
        public float TickInterval => Mathf.Max(0f, tickInterval);
        public int TickCount => Mathf.Max(0, tickCount);
        public bool RequireAimLockDuringCast => requireAimLockDuringCast;
    }
}
