# Plan — `SpringDamper`: lò xo–giảm chấn độc lập framerate lên Horcrux

Plan gốc Horcrux, không bản tạm; dự án chưa có call site.
**Phụ thuộc:** không — gõ lúc nào cũng được.
Kiểm: `bash "<horcrux>/Editor/CompileCheck/Tools~/verify.sh" --csproj com.horcrux.runtime.csproj,Horcrux.EditModeTests.csproj --tests Horcrux.EditModeTests`.

## Kịch bản

| Tình huống | Hệ làm gì | Caller thấy |
|---|---|---|
| chủ tạo `SpringConfig.FromFrequency(f, ζ)` một lần, `Solve(ref state, target, cfg, dt)` mỗi frame | tiến `(Position, Velocity)` đúng `dt` giây bằng nghiệm đóng | giá trị đuổi theo đích, có đà |
| `ζ < 1` | nhánh cản yếu | vượt đích, nảy lui, biên độ tắt dần |
| `abs(ζ − 1) ≤ 1e-4` | nhánh tới hạn | về đích nhanh nhất, không vượt |
| `ζ > 1` | nhánh cản mạnh | bò về, không vượt |
| đổi `target` giữa chừng | truyền số khác, vận tốc giữ | chuyển động không gãy, không gọi API nào khác |
| lag spike (`dt = 2`) · framerate khác nhau | nghiệm đóng đúng mọi `dt` | đúng điểm trên đường cong, không `NaN`; 1 bước `T` = `n` bước `T/n` |
| nhiều vật, `dt` nhỏ đều: `SpringMethod.SemiImplicit` | Euler bán ẩn, không `exp/sincos` | rẻ hơn, xấp xỉ, phụ thuộc framerate; nổ khi `ω₀·dt` lớn |
| `Vector2`, `Vector3` | gọi lõi scalar từng trục, cùng config | mỗi trục bằng đúng kết quả scalar |
| `dt ≤ 0` | trả nguyên | đứng yên |
| `frequency ≤ 0` · `default(SpringConfig)` | `IsActive` false | đứng yên, không `NaN` |
| `ζ < 0` | clamp 0 | lắc mãi như `ζ = 0` |
| `ζ` lớn **và** `dt` lớn (`s·dt > 88`, ví dụ `f = 10, ζ = 10, dt = 2`) | `cosh` tràn `float` | `NaN` — chưa xử lý, xem khối cuối |

## Flow

```
chủ (vòng lặp của dự án: Update / LateUpdate / FixedUpdate)
 │ FromFrequency(f, ζ) một lần   │ Solve(ref state, target, cfg, dt[, method]) mỗi frame       │ đọc state.Position
 ▼                               ▼                                                             │
SpringConfig ◄────── in ────── FloatSpring · Vector2Spring · Vector3Spring ◄───────────────────┘
SpringConfig.cs                FloatSpring.cs · Vector2Spring.cs · Vector3Spring.cs
Omega0, ZetaOmega, OmegaD,            │ từng trục
Mode, IsActive                        ▼
                               SpringSolver.Solve(pos, vel, target, cfg, method, dt)           SpringSolver.cs
                                 ├── SolveAnalytic      nghiệm đóng ba chế độ
                                 └── SolveSemiImplicit  Euler bán ẩn
(mọi file trong Runtime/Utilities/PhysXHelper/Spring/)
```

## Thuật toán

`m·ẍ = −k(x − target) − c·ẋ`, đổi biến `y = x − target` (đích hằng trong một bước) → dạng chuẩn `ÿ + 2ζω₀ẏ + ω₀²y = 0`, với `ω₀ = 2πf = √(k/m)`, `ζ = c / (2√(km))`. Nghiệm phương trình đặc trưng `r = −ζω₀ ± ω₀√(ζ² − 1)`; dấu `ζ² − 1` chia ba chế độ. Chung `E = e^(−ζω₀·Δt)`.

| Chế độ | Đại lượng phụ | `y(Δt)` | `v(Δt)` |
|---|---|---|---|
| cản yếu `ζ < 1` | `ω_d = ω₀√(1 − ζ²)` · `B = (v₀ + ζω₀y₀)/ω_d` · `C, S = cos, sin(ω_dΔt)` | `E(y₀C + BS)` | `E[(−ζω₀y₀ + Bω_d)C − (ζω₀B + y₀ω_d)S]` |
| tới hạn `ζ = 1` | `coeff = v₀ + ω₀y₀` · `E = e^(−ω₀Δt)` | `E(y₀ + coeff·Δt)` | `E(v₀ − ω₀·coeff·Δt)` |
| cản mạnh `ζ > 1` | `s = ω₀√(ζ² − 1)` · `B = (v₀ + ζω₀y₀)/s` · `Ch, Sh = cosh, sinh(sΔt)` | `E(y₀Ch + BSh)` | `E[(−ζω₀y₀ + Bs)Ch + (y₀s − ζω₀B)Sh]` |
| Euler bán ẩn | `a = −ω₀²y − 2ζω₀v` | `y + v_new·Δt` | `v_new = v + a·Δt` |

