using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Horcrux.Runtime.Utilities.ExtensionMethods
{
    public static class CanvasGroupExtensions
    {
        public static async UniTask CharmAlpha(this CanvasGroup self, float target, float duration,
            CancellationToken ct, float delay = 0f, Action onComplete = null)
        {
            float elapsed = 0;
            float from = self.alpha;
            float invDuration = 1.0f / Mathf.Max(duration, 0.0001f);
            
            try
            {
                if(delay > 0f)
                    await UniTask.Delay((int)(delay * 1000), DelayType.UnscaledDeltaTime, cancellationToken: ct);

                while (elapsed < duration)
                {
                    elapsed += Time.unscaledDeltaTime;
                    self.alpha = Mathf.Lerp(from, target, elapsed * invDuration);
                    await UniTask.Yield(PlayerLoopTiming.Update, ct);
                }
            }
            finally
            {
                self.alpha = target;
                onComplete?.Invoke();
            }
        }
    }
}