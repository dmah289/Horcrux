using UnityEngine;

namespace Horcrux.Runtime.Implementations.Combo
{
    public static class ComboServiceHelper
    {
        public static float EvaluatePitchScale(int step, float basePitch, float semitonesPerStep, int maxStep)
        {
            return basePitch * Mathf.Pow(2f, Mathf.Clamp(step, 0, maxStep) * semitonesPerStep / 12f);
        }
    }
}