using System.Collections.Generic;
using System.Text;
using DuckovProto.Runes.Data;
using UnityEngine;

namespace DuckovProto.Runes
{
    public sealed class RuneGuideScorer
    {
        public const int DefaultResampleCount = 128;

        private readonly List<Vector2> userSamples = new List<Vector2>(DefaultResampleCount);
        private readonly List<Vector2> guideSamples = new List<Vector2>(DefaultResampleCount);
        private readonly List<Vector2> alignedUserSamples = new List<Vector2>(DefaultResampleCount);

        public float Score(RuneDefinitionSO rune, IReadOnlyList<Vector2> userStrokeNorm, out string reason)
        {
            reason = "invalid_input";
            if (rune == null || userStrokeNorm == null || userStrokeNorm.Count < 2)
            {
                return 0f;
            }

            IReadOnlyList<Vector2> guide = rune.GuidePolylineNorm;
            if (guide == null || guide.Count < 2)
            {
                reason = "missing_guide";
                return 0f;
            }

            ResampleArcLength(userStrokeNorm, DefaultResampleCount, userSamples);
            ResampleArcLength(guide, DefaultResampleCount, guideSamples);
            AlignUserToGuideBounds(userSamples, guideSamples, alignedUserSamples);

            ExtraRuleFlags flags = rune.ExtraRuleFlagMask;
            float shapeScore = ComputeShapeScore(guideSamples, alignedUserSamples, rune.BaseTol, flags, out float chamferDistance);
            float strictScore = ComputeStrictSegmentScore(
                rune.StrictSegments,
                guideSamples,
                alignedUserSamples,
                guide.Count,
                rune.BaseTol);
            float gateScore = ComputeGateScore(rune.Gates, alignedUserSamples, flags);

            bool hasStrict = rune.StrictSegments != null && rune.StrictSegments.Count > 0;
            bool hasGate = rune.Gates != null && rune.Gates.Count > 0;
            float shapeWeight = flags.HasFlag(ExtraRuleFlags.FocusIntersections) ? 0.52f : 0.62f;
            float strictWeight = flags.HasFlag(ExtraRuleFlags.FocusIntersections) ? 0.20f : 0.24f;
            float gateWeight = flags.HasFlag(ExtraRuleFlags.FocusIntersections) ? 0.28f : 0.14f;

            if (!hasStrict)
            {
                shapeWeight += strictWeight;
                strictWeight = 0f;
                strictScore = 0f;
            }

            if (!hasGate)
            {
                shapeWeight += gateWeight;
                gateWeight = 0f;
                gateScore = 0f;
            }

            float score = (shapeScore * shapeWeight) + (strictScore * strictWeight) + (gateScore * gateWeight);
            bool strictHorizontalPass = EvaluateStrictHorizontalRule(flags, rune.StrictSegments, alignedUserSamples, guide.Count);
            if (!strictHorizontalPass)
            {
                score *= 0.25f;
            }

            score = Mathf.Clamp01(score);
            reason = BuildReason(shapeScore, strictScore, gateScore, chamferDistance, strictHorizontalPass, score, rune.PassScore);
            return score;
        }

        private static string BuildReason(
            float shape,
            float strict,
            float gate,
            float chamferDist,
            bool strictHorizontalPass,
            float score,
            float passScore)
        {
            StringBuilder sb = new StringBuilder(160);
            sb.Append("shape=").Append(shape.ToString("0.000"));
            sb.Append(",strict=").Append(strict.ToString("0.000"));
            sb.Append(",gate=").Append(gate.ToString("0.000"));
            sb.Append(",chamfer=").Append(chamferDist.ToString("0.000"));
            if (!strictHorizontalPass)
            {
                sb.Append(",strictHorizontal=fail");
            }

            sb.Append(",final=").Append(score.ToString("0.000"));
            sb.Append(",pass=").Append(passScore.ToString("0.000"));
            return sb.ToString();
        }

        private static float ComputeShapeScore(
            IReadOnlyList<Vector2> guide,
            IReadOnlyList<Vector2> user,
            float baseTol,
            ExtraRuleFlags flags,
            out float chamferDistance)
        {
            float forward = DirectedChamfer(user, guide, flags);
            float backward = DirectedChamfer(guide, user, flags);
            chamferDistance = (forward + backward) * 0.5f;
            float tol = Mathf.Max(0.01f, baseTol);
            return 1f - Mathf.Clamp01(chamferDistance / tol);
        }

        private static float DirectedChamfer(IReadOnlyList<Vector2> from, IReadOnlyList<Vector2> to, ExtraRuleFlags flags)
        {
            float weightedDistance = 0f;
            float totalWeight = 0f;
            bool focusRight = flags.HasFlag(ExtraRuleFlags.FocusRightSide);

            for (int i = 0; i < from.Count; i++)
            {
                Vector2 p = from[i];
                float minSq = float.MaxValue;
                for (int j = 0; j < to.Count; j++)
                {
                    float sq = (to[j] - p).sqrMagnitude;
                    if (sq < minSq)
                    {
                        minSq = sq;
                    }
                }

                float w = 1f;
                if (focusRight)
                {
                    if (p.x > 0f)
                    {
                        w = 1.35f;
                    }
                    else if (p.x < 0f && p.y < 0f)
                    {
                        w = 0.75f;
                    }
                }

                weightedDistance += Mathf.Sqrt(minSq) * w;
                totalWeight += w;
            }

            if (totalWeight <= 0f)
            {
                return 0f;
            }

            return weightedDistance / totalWeight;
        }