| Bất biến | Từ đâu |
|---|---|
| `Δt = 0` → `(y₀, v₀)`; `Δt → ∞`, `ζ > 0` → `(0, 0)` | thế vào từng hộp; `E → 0` |
| độc lập framerate, ổn định vô điều kiện | `E(a)·E(b) = E(a + b)`; mỗi nghiệm = hàm bị chặn hoặc tăng chậm hơn bao × `E` giảm, không lặp tích luỹ |
| cản mạnh không nổ | `ζω₀ > s` (vì `ζ² > ζ² − 1`): bao co nhanh hơn hyperbolic giãn; giới hạn số học `s·Δt > 88` |
| `ζ = 0` | `y = y₀cos(ω₀t) + (v₀/ω₀)sin(ω₀t)` = `HarmonicOscillator` |
| `ζ < 1`, `v₀ = −ζω₀y₀` (`B = 0`) | `y = y₀·E·cos(ω_d t)` = `DampedOscillator.GetDisplacement(Cos, ω_d/2π, ζω₀, t, y₀)` |
| Euler bán ẩn (`v` trước, vị trí dùng `v` mới) | gần bảo toàn năng lượng; phân kỳ khi `ω₀·Δt` lớn, an toàn cỡ vài phần mười |
| vector | mỗi trục phương trình riêng, không số hạng trộn trục, chung `ω₀, ζ` |
| chi phí mỗi frame | một `exp` và một cặp `sin/cos` (`cosh/sinh`) mỗi trục; hệ số tính lúc tạo config; toàn `struct`, state `ref`, config `in`: zero-GC |

---

## GĐ1 — xong thì một giá trị float đuổi theo đích, có đà, nảy rồi dừng, đúng với mọi `dt`

```
  chủ ──FromFrequency+──► SpringConfig+ ──in──► FloatSpring.Solve+(ref state+, target, cfg, dt) ──► SpringSolver.Solve+ ──► SolveAnalytic+ (cản yếu)
                                                     ▲ đọc Position
```

`Runtime/Utilities/PhysXHelper/Spring/FloatSpring.cs` — file mới (call site):

```csharp
using System.Runtime.CompilerServices;

namespace Horcrux.Runtime.Utilities.PhysXHelper
{
    /// <summary>Vị trí và vận tốc của một giá trị đang đuổi theo đích. Chủ giữ struct này, truyền ref mỗi frame.</summary>
    public struct FloatSpringState
    {
        public float Position;
        public float Velocity;

        public FloatSpringState(float position, float velocity = 0f)
        {
            Position = position;
            Velocity = velocity;
        }
    }

    public static class FloatSpring
    {
        /// <summary>
        /// Tiến state về target đúng dt giây, ghi tại chỗ. target là tham số, không nằm trong state: đổi đích giữa chừng
        /// chỉ là truyền số khác, vận tốc giữ nguyên nên chuyển động không gãy.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Solve(ref FloatSpringState s, float target, in SpringConfig cfg, float dt)
        {
            (s.Position, s.Velocity) = SpringSolver.Solve(s.Position, s.Velocity, target, cfg, dt);
        }
    }
}
```

`Runtime/Utilities/PhysXHelper/Spring/SpringSolver.cs` — file mới:

