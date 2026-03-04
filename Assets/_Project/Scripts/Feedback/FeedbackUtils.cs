using System.Collections;
using UnityEngine;

namespace DuckovProto.Feedback
{
    public enum VfxPreset
    {
        Fire,
        Ice,
        Arcane,
        Lightning
    }

    public static class FeedbackUtils
    {
        public static void SpawnRing(Vector3 pos, float radius, float lifetime, float yOffset)
        {
            SpawnRing(pos, radius, lifetime, yOffset, VfxPreset.Arcane, 1f);
        }

        public static void SpawnRing(
            Vector3 pos,
            float radius,
            float lifetime,
            float yOffset,
            VfxPreset preset,
            float intensity)
        {
            if (lifetime <= 0f || radius <= 0f)
            {
                return;
            }

            GameObject go = new GameObject("Fx_Ring");
            LineRenderer lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace = true;
            lr.loop = true;
            lr.positionCount = 48;
            lr.widthMultiplier = Mathf.Lerp(0.055f, 0.028f, Mathf.Clamp01(intensity));
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.receiveShadows = false;
            lr.material = new Material(Shader.Find("Sprites/Default"));
            Color color = MainColor(preset, intensity);

            FeedbackRunner.Instance.StartCoroutine(AnimateRing(lr, pos + Vector3.up * yOffset, radius, lifetime, color));
        }

        public static void SpawnGlyphConvergeBurst(
            Vector3 pos,
            VfxPreset preset,
            float baseRadius,
            float totalDuration = 0.3f,
            float intensity = 1f)
        {
            totalDuration = Mathf.Clamp(totalDuration, 0.25f, 0.35f);
            FeedbackRunner.Instance.StartCoroutine(AnimateGlyphConvergeBurst(pos, preset, Mathf.Max(0.1f, baseRadius), totalDuration, intensity));
        }

        public static void SpawnImpactPuff(Vector3 pos, float lifetime)
        {
            if (lifetime <= 0f)
            {
                return;
            }

            int count = Random.Range(3, 6);
            for (int i = 0; i < count; i++)
            {
                GameObject puff = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                puff.name = "Fx_Puff";
                puff.transform.position = pos + Vector3.up * 0.06f;
                float startScale = Random.Range(0.06f, 0.1f);
                puff.transform.localScale = Vector3.one * startScale;

                Collider col = puff.GetComponent<Collider>();
                if (col != null)
                {
                    Object.Destroy(col);
                }

                Renderer renderer = puff.GetComponent<Renderer>();
                if (renderer != null)
                {
                    renderer.material = new Material(Shader.Find("Sprites/Default"));
                    renderer.material.color = new Color(1f, 1f, 1f, 0.9f);
                }

                Vector2 dir2D = Random.insideUnitCircle.normalized;
                if (dir2D.sqrMagnitude < 0.001f)
                {
                    dir2D = Vector2.right;
                }

                Vector3 endOffset = new Vector3(dir2D.x, Random.Range(0.06f, 0.16f), dir2D.y) * Random.Range(0.22f, 0.42f);
                FeedbackRunner.Instance.StartCoroutine(AnimatePuff(puff.transform, renderer, puff.transform.position, puff.transform.position + endOffset, lifetime));
            }
        }

        public static void SpawnParticleBurst(
            VfxPreset preset,
            Vector3 pos,
            int burstCount = 12,
            float intensity = 1f,
            float sizeScale = 1f,
            float speedScale = 1f,
            float lifetimeScale = 1f,
            float spread = 1f)
        {
            if (burstCount <= 0)
            {
                return;
            }

            PresetConfig cfg = GetPresetConfig(preset);
            float startLifetime = cfg.baseLifetime * Mathf.Clamp(lifetimeScale, 0.4f, 1.8f);
            float startSpeed = cfg.baseSpeed * Mathf.Clamp(speedScale, 0.4f, 2.2f);
            float startSize = cfg.baseSize * Mathf.Clamp(sizeScale, 0.4f, 2.2f);
            float intensityClamped = Mathf.Clamp(intensity, 0.5f, 1.8f);

            ParticleSystem ps = FeedbackRunner.Instance.RentBurstParticle();
            Transform t = ps.transform;
            t.position = pos;

            ParticleSystem.MainModule main = ps.main;
            main.playOnAwake = false;
            main.loop = false;
            main.startLifetime = startLifetime;
            main.startSpeed = startSpeed;
            main.startSize = startSize;
            main.startColor = BuildGradient(cfg.mainColor * intensityClamped, cfg.highlightColor * intensityClamped);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = Mathf.Max(24, burstCount * 3);
            main.gravityModifier = 0f;

            ParticleSystem.EmissionModule emission = ps.emission;
            emission.enabled = false;

            ParticleSystem.ShapeModule shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = Mathf.Max(0.01f, spread * 0.22f);

            ParticleSystem.SizeOverLifetimeModule sizeOverLifetime = ps.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, cfg.sizeOverLifetime);