        private static float ComputeStrictSegmentScore(
            IReadOnlyList<StrictSegment> strictSegments,
            IReadOnlyList<Vector2> guideResampled,
            IReadOnlyList<Vector2> userResampled,
            int guideRawCount,
            float baseTol)
        {
            if (strictSegments == null || strictSegments.Count == 0)
            {
                return 0f;
            }

            float weighted = 0f;
            float totalWeight = 0f;
            int maxGuideRawIndex = Mathf.Max(1, guideRawCount - 1);
            int maxResampledIndex = Mathf.Max(1, guideResampled.Count - 1);

            for (int i = 0; i < strictSegments.Count; i++)
            {
                StrictSegment segment = strictSegments[i];
                int rawStart = Mathf.Clamp(Mathf.Min(segment.startIdx, segment.endIdx), 0, maxGuideRawIndex);
                int rawEnd = Mathf.Clamp(Mathf.Max(segment.startIdx, segment.endIdx), 0, maxGuideRawIndex);
                int sampleStart = Mathf.RoundToInt((rawStart / (float)maxGuideRawIndex) * maxResampledIndex);
                int sampleEnd = Mathf.RoundToInt((rawEnd / (float)maxGuideRawIndex) * maxResampledIndex);
                sampleStart = Mathf.Clamp(sampleStart, 0, maxResampledIndex);
                sampleEnd = Mathf.Clamp(sampleEnd, sampleStart, maxResampledIndex);

                int count = Mathf.Max(1, sampleEnd - sampleStart + 1);
                float accum = 0f;
                for (int k = 0; k < count; k++)
                {
                    int idx = sampleStart + k;
                    accum += Vector2.Distance(userResampled[idx], guideResampled[idx]);
                }

                float avgDist = accum / count;
                float segTol = Mathf.Max(0.01f, baseTol * Mathf.Max(0.01f, segment.tolMultiplier));
                float segScore = 1f - Mathf.Clamp01(avgDist / segTol);

                float w = Mathf.Max(0f, segment.weight);
                weighted += segScore * w;
                totalWeight += w;
            }

            if (totalWeight <= 0f)
            {
                return 0f;
            }

            return weighted / totalWeight;
        }

        private static float ComputeGateScore(IReadOnlyList<Gate> gates, IReadOnlyList<Vector2> userResampled, ExtraRuleFlags flags)
        {
            if (gates == null || gates.Count == 0)
            {
                return 0f;
            }

            bool emphasize = flags.HasFlag(ExtraRuleFlags.FocusIntersections);
            float weighted = 0f;
            float totalWeight = 0f;
            for (int i = 0; i < gates.Count; i++)
            {
                Gate gate = gates[i];
                float minDist = float.MaxValue;
                for (int j = 0; j < userResampled.Count; j++)
                {
                    float dist = Vector2.Distance(userResampled[j], gate.pos);
                    if (dist < minDist)
                    {
                        minDist = dist;
                    }
                }

                float gateTol = Mathf.Max(0.01f, gate.tol);
                float hit = 1f - Mathf.Clamp01(minDist / gateTol);
                float w = Mathf.Max(0f, gate.weight);
                if (emphasize)
                {
                    w *= 1.8f;
                }

                weighted += hit * w;
                totalWeight += w;
            }

            if (totalWeight <= 0f)
            {
                return 0f;
            }

            return weighted / totalWeight;
        }

        private static bool EvaluateStrictHorizontalRule(
            ExtraRuleFlags runeFlags,
            IReadOnlyList<StrictSegment> strictSegments,
            IReadOnlyList<Vector2> userResampled,
            int guideRawCount)
        {
            bool runeRequires = runeFlags.HasFlag(ExtraRuleFlags.StrictHorizontal);
            bool hasStrictSegments = strictSegments != null && strictSegments.Count > 0;
            int maxGuideRawIndex = Mathf.Max(1, guideRawCount - 1);
            int maxResampledIndex = Mathf.Max(1, userResampled.Count - 1);

            if (!runeRequires && !hasStrictSegments)
            {
                return true;
            }

            bool anyChecked = false;
            if (hasStrictSegments)
            {
                for (int i = 0; i < strictSegments.Count; i++)
                {
                    StrictSegment segment = strictSegments[i];
                    bool segRequires = segment.extraRule.HasFlag(ExtraRuleFlags.StrictHorizontal);
                    if (!segRequires && !runeRequires)
                    {
                        continue;
                    }

                    anyChecked = true;
                    int rawStart = Mathf.Clamp(Mathf.Min(segment.startIdx, segment.endIdx), 0, maxGuideRawIndex);
                    int rawEnd = Mathf.Clamp(Mathf.Max(segment.startIdx, segment.endIdx), 0, maxGuideRawIndex);
                    int sampleStart = Mathf.RoundToInt((rawStart / (float)maxGuideRawIndex) * maxResampledIndex);
                    int sampleEnd = Mathf.RoundToInt((rawEnd / (float)maxGuideRawIndex) * maxResampledIndex);
                    sampleStart = Mathf.Clamp(sampleStart, 0, maxResampledIndex);
                    sampleEnd = Mathf.Clamp(sampleEnd, sampleStart, maxResampledIndex);

                    if (!SegmentIsHorizontal(userResampled, sampleStart, sampleEnd))
                    {
                        return false;
                    }
                }
            }

            if (!anyChecked && runeRequires)
            {
                return SegmentIsHorizontal(userResampled, 0, maxResampledIndex);
            }

            return true;
        }

