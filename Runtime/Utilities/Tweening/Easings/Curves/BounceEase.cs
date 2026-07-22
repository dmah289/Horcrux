namespace Horcrux.Runtime.Tweening.Easing
{
    internal static class BounceEase
    {
        private const float N1 = 7.5625f;
        private const float D1 = 2.75f;

        public static float In(float t) => 1f - Out(1f - t);

        public static float Out(float t)
        {
            if (t < 1f / D1)
            {
                return N1 * t * t;
            }
            if (t < 2f / D1)
            {
                t -= 1.5f / D1;
                return N1 * t * t + 0.75f;
            }
            if (t < 2.5f / D1)
            {
                t -= 2.25f / D1;
                return N1 * t * t + 0.9375f;
            }
            t -= 2.625f / D1;
            return N1 * t * t + 0.984375f;
        }

        public static float InOut(float t)
            => t < 0.5f
                ? (1f - Out(1f - 2f * t)) * 0.5f
                : (1f + Out(2f * t - 1f)) * 0.5f;
    }
}
