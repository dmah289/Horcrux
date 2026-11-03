using System;
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
