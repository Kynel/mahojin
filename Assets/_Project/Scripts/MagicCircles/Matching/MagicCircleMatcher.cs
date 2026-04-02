using System.Collections.Generic;
using DuckovProto.MagicCircles.Data;
using UnityEngine;

namespace DuckovProto.MagicCircles.Matching
{
    public sealed class MagicCircleMatcher
    {
        private readonly List<Vector2> preparedReference = new List<Vector2>(256);
        private readonly List<Vector2> preparedUser = new List<Vector2>(256);
        private readonly List<Vector2> candidateUser = new List<Vector2>(256);
        private readonly List<Vector2> rotatedUser = new List<Vector2>(256);

        public void EvaluateAll(MagicCircleLibrarySO library, IReadOnlyList<Vector2> userStrokeNorm, List<MagicCircleMatchResult> output)
        {
            output.Clear();
            if (library == null || library.Circles == null)
            {
                return;
            }

            IReadOnlyList<MagicCircleDefinitionSO> circles = library.Circles;
            for (int i = 0; i < circles.Count; i++)
            {
                MagicCircleDefinitionSO circle = circles[i];
                if (circle == null || !circle.Enabled)
                {
                    continue;
                }

                output.Add(EvaluateCircle(circle, userStrokeNorm));
            }

            output.Sort((a, b) => b.Score.CompareTo(a.Score));
        }

        public MagicCircleMatchResult EvaluateCircle(MagicCircleDefinitionSO circle, IReadOnlyList<Vector2> userStrokeNorm)
        {
            MagicCircleMatchResult invalid = new MagicCircleMatchResult
            {
                Circle = circle,
                MatchedId = circle != null ? circle.Id : string.Empty,
                MatchedDisplayName = circle != null ? circle.DisplayName : "Unknown",
                Threshold = circle != null ? circle.PassThreshold : 1f,
                Score = 0f,
                Passed = false,
                Reason = "Invalid circle",
                AlignedReference = new Vector2[0],
                AlignedUser = new Vector2[0],
            };

            if (circle == null)
            {
                return invalid;
            }

            if (userStrokeNorm == null || userStrokeNorm.Count < 2)
            {
                invalid.Reason = "Stroke too short";
                return invalid;
            }

            if (!circle.HasReferenceStroke)
            {
                invalid.Reason = "Missing reference";
                return invalid;
            }

            StrokeMathSimple.PrepareStroke(
                circle.ReferenceStrokeNorm,
                circle.ResampleCount,
                circle.AutoNormalizeTranslation,
                circle.AutoNormalizeScale,
                preparedReference);

            StrokeMathSimple.PrepareStroke(
                userStrokeNorm,
                circle.ResampleCount,
                circle.AutoNormalizeTranslation,
                circle.AutoNormalizeScale,
                preparedUser);

            if (preparedReference.Count < 2 || preparedUser.Count < 2)
            {
                invalid.Reason = "Stroke too short";
                return invalid;
            }

            MagicCircleMatchResult best = EvaluateVariant(circle, preparedReference, preparedUser, false);
            if (circle.AllowReverseStroke)
            {
                StrokeMathSimple.ReverseInto(preparedUser, candidateUser);
                MagicCircleMatchResult reversed = EvaluateVariant(circle, preparedReference, candidateUser, true);
                if (reversed.Score > best.Score)
                {
                    best = reversed;
                }
            }

            best.Passed = best.Score >= circle.PassThreshold;
            best.Reason = best.Passed
                ? "Pass"
                : $"Need {Mathf.RoundToInt(circle.PassThreshold * 100f)}%";

            return best;
        }

        private MagicCircleMatchResult EvaluateVariant(
            MagicCircleDefinitionSO circle,
            IReadOnlyList<Vector2> reference,
            IReadOnlyList<Vector2> user,
            bool usedReverse)
        {
            if (circle.AutoNormalizeRotation)
            {
                float rotation = StrokeMathSimple.ComputeBestRotationRad(reference, user);
                StrokeMathSimple.RotateInto(user, rotation, rotatedUser);
            }
            else
            {
                StrokeMathSimple.CopyInto(user, rotatedUser);
            }

            float rmse = StrokeMathSimple.ComputeRmse(reference, rotatedUser);
            float score = Mathf.Clamp01(StrokeMathSimple.RmseToScore(rmse));

            return new MagicCircleMatchResult
            {
                Circle = circle,
                MatchedId = circle.Id,
                MatchedDisplayName = circle.DisplayName,
                Score = score,
                Threshold = circle.PassThreshold,
                Rmse = rmse,
                UsedReverse = usedReverse,
                AlignedReference = StrokeMathSimple.ToArray(reference),
                AlignedUser = StrokeMathSimple.ToArray(rotatedUser),
            };
        }
    }
}
