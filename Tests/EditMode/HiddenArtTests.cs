using System;
using System.IO;
using Horcrux.Editor.SpriteAtlasBuilder;
using Horcrux.Editor.UIReskin;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Horcrux.Tests
{
    /// <summary>
    /// Gỡ art trên skin cũ đã tắt: chạy thử không đổi gì, gỡ thật thành override của variant mà object vẫn còn, gốc đang bật bị
    /// từ chối, và art đã gỡ trên variant không còn tính là nạp qua prefab gốc.
    /// </summary>
    public sealed class HiddenArtTests
    {
        private const string Folder = "Assets/HorcruxHiddenArtTestTemp";
        private const string BasePath = Folder + "/base.prefab";
        private const string VariantPath = Folder + "/variant.prefab";

        [SetUp]
        public void SetUp()
        {
            AssetDatabase.CreateFolder("Assets", "HorcruxHiddenArtTestTemp");
            var shown = WriteSprite(Folder + "/shown.png");
            var old = WriteSprite(Folder + "/old.png");

            var root = new GameObject("base", typeof(RectTransform), typeof(Image));
            root.GetComponent<Image>().sprite = shown;
            var skin = new GameObject("old_skin", typeof(RectTransform));
            skin.transform.SetParent(root.transform);
            var icon = new GameObject("icon", typeof(RectTransform), typeof(Image));
            icon.transform.SetParent(skin.transform);
            icon.GetComponent<Image>().sprite = old;
            skin.SetActive(false);
            PrefabUtility.SaveAsPrefabAsset(root, BasePath);
            Object.DestroyImmediate(root);
            PrefabEdit.CreateVariant(BasePath, VariantPath);
        }

        [TearDown]
        public void TearDown() => AssetDatabase.DeleteAsset(Folder);

        [Test]
        public void List_GroupsReferencesByOutermostInactiveObject()
        {
            Assert.AreEqual("old_skin | 1 ref | " + Folder + "/old.png\n", HiddenArt.List(VariantPath));
        }

        [Test]
        public void Clear_DryRunKeepsSpriteAndApplyOverridesOnVariantOnly()
        {
            var line = "old_skin/icon:Image.m_Sprite = " + Folder + "/old.png\n";
            Assert.AreEqual("dry run\n" + line, HiddenArt.Clear(VariantPath, new[] { "old_skin" }));
            Assert.IsNotNull(Icon(VariantPath).sprite);

            Assert.AreEqual("cleared\n" + line, HiddenArt.Clear(VariantPath, new[] { "old_skin" }, true));
            Assert.IsNull(Icon(VariantPath).sprite);
            Assert.IsNotNull(Icon(BasePath).sprite);
            Assert.IsNotNull(AssetDatabase.LoadAssetAtPath<GameObject>(VariantPath).GetComponent<Image>().sprite);
        }

        [Test]
        public void Clear_RejectsActiveRoot()
        {
            PrefabEdit.Edit(VariantPath, root => root.transform.Find("old_skin").gameObject.SetActive(true));
            Assert.Throws<ArgumentException>(() => HiddenArt.Clear(VariantPath, new[] { "old_skin" }));
        }

        [Test]
        public void Usage_DropsArtClearedOnVariantEvenThoughBaseStillHoldsIt()
        {
            StringAssert.Contains(Folder + "/old.png", AtlasAudit.Usage(false, VariantPath));
            HiddenArt.Clear(VariantPath, new[] { "old_skin" }, true);
            StringAssert.DoesNotContain(Folder + "/old.png", AtlasAudit.Usage(false, VariantPath));
        }

        private static Image Icon(string prefabPath) =>
            AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath).transform.Find("old_skin/icon").GetComponent<Image>();

        private static Sprite WriteSprite(string path)
        {
            var texture = new Texture2D(8, 8);
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }
    }
}
