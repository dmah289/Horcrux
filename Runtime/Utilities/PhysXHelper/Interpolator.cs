using UnityEngine;

namespace Horcrux.Runtime.Utilities.PhysXHelper
{
    public static class Interpolator
    {
        /// <summary>
        /// Calculate the unclamped ratio of a value between two bounds.
        /// </summary>
        /// <param name="a">Start value</param>
        /// <param name="b">End value</param>
        /// <param name="v">Current value</param>
        /// <returns>Current value's unclamped ratio from start to end</returns>
        public static float InverseLerpUnclamped(float a, float b, float v)
        {
            float d = b - a;
            return d != 0f ? (v - a) / d : 0f;
        }
        
        /// <summary>
        /// Calculate the unclamped ratio of a value between two bounds, with precomputed inverse range.
        /// </summary>
        /// <remarks>Caller must guarantee that invRange != infinity by itself, otherwise the result will be NaN</remarks>
        /// <param name="a">Start value</param>
        /// <param name="invRange">Inverse of range</param>
        /// <param name="v">Current value</param>
        /// <returns> Current value's unclamped ratio from start to end</returns>
        public static float InverseLerpUnclampedPrecomputed(float a, float invRange, float v)
            => (v - a) * invRange;

        /// <summary>
        /// Remap a value from one range to another
        /// </summary>
        /// <param name="v">Current value's from range</param>
        /// <param name="fromMin">Min value in from_range</param>
        /// <param name="fromMax">Max value in from_range</param>
        /// <param name="toMin">Min value in to_range</param>
        /// <param name="toMax">Max value in to_range</param>
        /// <param name="clamp">If true, allow clamping result value</param>
        /// <returns>Mapped value in to_range from from_range</returns>
        public static float Remap(float v, float fromMin, float fromMax, float toMin, float toMax, bool clamp = false)
        {
            float t = InverseLerpUnclamped(fromMin, fromMax, v);
            if (clamp) t = Mathf.Clamp01(t);
            return Mathf.LerpUnclamped(toMin, toMax, t);
        }

        /// <summary>
        /// Remap a value from one range to another
        /// </summary>
        /// <remarks>Caller must guarantee that invFromRange != infinity by itself, otherwise the result will be NaN</remarks>
        /// <param name="v">Current value's from range</param>
        /// <param name="fromMin">Min value in from_range</param>
        /// <param name="invFromRange">Inverse of from_range</param>
        /// <param name="toMin">Min value in to_range</param>
        /// <param name="toMax">Max value in to_range</param>
        /// <param name="clamp">If true, allow clamping result value</param>
        /// <returns>Mapped value in to_range from from_range</returns>
        public static float RemapPrecomputed(float v, float fromMin, float invFromRange, float toMin, float toMax,
            bool clamp = false)
        {
            float t = InverseLerpUnclampedPrecomputed(fromMin, invFromRange, v);
            if(clamp) t = Mathf.Clamp01(t);
            return Mathf.LerpUnclamped(toMin, toMax, t);
        }
    }
}