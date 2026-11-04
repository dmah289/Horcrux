using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Horcrux.Editor.Probe;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Horcrux.Editor.UIReskin
{
    /// <summary>
    /// So ảnh chụp với ảnh demo bằng số đo pixel, không cần Python. Mọi toạ độ tính từ góc trên trái như phần mềm ảnh (demo đo
    /// bằng mắt hay bằng tool ảnh khớp số), vùng là RectInt x, y, width, height theo hệ đó. Ảnh đọc thẳng từ file PNG.
    /// </summary>
    public static class DemoCompare
    {
        private const int Gap = 20;
        private static readonly Color32 GapColor = new(255, 0, 255, 255);
        private static readonly Color32 GridColor = new(255, 255, 0, 255);

        /// <summary>
        /// Demo bên trái, ảnh chụp (đưa về cỡ demo) bên phải, cắt dải hàng y0..y1 (y1 âm là tới đáy). Vạch vàng mỗi 100 px theo y
        /// tuyệt đối, dày 3 px ở mỗi 500, kẻ qua cả hai ảnh để so cùng hàng.
        /// </summary>
        public static string SideBySide(string demoPath, string capturePath, string outPath, int y0 = 0, int y1 = -1)
        {
            var demo = Load(demoPath);
            var capture = Load(capturePath).Resized(demo.Width, demo.Height);
            if (y1 < 0 || y1 > demo.Height)
                y1 = demo.Height;
            int width = demo.Width * 2 + Gap, height = y1 - y0;
            var output = new Pixels(width, height);
            for (var y = 0; y < height; y++)
            {
                var source = y0 + y;
                var grid = source % 100 == 0 || source % 500 < 3;
                for (var x = 0; x < width; x++)
                {
                    Color32 c;
                    if (x < demo.Width) c = demo.At(x, source);
                    else if (x < demo.Width + Gap) c = GapColor;
                    else c = capture.At(x - demo.Width - Gap, source);
                    output.Set(x, y, grid ? GridColor : c);
                }
            }
            output.Save(outPath);
            return outPath;
        }

        /// <summary>Khung bao các pixel trong region thoả match: <c>x0,y0 x1,y1 (w x h)</c>, hai góc đều tính vào; không có thì <c>none</c>.</summary>
        public static string BBox(string imagePath, RectInt region, Func<Color32, bool> match)
        {
            var image = Load(imagePath);
            int minX = int.MaxValue, minY = int.MaxValue, maxX = -1, maxY = -1;
            for (var y = Math.Max(0, region.y); y < Math.Min(image.Height, region.yMax); y++)
            for (var x = Math.Max(0, region.x); x < Math.Min(image.Width, region.xMax); x++)
            {
                if (!match(image.At(x, y)))
                    continue;
                minX = Math.Min(minX, x); maxX = Math.Max(maxX, x);
                minY = Math.Min(minY, y); maxY = Math.Max(maxY, y);
            }
            return maxX < 0 ? "none" : $"{minX},{minY} {maxX},{maxY} ({maxX - minX + 1} x {maxY - minY + 1})";
        }

        /// <summary>Màu lệch từng kênh RGB không quá tolerance.</summary>
        public static Func<Color32, bool> Near(Color32 color, int tolerance) => c =>
            Math.Abs(c.r - color.r) <= tolerance && Math.Abs(c.g - color.g) <= tolerance && Math.Abs(c.b - color.b) <= tolerance;

        /// <summary>
        /// Đi dọc một cột (vertical) hay một hàng từ from tới to, cắt thành dải màu liền: mỗi dải một dòng <c>a-b r,g,b</c>. Pixel
        /// lệch pixel đầu dải quá tolerance (tổng ba kênh) thì sang dải mới; dải ngắn hơn minLength bỏ. Đo mép, độ dày viền, khoảng cách.
        /// </summary>
        public static string Runs(string imagePath, bool vertical, int index, int from, int to, int tolerance = 30, int minLength = 3)
        {
            var image = Load(imagePath);
            var sb = new StringBuilder();
            to = Math.Min(to, vertical ? image.Height : image.Width);
            var start = from;
            var first = vertical ? image.At(index, from) : image.At(from, index);
            for (var i = from + 1; i <= to; i++)
            {
                var ended = i == to;
                Color32 c = default;
                if (!ended)
                {
                    c = vertical ? image.At(index, i) : image.At(i, index);
                    ended = Math.Abs(c.r - first.r) + Math.Abs(c.g - first.g) + Math.Abs(c.b - first.b) > tolerance;
                }
                if (!ended)
                    continue;
                if (i - start >= minLength)
                    sb.Append(start).Append('-').Append(i - 1).Append(' ').Append(first.r).Append(',').Append(first.g).Append(',').Append(first.b).Append('\n');
                start = i;
                first = c;
            }
            return sb.ToString();
        }

        /// <summary>
        /// Tìm chỗ và tỉ lệ đặt art (PNG có alpha) khớp nhất trong region của ảnh: <c>x,y w x h scale s rmse e</c>, x, y là góc trên
        /// trái của art đã scale, s là w chia bề rộng art. So các pixel art có alpha ≥ 128. Dò thô ở ảnh thu nhỏ (tới 8 lần, art thu
        /// còn ít nhất 8 px) với bước scale step, rồi dò tinh ở cỡ thật quanh điểm thô, mỗi bề rộng nguyên một lần. rmse theo
        /// 0–255 trên ba kênh.
        /// </summary>
        public static string TemplateMatch(string artPath, string imagePath, RectInt region, float minScale, float maxScale, float step = 0.02f)
        {
            var art = Load(artPath);
            var image = Load(imagePath);
            var coarse = (int)Mathf.Clamp(Mathf.Min(art.Width, art.Height) * Mathf.Max(minScale, 0.01f) / 8f, 1f, 8f);
            var smallImage = coarse > 1 ? image.Shrunk(coarse) : image;
            var smallRegion = new RectInt(region.x / coarse, region.y / coarse, region.width / coarse, region.height / coarse);
            var best = (x: 0, y: 0, scale: minScale, error: double.MaxValue);
            for (var s = minScale; s <= maxScale + 1e-4f; s += step)
            {
                var template = art.Resized(Mathf.Max(1, Mathf.RoundToInt(art.Width * s / coarse)), Mathf.Max(1, Mathf.RoundToInt(art.Height * s / coarse)));
                var found = Search(smallImage, template, smallRegion, 1);
                if (found.error < best.error)
                    best = (found.x * coarse, found.y * coarse, s, found.error);
            }

            var fine = (x: best.x, y: best.y, width: art.Width, error: double.MaxValue);
            var minWidth = Mathf.Max(1, Mathf.RoundToInt(art.Width * (best.scale - step)));
            var maxWidth = Mathf.RoundToInt(art.Width * (best.scale + step));
            var window = new RectInt(best.x - coarse, best.y - coarse, 2 * coarse + 1, 2 * coarse + 1);
            for (var width = minWidth; width <= maxWidth; width++)
            {
                var template = art.Resized(width, Mathf.Max(1, Mathf.RoundToInt((float)art.Height * width / art.Width)));
                var found = Search(image, template, window, art.Width > 64 ? 2 : 1);
                if (found.error < fine.error)
                    fine = (found.x, found.y, width, found.error);
            }
            var height = Mathf.Max(1, Mathf.RoundToInt((float)art.Height * fine.width / art.Width));
            return $"{fine.x},{fine.y} {fine.width} x {height} scale {(float)fine.width / art.Width:0.###} rmse {Math.Sqrt(fine.error):0.#}";
        }

        /// <summary>
        /// Ghép mọi PNG trong folder (theo tên) thành lưới, mỗi ô cell×cell giữ tỉ lệ trên nền xám; trả bảng <c>i: tên WxH</c> theo
        /// thứ tự ô (trên trái, theo hàng). Dùng để nhận art bằng mắt trước khi tin tên file.
        /// </summary>
        public static string ContactSheet(string folder, string outPath, int cell = 256, int columns = 6)
        {
            var files = Directory.GetFiles(folder, "*.png");
            Array.Sort(files, StringComparer.Ordinal);
            var cells = new List<Texture2D>();
            var sb = new StringBuilder();
            try
            {
                for (var i = 0; i < files.Length; i++)
                {
                    var art = Load(files[i]);
                    sb.Append(i).Append(": ").Append(Path.GetFileName(files[i])).Append(' ').Append(art.Width).Append('x').Append(art.Height).Append('\n');
                    var scale = Mathf.Min(1f, (float)cell / Mathf.Max(art.Width, art.Height));
                    int w = Mathf.Max(1, Mathf.RoundToInt(art.Width * scale)), h = Mathf.Max(1, Mathf.RoundToInt(art.Height * scale));
                    var fitted = art.Resized(w, h);
                    var tile = new Pixels(cell, cell);
                    int ox = (cell - w) / 2, oy = (cell - h) / 2;
                    for (var y = 0; y < cell; y++)
                    for (var x = 0; x < cell; x++)
                    {
                        var inside = x >= ox && x < ox + w && y >= oy && y < oy + h;
                        tile.Set(x, y, inside ? OverGrey(fitted.At(x - ox, y - oy)) : new Color32(128, 128, 128, 255));
                    }
                    cells.Add(tile.ToTexture());
                }
                if (cells.Count == 0)
                    return "không có PNG trong " + folder;
                var sheet = FrameSheet.Compose(cells, columns);
                File.WriteAllBytes(outPath, sheet.EncodeToPNG());
                Object.DestroyImmediate(sheet);
            }
            finally
            {
                foreach (var texture in cells)
                    Object.DestroyImmediate(texture);
            }
            return sb.ToString();
        }

        // Sai số bình phương trung bình của template đặt tại mọi điểm trong vùng; stride lấy mẫu pixel template.
        private static (int x, int y, double error) Search(Pixels image, Pixels template, RectInt region, int stride)
        {
            var best = (x: region.x, y: region.y, error: double.MaxValue);
            int x0 = Math.Max(0, region.x), y0 = Math.Max(0, region.y);
            int x1 = Math.Min(image.Width - template.Width, region.xMax), y1 = Math.Min(image.Height - template.Height, region.yMax);
            for (var oy = y0; oy <= y1; oy++)
            for (var ox = x0; ox <= x1; ox++)
            {
                double sum = 0;
                var count = 0;
                for (var ty = 0; ty < template.Height; ty += stride)
                for (var tx = 0; tx < template.Width; tx += stride)
                {
                    var t = template.At(tx, ty);
                    if (t.a < 128)
                        continue;
                    var c = image.At(ox + tx, oy + ty);
                    int dr = c.r - t.r, dg = c.g - t.g, db = c.b - t.b;
                    sum += dr * dr + dg * dg + db * db;
                    count++;
                }
                if (count == 0)
                    continue;
                var error = sum / (3.0 * count);
                if (error < best.error)
                    best = (ox, oy, error);
            }
            return best;
        }

        private static Color32 OverGrey(Color32 c)
        {
            var a = c.a / 255f;
            return new Color32((byte)(c.r * a + 128 * (1 - a)), (byte)(c.g * a + 128 * (1 - a)), (byte)(c.b * a + 128 * (1 - a)), 255);
        }

        private static Pixels Load(string path)
        {
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                if (!texture.LoadImage(File.ReadAllBytes(path)))
                    throw new ArgumentException("không đọc được ảnh " + path);
                return new Pixels(texture);
            }
            finally
            {
                Object.DestroyImmediate(texture);
            }
        }

        // Ảnh trong bộ nhớ, toạ độ gốc trên trái.
        private sealed class Pixels
        {
            public readonly int Width, Height;
            private readonly Color32[] _data;

            public Pixels(int width, int height)
            {
                Width = width;
                Height = height;
                _data = new Color32[width * height];
            }

            // Texture2D gốc dưới trái: lật hàng một lần lúc nạp
            public Pixels(Texture2D texture) : this(texture.width, texture.height)
            {
                var source = texture.GetPixels32();
                for (var y = 0; y < Height; y++)
                    Array.Copy(source, (Height - 1 - y) * Width, _data, y * Width, Width);
            }

            public Color32 At(int x, int y) => _data[y * Width + x];

            public void Set(int x, int y, Color32 c) => _data[y * Width + x] = c;

            /// <summary>Song tuyến; cùng cỡ thì trả chính nó.</summary>
            public Pixels Resized(int width, int height)
            {
                if (width == Width && height == Height)
                    return this;
                var result = new Pixels(width, height);
                for (var y = 0; y < height; y++)
                {
                    var fy = Mathf.Clamp((y + 0.5f) * Height / height - 0.5f, 0, Height - 1);
                    int y0 = (int)fy, y1 = Math.Min(y0 + 1, Height - 1);
                    var ty = fy - y0;
                    for (var x = 0; x < width; x++)
                    {
                        var fx = Mathf.Clamp((x + 0.5f) * Width / width - 0.5f, 0, Width - 1);
                        int x0 = (int)fx, x1 = Math.Min(x0 + 1, Width - 1);
                        var tx = fx - x0;
                        var top = Color32.Lerp(At(x0, y0), At(x1, y0), tx);
                        var bottom = Color32.Lerp(At(x0, y1), At(x1, y1), tx);
                        result.Set(x, y, Color32.Lerp(top, bottom, ty));
                    }
                }
                return result;
            }

            /// <summary>Thu nhỏ nguyên lần, mỗi pixel là trung bình một ô factor×factor.</summary>
            public Pixels Shrunk(int factor)
            {
                var result = new Pixels(Width / factor, Height / factor);
                var area = factor * factor;
                for (var y = 0; y < result.Height; y++)
                for (var x = 0; x < result.Width; x++)
                {
                    int r = 0, g = 0, b = 0, a = 0;
                    for (var dy = 0; dy < factor; dy++)
                    for (var dx = 0; dx < factor; dx++)
                    {
                        var c = At(x * factor + dx, y * factor + dy);
                        r += c.r; g += c.g; b += c.b; a += c.a;
                    }
                    result.Set(x, y, new Color32((byte)(r / area), (byte)(g / area), (byte)(b / area), (byte)(a / area)));
                }
                return result;
            }

            public Texture2D ToTexture()
            {
                var texture = new Texture2D(Width, Height, TextureFormat.RGBA32, false);
                var flipped = new Color32[_data.Length];
                for (var y = 0; y < Height; y++)
                    Array.Copy(_data, y * Width, flipped, (Height - 1 - y) * Width, Width);
                texture.SetPixels32(flipped);
                texture.Apply();
                return texture;
            }

            public void Save(string path)
            {
                var texture = ToTexture();
                File.WriteAllBytes(path, texture.EncodeToPNG());
                Object.DestroyImmediate(texture);
            }
        }
    }
}