```csharp
using Unity.Mathematics;

namespace Horcrux.Runtime.Utilities.PhysXHelper
{
    /// <summary>Lõi giải một chiều: (pos, vel) sau dt giây khi đuổi theo target. Vector gọi vào đây từng trục.</summary>
    public static class SpringSolver
    {
        /// <summary>Giải ÿ + 2ζω₀ẏ + ω₀²y = 0 với y = pos − target, rồi đổi về pos.</summary>
        // Tuple thay out: value trên stack, một dòng gán ở call site.
        public static (float pos, float vel) Solve(float pos, float vel, float target, in SpringConfig cfg, float dt)
        {
            if (!cfg.IsActive || dt <= 0f) return (pos, vel);

            float y = pos - target;
            (float ny, float nv) = SolveAnalytic(y, vel, cfg, dt);
            return (ny + target, nv);
        }

        /// <summary>Nghiệm đóng cản yếu: đúng với mọi dt, không tích luỹ sai số.</summary>
        internal static (float, float) SolveAnalytic(float y0, float v0, in SpringConfig cfg, float dt)
        {
            float zw = cfg.ZetaOmega;
            float wd = cfg.OmegaD;
            float e = math.exp(-zw * dt);          // E = e^(−ζω₀·dt), bao hình
            float c = math.cos(wd * dt);
            float s = math.sin(wd * dt);
            float b = (v0 + zw * y0) / wd;         // B = (v₀ + ζω₀y₀) / ω_d

            float y = e * (y0 * c + b * s);
            float v = e * ((-zw * y0 + b * wd) * c - (zw * b + y0 * wd) * s);
            return (y, v);
        }
    }
}
```

`Runtime/Utilities/PhysXHelper/Spring/SpringConfig.cs` — file mới:

```csharp
using UnityEngine;

namespace Horcrux.Runtime.Utilities.PhysXHelper
{
    /// <summary>
    /// Núm của lò xo (tần số f Hz, tỉ số giảm chấn ζ) đã đổi sang hệ số mà nghiệm đóng dùng mỗi frame. Tạo một lần qua
    /// factory, dùng chung cho mọi state cùng cảm giác; không đổi sau khi tạo. default là lò xo tắt.
    /// </summary>
    public readonly struct SpringConfig
    {
        /// <summary>ζω₀ — tốc độ co của bao hình e^(−ζω₀·dt).</summary>
        public readonly float ZetaOmega;

        /// <summary>ω_d = ω₀√(1−ζ²) — tần số dao động thật, nhỏ hơn ω₀ vì bị cản.</summary>
        public readonly float OmegaD;

        /// <summary>false khi frequency ≤ 0: lò xo tắt, Solve trả nguyên state.</summary>
        public readonly bool IsActive;

        // ζ sát 1 thì ω_d sát 0 và B = (…)/ω_d nổ. Tạm chặn ζ dưới dải này; GĐ2 thay bằng nhánh tới hạn.
        private const float CriticalEpsilon = 1e-4f;

        private SpringConfig(float zetaOmega, float omegaD, bool isActive)
        {
            ZetaOmega = zetaOmega;
            OmegaD = omegaD;
            IsActive = isActive;
        }

        /// <param name="frequency">f (Hz): số lần lắc mỗi giây khi không cản — cảm giác cứng, nhanh. ≤ 0 → tắt.</param>
        /// <param name="dampingRatio">ζ: 0 lắc mãi, càng gần 1 càng ít nảy. Âm là bơm năng lượng → clamp 0.</param>
        public static SpringConfig FromFrequency(float frequency, float dampingRatio)
        {
            // Trả default: field chưa gán cũng là lò xo tắt, không NaN; hai đường "tắt" là một.
            if (frequency <= 0f) return default;                                    // IsActive false

            float omega0 = 2f * Mathf.PI * frequency;                               // ω₀ = 2πf
            float zeta = Mathf.Clamp(dampingRatio, 0f, 1f - CriticalEpsilon);       // tạm: GĐ2 mở ζ ≥ 1
            float omegaD = omega0 * Mathf.Sqrt(1f - zeta * zeta);                   // ω_d
            return new SpringConfig(zeta * omega0, omegaD, true);
        }
    }
}
```

`Tests/EditMode/SpringDamperTests.cs` (agent) — `namespace Horcrux.Tests`; hằng `f = 2`, `ζ = 0.3` trừ khi ghi khác; `ω₀ = 12.566`:

