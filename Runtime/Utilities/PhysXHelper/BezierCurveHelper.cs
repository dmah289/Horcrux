using System.Runtime.CompilerServices;
using UnityEngine;

namespace Horcrux.Runtime.Utilities.PhysXHelper
{
    public static class BezierCurveHelper
    {
        /// <summary>
        /// Formula : u²·from + 2u·t·control + t²·target
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector3 EvaluateQuadraticBezier(float t, Vector3 from, Vector3 target, Vector3 controlPoint)
        {
            float u = 1f - t;
            return u * u * from + 2f * u * t * controlPoint + t * t * target;
        }

        /// <summary>
        /// Get center point on straight vector then shift by perpendicular direction
        /// </summary>
        public static Vector3 ComputeControlPoint(Vector3 from, Vector3 target, float controlOffset)
        {
            Vector3 straightDirection = target - from;
            // Rotate straight vector 90 degrees
            Vector3 perpendicularDirection = new Vector3(-straightDirection.y, straightDirection.x, 0).normalized;
            return (from + target) * 0.5f + perpendicularDirection * controlOffset;
        }
    }
}