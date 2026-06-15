using UnityEngine;

namespace Horcrux.Editor.Common
{
    public static class StaticGUILayout
    {
        public static readonly GUILayoutOption[] TbAddGroup   = { GUILayout.Width(100) };
        public static readonly GUILayoutOption[] TbScene      = { GUILayout.Width(180) };
        public static readonly GUILayoutOption[] Mini24       = { GUILayout.Width(24) };
        public static readonly GUILayoutOption[] Mini40       = { GUILayout.Width(40) };
        public static readonly GUILayoutOption[] PlayBtn      = { GUILayout.Height(32) };
        public static readonly GUILayoutOption[] Ping         = { GUILayout.MaxWidth(50) };
        public static readonly GUILayoutOption[] AssetName    = { GUILayout.MinWidth(150) };
        public static readonly GUILayoutOption[] FolderName   = { GUILayout.MinWidth(200) };
        public static readonly GUILayoutOption[] Remove       = { GUILayout.MinWidth(50), GUILayout.MaxWidth(60) };
    }
}