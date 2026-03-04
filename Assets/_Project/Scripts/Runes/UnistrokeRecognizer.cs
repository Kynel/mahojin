using System.Collections.Generic;
using UnityEngine;

namespace DuckovProto.Runes
{
    public sealed class UnistrokeRecognizer
    {
        public readonly struct RecognitionResult
        {
            public readonly string Name;
            public readonly float Score;
            public readonly float Distance;

            public RecognitionResult(string name, float score, float distance)
            {
                Name = name;
                Score = score;
                Distance = distance;
            }
        }

        private sealed class Template
        {
            public readonly string Name;
            public readonly List<Vector2> Points;

            public Template(string name, List<Vector2> points)
            {
                Name = name;
                Points = points;
            }
        }

        private const float SquareSize = 1f;
        private const float HalfDiagonal = 0.70710677f; // sqrt(2) / 2
        private const float AngleRange = 0.7853982f; // +-45deg in rad
        private const float AnglePrecision = 0.034906585f; // 2deg in rad
        private const float Phi = 0.61803398875f;

        private readonly List<Template> templates = new List<Template>();
        private readonly int sampleCount;

        public UnistrokeRecognizer(int sampleCount = 64)
        {
            this.sampleCount = Mathf.Max(8, sampleCount);
        }

        public void AddTemplate(string name, IReadOnlyList<Vector2> points2D)
        {
            if (string.IsNullOrEmpty(name) || points2D == null || points2D.Count < 2)
            {
                return;
            }

            List<Vector2> normalized = Normalize(points2D);
            templates.Add(new Template(name, normalized));
        }

        public RecognitionResult Recognize(IReadOnlyList<Vector2> points2D)
        {
            if (points2D == null || points2D.Count < 2 || templates.Count == 0)
            {
                return new RecognitionResult("Unknown", 0f, float.MaxValue);
            }

            List<Vector2> candidate = Normalize(points2D);
            float bestDistance = float.MaxValue;
            string bestName = "Unknown";

            for (int i = 0; i < templates.Count; i++)
            {
                float distance = DistanceAtBestAngle(
                    candidate,
                    templates[i].Points,
                    -AngleRange,
                    AngleRange,
                    AnglePrecision);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    bestName = templates[i].Name;
                }
            }

            float score = Mathf.Clamp01(1f - (bestDistance / HalfDiagonal));
            return new RecognitionResult(bestName, score, bestDistance);
        }

        private List<Vector2> Normalize(IReadOnlyList<Vector2> input)
        {
            List<Vector2> points = Resample(input, sampleCount);
            float angle = IndicativeAngle(points);
            points = RotateBy(points, -angle);
            points = ScaleToSquare(points, SquareSize);
            points = TranslateToOrigin(points);
            return points;
        }

        private static List<Vector2> Resample(IReadOnlyList<Vector2> input, int n)
        {
            List<Vector2> points = new List<Vector2>(input.Count);
            for (int i = 0; i < input.Count; i++)
            {
                points.Add(input[i]);
            }

            float interval = PathLength(points) / (n - 1);
            float distanceSoFar = 0f;
            List<Vector2> result = new List<Vector2>(n) { points[0] };

            int index = 1;
            while (index < points.Count)
            {
                Vector2 previous = points[index - 1];
                Vector2 current = points[index];
                float segmentLength = Vector2.Distance(previous, current);

                if (segmentLength <= Mathf.Epsilon)
                {
                    index++;
                    continue;
                }

                if (distanceSoFar + segmentLength >= interval)
                {
                    float t = (interval - distanceSoFar) / segmentLength;
                    Vector2 newPoint = Vector2.Lerp(previous, current, t);
                    result.Add(newPoint);
                    points.Insert(index, newPoint);
                    distanceSoFar = 0f;
                    index++;
                }
                else
                {
                    distanceSoFar += segmentLength;
                    index++;
                }
            }

            while (result.Count < n)
            {
                result.Add(points[points.Count - 1]);
            }

            if (result.Count > n)
            {
                result.RemoveRange(n, result.Count - n);
            }

            return result;
        }

        private static float IndicativeAngle(IReadOnlyList<Vector2> points)
        {
            Vector2 centroid = Centroid(points);
            return Mathf.Atan2(centroid.y - points[0].y, centroid.x - points[0].x);
        }

