using System;
using System.Collections.Generic;
using System.Text;
using Horcrux.Editor.Common;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Horcrux.Editor.UIReskin
{
    /// <summary>
    /// Soát prefab sau khi dựng: script mất, reference gãy, listener lưu sẵn trong UnityEvent, override của từng prefab instance.
    /// Override "nhiễu" là giá trị engine ghi lại mỗi lần tính (vị trí do LayoutGroup xếp, cỡ do ContentSizeFitter, mã style
    /// của TMP): giữ trong file thì che thay đổi của prefab gốc. Gỡ override luôn chạy thử trước (apply false), in đúng dòng sẽ gỡ.
    /// </summary>
    public static class PrefabAudit
    {
        /// <summary>Mỗi prefab trong folder (kể cả thư mục con) một khối: base của variant, số override, rồi từng lỗi.</summary>
        public static string Report(string folder)
        {
            var sb = new StringBuilder();
            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { folder }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    AppendPrefab(sb, path, root);
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }
            return sb.ToString();
        }

        /// <summary>
        /// Một dòng mỗi override của các prefab instance ngoài cùng (kể cả root của variant): <c>instance | object:Type.property = giá trị</c>,
        /// dòng nhiễu mở đầu bằng <c>~</c>. Thêm dòng <c>+</c> component, object thêm vào và <c>-</c> component đã gỡ.
        /// Override bắt buộc (vị trí, tên của root instance) không in. filter giữ dòng chứa chuỗi đó.
        /// </summary>
        public static string ListOverrides(string prefabPath, string filter = null)
        {
            var sb = new StringBuilder();
            var root = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                foreach (var instance in OutermostInstances(root))
                {
                    foreach (var entry in Collect(root, instance))
                        AppendIf(sb, entry.Line, filter);
                    foreach (var added in PrefabUtility.GetAddedGameObjects(instance))
                        AppendIf(sb, "+ " + PathOf(root, added.instanceGameObject.transform), filter);
                    foreach (var added in PrefabUtility.GetAddedComponents(instance))
                        AppendIf(sb, "+ " + PathOf(root, added.instanceComponent.transform) + ":" + added.instanceComponent.GetType().Name, filter);
                    foreach (var removed in PrefabUtility.GetRemovedComponents(instance))
                        AppendIf(sb, "- " + PathOf(root, removed.containingInstanceGameObject.transform) + ":" + removed.assetComponent.GetType().Name, filter);
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
            return sb.ToString();
        }

        /// <summary>
        /// Gỡ override có dòng (đúng định dạng <see cref="ListOverrides"/>) được select chọn. apply false chỉ in dòng sẽ gỡ.
        /// Override bắt buộc không bao giờ bị gỡ.
        /// </summary>
        public static string RevertOverrides(string prefabPath, Func<string, bool> select, bool apply = false)
        {
            var sb = new StringBuilder();
            Action<GameObject> revert = root =>
            {
                foreach (var instance in OutermostInstances(root))
                {
                    var keep = new List<PropertyModification>();
                    var removed = 0;
                    foreach (var entry in Collect(root, instance, includeDefault: true))
                    {
                        if (!entry.Default && select(entry.Line))
                        {
                            sb.Append(entry.Line).Append('\n');
                            removed++;
                        }
                        else
                            keep.Add(entry.Modification);
                    }
                    if (apply && removed > 0)
                        PrefabUtility.SetPropertyModifications(instance, keep.ToArray());
                }
            };
            if (apply)
                PrefabEdit.Edit(prefabPath, revert);
            else
                ReadOnly(prefabPath, revert);
            return (apply ? "reverted\n" : "dry run\n") + sb;
        }

        /// <summary>Gỡ mọi override nhiễu (dòng <c>~</c>).</summary>
        public static string PruneNoise(string prefabPath, bool apply = false) =>
            RevertOverrides(prefabPath, line => line.StartsWith("~"), apply);

        private struct Entry
        {
            public PropertyModification Modification;
            public string Line;
            public bool Default;
        }

        private static void AppendPrefab(StringBuilder sb, string path, GameObject root)
        {
            sb.Append(path);
            var source = PrefabUtility.GetCorrespondingObjectFromSource(root);
            if (source != null)
                sb.Append("  variant of ").Append(AssetDatabase.GetAssetPath(source));
            sb.Append('\n');

            foreach (var instance in OutermostInstances(root))
            {
                int total = 0, noise = 0;
                foreach (var entry in Collect(root, instance))
                {
                    total++;
                    if (entry.Line[0] == '~')
                        noise++;
                }
                if (total > 0)
                    sb.Append("  overrides ").Append(PathOf(root, instance.transform)).Append(": ").Append(total)
                        .Append(noise > 0 ? " (noise " + noise + ")" : "").Append('\n');
            }

            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                var missing = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject);
                if (missing > 0)
                    sb.Append("  missing script: ").Append(PathOf(root, t)).Append(" x").Append(missing).Append('\n');
                foreach (var component in t.GetComponents<Component>())
                {
                    if (component == null)
                        continue;
                    var visitor = new ReferenceVisitor { Lines = sb, Prefix = PathOf(root, t) + ":" + component.GetType().Name };
                    SerializedPropertyWalker.Walk(new SerializedObject(component), ref visitor);
                }
            }
        }

        // reference gãy (asset đã xoá, object đã gỡ) và listener lưu sẵn trong UnityEvent (m_PersistentCalls.m_Calls[i].m_Target)
        private struct ReferenceVisitor : IReferencePropertyVisitor
        {
            public StringBuilder Lines;
            public string Prefix;

            public void Visit(SerializedObject so, SerializedProperty property, RefPropertyKind kind)
            {
                if (kind != RefPropertyKind.ObjectReference)
                    return;
                var path = property.propertyPath;
                if (path.Contains("m_PersistentCalls.m_Calls") && path.EndsWith("m_Target"))
                {
                    var method = so.FindProperty(path.Substring(0, path.Length - "m_Target".Length) + "m_MethodName");
                    var target = property.objectReferenceValue;
                    Lines.Append("  listener: ").Append(Prefix).Append('.').Append(path.Substring(0, path.IndexOf(".m_PersistentCalls")))
                        .Append(" -> ").Append(target != null ? target.name + ":" + target.GetType().Name : "null")
                        .Append('.').Append(method != null ? method.stringValue : "?").Append('\n');
                    return;
                }
                if (property.objectReferenceValue == null && property.objectReferenceInstanceIDValue != 0)
                    Lines.Append("  missing ref: ").Append(Prefix).Append('.').Append(path).Append('\n');
            }
        }

        private static List<GameObject> OutermostInstances(GameObject root)
        {
            var result = new List<GameObject>();
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                if (PrefabUtility.IsOutermostPrefabInstanceRoot(t.gameObject))
                    result.Add(t.gameObject);
            return result;
        }

        private static List<Entry> Collect(GameObject root, GameObject instance, bool includeDefault = false)
        {
            var entries = new List<Entry>();
            var modifications = PrefabUtility.GetPropertyModifications(instance);
            if (modifications == null)
                return entries;

            // target của override là object trong prefab nguồn trực tiếp; tra ngược ra object trên instance để đọc layout cha
            var bySource = new Dictionary<Object, Object>();
            foreach (var t in instance.GetComponentsInChildren<Transform>(true))
            {
                Map(bySource, t.gameObject);
                foreach (var component in t.GetComponents<Component>())
                    if (component != null)
                        Map(bySource, component);
            }

            var instancePath = PathOf(root, instance.transform);
            foreach (var modification in modifications)
            {
                var isDefault = PrefabUtility.IsDefaultOverride(modification);
                if (isDefault && !includeDefault)
                    continue;
                bySource.TryGetValue(modification.target, out var target);
                var line = new StringBuilder();
                line.Append(IsNoise(target, modification.propertyPath) ? "~ " : "  ").Append(instancePath).Append(" | ");
                line.Append(target != null ? PathOf(root, TransformOf(target)) + ":" + target.GetType().Name : "?" + modification.target);
                line.Append('.').Append(modification.propertyPath).Append(" = ");
                line.Append(modification.objectReference != null ? modification.objectReference.name : modification.value);
                entries.Add(new Entry { Modification = modification, Line = line.ToString(), Default = isDefault });
            }
            return entries;
        }

        private static void Map(Dictionary<Object, Object> bySource, Object instanceObject)
        {
            var source = PrefabUtility.GetCorrespondingObjectFromSource(instanceObject);
            if (source != null)
                bySource[source] = instanceObject;
        }

        private static bool IsNoise(Object target, string propertyPath)
        {
            if (propertyPath == "m_TextStyleHashCode")
                return true;
            if (!(target is RectTransform rect))
                return false;

            var axis = propertyPath.EndsWith(".x") ? 0 : propertyPath.EndsWith(".y") ? 1 : -1;
            if (axis < 0)
                return false;
            var isSize = propertyPath.StartsWith("m_SizeDelta");
            var isPlacement = propertyPath.StartsWith("m_AnchoredPosition") || propertyPath.StartsWith("m_AnchorMin") ||
                              propertyPath.StartsWith("m_AnchorMax");

            // ContentSizeFitter trên chính object ghi cỡ theo trục không Unconstrained
            if (isSize && rect.TryGetComponent<ContentSizeFitter>(out var fitter) && fitter.enabled &&
                (axis == 0 ? fitter.horizontalFit : fitter.verticalFit) != ContentSizeFitter.FitMode.Unconstrained)
                return true;

            // LayoutGroup của cha ghi anchor và vị trí mọi con, ghi cỡ khi Grid hoặc bật childControl theo trục
            if (!(rect.parent != null && rect.parent.TryGetComponent<LayoutGroup>(out var group) && group.enabled))
                return false;
            if (rect.TryGetComponent<LayoutElement>(out var element) && element.ignoreLayout)
                return false;
            if (isPlacement)
                return true;
            if (!isSize)
                return false;
            return group switch
            {
                GridLayoutGroup => true,
                HorizontalOrVerticalLayoutGroup line => axis == 0 ? line.childControlWidth : line.childControlHeight,
                _ => false,
            };
        }

        private static void ReadOnly(string prefabPath, Action<GameObject> read)
        {
            var root = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                read(root);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void AppendIf(StringBuilder sb, string line, string filter)
        {
            if (filter == null || line.Contains(filter))
                sb.Append(line).Append('\n');
        }

        private static Transform TransformOf(Object target) =>
            target is Component component ? component.transform : ((GameObject)target).transform;

        private static string PathOf(GameObject root, Transform t) =>
            t == root.transform ? root.name : AnimationUtility.CalculateTransformPath(t, root.transform);
    }
}
