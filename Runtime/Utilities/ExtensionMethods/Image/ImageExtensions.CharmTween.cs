using System.Threading;
using Cysharp.Threading.Tasks;
using Horcrux.Runtime.Tweening.Easing;
using UnityEngine;
using UnityEngine.UI;

namespace Horcrux.Runtime.Utilities.ExtensionMethods
{
    public static partial class ImageExtensions
    {
        public static async UniTask CharmFillAmount(this Image self, float from, float to, float duration,
            CancellationToken ct = default)
        {
            float invDuration = 1f / Mathf.Max(duration, 0.0001f);
            float elapsed = 0f;

            try
            {
                while (elapsed < duration)
                {
                    elapsed += Time.unscaledDeltaTime;
                    self.fillAmount = Mathf.Lerp(from, to, Easer.Evaluate(EaseType.OutQuad, elapsed * invDuration));
                    await UniTask.Yield(PlayerLoopTiming.Update, ct);
                }
            }
            finally
            {
                self.fillAmount = to;
            }
        }
    }
}
