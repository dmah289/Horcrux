using System.Threading;
using Cysharp.Threading.Tasks;
using Horcrux.Runtime.Tweening.Easing;
using Horcrux.Runtime.Utilities.Common;
using Horcrux.Runtime.Utilities.ExtensionMethods;
using Horcrux.Runtime.Utilities.PhysXHelper;
using Horcrux.Runtime.Utilities.Tweening;
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
                selfRect.CharmPunchScale(Vector3.one, 0.2f, duration, ct: ct),
                CharmTween.CastAsync(duration, EaseType.Linear, FlySequence, ct, Land));

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

            void Land()
            {
                gameObject.SetActive(false);
            }
        }

        #endregion
        
        #region Class Methods

        private void PlayBurst()
        {
            if (burst == null)
                return;

            burst.Play();
        }

        #endregion
    }
}