using System.Threading;
using Cysharp.Threading.Tasks;
using Horcrux.Runtime.Tweening.Easing;
using Horcrux.Runtime.Utilities.Tweening;
using TMPro;
using UnityEngine;

namespace Horcrux.Runtime.Utilities.UI
{
    public class ProgressBar : MonoBehaviour
    {
        [Splitter("References")]
        [SerializeField] private RectTransform fill;
        [SerializeField] private TextMeshProUGUI label;
        
        [Splitter("Configs")]
        [SerializeField] private float fullWidth;
        [SerializeField] private EaseType ease;
        [SerializeField] private float speed;

        #region API

        public void Set(int curr, int goal)
        {
            Write(curr, goal);
        }

        public async UniTask PlayAsync(int from, int to, int goal, CancellationToken ct)
        {
            float duration = Mathf.Abs(to - from) / Mathf.Max(0.0001f, speed);
            await CharmTween.CastAsync(duration, ease, spell, ct, onComplete);
            
            void spell(float t) => Write(Mathf.Lerp(from, to, t), goal);
            void onComplete() => Write(to, goal);
        }

        #endregion

        #region Class Methods

        private void Write(float curr, int goal)
        {
            if (goal <= 0)
            {
                Debug.LogError($"[ProgressBar]: Goal of {gameObject.name} <= 0");
                return;
            }
            
            fill?.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, fullWidth * curr / goal);
            label?.SetText("{0}/{1}", Mathf.RoundToInt(curr), goal);
        }

        #endregion
    }
}