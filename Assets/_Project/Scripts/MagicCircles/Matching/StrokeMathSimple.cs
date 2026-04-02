using System.Collections.Generic;
using UnityEngine;

namespace DuckovProto.MagicCircles.Matching
{
    public static class StrokeMathSimple
    {
        private const float Epsilon = 0.0001f;

        public static void PrepareStroke(
            IReadOnlyList<Vector2> source,
            int resampleCount,
            bool normalizeTranslation,
            bool normalizeScale,
            List<Vector2> output)
        {
            if (output == null)
            {
                return;
            }

            Resample(source, Mathf.Max(2, resampleCount), output);
            if (output.Count == 0)
            {
                return;
            }

            if (normalizeTranslation)
            {
                Vector2 centroid = ComputeCentroid(output);
                for (int i = 0; i < output.Count; i++)
                {
                    output[i] -= centroid;
                }
            }

            if (normalizeScale)
            {
                NormalizeScale(output);
            }
        }

        public static void ReverseInto(IReadOnlyList<Vector2> source, List<Vector2> output)
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

        public static void CopyInto(IReadOnlyList<Vector2> source, List<Vector2> output)
        {
            output.Clear();
            if (source == null)
            {
                return;
            }

            for (int i = 0; i < source.Count; i++)
            {
                output.Add(source[i]);
            }
        }

        public static float ComputeBestRotationRad(IReadOnlyList<Vector2> reference, IReadOnlyList<Vector2> candidate)
        {
            if (reference == null || candidate == null)
            {
                return 0f;
            }

            int count = Mathf.Min(reference.Count, candidate.Count);
            if (count < 2)
            {
                return 0f;
            }

            float dot = 0f;
            float cross = 0f;
            for (int i = 0; i < count; i++)
            {
                Vector2 a = candidate[i];
                Vector2 b = reference[i];
                dot += Vector2.Dot(a, b);
                cross += a.x * b.y - a.y * b.x;
            }

            return Mathf.Atan2(cross, dot);
        }

        public static void RotateInto(IReadOnlyList<Vector2> source, float angleRad, List<Vector2> output)
        {
            output.Clear();
            if (source == null)
            {
                return;
            }

            float cos = Mathf.Cos(angleRad);
            float sin = Mathf.Sin(angleRad);
            for (int i = 0; i < source.Count; i++)
            {
                Vector2 p = source[i];
                output.Add(new Vector2(
                    p.x * cos - p.y * sin,
                    p.x * sin + p.y * cos));
            }
        }

        public static float ComputeRmse(IReadOnlyList<Vector2> a, IReadOnlyList<Vector2> b)
        {
            if (a == null || b == null)
            {
                return float.MaxValue;
            }

            int count = Mathf.Min(a.Count, b.Count);
            if (count < 2)
            {
                return float.MaxValue;
            }

            float sum = 0f;
            for (int i = 0; i < count; i++)
            {
                sum += (a[i] - b[i]).sqrMagnitude;
            }

            return Mathf.Sqrt(sum / count);
        }

        public static float RmseToScore(float rmse, float tolerance = 0.35f)
        {
            float safeTolerance = Mathf.Max(0.01f, tolerance);
            float normalized = rmse / safeTolerance;
            return Mathf.Exp(-(normalized * normalized));
        }

        public static Vector2[] ToArray(IReadOnlyList<Vector2> source)
        {
            if (source == null || source.Count == 0)
            {
                return new Vector2[0];
            }

            Vector2[] output = new Vector2[source.Count];
            for (int i = 0; i < source.Count; i++)
            {
                output[i] = source[i];
            }

            return output;
        }

        private static void Resample(IReadOnlyList<Vector2> source, int count, List<Vector2> output)
        {
            output.Clear();
            if (source == null || source.Count == 0)
            {
                return;
            }

            List<Vector2> filtered = new List<Vector2>(source.Count);
            filtered.Add(source[0]);
            for (int i = 1; i < source.Count; i++)
            {
                Vector2 point = source[i];
                if ((point - filtered[filtered.Count - 1]).sqrMagnitude > Epsilon * Epsilon)
                {
                    filtered.Add(point);
                }
            }

            if (filtered.Count == 1)
            {
                for (int i = 0; i < count; i++)
                {
                    output.Add(filtered[0]);
                }

                return;
            }

            List<float> cumulative = new List<float>(filtered.Count) { 0f };
            float totalLength = 0f;
            for (int i = 1; i < filtered.Count; i++)
            {
                totalLength += Vector2.Distance(filtered[i - 1], filtered[i]);
                cumulative.Add(totalLength);
            }

            if (totalLength <= Epsilon)
            {
                for (int i = 0; i < count; i++)
                {
                    output.Add(filtered[0]);
                }

                return;
            }

            float step = totalLength / (count - 1);
            int segmentIndex = 0;
            for (int sampleIndex = 0; sampleIndex < count; sampleIndex++)
            {
                float targetDistance = sampleIndex == count - 1 ? totalLength : step * sampleIndex;
                while (segmentIndex < cumulative.Count - 2 && cumulative[segmentIndex + 1] < targetDistance)
                {
                    segmentIndex++;
                }

                float segmentStart = cumulative[segmentIndex];
                float segmentEnd = cumulative[segmentIndex + 1];
                float segmentLength = Mathf.Max(Epsilon, segmentEnd - segmentStart);
                float t = Mathf.Clamp01((targetDistance - segmentStart) / segmentLength);
                Vector2 sample = Vector2.Lerp(filtered[segmentIndex], filtered[segmentIndex + 1], t);
                output.Add(sample);
            }
        }

        private static Vector2 ComputeCentroid(IReadOnlyList<Vector2> points)
        {
            if (points == null || points.Count == 0)
            {
                return Vector2.zero;
            }

            Vector2 sum = Vector2.zero;
            for (int i = 0; i < points.Count; i++)
            {
                sum += points[i];
            }

            return sum / points.Count;
        }

        private static void NormalizeScale(List<Vector2> points)
        {
            float maxMagnitude = 0f;
            for (int i = 0; i < points.Count; i++)
            {
                float magnitude = points[i].magnitude;
                if (magnitude > maxMagnitude)
                {
                    maxMagnitude = magnitude;
                }
            }

            if (maxMagnitude <= Epsilon)
            {
                return;
            }

            for (int i = 0; i < points.Count; i++)
            {
                points[i] /= maxMagnitude;
            }
        }
    }
}
