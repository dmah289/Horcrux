using System;
using System.Collections.Generic;
using Horcrux.Editor.Common;
using UnityEditor;
using UnityEngine;

namespace Horcrux.Editor.PlayerPrefsEditor
{
    public class PlayerPrefsEditor : EditorWindow
    {
        private IPlayerPrefsProvider playerPrefsProvider =
#if UNITY_EDITOR_WIN
            new WindowsPlayerPrefsProvider();
#else
            null;
#endif

        private string searchField = "";
        private Vector2 scrollPos;
        private Dictionary<string, string> inputPlayerPrefs = new();
        private Dictionary<string, Vector2> m_stringScrollPositions = new();
        private HashSet<string> savedKey = new();
        private List<PlayerPrefsPair> currentPairs;
        private int visibleCount;
        private const int MAX_STRING_LINES = 7;

        // Cached per-row data — rebuilt once per Layout event, reused across Repaint
        private string[] cachedValueStrs = Array.Empty<string>();
        private bool[] cachedIsJson = Array.Empty<bool>();

        // Reusable GUIContent for CalcHeight — avoids GC alloc per string row per frame
        private readonly GUIContent m_calcHeightContent = new();

        // Styles
        private GUIStyle typeStyle;
        private GUIStyle wordWrapStyle;

        // Layout options — rebuilt only when window width changes
        private float cachedWidth;
        private GUILayoutOption[] keyWidthOpt, typeWidthOpt, valueWidthOpt, actionWidthOpt;
        private static readonly GUILayoutOption[] jsonBtnWidth = { GUILayout.Width(24) };
        private static readonly GUILayoutOption[] searchLabelWidth = { GUILayout.Width(50) };
        private static readonly GUILayoutOption[] clearSearchWidth = { GUILayout.Width(20) };
        private static readonly GUILayoutOption[] expandWidthTrue = { GUILayout.ExpandWidth(true) };

        // Colors
        private static readonly Color RowEvenColor = new(0f, 0f, 0f, 0.06f);
        private static readonly Color RowOddColor = new(0f, 0f, 0f, 0.14f);
        private static readonly Color ModifiedBgColor = new(1f, 0.6f, 0.2f, 0.5f);
        private static readonly Color SaveColor = new(0.35f, 0.85f, 0.45f, 1f);
        private static readonly Color RevertColor = new(0.55f, 0.75f, 1f, 1f);

        // Status bar — only rebuild string when values change
        private readonly GUIContent statusContent = new();
        private int lastStatusTotal = -1, lastStatusVisible = -1, lastStatusModified = -1;
        private bool lastStatusHasSearch;

        [MenuItem("manhnd_sdk/Player Prefs Editor")]
        public static void ShowWindow()
        {
            var window = GetWindow<PlayerPrefsEditor>();
            window.minSize = new Vector2(760f, 300);
            window.titleContent = new GUIContent("Player Prefs Editor");
            window.Show();
        }

        private void OnGUI()
        {
            StaticStyles.Ensure();
            StaticGUIContent.EnsureIcons();
            EnsureLocalStyles();
            CacheLayoutOptions();

            if (playerPrefsProvider == null)
            {
                EditorGUILayout.HelpBox("PlayerPrefs Editor is only supported on Windows.", MessageType.Warning);
                return;
            }

            if (Event.current.type == EventType.Layout)
            {
                currentPairs = playerPrefsProvider.PlayerPrefsPairs;
                CachePerRowData();
                PurgeStaleDirtyEntries();
            }

            if (currentPairs == null) return;

            HandleScrollWheelBoost();
            GUILayout.Label("Player Prefs Editor", StaticStyles.SectionTitle);
            DrawQuickButtons();
            DrawSearchField();
            DrawColumnHeaders();
            DrawPlayerPrefs();
            DrawStatusBar();
        }

        // ──────────────── Init ────────────────

        private void EnsureLocalStyles()
        {
            if (typeStyle != null) return;
            typeStyle = new GUIStyle(EditorStyles.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold,
            };
            wordWrapStyle = new GUIStyle(EditorStyles.textArea)
            {
                wordWrap = true,
            };
        }

