using System;
using System.Collections.Generic;
using Horcrux.Editor.Common;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Horcrux.Editor.NullRefScanner
{
    public class NullRefScannerWindow : EditorWindow
    {
        // ──────────────── Types ────────────────

        private enum ScanScope { Scene, Selection, Prefabs }

        // ──────────────── Static eager cache ────────────────

        private static readonly GUILayoutOption[] _scopeBtnOpts = { GUILayout.Width(75) };

        private static readonly Color RowEvenColor = new(0f, 0f, 0f, 0.06f);
        private static readonly Color RowOddColor  = new(0f, 0f, 0f, 0.12f);

        // ──────────────── Per-instance state ────────────────

        private ScanScope _scope = ScanScope.Scene;
        private string    _filterText = "";
        private Vector2   _scroll;

        private List<GameObjectResult> _results;         // null until first scan
        private List<GameObjectResult> _filteredResults;  // rebuilt on Layout when dirty
        private bool _filterDirty = true;

        // ──────────────── Dirty-flag cache: status bar ────────────────

        private GUIContent _statusContent;
        private int        _lastStatusTotal;
        private int        _lastStatusFiltered;
        private bool       _lastStatusHasFilter;

        // ──────────────── Reusable GUIContent for dynamic labels ────────────────

        private readonly GUIContent _goLabelContent = new();

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
            DrawResults();
            GUILayout.EndScrollView();

            DrawStatusBar();
        }

        // ──────────────── Toolbar ────────────────

        private void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            // Scope radio buttons
            DrawScopeToggle(ScanScope.Scene,     "Scene");
            DrawScopeToggle(ScanScope.Selection,  "Selection");
            DrawScopeToggle(ScanScope.Prefabs,    "Prefabs");

            GUILayout.FlexibleSpace();

            // Scan button
            if (GUILayout.Button(StaticGUIContent.ScannerScan, EditorStyles.toolbarButton,
                    StaticGUILayout.ScanBtn))
                ExecuteScan();

            EditorGUILayout.EndHorizontal();
        }

        private void DrawScopeToggle(ScanScope scope, string label)
        {
            bool active = _scope == scope;
            bool toggled = GUILayout.Toggle(active, label, EditorStyles.toolbarButton, _scopeBtnOpts);
            if (toggled && !active)
                _scope = scope;
        }

        // ──────────────── Filter bar ────────────────

        private void DrawFilterBar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            GUILayout.Label("Filter:", EditorStyles.miniLabel, StaticGUILayout.Mini40);

            EditorGUI.BeginChangeCheck();
            _filterText = GUILayout.TextField(_filterText, EditorStyles.toolbarSearchField);
            if (EditorGUI.EndChangeCheck())
                _filterDirty = true;

            if (!string.IsNullOrEmpty(_filterText))
            {
                if (GUILayout.Button("✕", EditorStyles.toolbarButton, StaticGUILayout.Mini24))
                {
                    _filterText = "";
                    _filterDirty = true;
                    GUI.FocusControl(null);
                }
            }

            GUILayout.FlexibleSpace();

            if (_filteredResults != null)
            {
                int count = CountTotalIssues(_filteredResults);
                GUILayout.Label($"Found: {count} issues", EditorStyles.miniLabel);
            }

            EditorGUILayout.EndHorizontal();
        }

        // ──────────────── Results ────────────────

        private void DrawResults()
        {
            if (_results == null)
            {
                EditorGUILayout.HelpBox("Click Scan to find null references.", MessageType.Info);
                return;
            }

            var list = _filteredResults ?? _results;

            if (list.Count == 0)
            {
                string msg = _results.Count == 0
                    ? "✅ No null references found!"
                    : "No results match the filter.";
                EditorGUILayout.HelpBox(msg, MessageType.Info);
                return;
            }

            for (int i = 0; i < list.Count; i++)
            {
                DrawGameObjectRow(list[i], i);
            }
        }

        private void DrawGameObjectRow(GameObjectResult goResult, int index)
        {
            int goId = goResult.gameObject != null ? goResult.gameObject.GetInstanceID() : index;
            string foldKey = "NullRefScanner_GO_" + goId;
            bool expanded = SessionState.GetBool(foldKey, true);

            // Row background
            Rect rowRect = EditorGUILayout.BeginHorizontal();
            if (Event.current.type == EventType.Repaint)
            {
                Color bg = index % 2 == 0 ? RowEvenColor : RowOddColor;
                EditorGUI.DrawRect(rowRect, bg);
            }

            // Foldout
            expanded = EditorGUILayout.Foldout(expanded, GUIContent.none, true);
            SessionState.SetBool(foldKey, expanded);

            // Clickable GO name with issue count
            _goLabelContent.text = $"🎮 {goResult.gameObjectName} ({goResult.TotalIssueCount} issues)";
            if (GUILayout.Button(_goLabelContent, StaticStyles.AssetButton))
            {
                if (goResult.gameObject != null)
                    SelectAndPing(goResult.gameObject);
            }

            EditorGUILayout.EndHorizontal();

            if (!expanded) return;

            // Draw components indented
            EditorGUI.indentLevel++;
            for (int c = 0; c < goResult.components.Count; c++)
                DrawComponentRow(goResult, c, goResult.components[c]);
            EditorGUI.indentLevel--;
        }

        private void DrawComponentRow(GameObjectResult parent, int compIndex, ComponentResult comp)
        {
            if (comp.isMissingScript)
            {
                EditorGUILayout.BeginHorizontal();
                GUILayout.Space(20);
                Color saved = GUI.contentColor;
                GUI.contentColor = StaticColor.WarningColor;
                GUILayout.Label("⚠ Missing Script", EditorStyles.boldLabel);
                GUI.contentColor = saved;
                EditorGUILayout.EndHorizontal();
                return;
            }

            int goId = parent.gameObject != null ? parent.gameObject.GetInstanceID() : 0;
            string foldKey = $"NullRefScanner_Comp_{goId}_{compIndex}";
            bool expanded = SessionState.GetBool(foldKey, true);

            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(20);
            expanded = EditorGUILayout.Foldout(expanded, $"📦 {comp.componentName}", true);
            SessionState.SetBool(foldKey, expanded);
            EditorGUILayout.EndHorizontal();

            if (!expanded) return;

            for (int f = 0; f < comp.fields.Count; f++)
                DrawFieldRow(parent.gameObject, comp.component, comp, comp.fields[f]);
        }

        private void DrawFieldRow(GameObject go, Component comp, ComponentResult compResult, FieldResult field)
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(40);

            // Warning icon
            Color saved = GUI.contentColor;
            GUI.contentColor = StaticColor.WarningColor;
            GUILayout.Label("⚠", StaticGUILayout.WarningIcon);
            GUI.contentColor = saved;

            // Full path: ComponentName > readable path — click to ping component
            string fullPath = $"{compResult.componentName} > {field.displayPath}";
            if (GUILayout.Button(fullPath, EditorStyles.label))
            {
                if (go != null)
                    SelectAndPingProperty(go, comp, field.propertyPath);
            }

            // Separator + type
            GUILayout.Label("——", EditorStyles.miniLabel, StaticGUILayout.Mini24);
            GUILayout.Label(field.fieldTypeName, EditorStyles.miniLabel);

            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
        }

        // ──────────────── Status bar ────────────────

        private void DrawStatusBar()
        {
            if (_results == null) return;

            int total    = CountTotalIssues(_results);
            int filtered = _filteredResults != null ? CountTotalIssues(_filteredResults) : total;
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
                _statusContent.text = hasFilter
                    ? $"Showing {filtered} of {total} issues"
                    : $"Total: {total} issues in {_results.Count} GameObjects";
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

            _filteredResults = new List<GameObjectResult>();
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
                var matchedComps = new List<ComponentResult>();
                for (int c = 0; c < go.components.Count; c++)
                {
                    var comp = go.components[c];
                    if (comp.componentName.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        matchedComps.Add(comp);
                        continue;
                    }

                    // Match field names
                    var matchedFields = new List<FieldResult>();
                    for (int f = 0; f < comp.fields.Count; f++)
                    {
                        var field = comp.fields[f];
                        if (field.fieldName.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0
                            || field.fieldTypeName.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            matchedFields.Add(field);
                        }
                    }

                    if (matchedFields.Count > 0)
                    {
                        matchedComps.Add(new ComponentResult
                        {
                            component      = comp.component,
                            componentName  = comp.componentName,
                            isMissingScript = comp.isMissingScript,
                            fields         = matchedFields
                        });
                    }
                }

                if (matchedComps.Count > 0)
                {
                    _filteredResults.Add(new GameObjectResult
                    {
                        gameObject     = go.gameObject,
                        gameObjectName = go.gameObjectName,
                        components     = matchedComps
                    });
                }
            }
        }

        // ──────────────── Helpers ────────────────

        private static int CountTotalIssues(List<GameObjectResult> results)
        {
            int count = 0;
            for (int i = 0; i < results.Count; i++)
                count += results[i].TotalIssueCount;
            return count;
        }

        private static void SelectAndPing(GameObject go)
        {
            if (IsPrefabAsset(go))
                OpenPrefabAndSelect(go);
            else
            {
                Selection.activeGameObject = go;
                EditorGUIUtility.PingObject(go);
            }
        }

        /// <summary>
        /// Select GO, expand đúng component chứa trường null trong Inspector.
        /// Nếu là prefab asset → mở prefab stage trước.
        /// </summary>
        private static void SelectAndPingProperty(GameObject go, Component comp, string propertyPath)
        {
            if (IsPrefabAsset(go))
                OpenPrefabAndSelect(go);
            else
            {
                Selection.activeGameObject = go;
                EditorGUIUtility.PingObject(go);
            }

            if (comp == null) return;

            // Delay để đợi prefab stage mở xong / Inspector cập nhật
            EditorApplication.delayCall += () => ExpandComponent(comp);
        }

        private static bool IsPrefabAsset(GameObject go)
        {
            return go != null && PrefabUtility.IsPartOfPrefabAsset(go);
        }

        /// <summary>
        /// Mở prefab stage và select đúng GO bên trong prefab.
        /// </summary>
        private static void OpenPrefabAndSelect(GameObject go)
        {
            // Tìm prefab root để mở stage
            GameObject prefabRoot = PrefabUtility.GetOutermostPrefabInstanceRoot(go);
            if (prefabRoot == null)
                prefabRoot = go.transform.root.gameObject;

            string assetPath = AssetDatabase.GetAssetPath(prefabRoot);
            if (string.IsNullOrEmpty(assetPath))
                assetPath = AssetDatabase.GetAssetPath(go);

            if (!string.IsNullOrEmpty(assetPath))
            {
                // Mở prefab stage
                AssetDatabase.OpenAsset(AssetDatabase.LoadAssetAtPath<GameObject>(assetPath));

                // Delay để prefab stage load xong → tìm và select đúng GO bên trong
                var targetPath = GetTransformPath(go.transform);
                EditorApplication.delayCall += () =>
                {
                    var stage = PrefabStageUtility.GetCurrentPrefabStage();

                    if (stage != null)
                    {
                        // Tìm GO theo path trong prefab stage
                        Transform found = stage.prefabContentsRoot.transform;
                        if (!string.IsNullOrEmpty(targetPath))
                        {
                            var child = stage.prefabContentsRoot.transform.Find(targetPath);
                            if (child != null) found = child;
                        }
                        Selection.activeGameObject = found.gameObject;
                        EditorGUIUtility.PingObject(found.gameObject);
                    }
                };
            }
        }

        /// <summary>Lấy path tương đối từ root đến transform (dùng để tìm lại trong prefab stage).</summary>
        private static string GetTransformPath(Transform t)
        {
            var parts = new List<string>();
            Transform current = t;
            while (current.parent != null)
            {
                parts.Add(current.name);
                current = current.parent;
            }
            parts.Reverse();
            return string.Join("/", parts);
        }

        /// <summary>Expand component trong Inspector thông qua ActiveEditorTracker.</summary>
        private static void ExpandComponent(Component comp)
        {
            var tracker = ActiveEditorTracker.sharedTracker;
            var editors = tracker.activeEditors;
            for (int i = 0; i < editors.Length; i++)
            {
                if (editors[i].target == comp)
                {
                    tracker.SetVisible(i, 1);
                    break;
                }
            }
        }

        private static void DrawSeparator()
        {
            Rect r = GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none, GUILayout.Height(1));
            EditorGUI.DrawRect(r, new Color(0.5f, 0.5f, 0.5f, 0.3f));
        }

        private void HandleScrollWheelBoost()
        {
            if (Event.current.type != EventType.ScrollWheel) return;
            _scroll += Event.current.delta * 20f;
            Event.current.Use();
        }
    }
}
