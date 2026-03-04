using System.Collections.Generic;
using DuckovProto.Combat;
using DuckovProto.Feedback;
using DuckovProto.Player;
using DuckovProto.Spells;
using UnityEngine;

namespace DuckovProto.Runes
{
    public sealed class RuneCastController : MonoBehaviour
    {
        public enum AbilityId
        {
            Firebolt,
            IceLance,
            Blink,
            Nova,
            Chain,
        }

        private enum SpellType
        {
            None,
            Firebolt,
            IceLance,
            Blink,
            Nova,
            Chain,
        }

        [SerializeField] private RuneDrawController runeDrawController;
        [SerializeField] private PlayerAim playerAim;
        [SerializeField] private FireboltSpell fireboltSpell;
        [SerializeField] private IceLanceSpell iceLanceSpell;
        [SerializeField] private BlinkSpell blinkSpell;
        [SerializeField] private NovaSpell novaSpell;
        [SerializeField] private ChainLightningSpell chainLightningSpell;
        [SerializeField] private Transform caster;
        [SerializeField] private bool enableFeedback = true;
        [SerializeField] private Mana mana;

        [Header("Cooldowns")]
        [SerializeField] private float globalCooldown = 0.2f;
        [SerializeField] private float fireboltCooldown = 0.35f;
        [SerializeField] private float iceLanceCooldown = 0.25f;
        [SerializeField] private float blinkCooldown = 1.0f;
        [SerializeField] private float novaCooldown = 0.8f;
        [SerializeField] private float chainCooldown = 1.4f;

        [Header("Mana Costs")]
        [SerializeField] private float fireboltManaCost = 14f;
        [SerializeField] private float iceLanceManaCost = 8f;
        [SerializeField] private float blinkManaCost = 22f;
        [SerializeField] private float novaManaCost = 30f;
        [SerializeField] private float chainManaCost = 26f;
        [SerializeField] private float insufficientManaMessageDuration = 0.5f;

        private UnistrokeRecognizer recognizer;
        private float lastGlobalCastTime = -999f;
        private float lastFireboltCastTime = -999f;
        private float lastIceLanceCastTime = -999f;
        private float lastBlinkCastTime = -999f;
        private float lastNovaCastTime = -999f;
        private float lastChainCastTime = -999f;
        private SpellType lastAttemptedSpell = SpellType.None;
        private string lastRuneName = "None";
        private string lastSpellName = "None";
        private string cooldownStateText = "Ready";
        private float transientStateUntil = -1f;
        private string transientStateText;

        public string LastRuneName => lastRuneName;
        public string LastSpellName => lastSpellName;
        public string CooldownStateText => cooldownStateText;
        
        public float GetCooldownRemaining(AbilityId id)
        {
            SpellType spell = SpellFromAbilityId(id);
            if (spell == SpellType.None)
            {
                return 0f;
            }

            return RemainingTime(GetLastCastTime(spell), CooldownFor(spell), Time.time);
        }

        public float GetCooldownDuration(AbilityId id)
        {
            SpellType spell = SpellFromAbilityId(id);
            if (spell == SpellType.None)
            {
                return 0f;
            }

            return Mathf.Max(0f, CooldownFor(spell));
        }

        public float GetGlobalCooldownRemaining()
        {
            return RemainingTime(lastGlobalCastTime, globalCooldown, Time.time);
        }

        public float GetManaCost(AbilityId id)
        {
            SpellType spell = SpellFromAbilityId(id);
            if (spell == SpellType.None)
            {
                return 0f;
            }

            return Mathf.Max(0f, ManaCostFor(spell));
        }

        public bool IsCastAvailable(AbilityId id)
        {
            SpellType spell = SpellFromAbilityId(id);
            if (spell == SpellType.None)
            {
                return false;
            }

            if (GetGlobalCooldownRemaining() > 0f)
            {
                return false;
            }

            if (GetCooldownRemaining(id) > 0f)
            {
                return false;
            }

            ResolveManaIfMissing();
            float cost = GetManaCost(id);
            if (mana == null || cost <= 0f)
            {
                return true;
            }

            return mana.CurrentMana + 0.0001f >= cost;
        }

