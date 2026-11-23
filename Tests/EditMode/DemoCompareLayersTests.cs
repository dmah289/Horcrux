using System;
using System.IO;
using Horcrux.Editor.UIReskin;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Horcrux.Tests
{
    /// <summary>
    /// Tách lớp chữ art dọc đường cắt: pixel khử răng cưa giữa hai lớp bị bỏ, ruột gradient là một lớp, lớp bị nhiễu cắt đôi gộp
    /// lại; so demo với ảnh chụp bắt thiếu lớp bóng và viền mỏng hơn mà Diff trung bình bỏ qua.
    /// Đối chứng một lần lúc viết: blend 0 thì case khử răng cưa ra 4 lớp; Runs (ranh theo lệch so với pixel đầu dải, tolerance
    /// 12) cắt case gradient thành 8 dải; tắt bước gộp thì case nhiễu ra 2 lớp 0-4, 6-9.
    /// </summary>
    public sealed class DemoCompareLayersTests
    {
        private static readonly Color32 White = new(255, 255, 255, 255);
        private static readonly Color32 Black = new(0, 0, 0, 255);
        private static readonly Color32 Red = new(255, 0, 0, 255);
        private static readonly Color32 Shadow = new(60, 60, 60, 255);

        private string _folder;

        [SetUp]
        public void SetUp()
        {
            _folder = Path.Combine(Path.GetTempPath(), "HorcruxDemoCompareLayersTests");
            Directory.CreateDirectory(_folder);
        }

        [TearDown]
        public void TearDown() => Directory.Delete(_folder, true);

        [Test]
        public void Layers_DropsAntiAliasPixelBetweenLayers()
        {
            var path = WriteColumn("aa.png", 16, y => y < 5 ? White : y == 5 ? new Color32(128, 128, 128, 255) : y < 10 ? Black : Red);
            Assert.AreEqual("0-4 (5) 255,255,255\n6-9 (4) 0,0,0\n10-15 (6) 255,0,0\n", DemoCompare.Layers(path, true, 1, 0, 16));
        }

        [Test]
        public void Layers_KeepsGradientFillAsOneLayerWithBothEnds()
        {
            var path = WriteColumn("gradient.png", 24, y => y < 4 ? Black : new Color32(255, (byte)(200 - 5 * (y - 4)), 0, 255));
            Assert.AreEqual("0-3 (4) 0,0,0\n4-23 (20) 255,195,0→255,110,0\n", DemoCompare.Layers(path, true, 1, 0, 24));
        }

        [Test]
        public void Layers_JoinsLayerSplitByNoisePixel()
        {
            var path = WriteColumn("noise.png", 10, y => y == 5 ? new Color32(255, 60, 60, 255) : Red);
            Assert.AreEqual("0-9 (10) 255,0,0\n", DemoCompare.Layers(path, true, 1, 0, 10));
        }

        [Test]
        public void LayerDiff_FlagsMissingShadowAndThinnerOutline()
        {
            var demo = WriteColumn("demo.png", 24, y => y < 5 ? White : y < 9 ? Black : y < 15 ? Red : y < 19 ? Shadow : White);
            var capture = WriteColumn("cap.png", 24, y => y < 5 ? White : y < 7 ? Black : y < 15 ? Red : White);
            var report = DemoCompare.LayerDiff(demo, capture, true, 1, 0, 24);
            StringAssert.StartsWith("lệch: demo 5 lớp, chụp 4 lớp\n", report);
            StringAssert.Contains("5-8 (4) 0,0,0 | 5-6 (2) 0,0,0 | Δmàu 0 Δdày -2 ✗", report);
        }

        [Test]
        public void LayerDiff_SameImageMatches()
        {
            var path = WriteColumn("same.png", 16, y => y < 5 ? White : y < 10 ? Black : Red);
            StringAssert.StartsWith("khớp 3 lớp\n", DemoCompare.LayerDiff(path, path, true, 1, 0, 16));
        }

        // Ảnh rộng 3 px, mỗi hàng một màu; y tính từ trên như DemoCompare.
        private string WriteColumn(string name, int height, Func<int, Color32> colour)
        {
            var texture = new Texture2D(3, height, TextureFormat.RGBA32, false);
            var pixels = new Color32[3 * height];
            for (var y = 0; y < height; y++)
            for (var x = 0; x < 3; x++)
                pixels[(height - 1 - y) * 3 + x] = colour(y);
            texture.SetPixels32(pixels);
            var path = Path.Combine(_folder, name);
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            return path;
        }
    }
}
