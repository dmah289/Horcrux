using System.Threading;
using Cysharp.Threading.Tasks;
using Horcrux.Runtime.Tweening.Easing;
using Horcrux.Runtime.Utilities.Tweening;
using UnityEngine;

namespace Horcrux.Runtime.Utilities.ExtensionMethods
{
    public static partial class RectTransformExtensions
    {
        public static async UniTask CharmSizeDelta(this RectTransform self, Vector2 targetSizeDelta,
            float duration, CancellationToken ct)
        {
            Vector2 from = self.sizeDelta;
            
            await CharmTween.CastAsync(duration, EaseType.Linear, spell, ct, onComplete);

            void spell(float t)
            {
                self.sizeDelta = Vector2.Lerp(from, targetSizeDelta, t);
            }

            void onComplete()
            {
                self.sizeDelta = targetSizeDelta;
            }
        }

        public static async UniTask CharmAnchoredPosition(this RectTransform self, Vector2 target, EaseType ease,
            float duration, CancellationToken ct)
        {
            Vector2 from = self.anchoredPosition;

            await CharmTween.CastAsync(duration, ease, spell, ct, onComplete);

            void spell(float t)
            {
                self.anchoredPosition = Vector2.LerpUnclamped(from, target, t);
            }

            void onComplete()
            {
                if (self != null)
                    self.anchoredPosition = target;
            }
        }
    }
}