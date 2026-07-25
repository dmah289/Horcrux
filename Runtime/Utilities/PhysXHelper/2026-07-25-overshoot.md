# Overshoot Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.
>
> **Plan này là artifact học tập + triển khai, tự chứa.** Người dùng tự code lại để rèn tư duy → §0 dẫn giải **đầy đủ toán học** (vì sao đường Back vọt lố, đỉnh vọt bằng bao nhiêu, cách suy tham số từ đỉnh mong muốn), các task chứa **code hoàn chỉnh** kèm chú thích self-doc & lý do tối ưu. Không code test → mỗi task có **checklist kiểm chứng** (kiểm mốc + round-trip).

**Goal:** `Overshoot` — họ đường cong Back (In/Out/InOut) với **biên độ vọt lố chỉnh được**, dạng static, stateless, thuần tính toán. Khác `BackEase.cs` (vọt cố định ~10%): tại đây designer khai báo **đỉnh vọt mong muốn** (vd `0.2` = vọt 20%), hệ tự suy tham số `tension` nội bộ. Kèm helper áp thẳng vào giá trị (scale/pos/alpha).

**Architecture:** 1 file `Overshoot.cs`. Ba tầng: **(1) curve** thuần đa thức (`OutBack`/`InBack`/`InOutBack`) — hot path; **(2) nghịch đảo** `TensionForPeak(peak)` bằng Newton — gọi 1 lần lúc authoring (giống idiom `*Precomputed` của `Interpolator`); **(3) apply** `OvershootTo` nội suy `LerpUnclamped`. Cả ba đều xoay quanh **một** đỉnh vọt `p(s) = 4s³/27(s+1)²`.

**Tech Stack:** C# (Unity), `Unity.Mathematics` (`math.min/clamp`), `UnityEngine.Mathf.LerpUnclamped` — nhất quán `Interpolator.cs`/`BackEase.cs`. Thuần toán — không Addressables/UniTask.

## Global Constraints

| Ràng buộc | Giá trị |
|---|---|
| Namespace | `Horcrux.Runtime.Utilities.PhysXHelper` |
| Tầng phụ thuộc | Tầng 2, mục 12. **Độc lập** — chỉ `Unity.Mathematics`/`Mathf`, không gọi helper khác. (`← Easing` trong `Pendings.md` chỉ là ghi chú "cùng họ Back"; đây là bản overshoot chỉnh được, không import `Easer`.) |
| Zero-GC | thuần `float`/`int`/`enum` (stack); không `new` reference-type, LINQ, closure, string; stateless (không field) |
| SOLID | 1 class = 1 trách nhiệm (sinh giá trị đường cong Back, **không** giữ state, **không** đụng Transform/AudioSource); mở rộng qua overload, không sửa hàm cũ |
| Self-doc | tên nói rõ mục đích (`TensionForPeak`≠`Solve`); XML doc kèm công thức + "tại sao" ở mọi hàm public |
| Tham số hóa | designer khai báo **đỉnh vọt** `peak` (trực giác) → hệ suy `tension` (magic constant); không bắt dò `tension` thủ công |
| Guard biên | `peak ≤ 0` → `tension = 0` (không vọt, tránh `0/0` trong Newton); `OvershootTo` kẹp `t∈[0,1]` (ngoài khoảng cubic phân kỳ) |
| Unclamped | đường cong vọt ra ngoài `[0,1]` → **bắt buộc** `LerpUnclamped` khi áp giá trị |

---

## §0. Nền toán học (đọc trước khi code)

> Mục tiêu: hiểu **tại sao** đường Back vọt lố, **đỉnh vọt** liên hệ tham số `tension` ra sao, và cách **nghịch đảo** đỉnh mong muốn → `tension`. Đọc §0.1→0.4 theo thứ tự: hiện tượng → đỉnh vọt → 3 hướng cong → nghịch đảo.

### 0.1. Bản chất — "vọt quá đích rồi lắng về"

Chuyển động có **lực đàn hồi** (lò xo, cơ bắp, quán tính) không dừng đúng đích: nó **vọt qua** một chút rồi kéo ngược về ổn định. Mắt người đọc chuyển động này là "có lực, bén" — ngược với ease trơn (tới đích là dừng, cảm giác "mềm, hụt").

