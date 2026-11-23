using System;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Horcrux.Editor.Probe
{
    /// <summary>
    /// Phần chung của script đo chạy trong Play qua playrun.sh: ghi file kết quả và cờ .done ở mọi đường ra, chờ object theo tên,
    /// đọc field và gọi method không public kể cả khai trên base, ghi bước đang làm để lượt bị huỷ nói được nó dừng ở đâu.
    /// </summary>
    public static class PlayProbe
    {
        private const BindingFlags DeclaredInstance =
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

        private static string _step;
        private static float _stepSince;

        public static string DoneFlagPath(string outPath) => outPath + ".done";

        /// <summary>
        /// Chạy body trong đời phiên Play rồi ghi "kết quả + LOG" ra outPath và cờ .done, kể cả khi body ném hay thoát Play.
        /// LOG gom lỗi console và exception của body.
        /// </summary>
        public static void Run(string outPath, Func<StringBuilder, CancellationToken, UniTask> body)
        {
            File.Delete(DoneFlagPath(outPath));
            _step = null;
            RunAsync(outPath, body, Application.exitCancellationToken).Forget();
        }

        /// <summary>
        /// Ghi bước script đang làm. Body ném hay bị huỷ (thoát Play, runner hết giờ) thì LOG ghi bước cuối kèm số giây đã ở đó:
        /// một lượt chờ không bao giờ tới vẫn cho biết nó chờ gì. Vòng chờ tự viết trong script gọi trước vòng.
        /// </summary>
        public static void Step(string what)
        {
            _step = what;
            _stepSince = Time.realtimeSinceStartup;
        }

        /// <summary>Chờ tới khi có GameObject đang active mang đúng tên; kiểm mỗi frame ở Update.</summary>
        public static async UniTask<GameObject> WaitForActiveAsync(string objectName, CancellationToken ct)
        {
            Step("chờ object " + objectName);
            while (true)
            {
                foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                    if (t.name == objectName)
                        return t.gameObject;
                await UniTask.Yield(ct);
            }
        }

        /// <summary>
        /// Chờ tới khi scene mang đúng tên đã nạp xong; kiểm mỗi frame ở Update. Gọi service của game trước khi scene chứa nó nạp
        /// là chạy trước boot: game kẹt ở màn chờ.
        /// </summary>
        public static async UniTask<Scene> WaitForSceneAsync(string sceneName, CancellationToken ct)
        {
            Step("chờ scene " + sceneName);
            while (true)
            {
                var scene = SceneManager.GetSceneByName(sceneName);
                if (scene.isLoaded)
                    return scene;
                await UniTask.Yield(ct);
            }
        }

        /// <summary>
        /// Bấm như ngón tay: pointerDown, pointerUp, pointerClick vào handler gần nhất từ target lên cha. Nút nghe ở down hay up
        /// đều nhận; không đi qua raycast nên object bị che vẫn nhận. Cần EventSystem trong scene.
        /// </summary>
        public static void Click(GameObject target)
        {
            var data = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left };
            ExecuteEvents.ExecuteHierarchy(target, data, ExecuteEvents.pointerDownHandler);
            ExecuteEvents.ExecuteHierarchy(target, data, ExecuteEvents.pointerUpHandler);
            ExecuteEvents.ExecuteHierarchy(target, data, ExecuteEvents.pointerClickHandler);
        }

        /// <summary>Đọc field theo tên, kể cả private khai trên base: GetField(NonPublic) không thấy private của base.</summary>
        public static T Field<T>(object target, string fieldName) => (T)FindField(target.GetType(), fieldName).GetValue(target);

        /// <summary>Gọi method theo tên, kể cả private khai trên base. Trùng tên nhiều overload thì ném AmbiguousMatchException.</summary>
        public static object Call(object target, string methodName, params object[] args) =>
            FindMethod(target.GetType(), methodName).Invoke(target, args);

        private static async UniTaskVoid RunAsync(string outPath, Func<StringBuilder, CancellationToken, UniTask> body, CancellationToken ct)
        {
            var result = new StringBuilder();
            var log = new StringBuilder();
            Application.LogCallback onLog = (message, stackTrace, type) =>
            {
                if (type is LogType.Error or LogType.Exception or LogType.Assert)
                    log.Append('[').Append(type).Append("] ").Append(message).Append('\n').Append(stackTrace).Append('\n');
            };
            Application.logMessageReceived += onLog;
            try
            {
                await body(result, ct);
            }
            catch (Exception e)
            {
                if (_step != null)
                    log.Append("probe: dừng ở bước \"").Append(_step).Append("\" sau ")
                       .Append((Time.realtimeSinceStartup - _stepSince).ToString("F1")).Append(" s\n");
                log.Append("probe: ").Append(e).Append('\n');
            }
            finally
            {
                Application.logMessageReceived -= onLog;
                File.WriteAllText(outPath, result.Append("\nLOG\n").Append(log).ToString());
                File.WriteAllText(DoneFlagPath(outPath), "1");
            }
        }

        private static FieldInfo FindField(Type type, string fieldName)
        {
            for (var t = type; t != null; t = t.BaseType)
            {
                var field = t.GetField(fieldName, DeclaredInstance);
                if (field != null)
                    return field;
            }
            throw new MissingFieldException(type.FullName, fieldName);
        }

        private static MethodInfo FindMethod(Type type, string methodName)
        {
            for (var t = type; t != null; t = t.BaseType)
            {
                var method = t.GetMethod(methodName, DeclaredInstance);
                if (method != null)
                    return method;
            }
            throw new MissingMethodException(type.FullName, methodName);
        }
    }
}
