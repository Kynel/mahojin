using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace DuckovProto.Vision
{
    [DisallowMultipleComponent]
    public sealed class VisionPostFXBootstrapper : MonoBehaviour
    {
        [SerializeField] private bool enableVisionDarkening = true;
        [SerializeField] private float targetPostExposure = 0f;

        private void Start()
        {
            ApplyVisionExposure();
        }

        private void OnEnable()
        {
            ApplyVisionExposure();
        }

        private void OnValidate()
        {
            targetPostExposure = Mathf.Clamp(targetPostExposure, -2f, 0.5f);
        }

        private void ApplyVisionExposure()
        {
            if (!enableVisionDarkening)
            {
                return;
            }

            Volume volume = FindGlobalVolume();
            if (volume == null)
            {
                return;
            }

            VolumeProfile profile = volume.profile != null ? volume.profile : volume.sharedProfile;
            if (profile == null)
            {
                return;
            }

            if (!profile.TryGet(out ColorAdjustments colorAdjustments))
            {
                colorAdjustments = profile.Add<ColorAdjustments>(true);
            }

            colorAdjustments.active = true;
            colorAdjustments.postExposure.overrideState = true;
            colorAdjustments.postExposure.value = Mathf.Clamp(targetPostExposure, -2f, 0.5f);
        }

        private static Volume FindGlobalVolume()
        {
            Volume[] volumes = FindObjectsByType<Volume>(FindObjectsSortMode.None);
            for (int i = 0; i < volumes.Length; i++)
            {
                if (volumes[i] != null && volumes[i].isGlobal)
                {
                    return volumes[i];
                }
            }

            return null;
        }
    }
}