        private void CacheLayoutOptions()
        {
            float w = position.width;
            if (Math.Abs(w - cachedWidth) < 1f) return;
            cachedWidth = w;

            int max = (int)w;
            keyWidthOpt = new[] { GUILayout.Width((int)(max * 0.25f)) };
            typeWidthOpt = new[] { GUILayout.Width((int)(max * 0.1f)) };
            valueWidthOpt = new[] { GUILayout.Width((int)(max * 0.4f)) };
            actionWidthOpt = new[] { GUILayout.Width((int)(max * 0.23f / 3f)) };
        }

        private void CachePerRowData()
        {
            int count = currentPairs.Count;
            if (cachedValueStrs.Length < count)
            {
                cachedValueStrs = new string[count];
                cachedIsJson = new bool[count];
            }

            for (int i = 0; i < count; i++)
            {
                PlayerPrefsPair pair = currentPairs[i];
                object v = pair.Value;

                cachedValueStrs[i] = v is string s ? s : v.ToString();
                cachedIsJson[i] = v is string && JsonEditWindow.IsJsonLike(cachedValueStrs[i]);
            }
        }

        // ──────────────── Draw ────────────────

        private void DrawQuickButtons()
        {
            Color origBg = GUI.backgroundColor;
            bool hasModified = inputPlayerPrefs.Count > 0;
            bool hasEntries = currentPairs.Count > 0;

            GUILayout.BeginHorizontal();

            GUI.enabled = hasModified;
            GUI.backgroundColor = SaveColor;
            if (GUILayout.Button(StaticGUIContent.PrefsSaveAll))
                SaveAll();

            GUI.backgroundColor = RevertColor;
            if (GUILayout.Button(StaticGUIContent.PrefsRevertAll))
                RevertAll();

            GUI.enabled = hasEntries;
            GUI.backgroundColor = StaticColor.DangerColor;
            if (GUILayout.Button(StaticGUIContent.PrefsDeleteAll))
            {
                string message = searchField.Length > 0
                    ? $"This will delete ALL {currentPairs.Count} entries, not just the filtered results.\nThis cannot be undone."
                    : $"Delete all {currentPairs.Count} entries? This cannot be undone.";

                if (EditorUtility.DisplayDialog("Delete All PlayerPrefs", message, "Delete All", "Cancel"))
                    DeleteAll();
            }

            GUILayout.EndHorizontal();
            GUI.enabled = true;
            GUI.backgroundColor = origBg;
        }

        private void DrawSearchField()
        {
            GUILayout.BeginHorizontal(EditorStyles.toolbar);
            GUILayout.Label("Search", searchLabelWidth);
            searchField = GUILayout.TextField(searchField);
            if (searchField.Length > 0 && GUILayout.Button("X", clearSearchWidth))
                searchField = "";
            GUILayout.EndHorizontal();
        }

        private void DrawColumnHeaders()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            GUILayout.Label("Key", EditorStyles.boldLabel, keyWidthOpt);
            GUILayout.Label("Type", EditorStyles.boldLabel, typeWidthOpt);
            GUILayout.Label("Value", EditorStyles.boldLabel, valueWidthOpt);
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
        }