Đường **Back** mô phỏng đúng thế bằng đa thức bậc 3 có **1 tham số `tension` `s`** điều khiển độ vọt:

| Thành phần | Vai trò |
|---|---|
| **tension `s`** | độ "căng" của cú vọt; `s=0` → cubic trơn (không vọt), `s` càng lớn → vọt càng mạnh |
| **peak `p`** | biên độ vọt lố (phần vượt quá đích, vd `0.1` = vọt 10%) — thứ designer *thật sự* muốn chỉnh |
| **hướng** | **Out** vọt ở cuối (qua đích rồi về) · **In** hụt ở đầu (lùi lấy đà rồi phóng) · **InOut** cả hai |

**Vấn đề cốt lõi:** curve nhận `s`, nhưng `s` không trực giác (vọt 10% ứng `s=1.70158` — số ma thuật trong `BackEase.cs`). Designer nghĩ bằng `p`. → cần cầu nối `p → s`: trước hết suy `p(s)` (§0.2), rồi nghịch đảo (§0.4).

> **Lựa chọn mô hình — cubic 1 cú vọt, KHÔNG phải lò xo thật:** đàn hồi vật lý là **dao động tắt dần** (`x(t)=e^(−λt)cos(ω_d t)` — vọt qua, nảy về, vọt lại nhỏ hơn… vô số lần nhỏ dần). Back thay bằng **một** đa thức bậc 3 → chỉ **một** cú vọt rồi lắng thẳng, không nảy lại. Chọn cubic vì: (1) rẻ (3 phép nhân, không `exp`/`cos`, không cần trạng thái giữa các frame); (2) tất định theo `t∈[0,1]` — hợp tween có thời lượng cố định. Cần nảy nhiều lần thật (rung rinh, settle) → dùng `DampedOscillator`/`SpringDamper` (dao động có `ω_d`, `λ`). Back chỉ cho *cảm giác* bén, không mô phỏng lực.

### 0.2. Nguyên lý — suy đỉnh vọt `p(s)` từ đạo hàm

**Dạng của Back từ đâu ra?** Cần một cubic qua `(0,0)` và `(1,1)` (ease bình thường) **cộng** một số hạng tạo "bướu" vọt quanh đích. Số hạng `s(t−1)²` làm đúng thế: nó bằng 0 tại `t=1` (không phá đích) nhưng khác 0 quanh đó (tạo bướu); `s` chỉnh độ cao bướu. Kèm số hạng bậc 3 để đường về `1` trơn → dạng chuẩn **OutBack** (đích = 1):

$$g(t) = 1 + (s+1)(t-1)^3 + s(t-1)^2$$

**Kiểm dạng:** `g(0) = 1 + (s+1)(−1) + s = 0` ✓ (xuất phát 0); `g(1) = 1` ✓ (chạm đích). Giờ tìm **đỉnh vọt** = điểm `g` cao nhất → nơi vận tốc `g'(t) = 0`. Giải từng bước, **không nhảy**:

① Đặt `f = t − 1` → `g = 1 + (s+1)f³ + s·f²`. Đạo hàm (vì `df/dt = 1`):

$$g'(t) = 3(s+1)f^2 + 2s\,f = f\,[\,3(s+1)f + 2s\,]$$

② `g' = 0` cho hai nghiệm: `f = 0` (tức `t = 1`, chính là **đích** — không phải đỉnh vọt) và

$$f^* = \frac{-2s}{3(s+1)} \quad\Rightarrow\quad t^* = 1 - \frac{2s}{3(s+1)}$$

③ Đỉnh vọt = `g(t*) − 1`. Gom nhân tử (đúng dạng Horner mà code dùng): `g − 1 = f²·[(s+1)f + s]`. Thế `f*`: từ `(s+1)f* = −2s/3` → ngoặc `= s/3`; và `f*² = 4s²/9(s+1)²`. Nhân hai phần:

$$\boxed{\;p(s) = g(t^*) - 1 = \frac{4s^2}{9(s+1)^2}\cdot\frac{s}{3} = \frac{4s^3}{27\,(s+1)^2}\;}$$

