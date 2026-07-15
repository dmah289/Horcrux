using UnityEngine;

namespace Horcrux.Runtime.Utilities.PhysXHelper
{
    public static class HarmonicOscillator
    {
        public enum WaveStyle
        {
            Sin, Cos
        }
        
        /// <summary>
        /// Calculates the instantaneous displacement of a simple harmonic oscillation using either Sin or Cos wave function.
        /// <para>Formula (Sin): x = A * sin(ω * t + ϕ) -> Start at 0 (Equilibrium) when t = 0, ϕ = 0 </para>>
        /// <para>Formula (Cos): x = A * cos(ω * t + ϕ) -> Start at +A (Positive amplitude) when t = 0, ϕ = 0</para>>
        /// </summary>
        /// <param name="frequency">The ordinary frequency (f) in Hz. Used to calculate angular frequency ω = 2.π.f.</param>
        /// <param name="time">The current time in seconds. </param>
        /// <param name="amplitude">Maximum absolute displacement from the equilibrium.</param>
        /// <param name="phaseShift">The initial phase angle in radians. Defines the starting state of the cycle at t = 0.</param>
        /// <returns>The instantaneous displacement bounded within the range [-A, A] </returns>
        public static float GetHarmonicDisplacement(
            WaveStyle waveStyle,
            float frequency,
            float time,
            float amplitude = 1f,
            float phaseShift = 0f)
        {
            float omega = 2f * Mathf.PI * frequency;
            float currPhase = omega * time + phaseShift;
            
            if(waveStyle == WaveStyle.Sin)
                return amplitude * Mathf.Sin(currPhase);
            
            return amplitude * Mathf.Cos(currPhase);
        }
    }
}