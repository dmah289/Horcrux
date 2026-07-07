using System.Collections.Generic;
using UnityEngine;

namespace Horcrux.Editor.NullRefScanner
{
    // ──────────────── Field-level result ────────────────

    public class FieldResult
    {
        public string fieldName;      // display name (e.g. "Rigidbody")
        public string fieldTypeName;  // expected type (e.g. "Transform")
        public string propertyPath;   // SerializedProperty path for re-lookup
        public string displayPath;    // đường dẫn dễ đọc (e.g. "Enemies[2] > Target")
    }

    // ──────────────── Component-level result ────────────────

    public class ComponentResult
    {
        public Component component;       // null when missing script
        public string    componentName;   // "Missing Script" if null
        public bool      isMissingScript;
        public List<FieldResult> fields;
    }

    // ──────────────── GameObject-level result ────────────────

    public class GameObjectResult
    {
        public GameObject             gameObject;
        public string                 gameObjectName;  // cached go.name
        public List<ComponentResult>  components;

        public int TotalIssueCount
        {
            get
            {
                int count = 0;
                for (int i = 0; i < components.Count; i++)
                {
                    var comp = components[i];
                    count += comp.isMissingScript ? 1 : comp.fields.Count;
                }
                return count;
            }
        }
    }
}
