using UnityEditor;
using UnityEngine;

namespace Horcrux.Editor.Common
{
    // GUIStyle init is deferred to first OnGUI because GUI.skin is not available before that.
    public static class StaticStyles
    {
        public static GUIStyle SectionTitle;
        public static GUIStyle GroupTitle;
        public static GUIStyle AssetButton;     // left-aligned for readable names
        public static GUIStyle PlayButton;
        public static GUIStyle GroupBox;
        public static GUIStyle DropHint;

        public static void Ensure()
        {
            if (SectionTitle != null) return;

            SectionTitle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 13,
                margin = new RectOffset(2, 2, 6, 0),
            };

            GroupTitle = new GUIStyle(EditorStyles.foldout) { fontSize = 13, fontStyle = FontStyle.Bold };

            AssetButton = new GUIStyle(GUI.skin.button)
            {
                alignment = TextAnchor.MiddleLeft,
                padding = new RectOffset(8, 4, 2, 2),
            };

            PlayButton = new GUIStyle(GUI.skin.button)
            {
                fontSize = 14,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
            };

            GroupBox = new GUIStyle(GUI.skin.box)
            {
                padding = new RectOffset(10, 10, 8, 8),
                margin = new RectOffset(2, 2, 4, 4),
            };

            DropHint = new GUIStyle(EditorStyles.helpBox)
            {
                fontSize = 12,
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Italic,
            };
        }
    }
}