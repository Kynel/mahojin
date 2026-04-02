using UnityEngine;

namespace DuckovProto.Combat
{
    [DisallowMultipleComponent]
    public sealed class AimLockController : MonoBehaviour
    {
        private static readonly RaycastHit[] CursorHitBuffer = new RaycastHit[16];

        private enum LockMode
        {
            None,
            Target,
            Point
        }

        [SerializeField] private LayerMask enemyMask = 1 << 8;
        [SerializeField] private LayerMask groundMask = 1 << 7;
        [SerializeField] private LayerMask wallMask = 1 << 10;
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
            int mask = enemyMask.value | groundMask.value | wallMask.value;
            if (mask == 0)
            {
                ClearLock();
                return;
            }

            int hitCount = Physics.RaycastNonAlloc(
                ray,
                CursorHitBuffer,
                maxRayDistance,
                mask,
                QueryTriggerInteraction.Ignore);

            if (hitCount > 0 && TryResolveCursorHit(hitCount, out RaycastHit bestHit, out bool hitEnemy))
            {
                if (hitEnemy)
                {
                    lockMode = LockMode.Target;
                    targetTransform = ResolveEnemyRoot(bestHit.transform);
                    pointLockWorld = bestHit.point;
                    lockExpireTime = Time.time + Mathf.Max(0.01f, lockDuration);
                    return;
                }

                lockMode = LockMode.Point;
                targetTransform = null;
                pointLockWorld = bestHit.point;
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
                return ResolveTargetAimPointWorld(targetTransform);
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

        private Vector3 ResolveTargetAimPointWorld(Transform target)
        {
            if (target == null)
            {
                return transform.position + (transform.forward * 8f) + (Vector3.up * aimPointHeightOffset);
            }

            Collider targetCollider = target.GetComponent<Collider>();
            if (targetCollider == null)
            {
                targetCollider = target.GetComponentInChildren<Collider>();
            }

            if (targetCollider != null)
            {
                return targetCollider.bounds.center + (Vector3.up * aimPointHeightOffset);
            }

            return target.position + (Vector3.up * aimPointHeightOffset);
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

        private bool TryResolveCursorHit(int hitCount, out RaycastHit bestHit, out bool hitEnemy)
        {
            bestHit = default;
            hitEnemy = false;

            bool foundEnemy = false;
            bool foundGround = false;
            bool foundWall = false;
            RaycastHit nearestEnemy = default;
            RaycastHit nearestGround = default;
            RaycastHit nearestWall = default;
            float nearestEnemyDistance = float.MaxValue;
            float nearestGroundDistance = float.MaxValue;
            float nearestWallDistance = float.MaxValue;

            for (int i = 0; i < hitCount; i++)
            {
                RaycastHit candidate = CursorHitBuffer[i];
                Collider col = candidate.collider;
                if (col == null)
                {
                    continue;
                }

                int layerMask = 1 << col.gameObject.layer;
                bool isEnemy = (enemyMask.value & layerMask) != 0;
                bool isGround = (groundMask.value & layerMask) != 0;
                bool isWall = (wallMask.value & layerMask) != 0;
                if (!isEnemy && !isGround && !isWall)
                {
                    continue;
                }

                if (candidate.distance < 0f)
                {
                    continue;
                }

                if (isWall && candidate.distance < nearestWallDistance)
                {
                    foundWall = true;
                    nearestWallDistance = candidate.distance;
                    nearestWall = candidate;
                }

                if (isEnemy && candidate.distance < nearestEnemyDistance)
                {
                    foundEnemy = true;
                    nearestEnemyDistance = candidate.distance;
                    nearestEnemy = candidate;
                }

                if (isGround && candidate.distance < nearestGroundDistance)
                {
                    foundGround = true;
                    nearestGroundDistance = candidate.distance;
                    nearestGround = candidate;
                }
            }

            // Keep walls as cover, but prefer an enemy over ground when the enemy is not occluded.
            if (foundEnemy && (!foundWall || nearestEnemyDistance <= nearestWallDistance))
            {
                bestHit = nearestEnemy;
                hitEnemy = true;
                return true;
            }

            if (foundWall && (!foundGround || nearestWallDistance <= nearestGroundDistance))
            {
                bestHit = nearestWall;
                return true;
            }

            if (foundGround)
            {
                bestHit = nearestGround;
                return true;
            }

            if (foundWall)
            {
                bestHit = nearestWall;
                return true;
            }

            return false;
        }
    }
}
