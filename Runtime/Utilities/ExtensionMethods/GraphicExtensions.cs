using System.Threading;
using Cysharp.Threading.Tasks;
using Horcrux.Runtime.Tweening.Easing;
using Horcrux.Runtime.Utilities.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace Horcrux.Runtime.Utilities.ExtensionMethods
{
    public static class GraphicExtensions
    {
        public static void SetAlpha(this Graphic self, float alpha)
        {
            if (self == null)
                return;

            Color color = self.color;
            color.a = alpha;
            self.color = color;
        }
        
        public static async UniTask CharmAlpha(this Graphic self, float from, float to,
            EaseType ease, float duration, CancellationToken ct)
        {
            await CharmTween.CastAsync(duration, ease, spell, ct, onComplete);
            
            void spell(float t)
            {
                self.SetAlpha(Mathf.Lerp(from, to, Easer.Evaluate(ease, t)));
            }
            
            void onComplete()
            {
                self.SetAlpha(to);
            }
        }
    }
}