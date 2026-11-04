using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using Horcrux.Editor.SpriteAtlasBuilder;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Horcrux.Tests
{
    /// <summary>
    /// Soát atlas: texture quy về atlas có thư mục phủ nó, sprite giữ bởi object tắt vẫn tính là nạp, hai đơn vị cùng dùng một atlas
    /// hiện ở mục shared, atlas pack ra một trang có tỉ lệ lấp.
    /// </summary>
    public sealed class AtlasAuditTests
    {
        private const string Folder = "Assets/HorcruxAtlasAuditTestTemp";
        private const string Packed = Folder + "/Packed";
        private const string AtlasPath = Folder + "/test_atlas.spriteatlasv2";

        [SetUp]
        public void SetUp()
        {
            AssetDatabase.CreateFolder("Assets", "HorcruxAtlasAuditTestTemp");
            AssetDatabase.CreateFolder(Folder, "Packed");
        }

        [TearDown]
        public void TearDown() => AssetDatabase.DeleteAsset(Folder);

        [Test]
        public void Usage_GroupsAtlasedTexturesAndListsLooseOnesWithState()
        {
            var inAtlas = WriteSprite(Packed + "/a.png", Color.red);
            var loose = WriteSprite(Folder + "/b.png", Color.blue);
            SpriteAtlasBuilder.WriteFolderAtlases(new[] { new FolderAtlas(Packed, AtlasPath) });
            var first = WritePrefab("first.prefab", inAtlas, loose);
            var second = WritePrefab("second.prefab", inAtlas, null);

            var report = AtlasAudit.Usage(false, first, second);

            StringAssert.Contains("  " + AtlasPath + " 1 tex (on 1)\n", report);
            StringAssert.Contains("  - | " + Folder + "/b.png 8x8 | off\n", report);
            StringAssert.Contains("== shared\n  " + AtlasPath + " | first, second\n", report);
            StringAssert.DoesNotContain("b.png | first", report);
        }

        [Test]
        public void TextureAtlasMap_CoversTexturesUnderFolderPackable()
        {
            WriteSprite(Packed + "/a.png", Color.red);
            WriteSprite(Folder + "/b.png", Color.blue);
            SpriteAtlasBuilder.WriteFolderAtlases(new[] { new FolderAtlas(Packed, AtlasPath) });

            var map = AtlasAudit.TextureAtlasMap();

            CollectionAssert.AreEqual(new List<string> { AtlasPath }, map[Packed + "/a.png"]);
            Assert.IsFalse(map.ContainsKey(Folder + "/b.png"));
        }

        [Test]
        public void Atlases_ReportsOnePageWithFill()
        {
            WriteSprite(Packed + "/a.png", Color.red);
            SpriteAtlasBuilder.WriteFolderAtlases(new[] { new FolderAtlas(Packed, AtlasPath) });

            var report = AtlasAudit.Atlases(AtlasPath);

            StringAssert.Contains("  pack " + Packed + "\n", report);
            StringAssert.IsMatch(@"  page \d+x\d+ \S+ fill \d+%\n", report);
            Assert.AreEqual(1, Regex.Matches(report, "  page ").Count);
        }

        private static Sprite WriteSprite(string path, Color color)
        {
            var texture = new Texture2D(8, 8);
            var pixels = new Color[64];
            for (var i = 0; i < pixels.Length; i++) pixels[i] = color;
            texture.SetPixels(pixels);
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        /// <summary>Gốc vẽ <paramref name="active"/>; con tắt giữ <paramref name="hidden"/> khi có.</summary>
        private static string WritePrefab(string name, Sprite active, Sprite hidden)
        {
            var root = new GameObject(Path.GetFileNameWithoutExtension(name), typeof(RectTransform), typeof(Image));
            root.GetComponent<Image>().sprite = active;
            if (hidden != null)
            {
                var child = new GameObject("old", typeof(RectTransform), typeof(Image));
                child.transform.SetParent(root.transform);
                child.GetComponent<Image>().sprite = hidden;
                child.SetActive(false);
            }

            var path = Folder + "/" + name;
            PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return path;
        }
    }
}
