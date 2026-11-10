using System;
using UnityEditor;
using UnityEngine;

namespace Horcrux.Editor.UIReskin
{
    public static partial class DemoCompare
    {
        public const string RadialGlowShader = "Horcrux/ui_radial_glow";

        private static readonly float[] CoarseFalloffs = { 0.5f, 0.75f, 1f, 1.5f, 2f, 2.5f, 3f, 4f };
        private const int CoarseSamples = 1500;
        private const int FineSamples = 40000;

        /// <summary>
        /// Dò thông số shader <c>Horcrux/ui_radial_glow</c> từ ảnh demo mà rect nền phủ kín. Fit trong không gian màu project,
        /// đúng chỗ shader nội suy; exclude (logo, chữ, viền ảnh) bỏ khỏi phép đo. Kèm số lệch của bản render mô phỏng.
        /// </summary>
        public static RadialGlowFit FitRadialGlow(string demoPath, params RectInt[] exclude)
        {
            var demo = Load(demoPath);
            var linear = QualitySettings.activeColorSpace == ColorSpace.Linear;
            var aspect = (float)demo.Width / demo.Height;
            var all = GlowSamples.From(demo, exclude, linear, int.MaxValue);
            if (all.Count < 16)
                throw new ArgumentException("ngoài exclude còn quá ít pixel để fit: " + all.Count);
            var coarse = GlowSamples.From(demo, exclude, linear, CoarseSamples);
            var fine = GlowSamples.From(demo, exclude, linear, FineSamples);

            var best = new GlowShape(new Vector2(0.5f, 0.5f), 0.5f, 1f);
            var bestError = double.MaxValue;
            for (var cx = 0f; cx <= 1.0001f; cx += 0.1f)
            for (var cy = 0f; cy <= 1.0001f; cy += 0.05f)
            for (var radius = 0.05f; radius <= 1.5001f; radius += 0.05f)
                foreach (var falloff in CoarseFalloffs)
                {
                    var shape = new GlowShape(new Vector2(cx, cy), radius, falloff);
                    var error = coarse.Solve(shape, aspect, out _, out _);
                    if (error < bestError) { bestError = error; best = shape; }
                }

            // Dò mẫu quanh điểm thô: thử 80 láng giềng, không khá hơn thì chia đôi bước.
            bestError = fine.Solve(best, aspect, out _, out _);
            float stepCenter = 0.05f, stepRadius = 0.05f, stepFalloff = 0.25f;
            while (stepCenter > 0.0005f)
            {
                var moved = false;
                for (var i = -1; i <= 1; i++)
                for (var j = -1; j <= 1; j++)
                for (var k = -1; k <= 1; k++)
                for (var l = -1; l <= 1; l++)
                {
                    if (i == 0 && j == 0 && k == 0 && l == 0)
                        continue;
                    var shape = new GlowShape(best.Center + new Vector2(i * stepCenter, j * stepCenter),
                        Mathf.Max(0.01f, best.Radius + k * stepRadius), Mathf.Max(0.1f, best.Falloff + l * stepFalloff));
                    var error = fine.Solve(shape, aspect, out _, out _);
                    if (error < bestError) { bestError = error; best = shape; moved = true; }
                }
                if (moved)
                    continue;
                stepCenter *= 0.5f; stepRadius *= 0.5f; stepFalloff *= 0.5f;
            }

            all.Solve(best, aspect, out var edge, out var center);
            var model = new Pixels(demo.Width, demo.Height);
            for (var y = 0; y < demo.Height; y++)
            for (var x = 0; x < demo.Width; x++)
            {
                var f = best.Weight(UvX(x, demo.Width), UvY(y, demo.Height), aspect);
                model.Set(x, y, ToByteColor(Vector3.LerpUnclamped(edge, center, f), linear));
            }
            return new RadialGlowFit(best.Center, best.Radius, best.Falloff, ToMaterialColor(center, linear), ToMaterialColor(edge, linear), linear,
                DiffStats(demo, model, exclude));
        }

        private static float UvX(int x, int width) => (x + 0.5f) / width;

        // Ảnh gốc trên trái, uv của Image gốc dưới trái.
        private static float UvY(int y, int height) => 1f - (y + 0.5f) / height;

        private static float ToFitSpace(byte channel, bool linear) => linear ? Mathf.GammaToLinearSpace(channel / 255f) : channel / 255f;

        private static Color ToMaterialColor(Vector3 c, bool linear)
        {
            var clamped = new Vector3(Mathf.Clamp01(c.x), Mathf.Clamp01(c.y), Mathf.Clamp01(c.z));
            return linear
                ? new Color(Mathf.LinearToGammaSpace(clamped.x), Mathf.LinearToGammaSpace(clamped.y), Mathf.LinearToGammaSpace(clamped.z), 1f)
                : new Color(clamped.x, clamped.y, clamped.z, 1f);
        }

        private static Color32 ToByteColor(Vector3 c, bool linear) => ToMaterialColor(c, linear);

