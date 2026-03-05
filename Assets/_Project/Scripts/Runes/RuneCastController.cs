using System.Collections.Generic;
using DuckovProto.Combat;
using DuckovProto.Spells;
using DuckovProto.Spells.Data;
using UnityEngine;

namespace DuckovProto.Runes
{
    [DisallowMultipleComponent]
    public sealed class RuneCastController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private SpellRunner spellRunner;
        [SerializeField] private Transform caster;
        [SerializeField] private Mana mana;
        [SerializeField] private AimLockController aimLockController;

        [Header("Gate")]
        [SerializeField] private float globalCooldown = 0.2f;
        [SerializeField] private float statusMessageDuration = 0.5f;
        [SerializeField] private bool logCastState;

        private readonly Dictionary<string, float> nextReadyBySpellId = new Dictionary<string, float>(16);
        private readonly Dictionary<string, float> cooldownBySpellId = new Dictionary<string, float>(16);

        private string lastAttemptedSpellId = string.Empty;
        private string lastRuneName = "None";
        private string lastSpellName = "None";
        private string cooldownStateText = "Ready";

        private float globalNextReadyTime;
        private float transientStateUntil = -1f;
        private string transientStateText;

        public string LastRuneName => lastRuneName;
        public string LastSpellName => lastSpellName;
        public string CooldownStateText => cooldownStateText;

        private void Awake()
        {
            ResolveRefsIfMissing();
        }

        private void Update()
        {
            ResolveRefsIfMissing();
            UpdateCooldownStateText();
        }

        public bool TryCastSpell(SpellDefinitionSO spell, CastContext ctx, out string failReason)
        {
            return TryCastSpell(spell, ctx, string.Empty, out failReason);
        }

        public bool TryCastSpell(SpellDefinitionSO spell, CastContext ctx, string runeDisplayName, out string failReason)
        {
            ResolveRefsIfMissing();

            if (!string.IsNullOrWhiteSpace(runeDisplayName))
            {
                lastRuneName = runeDisplayName;
            }

            if (spell == null)
            {
                failReason = "Invalid spell";
                SetBlockedState(failReason);
                return false;
            }

            string spellId = GetSpellKey(spell);
            lastAttemptedSpellId = spellId;
            cooldownBySpellId[spellId] = spell.Cooldown;

            float now = Time.time;
            float globalRemaining = Mathf.Max(0f, globalNextReadyTime - now);
            if (globalRemaining > 0f)
            {
                failReason = $"Global cooldown {globalRemaining:F2}s";
                SetBlockedState(failReason);
                return false;
            }

            float spellRemaining = GetCooldownRemaining(spellId);
            if (spellRemaining > 0f)
            {
                failReason = $"{GetSpellDisplayName(spell)} cooldown {spellRemaining:F2}s";
                SetBlockedState(failReason);
                return false;
            }

            float manaCost = spell.ManaCost;
            if (mana != null && manaCost > 0f && !mana.TrySpend(manaCost))
            {
                failReason = "Not enough mana";
                SetBlockedState(failReason);
                return false;
            }

            CastContext context = BuildContext(ctx);
            if (spellRunner == null)
            {
                if (mana != null && manaCost > 0f)
                {
                    mana.Restore(manaCost);
                }

                failReason = "Missing SpellRunner";
                SetBlockedState(failReason);
                return false;
            }

            spellRunner.Execute(spell, context);

            globalNextReadyTime = now + Mathf.Max(0f, globalCooldown);
            nextReadyBySpellId[spellId] = now + Mathf.Max(0f, spell.Cooldown);

            lastSpellName = GetSpellDisplayName(spell);
            failReason = string.Empty;
            SetTransientStateText($"Cast: {lastSpellName}", 0.2f);
            return true;
        }

        public float GetGlobalCooldownRemaining()
        {
            return Mathf.Max(0f, globalNextReadyTime - Time.time);
        }

        public float GetCooldownRemaining(string spellId)
        {
            if (string.IsNullOrWhiteSpace(spellId))
            {
                return 0f;
            }

            if (!nextReadyBySpellId.TryGetValue(spellId, out float nextReady))
            {
                return 0f;
            }

            return Mathf.Max(0f, nextReady - Time.time);
        }

        public float GetCooldownDuration(string spellId)
        {
            if (string.IsNullOrWhiteSpace(spellId))
            {
                return 0f;
            }

            return cooldownBySpellId.TryGetValue(spellId, out float duration)
                ? Mathf.Max(0f, duration)
                : 0f;
        }

