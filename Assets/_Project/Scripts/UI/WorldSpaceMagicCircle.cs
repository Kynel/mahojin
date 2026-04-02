using System.Collections.Generic;
using UnityEngine;

namespace DuckovProto.UI
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(LineRenderer))]
    public sealed class WorldSpaceMagicCircle : MonoBehaviour
    {
        private LineRenderer lineRenderer;

        [SerializeField] private float worldScale = 3f;
        [SerializeField] private float yOffset = 0.05f;

        private void Awake()
        {
            lineRenderer = GetComponent<LineRenderer>();
            lineRenderer.useWorldSpace = true;
            lineRenderer.startWidth = 0.1f;
            lineRenderer.endWidth = 0.1f;

            // Basic material assignment
            lineRenderer.material = new Material(Shader.Find("Sprites/Default"));
            lineRenderer.startColor = new Color(1f, 0.15f, 0.15f, 0.98f);
            lineRenderer.endColor = new Color(1f, 0.15f, 0.15f, 0.98f);

            Hide();
        }

        public void Show()
        {
            gameObject.SetActive(true);
            lineRenderer.positionCount = 0;
        }

        public void Hide()
        {
            if (lineRenderer != null)
            {
                lineRenderer.positionCount = 0;
            }
            gameObject.SetActive(false);
        }

        public void UpdateLine(IReadOnlyList<Vector2> normalizedPoints, Color color, Transform playerTransform)
        {
            if (normalizedPoints == null || normalizedPoints.Count == 0 || playerTransform == null)
            {
                lineRenderer.positionCount = 0;
                return;
            }

            lineRenderer.positionCount = normalizedPoints.Count;
            lineRenderer.startColor = color;
            lineRenderer.endColor = color;

            Vector3 center = playerTransform.position + Vector3.up * yOffset;

            for (int i = 0; i < normalizedPoints.Count; i++)
            {
                Vector2 p = normalizedPoints[i];
                // Map 2D normalized stroke to XZ plane around the player
                Vector3 worldPos = center + new Vector3(p.x * worldScale, 0f, p.y * worldScale);
                lineRenderer.SetPosition(i, worldPos);
            }
        }
    }
}
