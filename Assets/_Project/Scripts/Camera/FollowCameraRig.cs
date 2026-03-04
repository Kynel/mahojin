using UnityEngine;
using UnityEngine.SceneManagement;

namespace DuckovProto.CameraSystem
{
    [DefaultExecutionOrder(1200)]
    [DisallowMultipleComponent]
    public sealed class FollowCameraRig : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Transform target;
        [SerializeField] private Transform pivot;
        [SerializeField] private Camera targetCamera;
        [SerializeField] private string targetTag = "Player";

        [Header("Follow")]
        [SerializeField] private float followSmoothTime = 0.15f;
        [SerializeField] private float rigHeightOffset = 0.35f;
        [SerializeField] private float rigHeightSmoothTime = 0.16f;

        [Header("Ground")]
        [SerializeField] private LayerMask groundMask = 1 << 7; // Ground
        [SerializeField] private float groundProbeHeight = 24f;
        [SerializeField] private float groundProbeDistance = 48f;

        [Header("View")]
        [SerializeField] private float pivotPitch = 60f;
        [SerializeField] private float pivotYaw = 0f;
        [SerializeField] private float cameraDistance = 20f;
        [SerializeField] private float minDistance = 7f;
        [SerializeField] private float maxDistance = 26f;

        [Header("Obstacle Avoidance")]
        [SerializeField] private LayerMask obstacleMask = 1 << 10; // Wall
        [SerializeField] private float collisionProbeRadius = 0.4f;
        [SerializeField] private float collisionSafetyMargin = 0.25f;
        [SerializeField] private float avoidSmoothTime = 0.1f;

        [Header("Look Ahead")]
        [SerializeField] private bool useLookAhead;
        [SerializeField] private float lookAheadDistance = 2f;
        [SerializeField] private float lookAheadSmooth = 0.2f;
        [SerializeField] private float lookAheadVelocityThreshold = 0.15f;

        [Header("Debug")]
        [SerializeField] private bool drawDebug;

        private Rigidbody targetRigidbody;
        private Vector3 positionVelocity;
        private float yVelocity;
        private float distanceVelocity;
        private float currentDistance;
        private Vector3 currentLookAhead;
        private Vector3 lookAheadVelocity;
        private Vector3 lastTargetPosition;
        private float refindTime;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            Scene active = SceneManager.GetActiveScene();
            if (active.name != "Prototype_Arena")
            {
                return;
            }

            if (FindFirstObjectByType<FollowCameraRig>() != null)
            {
                return;
            }

