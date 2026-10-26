using System;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Horcrux.Editor.Probe
{
    /// <summary>
    /// Phần chung của script đo chạy trong Play qua playrun.sh: ghi file kết quả và cờ .done ở mọi đường ra, chờ object theo tên,
    /// đọc field và gọi method không public kể cả khai trên base.
    /// </summary>
    public static class PlayProbe
    {
        private const BindingFlags DeclaredInstance =
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

        public static string DoneFlagPath(string outPath) => outPath + ".done";

        /// <summary>
        /// Chạy body trong đời phiên Play rồi ghi "kết quả + LOG" ra outPath và cờ .done, kể cả khi body ném hay thoát Play.
        /// LOG gom lỗi console và exception của body.
        /// </summary>
        public static void Run(string outPath, Func<StringBuilder, CancellationToken, UniTask> body)
        {
            File.Delete(DoneFlagPath(outPath));
            RunAsync(outPath, body, Application.exitCancellationToken).Forget();
        }

        /// <summary>Chờ tới khi có GameObject đang active mang đúng tên; kiểm mỗi frame ở Update.</summary>
        public static async UniTask<GameObject> WaitForActiveAsync(string objectName, CancellationToken ct)
        {
            while (true)
            {
                foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                    if (t.name == objectName)
                        return t.gameObject;
                await UniTask.Yield(ct);
            }
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
