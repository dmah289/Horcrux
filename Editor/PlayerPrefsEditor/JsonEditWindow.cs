using System;
using System.Text;
using Horcrux.Editor.Common;
using UnityEditor;
using UnityEngine;

namespace Horcrux.Editor.PlayerPrefsEditor
{
    public class JsonEditWindow : EditorWindow
    {
        private string key;
        private string indentedJson;
        private Vector2 scrollPos;
        private Action<string> onSave;
        private GUIStyle monoStyle;
        private bool jsonValid = true;

        private static JsonEditWindow currentInstance;

        // Cached GUIContent / GUILayoutOption — zero GC per frame
        private GUIContent toolbarLabel;
        private static readonly GUILayoutOption[] saveBtnWidth = { GUILayout.Width(80) };
        private static readonly GUILayoutOption[] cancelBtnWidth = { GUILayout.Width(80) };
        private static readonly GUILayoutOption[] expandHeight = { GUILayout.ExpandHeight(true) };

        public static void Open(string key, string compactJson, Action<string> onSave)
        {
            if (currentInstance != null)
            {
                currentInstance.Close();
                currentInstance = null;
            }

            var window = CreateInstance<JsonEditWindow>();
            window.key = key;
            window.indentedJson = IndentJson(compactJson);
            window.onSave = onSave;
            window.titleContent = new GUIContent($"JSON — {key}");
            window.toolbarLabel = new GUIContent($"Key: {key}");
            window.minSize = new Vector2(400f, 300f);
            currentInstance = window;
            window.ShowUtility();
        }

        private void OnDestroy()
        {
            if (currentInstance == this)
                currentInstance = null;
        }

        public static bool IsJsonLike(string value)
        {
            if (string.IsNullOrEmpty(value) || value.Length < 2) return false;
            char first = value[0];
            if (first != '{' && first != '[') return false;

            int depth = 0;
            bool inString = false;
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                if (inString)
                {
                    if (c == '\\') { i++; continue; }
                    if (c == '"') inString = false;
                    continue;
                }
                switch (c)
                {
                    case '"': inString = true; break;
                    case '{': case '[': depth++; break;
                    case '}': case ']': depth--; break;
                }
                if (depth < 0) return false;
            }
            return depth == 0;
        }

        private void OnGUI()
        {
            StaticGUIContent.EnsureIcons();
            EnsureMonoStyle();

            // Toolbar
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            GUILayout.Label(toolbarLabel, EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            // Editor area
            scrollPos = EditorGUILayout.BeginScrollView(scrollPos);
            EditorGUI.BeginChangeCheck();
            indentedJson = EditorGUILayout.TextArea(indentedJson, monoStyle, expandHeight);
            if (EditorGUI.EndChangeCheck())
                jsonValid = IsJsonLike(MinifyJson(indentedJson));
            EditorGUILayout.EndScrollView();

            if (!jsonValid)
                EditorGUILayout.HelpBox("JSON không hợp lệ. Hãy kiểm tra lại trước khi lưu.", MessageType.Warning);

            // Buttons
            Color origBg = GUI.backgroundColor;

            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();

            GUI.enabled = jsonValid;
            GUI.backgroundColor = SaveColor;
            if (GUILayout.Button(StaticGUIContent.PrefsSave, saveBtnWidth))
            {
                string compact = MinifyJson(indentedJson);
                onSave?.Invoke(compact);
                Close();
            }

            GUI.enabled = true;
            GUI.backgroundColor = origBg;
            if (GUILayout.Button("Cancel", cancelBtnWidth))
                Close();

            GUILayout.EndHorizontal();
            GUILayout.Space(4);
        }

        private static readonly Color SaveColor = new(0.35f, 0.85f, 0.45f, 1f);

        private void EnsureMonoStyle()
        {
            if (monoStyle != null) return;

            Font mono = Font.CreateDynamicFontFromOSFont("Consolas", 13);
            monoStyle = new GUIStyle(EditorStyles.textArea)
            {
                fontSize = 13,
                wordWrap = false,
            };
            if (mono != null)
                monoStyle.font = mono;
        }

        // ──────────────── JSON indent / minify ────────────────

        private static string IndentJson(string json)
        {
            if (string.IsNullOrEmpty(json)) return json;

            var sb = new StringBuilder(json.Length * 2);
            int depth = 0;
            bool inString = false;

            for (int i = 0; i < json.Length; i++)
            {
                char c = json[i];

                if (inString)
                {
                    sb.Append(c);
                    if (c == '\\') { i++; if (i < json.Length) sb.Append(json[i]); continue; }
                    if (c == '"') inString = false;
                    continue;
                }

                switch (c)
                {
                    case '"':
                        inString = true;
                        sb.Append(c);
                        break;

                    case '{': case '[':
                        sb.Append(c);
                        if (IsEmptyPair(json, i))
                        {
                            sb.Append(json[++i]);
                        }
                        else
                        {
                            depth++;
                            sb.Append('\n');
                            AppendIndent(sb, depth);
                        }
                        break;

                    case '}': case ']':
                        depth--;
                        sb.Append('\n');
                        AppendIndent(sb, depth);
                        sb.Append(c);
                        break;

                    case ',':
                        sb.Append(c);
                        sb.Append('\n');
                        AppendIndent(sb, depth);
                        break;

                    case ':':
                        sb.Append(": ");
                        break;

                    default:
                        if (!char.IsWhiteSpace(c))
                            sb.Append(c);
                        break;
                }
            }

            return sb.ToString();
        }

        private static bool IsEmptyPair(string json, int openIndex)
        {
            char open = json[openIndex];
            char close = open == '{' ? '}' : ']';
            for (int j = openIndex + 1; j < json.Length; j++)
            {
                if (char.IsWhiteSpace(json[j])) continue;
                return json[j] == close;
            }
            return false;
        }

        private static void AppendIndent(StringBuilder sb, int depth)
        {
            for (int i = 0; i < depth; i++)
                sb.Append("    ");
        }

        private static string MinifyJson(string json)
        {
            if (string.IsNullOrEmpty(json)) return json;

            var sb = new StringBuilder(json.Length);
            bool inString = false;

            for (int i = 0; i < json.Length; i++)
            {
                char c = json[i];

                if (inString)
                {
                    sb.Append(c);
                    if (c == '\\') { i++; if (i < json.Length) sb.Append(json[i]); continue; }
                    if (c == '"') inString = false;
                    continue;
                }

                if (char.IsWhiteSpace(c)) continue;
                if (c == '"') inString = true;
                sb.Append(c);
            }

            return sb.ToString();
        }
    }
}