            GameObject rigObject = new GameObject("CameraRig");
            rigObject.AddComponent<FollowCameraRig>();
        }

        private void Awake()
        {
            EnsureHierarchy();
            ResolveReferences(true);
            currentDistance = Mathf.Clamp(cameraDistance, minDistance, maxDistance);

            if (target != null)
            {
                Vector3 startPos = transform.position;
                startPos.x = target.position.x;
                startPos.z = target.position.z;
                if (TryGetGroundY(target.position, out float groundY))
                {
                    startPos.y = groundY + rigHeightOffset;
                }

                transform.position = startPos;
                lastTargetPosition = target.position;
            }
        }

        private void LateUpdate()
        {
            ResolveReferences(false);
            if (target == null || targetCamera == null || pivot == null)
            {
                return;
            }

            float dt = Mathf.Max(0.0001f, Time.deltaTime);
            UpdateRigPosition(dt);
            UpdateCameraPosition(dt);

            lastTargetPosition = target.position;
        }

        private void ResolveReferences(bool force)
        {
            if (!force && Time.unscaledTime < refindTime)
            {
                return;
            }

            if (target == null)
            {
                GameObject go = GameObject.FindWithTag(targetTag);
                if (go != null)
                {
                    target = go.transform;
                    targetRigidbody = go.GetComponent<Rigidbody>();
                }
            }
            else if (targetRigidbody == null)
            {
                targetRigidbody = target.GetComponent<Rigidbody>();
            }

            if (targetCamera == null)
            {
                targetCamera = Camera.main;
                if (targetCamera == null)
                {
                    targetCamera = FindFirstObjectByType<Camera>();
                }
            }

            if (pivot == null)
            {
                Transform foundPivot = transform.Find("Pivot");
                if (foundPivot != null)
                {
                    pivot = foundPivot;
                }
            }

            if (pivot != null && targetCamera != null && targetCamera.transform.parent != pivot)
            {
                targetCamera.transform.SetParent(pivot, true);
            }

            refindTime = Time.unscaledTime + 0.25f;
        }

        private void EnsureHierarchy()
        {
            if (pivot == null)
            {
                Transform existingPivot = transform.Find("Pivot");
                if (existingPivot != null)
                {
                    pivot = existingPivot;
                }
                else
                {
                    GameObject pivotObj = new GameObject("Pivot");
                    pivot = pivotObj.transform;
                    pivot.SetParent(transform, false);
                }
            }

            if (targetCamera == null)
            {
                targetCamera = Camera.main;
                if (targetCamera == null)
                {
                    targetCamera = FindFirstObjectByType<Camera>();
                }
            }

            if (targetCamera != null && targetCamera.transform.parent != pivot)
            {
                targetCamera.transform.SetParent(pivot, true);
            }
        }

        private void UpdateRigPosition(float dt)
        {
            Vector3 followPoint = target.position + GetLookAheadOffset(dt);

            Vector3 desired = transform.position;
            desired.x = followPoint.x;
            desired.z = followPoint.z;

            Vector3 smoothed = Vector3.SmoothDamp(transform.position, desired, ref positionVelocity, Mathf.Max(0.01f, followSmoothTime));

            float targetY = smoothed.y;
            if (TryGetGroundY(followPoint, out float groundY))
            {
                targetY = groundY + rigHeightOffset;
            }

            smoothed.y = Mathf.SmoothDamp(smoothed.y, targetY, ref yVelocity, Mathf.Max(0.01f, rigHeightSmoothTime), Mathf.Infinity, dt);
            transform.position = smoothed;
        }

        private void UpdateCameraPosition(float dt)
        {
            pivot.localRotation = Quaternion.Euler(pivotPitch, pivotYaw, 0f);

            float desiredDistance = Mathf.Clamp(cameraDistance, minDistance, maxDistance);
            Vector3 head = target.position + Vector3.up * 1f;
            Vector3 desiredCameraWorld = pivot.position + (pivot.rotation * (Vector3.back * desiredDistance));
            Vector3 toCam = desiredCameraWorld - head;
            float rayDistance = toCam.magnitude;
            if (rayDistance > 0.001f)
            {
                Vector3 dir = toCam / rayDistance;
                if (Physics.SphereCast(
                        head,
                        Mathf.Max(0.05f, collisionProbeRadius),
                        dir,
                        out RaycastHit hit,
                        rayDistance,
                        obstacleMask,
                        QueryTriggerInteraction.Ignore))
                {
                    desiredDistance = Mathf.Clamp(hit.distance - collisionSafetyMargin, minDistance, desiredDistance);
                }
            }

            currentDistance = Mathf.SmoothDamp(
                currentDistance,
                desiredDistance,
                ref distanceVelocity,
                Mathf.Max(0.01f, avoidSmoothTime),
                Mathf.Infinity,
                dt);

            currentDistance = Mathf.Clamp(currentDistance, minDistance, maxDistance);

            targetCamera.transform.localPosition = new Vector3(0f, 0f, -currentDistance);
            targetCamera.transform.localRotation = Quaternion.identity;
        }

        private Vector3 GetLookAheadOffset(float dt)
        {
            if (!useLookAhead || target == null)
            {
                currentLookAhead = Vector3.SmoothDamp(currentLookAhead, Vector3.zero, ref lookAheadVelocity, Mathf.Max(0.01f, lookAheadSmooth), Mathf.Infinity, dt);
                return currentLookAhead;
            }

            Vector3 velocity = Vector3.zero;
            if (targetRigidbody != null)
            {
                velocity = targetRigidbody.linearVelocity;
            }
            else
            {
                velocity = (target.position - lastTargetPosition) / dt;
            }

            velocity.y = 0f;
            Vector3 targetAhead = Vector3.zero;
            if (velocity.sqrMagnitude >= lookAheadVelocityThreshold * lookAheadVelocityThreshold)
            {
                targetAhead = velocity.normalized * lookAheadDistance;
            }

            currentLookAhead = Vector3.SmoothDamp(currentLookAhead, targetAhead, ref lookAheadVelocity, Mathf.Max(0.01f, lookAheadSmooth), Mathf.Infinity, dt);
            return currentLookAhead;
        }

        private bool TryGetGroundY(Vector3 worldPos, out float y)
        {
            Vector3 origin = new Vector3(worldPos.x, worldPos.y + groundProbeHeight, worldPos.z);
            if (Physics.Raycast(
                    origin,
                    Vector3.down,
                    out RaycastHit hit,
                    groundProbeHeight + groundProbeDistance,
                    groundMask,
                    QueryTriggerInteraction.Ignore))
            {
                y = hit.point.y;
                return true;
            }

            y = worldPos.y;
            return false;
        }

        private void OnDrawGizmosSelected()
        {
            if (!drawDebug || target == null || pivot == null)
            {
                return;
            }

            Vector3 head = target.position + Vector3.up;
            Vector3 camPos = targetCamera != null ? targetCamera.transform.position : pivot.position;

            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(head, camPos);
            Gizmos.DrawWireSphere(head, collisionProbeRadius);
        }
    }
}
