using System.Runtime.CompilerServices;
using Unity.Mathematics;
using UnityEngine;

namespace Horcrux.Runtime.Utilities.PhysXHelper
{
    public static class Interpolator
    {
        /// <summary>Inverse of Lerp: t where v = a + (b - a) * t. Unclamped; returns 0 if a == b.</summary>
        /// <param name="a">Range start (maps to 0).</param>
        /// <param name="b">Range end (maps to 1).</param>
        /// <param name="v">Value to locate.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float InverseLerpUnclamped(float a, float b, float v)
        {
            float d = b - a;
            return d != 0f ? (v - a) / d : 0f;
        }
        
        /// <summary><see cref="InverseLerpUnclamped"/> for hot loops sharing one range: division becomes a multiplication. NaN if invRange isn't finite.</summary>
        /// <param name="a">Range start (maps to 0).</param>
        /// <param name="invRange">Precomputed 1 / (b - a).</param>
        /// <param name="v">Value to locate.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float InverseLerpUnclampedPrecomputed(float a, float invRange, float v)
            => (v - a) * invRange;

        /// <summary>Remap v from [fromMin, fromMax] to [toMin, toMax], keeping its relative position.</summary>
        /// <param name="v">Value in the source range.</param>
        /// <param name="clamp">Clamp result to target range. Pass a literal so the branch folds away (zero-cost).</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float Remap(float v, float fromMin, float fromMax, float toMin, float toMax, bool clamp = false)
        {
            float t = InverseLerpUnclamped(fromMin, fromMax, v);
            if (clamp) t = Mathf.Clamp01(t);
            return Mathf.LerpUnclamped(toMin, toMax, t);
        }

        /// <summary><see cref="Remap"/> for hot loops sharing one source range: division becomes a multiply. NaN if invFromRange isn't finite.</summary>
        /// <param name="invFromRange">Precomputed 1 / (fromMax - fromMin).</param>
        /// <param name="clamp">Clamp result to target range. Pass a literal so the branch folds away (zero-cost).</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float RemapPrecomputed(float v, float fromMin, float invFromRange, float toMin, float toMax,
            bool clamp = false)
        {
            float t = InverseLerpUnclampedPrecomputed(fromMin, invFromRange, v);
            if(clamp) t = Mathf.Clamp01(t);
            return Mathf.LerpUnclamped(toMin, toMax, t);
        }

        /// <summary>Perlin's quintic smoothstep: zero velocity and acceleration at both ends (smoother than cubic smoothstep).</summary>
        /// <remarks>
        /// S(t) = 6t^5 - 15t^4 + 10t^3 <br/>
        /// S'(t)  = 30t^2 * (t-1)^2      -> 0 at t=0,1 (velocity) <br/>
        /// S''(t) = 60t(t-1)(2t-1)       -> 0 at t=0,1 (acceleration) <br/>
        /// </remarks>
        /// <param name="t">Progress, clamped to [0,1].</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float SmootherStep(float t)
        {
            t = Mathf.Clamp01(t);
            return t * t * t * (t * (6f * t - 15f) + 10f);
        }

        /// <summary>
        /// Framerate-independent exponential move of a toward b.
        /// </summary>
        /// <remarks>
        /// Formula : x(t) = b + (a - b) * e^(-decay * dt)
        /// Half-life (time to cover half the gap) = ln(2) / decay.
        /// </remarks>
        /// <param name="a">Current value.</param>
        /// <param name="b">Target value.</param>
        /// <param name="decay">Convergence rate in 1/s.</param>
        /// <param name="dt">Elapsed time in seconds</param>
        /// <returns>Value moved toward b; never overshoots.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float ExpDecay(float a, float b, float decay, float dt)
        {
            if(decay <= 0f) return a; // No decay, no movement
            return b + (a - b) * math.exp(-decay * dt);
        }

        /// <summary>
        /// The framerate-independent interpolation factor t of <see cref="ExpDecay"/>: 
        /// ExpDecay(a, b, decay, dt) = Lerp(a, b, DecayFactor(decay, dt)).
        /// </summary>
        /// <remarks>Formula : t = 1 - e^(-decay * dt)</remarks>
        /// <param name="decay">Convergence rate in 1/s.</param>
        /// <param name="dt">Elapsed time in seconds</param>
        /// <returns>Blend factor in [0, 1) toward the target.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float DecayFactor(float decay, float dt)
        {
            if(decay <= 0f) return 0f; // No decay, no movement
            return 1 - math.exp(-decay * dt);
        }
    }
}