using Horcrux.Editor.UIReskin;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Horcrux.Tests
{
    /// <summary>
    /// Đẩy override lên prefab gốc: chuỗi card → coin → pack_1, pack_2, cạnh nhánh card → bundle. Override của pack_1 lên coin
    /// thì pack_2 nhận theo, card và bundle giữ nguyên; target ngoài chuỗi thì ném trước khi ghi.
    /// </summary>
    public sealed class PrefabAuditTests
    {
        private const string Folder = "Assets/HorcruxPrefabAuditTestTemp";
        private static readonly Vector2 BaseSize = new Vector2(300, 106);
        private static readonly Vector2 EditedSize = new Vector2(302, 138);

        [SetUp]
        public void SetUp() => AssetDatabase.CreateFolder("Assets", "HorcruxPrefabAuditTestTemp");

        [TearDown]
        public void TearDown() => AssetDatabase.DeleteAsset(Folder);

        [Test]
        public void ApplyOverrides_PushesToChosenBase_SiblingsFollow_OtherBranchKeeps()
        {
            var card = Folder + "/card.prefab";
            var root = new GameObject("card", typeof(RectTransform));
            UIBuild.Node(root.transform, "btn", BaseSize);
            PrefabUtility.SaveAsPrefabAsset(root, card);
            Object.DestroyImmediate(root);

            var coin = PrefabEdit.CreateVariant(card, Folder + "/coin.prefab");
            var bundle = PrefabEdit.CreateVariant(card, Folder + "/bundle.prefab");
            var pack1 = PrefabEdit.CreateVariant(coin, Folder + "/pack_1.prefab",
                go => ((RectTransform)PrefabEdit.Child(go, "btn")).sizeDelta = EditedSize);
            var pack2 = PrefabEdit.CreateVariant(coin, Folder + "/pack_2.prefab");

            System.Func<string, bool> size = line => line.Contains("m_SizeDelta");

            var dry = PrefabAudit.ApplyOverrides(pack1, size, coin);
            StringAssert.StartsWith("dry run to " + coin, dry);
            StringAssert.Contains("btn:RectTransform.m_SizeDelta.y = 138", dry);
            Assert.AreEqual(BaseSize, BtnSize(pack2), "dry run không ghi");

            Assert.Throws<System.ArgumentException>(() => PrefabAudit.ApplyOverrides(pack1, size, bundle, apply: true));
            Assert.AreEqual(BaseSize, BtnSize(bundle), "target ngoài chuỗi không ghi gì");

            PrefabAudit.ApplyOverrides(pack1, size, coin, apply: true);
            Assert.AreEqual(EditedSize, BtnSize(coin));
            Assert.AreEqual(EditedSize, BtnSize(pack2), "variant anh em nhận từ gốc");
            Assert.AreEqual(EditedSize, BtnSize(pack1));
            StringAssert.DoesNotContain("m_SizeDelta", PrefabAudit.ListOverrides(pack1), "override rời variant");
            Assert.AreEqual(BaseSize, BtnSize(card), "gốc trên target giữ nguyên");
            Assert.AreEqual(BaseSize, BtnSize(bundle), "nhánh khác giữ nguyên");
        }

        private static Vector2 BtnSize(string prefabPath) =>
            ((RectTransform)AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath).transform.Find("btn")).sizeDelta;
    }
}
