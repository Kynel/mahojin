using System.Collections.Generic;
using DuckovProto.Runes.Data;
using UnityEngine;

namespace DuckovProto.Runes
{
    public sealed class RuneResolver
    {
        private readonly RuneGuideScorer scorer;

        public RuneResolver(RuneGuideScorer scorer = null)
        {
            this.scorer = scorer ?? new RuneGuideScorer();
        }

        public (RuneDefinitionSO best, float bestScore, string reason) ResolveBest(
            IReadOnlyList<RuneDefinitionSO> runes,
            IReadOnlyList<Vector2> userStroke)
        {
            (RuneDefinitionSO best, float bestScore, string bestReason) = ResolveBestAllowLowScore(runes, userStroke);

            if (best == null)
            {
                return (null, 0f, "no_valid_rune");
            }

            if (bestScore < best.PassScore)
            {
                return (null, bestScore, $"below_pass:{best.DisplayName}:{bestReason}");
            }

            return (best, bestScore, $"pass:{best.DisplayName}:{bestReason}");
        }

        public (RuneDefinitionSO best, float bestScore, string reason) ResolveBestAllowLowScore(
            IReadOnlyList<RuneDefinitionSO> runes,
            IReadOnlyList<Vector2> userStroke)
        {
            if (runes == null || runes.Count == 0)
            {
                return (null, 0f, "no_runes");
            }

            RuneDefinitionSO best = null;
            float bestScore = 0f;
            string bestReason = "no_candidate";

            for (int i = 0; i < runes.Count; i++)
            {
                RuneDefinitionSO rune = runes[i];
                if (rune == null)
                {
                    continue;
                }

                float score = scorer.Score(rune, userStroke, out string reason);
                if (best == null || score > bestScore)
                {
                    best = rune;
                    bestScore = score;
                    bestReason = reason;
                }
            }

            if (best == null)
            {
                return (null, 0f, "no_valid_rune");
            }

            return (best, bestScore, $"best:{best.DisplayName}:{bestReason}");
        }

        public (RuneDefinitionSO best, float bestScore, string reason) ResolveBest(
            RuneLibrarySO library,
            IReadOnlyList<Vector2> userStroke)
        {
            if (library == null)
            {
                return (null, 0f, "missing_library");
            }

            return ResolveBest(library.Runes, userStroke);
        }

        public (RuneDefinitionSO best, float bestScore, string reason) ResolveBestAllowLowScore(
            RuneLibrarySO library,
            IReadOnlyList<Vector2> userStroke)
        {
            if (library == null)
            {
                return (null, 0f, "missing_library");
            }

            return ResolveBestAllowLowScore(library.Runes, userStroke);
        }
    }
}