| Case | Input | Kỳ vọng |
|---|---|---|
| frame đầu, đã ở đích | state `(10, 0)`, target 10 | state không đổi |
| `dt = 0` · `dt < 0` | bất kỳ | state nguyên |
| tắt | `FromFrequency(0, 0.5)` · `default(SpringConfig)` | `IsActive` false; `Solve` trả nguyên |
| `ζ` âm | `FromFrequency(2, −1)` | `ZetaOmega` 0, `OmegaD` = `ω₀` — như `ζ = 0` |
| hệ số | `FromFrequency(2, 0.5)` | `ZetaOmega` ≈ 6.283, `OmegaD` ≈ 10.883 (= `ω₀√0.75`) |
| hội tụ | `(0, 0)` → 10, 300 bước `dt = 1/60` | `Position` ≈ 10 (±1e-3), `Velocity` ≈ 0 |
| nảy | như trên, ghi `Position` từng bước | vượt 10 ít nhất một lần; đỉnh vượt sau nhỏ hơn đỉnh trước |
| đổi đích giữa chừng | 60 bước về 10 rồi target 0, 300 bước | không gọi API nào khác; `Velocity` ngay trước lần `Solve` đầu sau đổi đích bằng `Velocity` bước trước; `Position` ≈ 0 |
| `ζ = 0` khớp `HarmonicOscillator` | `(1, 0)` → 0, `ζ = 0`, một bước `dt = t`, `t ∈ {0.1, 0.37, 1}` | `Position` = `HarmonicOscillator.GetHarmonicDisplacement(WaveStyle.Cos, 2, t)` ±1e-4; biên độ không co |
| cản yếu khớp `DampedOscillator` | `y₀ = 1`, `v₀ = −ZetaOmega`, target 0, một bước `dt = t` | `Position` = `DampedOscillator.GetDisplacement(Cos, OmegaD/2π, ZetaOmega, t, 1)` ±1e-4; `Velocity` = `GetVelocity(...)` ±1e-3 |
| độc lập framerate | `(0, 0)` → 10: 1 bước `dt = 1` so 100 bước `dt = 0.01` | `Position` lệch < 1e-3 |
| lag spike | `(0, 0)` → 10, một bước `dt = 2` | ≈ 10 (±1e-2), `Velocity` ≈ 0, không `NaN` |
| đạo hàm số | `v(t)` so `(y(t+h) − y(t−h))/2h`, `h = 1e-3`, mỗi vế một bước từ `(1, 0)` | lệch tương đối < 1e-2 |

---

## GĐ2 — xong thì designer vặn `ζ = 1` được về đích nhanh nhất không vượt, `ζ > 1` được chuyển động ì

```
  chủ ──FromFrequency(f, ζ bất kỳ ≥ 0)+──► SpringConfig (Mode+, Omega0+) ──► FloatSpring.Solve ──► SpringSolver.Solve ──► SolveAnalytic
                                                                                                          ├── UnderDamped
                                                                                                          ├── OverDamped+
                                                                                                          └── CriticallyDamped+
```

`SpringConfig.cs` › thêm trước `struct SpringConfig`:

```csharp
    /// <summary>Theo ζ: dưới 1 nảy, bằng 1 về nhanh nhất không nảy, trên 1 bò về.</summary>
    public enum SpringMode { UnderDamped, CriticallyDamped, OverDamped }
```

`SpringConfig.cs` › field — thêm `Omega0` trước `ZetaOmega`, thay doc `OmegaD`, thêm `Mode` sau `OmegaD`:

```csharp
        /// <summary>ω₀ = 2πf — tần số góc tự nhiên.</summary>
        public readonly float Omega0;

        /// <summary>ζω₀ — tốc độ co của bao hình e^(−ζω₀·dt).</summary>
        public readonly float ZetaOmega;

        /// <summary>Under: ω_d = ω₀√(1−ζ²) · Over: s = ω₀√(ζ²−1) · Critical: 0.</summary>
        public readonly float OmegaD;

        public readonly SpringMode Mode;
```

`SpringConfig.cs` › `CriticalEpsilon`, constructor — cũ → mới:

```csharp
        // ζ sát 1 thì ω_d sát 0 và B = (…)/ω_d nổ. Tạm chặn ζ dưới dải này; GĐ2 thay bằng nhánh tới hạn.
        private const float CriticalEpsilon = 1e-4f;

        private SpringConfig(float zetaOmega, float omegaD, bool isActive)
        {
            ZetaOmega = zetaOmega;
            OmegaD = omegaD;
            IsActive = isActive;
        }
```

```csharp
        // Dải coi ζ = 1: hai nhánh kia chia cho ω_d hay s, sát 1 thì sát 0.
        private const float CriticalEpsilon = 1e-4f;

        private SpringConfig(float omega0, float zetaOmega, float omegaD, SpringMode mode, bool isActive)
        {
            Omega0 = omega0;
            ZetaOmega = zetaOmega;
            OmegaD = omegaD;
            Mode = mode;
            IsActive = isActive;
        }
```

`SpringConfig.cs` › `FromFrequency` — cũ → mới, ba nhánh theo dấu `ζ² − 1`:

```csharp
            float omega0 = 2f * Mathf.PI * frequency;                               // ω₀ = 2πf
            float zeta = Mathf.Clamp(dampingRatio, 0f, 1f - CriticalEpsilon);       // tạm: GĐ2 mở ζ ≥ 1
            float omegaD = omega0 * Mathf.Sqrt(1f - zeta * zeta);                   // ω_d
            return new SpringConfig(zeta * omega0, omegaD, true);
```

