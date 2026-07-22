using UnityEngine;

namespace Horcrux.Runtime.Tweening.Easing
{
    internal static class ExpoEase
    {
        public static float In(float t)
            => t <= 0f ? 0f : Mathf.Pow(2f, 10f * t - 10f);

        public static float Out(float t)
            => t >= 1f ? 1f : 1f - Mathf.Pow(2f, -10f * t);

        public static float InOut(float t)
        {
            if (t <= 0f) return 0f;
            if (t >= 1f) return 1f;
            return t < 0.5f
                ? Mathf.Pow(2f, 20f * t - 10f) * 0.5f
                : (2f - Mathf.Pow(2f, -20f * t + 10f)) * 0.5f;
        }
    }
}