**Kiểm tái lập:** thế `s = 1.70158` (hằng `C1` trong `BackEase.cs`) → `p = 0.10000` → đúng "Back vọt ~10%". `s=0 → p=0` (không vọt) ✓. `t* ≈ 0.580` → đỉnh nằm gần cuối, khớp trực giác "vọt ở cuối" (§0.1).

### 0.3. Ba hướng cong + hệ quả hằng số `1.525`

Cả ba dùng chung `p(s)` (§0.2), chỉ khác *đỉnh vọt xuất hiện ở đâu*:

| Hướng | Công thức | Biên độ vọt | Vọt ở đâu |
|---|---|---|---|
| **Out** | `1 + (s+1)(t−1)³ + s(t−1)²` | `+p(s)` | cuối (`t*≈0.58`), qua đích rồi về |
| **In** | `(s+1)t³ − s·t²` | `−p(s)` | đầu (`t*≈0.42`), hụt xuống rồi phóng |
| **InOut** | 2 nửa, tham số `c` (xem dưới) | `±p(c)/2` mỗi phía | cả đầu lẫn cuối |

- **In là phản chiếu của Out:** `InBack(t) = 1 − OutBack(1−t)` → cùng độ lớn đỉnh `p(s)`, đổi dấu (hụt thay vì vọt). Chứng: thay `t→1−t` và lấy `1−(·)` trên công thức Out ra đúng công thức In. → **một** `p(s)` phục vụ cả In lẫn Out.

- **InOut** ghép nửa In (0→0.5) + nửa Out (0.5→1), scale về `[0,1]`; mỗi nửa dùng tension `c`:

$$\text{InOut}(t) = \begin{cases} \tfrac{1}{2}\,x^2\big[(c+1)x - c\big], & x = 2t,\ t < 0.5 \\[4pt] \tfrac{1}{2}\big(y^2\big[(c+1)y + c\big] + 2\big), & y = 2t-2,\ t \ge 0.5 \end{cases}$$

  Vì mỗi nửa bị scale `½`, biên độ vọt của InOut là **`p(c)/2`** (không phải `p(c)`).

- **Hệ quả — hằng `1.525` không phải tiên đề:** `BackEase.cs` đặt `C2 = 1.70158 × 1.525` cho InOut. Vì sao `1.525`? Để InOut **cũng vọt ~10%** giống In/Out (đồng nhất cảm giác). Cần `p(c)/2 = 0.1` → `p(c) = 0.2` → giải ra `c ≈ 2.5924`, tức `c / 1.70158 ≈ 1.5235 ≈ 1.525`. Kiểm: `p(2.5949)/2 = 0.1001` ✓. → `1.525` **rơi ra** từ ràng buộc "InOut vọt bằng In/Out", không tự nhiên có.

### 0.4. Nghịch đảo `peak → tension` bằng Newton

Designer cho `peak`, cần `s` sao cho `p(s) = peak`. Từ §0.2, đây là **phương trình bậc 3** (nhân chéo `p(s)=peak`):

$$F(s) = 4s^3 - 27\,\text{peak}\,(s+1)^2 = 0$$

Bậc 3 có dạng đóng (Cardano) nhưng biểu thức nặng (`cbrt`, xử lý 3 nghiệm). Vì `p(s)` **đơn điệu tăng** trên `s>0` (mỗi `peak>0` có đúng 1 nghiệm dương), dùng **Newton** vừa gọn vừa dạy được:

