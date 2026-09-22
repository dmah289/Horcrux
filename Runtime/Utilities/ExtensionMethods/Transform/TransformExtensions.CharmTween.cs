using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Horcrux.Runtime.Tweening.Easing;
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
                self.position = target;
                onComplete?.Invoke(self);
            }
        }

        public static async UniTask CharmScale(this Transform self, Vector3 target, EaseType ease,
            float duration, float delaySeconds = 0f, CancellationToken ct = default,
            Action<Transform> onComplete = null)
        {
            Vector3 from = self.localScale;
            float invDuration = 1f / Mathf.Max(duration, 0.0001f);
            float elapsed = 0f;

            try
            {
                if (delaySeconds > 0f)
                    await UniTask.Delay((int)(delaySeconds * 1000f), DelayType.UnscaledDeltaTime, cancellationToken: ct);

                while (elapsed < duration)
                {
                    elapsed += Time.unscaledDeltaTime;
                    self.localScale = Vector3.LerpUnclamped(from, target, Easer.Evaluate(ease, elapsed * invDuration));
                    await UniTask.Yield(PlayerLoopTiming.Update, ct);
                }
            }
            finally
            {
                self.localScale = target;
                onComplete?.Invoke(self);
            }
        }

        // formula(timeRatio, easedRatio) owns the shape, so there is no end scale to snap to.
        public static async UniTask CharmScale(this Transform self, EaseType ease, float duration,
            Func<float, float, Vector3> formula, float delaySeconds = 0f, CancellationToken ct = default,
            Action<Transform> onComplete = null)
        {
            float invDuration = 1f / Mathf.Max(duration, 0.0001f);
            float elapsed = 0f;

            try
            {
                if (delaySeconds > 0f)
                    await UniTask.Delay((int)(delaySeconds * 1000f), DelayType.UnscaledDeltaTime, cancellationToken: ct);

                while (elapsed < duration)
                {
                    elapsed += Time.unscaledDeltaTime;
                    float timeRatio = elapsed * invDuration;
                    self.localScale = formula(timeRatio, Easer.Evaluate(ease, timeRatio));
                    await UniTask.Yield(PlayerLoopTiming.Update, ct);
                }
            }
            finally
            {
                onComplete?.Invoke(self);
            }
        }
    }
}
