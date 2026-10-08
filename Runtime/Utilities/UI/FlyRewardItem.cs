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

        #region API

        public async UniTask FlyAsync(Sprite sprite, Vector3 from, Vector3 to, CancellationToken ct)
        {
            icon.sprite = sprite;
            icon.SetAlpha(1);
            selfRect.position = from;
            selfRect.localScale = Vector3.one;
            gameObject.SetActive(true);
            bool burstFired = false;

            await UniTask.WhenAll(
                Tween.PunchScale(selfRect, Vector3.one * 0.2f, duration, frequency: 2, enableFalloff: false,
                    easeBetweenShakes: Ease.InOutSine, useUnscaledTime: true),
                Tween.Custom(0f, 1f, duration, FlySequence, Ease.Linear, useUnscaledTime: true)
                    .SetCancellationToken(ct));
            
            gameObject.SetActive(false);

            void FlySequence(float t)
            {
                selfRect.position = Vector3.LerpUnclamped(from, to, Easer.Evaluate(moveEase, t));
                icon.SetAlpha(Mathf.Lerp(1f, targetAlpha, Mathf.InverseLerp(fadeStartAtRatio, 1f, t)));
                
                if(!burstFired && t >= burstAtRatio)
                {
                    burstFired = true;
                    PlayBurst();
                }
            }
        }

        #endregion
        
        #region Class Methods

        private void PlayBurst()
        {
            if (burst == null)
                return;

            burst.SetActive(false);
            burst.SetActive(true);
            burst.Play();
        }

        #endregion
    }
}