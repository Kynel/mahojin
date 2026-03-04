using DuckovProto.Runes;
using DuckovProto.Player;
using UnityEngine;
using UnityEngine.UI;

namespace DuckovProto.UI
{
    public sealed class PrototypeHUD : MonoBehaviour
    {
        [SerializeField] private RuneCastController runeCastController;
        [SerializeField] private PlayerVitals playerVitals;
        [SerializeField] private Text line1Text;
        [SerializeField] private Text line2Text;
        [SerializeField] private Text line3Text;
        [SerializeField] private bool enableOnGuiFallback = false;
        [SerializeField] private int fallbackFontSize = 15;
        [SerializeField] private Vector2 hudAnchorOffset = new Vector2(12f, -12f);
        [SerializeField] private Vector2 hudSize = new Vector2(460f, 74f);

        private void Awake()
        {
            if (runeCastController == null)
            {
                runeCastController = FindFirstObjectByType<RuneCastController>();
            }

            if (playerVitals == null)
            {
                playerVitals = FindFirstObjectByType<PlayerVitals>();
            }

            EnsureHudElements();
        }

        private void Update()
        {
            if (runeCastController == null)
            {
                runeCastController = FindFirstObjectByType<RuneCastController>();
            }

            if (playerVitals == null)
            {
                playerVitals = FindFirstObjectByType<PlayerVitals>();
            }

            string hpMpText = "HP ?/?  MP ?/?";
            if (playerVitals != null)
            {
                hpMpText =
                    $"HP {Mathf.CeilToInt(playerVitals.HP)}/{Mathf.CeilToInt(playerVitals.MaxHP)}  " +
                    $"MP {Mathf.CeilToInt(playerVitals.Mana)}/{Mathf.CeilToInt(playerVitals.MaxMana)} " +
                    $"(+{playerVitals.ManaRegenPerSecond:0.#}/s)";
            }

            if (line1Text != null)
            {
                line1Text.text = hpMpText;
            }

            if (line2Text != null)
            {
                string rune = runeCastController != null ? runeCastController.LastRuneName : "None";
                string spell = runeCastController != null ? runeCastController.LastSpellName : "None";
                line2Text.text = $"Rune: {rune}  Spell: {spell}";
            }

            if (line3Text != null)
            {
                line3Text.text = runeCastController != null ? runeCastController.CooldownStateText : "Ready";
            }
        }

        private void OnGUI()
        {
            if (!enableOnGuiFallback)
            {
                return;
            }

            string rune = "None";
            string spell = "None";
            string cd = "Ready";
            string hpMp = "HP ?/?  MP ?/?";
            if (runeCastController != null)
            {
                rune = runeCastController.LastRuneName;
                spell = runeCastController.LastSpellName;
                cd = runeCastController.CooldownStateText;
            }
            if (playerVitals != null)
            {
                hpMp =
                    $"HP {Mathf.CeilToInt(playerVitals.HP)}/{Mathf.CeilToInt(playerVitals.MaxHP)}  " +
                    $"MP {Mathf.CeilToInt(playerVitals.Mana)}/{Mathf.CeilToInt(playerVitals.MaxMana)} " +
                    $"(+{playerVitals.ManaRegenPerSecond:0.#}/s)";
            }

            GUIStyle style = new GUIStyle(GUI.skin.label)
            {
                fontSize = Mathf.Max(12, fallbackFontSize),
                normal = { textColor = Color.white },
                alignment = TextAnchor.UpperLeft
            };

            // Draw shadow first for readability regardless of background.
            GUIStyle shadow = new GUIStyle(style);
            shadow.normal.textColor = Color.black;
            GUI.Label(new Rect(13f, 13f, 460f, 22f), hpMp, shadow);
            GUI.Label(new Rect(13f, 35f, 460f, 22f), $"Rune: {rune}  Spell: {spell}", shadow);
            GUI.Label(new Rect(13f, 57f, 460f, 22f), cd, shadow);
            GUI.Label(new Rect(12f, 12f, 460f, 22f), hpMp, style);
            GUI.Label(new Rect(12f, 34f, 460f, 22f), $"Rune: {rune}  Spell: {spell}", style);
            GUI.Label(new Rect(12f, 56f, 460f, 22f), cd, style);
        }

        private void EnsureHudElements()
        {
            Canvas canvas = FindExistingHudCanvas();
            if (canvas == null)
            {
                GameObject canvasObject = new GameObject("PrototypeHUDCanvas");
                canvas = canvasObject.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 1000;
                CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920f, 1080f);
                scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
                scaler.matchWidthOrHeight = 0.5f;
                canvasObject.AddComponent<GraphicRaycaster>();
            }

            RectTransform hudRoot = EnsureHudRoot(canvas.transform);
            if (line1Text == null)
            {
                line1Text = CreateHudLine(hudRoot, "HUDLine1", new Vector2(0f, 0f));
            }

            if (line2Text == null)
            {
                line2Text = CreateHudLine(hudRoot, "HUDLine2", new Vector2(0f, -22f));
            }

            if (line3Text == null)
            {
                line3Text = CreateHudLine(hudRoot, "HUDLine3", new Vector2(0f, -44f));
            }
        }

        private RectTransform EnsureHudRoot(Transform canvasTransform)
        {
            Transform existing = canvasTransform.Find("HUDRoot");
            RectTransform root;
            if (existing == null)
            {
                GameObject rootObject = new GameObject("HUDRoot");
                rootObject.transform.SetParent(canvasTransform, false);
                root = rootObject.AddComponent<RectTransform>();
            }
            else
            {
                root = existing.GetComponent<RectTransform>();
                if (root == null)
                {
                    root = existing.gameObject.AddComponent<RectTransform>();
                }
            }

            root.anchorMin = new Vector2(0f, 1f);
            root.anchorMax = new Vector2(0f, 1f);
            root.pivot = new Vector2(0f, 1f);
            root.anchoredPosition = hudAnchorOffset;
            root.sizeDelta = hudSize;
            return root;
        }

        private static Text CreateHudLine(Transform parent, string name, Vector2 anchoredPos)
        {
            GameObject textObject = new GameObject(name);
            textObject.transform.SetParent(parent, false);

            RectTransform rect = textObject.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = anchoredPos;
            rect.sizeDelta = new Vector2(420f, 22f);

            Text text = textObject.AddComponent<Text>();
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font == null)
            {
                font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            }

            text.font = font;
            text.fontSize = 15;
            text.alignment = TextAnchor.MiddleLeft;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.color = Color.white;
            text.text = string.Empty;
            return text;
        }

        private static Canvas FindExistingHudCanvas()
        {
            Canvas[] canvases = FindObjectsByType<Canvas>(FindObjectsSortMode.None);
            for (int i = 0; i < canvases.Length; i++)
            {
                if (canvases[i] != null && canvases[i].name == "PrototypeHUDCanvas")
                {
                    return canvases[i];
                }
            }

            return null;
        }
    }
}
