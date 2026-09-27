using System.Threading;
using Cysharp.Threading.Tasks;
using Horcrux.Runtime.Tweening.Easing;
using Horcrux.Runtime.Utilities.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace Horcrux.Runtime.Utilities.ExtensionMethods
{
    public static partial class ImageExtensions
    {
        public static async UniTask CharmFillAmount(this Image self, float from, float to,
            EaseType ease, float duration, CancellationToken ct)
        {
            await CharmTween.CastAsync(duration, ease, spell, ct, onComplete);
            
            void spell(float t)
            {
                self.fillAmount = Mathf.Lerp(from, to, Easer.Evaluate(ease, t));
            }
            
            void onComplete()
            {
                self.fillAmount = to;
            }
        }
    }
}
