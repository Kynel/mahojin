using System;
using System.Collections;
using System.Collections.Generic;
using DuckovProto.Player;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DuckovProto.Runes
{
    public sealed class RuneDrawController : MonoBehaviour
    {
        public event Action<IReadOnlyList<Vector3>> StrokeCompleted;

        [Header("Drawing")]
        [SerializeField] private LayerMask groundMask = ~0;
        [SerializeField] private float minPointDistance = 0.1f;
        [SerializeField] private int maxPoints = 256;
        [SerializeField] private int minStrokePoints = 6;
        [SerializeField] private float strokeLineYOffset = 0.05f;
        [SerializeField] private float strokeHideDelay = 0.35f;

        [Header("References")]
        [SerializeField] private LineRenderer strokeLine;
        [SerializeField] private PlayerAim playerAim;
        [SerializeField] private Camera aimCamera;

        private readonly List<Vector3> strokePoints = new List<Vector3>(256);
        private Coroutine hideStrokeCoroutine;
        private bool isDrawing;

        private void Awake()
        {
            if (playerAim == null)
            {
                playerAim = GetComponent<PlayerAim>();
            }

            if (aimCamera == null)
            {
                aimCamera = Camera.main;
            }

            EnsureStrokeLine();
            HideStrokeLine();
        }

        private void Update()
        {
            if (Keyboard.current == null)
            {
                return;
            }

            if (Keyboard.current.spaceKey.wasPressedThisFrame)
            {
                BeginDrawing();
            }

            if (isDrawing)
            {
                SamplePoint();
            }

            if (Keyboard.current.spaceKey.wasReleasedThisFrame)
            {
                EndDrawing();
            }
        }

        private void BeginDrawing()
        {
            isDrawing = true;
            strokePoints.Clear();

            if (hideStrokeCoroutine != null)
            {
                StopCoroutine(hideStrokeCoroutine);
                hideStrokeCoroutine = null;
            }

            if (playerAim != null)
            {
                playerAim.BeginFreezeVector();
            }

            EnsureStrokeLine();
            strokeLine.positionCount = 0;
            strokeLine.enabled = true;

            SamplePoint();
        }

        private void EndDrawing()
        {
            if (!isDrawing)
            {
                return;
            }

            isDrawing = false;

            // Prototype rule: ignore very short strokes instead of emitting completion.
            if (strokePoints.Count >= minStrokePoints)
            {
                List<Vector3> completedStroke = new List<Vector3>(strokePoints);
                StrokeCompleted?.Invoke(completedStroke);
                Debug.Log($"StrokeCompleted: {completedStroke.Count} points", this);
            }

            if (playerAim != null)
            {
                playerAim.EndFreezeVector();
            }

            if (strokeHideDelay <= 0f)
            {
                HideStrokeLine();
            }
            else
            {
                hideStrokeCoroutine = StartCoroutine(HideStrokeLineAfterDelay());
            }
        }

        private void SamplePoint()
        {
            if (strokePoints.Count >= maxPoints)
            {
                return;
            }

            if (!TryGetMouseGroundPoint(out Vector3 hitPoint))
            {
                return;
            }

            if (strokePoints.Count > 0)
            {
                Vector3 previous = strokePoints[strokePoints.Count - 1];
                if ((hitPoint - previous).sqrMagnitude < minPointDistance * minPointDistance)
                {
                    return;
                }
            }

            strokePoints.Add(hitPoint);
            RefreshStrokeLine();
        }

        private bool TryGetMouseGroundPoint(out Vector3 hitPoint)
        {
            if (Mouse.current == null)
            {
                hitPoint = default;
                return false;
            }

            if (aimCamera == null)
            {
                aimCamera = Camera.main;
                if (aimCamera == null)
                {
                    hitPoint = default;
                    return false;
                }
            }

            Ray ray = aimCamera.ScreenPointToRay(Mouse.current.position.ReadValue());
            if (Physics.Raycast(ray, out RaycastHit hit, 500f, groundMask))
            {
                hitPoint = hit.point;
                return true;
            }

            hitPoint = default;
            return false;
        }

        private void EnsureStrokeLine()
        {
            if (strokeLine == null)
            {
                Transform child = transform.Find("RuneStrokeLine");
                if (child != null)
                {
                    strokeLine = child.GetComponent<LineRenderer>();
                }
            }

            if (strokeLine == null)
            {
                GameObject lineObject = new GameObject("RuneStrokeLine");
                lineObject.transform.SetParent(transform, false);
                strokeLine = lineObject.AddComponent<LineRenderer>();
            }

            strokeLine.useWorldSpace = true;
            strokeLine.loop = false;
            strokeLine.positionCount = 0;
            strokeLine.startWidth = 0.05f;
            strokeLine.endWidth = 0.05f;

            if (strokeLine.sharedMaterial == null)
            {
                Shader lineShader = Shader.Find("Sprites/Default");
                if (lineShader != null)
                {
                    strokeLine.sharedMaterial = new Material(lineShader);
                }
            }
        }

        private void RefreshStrokeLine()
        {
            if (strokeLine == null)
            {
                return;
            }

            strokeLine.positionCount = strokePoints.Count;
            for (int i = 0; i < strokePoints.Count; i++)
            {
                Vector3 p = strokePoints[i];
                p.y += strokeLineYOffset;
                strokeLine.SetPosition(i, p);
            }
        }

        private IEnumerator HideStrokeLineAfterDelay()
        {
            yield return new WaitForSeconds(strokeHideDelay);
            hideStrokeCoroutine = null;
            HideStrokeLine();
        }

        private void HideStrokeLine()
        {
            if (strokeLine == null)
            {
                return;
            }

            strokeLine.positionCount = 0;
            strokeLine.enabled = false;
        }
    }
}
