using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Horcrux.Editor.Probe
{
    /// <summary>
    /// Chụp Game view ở các khoảnh khắc script chọn rồi ghép thành một PNG lưới, ảnh đầu ở góc trên trái, đi theo hàng.
    /// Chỉ để agent tự kiểm lỗi hình (lệch, mất renderer); cảm giác chuyển động vẫn do developer chơi thử.
    /// </summary>
    public sealed class FrameSheet : IDisposable
    {
        private const int Gap = 4;
        private static readonly Color32 GapColor = new(40, 40, 40, 255);

        private readonly RectInt _crop;
        private readonly int _shrink;
        private readonly List<Texture2D> _shots = new();

        /// <param name="crop">Vùng cắt theo pixel Game view, gốc dưới trái; width 0 lấy cả màn.</param>
        /// <param name="shrink">Thu nhỏ nguyên lần, lấy trung bình từng ô shrink×shrink.</param>
        public FrameSheet(RectInt crop, int shrink = 2)
        {
            _crop = crop;
            _shrink = Mathf.Max(1, shrink);
        }

        public int Count => _shots.Count;

        /// <summary>Chờ cuối frame hiện tại (đã vẽ xong) rồi chụp. Gọi trong Act của FeelTrace thì chụp đúng frame đó.</summary>
        public async UniTask CaptureAsync(CancellationToken ct)
        {
            await UniTask.WaitForEndOfFrame(ct);
            var screen = ScreenCapture.CaptureScreenshotAsTexture();
            var crop = _crop.width > 0 ? _crop : new RectInt(0, 0, screen.width, screen.height);
            _shots.Add(Shrink(screen, crop, _shrink));
            Object.DestroyImmediate(screen);
        }

        public void Save(string pngPath, int columns)
        {
            var sheet = Compose(_shots, columns);
            File.WriteAllBytes(pngPath, sheet.EncodeToPNG());
            Object.DestroyImmediate(sheet);
        }

        public void Dispose()
        {
            foreach (var shot in _shots)
                Object.DestroyImmediate(shot);
            _shots.Clear();
        }

        /// <summary>Cắt vùng crop rồi thu nhỏ nguyên lần, mỗi pixel đích là trung bình một ô shrink×shrink. Texture mới, người gọi huỷ.</summary>
        public static Texture2D Shrink(Texture2D source, RectInt crop, int shrink)
        {
            var src = source.GetPixels32();
            int width = crop.width / shrink, height = crop.height / shrink;
            var dst = new Color32[width * height];
            var area = shrink * shrink;
            for (var y = 0; y < height; y++)
            for (var x = 0; x < width; x++)
            {
                int r = 0, g = 0, b = 0, a = 0;
                for (var dy = 0; dy < shrink; dy++)
                for (var dx = 0; dx < shrink; dx++)
                {
                    var c = src[(crop.y + y * shrink + dy) * source.width + crop.x + x * shrink + dx];
                    r += c.r; g += c.g; b += c.b; a += c.a;
                }
                dst[y * width + x] = new Color32((byte)(r / area), (byte)(g / area), (byte)(b / area), (byte)(a / area));
            }
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            texture.SetPixels32(dst);
            return texture;
        }

        /// <summary>Mọi ảnh cùng cỡ ảnh đầu. Texture gốc dưới trái nên hàng 0 của lưới nằm ở dải y cao nhất.</summary>
        public static Texture2D Compose(IReadOnlyList<Texture2D> shots, int columns)
        {
            int w = shots[0].width, h = shots[0].height;
            var rows = (shots.Count + columns - 1) / columns;
            int sheetWidth = columns * w + (columns + 1) * Gap, sheetHeight = rows * h + (rows + 1) * Gap;
            var sheet = new Texture2D(sheetWidth, sheetHeight, TextureFormat.RGBA32, false);
            var fill = new Color32[sheetWidth * sheetHeight];
            Array.Fill(fill, GapColor);
            sheet.SetPixels32(fill);
            for (var i = 0; i < shots.Count; i++)
            {
                int column = i % columns, row = i / columns;
                var x = Gap + column * (w + Gap);
                var y = sheetHeight - (row + 1) * (h + Gap);
                sheet.SetPixels32(x, y, w, h, shots[i].GetPixels32());
            }
            sheet.Apply();
            return sheet;
        }
    }
}