```csharp
            float omega0 = 2f * Mathf.PI * frequency;                               // ω₀ = 2πf
            float zeta = Mathf.Max(dampingRatio, 0f);                               // ζ < 0 bơm năng lượng

            if (zeta < 1f - CriticalEpsilon)
                return new SpringConfig(omega0, zeta * omega0, omega0 * Mathf.Sqrt(1f - zeta * zeta),
                    SpringMode.UnderDamped, true);

            if (zeta > 1f + CriticalEpsilon)
                return new SpringConfig(omega0, zeta * omega0, omega0 * Mathf.Sqrt(zeta * zeta - 1f),
                    SpringMode.OverDamped, true);

            // Dải tới hạn: ζ coi là 1 nên ζω₀ = ω₀; bao hình e^(−ζω₀·dt) dùng chung đúng là e^(−ω₀·dt) của nhánh này.
            return new SpringConfig(omega0, omega0, 0f, SpringMode.CriticallyDamped, true);
```

`SpringSolver.cs` › `SolveAnalytic` — thay cả thân GĐ1; nhánh cản yếu giữ nguyên từng dòng thành `case` đầu:

```csharp
        /// <summary>Nghiệm đóng ba chế độ: đúng với mọi dt, không tích luỹ sai số.</summary>
        internal static (float, float) SolveAnalytic(float y0, float v0, in SpringConfig cfg, float dt)
        {
            float zw = cfg.ZetaOmega;
            float e = math.exp(-zw * dt);          // E = e^(−ζω₀·dt), bao hình chung

            switch (cfg.Mode)
            {
                case SpringMode.UnderDamped:
                {
                    float wd = cfg.OmegaD;             // ω_d
                    float c = math.cos(wd * dt);
                    float s = math.sin(wd * dt);
                    float b = (v0 + zw * y0) / wd;     // B = (v₀ + ζω₀y₀) / ω_d

                    float y = e * (y0 * c + b * s);
                    float v = e * ((-zw * y0 + b * wd) * c - (zw * b + y0 * wd) * s);
                    return (y, v);
                }
                case SpringMode.OverDamped:
                {
                    float sc = cfg.OmegaD;             // s = ω₀√(ζ²−1)
                    // cosh/sinh thay hai mũ: không trừ hai số lớn khi ζ lớn; tràn float khi s·dt > 88.
                    float ch = math.cosh(sc * dt);
                    float sh = math.sinh(sc * dt);
                    float b = (v0 + zw * y0) / sc;     // B = (v₀ + ζω₀y₀) / s

                    float y = e * (y0 * ch + b * sh);
                    float v = e * ((-zw * y0 + b * sc) * ch + (y0 * sc - zw * b) * sh);
                    return (y, v);
                }
                default:                               // CriticallyDamped, E = e^(−ω₀·dt) vì ZetaOmega = Omega0
                {
                    float w0 = cfg.Omega0;
                    float coeff = v0 + w0 * y0;        // B = v₀ + ω₀y₀
                    float y = e * (y0 + coeff * dt);
                    float v = e * (v0 - w0 * coeff * dt);
                    return (y, v);
                }
            }
        }
```

`Tests/EditMode/SpringDamperTests.cs` (agent):

| Case | Input | Kỳ vọng |
|---|---|---|
| test GĐ1 | không sửa | xanh — nhánh cản yếu không đổi |
| chọn chế độ | `ζ ∈ {1, 1 − 5e-5, 1 + 5e-5}` | `Mode` Critically, `OmegaD` 0, `ZetaOmega` = `Omega0` |
| sát ranh | `ζ = 1 − 2e-4` · `ζ = 1 + 2e-4` | Under · Over |
| hệ số over | `FromFrequency(2, 2)` | `Mode` Over, `OmegaD` ≈ 21.766 (= `ω₀√3`) |
| tới hạn đơn điệu | `(0, 0)` → 10, `ζ = 1`, 300 bước `dt = 1/60`, ghi `Position` | không bước nào > 10; không giảm; cuối ≈ 10 |
| cản mạnh chậm hơn | `ζ = 2` so `ζ = 1`, cùng `(0, 0)` → 10, tại `t = 0.5` | `pos(ζ=2) < pos(ζ=1) < 10`; `ζ = 2` không vượt |
| liên tục qua ranh | `ζ = 1 − 2e-4` so `ζ = 1`, `(1, 0)` → 0, một bước `dt = 0.1` | `Position` lệch < 1e-3 |
| `dt = 0` | mỗi chế độ, `(1, 3)` | `(1, 3)` |
| độc lập framerate | over và critical: 1 bước `dt = 1` so 100 bước `dt = 0.01` | lệch < 1e-3 |
| lag spike over | `ζ = 2`, `(0, 0)` → 10, một bước `dt = 2` (`s·dt` ≈ 43.5) | ≈ 10, không `NaN` |
| đạo hàm số | over và critical, `h = 1e-3` từ `(1, 0)` | lệch tương đối < 1e-2 |

