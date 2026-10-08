using System.Threading;
using Horcrux.Runtime.Utilities.Common;
using Horcrux.Runtime.Utilities.PhysXHelper;
using Horcrux.Runtime.Utilities.Tweening;
using PrimeTween;
using UnityEngine;

namespace Horcrux.Runtime.Utilities.ExtensionMethods
{
    public static partial class TransformExtensions
    {
        public static Tween CharmPointAndBob(this Transform self, Vector3 target, Vector3 direction,
            float backupDistance, float targetPadding, Ease ease, float speed, CancellationToken ct)
        {
            Vector3 normalizedDirection = direction.normalized;
            Vector3 to = target - normalizedDirection * targetPadding;
            Vector3 from = to - normalizedDirection * backupDistance;
            float oneWayDuration = backupDistance / speed;

            return Tween.Position(self, from, to, oneWayDuration, ease,
                    cycles: -1, cycleMode: CycleMode.Rewind, useUnscaledTime: true)
                .SetCancellationToken(ct);
        }

        public static Tween CharmFlyArc(this Transform self, Vector3 target, ArcFlightSpec spec,
            CancellationToken ct)
        {
            Vector3 from = self.position;
            Vector3 controlPoint = BezierCurveHelper.ComputeControlPoint(from, target, spec.controlOffset, spec.controlAtRatio);

            float arcLength = BezierCurveHelper.ApproximateQuadraticBezierLength(from, target, controlPoint);
            float duration = arcLength / spec.speed;

            return Tween.Custom(0f, 1f, duration, spell, spec.progressEase, useUnscaledTime: true)
                .SetCancellationToken(ct);
            
            void spell(float t) => self.position = BezierCurveHelper.EvaluateQuadraticBezier(t, from, target, controlPoint);
        }
        
        public static Tween CharmSquashStretch(this Transform self, SquashStretchSpec spec, CancellationToken ct)
        {
            return Tween.Custom(0f, 1f, spec.duration, spell, spec.ease, useUnscaledTime: true)
                .SetCancellationToken(ct);

            void spell(float t) => self.localScale = SquashStretch.GetSquashStretch(t, spec.startScale, spec.endScale,
                AxisType.Y, CoordinateSystem.XY);
        }
    }
}
