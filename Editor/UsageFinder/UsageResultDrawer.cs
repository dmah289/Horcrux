using System.Collections.Generic;
using Horcrux.Editor.Common;
using UnityEditor;
using UnityEngine;

namespace Horcrux.Editor.UsageFinder
{
    /// <summary>
    /// Vẽ danh sách referencer (asset → optional detail lines).
    /// Zero allocation trong OnGUI — display strings cached trên <see cref="UsageEntry"/>,
    /// chỉ update .text trên reusable GUIContent.
    /// </summary>
    public sealed class UsageResultDrawer
    {
        // ──────────────── Static eager cache ────────────────

        private static readonly Color RowEvenColor = new(0f, 0f, 0f, 0.06f);
        private static readonly Color RowOddColor  = new(0f, 0f, 0f, 0.12f);

        private static readonly GUIContent NoTargetMsg   = new("Select or drop an asset to find its usages.");
        private static readonly GUIContent NoResultAsset = new("✅ No other asset references this — safe to edit/delete.");
        private static readonly GUIContent NoResultAddr  = new("✅ No AssetReference points to this asset.");
        private static readonly GUIContent NoMatchMsg    = new("No results match the filter.");

        // ──────────────── Reusable GUIContent ────────────────

        private readonly GUIContent _entryLabel  = new();
        private readonly GUIContent _pathLabel   = new();
        private readonly GUIContent _detailLabel = new();

        // ──────────────── Public API ────────────────

        /// <param name="isAddressableTab">Đổi thông điệp "không có kết quả" cho đúng ngữ cảnh tab.</param>
        public void Draw(List<UsageEntry> results, List<UsageEntry> displayList, bool isAddressableTab)
        {
            if (results == null)
            {
                EditorGUILayout.HelpBox(NoTargetMsg.text, MessageType.Info);
                return;
            }

            if (displayList.Count == 0)
            {
                string msg = results.Count == 0
                    ? (isAddressableTab ? NoResultAddr.text : NoResultAsset.text)
                    : NoMatchMsg.text;
                EditorGUILayout.HelpBox(msg, MessageType.Info);
                return;
            }

            for (int i = 0; i < displayList.Count; i++)
                DrawEntryRow(displayList[i], i);
        }

        // ──────────────── Entry row ────────────────

        private void DrawEntryRow(UsageEntry entry, int index)
        {
            bool hasDetails = entry.DetailCount > 0;
            bool expanded = hasDetails && SessionState.GetBool(entry.foldoutKey, true);

            Rect rowRect = EditorGUILayout.BeginHorizontal();
            if (Event.current.type == EventType.Repaint)
                EditorGUI.DrawRect(rowRect, index % 2 == 0 ? RowEvenColor : RowOddColor);

            // Foldout chỉ khi có detail
            if (hasDetails)
            {
                bool newExpanded = EditorGUILayout.Foldout(expanded, GUIContent.none, true);
                if (newExpanded != expanded)
                    SessionState.SetBool(entry.foldoutKey, newExpanded);
                expanded = newExpanded;
            }
            else
            {
                GUILayout.Space(14); // căn lề với các dòng có foldout
            }

            // Clickable label: icon + tên file → ping asset. Tooltip = full path (click để ping/select).
            _entryLabel.text    = entry.displayLabel;
            _entryLabel.image   = entry.icon;
            _entryLabel.tooltip = entry.pathLabel;
            if (GUILayout.Button(_entryLabel, StaticStyles.AssetButton, GUILayout.ExpandWidth(false)))
            {
                if (entry.asset != null)
                {
                    Selection.activeObject = entry.asset;
                    EditorGUIUtility.PingObject(entry.asset);
                }
            }

            // Thư mục chứa (mờ bên phải) — gọn hơn full path, không lặp tên file
            _pathLabel.text = entry.folderLabel;
            GUILayout.Label(_pathLabel, EditorStyles.miniLabel);

            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            if (!hasDetails || !expanded) return;

            EditorGUI.indentLevel++;
            for (int d = 0; d < entry.detailLabels.Count; d++)
            {
                EditorGUILayout.BeginHorizontal();
                GUILayout.Space(30);
                _detailLabel.text = entry.detailLabels[d];
                GUILayout.Label(_detailLabel, EditorStyles.miniLabel);
                GUILayout.FlexibleSpace();
                EditorGUILayout.EndHorizontal();
            }
            EditorGUI.indentLevel--;
        }
    }
}
