using System.Collections.Generic;
using System.IO;
using DuckovProto.Player;
using DuckovProto.Runes;
using DuckovProto.Runes.Data;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif
#if UNITY_EDITOR
using UnityEditor.SceneManagement;
#endif

namespace DuckovProto.UI
{
    [DefaultExecutionOrder(1000)]
    [DisallowMultipleComponent]
    public sealed class GameHUD : MonoBehaviour
    {
        private sealed class RuneExampleCard
        {
            public RectTransform Root;
            public Image Background;
            public string RuneId;
            public Text NameText;
            public Text DescText;
            public Text ScoreText;
            public UiPolylineGraphic GuideLine;
            public UiPolylineGraphic BorderLine;
            public RectTransform StrictRoot;
            public Image StartDot;
            public Image EndDot;
            public readonly List<UiPolylineGraphic> StrictLines = new List<UiPolylineGraphic>(8);
            public RectTransform GuideSegmentsRoot;
            public RectTransform BorderSegmentsRoot;
            public readonly List<RectTransform> GuideSegments = new List<RectTransform>(192);
            public readonly List<RectTransform> BorderSegments = new List<RectTransform>(192);
        }

        [SerializeField] private RuneCastController runeCastController;
        [SerializeField] private RuneCastPipelineController runeCastPipelineController;
        [SerializeField] private RuneLibrarySO runeLibraryFallback;
        [SerializeField] private PlayerVitals playerVitals;
        [SerializeField] private bool disableLegacyPrototypeHud = true;
        [SerializeField] private string restartSceneName = "Prototype_Arena";
        [SerializeField] private string restartScenePath = "Assets/_Project/Scenes/Prototype_Arena.unity";

        private Canvas canvas;
        private RectTransform runeExamplesPanelRect;
        private Image hpFill;
        private Image mpFill;
        private Text hpText;
        private Text mpText;
        private Text stateText;
        private Font hudFont;

        private readonly List<RuneExampleCard> runeCards = new List<RuneExampleCard>(3);
        private string runeSetSignature = string.Empty;
        private float nextRefindTime;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (FindFirstObjectByType<GameHUD>() != null)
            {
                return;
            }

            GameObject hudObject = new GameObject("GameHUD");
            hudObject.AddComponent<GameHUD>();
        }

        private void Awake()
        {
            EnsureEventSystem();
            EnsureHudRoot();
            ResolveReferences(true);
            if (disableLegacyPrototypeHud)
            {
                DisableLegacyHud();
            }
        }

        private void Update()
        {
            ResolveReferences(false);
            UpdateVitals();
            UpdateStateMessage();
            UpdateRuneExamples();
        }

        private void ResolveReferences(bool force)
        {
            if (!force && Time.time < nextRefindTime)
            {
                return;
            }

            if (runeCastController == null)
            {
                runeCastController = FindFirstObjectByType<RuneCastController>();
            }

            if (runeCastPipelineController == null)
            {
                runeCastPipelineController = FindFirstObjectByType<RuneCastPipelineController>();
            }

            if (runeLibraryFallback == null)
            {
                runeLibraryFallback = TryFindObject<RuneLibrarySO>("RuneLibrary");
            }

            if (playerVitals == null)
            {
                playerVitals = FindFirstObjectByType<PlayerVitals>();
            }

            nextRefindTime = Time.time + 0.25f;
        }

        private static void EnsureEventSystem()
        {
            EventSystem eventSystem = FindFirstObjectByType<EventSystem>();
            if (eventSystem == null)
            {
                GameObject eventSystemObject = new GameObject("EventSystem");
                eventSystem = eventSystemObject.AddComponent<EventSystem>();
            }

            if (!eventSystem.enabled)
            {
                eventSystem.enabled = true;
            }

#if ENABLE_INPUT_SYSTEM
            InputSystemUIInputModule inputModule = eventSystem.GetComponent<InputSystemUIInputModule>();
            if (inputModule == null)
            {
                inputModule = eventSystem.gameObject.AddComponent<InputSystemUIInputModule>();
            }

            if (!inputModule.enabled)
            {
                inputModule.enabled = true;
            }

            StandaloneInputModule standalone = eventSystem.GetComponent<StandaloneInputModule>();
            if (standalone != null)
            {
                standalone.enabled = false;
            }
#else
            StandaloneInputModule standalone = eventSystem.GetComponent<StandaloneInputModule>();
            if (standalone == null)
            {
                standalone = eventSystem.gameObject.AddComponent<StandaloneInputModule>();
            }

            if (!standalone.enabled)
            {
                standalone.enabled = true;
            }
#endif
        }

        private void EnsureHudRoot()
        {
            hudFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (hudFont == null)
            {
                hudFont = Resources.GetBuiltinResource<Font>("Arial.ttf");
            }

            canvas = FindExistingCanvas();
            if (canvas == null)
            {
                GameObject canvasObject = new GameObject("GameHUDCanvas");
                canvas = canvasObject.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 1100;

                CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920f, 1080f);
                scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
                scaler.matchWidthOrHeight = 0.5f;

                canvasObject.AddComponent<GraphicRaycaster>();
            }
            else
            {
                CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
                if (scaler == null)
                {
                    scaler = canvas.gameObject.AddComponent<CanvasScaler>();
                }

                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920f, 1080f);
                scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
                scaler.matchWidthOrHeight = 0.5f;

