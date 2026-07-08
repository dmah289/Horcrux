using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Horcrux.Editor.NullRefScanner
{
    /// <summary>
    /// Logic quét null reference — static, không phụ thuộc UI.
    /// Dùng SerializedObject / SerializedProperty API để không cần reference đến runtime assembly.
    /// </summary>
    public static class NullRefScannerCore
    {
        // ──────────────── Internal property skip list ────────────────

        /// <summary>
        /// Các property thuộc base class của MonoBehaviour (Object → Component → Behaviour → MonoBehaviour).
        /// Next() iterate qua tất cả các property này nhưng chúng là internal Unity —
        /// không phải user field và nhiều cái luôn null by design (m_PrefabInstance, m_PrefabAsset,...).
        /// Phải skip để tránh false positive.
        /// </summary>
        private static readonly HashSet<string> InternalPropertyPaths = new()
        {
            "m_ObjectHideFlags",
            "m_CorrespondingSourceObject",
            "m_PrefabInstance",
            "m_PrefabAsset",
            "m_GameObject",
            "m_Enabled",
            "m_EditorHideFlags",
            "m_Script",
            "m_Name",
            "m_EditorClassIdentifier",
        };

        // ──────────────── Reusable buffers (non-alloc) ────────────────

        private static readonly List<Component> CompBuffer = new(16);
        private static readonly StringBuilder PathBuilder = new(128);
        private static readonly List<string>  PathParts   = new(8);

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
            // Non-alloc: reuse static buffer
            CompBuffer.Clear();
            go.GetComponents(CompBuffer);

            int goId = go.GetInstanceID();
            List<ComponentResult> compResults = null; // lazy alloc

            for (int i = 0; i < CompBuffer.Count; i++)
            {
                if (CompBuffer[i] == null)
                {
                    // Missing script detected
                    compResults ??= new List<ComponentResult>();
                    compResults.Add(new ComponentResult(
                        null, "Missing Script", true,
                        new List<FieldResult>(), goId, i));
                    continue;
                }

                // Bỏ qua built-in Unity components — chỉ quét user scripts (MonoBehaviour)
                if (CompBuffer[i] is not MonoBehaviour)
                    continue;

                ComponentResult cr = ScanComponent(CompBuffer[i], goId, i);
                if (cr != null)
                {
                    compResults ??= new List<ComponentResult>();
                    compResults.Add(cr);
                }
            }

            if (compResults == null)
                return null; // no issues on this GO

            return new GameObjectResult(go, go.name, compResults);
        }

        // ──────────────── Component scan via SerializedProperty ────────────────

        private static ComponentResult ScanComponent(Component component, int goInstanceId, int compIndex)
        {
            List<FieldResult> fields = null; // lazy alloc — most components are clean
            string componentName = component.GetType().Name;

            using (var so = new SerializedObject(component))
            {
                SerializedProperty iterator = so.GetIterator();
                bool enterChildren = true;

                while (iterator.Next(enterChildren))
                {
                    enterChildren = true;

                    // Skip internal Unity base class properties
                    if (InternalPropertyPaths.Contains(iterator.propertyPath))
                    {
                        enterChildren = false;
                        continue;
                    }

                    // ── AssetReference (Addressables) — serializable class chứa m_AssetGUID ──
                    if (IsAssetReferenceType(iterator.type))
                    {
                        SerializedProperty guidProp = iterator.FindPropertyRelative("m_AssetGUID");
                        if (guidProp != null)
                        {
                            if (string.IsNullOrEmpty(guidProp.stringValue))
                            {
                                fields ??= new List<FieldResult>();
                                fields.Add(new FieldResult(
                                    iterator.displayName, iterator.type,
                                    iterator.propertyPath,
                                    BuildDisplayPath(so, iterator.propertyPath),
                                    NullRefKind.NullAssetRef, componentName));
                            }

                            enterChildren = false;
                            continue;
                        }
                    }

                    // ── ExposedReference<T> (Timeline/Playable) ──
                    if (IsExposedReferenceType(iterator.type))
                    {
                        SerializedProperty nameProp = iterator.FindPropertyRelative("exposedName");
                        if (nameProp != null)
                        {
                            if (string.IsNullOrEmpty(nameProp.stringValue))
                            {
                                fields ??= new List<FieldResult>();
                                fields.Add(new FieldResult(
                                    iterator.displayName, iterator.type,
                                    iterator.propertyPath,
                                    BuildDisplayPath(so, iterator.propertyPath),
                                    NullRefKind.NullExposedRef, componentName));
                            }

                            enterChildren = false;
                            continue;
                        }
                    }

                    // ── ObjectReference (Transform, Image, GameObject,...) ──
                    if (iterator.propertyType == SerializedPropertyType.ObjectReference)
                    {
                        if (iterator.objectReferenceValue == null)
                        {
                            bool isMissing = iterator.objectReferenceInstanceIDValue != 0;
                            fields ??= new List<FieldResult>();
                            fields.Add(new FieldResult(
                                iterator.displayName, CleanTypeName(iterator.type),
                                iterator.propertyPath,
                                BuildDisplayPath(so, iterator.propertyPath),
                                isMissing ? NullRefKind.MissingRef : NullRefKind.Unassigned,
                                componentName));
                        }

                        enterChildren = false;
                        continue;
                    }

                    // ── [SerializeReference] — ManagedReference ──
#if UNITY_2021_2_OR_NEWER
                    if (iterator.propertyType == SerializedPropertyType.ManagedReference)
                    {
                        if (iterator.managedReferenceValue == null)
                        {
                            fields ??= new List<FieldResult>();
                            fields.Add(new FieldResult(
                                iterator.displayName,
                                string.IsNullOrEmpty(iterator.managedReferenceFieldTypename)
                                    ? "object"
                                    : CleanManagedTypeName(iterator.managedReferenceFieldTypename),
                                iterator.propertyPath,
                                BuildDisplayPath(so, iterator.propertyPath),
                                NullRefKind.NullManagedRef, componentName));

                            enterChildren = false;
                        }
                        continue;
                    }
#endif
                }
            }

            if (fields == null)
                return null;

            return new ComponentResult(component, componentName, false,
                                      fields, goInstanceId, compIndex);
        }

        // ──────────────── Display path builder ────────────────

        /// <summary>
        /// Chuyển propertyPath raw thành đường dẫn dễ đọc dùng displayName.
        /// Ví dụ: "enemies.Array.data[2].target" → "Enemies[2] > Target"
        /// Dùng static StringBuilder + List để giảm allocation.
        /// </summary>
        private static string BuildDisplayPath(SerializedObject so, string propertyPath)
        {
            string[] segments = propertyPath.Split('.');
            PathParts.Clear();

            // Build lookup path tích lũy bằng StringBuilder — tránh string.Join mỗi segment
            PathBuilder.Clear();

            for (int i = 0; i < segments.Length; i++)
            {
                string seg = segments[i];

                if (seg == "Array")
                    continue;

                if (seg.StartsWith("data[", StringComparison.Ordinal))
                {
                    int bracketStart = seg.IndexOf('[');
                    if (PathParts.Count > 0)
                        PathParts[PathParts.Count - 1] += seg.Substring(bracketStart);
                    continue;
                }

                // Build lookup path tích lũy
                if (PathBuilder.Length > 0)
                    PathBuilder.Append('.');
                PathBuilder.Append(seg);

                // Lookup không cần string.Join — dùng propertyPath cắt đúng vị trí
                // Tuy nhiên FindProperty cần string → dùng PathBuilder chỉ khi segment đơn giản
                // Fallback: dùng segments[0..i+1] nhưng build bằng StringBuilder
                string lookupPath = string.Join(".", segments, 0, i + 1);
                SerializedProperty prop = so.FindProperty(lookupPath);
                PathParts.Add(prop != null ? prop.displayName : seg);
            }

            // Join bằng StringBuilder — 1 allocation cuối cùng
            PathBuilder.Clear();
            for (int i = 0; i < PathParts.Count; i++)
            {
                if (i > 0) PathBuilder.Append(" > ");
                PathBuilder.Append(PathParts[i]);
            }
            return PathBuilder.ToString();
        }

        // ──────────────── Type name helpers ────────────────

        private static bool IsAssetReferenceType(string type)
        {
            return type != null && type.StartsWith("AssetReference", StringComparison.Ordinal);
        }

        private static bool IsExposedReferenceType(string type)
        {
            return type != null && type.StartsWith("ExposedReference", StringComparison.Ordinal);
        }

        /// <summary>Strip "PPtr&lt;$Transform&gt;" → "Transform".</summary>
        private static string CleanTypeName(string rawType)
        {
            if (rawType != null && rawType.StartsWith("PPtr<$", StringComparison.Ordinal))
                return rawType.Substring(6, rawType.Length - 7);
            return rawType ?? "Object";
        }

        /// <summary>Strip "assemblyName Namespace.TypeName" → "TypeName".</summary>
        private static string CleanManagedTypeName(string fullTypeName)
        {
            int lastDot = fullTypeName.LastIndexOf('.');
            if (lastDot >= 0 && lastDot < fullTypeName.Length - 1)
                return fullTypeName.Substring(lastDot + 1);

            int space = fullTypeName.IndexOf(' ');
            if (space >= 0 && space < fullTypeName.Length - 1)
                return fullTypeName.Substring(space + 1);

            return fullTypeName;
        }
    }
}
