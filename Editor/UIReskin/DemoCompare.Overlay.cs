using System;
using UnityEngine;

namespace Horcrux.Editor.UIReskin
{
    public static partial class DemoCompare
    {
        /// <summary>
        /// Alpha cho lớp phủ màu overlay để, trộn trong không gian màu project lên từng background, ra đúng màu observed đo trên demo.
        /// Demo trộn ở sRGB nên project Linear cần alpha khác số hoạ sĩ ghi; kèm alpha trộn ở sRGB để so. Mỗi cặp background,
        /// observed là một mẫu (cùng chỉ số); bình phương tối thiểu trên mọi kênh của mọi mẫu.
        /// </summary>
        public static OverlayAlphaFit FitOverlayAlpha(Color32 overlay, Color32[] backgrounds, Color32[] observed)
        {
            if (backgrounds.Length == 0 || backgrounds.Length != observed.Length)
                throw new ArgumentException($"cần số background bằng số observed và khác 0: {backgrounds.Length}, {observed.Length}");

            var linear = QualitySettings.activeColorSpace == ColorSpace.Linear;
            var alpha = SolveOverlayAlpha(overlay, backgrounds, observed, linear);
            var gammaAlpha = linear ? SolveOverlayAlpha(overlay, backgrounds, observed, false) : alpha;

            var maxError = 0;
            for (var i = 0; i < backgrounds.Length; i++)
            for (var channel = 0; channel < 3; channel++)
            {
                var background = ToFitSpace(backgrounds[i][channel], linear);
                var blended = Mathf.Lerp(background, ToFitSpace(overlay[channel], linear), alpha);
                var shown = Mathf.RoundToInt((linear ? Mathf.LinearToGammaSpace(blended) : blended) * 255f);
                maxError = Mathf.Max(maxError, Mathf.Abs(shown - observed[i][channel]));
            }
            return new OverlayAlphaFit(alpha, gammaAlpha, linear, maxError);
        }

        // observed = background + a (overlay − background) mỗi kênh: a = Σ d·e / Σ e², d = observed − background, e = overlay − background.
        private static float SolveOverlayAlpha(Color32 overlay, Color32[] backgrounds, Color32[] observed, bool linear)
        {
            double numerator = 0, denominator = 0;
            for (var i = 0; i < backgrounds.Length; i++)
            for (var channel = 0; channel < 3; channel++)
            {
                var background = ToFitSpace(backgrounds[i][channel], linear);
                var d = ToFitSpace(observed[i][channel], linear) - background;
                var e = ToFitSpace(overlay[channel], linear) - background;
                numerator += d * e;
                denominator += e * e;
            }
            if (denominator < 1e-9)
                throw new ArgumentException("overlay trùng màu mọi background: alpha nào cũng ra cùng màu");
            return Mathf.Clamp01((float)(numerator / denominator));
        }
    }

    /// <summary>Alpha lớp phủ dò từ demo: Alpha cho project, GammaAlpha nếu trộn ở sRGB như tool thiết kế.</summary>
    public readonly struct OverlayAlphaFit
    {
        public readonly float Alpha, GammaAlpha;
        public readonly bool FittedInLinear;

        /// <summary>Lệch lớn nhất một kênh (0–255) giữa màu trộn bằng Alpha và màu đo trên demo.</summary>
        public readonly int MaxError;

        internal OverlayAlphaFit(float alpha, float gammaAlpha, bool fittedInLinear, int maxError)
        {
            Alpha = alpha;
            GammaAlpha = gammaAlpha;
            FittedInLinear = fittedInLinear;
            MaxError = maxError;
        }

        public override string ToString() =>
            $"alpha {Alpha:0.###} ({Mathf.RoundToInt(Alpha * 255f)}/255) fit {(FittedInLinear ? "linear" : "gamma")} | " +
            $"sRGB blend {GammaAlpha:0.###} | max error {MaxError}";
    }
}
