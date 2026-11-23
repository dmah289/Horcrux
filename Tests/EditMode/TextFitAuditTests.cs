using System.IO;
using Horcrux.Editor.UIReskin;
using Horcrux.Runtime.Utilities.UI;
using LightSide;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Horcrux.Tests
{
    /// <summary>
    /// Chữ đổi theo dữ liệu co trong vùng art: InnerRegion đo đúng lòng art kể cả lòng gradient, TextFit chỉ đánh dấu chữ do
    /// script ghi mà cỡ cố định và chữ autosize phủ cả rect Image.
    /// </summary>
    public sealed class TextFitAuditTests
    {
        private const string Folder = "Assets/HorcruxTextFitTestTemp";
        private string _pngFolder;

        [SetUp]
        public void SetUp()
        {
            AssetDatabase.CreateFolder("Assets", "HorcruxTextFitTestTemp");
            _pngFolder = Path.Combine(Path.GetTempPath(), "HorcruxTextFitTests");
            Directory.CreateDirectory(_pngFolder);
        }

        [TearDown]
        public void TearDown()
        {
            AssetDatabase.DeleteAsset(Folder);
            Directory.Delete(_pngFolder, true);
        }

        // Nút 40x20: lề trong suốt 2 px, viền tối 3 px, lòng gradient dọc 8 mỗi hàng. Đối chứng đã chạy, so với seed thay vì
        // pixel kề trên cùng ảnh: "5,7 34,13 (30 x 7)" — hàng thứ tư tính từ tâm lệch 32 > 24, case đỏ.
        [Test]
        public void InnerRegion_FindsGradientFaceInsideOutline()
        {
            var path = WriteButtonPng();
            Assert.AreEqual("5,5 34,14 (30 x 10)", DemoCompare.InnerRegion(path));
        }

        [Test]
        public void InnerRegion_SeedOnOutlineStaysOnOutline()
        {
            var path = WriteButtonPng();
            Assert.AreEqual("2,2 4,17 (3 x 16)", DemoCompare.InnerRegion(path, seedX: 3, seedY: 10));
        }

        [Test]
        public void InnerRegion_SeedOutsideImageThrows()
        {
            var path = WriteButtonPng();
            Assert.Throws<System.ArgumentException>(() => DemoCompare.InnerRegion(path, seedX: 40, seedY: 0));
        }

        [Test]
        public void TextFit_FlagsFixedDataTextAndAutosizeOverWholeImage()
        {
            var root = new GameObject("button", typeof(RectTransform), typeof(Image));
            try
            {
                ((RectTransform)root.transform).sizeDelta = new Vector2(200, 80);
                var price = AddText(root.transform, "price", Vector2.zero, true);
                AddText(root.transform, "inset", new Vector2(12, 8), true);
                AddText(root.transform, "label", Vector2.zero, false);
                var count = AddText(root.transform, "count", new Vector2(12, 8), false);
                var setter = new SerializedObject(root.AddComponent<UniTextTypewriter>());
                setter.FindProperty("text").objectReferenceValue = count;
                setter.ApplyModifiedPropertiesWithoutUndo();
                Assert.IsNotNull(price);
                PrefabUtility.SaveAsPrefabAsset(root, Folder + "/button.prefab");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }

            Assert.AreEqual(
                Folder + "/button.prefab\n" +
                "! price | size 40 | auto 10-40 | image rect\n" +
                "  inset | size 40 | auto 10-40\n" +
                "  label | size 40 | auto off\n" +
                "! count | size 40 | auto off | set by UniTextTypewriter.text | fixed\n",
                PrefabAudit.TextFit(Folder));
        }

        private static UniTextBase AddText(Transform parent, string name, Vector2 inset, bool autoSize)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = inset;
            rect.offsetMax = -inset;
            var text = go.AddComponent<UniText>();
            var so = new SerializedObject(text);
            so.FindProperty("fontSize").floatValue = 40;
            so.FindProperty("autoSize").boolValue = autoSize;
            so.FindProperty("minFontSize").floatValue = 10;
            so.FindProperty("maxFontSize").floatValue = 40;
            so.ApplyModifiedPropertiesWithoutUndo();
            return text;
        }

        private string WriteButtonPng()
        {
            var texture = new Texture2D(40, 20, TextureFormat.RGBA32, false);
            try
            {
                for (var y = 0; y < 20; y++)
                for (var x = 0; x < 40; x++)
                {
                    // Texture2D gốc dưới trái; ảnh đọc ra gốc trên trái nên hàng top = 19 - y
                    var top = 19 - y;
                    Color32 c;
                    if (x < 2 || x >= 38 || top < 2 || top >= 18)
                        c = new Color32(0, 0, 0, 0);
                    else if (x < 5 || x >= 35 || top < 5 || top >= 15)
                        c = new Color32(20, 60, 20, 255);
                    else
                        c = new Color32(100, (byte)(100 + top * 8), 100, 255);
                    texture.SetPixel(x, y, c);
                }
                var path = Path.Combine(_pngFolder, "button.png");
                File.WriteAllBytes(path, texture.EncodeToPNG());
                return path;
            }
            finally
            {
                Object.DestroyImmediate(texture);
            }
        }
    }
}
