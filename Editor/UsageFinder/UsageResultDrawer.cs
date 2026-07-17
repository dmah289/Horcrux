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

        private static readonly GUIContent NoTargetMsg    = new("Select or drop an asset to find its usages.");
        private static readonly GUIContent AwaitScanMsg   = new("Bấm \"🔍 Scan Addressable Usages\" để quét AssetReference trỏ tới target.");
        private static readonly GUIContent NoResultAsset  = new("✅ No other asset references this — safe to edit/delete.");
        private static readonly GUIContent NoResultAddr   = new("✅ No AssetReference points to this asset.");
        private static readonly GUIContent NoMatchMsg     = new("No results match the filter.");

        // Khi scan/build chưa hoàn tất (index build bị hủy, hoặc scan Addressable bị Cancel) → KHÔNG được
        // khẳng định "safe". 0 kết quả lúc này chỉ nghĩa "chưa quét xong", không phải "không ai dùng".
        private static readonly GUIContent IncompleteAsset = new("⚠️ Index chưa build xong (đã hủy). Kết quả không đầy đủ — bấm \"↻ Rebuild Index\" rồi thử lại trước khi kết luận.");
        private static readonly GUIContent IncompleteAddr  = new("⚠️ Scan đã bị hủy giữa chừng. Kết quả không đầy đủ — bấm Scan lại trước khi kết luận không có AssetReference nào trỏ tới.");

        // ──────────────── Reusable GUIContent ────────────────

        private readonly GUIContent _entryLabel  = new();
        private readonly GUIContent _pathLabel   = new();
        private readonly GUIContent _detailLabel = new();

        // ──────────────── Public API ────────────────

        /// <param name="isAddressableTab">Đổi thông điệp "không có kết quả" cho đúng ngữ cảnh tab.</param>
        /// <param name="complete">
        /// false nếu build index bị hủy / scan Addressable bị Cancel → 0 kết quả KHÔNG đồng nghĩa
        /// "không ai dùng"; hiện cảnh báo thay vì khẳng định "safe".
        /// </param>
        public void Draw(List<UsageEntry> results, List<UsageEntry> displayList,
                         bool isAddressableTab, bool complete, bool hasTarget)
        {
            if (results == null)
            {
                if (!complete)
                {
                    // Scan/build bị hủy để lại _results=null → cảnh báo, không im lặng.
                    EditorGUILayout.HelpBox(
                        (isAddressableTab ? IncompleteAddr : IncompleteAsset).text, MessageType.Warning);
                    return;
                }

                // Có target nhưng chưa scan (Addressable tab chờ bấm Scan) ≠ chưa chọn target.
                GUIContent msg = (hasTarget && isAddressableTab) ? AwaitScanMsg : NoTargetMsg;
                EditorGUILayout.HelpBox(msg.text, MessageType.Info);
                return;
            }

            if (displayList.Count == 0)
            {
                if (results.Count == 0 && !complete)
                {
                    // Chưa quét xong → tuyệt đối không nói "safe to edit/delete".
                    EditorGUILayout.HelpBox(
                        (isAddressableTab ? IncompleteAddr : IncompleteAsset).text, MessageType.Warning);
                    return;
                }

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

            // Thụt lề do GUILayout.Space(30) — detail rows dùng GUILayout.Label (không chịu indentLevel).
            for (int d = 0; d < entry.detailLabels.Count; d++)
            {
                EditorGUILayout.BeginHorizontal();
                GUILayout.Space(30);
                _detailLabel.text = entry.detailLabels[d];
                GUILayout.Label(_detailLabel, EditorStyles.miniLabel);
                GUILayout.FlexibleSpace();
                EditorGUILayout.EndHorizontal();
            }
        }
    }
}
