using System;
using System.Collections;
using System.Collections.Generic;
using DuckovProto.Combat;
using DuckovProto.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace DuckovProto.Runes
{
    [DisallowMultipleComponent]
    public sealed class MagicCircleDrawController : MonoBehaviour
    {
        private enum DrawState
        {
            Idle,
            CastingModeActive,
            Drawing,
        }

        public event Action<IReadOnlyList<Vector2>> StrokeSubmittedNorm;

        [SerializeField] private AimLockController aimLockController;
        [SerializeField] private MagicCircleUI magicCircleUI;
        [SerializeField] private Camera aimCamera;

        [Header("Input Mode")]
        [SerializeField] private bool rightClickStartsCasting = true;
        [SerializeField] private bool allowLeftDragToDraw = true;

        [Header("World Space Extension")]
        [SerializeField] private WorldSpaceMagicCircle worldSpaceMagicCircle;
        [SerializeField] private Transform playerTransform;

        [Header("Draw Rules")]
        [SerializeField] private float maxDrawTime = 3f;
        [SerializeField] private float uiRadiusPx = 140f;
        [SerializeField] private float minPointDistancePx = 3f;
        [SerializeField] private int minPoints = 16;
        [SerializeField] private bool clearAimLockOnSubmit;
        [SerializeField] private bool clearAimLockOnCancelOrFail = true;

        private RuneCastPipelineController runeCastPipelineController;

        private readonly List<Vector2> localStrokePoints = new List<Vector2>(256);
        private readonly List<Vector2> normalizedStrokePoints = new List<Vector2>(256);

        private DrawState state = DrawState.Idle;
        private float drawStartTime;
        private Vector2 castStartScreenPos;
        private bool hasAnyStroke;
        private Coroutine delayedCloseRoutine;

        private void Awake()
        {
            if (aimLockController == null)
            {
                aimLockController = GetComponent<AimLockController>();
            }

            if (magicCircleUI == null)
            {
                magicCircleUI = GetComponent<MagicCircleUI>();
            }

            if (magicCircleUI == null)
            {
                magicCircleUI = gameObject.AddComponent<MagicCircleUI>();
            }

            if (aimCamera == null)
            {
                aimCamera = Camera.main;
            }

            runeCastPipelineController = GetComponent<RuneCastPipelineController>();

            if (worldSpaceMagicCircle == null)
            {
                var circleObj = new GameObject("WorldSpaceMagicCircle");
                worldSpaceMagicCircle = circleObj.AddComponent<WorldSpaceMagicCircle>();
            }
            if (playerTransform == null)
            {
                playerTransform = transform;
            }
        }

        private void Update()
        {
            Mouse mouse = Mouse.current;
            if (mouse == null)
            {
                return;
            }

            if (state != DrawState.Idle && Time.unscaledTime - drawStartTime > maxDrawTime)
            {
                CancelDraw("Time out");
                return;
            }

            if (state == DrawState.Idle && rightClickStartsCasting && mouse.rightButton.wasPressedThisFrame)
            {
                TryEnterCastingMode(mouse.position.ReadValue());
            }

            if (state == DrawState.Idle)
            {
                return;
            }

            if (mouse.leftButton.wasReleasedThisFrame || mouse.rightButton.wasReleasedThisFrame)
            {
                FinishAttempt();
                return;
            }

            if (!allowLeftDragToDraw)
            {
                return;
            }

            if (mouse.leftButton.isPressed)
            {
                state = DrawState.Drawing;
                SampleStrokePoint(mouse.position.ReadValue());
            }
            else if (state == DrawState.Drawing)
            {
                state = DrawState.CastingModeActive;
            }
        }

        private void TryEnterCastingMode(Vector2 mouseScreenPos)
        {
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            {
                return;
            }

            if (aimCamera == null)
            {
                aimCamera = Camera.main;
            }

            if (magicCircleUI == null)
            {
                return;
            }

            if (delayedCloseRoutine != null)
            {
                StopCoroutine(delayedCloseRoutine);
                delayedCloseRoutine = null;
            }

            localStrokePoints.Clear();
            normalizedStrokePoints.Clear();
            hasAnyStroke = false;
            castStartScreenPos = mouseScreenPos;

            magicCircleUI.Close();
            magicCircleUI.Show(castStartScreenPos, uiRadiusPx);

            if (aimLockController != null && aimCamera != null)
            {
                aimLockController.BeginLockFromCursor(aimCamera, castStartScreenPos);
            }

            if (worldSpaceMagicCircle != null)
            {
                worldSpaceMagicCircle.Show();
            }

            drawStartTime = Time.unscaledTime;
            state = DrawState.CastingModeActive;
        }

        private void SampleStrokePoint(Vector2 mouseScreenPos)
        {
            if (!magicCircleUI.TryGetLocalPoint(mouseScreenPos, out Vector2 localPoint))
            {
                CancelDraw("Draw canceled");
                return;
            }

            float radius = Mathf.Max(1f, magicCircleUI.ActiveRadiusPx);
            if (localPoint.sqrMagnitude > radius * radius)
            {
                CancelDraw("Out of circle");
                return;
            }

            if (localStrokePoints.Count > 0)
            {
                Vector2 prev = localStrokePoints[localStrokePoints.Count - 1];
                if ((localPoint - prev).sqrMagnitude < minPointDistancePx * minPointDistancePx)
                {
                    return;
                }
            }

            localStrokePoints.Add(localPoint);
            hasAnyStroke = true;
            UpdateVisualFeedback(radius);
        }

        private void UpdateVisualFeedback(float radius)
        {
            Color strokeColor = new Color(1f, 0.15f, 0.15f, 0.98f); // Default Red

            normalizedStrokePoints.Clear();
            for (int i = 0; i < localStrokePoints.Count; i++)
            {
                Vector2 p = localStrokePoints[i] / radius;
                p.x = Mathf.Clamp(p.x, -1f, 1f);
                p.y = Mathf.Clamp(p.y, -1f, 1f);
                normalizedStrokePoints.Add(p);
            }

            if (runeCastPipelineController != null && localStrokePoints.Count >= Mathf.Max(2, minPoints / 2))
            {
                var liveScoreInfo = runeCastPipelineController.EvaluateStrokeLive(normalizedStrokePoints);

                if (liveScoreInfo.bestRune != null)
                {
                    float passScore = runeCastPipelineController.GetConfiguredPassScore(liveScoreInfo.bestRune);
                    float normalizedScore = Mathf.Clamp01(liveScoreInfo.bestScore / passScore);

                    // Determine base color based on spell type
                    Color targetColor;
                    string linkedSpell = liveScoreInfo.bestRune.LinkedSpellId?.ToLowerInvariant() ?? "";

                    if (linkedSpell.Contains("water")) targetColor = new Color(0.15f, 0.45f, 1f, 0.98f); // Blue
                    else if (linkedSpell.Contains("fire")) targetColor = new Color(1f, 0.35f, 0.15f, 0.98f); // Orange-Red
                    else if (linkedSpell.Contains("light") || linkedSpell.Contains("rail")) targetColor = new Color(1f, 0.9f, 0.1f, 0.98f); // Yellow
                    else targetColor = new Color(0.8f, 0.8f, 0.8f, 0.98f); // Default grayish white

                    // Interpolate from red to target color based on score
                    strokeColor = Color.Lerp(new Color(1f, 0.15f, 0.15f, 0.98f), targetColor, normalizedScore * normalizedScore);
                }
            }

            magicCircleUI.UpdateUserLine(localStrokePoints, strokeColor);

            if (worldSpaceMagicCircle != null && playerTransform != null)
            {
                worldSpaceMagicCircle.UpdateLine(normalizedStrokePoints, strokeColor, playerTransform);
            }
        }

        private void FinishAttempt()
        {
            if (state == DrawState.Idle)
            {
                return;
            }

            float elapsed = Time.unscaledTime - drawStartTime;
            if (elapsed > maxDrawTime)
            {
                CancelDraw("Time out");
                return;
            }

            if (!hasAnyStroke || localStrokePoints.Count < Mathf.Max(2, minPoints))
            {
                FailAndClose("Too short");
                return;
            }

            float radius = Mathf.Max(1f, magicCircleUI.ActiveRadiusPx);
            normalizedStrokePoints.Clear();
            for (int i = 0; i < localStrokePoints.Count; i++)
            {
                Vector2 p = localStrokePoints[i] / radius;
                p.x = Mathf.Clamp(p.x, -1f, 1f);
                p.y = Mathf.Clamp(p.y, -1f, 1f);
                normalizedStrokePoints.Add(p);
            }

            List<Vector2> submitted = new List<Vector2>(normalizedStrokePoints);
            StrokeSubmittedNorm?.Invoke(submitted);

            CleanupAfterFinish();
        }

        private void CancelDraw(string message)
        {
            if (state == DrawState.Idle)
            {
                return;
            }

            hasAnyStroke = false;
            state = DrawState.Idle;

            if (magicCircleUI != null)
            {
                magicCircleUI.ShowMessage(message, 0.25f);

                if (delayedCloseRoutine != null)
                {
                    StopCoroutine(delayedCloseRoutine);
                }

                delayedCloseRoutine = StartCoroutine(VisualFeedbackAndCloseRoutine(new Color(0.8f, 0.1f, 0.1f, 1f), 0.25f)); // Flash red for failure
            }
            else
            {
                ClearDrawData();
            }
        }

        private void FailAndClose(string message)
        {
            if (state == DrawState.Idle) return;
            CancelDraw(message);
        }

        private void CleanupAfterFinish()
        {
            state = DrawState.Idle;
            hasAnyStroke = false;

            if (magicCircleUI != null)
            {
                if (delayedCloseRoutine != null)
                {
                    StopCoroutine(delayedCloseRoutine);
                }
                delayedCloseRoutine = StartCoroutine(VisualFeedbackAndCloseRoutine(new Color(0.1f, 0.8f, 0.3f, 1f), 0.35f)); // Flash green for success
            }
            else
            {
                ClearDrawData();
            }
        }

        private void ClearDrawData()
        {
            localStrokePoints.Clear();
            normalizedStrokePoints.Clear();

            if (magicCircleUI != null)
            {
                magicCircleUI.UpdateUserLine(localStrokePoints);
                magicCircleUI.Close();
            }

            if (worldSpaceMagicCircle != null)
            {
                worldSpaceMagicCircle.Hide();
            }

            if (clearAimLockOnSubmit && aimLockController != null)
            {
                aimLockController.ClearLock();
            }
        }

        private IEnumerator VisualFeedbackAndCloseRoutine(Color flashColor, float delay)
        {
            float elapsed = 0f;

            // Initial flash
            if (magicCircleUI != null) magicCircleUI.UpdateUserLine(localStrokePoints, flashColor);
            if (worldSpaceMagicCircle != null) worldSpaceMagicCircle.UpdateLine(normalizedStrokePoints, flashColor, playerTransform);

            while (elapsed < delay)
            {
                elapsed += Time.unscaledDeltaTime;

                // Fade out
                float alpha = Mathf.Lerp(1f, 0f, elapsed / delay);
                Color fadeColor = new Color(flashColor.r, flashColor.g, flashColor.b, alpha);

                if (magicCircleUI != null) magicCircleUI.UpdateUserLine(localStrokePoints, fadeColor);
                if (worldSpaceMagicCircle != null) worldSpaceMagicCircle.UpdateLine(normalizedStrokePoints, fadeColor, playerTransform);

                yield return null;
            }

            delayedCloseRoutine = null;
            ClearDrawData();

            if (clearAimLockOnCancelOrFail && aimLockController != null && flashColor.r > flashColor.g) // Hacky way to check if it was a failure (red flash)
            {
                aimLockController.ClearLock();
            }
        }
    }
}
