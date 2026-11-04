using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Horcrux.Editor.UIReskin
{
    /// <summary>
    /// Art trên skin cũ đã tắt. Skin cũ tắt để giữ cây và merge với prefab gốc, nhưng sprite, texture nó giữ vẫn nạp theo prefab,
    /// kéo theo cả trang atlas chứa chúng. Reskin không dùng lại art kiểu cũ nên gỡ reference art, giữ object. Gỡ luôn chạy thử
    /// trước (apply false), in đúng dòng sẽ gỡ.
    /// </summary>
    public static class HiddenArt
    {
        /// <summary>
        /// Mỗi gốc tắt một dòng <c>gốc | N ref | texture, ...</c>. Gốc là object tắt ngoài cùng (<c>""</c> khi chính root tắt), hay
        /// <c>object:Type</c> cho component tắt trên object đang bật. Gốc tắt có thể là skin cũ, cũng có thể là trạng thái code bật
        /// lên (huy hiệu, ô thưởng): đọc từng dòng trước khi chọn.
        /// </summary>
        public static string List(string prefabPath)
        {
            var byRoot = new SortedDictionary<string, List<string>>();
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            foreach (var component in prefab.GetComponentsInChildren<Component>(true))
            {
                if (component == null) continue;
                var hidden = OutermostInactive(component.transform, prefab.transform);
                string key;
                if (hidden != null)
                    key = PathOf(prefab, hidden);
                else if (component is Behaviour { enabled: false })
                    key = PathOf(prefab, component.transform) + ":" + component.GetType().Name;
                else
                    continue;

                foreach (var reference in ArtReferences(component))
                {
                    if (!byRoot.TryGetValue(key, out var textures)) byRoot[key] = textures = new List<string>();
                    textures.Add(AssetDatabase.GetAssetPath(reference.objectReferenceValue));
                }
            }

            var sb = new StringBuilder();
            foreach (var pair in byRoot)
                sb.Append(pair.Key).Append(" | ").Append(pair.Value.Count).Append(" ref | ")
                    .Append(string.Join(", ", pair.Value.Distinct().OrderBy(t => t))).Append('\n');
            return sb.ToString();
        }

        /// <summary>
        /// Gỡ mọi Sprite, Texture2D mà component dưới các gốc (đường dẫn object từ root, như <see cref="PrefabEdit.Child"/>) giữ,
        /// object và component giữ nguyên. Gốc đang bật thì ném: gỡ art trên đó làm hỏng hình đang hiện. Trên variant, gỡ thành
        /// override của variant. Mỗi dòng <c>object:Type.property = texture</c>.
        /// </summary>
        public static string Clear(string prefabPath, string[] roots, bool apply = false)
        {
            var sb = new StringBuilder();
            Action<GameObject> clear = root =>
            {
                foreach (var path in roots)
                {
                    var node = PrefabEdit.Child(root, path);
                    if (OutermostInactive(node, root.transform) == null)
                        throw new ArgumentException(path + " đang bật trong " + prefabPath);
                    foreach (var component in node.GetComponentsInChildren<Component>(true))
                    {
                        if (component == null) continue;
                        var serialized = new SerializedObject(component);
                        var changed = false;
                        foreach (var reference in ArtReferences(serialized))
                        {
                            sb.Append(PathOf(root, component.transform)).Append(':').Append(component.GetType().Name).Append('.')
                                .Append(reference.propertyPath).Append(" = ").Append(AssetDatabase.GetAssetPath(reference.objectReferenceValue)).Append('\n');
                            reference.objectReferenceValue = null;
                            changed = true;
                        }

                        if (apply && changed)
                            serialized.ApplyModifiedPropertiesWithoutUndo();
                    }
                }
            };

            if (apply)
                PrefabEdit.Edit(prefabPath, clear);
            else
            {
                var root = PrefabUtility.LoadPrefabContents(prefabPath);
                try
                {
                    clear(root);
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }

            return (apply ? "cleared\n" : "dry run\n") + sb;
        }

        private static IEnumerable<SerializedProperty> ArtReferences(Component component) => ArtReferences(new SerializedObject(component));

        // Mảng (sprite của UV module ParticleSystem) đi từng phần tử; property trả ra là con trỏ của iterator, dùng ngay trong vòng lặp.
        private static IEnumerable<SerializedProperty> ArtReferences(SerializedObject serialized)
        {
            var property = serialized.GetIterator();
            while (property.Next(true))
                if (property.propertyType == SerializedPropertyType.ObjectReference && property.objectReferenceValue is Sprite or Texture2D)
                    yield return property;
        }

        private static Transform OutermostInactive(Transform node, Transform root)
        {
            Transform hidden = null;
            for (var t = node; t != null; t = t == root ? null : t.parent)
                if (!t.gameObject.activeSelf) hidden = t;
            return hidden;
        }

        private static string PathOf(GameObject root, Transform t) =>
            t == root.transform ? "" : AnimationUtility.CalculateTransformPath(t, root.transform);
    }
}
