using System.Collections.Generic;
using DuckovProto.Runes.Data;
using UnityEngine;

namespace DuckovProto.Runes
{
    public sealed class LightningReferenceMatcher
    {
        private const int SampleCount = 128;
        private const float StrictFailMultiplier = 0.6f;

        private readonly List<Vector2> userResampled = new List<Vector2>(SampleCount);
        private readonly List<Vector2> userNormalized = new List<Vector2>(SampleCount);
        private readonly List<Vector2> userReversed = new List<Vector2>(SampleCount);
        private readonly List<Vector2> userAligned = new List<Vector2>(SampleCount);

        private readonly List<Vector2> referenceResampled = new List<Vector2>(SampleCount);
        private readonly List<Vector2> referenceNormalized = new List<Vector2>(SampleCount);

        private readonly HashSet<string> warnedMissingReferenceIds = new HashSet<string>();

        public float Score(RuneDefinitionSO lightningRune, IReadOnlyList<Vector2> userStrokeNorm, out string reason)
        {
            reason = "invalid_input";
            if (lightningRune == null || userStrokeNorm == null || userStrokeNorm.Count < 2)
            {
                return 0f;
            }

            IReadOnlyList<Vector2> reference = lightningRune.ReferenceStrokeNorm;
            if (reference == null || reference.Count < 2)
            {
                WarnMissingReference(lightningRune);
                reason = "missing_reference";
                return 0f;
            }

            StrokeMath.ResampleByArcLength(userStrokeNorm, SampleCount, userResampled);
            StrokeMath.ResampleByArcLength(reference, SampleCount, referenceResampled);

            if (!StrokeMath.NormalizeCenterScale(userResampled, userNormalized) ||
                !StrokeMath.NormalizeCenterScale(referenceResampled, referenceNormalized))
            {
                reason = "normalize_failed";
                return 0f;
            }

            float forwardScore = EvaluateAligned(
                lightningRune,
                userNormalized,
                out string forwardReason);

            BuildReversed(userNormalized, userReversed);
            float reverseScore = EvaluateAligned(
                lightningRune,
                userReversed,
                out string reverseReason);

            if (forwardScore >= reverseScore)
            {
                reason = "fwd:" + forwardReason;
                return forwardScore;
            }

            reason = "rev:" + reverseReason;
            return reverseScore;
        }

        private float EvaluateAligned(
            RuneDefinitionSO rune,
            IReadOnlyList<Vector2> userPoints,
            out string reason)
        {
            float angle = StrokeMath.BestFitRotation2D(referenceNormalized, userPoints);
            StrokeMath.ApplyRotation(userPoints, angle, userAligned);

            float rmse = StrokeMath.RMSE(referenceNormalized, userAligned);
            float chamfer = StrokeMath.Chamfer(referenceNormalized, userAligned);

            float tolRmse = Mathf.Max(0.0001f, rune.ReferenceRmseTol);
            float tolChamfer = Mathf.Max(0.0001f, rune.ReferenceChamferTol);
            float s1 = Mathf.Exp(-Mathf.Pow(rmse / tolRmse, 2f));
            float s2 = Mathf.Exp(-Mathf.Pow(chamfer / tolChamfer, 2f));

            bool strictPass = true;
            if (rune.EnforceStrictHorizontalSegment)
            {
                strictPass = EvaluateStrictHorizontal(rune, userAligned, out float strictAngle, out float strictYStd);
                reason = $"rmse={rmse:0.000},chamfer={chamfer:0.000},s1={s1:0.000},s2={s2:0.000},angle={strictAngle:0.0},yStd={strictYStd:0.000},strict={(strictPass ? "ok" : "fail")}";
            }
            else
            {
                reason = $"rmse={rmse:0.000},chamfer={chamfer:0.000},s1={s1:0.000},s2={s2:0.000},strict=off";
            }

            float score = Mathf.Clamp01(s1 * s2);
            if (!strictPass)
            {
                score *= StrictFailMultiplier;
            }

            return Mathf.Clamp01(score);
        }

        private static void BuildReversed(IReadOnlyList<Vector2> source, List<Vector2> output)
        {
            output.Clear();
            if (source == null)
            {
                return;
            }

            for (int i = source.Count - 1; i >= 0; i--)
            {
                output.Add(source[i]);
            }
        }

        private static bool EvaluateStrictHorizontal(
            RuneDefinitionSO rune,
            IReadOnlyList<Vector2> points,
            out float angle,
            out float yStd)
        {
            angle = 180f;
            yStd = 999f;

            if (points == null || points.Count < 2)
            {
                return false;
            }

            int maxIndex = points.Count - 1;
            int start = Mathf.Clamp(Mathf.Min(rune.StrictSegmentStartIdx, rune.StrictSegmentEndIdx), 0, maxIndex - 1);
            int end = Mathf.Clamp(Mathf.Max(rune.StrictSegmentStartIdx, rune.StrictSegmentEndIdx), start + 1, maxIndex);

            Vector2 segment = points[end] - points[start];
            if (segment.sqrMagnitude <= 0.000001f)
            {
                return false;
            }

            angle = Mathf.Abs(Mathf.Atan2(segment.y, segment.x) * Mathf.Rad2Deg);
            if (angle > 90f)
            {
                angle = 180f - angle;
            }

            float meanY = 0f;
            int count = end - start + 1;
            for (int i = start; i <= end; i++)
            {
                meanY += points[i].y;
            }

            meanY /= Mathf.Max(1, count);

            float variance = 0f;
            for (int i = start; i <= end; i++)
            {
                float dy = points[i].y - meanY;
                variance += dy * dy;
            }

            yStd = Mathf.Sqrt(variance / Mathf.Max(1, count));
            return angle <= rune.StrictAngleDeg && yStd <= rune.StrictYStd;
        }

        private void WarnMissingReference(RuneDefinitionSO rune)
        {
            string id = string.IsNullOrWhiteSpace(rune.Id) ? rune.name : rune.Id;
            if (warnedMissingReferenceIds.Contains(id))
            {
                return;
            }

            warnedMissingReferenceIds.Add(id);
            Debug.LogWarning($"[LightningReferenceMatcher] Missing referenceStrokeNorm for rune '{rune.DisplayName}' ({id}).", rune);
        }
    }
}