        private static bool SegmentIsHorizontal(IReadOnlyList<Vector2> points, int start, int end)
        {
            if (points == null || points.Count < 2 || end <= start)
            {
                return false;
            }

            Vector2 dir = points[end] - points[start];
            float angleDeg = Mathf.Abs(Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);
            if (angleDeg > 90f)
            {
                angleDeg = 180f - angleDeg;
            }

            float yStd = ComputeYStd(points, start, end);
            return angleDeg <= 12f && yStd <= 0.06f;
        }

        private static float ComputeYStd(IReadOnlyList<Vector2> points, int start, int end)
        {
            int count = Mathf.Max(1, end - start + 1);
            float mean = 0f;
            for (int i = start; i <= end; i++)
            {
                mean += points[i].y;
            }

            mean /= count;
            float variance = 0f;
            for (int i = start; i <= end; i++)
            {
                float d = points[i].y - mean;
                variance += d * d;
            }

            variance /= count;
            return Mathf.Sqrt(variance);
        }

        private static void ResampleArcLength(IReadOnlyList<Vector2> input, int sampleCount, List<Vector2> output)
        {
            output.Clear();
            if (input == null || input.Count == 0 || sampleCount <= 0)
            {
                return;
            }

            if (input.Count == 1)
            {
                for (int i = 0; i < sampleCount; i++)
                {
                    output.Add(input[0]);
                }

                return;
            }

            float[] cumulative = new float[input.Count];
            cumulative[0] = 0f;
            for (int i = 1; i < input.Count; i++)
            {
                cumulative[i] = cumulative[i - 1] + Vector2.Distance(input[i - 1], input[i]);
            }

            float totalLength = cumulative[input.Count - 1];
            if (totalLength <= Mathf.Epsilon)
            {
                Vector2 first = input[0];
                for (int i = 0; i < sampleCount; i++)
                {
                    output.Add(first);
                }

                return;
            }

            int seg = 1;
            for (int i = 0; i < sampleCount; i++)
            {
                float t = i / (float)(sampleCount - 1);
                float target = t * totalLength;
                while (seg < cumulative.Length && cumulative[seg] < target)
                {
                    seg++;
                }

                int b = Mathf.Clamp(seg, 1, input.Count - 1);
                int a = b - 1;
                float l0 = cumulative[a];
                float l1 = cumulative[b];
                float range = Mathf.Max(Mathf.Epsilon, l1 - l0);
                float lerp = Mathf.Clamp01((target - l0) / range);
                output.Add(Vector2.Lerp(input[a], input[b], lerp));
            }
        }

        private static void AlignUserToGuideBounds(
            IReadOnlyList<Vector2> user,
            IReadOnlyList<Vector2> guide,
            List<Vector2> output)
        {
            output.Clear();
            if (user == null || guide == null || user.Count == 0)
            {
                return;
            }

            ComputeBounds(user, out Vector2 userMin, out Vector2 userMax);
            ComputeBounds(guide, out Vector2 guideMin, out Vector2 guideMax);

            Vector2 userCenter = (userMin + userMax) * 0.5f;
            Vector2 guideCenter = (guideMin + guideMax) * 0.5f;

            float userSize = Mathf.Max(userMax.x - userMin.x, userMax.y - userMin.y);
            float guideSize = Mathf.Max(guideMax.x - guideMin.x, guideMax.y - guideMin.y);
            float scale = (userSize > 0.0001f && guideSize > 0.0001f) ? (guideSize / userSize) : 1f;

            for (int i = 0; i < user.Count; i++)
            {
                Vector2 p = user[i];
                Vector2 normalized = (p - userCenter) * scale + guideCenter;
                output.Add(normalized);
            }
        }

        private static void ComputeBounds(IReadOnlyList<Vector2> points, out Vector2 min, out Vector2 max)
        {
            if (points == null || points.Count == 0)
            {
                min = Vector2.zero;
                max = Vector2.zero;
                return;
            }

            Vector2 first = points[0];
            min = first;
            max = first;

            for (int i = 1; i < points.Count; i++)
            {
                Vector2 p = points[i];
                if (p.x < min.x) min.x = p.x;
                if (p.y < min.y) min.y = p.y;
                if (p.x > max.x) max.x = p.x;
                if (p.y > max.y) max.y = p.y;
            }
        }
    }
}
