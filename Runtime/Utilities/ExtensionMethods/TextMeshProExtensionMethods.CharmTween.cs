using System.Threading;
using PrimeTween;
using TMPro;
using UnityEngine;

namespace Horcrux.Runtime.Utilities.ExtensionMethods
{
    public static class TextMeshProExtensionMethods
    {
        public static Tween CharmCount(this TMP_Text self, int start, int end, string format, float duration,
            float startDelay = 0f, CancellationToken ct = default)
        {
            return Tween.Custom(0f, 1f, duration, spell, Ease.Linear, startDelay: startDelay, useUnscaledTime: true)
                .SetCancellationToken(ct);

            void spell(float t) => self.SetText(format, Mathf.RoundToInt(Mathf.Lerp(start, end, t)));
        }
    }
}