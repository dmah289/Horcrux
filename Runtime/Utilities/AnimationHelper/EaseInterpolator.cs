namespace Horcrux.Runtime.Utilities.AnimationHelper
{
    public static class EaseInterpolator
    {
        /// <summary>
        /// Compute the interpolation coefficient.
        /// </summary>
        /// <remarks>
        /// The input parameter <paramref name="t"/> is internally clamped between [0,1].<br/>
        /// Highly recommended to handle the <c>t >= 1</c> condition before calling this function.<br/>
        /// Use (<c>t = elapsed * inverseDuration</c>) instead of (<c>t = elapsed / duration</c>).
        /// </remarks>
        /// <param name="t">The normalized time progression.</param>
        /// <returns>The interpolated value may exceed the [0,1] range, must use unclamped interpolation methods.</returns>
        public static float GetEaseValue(EaseType easeType, float t)
        {
            if (t < 0f) t = 0f;
            if (t > 1f) t = 1f;

            return easeType switch
            {
                EaseType.Linear => t,
                
                EaseType.InQuad => QuadEase.In(t),
                
                _ => t
            };
        }
    }
}