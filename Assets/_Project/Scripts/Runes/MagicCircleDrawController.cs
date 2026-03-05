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

        [Header("Draw Rules")]
        [SerializeField] private float maxDrawTime = 3f;
        [SerializeField] private float uiRadiusPx = 140f;
        [SerializeField] private float minPointDistancePx = 3f;
        [SerializeField] private int minPoints = 16;
        [SerializeField] private bool clearAimLockOnSubmit;
        [SerializeField] private bool clearAimLockOnCancelOrFail = true;

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
            magicCircleUI.UpdateUserLine(localStrokePoints);
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

            localStrokePoints.Clear();
            normalizedStrokePoints.Clear();
            hasAnyStroke = false;
            state = DrawState.Idle;

            if (magicCircleUI != null)
            {
                magicCircleUI.UpdateUserLine(localStrokePoints);
                magicCircleUI.ShowMessage(message, 0.25f);

                if (delayedCloseRoutine != null)
                {
                    StopCoroutine(delayedCloseRoutine);
                }

                delayedCloseRoutine = StartCoroutine(CloseUiAfterDelay(0.25f));
            }

            if (clearAimLockOnCancelOrFail && aimLockController != null)
            {
                aimLockController.ClearLock();
            }
        }

        private void FailAndClose(string message)
        {
            CancelDraw(message);
        }

        private void CleanupAfterFinish()
        {
            state = DrawState.Idle;
            localStrokePoints.Clear();
            normalizedStrokePoints.Clear();
            hasAnyStroke = false;

            if (magicCircleUI != null)
            {
                magicCircleUI.Close();
            }

            if (clearAimLockOnSubmit && aimLockController != null)
            {
                aimLockController.ClearLock();
            }
        }

        private IEnumerator CloseUiAfterDelay(float delay)
        {
            yield return new WaitForSecondsRealtime(Mathf.Max(0f, delay));
            delayedCloseRoutine = null;

            if (magicCircleUI != null)
            {
                magicCircleUI.Close();
            }
        }
    }
}
