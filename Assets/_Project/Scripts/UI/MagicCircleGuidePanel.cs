using System.Collections.Generic;
using DuckovProto.MagicCircles;
using DuckovProto.MagicCircles.Data;
using DuckovProto.MagicCircles.Matching;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace DuckovProto.UI
{
    [DisallowMultipleComponent]
    public sealed partial class MagicCircleGuidePanel : MonoBehaviour
    {
        private sealed class PreviewView
        {
            public RectTransform Root;
            public UiCircleFillGraphic Background;
            public UiPolylineGraphic Border;
            public Image HorizontalAxis;
            public Image VerticalAxis;
            public float ReferenceThickness;
            public float UserThickness;
            public RectTransform ReferenceGlowRoot;
            public RectTransform ReferenceLineRoot;
            public RectTransform UserLineRoot;
            public readonly List<Image> ReferenceGlowSegments = new List<Image>(192);
            public readonly List<Image> ReferenceLineSegments = new List<Image>(192);
            public readonly List<Image> UserLineSegments = new List<Image>(192);
        }

        private sealed class CircleCardView
        {
            public string CircleId;
            public RectTransform Root;
            public Button Button;
            public Image Background;
            public Text NameText;
            public Text SpellText;
            public Text ScoreText;
            public PreviewView Preview;
        }

        [SerializeField] private MagicCircleCastPipeline magicCircleCastPipeline;
        [SerializeField] private MagicCircleLibrarySO magicCircleLibraryFallback;
        [SerializeField] private Canvas targetCanvas;
        [SerializeField] private bool startVisible = true;
        [SerializeField] private bool allowToggle = true;
        [SerializeField] private float recentStrokeLifetime = 5f;

        private readonly List<CircleCardView> cards = new List<CircleCardView>(8);
        private readonly List<MagicCircleDefinitionSO> visibleCircles = new List<MagicCircleDefinitionSO>(8);
        private readonly List<Vector2> previewPoints = new List<Vector2>(256);
        private readonly List<Vector2> uiPoints = new List<Vector2>(256);
        private readonly List<Vector2> circleFramePoints = new List<Vector2>(72);
        private readonly List<Vector2> preparedReference = new List<Vector2>(256);
        private readonly List<Vector2> preparedUser = new List<Vector2>(256);
        private readonly List<Vector2> reversedUser = new List<Vector2>(256);
        private readonly List<Vector2> rotatedUser = new List<Vector2>(256);
        private readonly List<Vector2> bestOverlayUser = new List<Vector2>(256);
        private readonly MagicCircleMatchResult previewFallbackResult = new MagicCircleMatchResult();

        private Font uiFont;
        private RectTransform root;
        private Text summaryText;
        private RectTransform listContent;
        private Text detailTitleText;
        private Text detailSpellText;
        private Text detailDescriptionText;
        private Text detailHintText;
        private Text detailStatusText;
        private Text detailLegendText;
        private PreviewView detailPreview;
        private string selectedCircleId = string.Empty;
        private bool isVisible;
        private float lastSeenEvaluationTime = -999f;

        public void AttachToCanvas(Canvas canvas, Font font)
        {
            targetCanvas = canvas;
            if (font != null)
            {
                uiFont = font;
            }

            EnsureUi();
            ApplyVisibility();
        }

        private void Awake()
        {
            isVisible = startVisible;
            if (uiFont == null)
            {
                uiFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                if (uiFont == null)
                {
                    uiFont = Resources.GetBuiltinResource<Font>("Arial.ttf");
                }
            }

            EnsureUi();
            ApplyVisibility();
        }

        private void Update()
        {
            HandleToggle();
            if (!isVisible)
            {
                return;
            }

            ResolveReferences();
            EnsureUi();
            RefreshCircleList();
            RefreshSummary();
            RefreshSelection();
            RefreshDetail();
        }

        private void HandleToggle()
        {
            if (!allowToggle)
            {
                return;
            }

            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return;
            }

            bool pointerOverUi = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
            if (keyboard.f2Key.wasPressedThisFrame && !pointerOverUi)
            {
                isVisible = !isVisible;
                ApplyVisibility();
            }
        }

        private void ResolveReferences()
        {
            if (magicCircleCastPipeline == null)
            {
                MagicCircleRuntimeLocator.TryGetPipeline(out magicCircleCastPipeline);
            }

            if (magicCircleLibraryFallback == null)
            {
                magicCircleLibraryFallback = TryFindObject<MagicCircleLibrarySO>("MagicCircleLibrary");
            }

            if (targetCanvas == null)
            {
                MagicCircleRuntimeLocator.TryGetHudCanvas(out targetCanvas);
            }
        }

        private void ApplyVisibility()
        {
            if (root != null)
            {
                root.gameObject.SetActive(isVisible);
            }
        }
    }
}
