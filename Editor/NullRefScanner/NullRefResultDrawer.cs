using System.Collections.Generic;
using Horcrux.Editor.Common;
using UnityEditor;
using UnityEngine;

namespace Horcrux.Editor.NullRefScanner
{
    /// <summary>
    /// Vẽ cây kết quả 3 tầng (GO → Component → Field).
    /// Zero allocation trong OnGUI — tất cả display strings đã cached trên result objects.
    /// </summary>
    public sealed class NullRefResultDrawer
    {
        // ──────────────── Static eager cache ────────────────

        private static readonly Color RowEvenColor = new(0f, 0f, 0f, 0.06f);
        private static readonly Color RowOddColor  = new(0f, 0f, 0f, 0.12f);

        // Static readonly GUIContent cho các string literal
        private static readonly GUIContent MissingScriptLabel = new("⚠ Missing Script");
        private static readonly GUIContent SeparatorLabel     = new("——");
        private static readonly GUIContent EmptyLabel         = new();
        private static readonly GUIContent NoResultsMsg       = new("✅ No null references found!");
        private static readonly GUIContent NoMatchMsg         = new("No results match the filter.");
        private static readonly GUIContent PromptMsg          = new("Click Scan to find null references.");

        // ──────────────── Reusable GUIContent (update .text only) ────────────────

        private readonly GUIContent _goLabelContent    = new();
        private readonly GUIContent _compLabelContent   = new();
        private readonly GUIContent _fieldLabelContent  = new();
        private readonly GUIContent _fieldTypeContent   = new();
        private readonly GUIContent _kindTagContent     = new();
        private readonly GUIContent _kindIconContent    = new();

        // ──────────────── Public API ────────────────

        /// <summary>Vẽ toàn bộ results hoặc placeholder messages.</summary>
        public void Draw(List<GameObjectResult> results, List<GameObjectResult> displayList)
        {
            if (results == null)
            {
                EditorGUILayout.HelpBox(PromptMsg.text, MessageType.Info);
                return;
            }

            if (displayList.Count == 0)
            {
                string msg = results.Count == 0 ? NoResultsMsg.text : NoMatchMsg.text;
                EditorGUILayout.HelpBox(msg, MessageType.Info);
                return;
            }

            for (int i = 0; i < displayList.Count; i++)
                DrawGameObjectRow(displayList[i], i);
        }

        // ──────────────── GO row ────────────────

        private void DrawGameObjectRow(GameObjectResult goResult, int index)
        {
            bool expanded = SessionState.GetBool(goResult.foldoutKey, true);

            // Row background
            Rect rowRect = EditorGUILayout.BeginHorizontal();
            if (Event.current.type == EventType.Repaint)
            {
                Color bg = index % 2 == 0 ? RowEvenColor : RowOddColor;
                EditorGUI.DrawRect(rowRect, bg);
            }

            // Foldout
            bool newExpanded = EditorGUILayout.Foldout(expanded, EmptyLabel, true);
            if (newExpanded != expanded)
                SessionState.SetBool(goResult.foldoutKey, newExpanded);

            // Clickable GO label — cached string, reusable GUIContent
            _goLabelContent.text = goResult.goLabel;
            if (GUILayout.Button(_goLabelContent, StaticStyles.AssetButton))
            {
                if (goResult.gameObject != null)
                    NullRefNavigationHelper.SelectAndPing(goResult.gameObject);
            }

            EditorGUILayout.EndHorizontal();

            if (!newExpanded) return;

            // Draw components indented
            EditorGUI.indentLevel++;
            for (int c = 0; c < goResult.components.Count; c++)
                DrawComponentRow(goResult, goResult.components[c]);
            EditorGUI.indentLevel--;
        }

        // ──────────────── Component row ────────────────

        private void DrawComponentRow(GameObjectResult parent, ComponentResult comp)
        {
            if (comp.isMissingScript)
            {
                EditorGUILayout.BeginHorizontal();
                GUILayout.Space(20);
                Color saved = GUI.contentColor;
                GUI.contentColor = StaticColor.WarningColor;
                GUILayout.Label(MissingScriptLabel, EditorStyles.boldLabel);
                GUI.contentColor = saved;
                EditorGUILayout.EndHorizontal();
                return;
            }

            bool expanded = SessionState.GetBool(comp.foldoutKey, true);

            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(20);

            _compLabelContent.text = comp.compLabel;
            bool newExpanded = EditorGUILayout.Foldout(expanded, _compLabelContent, true);
            if (newExpanded != expanded)
                SessionState.SetBool(comp.foldoutKey, newExpanded);

            EditorGUILayout.EndHorizontal();

            if (!newExpanded) return;

            for (int f = 0; f < comp.fields.Count; f++)
                DrawFieldRow(parent.gameObject, comp.component, comp.fields[f]);
        }

        // ──────────────── Field row ────────────────

        private void DrawFieldRow(GameObject go, Component comp, FieldResult field)
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(40);

            // Icon + color via lookup table — zero switch, zero alloc
            ref readonly var info = ref NullRefKindDisplay.Get(field.kind);

            Color saved = GUI.contentColor;
            GUI.contentColor = info.IsDanger ? StaticColor.DangerColor : StaticColor.WarningColor;
            _kindIconContent.text = info.Icon;
            GUILayout.Label(_kindIconContent, StaticGUILayout.WarningIcon);
            GUI.contentColor = saved;

            // Clickable display label — cached at scan time
            _fieldLabelContent.text = field.displayLabel;
            if (GUILayout.Button(_fieldLabelContent, EditorStyles.label))
            {
                if (go != null)
                    NullRefNavigationHelper.SelectAndPingProperty(go, comp, field.propertyPath);
            }

            // Separator + type + kind tag
            GUILayout.Label(SeparatorLabel, EditorStyles.miniLabel, StaticGUILayout.Mini24);

            _fieldTypeContent.text = field.fieldTypeName;
            GUILayout.Label(_fieldTypeContent, EditorStyles.miniLabel);

            _kindTagContent.text = field.kindTag;
            GUILayout.Label(_kindTagContent, EditorStyles.miniLabel);

            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
        }
    }
}