---

## GĐ3 — xong thì chủ có nhiều vật và `dt` cố định đổi được sang bước rẻ không `exp/sincos`

```
  chủ ──Solve(ref state, target, cfg, dt[, method+])──► FloatSpring ──► SpringSolver.Solve(…, method+, dt)
                                                                              ├── Analytic     → SolveAnalytic
                                                                              └── SemiImplicit → SolveSemiImplicit+
```

`SpringSolver.cs` › thêm trước `class SpringSolver`:

```csharp
    /// <summary>Analytic: nghiệm đóng, đúng mọi dt. SemiImplicit: Euler bán ẩn, rẻ hơn, chỉ khi dt nhỏ và đều.</summary>
    public enum SpringMethod { Analytic, SemiImplicit }
```

`SpringSolver.cs` › `Solve` — cũ → mới:

```csharp
        public static (float pos, float vel) Solve(float pos, float vel, float target, in SpringConfig cfg, float dt)
        {
            if (!cfg.IsActive || dt <= 0f) return (pos, vel);

            float y = pos - target;
            (float ny, float nv) = SolveAnalytic(y, vel, cfg, dt);
            return (ny + target, nv);
        }
```

```csharp
        public static (float pos, float vel) Solve(float pos, float vel, float target,
            in SpringConfig cfg, SpringMethod method, float dt)
        {
            if (!cfg.IsActive || dt <= 0f) return (pos, vel);

            float y = pos - target;
            (float ny, float nv) = method == SpringMethod.Analytic
                ? SolveAnalytic(y, vel, cfg, dt)
                : SolveSemiImplicit(y, vel, cfg, dt);
            return (ny + target, nv);
        }
```

`SpringSolver.cs` › thêm sau `SolveAnalytic`:

```csharp
        /// <summary>Euler bán ẩn: vận tốc trước, vị trí dùng vận tốc mới. Phân kỳ khi ω₀·dt lớn.</summary>
        internal static (float, float) SolveSemiImplicit(float y, float v, in SpringConfig cfg, float dt)
        {
            // Đọc ZetaOmega, không cần ζ thô: không nhánh nào đọc ζ nên config không có field Zeta.
            float a = -cfg.Omega0 * cfg.Omega0 * y - 2f * cfg.ZetaOmega * v;   // a = −ω₀²y − 2ζω₀v
            v += a * dt;
            y += v * dt;
            return (y, v);
        }
```

`FloatSpring.cs` › `Solve` — cũ → mới:

```csharp
        public static void Solve(ref FloatSpringState s, float target, in SpringConfig cfg, float dt)
        {
            (s.Position, s.Velocity) = SpringSolver.Solve(s.Position, s.Velocity, target, cfg, dt);
        }
```

```csharp
        // Mặc định Analytic: chất lượng trước, rẻ là opt-in có ý thức; call site cũ không đổi.
        public static void Solve(ref FloatSpringState s, float target, in SpringConfig cfg, float dt,
            SpringMethod method = SpringMethod.Analytic)
        {
            (s.Position, s.Velocity) = SpringSolver.Solve(s.Position, s.Velocity, target, cfg, method, dt);
        }
```

`Tests/EditMode/SpringDamperTests.cs` (agent):

| Case | Input | Kỳ vọng |
|---|---|---|
| test GĐ1–2 | không sửa | xanh — mặc định `Analytic` |
| mặc định | `FloatSpring.Solve(ref s, 10, cfg, dt)` không truyền `method` | trùng bit với `Analytic` tường minh |
| Euler hội tụ | `SemiImplicit`, `(0, 0)` → 10, `f = 2`, `ζ = 0.3`, 300 bước `dt = 1/60` (`ω₀·dt` = 0.21) | ≈ 10 (±0.05), `Velocity` ≈ 0 |
| Euler là xấp xỉ | Euler so Analytic tại `t = 1`, `dt = 1/60` | lệch > 0 và < 0.2 |
| sai số giảm theo `dt` | Euler `dt = 1/600` so `dt = 1/60`, cùng `t = 1`, so Analytic | lệch của `1/600` nhỏ hơn lệch của `1/60` |
| Euler phụ thuộc framerate | 1 bước `dt = 1` so 100 bước `dt = 0.01` | lệch > 1 |
| Euler nổ | `dt = 0.5` (`ω₀·dt` = 6.3), 20 bước từ `(0, 0)` → 10 | `abs(Position − 10)` tăng qua các bước, không hội tụ |
| guard | `SemiImplicit` với `dt = 0` · config tắt | state nguyên |
| Euler ba chế độ | `ζ ∈ {0.3, 1, 2}`, `dt = 1/60`, 300 bước | đều ≈ 10 (±0.05) |

