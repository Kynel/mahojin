using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace DuckovProto.Feedback
{
    public sealed class VolumeBootstrapper : MonoBehaviour
    {
        [SerializeField] private bool enableBloom = true;
        [SerializeField] private float targetIntensity = 0.45f;
        [SerializeField] private float targetThreshold = 1.1f;
        [SerializeField] private float targetScatter = 0.6f;

        private void Start()
        {
            if (!enableBloom)
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

            if (!profile.TryGet(out Bloom bloom))
            {
                bloom = profile.Add<Bloom>(true);
            }

            bloom.active = true;
            bloom.intensity.overrideState = true;
            bloom.threshold.overrideState = true;
            bloom.scatter.overrideState = true;

            // Keep bloom subtle: never below soft floor, never above strong cap.
            float intensity = Mathf.Clamp(targetIntensity, 0.25f, 0.6f);
            float threshold = Mathf.Clamp(targetThreshold, 1.0f, 1.2f);
            float scatter = Mathf.Clamp(targetScatter, 0.45f, 0.7f);

            if (bloom.intensity.value > 0f)
            {
                intensity = Mathf.Clamp(bloom.intensity.value, 0.25f, 0.6f);
            }

            if (bloom.threshold.value > 0f)
            {
                threshold = Mathf.Clamp(bloom.threshold.value, 1.0f, 1.2f);
            }

            bloom.intensity.value = intensity;
            bloom.threshold.value = threshold;
            bloom.scatter.value = scatter;
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
