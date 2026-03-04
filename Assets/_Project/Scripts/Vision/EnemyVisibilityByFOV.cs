using System.Collections.Generic;
using DuckovProto.Combat;
using UnityEngine;

namespace DuckovProto.Vision
{
    [DisallowMultipleComponent]
    public sealed class EnemyVisibilityByFOV : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        private sealed class EnemyEntry
        {
            public Transform Root;
            public Renderer[] Renderers;
        }

        private sealed class WallEntry
        {
            public Transform Root;
            public Renderer Renderer;
            public int ColorPropertyId;
            public Color BaseColor;
            public bool IsDimmed;
        }

        [SerializeField] private FOVConeMesh fovConeMesh;

        [Header("FOV")]
        [SerializeField] private float viewAngleDeg = 140f;
        [SerializeField] private float viewDistance = 28f;
        [SerializeField] private float fovAnglePadding = 1.25f;
        [SerializeField] private float distancePadding = 0.22f;

        [Header("Masks")]
        [SerializeField] private LayerMask enemyMask = 1 << 8;
        [SerializeField] private LayerMask wallMask = 1 << 10;

        [Header("Sampling")]
        [SerializeField] private float eyeHeight = 1f;
        [SerializeField] private float enemyEyeHeight = 1f;
        [SerializeField] private float losProbeRadius = 0.14f;

        [Header("Update")]
        [SerializeField] private float updateInterval = 0.075f;
        [SerializeField] private float cacheRefreshInterval = 0.35f;
        [SerializeField] private float visibilityGraceTime = 0.06f;

        [Header("Wall Dimming")]
        [SerializeField] private bool dimWallsOutsideFov;
        [SerializeField] private float hiddenWallBrightness = 0.38f;
        [SerializeField] private float wallSampleInset = 0.25f;
        [SerializeField] private float wallCacheRefreshInterval = 0.75f;

        private readonly List<EnemyEntry> trackedEnemies = new List<EnemyEntry>(32);
        private readonly List<WallEntry> trackedWalls = new List<WallEntry>(128);
        private readonly Collider[] overlapBuffer = new Collider[128];
        private readonly HashSet<Transform> candidateRoots = new HashSet<Transform>();
        private readonly HashSet<Transform> visibleRoots = new HashSet<Transform>();
        private readonly Dictionary<Transform, float> visibleGraceUntil = new Dictionary<Transform, float>();
        private readonly HashSet<Transform> aliveRoots = new HashSet<Transform>();
        private readonly List<Transform> staleRoots = new List<Transform>();
        private readonly Vector3[] wallSamplePoints = new Vector3[6];
        private MaterialPropertyBlock wallPropertyBlock;

        private float nextUpdateTime;
        private float nextCacheRefreshTime;
        private float nextWallCacheRefreshTime;
        private bool wallDimmingApplied;

        private void Awake()
        {
            if (fovConeMesh == null)
            {
                fovConeMesh = GetComponent<FOVConeMesh>();
            }

            EnsureWallPropertyBlock();
        }

        private void OnEnable()
        {
            nextCacheRefreshTime = 0f;
            nextWallCacheRefreshTime = 0f;
            nextUpdateTime = 0f;
            RebuildEnemyCache();
            EvaluateVisibility();
            if (dimWallsOutsideFov)
            {
                RebuildWallCache();
                EvaluateWallDimming();
            }
        }

        private void OnDisable()
        {
            SetAllRenderersVisible(true);
            RestoreAllWallBrightness();
            visibleGraceUntil.Clear();
        }

        private void OnValidate()
        {
            viewAngleDeg = Mathf.Clamp(viewAngleDeg, 5f, 360f);
            viewDistance = Mathf.Max(1f, viewDistance);
            fovAnglePadding = Mathf.Clamp(fovAnglePadding, 0f, 15f);
            distancePadding = Mathf.Clamp(distancePadding, 0f, 2f);
            eyeHeight = Mathf.Clamp(eyeHeight, 0f, 3f);
            enemyEyeHeight = Mathf.Clamp(enemyEyeHeight, 0f, 3f);
            losProbeRadius = Mathf.Clamp(losProbeRadius, 0f, 0.5f);
            updateInterval = Mathf.Clamp(updateInterval, 0.02f, 0.5f);
            cacheRefreshInterval = Mathf.Clamp(cacheRefreshInterval, 0.1f, 2f);
            visibilityGraceTime = Mathf.Clamp(visibilityGraceTime, 0f, 0.5f);
            hiddenWallBrightness = Mathf.Clamp(hiddenWallBrightness, 0.05f, 1f);
            wallSampleInset = Mathf.Clamp(wallSampleInset, 0f, 2f);
            wallCacheRefreshInterval = Mathf.Clamp(wallCacheRefreshInterval, 0.1f, 3f);
        }

