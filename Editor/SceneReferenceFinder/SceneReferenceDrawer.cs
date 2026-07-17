using System.Collections.Generic;
using Horcrux.Editor.Common;
using UnityEditor;
using UnityEngine;

namespace Horcrux.Editor.SceneReferenceFinder
{
    /// <summary>
    /// Vẽ cây 3 tầng: GameObject (referencer) → Component → field trỏ tới target.
    /// Zero allocation trong OnGUI — display strings cached, reusable GUIContent.
    /// </summary>
    public sealed class SceneReferenceDrawer
    {
        private static readonly Color RowEvenColor = new(0f, 0f, 0f, 0.06f);
        private static readonly Color RowOddColor  = new(0f, 0f, 0f, 0.12f);

        private static readonly GUIContent EmptyLabel     = new();
        private static readonly GUIContent SeparatorLabel = new("——");
        private static readonly GUIContent NoTargetMsg    = new("Select a GameObject/Component, then Scan to find what references it.");
        private static readonly GUIContent NoResultMsg    = new("✅ Không component nào trong scope (scene đang load / prefab stage) trỏ tới đối tượng này. Lưu ý: scene chưa mở và asset khác chưa được quét.");
        private static readonly GUIContent NoMatchMsg     = new("No results match the filter.");

        private readonly GUIContent _goLabel    = new();
        private readonly GUIContent _compLabel  = new();
        private readonly GUIContent _fieldLabel = new();
        private readonly GUIContent _typeLabel  = new();

        public void Draw(List<SceneRefObjectResult> results, List<SceneRefObjectResult> displayList)
        {
            if (results == null)
            {
                EditorGUILayout.HelpBox(NoTargetMsg.text, MessageType.Info);
                return;
            }

            if (displayList.Count == 0)
            {
                EditorGUILayout.HelpBox(results.Count == 0 ? NoResultMsg.text : NoMatchMsg.text, MessageType.Info);
                return;
            }

            for (int i = 0; i < displayList.Count; i++)
                DrawGameObjectRow(displayList[i], i);
        }

        private void DrawGameObjectRow(SceneRefObjectResult go, int index)
        {
            bool expanded = SessionState.GetBool(go.foldoutKey, true);

            Rect rowRect = EditorGUILayout.BeginHorizontal();
            if (Event.current.type == EventType.Repaint)
                EditorGUI.DrawRect(rowRect, index % 2 == 0 ? RowEvenColor : RowOddColor);

            bool newExpanded = EditorGUILayout.Foldout(expanded, EmptyLabel, true);
            if (newExpanded != expanded)
                SessionState.SetBool(go.foldoutKey, newExpanded);

            _goLabel.text = go.goLabel;
            if (GUILayout.Button(_goLabel, StaticStyles.AssetButton))
            {
                if (go.gameObject != null)
                    NavigationHelper.SelectAndPing(go.gameObject);
            }

            EditorGUILayout.EndHorizontal();

            if (!newExpanded) return;

            EditorGUI.indentLevel++;
            for (int c = 0; c < go.components.Count; c++)
                DrawComponentRow(go, go.components[c]);
            EditorGUI.indentLevel--;
        }

        private void DrawComponentRow(SceneRefObjectResult parent, SceneRefComponentResult comp)
        {
            bool expanded = SessionState.GetBool(comp.foldoutKey, true);

            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(20);
            _compLabel.text = comp.compLabel;
            bool newExpanded = EditorGUILayout.Foldout(expanded, _compLabel, true);
            if (newExpanded != expanded)
                SessionState.SetBool(comp.foldoutKey, newExpanded);
            EditorGUILayout.EndHorizontal();

            if (!newExpanded) return;

            for (int f = 0; f < comp.fields.Count; f++)
                DrawFieldRow(parent.gameObject, comp.component, comp.fields[f]);
        }

        private void DrawFieldRow(GameObject go, Component comp, SceneRefFieldResult field)
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(40);

            _fieldLabel.text = field.displayLabel;
            if (GUILayout.Button(_fieldLabel, EditorStyles.label))
            {
                if (go != null)
                    NavigationHelper.SelectAndPingProperty(go, comp, field.propertyPath);
            }

            GUILayout.Label(SeparatorLabel, EditorStyles.miniLabel, StaticGUILayout.Mini24);

            _typeLabel.text = field.fieldTypeName;
            GUILayout.Label(_typeLabel, EditorStyles.miniLabel);

            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
        }
    }
}
