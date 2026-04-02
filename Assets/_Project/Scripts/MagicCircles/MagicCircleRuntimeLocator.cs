using UnityEngine;

namespace DuckovProto.MagicCircles
{
    public static class MagicCircleRuntimeLocator
    {
        private const string HudCanvasName = "GameHUDCanvas";

        private static MagicCircleCastPipeline cachedPipeline;
        private static MagicCircleCastController cachedCastController;
        private static MagicCircleDrawController cachedDrawController;
        private static Canvas cachedHudCanvas;

        public static bool TryGetPipeline(out MagicCircleCastPipeline pipeline)
        {
            Resolve();
            pipeline = cachedPipeline;
            return pipeline != null;
        }

        public static bool TryGetCastController(out MagicCircleCastController castController)
        {
            Resolve();
            castController = cachedCastController;
            return castController != null;
        }

        public static bool TryGetDrawController(out MagicCircleDrawController drawController)
        {
            Resolve();
            drawController = cachedDrawController;
            return drawController != null;
        }

        public static bool TryGetHudCanvas(out Canvas hudCanvas)
        {
            Resolve();
            hudCanvas = cachedHudCanvas;
            return hudCanvas != null;
        }

        private static void Resolve()
        {
            if (cachedPipeline == null)
            {
                cachedPipeline = Object.FindFirstObjectByType<MagicCircleCastPipeline>();
            }

            if (cachedCastController == null)
            {
                cachedCastController = Object.FindFirstObjectByType<MagicCircleCastController>();
            }

            if (cachedDrawController == null)
            {
                cachedDrawController = Object.FindFirstObjectByType<MagicCircleDrawController>();
            }

            if (cachedHudCanvas == null)
            {
                Canvas[] canvases = Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None);
                for (int i = 0; i < canvases.Length; i++)
                {
                    Canvas candidate = canvases[i];
                    if (candidate != null && candidate.name == HudCanvasName)
                    {
                        cachedHudCanvas = candidate;
                        break;
                    }
                }
            }
        }
    }
}
