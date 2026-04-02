using System.Collections.Generic;
using DuckovProto.MagicCircles.Data;
using DuckovProto.MagicCircles.Matching;
using DuckovProto.Spells.Data;
using UnityEngine;
using UnityEngine.UI;

namespace DuckovProto.UI
{
    public sealed partial class MagicCircleGuidePanel
    {
        private void RefreshCircleList()
        {
            visibleCircles.Clear();
            MagicCircleLibrarySO library = magicCircleCastPipeline != null ? magicCircleCastPipeline.MagicCircleLibrary : null;
            if (library == null)
            {
                library = magicCircleLibraryFallback;
            }

            if (library == null || library.Circles == null)
            {
                EnsureCardPool(0);
                return;
            }

            for (int i = 0; i < library.Circles.Count; i++)
            {
                MagicCircleDefinitionSO circle = library.Circles[i];
                if (circle != null && circle.Enabled)
                {
                    visibleCircles.Add(circle);
                }
            }

            EnsureCardPool(visibleCircles.Count);
            for (int i = 0; i < cards.Count; i++)
            {
                bool active = i < visibleCircles.Count;
                cards[i].Root.gameObject.SetActive(active);
                if (active)
                {
                    UpdateCard(cards[i], visibleCircles[i], i);
                }
            }
        }

        private void RefreshSummary()
        {
            if (summaryText == null)
            {
                return;
            }

            if (magicCircleCastPipeline == null || magicCircleCastPipeline.LastResults.Count == 0)
            {
                summaryText.text = "Right panel shows the guide line and your last stroke. F2 hide.";
                return;
            }

            MagicCircleMatchResult best = magicCircleCastPipeline.LastResults[0];
            string verdict = best.Passed ? "PASS" : "FAIL";
            summaryText.text =
                $"Best: {best.MatchedDisplayName}  {Mathf.RoundToInt(best.Score * 100f)}%  {verdict}  |  F2 Hide";
        }

        private void RefreshSelection()
        {
            if (visibleCircles.Count == 0)
            {
                selectedCircleId = string.Empty;
                return;
            }

            if (magicCircleCastPipeline != null &&
                magicCircleCastPipeline.LastEvaluationTime > lastSeenEvaluationTime)
            {
                lastSeenEvaluationTime = magicCircleCastPipeline.LastEvaluationTime;
                if (!string.IsNullOrWhiteSpace(magicCircleCastPipeline.LastBestCircleId))
                {
                    selectedCircleId = magicCircleCastPipeline.LastBestCircleId;
                }
            }

            if (!string.IsNullOrWhiteSpace(magicCircleCastPipeline != null ? magicCircleCastPipeline.LastBestCircleId : string.Empty) &&
                !ContainsCircle(selectedCircleId))
            {
                selectedCircleId = magicCircleCastPipeline.LastBestCircleId;
            }

            if (string.IsNullOrWhiteSpace(selectedCircleId) || !ContainsCircle(selectedCircleId))
            {
                selectedCircleId = visibleCircles[0].Id;
            }
        }

        private void RefreshDetail()
        {
            MagicCircleDefinitionSO circle = GetSelectedCircle();
            if (circle == null)
            {
                detailTitleText.text = "No Circle";
                detailSpellText.text = string.Empty;
                detailDescriptionText.text = "No magic circles are registered.";
                detailHintText.text = string.Empty;
                detailLegendText.text = string.Empty;
                detailStatusText.text = string.Empty;
                RenderPreview(detailPreview, null, null);
                return;
            }

            MagicCircleMatchResult result = null;
            bool hasRecent = magicCircleCastPipeline != null &&
                             magicCircleCastPipeline.TryGetLastResult(circle.Id, out result, out bool recent) &&
                             recent &&
                             Time.time - magicCircleCastPipeline.LastEvaluationTime <= Mathf.Max(0.1f, recentStrokeLifetime);

            detailTitleText.text = circle.DisplayName;
            detailTitleText.color = Color.Lerp(Color.white, circle.ThemeColor, 0.28f);
            detailSpellText.text = ResolveSpellDisplayName(circle);
            detailDescriptionText.text = circle.Description;
            detailHintText.text = string.IsNullOrWhiteSpace(circle.HowToDrawHint)
                ? "Trace the sample line in one stroke."
                : circle.HowToDrawHint;
            detailLegendText.text = "Guide = bright line   |   Your last stroke = mint line";

            if (hasRecent)
            {
                string verdict = result.Passed ? "PASS" : "FAIL";
                detailStatusText.text =
                    $"Match {Mathf.RoundToInt(result.Score * 100f)}% / Need {Mathf.RoundToInt(result.Threshold * 100f)}%  {verdict}";
                detailStatusText.color = result.Passed
                    ? new Color(0.68f, 1f, 0.74f, 1f)
                    : new Color(1f, 0.76f, 0.54f, 1f);
                RenderPreview(detailPreview, circle, result);
                return;
            }

            MagicCircleMatchResult previewOverlay = TryBuildPreviewOverlay(circle) ? previewFallbackResult : null;
            detailStatusText.text = $"Need {Mathf.RoundToInt(circle.PassThreshold * 100f)}%  No recent draw";
            detailStatusText.color = new Color(0.82f, 0.90f, 0.98f, 0.96f);
            if (previewOverlay != null)
            {
                detailStatusText.text = $"Need {Mathf.RoundToInt(circle.PassThreshold * 100f)}%  Last stroke shown for comparison";
            }

            RenderPreview(detailPreview, circle, previewOverlay);
        }

