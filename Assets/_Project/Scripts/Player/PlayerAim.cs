using DuckovProto.Combat;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DuckovProto.Player
{
    public sealed class PlayerAim : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Camera aimCamera;
        [SerializeField] private Transform rotateTarget;
        [SerializeField] private Transform aimPivot;
        [SerializeField] private LineRenderer aimLine;
        [SerializeField] private AimLockController aimLockController;

        [Header("Aim Settings")]
        [SerializeField] private float lineYOffset = 0.1f;
        [SerializeField] private float minLookDistance = 0.05f;
        [SerializeField] private float lineWidth = 0.03f;
        [SerializeField] private float frozenDefaultLength = 10f;

        [Header("Ground Detection")]
        [SerializeField] private LayerMask groundMask = ~0;
        [SerializeField] private bool useColliderRaycast = false;
        [SerializeField] private float fallbackPlaneHeight = 0f;
        [SerializeField] private float rayDistance = 500f;

        private bool hasAim;
        private Vector3 lastAimStartWorld;
        private Vector3 lastAimEndWorld;
        private Vector3 lastAimDir;
        private float lastAimLen;

        private bool freezeVectorActive;
        private Vector3 frozenDir;
        private float frozenLen;

        public bool IsFreezeVectorActive => freezeVectorActive;

        private void Reset()
        {
            if (aimCamera == null)
            {
                aimCamera = Camera.main;
            }

            if (rotateTarget == null)
            {
                rotateTarget = transform;
            }
        }

        private void Awake()
        {
            if (aimCamera == null)
            {
                aimCamera = Camera.main;
            }

            if (rotateTarget == null)
            {
                rotateTarget = transform;
            }

            if (aimLockController == null)
            {
                aimLockController = GetComponent<AimLockController>();
            }

            EnsureAimLine();
        }

        private void Update()
        {
            if (rotateTarget == null)
            {
                SetAimLineActive(false);
                return;
            }

            if (aimLockController == null)
            {
                aimLockController = GetComponent<AimLockController>();
            }

            if (TryApplyLockedAim())
            {
                return;
            }

            if (freezeVectorActive)
            {
                UpdateFrozenAimLine();
                return;
            }

            if (aimCamera == null || Mouse.current == null)
            {
                SetAimLineActive(false);
                return;
            }

            Ray ray = aimCamera.ScreenPointToRay(Mouse.current.position.ReadValue());
            if (!TryGetGroundPoint(ray, out Vector3 targetPoint))
            {
                SetAimLineActive(false);
                return;
            }

            Vector3 start = rotateTarget.position + new Vector3(0f, lineYOffset, 0f);
            Vector3 end = targetPoint + new Vector3(0f, lineYOffset, 0f);
            Vector3 aimVector = end - start;
            float aimLength = aimVector.magnitude;

            if (aimLength > 0.0001f)
            {
                Vector3 normalized = aimVector / aimLength;
                hasAim = true;
                lastAimStartWorld = start;
                lastAimEndWorld = end;
                lastAimDir = normalized;
                lastAimLen = aimLength;
            }

            Vector3 flatDirection = targetPoint - rotateTarget.position;
            flatDirection.y = 0f;
            if (flatDirection.sqrMagnitude >= minLookDistance * minLookDistance)
            {
                Quaternion lookRotation = Quaternion.LookRotation(flatDirection.normalized, Vector3.up);
                Vector3 yawOnly = lookRotation.eulerAngles;
                rotateTarget.rotation = Quaternion.Euler(0f, yawOnly.y, 0f);

                // Backward compatibility for setups still reading AimPivot rotation.
                if (aimPivot != null)
                {
                    aimPivot.rotation = rotateTarget.rotation;
                }
            }

            UpdateAimLine(start, end);
        }

        private bool TryApplyLockedAim()
        {
            if (aimLockController == null || !aimLockController.HasLock)
            {
                return false;
            }

            Vector3 lockedPoint = aimLockController.GetLockedAimPointWorld();
            Vector3 start = rotateTarget.position + new Vector3(0f, lineYOffset, 0f);
            Vector3 end = lockedPoint;
            Vector3 aimVector = end - start;
            float aimLength = aimVector.magnitude;

            if (aimLength > 0.0001f)
            {
                Vector3 normalized = aimVector / aimLength;
                hasAim = true;
                lastAimStartWorld = start;
                lastAimEndWorld = end;
                lastAimDir = normalized;
                lastAimLen = aimLength;
            }

            Vector3 flatDirection = lockedPoint - rotateTarget.position;
            flatDirection.y = 0f;
            if (flatDirection.sqrMagnitude >= minLookDistance * minLookDistance)
            {
                Quaternion lookRotation = Quaternion.LookRotation(flatDirection.normalized, Vector3.up);
                Vector3 yawOnly = lookRotation.eulerAngles;
                rotateTarget.rotation = Quaternion.Euler(0f, yawOnly.y, 0f);

                if (aimPivot != null)
                {
                    aimPivot.rotation = rotateTarget.rotation;
                }
            }

            UpdateAimLine(start, end);
            return true;
        }

        private bool TryGetGroundPoint(Ray ray, out Vector3 hitPoint)
        {
            if (useColliderRaycast && Physics.Raycast(ray, out RaycastHit hit, rayDistance, groundMask))
            {
                hitPoint = hit.point;
                return true;
            }

            Plane groundPlane = new Plane(Vector3.up, new Vector3(0f, fallbackPlaneHeight, 0f));
            if (groundPlane.Raycast(ray, out float enter))
            {
                hitPoint = ray.GetPoint(enter);
                return true;
            }

            hitPoint = default;
            return false;
        }

        private void EnsureAimLine()
        {
            if (aimLine == null)
            {
                aimLine = GetComponent<LineRenderer>();
            }

            if (aimLine == null)
            {
                aimLine = gameObject.AddComponent<LineRenderer>();
            }

            aimLine.useWorldSpace = true;
            aimLine.loop = false;
            aimLine.positionCount = 2;
            aimLine.startWidth = lineWidth;
            aimLine.endWidth = lineWidth;
            aimLine.enabled = true;
        }

        private void UpdateAimLine(Vector3 start, Vector3 end)
        {
            if (aimLine == null)
            {
                return;
            }

            aimLine.startWidth = lineWidth;
            aimLine.endWidth = lineWidth;
            aimLine.SetPosition(0, start);
            aimLine.SetPosition(1, end);
            aimLine.enabled = true;
        }

        private void SetAimLineActive(bool isActive)
        {
            if (aimLine != null)
            {
                aimLine.enabled = isActive;
            }
        }

        public void BeginFreezeVector()
        {
            if (freezeVectorActive)
            {
                return;
            }

            freezeVectorActive = true;
            frozenDir = Vector3.forward;
            frozenLen = Mathf.Max(0.01f, frozenDefaultLength);

            if (hasAim && lastAimDir.sqrMagnitude > 0.0001f)
            {
                frozenDir = lastAimDir.normalized;
                frozenLen = Mathf.Max(0.01f, lastAimLen);
            }
            else if (rotateTarget != null && rotateTarget.forward.sqrMagnitude > 0.0001f)
            {
                frozenDir = rotateTarget.forward.normalized;
            }
        }

        public void EndFreezeVector()
        {
            freezeVectorActive = false;
        }

        public Vector3 GetFrozenAimDirection()
        {
            if (frozenDir.sqrMagnitude <= 0.0001f)
            {
                return rotateTarget != null ? rotateTarget.forward.normalized : Vector3.forward;
            }

            return frozenDir.normalized;
        }

        private void UpdateFrozenAimLine()
        {
            if (rotateTarget == null || aimLine == null)
            {
                return;
            }

            Vector3 dir = GetFrozenAimDirection();
            float length = Mathf.Max(0.01f, frozenLen);
            Vector3 start = rotateTarget.position + new Vector3(0f, lineYOffset, 0f);
            Vector3 end = start + (dir * length);

            aimLine.startWidth = lineWidth;
            aimLine.endWidth = lineWidth;
            aimLine.SetPosition(0, start);
            aimLine.SetPosition(1, end);
            aimLine.enabled = true;
        }
    }
}
