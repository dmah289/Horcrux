using System;
using UnityEngine;

namespace Horcrux.Editor.UIReskin
{
    public static partial class DemoCompare
    {
        /// <summary>
        /// Lòng của art, vùng chữ được phép nằm: từ seed (mặc định tâm ảnh) đi ra bốn phía tới mép, mép là chỗ hai pixel kề lệch
        /// quá tolerance (tổng RGBA). Trả <c>x0,y0 x1,y1 (w x h)</c> gốc trên trái, đo trên hàng và cột qua seed.
        /// </summary>
        public static string InnerRegion(string imagePath, int tolerance = 24, int seedX = -1, int seedY = -1)
        {
            var image = Load(imagePath);
            if (seedX < 0)
                seedX = image.Width / 2;
            if (seedY < 0)
                seedY = image.Height / 2;
            if (seedX >= image.Width || seedY >= image.Height)
                throw new ArgumentException($"seed {seedX},{seedY} nằm ngoài ảnh {image.Width}x{image.Height}: {imagePath}");

            Func<int, Color32> row = x => image.At(x, seedY);
            Func<int, Color32> column = y => image.At(seedX, y);
            var x0 = LastBeforeEdge(row, seedX, -1, 0, tolerance);
            var x1 = LastBeforeEdge(row, seedX, 1, image.Width - 1, tolerance);
            var y0 = LastBeforeEdge(column, seedY, -1, 0, tolerance);
            var y1 = LastBeforeEdge(column, seedY, 1, image.Height - 1, tolerance);
            return $"{x0},{y0} {x1},{y1} ({x1 - x0 + 1} x {y1 - y0 + 1})";
        }

        // So với pixel kề, không với seed: lòng tô gradient vẫn liền một vùng, chỉ bước nhảy ở viền mới cắt.
        private static int LastBeforeEdge(Func<int, Color32> at, int start, int step, int limit, int tolerance)
        {
            var prev = at(start);
            for (var i = start; i != limit; i += step)
            {
                var next = at(i + step);
                if (Math.Abs(next.r - prev.r) + Math.Abs(next.g - prev.g) + Math.Abs(next.b - prev.b) + Math.Abs(next.a - prev.a) > tolerance)
                    return i;
                prev = next;
            }
            return limit;
        }
    }
}