        public string GetRuneLabel(AbilityId id)
        {
            switch (id)
            {
                case AbilityId.Firebolt: return "Circle";
                case AbilityId.IceLance: return "Line";
                case AbilityId.Blink: return "V";
                case AbilityId.Nova: return "Triangle";
                case AbilityId.Chain: return "Z";
                default: return string.Empty;
            }
        }

        private void Awake()
        {
            if (runeDrawController == null)
            {
                runeDrawController = GetComponent<RuneDrawController>();
            }

            if (fireboltSpell == null)
            {
                fireboltSpell = GetComponent<FireboltSpell>();
            }

            if (playerAim == null)
            {
                playerAim = GetComponent<PlayerAim>();
            }

            if (iceLanceSpell == null)
            {
                iceLanceSpell = GetComponent<IceLanceSpell>();
            }

            if (blinkSpell == null)
            {
                blinkSpell = GetComponent<BlinkSpell>();
            }

            if (novaSpell == null)
            {
                novaSpell = GetComponent<NovaSpell>();
            }

            if (chainLightningSpell == null)
            {
                chainLightningSpell = GetComponent<ChainLightningSpell>();
            }

            ResolveManaIfMissing();

            if (caster == null)
            {
                caster = transform;
            }

            recognizer = new UnistrokeRecognizer(64);
            RegisterTemplates();
        }

        private void OnEnable()
        {
            if (runeDrawController != null)
            {
                runeDrawController.StrokeCompleted += OnStrokeCompleted;
            }
        }

        private void OnDisable()
        {
            if (runeDrawController != null)
            {
                runeDrawController.StrokeCompleted -= OnStrokeCompleted;
            }
        }

        private void Update()
        {
            UpdateCooldownStateText();
        }

        private void OnStrokeCompleted(IReadOnlyList<Vector3> worldPoints)
        {
            if (worldPoints == null || worldPoints.Count < 2)
            {
                return;
            }

            List<Vector2> stroke2D = new List<Vector2>(worldPoints.Count);
            for (int i = 0; i < worldPoints.Count; i++)
            {
                Vector3 p = worldPoints[i];
                stroke2D.Add(new Vector2(p.x, p.z));
            }

            string runeName;
            float score;
            float distance;

            if (TryRecognizeCircle(stroke2D, out float circleScore))
            {
                runeName = "Circle";
                score = circleScore;
                distance = 0f;
            }
            else
            {
                UnistrokeRecognizer.RecognitionResult result = recognizer.Recognize(stroke2D);
                runeName = result.Name;
                score = result.Score;
                distance = result.Distance;
            }

            Debug.Log($"Rune: {runeName} score={score:F2} distance={distance:F3}", this);
            lastRuneName = runeName;

            Vector3 castDirection = caster.forward;
            if (playerAim != null && playerAim.IsFreezeVectorActive)
            {
                castDirection = playerAim.GetFrozenAimDirection();
            }

            SpellType spell = SpellFromRune(runeName);
            lastAttemptedSpell = spell;
            if (spell == SpellType.None)
            {
                Debug.Log($"Rune '{runeName}' recognized (no spell bound yet).", this);
                return;
            }

            if (!IsCooldownReady(spell, out float globalRemaining, out float spellRemaining))
            {
                string spellName = SpellName(spell);
                if (globalRemaining > 0f)
                {
                    Debug.Log($"Cast blocked: Global cooldown {globalRemaining:F2}s", this);
                }
                else
                {
                    Debug.Log($"Cast blocked: {spellName} cooldown {spellRemaining:F2}s", this);
                }

                return;
            }

            ResolveManaIfMissing();
            float manaCost = ManaCostFor(spell);
            if (mana != null && manaCost > 0f && !mana.TrySpend(manaCost))
            {
                lastSpellName = "Blocked";
                SetTransientStateText("Not enough mana", insufficientManaMessageDuration);
                Debug.Log($"Cast blocked: Not enough mana for {SpellName(spell)} ({mana.CurrentMana:F0}/{manaCost:F0})", this);
                return;
            }

            bool didCast = false;
            switch (spell)
            {
                case SpellType.Firebolt:
                    if (fireboltSpell != null)
                    {
                        fireboltSpell.Cast(caster, castDirection);
                        didCast = true;
                    }
                    break;

                case SpellType.IceLance:
                    if (iceLanceSpell != null)
                    {
                        iceLanceSpell.Cast(caster, castDirection);
                        didCast = true;
                    }
                    break;

                case SpellType.Blink:
                    if (blinkSpell != null)
                    {
                        blinkSpell.Cast(caster, castDirection);
                        didCast = true;
                    }
                    break;

                case SpellType.Nova:
                    if (novaSpell != null)
                    {
                        novaSpell.Cast(caster);
                        didCast = true;
                    }
                    break;

                case SpellType.Chain:
                    if (chainLightningSpell != null)
                    {
                        chainLightningSpell.Cast(caster, castDirection);
                        didCast = true;
                    }
                    break;

                default:
                    break;
            }

            if (!didCast)
            {
                if (mana != null && manaCost > 0f)
                {
                    mana.Restore(manaCost);
                }

                return;
            }

            ConsumeCooldown(spell);
            lastSpellName = SpellName(spell);
            PlayCastFeedback(spell);
        }