        public bool IsSpellCastAvailable(SpellDefinitionSO spell)
        {
            if (spell == null)
            {
                return false;
            }

            if (GetGlobalCooldownRemaining() > 0f)
            {
                return false;
            }

            string spellId = GetSpellKey(spell);
            if (GetCooldownRemaining(spellId) > 0f)
            {
                return false;
            }

            ResolveRefsIfMissing();
            float cost = spell.ManaCost;
            if (mana == null || cost <= 0f)
            {
                return true;
            }

            return mana.CurrentMana + 0.0001f >= cost;
        }

        public void ReportState(string text, float duration = -1f)
        {
            float d = duration >= 0f ? duration : statusMessageDuration;
            SetTransientStateText(text, d);
        }

        private CastContext BuildContext(CastContext source)
        {
            CastContext context = source ?? new CastContext();

            if (context.caster == null)
            {
                context.caster = caster != null ? caster : transform;
            }

            if (context.aimLock == null)
            {
                context.aimLock = aimLockController;
            }

            if (context.cam == null)
            {
                context.cam = Camera.main;
            }

            if (context.stateReporter == null)
            {
                context.stateReporter = message => ReportState(message);
            }

            return context;
        }

        private void ResolveRefsIfMissing()
        {
            if (caster == null)
            {
                caster = transform;
            }

            if (spellRunner == null)
            {
                spellRunner = GetComponent<SpellRunner>();
            }

            if (mana == null)
            {
                mana = GetComponent<Mana>();
                if (mana == null)
                {
                    mana = GetComponentInParent<Mana>();
                }

                if (mana == null)
                {
                    GameObject player = GameObject.FindWithTag("Player");
                    if (player != null)
                    {
                        mana = player.GetComponent<Mana>();
                    }
                }
            }

            if (aimLockController == null)
            {
                aimLockController = GetComponent<AimLockController>();
                if (aimLockController == null)
                {
                    aimLockController = GetComponentInParent<AimLockController>();
                }
            }
        }

        private void UpdateCooldownStateText()
        {
            float now = Time.time;
            if (now < transientStateUntil && !string.IsNullOrEmpty(transientStateText))
            {
                cooldownStateText = transientStateText;
                return;
            }

            float globalRemaining = Mathf.Max(0f, globalNextReadyTime - now);
            if (globalRemaining > 0f)
            {
                cooldownStateText = $"Global cd: {globalRemaining:F2}s";
                return;
            }

            if (!string.IsNullOrWhiteSpace(lastAttemptedSpellId))
            {
                float spellRemaining = GetCooldownRemaining(lastAttemptedSpellId);
                if (spellRemaining > 0f)
                {
                    string name = string.IsNullOrWhiteSpace(lastSpellName) || lastSpellName == "Blocked"
                        ? lastAttemptedSpellId
                        : lastSpellName;
                    cooldownStateText = $"{name} cd: {spellRemaining:F2}s";
                    return;
                }
            }

            cooldownStateText = GetReadyOrAimLockStateText();
        }

        private string GetReadyOrAimLockStateText()
        {
            if (aimLockController == null || !aimLockController.HasLock)
            {
                return "Ready";
            }

            float remain = aimLockController.RemainingLockTime;
            if (aimLockController.IsTargetLock)
            {
                return $"Aim lock: Target ({remain:F1}s)";
            }

            if (aimLockController.IsPointLock)
            {
                return $"Aim lock: Point ({remain:F1}s)";
            }

            return "Aim lock";
        }

        private void SetBlockedState(string failReason)
        {
            lastSpellName = "Blocked";
            SetTransientStateText(failReason, statusMessageDuration);
        }

        private void SetTransientStateText(string text, float duration)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return;
            }

            transientStateText = text;
            transientStateUntil = Time.time + Mathf.Max(0f, duration);

            if (logCastState)
            {
                Debug.Log($"[RuneCast] {text}", this);
            }
        }

        private static string GetSpellDisplayName(SpellDefinitionSO spell)
        {
            if (spell == null)
            {
                return "Spell";
            }

            if (!string.IsNullOrWhiteSpace(spell.DisplayName))
            {
                return spell.DisplayName;
            }

            return GetSpellKey(spell);
        }

        private static string GetSpellKey(SpellDefinitionSO spell)
        {
            if (spell == null)
            {
                return "spell";
            }

            if (!string.IsNullOrWhiteSpace(spell.Id))
            {
                return spell.Id;
            }

            if (!string.IsNullOrWhiteSpace(spell.DisplayName))
            {
                return spell.DisplayName;
            }

            return "spell";
        }
    }
}
