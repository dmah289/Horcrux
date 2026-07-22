namespace Horcrux.Runtime.Tweening.Easing
{
    public static class Easer
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
        public static float Evaluate(EaseType easeType, float t)
        {
            if (t < 0f) t = 0f;
            if (t > 1f) t = 1f;

            return easeType switch
            {
                EaseType.Linear => t,
                
                EaseType.InQuad => QuadEase.In(t),
                EaseType.OutQuad => QuadEase.Out(t),
                EaseType.InOutQuad => QuadEase.InOut(t),
                
                EaseType.InCubic => CubicEase.In(t),
                EaseType.OutCubic => CubicEase.Out(t),
                EaseType.InOutCubic => CubicEase.InOut(t),

                EaseType.InQuart => QuartEase.In(t),
                EaseType.OutQuart => QuartEase.Out(t),
                EaseType.InOutQuart => QuartEase.InOut(t),

                EaseType.InQuint => QuintEase.In(t),
                EaseType.OutQuint => QuintEase.Out(t),
                EaseType.InOutQuint => QuintEase.InOut(t),

                EaseType.InSine => SineEase.In(t),
                EaseType.OutSine => SineEase.Out(t),
                EaseType.InOutSine => SineEase.InOut(t),

                EaseType.InExpo => ExpoEase.In(t),
                EaseType.OutExpo => ExpoEase.Out(t),
                EaseType.InOutExpo => ExpoEase.InOut(t),

                EaseType.InCirc => CircEase.In(t),
                EaseType.OutCirc => CircEase.Out(t),
                EaseType.InOutCirc => CircEase.InOut(t),

                EaseType.InBack => BackEase.In(t),
                EaseType.OutBack => BackEase.Out(t),
                EaseType.InOutBack => BackEase.InOut(t),

                EaseType.InElastic => ElasticEase.In(t),
                EaseType.OutElastic => ElasticEase.Out(t),
                EaseType.InOutElastic => ElasticEase.InOut(t),

                EaseType.InBounce => BounceEase.In(t),
                EaseType.OutBounce => BounceEase.Out(t),
                EaseType.InOutBounce => BounceEase.InOut(t),

                _ => t
            };
        }
    }
}