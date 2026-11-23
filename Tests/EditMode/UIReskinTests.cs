using System.IO;
using Horcrux.Editor.UIReskin;
using Horcrux.Runtime.Utilities.UI;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Horcrux.Tests
{
    /// <summary>
    /// Tool reskin UI: đo ảnh theo toạ độ trên trái, override do layout ghi được nhận là nhiễu và gỡ đúng phần đó, 9-slice đọc
    /// trước khi ghi, sprite asset TMP mang face info và version, chép logic nối lại reference.
    /// </summary>
    public sealed class UIReskinTests
    {
        private const string Folder = "Assets/HorcruxUIReskinTestTemp";
        private string _pngFolder;

        [SetUp]
        public void SetUp()
        {
            AssetDatabase.CreateFolder("Assets", "HorcruxUIReskinTestTemp");
            _pngFolder = Path.Combine(Path.GetTempPath(), "HorcruxUIReskinTests");
            Directory.CreateDirectory(_pngFolder);
        }

        [TearDown]
        public void TearDown()
        {
            AssetDatabase.DeleteAsset(Folder);
            Directory.Delete(_pngFolder, true);
        }

        [Test]
        public void BBox_MeasuresFromTopLeft()
        {
            var path = WritePng("box.png", 20, 10, (x, y) => x >= 3 && x <= 6 && y >= 2 && y <= 4 ? Color.red : Color.white);
            Assert.AreEqual("3,2 6,4 (4 x 3)", DemoCompare.BBox(path, new RectInt(0, 0, 20, 10), DemoCompare.Near(new Color32(255, 0, 0, 255), 10)));
            Assert.AreEqual("none", DemoCompare.BBox(path, new RectInt(10, 0, 10, 10), DemoCompare.Near(new Color32(255, 0, 0, 255), 10)));
        }

        [Test]
        public void Runs_SplitsColumnIntoColourBands()
        {
            var path = WritePng("bands.png", 4, 12, (x, y) => y < 5 ? Color.white : y < 9 ? Color.blue : Color.black);
            Assert.AreEqual("0-4 255,255,255\n5-8 0,0,255\n9-11 0,0,0\n", DemoCompare.Runs(path, true, 1, 0, 12));
        }

        [Test]
        public void TemplateMatch_FindsPlacedArtAtScaleOne()
        {
            Color Art(int x, int y) => new Color((x * 37 % 255) / 255f, (y * 53 % 255) / 255f, ((x + y) * 11 % 255) / 255f);
            var art = WritePng("art.png", 32, 24, Art);
            var image = WritePng("scene.png", 200, 160,
                (x, y) => x >= 70 && x < 102 && y >= 50 && y < 74 ? Art(x - 70, y - 50) : new Color(0.2f, 0.3f, 0.4f));
            StringAssert.StartsWith("70,50 32 x 24 scale 1 ", DemoCompare.TemplateMatch(art, image, new RectInt(0, 0, 200, 160), 0.9f, 1.1f, 0.05f));
        }

        [Test]
        public void SideBySide_PutsCaptureRightOfDemoAtDemoSize()
        {
            var demo = WritePng("demo.png", 40, 30, (x, y) => Color.white);
            var capture = WritePng("cap.png", 20, 15, (x, y) => Color.green);
            var output = Path.Combine(_pngFolder, "out.png");
            DemoCompare.SideBySide(demo, capture, output, 5, 25);
            var texture = new Texture2D(2, 2);
            texture.LoadImage(File.ReadAllBytes(output));
            Assert.AreEqual(new Vector2Int(100, 20), new Vector2Int(texture.width, texture.height));
            Assert.AreEqual((Color32)Color.green, (Color32)texture.GetPixel(80, 10));
            Object.DestroyImmediate(texture);
        }

        [Test]
        public void Diff_CountsOnlyPixelsOutsideExclude()
        {
            var demo = WritePng("diff_demo.png", 20, 10, (x, y) => Color.white);
            var capture = WritePng("diff_cap.png", 20, 10, (x, y) => x < 4 && y < 3 ? Color.red : Color.white);
            Assert.AreEqual("mean 0 p95 0 max 0 at 4,0 (188 px)", DemoCompare.Diff(demo, capture, new RectInt(0, 0, 4, 3)));
            StringAssert.Contains("max 255 at 0,0 (200 px)", DemoCompare.Diff(demo, capture));
        }

        [Test]
        public void FitRadialGlow_RecoversRenderedParameters()
        {
            // Oracle viết riêng theo frag của ui_radial_glow: nội suy trong không gian màu project.
            var linear = QualitySettings.activeColorSpace == ColorSpace.Linear;
            Color edge = new Color32(21, 46, 94, 255), center = new Color32(36, 74, 150, 255);
            Vector2 glowCenter = new(0.45f, 0.6f);
            const int width = 120, height = 260;
            const float radius = 0.35f, falloff = 2f, aspect = (float)width / height;
            Color Glow(int x, int y)
            {
                float u = (x + 0.5f) / width, v = 1f - (y + 0.5f) / height;
                float dx = (u - glowCenter.x) * aspect, dy = v - glowCenter.y;
                var f = Mathf.Pow(Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy) / radius), falloff);
                return linear ? Color.LerpUnclamped(edge.linear, center.linear, f).gamma : Color.LerpUnclamped(edge, center, f);
            }
            var logo = new RectInt(20, 90, 80, 40);
            var demo = WritePng("glow.png", width, height, (x, y) => logo.Contains(new Vector2Int(x, y)) ? Color.yellow : Glow(x, y));

            var fit = DemoCompare.FitRadialGlow(demo, logo);

            Assert.AreEqual(glowCenter.x, fit.Center.x, 0.01f, fit.ToString());
            Assert.AreEqual(glowCenter.y, fit.Center.y, 0.01f, fit.ToString());
            Assert.AreEqual(radius, fit.Radius, 0.01f, fit.ToString());
            Assert.AreEqual(falloff, fit.Falloff, 0.1f, fit.ToString());
            AssertColor(center, fit.CenterColor, fit.ToString());
            AssertColor(edge, fit.EdgeColor, fit.ToString());
            StringAssert.StartsWith("mean 0", fit.ModelDiff);
        }

        private static void AssertColor(Color expected, Color actual, string message)
        {
            const float tolerance = 1.5f / 255f;
            Assert.AreEqual(expected.r, actual.r, tolerance, message);
            Assert.AreEqual(expected.g, actual.g, tolerance, message);
            Assert.AreEqual(expected.b, actual.b, tolerance, message);
        }

        [Test]
        public void PruneNoise_RemovesLayoutDrivenOverridesOnly()
        {
            var basePath = Folder + "/base.prefab";
            var root = new GameObject("base", typeof(RectTransform));
            var row = UIBuild.Node(root.transform, "row", new Vector2(300, 100));
            row.gameObject.AddComponent<HorizontalLayoutGroup>().childControlWidth = false;
            UIBuild.Node(row, "cell", new Vector2(50, 50));
            PrefabUtility.SaveAsPrefabAsset(root, basePath);
            Object.DestroyImmediate(root);

            var variantPath = PrefabEdit.CreateVariant(basePath, Folder + "/variant.prefab", go =>
            {
                var cell = (RectTransform)PrefabEdit.Child(go, "row/cell");
                cell.anchoredPosition = new Vector2(12, -7);
                cell.sizeDelta = new Vector2(80, 50);
            });

            var listed = PrefabAudit.ListOverrides(variantPath);
            StringAssert.Contains("~ variant | row/cell:RectTransform.m_AnchoredPosition.x = 12", listed);
            StringAssert.Contains("  variant | row/cell:RectTransform.m_SizeDelta.x = 80", listed);

            StringAssert.StartsWith("dry run", PrefabAudit.PruneNoise(variantPath));
            StringAssert.Contains("~ ", PrefabAudit.ListOverrides(variantPath));

            PrefabAudit.PruneNoise(variantPath, apply: true);
            var after = PrefabAudit.ListOverrides(variantPath);
            StringAssert.DoesNotContain("~ ", after);
            StringAssert.Contains("m_SizeDelta.x = 80", after);
            StringAssert.Contains("variant of " + basePath, PrefabAudit.Report(Folder));
        }

        [Test]
        public void SetBorder_ReportsOldValueAndWrites()
        {
            var texture = ImportSprite("slice.png", 30, 30);
            Assert.AreEqual("slice: (0,0,0,0) -> (5,6,7,8)", SpriteSlicing.SetBorder(texture, new Vector4(5, 6, 7, 8)));
            Assert.AreEqual(new Vector4(5, 6, 7, 8), SpriteSlicing.LargestSlice(texture).border);
            Assert.AreEqual("slice: (5,6,7,8) (giữ)", SpriteSlicing.SetBorder(texture, new Vector4(5, 6, 7, 8)));
        }

        [Test]
        public void TmpSpriteAsset_HasFaceInfoMetricsAndVersion()
        {
            if (Shader.Find("TextMeshPro/Sprite") == null)
                Assert.Ignore("Dự án không có TMP Essential Resources: sprite asset TMP không dựng được.");
            var texture = ImportSprite("coin.png", 111, 115);
            var asset = TmpSpriteAssetBuilder.Create(texture, Folder + "/coin_sprite.asset");
            Assert.AreEqual(115f, asset.faceInfo.pointSize);
            Assert.AreEqual(92f, asset.faceInfo.ascentLine);
            Assert.AreEqual(-23f, asset.faceInfo.descentLine);
            var metrics = asset.spriteGlyphTable[0].metrics;
            Assert.AreEqual(new Vector4(4, 92, 119, 115), new Vector4(metrics.horizontalBearingX, metrics.horizontalBearingY, metrics.horizontalAdvance, metrics.height));
            Assert.AreEqual("1.1.0", new SerializedObject(asset).FindProperty("m_Version").stringValue);
            Assert.AreEqual(1, asset.spriteCharacterTable.Count);
        }

        [Test]
        public void CopyLogic_RemapsReferencesToDestination()
        {
            var source = new GameObject("old", typeof(RectTransform), typeof(Image));
            var destination = new GameObject("new", typeof(RectTransform), typeof(Image));
            try
            {
                // component logic bất kỳ trỏ vào chính object của nó: assembly test chỉ chạy Editor nên không gắn được MonoBehaviour của nó
                var logic = new SerializedObject(source.AddComponent<FlyRewardItem>());
                logic.FindProperty("selfRect").objectReferenceValue = source.transform;
                logic.FindProperty("icon").objectReferenceValue = source.GetComponent<Image>();
                logic.FindProperty("duration").floatValue = 2f;
                logic.ApplyModifiedPropertiesWithoutUndo();

                var log = ComponentTransplant.CopyLogic(source, destination);
                Assert.IsTrue(destination.TryGetComponent<FlyRewardItem>(out var copy), log);
                var copied = new SerializedObject(copy);
                Assert.AreSame(destination.transform, copied.FindProperty("selfRect").objectReferenceValue);
                Assert.AreSame(destination.GetComponent<Image>(), copied.FindProperty("icon").objectReferenceValue);
                Assert.AreEqual(2f, copied.FindProperty("duration").floatValue);
                Assert.AreEqual(1, destination.GetComponents<Image>().Length);
            }
            finally
            {
                Object.DestroyImmediate(source);
                Object.DestroyImmediate(destination);
            }
        }

        [Test]
        public void MakeSingle_ClearsStaleSlices_AndStaysStableOnReimport()
        {
            var path = ImportSprite("sliced.png", 40, 20);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.spriteImportMode = SpriteImportMode.Multiple;
#pragma warning disable CS0618
            importer.spritesheet = new[]
            {
                new SpriteMetaData { name = "sliced_0", rect = new Rect(0, 0, 20, 20), pivot = new Vector2(0.5f, 0.5f) },
                new SpriteMetaData { name = "sliced_1", rect = new Rect(20, 0, 20, 20), pivot = new Vector2(0.5f, 0.5f) },
            };
#pragma warning restore CS0618
            importer.SaveAndReimport();

            Assert.AreEqual(path + ": 2 mảnh cũ -> Single sliced", SpriteSlicing.MakeSingle(path));

            var metaAfter = File.ReadAllText(path + ".meta");
            Assert.That(metaAfter, Does.Not.Contain("sliced_0"));
            Assert.That(metaAfter, Does.Not.Contain("sliced_1"));
            var sprites = 0;
            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(path))
                if (asset is Sprite)
                    sprites++;
            Assert.AreEqual(1, sprites);

            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            Assert.AreEqual(metaAfter, File.ReadAllText(path + ".meta"));
        }

        [Test]
        public void World_CentersRenderersAtPixelsPerUnitAndPosesEachShot()
        {
            var texture = new Texture2D(100, 100, TextureFormat.RGBA32, false);
            var white = new Color32[100 * 100];
            for (var i = 0; i < white.Length; i++)
                white[i] = new Color32(255, 255, 255, 255);
            texture.SetPixels32(white);
            texture.Apply();
            // Pivot dưới trái: bounds lệch khỏi gốc object, camera phải theo tâm bounds mới thấy sprite giữa khung.
            var sprite = Sprite.Create(texture, new Rect(0, 0, 100, 100), Vector2.zero, 100f);
            var source = new GameObject("world source");
            source.transform.position = new Vector3(5f, 3f, 0f);
            source.AddComponent<SpriteRenderer>().sprite = sprite;
            var output = Path.Combine(_pngFolder, "world.png");
            var posed = new System.Collections.Generic.List<int>();
            try
            {
                UICapture.World(source, 200, 200, 100f, Color.black, 2, (copy, i) =>
                {
                    posed.Add(i);
                    if (i == 1)
                        copy.transform.localScale = new Vector3(0.5f, 0.5f, 1f);
                }, output, 2);

                var sheet = new Texture2D(2, 2);
                sheet.LoadImage(File.ReadAllBytes(output));
                // FrameSheet chừa khe 4 px quanh mỗi ảnh: ảnh 0 bắt đầu ở (4, 4), ảnh 1 ở (208, 4).
                Assert.AreEqual(new Vector2Int(2 * 200 + 3 * 4, 200 + 2 * 4), new Vector2Int(sheet.width, sheet.height));
                CollectionAssert.AreEqual(new[] { 0, 1 }, posed);
                // Ảnh 0: sprite 1 unit = 100 px giữa khung 200 (x 50…149). Ảnh 1 thu nửa quanh pivot dưới trái (x 50…99, y 50…99).
                Assert.Greater(sheet.GetPixel(4 + 55, 4 + 100).r, 0.9f);
                Assert.Greater(sheet.GetPixel(4 + 145, 4 + 145).r, 0.9f);
                Assert.Less(sheet.GetPixel(4 + 45, 4 + 100).r, 0.1f);
                Assert.Greater(sheet.GetPixel(208 + 55, 4 + 55).r, 0.9f);
                Assert.Less(sheet.GetPixel(208 + 145, 4 + 145).r, 0.1f);
                Assert.AreEqual(new Vector3(5f, 3f, 0f), source.transform.position);
                Object.DestroyImmediate(sheet);
            }
            finally
            {
                Object.DestroyImmediate(source);
                Object.DestroyImmediate(sprite);
                Object.DestroyImmediate(texture);
            }
        }

        [Test]
        public void FitOverlayAlpha_RecoversAlphaBlendedInProjectColorSpace()
        {
            var overlay = new Color32(10, 30, 70, 255);
            var backgrounds = new[] { new Color32(41, 84, 154, 255), new Color32(200, 180, 160, 255) };
            var observed = new Color32[2];
            var linear = QualitySettings.activeColorSpace == ColorSpace.Linear;
            for (var i = 0; i < 2; i++)
            {
                Color background = backgrounds[i];
                Color over = overlay;
                observed[i] = linear
                    ? (Color32)Color.Lerp(background.linear, over.linear, 0.6f).gamma
                    : (Color32)Color.Lerp(background, over, 0.6f);
            }

            var fit = DemoCompare.FitOverlayAlpha(overlay, backgrounds, observed);
            Assert.AreEqual(0.6f, fit.Alpha, 0.01f);
            Assert.LessOrEqual(fit.MaxError, 1);
            Assert.AreEqual(linear, fit.FittedInLinear);
            if (linear)
                Assert.Less(fit.GammaAlpha, fit.Alpha - 0.05f, "trộn Linear sáng hơn sRGB: cùng màu đo được thì alpha sRGB thấp hơn");
            Assert.Throws<System.ArgumentException>(() => DemoCompare.FitOverlayAlpha(overlay, backgrounds, new Color32[1]));
        }

        private string WritePng(string name, int width, int height, System.Func<int, int, Color> pixel)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            for (var y = 0; y < height; y++)
            for (var x = 0; x < width; x++)
                texture.SetPixel(x, height - 1 - y, pixel(x, y));
            var path = Path.Combine(_pngFolder, name);
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            return path;
        }

        private string ImportSprite(string name, int width, int height)
        {
            var path = Folder + "/" + name;
            File.Copy(WritePng(name, width, height, (x, y) => Color.yellow), path);
            AssetDatabase.ImportAsset(path);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.SaveAndReimport();
            return path;
        }
    }
}
