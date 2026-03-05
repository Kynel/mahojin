using System.Collections.Generic;
using System.Text;
using DuckovProto.Combat;
using DuckovProto.Runes.Data;
using DuckovProto.Spells;
using DuckovProto.Spells.Data;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace DuckovProto.Runes
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(MagicCircleDrawController))]
    [RequireComponent(typeof(RuneCastController))]
    public sealed class RuneCastPipelineController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private MagicCircleDrawController magicCircleDrawController;
        [SerializeField] private RuneCastController runeCastController;
        [SerializeField] private SpellRunner spellRunner;
        [SerializeField] private AimLockController aimLockController;
        [SerializeField] private Transform caster;

        [Header("Data")]
        [SerializeField] private RuneLibrarySO runeLibrary;
        [SerializeField] private SpellLibrarySO spellLibrary;

        [Header("Cast Context Masks")]
        [SerializeField] private LayerMask enemyMask = 1 << 8;
        [SerializeField] private LayerMask wallMask = 1 << 10;
        [SerializeField] private LayerMask groundMask = 1 << 7;

        [Header("Recognition")]
        [SerializeField] private float minimumAcceptScore = 0.35f;
        [SerializeField] private float scoreCacheLifetime = 8f;

        [Header("State Message")]
        [SerializeField] private float resolvedMessageDuration = 0.35f;
        [SerializeField] private float unclearMessageDuration = 0.45f;
        [SerializeField] private bool allowLowScoreFallback;
        [SerializeField] private float lowScoreFallbackMin = 0.45f;
        [SerializeField] private bool logResolveDetails;

        private RuneGuideScorer scorer;
        private LightningReferenceMatcher lightningReferenceMatcher;
        private Camera cachedCamera;
        private readonly Dictionary<string, float> lastScoreByRuneId = new Dictionary<string, float>(16);
        private readonly Dictionary<string, float> passScoreByRuneId = new Dictionary<string, float>(16);
        private float lastScoreTime = -999f;
        private string lastBestRuneId = string.Empty;
        private float lastBestScore;
        private bool lastBestPassed;
        private string lastDecisionSummary = string.Empty;

        public RuneLibrarySO RuneLibrary => runeLibrary;
        public SpellLibrarySO SpellLibrary => spellLibrary;
        public float LastBestScore => lastBestScore;
        public bool LastBestPassed => lastBestPassed;
        public string LastBestRuneId => lastBestRuneId;
        public string LastDecisionSummary => lastDecisionSummary;

        public float GetConfiguredPassScore(RuneDefinitionSO rune)
        {
            return rune != null ? rune.PassScore : 1f;
        }

        public bool TryGetLastRuneScore(string runeId, out float score, out float passScore, out bool recent)
        {
            score = 0f;
            passScore = 0f;
            recent = false;
            if (string.IsNullOrWhiteSpace(runeId))
            {
                return false;
            }

            if (!lastScoreByRuneId.TryGetValue(runeId, out score))
            {
                return false;
            }

            passScoreByRuneId.TryGetValue(runeId, out passScore);
            recent = Time.time - lastScoreTime <= Mathf.Max(0.1f, scoreCacheLifetime);
            return true;
        }

        private void Awake()
        {
            ResolveReferences();
            EnsureRecognizers();
        }

        private void OnEnable()
        {
            ResolveReferences();
            EnsureRecognizers();
            if (magicCircleDrawController != null)
            {
                magicCircleDrawController.StrokeSubmittedNorm += HandleStrokeSubmitted;
            }
        }

        private void OnDisable()
        {
            if (magicCircleDrawController != null)
            {
                magicCircleDrawController.StrokeSubmittedNorm -= HandleStrokeSubmitted;
            }
        }

        private void HandleStrokeSubmitted(IReadOnlyList<Vector2> stroke)
        {
            ResolveReferences();
            EnsureRecognizers();

            if (scorer == null || lightningReferenceMatcher == null || runeLibrary == null || spellLibrary == null)
            {
                runeCastController?.ReportState("Rune data missing", unclearMessageDuration);
                return;
            }

            (RuneDefinitionSO bestRune, float bestScore, string reason) = ScoreAllRunes(stroke);
            if (bestRune == null)
            {
                lastDecisionSummary = "no_rune";
                runeCastController?.ReportState("Unclear", unclearMessageDuration);
                return;
            }

            float runeRequiredScore = GetRequiredPassScore(bestRune);
            bool passedByRune = bestScore >= runeRequiredScore;
            bool passedByGlobal = bestScore >= Mathf.Max(0f, minimumAcceptScore);
            bool strictPass = passedByRune && passedByGlobal;

            if (!strictPass)
            {
                bool fallbackPass = allowLowScoreFallback && bestScore >= Mathf.Max(0f, lowScoreFallbackMin);
                if (!fallbackPass)
                {
                    lastBestPassed = false;
                    float required = Mathf.Max(runeRequiredScore, minimumAcceptScore);
                    lastDecisionSummary = $"unclear:{bestRune.DisplayName}:{bestScore:0.000}<{required:0.000}";
                    runeCastController?.ReportState(
                        $"Unclear {Mathf.RoundToInt(bestScore * 100f)}%/{Mathf.RoundToInt(required * 100f)}%",
                        unclearMessageDuration);

                    if (logResolveDetails)
                    {
                        Debug.Log($"[RunePipeline] unclear. {lastDecisionSummary} reason={reason}", this);
                    }

                    return;
                }

                reason = $"fallback:{reason}";
            }

            SpellDefinitionSO spell = spellLibrary.GetById(bestRune.LinkedSpellId);
            if (spell == null)
            {
                runeCastController?.ReportState("Missing spell", unclearMessageDuration);
                return;
            }

            CastContext ctx = BuildCastContext();
            if (runeCastController == null)
            {
                return;
            }

            bool didCast = runeCastController.TryCastSpell(spell, ctx, bestRune.DisplayName, out string failReason);
            if (didCast)
            {
                lastBestPassed = true;
                lastDecisionSummary = $"cast:{bestRune.DisplayName}:{bestScore:0.000}";
                if (logResolveDetails)
                {
                    Debug.Log($"[RunePipeline] cast {bestRune.DisplayName} score={bestScore:0.000} ({reason})", this);
                }

                runeCastController.ReportState($"{bestRune.DisplayName} ({bestScore:0.00})", resolvedMessageDuration);
                return;
            }

            if (!string.IsNullOrWhiteSpace(failReason))
            {
                lastBestPassed = false;
                lastDecisionSummary = $"blocked:{bestRune.DisplayName}:{failReason}";
                runeCastController.ReportState(failReason, unclearMessageDuration);
            }
        }

        private (RuneDefinitionSO best, float bestScore, string reason) ScoreAllRunes(IReadOnlyList<Vector2> stroke)
        {
            lastScoreByRuneId.Clear();
            passScoreByRuneId.Clear();
            lastScoreTime = Time.time;
            StringBuilder debugBuilder = logResolveDetails ? new StringBuilder(192) : null;

            IReadOnlyList<RuneDefinitionSO> runes = runeLibrary != null ? runeLibrary.Runes : null;
            if (runes == null || runes.Count == 0)
            {
                lastBestRuneId = string.Empty;
                lastBestScore = 0f;
                lastBestPassed = false;
                return (null, 0f, "no_runes");
            }

            RuneDefinitionSO bestRune = null;
            float bestScore = -1f;
            string bestReason = "no_candidate";

            for (int i = 0; i < runes.Count; i++)
            {
                RuneDefinitionSO rune = runes[i];
                if (rune == null)
                {
                    continue;
                }

                float requiredPass = rune.PassScore;
                float score;
                string reason;

                if (rune.UseReferenceStrokeComparison)
                {
                    score = lightningReferenceMatcher.Score(rune, stroke, out reason);
                }
                else
                {
                    score = scorer.Score(rune, stroke, out reason);
                }

                if (!string.IsNullOrWhiteSpace(rune.Id))
                {
                    lastScoreByRuneId[rune.Id] = score;
                    passScoreByRuneId[rune.Id] = requiredPass;
                }

                if (debugBuilder != null)
                {
                    if (debugBuilder.Length > 0)
                    {
                        debugBuilder.Append(" | ");
                    }

                    debugBuilder.Append(rune.DisplayName);
                    debugBuilder.Append(':');
                    debugBuilder.Append(score.ToString("0.000"));
                    debugBuilder.Append('/');
                    debugBuilder.Append(requiredPass.ToString("0.000"));
                    debugBuilder.Append(' ');
                    debugBuilder.Append('(');
                    debugBuilder.Append(reason);
                    debugBuilder.Append(')');
                }

                if (bestRune == null || score > bestScore)
                {
                    bestRune = rune;
                    bestScore = score;
                    bestReason = reason;
                }
            }

            lastBestRuneId = bestRune != null ? bestRune.Id : string.Empty;
            lastBestScore = Mathf.Max(0f, bestScore);
            lastBestPassed = bestRune != null && bestScore >= Mathf.Max(GetRequiredPassScore(bestRune), minimumAcceptScore);

            if (debugBuilder != null)
            {
                Debug.Log($"[RunePipeline] scores => {debugBuilder}", this);
            }

            return (bestRune, Mathf.Max(0f, bestScore), bestReason);
        }

        private float GetRequiredPassScore(RuneDefinitionSO rune)
        {
            if (rune == null)
            {
                return 1f;
            }

            if (!string.IsNullOrWhiteSpace(rune.Id) && passScoreByRuneId.TryGetValue(rune.Id, out float pass))
            {
                return pass;
            }

            return GetConfiguredPassScore(rune);
        }

        private CastContext BuildCastContext()
        {
            return new CastContext
            {
                caster = caster != null ? caster : transform,
                aimLock = aimLockController,
                enemyMask = enemyMask,
                wallMask = wallMask,
                groundMask = groundMask,
                cam = cachedCamera != null ? cachedCamera : Camera.main,
                stateReporter = message => runeCastController?.ReportState(message),
            };
        }

        private void EnsureRecognizers()
        {
            if (scorer == null)
            {
                scorer = new RuneGuideScorer();
            }

            if (lightningReferenceMatcher == null)
            {
                lightningReferenceMatcher = new LightningReferenceMatcher();
            }
        }

        private void ResolveReferences()
        {
            if (magicCircleDrawController == null)
            {
                magicCircleDrawController = GetComponent<MagicCircleDrawController>();
            }

            if (runeCastController == null)
            {
                runeCastController = GetComponent<RuneCastController>();
            }

            if (spellRunner == null)
            {
                spellRunner = GetComponent<SpellRunner>();
                if (spellRunner == null)
                {
                    spellRunner = gameObject.AddComponent<SpellRunner>();
                }
            }

            if (aimLockController == null)
            {
                aimLockController = GetComponent<AimLockController>();
            }

            if (caster == null)
            {
                caster = transform;
            }

            if (cachedCamera == null)
            {
                cachedCamera = Camera.main;
            }

            if (runeLibrary == null)
            {
                runeLibrary = TryFindObject<RuneLibrarySO>("RuneLibrary");
            }

            if (spellLibrary == null)
            {
                spellLibrary = TryFindObject<SpellLibrarySO>("SpellLibrary");
            }

#if UNITY_EDITOR
            if (runeLibrary == null)
            {
                runeLibrary = AssetDatabase.LoadAssetAtPath<RuneLibrarySO>(
                    "Assets/_Project/ScriptableObjects/Runes/RuneLibrary.asset");
            }

            if (spellLibrary == null)
            {
                spellLibrary = AssetDatabase.LoadAssetAtPath<SpellLibrarySO>(
                    "Assets/_Project/ScriptableObjects/Spells/SpellLibrary.asset");
            }
#endif
        }

        private static T TryFindObject<T>(string assetName) where T : ScriptableObject
        {
            T[] loaded = Resources.FindObjectsOfTypeAll<T>();
            for (int i = 0; i < loaded.Length; i++)
            {
                T candidate = loaded[i];
                if (candidate == null)
                {
                    continue;
                }

                if (candidate.name == assetName)
                {
                    return candidate;
                }
            }

            return null;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            minimumAcceptScore = Mathf.Clamp01(minimumAcceptScore);
            lowScoreFallbackMin = Mathf.Clamp01(lowScoreFallbackMin);
            scoreCacheLifetime = Mathf.Max(0.1f, scoreCacheLifetime);

            if (runeLibrary == null)
            {
                runeLibrary = AssetDatabase.LoadAssetAtPath<RuneLibrarySO>(
                    "Assets/_Project/ScriptableObjects/Runes/RuneLibrary.asset");
            }

            if (spellLibrary == null)
            {
                spellLibrary = AssetDatabase.LoadAssetAtPath<SpellLibrarySO>(
                    "Assets/_Project/ScriptableObjects/Spells/SpellLibrary.asset");
            }
        }
#endif
    }
}
