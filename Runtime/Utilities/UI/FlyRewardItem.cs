using System.Threading;
using Cysharp.Threading.Tasks;
using Horcrux.Runtime.Tweening.Easing;
using Horcrux.Runtime.Utilities.ExtensionMethods;
using PrimeTween;
using UnityEngine;
using UnityEngine.UI;

namespace Horcrux.Runtime.Utilities.UI
{
    public class FlyRewardItem : MonoBehaviour
    {
        #region Properties

        [Splitter("References")]
        [SerializeField] private RectTransform selfRect;
        [SerializeField] private Image icon;
        [SerializeField] private ParticleSystem burst;

        [Splitter("Configs")]
        [SerializeField] private EaseType moveEase;
        [SerializeField] private float duration = 0.5f;

        [SerializeField] private float targetAlpha = 0.3f;
        [SerializeField] private float fadeStartAtRatio = 0.6f;

        [SerializeField] private float burstAtRatio = 0.8f;

        // Flight state lives in fields so the per-frame callback is a static lambda: no closure per flight.
        private Vector3 _flyFrom;
        private Vector3 _flyTo;
        private bool _isBurstFired;

        #endregion Properties

        #region API

        /// <summary>Flies the icon from <paramref name="from"/> to <paramref name="to"/>; inactive again on every way out, cancel included.</summary>
        public async UniTask FlyAsync(Sprite sprite, Vector3 from, Vector3 to, CancellationToken ct)
        {
            icon.sprite = sprite;
            icon.SetAlpha(1);
            selfRect.position = from;
            selfRect.localScale = Vector3.one;
            _flyFrom = from;
            _flyTo = to;
            _isBurstFired = false;
            gameObject.SetActive(true);

            try
            {
                // One sequence, one token: cancel stops the punch together with the flight.
                await Sequence.Create(useUnscaledTime: true)
                    .Group(Tween.PunchScale(selfRect, Vector3.one * 0.2f, duration, frequency: 2, enableFalloff: false,
                        easeBetweenShakes: Ease.InOutSine, useUnscaledTime: true))
                    .Group(Tween.Custom(this, 0f, 1f, duration, static (self, t) => self.ApplyFlight(t), Ease.Linear,
                        useUnscaledTime: true))
                    .SetCancellationToken(ct);
            }
            finally
            {
                if (this != null)
                    gameObject.SetActive(false);
            }
        }

        #endregion API

        #region Class Methods

        private void ApplyFlight(float t)
        {
            selfRect.position = Vector3.LerpUnclamped(_flyFrom, _flyTo, Easer.Evaluate(moveEase, t));
            icon.SetAlpha(Mathf.Lerp(1f, targetAlpha, Mathf.InverseLerp(fadeStartAtRatio, 1f, t)));

            if (_isBurstFired || t < burstAtRatio)
                return;

            _isBurstFired = true;
            PlayBurst();
        }

        private void PlayBurst()
        {
            if (burst == null)
                return;

            burst.SetActive(false);
            burst.SetActive(true);
            burst.Play();
        }

        #endregion Class Methods
    }
}
