using System.Threading;
using Cysharp.Threading.Tasks;
using PrimeTween;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Horcrux.Runtime.Utilities.UI
{
    public class ProgressBar : MonoBehaviour
    {
        public enum FillMode
        {
            /// <summary>Resizes the fill width to <c>fullWidth * curr / goal</c>.</summary>
            SetWidth,
            /// <summary>Writes <c>curr / goal</c> to <see cref="Image.fillAmount"/> of the Image on fill; the rect keeps its size.</summary>
            SetFill,
        }

        [Splitter("References")]
        [SerializeField] private RectTransform fill;
        [SerializeField] private TextMeshProUGUI label;

        [Splitter("Configs")]
        [SerializeField] private bool useUnscaledTime = true;
        [Tooltip("SetFill: the Image on fill must be Image Type Filled; Full Width is unused.")]
        [SerializeField] private FillMode fillMode;
        [SerializeField] private float fullWidth;
        [SerializeField] private Ease ease;
        [SerializeField] private float speed;

        private float _goal;
        private Image _fillImage;

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
            if (fill == null)
                return;

            if (fillMode == FillMode.SetWidth)
            {
                fill.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, fullWidth * curr / _goal);
                return;
            }

            if (_fillImage == null && !fill.TryGetComponent(out _fillImage))
            {
                Debug.LogError($"[ProgressBar]: Fill of {gameObject.name} has no Image for SetFill", this);
                return;
            }

            _fillImage.fillAmount = curr / _goal;
        }

        #endregion
    }
}
