using Horcrux.Runtime.Utilities.Common;
using LightSide;
using PrimeTween;
using UnityEngine;

namespace Horcrux.Runtime.Utilities.UI
{
    /// <summary>
    /// Chữ hiện dần từng ký tự: đẩy mặt hiện của RevealModifier từ 0 tới hết chữ, theo tốc độ ký tự mỗi giây. Kiểu một
    /// ký tự xuất hiện (nảy, mờ) là handler của RevealModifier trên chính chữ, chỉnh trong Inspector của chữ.
    /// </summary>
    public sealed class UniTextTypewriter : MonoBehaviour
    {
        #region Properties

        [Splitter("References")]
        [Tooltip("Text whose Styles hold a whole-text RevealModifier.")]
        [SerializeField] private UniTextBase text;

        [Splitter("Configs")]
        [Tooltip("Characters revealed per second. Speed, not duration: a longer text takes longer.")]
        [Min(0.01f)]
        [SerializeField] private float charactersPerSecond = 33f;
        [Tooltip("Start typing as soon as the object is enabled.")]
        [SerializeField] private bool playOnEnable = true;

        private RevealModifier _reveal;
        private Tween _typing;

        #endregion

        #region Unity Callbacks

        private void OnEnable()
        {
            if (playOnEnable) StartShowingText(true);
        }

        private void OnDisable() => _typing.Stop();

        #endregion

        #region API

        /// <summary>Đặt chữ mới, giấu hết; gọi StartShowingText để gõ.</summary>
        public void ShowText(string value)
        {
            _typing.Stop();
            text.Text = value;
            SetFront(0f);
        }

        /// <summary>Gõ từ ký tự đang hiện tới hết; <paramref name="restart"/> thì gõ lại từ đầu.</summary>
        public void StartShowingText(bool restart)
        {
            if (!TryGetReveal()) return;
            _typing.Stop();
            var from = restart ? 0f : CurrentPercent();
            var characterAmount = Mathf.Max(1, text.Text.Length);
            var duration = (100f - from) / 100f * characterAmount / charactersPerSecond;
            SetFront(from);
            _typing = Tween.Custom(this, from, 100f, duration, static (self, percent) => self.SetFront(percent),
                PrimeTween.Ease.Linear, useUnscaledTime: true);
        }

        #endregion

        #region Class Methods

        private bool TryGetReveal()
        {
            if (_reveal != null) return true;
            _reveal = text.GetModifier<RevealModifier>();
            if (_reveal == null)
                Debug.LogError("[UniTextTypewriter]: text has no RevealModifier in its Styles; add a whole-text Reveal style.", this);
            return _reveal != null;
        }

        private void SetFront(float percent)
        {
            if (TryGetReveal()) _reveal.Front = UnitValue.Percent(percent);
        }

        private float CurrentPercent() => _reveal.Front.value;

        #endregion
    }
}
