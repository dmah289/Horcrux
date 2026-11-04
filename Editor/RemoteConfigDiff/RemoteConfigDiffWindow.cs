using System.Collections.Generic;
using Horcrux.Runtime.Implementations.RemoteConfigSystem;
using UnityEditor;
using UnityEngine;

namespace Horcrux.Editor.RemoteConfigDiff
{
    /// <summary>Asset của remote config so với template xuất từ Firebase console. Chỉ đọc: không sửa asset, không sửa console.</summary>
    public sealed class RemoteConfigDiffWindow : EditorWindow
    {
        private static readonly GUIContent CollectionLabel = new("Collection", "Asset remote config của dự án (lớp con của BaseRemoteConfigCollection)");
        private static readonly GUIContent TemplateLabel = new("Template JSON", "Firebase console > Remote Config > ⋮ > Download default/current template, dán nguyên file");
        private static readonly GUIContent CompareLabel = new("So sánh", "Parse template, so từng biến [MarkedRemoteConfig] với giá trị trên asset");
        private static readonly GUIContent CopyLabel = new("Copy báo cáo", "Chép bảng markdown vào clipboard");
        private static readonly GUILayoutOption[] TemplateHeight = { GUILayout.MinHeight(120) };
        private static readonly GUILayoutOption[] StatusWidth = { GUILayout.Width(130) };
        private static readonly GUILayoutOption[] KeyWidth = { GUILayout.Width(200) };
        private static readonly Color MatchColor = new(0.45f, 0.8f, 0.5f);
        private static readonly Color WarnColor = new(0.95f, 0.75f, 0.3f);
        private static readonly Color ErrorColor = new(0.95f, 0.4f, 0.4f);

        private static GUIStyle _statusStyle;
        private static GUIStyle _valueStyle;

        private BaseRemoteConfigCollection _collection;
        private string _template = string.Empty;
        private string _error;
        private string _summary;
        private Vector2 _templateScroll;
        private Vector2 _rowsScroll;
        private readonly List<RemoteConfigDiffRow> _rows = new();

        [MenuItem("Horcrux/Remote Config/Diff")]
        private static void ShowWindow() => GetWindow<RemoteConfigDiffWindow>("Remote Config Diff").Show();

        private void OnGUI()
        {
            EnsureStyles();
            DrawInput();
            DrawResult();
        }

        private static void EnsureStyles()
        {
            if (_statusStyle != null)
                return;

            _statusStyle = new GUIStyle(EditorStyles.boldLabel);
            _valueStyle = new GUIStyle(EditorStyles.label) { wordWrap = true, richText = false };
        }

        private void DrawInput()
        {
            _collection = (BaseRemoteConfigCollection)EditorGUILayout.ObjectField(CollectionLabel, _collection,
                typeof(BaseRemoteConfigCollection), false);

            EditorGUILayout.LabelField(TemplateLabel);
            _templateScroll = EditorGUILayout.BeginScrollView(_templateScroll, TemplateHeight);
            _template = EditorGUILayout.TextArea(_template, GUILayout.ExpandHeight(true));
            EditorGUILayout.EndScrollView();

            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(_collection == null || string.IsNullOrWhiteSpace(_template)))
                {
                    if (GUILayout.Button(CompareLabel))
                        Compare();
                }

                using (new EditorGUI.DisabledScope(_rows.Count == 0))
                {
                    if (GUILayout.Button(CopyLabel))
                        GUIUtility.systemCopyBuffer = RemoteConfigDiffCore.FormatReport(_collection.name, _rows);
                }
            }
        }

        private void DrawResult()
        {
            if (_error != null)
            {
                EditorGUILayout.HelpBox(_error, MessageType.Error);
                return;
            }

            if (_summary == null)
                return;

            EditorGUILayout.LabelField(_summary, EditorStyles.boldLabel);
            _rowsScroll = EditorGUILayout.BeginScrollView(_rowsScroll);

            for (int i = 0; i < _rows.Count; i++)
                DrawRow(_rows[i]);

            EditorGUILayout.EndScrollView();
        }

        private void DrawRow(RemoteConfigDiffRow row)
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    Color previous = GUI.contentColor;
                    GUI.contentColor = ColorOf(row.Status);
                    EditorGUILayout.LabelField(row.Status.ToString(), _statusStyle, StatusWidth);
                    GUI.contentColor = previous;
                    EditorGUILayout.LabelField(row.Key ?? "(no key)", KeyWidth);
                    EditorGUILayout.LabelField(row.FieldName ?? "(console only)");
                }

                if (row.Status != RemoteConfigDiffStatus.Match)
                {
                    if (row.AssetJson != null)
                        EditorGUILayout.LabelField("Asset:   " + row.AssetJson, _valueStyle);
                    if (row.ConsoleJson != null)
                        EditorGUILayout.LabelField("Console: " + row.ConsoleJson, _valueStyle);
                }

                for (int i = 0; i < row.Warnings.Count; i++)
                    EditorGUILayout.HelpBox(row.Warnings[i], MessageType.Warning);
            }
        }

        private static Color ColorOf(RemoteConfigDiffStatus status) => status switch
        {
            RemoteConfigDiffStatus.Match => MatchColor,
            RemoteConfigDiffStatus.Mismatch or RemoteConfigDiffStatus.ConsoleUnparsable or RemoteConfigDiffStatus.InvalidField => ErrorColor,
            _ => WarnColor,
        };

        private void Compare()
        {
            _summary = null;

            if (!RemoteConfigDiffCore.TryCompare(_collection, _template, _rows, out _error))
                return;

            var counts = new Dictionary<RemoteConfigDiffStatus, int>();

            for (int i = 0; i < _rows.Count; i++)
                counts[_rows[i].Status] = counts.TryGetValue(_rows[i].Status, out int n) ? n + 1 : 1;

            var parts = new List<string>();

            foreach (RemoteConfigDiffStatus status in System.Enum.GetValues(typeof(RemoteConfigDiffStatus)))
            {
                if (counts.TryGetValue(status, out int n))
                    parts.Add($"{status} {n}");
            }

            _summary = $"{_rows.Count} dòng: " + string.Join(" · ", parts);
        }
    }
}