        private void DrawPlayerPrefs()
        {
            scrollPos = EditorGUILayout.BeginScrollView(scrollPos);
            Color origBg = GUI.backgroundColor;
            visibleCount = 0;
            bool hasSearch = searchField.Length > 0;

            for (int i = 0; i < currentPairs.Count; i++)
            {
                PlayerPrefsPair pair = currentPairs[i];
                string valueStr = cachedValueStrs[i];
                bool isJson = cachedIsJson[i];
                bool isString = pair.Value is string;

                if (hasSearch)
                {
                    bool match = pair.Key.IndexOf(searchField, StringComparison.OrdinalIgnoreCase) >= 0
                                 || valueStr.IndexOf(searchField, StringComparison.OrdinalIgnoreCase) >= 0
                                 || pair.AliasType.IndexOf(searchField, StringComparison.OrdinalIgnoreCase) >= 0;
                    if (!match) continue;
                }

                Rect row = EditorGUILayout.BeginHorizontal();
                EditorGUI.DrawRect(row, visibleCount % 2 == 0 ? RowEvenColor : RowOddColor);
                visibleCount++;

                // Key
                GUILayout.Label(pair.Key, keyWidthOpt);

                // Type
                typeStyle.normal.textColor = pair.TypeColor;
                GUILayout.Label(pair.AliasType, typeStyle, typeWidthOpt);

                // Value
                bool isChanged = inputPlayerPrefs.TryGetValue(pair.Key, out string dirtyValue);
                string displayValue = isChanged ? dirtyValue : valueStr;

                if (isChanged)
                    GUI.backgroundColor = ModifiedBgColor;

                string editedValue;
                if (isString)
                {
                    float valueWidth = cachedWidth * 0.4f;
                    float lineHeight = wordWrapStyle.lineHeight > 0 ? wordWrapStyle.lineHeight : EditorGUIUtility.singleLineHeight;
                    float maxHeight = lineHeight * MAX_STRING_LINES + wordWrapStyle.padding.vertical;
                    m_calcHeightContent.text = displayValue;
                    float fullHeight = wordWrapStyle.CalcHeight(m_calcHeightContent, valueWidth);

                    if (fullHeight > maxHeight)
                    {
                        if (!m_stringScrollPositions.TryGetValue(pair.Key, out Vector2 strScroll))
                            strScroll = Vector2.zero;

                        strScroll = EditorGUILayout.BeginScrollView(strScroll, GUILayout.Height(maxHeight), valueWidthOpt[0]);
                        editedValue = EditorGUILayout.TextArea(displayValue, wordWrapStyle, expandWidthTrue);
                        EditorGUILayout.EndScrollView();

                        m_stringScrollPositions[pair.Key] = strScroll;
                    }
                    else
                    {
                        editedValue = EditorGUILayout.TextArea(displayValue, wordWrapStyle, valueWidthOpt);
                    }
                }
                else
                {
                    editedValue = GUILayout.TextArea(displayValue, valueWidthOpt);
                }

                if (editedValue != displayValue)
                {
                    if (editedValue != valueStr)
                        inputPlayerPrefs[pair.Key] = editedValue;
                    else
                        inputPlayerPrefs.Remove(pair.Key);
                }

                GUI.backgroundColor = origBg;

                // JSON button — always rendered for string+json rows (stable control count)
                if (isJson)
                {
                    if (GUILayout.Button(StaticGUIContent.PrefsJsonEdit, jsonBtnWidth))
                    {
                        string capturedKey = pair.Key;
                        JsonEditWindow.Open(capturedKey, displayValue, compact =>
                        {
                            inputPlayerPrefs[capturedKey] = compact;
                        });
                    }
                }

                // Save / Revert — disabled when not dirty
                GUI.enabled = isChanged;

                GUI.backgroundColor = SaveColor;
                if (GUILayout.Button(StaticGUIContent.PrefsSave, actionWidthOpt) && isChanged)
                {
                    if (Save(pair.Key, pair.Value, dirtyValue))
                    {
                        inputPlayerPrefs.Remove(pair.Key);
                        PlayerPrefs.Save();
                    }
                }

                GUI.backgroundColor = RevertColor;
                if (GUILayout.Button(StaticGUIContent.PrefsRevert, actionWidthOpt))
                    inputPlayerPrefs.Remove(pair.Key);

                GUI.enabled = true;

                // Delete — always enabled
                GUI.backgroundColor = StaticColor.DangerColor;
                if (GUILayout.Button(StaticGUIContent.PrefsDelete, actionWidthOpt))
                {
                    inputPlayerPrefs.Remove(pair.Key);
                    m_stringScrollPositions.Remove(pair.Key);
                    PlayerPrefs.DeleteKey(pair.Key);
                    PlayerPrefs.Save();
                }

                GUI.backgroundColor = origBg;
                EditorGUILayout.EndHorizontal();
            }

            if (visibleCount == 0)
            {
                string hint = currentPairs.Count == 0
                    ? "No PlayerPrefs found for this project."
                    : "No entries match the search filter.";
                EditorGUILayout.HelpBox(hint, MessageType.Info);
            }

            GUI.backgroundColor = origBg;
            EditorGUILayout.EndScrollView();
        }

