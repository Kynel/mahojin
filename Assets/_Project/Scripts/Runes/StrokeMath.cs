using System.Collections.Generic;
using UnityEngine;

namespace DuckovProto.Runes
{
    public static class StrokeMath
    {
        public static void ResampleByArcLength(IReadOnlyList<Vector2> points, int sampleCount, List<Vector2> output)
        {
            output.Clear();
            if (points == null || points.Count == 0 || sampleCount <= 0)
            {
                return;
            }

            if (points.Count == 1)
            {
                for (int i = 0; i < sampleCount; i++)
                {
                    output.Add(points[0]);
                }

                return;
            }

            float[] cumulative = new float[points.Count];
            cumulative[0] = 0f;
            for (int i = 1; i < points.Count; i++)
            {
                cumulative[i] = cumulative[i - 1] + Vector2.Distance(points[i - 1], points[i]);
            }

            float totalLength = cumulative[points.Count - 1];
            if (totalLength <= Mathf.Epsilon)
            {
                for (int i = 0; i < sampleCount; i++)
                {
                    output.Add(points[0]);
                }

                return;
            }

            int seg = 1;
            for (int i = 0; i < sampleCount; i++)
            {
                float t = sampleCount == 1 ? 0f : i / (float)(sampleCount - 1);
                float target = t * totalLength;
                while (seg < cumulative.Length && cumulative[seg] < target)
                {
                    seg++;
                }

                int b = Mathf.Clamp(seg, 1, points.Count - 1);
                int a = b - 1;
                float l0 = cumulative[a];
                float l1 = cumulative[b];
                float range = Mathf.Max(Mathf.Epsilon, l1 - l0);
                float lerp = Mathf.Clamp01((target - l0) / range);
                output.Add(Vector2.Lerp(points[a], points[b], lerp));
            }
        }

        public static bool NormalizeCenterScale(IReadOnlyList<Vector2> points, List<Vector2> output)
        {
            output.Clear();
            if (points == null || points.Count == 0)
            {
                return false;
            }

            Vector2 centroid = Vector2.zero;
            for (int i = 0; i < points.Count; i++)
            {
                centroid += points[i];
            }

            centroid /= Mathf.Max(1, points.Count);

            float maxRadius = 0f;
            for (int i = 0; i < points.Count; i++)
            {
                Vector2 centered = points[i] - centroid;
                float mag = centered.magnitude;
                if (mag > maxRadius)
                {
                    maxRadius = mag;
                }
            }

            if (maxRadius <= Mathf.Epsilon)
            {
                return false;
            }

            float inv = 1f / maxRadius;
            for (int i = 0; i < points.Count; i++)
            {
                output.Add((points[i] - centroid) * inv);
            }

            return true;
        }

        public static float BestFitRotation2D(IReadOnlyList<Vector2> referencePoints, IReadOnlyList<Vector2> userPoints)
        {
            int count = Mathf.Min(
                referencePoints != null ? referencePoints.Count : 0,
                userPoints != null ? userPoints.Count : 0);

            if (count <= 1)
            {
                return 0f;
            }

            float dot = 0f;
            float cross = 0f;
            for (int i = 0; i < count; i++)
            {
                Vector2 r = referencePoints[i];
                Vector2 u = userPoints[i];
                dot += (u.x * r.x) + (u.y * r.y);
                cross += (u.x * r.y) - (u.y * r.x);
            }

            return Mathf.Atan2(cross, dot);
        }

        public static void ApplyRotation(IReadOnlyList<Vector2> points, float angleRad, List<Vector2> output)
        {
            output.Clear();
            if (points == null)
            {
                return;
            }

            float c = Mathf.Cos(angleRad);
            float s = Mathf.Sin(angleRad);
            for (int i = 0; i < points.Count; i++)
            {
                Vector2 p = points[i];
                output.Add(new Vector2((p.x * c) - (p.y * s), (p.x * s) + (p.y * c)));
            }
        }

        public static float RMSE(IReadOnlyList<Vector2> a, IReadOnlyList<Vector2> b)
        {
            int count = Mathf.Min(a != null ? a.Count : 0, b != null ? b.Count : 0);
            if (count <= 0)
            {
                return 1f;
            }

            float sumSq = 0f;
            for (int i = 0; i < count; i++)
            {
                Vector2 d = a[i] - b[i];
                sumSq += d.sqrMagnitude;
            }

            return Mathf.Sqrt(sumSq / count);
        }

        public static float Chamfer(IReadOnlyList<Vector2> a, IReadOnlyList<Vector2> b)
        {
            int countA = a != null ? a.Count : 0;
            int countB = b != null ? b.Count : 0;
            if (countA <= 0 || countB <= 0)
            {
                return 1f;
            }

            float ab = DirectedChamfer(a, b);
            float ba = DirectedChamfer(b, a);
            return 0.5f * (ab + ba);
        }

        private static float DirectedChamfer(IReadOnlyList<Vector2> from, IReadOnlyList<Vector2> to)
        {
            if (from == null || to == null || from.Count == 0 || to.Count == 0)
            {
                return 1f;
            }

            float sum = 0f;
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

                sum += Mathf.Sqrt(minSq);
            }

            return sum / Mathf.Max(1, from.Count);
        }
    }
}
