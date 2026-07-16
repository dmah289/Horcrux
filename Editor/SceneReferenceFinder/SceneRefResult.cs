using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Horcrux.Editor.SceneReferenceFinder
{
    /// <summary>Shared StringBuilder — build cached display strings tại scan time.</summary>
    internal static class SceneRefStringHelper
    {
        internal static readonly StringBuilder SB = new(96);
    }

    // ──────────────── Field-level result ────────────────

    /// <summary>Một field trỏ tới target — immutable, cache display data.</summary>
    public sealed class SceneRefFieldResult
    {
        public readonly string fieldTypeName;  // kiểu field (vd "Transform")
        public readonly string propertyPath;   // để re-lookup / expand Inspector
        public readonly string displayLabel;   // "ComponentName > DisplayPath"

        public SceneRefFieldResult(string fieldTypeName, string propertyPath,
                                   string displayPath, string componentName)
        {
            this.fieldTypeName = fieldTypeName;
            this.propertyPath  = propertyPath;

            var sb = SceneRefStringHelper.SB;
            sb.Clear();
            sb.Append(componentName).Append(" > ").Append(displayPath);
            displayLabel = sb.ToString();
        }
    }

    // ──────────────── Component-level result ────────────────

    /// <summary>Một component chứa ≥1 field trỏ tới target.</summary>
    public sealed class SceneRefComponentResult
    {
        public readonly Component                  component;
        public readonly string                     componentName;
        public readonly List<SceneRefFieldResult>  fields;

        public readonly string foldoutKey;  // SessionState key
        public readonly string compLabel;   // "📦 ComponentName"

        public SceneRefComponentResult(Component component, string componentName,
                                       List<SceneRefFieldResult> fields,
                                       int goInstanceId, int compIndex)
        {
            this.component     = component;
            this.componentName = componentName;
            this.fields        = fields;

            var sb = SceneRefStringHelper.SB;
            sb.Clear();
            sb.Append("HorcruxSceneRef_Comp_").Append(goInstanceId).Append('_').Append(compIndex);
            foldoutKey = sb.ToString();

            sb.Clear();
            sb.Append("📦 ").Append(componentName);
            compLabel = sb.ToString();
        }
    }

    // ──────────────── GameObject-level result ────────────────

    /// <summary>Một GameObject (referencer) chứa ≥1 component trỏ tới target.</summary>
    public sealed class SceneRefObjectResult
    {
        public readonly GameObject                     gameObject;
        public readonly List<SceneRefComponentResult>  components;

        public readonly int    totalRefCount;  // stored, not recomputed
        public readonly string goLabel;         // "🎮 Name (N refs)"
        public readonly string foldoutKey;

        public SceneRefObjectResult(GameObject go, List<SceneRefComponentResult> components)
        {
            gameObject     = go;
            this.components = components;

            int count = 0;
            for (int i = 0; i < components.Count; i++)
                count += components[i].fields.Count;
            totalRefCount = count;

            var sb = SceneRefStringHelper.SB;
            sb.Clear();
            sb.Append("🎮 ").Append(go != null ? go.name : "<null>")
              .Append(" (").Append(count).Append(count == 1 ? " ref)" : " refs)");
            goLabel = sb.ToString();

            sb.Clear();
            sb.Append("HorcruxSceneRef_GO_").Append(go != null ? go.GetInstanceID() : 0);
            foldoutKey = sb.ToString();
        }
    }
}
