using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Horcrux.Editor.Probe
{
    /// <summary>
    /// Quay Game view thành chuỗi PNG <c>f_0000.png…</c> ở nhịp khoá 30 hình/s, để <c>video_demo.py</c> đọc như một video
    /// (docs/Editor/VideoDemo.md). Nhịp tất định dù ghi PNG chậm, vì anim theo <c>Time.deltaTime</c> chỉ tiến 1/30 s mỗi frame.
    /// </summary>
    public static class FrameRecorder
    {
        /// <summary><c>video_demo.py</c> coi chuỗi ảnh là 30 hình/s; đổi ở đây thì số giây đo ra sai mà không gì báo.</summary>
        public const int Framerate = 30;

        public const string FilePattern = "f_%04d.png";

        private static readonly Regex OwnFile = new(@"^f_\d{4}\.png$");

        /// <summary>
        /// Khoá <c>Time.captureFramerate</c>, gọi <paramref name="begin"/> (lệnh mở anim), rồi chụp <paramref name="frameCount"/> frame
        /// liền, frame 0 là frame <paramref name="begin"/> chạy. Xoá ảnh <c>f_####.png</c> cũ trong thư mục trước khi ghi, để lần quay ngắn
        /// không lẫn đuôi lần dài. <c>captureFramerate</c> trả về giá trị cũ ở mọi đường ra.
        /// </summary>
        /// <param name="directory">Đường dẫn tuyệt đối, ngoài thư mục tạm của <c>playrun.sh</c> (runner xoá thư mục đó khi xong).</param>
        /// <returns>Đường dẫn mẫu <c>&lt;directory&gt;/f_%04d.png</c> để đưa thẳng cho <c>video_demo.py</c>.</returns>
        public static async UniTask<string> RecordAsync(string directory, int frameCount, Action begin, CancellationToken ct)
        {
            Directory.CreateDirectory(directory);
            foreach (var file in Directory.GetFiles(directory, "f_*.png"))
                if (OwnFile.IsMatch(Path.GetFileName(file)))
                    File.Delete(file);

            var previous = Time.captureFramerate;
            Time.captureFramerate = Framerate;
            try
            {
                begin?.Invoke();
                for (var i = 0; i < frameCount; i++)
                {
                    await UniTask.WaitForEndOfFrame(ct);
                    var screen = ScreenCapture.CaptureScreenshotAsTexture();
                    try
                    {
                        File.WriteAllBytes(Path.Combine(directory, $"f_{i:D4}.png"), screen.EncodeToPNG());
                    }
                    finally
                    {
                        Object.DestroyImmediate(screen);
                    }
                }
            }
            finally
            {
                Time.captureFramerate = previous;
            }
            return Path.Combine(directory, FilePattern);
        }
    }
}
