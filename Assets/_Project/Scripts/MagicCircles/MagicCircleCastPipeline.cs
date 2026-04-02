using System.Collections.Generic;
using System.Text;
using DuckovProto.Combat;
using DuckovProto.MagicCircles.Data;
using DuckovProto.MagicCircles.Matching;
using DuckovProto.Spells;
using DuckovProto.Spells.Data;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace DuckovProto.MagicCircles
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(MagicCircleDrawController))]
    [RequireComponent(typeof(MagicCircleCastController))]
    public class MagicCircleCastPipeline : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private MagicCircleDrawController magicCircleDrawController;
        [SerializeField] private MagicCircleCastController magicCircleCastController;
        [SerializeField] private AimLockController aimLockController;
        [SerializeField] private Transform caster;

        [Header("Data")]
        [SerializeField] private MagicCircleLibrarySO magicCircleLibrary;
        [SerializeField] private SpellLibrarySO spellLibrary;

        [Header("Cast Context Masks")]
        [SerializeField] private LayerMask enemyMask = 1 << 8;
        [SerializeField] private LayerMask wallMask = 1 << 10;
        [SerializeField] private LayerMask groundMask = 1 << 7;

        [Header("Feedback")]
        [SerializeField] private float recentEvaluationLifetime = 5f;
        [SerializeField] private float resolvedMessageDuration = 0.35f;
        [SerializeField] private float unclearMessageDuration = 0.45f;
        [SerializeField] private bool logResolveDetails;

        private readonly MagicCircleMatcher matcher = new MagicCircleMatcher();
        private readonly List<MagicCircleMatchResult> lastResults = new List<MagicCircleMatchResult>(8);
        private readonly List<Vector2> lastSubmittedStroke = new List<Vector2>(256);

        private Camera cachedCamera;
        private float lastEvaluationTime = -999f;
        private string lastBestCircleId = string.Empty;
        private float lastBestScore;
        private bool lastBestPassed;
        private string lastFailureReason = string.Empty;

        public MagicCircleLibrarySO MagicCircleLibrary => magicCircleLibrary;
        public SpellLibrarySO SpellLibrary => spellLibrary;
        public IReadOnlyList<MagicCircleMatchResult> LastResults => lastResults;
        public IReadOnlyList<Vector2> LastSubmittedStroke => lastSubmittedStroke;
        public float LastEvaluationTime => lastEvaluationTime;
        public string LastBestCircleId => lastBestCircleId;
        public float LastBestScore => lastBestScore;
        public bool LastBestPassed => lastBestPassed;
        public string LastFailureReason => lastFailureReason;

        protected virtual void Awake()
        {
            ResolveReferences();
        }

        protected virtual void OnEnable()
        {
            ResolveReferences();
            if (magicCircleDrawController != null)
            {
                magicCircleDrawController.StrokeSubmittedNorm += HandleStrokeSubmitted;
            }
        }

        protected virtual void OnDisable()
        {
            if (magicCircleDrawController != null)
            {
                magicCircleDrawController.StrokeSubmittedNorm -= HandleStrokeSubmitted;
            }
        }

        public bool TryGetLastResult(string circleId, out MagicCircleMatchResult result, out bool recent)
        {
            result = null;
            recent = Time.time - lastEvaluationTime <= Mathf.Max(0.1f, recentEvaluationLifetime);
            if (string.IsNullOrWhiteSpace(circleId))
            {
                return false;
            }

            for (int i = 0; i < lastResults.Count; i++)
            {
                MagicCircleMatchResult candidate = lastResults[i];
                if (candidate != null && candidate.Circle != null && candidate.Circle.Id == circleId)
                {
                    result = candidate;
                    return true;
                }
            }

            return false;
        }

        public string BuildTopCandidatesText(int count = 3)
        {
            if (lastResults.Count == 0)
            {
                return "No recent stroke";
            }

            int limit = Mathf.Clamp(count, 1, lastResults.Count);
            StringBuilder builder = new StringBuilder(128);
            for (int i = 0; i < limit; i++)
            {
                MagicCircleMatchResult result = lastResults[i];
                if (result == null || result.Circle == null)
                {
                    continue;
                }

                if (builder.Length > 0)
                {
                    builder.Append(" | ");
                }

                builder.Append(i + 1)
                    .Append(". ")
                    .Append(result.Circle.DisplayName)
                    .Append(' ')
                    .Append(Mathf.RoundToInt(result.Score * 100f))
                    .Append('%');
            }

            return builder.ToString();
        }

        private void HandleStrokeSubmitted(IReadOnlyList<Vector2> stroke)
        {
            ResolveReferences();
            if (magicCircleLibrary == null || spellLibrary == null || magicCircleCastController == null)
            {
                magicCircleCastController?.ReportState("Magic circle data missing", unclearMessageDuration);
                return;
            }

            lastSubmittedStroke.Clear();
            if (stroke != null)
            {
                for (int i = 0; i < stroke.Count; i++)
                {
                    lastSubmittedStroke.Add(stroke[i]);
                }
            }

            matcher.EvaluateAll(magicCircleLibrary, stroke, lastResults);
            lastEvaluationTime = Time.time;

            if (lastResults.Count == 0)
            {
                lastBestCircleId = string.Empty;
                lastBestScore = 0f;
                lastBestPassed = false;
                lastFailureReason = "No magic circles";
                magicCircleCastController.ReportState("No magic circles", unclearMessageDuration);
                return;
            }

            MagicCircleMatchResult best = lastResults[0];
            lastBestCircleId = best.Circle != null ? best.Circle.Id : string.Empty;
            lastBestScore = best.Score;
            lastBestPassed = best.Passed;
            lastFailureReason = best.Passed ? string.Empty : best.Reason;

            if (logResolveDetails)
            {
                Debug.Log($"[MagicCirclePipeline] {BuildTopCandidatesText(3)}", this);
            }

            if (!best.Passed)
            {
                magicCircleCastController.ReportState(
                    $"Unclear {Mathf.RoundToInt(best.Score * 100f)}%/{Mathf.RoundToInt(best.Threshold * 100f)}%",
                    unclearMessageDuration);
                return;
            }

            SpellDefinitionSO spell = spellLibrary.GetById(best.Circle.LinkedSpellId);
            if (spell == null)
            {
                lastBestPassed = false;
                lastFailureReason = "Missing spell";
                magicCircleCastController.ReportState("Missing spell", unclearMessageDuration);
                return;
            }

            CastContext castContext = BuildCastContext();
            bool casted = magicCircleCastController.TryCastSpell(spell, castContext, best.Circle.DisplayName, out string failReason);
            if (casted)
            {
                magicCircleCastController.ReportState($"{best.Circle.DisplayName} ({best.Score:0.00})", resolvedMessageDuration);
                return;
            }

            lastBestPassed = false;
            lastFailureReason = failReason;
            if (!string.IsNullOrWhiteSpace(failReason))
            {
                magicCircleCastController.ReportState(failReason, unclearMessageDuration);
            }
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
                stateReporter = message => magicCircleCastController?.ReportState(message),
            };
        }

        private void ResolveReferences()
        {
            if (magicCircleDrawController == null)
            {
                magicCircleDrawController = GetComponent<MagicCircleDrawController>();
            }

            if (magicCircleCastController == null)
            {
                magicCircleCastController = GetComponent<MagicCircleCastController>();
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
        }

#if UNITY_EDITOR
        protected virtual void OnValidate()
        {
            recentEvaluationLifetime = Mathf.Max(0.1f, recentEvaluationLifetime);

            if (magicCircleLibrary == null)
            {
                magicCircleLibrary = AssetDatabase.LoadAssetAtPath<MagicCircleLibrarySO>(
                    "Assets/_Project/ScriptableObjects/MagicCircles/MagicCircleLibrary.asset");
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
