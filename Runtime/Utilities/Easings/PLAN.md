# PLAN.md — Hoàn thiện các họ Easing Curve

Kế hoạch bổ sung 9 họ curve còn thiếu cho `Easer`, bám đúng pattern của
`Curves/QuadEase.cs`. Sau khi thêm file curve, cập nhật `switch` trong
`Easer.Evaluate`.

## Convention bắt buộc (theo QuadEase)

- `namespace Horcrux.Runtime.Tweening.Easing`
- `internal static class XxxEase` — mỗi họ 1 file trong `Curves/`.
- 3 method: `In(float t)`, `Out(float t)`, `InOut(float t)` — `public static float`.
- **Zero-alloc**: số học thuần, không `new`, không LINQ, không closure.
- `t` đã được clamp `[0,1]` bởi `Easer` → **không clamp lại** trong curve.
- Kết quả **có thể vượt `[0,1]`** (Back/Elastic overshoot) → caller dùng `LerpUnclamped`.
- Ưu tiên **nhân trực tiếp** thay vì `Mathf.Pow` cho lũy thừa nguyên (rẻ hơn, tránh overhead).
- **Pre-square** cho lũy thừa ≥4: cache `t² = t*t` rồi `t⁴=(t²)²`, `t⁵=(t²)²·t` — tiết kiệm 1 phép nhân (float mul không kết hợp an toàn → compiler không tự gộp). Cubic (mũ 3) không lợi, giữ `t*t*t`.
- `Mathf.Sin/Cos/Sqrt/Pow` chỉ dùng khi bắt buộc (Sine, Expo, Circ, Elastic).

## Trạng thái

| Họ | File | Trạng thái |
|----|------|-----------|
| Quad | `Curves/QuadEase.cs` | ✅ Đã có |
| Cubic | `Curves/CubicEase.cs` | ⬜ Cần thêm |
| Quart | `Curves/QuartEase.cs` | ⬜ Cần thêm |
| Quint | `Curves/QuintEase.cs` | ⬜ Cần thêm |
| Sine | `Curves/SineEase.cs` | ⬜ Cần thêm |
| Expo | `Curves/ExpoEase.cs` | ⬜ Cần thêm |
| Circ | `Curves/CircEase.cs` | ⬜ Cần thêm |
| Back | `Curves/BackEase.cs` | ⬜ Cần thêm |
| Elastic | `Curves/ElasticEase.cs` | ⬜ Cần thêm |
| Bounce | `Curves/BounceEase.cs` | ⬜ Cần thêm |

---

## 1. CubicEase — lũy thừa 3

```csharp
namespace Horcrux.Runtime.Tweening.Easing
{
    internal static class CubicEase
    {
        public static float In(float t) => t * t * t;

        public static float Out(float t)
        {
            float f = t - 1f;
            return f * f * f + 1f;
        }

        public static float InOut(float t)
        {
            if (t < 0.5f) return 4f * t * t * t;
            float f = -2f * t + 2f;
            return 1f - f * f * f * 0.5f;
        }
    }
}
```

## 2. QuartEase — lũy thừa 4

Pre-square: `t⁴ = (t²)²` → 2 phép nhân thay vì 3 (float mul không kết hợp an toàn
nên compiler không tự gộp; phải tự cache `t²`).

```csharp
namespace Horcrux.Runtime.Tweening.Easing
{
    internal static class QuartEase
    {
        public static float In(float t)
        {
            float t2 = t * t;
            return t2 * t2;
        }

        public static float Out(float t)
        {
            float f = t - 1f;
            float f2 = f * f;
            return 1f - f2 * f2;
        }

        public static float InOut(float t)
        {
            if (t < 0.5f)
            {
                float t2 = t * t;
                return 8f * t2 * t2;
            }
            float f = -2f * t + 2f;
            float f2 = f * f;
            return 1f - f2 * f2 * 0.5f;
        }
    }
}
```

## 3. QuintEase — lũy thừa 5

Pre-square: `t⁵ = (t²)² · t` → 3 phép nhân thay vì 4.

