using System;
using System.IO;
using System.Text;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Horcrux.Editor.FeelTrace
{
    /// <summary>
    /// Quay trạng thái mỗi frame ở PostLateUpdate với fps khoá, để hai lần chạy cùng kịch bản so được từng frame.
    /// Kịch bản cấp record (ghi gì) và act (gõ gì ở frame nào); recorder lo khoá fps, bắt log lỗi, ghi file và cờ .done.
    /// </summary>
    public sealed class FeelTraceRecorder
    {
        private readonly string _outPath;
        private readonly int _frameCount;
        private readonly int _captureFramerate;
        private readonly StringBuilder _trace = new(1 << 22);
        private readonly FeelTraceWriter _writer;

        /// <summary>Ghi chú tự do trên một dòng: chỉ để người đọc.</summary>
        public StringBuilder Notes { get; } = new();

        /// <summary>Khoá so sánh trên một dòng: thứ quyết bố cục ban đầu. Chỉ so hai file cùng chữ ký.</summary>
        public StringBuilder Signature { get; } = new();

        /// <summary>Lỗi console, lỗi của act, và kiểm phụ của kịch bản. Mỗi dòng kết thúc bằng '\n'; không vào TRACE nên không làm lệch diff.</summary>
        public StringBuilder Log { get; } = new();

        public FeelTraceRecorder(string outPath, int frameCount, int captureFramerate = 60)
        {
            _outPath = outPath;
            _frameCount = frameCount;
            _captureFramerate = captureFramerate;
            _writer = new FeelTraceWriter(_trace);
        }

        public static string DoneFlagPath(string outPath) => outPath + ".done";

        /// <summary>
        /// Chờ isReady đúng và ít nhất minIdleFrames frame, rồi mỗi frame: record ghi trạng thái, act gõ kịch bản với chỉ số frame.
        /// Lỗi của act ghi vào LOG và chạy tiếp; lỗi khác hay thoát Play dừng quay nhưng vẫn ghi file và cờ .done.
        /// </summary>
        public void Start(Func<bool> isReady, int minIdleFrames, Action<FeelTraceWriter> record, Action<int> act)
        {
            File.Delete(DoneFlagPath(_outPath));
            RunAsync(isReady, minIdleFrames, record, act, Application.exitCancellationToken).Forget();
        }

        // Token là đời của phiên Play: thoát Play giữa chừng thì vòng dừng ngay, không chạy tiếp trên object đã huỷ ở edit mode.
        private async UniTaskVoid RunAsync(Func<bool> isReady, int minIdleFrames, Action<FeelTraceWriter> record, Action<int> act, CancellationToken ct)
        {
            Application.logMessageReceived += OnLog;
            Time.captureFramerate = _captureFramerate;
            try
            {
                var waited = 0;
                while (!isReady() || waited < minIdleFrames)
                {
                    waited++;
                    await UniTask.Yield(PlayerLoopTiming.PostLateUpdate, ct);
                }

                for (var frame = 0; frame < _frameCount; frame++)
                {
                    _trace.Append('F');
                    FeelTraceWriter.AppendInt(_trace, frame);
                    record(_writer);
                    _trace.Append('\n');
                    try
                    {
                        act(frame);
                    }
                    catch (Exception e)
                    {
                        Log.Append("act frame ").Append(frame).Append(": ").Append(e).Append('\n');
                    }
                    await UniTask.Yield(PlayerLoopTiming.PostLateUpdate, ct);
                }
            }
            catch (Exception e)
            {
                Log.Append("recorder: ").Append(e).Append('\n');
            }
            finally
            {
                Time.captureFramerate = 0;
                Application.logMessageReceived -= OnLog;
                WriteFile();
            }
        }

        // Ghi thẳng từng StringBuilder ra stream: nối chuỗi sẽ chép trace nhiều MB thêm hai lần.
        private void WriteFile()
        {
            using (var file = new StreamWriter(_outPath, false))
            {
                file.Write("NOTES ");
                file.Write(Notes);
                file.Write("\nSIG ");
                file.Write(Signature);
                file.Write("\nLOG\n");
                file.Write(Log);
                file.Write("\nTRACE\n");
                file.Write(_trace);
            }
            File.WriteAllText(DoneFlagPath(_outPath), "1");
        }

        private void OnLog(string message, string stackTrace, LogType type)
        {
            if (type is LogType.Error or LogType.Exception or LogType.Assert)
                Log.Append('[').Append(type).Append("] ").Append(message).Append('\n').Append(stackTrace).Append('\n');
        }
    }
}
