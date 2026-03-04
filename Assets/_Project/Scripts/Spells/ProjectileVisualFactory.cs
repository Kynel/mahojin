using System.Collections.Generic;
using UnityEngine;

namespace DuckovProto.Spells
{
    public static class ProjectileVisualFactory
    {
        public static void CreateFireballVisual(GameObject projectile)
        {
            if (projectile == null)
            {
                return;
            }

            MeshRenderer core = projectile.GetComponent<MeshRenderer>();
            if (core == null)
            {
                core = projectile.AddComponent<MeshRenderer>();
                MeshFilter filter = projectile.GetComponent<MeshFilter>();
                if (filter == null)
                {
                    filter = projectile.AddComponent<MeshFilter>();
                    filter.sharedMesh = Resources.GetBuiltinResource<Mesh>("Sphere.fbx");
                }
            }

            projectile.transform.localScale = Vector3.one * 0.32f;
            core.material = new Material(Shader.Find("Sprites/Default"));
            core.material.color = new Color(1f, 0.86f, 0.46f, 0.95f);

            GameObject shellRoot = new GameObject("FireShell");
            shellRoot.transform.SetParent(projectile.transform, false);
            shellRoot.AddComponent<LocalJitterMotion>().Initialize(0.06f, 7f);

            for (int i = 0; i < 4; i++)
            {
                float a = (i / 4f) * Mathf.PI * 2f;
                Vector3 localPos = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * 0.08f;
                GameObject shell = CreatePrimitiveChild(shellRoot.transform, PrimitiveType.Sphere, $"Shell_{i}", localPos, Vector3.one * 0.17f, true);
                Renderer r = shell.GetComponent<Renderer>();
                if (r != null)
                {
                    r.material = new Material(Shader.Find("Sprites/Default"));
                    r.material.color = new Color(1f, 0.45f, 0.15f, 0.72f);
                }
            }
        }

        public static void CreateIceLanceVisual(GameObject projectile)
        {
            if (projectile == null)
            {
                return;
            }

            MeshRenderer rootRenderer = projectile.GetComponent<MeshRenderer>();
            if (rootRenderer != null)
            {
                rootRenderer.enabled = false;
            }

            MeshFilter rootFilter = projectile.GetComponent<MeshFilter>();
            if (rootFilter != null)
            {
                rootFilter.sharedMesh = null;
            }

            GameObject shaft = CreatePrimitiveChild(projectile.transform, PrimitiveType.Capsule, "IceShaft", Vector3.zero, new Vector3(0.12f, 0.22f, 0.12f), true);
            shaft.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            SetColor(shaft, new Color(0.62f, 0.9f, 1f, 0.95f));

            GameObject tip = CreatePrimitiveChild(projectile.transform, PrimitiveType.Sphere, "IceTip", new Vector3(0f, 0f, 0.22f), new Vector3(0.1f, 0.06f, 0.12f), true);
            SetColor(tip, new Color(0.88f, 0.98f, 1f, 1f));

            GameObject tail = CreatePrimitiveChild(projectile.transform, PrimitiveType.Cylinder, "IceTail", new Vector3(0f, 0f, -0.15f), new Vector3(0.05f, 0.08f, 0.05f), true);
            tail.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            SetColor(tail, new Color(0.5f, 0.8f, 1f, 0.75f));

            projectile.AddComponent<MicroRotationJitter>().Initialize(2.4f, 14f);
        }

        public static void CreateLightningOrbVisual(GameObject projectile)
        {
            if (projectile == null)
            {
                return;
            }

            projectile.transform.localScale = Vector3.one * 0.22f;
            SetColor(projectile, new Color(1f, 0.95f, 0.25f, 0.95f));

            LightningArcAnimator arcAnimator = projectile.AddComponent<LightningArcAnimator>();
            arcAnimator.Initialize(5, 0.23f, 0.05f, 0.02f);
        }

        private static GameObject CreatePrimitiveChild(
            Transform parent,
            PrimitiveType type,
            string name,
            Vector3 localPos,
            Vector3 localScale,
            bool removeCollider)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = localScale;
            if (removeCollider)
            {
                Collider col = go.GetComponent<Collider>();
                if (col != null)
                {
                    Object.Destroy(col);
                }
            }

            return go;
        }

        private static void SetColor(GameObject go, Color color)
        {
            Renderer renderer = go.GetComponent<Renderer>();
            if (renderer == null)
            {
                return;
            }

            renderer.material = new Material(Shader.Find("Sprites/Default"));
            renderer.material.color = color;
        }