---

## GĐ4 — xong thì camera, vị trí vật, scale ba trục đuổi theo đích bằng một lời gọi

```
  chủ ──Solve(ref Vector3SpringState+, Vector3 target, cfg, dt[, method])──► Vector3Spring+ ──x──► SpringSolver.Solve
                                                                                             ──y──►
                                                                                             ──z──►
  (Vector2Spring+ hai trục, cùng hình)
```

`Runtime/Utilities/PhysXHelper/Spring/Vector3Spring.cs` — file mới:

```csharp
using UnityEngine;

namespace Horcrux.Runtime.Utilities.PhysXHelper
{
    public struct Vector3SpringState
    {
        public Vector3 Position;
        public Vector3 Velocity;

        public Vector3SpringState(Vector3 position, Vector3 velocity = default)
        {
            Position = position;
            Velocity = velocity;
        }
    }

    public static class Vector3Spring
    {
        /// <summary>Ba trục độc lập, cùng config: lò xo tuyến tính tách biến nên không có toán vector.</summary>
        // Không AggressiveInlining: thân ba lời gọi, không còn là wrapper mỏng.
        public static void Solve(ref Vector3SpringState s, Vector3 target, in SpringConfig cfg, float dt,
            SpringMethod method = SpringMethod.Analytic)
        {
            var (px, vx) = SpringSolver.Solve(s.Position.x, s.Velocity.x, target.x, cfg, method, dt);
            var (py, vy) = SpringSolver.Solve(s.Position.y, s.Velocity.y, target.y, cfg, method, dt);
            var (pz, vz) = SpringSolver.Solve(s.Position.z, s.Velocity.z, target.z, cfg, method, dt);
            s.Position = new Vector3(px, py, pz);
            s.Velocity = new Vector3(vx, vy, vz);
        }
    }
}
```

`Runtime/Utilities/PhysXHelper/Spring/Vector2Spring.cs` — file mới:

```csharp
using UnityEngine;

namespace Horcrux.Runtime.Utilities.PhysXHelper
{
    public struct Vector2SpringState
    {
        public Vector2 Position;
        public Vector2 Velocity;

        public Vector2SpringState(Vector2 position, Vector2 velocity = default)
        {
            Position = position;
            Velocity = velocity;
        }
    }

    public static class Vector2Spring
    {
        /// <summary>Hai trục độc lập, cùng config: lò xo tuyến tính tách biến nên không có toán vector.</summary>
        public static void Solve(ref Vector2SpringState s, Vector2 target, in SpringConfig cfg, float dt,
            SpringMethod method = SpringMethod.Analytic)
        {
            var (px, vx) = SpringSolver.Solve(s.Position.x, s.Velocity.x, target.x, cfg, method, dt);
            var (py, vy) = SpringSolver.Solve(s.Position.y, s.Velocity.y, target.y, cfg, method, dt);
            s.Position = new Vector2(px, py);
            s.Velocity = new Vector2(vx, vy);
        }
    }
}
```

`Tests/EditMode/SpringDamperTests.cs` (agent):

| Case | Input | Kỳ vọng |
|---|---|---|
| trục = scalar | `Vector3` state `(1, −2, 3)`, vel `(0.5, 0, −1)`, target `(5, −3, 10)`, một bước `dt = 0.1` | mỗi trục **bằng đúng** (`==`) `SpringSolver.Solve` scalar cùng tham số trục đó |
| hội tụ 3 trục | `(0, 0, 0)` → `(5, −3, 10)`, 300 bước `dt = 1/60` | `Position` ≈ target (±1e-3 mỗi trục), `Velocity` ≈ 0 |
| trục đã ở đích | `Position.x = target.x`, `Velocity.x = 0`, hai trục kia chưa | trục `x` không đổi qua mọi bước |
| `Vector2` | `(0, 0)` → `(5, −3)` | hội tụ; trục = scalar |
| `dt = 0` · config tắt | bất kỳ | state nguyên |
| Euler qua wrapper | `method = SemiImplicit`, `dt = 1/60` | hội tụ ≈ target (±0.05) |
| `velocity` mặc định | `new Vector3SpringState(p)` | `Velocity` = `Vector3.zero` |