```csharp
namespace Horcrux.Runtime.Tweening.Easing
{
    internal static class QuintEase
    {
        public static float In(float t)
        {
            float t2 = t * t;
            return t2 * t2 * t;
        }

        public static float Out(float t)
        {
            float f = t - 1f;
            float f2 = f * f;
            return 1f + f2 * f2 * f;
        }

        public static float InOut(float t)
        {
            if (t < 0.5f)
            {
                float t2 = t * t;
                return 16f * t2 * t2 * t;
            }
            float f = -2f * t + 2f;
            float f2 = f * f;
            return 1f - f2 * f2 * f * 0.5f;
        }
    }
}
```

## 4. SineEase — lượng giác

Cần `using UnityEngine;` cho `Mathf`.

```csharp
using UnityEngine;

namespace Horcrux.Runtime.Tweening.Easing
{
    internal static class SineEase
    {
        private const float HalfPi = Mathf.PI * 0.5f;

        public static float In(float t) => 1f - Mathf.Cos(t * HalfPi);

        public static float Out(float t) => Mathf.Sin(t * HalfPi);

        public static float InOut(float t) => -(Mathf.Cos(Mathf.PI * t) - 1f) * 0.5f;
    }
}
```

## 5. ExpoEase — lũy thừa cơ số 2

Xử lý biên `t == 0` và `t == 1` để tránh sai số của `Mathf.Pow`.

```csharp
using UnityEngine;

namespace Horcrux.Runtime.Tweening.Easing
{
    internal static class ExpoEase
    {
        public static float In(float t)
            => t <= 0f ? 0f : Mathf.Pow(2f, 10f * t - 10f);

        public static float Out(float t)
            => t >= 1f ? 1f : 1f - Mathf.Pow(2f, -10f * t);

        public static float InOut(float t)
        {
            if (t <= 0f) return 0f;
            if (t >= 1f) return 1f;
            return t < 0.5f
                ? Mathf.Pow(2f, 20f * t - 10f) * 0.5f
                : (2f - Mathf.Pow(2f, -20f * t + 10f)) * 0.5f;
        }
    }
}
```

## 6. CircEase — cung tròn

```csharp
using UnityEngine;

namespace Horcrux.Runtime.Tweening.Easing
{
    internal static class CircEase
    {
        public static float In(float t) => 1f - Mathf.Sqrt(1f - t * t);

        public static float Out(float t)
        {
            float f = t - 1f;
            return Mathf.Sqrt(1f - f * f);
        }

        public static float InOut(float t)
        {
            if (t < 0.5f)
            {
                float x = 2f * t;
                return (1f - Mathf.Sqrt(1f - x * x)) * 0.5f;
            }
            float y = -2f * t + 2f;
            return (Mathf.Sqrt(1f - y * y) + 1f) * 0.5f;
        }
    }
}
```

## 7. BackEase — overshoot (vượt biên rồi quay lại)

Hằng số theo chuẩn Penner. Kết quả có thể `< 0` hoặc `> 1`.

```csharp
namespace Horcrux.Runtime.Tweening.Easing
{
    internal static class BackEase
    {
        private const float C1 = 1.70158f;
        private const float C3 = C1 + 1f;          // 2.70158
        private const float C2 = C1 * 1.525f;      // 2.5949095

        public static float In(float t) => C3 * t * t * t - C1 * t * t;

        public static float Out(float t)
        {
            float f = t - 1f;
            return 1f + C3 * f * f * f + C1 * f * f;
        }

        public static float InOut(float t)
        {
            if (t < 0.5f)
            {
                float x = 2f * t;
                return x * x * ((C2 + 1f) * x - C2) * 0.5f;
            }
            float y = 2f * t - 2f;
            return (y * y * ((C2 + 1f) * y + C2) + 2f) * 0.5f;
        }
    }
}
```

## 8. ElasticEase — dao động đàn hồi

Hằng số `c4 = 2π/3`, `c5 = 2π/4.5`. Kết quả overshoot mạnh.

