using System.Runtime.CompilerServices;
using UnityEngine;

namespace Horcrux.Runtime.Utilities.ExtensionMethods
{
    public static class JumpArc
    {
        public static float ComputeArcCoefficient(float fromY, float toY, float height)
            => toY - fromY + 4f * height;
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float EvaluateHeight(float linearY, float arcCoefficient, float t)
            => linearY + arcCoefficient * t * (1f - t);

        public static Vector3 Evaluate(Vector3 from, Vector3 to, float height, float t)
        {
            Vector3 pos = Vector3.LerpUnclamped(from, to, t);
            pos.y = EvaluateHeight(pos.y, ComputeArcCoefficient(from.y, to.y, height), t);
            return pos;
        }
    }
}