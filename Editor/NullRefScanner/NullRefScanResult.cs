using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Horcrux.Editor.NullRefScanner
{
    /// <summary>Shared StringBuilder — dùng chung cho tất cả constructors tại scan time.</summary>
    internal static class ScanStringHelper
    {
        internal static readonly StringBuilder SB = new(64);
    }

    // ──────────────── Null reference kind ────────────────

    /// <summary>Phân loại mức độ nghiêm trọng của null reference.</summary>
    public enum NullRefKind
    {
        /// <summary>Field chưa được gán (instanceID == 0). Có thể intentional.</summary>
        Unassigned,

        /// <summary>Object từng được gán nhưng đã bị xóa/missing (instanceID != 0). Luôn là bug.</summary>
        MissingRef,

        /// <summary>AssetReference (Addressables) có m_AssetGUID rỗng.</summary>
        NullAssetRef,

        /// <summary>[SerializeReference] field == null (ManagedReference).</summary>
        NullManagedRef,

        /// <summary>ExposedReference có exposedName rỗng (Timeline/Playable).</summary>
        NullExposedRef,
    }

    // ──────────────── Field-level result ────────────────

    public sealed class FieldResult
    {
        public readonly string     fieldName;      // display name (e.g. "Rigidbody")
        public readonly string     fieldTypeName;  // expected type (e.g. "Transform")
        public readonly string     propertyPath;   // SerializedProperty path for re-lookup
        public readonly string     displayPath;    // đường dẫn dễ đọc (e.g. "Enemies[2] > Target")
        public readonly NullRefKind kind;

        // ── Cached display data (format 1 lần tại scan time) ──
        public readonly string     displayLabel;   // "ComponentName > DisplayPath"
        public readonly string     kindTag;        // "[missing]", "[unassigned]",...

        public FieldResult(string fieldName, string fieldTypeName, string propertyPath,
                           string displayPath, NullRefKind kind, string componentName)
        {
            this.fieldName     = fieldName;
            this.fieldTypeName = fieldTypeName;
            this.propertyPath  = propertyPath;
            this.displayPath   = displayPath;
            this.kind          = kind;

            var sb = ScanStringHelper.SB;
            sb.Clear();
            sb.Append(componentName).Append(" > ").Append(displayPath);
            displayLabel = sb.ToString();
            kindTag      = NullRefKindDisplay.Get(kind).Tag;
        }
    }

    // ──────────────── Component-level result ────────────────

    public sealed class ComponentResult
    {
        public readonly Component       component;       // null when missing script
        public readonly string          componentName;   // "Missing Script" if null
        public readonly bool            isMissingScript;
        public readonly List<FieldResult> fields;

        // ── Cached display data ──
        public readonly string foldoutKey;    // "NullRefScanner_Comp_{goId}_{index}"
        public readonly string compLabel;     // "📦 ComponentName"

        public ComponentResult(Component component, string componentName, bool isMissingScript,
                               List<FieldResult> fields, int goInstanceId, int compIndex)
        {
            this.component      = component;
            this.componentName  = componentName;
            this.isMissingScript = isMissingScript;
            this.fields         = fields;

            var sb = ScanStringHelper.SB;
            sb.Clear();
            sb.Append("NullRefScanner_Comp_").Append(goInstanceId).Append('_').Append(compIndex);
            foldoutKey = sb.ToString();

            sb.Clear();
            sb.Append("📦 ").Append(componentName);
            compLabel = sb.ToString();
        }
    }

    // ──────────────── GameObject-level result ────────────────

    public sealed class GameObjectResult
    {
        public readonly GameObject             gameObject;
        public readonly string                 gameObjectName;
        public readonly List<ComponentResult>  components;

        // ── Cached display data ──
        public readonly int    totalIssueCount;  // stored, not computed
        public readonly string goLabel;           // "🎮 Name (N issues)"
        public readonly string foldoutKey;        // "NullRefScanner_GO_{instanceID}"
        public readonly int    maxSeverity;       // for sorting: cao hơn = nghiêm trọng hơn

        public GameObjectResult(GameObject go, string goName, List<ComponentResult> components)
        {
            gameObject     = go;
            gameObjectName = goName;
            this.components = components;

            // Compute once
            int count    = 0;
            int severity = 0;
            for (int i = 0; i < components.Count; i++)
            {
                var comp = components[i];
                if (comp.isMissingScript)
                {
                    count++;
                    int s = NullRefKindDisplay.Get(NullRefKind.MissingRef).Severity;
                    if (s > severity) severity = s;
                }
                else
                {
                    count += comp.fields.Count;
                    for (int f = 0; f < comp.fields.Count; f++)
                    {
                        int s = NullRefKindDisplay.Get(comp.fields[f].kind).Severity;
                        if (s > severity) severity = s;
                    }
                }
            }

            totalIssueCount = count;
            maxSeverity     = severity;

            var sb = ScanStringHelper.SB;
            sb.Clear();
            sb.Append("🎮 ").Append(goName).Append(" (").Append(count).Append(" issues)");
            goLabel = sb.ToString();

            sb.Clear();
            sb.Append("NullRefScanner_GO_").Append(go != null ? go.GetInstanceID() : 0);
            foldoutKey = sb.ToString();
        }
    }
}
