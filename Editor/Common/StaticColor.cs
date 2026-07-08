using UnityEngine;

namespace Horcrux.Editor.Common
{
    public static class StaticColor
    {
        public static readonly Color DangerColor    = new(0.95f, 0.35f, 0.35f, 1f);
        public static readonly Color PlayTintColor  = new(0.45f, 0.85f, 0.55f, 1f);
        public static readonly Color RootHoverColor = new(0.35f, 0.7f, 1f, 0.45f);    // root mode: soft blue
        public static readonly Color PinHoverColor  = new(1f, 0.78f, 0.35f, 0.5f);    // pin  mode: soft amber
        public static readonly Color WarningColor   = new(1f, 0.76f, 0.28f, 1f);      // null ref warning: amber
        public static readonly Color SeparatorColor = new(0.5f, 0.5f, 0.5f, 0.3f);   // thin separator line
        public static readonly Color ScanBtnColor  = new(0.3f, 0.75f, 0.55f, 1f);   // scan button: calm green
        public static readonly Color ScopeActiveColor = new(0.35f, 0.65f, 0.95f, 1f); // active scope toggle: soft blue
    }
}