using System;
using System.Text;
using System.Threading;
using LightSide;
using PrimeTween;
using UnityEngine;

namespace Horcrux.Runtime.Utilities.ExtensionMethods
{
    public static class UniTextExtensionMethods
    {
        /// <summary>Ghi số nguyên không tạo chuỗi; cùng số với lần trước thì UniText không dựng lại.</summary>
        public static void SetNumber(this UniTextBase self, int value)
        {
            Span<char> digits = stackalloc char[11];
            value.TryFormat(digits, out var written);
            self.SetText((ReadOnlySpan<char>)digits.Slice(0, written));
        }

        /// <summary>
        /// Đếm số từ <paramref name="start"/> tới <paramref name="end"/>; <paramref name="format"/> chứa đúng một
        /// <c>{0}</c> là chỗ đặt số. Mỗi frame ghi lại chữ không cấp phát.
        /// </summary>
        public static Tween CharmCount(this UniTextBase self, int start, int end, string format, float duration,
            float startDelay = 0f, CancellationToken ct = default)
        {
            var slot = format.IndexOf("{0}", StringComparison.Ordinal);
            var builder = new StringBuilder(format.Length + 11);
            return Tween.Custom(0f, 1f, duration, spell, PrimeTween.Ease.Linear, startDelay: startDelay, useUnscaledTime: true)
                .SetCancellationToken(ct);

            void spell(float t)
            {
                Span<char> digits = stackalloc char[11];
                Mathf.RoundToInt(Mathf.Lerp(start, end, t)).TryFormat(digits, out var written);
                builder.Clear().Append(format, 0, slot).Append(digits.Slice(0, written))
                    .Append(format, slot + 3, format.Length - slot - 3);
                self.SetText(builder);
            }
        }
    }
}