        private void Update()
        {
            SyncFromFovConeIfAssigned();

            if (Time.time >= nextCacheRefreshTime)
            {
                RebuildEnemyCache();
                nextCacheRefreshTime = Time.time + cacheRefreshInterval;
            }

            if (dimWallsOutsideFov && Time.time >= nextWallCacheRefreshTime)
            {
                RebuildWallCache();
                nextWallCacheRefreshTime = Time.time + wallCacheRefreshInterval;
            }

            if (Time.time < nextUpdateTime)
            {
                return;
            }

            nextUpdateTime = Time.time + updateInterval;
            EvaluateVisibility();
            if (dimWallsOutsideFov)
            {
                EvaluateWallDimming();
            }
            else if (wallDimmingApplied)
            {
                RestoreAllWallBrightness();
            }
        }

        private void SyncFromFovConeIfAssigned()
        {
            if (fovConeMesh == null)
            {
                return;
            }

            viewAngleDeg = fovConeMesh.ViewAngleDeg;
            viewDistance = fovConeMesh.ViewDistance;
            eyeHeight = fovConeMesh.EyeHeight;
            wallMask = fovConeMesh.WallMask;
        }

        private void RebuildEnemyCache()
        {
            trackedEnemies.Clear();
            aliveRoots.Clear();

            Health[] healthComponents = FindObjectsByType<Health>(FindObjectsSortMode.None);
            for (int i = 0; i < healthComponents.Length; i++)
            {
                Health health = healthComponents[i];
                if (health == null)
                {
                    continue;
                }

                Transform enemyRoot = health.transform;
                if (!LayerInMask(enemyRoot.gameObject.layer, enemyMask))
                {
                    continue;
                }

                aliveRoots.Add(enemyRoot);
                trackedEnemies.Add(new EnemyEntry
                {
                    Root = enemyRoot,
                    Renderers = enemyRoot.GetComponentsInChildren<Renderer>(true),
                });
            }

            if (visibleGraceUntil.Count == 0)
            {
                return;
            }

            staleRoots.Clear();
            foreach (Transform root in visibleGraceUntil.Keys)
            {
                if (!aliveRoots.Contains(root))
                {
                    staleRoots.Add(root);
                }
            }

            for (int i = 0; i < staleRoots.Count; i++)
            {
                visibleGraceUntil.Remove(staleRoots[i]);
            }
        }

        private void RebuildWallCache()
        {
            RestoreAllWallBrightness();
            trackedWalls.Clear();

            Renderer[] renderers = FindObjectsByType<Renderer>(FindObjectsSortMode.None);
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null || !renderer.gameObject.activeInHierarchy)
                {
                    continue;
                }

                Transform wallRoot = FindWallRoot(renderer.transform);
                if (wallRoot == null)
                {
                    continue;
                }

                Material sharedMaterial = renderer.sharedMaterial;
                if (sharedMaterial == null)
                {
                    continue;
                }

