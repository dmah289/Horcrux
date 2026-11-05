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