        private static List<Vector2> RotateBy(IReadOnlyList<Vector2> points, float angle)
        {
            Vector2 centroid = Centroid(points);
            float cos = Mathf.Cos(angle);
            float sin = Mathf.Sin(angle);
            List<Vector2> rotated = new List<Vector2>(points.Count);

            for (int i = 0; i < points.Count; i++)
            {
                Vector2 p = points[i] - centroid;
                float x = (p.x * cos) - (p.y * sin);
                float y = (p.x * sin) + (p.y * cos);
                rotated.Add(new Vector2(x, y) + centroid);
            }

            return rotated;
        }

        private static List<Vector2> ScaleToSquare(IReadOnlyList<Vector2> points, float size)
        {
            float minX = float.MaxValue;
            float minY = float.MaxValue;
            float maxX = float.MinValue;
            float maxY = float.MinValue;

            for (int i = 0; i < points.Count; i++)
            {
                Vector2 p = points[i];
                minX = Mathf.Min(minX, p.x);
                minY = Mathf.Min(minY, p.y);
                maxX = Mathf.Max(maxX, p.x);
                maxY = Mathf.Max(maxY, p.y);
            }

            float width = Mathf.Max(maxX - minX, Mathf.Epsilon);
            float height = Mathf.Max(maxY - minY, Mathf.Epsilon);
            List<Vector2> scaled = new List<Vector2>(points.Count);

            for (int i = 0; i < points.Count; i++)
            {
                Vector2 p = points[i];
                float x = p.x * (size / width);
                float y = p.y * (size / height);
                scaled.Add(new Vector2(x, y));
            }

            return scaled;
        }

        private static List<Vector2> TranslateToOrigin(IReadOnlyList<Vector2> points)
        {
            Vector2 centroid = Centroid(points);
            List<Vector2> translated = new List<Vector2>(points.Count);
            for (int i = 0; i < points.Count; i++)
            {
                translated.Add(points[i] - centroid);
            }

            return translated;
        }

        private static float PathDistance(IReadOnlyList<Vector2> a, IReadOnlyList<Vector2> b)
        {
            int count = Mathf.Min(a.Count, b.Count);
            if (count == 0)
            {
                return float.MaxValue;
            }

            float total = 0f;
            for (int i = 0; i < count; i++)
            {
                total += Vector2.Distance(a[i], b[i]);
            }

            return total / count;
        }

        private static float DistanceAtBestAngle(
            IReadOnlyList<Vector2> points,
            IReadOnlyList<Vector2> templatePoints,
            float angleA,
            float angleB,
            float angleThreshold)
        {
            float x1 = (Phi * angleA) + ((1f - Phi) * angleB);
            float f1 = DistanceAtAngle(points, templatePoints, x1);
            float x2 = ((1f - Phi) * angleA) + (Phi * angleB);
            float f2 = DistanceAtAngle(points, templatePoints, x2);

            while (Mathf.Abs(angleB - angleA) > angleThreshold)
            {
                if (f1 < f2)
                {
                    angleB = x2;
                    x2 = x1;
                    f2 = f1;
                    x1 = (Phi * angleA) + ((1f - Phi) * angleB);
                    f1 = DistanceAtAngle(points, templatePoints, x1);
                }
                else
                {
                    angleA = x1;
                    x1 = x2;
                    f1 = f2;
                    x2 = ((1f - Phi) * angleA) + (Phi * angleB);
                    f2 = DistanceAtAngle(points, templatePoints, x2);
                }
            }

            return Mathf.Min(f1, f2);
        }

        private static float DistanceAtAngle(
            IReadOnlyList<Vector2> points,
            IReadOnlyList<Vector2> templatePoints,
            float angle)
        {
            List<Vector2> rotated = RotateBy(points, angle);
            return PathDistance(rotated, templatePoints);
        }

        private static float PathLength(IReadOnlyList<Vector2> points)
        {
            float length = 0f;
            for (int i = 1; i < points.Count; i++)
            {
                length += Vector2.Distance(points[i - 1], points[i]);
            }

            return length;
        }

        private static Vector2 Centroid(IReadOnlyList<Vector2> points)
        {
            Vector2 centroid = Vector2.zero;
            for (int i = 0; i < points.Count; i++)
            {
                centroid += points[i];
            }

            return centroid / points.Count;
        }
    }
}
