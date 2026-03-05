using UnityEngine;

namespace DuckovProto.Combat
{
    [DisallowMultipleComponent]
    public sealed class AimLockController : MonoBehaviour
    {
        private enum LockMode
        {
            None,
            Target,
            Point
        }

        [SerializeField] private LayerMask enemyMask = 1 << 8;
        [SerializeField] private LayerMask groundMask = 1 << 7;
        [SerializeField] private float lockDuration = 3f;
        [SerializeField] private float aimPointHeightOffset = 0.6f;
        [SerializeField] private bool lockIgnoresLOS = true;

        private LockMode lockMode;
        private Transform targetTransform;
        private Vector3 pointLockWorld;
        private float lockExpireTime = -1f;

        public bool HasLock => IsLockValid();
        public bool IsTargetLock => IsLockValid() && lockMode == LockMode.Target;
        public bool IsPointLock => IsLockValid() && lockMode == LockMode.Point;
        public float RemainingLockTime => IsLockValid() ? Mathf.Max(0f, lockExpireTime - Time.time) : 0f;

        private void Update()
        {
            if (!IsLockValid())
            {
                ClearLock();
            }
        }

        public void BeginLockFromCursor(Camera cam, Vector2 screenPos)
        {
            if (cam == null)
            {
                ClearLock();
                return;
            }

            Ray ray = cam.ScreenPointToRay(screenPos);
            const float maxRayDistance = 500f;

            if (Physics.Raycast(ray, out RaycastHit enemyHit, maxRayDistance, enemyMask, QueryTriggerInteraction.Ignore))
            {
                lockMode = LockMode.Target;
                targetTransform = ResolveEnemyRoot(enemyHit.transform);
                pointLockWorld = enemyHit.point;
                lockExpireTime = Time.time + Mathf.Max(0.01f, lockDuration);
                return;
            }

            if (Physics.Raycast(ray, out RaycastHit groundHit, maxRayDistance, groundMask, QueryTriggerInteraction.Ignore))
            {
                lockMode = LockMode.Point;
                targetTransform = null;
                pointLockWorld = groundHit.point;
                lockExpireTime = Time.time + Mathf.Max(0.01f, lockDuration);
                return;
            }

            ClearLock();
        }

        public Vector3 GetLockedAimPointWorld()
        {
            if (!IsLockValid())
            {
                return transform.position + (transform.forward * 8f) + (Vector3.up * aimPointHeightOffset);
            }

            if (lockMode == LockMode.Target && targetTransform != null)
            {
                return targetTransform.position + (Vector3.up * aimPointHeightOffset);
            }

            if (lockMode == LockMode.Point)
            {
                return pointLockWorld + (Vector3.up * aimPointHeightOffset);
            }

            return transform.position + (transform.forward * 8f) + (Vector3.up * aimPointHeightOffset);
        }

        public Vector3 GetLockedAimDirection(Transform caster)
        {
            Vector3 origin = caster != null ? caster.position : transform.position;
            Vector3 targetPoint = GetLockedAimPointWorld();

            if (!lockIgnoresLOS)
            {
                // Prototype scope: LOS cut-off is intentionally omitted.
            }

            Vector3 dir = targetPoint - origin;
            dir.y = 0f;
            if (dir.sqrMagnitude <= 0.0001f)
            {
                Vector3 fallback = caster != null ? caster.forward : transform.forward;
                fallback.y = 0f;
                if (fallback.sqrMagnitude <= 0.0001f)
                {
                    return Vector3.forward;
                }

                return fallback.normalized;
            }

            return dir.normalized;
        }

        public void ClearLock()
        {
            lockMode = LockMode.None;
            targetTransform = null;
            pointLockWorld = Vector3.zero;
            lockExpireTime = -1f;
        }

        public void ExtendLock(float extraSeconds)
        {
            if (!IsLockValid() || extraSeconds <= 0f)
            {
                return;
            }

            float baseTime = Mathf.Max(lockExpireTime, Time.time);
            lockExpireTime = baseTime + extraSeconds;
        }

        public void EnsureLockUntil(float durationFromNow)
        {
            if (!IsLockValid())
            {
                return;
            }

            float targetExpire = Time.time + Mathf.Max(0f, durationFromNow);
            if (targetExpire > lockExpireTime)
            {
                lockExpireTime = targetExpire;
            }
        }

        private bool IsLockValid()
        {
            if (lockMode == LockMode.None)
            {
                return false;
            }

            if (lockExpireTime > 0f && Time.time > lockExpireTime)
            {
                return false;
            }

            if (lockMode == LockMode.Target && targetTransform == null)
            {
                return false;
            }

            return true;
        }

        private static Transform ResolveEnemyRoot(Transform source)
        {
            Transform current = source;
            while (current != null)
            {
                if (current.gameObject.layer == LayerMask.NameToLayer("Enemy"))
                {
                    return current;
                }

                current = current.parent;
            }

            return source;
        }
    }
}
