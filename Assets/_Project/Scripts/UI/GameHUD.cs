using DuckovProto.MagicCircles;
using DuckovProto.Player;
using UnityEngine;
using UnityEngine.UI;

namespace DuckovProto.UI
{
    [DefaultExecutionOrder(1000)]
    [DisallowMultipleComponent]
    public sealed partial class GameHUD : MonoBehaviour
    {
        [SerializeField] private MagicCircleCastController magicCircleCastController;
        [SerializeField] private MagicCircleDrawController magicCircleDrawController;
        [SerializeField] private PlayerVitals playerVitals;
        [SerializeField] private MagicCircleGuidePanel magicCircleGuidePanel;
        [SerializeField] private bool showMagicCircleGuidePanel = true;
        [SerializeField] private string restartSceneName = "Prototype_Arena";
        [SerializeField] private string restartScenePath = "Assets/_Project/Scenes/Prototype_Arena.unity";

        private Canvas canvas;
        private Font hudFont;
        private Image hpFill;
        private Image mpFill;
        private Text hpText;
        private Text mpText;
        private Text stateText;

        // Feedback UI elements
        private Text feedbackText;
        private CanvasGroup feedbackCanvasGroup;
        private Coroutine feedbackCoroutine;

        private static GameHUD instance;

        public static bool TryGetInstance(out GameHUD hud)
        {
            hud = instance;
            return hud != null;
        }

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
            instance = this;
            EnsureEventSystem();
            EnsureCanvasAndLayout();
            ResolveReferences();
            EnsureMagicCircleGuidePanel();
        }

        private void OnDestroy()
        {
            if (instance == this)
            {
                instance = null;
            }
        }

        private void Update()
        {
            ResolveReferences();
            UpdateVitals();
            UpdateStateMessage();
        }

        public void ShowFeedback(string message, Color color)
        {
            if (feedbackText == null || feedbackCanvasGroup == null) return;

            feedbackText.text = message;
            feedbackText.color = color;

            if (feedbackCoroutine != null)
            {
                StopCoroutine(feedbackCoroutine);
            }
            feedbackCoroutine = StartCoroutine(AnimateFeedback());
        }

        private System.Collections.IEnumerator AnimateFeedback()
        {
            // Popup
            feedbackCanvasGroup.alpha = 1f;
            RectTransform rect = feedbackText.rectTransform;
            rect.localScale = Vector3.one * 1.5f;

            float t = 0;
            while (t < 0.15f)
            {
                t += Time.deltaTime;
                rect.localScale = Vector3.Lerp(Vector3.one * 1.5f, Vector3.one, t / 0.15f);
                yield return null;
            }
            rect.localScale = Vector3.one;

            yield return new WaitForSeconds(0.8f);

            // Fade out
            t = 0;
            while (t < 0.3f)
            {
                t += Time.deltaTime;
                feedbackCanvasGroup.alpha = Mathf.Lerp(1f, 0f, t / 0.3f);
                yield return null;
            }
            feedbackCanvasGroup.alpha = 0f;
        }

        private void ResolveReferences()
        {
            if (magicCircleCastController == null)
            {
                MagicCircleRuntimeLocator.TryGetCastController(out magicCircleCastController);
            }

            if (magicCircleDrawController == null)
            {
                MagicCircleRuntimeLocator.TryGetDrawController(out magicCircleDrawController);
            }

            if (playerVitals == null)
            {
                PlayerRuntimeLocator.TryGetVitals(out playerVitals);
            }

            if (showMagicCircleGuidePanel && magicCircleGuidePanel == null)
            {
                magicCircleGuidePanel = GetComponent<MagicCircleGuidePanel>();
            }
        }
    }
}