        private sealed class LocalJitterMotion : MonoBehaviour
        {
            private float amp;
            private float freq;
            private Vector3 basePos;
            private float seedX;
            private float seedY;
            private bool initialized;

            public void Initialize(float amplitude, float frequency)
            {
                amp = Mathf.Max(0f, amplitude);
                freq = Mathf.Max(0f, frequency);
                basePos = transform.localPosition;
                seedX = Random.Range(0f, 100f);
                seedY = Random.Range(0f, 100f);
                initialized = true;
            }

            private void Update()
            {
                if (!initialized)
                {
                    return;
                }

                float t = Time.time * freq;
                float x = Mathf.Sin(t + seedX) * amp;
                float y = Mathf.Cos((t * 1.15f) + seedY) * amp * 0.4f;
                float z = Mathf.Sin((t * 0.8f) + seedY) * amp;
                transform.localPosition = basePos + new Vector3(x, y, z);
            }
        }

        private sealed class MicroRotationJitter : MonoBehaviour
        {
            private float angle;
            private float freq;
            private Quaternion baseRot;
            private float seed;
            private bool initialized;

            public void Initialize(float maxAngle, float frequency)
            {
                angle = Mathf.Max(0f, maxAngle);
                freq = Mathf.Max(0f, frequency);
                baseRot = transform.localRotation;
                seed = Random.Range(0f, 100f);
                initialized = true;
            }

            private void Update()
            {
                if (!initialized)
                {
                    return;
                }

                float t = Time.time * freq;
                float x = Mathf.Sin(t + seed) * angle;
                float y = Mathf.Cos((t * 1.13f) + seed) * angle;
                transform.localRotation = baseRot * Quaternion.Euler(x, y, 0f);
            }
        }

        private sealed class LightningArcAnimator : MonoBehaviour
        {
            private readonly List<LineRenderer> arcs = new List<LineRenderer>(8);
            private float radius;
            private float updateInterval;
            private float baseWidth;
            private float timer;
            private float burstTimer;

            public void Initialize(int arcCount, float arcRadius, float interval, float width)
            {
                radius = Mathf.Max(0.05f, arcRadius);
                updateInterval = Mathf.Clamp(interval, 0.02f, 0.1f);
                baseWidth = Mathf.Max(0.008f, width);

                int count = Mathf.Clamp(arcCount, 3, 6);
                for (int i = 0; i < count; i++)
                {
                    GameObject arc = new GameObject($"Arc_{i}");
                    arc.transform.SetParent(transform, false);
                    LineRenderer lr = arc.AddComponent<LineRenderer>();
                    lr.useWorldSpace = false;
                    lr.positionCount = 3;
                    lr.widthMultiplier = baseWidth;
                    lr.numCornerVertices = 1;
                    lr.numCapVertices = 1;
                    lr.material = new Material(Shader.Find("Sprites/Default"));
                    lr.startColor = new Color(1f, 0.9f, 0.22f, 0.95f);
                    lr.endColor = new Color(1f, 1f, 1f, 0.85f);
                    lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    lr.receiveShadows = false;
                    arcs.Add(lr);
                }

                RebuildArcs(true);
            }

            private void Update()
            {
                timer += Time.deltaTime;
                burstTimer += Time.deltaTime;
                if (timer >= updateInterval)
                {
                    timer = 0f;
                    RebuildArcs(false);
                }

                if (burstTimer >= 0.25f)
                {
                    burstTimer = 0f;
                    // small occasional electric pop
                    DuckovProto.Feedback.FeedbackUtils.SpawnParticleBurst(
                        DuckovProto.Feedback.VfxPreset.Lightning,
                        transform.position + Vector3.up * 0.03f,
                        6,
                        0.9f,
                        0.8f,
                        0.9f,
                        0.55f,
                        0.35f);
                }
            }

            private void RebuildArcs(bool initial)
            {
                for (int i = 0; i < arcs.Count; i++)
                {
                    LineRenderer lr = arcs[i];
                    if (lr == null)
                    {
                        continue;
                    }

                    Vector3 end = Random.insideUnitSphere * radius;
                    end.y *= 0.55f;
                    Vector3 mid = end * 0.5f + (Random.insideUnitSphere * radius * 0.2f);
                    mid.y *= 0.6f;

                    lr.SetPosition(0, Vector3.zero);
                    lr.SetPosition(1, mid);
                    lr.SetPosition(2, end);
                    lr.widthMultiplier = initial ? baseWidth : Random.Range(baseWidth * 0.75f, baseWidth * 1.3f);
                }
            }
        }
    }
}