        private void RegisterTemplates()
        {
            recognizer.AddTemplate("Line", new[]
            {
                new Vector2(-1f, 0f),
                new Vector2(-0.5f, 0f),
                new Vector2(0f, 0f),
                new Vector2(0.5f, 0f),
                new Vector2(1f, 0f),
            });

            recognizer.AddTemplate("V", new[]
            {
                new Vector2(-1f, 1f),
                new Vector2(-0.5f, 0.5f),
                new Vector2(0f, 0f),
                new Vector2(0.5f, 0.5f),
                new Vector2(1f, 1f),
            });

            recognizer.AddTemplate("Circle", new[]
            {
                new Vector2(0f, 1f),
                new Vector2(0.38f, 0.92f),
                new Vector2(0.7f, 0.7f),
                new Vector2(0.92f, 0.38f),
                new Vector2(1f, 0f),
                new Vector2(0.92f, -0.38f),
                new Vector2(0.7f, -0.7f),
                new Vector2(0.38f, -0.92f),
                new Vector2(0f, -1f),
                new Vector2(-0.38f, -0.92f),
                new Vector2(-0.7f, -0.7f),
                new Vector2(-0.92f, -0.38f),
                new Vector2(-1f, 0f),
                new Vector2(-0.92f, 0.38f),
                new Vector2(-0.7f, 0.7f),
                new Vector2(-0.38f, 0.92f),
                new Vector2(0f, 1f),
            });

            recognizer.AddTemplate("Triangle", new[]
            {
                new Vector2(0f, 1f),
                new Vector2(-0.8f, -0.6f),
                new Vector2(0.8f, -0.6f),
                new Vector2(0f, 1f),
            });

            recognizer.AddTemplate("Z", new[]
            {
                new Vector2(-1f, 1f),
                new Vector2(1f, 1f),
                new Vector2(-1f, -1f),
                new Vector2(1f, -1f),
            });
        }

        private static bool TryRecognizeCircle(IReadOnlyList<Vector2> points, out float score)
        {
            score = 0f;
            if (points == null || points.Count < 12)
            {
                return false;
            }

            float minX = float.MaxValue;
            float maxX = float.MinValue;
            float minY = float.MaxValue;
            float maxY = float.MinValue;
            float pathLength = 0f;

            for (int i = 0; i < points.Count; i++)
            {
                Vector2 p = points[i];
                minX = Mathf.Min(minX, p.x);
                maxX = Mathf.Max(maxX, p.x);
                minY = Mathf.Min(minY, p.y);
                maxY = Mathf.Max(maxY, p.y);

                if (i > 0)
                {
                    pathLength += Vector2.Distance(points[i - 1], p);
                }
            }

            float width = Mathf.Max(0.001f, maxX - minX);
            float height = Mathf.Max(0.001f, maxY - minY);
            float aspect = width / height;
            float endGap = Vector2.Distance(points[0], points[points.Count - 1]);

            bool isClosed = endGap <= pathLength * 0.22f;
            bool isRoundish = aspect >= 0.6f && aspect <= 1.66f;
            if (!isClosed || !isRoundish)
            {
                return false;
            }

            float closeScore = Mathf.Clamp01(1f - (endGap / (pathLength * 0.22f)));
            float aspectScore = Mathf.Clamp01(1f - Mathf.Abs(1f - aspect));
            score = Mathf.Clamp01((closeScore * 0.6f) + (aspectScore * 0.4f));
            return true;
        }