        private void UpdateCard(CircleCardView card, MagicCircleDefinitionSO circle, int index)
        {
            card.CircleId = circle.Id;
            card.Root.anchorMin = new Vector2(0f, 1f);
            card.Root.anchorMax = new Vector2(1f, 1f);
            card.Root.pivot = new Vector2(0f, 1f);
            card.Root.anchoredPosition = new Vector2(0f, -(index * 96f));
            card.Root.sizeDelta = new Vector2(0f, 96f); // Larger list items

            bool selected = circle.Id == selectedCircleId;
            bool best = magicCircleCastPipeline != null && circle.Id == magicCircleCastPipeline.LastBestCircleId;
            Color baseColor = selected
                ? new Color(0.18f, 0.24f, 0.34f, 0.95f)
                : best
                    ? new Color(0.16f, 0.20f, 0.28f, 0.92f)
                    : new Color(0.12f, 0.14f, 0.18f, 0.86f);
            card.Background.color = baseColor;

            card.NameText.text = circle.DisplayName;
            card.SpellText.text = ResolveSpellDisplayName(circle);

            MagicCircleMatchResult result = null;
            bool hasRecent = magicCircleCastPipeline != null &&
                             magicCircleCastPipeline.TryGetLastResult(circle.Id, out result, out bool recent) &&
                             recent &&
                             Time.time - magicCircleCastPipeline.LastEvaluationTime <= Mathf.Max(0.1f, recentStrokeLifetime);

            if (hasRecent)
            {
                string verdict = result.Passed ? "OK" : "Fail";
                card.ScoreText.text =
                    $"{Mathf.RoundToInt(result.Score * 100f)}% / {Mathf.RoundToInt(circle.PassThreshold * 100f)}%  {verdict}";
                card.ScoreText.color = result.Passed
                    ? new Color(0.70f, 1f, 0.74f, 1f)
                    : new Color(1f, 0.76f, 0.58f, 1f);
            }
            else
            {
                card.ScoreText.text = $"Need {Mathf.RoundToInt(circle.PassThreshold * 100f)}%";
                card.ScoreText.color = new Color(0.80f, 0.88f, 0.96f, 0.92f);
            }

            RenderPreview(card.Preview, circle, hasRecent && (selected || best) ? result : null);
        }

