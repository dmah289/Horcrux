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

        private const int OverlaySearchSamples = 20000;

        /// <summary>
        /// Màu và alpha cho lớp phủ trên art để project ra đúng màu hoạ sĩ ghi (màu + % là phép trộn sRGB của tool thiết kế). Mẫu là
        /// mọi pixel đặc của artPath (alpha 255), đích là phép trộn sRGB của designerColor với designerAlpha lên từng pixel đó. Fit
        /// trong không gian màu project; Gamma thì ra lại đúng số hoạ sĩ. Khác <see cref="FitOverlayAlpha"/>: thả cả màu, vì trong
        /// Linear chỉ đổi alpha không đưa được mọi tông của art về cùng lúc. Art một màu thì alpha không xác định: ném, dùng
        /// FitOverlayAlpha với màu cố định.
        /// </summary>
        public static OverlayFit FitOverlay(string artPath, Color32 designerColor, float designerAlpha)
        {
            if (designerAlpha <= 0f || designerAlpha > 1f)
                throw new ArgumentOutOfRangeException(nameof(designerAlpha), designerAlpha, "alpha hoạ sĩ trong (0, 1]");
            var art = Load(artPath);
            var solid = new System.Collections.Generic.List<Color32>();
            for (var y = 0; y < art.Height; y++)
            for (var x = 0; x < art.Width; x++)
            {
                var c = art.At(x, y);
                if (c.a == 255)
                    solid.Add(c);
            }
            if (solid.Count == 0)
                throw new ArgumentException("art không có pixel đặc: " + artPath);
            int min = 255, max = 0;
            foreach (var c in solid)
            {
                var luma = (c.r + c.g + c.b) / 3;
                min = Math.Min(min, luma);
                max = Math.Max(max, luma);
            }
            if (max - min < 8)
                throw new ArgumentException("art gần như một màu, alpha nào cũng có màu khớp; dùng FitOverlayAlpha với màu cố định: " + artPath);

            var linear = QualitySettings.activeColorSpace == ColorSpace.Linear;
            var all = OverlaySamples.From(solid, designerColor, designerAlpha, linear, int.MaxValue);
            var search = OverlaySamples.From(solid, designerColor, designerAlpha, linear, OverlaySearchSamples);

            // Alpha dò lưới 0.01 rồi tinh 0.001 quanh điểm tốt nhất; với mỗi alpha, màu có nghiệm đóng.
            var best = (alpha: 1f, score: double.MaxValue);
            for (var a = 0.01f; a <= 1.0001f; a += 0.01f)
            {
                var score = search.Score(a, search.SolveColor(a));
                if (score < best.score) best = (a, score);
            }
            var coarse = best.alpha;
            for (var a = Mathf.Max(0.001f, coarse - 0.01f); a <= Mathf.Min(1f, coarse + 0.01f) + 1e-5f; a += 0.001f)
            {
                var score = search.Score(a, search.SolveColor(a));
                if (score < best.score) best = (a, score);
            }

            var fitted = all.SolveColor(best.alpha);
            var specColor = new Vector3(ToFitSpace(designerColor.r, linear), ToFitSpace(designerColor.g, linear), ToFitSpace(designerColor.b, linear));
            return new OverlayFit(ToMaterialColor(fitted, linear), best.alpha, linear, all.Errors(best.alpha, fitted),
                all.Errors(designerAlpha, specColor));
        }

        private sealed class OverlaySamples
        {
            private readonly Vector3[] _background, _target;
            private readonly bool _linear;

            private OverlaySamples(int count, bool linear)
            {
                _background = new Vector3[count];
                _target = new Vector3[count];
                _linear = linear;
            }

            // Lấy đều tối đa limit pixel; đích là phép trộn sRGB làm tròn như ảnh demo xuất từ tool thiết kế.
            public static OverlaySamples From(System.Collections.Generic.List<Color32> solid, Color32 color, float alpha, bool linear, int limit)
            {
                var stride = Math.Max(1, (int)Math.Ceiling((double)solid.Count / limit));
                var samples = new OverlaySamples((solid.Count + stride - 1) / stride, linear);
                for (int i = 0, j = 0; i < solid.Count; i += stride, j++)
                {
                    var c = solid[i];
                    samples._background[j] = new Vector3(ToFitSpace(c.r, linear), ToFitSpace(c.g, linear), ToFitSpace(c.b, linear));
                    samples._target[j] = new Vector3(
                        ToFitSpace(Blend(c.r, color.r, alpha), linear),
                        ToFitSpace(Blend(c.g, color.g, alpha), linear),
                        ToFitSpace(Blend(c.b, color.b, alpha), linear));
                }
                return samples;
            }

            private static byte Blend(byte background, byte overlay, float alpha) =>
                (byte)Mathf.RoundToInt(background + (overlay - background) * alpha);

            // target = (1 − a)·background + a·color từng kênh: bình phương tối thiểu theo color là trung bình, rồi kẹp về [0, 1].
            public Vector3 SolveColor(float alpha)
            {
                var sum = Vector3.zero;
                for (var i = 0; i < _target.Length; i++)
                    sum += _target[i] - (1f - alpha) * _background[i];
                var c = sum / (alpha * _target.Length);
                return new Vector3(Mathf.Clamp01(c.x), Mathf.Clamp01(c.y), Mathf.Clamp01(c.z));
            }

            // Lệch nhìn thấy (sRGB 0–255, kênh lệch nhất) của từng pixel: p99 là điểm chọn, mean phá hoà.
            public double Score(float alpha, Vector3 color)
            {
                var errors = ShownErrors(alpha, color);
                Array.Sort(errors);
                return errors[(int)(errors.Length * 0.99)] * 1000.0 + Mean(errors);
            }

            public string Errors(float alpha, Vector3 color)
            {
                var errors = ShownErrors(alpha, color);
                Array.Sort(errors);
                return $"mean {Mean(errors):0.##} p99 {errors[(int)(errors.Length * 0.99)]:0.##} max {errors[errors.Length - 1]:0.##} ({errors.Length} px)";
            }

            private float[] ShownErrors(float alpha, Vector3 color)
            {
                var errors = new float[_target.Length];
                for (var i = 0; i < _target.Length; i++)
                {
                    var shown = Vector3.LerpUnclamped(_background[i], color, alpha);
                    float e = 0;
                    for (var channel = 0; channel < 3; channel++)
                        e = Mathf.Max(e, Mathf.Abs(ToShown(shown[channel]) - ToShown(_target[i][channel])));
                    errors[i] = e;
                }
                return errors;
            }

            private float ToShown(float value) => 255f * (_linear ? Mathf.LinearToGammaSpace(Mathf.Clamp01(value)) : Mathf.Clamp01(value));

            private static double Mean(float[] values)
            {
                double sum = 0;
                foreach (var v in values) sum += v;
                return sum / values.Length;
            }
        }
    }

    /// <summary>
    /// Lớp phủ fit từ màu + % của hoạ sĩ: Color (màu Inspector) và Alpha đặt lên Image phủ. Error là lệch nhìn thấy của bản fit,
    /// AsIsError là lệch khi đặt nguyên số hoạ sĩ; cùng định dạng <c>mean m p99 p max x (n px)</c>, 0–255 kênh lệch nhất.
    /// </summary>
    public readonly struct OverlayFit
    {
        public readonly Color Color;
        public readonly float Alpha;
        public readonly bool FittedInLinear;
        public readonly string Error, AsIsError;

        internal OverlayFit(Color color, float alpha, bool fittedInLinear, string error, string asIsError)
        {
            Color = color;
            Alpha = alpha;
            FittedInLinear = fittedInLinear;
            Error = error;
            AsIsError = asIsError;
        }

        /// <summary>Màu Image phủ: Color kèm Alpha.</summary>
        public Color ImageColor => new(Color.r, Color.g, Color.b, Alpha);

        public override string ToString() =>
            $"color #{ColorUtility.ToHtmlStringRGB(Color)} alpha {Alpha:0.###} fit {(FittedInLinear ? "linear" : "gamma")} | error {Error} | as-is {AsIsError}";
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