$$s \leftarrow s - \frac{F(s)}{F'(s)}, \qquad F'(s) = 12s^2 - 54\,\text{peak}\,(s+1)$$

**Seed neo vào hằng đã biết** (`p(1.70158)=0.1` từ §0.2): xấp xỉ `p(s) ≈ s/17` quanh vùng dùng → chọn `s₀ = (1.70158/0.1)·peak = 17.0158·peak`. Seed sát → hội tụ nhanh:

| peak | 4 vòng (sai số) | 6 vòng (sai số) | `tension s` |
|---|---|---|---|
| 0.05 | `1.5e-6` | `<1e-16` | 1.1653 |
| 0.10 | `0` | `0` | **1.70154** (≈ hằng `BackEase`) |
| 0.20 | `1.4e-8` | `<1e-16` | 2.5924 |
| 0.30 | `3.9e-6` | `<1e-16` | 3.3941 |
| 0.50 | `2.0e-4` | `2.5e-14` | 4.8963 |

→ **6 vòng** đủ chạm sai số máy cho `peak ∈ [0.05, 1]` (vùng game feel). Chọn cố định 6 (không lặp tới hội tụ) → không nhánh, không alloc.

- **Guard `peak ≤ 0`:** seed `s₀=0` → `F=0, F'=0` → `0/0 = NaN`. Chặn sớm: `peak ≤ 0` → trả `0` (`p(0)=0`, đúng nghĩa "không vọt").
- **InOut:** biên độ InOut là `p(c)/2` (§0.3) → muốn InOut vọt `peak` thì cần `p(c) = 2·peak` → gọi `TensionForPeak(2·peak)`. (Nêu rõ ở XML doc để caller khỏi nhầm.)

### 0.5. Kiểm mốc (xác nhận công thức đúng trước khi code)

| Mốc | Kỳ vọng | Kiểm |
|---|---|---|
| `OutBack(0, s)` | `0` | `1+(s+1)(−1)³+s·1 = 0` |
| `OutBack(1, s)` | `1` | `f=0 → 1` (đích) |
| đỉnh `OutBack(t*, 1.70158)−1` | `≈ 0.1000` | `p(1.70158)` (§0.2) |
| `InBack(0, s)` / `InBack(1, s)` | `0` / `1` | phản chiếu Out |
| đáy `InBack(t*, 1.70158)` | `≈ −0.1000` | `−p(s)`, hụt (§0.3) |
| `InOutBack(0)` / `(0.5)` / `(1)` | `0` / `0.5` / `1` | đối xứng qua tâm |
| `TensionForPeak(0.1)` | `≈ 1.70158` | khớp `C1` của `BackEase.cs` (§0.4) |
| `TensionForPeak(0.2)` | `≈ 2.5924` | (§0.4) |
| `TensionForPeak(0)` / âm | `0` | guard, không NaN (§0.4) |
| **round-trip** `p(TensionForPeak(0.2))` | `≈ 0.2` | Newton nghịch đảo đúng |
| `OvershootTo(a, b, 0, ·)` / `(a, b, 1, ·)` | `a` / `b` | kẹp biên `t` |

---

## Bản đồ triển khai

```
PhysXHelper/
└── Overshoot.cs   1 file, 2 task tăng dần
     ├── Task 1  const + OutBack/InBack/InOutBack (curve) + TensionForPeak (nghịch đảo Newton)   §0.2, §0.3, §0.4
     └── Task 2  enum EaseDir + OvershootTo (apply, LerpUnclamped)                                §0.3
```
Thứ tự: **1 → 2**. Task 2 *modify* file Task 1 tạo (thêm enum + hàm, không sửa hàm cũ → Open/Closed).

---

### Task 1: `OutBack`/`InBack`/`InOutBack` + `TensionForPeak` — curve & nghịch đảo tham số

**Files:**
- Create: `Assets/Horcrux/Runtime/Utilities/PhysXHelper/Overshoot.cs`

**Interfaces:**
- Consumes: — (chỉ `Unity.Mathematics`).
- Produces:
  - `static float OutBack(float t, float tension)`
  - `static float InBack(float t, float tension)`
  - `static float InOutBack(float t, float tension)`
  - `static float TensionForPeak(float peak)`
  - `const float DefaultTension`, `SeedSlope`; `const int NewtonIters`

**Bản đồ toán → code:** curve = dán thẳng công thức §0.3; `TensionForPeak` = Newton §0.4 (`F=4s³−27·peak·(s+1)²`, `F'=12s²−54·peak·(s+1)`, seed `17.0158·peak`, 6 vòng, guard `peak≤0`).

**Self-doc & tối ưu:**

| Quyết định | Lý do |
|---|---|
| `static`, thuần `float`, không field | stateless, zero-GC, thread-safe, dễ test |
| curve dạng **Horner** (`f²·[(s+1)f+s]`), không `math.pow` | gộp bậc 3 còn **3 phép nhân** (khai triển mất 5); rẻ hơn `pow` tổng quát; nhất quán Horner của `Interpolator.SmootherStep` |
| `[AggressiveInlining]` cho 3 curve | thân mỏng (vài phép nhân) → nội tuyến khỏi phí gọi hàm (như `Interpolator.cs`) |
| `TensionForPeak` **không** `AggressiveInlining` | có vòng lặp — không phải wrapper mỏng; gọi lúc authoring nên không cần |
| `NewtonIters = 6` hằng, lặp cố định | 6 vòng chạm sai số máy cho peak∈[0.05,1] (§0.4) → bỏ điều kiện hội tụ → không nhánh trong vòng |
| `SeedSlope = 17.0158f` (`=1.70158/0.1`) | seed neo `p(1.70158)=0.1` → hội tụ nhanh, self-doc bằng tên (§0.4) |
| guard `peak ≤ 0 → 0f` | seed 0 làm `F/F' = 0/0 = NaN`; trả 0 = "không vọt", đúng `p(0)=0` (§0.4) |
| `DefaultTension = 1.70158f` | tiện dùng nhanh (= vọt 10%, đồng bộ `BackEase`); tên thay số ma thuật |
| tách `s1 = s+1` trong vòng | dùng 3 lần (`F`, `F'`) → tính 1 lần, tránh lặp phép cộng |

- [ ] **Step 1: Tạo file với code Task 1**

```csharp
using System.Runtime.CompilerServices;
using Unity.Mathematics;

namespace Horcrux.Runtime.Utilities.PhysXHelper
{
    /// <summary>
    /// Họ đường cong Back (In/Out/InOut) với biên độ vọt lố CHỈNH ĐƯỢC: mô phỏng chuyển động
    /// đàn hồi "vọt quá đích rồi lắng về". Static, stateless, zero-GC.
    /// </summary>
    /// <remarks>
    /// Khác BackEase (vọt cố định ~10%): designer khai báo ĐỈNH vọt <c>peak</c> (vd 0.2 = vọt 20%),
    /// gọi <see cref="TensionForPeak"/> ra <c>tension</c> rồi truyền vào curve.
    /// Đỉnh vọt: p(s) = 4s³/27(s+1)² (§0.2). Curve vọt ra ngoài [0,1] → áp giá trị bằng LerpUnclamped.
    /// Class chỉ sinh giá trị đường cong — không giữ state, không đụng Transform/AudioSource.
    /// </remarks>
    public static class Overshoot
    {
        /// <summary>Tension cho vọt ~10% (= hằng của BackEase). Dùng nhanh khi khỏi tính từ peak.</summary>
        public const float DefaultTension = 1.70158f;      // p(1.70158) ≈ 0.10 (§0.2)

        private const int   NewtonIters = 6;               // 6 vòng chạm sai số máy, peak∈[0.05,1] (§0.4)
        private const float SeedSlope   = 17.0158f;        // = 1.70158/0.1: seed neo p(1.70158)=0.1 (§0.4)

        /// <summary>OutBack: tiến tới đích, vọt qua rồi lắng về. Đỉnh +p(tension) ở cuối (t*≈0.58).</summary>
        /// <remarks>Formula: 1 + (s+1)(t−1)³ + s(t−1)² (§0.3). Trả giá trị có thể &gt; 1 (phần vọt).</remarks>
        /// <param name="t">Tiến độ [0,1] (không tự kẹp — dùng qua OvershootTo để an toàn biên).</param>
        /// <param name="tension">Độ căng cú vọt; lấy từ <see cref="TensionForPeak"/> hoặc DefaultTension.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float OutBack(float t, float tension)
        {
            float f = t - 1f;
            // 1 + (s+1)(t−1)³ + s(t−1)²  ==  1 + f²·[(s+1)f + s]  (Horner: 5→3 phép nhân)
            return 1f + f * f * ((tension + 1f) * f + tension);
        }

        /// <summary>InBack: lùi lấy đà (hụt xuống 0) rồi phóng tới đích. Đáy −p(tension) ở đầu (t*≈0.42).</summary>
        /// <remarks>Formula: (s+1)t³ − s·t² (§0.3). Phản chiếu của OutBack: InBack(t)=1−OutBack(1−t).</remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float InBack(float t, float tension)
            => t * t * ((tension + 1f) * t - tension); // (s+1)t³ − s·t² == t²·[(s+1)t − s] (Horner: 5→3 nhân)

        /// <summary>InOutBack: hụt ở đầu + vọt ở cuối. Biên độ mỗi phía = p(tension)/2 (§0.3).</summary>
        /// <remarks>
        /// Formula: nửa đầu ½x²[(c+1)x−c] với x=2t; nửa sau ½(y²[(c+1)y+c]+2) với y=2t−2 (§0.3).
        /// Muốn vọt đúng <c>peak</c> → truyền <c>TensionForPeak(2·peak)</c> (vì biên độ = p(c)/2).
        /// </remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float InOutBack(float t, float tension)
        {
            if (t < 0.5f)
            {
                float x = 2f * t;
                return x * x * ((tension + 1f) * x - tension) * 0.5f;       // nửa đầu (§0.3)
            }
            float y = 2f * t - 2f;
            return (y * y * ((tension + 1f) * y + tension) + 2f) * 0.5f;    // nửa sau (§0.3)
        }

        /// <summary>
        /// Nghịch đảo p(s)=peak → tension (§0.4): giải bậc 3 4s³−27·peak·(s+1)²=0 bằng Newton.
        /// Gọi 1 lần lúc authoring (không phải mỗi frame), cache kết quả rồi truyền vào curve.
        /// </summary>
        /// <remarks>
        /// F(s)=4s³−27·peak·(s+1)², F′(s)=12s²−54·peak·(s+1). Seed 17.0158·peak, 6 vòng → sai số máy.
        /// InOut cần vọt <c>peak</c> → gọi TensionForPeak(2·peak) (biên độ InOut = p(c)/2, §0.3).
        /// </remarks>
        /// <param name="peak">Đỉnh vọt lố mong muốn (0.1 = vọt 10%). ≤ 0 → trả 0 (không vọt).</param>
        /// <returns>tension truyền vào OutBack/InBack/InOutBack.</returns>
        public static float TensionForPeak(float peak)
        {
            if (peak <= 0f) return 0f;          // không vọt; tránh 0/0 trong Newton (§0.4)
            float s = SeedSlope * peak;         // seed neo p(1.70158)=0.1 (§0.4)
            for (int i = 0; i < NewtonIters; i++)
            {
                float s1 = s + 1f;
                float F  = 4f * s * s * s - 27f * peak * s1 * s1;  // 4s³ − 27·peak·(s+1)²
                float Fp = 12f * s * s - 54f * peak * s1;          // F′(s) = 12s² − 54·peak·(s+1)
                s -= F / Fp;                                       // Newton: s ← s − F/F′
            }
            return s;
        }
    }
}
```

- [ ] **Step 2: Kiểm chứng (chạy tay / nhẩm theo §0.5)**

| Input | Kỳ vọng |
|---|---|
| `OutBack(0, 1.70158f)` | `0` |
| `OutBack(1, 1.70158f)` | `1` |
| `OutBack(0.58f, 1.70158f)` | `≈ 1.1000` (đỉnh ≈ 1 + 0.1; đỉnh chính xác tại `t*≈0.5801`) |
| `InBack(1, 1.70158f)` | `1` |
| `InBack(0.42f, 1.70158f)` | `≈ −0.1000` (đáy ≈ −0.1; đáy chính xác tại `t*≈0.4199`) |
| `InOutBack(0.5f, 2.5949f)` | `0.5` |
| `TensionForPeak(0.1f)` | `≈ 1.70154` (khớp `BackEase.C1`) |
| `TensionForPeak(0.2f)` | `≈ 2.5924` |
| `TensionForPeak(0f)` / `TensionForPeak(-1f)` | `0` (không NaN) |
| `TensionForPeak(0.2f)` → thế lại `p(s)` | `≈ 0.2` (round-trip) |

Unity biên dịch sạch (Console không lỗi).

- [ ] **Step 3: Commit**

```bash
git add Assets/Horcrux/Runtime/Utilities/PhysXHelper/Overshoot.cs
git commit -m "feat(physx): Overshoot - tunable Back curves + TensionForPeak (core)"
```

---

### Task 2: `EaseDir` + `OvershootTo` — áp đường cong vào giá trị

**Files:**
- Modify: `Assets/Horcrux/Runtime/Utilities/PhysXHelper/Overshoot.cs` (thêm enum + 1 hàm, không sửa hàm cũ)

**Interfaces:**
- Consumes: `OutBack`/`InBack`/`InOutBack(float, float)` — Task 1.
- Produces:
  - `enum EaseDir { In, Out, InOut }`
  - `static float OvershootTo(float from, float to, float t, float tension, EaseDir dir)`

**Bản đồ toán → code:** chọn curve theo `dir` (§0.3) → hệ số `k` → `LerpUnclamped(from, to, k)`. Kẹp `t∈[0,1]` (ngoài khoảng cubic phân kỳ) trả thẳng `from`/`to` (đích chính xác).

**Self-doc & tối ưu:**

| Quyết định | Lý do |
|---|---|
| `OvershootTo` delegate về curve Task 1 | DRY — công thức đường cong ở 1 chỗ, apply chỉ chọn hướng + nội suy |
| `Mathf.LerpUnclamped` (không `Lerp`) | curve vọt ra ngoài `[0,1]` → `Lerp` kẹp mất phần vọt; phải Unclamped (§0.3) |
| kẹp `t ≤ 0 → from`, `t ≥ 1 → to` | ngoài `[0,1]` đa thức bậc 3 phân kỳ; chặn sớm cho đích **chính xác** + khỏi tính cong (giống `Easer.Evaluate`) |
| `enum EaseDir` (In/Out/InOut) | ý gọi rõ ràng, self-doc thay `int`/`bool`; `switch` biên dịch thành bảng nhảy rẻ |
| `switch` expression trả `k` | 1 nhánh dự đoán tốt (dir cố định mỗi call-site); rẻ so với chi phí feel |
| `[AggressiveInlining]` | thân mỏng (1 switch + 1 lerp) → nội tuyến khỏi phí gọi hàm |
| trả `float`, không tự set Transform | SRP — class sinh giá trị, caller gán `transform.localScale`/`position`/`color.a` |
| `tension` truyền vào (không tự tính từ peak) | tách precompute khỏi hot path: caller `TensionForPeak` 1 lần, `OvershootTo` gọi mỗi frame chỉ nội suy |

- [ ] **Step 1: Thêm enum + code Task 2 vào file** (enum ngoài class, cùng namespace; hàm đặt sau `TensionForPeak`)

```csharp
    /// <summary>Hướng đường cong Back: In (hụt đầu), Out (vọt cuối), InOut (cả hai).</summary>
    public enum EaseDir { In, Out, InOut }
```

```csharp
        /// <summary>
        /// Nội suy from→to theo đường cong Back, vọt lố rồi lắng đúng đích. Dùng cho pop/progress/floating text.
        /// </summary>
        /// <remarks>
        /// k = curve(t, tension) theo <paramref name="dir"/> (§0.3); trả LerpUnclamped(from, to, k)
        /// vì k có thể &gt; 1 hoặc &lt; 0 (phần vọt). t được kẹp [0,1] → đích chính xác tại biên.
        /// tension lấy từ <see cref="TensionForPeak"/> (gọi 1 lần), truyền vào đây mỗi frame.
        /// </remarks>
        /// <param name="from">Giá trị đầu (t=0).</param>
        /// <param name="to">Giá trị đích (t=1, chạm chính xác sau khi vọt).</param>
        /// <param name="t">Tiến độ; kẹp [0,1].</param>
        /// <param name="tension">Độ căng cú vọt (§0.4).</param>
        /// <param name="dir">Hướng cong (§0.3).</param>
        /// <returns>Giá trị đã nội suy (có thể vượt [from,to] tại đoạn vọt).</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float OvershootTo(float from, float to, float t, float tension, EaseDir dir)
        {
            if (t <= 0f) return from;
            if (t >= 1f) return to;                 // đích chính xác, khỏi tính cong
            float k = dir switch
            {
                EaseDir.In  => InBack(t, tension),
                EaseDir.Out => OutBack(t, tension),
                _           => InOutBack(t, tension),
            };
            return Mathf.LerpUnclamped(from, to, k); // k vọt ngoài [0,1] → phải Unclamped (§0.3)
        }
```

> **Lưu ý import:** `Mathf` thuộc `UnityEngine` → thêm `using UnityEngine;` đầu file (nhất quán `Interpolator.cs` đã dùng `Mathf.Clamp01`/`LerpUnclamped`).

- [ ] **Step 2: Kiểm chứng — kiểm mốc (§0.5)**

| Input | Kỳ vọng |
|---|---|
| `OvershootTo(0, 100, 0f, DefaultTension, Out)` | `0` (biên đầu) |
| `OvershootTo(0, 100, 1f, DefaultTension, Out)` | `100` (đích chính xác) |
| `OvershootTo(0, 100, 0.58f, DefaultTension, Out)` | `≈ 110.00` (vọt qua 100 rồi sẽ về) |
| `OvershootTo(0, 100, 0.42f, DefaultTension, In)` | `≈ −10.00` (hụt dưới 0 lấy đà) |
| `OvershootTo(0, 100, 0.5f, TensionForPeak(0.4f), InOut)` | `50` (tâm đối xứng) |
| `OvershootTo(a, b, 2f, ·, ·)` | `b` (kẹp `t≥1`) |

- [ ] **Step 3: Commit**

```bash
git add Assets/Horcrux/Runtime/Utilities/PhysXHelper/Overshoot.cs
git commit -m "feat(physx): Overshoot - EaseDir + OvershootTo apply helper"
```

---

## Ghi chú thực thi

- **Precompute tension:** `TensionForPeak` giải Newton → **không** gọi mỗi frame. Pattern đúng: `float s = Overshoot.TensionForPeak(0.2f);` (1 lần lúc setup), rồi `OvershootTo(..., s, ...)` trong `Update`/tween. Giống idiom `*Precomputed` của `Interpolator`.
- **InOut cần `TensionForPeak(2·peak)`**, In/Out dùng `TensionForPeak(peak)` trực tiếp (vì sao — xem §0.4).
- **Chỉ sinh giá trị, không áp thẳng:** `OvershootTo` trả `float`; caller gán `transform.localScale = Vector3.one * OvershootTo(...)` hoặc dùng cho `position`/`color.a` — tách trách nhiệm (SRP). Ghép vọt lố với squash/fade là hệ tổng hợp tầng 3 (`FloatingText`, `ProgressPop`).
- **Vọt ra ngoài biên là chủ ý:** đoạn vọt cho `OvershootTo` trả giá trị `> to` hoặc `< from` — đây là *hiệu ứng*, không phải lỗi; đừng "sửa" bằng `Clamp` (mất luôn cảm giác bén). Chỉ kẹp `t`, không kẹp kết quả.
- **File `.meta`:** Unity tự sinh khi import — commit kèm nếu repo giữ GUID ổn định.
- **Kiểm chứng:** chạy tay qua script tạm hoặc nhẩm theo kiểm mốc §0.5 — **không** tạo file test. Xóa script tạm trước khi commit.
- **Muốn test tự động sau này:** dựng NUnit EditMode theo bảng kiểm chứng (đặc biệt: round-trip `p(TensionForPeak(peak))≈peak`; đối xứng `InBack(t)=1−OutBack(1−t)`; guard `peak≤0→0`; kẹp `t` biên) — ngoài phạm vi plan này.
- **Cập nhật roadmap:** sau khi xong, đánh dấu `Overshoot` ✅ trong `Pendings.md` (Tầng 2, mục 12) — mở khóa `FloatingText` (#34), `ProgressPop` (#35), `CameraPunch`/`ZoomPunch` (#23).
