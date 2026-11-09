using System;
using System.Collections;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Horcrux.Editor.Probe
{
    /// <summary>
    /// Chuyển view Play chính sang Device Simulator và chọn thiết bị theo tên, trong và ngoài Play. Simulator giả Screen.width,
    /// height, safeArea, cutouts của máy: thứ Game view không có (Game view luôn có safe area bằng cả màn). Unity không có API
    /// công khai: đi qua reflection vào PlayModeView, SimulatorWindow (Unity 6). Thiết bị là file <c>.device</c> ở bất kỳ đâu
    /// trong Assets (thêm máy thật ở <c>Editor/Probe/Devices/</c>) cộng bộ máy mẫu của Unity.
    /// </summary>
    public static class SimulatorDevice
    {
        private const BindingFlags Any = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
        private const string PreviousViewKey = "Horcrux.SimulatorDevice.PreviousView";
        private const string PreviousDeviceKey = "Horcrux.SimulatorDevice.PreviousDevice";

        private static readonly Type PlayModeViewType = typeof(EditorWindow).Assembly.GetType("UnityEditor.PlayModeView");
        private static readonly Type GameViewType = typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView");
        private static readonly Type SimulatorWindowType =
            Type.GetType("UnityEditor.DeviceSimulation.SimulatorWindow, UnityEditor.DeviceSimulatorModule");

        /// <summary>
        /// Đưa Simulator lên làm view Play chính và chọn thiết bị có <c>friendlyName</c> trùng <paramref name="name"/> (không phân biệt
        /// hoa thường; bỏ được đuôi cỡ <c>" (1170x2532)"</c> của máy mẫu Unity). Nhớ view và thiết bị đang chọn cho <see cref="Restore"/> (chỉ lần đầu tới khi Restore). Không thấy tên thì ném
        /// ArgumentException kèm danh sách. Trả <c>index: tên rộng×cao safe x,y,w,h</c>.
        /// </summary>
        public static string Set(string name)
        {
            if (SessionState.GetString(PreviousViewKey, "") == "")
            {
                var current = MainView();
                var onSimulator = current != null && current.GetType() == SimulatorWindowType;
                SessionState.SetString(PreviousViewKey, onSimulator ? "Simulator" : "Game");
                SessionState.SetInt(PreviousDeviceKey, onSimulator ? DeviceIndex(Main(current)) : -1);
            }

            var main = Main(Swap(SimulatorWindowType));
            var index = IndexOf(main, name);
            if (index < 0)
            {
                main.GetType().GetMethod("UpdateDeviceList", Any).Invoke(main, null);
                index = IndexOf(main, name);
            }
            if (index < 0)
                throw new ArgumentException("SimulatorDevice: no device named '" + name + "'. Devices:\n" + List());
            main.GetType().GetProperty("deviceIndex", Any).SetValue(main, index);
            return Describe(main, index);
        }

        /// <summary>Trả view Play chính và thiết bị về như trước lần <see cref="Set"/> đầu; chưa Set thì không làm gì.</summary>
        public static string Restore()
        {
            var previous = SessionState.GetString(PreviousViewKey, "");
            if (previous == "")
                return "nothing to restore";
            var device = SessionState.GetInt(PreviousDeviceKey, -1);
            SessionState.EraseString(PreviousViewKey);
            SessionState.EraseInt(PreviousDeviceKey);
            if (previous == "Game")
            {
                UseGameView();
                return "game view";
            }
            var main = Main(Swap(SimulatorWindowType));
            if (device >= 0)
                main.GetType().GetProperty("deviceIndex", Any).SetValue(main, device);
            return Describe(main, DeviceIndex(main));
        }

        /// <summary>View Play chính đang là gì; Simulator thì kèm thiết bị.</summary>
        public static string Current()
        {
            var view = MainView();
            if (view == null || view.GetType() != SimulatorWindowType)
                return "game view";
            var main = Main(view);
            return Describe(main, DeviceIndex(main));
        }

        /// <summary>Mọi thiết bị Simulator thấy, một dòng mỗi máy: <c>index: tên</c>. Mở Simulator nếu chưa mở.</summary>
        public static string List()
        {
            var view = MainView();
            var main = view != null && view.GetType() == SimulatorWindowType ? Main(view) : FirstSimulator();
            if (main == null)
                return "no simulator window open";
            var devices = (Array)main.GetType().GetProperty("devices", Any).GetValue(main);
            var sb = new StringBuilder();
            for (var i = 0; i < devices.Length; i++)
                sb.Append(i).Append(": ").Append(FriendlyName(devices.GetValue(i))).Append('\n');
            return sb.ToString();
        }

        /// <summary>Đưa Game view về làm view Play chính nếu Simulator đang chiếm chỗ. GameViewSize gọi trước khi đổi cỡ.</summary>
        internal static void UseGameView()
        {
            var view = MainView();
            if (view != null && view.GetType() != GameViewType)
                Swap(GameViewType);
        }

        private static EditorWindow MainView() =>
            (EditorWindow)PlayModeViewType.GetMethod("GetMainPlayModeView", Any).Invoke(null, null);

        // swap trong cùng ô dock: view cũ đóng, view mới nhận chỗ và trạng thái đã serialize lần trước (thiết bị đã chọn)
        private static EditorWindow Swap(Type viewType)
        {
            var view = MainView();
            if (view == null)
                return EditorWindow.GetWindow(viewType);
            if (view.GetType() != viewType)
            {
                PlayModeViewType.GetMethod("SwapMainWindow", Any).Invoke(view, new object[] { viewType });
                view = MainView();
            }
            return view;
        }

        private static object Main(EditorWindow simulator) => SimulatorWindowType.GetProperty("main", Any).GetValue(simulator);

        private static object FirstSimulator()
        {
            var instances = (IList)SimulatorWindowType.GetField("s_SimulatorInstances", Any).GetValue(null);
            return instances.Count > 0 ? Main((EditorWindow)instances[0]) : null;
        }

        private static int DeviceIndex(object main) => (int)main.GetType().GetProperty("deviceIndex", Any).GetValue(main);

        private static int IndexOf(object main, string name)
        {
            // máy mẫu của Unity mang đuôi cỡ "Tên (1170x2532)": tên trần cũng khớp
            var devices = (Array)main.GetType().GetProperty("devices", Any).GetValue(main);
            for (var i = 0; i < devices.Length; i++)
            {
                var friendly = FriendlyName(devices.GetValue(i));
                if (string.Equals(friendly, name, StringComparison.OrdinalIgnoreCase) ||
                    friendly.StartsWith(name + " (", StringComparison.OrdinalIgnoreCase))
                    return i;
            }
            return -1;
        }

        private static object Info(object asset) => asset.GetType().GetField("deviceInfo", Any).GetValue(asset);

        private static string FriendlyName(object asset)
        {
            var info = Info(asset);
            return (string)info.GetType().GetField("friendlyName", Any).GetValue(info);
        }

        private static string Describe(object main, int index)
        {
            var asset = ((Array)main.GetType().GetProperty("devices", Any).GetValue(main)).GetValue(index);
            var info = Info(asset);
            var screen = ((Array)info.GetType().GetField("screens", Any).GetValue(info)).GetValue(0);
            var width = screen.GetType().GetField("width", Any).GetValue(screen);
            var height = screen.GetType().GetField("height", Any).GetValue(screen);
            var orientations = (Array)screen.GetType().GetField("orientations", Any).GetValue(screen);
            var safe = orientations.Length > 0
                ? (Rect)orientations.GetValue(0).GetType().GetField("safeArea", Any).GetValue(orientations.GetValue(0))
                : default;
            return index + ": " + FriendlyName(asset) + " " + width + "x" + height +
                   " safe " + safe.x + "," + safe.y + "," + safe.width + "," + safe.height;
        }
    }
}
