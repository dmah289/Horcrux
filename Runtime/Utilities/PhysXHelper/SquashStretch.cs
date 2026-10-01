using System.Runtime.CompilerServices;
using Horcrux.Runtime.Utilities.Common;
using Horcrux.Runtime.Tweening.Easing;
using Unity.Mathematics;
using UnityEngine;

namespace Horcrux.Runtime.Utilities.PhysXHelper
{
    /// <summary>
    /// Deform to preserve volume. Product of scale factors = 1.
    /// <remarks>Formula : c = s^(-1/n). s = Scale of the primary axis, n = Number of complement axes.</remarks>
    /// </summary>
    public static class SquashStretch
    {
        private const float MinScale = 1e-4f;
        private const float MaxScale = 1e4f;

        /// <summary>
        /// Calculate volume-preserving scale derived from the on main axis.
        /// </summary>
        /// <param name="stretchScale">Scale factor of the primary axis.</param>
        /// <param name="stretchedAxis">Primary axis to scale</param>
        /// <param name="coordinateSystem">Coordinate system to calculate the complement.</param>
        /// <returns>Volume-preserving scale.</returns>
        public static Vector3 GetVolumePreservingScale(float stretchScale, 
            AxisType stretchedAxis, CoordinateSystem coordinateSystem)
        {
            // min(max(..)) not math.clamp: clamp is max(lo, min(hi, x)) and math.min(hi, NaN) returns hi,
            // so NaN would land on the ceiling (10^4x blow-up). This order sends NaN to the floor
            // (invisible sliver) while the outer min still caps +∞ before it reaches localScale.
            stretchScale = math.min(math.max(stretchScale, MinScale), MaxScale);
            float compScale = coordinateSystem.Is2D() ? 1f / stretchScale 
                : math.rsqrt(stretchScale);

            switch (coordinateSystem)
            {
                case CoordinateSystem.XY:
                    switch (stretchedAxis)
                    {
                        case AxisType.X : return new Vector3(stretchScale, compScale, 1f);
                        case AxisType.Y : return new Vector3(compScale, stretchScale, 1f);
                        default: return Vector3.one;
                    }

                case CoordinateSystem.XZ:
                    switch (stretchedAxis)
                    {
                        case AxisType.X : return new Vector3(stretchScale, 1f, compScale);
                        case AxisType.Z : return new Vector3(compScale, 1f, stretchScale);
                        default: return Vector3.one;
                    }

                case CoordinateSystem.YZ:
                    switch (stretchedAxis)
                    {
                        case AxisType.Y : return new Vector3(1f, stretchScale, compScale);
                        case AxisType.Z : return new Vector3(1f, compScale, stretchScale);
                        default: return Vector3.one;
                    }

                default: // XYZ
                    switch (stretchedAxis)
                    {
                        case AxisType.X : return new Vector3(stretchScale, compScale, compScale);
                        case AxisType.Y : return new Vector3(compScale, stretchScale, compScale);
                        default: return new Vector3(compScale, compScale, stretchScale);
                    }
            }
        }
        
        /// <summary>
        /// Squash based on impact intensity.
        /// </summary>
        /// <remarks>Formula: s = lerp(1, minScale, saturate(impact/maxImpact)).</remarks>
        /// <param name="impact">Impact intensity.</param>
        /// <param name="maxImpact">Impact intensity threshold for <see cref="minScale"/>>.</param>
        /// <param name="minScale">Min scale factor of primary axis.</param>
        /// <returns>Volume-preserving scale based on impact intensity.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector3 GetSquashFromImpact(float impact, float maxImpact, float minScale,
            AxisType primaryAxis, CoordinateSystem coordinateSystem)
        {
            float normalizedImpact = maxImpact > 0f ? math.saturate(impact / maxImpact) : 0f;
            float s = math.lerp(1f, minScale, normalizedImpact);
            return GetVolumePreservingScale(s, primaryAxis, coordinateSystem);
        }
        
        /// <summary>
        /// Stretch based on speed.
        /// </summary>
        /// <remarks>Formula: s = 1 + clamp(speed*stretchPerSpeed, 0, maxStretch-1)</remarks>
        /// <param name="speed"></param>
        /// <param name="stretchPerSpeed"></param>
        /// <param name="maxStretch"></param>
        /// <returns>Volume-preserving scale based on speed.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector3 GetDirectionalStretch(float speed, float stretchPerSpeed, float maxStretch,
            AxisType primaryAxis, CoordinateSystem coordinateSystem)
        {
            float maxExtra = math.max(maxStretch - 1f, 0f);
            float extra = math.clamp(speed * stretchPerSpeed, 0f, maxExtra);
            float s = 1f + extra;
            return GetVolumePreservingScale(s, primaryAxis, coordinateSystem);
        }

        /// <summary>
        /// Squash-Stretch-Idle based on time progress (must use unclamped ease).<br/>
        /// Only Stretch with OutBack/OutElastic.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector3 GetSquashStretch(float t, EaseType easeType, float startScale, float endScale,
            AxisType primaryAxis, CoordinateSystem coordinateSystem)
        {
            float eased = Easer.Evaluate(easeType, t);
            float s = math.lerp(startScale, endScale, eased);
            return GetVolumePreservingScale(s, primaryAxis, coordinateSystem);
        }
    }
}