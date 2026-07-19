using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Horcrux.Editor.Common
{
    /// <summary>
    /// Static utility dùng chung — selection, ping, prefab-stage navigation.
    /// Tách khỏi tool cụ thể để mọi Editor tool (NullRefScanner, UsageFinder, SceneReferenceFinder)
    /// đều điều hướng nhất quán tới scene object lẫn prefab asset.
    /// </summary>
    public static class NavigationHelper
    {
        /// <summary>
        /// Select + ping một asset object (SO/material/asset con). Nếu là GameObject (prefab) →
        /// dùng <see cref="SelectAndPingProperty"/> để mở prefab stage. Với asset thường: select và
        /// expand đúng property trong Inspector (delay để Inspector cập nhật).
        /// </summary>
        public static void SelectAndExpandAsset(Object assetObject, Component component, string propertyPath)
        {
            if (assetObject == null) return;

            if (assetObject is GameObject go)
            {
                SelectAndPingProperty(go, component, propertyPath);
                return;
            }

            Selection.activeObject = assetObject;
            EditorGUIUtility.PingObject(assetObject);

            if (!string.IsNullOrEmpty(propertyPath))
                EditorApplication.delayCall += () => ExpandAssetProperty(assetObject, propertyPath);
        }

        /// <summary>Select và ping GameObject — hỗ trợ cả scene object và prefab asset.</summary>
        public static void SelectAndPing(GameObject go)
        {
            if (IsPrefabAsset(go))
                OpenPrefabAndSelect(go);
            else
            {
                Selection.activeGameObject = go;
                EditorGUIUtility.PingObject(go);
            }
        }

        /// <summary>
        /// Select GO, expand đúng component chứa trường quan tâm trong Inspector.
        /// Nếu là prefab asset → mở prefab stage trước.
        /// </summary>
        public static void SelectAndPingProperty(GameObject go, Component comp, string propertyPath)
        {
            if (IsPrefabAsset(go))
                OpenPrefabAndSelect(go);
            else
            {
                Selection.activeGameObject = go;
                EditorGUIUtility.PingObject(go);
            }

            if (comp == null) return;

            // Delay để đợi prefab stage mở xong / Inspector cập nhật
            EditorApplication.delayCall += () => ExpandComponent(comp);
        }

        // ──────────────── Internal helpers ────────────────

        /// <summary>Chọn asset và expand đúng property trong Inspector qua ActiveEditorTracker.</summary>
        private static void ExpandAssetProperty(Object assetObject, string propertyPath)
        {
            var tracker = ActiveEditorTracker.sharedTracker;
            var editors = tracker.activeEditors;
            for (int i = 0; i < editors.Length; i++)
            {
                if (editors[i].target == assetObject)
                {
                    tracker.SetVisible(i, 1);
                    editors[i].serializedObject.Update();
                    SerializedProperty prop = editors[i].serializedObject.FindProperty(propertyPath);
                    if (prop != null)
                        prop.isExpanded = true;
                    break;
                }
            }
        }

        private static bool IsPrefabAsset(GameObject go)
        {
            return go != null && PrefabUtility.IsPartOfPrefabAsset(go);
        }

        /// <summary>Mở prefab stage và select đúng GO bên trong prefab.</summary>
        private static void OpenPrefabAndSelect(GameObject go)
        {
            GameObject prefabRoot = PrefabUtility.GetOutermostPrefabInstanceRoot(go);
            if (prefabRoot == null)
                prefabRoot = go.transform.root.gameObject;

            string assetPath = AssetDatabase.GetAssetPath(prefabRoot);
            if (string.IsNullOrEmpty(assetPath))
                assetPath = AssetDatabase.GetAssetPath(go);

            if (!string.IsNullOrEmpty(assetPath))
            {
                AssetDatabase.OpenAsset(AssetDatabase.LoadAssetAtPath<GameObject>(assetPath));

                var targetPath = GetTransformPath(go.transform);
                EditorApplication.delayCall += () =>
                {
                    var stage = PrefabStageUtility.GetCurrentPrefabStage();
                    if (stage != null)
                    {
                        Transform found = stage.prefabContentsRoot.transform;
                        if (!string.IsNullOrEmpty(targetPath))
                        {
                            var child = stage.prefabContentsRoot.transform.Find(targetPath);
                            if (child != null) found = child;
                        }
                        Selection.activeGameObject = found.gameObject;
                        EditorGUIUtility.PingObject(found.gameObject);
                    }
                };
            }
        }

        /// <summary>Lấy path tương đối từ root đến transform (dùng để tìm lại trong prefab stage).</summary>
        private static string GetTransformPath(Transform t)
        {
            var parts = new List<string>();
            Transform current = t;
            while (current.parent != null)
            {
                parts.Add(current.name);
                current = current.parent;
            }
            parts.Reverse();
            return string.Join("/", parts);
        }

        /// <summary>Expand component trong Inspector thông qua ActiveEditorTracker.</summary>
        private static void ExpandComponent(Component comp)
        {
            var tracker = ActiveEditorTracker.sharedTracker;
            var editors = tracker.activeEditors;
            for (int i = 0; i < editors.Length; i++)
            {
                if (editors[i].target == comp)
                {
                    tracker.SetVisible(i, 1);
                    break;
                }
            }
        }
    }
}