---

## Chuyển dự án (agent)

| Chỗ | Đổi |
|---|---|
| `docs/Runtime/SpringDamper/SpringDamper.md` | tạo, mở `Code: Runtime/Utilities/PhysXHelper/Spring`: Flow · bảng chọn hệ `SpringDamper` (đích đổi được, có đà, bước theo `dt`) / `DampedOscillator` (quanh cân bằng cố định theo `t`) / `Interpolator.ExpDecay` (bậc một, không nảy) · toán: khối Thuật toán, dẫn giải đủ (hiện tượng, chuẩn hoá `(m, k, c)` → `(ω₀, ζ)`, phương trình đặc trưng, nghiệm từng chế độ bước ①–④) lấy từ `§0` của Plan này ở Horcrux `66e2ecf` · bảng API năm file · Design decisions từ comment và khối Không làm · bẫy: Euler với `ω₀·dt` lớn, `cosh` tràn khi `s·dt > 88`, `default` là tắt, dải tới hạn · Testing trỏ `Tests/EditMode/SpringDamperTests.cs` |
| `docs/Runtime/SpringDamper/SpringDamper.html` | tạo từ `.md`: kịch bản (đuổi theo đích, đổi đích giữa chừng, ba chế độ theo `ζ`) · cách dùng (config một lần, `Solve` mỗi frame, khi nào `SemiImplicit`) · flow · lưu ý; demo nếu có: slider `f`, `ζ` vẽ `Position` theo thời gian |
| `docs/Runtime/DampedOscillator/DampedOscillator.html` §5 | chỗ nhắc `SpringDamper` thành link tới `SpringDamper.html` |
| Plan này | xoá cùng `.meta` |

## Không làm · Mở rộng sau

| | Phương án | Vì sao |
|---|---|---|
| bỏ | state giữ `Target`, `SetTarget` | thêm một cửa ghi; chủ có đích đổi mỗi frame (camera bám) phải gọi hai hàm |
| bỏ | spring là `MonoBehaviour` tự chạy `Update` | chủ đã có vòng lặp và nhịp riêng; struct ghép vào mọi vòng lặp, pool, mảng |
| bỏ | phơi `m, k, c` làm núm | ba số dư một bậc tự do; `f` là "nhanh, cứng", `ζ` là "nảy nhiều hay ít" |
| bỏ | nghiệm cản mạnh dạng tổng hai mũ `C₁e^{r₁t} + C₂e^{r₂t}` | trừ hai số chênh lớn khi `ζ` lớn, mất chữ số với `float` |
| bỏ | dải tới hạn giữ `ζ` thật, `E` lệch công thức tới hạn | hai công thức cho một nhánh; sai số `ω₀·10⁻⁴·dt` dưới ngưỡng `float` |
| bỏ | explicit Euler (vị trí dùng `v` cũ) | bơm năng lượng mỗi bước, dễ nổ |
| bỏ | field `Zeta`, field `Method` trong config | không nhánh nào đọc `ζ` thô; `method` là tham số để một config dùng cho cả vật cần đúng lẫn vật cần rẻ |
| bỏ | generic `Spring<T>` với `IVectorOps`; `SolveAnalytic` riêng cho `Vector3` | một interface cho ba kiểu đã biết; sửa lõi scalar một chỗ thì ba kiểu hưởng |
| bỏ | ngủ khi tới đích `IsSettled` | chưa chủ nào cần; chủ tự so `abs(Position − target)`, `abs(Velocity)` |
| sau | `SpringConfig.FromPhysical(stiffness, damping, mass)`, `ToPhysical(mass)` | khi có số vật lý thật: `ω₀ = √(k/m)`, `ζ = c/(2√(km))`; ngược lại `k = ω₀²m`, `c = 2ζω₀m` |
| sau | nhánh cản mạnh bằng hai mũ âm `e^(−(ζω₀−s)dt)`, `e^(−(ζω₀+s)dt)`, hoặc chặn `dt` | khi gặp `s·dt > 88` (`ζ` lớn và `dt` lớn cùng lúc) |
| sau | giới hạn tốc độ theo độ dài vector, hiệu ứng ghép trục phi tuyến | hệ riêng gọi `SpringSolver`; lò xo tuyến tính tách trục |