                if (canvas.GetComponent<GraphicRaycaster>() == null)
                {
                    canvas.gameObject.AddComponent<GraphicRaycaster>();
                }
            }

            RectTransform root = EnsureChildRect(canvas.transform, "HUDRoot");
            root.anchorMin = new Vector2(0f, 0f);
            root.anchorMax = new Vector2(1f, 1f);
            root.offsetMin = Vector2.zero;
            root.offsetMax = Vector2.zero;

            BuildVitalsPanel(root);
            BuildStatePanel(root);
            BuildRuneExamplesPanel(root);
            BuildSystemButtons(root);
        }

        private void BuildVitalsPanel(RectTransform root)
        {
            RectTransform panel = EnsureChildRect(root, "VitalsPanel");
            panel.anchorMin = new Vector2(0f, 0f);
            panel.anchorMax = new Vector2(0f, 0f);
            panel.pivot = new Vector2(0f, 0f);
            panel.anchoredPosition = new Vector2(20f, 20f);
            panel.sizeDelta = new Vector2(440f, 122f);

            Image panelBg = EnsureImage(panel, new Color(0f, 0f, 0f, 0.3f));
            panelBg.raycastTarget = false;

            CreateBar(panel, "HPBar", new Vector2(14f, -16f), new Color(0.48f, 0.06f, 0.09f, 1f), out hpFill, out hpText);
            CreateBar(panel, "MPBar", new Vector2(14f, -68f), new Color(0.12f, 0.42f, 0.66f, 1f), out mpFill, out mpText);
        }

        private void BuildStatePanel(RectTransform root)
        {
            RectTransform panel = EnsureChildRect(root, "StatePanel");
            panel.anchorMin = new Vector2(0.5f, 0f);
            panel.anchorMax = new Vector2(0.5f, 0f);
            panel.pivot = new Vector2(0.5f, 0f);
            panel.anchoredPosition = new Vector2(0f, 24f);
            panel.sizeDelta = new Vector2(460f, 34f);

            Image bg = EnsureImage(panel, new Color(0f, 0f, 0f, 0.35f));
            bg.raycastTarget = false;

            stateText = EnsureText(panel, "StateText", 14, TextAnchor.MiddleCenter, Color.white);
            RectTransform stateRect = stateText.rectTransform;
            stateRect.anchorMin = Vector2.zero;
            stateRect.anchorMax = Vector2.one;
            stateRect.offsetMin = Vector2.zero;
            stateRect.offsetMax = Vector2.zero;
        }

        private void BuildRuneExamplesPanel(RectTransform root)
        {
            RectTransform panel = EnsureChildRect(root, "RuneExamplesPanel");
            panel.anchorMin = new Vector2(0.5f, 0.5f);
            panel.anchorMax = new Vector2(0.5f, 0.5f);
            panel.pivot = new Vector2(0.5f, 0.5f);
            panel.anchoredPosition = new Vector2(0f, 300f);
            panel.sizeDelta = new Vector2(700f, 150f);

            Image bg = EnsureImage(panel, new Color(0f, 0f, 0f, 0.15f));
            bg.raycastTarget = false;

            Text title = EnsureText(panel, "Title", 16, TextAnchor.UpperCenter, new Color(1f, 0.96f, 0.84f, 0.5f));
            title.text = "Spell Guide";
            RectTransform titleRect = title.rectTransform;
            titleRect.anchorMin = new Vector2(0f, 1f);
            titleRect.anchorMax = new Vector2(1f, 1f);
            titleRect.pivot = new Vector2(0.5f, 1f);
            titleRect.anchoredPosition = new Vector2(0f, -5f);
            titleRect.sizeDelta = new Vector2(0f, 24f);

            RectTransform content = EnsureChildRect(panel, "Content");
            content.anchorMin = new Vector2(0f, 0f);
            content.anchorMax = new Vector2(1f, 1f);
            content.offsetMin = new Vector2(10f, 10f);
            content.offsetMax = new Vector2(-10f, -30f);

            HorizontalLayoutGroup layout = content.GetComponent<HorizontalLayoutGroup>();
            if (layout == null)
            {
                VerticalLayoutGroup vlg = content.GetComponent<VerticalLayoutGroup>();
                if (vlg != null) DestroyImmediate(vlg);

                layout = content.gameObject.AddComponent<HorizontalLayoutGroup>();
            }

            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.spacing = 20f;
            layout.childControlHeight = true;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = true;
            layout.childForceExpandWidth = true;
            layout.padding = new RectOffset(5, 5, 5, 5);

            EnsureRuneCardPool(content, 3);
            panel.gameObject.SetActive(false); // Hide the panel by default
            runeExamplesPanelRect = panel;
        }

        private void EnsureRuneCardPool(RectTransform parent, int count)
        {
            while (runeCards.Count < count)
            {
                int index = runeCards.Count;
                RuneExampleCard card = new RuneExampleCard();

                RectTransform cardRect = EnsureChildRect(parent, $"RuneCard_{index}");
                LayoutElement element = cardRect.GetComponent<LayoutElement>();
                if (element == null)
                {
                    element = cardRect.gameObject.AddComponent<LayoutElement>();
                }

                element.preferredHeight = 120f;
                element.minHeight = 120f;
                element.preferredWidth = 200f;
                element.minWidth = 200f;
                element.flexibleHeight = 0f;
                element.flexibleWidth = 0f;

                card.Background = EnsureImage(cardRect, new Color(0.08f, 0.08f, 0.08f, 0.4f));
                card.Background.raycastTarget = false;
                card.Root = cardRect;

                RectTransform preview = EnsureChildRect(cardRect, "Preview");
                preview.anchorMin = new Vector2(0.5f, 0.5f);
                preview.anchorMax = new Vector2(0.5f, 0.5f);
                preview.pivot = new Vector2(0.5f, 0.5f);
                preview.anchoredPosition = new Vector2(0f, 20f);
                preview.sizeDelta = new Vector2(70f, 70f);

                UiCircleFillGraphic fill = preview.GetComponent<UiCircleFillGraphic>();
                if (fill == null)
                {
                    fill = preview.gameObject.AddComponent<UiCircleFillGraphic>();
                }

                fill.color = new Color(0f, 0f, 0f, 0.2f);
                fill.SetSegments(64);
                fill.raycastTarget = false;

                RectTransform borderRect = EnsureChildRect(preview, "BorderLine");
                borderRect.anchorMin = new Vector2(0.5f, 0.5f);
                borderRect.anchorMax = new Vector2(0.5f, 0.5f);
                borderRect.pivot = new Vector2(0.5f, 0.5f);
                borderRect.sizeDelta = new Vector2(70f, 70f);
                borderRect.anchoredPosition = Vector2.zero;

                card.BorderLine = EnsurePolyline(borderRect, new Color(0.92f, 0.86f, 0.64f, 0.95f), 2.6f);
                card.BorderLine.SetPoints(BuildCirclePoints(32f, 64));
                card.BorderLine.gameObject.SetActive(false);

                card.BorderSegmentsRoot = EnsureChildRect(preview, "BorderSegmentsRoot");
                card.BorderSegmentsRoot.anchorMin = new Vector2(0.5f, 0.5f);
                card.BorderSegmentsRoot.anchorMax = new Vector2(0.5f, 0.5f);
                card.BorderSegmentsRoot.pivot = new Vector2(0.5f, 0.5f);
                card.BorderSegmentsRoot.sizeDelta = new Vector2(70f, 70f);
                card.BorderSegmentsRoot.anchoredPosition = Vector2.zero;
                RenderSegmentLine(
                    card.BorderSegmentsRoot,
                    card.BorderSegments,
                    BuildCirclePoints(32f, 64),
                    2.4f,
                    new Color(0.92f, 0.86f, 0.64f, 0.95f));

                RectTransform guideRect = EnsureChildRect(preview, "GuideLine");
                guideRect.anchorMin = new Vector2(0.5f, 0.5f);
                guideRect.anchorMax = new Vector2(0.5f, 0.5f);
                guideRect.pivot = new Vector2(0.5f, 0.5f);
                guideRect.sizeDelta = new Vector2(70f, 70f);
                guideRect.anchoredPosition = Vector2.zero;
                card.GuideLine = EnsurePolyline(guideRect, new Color(0.96f, 0.96f, 0.96f, 1f), 4.4f);
                card.GuideLine.gameObject.SetActive(false);

                card.GuideSegmentsRoot = EnsureChildRect(preview, "GuideSegmentsRoot");
                card.GuideSegmentsRoot.anchorMin = new Vector2(0.5f, 0.5f);
                card.GuideSegmentsRoot.anchorMax = new Vector2(0.5f, 0.5f);
                card.GuideSegmentsRoot.pivot = new Vector2(0.5f, 0.5f);
                card.GuideSegmentsRoot.sizeDelta = new Vector2(70f, 70f);
                card.GuideSegmentsRoot.anchoredPosition = Vector2.zero;

                card.StrictRoot = EnsureChildRect(preview, "StrictRoot");
                card.StrictRoot.anchorMin = new Vector2(0.5f, 0.5f);
                card.StrictRoot.anchorMax = new Vector2(0.5f, 0.5f);
                card.StrictRoot.pivot = new Vector2(0.5f, 0.5f);
                card.StrictRoot.sizeDelta = new Vector2(70f, 70f);
                card.StrictRoot.anchoredPosition = Vector2.zero;

                RectTransform startDotRect = EnsureChildRect(preview, "StartDot");
                startDotRect.anchorMin = new Vector2(0.5f, 0.5f);
                startDotRect.anchorMax = new Vector2(0.5f, 0.5f);
                startDotRect.pivot = new Vector2(0.5f, 0.5f);
                startDotRect.sizeDelta = new Vector2(8f, 8f);
                card.StartDot = EnsureImage(startDotRect, new Color(0.35f, 1f, 0.42f, 0.95f));

                RectTransform endDotRect = EnsureChildRect(preview, "EndDot");
                endDotRect.anchorMin = new Vector2(0.5f, 0.5f);
                endDotRect.anchorMax = new Vector2(0.5f, 0.5f);
                endDotRect.pivot = new Vector2(0.5f, 0.5f);
                endDotRect.sizeDelta = new Vector2(8f, 8f);
                card.EndDot = EnsureImage(endDotRect, new Color(1f, 0.42f, 0.42f, 0.95f));

                card.NameText = EnsureText(cardRect, "Name", 16, TextAnchor.UpperCenter, new Color(0.97f, 0.97f, 0.97f, 1f));
                RectTransform nameRect = card.NameText.rectTransform;
                nameRect.anchorMin = new Vector2(0f, 0f);
                nameRect.anchorMax = new Vector2(1f, 0f);
                nameRect.pivot = new Vector2(0.5f, 0f);
                nameRect.anchoredPosition = new Vector2(0f, 22f);
                nameRect.sizeDelta = new Vector2(0f, 20f);

                card.DescText = EnsureText(cardRect, "Desc", 12, TextAnchor.UpperCenter, new Color(0.8f, 0.8f, 0.8f, 0.96f));
                RectTransform descRect = card.DescText.rectTransform;
                descRect.anchorMin = new Vector2(0f, 0f);
                descRect.anchorMax = new Vector2(1f, 0f);
                descRect.pivot = new Vector2(0.5f, 0f);
                descRect.anchoredPosition = new Vector2(0f, 5f);
                descRect.sizeDelta = new Vector2(0f, 30f);

                card.ScoreText = EnsureText(cardRect, "Score", 10, TextAnchor.UpperCenter, new Color(0.86f, 0.94f, 1f, 0.5f));
                RectTransform scoreRect = card.ScoreText.rectTransform;
                scoreRect.anchorMin = new Vector2(0f, 0f);
                scoreRect.anchorMax = new Vector2(1f, 0f);
                scoreRect.pivot = new Vector2(0.5f, 0f);
                scoreRect.anchoredPosition = new Vector2(0f, 40f);
                scoreRect.sizeDelta = new Vector2(0f, 18f);
                card.ScoreText.gameObject.SetActive(false);

                runeCards.Add(card);
            }
        }

        private void UpdateVitals()
        {
            float hpNormalized = 0f;
            float mpNormalized = 0f;
            string hpLabel = "HP ?/?";
            string mpLabel = "MP ?/?";

            if (playerVitals != null)
            {
                float hpMax = Mathf.Max(1f, playerVitals.MaxHP);
                float mpMax = Mathf.Max(1f, playerVitals.MaxMana);
                hpNormalized = Mathf.Clamp01(playerVitals.HP / hpMax);
                mpNormalized = Mathf.Clamp01(playerVitals.Mana / mpMax);
                hpLabel = $"HP {Mathf.CeilToInt(playerVitals.HP)}/{Mathf.CeilToInt(playerVitals.MaxHP)}";
                mpLabel =
                    $"MP {Mathf.CeilToInt(playerVitals.Mana)}/{Mathf.CeilToInt(playerVitals.MaxMana)} (+{playerVitals.ManaRegenPerSecond:0.#}/s)";
            }

            if (hpFill != null)
            {
                hpFill.fillAmount = hpNormalized;
            }

            if (mpFill != null)
            {
                mpFill.fillAmount = mpNormalized;
            }

            if (hpText != null)
            {
                hpText.text = hpLabel;
            }

            if (mpText != null)
            {
                mpText.text = mpLabel;
            }
        }

        private void UpdateStateMessage()
        {
            if (stateText == null)
            {
                return;
            }

            stateText.text = runeCastController != null
                ? runeCastController.CooldownStateText
                : string.Empty;
        }

        private void UpdateRuneExamples()
        {
            RuneLibrarySO library = runeCastPipelineController != null ? runeCastPipelineController.RuneLibrary : null;
            if (library == null)
            {
                library = runeLibraryFallback;
            }

            if (library == null)
            {
                SetRunePlaceholders();
                return;
            }

            IReadOnlyList<RuneDefinitionSO> runes = library.Runes;
            int count = Mathf.Min(runeCards.Count, runes != null ? runes.Count : 0);

            bool isCastingMode = UnityEngine.InputSystem.Mouse.current != null &&
                                 UnityEngine.InputSystem.Mouse.current.rightButton.isPressed;

            if (runeExamplesPanelRect != null && runeExamplesPanelRect.gameObject.activeSelf != isCastingMode)
            {
                runeExamplesPanelRect.gameObject.SetActive(isCastingMode);
            }

            if (!isCastingMode)
            {
                return;
            }

            string newSignature = BuildRuneSignature(runes, count);
            if (newSignature != runeSetSignature)
            {
                for (int i = 0; i < runeCards.Count; i++)
                {
                    RuneExampleCard card = runeCards[i];
                    if (i < count)
                    {
                        ApplyRuneToCard(card, runes[i]);
                    }
                    else
                    {
                        ApplyRunePlaceholder(card, i + 1);
                    }
                }

                runeSetSignature = newSignature;
            }

            string activeRuneName = runeCastController != null ? runeCastController.LastRuneName : string.Empty;
            string activeRuneId = runeCastPipelineController != null ? runeCastPipelineController.LastBestRuneId : string.Empty;
            for (int i = 0; i < runeCards.Count; i++)
            {
                RuneExampleCard card = runeCards[i];
                bool scoreVisible = card.ScoreText != null && card.ScoreText.gameObject.activeSelf;
                bool active = i < count && runes[i] != null &&
                    (runes[i].DisplayName == activeRuneName || runes[i].Id == activeRuneId);
                card.Background.color = active
                    ? new Color(0.2f, 0.19f, 0.12f, 0.82f)
                    : new Color(0.08f, 0.08f, 0.08f, 0.58f);

                if (i >= count || runes[i] == null || runeCastPipelineController == null)
                {
                    if (scoreVisible)
                    {
                        card.ScoreText.text = "Match --";
                    }

                    continue;
                }

                RuneDefinitionSO rune = runes[i];
                if (runeCastPipelineController.TryGetLastRuneScore(rune.Id, out float score, out float pass, out bool recent) && recent)
                {
                    bool passNow = score >= pass;
                    if (scoreVisible)
                    {
                        card.ScoreText.text = $"Match {Mathf.RoundToInt(score * 100f)}% / Need {Mathf.RoundToInt(pass * 100f)}% {(passNow ? "OK" : "Fail")}";
                        card.ScoreText.color = passNow
                            ? new Color(0.7f, 1f, 0.72f, 0.98f)
                            : new Color(1f, 0.7f, 0.7f, 0.98f);
                    }
                }
                else
                {
                    if (scoreVisible)
                    {
                        float need = GetDisplayPassScore(rune);
                        card.ScoreText.text = $"Match -- / Need {Mathf.RoundToInt(need * 100f)}%";
                        card.ScoreText.color = new Color(0.86f, 0.94f, 1f, 0.97f);
                    }
                }
            }
        }

        private void SetRunePlaceholders()
        {
            if (runeSetSignature == "__placeholder__")
            {
                return;
            }

            for (int i = 0; i < runeCards.Count; i++)
            {
                ApplyRunePlaceholder(runeCards[i], i + 1);
            }

            runeSetSignature = "__placeholder__";
        }

        private void ApplyRuneToCard(RuneExampleCard card, RuneDefinitionSO rune)
        {
            if (card == null)
            {
                return;
            }

            card.NameText.text = string.IsNullOrWhiteSpace(rune.DisplayName) ? rune.Id : rune.DisplayName;
            card.RuneId = rune.Id;
            card.DescText.text = BuildRuneDescription(rune);

            IReadOnlyList<Vector2> previewSource = GetPreviewStrokeSource(rune);
            List<Vector2> guide = ConvertGuideToPreview(previewSource, 25f);
            card.GuideLine.SetPoints(guide);
            RenderSegmentLine(
                card.GuideSegmentsRoot,
                card.GuideSegments,
                guide,
                3.2f,
                new Color(0.96f, 0.96f, 0.96f, 1f));
            if (guide.Count > 0 && card.StartDot != null)
            {
                card.StartDot.rectTransform.anchoredPosition = guide[0];
                card.StartDot.enabled = true;
            }

            if (guide.Count > 1 && card.EndDot != null)
            {
                card.EndDot.rectTransform.anchoredPosition = guide[guide.Count - 1];
                card.EndDot.enabled = true;
            }

            bool referenceMode = rune.UseReferenceStrokeComparison &&
                rune.ReferenceStrokeNorm != null &&
                rune.ReferenceStrokeNorm.Count > 1;
            int strictCount = referenceMode ? 1 : (rune.StrictSegments != null ? rune.StrictSegments.Count : 0);
            EnsureStrictLinePool(card, strictCount);

            for (int i = 0; i < card.StrictLines.Count; i++)
            {
                UiPolylineGraphic strictLine = card.StrictLines[i];
                if (i >= strictCount)
                {
                    strictLine.gameObject.SetActive(false);
                    continue;
                }

                List<Vector2> strictPoints;
                if (referenceMode)
                {
                    strictPoints = BuildStrictRangePreview(
                        previewSource,
                        rune.StrictSegmentStartIdx,
                        rune.StrictSegmentEndIdx,
                        127,
                        25f);
                }
                else
                {
                    StrictSegment seg = rune.StrictSegments[i];
                    strictPoints = BuildStrictSegmentPreview(rune.GuidePolylineNorm, seg, 25f);
                }

                strictLine.SetPoints(strictPoints);
                strictLine.gameObject.SetActive(strictPoints.Count >= 2);
            }

            if (card.ScoreText != null)
            {
                float need = GetDisplayPassScore(rune);
                card.ScoreText.text = $"Match -- / Need {Mathf.RoundToInt(need * 100f)}%";
                card.ScoreText.color = new Color(0.86f, 0.94f, 1f, 0.97f);
            }
        }

        private static void ApplyRunePlaceholder(RuneExampleCard card, int index)
        {
            card.NameText.text = $"Rune {index}";
            card.DescText.text = "No rune data";
            card.RuneId = string.Empty;
            card.GuideLine.SetPoints(null);
            RenderSegmentLine(card.GuideSegmentsRoot, card.GuideSegments, null, 5.2f, Color.white);
            if (card.StartDot != null)
            {
                card.StartDot.enabled = false;
            }

            if (card.EndDot != null)
            {
                card.EndDot.enabled = false;
            }

            for (int i = 0; i < card.StrictLines.Count; i++)
            {
                card.StrictLines[i].gameObject.SetActive(false);
            }

            if (card.ScoreText != null)
            {
                card.ScoreText.text = "Match --";
                card.ScoreText.color = new Color(0.86f, 0.94f, 1f, 0.97f);
            }
        }

        private static string BuildRuneSignature(IReadOnlyList<RuneDefinitionSO> runes, int count)
        {
            if (runes == null || count <= 0)
            {
                return "none";
            }

            System.Text.StringBuilder sb = new System.Text.StringBuilder(96);
            for (int i = 0; i < count; i++)
            {
                RuneDefinitionSO rune = runes[i];
                if (rune == null)
                {
                    sb.Append("null;");
                    continue;
                }

                sb.Append(rune.Id);
                sb.Append('|');
                sb.Append(rune.DisplayName);
                sb.Append(';');
            }

            return sb.ToString();
        }

        private static string BuildRuneDescription(RuneDefinitionSO rune)
        {
            string id = rune != null ? rune.Id : string.Empty;
            string spell = rune != null ? rune.LinkedSpellId : string.Empty;

            if (ContainsAny(id, spell, "water"))
            {
                return "Draw: left->right wave (3 peaks)\nCast: Ricochet x1";
            }

            if (ContainsAny(id, spell, "fire", "flame"))
            {
                return "Draw: vertical flame stroke\nCast: Channel 2s / 3 ticks";
            }

            if (ContainsAny(id, spell, "light", "rail", "thunder"))
            {
                return "Draw: lightning bolt (single stroke)\nCast: Instant rail";
            }

            return "Draw to match the white guide";
        }

        private static bool ContainsAny(string a, string b, params string[] terms)
        {
            string la = string.IsNullOrEmpty(a) ? string.Empty : a.ToLowerInvariant();
            string lb = string.IsNullOrEmpty(b) ? string.Empty : b.ToLowerInvariant();

            for (int i = 0; i < terms.Length; i++)
            {
                string t = terms[i];
                if (la.Contains(t) || lb.Contains(t))
                {
                    return true;
                }
            }

            return false;
        }

        private float GetDisplayPassScore(RuneDefinitionSO rune)
        {
            if (runeCastPipelineController != null)
            {
                return runeCastPipelineController.GetConfiguredPassScore(rune);
            }

            return rune != null ? rune.PassScore : 1f;
        }

        private static List<Vector2> ConvertGuideToPreview(IReadOnlyList<Vector2> guide, float radius)
        {
            List<Vector2> result = new List<Vector2>();
            if (guide == null)
            {
                return result;
            }

            for (int i = 0; i < guide.Count; i++)
            {
                Vector2 p = guide[i];
                result.Add(new Vector2(Mathf.Clamp(p.x, -1f, 1f), Mathf.Clamp(p.y, -1f, 1f)) * radius);
            }

            return result;
        }

        private static IReadOnlyList<Vector2> GetPreviewStrokeSource(RuneDefinitionSO rune)
        {
            if (rune == null)
            {
                return null;
            }

            if (rune.UseReferenceStrokeComparison &&
                rune.ReferenceStrokeNorm != null &&
                rune.ReferenceStrokeNorm.Count > 1)
            {
                return rune.ReferenceStrokeNorm;
            }

            return rune.GuidePolylineNorm;
        }

        private static List<Vector2> BuildStrictSegmentPreview(IReadOnlyList<Vector2> guide, StrictSegment seg, float radius)
        {
            List<Vector2> result = new List<Vector2>();
            if (guide == null || guide.Count < 2)
            {
                return result;
            }

            int maxIndex = guide.Count - 1;
            int start = Mathf.Clamp(Mathf.Min(seg.startIdx, seg.endIdx), 0, maxIndex);
            int end = Mathf.Clamp(Mathf.Max(seg.startIdx, seg.endIdx), 0, maxIndex);

            for (int i = start; i <= end; i++)
            {
                Vector2 p = guide[i];
                result.Add(new Vector2(Mathf.Clamp(p.x, -1f, 1f), Mathf.Clamp(p.y, -1f, 1f)) * radius);
            }

            return result;
        }

        private static List<Vector2> BuildStrictRangePreview(
            IReadOnlyList<Vector2> guide,
            int startIndex,
            int endIndex,
            int sourceMaxIndex,
            float radius)
        {
            List<Vector2> result = new List<Vector2>();
            if (guide == null || guide.Count < 2)
            {
                return result;
            }

            int guideMax = guide.Count - 1;
            int sourceMax = Mathf.Max(1, sourceMaxIndex);
            int start = Mathf.Clamp(Mathf.Min(startIndex, endIndex), 0, sourceMax);
            int end = Mathf.Clamp(Mathf.Max(startIndex, endIndex), 0, sourceMax);

            int guideStart = Mathf.RoundToInt((start / (float)sourceMax) * guideMax);
            int guideEnd = Mathf.RoundToInt((end / (float)sourceMax) * guideMax);
            guideStart = Mathf.Clamp(guideStart, 0, guideMax);
            guideEnd = Mathf.Clamp(guideEnd, guideStart, guideMax);

            for (int i = guideStart; i <= guideEnd; i++)
            {
                Vector2 p = guide[i];
                result.Add(new Vector2(Mathf.Clamp(p.x, -1f, 1f), Mathf.Clamp(p.y, -1f, 1f)) * radius);
            }

            return result;
        }

        private static void EnsureStrictLinePool(RuneExampleCard card, int count)
        {
            while (card.StrictLines.Count < count)
            {
                RectTransform segRect = EnsureChildRect(card.StrictRoot, "StrictSeg_" + card.StrictLines.Count);
                segRect.anchorMin = new Vector2(0.5f, 0.5f);
                segRect.anchorMax = new Vector2(0.5f, 0.5f);
                segRect.pivot = new Vector2(0.5f, 0.5f);
                segRect.anchoredPosition = Vector2.zero;
                segRect.sizeDelta = card.StrictRoot.sizeDelta;

                UiPolylineGraphic line = EnsurePolyline(segRect, new Color(0.96f, 0.2f, 0.2f, 0.97f), 4f);
                card.StrictLines.Add(line);
            }
        }

        private static void RenderSegmentLine(
            RectTransform root,
            List<RectTransform> pool,
            IReadOnlyList<Vector2> points,
            float thickness,
            Color color)
        {
            if (root == null || pool == null)
            {
                return;
            }

            int needed = points != null ? Mathf.Max(0, points.Count - 1) : 0;
            while (pool.Count < needed)
            {
                RectTransform seg = EnsureChildRect(root, "Seg_" + pool.Count);
                seg.anchorMin = new Vector2(0.5f, 0.5f);
                seg.anchorMax = new Vector2(0.5f, 0.5f);
                seg.pivot = new Vector2(0.5f, 0.5f);
                seg.anchoredPosition = Vector2.zero;
                seg.sizeDelta = Vector2.zero;

                Image img = EnsureImage(seg, color);
                img.raycastTarget = false;
                pool.Add(seg);
            }

            for (int i = 0; i < pool.Count; i++)
            {
                bool active = i < needed;
                RectTransform seg = pool[i];
                if (seg == null)
                {
                    continue;
                }

                seg.gameObject.SetActive(active);
                if (!active)
                {
                    continue;
                }

                Vector2 a = points[i];
                Vector2 b = points[i + 1];
                Vector2 d = b - a;
                float len = d.magnitude;
                if (len < 0.001f)
                {
                    seg.gameObject.SetActive(false);
                    continue;
                }

                seg.anchoredPosition = (a + b) * 0.5f;
                seg.sizeDelta = new Vector2(len, Mathf.Max(1f, thickness));
                seg.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);

                Image segImage = seg.GetComponent<Image>();
                if (segImage != null)
                {
                    segImage.color = color;
                }
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

        private void CreateBar(
            RectTransform parent,
            string name,
            Vector2 anchoredPos,
            Color fillColor,
            out Image fill,
            out Text text)
        {
            RectTransform bar = EnsureChildRect(parent, name);
            bar.anchorMin = new Vector2(0f, 1f);
            bar.anchorMax = new Vector2(0f, 1f);
            bar.pivot = new Vector2(0f, 1f);
            bar.anchoredPosition = anchoredPos;
            bar.sizeDelta = new Vector2(412f, 34f);

            EnsureImage(bar, new Color(0.04f, 0.04f, 0.04f, 0.86f));

            RectTransform fillRect = EnsureChildRect(bar, "Fill");
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.offsetMin = new Vector2(1f, 1f);
            fillRect.offsetMax = new Vector2(-1f, -1f);
            fill = EnsureImage(fillRect, fillColor);
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillOrigin = (int)Image.OriginHorizontal.Left;
            fill.fillAmount = 1f;

            text = EnsureText(bar, "Text", 17, TextAnchor.MiddleCenter, Color.white);
            RectTransform textRect = text.rectTransform;
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;
        }

        private void BuildSystemButtons(RectTransform root)
        {
            RectTransform panel = EnsureChildRect(root, "SystemButtonsPanel");
            panel.anchorMin = new Vector2(1f, 1f);
            panel.anchorMax = new Vector2(1f, 1f);
            panel.pivot = new Vector2(1f, 1f);
            panel.anchoredPosition = new Vector2(-20f, -20f);
            panel.sizeDelta = new Vector2(180f, 88f);

            Image panelBg = EnsureImage(panel, new Color(0f, 0f, 0f, 0.28f));
            panelBg.raycastTarget = false;

            VerticalLayoutGroup layout = panel.GetComponent<VerticalLayoutGroup>();
            if (layout == null)
            {
                layout = panel.gameObject.AddComponent<VerticalLayoutGroup>();
            }

            layout.childAlignment = TextAnchor.UpperCenter;
            layout.spacing = 8f;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            layout.padding = new RectOffset(8, 8, 8, 8);

            CreateSystemButton(panel, "RestartButton", "재시작", new Color(0.18f, 0.36f, 0.2f, 0.95f), OnRestartButtonPressed);
            CreateSystemButton(panel, "QuitButton", "게임종료", new Color(0.45f, 0.2f, 0.2f, 0.95f), OnQuitButtonPressed);
        }

        private void CreateSystemButton(
            RectTransform parent,
            string name,
            string label,
            Color color,
            UnityEngine.Events.UnityAction onClick)
        {
            RectTransform buttonRect = EnsureChildRect(parent, name);
            LayoutElement element = buttonRect.GetComponent<LayoutElement>();
            if (element == null)
            {
                element = buttonRect.gameObject.AddComponent<LayoutElement>();
            }

            element.minHeight = 32f;
            element.preferredHeight = 32f;
            element.flexibleHeight = 0f;

            Image image = buttonRect.GetComponent<Image>();
            if (image == null)
            {
                image = buttonRect.gameObject.AddComponent<Image>();
            }

            image.color = color;
            image.raycastTarget = true;

            Button button = buttonRect.GetComponent<Button>();
            if (button == null)
            {
                button = buttonRect.gameObject.AddComponent<Button>();
            }

            button.targetGraphic = image;
            button.onClick.RemoveListener(onClick);
            button.onClick.AddListener(onClick);

            ColorBlock colors = button.colors;
            colors.normalColor = color;
            colors.highlightedColor = Color.Lerp(color, Color.white, 0.18f);
            colors.pressedColor = Color.Lerp(color, Color.black, 0.2f);
            colors.selectedColor = colors.highlightedColor;
            colors.disabledColor = new Color(color.r, color.g, color.b, 0.45f);
            button.colors = colors;

            Text labelText = EnsureText(buttonRect, "Label", 14, TextAnchor.MiddleCenter, Color.white);
            labelText.text = label;
            RectTransform textRect = labelText.rectTransform;
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;
        }

        private static Canvas FindExistingCanvas()
        {
            Canvas[] canvases = FindObjectsByType<Canvas>(FindObjectsSortMode.None);
            for (int i = 0; i < canvases.Length; i++)
            {
                if (canvases[i] != null && canvases[i].name == "GameHUDCanvas")
                {
                    return canvases[i];
                }
            }

            return null;
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

        private Text EnsureText(RectTransform parent, string name, int fontSize, TextAnchor anchor, Color color)
        {
            RectTransform rect = EnsureChildRect(parent, name);
            Text text = rect.GetComponent<Text>();
            if (text == null)
            {
                text = rect.gameObject.AddComponent<Text>();
            }

            text.font = hudFont;
            text.fontSize = fontSize;
            text.alignment = anchor;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.color = color;
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

        private static T TryFindObject<T>(string assetName) where T : ScriptableObject
        {
            T[] loaded = Resources.FindObjectsOfTypeAll<T>();
            for (int i = 0; i < loaded.Length; i++)
            {
                T candidate = loaded[i];
                if (candidate == null)
                {
                    continue;
                }

                if (candidate.name == assetName)
                {
                    return candidate;
                }
            }

            return null;
        }

        private void DisableLegacyHud()
        {
            PrototypeHUD[] legacyHuds = FindObjectsByType<PrototypeHUD>(FindObjectsSortMode.None);
            for (int i = 0; i < legacyHuds.Length; i++)
            {
                if (legacyHuds[i] != null)
                {
                    legacyHuds[i].enabled = false;
                }
            }

            Canvas[] canvases = FindObjectsByType<Canvas>(FindObjectsSortMode.None);
            for (int i = 0; i < canvases.Length; i++)
            {
                if (canvases[i] != null && canvases[i].name == "PrototypeHUDCanvas")
                {
                    canvases[i].gameObject.SetActive(false);
                }
            }
        }

        private void OnRestartButtonPressed()
        {
            if (TryRestartConfiguredScene())
            {
                return;
            }

            Scene activeScene = SceneManager.GetActiveScene();
            if (!activeScene.IsValid())
            {
                return;
            }

            if (activeScene.buildIndex >= 0)
            {
                SceneManager.LoadScene(activeScene.buildIndex);
            }
            else
            {
                SceneManager.LoadScene(activeScene.name);
            }
        }

        private bool TryRestartConfiguredScene()
        {
#if UNITY_EDITOR
            if (!string.IsNullOrWhiteSpace(restartScenePath) && File.Exists(restartScenePath))
            {
                LoadSceneParameters parameters = new LoadSceneParameters(LoadSceneMode.Single);
                if (EditorSceneManager.LoadSceneAsyncInPlayMode(restartScenePath, parameters) != null)
                {
                    return true;
                }
            }
#endif
            if (!string.IsNullOrWhiteSpace(restartScenePath))
            {
                int buildIndex = SceneUtility.GetBuildIndexByScenePath(restartScenePath);
                if (buildIndex >= 0)
                {
                    SceneManager.LoadScene(buildIndex);
                    return true;
                }
            }

            if (!string.IsNullOrWhiteSpace(restartSceneName) && Application.CanStreamedLevelBeLoaded(restartSceneName))
            {
                SceneManager.LoadScene(restartSceneName);
                return true;
            }

            return false;
        }

        private void OnQuitButtonPressed()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
