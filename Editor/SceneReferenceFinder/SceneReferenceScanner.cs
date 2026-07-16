using System.Collections.Generic;
using Horcrux.Editor.Common;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Horcrux.Editor.SceneReferenceFinder
{
    /// <summary>
    /// Tìm mọi component trong scope đang trỏ (ObjectReference) tới target GameObject/Component.
    /// Trả lời "xóa cái này có gây null reference ở đâu không?".
    ///
    /// Target là GameObject → match cả reference tới GO lẫn tới mọi Component trên nó
    /// (build sẵn set instanceID). Static, dùng chung <see cref="SerializedPropertyWalker"/>.
    /// </summary>
    public static class SceneReferenceScanner
    {
        // ──────────────── Reusable buffers (non-alloc) ────────────────

        private static readonly HashSet<int>    TargetIds  = new();
        private static readonly List<Component>  CompBuffer = new(16);

        // ──────────────── Visitor: match objectReferenceValue trong target set ────────────────

        private struct RefMatchVisitor : IReferencePropertyVisitor
        {
            public HashSet<int>                TargetIds;
            public string                      ComponentName;
            public List<SceneRefFieldResult>   Fields; // lazy alloc

            public void Visit(SerializedObject so, SerializedProperty p, RefPropertyKind kind)
            {
                if (kind != RefPropertyKind.ObjectReference)
                    return;

                // Match theo instanceID để không cần load object (nhanh + đúng cả khi ref tới Component)
                int id = p.objectReferenceInstanceIDValue;
                if (id == 0 || !TargetIds.Contains(id))
                    return;

                Fields ??= new List<SceneRefFieldResult>();
                Fields.Add(new SceneRefFieldResult(
                    SerializedPropertyWalker.CleanTypeName(p.type),
                    p.propertyPath,
                    SerializedPropertyWalker.BuildDisplayPath(so, p.propertyPath),
                    ComponentName));
            }
        }

        // ──────────────── Public entry ────────────────

        /// <summary>
        /// Quét <paramref name="roots"/> + con cháu, tìm component trỏ tới <paramref name="target"/>.
        /// <paramref name="target"/> có thể là GameObject hoặc Component.
        /// </summary>
        public static List<SceneRefObjectResult> Scan(Object target, IList<GameObject> roots)
        {
            var results = new List<SceneRefObjectResult>();
            if (target == null || roots == null)
                return results;

            BuildTargetIds(target);
            if (TargetIds.Count == 0)
                return results;

            for (int i = 0; i < roots.Count; i++)
            {
                if (roots[i] != null)
                    ScanRecursive(roots[i], results);
            }

            return results;
        }

        // ──────────────── Target id set ────────────────

        /// <summary>
        /// GO target → gồm instanceID của GO + mọi Component của nó (xóa GO là xóa hết component).
        /// Component target → chỉ instanceID của component đó.
        /// </summary>
        private static void BuildTargetIds(Object target)
        {
            TargetIds.Clear();

            if (target is GameObject go)
            {
                TargetIds.Add(go.GetInstanceID());
                CompBuffer.Clear();
                go.GetComponents(CompBuffer);
                for (int i = 0; i < CompBuffer.Count; i++)
                {
                    if (CompBuffer[i] != null)
                        TargetIds.Add(CompBuffer[i].GetInstanceID());
                }
            }
            else if (target is Component comp)
            {
                TargetIds.Add(comp.GetInstanceID());
            }
        }

        // ──────────────── Traversal ────────────────

        private static void ScanRecursive(GameObject go, List<SceneRefObjectResult> results)
        {
            SceneRefObjectResult r = ScanSingleGameObject(go);
            if (r != null)
                results.Add(r);

            Transform t = go.transform;
            for (int i = 0; i < t.childCount; i++)
                ScanRecursive(t.GetChild(i).gameObject, results);
        }

        private static SceneRefObjectResult ScanSingleGameObject(GameObject go)
        {
            CompBuffer.Clear();
            go.GetComponents(CompBuffer);

            int goId = go.GetInstanceID();
            List<SceneRefComponentResult> compResults = null;

            for (int i = 0; i < CompBuffer.Count; i++)
            {
                Component c = CompBuffer[i];
                if (c == null || c is not MonoBehaviour)
                    continue; // chỉ user scripts mới chứa reference cần kiểm

                string componentName = c.GetType().Name;

                using var so = new SerializedObject(c);
                var visitor = new RefMatchVisitor { TargetIds = TargetIds, ComponentName = componentName };
                SerializedPropertyWalker.Walk(so, ref visitor);

                if (visitor.Fields != null)
                {
                    compResults ??= new List<SceneRefComponentResult>();
                    compResults.Add(new SceneRefComponentResult(c, componentName, visitor.Fields, goId, i));
                }
            }

            return compResults == null ? null : new SceneRefObjectResult(go, compResults);
        }
    }
}
