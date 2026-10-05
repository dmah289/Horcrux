using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Horcrux.Runtime.Tweening.Easing;
using Horcrux.Runtime.Utilities.Common;
using Horcrux.Runtime.Utilities.PhysXHelper;
using Horcrux.Runtime.Utilities.Tweening;
using UnityEngine;

namespace Horcrux.Runtime.Utilities.ExtensionMethods
{
    public static partial class TransformExtensions
    {
        public static async UniTask CharmMove(this Transform self, Vector3 target, EaseType ease,
            float duration, float delaySeconds = 0f, CancellationToken ct = default,
            Action<Transform> onComplete = null)
        {
            Vector3 from = self.position;
            float invDuration = 1f / Mathf.Max(duration, 0.0001f);
            float elapsed = 0f;

            try
            {
                if (delaySeconds > 0f)
                    await UniTask.Delay((int)(delaySeconds * 1000f), DelayType.UnscaledDeltaTime, cancellationToken: ct);

                while (elapsed < duration)
                {
                    elapsed += Time.unscaledDeltaTime;
                    self.position = Vector3.LerpUnclamped(from, target, Easer.Evaluate(ease, elapsed * invDuration));
                    await UniTask.Yield(PlayerLoopTiming.Update, ct);
                }
            }
            finally
            {
                if (self != null)
                {
                    self.position = target;
                    onComplete?.Invoke(self);
                }
            }
        }
        
        public static async UniTask CharmPointAndBob(this Transform self, Vector3 target, Vector3 direction,
            float backupDistance, float targetPadding, EaseType ease, float oneWayDuration, CancellationToken ct)
        {
            Vector3 normalizedDirection = direction.normalized;
            Vector3 to = target - normalizedDirection * targetPadding;
            Vector3 from = to - normalizedDirection * backupDistance;
            float invOneWaySeconds = 1f / Mathf.Max(oneWayDuration, 0.0001f);
            float cyclePhase = 0f;

            while (true)
            {
                cyclePhase += Time.unscaledDeltaTime * invOneWaySeconds;
                while (cyclePhase >= 2f)
                    cyclePhase -= 2f;

                float oneWayRatio = cyclePhase < 1f ? cyclePhase : 2f - cyclePhase;
                self.position = Vector3.LerpUnclamped(from, to, Easer.Evaluate(ease, oneWayRatio));
                
                await UniTask.Yield(PlayerLoopTiming.Update);

                if (ct.IsCancellationRequested)
                    return;
            }
        }

        public static async UniTask CharmFlyArc(this Transform self, Vector3 target, ArcFlightSpec spec,
            CancellationToken ct)
        {
            Vector3 from = self.position;
            Vector3 controlPoint = BezierCurveHelper.ComputeControlPoint(from, target, spec.controlOffset);

            await CharmTween.CastAsync(spec.duration, EaseType.Linear, spell, ct, onComplete);
            
            void spell(float t)
            {
                float progressEased = Easer.Evaluate(spec.progressEase, t);
                self.position = BezierCurveHelper.EvaluateQuadraticBezier(progressEased, from, target, controlPoint);
                
                float scale = Mathf.LerpUnclamped(spec.startScale, spec.endScale, Easer.Evaluate(spec.scaleEase, t));
                self.localScale = Vector3.one * scale;
            }

            void onComplete()
            {
                if (self != null)
                {
                    self.position = target;
                    self.localScale = Vector3.one * spec.endScale;
                }
            }
        }
        
        public static async UniTask CharmFlyArc(this Transform self, Vector3 target, SquashStretchFlightSpec spec,
            CancellationToken ct)
        {
            Vector3 from = self.position;
            Vector3 controlPoint = BezierCurveHelper.ComputeControlPoint(from, target, spec.controlOffset);
            Vector3 landScale = SquashStretch.GetVolumePreservingScale(spec.endScale, AxisType.Y, CoordinateSystem.XY);

            try
            {
                await CharmTween.CastAsync(spec.progressDuration, spec.progressEase, flySpell, ct);

                if (!spec.recoverAfterLanding)
                    return;

                await CharmTween.CastAsync(spec.recoverDuration, EaseType.Linear, recoverSpell, ct);
            }
            finally
            {
                if (self != null)
                {
                    self.position = target;
                    self.localScale = spec.recoverAfterLanding ? spec.recoverScale * Vector3.one : landScale;
                }
            }
            
            
            void flySpell(float t)
            {
                self.position = BezierCurveHelper.EvaluateQuadraticBezier(t, from, target, controlPoint);
                
                self.localScale = SquashStretch.GetSquashStretch(t, EaseType.Linear, spec.startScale, spec.endScale,
                    AxisType.Y, CoordinateSystem.XY);
            }

            void recoverSpell(float t)
            {
                self.localScale = Vector3.LerpUnclamped(landScale, Vector3.one * spec.recoverScale, t);
            }
        }
    }
}