```csharp
using UnityEngine;

namespace Horcrux.Runtime.Tweening.Easing
{
    internal static class ElasticEase
    {
        private const float C4 = 2f * Mathf.PI / 3f;
        private const float C5 = 2f * Mathf.PI / 4.5f;

        public static float In(float t)
        {
            if (t <= 0f) return 0f;
            if (t >= 1f) return 1f;
            return -Mathf.Pow(2f, 10f * t - 10f) * Mathf.Sin((10f * t - 10.75f) * C4);
        }

        public static float Out(float t)
        {
            if (t <= 0f) return 0f;
            if (t >= 1f) return 1f;
            return Mathf.Pow(2f, -10f * t) * Mathf.Sin((10f * t - 0.75f) * C4) + 1f;
        }

        public static float InOut(float t)
        {
            if (t <= 0f) return 0f;
            if (t >= 1f) return 1f;
            return t < 0.5f
                ? -(Mathf.Pow(2f, 20f * t - 10f) * Mathf.Sin((20f * t - 11.125f) * C5)) * 0.5f
                : Mathf.Pow(2f, -20f * t + 10f) * Mathf.Sin((20f * t - 11.125f) * C5) * 0.5f + 1f;
        }
    }
}
```

## 9. BounceEase — nảy

`Out` là hàm gốc; `In`/`InOut` suy ra từ `Out` (đối xứng).

```csharp
namespace Horcrux.Runtime.Tweening.Easing
{
    internal static class BounceEase
    {
        private const float N1 = 7.5625f;
        private const float D1 = 2.75f;

        public static float In(float t) => 1f - Out(1f - t);

        public static float Out(float t)
        {
            if (t < 1f / D1)
            {
                return N1 * t * t;
            }
            if (t < 2f / D1)
            {
                t -= 1.5f / D1;
                return N1 * t * t + 0.75f;
            }
            if (t < 2.5f / D1)
            {
                t -= 2.25f / D1;
                return N1 * t * t + 0.9375f;
            }
            t -= 2.625f / D1;
            return N1 * t * t + 0.984375f;
        }

        public static float InOut(float t)
            => t < 0.5f
                ? (1f - Out(1f - 2f * t)) * 0.5f
                : (1f + Out(2f * t - 1f)) * 0.5f;
    }
}
```

---

## Cập nhật `Easer.Evaluate`

Thêm các case còn thiếu vào `switch`, giữ nguyên clamp `[0,1]` ở đầu hàm.
Giữ arm fallback `_ => t` để an toàn.

```csharp
return easeType switch
{
    EaseType.Linear => t,

    EaseType.InQuad => QuadEase.In(t),
    EaseType.OutQuad => QuadEase.Out(t),
    EaseType.InOutQuad => QuadEase.InOut(t),

    EaseType.InCubic => CubicEase.In(t),
    EaseType.OutCubic => CubicEase.Out(t),
    EaseType.InOutCubic => CubicEase.InOut(t),

    EaseType.InQuart => QuartEase.In(t),
    EaseType.OutQuart => QuartEase.Out(t),
    EaseType.InOutQuart => QuartEase.InOut(t),

    EaseType.InQuint => QuintEase.In(t),
    EaseType.OutQuint => QuintEase.Out(t),
    EaseType.InOutQuint => QuintEase.InOut(t),

    EaseType.InSine => SineEase.In(t),
    EaseType.OutSine => SineEase.Out(t),
    EaseType.InOutSine => SineEase.InOut(t),

    EaseType.InExpo => ExpoEase.In(t),
    EaseType.OutExpo => ExpoEase.Out(t),
    EaseType.InOutExpo => ExpoEase.InOut(t),

    EaseType.InCirc => CircEase.In(t),
    EaseType.OutCirc => CircEase.Out(t),
    EaseType.InOutCirc => CircEase.InOut(t),

    EaseType.InBack => BackEase.In(t),
    EaseType.OutBack => BackEase.Out(t),
    EaseType.InOutBack => BackEase.InOut(t),

    EaseType.InElastic => ElasticEase.In(t),
    EaseType.OutElastic => ElasticEase.Out(t),
    EaseType.InOutElastic => ElasticEase.InOut(t),

    EaseType.InBounce => BounceEase.In(t),
    EaseType.OutBounce => BounceEase.Out(t),
    EaseType.InOutBounce => BounceEase.InOut(t),

    _ => t
};
```

## Checklist triển khai

- [ ] Tạo 9 file curve trong `Curves/` theo công thức trên.
- [ ] Bổ sung 27 case vào `switch` của `Easer`.
- [ ] Xác nhận biên: `In(0)=0`, `Out(1)=1`, `InOut(0)=0`, `InOut(1)=1` cho mọi họ
      (Back/Elastic có thể overshoot giữa quãng nhưng vẫn về đúng biên).
- [ ] Kiểm tra liên tục tại `t = 0.5` cho các hàm `InOut`.
```
