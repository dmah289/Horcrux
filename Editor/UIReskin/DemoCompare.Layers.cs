using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Horcrux.Editor.UIReskin
{
    // Chữ art, nút có viền và bóng là một chồng lớp. Khung bao đo bằng màu ruột không thấy viền và bóng, lớp mỏng chìm trong Diff
    // trung bình: đo và nghiệm thu theo lớp dọc một đường cắt.
    public static partial class DemoCompare
    {
        // Lệch kênh lớn nhất giữa hai đầu lõi dải không quá số này thì dải là màu phẳng, quá thì là gradient.
        private const int FlatSpan = 12;

        /// <summary>
        /// Các lớp dọc cột (vertical) hay hàng <c>index</c>, từ <c>from</c> tới trước <c>to</c>: mỗi dòng <c>a-b (dày) r,g,b</c>, lớp
        /// gradient ghi <c>đầu→cuối</c>. Ranh lớp là chỗ hai pixel kề lệch tổng ba kênh quá <c>edge</c>, nên ruột gradient vẫn là một
        /// lớp. Dải không quá <c>blend</c> px là pixel khử răng cưa pha hai lớp kề, bị bỏ (<c>blend</c> 0 giữ nét 1 px thật); hai lớp
        /// kề cùng màu (nhiễu nén cắt đôi) gộp lại. Màu đo ở lõi dải, bỏ một pixel mỗi mép.
        /// </summary>
        public static string Layers(string imagePath, bool vertical, int index, int from, int to, int edge = 12, int blend = 1)
        {
            var sb = new StringBuilder();
            foreach (var layer in SplitLayers(Load(imagePath), vertical, index, from, to, edge, blend))
                sb.Append(layer.Describe()).Append('\n');
            return sb.ToString();
        }

        /// <summary>
        /// So demo với ảnh chụp (đưa về cỡ demo) lớp theo lớp trên cùng đường cắt. Dòng đầu <c>khớp n lớp</c>, <c>lệch k/n lớp</c>
        /// hoặc <c>lệch: demo n lớp, chụp m lớp</c> (thiếu hay thừa viền, bóng); sau đó mỗi lớp một dòng
        /// <c>demo | chụp | Δmàu Δdày</c>, Δmàu là kênh lệch nhất theo 0–255, Δdày là px chụp trừ demo, lớp vượt ngưỡng đánh ✗.
        /// Khác số lớp thì ghép theo thứ tự, phần dư ghi —.
        /// </summary>
        public static string LayerDiff(string demoPath, string capturePath, bool vertical, int index, int from, int to,
            int colourTolerance = 12, int thicknessTolerance = 1, int edge = 12, int blend = 1)
        {
            var demoImage = Load(demoPath);
            var demo = SplitLayers(demoImage, vertical, index, from, to, edge, blend);
            var capture = SplitLayers(Load(capturePath).Resized(demoImage.Width, demoImage.Height), vertical, index, from, to,
                edge, blend);

            var rows = new StringBuilder();
            var mismatches = 0;
            for (var i = 0; i < Math.Max(demo.Count, capture.Count); i++)
            {
                rows.Append(i + 1).Append("  ");
                if (i >= demo.Count || i >= capture.Count)
                {
                    mismatches++;
                    rows.Append(i < demo.Count ? demo[i].Describe() : "—").Append(" | ")
                        .Append(i < capture.Count ? capture[i].Describe() : "—").Append(" ✗\n");
                    continue;
                }

                var colour = demo[i].ColourDelta(capture[i]);
                var thickness = capture[i].Length - demo[i].Length;
                var off = colour > colourTolerance || Math.Abs(thickness) > thicknessTolerance;
                if (off)
                    mismatches++;
                rows.Append(demo[i].Describe()).Append(" | ").Append(capture[i].Describe())
                    .Append(" | Δmàu ").Append(colour).Append(" Δdày ").Append(thickness.ToString("+0;-0;0"))
                    .Append(off ? " ✗" : "").Append('\n');
            }

            var head = demo.Count != capture.Count ? $"lệch: demo {demo.Count} lớp, chụp {capture.Count} lớp"
                : mismatches == 0 ? $"khớp {demo.Count} lớp"
                : $"lệch {mismatches}/{demo.Count} lớp";
            return head + "\n" + rows;
        }

        private static List<Layer> SplitLayers(Pixels image, bool vertical, int index, int from, int to, int edge, int blend)
        {
            int length = vertical ? image.Height : image.Width, across = vertical ? image.Width : image.Height;
            to = Math.Min(to, length);
            if (index < 0 || index >= across || from < 0 || from >= to)
                throw new ArgumentException($"đường cắt {(vertical ? "cột" : "hàng")} {index}, {from}..{to} nằm ngoài ảnh {image.Width}x{image.Height}");

            Color32 At(int i) => vertical ? image.At(index, i) : image.At(i, index);

            var layers = new List<Layer>();
            var start = from;
            for (var i = from + 1; i <= to; i++)
            {
                if (i < to && Step(At(i - 1), At(i)) <= edge)
                    continue;

                if (i - start > blend)
                {
                    var layer = Layer.Measure(At, start, i - 1);
                    var last = layers.Count - 1;
                    if (last >= 0 && Step(layers[last].Last, layer.First) <= edge)
                        layers[last] = layers[last].Join(layer);
                    else
                        layers.Add(layer);
                }

                start = i;
            }

            return layers;
        }

        // Tổng lệch ba kênh: thước của ranh lớp, cùng thước với tolerance của Runs.
        private static int Step(Color32 a, Color32 b) => Math.Abs(a.r - b.r) + Math.Abs(a.g - b.g) + Math.Abs(a.b - b.b);

        private static int ChannelDelta(Color32 a, Color32 b) =>
            Math.Max(Math.Abs(a.r - b.r), Math.Max(Math.Abs(a.g - b.g), Math.Abs(a.b - b.b)));

        private readonly struct Layer
        {
            public readonly int Start, End;
            public readonly Color32 First, Last, Mean;

            private Layer(int start, int end, Color32 first, Color32 last, Color32 mean)
            {
                Start = start;
                End = end;
                First = first;
                Last = last;
                Mean = mean;
            }

            public int Length => End - Start + 1;

            private bool IsGradient => ChannelDelta(First, Last) > FlatSpan;

            // Lõi bỏ một pixel mỗi mép: mép pha màu lớp kề.
            public static Layer Measure(Func<int, Color32> at, int start, int end)
            {
                var inset = end - start >= 2 ? 1 : 0;
                int a = start + inset, b = end - inset;
                long r = 0, g = 0, bl = 0;
                for (var i = a; i <= b; i++)
                {
                    var c = at(i);
                    r += c.r;
                    g += c.g;
                    bl += c.b;
                }

                var n = b - a + 1;
                return new Layer(start, end, at(a), at(b), new Color32((byte)(r / n), (byte)(g / n), (byte)(bl / n), 255));
            }

            // Hai nửa của một lớp bị dải nhiễu cắt: trung bình theo bề dày, đầu lấy của nửa trước, cuối của nửa sau.
            public Layer Join(Layer next)
            {
                int w0 = Length, w1 = next.Length, w = w0 + w1;
                var mean = new Color32((byte)((Mean.r * w0 + next.Mean.r * w1) / w), (byte)((Mean.g * w0 + next.Mean.g * w1) / w),
                    (byte)((Mean.b * w0 + next.Mean.b * w1) / w), 255);
                return new Layer(Start, next.End, First, next.Last, mean);
            }

            // Lớp phẳng so màu trung bình; có bên gradient thì so từng đầu, vì trung bình hai gradient ngược chiều vẫn trùng.
            public int ColourDelta(Layer other) => IsGradient || other.IsGradient
                ? Math.Max(ChannelDelta(First, other.First), ChannelDelta(Last, other.Last))
                : ChannelDelta(Mean, other.Mean);

            public string Describe() => $"{Start}-{End} ({Length}) " +
                                        (IsGradient ? $"{Rgb(First)}→{Rgb(Last)}" : Rgb(Mean));

            private static string Rgb(Color32 c) => $"{c.r},{c.g},{c.b}";
        }
    }
}
