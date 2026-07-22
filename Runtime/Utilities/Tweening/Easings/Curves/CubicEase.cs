namespace Horcrux.Runtime.Tweening.Easing
{
    internal static class CubicEase
    {
        public static float In(float t) => t * t * t;

        public static float Out(float t)
        {
            float f = t - 1f;
            return f * f * f + 1f;
        }

        public static float InOut(float t)
        {
            if (t < 0.5f) return 4f * t * t * t;
            float f = -2f * t + 2f;
            return 1f - f * f * f * 0.5f;
        }
    }
}