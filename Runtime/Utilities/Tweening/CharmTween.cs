using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Horcrux.Runtime.Tweening.Easing;
using UnityEngine;

namespace Horcrux.Runtime.Utilities.Tweening
{
    public static class CharmTween
    {
        public static async UniTask CastAsync(float durationSeconds, EaseType ease,
            Action<float> spell, CancellationToken ct)
        {
            float invDurationSeconds = 1f / Mathf.Max(durationSeconds, 0.0001f);
            float t = 0f;

            while (t < 1f)
            {
                t += Time.unscaledDeltaTime * invDurationSeconds;
                spell(Easer.Evaluate(ease, t));
                await UniTask.Yield(PlayerLoopTiming.Update, ct);
            }
        }
    }
}