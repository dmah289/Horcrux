using System.Runtime.CompilerServices;
using Unity.Mathematics;
using UnityEngine;

namespace Horcrux.Runtime.Utilities.PhysXHelper
{
    /// <summary>
    /// Harmonic Oscillator multiply exponential envelope, amplitude decays to 0. 
    /// </summary>
    public static class DampedOscillator
    {
        private const float Ln2 = 0.6931472f;
        
        /// <summary>
        /// Calculate oscillator's current envelope amplitude.
        /// </summary>
        /// <remarks>Formula : E = A * e^(-λt).</remarks>>
        /// <param name="decay">Decay factor (1/sec). If non-positive value -> return amplitude.</param>
        /// <param name="amplitude">Initial amplitude.</param>
        /// <returns>Current envelope amplitude.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float GetEnvelope(float decay, float time, float amplitude = 1f)
        {
            if (decay <= 0f) return amplitude;
            if (time <= 0f) return amplitude;
            return amplitude * math.exp(-decay * time);
        }

        /// <summary>
        /// Calculate instantaneous displacement of damped oscillator.
        /// </summary>
        /// <remarks>Formula : x(t) = A * e^(−λt) * cos(ωt+φ).</remarks>
        /// <param name="waveStyle">Cos: Start from initial amplitude, Sin: Start from equilibrium (if phaseShift = 0).</param>
        /// <param name="decay">Decay factor λ (1/sec). Non-positive value -> Harmonic Oscillator.</param>
        /// <param name="amplitude">Initial amplitude.</param>
        /// <param name="phaseShift">Initial phase (radian).</param>
        /// <returns>Instantaneous displacement at time t, in the range of [-E,E].</returns>
        public static float GetDisplacement(WaveStyle waveStyle, float frequency, float decay, float time,
            float amplitude = 1f, float phaseShift = 0f)
        {
            float omega = 2 * math.PI * frequency;
            float envelope = GetEnvelope(decay, time, amplitude);
            float phase = omega * time + phaseShift;
            
            return waveStyle == WaveStyle.Cos ? envelope * math.cos(phase) : envelope * math.sin(phase);
        }
        
        /// <summary>
        /// Instantaneous velocity (Derivative of displacement by time) of the damping oscillator.
        /// </summary>
        /// <remarks>
        /// Cos: v(t) = A*e^(−λt) * [-λ*cos(ωt+φ) - ω*sin(ωt+φ)].<br/>
        /// Sin: v(t) = A*e^(−λt) * [-λ*sin(ωt+φ) + ω*cos(ωt+φ)].<br/>
        /// </remarks>
        /// <param name="waveStyle">waveStyle used for displacement.</param>
        /// <param name="decay">Decay factor λ (1/sec). Non-positive value -> Harmonic Oscillator.</param>
        /// <param name="amplitude">Initial amplitude.</param>
        /// <param name="phaseShift">Initial phase.</param>
        /// <returns>Instantaneous velocity at time t.</returns>
        public static float GetVelocity(WaveStyle waveStyle, float frequency, float decay, float time,
            float amplitude = 1f, float phaseShift = 0f)
        {
            float lambda = math.max(decay, 0f);
            float omega = 2 * math.PI * frequency;
            float envelope = GetEnvelope(decay, time, amplitude);
            float phase = omega * time + phaseShift;
            float c = math.cos(phase);
            float s = math.sin(phase);

            return waveStyle == WaveStyle.Cos ? envelope * (-lambda * c - omega * s)
                : envelope * (-lambda * s + omega * c);
        }

        /// <summary>
        /// The time for the envelope to drop below a threshold ratio of A, considered stopped.
        /// </summary>
        /// <remarks>Formula : t* = -ln(threshold) / λ.</remarks>
        /// <param name="decay">Decay factor λ (1/sec). Non-positive value -> Harmonic Oscillator</param>
        /// <param name="threshold">Threshold ratio in range of (0,1)</param>
        /// <returns>Time to settle down.</returns>>
        public static float GetSettlingTime(float decay, float threshold = 0.05f)
        {
            if (decay <= 0f) return float.PositiveInfinity;
            if(threshold <= 0f) return float.PositiveInfinity;
            if (threshold >= 1f) return 0f;

            return -math.log(threshold) / decay;
        }

        /// <summary><see cref="GetDisplacement"/> Pass inverse of half lifetime instead of decay.</summary>
        /// <param name="invHalfLife">Inverse of the time for amplitude to halve (1/halfLife). ≤ 0 → no decay (harmonic oscillator).</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float GetDisplacementHalfLife(WaveStyle waveStyle, float frequency, float invHalfLife, float time,
            float amplitude = 1f, float phaseShift = 0f)
            => GetDisplacement(waveStyle, frequency, Ln2 * invHalfLife, time, amplitude, phaseShift);

        /// <summary><see cref="GetVelocity"/> Pass inverse of half lifetime instead of decay.</summary>
        /// <param name="invHalfLife">Inverse of the time for amplitude to halve (1/halfLife). ≤ 0 → no decay (harmonic oscillator).</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float GetVelocityHalfLife(WaveStyle waveStyle, float frequency, float invHalfLife, float time,
            float amplitude = 1f, float phaseShift = 0f)
            => GetVelocity(waveStyle, frequency, Ln2 * invHalfLife, time, amplitude, phaseShift);

        /// <summary><see cref="GetEnvelope"/> Pass inverse of half lifetime instead of decay.</summary>
        /// <param name="invHalfLife">Inverse of the time for amplitude to halve (1/halfLife). ≤ 0 → no decay (returns amplitude).</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float GetEnvelopeHalfLife(float invHalfLife, float time, float amplitude = 1f)
            => GetEnvelope(Ln2 * invHalfLife, time, amplitude);

        /// <summary><see cref="GetSettlingTime"/> Pass inverse of half lifetime instead of decay.</summary>
        /// <param name="invHalfLife">Inverse of the time for amplitude to halve (1/halfLife). ≤ 0 → never settles (+∞).</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float GetSettlingTimeHalfLife(float invHalfLife, float threshold = 0.05f)
            => GetSettlingTime(Ln2 * invHalfLife, threshold);
    }
}