using System;
using DuckovProto.Combat;
using UnityEngine;

namespace DuckovProto.Spells
{
    [Serializable]
    public sealed class CastContext
    {
        public Transform caster;
        public AimLockController aimLock;
        public LayerMask enemyMask;
        public LayerMask wallMask;
        public LayerMask groundMask;
        public Camera cam;
        public Action<string> stateReporter;

        public Vector3 ResolveAimDirection()
        {
            if (aimLock != null && aimLock.HasLock)
            {
                return aimLock.GetLockedAimDirection(caster);
            }

            Vector3 fallback = caster != null ? caster.forward : Vector3.forward;
            fallback.y = 0f;
            if (fallback.sqrMagnitude <= 0.0001f && cam != null)
            {
                fallback = cam.transform.forward;
                fallback.y = 0f;
            }

            if (fallback.sqrMagnitude <= 0.0001f)
            {
                fallback = Vector3.forward;
            }

            return fallback.normalized;
        }

        public Vector3 ResolveCastOrigin(float heightOffset = 1f)
        {
            Vector3 origin = caster != null ? caster.position : Vector3.zero;
            origin.y += heightOffset;
            return origin;
        }

        public void ReportState(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                return;
            }

            stateReporter?.Invoke(message);
        }
    }
}
