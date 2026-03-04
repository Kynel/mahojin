using UnityEngine;
using UnityEngine.InputSystem;

namespace DuckovProto.Player
{
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(CapsuleCollider))]
    public sealed class PlayerMotor : MonoBehaviour
    {
        [Header("Input")]
        [SerializeField] private InputActionReference moveAction;
        [SerializeField] private float inputDeadzone = 0.08f;

        [Header("Movement")]
        [SerializeField] private float moveSpeed = 6f;
        [SerializeField] private float acceleration = 30f;
        [SerializeField] private float deceleration = 46f;
        [SerializeField] private float stopVelocity = 0.03f;

        [Header("Collision")]
        [SerializeField] private bool ignoreEnemyCollision = true;
        [SerializeField] private LayerMask obstacleMask = 1 << 10; // Wall
        [SerializeField] private float collisionSkin = 0.04f;
        [SerializeField] private int maxSlideIterations = 2;

        [Header("Ground")]
        [SerializeField] private bool followGroundHeight = true;
        [SerializeField] private LayerMask groundMask = 1 << 7; // Ground
        [SerializeField] private float groundProbeHeight = 10f;
        [SerializeField] private float groundProbeDistance = 30f;
        [SerializeField] private float groundOffset = 1.02f;

        [Header("References")]
        [SerializeField] private Rigidbody rb;
        [SerializeField] private CapsuleCollider capsule;

        private Vector2 moveInput;
        private Vector3 currentVelocity;
        private bool collisionRulesInitialized;

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

            InitializeCollisionRules();
        }

        private void OnEnable()
        {
            if (moveAction != null)
            {
                InputAction action = moveAction.action;
                if (action != null)
                {
                    action.Enable();
                }
            }

            InitializeCollisionRules();
        }

        private void OnDisable()
        {
            if (moveAction != null)
            {
                InputAction action = moveAction.action;
                if (action != null)
                {
                    action.Disable();
                }
            }

            moveInput = Vector2.zero;
            currentVelocity = Vector3.zero;
            if (rb != null)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }
        }

        private void OnValidate()
        {
            inputDeadzone = Mathf.Clamp(inputDeadzone, 0f, 0.45f);
            moveSpeed = Mathf.Max(0f, moveSpeed);
            acceleration = Mathf.Max(0f, acceleration);
            deceleration = Mathf.Max(0f, deceleration);
            stopVelocity = Mathf.Max(0f, stopVelocity);
            collisionSkin = Mathf.Clamp(collisionSkin, 0f, 0.25f);
            maxSlideIterations = Mathf.Clamp(maxSlideIterations, 1, 5);
            groundProbeHeight = Mathf.Max(0.5f, groundProbeHeight);
            groundProbeDistance = Mathf.Max(1f, groundProbeDistance);
        }

        private void Update()
        {
            if (moveAction == null)
            {
                moveInput = Vector2.zero;
                return;
            }

            InputAction action = moveAction.action;
            Vector2 raw = action != null ? action.ReadValue<Vector2>() : Vector2.zero;
            moveInput = ApplyDeadzone(raw);
        }

        private void FixedUpdate()
        {
            if (rb == null)
            {
                return;
            }

            float dt = Time.fixedDeltaTime;
            Vector3 desiredDirection = new Vector3(moveInput.x, 0f, moveInput.y);
            if (desiredDirection.sqrMagnitude > 1f)
            {
                desiredDirection.Normalize();
            }

            Vector3 targetVelocity = desiredDirection * moveSpeed;
            float accel = desiredDirection.sqrMagnitude > 0.0001f ? acceleration : deceleration;
            currentVelocity = Vector3.MoveTowards(currentVelocity, targetVelocity, accel * dt);

            if (desiredDirection.sqrMagnitude <= 0.0001f && currentVelocity.sqrMagnitude <= stopVelocity * stopVelocity)
            {
                currentVelocity = Vector3.zero;
            }

            Vector3 planarMove = new Vector3(currentVelocity.x, 0f, currentVelocity.z) * dt;
            if (planarMove.sqrMagnitude > 0.0000001f)
            {
                planarMove = ResolvePlanarMovement(planarMove);
            }

            Vector3 nextPosition = rb.position + planarMove;
            if (followGroundHeight && TryGetGroundY(nextPosition, out float groundY))
            {
                nextPosition.y = groundY + groundOffset;
            }

            rb.MovePosition(nextPosition);
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        private void InitializeCollisionRules()
        {
            if (collisionRulesInitialized || !ignoreEnemyCollision)
            {
                return;
            }

            int playerLayer = gameObject.layer;
            int enemyLayer = LayerMask.NameToLayer("Enemy");
            if (playerLayer >= 0 && enemyLayer >= 0)
            {
                Physics.IgnoreLayerCollision(playerLayer, enemyLayer, true);
            }

            collisionRulesInitialized = true;
        }

        private Vector2 ApplyDeadzone(Vector2 input)
        {
            float deadzone = Mathf.Clamp(inputDeadzone, 0f, 0.45f);
            float magnitude = input.magnitude;
            if (magnitude <= deadzone)
            {
                return Vector2.zero;
            }

            float normalized = Mathf.InverseLerp(deadzone, 1f, magnitude);
            return input.normalized * Mathf.Clamp01(normalized);
        }

        private Vector3 ResolvePlanarMovement(Vector3 requestedMove)
        {
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
    }
}