        /// <summary>Hình dạng vầng sáng; Weight là bản C# của frag trong ui_radial_glow.shader, đổi shader thì đổi theo.</summary>
        private readonly struct GlowShape
        {
            public readonly Vector2 Center;
            public readonly float Radius, Falloff;

            public GlowShape(Vector2 center, float radius, float falloff)
            {
                Center = center;
                Radius = radius;
                Falloff = falloff;
            }

            public float Weight(float u, float v, float aspect)
            {
                float dx = (u - Center.x) * aspect, dy = v - Center.y;
                var t = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy) / Radius);
                return Mathf.Pow(t, Falloff);
            }
        }

        private sealed class GlowSamples
        {
            private readonly float[] _u, _v;
            private readonly Vector3[] _color;
            public int Count => _u.Length;

            private GlowSamples(int count)
            {
                _u = new float[count];
                _v = new float[count];
                _color = new Vector3[count];
            }

            // Lấy mẫu đều theo lưới, tối đa limit điểm ngoài exclude.
            public static GlowSamples From(Pixels image, RectInt[] exclude, bool linear, int limit)
            {
                var included = 0;
                for (var y = 0; y < image.Height; y++)
                for (var x = 0; x < image.Width; x++)
                    if (!Excluded(x, y, exclude))
                        included++;
                var stride = Math.Max(1, (int)Math.Ceiling(Math.Sqrt((double)included / limit)));
                var count = 0;
                for (var y = 0; y < image.Height; y += stride)
                for (var x = 0; x < image.Width; x += stride)
                    if (!Excluded(x, y, exclude))
                        count++;
                var samples = new GlowSamples(count);
                var i = 0;
                for (var y = 0; y < image.Height; y += stride)
                for (var x = 0; x < image.Width; x += stride)
                {
                    if (Excluded(x, y, exclude))
                        continue;
                    var c = image.At(x, y);
                    samples._u[i] = UvX(x, image.Width);
                    samples._v[i] = UvY(y, image.Height);
                    samples._color[i] = new Vector3(ToFitSpace(c.r, linear), ToFitSpace(c.g, linear), ToFitSpace(c.b, linear));
                    i++;
                }
                return samples;
            }

            /// <summary>
            /// Với hình dạng cho trước, màu = edge + (center − edge)·f là hồi quy tuyến tính theo f từng kênh: giải đóng, trả tổng bình
            /// phương phần dư ba kênh. f không đổi trên mọi mẫu thì không giải được, trả vô cùng.
            /// </summary>
            public double Solve(GlowShape shape, float aspect, out Vector3 edge, out Vector3 center)
            {
                double n = Count, sf = 0, sff = 0;
                double svr = 0, svg = 0, svb = 0, sfr = 0, sfg = 0, sfb = 0, srr = 0, sgg = 0, sbb = 0;
                for (var i = 0; i < Count; i++)
                {
                    double f = shape.Weight(_u[i], _v[i], aspect);
                    var c = _color[i];
                    sf += f; sff += f * f;
                    svr += c.x; svg += c.y; svb += c.z;
                    sfr += f * c.x; sfg += f * c.y; sfb += f * c.z;
                    srr += c.x * c.x; sgg += c.y * c.y; sbb += c.z * c.z;
                }
                var variance = n * sff - sf * sf;
                if (variance < 1e-9 * n * n)
                {
                    edge = center = default;
                    return double.MaxValue;
                }
                double br = (n * sfr - sf * svr) / variance, bg = (n * sfg - sf * svg) / variance, bb = (n * sfb - sf * svb) / variance;
                double ar = (svr - br * sf) / n, ag = (svg - bg * sf) / n, ab = (svb - bb * sf) / n;
                edge = new Vector3((float)ar, (float)ag, (float)ab);
                center = new Vector3((float)(ar + br), (float)(ag + bg), (float)(ab + bb));
                return srr - ar * svr - br * sfr + sgg - ag * svg - bg * sfg + sbb - ab * svb - bb * sfb;
            }
        }
    }

    /// <summary>Thông số ui_radial_glow dò từ demo: đơn vị đúng ô material (Center theo uv gốc dưới trái, Radius theo chiều cao rect).</summary>
    public readonly struct RadialGlowFit
    {
        public readonly Vector2 Center;
        public readonly float Radius, Falloff;
        public readonly Color CenterColor, EdgeColor;
        public readonly bool FittedInLinear;

        /// <summary>Lệch của bản render mô phỏng so với demo, định dạng của <see cref="DemoCompare.Diff"/>.</summary>
        public readonly string ModelDiff;

        internal RadialGlowFit(Vector2 center, float radius, float falloff, Color centerColor, Color edgeColor, bool fittedInLinear,
            string modelDiff)
        {
            Center = center;
            Radius = radius;
            Falloff = falloff;
            CenterColor = centerColor;
            EdgeColor = edgeColor;
            FittedInLinear = fittedInLinear;
            ModelDiff = modelDiff;
        }

        /// <summary>Ghi thông số vào material và lưu asset. Material không dùng ui_radial_glow thì ném.</summary>
        public void ApplyTo(Material material)
        {
            if (material.shader.name != DemoCompare.RadialGlowShader)
                throw new ArgumentException($"material {material.name} dùng {material.shader.name}, không phải {DemoCompare.RadialGlowShader}");
            material.SetVector("_Center", new Vector4(Center.x, Center.y, 0f, 0f));
            material.SetFloat("_Radius", Radius);
            material.SetFloat("_Falloff", Falloff);
            material.SetColor("_CenterColor", CenterColor);
            material.SetColor("_EdgeColor", EdgeColor);
            EditorUtility.SetDirty(material);
            AssetDatabase.SaveAssetIfDirty(material);
        }

        public override string ToString() =>
            $"_Center ({Center.x:0.###}, {Center.y:0.###}) _Radius {Radius:0.###} _Falloff {Falloff:0.##} " +
            $"_CenterColor #{ColorUtility.ToHtmlStringRGB(CenterColor)} _EdgeColor #{ColorUtility.ToHtmlStringRGB(EdgeColor)} " +
            $"fit {(FittedInLinear ? "linear" : "gamma")} | model {ModelDiff}";
    }
}
