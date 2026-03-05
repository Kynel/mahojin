using UnityEngine;

namespace DuckovProto.AI
{
    [DisallowMultipleComponent]
    public sealed class PatternEnemyMover : MonoBehaviour
    {
        [SerializeField] private float radius = 4f;
        [SerializeField] private float angularSpeedDeg = 55f;
        [SerializeField] private bool faceMoveDirection = true;
        [SerializeField] private float heightOffset;

        private Rigidbody cachedRigidbody;
        private Vector3 center;
        private float angleRad;

        private void Awake()
        {
            cachedRigidbody = GetComponent<Rigidbody>();
            float clampedRadius = Mathf.Max(0f, radius);
            center = transform.position;
            if (clampedRadius > 0.001f)
            {
                // Keep the initial scene position on the orbit path to avoid a first-frame jump.
                center -= new Vector3(clampedRadius, 0f, 0f);
            }

            if (cachedRigidbody != null)
            {
                cachedRigidbody.linearVelocity = Vector3.zero;
                cachedRigidbody.angularVelocity = Vector3.zero;
                cachedRigidbody.useGravity = false;
                cachedRigidbody.isKinematic = true;
            }

            angleRad = 0f;
        }

        private void Update()
        {
            angleRad += angularSpeedDeg * Mathf.Deg2Rad * Time.deltaTime;

            Vector3 nextPosition = center;
            nextPosition.x += Mathf.Cos(angleRad) * radius;
            nextPosition.z += Mathf.Sin(angleRad) * radius;
            nextPosition.y += heightOffset;

            Vector3 tangent = new Vector3(-Mathf.Sin(angleRad), 0f, Mathf.Cos(angleRad));

            if (cachedRigidbody != null)
            {
                cachedRigidbody.MovePosition(nextPosition);
            }
            else
            {
                transform.position = nextPosition;
            }

            if (faceMoveDirection && tangent.sqrMagnitude > 0.0001f)
            {
                transform.rotation = Quaternion.LookRotation(tangent.normalized, Vector3.up);
            }
        }
    }
}
