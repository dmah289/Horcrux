using System;
using System.Collections.Generic;
using System.Text;
using Horcrux.Editor.Common;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Horcrux.Editor.NullRefScanner
{
    public sealed class NullRefScannerWindow : EditorWindow
    {
        // ──────────────── Types ────────────────

        private enum ScanScope { Scene, Selection, Prefabs }

        // ──────────────── Static eager cache ────────────────

        // Static readonly GUIContent cho string literals dùng trong OnGUI
        private static readonly GUIContent ClearBtnLabel  = new("✕");
        private static readonly GUIContent ScopeScene     = new("Scene",     "Scan all GameObjects in the active scene (including children)");
        private static readonly GUIContent ScopeSelection = new("Selection", "Scan only selected GameObjects in the Hierarchy (including children)");
        private static readonly GUIContent ScopePrefabs   = new("Prefabs",   "Scan selected Prefab assets in the Project window");

        // ──────────────── Per-instance state ────────────────

        private ScanScope _scope = ScanScope.Scene;
        private string    _filterText = "";
        private Vector2   _scroll;

        private List<GameObjectResult> _results;          // null until first scan
        private List<GameObjectResult> _filteredResults;   // rebuilt on Layout when dirty
        private bool _filterDirty = true;

        // ──────────────── Cached counts (computed in RebuildFilteredResults) ────────────────

        private int _cachedTotalIssues;
        private int _cachedFilteredIssues;

        // ──────────────── Dirty-flag cache: status bar ────────────────

        private GUIContent _statusContent;
        private int        _lastStatusTotal;
        private int        _lastStatusFiltered;
        private bool       _lastStatusHasFilter;

        // ──────────────── Dirty-flag cache: filter count ────────────────

        private readonly GUIContent _filterCountContent = new();
        private int _lastFilterCount = -1;

        // ──────────────── Reusable StringBuilder (status/filter strings) ────────────────

        private static readonly StringBuilder SharedSB = new(64);

        // ──────────────── Result drawer (SRP) ────────────────

        private NullRefResultDrawer _resultDrawer;

        // ──────────────── Menu ────────────────

        [MenuItem("Horcrux/Null Reference Scanner")]
        private static void ShowWindow()
        {
            var window = GetWindow<NullRefScannerWindow>();
            window.titleContent = StaticGUIContent.ScannerTitle;
            window.Show();
        }

        // ──────────────── OnGUI ────────────────

        private void OnGUI()
        {
            StaticStyles.Ensure();
            StaticGUIContent.EnsureIcons();
            _resultDrawer ??= new NullRefResultDrawer();

            HandleScrollWheelBoost();

            // Event-phase cache: rebuild filtered results only during Layout
            if (Event.current.type == EventType.Layout)
            {
                if (_filterDirty && _results != null)
                {
                    RebuildFilteredResults();
                    _filterDirty = false;
                }
            }

            DrawToolbar();
            DrawFilterBar();
            DrawSeparator();

            _scroll = GUILayout.BeginScrollView(_scroll);
            _resultDrawer.Draw(_results, _filteredResults ?? _results);
            GUILayout.EndScrollView();

            DrawStatusBar();
        }

        // ──────────────── Toolbar ────────────────

        private void DrawToolbar()
        {
            // Row 1: Scope toggles — chia đều chiều ngang, active toggle có màu xanh dương
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            DrawScopeToggle(ScanScope.Scene,     ScopeScene);
            DrawScopeToggle(ScanScope.Selection,  ScopeSelection);
            DrawScopeToggle(ScanScope.Prefabs,    ScopePrefabs);

            EditorGUILayout.EndHorizontal();

            // Row 2: Scan button — full width, màu xanh lá
            Color savedBg = GUI.backgroundColor;
            GUI.backgroundColor = StaticColor.ScanBtnColor;
            if (GUILayout.Button(StaticGUIContent.ScannerScan, StaticGUILayout.ScanFullWidth))
                ExecuteScan();
            GUI.backgroundColor = savedBg;
        }

        private void DrawScopeToggle(ScanScope scope, GUIContent content)
        {
            bool active = _scope == scope;

            // Active scope → tint xanh dương
            Color savedBg = GUI.backgroundColor;
            if (active)
                GUI.backgroundColor = StaticColor.ScopeActiveColor;

            bool toggled = GUILayout.Toggle(active, content, EditorStyles.toolbarButton,
                               GUILayout.ExpandWidth(true));

            GUI.backgroundColor = savedBg;

            if (toggled && !active)
                _scope = scope;
        }

        // ──────────────── Filter bar ────────────────

        private void DrawFilterBar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            // Search field — expand full width
            EditorGUI.BeginChangeCheck();
            _filterText = GUILayout.TextField(_filterText, EditorStyles.toolbarSearchField,
                              GUILayout.ExpandWidth(true));
            if (EditorGUI.EndChangeCheck())
                _filterDirty = true;

            // Clear button
            if (!string.IsNullOrEmpty(_filterText))
            {
                if (GUILayout.Button(ClearBtnLabel, EditorStyles.toolbarButton, StaticGUILayout.Mini24))
                {
                    _filterText = "";
                    _filterDirty = true;
                    GUI.FocusControl(null);
                }
            }

            // Issue count — hiển thị sát bên phải
            if (_filteredResults != null)
            {
                int count = _cachedFilteredIssues;
                if (count != _lastFilterCount)
                {
                    _lastFilterCount = count;
                    SharedSB.Clear();
                    SharedSB.Append("Found: ").Append(count).Append(" issues");
                    _filterCountContent.text = SharedSB.ToString();
                }
                GUILayout.Label(_filterCountContent, EditorStyles.miniLabel, GUILayout.ExpandWidth(false));
            }

            EditorGUILayout.EndHorizontal();
        }

        // ──────────────── Status bar ────────────────

        private void DrawStatusBar()
        {
            if (_results == null) return;

            int total    = _cachedTotalIssues;
            int filtered = _cachedFilteredIssues;
            bool hasFilter = !string.IsNullOrEmpty(_filterText);

            // Dirty-flag cache: rebuild only when values change
            if (_statusContent == null
                || _lastStatusTotal != total
                || _lastStatusFiltered != filtered
                || _lastStatusHasFilter != hasFilter)
            {
                _lastStatusTotal    = total;
                _lastStatusFiltered = filtered;
                _lastStatusHasFilter = hasFilter;

                _statusContent ??= new GUIContent();
                SharedSB.Clear();
                if (hasFilter)
                    SharedSB.Append("Showing ").Append(filtered).Append(" of ").Append(total).Append(" issues");
                else
                    SharedSB.Append("Total: ").Append(total).Append(" issues in ").Append(_results.Count).Append(" GameObjects");
                _statusContent.text = SharedSB.ToString();
            }

            DrawSeparator();
            GUILayout.Label(_statusContent, EditorStyles.centeredGreyMiniLabel);
        }

        // ──────────────── Scan execution ────────────────

        private void ExecuteScan()
        {
            switch (_scope)
            {
                case ScanScope.Scene:
                {
                    Scene scene = SceneManager.GetActiveScene();
                    GameObject[] roots = scene.GetRootGameObjects();
                    _results = NullRefScannerCore.ScanGameObjects(roots);
                    break;
                }
                case ScanScope.Selection:
                {
                    GameObject[] selected = Selection.gameObjects;
                    if (selected.Length == 0)
                    {
                        EditorUtility.DisplayDialog("Null Reference Scanner",
                            "No GameObjects selected.\nSelect objects in the Hierarchy first.",
                            "OK");
                        return;
                    }
                    _results = NullRefScannerCore.ScanGameObjects(selected);
                    break;
                }
                case ScanScope.Prefabs:
                {
                    var prefabs = NullRefScannerCore.GetSelectedPrefabs();
                    if (prefabs.Count == 0)
                    {
                        EditorUtility.DisplayDialog("Null Reference Scanner",
                            "No Prefabs selected.\nSelect prefab assets in the Project window first.",
                            "OK");
                        return;
                    }
                    _results = NullRefScannerCore.ScanGameObjects(prefabs);
                    break;
                }
            }

            // Sort by severity — MissingRef/NullManagedRef first (luôn là bug)
            _results.Sort(CompareBySeverityDesc);

            // Reset scroll + filter
            _scroll = Vector2.zero;
            _filterDirty = true;
            Repaint();
        }

        // ──────────────── Filter logic ────────────────

        private void RebuildFilteredResults()
        {
            // Cache total issues
            _cachedTotalIssues = CountTotalIssues(_results);

            if (string.IsNullOrEmpty(_filterText))
            {
                _filteredResults  = _results;
                _cachedFilteredIssues = _cachedTotalIssues;
                return;
            }

            // Reuse list nếu đã tồn tại
            if (_filteredResults == null || _filteredResults == _results)
                _filteredResults = new List<GameObjectResult>();
            else
                _filteredResults.Clear();

            string filter = _filterText;

            for (int i = 0; i < _results.Count; i++)
            {
                var go = _results[i];

                // Match GO name
                if (go.gameObjectName.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    _filteredResults.Add(go);
                    continue;
                }

                // Try matching component or field names
                List<ComponentResult> matchedComps = null;
                for (int c = 0; c < go.components.Count; c++)
                {
                    var comp = go.components[c];
                    if (comp.componentName.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        matchedComps ??= new List<ComponentResult>();
                        matchedComps.Add(comp);
                        continue;
                    }

                    // Match field names, type, displayPath, kind tag
                    List<FieldResult> matchedFields = null;
                    for (int f = 0; f < comp.fields.Count; f++)
                    {
                        var field = comp.fields[f];
                        if (field.fieldName.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0
                            || field.fieldTypeName.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0
                            || field.displayPath.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0
                            || field.kindTag.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            matchedFields ??= new List<FieldResult>();
                            matchedFields.Add(field);
                        }
                    }

                    if (matchedFields != null)
                    {
                        matchedComps ??= new List<ComponentResult>();
                        matchedComps.Add(new ComponentResult(
                            comp.component, comp.componentName, comp.isMissingScript,
                            matchedFields,
                            go.gameObject != null ? go.gameObject.GetInstanceID() : 0, c));
                    }
                }

                if (matchedComps != null)
                {
                    _filteredResults.Add(new GameObjectResult(
                        go.gameObject, go.gameObjectName, matchedComps));
                }
            }

            _cachedFilteredIssues = CountTotalIssues(_filteredResults);
        }

        // ──────────────── Helpers ────────────────

        /// <summary>Static comparison delegate — tránh lambda allocation mỗi lần sort.</summary>
        private static int CompareBySeverityDesc(GameObjectResult a, GameObjectResult b)
        {
            return b.maxSeverity.CompareTo(a.maxSeverity);
        }

        private static int CountTotalIssues(List<GameObjectResult> results)
        {
            int count = 0;
            for (int i = 0; i < results.Count; i++)
                count += results[i].totalIssueCount;
            return count;
        }

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
