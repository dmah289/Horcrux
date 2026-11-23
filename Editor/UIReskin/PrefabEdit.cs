using System;
using System.Collections.Generic;
using System.Text;
using Horcrux.Editor.Common;
using Horcrux.Editor.Utilities;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Horcrux.Editor.UIReskin
{
    /// <summary>
    /// Sửa prefab asset như mở Prefab Mode: nạp nội dung vào scene cô lập, gọi edit, lưu, dỡ. Sửa variant theo đường này thì
    /// thay đổi thành override của variant; sửa trên instance trong scene thì thay đổi không ghi lại sẽ mất khi lưu.
    /// </summary>
    public static class PrefabEdit
    {
        /// <summary>Edit ném thì không lưu, exception đi tiếp. Trả đường dẫn đã lưu.</summary>
        public static string Edit(string prefabPath, Action<GameObject> edit)
        {
            var root = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                edit(root);
                PrefabUtility.SaveAsPrefabAsset(root, prefabPath, out var saved);
                if (!saved)
                    throw new InvalidOperationException("không lưu được " + prefabPath);
                return prefabPath;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        /// <summary>
        /// Tạo variant của basePath tại variantPath (tạo thư mục nếu thiếu), rồi sửa bằng <see cref="Edit"/> nếu có edit.
        /// Variant đã tồn tại thì ném: ghi đè làm mất override đã author.
        /// </summary>
        public static string CreateVariant(string basePath, string variantPath, Action<GameObject> edit = null)
        {
            var baseAsset = AssetDatabase.LoadAssetAtPath<GameObject>(basePath);
            if (baseAsset == null)
                throw new ArgumentException("không có prefab " + basePath);
            if (AssetDatabase.LoadMainAssetAtPath(variantPath) != null)
                throw new InvalidOperationException("đã có " + variantPath);
            FolderUtility.CreateFolderRecursively(System.IO.Path.GetDirectoryName(variantPath));

            // instance dựng trong preview scene để scene đang mở không bị bẩn
            var scene = EditorSceneManager.NewPreviewScene();
            try
            {
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(baseAsset, scene);
                instance.name = System.IO.Path.GetFileNameWithoutExtension(variantPath);
                PrefabUtility.SaveAsPrefabAsset(instance, variantPath, out var saved);
                if (!saved)
                    throw new InvalidOperationException("không lưu được " + variantPath);
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(scene);
            }
            return edit == null ? variantPath : Edit(variantPath, edit);
        }

        /// <summary>
        /// Thay object cũ bằng một instance của prefab ở đúng chỗ: cùng cha, tên, thứ tự, transform (rect), trạng thái bật. Mọi ô trong
        /// cây chứa nó trỏ vào object cũ hay object con được nối sang object cùng đường dẫn, cùng kiểu trên instance mới; rồi xoá object cũ.
        /// </summary>
        /// <returns>Mỗi dòng một việc: <c>replace</c>, <c>remap</c>, và <c>unmapped</c> khi instance mới thiếu đích (ô để trống).</returns>
        public static string ReplaceWithPrefab(Transform old, GameObject prefab)
        {
            if (old.parent == null)
                throw new ArgumentException("không thay được root " + old.name);
            var log = new StringBuilder();
            var created = (GameObject)PrefabUtility.InstantiatePrefab(prefab, old.parent);
            created.name = old.name;
            created.transform.SetSiblingIndex(old.GetSiblingIndex());
            CopyPlacement(old, created.transform);
            created.SetActive(old.gameObject.activeSelf);
            log.Append("replace ").Append(old.name).Append(" -> ").Append(prefab.name).Append('\n');

            var root = old.root;
            foreach (var component in root.GetComponentsInChildren<Component>(true))
            {
                // Transform giữ cây cha con do Unity quản; object nằm trong cây cũ sẽ xoá cùng nó
                if (component == null || component is Transform || component.transform.IsChildOf(old) || component.transform.IsChildOf(created.transform))
                    continue;
                var so = new SerializedObject(component);
                var visitor = new ReferencesInto { Old = old, Paths = new List<string>() };
                SerializedPropertyWalker.Walk(so, ref visitor);
                foreach (var path in visitor.Paths)
                {
                    var property = so.FindProperty(path);
                    var replacement = Counterpart(property.objectReferenceValue, old, created.transform);
                    var field = component.GetType().Name + "." + path;
                    if (replacement == null)
                    {
                        log.Append("unmapped ").Append(field).Append(" (").Append(property.objectReferenceValue.GetType().Name).Append(")\n");
                        property.objectReferenceValue = null;
                        continue;
                    }
                    property.objectReferenceValue = replacement;
                    log.Append("remap ").Append(field).Append(" -> ").Append(replacement.GetType().Name).Append('\n');
                }
                so.ApplyModifiedPropertiesWithoutUndo();
            }
            Object.DestroyImmediate(old.gameObject);
            return log.ToString();
        }

        private static void CopyPlacement(Transform from, Transform to)
        {
            to.localRotation = from.localRotation;
            to.localScale = from.localScale;
            if (from is RectTransform a && to is RectTransform b)
            {
                b.anchorMin = a.anchorMin;
                b.anchorMax = a.anchorMax;
                b.pivot = a.pivot;
                b.sizeDelta = a.sizeDelta;
                b.anchoredPosition = a.anchoredPosition;
            }
            else
                to.localPosition = from.localPosition;
        }

        // Object cùng đường dẫn tương đối dưới instance mới; component thì thêm cùng kiểu. Không có thì null.
        private static Object Counterpart(Object value, Transform old, Transform created)
        {
            var transform = value is GameObject go ? go.transform : ((Component)value).transform;
            var relative = transform == old ? "" : AnimationUtility.CalculateTransformPath(transform, old);
            var target = relative.Length == 0 ? created : created.Find(relative);
            if (target == null)
                return null;
            return value is GameObject ? target.gameObject : (Object)target.GetComponent(value.GetType());
        }

        // đường dẫn property trỏ vào object cũ, con của nó, hay component trên chúng
        private struct ReferencesInto : IReferencePropertyVisitor
        {
            public Transform Old;
            public List<string> Paths;

            public void Visit(SerializedObject so, SerializedProperty property, RefPropertyKind kind)
            {
                if (kind != RefPropertyKind.ObjectReference)
                    return;
                var value = property.objectReferenceValue;
                var transform = value is GameObject go ? go.transform : value is Component c && c != null ? c.transform : null;
                if (transform != null && transform.IsChildOf(Old))
                    Paths.Add(property.propertyPath);
            }
        }

        /// <summary>Object con theo đường dẫn từ root (kể cả object tắt); không có thì ném, kèm tên các con trực tiếp của cha gần nhất.</summary>
        public static Transform Child(GameObject root, string path)
        {
            var found = root.transform.Find(path);
            if (found != null)
                return found;
            var slash = path.LastIndexOf('/');
            var parent = slash < 0 ? root.transform : root.transform.Find(path.Substring(0, slash));
            var names = "";
            if (parent != null)
                foreach (Transform c in parent)
                    names += (names.Length == 0 ? "" : ", ") + c.name;
            throw new ArgumentException("không có " + path + " dưới " + root.name + (parent != null ? "; con của cha: " + names : ""));
        }

        /// <summary>Asset duy nhất mang đúng tên trong folder (null là cả project). Không có hoặc trùng tên thì ném, kèm các đường dẫn.</summary>
        public static T AssetByName<T>(string name, string folder = null) where T : Object
        {
            var guids = AssetDatabase.FindAssets(name + " t:" + typeof(T).Name, folder == null ? null : new[] { folder });
            T match = null;
            var paths = "";
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(path))
                {
                    if (!(asset is T typed) || asset.name != name)
                        continue;
                    paths += " " + path;
                    if (match != null)
                        throw new ArgumentException("trùng tên " + name + ":" + paths);
                    match = typed;
                }
            }
            return match != null ? match : throw new ArgumentException("không có " + typeof(T).Name + " tên " + name);
        }
    }
}