                if (TryReadBaseColor(sharedMaterial, out int colorPropertyId, out Color baseColor))
                {
                    trackedWalls.Add(new WallEntry
                    {
                        Root = wallRoot,
                        Renderer = renderer,
                        ColorPropertyId = colorPropertyId,
                        BaseColor = baseColor,
                        IsDimmed = false,
                    });
                }
            }
        }

        private void EvaluateVisibility()
        {
            if (trackedEnemies.Count == 0)
            {
                return;
            }

            Vector3 eye = transform.position + Vector3.up * eyeHeight;
            Vector3 forward = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
            if (forward.sqrMagnitude < 0.0001f)
            {
                forward = Vector3.forward;
            }

            forward.Normalize();
            float halfAngle = viewAngleDeg * 0.5f;

            candidateRoots.Clear();
            visibleRoots.Clear();

            int hits = Physics.OverlapSphereNonAlloc(
                eye,
                viewDistance + distancePadding,
                overlapBuffer,
                enemyMask,
                QueryTriggerInteraction.Collide);

            for (int i = 0; i < hits; i++)
            {
                Collider overlap = overlapBuffer[i];
                if (overlap == null)
                {
                    continue;
                }

                Transform enemyRoot = FindEnemyRoot(overlap.transform);
                if (enemyRoot != null)
                {
                    candidateRoots.Add(enemyRoot);
                }

                overlapBuffer[i] = null;
            }

            float now = Time.time;
            foreach (Transform root in candidateRoots)
            {
                if (root == null)
                {
                    continue;
                }

                if (IsVisible(root, eye, forward, halfAngle))
                {
                    visibleRoots.Add(root);
                    visibleGraceUntil[root] = now + visibilityGraceTime;
                }
            }

            for (int i = trackedEnemies.Count - 1; i >= 0; i--)
            {
                EnemyEntry entry = trackedEnemies[i];
                if (entry == null || entry.Root == null)
                {
                    trackedEnemies.RemoveAt(i);
                    continue;
                }

                bool visible = visibleRoots.Contains(entry.Root);
                if (!visible && visibleGraceUntil.TryGetValue(entry.Root, out float until) && until > now)
                {
                    visible = true;
                }

                if (!visible && visibleGraceUntil.ContainsKey(entry.Root))
                {
                    visibleGraceUntil.Remove(entry.Root);
                }

                SetRenderersVisible(entry.Renderers, visible);
            }
        }

        private bool IsVisible(Transform enemyRoot, Vector3 eye, Vector3 forward, float halfAngle)
        {
            Vector3 enemyEye = enemyRoot.position + Vector3.up * enemyEyeHeight;
            Vector3 toEnemy = enemyEye - eye;
            float distance = toEnemy.magnitude;
            if (distance > viewDistance + distancePadding || distance <= 0.001f)
            {
                return false;
            }

            Vector3 toEnemyFlat = Vector3.ProjectOnPlane(toEnemy, Vector3.up);
            if (toEnemyFlat.sqrMagnitude > 0.00001f)
            {
                float angle = Vector3.Angle(forward, toEnemyFlat.normalized);
                if (angle > halfAngle + fovAnglePadding)
                {
                    return false;
                }
            }

            return HasAnyLineOfSight(eye, enemyRoot);
        }

        private void EvaluateWallDimming()
        {
            if (trackedWalls.Count == 0)
            {
                return;
            }

            Vector3 eye = transform.position + Vector3.up * eyeHeight;
            Vector3 forward = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
            if (forward.sqrMagnitude < 0.0001f)
            {
                forward = Vector3.forward;
            }

            forward.Normalize();
            float halfAngle = viewAngleDeg * 0.5f;

            for (int i = trackedWalls.Count - 1; i >= 0; i--)
            {
                WallEntry entry = trackedWalls[i];
                if (entry == null || entry.Root == null || entry.Renderer == null)
                {
                    trackedWalls.RemoveAt(i);
                    continue;
                }

                bool visible = IsWallVisible(entry, eye, forward, halfAngle);
                SetWallDimmed(entry, !visible);
            }
        }

        private bool HasAnyLineOfSight(Vector3 eye, Transform enemyRoot)
        {
            float torsoHeight = Mathf.Max(0.2f, enemyEyeHeight * 0.55f);
            float topHeight = enemyEyeHeight + 0.35f;

            if (HasLineOfSightToPoint(eye, enemyRoot.position + Vector3.up * enemyEyeHeight))
            {
                return true;
            }

            if (HasLineOfSightToPoint(eye, enemyRoot.position + Vector3.up * torsoHeight))
            {
                return true;
            }

            if (HasLineOfSightToPoint(eye, enemyRoot.position + Vector3.up * topHeight))
            {
                return true;
            }

            return false;
        }

        private bool IsWallVisible(WallEntry entry, Vector3 eye, Vector3 forward, float halfAngle)
        {
            Bounds bounds = entry.Renderer.bounds;
            int sampleCount = BuildWallSamplePoints(bounds);
            for (int i = 0; i < sampleCount; i++)
            {
                if (IsWallSampleVisible(entry.Root, eye, forward, halfAngle, wallSamplePoints[i]))
                {
                    return true;
                }
            }

            return false;
        }

        private int BuildWallSamplePoints(Bounds bounds)
        {
            Vector3 center = bounds.center;
            Vector3 extents = bounds.extents;
            float inset = Mathf.Max(0f, wallSampleInset);

            float x = Mathf.Max(0.05f, extents.x - inset);
            float z = Mathf.Max(0.05f, extents.z - inset);
            float y = Mathf.Clamp(extents.y * 0.75f, 0.2f, 1.8f);

            wallSamplePoints[0] = center;
            wallSamplePoints[1] = center + Vector3.up * y;
            wallSamplePoints[2] = center + Vector3.right * x;
            wallSamplePoints[3] = center - Vector3.right * x;
            wallSamplePoints[4] = center + Vector3.forward * z;
            wallSamplePoints[5] = center - Vector3.forward * z;
            return wallSamplePoints.Length;
        }

        private bool IsWallSampleVisible(Transform wallRoot, Vector3 eye, Vector3 forward, float halfAngle, Vector3 samplePoint)
        {
            Vector3 toSample = samplePoint - eye;
            float distance = toSample.magnitude;
            if (distance <= 0.001f)
            {
                return true;
            }

            if (distance > viewDistance + distancePadding)
            {
                return false;
            }

            Vector3 toSampleFlat = Vector3.ProjectOnPlane(toSample, Vector3.up);
            if (toSampleFlat.sqrMagnitude > 0.00001f)
            {
                float angle = Vector3.Angle(forward, toSampleFlat.normalized);
                if (angle > halfAngle + fovAnglePadding)
                {
                    return false;
                }
            }

            Vector3 dir = toSample / distance;
            if (!Physics.Raycast(eye, dir, out RaycastHit hit, distance + 0.05f, wallMask, QueryTriggerInteraction.Ignore))
            {
                return true;
            }

            Transform hitTransform = hit.collider != null ? hit.collider.transform : hit.transform;
            if (hitTransform == null)
            {
                return true;
            }

            return hitTransform == wallRoot || hitTransform.IsChildOf(wallRoot) || wallRoot.IsChildOf(hitTransform);
        }

        private bool HasLineOfSightToPoint(Vector3 from, Vector3 targetPoint)
        {
            Vector3 toTarget = targetPoint - from;
            float dist = toTarget.magnitude;
            if (dist <= 0.001f)
            {
                return true;
            }

            Vector3 dir = toTarget / dist;
            bool blocked;
            if (losProbeRadius > 0.001f)
            {
                blocked = Physics.SphereCast(
                    from,
                    losProbeRadius,
                    dir,
                    out _,
                    dist,
                    wallMask,
                    QueryTriggerInteraction.Ignore);
            }
            else
            {
                blocked = Physics.Raycast(from, dir, dist, wallMask, QueryTriggerInteraction.Ignore);
            }

            return !blocked;
        }

        private Transform FindEnemyRoot(Transform source)
        {
            Transform current = source;
            while (current != null)
            {
                if (LayerInMask(current.gameObject.layer, enemyMask))
                {
                    return current;
                }

                current = current.parent;
            }

            return null;
        }

        private Transform FindWallRoot(Transform source)
        {
            Transform current = source;
            while (current != null)
            {
                if (LayerInMask(current.gameObject.layer, wallMask))
                {
                    return current;
                }

                current = current.parent;
            }

            return null;
        }

        private static bool TryReadBaseColor(Material material, out int propertyId, out Color color)
        {
            if (material.HasProperty(BaseColorId))
            {
                propertyId = BaseColorId;
                color = material.GetColor(BaseColorId);
                return true;
            }

            if (material.HasProperty(ColorId))
            {
                propertyId = ColorId;
                color = material.GetColor(ColorId);
                return true;
            }

            propertyId = 0;
            color = Color.white;
            return false;
        }

        private static bool LayerInMask(int layer, LayerMask mask)
        {
            return (mask.value & (1 << layer)) != 0;
        }

        private void SetAllRenderersVisible(bool visible)
        {
            for (int i = 0; i < trackedEnemies.Count; i++)
            {
                SetRenderersVisible(trackedEnemies[i].Renderers, visible);
            }
        }

        private void SetWallDimmed(WallEntry entry, bool dimmed)
        {
            if (entry.IsDimmed == dimmed)
            {
                return;
            }

            Renderer renderer = entry.Renderer;
            if (renderer == null)
            {
                entry.IsDimmed = false;
                return;
            }

            EnsureWallPropertyBlock();
            wallPropertyBlock.Clear();
            if (dimmed)
            {
                float brightness = Mathf.Clamp01(hiddenWallBrightness);
                Color dimColor = entry.BaseColor;
                dimColor.r *= brightness;
                dimColor.g *= brightness;
                dimColor.b *= brightness;
                wallPropertyBlock.SetColor(entry.ColorPropertyId, dimColor);
                wallDimmingApplied = true;
            }

            renderer.SetPropertyBlock(wallPropertyBlock);
            entry.IsDimmed = dimmed;
        }

        private void RestoreAllWallBrightness()
        {
            if (wallPropertyBlock == null)
            {
                wallDimmingApplied = false;
                return;
            }

            for (int i = 0; i < trackedWalls.Count; i++)
            {
                WallEntry entry = trackedWalls[i];
                if (entry == null || entry.Renderer == null || !entry.IsDimmed)
                {
                    continue;
                }

                wallPropertyBlock.Clear();
                entry.Renderer.SetPropertyBlock(wallPropertyBlock);
                entry.IsDimmed = false;
            }

            wallDimmingApplied = false;
        }

        private void EnsureWallPropertyBlock()
        {
            if (wallPropertyBlock == null)
            {
                wallPropertyBlock = new MaterialPropertyBlock();
            }
        }

        private static void SetRenderersVisible(Renderer[] renderers, bool visible)
        {
            if (renderers == null)
            {
                return;
            }

            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer != null)
                {
                    renderer.enabled = visible;
                }
            }
        }
    }
}
