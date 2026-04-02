using UnityEngine;
using UnityEngine.UI;

namespace DuckovProto.UI
{
    public sealed partial class GameHUD
    {
        private void BuildVitalsPanel(RectTransform root)
        {
            RectTransform panel = EnsureChildRect(root, "VitalsPanel");
            panel.anchorMin = new Vector2(0f, 0f);
            panel.anchorMax = new Vector2(0f, 0f);
            panel.pivot = new Vector2(0f, 0f);
            panel.anchoredPosition = new Vector2(20f, 20f);
            panel.sizeDelta = new Vector2(440f, 122f);

            Image panelBg = EnsureImage(panel, new Color(0f, 0f, 0f, 0.30f));
            panelBg.raycastTarget = false;

            CreateBar(panel, "HPBar", new Vector2(14f, -16f), new Color(0.50f, 0.06f, 0.10f, 1f), out hpFill, out hpText);
            CreateBar(panel, "MPBar", new Vector2(14f, -68f), new Color(0.12f, 0.42f, 0.66f, 1f), out mpFill, out mpText);
        }

        private void BuildStatePanel(RectTransform root)
        {
            RectTransform panel = EnsureChildRect(root, "StatePanel");
            panel.anchorMin = new Vector2(0.5f, 0f);
            panel.anchorMax = new Vector2(0.5f, 0f);
            panel.pivot = new Vector2(0.5f, 0f);
            panel.anchoredPosition = new Vector2(0f, 24f);
            panel.sizeDelta = new Vector2(520f, 34f);

            Image bg = EnsureImage(panel, new Color(0f, 0f, 0f, 0.35f));
            bg.raycastTarget = false;

            stateText = EnsureText(panel, "StateText", 14, TextAnchor.MiddleCenter, Color.white);
            RectTransform rect = stateText.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private void BuildSystemButtons(RectTransform root)
        {
            RectTransform panel = EnsureChildRect(root, "SystemButtonsPanel");
            panel.anchorMin = new Vector2(1f, 1f);
            panel.anchorMax = new Vector2(1f, 1f);
            panel.pivot = new Vector2(1f, 1f);
            panel.anchoredPosition = new Vector2(-20f, -20f);
            panel.sizeDelta = new Vector2(232f, 46f);

            HorizontalLayoutGroup layout = panel.GetComponent<HorizontalLayoutGroup>();
            if (layout == null)
            {
                layout = panel.gameObject.AddComponent<HorizontalLayoutGroup>();
            }

            layout.spacing = 8f;
            layout.childAlignment = TextAnchor.MiddleRight;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            CreateButton(panel, "RestartButton", "재시작", RestartScene);
            CreateButton(panel, "QuitButton", "게임종료", QuitApplication);
        }

        private void UpdateVitals()
        {
            float hpNormalized = 0f;
            float mpNormalized = 0f;
            string hpLabel = "HP ?/?";
            string mpLabel = "MP ?/?";

            if (playerVitals != null)
            {
                float maxHp = Mathf.Max(1f, playerVitals.MaxHP);
                float maxMp = Mathf.Max(1f, playerVitals.MaxMana);
                hpNormalized = Mathf.Clamp01(playerVitals.HP / maxHp);
                mpNormalized = Mathf.Clamp01(playerVitals.Mana / maxMp);
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

            string mode = magicCircleDrawController != null ? magicCircleDrawController.DebugStateLabel : "Normal";
            string castState = magicCircleCastController != null ? magicCircleCastController.CooldownStateText : string.Empty;
            stateText.text = string.IsNullOrWhiteSpace(castState) ? mode : $"{mode} | {castState}";
        }

        private void CreateBar(
            RectTransform parent,
            string name,
            Vector2 anchoredPosition,
            Color fillColor,
            out Image fill,
            out Text label)
        {
            RectTransform barRoot = EnsureChildRect(parent, name);
            barRoot.anchorMin = new Vector2(0f, 1f);
            barRoot.anchorMax = new Vector2(0f, 1f);
            barRoot.pivot = new Vector2(0f, 1f);
            barRoot.anchoredPosition = anchoredPosition;
            barRoot.sizeDelta = new Vector2(412f, 34f);

            Image bg = EnsureImage(barRoot, new Color(0f, 0f, 0f, 0.55f));
            bg.raycastTarget = false;

            RectTransform fillRect = EnsureChildRect(barRoot, "Fill");
            fillRect.anchorMin = new Vector2(0f, 0f);
            fillRect.anchorMax = new Vector2(1f, 1f);
            fillRect.offsetMin = new Vector2(2f, 2f);
            fillRect.offsetMax = new Vector2(-2f, -2f);

            fill = EnsureImage(fillRect, fillColor);
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillAmount = 1f;
            fill.raycastTarget = false;

            label = EnsureText(barRoot, "Label", 17, TextAnchor.MiddleLeft, Color.white);
            RectTransform labelRect = label.rectTransform;
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = new Vector2(12f, 0f);
            labelRect.offsetMax = new Vector2(-12f, 0f);
        }

        private void CreateButton(RectTransform parent, string name, string label, UnityEngine.Events.UnityAction onClick)
        {
            RectTransform buttonRect = EnsureChildRect(parent, name);
            LayoutElement layout = buttonRect.GetComponent<LayoutElement>();
            if (layout == null)
            {
                layout = buttonRect.gameObject.AddComponent<LayoutElement>();
            }

            layout.preferredWidth = 112f;
            layout.preferredHeight = 38f;

            Image bg = EnsureImage(buttonRect, new Color(0f, 0f, 0f, 0.58f));
            bg.raycastTarget = true;

            Button button = buttonRect.GetComponent<Button>();
            if (button == null)
            {
                button = buttonRect.gameObject.AddComponent<Button>();
            }

            button.targetGraphic = bg;
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(onClick);

            Text text = EnsureText(buttonRect, "Label", 14, TextAnchor.MiddleCenter, new Color(1f, 0.97f, 0.84f, 0.96f));
            text.text = label;
            RectTransform textRect = text.rectTransform;
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;
        }

        private RectTransform EnsureChildRect(Transform parent, string name)
        {
            Transform existing = parent.Find(name);
            RectTransform rect;
            if (existing == null)
            {
                GameObject child = new GameObject(name);
                child.transform.SetParent(parent, false);
                rect = child.AddComponent<RectTransform>();
            }
            else
            {
                rect = existing.GetComponent<RectTransform>();
                if (rect == null)
                {
                    rect = existing.gameObject.AddComponent<RectTransform>();
                }
            }

            rect.localScale = Vector3.one;
            return rect;
        }

        private Image EnsureImage(RectTransform rect, Color color)
        {
            Image image = rect.GetComponent<Image>();
            if (image == null)
            {
                image = rect.gameObject.AddComponent<Image>();
            }

            image.color = color;
            return image;
        }

        private Text EnsureText(RectTransform parent, string name, int size, TextAnchor anchor, Color color)
        {
            RectTransform textRect = EnsureChildRect(parent, name);
            Text text = textRect.GetComponent<Text>();
            if (text == null)
            {
                text = textRect.gameObject.AddComponent<Text>();
            }

            text.font = hudFont;
            text.fontSize = size;
            text.alignment = anchor;
            text.color = color;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.supportRichText = false;
            return text;
        }
    }
}
