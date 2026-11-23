using Horcrux.Editor.Probe;
using Horcrux.Editor.UIReskin;
using Horcrux.Runtime.Utilities.UI;
using LightSide;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Horcrux.Tests
{
    /// <summary>
    /// Thay một con bằng prefab giữ chỗ và nối lại reference, đặt chữ UniText theo autosize, in wiring của component logic.
    /// Dựng trong preview scene để scene đang mở không bẩn.
    /// </summary>
    public sealed class PrefabEditReplaceTests
    {
        private const string Folder = "Assets/HorcruxPrefabReplaceTestTemp";
        private Scene _scene;
        private GameObject _prefab;

        [SetUp]
        public void SetUp()
        {
            AssetDatabase.CreateFolder("Assets", "HorcruxPrefabReplaceTestTemp");
            var source = new GameObject("btn", typeof(RectTransform), typeof(Image));
            new GameObject("icon", typeof(RectTransform), typeof(Image)).transform.SetParent(source.transform, false);
            _prefab = PrefabUtility.SaveAsPrefabAsset(source, Folder + "/btn.prefab");
            Object.DestroyImmediate(source);
            _scene = EditorSceneManager.NewPreviewScene();
        }

        [TearDown]
        public void TearDown()
        {
            EditorSceneManager.ClosePreviewScene(_scene);
            AssetDatabase.DeleteAsset(Folder);
        }

        // Đối chứng đã chạy: bỏ CopyPlacement → đỏ ở sizeDelta (100,100); bỏ dòng gán remap → đỏ ở selfRect.
        [Test]
        public void ReplaceWithPrefab_KeepsSlot_AndRemapsReferences()
        {
            var root = Node("root", null);
            var keep = Node("a", root.transform);
            var old = Node("old", root.transform);
            Node("b", root.transform);
            var oldIcon = Node("icon", old.transform).AddComponent<Image>();
            var extra = Node("extra", old.transform).AddComponent<Image>();
            var oldRect = (RectTransform)old.transform;
            oldRect.sizeDelta = new Vector2(120, 40);
            oldRect.anchoredPosition = new Vector2(5, 6);
            old.SetActive(false);

            Wire(root.AddComponent<FlyRewardItem>(), oldRect, oldIcon);
            var other = keep.AddComponent<FlyRewardItem>();
            Wire(other, (RectTransform)keep.transform, extra);

            var log = PrefabEdit.ReplaceWithPrefab(old.transform, _prefab);

            Assert.AreEqual(3, root.transform.childCount, log);
            var created = root.transform.GetChild(1);
            Assert.AreEqual("old", created.name);
            Assert.AreSame(_prefab, PrefabUtility.GetCorrespondingObjectFromSource(created.gameObject));
            Assert.AreEqual(new Vector2(120, 40), ((RectTransform)created).sizeDelta);
            Assert.AreEqual(new Vector2(5, 6), ((RectTransform)created).anchoredPosition);
            Assert.IsFalse(created.gameObject.activeSelf);

            var wired = new SerializedObject(root.GetComponent<FlyRewardItem>());
            Assert.AreSame(created, wired.FindProperty("selfRect").objectReferenceValue, log);
            Assert.AreSame(created.Find("icon").GetComponent<Image>(), wired.FindProperty("icon").objectReferenceValue, log);

            var unmapped = new SerializedObject(other);
            Assert.AreSame(keep.transform, unmapped.FindProperty("selfRect").objectReferenceValue);
            Assert.IsTrue(unmapped.FindProperty("icon").objectReferenceValue == null);
            StringAssert.Contains("unmapped FlyRewardItem.icon", log);
        }

        [Test]
        public void Refs_PrintsTreePath_Asset_AndNull()
        {
            var root = Node("root", null);
            var child = Node("c", root.transform);
            Wire(root.AddComponent<FlyRewardItem>(), (RectTransform)child.transform, _prefab.GetComponent<Image>());
            Wire(child.AddComponent<FlyRewardItem>(), null, null);

            var refs = PrefabDump.Refs(root);

            StringAssert.Contains("root:FlyRewardItem.selfRect -> c (RectTransform)\n", refs);
            StringAssert.Contains("root:FlyRewardItem.icon -> asset btn (Image)\n", refs);
            StringAssert.Contains("c:FlyRewardItem.icon -> null\n", refs);
            var filtered = PrefabDump.Refs(root, "c");
            StringAssert.StartsWith("c:FlyRewardItem.", filtered);
            StringAssert.DoesNotContain("root:", filtered);
        }

        // Đối chứng đã chạy: không đặt MaxFontSize → đỏ, MaxFontSize vẫn mặc định 72.
        [Test]
        public void SetText_AutoSizeAlsoSetsMaxFontSize()
        {
            var label = Node("txt", null).AddComponent<UniText>();
            label.AutoSize = true;
            UIBuild.SetText(label.transform, "Quit", 82f);
            Assert.AreEqual("Quit", label.Text);
            Assert.AreEqual(82f, label.MaxFontSize);

            label.AutoSize = false;
            UIBuild.SetText(label.transform, "Try again", 50f);
            Assert.AreEqual(50f, label.FontSize);
            Assert.AreEqual(82f, label.MaxFontSize);
        }

        private GameObject Node(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            if (parent == null)
                SceneManager.MoveGameObjectToScene(go, _scene);
            else
                go.transform.SetParent(parent, false);
            return go;
        }

        private static void Wire(FlyRewardItem item, RectTransform self, Image icon)
        {
            var so = new SerializedObject(item);
            so.FindProperty("selfRect").objectReferenceValue = self;
            so.FindProperty("icon").objectReferenceValue = icon;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
