using System;
using System.Reflection;
using UnityEditor;

namespace Horcrux.Editor.Probe
{
    /// <summary>
    /// Đặt Game view về một độ phân giải cố định, trong và ngoài Play. Unity không có API công khai cho việc này: đi qua
    /// reflection vào GameViewSizes, GameView (Unity 6). Cỡ chưa có trong danh sách thì thêm cỡ tuỳ chỉnh vào nhóm của platform
    /// đang chọn; danh sách này lưu trong preference của máy, không vào project. Layout UI cập nhật vài frame sau khi đổi cỡ.
    /// </summary>
    public static class GameViewSize
    {
        private const BindingFlags Any = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
        private const string PreviousKey = "Horcrux.GameViewSize.Previous";

        private static readonly Assembly EditorAssembly = typeof(EditorWindow).Assembly;
        private static readonly Type GameViewType = EditorAssembly.GetType("UnityEditor.GameView");

        /// <summary>Chọn cỡ width×height, nhớ cỡ đang chọn cho <see cref="Restore"/> (chỉ lần đầu tới khi Restore). Trả <c>index: nhãn</c>.</summary>
        public static string Set(int width, int height)
        {
            var group = CurrentGroup();
            var index = IndexOf(group, width, height);
            if (index < 0)
            {
                var sizeType = EditorAssembly.GetType("UnityEditor.GameViewSize");
                var kind = Enum.Parse(EditorAssembly.GetType("UnityEditor.GameViewSizeType"), "FixedResolution");
                var size = Activator.CreateInstance(sizeType, kind, width, height, "");
                group.GetType().GetMethod("AddCustomSize", Any).Invoke(group, new[] { size });
                index = IndexOf(group, width, height);
            }
            if (SessionState.GetInt(PreviousKey, -1) < 0)
                SessionState.SetInt(PreviousKey, SelectedIndex());
            Select(index);
            return index + ": " + Texts(group)[index];
        }

        /// <summary>Trả về cỡ trước lần <see cref="Set"/> đầu tiên; chưa Set thì không làm gì.</summary>
        public static string Restore()
        {
            var previous = SessionState.GetInt(PreviousKey, -1);
            if (previous < 0)
                return "nothing to restore";
            SessionState.EraseInt(PreviousKey);
            Select(previous);
            return previous + ": " + Texts(CurrentGroup())[previous];
        }

        /// <summary><c>index: nhãn</c> của cỡ đang chọn.</summary>
        public static string Current()
        {
            var index = SelectedIndex();
            return index + ": " + Texts(CurrentGroup())[index];
        }

        private static object CurrentGroup()
        {
            var sizesType = EditorAssembly.GetType("UnityEditor.GameViewSizes");
            var instance = typeof(ScriptableSingleton<>).MakeGenericType(sizesType).GetProperty("instance").GetValue(null);
            var groupType = sizesType.GetProperty("currentGroupType", Any).GetValue(instance);
            return sizesType.GetMethod("GetGroup", Any).Invoke(instance, new[] { groupType });
        }

        private static string[] Texts(object group) => (string[])group.GetType().GetMethod("GetDisplayTexts", Any).Invoke(group, null);

        // so theo kích thước thật của từng mục, không theo nhãn: nhãn có thể mang tên đặt tay
        private static int IndexOf(object group, int width, int height)
        {
            var groupType = group.GetType();
            var count = (int)groupType.GetMethod("GetTotalCount", Any).Invoke(group, null);
            var getSize = groupType.GetMethod("GetGameViewSize", Any);
            for (var i = 0; i < count; i++)
            {
                var size = getSize.Invoke(group, new object[] { i });
                var sizeType = size.GetType();
                if (sizeType.GetProperty("sizeType", Any).GetValue(size).ToString() == "FixedResolution" &&
                    (int)sizeType.GetProperty("width", Any).GetValue(size) == width &&
                    (int)sizeType.GetProperty("height", Any).GetValue(size) == height)
                    return i;
            }
            return -1;
        }

        private static EditorWindow Window() => EditorWindow.GetWindow(GameViewType, false, null, false);

        private static int SelectedIndex() => (int)GameViewType.GetProperty("selectedSizeIndex", Any).GetValue(Window());

        private static void Select(int index)
        {
            var window = Window();
            GameViewType.GetMethod("SizeSelectionCallback", Any).Invoke(window, new object[] { index, null });
            window.Repaint();
        }
    }
}
