using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Horcrux.Runtime.Tweening.Easing;
using Horcrux.Runtime.Utilities.PhysXHelper;
using Horcrux.Runtime.Utilities.Tweening;
using UnityEngine;

namespace Horcrux.Runtime.Utilities.ExtensionMethods
{
    public enum PunchStart
    {
        Dip, Overshoot
    }
    
    public static partial class TransformExtensions
    {
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
        
        public static UniTask CharmPunchScale(this Transform self, Vector3 restScale, float amplitude,
            float duration, CancellationToken ct, int cycles = 2, PunchStart punchStart = PunchStart.Dip)
        {
            float frequency = cycles * 0.5f;
            float signedAmplitude = punchStart == PunchStart.Dip ? -amplitude : amplitude;
            return CharmTween.CastAsync(duration, EaseType.Linear, spell, ct, onComplete);

            void spell(float t)
            {
                float wave = HarmonicOscillator.GetHarmonicDisplacement(WaveStyle.Sin, frequency, t);
                float displacement = signedAmplitude * wave;
                self.localScale = restScale * (1f + displacement);
            }

            void onComplete()
            {
                if(self != null)
                    self.localScale = restScale;
            }
        }
    }
}