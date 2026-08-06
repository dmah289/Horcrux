using System.Runtime.CompilerServices;
using Unity.Mathematics;

namespace Horcrux.Runtime.Utilities.AudioHelper
{
    /// <summary>
    /// AudioSource.pitch is play speed.
    /// 12 semitones equal a doubled frequency, or 1 octave.
    /// </summary>
    public static class AudioPitchHelper
    {
        private const float InvSemitonesPerOctave = 1f / 12f;

        /// <summary>
        /// Convert semitone distance into pitch factor.
        /// </summary>
        /// <remarks>Formula: ratio = 2^(semitones/12).</remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float SemitonesToRatio(float semitones)
            => math.exp2(semitones * InvSemitonesPerOctave);
        
        /// <summary>
        /// Calculate final pitch value for the combo.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float GetRampedPitch(int step, float semitonesPerStep, float maxSemitones, float basePitch = 1f)
            => basePitch * SemitonesToRatio(math.min(step * semitonesPerStep, maxSemitones));
        
        /// <summary>
        /// Calculate random pitch deviation symmetric around the base pitch.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float GetDetunedPitch(float signedUnit, float rangeSemitones, float basePitch = 1f)
            => basePitch * SemitonesToRatio(math.clamp(signedUnit, -1f, 1f) * rangeSemitones);
    }
}