            ParticleSystem.NoiseModule noise = ps.noise;
            noise.enabled = true;
            noise.strength = cfg.noiseStrength;
            noise.frequency = cfg.noiseFrequency;
            noise.scrollSpeed = 0.35f;

            ParticleSystem.VelocityOverLifetimeModule velocity = ps.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.Local;
            velocity.radial = new ParticleSystem.MinMaxCurve(cfg.radialVelocity);

            ParticleSystem.ColorOverLifetimeModule colorOverLifetime = ps.colorOverLifetime;
            colorOverLifetime.enabled = true;
            colorOverLifetime.color = BuildGradient(
                cfg.mainColor * intensityClamped,
                new Color(cfg.smokeColor.r, cfg.smokeColor.g, cfg.smokeColor.b, 0f));

            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ps.Emit(burstCount);
            ps.Play();
            FeedbackRunner.Instance.StartCoroutine(ReturnBurstAfter(ps, startLifetime + 0.15f));
        }

        public static void SpawnParticleBurst(
            Vector3 pos,
            Color startColor,
            int count,
            float startLifetime,
            float startSpeed,
            float startSize,
            float spread = 1f)
        {
            // Backward-compatible wrapper.
            SpawnParticleBurst(VfxPreset.Arcane, pos, count, 1f, startSize / 0.1f, startSpeed / 5f, startLifetime / 0.2f, spread);
        }

        public static void CameraShake(float duration, float amplitude)
        {
            FeedbackRunner.Instance.BeginCameraShake(duration, amplitude);
        }

        public static void HitStop(float duration)
        {
            if (duration <= 0f)
            {
                return;
            }

            if (FeedbackRunner.Instance.IsHitStopActive)
            {
                return;
            }

            FeedbackRunner.Instance.StartCoroutine(HitStopRoutine(duration));
        }

        public static void SpawnLightningLine(Vector3 start, Vector3 end, float lifetime, float width)
        {
            if (lifetime <= 0f)
            {
                Debug.DrawLine(start, end, Color.yellow, 0.08f);
                return;
            }

            GameObject go = new GameObject("Fx_ChainLine");
            LineRenderer lr = go.AddComponent<LineRenderer>();
            lr.positionCount = 2;
            lr.useWorldSpace = true;
            lr.SetPosition(0, start);
            lr.SetPosition(1, end);
            lr.widthMultiplier = Mathf.Max(0.01f, width);
            lr.numCornerVertices = 2;
            lr.numCapVertices = 2;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.receiveShadows = false;
            lr.material = new Material(Shader.Find("Sprites/Default"));

            FeedbackRunner.Instance.StartCoroutine(BlinkLightning(lr, lifetime));
        }

        private static IEnumerator AnimateRing(LineRenderer lr, Vector3 center, float targetRadius, float lifetime, Color baseColor)
        {
            int segments = lr.positionCount;
            float elapsed = 0f;
            float flashDuration = Mathf.Min(0.1f, lifetime * 0.4f);
            while (elapsed < lifetime && lr != null)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / Mathf.Max(0.0001f, lifetime));
                float expandT = Mathf.Clamp01(elapsed / Mathf.Max(0.0001f, flashDuration));
                float radius = Mathf.Lerp(targetRadius * 0.2f, targetRadius, expandT);
                float alpha = 1f - t;

                lr.startColor = new Color(baseColor.r, baseColor.g, baseColor.b, alpha * 0.9f);
                lr.endColor = new Color(baseColor.r, baseColor.g, baseColor.b, alpha * 0.9f);

                for (int i = 0; i < segments; i++)
                {
                    float a = ((float)i / segments) * Mathf.PI * 2f;
                    Vector3 p = center + new Vector3(Mathf.Cos(a) * radius, 0f, Mathf.Sin(a) * radius);
                    lr.SetPosition(i, p);
                }

