namespace Horcrux.Runtime.Horcrux.Runtime.Utilities.AnimationHelper
{
    public enum EaseType
    {
        Linear,
        EaseIn,
        OutQuad,
        EaseInOut
    }
    
    public static class EaseInterpolator
    {
        
        /// <summary>
        /// Compute the interpolation coefficient.
        /// </summary>
        /// <remarks>
        /// The input parameter <paramref name="t"/> is internally clamped between [0,1].<br/>
        /// Highly recommended to handle the <c>t >= 1</c> condition before calling this function.<br/>
        /// Use multiplication with the inverse duration (<c>t = elapsed * invDuration</c>) instead of division.
        /// </remarks>
        /// <param name="t">The normalized time progression.</param>
        /// <returns>The interpolated value may exceed the [0,1] range, must use unclamped interpolation methods.</returns>
        public static float GetEaseValue(EaseType easeType, float t)
        {
            if (t > 1f) t = 1f;
            else if (t < 0f) t = 0f;
            
            switch (easeType)
            {
                case EaseType.Linear:
                    return t;
                case EaseType.EaseIn:
                    return t * t;
                case EaseType.OutQuad:
                    return t * (2 - t);
                case EaseType.EaseInOut:
                    return t < 0.5f ? 2 * t * t : -1 + (4 - 2 * t) * t;
                default:
                    return t;
            }
        }
    }
}