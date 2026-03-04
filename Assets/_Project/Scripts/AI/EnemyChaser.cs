using UnityEngine;

namespace DuckovProto.AI
{
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(CapsuleCollider))]
    public sealed class EnemyChaser : MonoBehaviour
    {
        [Header("Targeting")]
        [SerializeField] private Transform target;
        [SerializeField] private float moveSpeed = 3.0f;
        [SerializeField] private float acceleration = 12.0f;
        [SerializeField] private float deceleration = 18.0f;
        [SerializeField] private float stopDistance = 2.0f;
        [SerializeField] private float aggroRange = 10.0f;
        [SerializeField] private float disengageRange = 12.0f;

        [Header("Obstacle Avoidance")]
        [SerializeField] private bool enableObstacleAvoidance = true;
        [SerializeField] private float avoidRayDistance = 2.0f;
        [SerializeField] private float avoidStrength = 1.0f;
        [SerializeField] private float avoidProbeRadius = 0.45f;
        [SerializeField] private LayerMask obstacleMask = 1 << 10; // Wall

        [Header("Collision Resolve")]
        [SerializeField] private bool enableCollisionSlide = true;
        [SerializeField] private float collisionSkin = 0.04f;
        [SerializeField] private int maxSlideIterations = 2;
        [SerializeField] private float stuckThresholdDistance = 0.015f;
        [SerializeField] private float stuckTimeForNudge = 0.35f;
        [SerializeField] private float stuckNudgeStrength = 0.7f;

        [Header("Grounding")]
        [SerializeField] private LayerMask groundMask = 1 << 7; // Ground
        [SerializeField] private float groundProbeHeight = 8f;
        [SerializeField] private float groundProbeDistance = 24f;
        [SerializeField] private float groundOffset = 1.02f;

        [Header("Debug")]
        [SerializeField] private bool enableDebugGizmos = true;
        [SerializeField] private bool isAggro;
        [SerializeField] private Rigidbody rb;
        [SerializeField] private CapsuleCollider capsule;

        private Vector3 currentVelocity;
        private float stuckTimer;
        private int lastAvoidSign = 1;

        private void Awake()
        {
            if (rb == null)
            {
                rb = GetComponent<Rigidbody>();
            }

            if (capsule == null)
            {
                capsule = GetComponent<CapsuleCollider>();
            }

            if (rb != null)
            {
                rb.useGravity = false;
                rb.interpolation = RigidbodyInterpolation.Interpolate;
                rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
                rb.constraints &= ~RigidbodyConstraints.FreezePositionY;
                rb.constraints |= RigidbodyConstraints.FreezeRotationX
                                  | RigidbodyConstraints.FreezeRotationZ;
            }
        }

        private void OnValidate()
        {
            moveSpeed = Mathf.Max(0f, moveSpeed);
            acceleration = Mathf.Max(0f, acceleration);
            deceleration = Mathf.Max(0f, deceleration);
            stopDistance = Mathf.Max(0.1f, stopDistance);
            aggroRange = Mathf.Max(stopDistance, aggroRange);
            disengageRange = Mathf.Max(aggroRange, disengageRange);

            avoidRayDistance = Mathf.Max(0.1f, avoidRayDistance);
            avoidStrength = Mathf.Max(0f, avoidStrength);
            avoidProbeRadius = Mathf.Clamp(avoidProbeRadius, 0.05f, 1.2f);

            collisionSkin = Mathf.Clamp(collisionSkin, 0f, 0.25f);
            maxSlideIterations = Mathf.Clamp(maxSlideIterations, 1, 5);
            stuckThresholdDistance = Mathf.Clamp(stuckThresholdDistance, 0.001f, 0.2f);
            stuckTimeForNudge = Mathf.Clamp(stuckTimeForNudge, 0.05f, 2f);
            stuckNudgeStrength = Mathf.Clamp(stuckNudgeStrength, 0f, 1.2f);

            groundProbeHeight = Mathf.Max(0.5f, groundProbeHeight);
            groundProbeDistance = Mathf.Max(1f, groundProbeDistance);
        }

        private void Update()
        {
            if (target == null)
            {
                GameObject player = GameObject.FindWithTag("Player");
                if (player != null)
                {
                    target = player.transform;
                }
            }
        }

        private void FixedUpdate()
        {
            if (rb == null)
            {
                return;
            }

            float dt = Time.fixedDeltaTime;
            if (target == null)
            {
                SoftStop(dt);
                return;
            }

            Vector3 toTarget = target.position - rb.position;
            toTarget.y = 0f;
            float distance = toTarget.magnitude;

            UpdateAggroState(distance);
            if (!isAggro)
            {
                SoftStop(dt);
                return;
            }

            bool wantsMove = distance > stopDistance;
            Vector3 desiredVelocity = Vector3.zero;
            Vector3 targetDir = toTarget.sqrMagnitude > 0.0001f ? toTarget.normalized : transform.forward;

            if (wantsMove)
            {
                Vector3 moveDir = targetDir;
                if (enableObstacleAvoidance)
                {
                    moveDir = GetSteeredDirection(moveDir);
                }

                desiredVelocity = moveDir * moveSpeed;
            }

            float accel = wantsMove ? acceleration : deceleration;
            currentVelocity = Vector3.MoveTowards(currentVelocity, desiredVelocity, accel * dt);

            if (!wantsMove && currentVelocity.sqrMagnitude <= 0.01f)
            {
                currentVelocity = Vector3.zero;
            }

            Vector3 requestedPlanarMove = new Vector3(currentVelocity.x, 0f, currentVelocity.z) * dt;
            Vector3 resolvedPlanarMove = requestedPlanarMove;
            bool blocked = false;

            if (enableCollisionSlide && requestedPlanarMove.sqrMagnitude > 0.0000001f)
            {
                resolvedPlanarMove = ResolvePlanarMovement(requestedPlanarMove, out blocked);
            }

            UpdateStuckState(targetDir, blocked, requestedPlanarMove, resolvedPlanarMove, dt);

            Vector3 nextPosition = rb.position + resolvedPlanarMove;
            if (TryGetGroundY(nextPosition, out float groundY))
            {
                nextPosition.y = groundY + groundOffset;
            }

            rb.MovePosition(nextPosition);

            Vector3 lookDirection = currentVelocity;
            lookDirection.y = 0f;
            if (lookDirection.sqrMagnitude < 0.0001f)
            {
                lookDirection = targetDir;
            }

            if (lookDirection.sqrMagnitude > 0.0001f)
            {
                Quaternion look = Quaternion.LookRotation(lookDirection.normalized, Vector3.up);
                rb.MoveRotation(Quaternion.Euler(0f, look.eulerAngles.y, 0f));
            }

            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        private void SoftStop(float dt)
        {
            currentVelocity = Vector3.MoveTowards(currentVelocity, Vector3.zero, deceleration * dt);
            if (currentVelocity.sqrMagnitude <= 0.0001f)
            {
                currentVelocity = Vector3.zero;
            }

            stuckTimer = 0f;
            if (rb != null)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }
        }

        private void UpdateAggroState(float distance)
        {
            if (!isAggro && distance <= aggroRange)
            {
                isAggro = true;
            }
            else if (isAggro && distance >= disengageRange)
            {
                isAggro = false;
            }
        }

        private void UpdateStuckState(Vector3 targetDir, bool blocked, Vector3 requestedMove, Vector3 resolvedMove, float dt)
        {
            float requested = requestedMove.magnitude;
            float resolved = resolvedMove.magnitude;
            bool hardBlocked = blocked
                               && requested > 0.01f
                               && resolved <= Mathf.Max(stuckThresholdDistance, requested * 0.2f);

            if (hardBlocked)
            {
                stuckTimer += dt;
            }
            else
            {
                stuckTimer = Mathf.Max(0f, stuckTimer - dt * 2f);
            }

            if (stuckTimer < stuckTimeForNudge)
            {
                return;
            }

            Vector3 side = Vector3.Cross(Vector3.up, targetDir).normalized * lastAvoidSign;
            if (side.sqrMagnitude > 0.0001f)
            {
                currentVelocity = side * (moveSpeed * stuckNudgeStrength);
            }

            lastAvoidSign *= -1;
            stuckTimer = 0f;
        }

        private Vector3 GetSteeredDirection(Vector3 desiredDir)
        {
            if (desiredDir.sqrMagnitude < 0.0001f || avoidRayDistance <= 0.01f)
            {
                return desiredDir;
            }

            Vector3 origin = rb.position + Vector3.up * 0.45f;
            float radius = Mathf.Max(0.05f, avoidProbeRadius);

            if (!Physics.SphereCast(
                    origin,
                    radius,
                    desiredDir,
                    out RaycastHit forwardHit,
                    avoidRayDistance,
                    obstacleMask,
                    QueryTriggerInteraction.Ignore))
            {
                return desiredDir;
            }

            Vector3 wallSlide = Vector3.ProjectOnPlane(desiredDir, forwardHit.normal);
            wallSlide.y = 0f;
            if (wallSlide.sqrMagnitude > 0.0001f)
            {
                wallSlide.Normalize();
                float slideClear = ProbeClearDistance(origin, wallSlide);
                if (slideClear >= avoidRayDistance * 0.35f)
                {
                    return wallSlide;
                }
            }

            Vector3 side = Vector3.Cross(Vector3.up, desiredDir).normalized;
            float steerFactor = Mathf.Max(0.1f, avoidStrength);
            Vector3 leftDir = (desiredDir + side * steerFactor).normalized;
            Vector3 rightDir = (desiredDir - side * steerFactor).normalized;

            float leftClear = ProbeClearDistance(origin, leftDir);
            float rightClear = ProbeClearDistance(origin, rightDir);

            float leftScore = leftClear + (lastAvoidSign > 0 ? 0.05f : 0f);
            float rightScore = rightClear + (lastAvoidSign < 0 ? 0.05f : 0f);
            if (leftScore >= rightScore)
            {
                lastAvoidSign = 1;
                return leftDir;
            }

            lastAvoidSign = -1;
            return rightDir;
        }

        private float ProbeClearDistance(Vector3 origin, Vector3 dir)
        {
            if (Physics.SphereCast(
                    origin,
                    Mathf.Max(0.05f, avoidProbeRadius),
                    dir,
                    out RaycastHit hit,
                    avoidRayDistance,
                    obstacleMask,
                    QueryTriggerInteraction.Ignore))
            {
                return hit.distance;
            }

            return avoidRayDistance + 0.5f;
        }

        private Vector3 ResolvePlanarMovement(Vector3 requestedMove, out bool blocked)
        {
            blocked = false;
            if (obstacleMask.value == 0 || capsule == null)
            {
                return requestedMove;
            }

            Vector3 currentPosition = rb.position;
            Vector3 remaining = requestedMove;
            Vector3 resolved = Vector3.zero;

            int iterations = Mathf.Max(1, maxSlideIterations);
            for (int i = 0; i < iterations; i++)
            {
                float distance = remaining.magnitude;
                if (distance <= 0.00001f)
                {
                    break;
                }

                Vector3 direction = remaining / distance;
                GetCapsulePointsAt(currentPosition, out Vector3 p1, out Vector3 p2, out float radius);

                if (!Physics.CapsuleCast(
                        p1,
                        p2,
                        radius,
                        direction,
                        out RaycastHit hit,
                        distance + collisionSkin,
                        obstacleMask,
                        QueryTriggerInteraction.Ignore))
                {
                    resolved += remaining;
                    break;
                }

                blocked = true;
                float travel = Mathf.Max(0f, hit.distance - collisionSkin);
                Vector3 step = direction * travel;
                resolved += step;
                currentPosition += step;

                remaining -= step;
                remaining = Vector3.ProjectOnPlane(remaining, hit.normal);
                remaining.y = 0f;
            }

            return resolved;
        }

        private void GetCapsulePointsAt(Vector3 worldPosition, out Vector3 p1, out Vector3 p2, out float radius)
        {
            float scaleX = Mathf.Abs(transform.lossyScale.x);
            float scaleY = Mathf.Abs(transform.lossyScale.y);
            float scaleZ = Mathf.Abs(transform.lossyScale.z);

            radius = capsule.radius * Mathf.Max(scaleX, scaleZ);
            float height = Mathf.Max(capsule.height * scaleY, radius * 2f);
            float segmentHalf = (height * 0.5f) - radius;

            Vector3 center = transform.TransformPoint(capsule.center) + (worldPosition - rb.position);
            p1 = center + Vector3.up * segmentHalf;
            p2 = center - Vector3.up * segmentHalf;
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
            if (!enableDebugGizmos)
            {
                return;
            }

            Vector3 center = transform.position;
            center.y += 0.05f;

            DrawCircleXZ(center, aggroRange, Color.yellow);
            DrawCircleXZ(center, disengageRange, Color.gray);
        }

        private static void DrawCircleXZ(Vector3 center, float radius, Color color)
        {
            if (radius <= 0f)
            {
                return;
            }

            Gizmos.color = color;
            const int segments = 48;
            Vector3 prev = center + new Vector3(radius, 0f, 0f);
            for (int i = 1; i <= segments; i++)
            {
                float t = (float)i / segments;
                float angle = t * Mathf.PI * 2f;
                Vector3 next = center + new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
                Gizmos.DrawLine(prev, next);
                prev = next;
            }
        }
    }
}