        private void RenderPreview(PreviewView preview, MagicCircleDefinitionSO circle, MagicCircleMatchResult result)
        {
            if (preview == null)
            {
                return;
            }

            float size = preview.Root.rect.width > 0f ? preview.Root.rect.width : preview.Root.sizeDelta.x;
            if (size <= 0f)
            {
                size = preview.Root.sizeDelta.x;
            }

            ConfigurePreviewFrame(preview, size);

            previewPoints.Clear();
            if (result != null && result.AlignedReference != null && result.AlignedReference.Length > 0)
            {
                for (int i = 0; i < result.AlignedReference.Length; i++)
                {
                    previewPoints.Add(result.AlignedReference[i]);
                }
            }
            else if (circle != null)
            {
                StrokeMathSimple.PrepareStroke(
                    circle.ReferenceStrokeNorm,
                    circle.ResampleCount,
                    circle.AutoNormalizeTranslation,
                    circle.AutoNormalizeScale,
                    previewPoints);
            }

            Color guideCore = circle != null
                ? Color.Lerp(new Color(1f, 0.98f, 0.92f, 1f), circle.ThemeColor, 0.18f)
                : new Color(0.96f, 0.95f, 0.92f, 1f);
            Color guideGlow = circle != null
                ? Color.Lerp(new Color(1f, 0.96f, 0.82f, 0.30f), circle.ThemeColor, 0.50f)
                : new Color(1f, 0.96f, 0.86f, 0.30f);
            guideGlow.a = 0.34f;

            float referenceThickness = Mathf.Max(2f, preview.ReferenceThickness);
            float glowThickness = referenceThickness * 2.15f;
            SetPreviewSegmentLine(preview.ReferenceGlowRoot, preview.ReferenceGlowSegments, previewPoints, size, 20f, glowThickness, guideGlow);
            SetPreviewSegmentLine(preview.ReferenceLineRoot, preview.ReferenceLineSegments, previewPoints, size, 20f, referenceThickness, guideCore);

            if (result != null && result.AlignedUser != null && result.AlignedUser.Length > 1)
            {
                uiPoints.Clear();
                for (int i = 0; i < result.AlignedUser.Length; i++)
                {
                    uiPoints.Add(result.AlignedUser[i]);
                }

                SetPreviewSegmentLine(
                    preview.UserLineRoot,
                    preview.UserLineSegments,
                    uiPoints,
                    size,
                    20f,
                    Mathf.Max(1.5f, preview.UserThickness),
                    new Color(0.72f, 1f, 0.90f, 0.96f));
            }
            else
            {
                SetPreviewSegmentLine(
                    preview.UserLineRoot,
                    preview.UserLineSegments,
                    null,
                    size,
                    20f,
                    Mathf.Max(1.5f, preview.UserThickness),
                    new Color(0.72f, 1f, 0.90f, 0.96f));
            }
        }

        private bool TryBuildPreviewOverlay(MagicCircleDefinitionSO circle)
        {
            if (circle == null || magicCircleCastPipeline == null)
            {
                return false;
            }

            if (Time.time - magicCircleCastPipeline.LastEvaluationTime > Mathf.Max(0.1f, recentStrokeLifetime))
            {
                return false;
            }

            IReadOnlyList<Vector2> lastSubmittedStroke = magicCircleCastPipeline.LastSubmittedStroke;
            if (lastSubmittedStroke == null || lastSubmittedStroke.Count < 2 || !circle.HasReferenceStroke)
            {
                return false;
            }

            StrokeMathSimple.PrepareStroke(
                circle.ReferenceStrokeNorm,
                circle.ResampleCount,
                circle.AutoNormalizeTranslation,
                circle.AutoNormalizeScale,
                preparedReference);

            StrokeMathSimple.PrepareStroke(
                lastSubmittedStroke,
                circle.ResampleCount,
                circle.AutoNormalizeTranslation,
                circle.AutoNormalizeScale,
                preparedUser);

            if (preparedReference.Count < 2 || preparedUser.Count < 2)
            {
                return false;
            }

            float bestRmse = float.MaxValue;
            if (TryEvaluateOverlayVariant(circle, preparedUser, ref bestRmse))
            {
                if (circle.AllowReverseStroke)
                {
                    StrokeMathSimple.ReverseInto(preparedUser, reversedUser);
                    TryEvaluateOverlayVariant(circle, reversedUser, ref bestRmse);
                }
            }

            if (bestOverlayUser.Count < 2)
            {
                return false;
            }

            previewFallbackResult.Circle = circle;
            previewFallbackResult.MatchedId = circle.Id;
            previewFallbackResult.MatchedDisplayName = circle.DisplayName;
            previewFallbackResult.Threshold = circle.PassThreshold;
            previewFallbackResult.AlignedReference = StrokeMathSimple.ToArray(preparedReference);
            previewFallbackResult.AlignedUser = StrokeMathSimple.ToArray(bestOverlayUser);
            previewFallbackResult.Rmse = bestRmse;
            previewFallbackResult.Score = StrokeMathSimple.RmseToScore(bestRmse);
            previewFallbackResult.Passed = previewFallbackResult.Score >= circle.PassThreshold;
            previewFallbackResult.Reason = "Preview";
            return true;
        }

