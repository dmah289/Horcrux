using System;
using System.Collections.Generic;
using System.Text;
using Horcrux.Editor.Common;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Horcrux.Editor.UsageFinder
{
    /// <summary>
    /// Usage Finder — trả lời "asset này đang được dùng ở đâu?".
    /// Gộp 2 tab cùng làm việc trên 1 asset nguồn:
    ///  • Asset Usages       — hard dependency, qua AssetReferenceIndex (nhanh, O(1) query).
    ///  • Addressable Usages — AssetReference (m_AssetGUID), quét project (chậm, cần scan).
    /// </summary>
    public sealed class UsageFinderWindow : EditorWindow
    {
        // ──────────────── Types ────────────────

        private enum Tab { AssetUsages, AddressableUsages }

        // ──────────────── Static eager cache ────────────────

        private static readonly GUIContent ClearBtnLabel = new("✕");
        private static readonly GUIContent TabAsset       = new("Asset Usages",       "Hard dependencies — assets referencing the target (via dependency index)");
        private static readonly GUIContent TabAddr        = new("Addressable Usages", "AssetReference (Addressables) pointing at the target — scans the project");
        private static readonly GUIContent TargetLabel    = new("Target", "Asset cần tìm usage — chọn trong Project rồi kéo vào đây, hoặc dùng menu chuột phải \"Find Usages (Horcrux)\"");
        private static readonly GUIContent ScanBtnLabel   = new("🔍 Scan Addressable Usages");
        private static readonly GUIContent RebuildLabel   = new("↻ Rebuild Index", "Full rebuild the dependency index (use after external changes like git checkout)");

        private static readonly StringBuilder SharedSB = new(64);

        // ──────────────── Per-instance state ────────────────

        private Tab      _tab = Tab.AssetUsages;
        private Object   _target;
        private string   _filterText = "";
        private Vector2  _scroll;

        private List<UsageEntry> _results;         // null cho tới scan/query đầu tiên
        private List<UsageEntry> _filteredResults;
        private bool _filterDirty = true;
        private bool _resultsComplete;              // false nếu index chưa build xong / scan bị hủy → KHÔNG khẳng định "safe"

        private UsageResultDrawer _drawer;

        // ──────────────── Status cache ────────────────

        private readonly GUIContent _statusContent = new();
        private int  _lastStatusCount = -1;
        private bool _lastStatusHasFilter;

        // ──────────────── Menu ────────────────

        [MenuItem("Horcrux/Usage Finder")]
        private static void ShowWindow()
        {
            var window = GetWindow<UsageFinderWindow>();
            window.titleContent = new GUIContent("Usage Finder");
            window.Show();
        }

        /// <summary>Context menu Project window: right-click asset → Find Usages.</summary>
        [MenuItem("Assets/Find Usages (Horcrux)", false, 30)]
        private static void FindUsagesFromContext()
        {
            var window = GetWindow<UsageFinderWindow>();
            window.titleContent = new GUIContent("Usage Finder");
            window._target = Selection.activeObject;
            window._tab = Tab.AssetUsages;
            window.RunActiveTab();
            window.Show();
        }

        [MenuItem("Assets/Find Usages (Horcrux)", true)]
        private static bool FindUsagesFromContextValidate()
        {
            return Selection.activeObject != null
                   && !string.IsNullOrEmpty(AssetDatabase.GetAssetPath(Selection.activeObject));
        }

        // ──────────────── OnGUI ────────────────

        private void OnGUI()
        {
            StaticStyles.Ensure();
            _drawer ??= new UsageResultDrawer();

            HandleScrollWheelBoost();

            if (Event.current.type == EventType.Layout && _filterDirty && _results != null)
            {
                RebuildFilteredResults();
                _filterDirty = false;
            }

            DrawTabs();
            DrawTargetRow();
            DrawFilterBar();
            DrawSeparator();

            _scroll = GUILayout.BeginScrollView(_scroll);
            _drawer.Draw(_results, _filteredResults ?? _results,
                _tab == Tab.AddressableUsages, _resultsComplete, _target != null);
            GUILayout.EndScrollView();

            DrawStatusBar();
        }

        // ──────────────── Tabs ────────────────

        private void DrawTabs()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            DrawTabToggle(Tab.AssetUsages, TabAsset);
            DrawTabToggle(Tab.AddressableUsages, TabAddr);
            EditorGUILayout.EndHorizontal();
        }

        private void DrawTabToggle(Tab tab, GUIContent content)
        {
            bool active = _tab == tab;
            Color saved = GUI.backgroundColor;
            if (active) GUI.backgroundColor = StaticColor.ScopeActiveColor;

            bool toggled = GUILayout.Toggle(active, content, EditorStyles.toolbarButton, GUILayout.ExpandWidth(true));

            GUI.backgroundColor = saved;

            if (toggled && !active)
            {
                _tab = tab;
                // Đổi tab → kết quả cũ không còn hợp lệ; Asset tab query lại ngay, Addr tab cần bấm Scan
                _results = null;
                _filteredResults = null;
                _resultsComplete = true; // reset: chưa scan lần nào ≠ scan dở dang
                if (_tab == Tab.AssetUsages)
                    RunActiveTab();
            }
        }

        // ──────────────── Target row ────────────────

        private void DrawTargetRow()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            EditorGUI.BeginChangeCheck();
            Object newTarget = EditorGUILayout.ObjectField(TargetLabel, _target, typeof(Object), false);
            if (EditorGUI.EndChangeCheck())
            {
                _target = newTarget;
                if (_tab == Tab.AssetUsages)
                {
                    RunActiveTab();   // Asset tab: query tức thì
                }
                else
                {
                    _results = null;         // Addr tab: chờ user bấm Scan
                    _resultsComplete = true; // đổi target ≠ scan dở dang
                }
            }

            EditorGUILayout.EndHorizontal();

            // Hàng action theo tab
            if (_tab == Tab.AssetUsages)
            {
                if (!AssetReferenceIndex.IsBuilt)
                    EditorGUILayout.HelpBox("Dependency index not built yet — it builds on first query.", MessageType.Info);

                if (GUILayout.Button(RebuildLabel, StaticGUILayout.ScanFullWidth))
                {
                    if (AssetReferenceIndex.Rebuild(true))
                    {
                        RunActiveTab();          // build xong → query lại
                    }
                    else
                    {
                        // Build bị hủy → index rỗng, không đáng tin. Đánh dấu incomplete + xóa kết quả cũ.
                        // KHÔNG query lại (sẽ kích hoạt EnsureBuilt build lần nữa qua GetReferencers).
                        _results = null;
                        _filteredResults = null;
                        _resultsComplete = false;
                    }
                }
            }
            else
            {
                Color saved = GUI.backgroundColor;
                GUI.backgroundColor = StaticColor.ScanBtnColor;
                if (GUILayout.Button(ScanBtnLabel, StaticGUILayout.ScanFullWidth))
                    RunActiveTab();
                GUI.backgroundColor = saved;
            }
        }

        // ──────────────── Filter bar ────────────────

        private void DrawFilterBar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            EditorGUI.BeginChangeCheck();
            _filterText = GUILayout.TextField(_filterText, EditorStyles.toolbarSearchField, GUILayout.ExpandWidth(true));
            if (EditorGUI.EndChangeCheck())
                _filterDirty = true;

            if (!string.IsNullOrEmpty(_filterText))
            {
                if (GUILayout.Button(ClearBtnLabel, EditorStyles.toolbarButton, StaticGUILayout.Mini24))
                {
                    _filterText = "";
                    _filterDirty = true;
                    GUI.FocusControl(null);
                }
            }

            EditorGUILayout.EndHorizontal();
        }

        // ──────────────── Status bar ────────────────

        private void DrawStatusBar()
        {
            if (_results == null) return;

            int count = (_filteredResults ?? _results).Count;
            bool hasFilter = !string.IsNullOrEmpty(_filterText);

            if (_lastStatusCount != count || _lastStatusHasFilter != hasFilter)
            {
                _lastStatusCount = count;
                _lastStatusHasFilter = hasFilter;

                SharedSB.Clear();
                if (hasFilter)
                    SharedSB.Append("Showing ").Append(count).Append(" of ").Append(_results.Count).Append(" referencers");
                else
                    SharedSB.Append("Found ").Append(count).Append(" referencer").Append(count == 1 ? "" : "s");
                _statusContent.text = SharedSB.ToString();
            }

            DrawSeparator();
            GUILayout.Label(_statusContent, EditorStyles.centeredGreyMiniLabel);
        }

        // ──────────────── Scan / query execution ────────────────

        private void RunActiveTab()
        {
            if (_target == null)
            {
                _results = null;
                _filteredResults = null;
                return;
            }

            if (_tab == Tab.AssetUsages)
            {
                // Đảm bảo index sẵn sàng TRƯỚC khi query. Nếu build bị hủy → kết quả rỗng không đáng tin.
                _resultsComplete = AssetReferenceIndex.EnsureBuilt();
                _results = AssetUsageScanner.Scan(_target);
            }
            else
            {
                _results = AddressableUsageScanner.Scan(_target, out bool cancelled);
                _resultsComplete = !cancelled;
            }

            _scroll = Vector2.zero;
            _filterDirty = true;
            Repaint();
        }

        // ──────────────── Filter logic ────────────────

        private void RebuildFilteredResults()
        {
            if (string.IsNullOrEmpty(_filterText))
            {
                _filteredResults = _results;
                return;
            }

            if (_filteredResults == null || _filteredResults == _results)
                _filteredResults = new List<UsageEntry>();
            else
                _filteredResults.Clear();

            string filter = _filterText;
            for (int i = 0; i < _results.Count; i++)
            {
                UsageEntry e = _results[i];
                if (e.displayLabel.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0
                    || e.pathLabel.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0
                    || e.typeName.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    _filteredResults.Add(e);
                }
            }
        }

        // ──────────────── Helpers ────────────────

        private static void DrawSeparator()
        {
            Rect r = GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none, GUILayout.Height(1));
            EditorGUI.DrawRect(r, StaticColor.SeparatorColor);
        }

        private void HandleScrollWheelBoost()
        {
            if (Event.current.type != EventType.ScrollWheel) return;
            _scroll += Event.current.delta * 20f;
            Event.current.Use();
        }
    }
}
