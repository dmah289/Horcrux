using System.Threading;
using Cysharp.Threading.Tasks;
using PrimeTween;
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
        [SerializeField] private bool useUnscaledTime = true;
        [SerializeField] private float fullWidth;
        [SerializeField] private Ease ease;
        [SerializeField] private float speed;

        private float _goal;

        #region API

        /// <param name="labelContent">Null keeps the current label text: fill only.</param>
        public void Set(float curr, float goal, string labelContent)
        {
            if (!TryWriteGoalAndLabel(goal, labelContent))
                return;

            WriteFill(curr);
        }

        /// <summary>Label is written once at the start; only the fill width changes per frame.</summary>
        /// <param name="labelContent">Null keeps the current label text: fill only.</param>
        public UniTask PlayAsync(float from, float to, float goal, string labelContent, CancellationToken ct)
        {
            if (!TryWriteGoalAndLabel(goal, labelContent))
                return UniTask.CompletedTask;

            // The tween writes its first value on the next update; without this the old width shows for one frame.
            WriteFill(from);

            float duration = Mathf.Abs(to - from) / Mathf.Max(0.0001f, speed);
            return Tween.Custom(this, from, to, duration, static (bar, value) => bar.WriteFill(value), ease,
                useUnscaledTime: useUnscaledTime).SetCancellationToken(ct);
        }

        #endregion

        #region Class Methods

        private bool TryWriteGoalAndLabel(float goal, string labelContent)
        {
            if (goal <= 0)
            {
                Debug.LogError($"[ProgressBar]: Goal of {gameObject.name} <= 0", this);
                return false;
            }

            _goal = goal;

            if (label != null && labelContent != null)
                label.SetText(labelContent);

            return true;
        }

        private void WriteFill(float curr)
        {
            if (fill != null)
                fill.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, fullWidth * curr / _goal);
        }

        #endregion
    }
}