        private bool TryEvaluateOverlayVariant(MagicCircleDefinitionSO circle, IReadOnlyList<Vector2> sourceUser, ref float bestRmse)
        {
            if (circle.AutoNormalizeRotation)
            {
                float rotation = StrokeMathSimple.ComputeBestRotationRad(preparedReference, sourceUser);
                StrokeMathSimple.RotateInto(sourceUser, rotation, rotatedUser);
            }
            else
            {
                StrokeMathSimple.CopyInto(sourceUser, rotatedUser);
            }

            float rmse = StrokeMathSimple.ComputeRmse(preparedReference, rotatedUser);
            if (rmse >= bestRmse)
            {
                return false;
            }

            bestRmse = rmse;
            StrokeMathSimple.CopyInto(rotatedUser, bestOverlayUser);
            return true;
        }

        private void ConfigurePreviewFrame(PreviewView preview, float size)
        {
            if (preview.Background != null)
            {
                preview.Background.color = new Color(1f, 0.98f, 0.92f, 0.045f);
            }

            if (preview.HorizontalAxis != null)
            {
                preview.HorizontalAxis.color = new Color(1f, 1f, 1f, 0.08f);
                RectTransform rect = preview.HorizontalAxis.rectTransform;
                rect.sizeDelta = new Vector2(size * 0.78f, 1.5f);
            }

            if (preview.VerticalAxis != null)
            {
                preview.VerticalAxis.color = new Color(1f, 1f, 1f, 0.08f);
                RectTransform rect = preview.VerticalAxis.rectTransform;
                rect.sizeDelta = new Vector2(1.5f, size * 0.78f);
            }

            circleFramePoints.Clear();
            float radius = size * 0.5f - 4f;
            for (int i = 0; i <= 64; i++)
            {
                float t = i / 64f;
                float angle = t * Mathf.PI * 2f;
                circleFramePoints.Add(new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius);
            }

            preview.Border.color = new Color(1f, 0.95f, 0.84f, 0.34f);
            preview.Border.SetPoints(circleFramePoints);
        }

        private void SetPreviewSegmentLine(
            RectTransform rootTransform,
            System.Collections.Generic.List<Image> segments,
            IReadOnlyList<Vector2> normalizedPoints,
            float size,
            float padding,
            float thickness,
            Color color)
        {
            if (rootTransform == null || segments == null)
            {
                return;
            }

            rootTransform.sizeDelta = new Vector2(size, size);

            uiPoints.Clear();
            if (normalizedPoints != null)
            {
                float radius = (size * 0.5f) - padding;
                for (int i = 0; i < normalizedPoints.Count; i++)
                {
                    Vector2 p = normalizedPoints[i];
                    uiPoints.Add(new Vector2(p.x * radius, p.y * radius));
                }
            }

            int needed = Mathf.Max(0, uiPoints.Count - 1);
            EnsureSegmentPool(rootTransform, segments, needed, color);
            for (int i = 0; i < segments.Count; i++)
            {
                bool active = i < needed;
                Image segment = segments[i];
                if (segment == null)
                {
                    continue;
                }

                segment.gameObject.SetActive(active);
                if (!active)
                {
                    continue;
                }

                Vector2 a = uiPoints[i];
                Vector2 b = uiPoints[i + 1];
                Vector2 d = b - a;
                float len = d.magnitude;
                if (len < 0.001f)
                {
                    segment.gameObject.SetActive(false);
                    continue;
                }

                RectTransform rect = segment.rectTransform;
                rect.anchoredPosition = (a + b) * 0.5f;
                rect.sizeDelta = new Vector2(len + thickness * 0.75f, thickness);
                rect.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
                segment.color = color;
            }
        }

        private string ResolveSpellDisplayName(MagicCircleDefinitionSO circle)
        {
            if (circle == null)
            {
                return string.Empty;
            }

            SpellLibrarySO spellLibrary = magicCircleCastPipeline != null ? magicCircleCastPipeline.SpellLibrary : null;
            SpellDefinitionSO spell = spellLibrary != null ? spellLibrary.GetById(circle.LinkedSpellId) : null;
            if (spell != null)
            {
                return spell.DisplayName;
            }

            return circle.LinkedSpellId;
        }

        private MagicCircleDefinitionSO GetSelectedCircle()
        {
            for (int i = 0; i < visibleCircles.Count; i++)
            {
                if (visibleCircles[i] != null && visibleCircles[i].Id == selectedCircleId)
                {
                    return visibleCircles[i];
                }
            }

            return null;
        }

        private bool ContainsCircle(string circleId)
        {
            if (string.IsNullOrWhiteSpace(circleId))
            {
                return false;
            }

            for (int i = 0; i < visibleCircles.Count; i++)
            {
                if (visibleCircles[i] != null && visibleCircles[i].Id == circleId)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
