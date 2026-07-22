namespace Horcrux.Runtime.Tweening.Easing
{
    internal static class QuintEase
    {
        public static float In(float t)
        {
            float t2 = t * t;
            return t2 * t2 * t;
        }

        public static float Out(float t)
        {
            float f = t - 1f;
            float f2 = f * f;
            return 1f + f2 * f2 * f;
        }

        public static float InOut(float t)
        {
            if (t < 0.5f)
            {
                float t2 = t * t;
                return 16f * t2 * t2 * t;
            }
            float f = -2f * t + 2f;
            float f2 = f * f;
            return 1f - f2 * f2 * f * 0.5f;
        }
    }
}
