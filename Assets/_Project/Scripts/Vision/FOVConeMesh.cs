using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace DuckovProto.Vision
{
    [DisallowMultipleComponent]
    public sealed class FOVConeMesh : MonoBehaviour
    {
        private const string ConeObjectName = "VisionCone";
        private const int VisionRenderQueue = 3550;

        private struct VisionRaySample
        {
            public Vector3 Direction;
            public float VisibleDistance;
            public bool InFov;
            public bool OccludedByWall;
        }

        [Header("View")]
        [SerializeField] private float viewAngleDeg = 140f;
        [SerializeField] private float viewDistance = 28f;
        [SerializeField] private int rayCount = 256;
        [SerializeField] private float eyeHeight = 1f;

        [Header("Layers")]
        [SerializeField] private LayerMask wallMask = 1 << 10;
        [SerializeField] private LayerMask groundMask = 1 << 7;

        [Header("Dark Mask")]
        [SerializeField] private float maskDistance = 52f;
        [SerializeField] private float meshHeightOffset = 0.02f;
        [SerializeField] private float softEdge = 1.2f;
        [SerializeField] private float endFade = 3f;
        [SerializeField] private float obstacleProbeRadius = 0.24f;
        [SerializeField] private float wallInset = 0.04f;
        [SerializeField] private int occlusionDilateSteps = 1;
        [SerializeField] private float occlusionExtraInset = 0.08f;
        [SerializeField] private bool hardOcclusionDarkness = true;

        [Header("Adaptive Sampling")]
        [SerializeField] private bool enableAdaptiveRefine = true;
        [SerializeField] private int maxRefineDepth = 4;
        [SerializeField] private float refineDistanceThreshold = 0.55f;
        [SerializeField] private float minRefineAngleDeg = 0.2f;
        [SerializeField] private bool refineOnOcclusionEdge = true;
        [SerializeField] private int maxAdaptiveSamples = 1400;

        [Header("Render")]
        [SerializeField] private float overlayDarkAlpha = 0.62f;
        [SerializeField] private Material coneMaterial;

        [Header("Debug")]
        [SerializeField] private bool debugDisableVisionRenderer;
        [SerializeField] private bool debugDrawRays;

        private Mesh mesh;
        private MeshFilter meshFilter;
        private MeshRenderer meshRenderer;
        private Material runtimeMaterial;
        private Vector3[] vertices = System.Array.Empty<Vector3>();
        private Color[] colors = System.Array.Empty<Color>();
        private int[] triangles = System.Array.Empty<int>();
        private VisionRaySample[] raySamples = System.Array.Empty<VisionRaySample>();
        private float[] dilatedDistancesA = System.Array.Empty<float>();
        private float[] dilatedDistancesB = System.Array.Empty<float>();
        private readonly List<VisionRaySample> adaptiveSamples = new List<VisionRaySample>(1024);

        public float ViewAngleDeg => viewAngleDeg;
        public float ViewDistance => viewDistance;
        public float EyeHeight => eyeHeight;
        public LayerMask WallMask => wallMask;

        private void Awake()
        {
            EnsureMeshObjects();
            RebuildBuffers();
        }

        private void OnEnable()
        {
            EnsureMeshObjects();
            UpdateMaterialSettings();
        }

        private void LateUpdate()
        {
            EnsureMeshObjects();

            if (meshRenderer != null)
            {
                meshRenderer.enabled = !debugDisableVisionRenderer;
            }

            if (debugDisableVisionRenderer)
            {
                return;
            }

            UpdateMeshGeometry();
            UpdateMaterialSettings();
        }

        private void OnDisable()
        {
            if (meshRenderer != null)
            {
                meshRenderer.enabled = false;
            }
        }

        private void OnDestroy()
        {
            if (runtimeMaterial != null)
            {
                Destroy(runtimeMaterial);
                runtimeMaterial = null;
            }

            if (mesh != null)
            {
                Destroy(mesh);
                mesh = null;
            }
        }

        private void OnValidate()
        {
            viewAngleDeg = Mathf.Clamp(viewAngleDeg, 10f, 360f);
            viewDistance = Mathf.Max(1f, viewDistance);
            maxAdaptiveSamples = Mathf.Clamp(maxAdaptiveSamples, 128, 4096);
            rayCount = Mathf.Clamp(rayCount, 48, Mathf.Min(1024, maxAdaptiveSamples));
            eyeHeight = Mathf.Clamp(eyeHeight, 0f, 3f);
            maskDistance = Mathf.Max(viewDistance + 1f, maskDistance);
            meshHeightOffset = Mathf.Clamp(meshHeightOffset, 0f, 0.05f);
            softEdge = Mathf.Max(0f, softEdge);
            endFade = Mathf.Max(0f, endFade);
            obstacleProbeRadius = Mathf.Clamp(obstacleProbeRadius, 0f, 1f);
            wallInset = Mathf.Clamp(wallInset, 0f, 0.5f);
            occlusionDilateSteps = Mathf.Clamp(occlusionDilateSteps, 0, 8);
            occlusionExtraInset = Mathf.Clamp(occlusionExtraInset, 0f, 1f);
            maxRefineDepth = Mathf.Clamp(maxRefineDepth, 0, 6);
            refineDistanceThreshold = Mathf.Clamp(refineDistanceThreshold, 0.05f, 4f);
            minRefineAngleDeg = Mathf.Clamp(minRefineAngleDeg, 0.05f, 10f);
            overlayDarkAlpha = Mathf.Clamp01(overlayDarkAlpha);

#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                if (coneMaterial != null)
                {
                    ApplyMaterialSettings(coneMaterial);
                }

                if (runtimeMaterial != null)
                {
                    ApplyMaterialSettings(runtimeMaterial);
                }

                return;
            }
#endif

            if (!Application.isPlaying)
            {
                return;
            }

            RebuildBuffers();
            UpdateMaterialSettings();
        }

        private void EnsureMeshObjects()
        {
            Transform coneTransform = transform.Find(ConeObjectName);
            if (coneTransform == null)
            {
                GameObject coneObject = new GameObject(ConeObjectName);
                coneTransform = coneObject.transform;
                coneTransform.SetParent(transform, false);
            }

            coneTransform.localPosition = Vector3.zero;
            coneTransform.localRotation = Quaternion.identity;
            coneTransform.localScale = Vector3.one;

            if (meshFilter == null)
            {
                meshFilter = coneTransform.GetComponent<MeshFilter>();
                if (meshFilter == null)
                {
                    meshFilter = coneTransform.gameObject.AddComponent<MeshFilter>();
                }
            }

            if (meshRenderer == null)
            {
                meshRenderer = coneTransform.GetComponent<MeshRenderer>();
                if (meshRenderer == null)
                {
                    meshRenderer = coneTransform.gameObject.AddComponent<MeshRenderer>();
                }
            }

            if (mesh == null)
            {
                mesh = new Mesh { name = "FOVDarkMask_Runtime" };
                mesh.MarkDynamic();
            }

            meshFilter.sharedMesh = mesh;
            meshRenderer.sharedMaterial = ResolveMaterial();
            meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
            meshRenderer.receiveShadows = false;
            meshRenderer.lightProbeUsage = LightProbeUsage.Off;
            meshRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            meshRenderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
            meshRenderer.allowOcclusionWhenDynamic = false;
            meshRenderer.enabled = !debugDisableVisionRenderer;
        }

        private Material ResolveMaterial()
        {
            if (coneMaterial != null)
            {
                return coneMaterial;
            }

            if (runtimeMaterial != null)
            {
                return runtimeMaterial;
            }

            Shader shader = Shader.Find("DuckovProto/VisionOverlayMaskURP");
            if (shader == null)
            {
                shader = Shader.Find("DuckovProto/VisionConeURP");
            }

            if (shader == null)
            {
                shader = Shader.Find("Universal Render Pipeline/Unlit");
            }

            if (shader == null)
            {
                shader = Shader.Find("Unlit/Color");
            }

            runtimeMaterial = new Material(shader)
            {
                name = "VisionCone_RuntimeMaterial",
                renderQueue = VisionRenderQueue,
            };

            runtimeMaterial.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            runtimeMaterial.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
            runtimeMaterial.SetInt("_ZWrite", 0);
            if (runtimeMaterial.HasProperty("_ZTest"))
            {
                runtimeMaterial.SetInt("_ZTest", (int)CompareFunction.LessEqual);
            }

            if (runtimeMaterial.HasProperty("_Surface"))
            {
                runtimeMaterial.SetFloat("_Surface", 1f);
            }

            if (runtimeMaterial.HasProperty("_Blend"))
            {
                runtimeMaterial.SetFloat("_Blend", 0f);
            }

            runtimeMaterial.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            runtimeMaterial.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            return runtimeMaterial;
        }

        private void RebuildBuffers()
        {
            EnsureBufferSize(Mathf.Max(48, rayCount));
            adaptiveSamples.Clear();
        }

        private void EnsureBufferSize(int sampleCount)
        {
            sampleCount = Mathf.Max(3, sampleCount);
            int vertexCount = sampleCount * 2;
            if (vertices.Length != vertexCount)
            {
                vertices = new Vector3[vertexCount];
                colors = new Color[vertexCount];
            }

            if (triangles.Length != sampleCount * 6)
            {
                triangles = new int[sampleCount * 6];
                for (int i = 0; i < sampleCount; i++)
                {
                    int next = (i + 1) % sampleCount;
                    int innerA = i * 2;
                    int outerA = innerA + 1;
                    int innerB = next * 2;
                    int outerB = innerB + 1;

                    int tri = i * 6;
                    triangles[tri] = innerA;
                    triangles[tri + 1] = outerA;
                    triangles[tri + 2] = innerB;
                    triangles[tri + 3] = outerA;
                    triangles[tri + 4] = outerB;
                    triangles[tri + 5] = innerB;
                }
            }

            if (raySamples.Length != sampleCount)
            {
                raySamples = new VisionRaySample[sampleCount];
                dilatedDistancesA = new float[sampleCount];
                dilatedDistancesB = new float[sampleCount];
            }
        }

        private int BuildSampleSet(Vector3 eyeOrigin, Vector3 forward, float halfAngle)
        {
            adaptiveSamples.Clear();

            int baseCount = Mathf.Clamp(rayCount, 48, Mathf.Max(48, maxAdaptiveSamples));
            float step = 360f / baseCount;

            float firstAngle = -180f;
            VisionRaySample first = SampleRayAtAngle(firstAngle, eyeOrigin, forward, halfAngle);
            float prevAngle = firstAngle;
            VisionRaySample prev = first;

            for (int i = 1; i < baseCount; i++)
            {
                float currentAngle = -180f + (step * i);
                VisionRaySample current = SampleRayAtAngle(currentAngle, eyeOrigin, forward, halfAngle);
                AppendRefinedSegment(eyeOrigin, forward, halfAngle, prevAngle, currentAngle, prev, current, 0);
                prevAngle = currentAngle;
                prev = current;
            }

            AppendRefinedSegment(eyeOrigin, forward, halfAngle, prevAngle, firstAngle + 360f, prev, first, 0);
            return adaptiveSamples.Count;
        }

        private void AppendRefinedSegment(
            Vector3 eyeOrigin,
            Vector3 forward,
            float halfAngle,
            float angleA,
            float angleB,
            VisionRaySample sampleA,
            VisionRaySample sampleB,
            int depth)
        {
            if (adaptiveSamples.Count >= maxAdaptiveSamples)
            {
                return;
            }

            float span = PositiveAngleDelta(angleA, angleB);
            bool canRefine = enableAdaptiveRefine
                             && depth < maxRefineDepth
                             && span > minRefineAngleDeg
                             && adaptiveSamples.Count < maxAdaptiveSamples - 2
                             && ShouldRefineSegment(sampleA, sampleB);

            if (canRefine)
            {
                float midAngle = NormalizeAngle(angleA + (span * 0.5f));
                VisionRaySample mid = SampleRayAtAngle(midAngle, eyeOrigin, forward, halfAngle);
                AppendRefinedSegment(eyeOrigin, forward, halfAngle, angleA, midAngle, sampleA, mid, depth + 1);
                AppendRefinedSegment(eyeOrigin, forward, halfAngle, midAngle, angleB, mid, sampleB, depth + 1);
                return;
            }

            adaptiveSamples.Add(sampleA);
        }

        private bool ShouldRefineSegment(VisionRaySample a, VisionRaySample b)
        {
            if (a.InFov != b.InFov)
            {
                return true;
            }

            if (refineOnOcclusionEdge && a.OccludedByWall != b.OccludedByWall)
            {
                return true;
            }

            if (a.InFov && b.InFov)
            {
                float threshold = refineDistanceThreshold;
                if (a.OccludedByWall || b.OccludedByWall)
                {
                    threshold *= 0.65f;
                }

                return Mathf.Abs(a.VisibleDistance - b.VisibleDistance) > threshold;
            }

            return false;
        }

        private static float PositiveAngleDelta(float angleA, float angleB)
        {
            float delta = angleB - angleA;
            if (delta <= 0f)
            {
                delta += 360f;
            }

            return delta;
        }

        private static float NormalizeAngle(float angle)
        {
            while (angle > 180f)
            {
                angle -= 360f;
            }

            while (angle <= -180f)
            {
                angle += 360f;
            }

            return angle;
        }

        private void UpdateMeshGeometry()
        {
            Vector3 eyeOrigin = transform.position + Vector3.up * eyeHeight;
            Vector3 forward = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
            if (forward.sqrMagnitude < 0.0001f)
            {
                forward = Vector3.forward;
            }

            forward.Normalize();
            float halfAngle = Mathf.Max(0.1f, viewAngleDeg * 0.5f);
            float outerDistance = Mathf.Max(maskDistance, viewDistance + 1f);
            const float darknessAlpha = 1f;
            Vector3 centerGround = ResolveGroundPoint(transform.position);
            centerGround.y += meshHeightOffset;

            int sampleCount = BuildSampleSet(eyeOrigin, forward, halfAngle);
            if (sampleCount < 3)
            {
                return;
            }

            EnsureBufferSize(sampleCount);
            for (int i = 0; i < sampleCount; i++)
            {
                raySamples[i] = adaptiveSamples[i];
            }

            ApplyOcclusionDilation(sampleCount);

            for (int i = 0; i < sampleCount; i++)
            {
                VisionRaySample sample = raySamples[i];
                int innerIndex = i * 2;
                int outerIndex = innerIndex + 1;

                Vector3 innerGround = centerGround;
                if (sample.InFov && sample.VisibleDistance > 0.01f)
                {
                    innerGround = ResolveGroundPoint(transform.position + sample.Direction * sample.VisibleDistance);
                    innerGround.y += meshHeightOffset;
                }

                Vector3 outerGround = ResolveGroundPoint(transform.position + sample.Direction * outerDistance);
                outerGround.y += meshHeightOffset;

                float innerAlpha = sample.InFov ? 0f : darknessAlpha;
                if (sample.InFov && softEdge > 0f)
                {
                    float edgeBlend = ComputeFovEdgeBlend(sample.Direction, forward, halfAngle);
                    innerAlpha = (1f - edgeBlend) * darknessAlpha;
                }
                if (hardOcclusionDarkness && sample.OccludedByWall)
                {
                    innerAlpha = darknessAlpha;
                }
                if (sample.InFov && endFade > 0f)
                {
                    float fadeStart = Mathf.Max(0f, viewDistance - endFade);
                    float rangeBlend = Mathf.InverseLerp(fadeStart, viewDistance, sample.VisibleDistance);
                    innerAlpha = Mathf.Max(innerAlpha, rangeBlend * darknessAlpha * 0.25f);
                }

                float outerAlpha = darknessAlpha;

                vertices[innerIndex] = transform.InverseTransformPoint(innerGround);
                vertices[outerIndex] = transform.InverseTransformPoint(outerGround);
                colors[innerIndex] = new Color(1f, 1f, 1f, innerAlpha);
                colors[outerIndex] = new Color(1f, 1f, 1f, outerAlpha);
            }

            mesh.Clear();
            mesh.vertices = vertices;
            mesh.colors = colors;
            mesh.triangles = triangles;
            mesh.RecalculateBounds();
        }

        private void ApplyOcclusionDilation(int sampleCount)
        {
            if (sampleCount <= 0 || raySamples.Length < sampleCount)
            {
                return;
            }

            for (int i = 0; i < sampleCount; i++)
            {
                dilatedDistancesA[i] = raySamples[i].VisibleDistance;
            }

            int steps = Mathf.Max(0, occlusionDilateSteps);
            for (int step = 0; step < steps; step++)
            {
                for (int i = 0; i < sampleCount; i++)
                {
                    if (!raySamples[i].InFov)
                    {
                        dilatedDistancesB[i] = 0f;
                        continue;
                    }

                    float minDistance = dilatedDistancesA[i];
                    int left = i - 1;
                    if (left < 0)
                    {
                        left = sampleCount - 1;
                    }

                    int right = i + 1;
                    if (right >= sampleCount)
                    {
                        right = 0;
                    }

                    if (raySamples[left].InFov)
                    {
                        minDistance = Mathf.Min(minDistance, dilatedDistancesA[left]);
                    }

                    if (raySamples[right].InFov)
                    {
                        minDistance = Mathf.Min(minDistance, dilatedDistancesA[right]);
                    }

                    dilatedDistancesB[i] = minDistance;
                }

                float[] swap = dilatedDistancesA;
                dilatedDistancesA = dilatedDistancesB;
                dilatedDistancesB = swap;
            }

            for (int i = 0; i < sampleCount; i++)
            {
                if (!raySamples[i].InFov)
                {
                    continue;
                }

                float refined = dilatedDistancesA[i];
                if (refined < viewDistance - 0.001f)
                {
                    refined = Mathf.Max(0f, refined - occlusionExtraInset);
                }

                raySamples[i] = new VisionRaySample
                {
                    Direction = raySamples[i].Direction,
                    VisibleDistance = Mathf.Clamp(refined, 0f, viewDistance),
                    InFov = true,
                    OccludedByWall = raySamples[i].OccludedByWall || refined < viewDistance - 0.001f,
                };
            }
        }

        private VisionRaySample SampleRayAtAngle(float angleFromForward, Vector3 eyeOrigin, Vector3 forward, float halfAngle)
        {
            Vector3 dir = Quaternion.AngleAxis(angleFromForward, Vector3.up) * forward;
            dir.y = 0f;
            dir.Normalize();

            bool inFov = Mathf.Abs(NormalizeAngle(angleFromForward)) <= halfAngle;
            float visibleDistance = 0f;
            bool occludedByWall = false;

            if (inFov)
            {
                visibleDistance = viewDistance;
                if (obstacleProbeRadius > 0.001f)
                {
                    if (Physics.SphereCast(
                            eyeOrigin,
                            obstacleProbeRadius,
                            dir,
                            out RaycastHit hit,
                            viewDistance,
                            wallMask,
                        QueryTriggerInteraction.Ignore))
                    {
                        visibleDistance = Mathf.Max(0f, hit.distance - wallInset);
                        occludedByWall = true;
                    }
                }
                else if (Physics.Raycast(eyeOrigin, dir, out RaycastHit hit, viewDistance, wallMask, QueryTriggerInteraction.Ignore))
                {
                    visibleDistance = Mathf.Max(0f, hit.distance - wallInset);
                    occludedByWall = true;
                }
            }

            return new VisionRaySample
            {
                Direction = dir,
                VisibleDistance = Mathf.Clamp(visibleDistance, 0f, viewDistance),
                InFov = inFov,
                OccludedByWall = occludedByWall,
            };
        }

        private float ComputeFovEdgeBlend(Vector3 dir, Vector3 forward, float halfAngle)
        {
            float angle = Vector3.Angle(forward, dir);
            float edgeBand = Mathf.Rad2Deg * (softEdge / Mathf.Max(viewDistance, 0.001f));
            edgeBand = Mathf.Clamp(edgeBand, 0.5f, halfAngle);
            float innerSafe = Mathf.Max(0f, halfAngle - edgeBand);
            return 1f - Mathf.InverseLerp(innerSafe, halfAngle, angle);
        }

        private Vector3 ResolveGroundPoint(Vector3 at)
        {
            Vector3 rayOrigin = at + Vector3.up * 60f;
            if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, 160f, groundMask, QueryTriggerInteraction.Ignore))
            {
                return hit.point;
            }

            return new Vector3(at.x, transform.position.y, at.z);
        }

        private void UpdateMaterialSettings()
        {
            if (meshRenderer == null)
            {
                return;
            }

            Material material = meshRenderer.sharedMaterial;
            if (material == null)
            {
                return;
            }

            ApplyMaterialSettings(material);
        }

        private void ApplyMaterialSettings(Material material)
        {
            if (material == null)
            {
                return;
            }

            material.renderQueue = VisionRenderQueue;
            Color overlayColor = new Color(0f, 0f, 0f, Mathf.Clamp01(overlayDarkAlpha));
            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", overlayColor);
            }

            if (material.HasProperty("_Color"))
            {
                material.SetColor("_Color", overlayColor);
            }

            if (material.HasProperty("_ZWrite"))
            {
                material.SetInt("_ZWrite", 0);
            }

            if (material.HasProperty("_ZTest"))
            {
                material.SetInt("_ZTest", (int)CompareFunction.LessEqual);
            }
        }

        private void OnDrawGizmosSelected()
        {
            if (!debugDrawRays)
            {
                return;
            }

            Gizmos.color = Color.yellow;
            Vector3 eyeOrigin = transform.position + Vector3.up * eyeHeight;
            Vector3 forward = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
            if (forward.sqrMagnitude < 0.0001f)
            {
                forward = Vector3.forward;
            }

            forward.Normalize();
            float halfAngle = viewAngleDeg * 0.5f;
            int debugRays = 40;
            for (int i = 0; i < debugRays; i++)
            {
                float t = i / (float)debugRays;
                float angle = Mathf.Lerp(-180f, 180f, t);
                Vector3 dir = Quaternion.AngleAxis(angle, Vector3.up) * forward;
                dir.y = 0f;
                dir.Normalize();

                bool inFov = Mathf.Abs(angle) <= halfAngle;
                float len = inFov ? viewDistance : maskDistance;
                Gizmos.DrawLine(eyeOrigin, eyeOrigin + dir * len);
            }
        }
    }
}