        private void PlayCastFeedback(SpellType spell)
        {
            if (!enableFeedback || caster == null)
            {
                return;
            }

            Vector3 pos = caster.position;
            switch (spell)
            {
                case SpellType.Blink:
                    FeedbackUtils.SpawnGlyphConvergeBurst(pos + Vector3.up * 0.02f, VfxPreset.Arcane, 0.75f, 0.28f, 0.9f);
                    FeedbackUtils.SpawnImpactPuff(pos + Vector3.up * 0.1f, 0.2f);
                    FeedbackUtils.CameraShake(0.06f, 0.05f);
                    break;

                case SpellType.Nova:
                    FeedbackUtils.SpawnGlyphConvergeBurst(pos + Vector3.up * 0.02f, VfxPreset.Arcane, 1.35f, 0.32f, 1.2f);
                    FeedbackUtils.SpawnRing(pos, 2.5f, 0.28f, 0.02f, VfxPreset.Arcane, 1.2f);
                    FeedbackUtils.CameraShake(0.1f, 0.12f);
                    break;

                case SpellType.Chain:
                    FeedbackUtils.SpawnGlyphConvergeBurst(pos + Vector3.up * 0.02f, VfxPreset.Lightning, 1.05f, 0.3f, 1.15f);
                    FeedbackUtils.SpawnParticleBurst(VfxPreset.Lightning, pos + Vector3.up * 0.15f, 12, 1.05f, 0.9f, 0.95f, 0.7f, 0.75f);
                    FeedbackUtils.CameraShake(0.08f, 0.08f);
                    break;

                case SpellType.IceLance:
                    FeedbackUtils.SpawnGlyphConvergeBurst(pos + Vector3.up * 0.02f, VfxPreset.Ice, 0.85f, 0.26f, 0.95f);
                    FeedbackUtils.SpawnRing(pos, 0.9f, 0.2f, 0.02f, VfxPreset.Ice, 0.9f);
                    FeedbackUtils.CameraShake(0.07f, 0.06f);
                    break;

                default:
                    FeedbackUtils.SpawnGlyphConvergeBurst(pos + Vector3.up * 0.02f, VfxPreset.Fire, 1.2f, 0.32f, 1.2f);
                    FeedbackUtils.SpawnRing(pos, 1.0f, 0.25f, 0.02f, VfxPreset.Fire, 1f);
                    FeedbackUtils.CameraShake(0.08f, 0.08f);
                    break;
            }
        }

        private SpellType SpellFromRune(string runeName)
        {
            switch (runeName)
            {
                case "Circle": return SpellType.Firebolt;
                case "Line": return SpellType.IceLance;
                case "V": return SpellType.Blink;
                case "Triangle": return SpellType.Nova;
                case "Z": return SpellType.Chain;
                default: return SpellType.None;
            }
        }

        private static SpellType SpellFromAbilityId(AbilityId id)
        {
            switch (id)
            {
                case AbilityId.Firebolt: return SpellType.Firebolt;
                case AbilityId.IceLance: return SpellType.IceLance;
                case AbilityId.Blink: return SpellType.Blink;
                case AbilityId.Nova: return SpellType.Nova;
                case AbilityId.Chain: return SpellType.Chain;
                default: return SpellType.None;
            }
        }

        private void ResolveManaIfMissing()
        {
            if (mana != null)
            {
                return;
            }

            mana = GetComponent<Mana>();
            if (mana != null)
            {
                return;
            }

            mana = GetComponentInParent<Mana>();
            if (mana != null)
            {
                return;
            }

            GameObject player = GameObject.FindWithTag("Player");
            if (player != null)
            {
                mana = player.GetComponent<Mana>();
            }
        }

