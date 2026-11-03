using System;
using System.IO;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Horcrux.Editor.UIReskin
{
    /// <summary>
    /// Chụp UI ra PNG để so với demo. Trong Play chụp Game view (gồm canvas Overlay); ngoài Play vẽ một canvas trong scene ở
    /// đúng độ phân giải cho trước, không cần Game view, không vào Play.
    /// </summary>
    public static class UICapture
    {
        /// <summary>Play: chờ frame hiện tại vẽ xong rồi ghi PNG đúng cỡ Game view. Gọi từ eval không chờ được thì file có sau một frame.</summary>
        public static async UniTask GameAsync(string pngPath, CancellationToken ct = default)
        {
            await UniTask.WaitForEndOfFrame(ct);
            var shot = ScreenCapture.CaptureScreenshotAsTexture();
            try
            {
                Write(pngPath, shot);
            }
            finally
            {
                Object.DestroyImmediate(shot);
            }
        }

        /// <summary>Bản không chờ của <see cref="GameAsync"/> cho eval một dòng.</summary>
        public static string Game(string pngPath)
        {
            File.Delete(pngPath);
            GameAsync(pngPath).Forget();
            return pngPath;
        }

        /// <summary>
        /// Ngoài Play: tạm chuyển canvas sang Screen Space–Camera với camera tạm vẽ vào RenderTexture width×height, dựng lại
        /// layout, vẽ, ghi PNG, rồi trả canvas như cũ. CanvasScaler tính theo cỡ RenderTexture như theo màn thật.
        /// Behaviour [ExecuteAlways] của dự án dưới canvas (không phải UIBehaviour) được tắt bật lại để fitter đọc bề rộng mới ngay, không chờ Update.
        /// </summary>
        public static string Canvas(Canvas canvas, int width, int height, string pngPath)
        {
            var mode = canvas.renderMode;
            var worldCamera = canvas.worldCamera;
            var planeDistance = canvas.planeDistance;
            var texture = new RenderTexture(width, height, 24);
            var cameraObject = new GameObject("UICapture camera") { hideFlags = HideFlags.HideAndDontSave };
            Texture2D shot = null;
            try
            {
                var camera = cameraObject.AddComponent<Camera>();
                camera.orthographic = true;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = Color.black;
                camera.cullingMask = 1 << canvas.gameObject.layer;
                camera.targetTexture = texture;

                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera;
                canvas.planeDistance = 10f;
                Relayout(canvas);
                camera.Render();

                var previous = RenderTexture.active;
                RenderTexture.active = texture;
                shot = new Texture2D(width, height, TextureFormat.RGB24, false);
                shot.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                shot.Apply();
                RenderTexture.active = previous;
                Write(pngPath, shot);
            }
            finally
            {
                canvas.renderMode = mode;
                canvas.worldCamera = worldCamera;
                canvas.planeDistance = planeDistance;
                Relayout(canvas);
                Object.DestroyImmediate(cameraObject);
                Object.DestroyImmediate(texture);
                if (shot != null)
                    Object.DestroyImmediate(shot);
            }
            return pngPath;
        }

        private static void Relayout(Canvas canvas)
        {
            // CanvasScaler chỉ tính lại khi được bật hay khi Update chạy
            if (canvas.TryGetComponent<CanvasScaler>(out var scaler) && scaler.enabled)
            {
                scaler.enabled = false;
                scaler.enabled = true;
            }
            UnityEngine.Canvas.ForceUpdateCanvases();
            foreach (var behaviour in canvas.GetComponentsInChildren<MonoBehaviour>())
            {
                if (behaviour == null || behaviour is UIBehaviour || !behaviour.enabled ||
                    !Attribute.IsDefined(behaviour.GetType(), typeof(ExecuteAlways)))
                    continue;
                behaviour.enabled = false;
                behaviour.enabled = true;
            }
            LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)canvas.transform);
            UnityEngine.Canvas.ForceUpdateCanvases();
        }

        private static void Write(string pngPath, Texture2D texture)
        {
            var folder = Path.GetDirectoryName(pngPath);
            if (!string.IsNullOrEmpty(folder))
                Directory.CreateDirectory(folder);
            File.WriteAllBytes(pngPath, texture.EncodeToPNG());
        }
    }
}
