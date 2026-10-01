using System.Threading;
using Cysharp.Threading.Tasks;
using Horcrux.Runtime.Tweening.Easing;
using Horcrux.Runtime.Utilities.Tweening;
using TMPro;
using UnityEngine;

namespace Horcrux.Runtime.Utilities.ExtensionMethods
{
    public static class TextMeshProExtensionMethods
    {
        public static async UniTask CharmCount(this TextMeshProUGUI self, int start, int end, string format, EaseType easeType, float duration,
            float delaySeconds = 0f, CancellationToken ct = default)
        {
            try
            {
                if (delaySeconds > 0f)
                    await UniTask.Delay((int)(delaySeconds * 1000f), DelayType.UnscaledDeltaTime,
                        cancellationToken: ct);

                await CharmTween.CastAsync(duration, easeType, spell, ct);
            }
            finally
            {
                if (self != null)
                    self.SetText(format, end);
            }

            void spell(float t)
            {
                self.SetText(format, Mathf.RoundToInt(Mathf.Lerp(start, end, t)));
            }
        }
    }
}