using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;

namespace Horcrux.Editor.Common
{
    /// <summary>Phân loại property mang reference mà walker phát hiện.</summary>
    public enum RefPropertyKind
    {
        /// <summary>SerializedPropertyType.ObjectReference (Transform, Image, GameObject,...).</summary>
        ObjectReference,

        /// <summary>AssetReference (Addressables) — serializable class chứa m_AssetGUID.</summary>
        AssetReference,

        /// <summary>ExposedReference&lt;T&gt; (Timeline/Playable) — chứa exposedName.</summary>
        ExposedReference,

        /// <summary>[SerializeReference] — ManagedReference.</summary>
        ManagedReference,
    }

    /// <summary>
    /// Visitor nhận từng reference-property walker tìm thấy. Dùng struct + generic constraint
    /// để tránh boxing/allocation — mỗi consumer tự quyết định làm gì (null-check, so target, so GUID).
    /// </summary>
    public interface IReferencePropertyVisitor
    {
        void Visit(SerializedObject so, SerializedProperty property, RefPropertyKind kind);
    }

    /// <summary>
    /// Traversal SerializedProperty dùng chung — tách từ NullRefScannerCore để tái sử dụng cho
    /// NullRefScanner, SceneReferenceFinder, AddressableUsageScanner.
    ///
    /// Walker kiểm soát toàn bộ kỷ luật iteration: skip internal Unity property, phân loại
    /// reference kind, và quyết định enterChildren (descend vào managed reference non-null).
    /// Consumer chỉ nhận callback cho mỗi reference-property — không đụng tới iteration logic.
    /// </summary>
    public static class SerializedPropertyWalker
    {
        // ──────────────── Internal property skip list ────────────────

        /// <summary>
        /// Property thuộc base class của MonoBehaviour (Object → Component → Behaviour → MonoBehaviour).
        /// Next() iterate qua tất cả nhưng chúng là internal Unity — nhiều cái luôn null by design.
        /// Phải skip để tránh false positive / xét nhầm.
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

        private static readonly StringBuilder PathBuilder = new(128);
        private static readonly List<string>  PathParts   = new(8);

        // ──────────────── Walk ────────────────

        /// <summary>
        /// Duyệt mọi serialized property của <paramref name="so"/>, gọi <paramref name="visitor"/>
        /// cho từng reference-property. Truyền visitor bằng ref để struct mutation (kết quả tích lũy) tồn tại.
        /// </summary>
        public static void Walk<TVisitor>(SerializedObject so, ref TVisitor visitor)
            where TVisitor : struct, IReferencePropertyVisitor
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

                // ── AssetReference (Addressables) ──
                if (IsAssetReferenceType(iterator.type))
                {
                    // Chỉ coi là AssetReference khi thực sự có m_AssetGUID (giữ discipline gốc:
                    // nếu không có → fall through xét các loại khác).
                    if (iterator.FindPropertyRelative("m_AssetGUID") != null)
                    {
                        visitor.Visit(so, iterator, RefPropertyKind.AssetReference);
                        enterChildren = false;
                        continue;
                    }
                }

                // ── ExposedReference<T> ──
                if (IsExposedReferenceType(iterator.type))
                {
                    if (iterator.FindPropertyRelative("exposedName") != null)
                    {
                        visitor.Visit(so, iterator, RefPropertyKind.ExposedReference);
                        enterChildren = false;
                        continue;
                    }
                }

                // ── ObjectReference ──
                if (iterator.propertyType == SerializedPropertyType.ObjectReference)
                {
                    visitor.Visit(so, iterator, RefPropertyKind.ObjectReference);
                    enterChildren = false;
                    continue;
                }

                // ── [SerializeReference] — ManagedReference ──
#if UNITY_2021_2_OR_NEWER
                if (iterator.propertyType == SerializedPropertyType.ManagedReference)
                {
                    visitor.Visit(so, iterator, RefPropertyKind.ManagedReference);

                    // Descend vào managed object non-null để quét field lồng bên trong.
                    // Null → không có gì để descend.
                    if (iterator.managedReferenceValue == null)
                        enterChildren = false;
                    continue;
                }
#endif
            }
        }

        // ──────────────── Display path builder ────────────────

        /// <summary>
        /// Chuyển propertyPath raw thành đường dẫn dễ đọc dùng displayName.
        /// Ví dụ: "enemies.Array.data[2].target" → "Enemies[2] > Target".
        /// Dùng static StringBuilder + List để giảm allocation.
        /// </summary>
        public static string BuildDisplayPath(SerializedObject so, string propertyPath)
        {
            string[] segments = propertyPath.Split('.');
            PathParts.Clear();
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

                if (PathBuilder.Length > 0)
                    PathBuilder.Append('.');
                PathBuilder.Append(seg);

                string lookupPath = string.Join(".", segments, 0, i + 1);
                SerializedProperty prop = so.FindProperty(lookupPath);
                PathParts.Add(prop != null ? prop.displayName : seg);
            }

            PathBuilder.Clear();
            for (int i = 0; i < PathParts.Count; i++)
            {
                if (i > 0) PathBuilder.Append(" > ");
                PathBuilder.Append(PathParts[i]);
            }
            return PathBuilder.ToString();
        }

        // ──────────────── Type name helpers ────────────────

        public static bool IsAssetReferenceType(string type)
        {
            return type != null && type.StartsWith("AssetReference", StringComparison.Ordinal);
        }

        public static bool IsExposedReferenceType(string type)
        {
            return type != null && type.StartsWith("ExposedReference", StringComparison.Ordinal);
        }

        /// <summary>Strip "PPtr&lt;$Transform&gt;" → "Transform".</summary>
        public static string CleanTypeName(string rawType)
        {
            if (rawType != null && rawType.StartsWith("PPtr<$", StringComparison.Ordinal))
                return rawType.Substring(6, rawType.Length - 7);
            return rawType ?? "Object";
        }

        /// <summary>Strip "assemblyName Namespace.TypeName" → "TypeName".</summary>
        public static string CleanManagedTypeName(string fullTypeName)
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
