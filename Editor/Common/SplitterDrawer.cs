using Horcrux.Runtime.Implementations.Utilities;
using UnityEditor;
using UnityEngine;

namespace Horcrux.Editor.Common
{
    [CustomPropertyDrawer(typeof(SplitterAttribute))]
    public sealed class SplitterDrawer : DecoratorDrawer
    {
        private static readonly Color AccentColor = new(0.35f, 0.7f, 1f, 1f);
        private static readonly Color LineColorDark = new(0.3f, 0.3f, 0.3f, 0.7f);
        private static readonly Color LineColorLight = new(0.65f, 0.65f, 0.65f, 0.7f);

        private const float AccentBarWidth = 5f;
        private const float AccentBarHeight = 16f;
        private const float LineHeight = 1f;
        private const float TopPadding = 20f;
        private const float BottomPadding = 4f;
        private const float LineTopOffset = 4f;

        private static GUIStyle labelStyle;
        private static readonly GUIContent ReusableContent = new();

        private static void EnsureStyle()
        {
            if (labelStyle != null) return;

            labelStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 16,
            };
        }

        public override float GetHeight()
        {
            return TopPadding + AccentBarHeight + LineTopOffset + LineHeight + BottomPadding;
        }

        public override void OnGUI(Rect position)
        {
            EnsureStyle();
            var attr = (SplitterAttribute)attribute;

            float barY = position.y + TopPadding;

            var accentRect = new Rect(position.x, barY, AccentBarWidth, AccentBarHeight);
            EditorGUI.DrawRect(accentRect, AccentColor);

            if (!string.IsNullOrEmpty(attr.Title))
            {
                ReusableContent.text = attr.Title;
                var textRect = new Rect(
                    position.x + AccentBarWidth,
                    barY,
                    position.width - AccentBarWidth,
                    AccentBarHeight);
                GUI.Label(textRect, ReusableContent, labelStyle);
            }

            float lineY = barY + AccentBarHeight + LineTopOffset;
            Color lineColor = EditorGUIUtility.isProSkin ? LineColorDark : LineColorLight;
            EditorGUI.DrawRect(new Rect(position.x, lineY, position.width, LineHeight), lineColor);
        }
    }
}
