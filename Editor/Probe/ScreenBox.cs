using System;
using UnityEngine;

namespace Horcrux.Editor.Probe
{
    /// <summary>
    /// Khung chữ nhật của một object trên màn Game view, theo pixel, gốc trên trái như ảnh chụp và ảnh demo: số đo so thẳng với
    /// <c>DemoCompare</c> mà không lật trục y. Object có RectTransform lấy rect của chính nó (UI trong canvas, chữ world); object
    /// khác gộp bounds mọi Renderer con đang bật.
    /// </summary>
    public readonly struct ScreenBox
    {
        public readonly float Left;
        public readonly float Top;
        public readonly float Right;
        public readonly float Bottom;

        private ScreenBox(float left, float top, float right, float bottom)
        {
            Left = left;
            Top = top;
            Right = right;
            Bottom = bottom;
        }

        public float Width => Right - Left;
        public float Height => Bottom - Top;

        /// <summary>
        /// Đo trong Play, sau khi layout đã chạy. Không có RectTransform lẫn Renderer con đang bật thì ném
        /// InvalidOperationException; PlayProbe.Run ghi nó vào LOG.
        /// </summary>
        public static ScreenBox Of(GameObject target)
        {
            var corners = new Vector3[8];
            if (target.transform is RectTransform rect)
            {
                rect.GetWorldCorners(corners);
                return FromCorners(corners, 4, CameraFor(rect));
            }

            var found = false;
            var bounds = default(Bounds);
            foreach (var r in target.GetComponentsInChildren<Renderer>())
            {
                if (!r.enabled)
                    continue;
                if (found) bounds.Encapsulate(r.bounds);
                else bounds = r.bounds;
                found = true;
            }
            if (!found)
                throw new InvalidOperationException("ScreenBox: " + target.name + " has no RectTransform and no enabled Renderer");

            return FromBounds(bounds, corners);
        }

        /// <summary>
        /// Khung của riêng một Renderer, không gộp con: sprite nền có con nhô ra ngoài (ô trong khay) thì bản gộp cây đo sai.
        /// Bounds của sprite gồm cả viền trong suốt; so với pixel demo thì lấy cột từ đây rồi đo màu bằng DemoCompare.Runs.
        /// </summary>
        public static ScreenBox Of(Renderer renderer) => FromBounds(renderer.bounds, new Vector3[8]);

        private static ScreenBox FromBounds(Bounds bounds, Vector3[] corners)
        {
            // tám góc thay vì min-max: camera phối cảnh làm góc gần, góc xa rơi khác chỗ trên màn
            var min = bounds.min;
            var max = bounds.max;
            for (var i = 0; i < 8; i++)
                corners[i] = new Vector3((i & 1) == 0 ? min.x : max.x, (i & 2) == 0 ? min.y : max.y, (i & 4) == 0 ? min.z : max.z);
            return FromCorners(corners, 8, Camera.main);
        }

        /// <summary><c>y top-bottom x left-right</c>, làm tròn pixel.</summary>
        public override string ToString() => $"y {Top:F0}-{Bottom:F0} x {Left:F0}-{Right:F0}";

        // null nghĩa là góc đã ở toạ độ màn: canvas Screen Space - Overlay
        private static Camera CameraFor(RectTransform rect)
        {
            var canvas = rect.GetComponentInParent<Canvas>();
            if (canvas == null)
                return Camera.main;
            canvas = canvas.rootCanvas;
            if (canvas.renderMode == RenderMode.ScreenSpaceOverlay)
                return null;
            return canvas.worldCamera != null ? canvas.worldCamera : Camera.main;
        }

        private static ScreenBox FromCorners(Vector3[] corners, int count, Camera camera)
        {
            float left = float.MaxValue, right = float.MinValue, low = float.MaxValue, high = float.MinValue;
            for (var i = 0; i < count; i++)
            {
                var p = camera != null ? camera.WorldToScreenPoint(corners[i]) : corners[i];
                left = Mathf.Min(left, p.x);
                right = Mathf.Max(right, p.x);
                low = Mathf.Min(low, p.y);
                high = Mathf.Max(high, p.y);
            }
            var height = Screen.height;
            return new ScreenBox(left, height - high, right, height - low);
        }
    }
}
