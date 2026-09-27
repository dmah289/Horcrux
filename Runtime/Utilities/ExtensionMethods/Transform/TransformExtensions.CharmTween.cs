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
                if (self != null)
                {
                    self.position = target;
                    onComplete?.Invoke(self);
                }
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
                if (self != null)
                {
                    self.localScale = target;
                    onComplete?.Invoke(self);
                }
            }
        }
        
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
    }
}