        private bool IsCooldownReady(SpellType spell, out float globalRemaining, out float spellRemaining)
        {
            float now = Time.time;
            globalRemaining = RemainingTime(lastGlobalCastTime, globalCooldown, now);
            spellRemaining = RemainingTime(GetLastCastTime(spell), CooldownFor(spell), now);
            return globalRemaining <= 0f && spellRemaining <= 0f;
        }

        private void ConsumeCooldown(SpellType spell)
        {
            float now = Time.time;
            lastGlobalCastTime = now;
            SetLastCastTime(spell, now);
        }

        private void UpdateCooldownStateText()
        {
            float now = Time.time;
            if (now < transientStateUntil && !string.IsNullOrEmpty(transientStateText))
            {
                cooldownStateText = transientStateText;
                return;
            }

            float globalRemaining = RemainingTime(lastGlobalCastTime, globalCooldown, now);
            if (globalRemaining > 0f)
            {
                cooldownStateText = $"Global cd: {globalRemaining:F2}s";
                return;
            }

            if (lastAttemptedSpell == SpellType.None)
            {
                cooldownStateText = "Ready";
                return;
            }

            float spellRemaining = RemainingTime(
                GetLastCastTime(lastAttemptedSpell),
                CooldownFor(lastAttemptedSpell),
                now);
            if (spellRemaining > 0f)
            {
                cooldownStateText = $"{SpellName(lastAttemptedSpell)} cd: {spellRemaining:F2}s";
            }
            else
            {
                cooldownStateText = "Ready";
            }
        }

        private float ManaCostFor(SpellType spell)
        {
            switch (spell)
            {
                case SpellType.Firebolt: return fireboltManaCost;
                case SpellType.IceLance: return iceLanceManaCost;
                case SpellType.Blink: return blinkManaCost;
                case SpellType.Nova: return novaManaCost;
                case SpellType.Chain: return chainManaCost;
                default: return 0f;
            }
        }

        private void SetTransientStateText(string text, float duration)
        {
            transientStateText = text;
            transientStateUntil = Time.time + Mathf.Max(0f, duration);
        }

        private float CooldownFor(SpellType spell)
        {
            switch (spell)
            {
                case SpellType.Firebolt: return fireboltCooldown;
                case SpellType.IceLance: return iceLanceCooldown;
                case SpellType.Blink: return blinkCooldown;
                case SpellType.Nova: return novaCooldown;
                case SpellType.Chain: return chainCooldown;
                default: return 0f;
            }
        }

        private float GetLastCastTime(SpellType spell)
        {
            switch (spell)
            {
                case SpellType.Firebolt: return lastFireboltCastTime;
                case SpellType.IceLance: return lastIceLanceCastTime;
                case SpellType.Blink: return lastBlinkCastTime;
                case SpellType.Nova: return lastNovaCastTime;
                case SpellType.Chain: return lastChainCastTime;
                default: return -999f;
            }
        }

        private void SetLastCastTime(SpellType spell, float time)
        {
            switch (spell)
            {
                case SpellType.Firebolt: lastFireboltCastTime = time; break;
                case SpellType.IceLance: lastIceLanceCastTime = time; break;
                case SpellType.Blink: lastBlinkCastTime = time; break;
                case SpellType.Nova: lastNovaCastTime = time; break;
                case SpellType.Chain: lastChainCastTime = time; break;
            }
        }

        private static float RemainingTime(float lastCastTime, float cooldown, float now)
        {
            float elapsed = now - lastCastTime;
            return Mathf.Max(0f, cooldown - elapsed);
        }

        private static string SpellName(SpellType spell)
        {
            switch (spell)
            {
                case SpellType.Firebolt: return "Firebolt";
                case SpellType.IceLance: return "IceLance";
                case SpellType.Blink: return "Blink";
                case SpellType.Nova: return "Nova";
                case SpellType.Chain: return "Chain";
                default: return "None";
            }
        }

    }
}
