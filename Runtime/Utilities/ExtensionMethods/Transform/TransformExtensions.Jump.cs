using System;
using PrimeTween;
using UnityEngine;

namespace Horcrux.Runtime.Utilities.ExtensionMethods
{
    public static partial class TransformExtensions
    {
        private static readonly Action<Transform, Vector3> SetJumpY = static (self, v) =>
        {
            Vector3 pos = self.position;
            pos.y = JumpArc.EvaluateHeight(v.x, v.z, v.y);
            self.position = pos;
        };

        private static readonly Action<Transform, Vector3> SetLocalJumpY = static (self, v) =>
        {
            Vector3 pos = self.localPosition;
            pos.y = JumpArc.EvaluateHeight(v.x, v.z, v.y);
            self.localPosition = pos;
        };

        public static Sequence CharmJump(this Transform self, Vector3 endValue, float height,
            float duration, Easing ease = default, bool useUnscaledTime = false)
        {
            Vector3 from = self.position;
            float arcCoefficient = JumpArc.ComputeArcCoefficient(from.y, endValue.y, height);

            return Sequence.Create(useUnscaledTime: useUnscaledTime)
                .Group(Tween.PositionX(self, from.x, endValue.x, duration, ease))
                .Group(Tween.PositionZ(self, from.z, endValue.z, duration, ease))
                .Group(Tween.Custom(self, new Vector3(from.y, 0f, arcCoefficient),
                    new Vector3(endValue.y, 1f, arcCoefficient), duration, SetJumpY, ease));
        }

        public static Sequence CharmLocalJump(this Transform self, Vector3 endValue, float height, float duration,
            Easing ease = default, bool useUnscaledTime = false)
        {
            Vector3 from = self.localPosition;
            float arcCoefficient = JumpArc.ComputeArcCoefficient(from.y, endValue.y, height);

            return Sequence.Create(useUnscaledTime: useUnscaledTime)
                .Group(Tween.LocalPositionX(self, from.x, endValue.x, duration, ease))
                .Group(Tween.LocalPositionZ(self, from.z, endValue.z, duration, ease))
                .Group(Tween.Custom(self, new Vector3(from.y, 0f, arcCoefficient),
                    new Vector3(endValue.y, 1f, arcCoefficient), duration, SetLocalJumpY, ease));
        }
    }
}
