using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace DuckovProto.UI
{
    [DisallowMultipleComponent]
    public sealed class MagicCircleUI : MonoBehaviour
    {
        [SerializeField] private Canvas targetCanvas;
        [SerializeField] private float defaultRadiusPx = 140f;
        [SerializeField] private float borderThickness = 4.0f;
        [SerializeField] private float userLineThickness = 6.0f;
        [SerializeField] private float gridThickness = 2.2f;
        [SerializeField] private Color backdropColor = new Color(0.08f, 0.09f, 0.12f, 0.58f);
        [SerializeField] private Color borderColor = new Color(0.98f, 0.93f, 0.78f, 1f);
        [SerializeField] private Color gridColor = new Color(0.98f, 0.93f, 0.78f, 0.55f);
        [SerializeField] private Color dashedRingColor = new Color(0.98f, 0.93f, 0.78f, 0.62f);

        private RectTransform root;
        private UiCircleFillGraphic backdropFill;
        private UiPolylineGraphic borderLine;
        private UiPolylineGraphic userLine;
        private RectTransform userSegmentsRoot;
        private RectTransform horizontalGuide;
        private RectTransform verticalGuide;
        private Text messageText;
        private readonly List<RectTransform> dashedDots = new List<RectTransform>(32);
        private readonly List<RectTransform> userSegments = new List<RectTransform>(256);
        private Coroutine hideMessageRoutine;
        private float activeRadiusPx;

        public float ActiveRadiusPx => activeRadiusPx;
        public bool IsVisible => root != null && root.gameObject.activeSelf;

        private void Awake()
        {
            EnsureUi();
        }

        public void Show(Vector2 screenPos, float radiusPx)
        {
            EnsureUi();
            activeRadiusPx = Mathf.Clamp(radiusPx > 0f ? radiusPx : defaultRadiusPx, 64f, 360f);

            root.sizeDelta = Vector2.one * (activeRadiusPx * 2f);
            UpdateDecor();

            if (TryScreenToCanvasLocal(screenPos, out Vector2 canvasLocalPos))
            {
                root.anchoredPosition = ClampToCanvas(canvasLocalPos, activeRadiusPx + 10f);
            }

            root.gameObject.SetActive(true);
            ClearUserLine();
            HideMessageImmediate();
        }

        public void UpdateUserLine(List<Vector2> localPoints)
        {
            EnsureUi();
            if (!IsVisible)
            {
                return;
            }

            if (userLine != null)
            {
                userLine.SetPoints(localPoints);
            }

            UpdateUserSegments(localPoints);
        }

        public void ShowMessage(string msg, float seconds = 0.25f)
        {
            EnsureUi();
            if (!IsVisible)
            {
                return;
            }

            messageText.text = msg;
            messageText.enabled = true;

            if (hideMessageRoutine != null)
            {
                StopCoroutine(hideMessageRoutine);
            }

            hideMessageRoutine = StartCoroutine(HideMessageAfter(seconds));
        }

        public void Close()
        {
            if (root == null)
            {
                return;
            }

            if (hideMessageRoutine != null)
            {
                StopCoroutine(hideMessageRoutine);
                hideMessageRoutine = null;
            }

            HideMessageImmediate();
            ClearUserLine();
            root.gameObject.SetActive(false);
        }

        public bool TryGetLocalPoint(Vector2 screenPos, out Vector2 localPoint)
        {
            localPoint = default;
            if (root == null || !root.gameObject.activeInHierarchy)
            {
                return false;
            }

            if (!TryScreenToCanvasLocal(screenPos, out Vector2 canvasLocalPos))
            {
                return false;
            }

            localPoint = canvasLocalPos - root.anchoredPosition;
            return true;
        }

        private void EnsureUi()
        {
            if (root != null)
            {
                return;
            }

            EnsureCanvas();

            RectTransform canvasRect = targetCanvas.GetComponent<RectTransform>();
            root = EnsureChildRect(canvasRect, "MagicCircleUIRoot");
            root.anchorMin = new Vector2(0.5f, 0.5f);
            root.anchorMax = new Vector2(0.5f, 0.5f);
            root.pivot = new Vector2(0.5f, 0.5f);
            root.sizeDelta = Vector2.one * (defaultRadiusPx * 2f);

            RectTransform borderRect = EnsureChildRect(root, "Border");
            borderRect.anchorMin = new Vector2(0.5f, 0.5f);
            borderRect.anchorMax = new Vector2(0.5f, 0.5f);
            borderRect.pivot = new Vector2(0.5f, 0.5f);
            borderRect.anchoredPosition = Vector2.zero;
            borderRect.sizeDelta = root.sizeDelta;

            RectTransform backdropRect = EnsureChildRect(root, "Backdrop");
            backdropRect.anchorMin = new Vector2(0.5f, 0.5f);
            backdropRect.anchorMax = new Vector2(0.5f, 0.5f);
            backdropRect.pivot = new Vector2(0.5f, 0.5f);
            backdropRect.anchoredPosition = Vector2.zero;
            backdropRect.sizeDelta = root.sizeDelta;
            backdropFill = EnsureCircleFill(backdropRect, backdropColor);

            borderLine = EnsurePolyline(borderRect, borderColor, borderThickness);

            horizontalGuide = EnsureChildRect(root, "GuideHorizontal");
            horizontalGuide.anchorMin = new Vector2(0.5f, 0.5f);
            horizontalGuide.anchorMax = new Vector2(0.5f, 0.5f);
            horizontalGuide.pivot = new Vector2(0.5f, 0.5f);
            EnsureImage(horizontalGuide, gridColor);

            verticalGuide = EnsureChildRect(root, "GuideVertical");
            verticalGuide.anchorMin = new Vector2(0.5f, 0.5f);
            verticalGuide.anchorMax = new Vector2(0.5f, 0.5f);
            verticalGuide.pivot = new Vector2(0.5f, 0.5f);
            EnsureImage(verticalGuide, gridColor);

            RectTransform userLineRect = EnsureChildRect(root, "UserLine");
            userLineRect.anchorMin = new Vector2(0.5f, 0.5f);
            userLineRect.anchorMax = new Vector2(0.5f, 0.5f);
            userLineRect.pivot = new Vector2(0.5f, 0.5f);
            userLineRect.anchoredPosition = Vector2.zero;
            userLineRect.sizeDelta = root.sizeDelta;
            userLine = EnsurePolyline(userLineRect, new Color(1f, 0.15f, 0.15f, 0.98f), userLineThickness);

            userSegmentsRoot = EnsureChildRect(root, "UserSegments");
            userSegmentsRoot.anchorMin = new Vector2(0.5f, 0.5f);
            userSegmentsRoot.anchorMax = new Vector2(0.5f, 0.5f);
            userSegmentsRoot.pivot = new Vector2(0.5f, 0.5f);
            userSegmentsRoot.anchoredPosition = Vector2.zero;
            userSegmentsRoot.sizeDelta = root.sizeDelta;

            messageText = EnsureText(root, "Message");
            messageText.enabled = false;

            // Draw order: backdrop -> guides -> border -> user line -> user segments -> message
            backdropRect.SetSiblingIndex(0);
            horizontalGuide.SetSiblingIndex(1);
            verticalGuide.SetSiblingIndex(2);
            borderRect.SetSiblingIndex(3);
            userLineRect.SetSiblingIndex(4);
            userSegmentsRoot.SetSiblingIndex(5);
            messageText.rectTransform.SetSiblingIndex(6);

            activeRadiusPx = defaultRadiusPx;
            UpdateDecor();
            root.gameObject.SetActive(false);
        }

        private void EnsureCanvas()
        {
            if (targetCanvas != null)
            {
                return;
            }

            Canvas[] canvases = FindObjectsByType<Canvas>(FindObjectsSortMode.None);
            for (int i = 0; i < canvases.Length; i++)
            {
                if (canvases[i] != null && canvases[i].name == "GameHUDCanvas")
                {
                    targetCanvas = canvases[i];
                    break;
                }
            }

            if (targetCanvas != null)
            {
                return;
            }

            GameObject canvasObj = new GameObject("GameHUDCanvas");
            targetCanvas = canvasObj.AddComponent<Canvas>();
            targetCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            targetCanvas.sortingOrder = 1100;

            CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            canvasObj.AddComponent<GraphicRaycaster>();
        }

        private void UpdateDecor()
        {
            float diameter = activeRadiusPx * 2f;
            if (root != null)
            {
                root.sizeDelta = Vector2.one * diameter;
            }

            if (horizontalGuide != null)
            {
                horizontalGuide.sizeDelta = new Vector2(diameter * 0.88f, gridThickness);
            }

            if (verticalGuide != null)
            {
                verticalGuide.sizeDelta = new Vector2(gridThickness, diameter * 0.88f);
            }

            if (borderLine != null)
            {
                borderLine.color = borderColor;
                borderLine.rectTransform.sizeDelta = Vector2.one * diameter;
                borderLine.SetThickness(borderThickness);
                borderLine.SetPoints(BuildCirclePoints(activeRadiusPx - (borderThickness * 0.75f), 72));
            }

            if (backdropFill != null)
            {
                backdropFill.color = backdropColor;
                backdropFill.rectTransform.sizeDelta = Vector2.one * (diameter * 0.94f);
            }

            if (userLine != null)
            {
                userLine.rectTransform.sizeDelta = Vector2.one * diameter;
                userLine.SetThickness(userLineThickness);
            }

            if (userSegmentsRoot != null)
            {
                userSegmentsRoot.sizeDelta = Vector2.one * diameter;
            }

            UpdateDashedRing(activeRadiusPx * 0.62f, 28);
            UpdateMessagePosition();
        }

        private void UpdateDashedRing(float ringRadius, int count)
        {
            while (dashedDots.Count < count)
            {
                RectTransform dot = EnsureChildRect(root, "Dot_" + dashedDots.Count);
                EnsureImage(dot, dashedRingColor);
                dot.anchorMin = new Vector2(0.5f, 0.5f);
                dot.anchorMax = new Vector2(0.5f, 0.5f);
                dot.pivot = new Vector2(0.5f, 0.5f);
                dashedDots.Add(dot);
            }

            for (int i = 0; i < dashedDots.Count; i++)
            {
                bool active = i < count;
                dashedDots[i].gameObject.SetActive(active);
                if (!active)
                {
                    continue;
                }

                float t = i / (float)count;
                float angle = t * Mathf.PI * 2f;
                Vector2 p = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * ringRadius;
                dashedDots[i].anchoredPosition = p;
                dashedDots[i].sizeDelta = Vector2.one * 4.8f;
            }
        }

        private static List<Vector2> BuildCirclePoints(float radius, int segments)
        {
            List<Vector2> points = new List<Vector2>(segments + 1);
            for (int i = 0; i <= segments; i++)
            {
                float t = i / (float)segments;
                float angle = t * Mathf.PI * 2f;
                points.Add(new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius);
            }

            return points;
        }

        private void UpdateMessagePosition()
        {
            if (messageText == null)
            {
                return;
            }

            RectTransform textRect = messageText.rectTransform;
            textRect.anchorMin = new Vector2(0.5f, 0.5f);
            textRect.anchorMax = new Vector2(0.5f, 0.5f);
            textRect.pivot = new Vector2(0.5f, 0.5f);
            textRect.anchoredPosition = new Vector2(0f, -activeRadiusPx - 20f);
            textRect.sizeDelta = new Vector2(240f, 22f);
        }

        private bool TryScreenToCanvasLocal(Vector2 screenPos, out Vector2 canvasLocalPos)
        {
            canvasLocalPos = default;
            if (targetCanvas == null)
            {
                return false;
            }

            RectTransform canvasRect = targetCanvas.GetComponent<RectTransform>();
            Camera eventCam = targetCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : targetCanvas.worldCamera;
            return RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenPos, eventCam, out canvasLocalPos);
        }

        private Vector2 ClampToCanvas(Vector2 canvasLocalPos, float margin)
        {
            RectTransform canvasRect = targetCanvas.GetComponent<RectTransform>();
            Vector2 half = canvasRect.rect.size * 0.5f;

            canvasLocalPos.x = Mathf.Clamp(canvasLocalPos.x, -half.x + margin, half.x - margin);
            canvasLocalPos.y = Mathf.Clamp(canvasLocalPos.y, -half.y + margin, half.y - margin);
            return canvasLocalPos;
        }

        private void ClearUserLine()
        {
            if (userLine != null)
            {
                userLine.SetPoints(null);
            }

            for (int i = 0; i < userSegments.Count; i++)
            {
                if (userSegments[i] != null)
                {
                    userSegments[i].gameObject.SetActive(false);
                }
            }
        }

        private void UpdateUserSegments(IReadOnlyList<Vector2> localPoints)
        {
            int needed = localPoints != null ? Mathf.Max(0, localPoints.Count - 1) : 0;
            EnsureUserSegmentPool(needed);

            for (int i = 0; i < userSegments.Count; i++)
            {
                bool active = i < needed;
                RectTransform segment = userSegments[i];
                if (segment == null)
                {
                    continue;
                }

                segment.gameObject.SetActive(active);
                if (!active)
                {
                    continue;
                }

                Vector2 a = localPoints[i];
                Vector2 b = localPoints[i + 1];
                Vector2 d = b - a;
                float len = d.magnitude;
                if (len < 0.001f)
                {
                    segment.gameObject.SetActive(false);
                    continue;
                }

                segment.anchoredPosition = (a + b) * 0.5f;
                segment.sizeDelta = new Vector2(len, userLineThickness);
                float angle = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;
                segment.localRotation = Quaternion.Euler(0f, 0f, angle);
            }
        }

        private void EnsureUserSegmentPool(int needed)
        {
            while (userSegments.Count < needed)
            {
                RectTransform seg = EnsureChildRect(userSegmentsRoot, "Seg_" + userSegments.Count);
                seg.anchorMin = new Vector2(0.5f, 0.5f);
                seg.anchorMax = new Vector2(0.5f, 0.5f);
                seg.pivot = new Vector2(0.5f, 0.5f);
                Image img = EnsureImage(seg, new Color(1f, 0.15f, 0.15f, 0.98f));
                img.raycastTarget = false;
                userSegments.Add(seg);
            }
        }

        private void HideMessageImmediate()
        {
            if (messageText == null)
            {
                return;
            }

            messageText.enabled = false;
            messageText.text = string.Empty;
        }

        private IEnumerator HideMessageAfter(float seconds)
        {
            yield return new WaitForSecondsRealtime(Mathf.Max(0f, seconds));
            hideMessageRoutine = null;
            HideMessageImmediate();
        }

        private static RectTransform EnsureChildRect(Transform parent, string name)
        {
            Transform child = parent.Find(name);
            RectTransform rect;
            if (child == null)
            {
                GameObject go = new GameObject(name);
                go.transform.SetParent(parent, false);
                rect = go.AddComponent<RectTransform>();
            }
            else
            {
                rect = child.GetComponent<RectTransform>();
                if (rect == null)
                {
                    rect = child.gameObject.AddComponent<RectTransform>();
                }
            }

            return rect;
        }

        private static Image EnsureImage(RectTransform owner, Color color)
        {
            Image image = owner.GetComponent<Image>();
            if (image == null)
            {
                image = owner.gameObject.AddComponent<Image>();
            }

            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private static Text EnsureText(RectTransform parent, string name)
        {
            RectTransform rect = EnsureChildRect(parent, name);
            Text text = rect.GetComponent<Text>();
            if (text == null)
            {
                text = rect.gameObject.AddComponent<Text>();
            }

            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font == null)
            {
                font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            }

            text.font = font;
            text.fontSize = 14;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = new Color(1f, 0.72f, 0.72f, 0.95f);
            text.raycastTarget = false;
            return text;
        }

        private static UiPolylineGraphic EnsurePolyline(RectTransform rect, Color color, float thickness)
        {
            UiPolylineGraphic poly = rect.GetComponent<UiPolylineGraphic>();
            if (poly == null)
            {
                poly = rect.gameObject.AddComponent<UiPolylineGraphic>();
            }

            poly.color = color;
            poly.SetThickness(thickness);
            poly.raycastTarget = false;
            return poly;
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
            fill.SetSegments(96);
            return fill;
        }
    }

    public sealed class UiPolylineGraphic : MaskableGraphic
    {
        [SerializeField] private float thickness = 4f;
        private readonly List<Vector2> points = new List<Vector2>(256);

        public void SetThickness(float value)
        {
            thickness = Mathf.Max(0.5f, value);
            SetVerticesDirty();
        }

        public void SetPoints(IReadOnlyList<Vector2> source)
        {
            points.Clear();
            if (source != null)
            {
                for (int i = 0; i < source.Count; i++)
                {
                    points.Add(source[i]);
                }
            }

            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            if (points.Count < 2)
            {
                return;
            }

            Color32 c = color;
            int vertexIndex = 0;
            float half = thickness * 0.5f;

            for (int i = 0; i < points.Count - 1; i++)
            {
                Vector2 a = points[i];
                Vector2 b = points[i + 1];
                Vector2 segment = b - a;
                float len = segment.magnitude;
                if (len <= 0.0001f)
                {
                    continue;
                }

                Vector2 dir = segment / len;
                Vector2 n = new Vector2(-dir.y, dir.x) * half;

                UIVertex v0 = UIVertex.simpleVert;
                UIVertex v1 = UIVertex.simpleVert;
                UIVertex v2 = UIVertex.simpleVert;
                UIVertex v3 = UIVertex.simpleVert;

                v0.color = c;
                v1.color = c;
                v2.color = c;
                v3.color = c;

                v0.position = a - n;
                v1.position = a + n;
                v2.position = b + n;
                v3.position = b - n;

                vh.AddVert(v0);
                vh.AddVert(v1);
                vh.AddVert(v2);
                vh.AddVert(v3);

                vh.AddTriangle(vertexIndex + 0, vertexIndex + 1, vertexIndex + 2);
                vh.AddTriangle(vertexIndex + 0, vertexIndex + 2, vertexIndex + 3);
                vertexIndex += 4;
            }
        }
    }

    public sealed class UiCircleFillGraphic : MaskableGraphic
    {
        [SerializeField] private int segments = 64;

        public void SetSegments(int value)
        {
            segments = Mathf.Clamp(value, 12, 256);
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            int seg = Mathf.Clamp(segments, 12, 256);

            Rect rect = rectTransform.rect;
            float radius = Mathf.Min(rect.width, rect.height) * 0.5f;
            Vector2 center = rect.center;

            UIVertex centerVertex = UIVertex.simpleVert;
            centerVertex.color = color;
            centerVertex.position = center;
            vh.AddVert(centerVertex);

            for (int i = 0; i <= seg; i++)
            {
                float t = i / (float)seg;
                float angle = t * Mathf.PI * 2f;
                Vector2 p = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;

                UIVertex v = UIVertex.simpleVert;
                v.color = color;
                v.position = p;
                vh.AddVert(v);
            }

            for (int i = 1; i <= seg; i++)
            {
                vh.AddTriangle(0, i, i + 1);
            }
        }
    }
}
