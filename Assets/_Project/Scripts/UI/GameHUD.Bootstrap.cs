using System.IO;
using DuckovProto.MagicCircles;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

namespace DuckovProto.UI
{
    public sealed partial class GameHUD
    {
        private void EnsureCanvasAndLayout()
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
            root.anchorMin = Vector2.zero;
            root.anchorMax = Vector2.one;
            root.offsetMin = Vector2.zero;
            root.offsetMax = Vector2.zero;

            BuildVitalsPanel(root);
            BuildStatePanel(root);
            BuildSystemButtons(root);
        }

        private void EnsureMagicCircleGuidePanel()
        {
            if (!showMagicCircleGuidePanel)
            {
                if (magicCircleGuidePanel != null)
                {
                    magicCircleGuidePanel.enabled = false;
                }

                return;
            }

            if (magicCircleGuidePanel == null)
            {
                magicCircleGuidePanel = GetComponent<MagicCircleGuidePanel>();
                if (magicCircleGuidePanel == null)
                {
                    magicCircleGuidePanel = gameObject.AddComponent<MagicCircleGuidePanel>();
                }
            }

            magicCircleGuidePanel.enabled = true;
            magicCircleGuidePanel.AttachToCanvas(canvas, hudFont);
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

        private void RestartScene()
        {
            string currentPath = SceneManager.GetActiveScene().path;
            string currentName = SceneManager.GetActiveScene().name;
            string targetName = !string.IsNullOrWhiteSpace(restartSceneName) ? restartSceneName : currentName;
            string targetPath = !string.IsNullOrWhiteSpace(restartScenePath) ? restartScenePath : currentPath;

#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                if (!string.IsNullOrWhiteSpace(targetPath) && File.Exists(targetPath))
                {
                    EditorSceneManager.OpenScene(targetPath);
                    return;
                }
            }
#endif

            if (!string.IsNullOrWhiteSpace(targetName))
            {
                SceneManager.LoadScene(targetName);
                return;
            }

            if (!string.IsNullOrWhiteSpace(currentName))
            {
                SceneManager.LoadScene(currentName);
            }
        }

        private static void QuitApplication()
        {
#if UNITY_EDITOR
            EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private static Canvas FindExistingCanvas()
        {
            if (MagicCircleRuntimeLocator.TryGetHudCanvas(out Canvas existingCanvas))
            {
                return existingCanvas;
            }

            return null;
        }
    }
}
