using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Horcrux.Editor.NullRefScanner
{
    /// <summary>
    /// Logic quét null reference — static, không phụ thuộc UI.
    /// Dùng SerializedObject / SerializedProperty API để không cần reference đến runtime assembly.
    /// </summary>
    public static class NullRefScannerCore
    {
        // ──────────────── Public entry points ────────────────

        /// <summary>Quét danh sách root GameObjects + children đệ quy (Scene / Selection scope).</summary>
        public static List<GameObjectResult> ScanGameObjects(IList<GameObject> roots)
        {
            var results = new List<GameObjectResult>();
            for (int i = 0; i < roots.Count; i++)
            {
                if (roots[i] != null)
                    ScanRecursive(roots[i], results);
            }
            return results;
        }

        /// <summary>Lấy danh sách prefab root đang chọn trong Project window.</summary>
        public static List<GameObject> GetSelectedPrefabs()
        {
            var prefabs = new List<GameObject>();
            Object[] selected = Selection.objects;
            for (int i = 0; i < selected.Length; i++)
            {
                if (selected[i] is GameObject go && PrefabUtility.IsPartOfPrefabAsset(go))
                    prefabs.Add(go);
            }
            return prefabs;
        }

        // ──────────────── Recursive traversal ────────────────

        private static void ScanRecursive(GameObject go, List<GameObjectResult> results)
        {
            GameObjectResult goResult = ScanSingleGameObject(go);
            if (goResult != null)
                results.Add(goResult);

            Transform t = go.transform;
            for (int i = 0; i < t.childCount; i++)
                ScanRecursive(t.GetChild(i).gameObject, results);
        }

        // ──────────────── Single GO scan ────────────────

        private static GameObjectResult ScanSingleGameObject(GameObject go)
        {
            Component[] components = go.GetComponents<Component>();
            var compResults = new List<ComponentResult>();

            for (int i = 0; i < components.Length; i++)
            {
                if (components[i] == null)
                {
                    // Missing script detected
                    compResults.Add(new ComponentResult
                    {
                        component      = null,
                        componentName  = "Missing Script",
                        isMissingScript = true,
                        fields         = new List<FieldResult>()
                    });
                    continue;
                }

                // Bỏ qua built-in Unity components — chỉ quét user scripts (MonoBehaviour)
                // Built-in components (MeshRenderer, Camera,...) dùng custom Inspector
                // nên displayName khác trên Inspector, và nhiều trường bị ẩn gây nhầm lẫn
                if (components[i] is not MonoBehaviour)
                    continue;

                ComponentResult cr = ScanComponent(components[i]);
                if (cr != null)
                    compResults.Add(cr);
            }

            if (compResults.Count == 0)
                return null; // no issues on this GO

            return new GameObjectResult
            {
                gameObject     = go,
                gameObjectName = go.name,
                components     = compResults
            };
        }

        // ──────────────── Component scan via SerializedProperty ────────────────

        private static ComponentResult ScanComponent(Component component)
        {
            var fields = new List<FieldResult>();

            using (var so = new SerializedObject(component))
            {
                SerializedProperty iterator = so.GetIterator();
                bool enterChildren = true;

                while (iterator.NextVisible(enterChildren))
                {
                    enterChildren = true;

                    // Skip the script reference field itself
                    if (iterator.propertyPath == "m_Script")
                    {
                        enterChildren = false;
                        continue;
                    }

                    // Check AssetReference (Addressables) — serializable class chứa m_AssetGUID
                    if (IsAssetReferenceType(iterator.type))
                    {
                        SerializedProperty guidProp = iterator.FindPropertyRelative("m_AssetGUID");
                        if (guidProp != null)
                        {
                            if (string.IsNullOrEmpty(guidProp.stringValue))
                            {
                                fields.Add(new FieldResult
                                {
                                    fieldName     = iterator.displayName,
                                    fieldTypeName = iterator.type,
                                    propertyPath  = iterator.propertyPath,
                                    displayPath   = BuildDisplayPath(so, iterator.propertyPath)
                                });
                            }

                            // Skip AssetReference internals — không iterate vào m_AssetGUID, m_SubObjectName,...
                            enterChildren = false;
                            continue;
                        }
                        // guidProp null → không phải AssetReference thật, fall through xử lý bình thường
                    }

                    // Check null ObjectReference (Transform, Image, GameObject,...)
                    if (iterator.propertyType == SerializedPropertyType.ObjectReference)
                    {
                        if (iterator.objectReferenceValue == null)
                        {
                            fields.Add(new FieldResult
                            {
                                fieldName     = iterator.displayName,
                                fieldTypeName = CleanTypeName(iterator.type),
                                propertyPath  = iterator.propertyPath,
                                displayPath   = BuildDisplayPath(so, iterator.propertyPath)
                            });
                        }

                        // ObjectReference is a leaf — don't enter children
                        enterChildren = false;
                    }
                }
            }

            if (fields.Count == 0)
                return null;

            return new ComponentResult
            {
                component      = component,
                componentName  = component.GetType().Name,
                isMissingScript = false,
                fields         = fields
            };
        }

        // ──────────────── Display path builder ────────────────

        /// <summary>
        /// Chuyển propertyPath raw thành đường dẫn dễ đọc dùng displayName.
        /// Ví dụ: "enemies.Array.data[2].target" → "Enemies[2] > Target"
        ///        "settings.prefab"               → "Settings > Prefab"
        ///        "myTransform"                    → "My Transform"
        /// </summary>
        private static string BuildDisplayPath(SerializedObject so, string propertyPath)
        {
            // Split theo "." — mỗi segment là 1 tầng
            string[] segments = propertyPath.Split('.');
            var parts = new System.Collections.Generic.List<string>();
            string currentPath = "";

            for (int i = 0; i < segments.Length; i++)
            {
                // Skip "Array" segment — nó luôn đi kèm "data[N]"
                if (segments[i] == "Array")
                    continue;

                // "data[N]" → lấy [N] gắn vào part trước
                if (segments[i].StartsWith("data["))
                {
                    int bracketStart = segments[i].IndexOf('[');
                    string index = segments[i].Substring(bracketStart); // "[2]"
                    if (parts.Count > 0)
                        parts[parts.Count - 1] += index;
                    continue;
                }

                // Build path tích lũy để FindProperty lấy displayName
                currentPath = i == 0 ? segments[i] : currentPath + "." + segments[i];

                // Nếu segment trước là "Array.data[N]" thì path thực phải qua đó
                // → dùng propertyPath cắt đến segment hiện tại
                string lookupPath = string.Join(".", segments, 0, i + 1);
                SerializedProperty prop = so.FindProperty(lookupPath);
                string displayName = prop != null ? prop.displayName : segments[i];

                parts.Add(displayName);
            }

            return string.Join(" > ", parts);
        }

        // ──────────────── Type name helpers ────────────────

        /// <summary>
        /// Kiểm tra property type có phải AssetReference hoặc subclass
        /// (AssetReferenceGameObject, AssetReferenceTexture,...).
        /// </summary>
        private static bool IsAssetReferenceType(string type)
        {
            return type != null && type.StartsWith("AssetReference");
        }

        /// <summary>
        /// SerializedProperty.type trả về "PPtr&lt;$Transform&gt;" cho ObjectReference.
        /// Strip prefix/suffix để chỉ còn type name sạch.
        /// </summary>
        private static string CleanTypeName(string rawType)
        {
            // "PPtr<$Transform>" -> "Transform"
            if (rawType != null && rawType.StartsWith("PPtr<$"))
                return rawType.Substring(6, rawType.Length - 7);
            return rawType ?? "Object";
        }
    }
}
