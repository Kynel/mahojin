using System.Collections.Generic;
using DuckovProto.MagicCircles.Data;
using UnityEngine;
using UnityEngine.UI;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace DuckovProto.UI
{
    public sealed partial class MagicCircleGuidePanel
    {
        private void EnsureUi()
        {
            if (root != null)
            {
                return;
            }

            ResolveReferences();
            if (targetCanvas == null)
            {
                return;
            }

            RectTransform canvasRect = targetCanvas.GetComponent<RectTransform>();
            root = EnsureChildRect(canvasRect, "MagicCircleGuidePanelRoot");
            root.anchorMin = new Vector2(1f, 0.5f);
            root.anchorMax = new Vector2(1f, 0.5f);
            root.pivot = new Vector2(1f, 0.5f);
            root.anchoredPosition = new Vector2(-18f, 0f);
            root.sizeDelta = new Vector2(520f, 680f);

            Image background = EnsureImage(root, new Color(0.06f, 0.08f, 0.11f, 0.90f));
            background.raycastTarget = false;

            Text title = EnsureText(root, "Title", 22, TextAnchor.UpperLeft, new Color(1f, 0.95f, 0.84f, 0.98f));
            title.text = "Magic Circle Guide";
            RectTransform titleRect = title.rectTransform;
            titleRect.anchorMin = new Vector2(0f, 1f);
            titleRect.anchorMax = new Vector2(1f, 1f);
            titleRect.pivot = new Vector2(0f, 1f);
            titleRect.anchoredPosition = new Vector2(16f, -12f);
            titleRect.sizeDelta = new Vector2(-32f, 28f);

            summaryText = EnsureText(root, "Summary", 12, TextAnchor.UpperLeft, new Color(0.84f, 0.90f, 0.98f, 0.95f));
            RectTransform summaryRect = summaryText.rectTransform;
            summaryRect.anchorMin = new Vector2(0f, 1f);
            summaryRect.anchorMax = new Vector2(1f, 1f);
            summaryRect.pivot = new Vector2(0f, 1f);
            summaryRect.anchoredPosition = new Vector2(16f, -42f);
            summaryRect.sizeDelta = new Vector2(-32f, 32f);

            RectTransform listPanel = EnsureChildRect(root, "ListPanel");
            listPanel.anchorMin = new Vector2(0f, 1f);
            listPanel.anchorMax = new Vector2(0f, 1f);
            listPanel.pivot = new Vector2(0f, 1f);
            listPanel.anchoredPosition = new Vector2(16f, -86f);
            listPanel.sizeDelta = new Vector2(186f, 578f);
            Image listBg = EnsureImage(listPanel, new Color(0.10f, 0.12f, 0.16f, 0.84f));
            listBg.raycastTarget = false;

            Text listTitle = EnsureText(listPanel, "ListTitle", 14, TextAnchor.UpperLeft, new Color(0.95f, 0.95f, 0.95f, 0.98f));
            listTitle.text = "Available Circles";
            RectTransform listTitleRect = listTitle.rectTransform;
            listTitleRect.anchorMin = new Vector2(0f, 1f);
            listTitleRect.anchorMax = new Vector2(1f, 1f);
            listTitleRect.pivot = new Vector2(0f, 1f);
            listTitleRect.anchoredPosition = new Vector2(12f, -10f);
            listTitleRect.sizeDelta = new Vector2(-24f, 20f);

            listContent = EnsureChildRect(listPanel, "ListContent");
            listContent.anchorMin = new Vector2(0f, 1f);
            listContent.anchorMax = new Vector2(1f, 1f);
            listContent.pivot = new Vector2(0f, 1f);
            listContent.anchoredPosition = new Vector2(10f, -36f);
            listContent.sizeDelta = new Vector2(-20f, 0f);

            RectTransform detailPanel = EnsureChildRect(root, "DetailPanel");
            detailPanel.anchorMin = new Vector2(0f, 0f);
            detailPanel.anchorMax = new Vector2(1f, 1f);
            detailPanel.pivot = new Vector2(0f, 1f);
            detailPanel.offsetMin = new Vector2(216f, 14f);
            detailPanel.offsetMax = new Vector2(-16f, -86f);
            Image detailBg = EnsureImage(detailPanel, new Color(0.08f, 0.09f, 0.13f, 0.84f));
            detailBg.raycastTarget = false;

            detailTitleText = EnsureText(detailPanel, "DetailTitle", 22, TextAnchor.UpperLeft, Color.white);
            RectTransform detailTitleRect = detailTitleText.rectTransform;
            detailTitleRect.anchorMin = new Vector2(0f, 1f);
            detailTitleRect.anchorMax = new Vector2(1f, 1f);
            detailTitleRect.pivot = new Vector2(0f, 1f);
            detailTitleRect.anchoredPosition = new Vector2(16f, -12f);
            detailTitleRect.sizeDelta = new Vector2(-32f, 28f);

            detailSpellText = EnsureText(detailPanel, "DetailSpell", 13, TextAnchor.UpperLeft, new Color(0.80f, 0.88f, 0.98f, 0.96f));
            RectTransform detailSpellRect = detailSpellText.rectTransform;
            detailSpellRect.anchorMin = new Vector2(0f, 1f);
            detailSpellRect.anchorMax = new Vector2(1f, 1f);
            detailSpellRect.pivot = new Vector2(0f, 1f);
            detailSpellRect.anchoredPosition = new Vector2(16f, -42f);
            detailSpellRect.sizeDelta = new Vector2(-32f, 20f);

            RectTransform previewHost = EnsureChildRect(detailPanel, "DetailPreviewHost");
            previewHost.anchorMin = new Vector2(0.5f, 1f);
            previewHost.anchorMax = new Vector2(0.5f, 1f);
            previewHost.pivot = new Vector2(0.5f, 1f);
            previewHost.anchoredPosition = new Vector2(0f, -78f);
            previewHost.sizeDelta = new Vector2(332f, 332f);
            detailPreview = BuildPreview(previewHost, "DetailPreview", 332f, 10f, 5f);

            detailDescriptionText = EnsureText(detailPanel, "DetailDescription", 13, TextAnchor.UpperLeft, new Color(0.96f, 0.96f, 0.96f, 0.95f));
            RectTransform descriptionRect = detailDescriptionText.rectTransform;
            descriptionRect.anchorMin = new Vector2(0f, 0f);
            descriptionRect.anchorMax = new Vector2(1f, 0f);
            descriptionRect.pivot = new Vector2(0f, 0f);
            descriptionRect.anchoredPosition = new Vector2(16f, 126f);
            descriptionRect.sizeDelta = new Vector2(-32f, 66f);

            detailHintText = EnsureText(detailPanel, "DetailHint", 12, TextAnchor.UpperLeft, new Color(0.84f, 0.92f, 0.84f, 0.96f));
            RectTransform hintRect = detailHintText.rectTransform;
            hintRect.anchorMin = new Vector2(0f, 0f);
            hintRect.anchorMax = new Vector2(1f, 0f);
            hintRect.pivot = new Vector2(0f, 0f);
            hintRect.anchoredPosition = new Vector2(16f, 68f);
            hintRect.sizeDelta = new Vector2(-32f, 48f);

            detailLegendText = EnsureText(detailPanel, "DetailLegend", 12, TextAnchor.UpperLeft, new Color(0.88f, 0.92f, 0.98f, 0.92f));
            RectTransform legendRect = detailLegendText.rectTransform;
            legendRect.anchorMin = new Vector2(0f, 0f);
            legendRect.anchorMax = new Vector2(1f, 0f);
            legendRect.pivot = new Vector2(0f, 0f);
            legendRect.anchoredPosition = new Vector2(16f, 52f);
            legendRect.sizeDelta = new Vector2(-32f, 18f);

            detailStatusText = EnsureText(detailPanel, "DetailStatus", 14, TextAnchor.UpperLeft, Color.white);
            RectTransform statusRect = detailStatusText.rectTransform;
            statusRect.anchorMin = new Vector2(0f, 0f);
            statusRect.anchorMax = new Vector2(1f, 0f);
            statusRect.pivot = new Vector2(0f, 0f);
            statusRect.anchoredPosition = new Vector2(16f, 22f);
            statusRect.sizeDelta = new Vector2(-32f, 36f);
        }

        private void EnsureCardPool(int count)
        {
            for (int i = cards.Count; i < count; i++)
            {
                cards.Add(CreateCard(i));
            }
        }

        private CircleCardView CreateCard(int index)
        {
            CircleCardView card = new CircleCardView();
            card.Root = EnsureChildRect(listContent, $"CircleCard_{index}");
            card.Background = EnsureImage(card.Root, new Color(0.12f, 0.14f, 0.18f, 0.86f));
            card.Button = card.Root.gameObject.GetComponent<Button>();
            if (card.Button == null)
            {
                card.Button = card.Root.gameObject.AddComponent<Button>();
            }

            card.Button.targetGraphic = card.Background;
            int capturedIndex = index;
            card.Button.onClick.AddListener(() =>
            {
                if (capturedIndex < visibleCircles.Count && visibleCircles[capturedIndex] != null)
                {
                    selectedCircleId = visibleCircles[capturedIndex].Id;
                }
            });

            RectTransform previewHost = EnsureChildRect(card.Root, "Preview");
            previewHost.anchorMin = new Vector2(0f, 0.5f);
            previewHost.anchorMax = new Vector2(0f, 0.5f);
            previewHost.pivot = new Vector2(0f, 0.5f);
            previewHost.anchoredPosition = new Vector2(10f, 0f);
            previewHost.sizeDelta = new Vector2(64f, 64f);
            card.Preview = BuildPreview(previewHost, "MiniPreview", 64f, 5f, 2.5f);

            card.NameText = EnsureText(card.Root, "Name", 15, TextAnchor.UpperLeft, Color.white);
            RectTransform nameRect = card.NameText.rectTransform;
            nameRect.anchorMin = new Vector2(0f, 1f);
            nameRect.anchorMax = new Vector2(1f, 1f);
            nameRect.pivot = new Vector2(0f, 1f);
            nameRect.anchoredPosition = new Vector2(82f, -14f);
            nameRect.sizeDelta = new Vector2(-90f, 20f);

            card.SpellText = EnsureText(card.Root, "Spell", 12, TextAnchor.UpperLeft, new Color(0.82f, 0.90f, 0.98f, 0.96f));
            RectTransform spellRect = card.SpellText.rectTransform;
            spellRect.anchorMin = new Vector2(0f, 1f);
            spellRect.anchorMax = new Vector2(1f, 1f);
            spellRect.pivot = new Vector2(0f, 1f);
            spellRect.anchoredPosition = new Vector2(82f, -36f);
            spellRect.sizeDelta = new Vector2(-90f, 18f);

            card.ScoreText = EnsureText(card.Root, "Score", 13, TextAnchor.UpperLeft, Color.white);
            RectTransform scoreRect = card.ScoreText.rectTransform;
            scoreRect.anchorMin = new Vector2(0f, 1f);
            scoreRect.anchorMax = new Vector2(1f, 1f);
            scoreRect.pivot = new Vector2(0f, 1f);
            scoreRect.anchoredPosition = new Vector2(82f, -58f);
            scoreRect.sizeDelta = new Vector2(-90f, 26f);

            return card;
        }

        private PreviewView BuildPreview(RectTransform parent, string name, float size, float referenceThickness, float userThickness)
        {
            PreviewView preview = new PreviewView();
            preview.Root = EnsureChildRect(parent, name);
            preview.Root.anchorMin = new Vector2(0.5f, 0.5f);
            preview.Root.anchorMax = new Vector2(0.5f, 0.5f);
            preview.Root.pivot = new Vector2(0.5f, 0.5f);
            preview.Root.anchoredPosition = Vector2.zero;
            preview.Root.sizeDelta = new Vector2(size, size);
            preview.ReferenceThickness = referenceThickness;
            preview.UserThickness = userThickness;

            RectTransform fillRect = EnsureChildRect(preview.Root, "Background");
            fillRect.anchorMin = new Vector2(0.5f, 0.5f);
            fillRect.anchorMax = new Vector2(0.5f, 0.5f);
            fillRect.pivot = new Vector2(0.5f, 0.5f);
            fillRect.anchoredPosition = Vector2.zero;
            fillRect.sizeDelta = new Vector2(size, size);
            preview.Background = EnsureCircleFill(fillRect, new Color(1f, 1f, 1f, 0.025f));

            RectTransform borderRect = EnsureChildRect(preview.Root, "Border");
            borderRect.anchorMin = new Vector2(0.5f, 0.5f);
            borderRect.anchorMax = new Vector2(0.5f, 0.5f);
            borderRect.pivot = new Vector2(0.5f, 0.5f);
            borderRect.anchoredPosition = Vector2.zero;
            borderRect.sizeDelta = new Vector2(size, size);
            preview.Border = EnsurePolyline(borderRect, new Color(1f, 0.95f, 0.84f, 0.34f), 2f);

            RectTransform hAxis = EnsureChildRect(preview.Root, "HorizontalAxis");
            hAxis.anchorMin = new Vector2(0.5f, 0.5f);
            hAxis.anchorMax = new Vector2(0.5f, 0.5f);
            hAxis.pivot = new Vector2(0.5f, 0.5f);
            hAxis.anchoredPosition = Vector2.zero;
            preview.HorizontalAxis = EnsureImage(hAxis, new Color(1f, 1f, 1f, 0.10f));

            RectTransform vAxis = EnsureChildRect(preview.Root, "VerticalAxis");
            vAxis.anchorMin = new Vector2(0.5f, 0.5f);
            vAxis.anchorMax = new Vector2(0.5f, 0.5f);
            vAxis.pivot = new Vector2(0.5f, 0.5f);
            vAxis.anchoredPosition = Vector2.zero;
            preview.VerticalAxis = EnsureImage(vAxis, new Color(1f, 1f, 1f, 0.10f));

            preview.ReferenceGlowRoot = EnsureChildRect(preview.Root, "ReferenceGlow");
            preview.ReferenceGlowRoot.anchorMin = new Vector2(0.5f, 0.5f);
            preview.ReferenceGlowRoot.anchorMax = new Vector2(0.5f, 0.5f);
            preview.ReferenceGlowRoot.pivot = new Vector2(0.5f, 0.5f);
            preview.ReferenceGlowRoot.anchoredPosition = Vector2.zero;
            preview.ReferenceGlowRoot.sizeDelta = new Vector2(size, size);

            preview.ReferenceLineRoot = EnsureChildRect(preview.Root, "ReferenceLine");
            preview.ReferenceLineRoot.anchorMin = new Vector2(0.5f, 0.5f);
            preview.ReferenceLineRoot.anchorMax = new Vector2(0.5f, 0.5f);
            preview.ReferenceLineRoot.pivot = new Vector2(0.5f, 0.5f);
            preview.ReferenceLineRoot.anchoredPosition = Vector2.zero;
            preview.ReferenceLineRoot.sizeDelta = new Vector2(size, size);

            preview.UserLineRoot = EnsureChildRect(preview.Root, "UserLine");
            preview.UserLineRoot.anchorMin = new Vector2(0.5f, 0.5f);
            preview.UserLineRoot.anchorMax = new Vector2(0.5f, 0.5f);
            preview.UserLineRoot.pivot = new Vector2(0.5f, 0.5f);
            preview.UserLineRoot.anchoredPosition = Vector2.zero;
            preview.UserLineRoot.sizeDelta = new Vector2(size, size);

            return preview;
        }

        private void EnsureSegmentPool(RectTransform parent, List<Image> segments, int needed, Color color)
        {
            while (segments.Count < needed)
            {
                RectTransform segmentRect = EnsureChildRect(parent, $"Seg_{segments.Count}");
                segmentRect.anchorMin = new Vector2(0.5f, 0.5f);
                segmentRect.anchorMax = new Vector2(0.5f, 0.5f);
                segmentRect.pivot = new Vector2(0.5f, 0.5f);
                segmentRect.anchoredPosition = Vector2.zero;
                segmentRect.localRotation = Quaternion.identity;
                Image segment = EnsureImage(segmentRect, color);
                segment.raycastTarget = false;
                segments.Add(segment);
            }
        }

        private static RectTransform EnsureChildRect(Transform parent, string name)
        {
            Transform child = parent.Find(name);
            RectTransform rect;
            if (child == null)
            {
                GameObject go = new GameObject(name, typeof(RectTransform));
                go.transform.SetParent(parent, false);
                rect = go.GetComponent<RectTransform>();
            }
            else
            {
                rect = child.GetComponent<RectTransform>();
                if (rect == null)
                {
                    rect = child.gameObject.AddComponent<RectTransform>();
                }
            }

            rect.localScale = Vector3.one;
            rect.localRotation = Quaternion.identity;
            return rect;
        }

        private Text EnsureText(Transform parent, string name, int fontSize, TextAnchor anchor, Color color)
        {
            RectTransform rect = EnsureChildRect(parent, name);
            Text text = rect.GetComponent<Text>();
            if (text == null)
            {
                text = rect.gameObject.AddComponent<Text>();
            }

            text.font = uiFont;
            text.fontSize = fontSize;
            text.alignment = anchor;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.color = color;
            text.supportRichText = false;
            return text;
        }

        private static Image EnsureImage(Transform parent, Color color)
        {
            RectTransform rect = parent as RectTransform;
            if (rect == null)
            {
                rect = EnsureChildRect(parent, "Image");
            }

            Image image = rect.GetComponent<Image>();
            if (image == null)
            {
                image = rect.gameObject.AddComponent<Image>();
            }

            image.color = color;
            return image;
        }

        private static UiPolylineGraphic EnsurePolyline(RectTransform rect, Color color, float thickness)
        {
            UiPolylineGraphic polyline = rect.GetComponent<UiPolylineGraphic>();
            if (polyline == null)
            {
                polyline = rect.gameObject.AddComponent<UiPolylineGraphic>();
            }

            polyline.color = color;
            polyline.raycastTarget = false;
            polyline.SetThickness(thickness);
            return polyline;
        }

        private static UiCircleFillGraphic EnsureCircleFill(RectTransform rect, Color color)
        {
            UiCircleFillGraphic fill = rect.GetComponent<UiCircleFillGraphic>();
            if (fill == null)
            {
                fill = rect.gameObject.AddComponent<UiCircleFillGraphic>();
            }

            fill.color = color;
            fill.raycastTarget = false;
            fill.SetSegments(64);
            return fill;
        }

        private static T TryFindObject<T>(string assetName) where T : ScriptableObject
        {
            T[] loaded = Resources.FindObjectsOfTypeAll<T>();
            for (int i = 0; i < loaded.Length; i++)
            {
                T candidate = loaded[i];
                if (candidate != null && candidate.name == assetName)
                {
                    return candidate;
                }
            }

#if UNITY_EDITOR
            if (typeof(T) == typeof(MagicCircleLibrarySO))
            {
                return AssetDatabase.LoadAssetAtPath<T>("Assets/_Project/ScriptableObjects/MagicCircles/MagicCircleLibrary.asset");
            }
#endif
            return null;
        }
    }
}
