using UnityEngine;
using System.Collections.Generic;

namespace DuckovProto.Feedback
{
    public sealed class FeedbackRunner : MonoBehaviour
    {
        private static FeedbackRunner instance;
        public static FeedbackRunner Instance
        {
            get
            {
                if (instance == null)
                {
                    GameObject go = new GameObject("FeedbackRunner");
                    instance = go.AddComponent<FeedbackRunner>();
                    DontDestroyOnLoad(go);
                }

                return instance;
            }
        }

        private bool hitStopActive;
        private Camera shakeCamera;
        private Vector3 shakeBaseLocalPos;
        private float shakeTimer;
        private float shakeDuration;
        private float shakeAmplitude;
        private readonly Queue<ParticleSystem> burstPool = new Queue<ParticleSystem>(32);

        public bool IsHitStopActive => hitStopActive;

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void LateUpdate()
        {
            if (shakeTimer <= 0f || shakeCamera == null)
            {
                return;
            }

            shakeTimer -= Time.unscaledDeltaTime;
            if (shakeTimer <= 0f)
            {
                shakeCamera.transform.localPosition = shakeBaseLocalPos;
                shakeCamera = null;
                return;
            }

            float t = shakeTimer / Mathf.Max(0.0001f, shakeDuration);
            float currentAmp = shakeAmplitude * t;
            Vector2 random = Random.insideUnitCircle * currentAmp;
            shakeCamera.transform.localPosition = shakeBaseLocalPos + new Vector3(random.x, random.y, 0f);
        }

        public void BeginCameraShake(float duration, float amplitude)
        {
            Camera main = Camera.main;
            if (main == null || duration <= 0f || amplitude <= 0f)
            {
                return;
            }

            if (shakeCamera != main)
            {
                if (shakeCamera != null)
                {
                    shakeCamera.transform.localPosition = shakeBaseLocalPos;
                }

                shakeCamera = main;
                shakeBaseLocalPos = shakeCamera.transform.localPosition;
            }

            shakeDuration = Mathf.Max(duration, 0.01f);
            shakeAmplitude = Mathf.Max(shakeAmplitude, amplitude);
            shakeTimer = Mathf.Max(shakeTimer, duration);
        }

        public void SetHitStopActive(bool active)
        {
            hitStopActive = active;
        }

        public ParticleSystem RentBurstParticle()
        {
            if (burstPool.Count > 0)
            {
                ParticleSystem pooled = burstPool.Dequeue();
                if (pooled != null)
                {
                    pooled.gameObject.SetActive(true);
                    return pooled;
                }
            }

            GameObject go = new GameObject("Fx_Burst");
            ParticleSystem ps = go.AddComponent<ParticleSystem>();
            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return ps;
        }

        public void ReturnBurstParticle(ParticleSystem ps)
        {
            if (ps == null)
            {
                return;
            }

            if (burstPool.Count >= 48)
            {
                Destroy(ps.gameObject);
                return;
            }

            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ps.gameObject.SetActive(false);
            burstPool.Enqueue(ps);
        }
    }
}
