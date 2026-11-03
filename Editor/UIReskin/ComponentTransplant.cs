using System;
using System.Collections.Generic;
using System.Text;
using Horcrux.Editor.Common;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.EventSystems;
using Object = UnityEngine.Object;

namespace Horcrux.Editor.UIReskin
{
    /// <summary>
    /// Chép component logic từ object cũ sang object mới của skin, giữ giá trị serialize. Reference trỏ vào object cũ được nối
    /// lại: tới bản chép (Initializer trỏ client của nó), tới GameObject mới, hoặc tới component cùng kiểu trên object mới
    /// (chữ, ảnh). Object cũ giữ nguyên; tắt nó là việc của người gọi.
    /// </summary>
    public static class ComponentTransplant
    {
        /// <param name="select">Chọn component để chép; null chép mọi MonoBehaviour không phải UIBehaviour (Graphic, Selectable, layout ở lại).</param>
        /// <returns>Mỗi dòng một việc: <c>copy</c>, <c>remap</c>, và <c>unmapped</c> khi object mới thiếu component cùng kiểu.</returns>
        public static string CopyLogic(GameObject source, GameObject destination, Func<Component, bool> select = null)
        {
            var log = new StringBuilder();
            var map = new Dictionary<Object, Object> { [source] = destination };
            var copies = new List<Component>();
            foreach (var component in source.GetComponents<MonoBehaviour>())
            {
                if (component == null || !(select != null ? select(component) : !(component is UIBehaviour)))
                    continue;
                ComponentUtility.CopyComponent(component);
                ComponentUtility.PasteComponentAsNew(destination);
                var all = destination.GetComponents<Component>();
                var copy = all[all.Length - 1];
                map[component] = copy;
                copies.Add(copy);
                log.Append("copy ").Append(component.GetType().Name).Append('\n');
            }

            foreach (var copy in copies)
            {
                var so = new SerializedObject(copy);
                var visitor = new SourceReferences { Source = source, Paths = new List<string>() };
                SerializedPropertyWalker.Walk(so, ref visitor);
                foreach (var path in visitor.Paths)
                {
                    var property = so.FindProperty(path);
                    var old = property.objectReferenceValue;
                    if (!map.TryGetValue(old, out var replacement) && old is Component oldComponent)
                        replacement = destination.GetComponent(oldComponent.GetType());
                    var field = copy.GetType().Name + "." + path;
                    if (replacement == null)
                    {
                        log.Append("unmapped ").Append(field).Append(" (").Append(old.GetType().Name).Append(")\n");
                        continue;
                    }
                    property.objectReferenceValue = replacement;
                    log.Append("remap ").Append(field).Append(" -> ").Append(replacement.GetType().Name).Append('\n');
                }
                so.ApplyModifiedPropertiesWithoutUndo();
            }
            return log.ToString();
        }

        // đường dẫn property trỏ vào chính object nguồn hoặc component của nó
        private struct SourceReferences : IReferencePropertyVisitor
        {
            public GameObject Source;
            public List<string> Paths;

            public void Visit(SerializedObject so, SerializedProperty property, RefPropertyKind kind)
            {
                if (kind != RefPropertyKind.ObjectReference)
                    return;
                var value = property.objectReferenceValue;
                if (value == Source || value is Component c && c != null && c.gameObject == Source)
                    Paths.Add(property.propertyPath);
            }
        }
    }
}
