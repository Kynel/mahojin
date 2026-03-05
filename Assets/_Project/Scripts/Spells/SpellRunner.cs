using DuckovProto.Combat;
using DuckovProto.Spells.Data;
using DuckovProto.Spells.Executors;
using UnityEngine;

namespace DuckovProto.Spells
{
    [DisallowMultipleComponent]
    public sealed class SpellRunner : MonoBehaviour
    {
        [Header("Default Masks")]
        [SerializeField] private LayerMask enemyMask = 1 << 8;
        [SerializeField] private LayerMask wallMask = 1 << 10;
        [SerializeField] private LayerMask groundMask = 1 << 7;
        [SerializeField] private bool logStateToConsole = true;

        private readonly ProjectileExecutor projectileExecutor = new ProjectileExecutor();
        private readonly HitscanExecutor hitscanExecutor = new HitscanExecutor();
        private readonly ChannelExecutor channelExecutor = new ChannelExecutor();

        public void Execute(SpellDefinitionSO spell, CastContext ctx)
        {
            if (spell == null)
            {
                return;
            }

            CastContext context = ctx ?? new CastContext();
            EnsureContextDefaults(context);

            switch (spell.CastType)
            {
                case SpellCastType.Projectile:
                    projectileExecutor.Execute(this, spell, context);
                    break;

                case SpellCastType.Hitscan:
                    hitscanExecutor.Execute(this, spell, context);
                    break;

                case SpellCastType.Channel:
                    channelExecutor.Execute(this, spell, context);
                    break;

                default:
                    context.ReportState($"Unknown cast type: {spell.CastType}");
                    break;
            }
        }

        private void EnsureContextDefaults(CastContext ctx)
        {
            if (ctx.caster == null)
            {
                ctx.caster = transform;
            }

            if (ctx.aimLock == null)
            {
                ctx.aimLock = GetComponent<AimLockController>();
                if (ctx.aimLock == null)
                {
                    ctx.aimLock = GetComponentInParent<AimLockController>();
                }
            }

            if (ctx.enemyMask.value == 0)
            {
                ctx.enemyMask = enemyMask;
            }

            if (ctx.wallMask.value == 0)
            {
                ctx.wallMask = wallMask;
            }

            if (ctx.groundMask.value == 0)
            {
                ctx.groundMask = groundMask;
            }

            if (ctx.cam == null)
            {
                ctx.cam = Camera.main;
            }

            if (ctx.stateReporter == null && logStateToConsole)
            {
                ctx.stateReporter = message => Debug.Log($"[SpellRunner] {message}", this);
            }
        }
    }
}
