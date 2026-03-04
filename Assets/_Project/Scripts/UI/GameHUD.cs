using DuckovProto.Player;
using DuckovProto.Runes;
using System.IO;
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
        private struct AbilitySlot
        {
            public RuneCastController.AbilityId Id;
            public Image Icon;
            public Image CooldownOverlay;
            public Image UnavailableOverlay;
            public Text RuneLabel;
            public Text CostLabel;
        }

        [SerializeField] private RuneCastController runeCastController;
        [SerializeField] private PlayerVitals playerVitals;
        [SerializeField] private bool disableLegacyPrototypeHud = true;
        [SerializeField] private string restartSceneName = "Prototype_Arena";
        [SerializeField] private string restartScenePath = "Assets/_Project/Scenes/Prototype_Arena.unity";

        private Canvas canvas;
        private Image hpFill;
        private Image mpFill;
        private Text hpText;
        private Text mpText;
        private Text stateText;
        private CanvasGroup abilityGroup;
        private AbilitySlot[] abilitySlots;
        private Font hudFont;

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
            UpdateAbilities();
            UpdateStateMessage();
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

        private void UpdateAbilities()
        {
            if (abilitySlots == null || abilitySlots.Length == 0)
            {
                return;
            }

            float globalRemaining = runeCastController != null ? runeCastController.GetGlobalCooldownRemaining() : 0f;
            if (abilityGroup != null)
            {
                abilityGroup.alpha = globalRemaining > 0f ? 0.85f : 1f;
            }

            float currentMana = playerVitals != null ? playerVitals.Mana : float.MaxValue;
            const float eps = 0.0001f;

            for (int i = 0; i < abilitySlots.Length; i++)
            {
                AbilitySlot slot = abilitySlots[i];

                float cooldownDuration = runeCastController != null
                    ? runeCastController.GetCooldownDuration(slot.Id)
                    : 0f;
                float cooldownRemaining = runeCastController != null
                    ? runeCastController.GetCooldownRemaining(slot.Id)
                    : 0f;
                float cooldownRatio = cooldownDuration > 0f
                    ? Mathf.Clamp01(cooldownRemaining / cooldownDuration)
                    : 0f;

                if (slot.CooldownOverlay != null)
                {
                    slot.CooldownOverlay.fillAmount = cooldownRatio;
                    slot.CooldownOverlay.enabled = cooldownRatio > 0f;
                }

                float manaCost = runeCastController != null
                    ? runeCastController.GetManaCost(slot.Id)
                    : 0f;
                bool hasEnoughMana = currentMana + eps >= manaCost;
                bool available = runeCastController != null && runeCastController.IsCastAvailable(slot.Id);

                if (slot.Icon != null)
                {
                    Color iconColor = slot.Icon.color;
                    iconColor.a = hasEnoughMana ? (available ? 1f : 0.8f) : 0.35f;
                    slot.Icon.color = iconColor;
                }

                if (slot.UnavailableOverlay != null)
                {
                    Color dimColor = slot.UnavailableOverlay.color;
                    dimColor.a = hasEnoughMana ? 0f : 0.45f;
                    slot.UnavailableOverlay.color = dimColor;
                    slot.UnavailableOverlay.enabled = !hasEnoughMana;
                }

                if (slot.CostLabel != null)
                {
                    slot.CostLabel.text = manaCost > 0f ? Mathf.CeilToInt(manaCost).ToString() : string.Empty;
                }

                if (slot.RuneLabel != null)
                {
                    slot.RuneLabel.text = runeCastController != null
                        ? CompactRuneLabel(runeCastController.GetRuneLabel(slot.Id))
                        : string.Empty;
                }
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
            BuildAbilityPanel(root);
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

        private void BuildAbilityPanel(RectTransform root)
        {
            RectTransform panel = EnsureChildRect(root, "AbilityPanel");
            panel.anchorMin = new Vector2(0.5f, 0f);
            panel.anchorMax = new Vector2(0.5f, 0f);
            panel.pivot = new Vector2(0.5f, 0f);
            panel.anchoredPosition = new Vector2(0f, 24f);
            panel.sizeDelta = new Vector2(380f, 120f);

            stateText = EnsureText(panel, "StateText", 14, TextAnchor.MiddleCenter, Color.white);
            RectTransform stateRect = stateText.rectTransform;
            stateRect.anchorMin = new Vector2(0f, 1f);
            stateRect.anchorMax = new Vector2(1f, 1f);
            stateRect.pivot = new Vector2(0.5f, 1f);
            stateRect.anchoredPosition = new Vector2(0f, -4f);
            stateRect.sizeDelta = new Vector2(0f, 20f);

            RectTransform slotsRoot = EnsureChildRect(panel, "Slots");
            slotsRoot.anchorMin = new Vector2(0.5f, 0f);
            slotsRoot.anchorMax = new Vector2(0.5f, 0f);
            slotsRoot.pivot = new Vector2(0.5f, 0f);
            slotsRoot.anchoredPosition = new Vector2(0f, 0f);
            slotsRoot.sizeDelta = new Vector2(380f, 74f);

            HorizontalLayoutGroup layout = slotsRoot.GetComponent<HorizontalLayoutGroup>();
            if (layout == null)
            {
                layout = slotsRoot.gameObject.AddComponent<HorizontalLayoutGroup>();
            }

            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.spacing = 10f;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            layout.padding = new RectOffset(0, 0, 0, 0);

            abilityGroup = slotsRoot.GetComponent<CanvasGroup>();
            if (abilityGroup == null)
            {
                abilityGroup = slotsRoot.gameObject.AddComponent<CanvasGroup>();
            }

            RuneCastController.AbilityId[] ids =
            {
                RuneCastController.AbilityId.Firebolt,
                RuneCastController.AbilityId.IceLance,
                RuneCastController.AbilityId.Blink,
                RuneCastController.AbilityId.Nova,
                RuneCastController.AbilityId.Chain,
            };

            abilitySlots = new AbilitySlot[ids.Length];
            for (int i = 0; i < ids.Length; i++)
            {
                abilitySlots[i] = EnsureAbilitySlot(slotsRoot, ids[i]);
            }
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

        private AbilitySlot EnsureAbilitySlot(RectTransform parent, RuneCastController.AbilityId id)
        {
            RectTransform slot = EnsureChildRect(parent, $"Slot_{id}");
            LayoutElement layout = slot.GetComponent<LayoutElement>();
            if (layout == null)
            {
                layout = slot.gameObject.AddComponent<LayoutElement>();
            }

            layout.preferredWidth = 62f;
            layout.preferredHeight = 62f;
            layout.flexibleWidth = 0f;
            layout.flexibleHeight = 0f;

            EnsureImage(slot, new Color(0f, 0f, 0f, 0.45f));

            RectTransform iconRect = EnsureChildRect(slot, "Icon");
            iconRect.anchorMin = new Vector2(0f, 0f);
            iconRect.anchorMax = new Vector2(1f, 1f);
            iconRect.offsetMin = new Vector2(4f, 4f);
            iconRect.offsetMax = new Vector2(-4f, -4f);
            Image icon = EnsureImage(iconRect, AbilityColor(id));

            RectTransform cooldownRect = EnsureChildRect(slot, "Cooldown");
            cooldownRect.anchorMin = new Vector2(0f, 0f);
            cooldownRect.anchorMax = new Vector2(1f, 1f);
            cooldownRect.offsetMin = new Vector2(4f, 4f);
            cooldownRect.offsetMax = new Vector2(-4f, -4f);
            Image cooldown = EnsureImage(cooldownRect, new Color(0f, 0f, 0f, 0.62f));
            cooldown.type = Image.Type.Filled;
            cooldown.fillMethod = Image.FillMethod.Radial360;
            cooldown.fillOrigin = (int)Image.Origin360.Top;
            cooldown.fillClockwise = false;
            cooldown.fillAmount = 0f;
            cooldown.enabled = false;

            RectTransform unavailableRect = EnsureChildRect(slot, "Unavailable");
            unavailableRect.anchorMin = new Vector2(0f, 0f);
            unavailableRect.anchorMax = new Vector2(1f, 1f);
            unavailableRect.offsetMin = new Vector2(4f, 4f);
            unavailableRect.offsetMax = new Vector2(-4f, -4f);
            Image unavailable = EnsureImage(unavailableRect, new Color(0f, 0f, 0f, 0f));
            unavailable.enabled = false;

            Text runeLabel = EnsureText(slot, "RuneLabel", 12, TextAnchor.LowerLeft, Color.white);
            RectTransform runeRect = runeLabel.rectTransform;
            runeRect.anchorMin = new Vector2(0f, 0f);
            runeRect.anchorMax = new Vector2(0f, 0f);
            runeRect.pivot = new Vector2(0f, 0f);
            runeRect.anchoredPosition = new Vector2(6f, 4f);
            runeRect.sizeDelta = new Vector2(24f, 14f);

            Text costLabel = EnsureText(slot, "CostLabel", 12, TextAnchor.UpperRight, new Color(1f, 1f, 1f, 0.9f));
            RectTransform costRect = costLabel.rectTransform;
            costRect.anchorMin = new Vector2(1f, 1f);
            costRect.anchorMax = new Vector2(1f, 1f);
            costRect.pivot = new Vector2(1f, 1f);
            costRect.anchoredPosition = new Vector2(-6f, -4f);
            costRect.sizeDelta = new Vector2(24f, 14f);

            return new AbilitySlot
            {
                Id = id,
                Icon = icon,
                CooldownOverlay = cooldown,
                UnavailableOverlay = unavailable,
                RuneLabel = runeLabel,
                CostLabel = costLabel,
            };
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

        private static Color AbilityColor(RuneCastController.AbilityId id)
        {
            switch (id)
            {
                case RuneCastController.AbilityId.Firebolt:
                    return new Color(0.92f, 0.35f, 0.16f, 1f);
                case RuneCastController.AbilityId.IceLance:
                    return new Color(0.32f, 0.75f, 0.96f, 1f);
                case RuneCastController.AbilityId.Blink:
                    return new Color(0.52f, 0.88f, 0.45f, 1f);
                case RuneCastController.AbilityId.Nova:
                    return new Color(0.94f, 0.82f, 0.3f, 1f);
                case RuneCastController.AbilityId.Chain:
                    return new Color(0.98f, 0.93f, 0.44f, 1f);
                default:
                    return Color.white;
            }
        }

        private static string CompactRuneLabel(string rune)
        {
            switch (rune)
            {
                case "Circle": return "O";
                case "Line": return "-";
                case "Triangle": return "^";
                default: return rune;
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