        private void DrawStatusBar()
        {
            int total = currentPairs.Count;
            int modified = inputPlayerPrefs.Count;
            bool hasSearch = searchField.Length > 0;

            if (total != lastStatusTotal || visibleCount != lastStatusVisible
                || modified != lastStatusModified || hasSearch != lastStatusHasSearch)
            {
                lastStatusTotal = total;
                lastStatusVisible = visibleCount;
                lastStatusModified = modified;
                lastStatusHasSearch = hasSearch;

                statusContent.text = hasSearch
                    ? $"{visibleCount} / {total} entries"
                    : $"{total} entries";
                if (modified > 0)
                    statusContent.text += $"  |  {modified} modified";
            }

            GUILayout.BeginHorizontal(EditorStyles.toolbar);
            GUILayout.Label(statusContent, EditorStyles.miniLabel);
            GUILayout.EndHorizontal();
        }

        // ──────────────── Data ────────────────

        private bool Save(string ppKey, object oldValue, string ppNewValue)
        {
            switch (oldValue)
            {
                case int:
                    if (int.TryParse(ppNewValue, out int intVal))
                    {
                        PlayerPrefs.SetInt(ppKey, intVal);
                        return true;
                    }
                    Debug.LogWarning($"[PlayerPrefsEditor] Cannot parse \"{ppNewValue}\" as int for key \"{ppKey}\"");
                    break;
                case float:
                    if (float.TryParse(ppNewValue, out float floatVal))
                    {
                        PlayerPrefs.SetFloat(ppKey, floatVal);
                        return true;
                    }
                    Debug.LogWarning($"[PlayerPrefsEditor] Cannot parse \"{ppNewValue}\" as float for key \"{ppKey}\"");
                    break;
                case string:
                    PlayerPrefs.SetString(ppKey, ppNewValue);
                    return true;
            }
            return false;
        }

        private void SaveAll()
        {
            foreach (var input in inputPlayerPrefs)
            {
                int index = FindPairIndex(input.Key);
                if (index >= 0 && Save(input.Key, currentPairs[index].Value, input.Value))
                    savedKey.Add(input.Key);
            }

            if (savedKey.Count > 0)
            {
                foreach (var key in savedKey)
                    inputPlayerPrefs.Remove(key);
                savedKey.Clear();
                PlayerPrefs.Save();
            }
        }

        private void RevertAll()
        {
            inputPlayerPrefs.Clear();
            m_stringScrollPositions.Clear();
        }

        private void DeleteAll()
        {
            for (int i = 0; i < currentPairs.Count; i++)
                PlayerPrefs.DeleteKey(currentPairs[i].Key);
            inputPlayerPrefs.Clear();
            m_stringScrollPositions.Clear();
            PlayerPrefs.Save();
        }

        private int FindPairIndex(string key)
        {
            for (int i = 0; i < currentPairs.Count; i++)
            {
                if (currentPairs[i].Key == key)
                    return i;
            }
            return -1;
        }

        private void HandleScrollWheelBoost()
        {
            if (Event.current.type != EventType.ScrollWheel) return;
            scrollPos += Event.current.delta * 20f;
            Event.current.Use();
        }

        private void PurgeStaleDirtyEntries()
        {
            if (inputPlayerPrefs.Count == 0 && m_stringScrollPositions.Count == 0) return;

            savedKey.Clear();
            foreach (var key in inputPlayerPrefs.Keys)
            {
                if (FindPairIndex(key) < 0)
                    savedKey.Add(key);
            }

            foreach (var key in m_stringScrollPositions.Keys)
            {
                if (FindPairIndex(key) < 0)
                    savedKey.Add(key);
            }

            foreach (var key in savedKey)
            {
                inputPlayerPrefs.Remove(key);
                m_stringScrollPositions.Remove(key);
            }
            savedKey.Clear();
        }

    }
}
