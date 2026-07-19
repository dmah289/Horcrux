using System.Collections.Generic;
using Horcrux.Editor.Common;
using UnityEditor;
using UnityEngine;

namespace Horcrux.Editor.UsageFinder
{
    /// <summary>
    /// Vẽ danh sách referencer (asset/scene → field hit clickable).
    /// Click 1 field → điều hướng tới NƠI CHỨA reference + highlight field.
    /// Zero allocation trong OnGUI — display strings cached trên <see cref="UsageEntry"/>/<see cref="UsageFieldHit"/>.
    /// </summary>
    public sealed class UsageResultDrawer
    {
        // ──────────────── Static eager cache ────────────────

        private static readonly Color RowEvenColor = new(0f, 0f, 0f, 0.06f);
        private static readonly Color RowOddColor  = new(0f, 0f, 0f, 0.12f);

        private static readonly GUIContent NoTargetMsg    = new("Chọn hoặc kéo 1 GameObject/asset rồi bấm Find All Usages.");
        private static readonly GUIContent NoResultMsg    = new("✅ Không tìm thấy field nào trong project / scene đang mở trỏ tới đối tượng này.");
        private static readonly GUIContent NoMatchMsg     = new("Không có kết quả khớp bộ lọc.");
        private static readonly GUIContent IncompleteMsg  = new("⚠️ Quá trình quét đã bị hủy giữa chừng. Kết quả KHÔNG đầy đủ — hãy quét lại trước khi kết luận không có tham chiếu nào.");

        // ──────────────── Reusable GUIContent ────────────────

        private readonly GUIContent _entryLabel  = new();
        private readonly GUIContent _pathLabel   = new();
        private readonly GUIContent _hitLabel    = new();

        // ──────────────── Public API ────────────────

        /// <param name="complete">false nếu scan bị hủy → 0 kết quả KHÔNG đồng nghĩa "không ai dùng".</param>
        public void Draw(List<UsageEntry> results, List<UsageEntry> displayList, bool complete)
        {
            if (results == null)
            {
                EditorGUILayout.HelpBox(complete ? NoTargetMsg.text : IncompleteMsg.text,
                    complete ? MessageType.Info : MessageType.Warning);
                return;
            }

            if (displayList.Count == 0)
            {
                if (results.Count == 0 && !complete)
                {
                    EditorGUILayout.HelpBox(IncompleteMsg.text, MessageType.Warning);
                    return;
                }
                EditorGUILayout.HelpBox(results.Count == 0 ? NoResultMsg.text : NoMatchMsg.text, MessageType.Info);
                return;
            }

            for (int i = 0; i < displayList.Count; i++)
                DrawEntryRow(displayList[i], i);
        }

        // ──────────────── Entry row ────────────────

        private void DrawEntryRow(UsageEntry entry, int index)
        {
            bool expanded = SessionState.GetBool(entry.foldoutKey, true);

            Rect rowRect = EditorGUILayout.BeginHorizontal();
            if (Event.current.type == EventType.Repaint)
                EditorGUI.DrawRect(rowRect, index % 2 == 0 ? RowEvenColor : RowOddColor);

            bool newExpanded = EditorGUILayout.Foldout(expanded, GUIContent.none, true);
            if (newExpanded != expanded)
                SessionState.SetBool(entry.foldoutKey, newExpanded);

            // Clickable label: icon + tên file → ping asset chứa. Tooltip = full path.
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

            _pathLabel.text = entry.folderLabel;
            GUILayout.Label(_pathLabel, EditorStyles.miniLabel);

            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            if (!newExpanded) return;

            for (int h = 0; h < entry.hits.Count; h++)
                DrawHitRow(entry.hits[h]);
        }

        // ──────────────── Hit row (clickable field) ────────────────

        private void DrawHitRow(UsageFieldHit hit)
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(30);

            _hitLabel.text = hit.displayLabel;
            if (GUILayout.Button(_hitLabel, EditorStyles.linkLabel, GUILayout.ExpandWidth(false)))
                Navigate(hit);

            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
        }

        private static void Navigate(UsageFieldHit hit)
        {
            switch (hit.navKind)
            {
                case UsageNavKind.DiskScene:
                    AssetReferenceScanner.OpenDiskSceneAndLocate(hit.scenePath, hit.targetGuid);
                    break;

                case UsageNavKind.OpenSceneField:
                case UsageNavKind.AssetField:
                    NavigationHelper.SelectAndExpandAsset(hit.navObject, hit.navComponent, hit.propertyPath);
                    break;
            }
        }
    }
}
