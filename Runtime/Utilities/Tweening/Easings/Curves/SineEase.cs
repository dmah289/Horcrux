using UnityEngine;

namespace Horcrux.Runtime.Tweening.Easing
{
    internal static class SineEase
    {
        private const float HalfPi = Mathf.PI * 0.5f;

        public static float In(float t) => 1f - Mathf.Cos(t * HalfPi);

        public static float Out(float t) => Mathf.Sin(t * HalfPi);

        public static float InOut(float t) => -(Mathf.Cos(Mathf.PI * t) - 1f) * 0.5f;
    }
}
