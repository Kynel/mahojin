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
            EnsureCanvasAndLayout();
            ResolveReferences();
            EnsureMagicCircleGuidePanel();
        }

        private void Update()
        {
            ResolveReferences();
            UpdateVitals();
            UpdateStateMessage();
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
