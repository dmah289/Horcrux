using System;
using System.Globalization;
using System.Text;
using UnityEngine;

namespace Horcrux.Editor.FeelTrace
{
    /// <summary>
    /// Ghi trạng thái một frame thành token: " |khoá" mở một entity, mỗi giá trị là một token sau một dấu cách.
    /// Số ghi F5 culture bất biến; vector ghi "x,y,z" thành một token.
    /// </summary>
    public sealed class FeelTraceWriter
    {
        private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

        // Đủ cho float F5 lớn nhất (-3.4e38 → 39 chữ số + dấu + phần thập phân) và mọi int.
        private const int NumberBufferLength = 64;

        private readonly StringBuilder _sb;

        public FeelTraceWriter(StringBuilder sb) => _sb = sb;

        /// <summary>Entity có khoá ổn định giữa các lần chạy, ví dụ chỉ số trong một danh sách cố định.</summary>
        public FeelTraceWriter Entity(string kind, int index)
        {
            _sb.Append(" |").Append(kind);
            AppendInt(_sb, index);
            return this;
        }

        /// <summary>Entity không có khoá ổn định, ví dụ ID ngẫu nhiên mỗi phiên: feeltrace.py đánh số theo thứ tự ghi trong frame.</summary>
        public FeelTraceWriter EntityByOrder(string kind)
        {
            _sb.Append(" |").Append(kind).Append('?');
            return this;
        }

        public FeelTraceWriter Value(float value)
        {
            _sb.Append(' ');
            AppendFloat(value);
            return this;
        }

        public FeelTraceWriter Value(Vector3 value)
        {
            _sb.Append(' ');
            AppendFloat(value.x);
            _sb.Append(',');
            AppendFloat(value.y);
            _sb.Append(',');
            AppendFloat(value.z);
            return this;
        }

        public FeelTraceWriter Value(int value)
        {
            _sb.Append(' ');
            AppendInt(_sb, value);
            return this;
        }

        public FeelTraceWriter Value(bool value) => Value(value ? 1 : 0);

        /// <summary>Token chữ, so khớp tuyệt đối. Không chứa dấu cách hay '|'.</summary>
        public FeelTraceWriter Value(string value)
        {
            _sb.Append(' ').Append(value);
            return this;
        }

        /// <summary>Ghi số nguyên không qua chuỗi trung gian: recorder gọi mỗi frame cho chỉ số frame.</summary>
        internal static void AppendInt(StringBuilder sb, int value)
        {
            Span<char> buffer = stackalloc char[NumberBufferLength];
            value.TryFormat(buffer, out var written, default, Invariant);
            sb.Append(buffer.Slice(0, written));
        }

        // Vài trăm số mỗi frame: ToString sẽ cấp phát một chuỗi cho mỗi số.
        private void AppendFloat(float value)
        {
            Span<char> buffer = stackalloc char[NumberBufferLength];
            value.TryFormat(buffer, out var written, "F5", Invariant);
            _sb.Append(buffer.Slice(0, written));
        }
    }
}
