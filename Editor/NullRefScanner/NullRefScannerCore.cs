using System.Collections.Generic;
using Horcrux.Editor.Common;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Horcrux.Editor.NullRefScanner
{
    /// <summary>
    /// Logic quét null reference — static, không phụ thuộc UI.
    /// Traversal SerializedProperty được ủy quyền cho <see cref="SerializedPropertyWalker"/> (dùng chung);
    /// core này chỉ quyết định "reference nào là null" qua <see cref="NullCheckVisitor"/>.
    /// </summary>
    public static class NullRefScannerCore
    {
        // ──────────────── Reusable buffers (non-alloc) ────────────────

        private static readonly List<Component> CompBuffer = new(16);

        // ──────────────── Null-check visitor ────────────────

        /// <summary>
        /// Visitor: ghi lại field khi reference đang null (theo từng RefPropertyKind).
        /// Struct + ref-pass → không boxing, list tích lũy tồn tại qua các callback.
        /// </summary>
        private struct NullCheckVisitor : IReferencePropertyVisitor
        {
            public string            ComponentName;
            public List<FieldResult> Fields; // lazy alloc — phần lớn component sạch

            public void Visit(SerializedObject so, SerializedProperty p, RefPropertyKind kind)
            {
                switch (kind)
                {
                    case RefPropertyKind.AssetReference:
                    {
                        SerializedProperty guid = p.FindPropertyRelative("m_AssetGUID");
                        if (guid != null && string.IsNullOrEmpty(guid.stringValue))
                            Add(so, p, p.type, NullRefKind.NullAssetRef);
                        break;
                    }
                    case RefPropertyKind.ExposedReference:
                    {
                        SerializedProperty name = p.FindPropertyRelative("exposedName");
                        if (name != null && string.IsNullOrEmpty(name.stringValue))
                            Add(so, p, p.type, NullRefKind.NullExposedRef);
                        break;
                    }
                    case RefPropertyKind.ObjectReference:
                    {
                        if (p.objectReferenceValue == null)
                        {
                            bool isMissing = p.objectReferenceInstanceIDValue != 0;
                            Add(so, p, SerializedPropertyWalker.CleanTypeName(p.type),
                                isMissing ? NullRefKind.MissingRef : NullRefKind.Unassigned);
                        }
                        break;
                    }
#if UNITY_2021_2_OR_NEWER
                    case RefPropertyKind.ManagedReference:
                    {
                        if (p.managedReferenceValue == null)
                        {
                            string typeName = string.IsNullOrEmpty(p.managedReferenceFieldTypename)
                                ? "object"
                                : SerializedPropertyWalker.CleanManagedTypeName(p.managedReferenceFieldTypename);
                            Add(so, p, typeName, NullRefKind.NullManagedRef);
                        }
                        break;
                    }
#endif
                }
            }

            private void Add(SerializedObject so, SerializedProperty p, string typeName, NullRefKind kind)
            {
                Fields ??= new List<FieldResult>();
                Fields.Add(new FieldResult(
                    p.displayName, typeName, p.propertyPath,
                    SerializedPropertyWalker.BuildDisplayPath(so, p.propertyPath),
                    kind, ComponentName));
            }
        }

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

        // ──────────────── Component scan via walker ────────────────

        private static ComponentResult ScanComponent(Component component, int goInstanceId, int compIndex)
        {
            string componentName = component.GetType().Name;

            using var so = new SerializedObject(component);
            var visitor = new NullCheckVisitor { ComponentName = componentName };
            SerializedPropertyWalker.Walk(so, ref visitor);

            if (visitor.Fields == null)
                return null;

            return new ComponentResult(component, componentName, false,
                                      visitor.Fields, goInstanceId, compIndex);
        }
    }
}