                yield return null;
            }

            if (lr != null)
            {
                Object.Destroy(lr.gameObject);
            }
        }

        private static IEnumerator AnimateGlyphConvergeBurst(
            Vector3 center,
            VfxPreset preset,
            float baseRadius,
            float totalDuration,
            float intensity)
        {
            const int ringCount = 3;
            const int segments = 36;
            LineRenderer[] rings = new LineRenderer[ringCount];

            for (int i = 0; i < ringCount; i++)
            {
                GameObject go = new GameObject($"Fx_GlyphRing_{i}");
                LineRenderer lr = go.AddComponent<LineRenderer>();
                lr.useWorldSpace = true;
                lr.loop = true;
                lr.positionCount = segments;
                lr.widthMultiplier = Mathf.Lerp(0.04f, 0.02f, (float)i / ringCount);
                lr.material = new Material(Shader.Find("Sprites/Default"));
                lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                lr.receiveShadows = false;
                rings[i] = lr;
            }

            float converge = 0.12f;
            float release = 0.08f;
            float fade = Mathf.Max(0.05f, totalDuration - (converge + release));
            float elapsed = 0f;
            Color main = MainColor(preset, intensity);

            while (elapsed < totalDuration)
            {
                elapsed += Time.deltaTime;
                float alpha;
                float phaseRadiusT;
                if (elapsed <= converge)
                {
                    phaseRadiusT = Mathf.Lerp(1.25f, 0.78f, elapsed / converge);
                    alpha = 0.95f;
                }
                else if (elapsed <= converge + release)
                {
                    float t = (elapsed - converge) / release;
                    phaseRadiusT = Mathf.Lerp(0.78f, 1.48f, t);
                    alpha = Mathf.Lerp(0.95f, 0.55f, t);
                }
                else
                {
                    float t = (elapsed - converge - release) / fade;
                    phaseRadiusT = 1.48f;
                    alpha = Mathf.Lerp(0.55f, 0f, t);
                }

                for (int i = 0; i < ringCount; i++)
                {
                    if (rings[i] == null)
                    {
                        continue;
                    }

                    float ringRadius = baseRadius * phaseRadiusT * (0.9f + i * 0.16f);
                    float spin = elapsed * (1.6f + i * 1.1f);
                    Color c = new Color(main.r, main.g, main.b, alpha * (1f - i * 0.15f));
                    rings[i].startColor = c;
                    rings[i].endColor = c;

                    for (int s = 0; s < segments; s++)
                    {
                        float a = ((float)s / segments) * Mathf.PI * 2f + spin;
                        Vector3 p = center + new Vector3(Mathf.Cos(a) * ringRadius, 0.04f + i * 0.01f, Mathf.Sin(a) * ringRadius);
                        rings[i].SetPosition(s, p);
                    }
                }

                yield return null;
            }

            for (int i = 0; i < ringCount; i++)
            {
                if (rings[i] != null)
                {
                    Object.Destroy(rings[i].gameObject);
                }
            }
        }

        private static IEnumerator AnimatePuff(Transform puff, Renderer renderer, Vector3 start, Vector3 end, float lifetime)
        {
            float elapsed = 0f;
            Vector3 startScale = puff.localScale;
            Color color = renderer != null ? renderer.material.color : Color.white;
            while (elapsed < lifetime && puff != null)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / Mathf.Max(0.0001f, lifetime));
                puff.position = Vector3.Lerp(start, end, t);
                puff.localScale = Vector3.Lerp(startScale, Vector3.zero, t);
                if (renderer != null)
                {
                    renderer.material.color = new Color(color.r, color.g, color.b, 1f - t);
                }

                yield return null;
            }

            if (puff != null)
            {
                Object.Destroy(puff.gameObject);
            }
        }

        private static IEnumerator ReturnBurstAfter(ParticleSystem ps, float duration)
        {
            yield return new WaitForSeconds(duration);
            FeedbackRunner.Instance.ReturnBurstParticle(ps);
        }

        private static IEnumerator BlinkLightning(LineRenderer lr, float totalLifetime)
        {
            float baseWidth = lr.widthMultiplier;
            Color c0 = new Color(1f, 0.92f, 0.15f, 0.95f);
            Color c1 = new Color(1f, 1f, 1f, 0.95f);

            float onA = Mathf.Min(0.04f, totalLifetime * 0.4f);
            float off = Mathf.Min(0.02f, totalLifetime * 0.2f);
            float onB = Mathf.Min(0.04f, totalLifetime * 0.4f);

            lr.startColor = c0;
            lr.endColor = c1;
            lr.widthMultiplier = baseWidth * 1.75f;
            yield return null;
            lr.widthMultiplier = baseWidth;
            yield return new WaitForSeconds(onA);

            lr.startColor = new Color(c0.r, c0.g, c0.b, 0f);
            lr.endColor = new Color(c1.r, c1.g, c1.b, 0f);
            yield return new WaitForSeconds(off);

            lr.startColor = c0;
            lr.endColor = c1;
            yield return new WaitForSeconds(onB);

            if (lr != null)
            {
                Object.Destroy(lr.gameObject);
            }
        }

        private static IEnumerator HitStopRoutine(float duration)
        {
            FeedbackRunner.Instance.SetHitStopActive(true);
            float original = Time.timeScale;
            Time.timeScale = 0.05f;
            yield return new WaitForSecondsRealtime(duration);
            Time.timeScale = original;
            FeedbackRunner.Instance.SetHitStopActive(false);
        }

        private static PresetConfig GetPresetConfig(VfxPreset preset)
        {
            switch (preset)
            {
                case VfxPreset.Fire:
                    return new PresetConfig(
                        new Color(1f, 0.56f, 0.14f, 0.95f),
                        new Color(1f, 0.95f, 0.65f, 1f),
                        new Color(0.24f, 0.2f, 0.18f, 0.4f),
                        0.22f,
                        5.2f,
                        0.12f,
                        0.22f,
                        0.65f,
                        0.32f);

                case VfxPreset.Ice:
                    return new PresetConfig(
                        new Color(0.58f, 0.9f, 1f, 0.95f),
                        new Color(0.92f, 1f, 1f, 1f),
                        new Color(0.2f, 0.26f, 0.32f, 0.3f),
                        0.18f,
                        7f,
                        0.09f,
                        0.17f,
                        0.82f,
                        0.38f);

                case VfxPreset.Lightning:
                    return new PresetConfig(
                        new Color(1f, 0.92f, 0.22f, 0.95f),
                        new Color(1f, 1f, 1f, 1f),
                        new Color(0.32f, 0.3f, 0.2f, 0.2f),
                        0.16f,
                        6.5f,
                        0.095f,
                        0.28f,
                        0.8f,
                        0.45f);

                default:
                    return new PresetConfig(
                        new Color(0.84f, 0.76f, 1f, 0.95f),
                        new Color(1f, 1f, 1f, 1f),
                        new Color(0.25f, 0.2f, 0.3f, 0.25f),
                        0.2f,
                        4.6f,
                        0.1f,
                        0.2f,
                        0.72f,
                        0.28f);
            }
        }

        private static Color MainColor(VfxPreset preset, float intensity)
        {
            PresetConfig config = GetPresetConfig(preset);
            return config.mainColor * Mathf.Clamp(intensity, 0.5f, 1.8f);
        }

        private static ParticleSystem.MinMaxGradient BuildGradient(Color from, Color to)
        {
            Gradient gradient = new Gradient();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(from, 0f),
                    new GradientColorKey(to, 1f)
                },
                new[]
                {
                    new GradientAlphaKey(from.a, 0f),
                    new GradientAlphaKey(to.a, 1f)
                });

            return new ParticleSystem.MinMaxGradient(gradient);
        }

        private readonly struct PresetConfig
        {
            public readonly Color mainColor;
            public readonly Color highlightColor;
            public readonly Color smokeColor;
            public readonly float baseLifetime;
            public readonly float baseSpeed;
            public readonly float baseSize;
            public readonly float noiseStrength;
            public readonly float noiseFrequency;
            public readonly float radialVelocity;
            public readonly AnimationCurve sizeOverLifetime;

            public PresetConfig(
                Color main,
                Color highlight,
                Color smoke,
                float lifetime,
                float speed,
                float size,
                float noise,
                float noiseFreq,
                float radial)
            {
                mainColor = main;
                highlightColor = highlight;
                smokeColor = smoke;
                baseLifetime = lifetime;
                baseSpeed = speed;
                baseSize = size;
                noiseStrength = noise;
                noiseFrequency = noiseFreq;
                radialVelocity = radial;
                sizeOverLifetime = new AnimationCurve(
                    new Keyframe(0f, 1f),
                    new Keyframe(0.55f, 0.68f),
                    new Keyframe(1f, 0f));
            }
        }
    }
}
