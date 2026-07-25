# ColorFlash Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.
>
> **Plan này là artifact học tập + triển khai, tự chứa.** Người dùng tự code lại để rèn tư duy → §0 dẫn giải **đầy đủ** bản chất (vì sao flash = envelope × blend, vì sao blend sai không gian màu làm tối màu, hai mô hình thời gian khác nhau ra sao), các task chứa **code hoàn chỉnh** kèm chú thích self-doc & lý do tối ưu. Không code test → mỗi task có **checklist kiểm chứng** (kiểm mốc + độc-lập-framerate).

**Goal:** `ColorFlash` — nháy màu tức thời báo sự kiện (trúng đòn, pickup, cảnh báo): blend nhanh màu nền về **màu nhấn** (thường trắng) rồi trả lại. Static, stateless, zero-GC. Tách 3 tầng độc lập: **màu** (blend gamma rẻ / linear đúng vật lý), **thời gian** (pulse chuẩn hóa / exp-decay tức thời), **áp** (combo 1-lời-gọi mỗi frame).

**Architecture:** 1 file `ColorFlash.cs`, ba tầng xếp chồng:

```
(3) Apply  FlashPulse / FlashDecayStep   ── combo: envelope × blend, 1 call/frame
             │            │
(2) Envelope PulseEnvelope / DecayEnvelope ── cường độ e∈[0,1] theo thời gian   §0.3, §0.4
             │            │  (← Interpolator.SmootherStep / ExpDecay)
(1) Blend   Flash(base, flash, e, linear) ── "màu nào" tại cường độ e            §0.2
```

Ý tưởng gốc: mọi flash tách làm **hai câu hỏi độc lập** — *(a) tại cường độ `e` thì ra màu gì* (blend, §0.2) và *(b) `e` biến thiên thế nào theo thời gian* (envelope, §0.3–0.4). Tách xong ghép lại ở tầng apply.

**Tech Stack:** C# (Unity). `UnityEngine` (`Color`, `Color.LerpUnclamped`, `.linear`/`.gamma`). Tái dùng `Interpolator` (`SmootherStep` §0.3, `ExpDecay` §0.4) — **cùng namespace**, khỏi `using`. Thuần toán — không Addressables/UniTask.

## Global Constraints

| Ràng buộc | Giá trị |
|---|---|
| Namespace | `Horcrux.Runtime.Utilities.PhysXHelper` |
| Tầng phụ thuộc | Tầng 2, mục 16. `← InterpolationHelper` (`Interpolator`): dùng `SmootherStep` (envelope pulse) + `ExpDecay` (envelope decay). Không đụng helper khác. |
| Zero-GC | thuần `Color`/`float` (struct, stack); không `new` reference-type, LINQ, closure, string; stateless (không field). Trạng thái decay do **caller** giữ (`ref float intensity`) |
| SOLID | 1 class = 1 trách nhiệm (**sinh `Color` đã flash**; không giữ state, không đụng `SpriteRenderer`/`Image`/`Material`); mở rộng qua overload/tham số, không sửa hàm cũ |
| Self-doc | tên nói rõ mục đích (`PulseEnvelope`≠`Curve`); XML doc kèm công thức + "tại sao" ở mọi hàm public |
| Tham số hóa | designer khai báo **màu nhấn + thời lượng/decay + đỉnh** (trực giác); không lộ chi tiết không gian màu trừ khi cần (`linear` opt-in) |
| Không gian màu | mặc định **gamma** (rẻ, khớp `Color.Lerp`/`ColorExtensions.Blend`); **linear** opt-in (đúng vật lý, §0.2). `bool linear` truyền literal → nhánh **fold** mất (zero-cost, idiom `Interpolator.Remap`) |
| Giữ alpha | flash chỉ đổi **RGB**, giữ nguyên `alpha` nền (nháy màu ≠ đổi độ mờ) |

---

## §0. Nền toán học (đọc trước khi code)

> Mục tiêu: hiểu **flash = envelope × blend**, vì sao blend phải đúng không gian màu, và hai đường cường độ theo thời gian. Đọc §0.1→0.4: hiện tượng → màu (blend) → thời gian pha 1 (pulse) → thời gian pha 2 (decay).

### 0.1. Bản chất — "chớp một cái rồi trả lại"

Khi một sự kiện cần **báo tức thì** (đòn trúng, nhặt item, cảnh báo), ta đẩy nhanh màu vật thể về một **màu nhấn** rồi kéo về màu gốc. Mắt bắt chuyển động sáng đột ngột này trước cả khi đọc nội dung → cảm giác "có phản hồi, đã tay".

Tách flash thành **hai thành phần nhân nhau**, mỗi thành phần một trách nhiệm:

| Thành phần | Ký hiệu | Vai trò | Quyết định |
|---|---|---|---|
| **blend** | `Flash(base, flash, e)` | tại cường độ `e` thì màu ra sao (nội suy nền↔nhấn) | *màu gì* — §0.2 |
| **envelope** | `e(t) ∈ [0,1]` | cường độ nhấn biến thiên theo thời gian (0→đỉnh→0) | *khi nào mạnh* — §0.3, §0.4 |

`e = 0` → hoàn toàn màu nền; `e = 1` → hoàn toàn màu nhấn. Envelope là một **xung**: lên nhanh tới đỉnh rồi tắt. Hai cách dựng xung (cùng mục tiêu, khác cảm giác + cách dùng):

| Mô hình | Đường `e(t)` | Kết thúc | Hợp ca |
|---|---|---|---|
| **Pulse chuẩn hóa** (§0.3) | out-and-back trên `t = elapsed/duration` | về **đúng** 0 tại `t=1` | hit 1 phát, đồng bộ tween/squash, orchestration |
| **Exp-decay tức thời** (§0.4) | hit → `e=1`, rồi `e·e^(−λ·dt)` | tiệm cận 0 (không mốc cứng) | bắn liên hoàn "nạp chồng", zero-config duration |

> **Lựa chọn mô hình:** cả hai chỉ là *đường cường độ* để feel, không mô phỏng quang học — nên chọn theo cách dùng: pulse khi cần biết trước thời lượng + kết thúc sạch, decay khi cần nạp-chồng + khỏi khai báo duration. Phần **đúng đắn vật lý thật sự** nằm ở *không gian màu* của blend (§0.2), không ở đường thời gian.

### 0.2. Blend — không gian màu gamma vs linear (lõi vật lý)

**Nguyên lý gốc:** cường độ ánh sáng **cộng tuyến tính** — trộn hai nguồn sáng thì *quang thông* (photon/giây) cộng lại. Nhưng giá trị `Color` (0–1) mà ta lưu **không** tỉ lệ thẳng với ánh sáng: màn hình mã hóa **sRGB gamma** để dồn bit cho vùng tối (mắt nhạy vùng tối). Quan hệ xấp xỉ (`γ ≈ 2.2`):

$$V_\text{linear} = V_\text{srgb}^{\,\gamma}, \qquad V_\text{srgb} = V_\text{linear}^{\,1/\gamma}$$

→ **muốn trộn ánh sáng đúng phải trộn ở miền linear**, không trộn thẳng số sRGB. Quy trình đúng: **giải mã → nội suy → mã hóa lại**:

$$\text{blend}(a,b,e) = \Big[(1-e)\,a_\text{srgb}^{\gamma} + e\,b_\text{srgb}^{\gamma}\Big]^{1/\gamma}$$

**Kiểm tái lập — vì sao gamma-thẳng làm tối:** trộn 50% đen (`0`) + trắng (`1`), `e=0.5`:

| Cách | Tính | Hiển thị |
|---|---|---|
| **gamma-thẳng** (`Color.Lerp`) | `0.5` luôn | **0.500** (tối) |
| **linear đúng** | `(0.5·0 + 0.5·1)^{1/2.2}` | **0.730** |
| **linear đúng** (sRGB piecewise chuẩn Unity) | decode→lerp→encode | **0.735** |

Chênh **0.23** ở midtone — flash trắng bằng gamma-thẳng trông "đục" hơn thực tế. Đây là lý do biến thể linear tồn tại.

**Chuyển đổi trong Unity (dùng thẳng, khỏi tự code):** `Color.linear` = decode (sRGB→linear), `Color.gamma` = encode (linear→sRGB) — dùng **sRGB piecewise chính xác** (không phải `pow(2.2)` thô), alpha giữ nguyên (alpha vốn tuyến tính). Nên:

$$\text{Flash}_\text{linear}(a,b,e) = \Big[\text{LerpUnclamped}(a_\text{.linear},\, b_\text{.linear},\, e)\Big]_\text{.gamma}$$

> **Lựa chọn mô hình:** mặc định **gamma-thẳng** (1 lerp, khớp `Color.Lerp`/`ColorExtensions.Blend` mà codebase quen) — flash rất ngắn (~0.1s) nên sai midtone thường không nhận ra. **Linear** opt-in cho ai cần độ sáng chuẩn (flash mạnh, cảnh nền HDR): đắt hơn (`.linear`/`.gamma` = pow mỗi kênh) nhưng đúng quang học. `bool linear` truyền literal → nhánh fold, không phí runtime khi không dùng.

### 0.3. Envelope pha 1 — pulse chuẩn hóa (out-and-back)

Xung chạy trên `t = elapsed/duration ∈ [0,1]`: lên tới **đỉnh** tại `p = peakAt` rồi về, **đúng 0** tại `t=1`.

**Cách dựng — chia đôi, mỗi nửa remap về `[0,1]` rồi qua cùng một hàm trơn `S`:**

| Nửa | Khoảng `t` | Remap → `[0,1]` | Qua `S` cho |
|---|---|---|---|
| attack | `0 → p` | `t/p` (0→0, p→1) | `S`: `0→1` |
| decay | `p → 1` | `(1−t)/(1−p)` (p→1, 1→0) | `S`: `1→0` (remap giảm) |

$$e(t) = \begin{cases} S\!\big(t / p\big), & 0 < t < p \\[4pt] S\!\big((1-t)/(1-p)\big), & p \le t < 1 \\[4pt] 0, & t \le 0 \ \text{hoặc}\ t \ge 1 \end{cases}$$

**Vì sao `S = Interpolator.SmootherStep`** (quintic `6t⁵−15t⁴+10t³`), không phải đường thẳng: `S` có **vận tốc 0 ở cả hai đầu** (`S′(0)=S′(1)=0`). Hệ quả kép:
- hai đầu xung (`t=0,1`) khởi/dừng **êm**, không giật khựng;
- tại **đỉnh** (`t=p`), hai nửa gặp nhau mà `S′(1)=0` cả hai phía → đạo hàm khớp `→0` (verify: `e′(p⁻),e′(p⁺)≈1e-9`) → đỉnh **bo tròn liền mạch**, không nhọn gãy.

**Kiểm mốc:** `e(0)=e(1)=0` ✓ (nền hai đầu); tại `t=p`, nửa decay cho `(1−p)/(1−p)=1 → S(1)=1` ✓ (chạm đỉnh nhấn).

`peakAt` là đòn bẩy cảm giác: **nhỏ** (vd `0.2`) → attack chớp nhanh, decay dài ("gắt", hợp hit); `0.5` → đối xứng.

### 0.4. Envelope pha 2 — exp-decay tức thời

Hit → `intensity = 1`, mỗi frame suy giảm mũ về 0. Nghiệm là `Interpolator.ExpDecay` với đích `0`:

$$e(t) = e_0 \cdot e^{-\lambda t} \quad (\lambda = \text{decay}, \ 1/\text{s})$$

Hai điều kiện phải đúng để cú tắt dần **giống nhau ở mọi fps** — tách bạch:

**(1) Bước cập nhật đúng — vì sao mũ, không nhân hằng-số.** Muốn tua tới cùng mốc thời gian phải cho cùng kết quả bất kể chia bao nhiêu bước. Hàm mũ có tính **nửa nhóm** `e^{−λ(t₁+t₂)} = e^{−λt₁}·e^{−λt₂}` → một bước `dt` = hai bước `dt/2` (verify: `1×0.1s = 2×0.05s`, lệch `5e-17`). Nhân hằng mỗi frame `intensity *= k` thì ra `k^{số frame}` — số frame đổi theo fps → **sai** (`0.9⁶⁰=0.0018` vs `0.9³⁰=0.042` cùng 1 giây).

**(2) Thứ tự đọc/ghi đúng — blend TRƯỚC, advance SAU.** Bước (1) chỉ đảm bảo *biến* `intensity` đúng; thứ **hiển thị ra màn hình** mới là cái người chơi thấy. Theo nguyên tắc integrator (đọc state → tiến hóa): dùng `intensity` **hiện tại** để blend, rồi mới suy giảm cho frame sau. Đảo lại (advance trước) sai hai mặt:

| | frame hit (`t=0`) | @wall-clock `0.1s` |
|---|---|---|
| **blend trước** (đúng) | `1` (full chớp) | `0.449 = e^{−8·0.1}` — khớp mọi fps |
| advance trước (sai) | `e^{−λdt}<1` (hụt) | `0.393` @60fps ≠ 30fps |

→ `FlashDecayStep` **bắt buộc** blend rồi mới `DecayEnvelope` (§ Task 3).

**Phụ:**
- **Half-life** (trực giác hơn `decay`): thời gian giảm nửa `t_½ = ln2/λ` (verify `λ=8 → t_½=0.0866s`, `e^{−8·0.0866}=0.5` ✓). Overload = `Interpolator.ExpDecayHalfLife`.
- **State ở caller:** `intensity` phải nhớ giữa các frame → caller giữ 1 `float` truyền `ref`, class vẫn stateless/zero-GC (§ Task 3).

### 0.5. Kiểm mốc (xác nhận trước khi code)

| Mốc | Kỳ vọng | Kiểm |
|---|---|---|
| `Flash(base, ·, 0)` | `base` (RGB) | Lerp endpoint `e=0` |
| `Flash(·, flash, 1)` | `flash` (RGB), alpha = nền | Lerp endpoint `e=1`, `r.a=base.a` |
| `Flash(black, white, 0.5, linear:false)` | RGB `0.5` | gamma-thẳng (§0.2) |
| `Flash(black, white, 0.5, linear:true)` | RGB `≈0.735` | linear đúng (§0.2) |
| `PulseEnvelope(0)` / `(1)` | `0` / `0` | về nền hai đầu (§0.3) |
| `PulseEnvelope(peakAt, peakAt)` | `1` | chạm đỉnh (§0.3) |
| `PulseEnvelope(0.25, 0.5)` | `0.5` | `S(0.5)=0.5` (§0.3) |
| `DecayEnvelope(1, 8, 0.0866)` | `≈0.5` | half-life `ln2/8` (§0.4) |
| `DecayEnvelope(1, 8, 0.1)` một bước vs hai bước `0.05` | bằng nhau | độc-lập-framerate (§0.4) |
| `DecayEnvelope(i, ≤0, dt)` | `i` | guard `decay≤0` (không phân rã) |

---

## Bản đồ triển khai

```
PhysXHelper/
└── ColorFlash.cs   1 file, 3 task tăng dần
     ├── Task 1  Flash(base, flash, e, linear)                    blend gamma/linear   §0.2
     ├── Task 2  PulseEnvelope + DecayEnvelope (+ HalfLife)       cường độ theo t      §0.3, §0.4
     └── Task 3  FlashPulse + FlashDecayStep                      combo apply 1 call   §0.1
```
Thứ tự **1 → 2 → 3**: Task 2 độc lập Task 1 (envelope thuần `float`), Task 3 ghép cả hai. Mỗi task *modify* file trước (thêm hàm, không sửa hàm cũ → Open/Closed).

---

### Task 1: `Flash` — blend nền↔nhấn (gamma / linear)

**Files:**
- Create: `Assets/Horcrux/Runtime/Utilities/PhysXHelper/ColorFlash.cs`

**Interfaces:**
- Consumes: — (chỉ `UnityEngine.Color`).
- Produces: `static Color Flash(Color baseColor, Color flashColor, float e, bool linear = false)`

**Bản đồ toán → code:** gamma = `Color.LerpUnclamped(base, flash, e)` (§0.2); linear = `LerpUnclamped(base.linear, flash.linear, e).gamma` (decode→lerp→encode, §0.2). Ghi đè `r.a = base.a` (giữ alpha nền).

**Self-doc & tối ưu:**

| Quyết định | Lý do |
|---|---|
| `static`, thuần `Color`, không field | stateless, zero-GC (struct trên stack), thread-safe |
| `bool linear = false`, truyền **literal** | nhánh fold lúc JIT → gamma không gánh chi phí linear; idiom `bool clamp` của `Interpolator.Remap` |
| dùng `.linear`/`.gamma` (không tự `pow`) | sRGB **piecewise chính xác** của Unity, đúng hơn `pow(2.2)` thô; alpha tự giữ (§0.2) |
| `LerpUnclamped` (không `Lerp`) | rẻ hơn (khỏi clamp); `e` từ envelope đã ∈[0,1]. Cho phép `e>1` = over-flash chủ ý |
| `r.a = baseColor.a` | flash đổi **màu**, không đổi độ mờ (nháy màu ≠ fade); tránh footgun khi `flash.a≠base.a` |

- [ ] **Step 1: Tạo file với code Task 1**

```csharp
using System.Runtime.CompilerServices;
using UnityEngine;

namespace Horcrux.Runtime.Utilities.PhysXHelper
{
    /// <summary>
    /// Nháy màu tức thời (hit/pickup/cảnh báo): blend màu nền về màu nhấn theo cường độ e rồi trả lại.
    /// Static, stateless, zero-GC. Tách 3 tầng: blend (màu, §0.2) · envelope (thời gian, §0.3-0.4) · apply.
    /// </summary>
    /// <remarks>
    /// flash = envelope(t) × blend(base, flash): e∈[0,1] điều cường độ, blend quyết màu tại e đó.
    /// Chỉ sinh <see cref="Color"/> — caller gán vào SpriteRenderer.color / Image.color / material.
    /// </remarks>
    public static class ColorFlash
    {
        /// <summary>
        /// Blend màu nền → màu nhấn tại cường độ <paramref name="e"/> (0 = nền, 1 = nhấn). Giữ alpha nền.
        /// </summary>
        /// <remarks>
        /// gamma (mặc định): LerpUnclamped thẳng — rẻ, khớp Color.Lerp (§0.2).
        /// linear (opt-in): decode(.linear)→lerp→encode(.gamma) — đúng quang học, midtone không bị tối (§0.2).
        /// </remarks>
        /// <param name="baseColor">Màu gốc (e=0). Alpha của nó được giữ nguyên ở kết quả.</param>
        /// <param name="flashColor">Màu nhấn (e=1), thường trắng.</param>
        /// <param name="e">Cường độ nhấn ∈[0,1] (lấy từ envelope §0.3/§0.4). e&gt;1 = over-flash.</param>
        /// <param name="linear">true = blend trong linear-space (đúng vật lý, đắt hơn). Truyền literal → fold.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Color Flash(Color baseColor, Color flashColor, float e, bool linear = false)
        {
            Color r = linear
                ? Color.LerpUnclamped(baseColor.linear, flashColor.linear, e).gamma // decode→lerp→encode (§0.2)
                : Color.LerpUnclamped(baseColor, flashColor, e);                    // gamma-thẳng, rẻ (§0.2)
            r.a = baseColor.a;   // flash chỉ đổi RGB, giữ nguyên độ mờ nền
            return r;
        }
    }
}
```

- [ ] **Step 2: Kiểm chứng (§0.5)**

| Input | Kỳ vọng |
|---|---|
| `Flash(Color.black, Color.white, 0f)` | `black` (a=1) |
| `Flash(Color.black, Color.white, 1f)` | `white` (a giữ = 1) |
| `Flash(Color.black, Color.white, 0.5f, false)` | RGB `≈0.5` |
| `Flash(Color.black, Color.white, 0.5f, true)` | RGB `≈0.735` |
| `Flash(c, white, e)` với `c.a=0.3` | kết quả `.a = 0.3` (giữ alpha nền) |

Unity biên dịch sạch (Console không lỗi).

- [ ] **Step 3: Commit**

```bash
git add Assets/Horcrux/Runtime/Utilities/PhysXHelper/ColorFlash.cs
git commit -m "feat(physx): ColorFlash - gamma/linear blend core"
```

---

### Task 2: `PulseEnvelope` + `DecayEnvelope` — cường độ theo thời gian

**Files:**
- Modify: `Assets/Horcrux/Runtime/Utilities/PhysXHelper/ColorFlash.cs`

**Interfaces:**
- Consumes: `Interpolator.SmootherStep`, `Interpolator.ExpDecay`, `Interpolator.ExpDecayHalfLife` (cùng namespace).
- Produces:
  - `static float PulseEnvelope(float t, float peakAt = 0.5f)`
  - `static float DecayEnvelope(float intensity, float decay, float dt)`
  - `static float DecayEnvelopeHalfLife(float intensity, float halfLife, float dt)`

**Bản đồ toán → code:** pulse = 2 nửa `SmootherStep` quanh `peakAt` (§0.3); decay = `ExpDecay` về đích `0` (§0.4); half-life = `ExpDecayHalfLife` về `0` (§0.4).

**Self-doc & tối ưu:**

| Quyết định | Lý do |
|---|---|
| pulse dùng lại `SmootherStep` cả 2 pha | DRY — không viết lại quintic; trơn C¹, vận tốc 0 hai đầu (§0.3) |
| decay **delegate** `ExpDecay(i, 0, ·, ·)` | phân rã về 0 *chính là* ExpDecay đích 0; tái dùng guard + `math.exp`, độc-lập-framerate (§0.4) |
| tách `PulseEnvelope` khỏi `Flash` | SRP — "cường độ theo thời gian" ⊥ "màu tại cường độ"; test/tái dùng riêng |
| guard `t≤0 ‖ t≥1 → 0` (pulse) | ngoài khoảng nhấn = màu nền; chặn `t/p` khỏi ra ngoài dải hữu ích |
| `peakAt` mặc định `0.5f` | đối xứng — ca thông dụng; nhỏ hơn → attack gắt hơn (§0.3) |
| `[AggressiveInlining]` cả ba | thân mỏng (1 nhánh + 1 call) → nội tuyến khỏi phí gọi hàm |

- [ ] **Step 1: Thêm 3 hàm vào class** (sau `Flash`)

```csharp
        /// <summary>
        /// Xung chuẩn hóa 0→1→0 trên t=elapsed/duration: lên tới đỉnh tại <paramref name="peakAt"/> rồi về 0.
        /// Về ĐÚNG 0 tại t=1 (kết thúc sạch). Dùng cho hit flash 1 phát / đồng bộ tween.
        /// </summary>
        /// <remarks>
        /// e(t) = S(t/p) khi t&lt;p ; S((1−t)/(1−p)) khi t≥p ; 0 ngoài (0,1). S = Interpolator.SmootherStep (§0.3).
        /// Nối trơn tại đỉnh (S′(1)=0 hai phía → không gãy). peakAt nhỏ = attack gắt, decay dài.
        /// </remarks>
        /// <param name="t">Tiến độ elapsed/duration; ngoài [0,1] → 0.</param>
        /// <param name="peakAt">Vị trí đỉnh ∈(0,1). 0.5 = đối xứng.</param>
        /// <returns>Cường độ nhấn ∈[0,1] để đưa vào <see cref="Flash"/>.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float PulseEnvelope(float t, float peakAt = 0.5f)
        {
            if (t <= 0f || t >= 1f) return 0f;                          // ngoài xung = nền (§0.3)
            return t < peakAt
                ? Interpolator.SmootherStep(t / peakAt)                 // attack 0→1 (§0.3)
                : Interpolator.SmootherStep((1f - t) / (1f - peakAt));  // decay 1→0 (§0.3)
        }

        /// <summary>
        /// Suy giảm cường độ theo phân rã mũ (độc-lập-framerate): dùng cho flash "nạp chồng" khi hit liên hoàn.
        /// Caller đặt intensity=1 lúc hit, mỗi frame gọi hàm này để giảm, rồi đưa vào <see cref="Flash"/>.
        /// </summary>
        /// <remarks>e = intensity·e^(−decay·dt) = ExpDecay(intensity, 0, decay, dt); độc-lập-fps nhờ nửa nhóm mũ (§0.4).</remarks>
        /// <param name="intensity">Cường độ hiện tại (1 lúc vừa hit).</param>
        /// <param name="decay">Tốc độ tắt 1/s (lớn = tắt nhanh). ≤0 → không tắt.</param>
        /// <param name="dt">Thời gian trôi qua (giây).</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float DecayEnvelope(float intensity, float decay, float dt)
            => Interpolator.ExpDecay(intensity, 0f, decay, dt);         // phân rã về 0 = ExpDecay đích 0 (§0.4)

        /// <summary><see cref="DecayEnvelope"/> tham số bằng half-life (trực giác hơn decay): thời gian giảm nửa.</summary>
        /// <remarks>e = intensity·2^(−dt/halfLife) = ExpDecayHalfLife(intensity, 0, halfLife, dt) (§0.4).</remarks>
        /// <param name="halfLife">Thời gian để cường độ giảm còn nửa (giây). ≤0 → không tắt.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float DecayEnvelopeHalfLife(float intensity, float halfLife, float dt)
            => Interpolator.ExpDecayHalfLife(intensity, 0f, halfLife, dt);
```

- [ ] **Step 2: Kiểm chứng (§0.5)**

| Input | Kỳ vọng |
|---|---|
| `PulseEnvelope(0f)` / `PulseEnvelope(1f)` | `0` / `0` |
| `PulseEnvelope(0.5f, 0.5f)` | `1` (đỉnh) |
| `PulseEnvelope(0.25f, 0.5f)` | `0.5` |
| `PulseEnvelope(0.1f, 0.2f)` | `0.5` (`S(0.5)`) |
| `DecayEnvelope(1f, 8f, 0.0866f)` | `≈0.5` (half-life) |
| `DecayEnvelope(1f, 8f, 0.1f)` vs 2× `DecayEnvelope(·, 8f, 0.05f)` | bằng nhau (`≈0.449`) |
| `DecayEnvelope(1f, 0f, 0.1f)` | `1` (guard decay≤0) |
| `DecayEnvelopeHalfLife(1f, 0.0866f, 0.0866f)` | `0.5` |

- [ ] **Step 3: Commit**

```bash
git add Assets/Horcrux/Runtime/Utilities/PhysXHelper/ColorFlash.cs
git commit -m "feat(physx): ColorFlash - pulse & exp-decay envelopes"
```

---

### Task 3: `FlashPulse` + `FlashDecayStep` — combo apply 1 lời-gọi/frame

**Files:**
- Modify: `Assets/Horcrux/Runtime/Utilities/PhysXHelper/ColorFlash.cs`

**Interfaces:**
- Consumes: `Flash`, `PulseEnvelope`, `DecayEnvelope` — Task 1–2.
- Produces:
  - `static Color FlashPulse(Color baseColor, Color flashColor, float t, float peakAt = 0.5f, bool linear = false)`
  - `static Color FlashDecayStep(Color baseColor, Color flashColor, ref float intensity, float decay, float dt, bool linear = false)`

**Bản đồ toán → code:** ghép §0.1 `flash = envelope × blend`. `FlashPulse` = `Flash(·, PulseEnvelope(t,peakAt), ·)` (stateless). `FlashDecayStep` = `Flash(·, intensity, ·)` bằng cường độ **hiện tại** rồi `DecayEnvelope` advance cho frame sau (thứ tự §0.4).

**Self-doc & tối ưu:**

| Quyết định | Lý do |
|---|---|
| combo delegate về Task 1–2 | DRY — logic màu/thời gian ở 1 chỗ; combo chỉ ghép, không lặp công thức |
| `FlashPulse` stateless (caller giữ `t`) | pulse cần `elapsed/duration` — caller cộng `Time.deltaTime`; class không giữ state |
| `FlashDecayStep(ref float intensity)` | decay cần nhớ intensity giữa frame; `ref` cập nhật tại chỗ, **zero-GC** (không box/alloc) |
| **blend trước, advance sau** (thứ tự cố định) | integrator chuẩn: giá trị *hiển thị* mới cần độc-lập-fps, không chỉ `intensity`. Advance-trước làm frame hit hụt (`e^{−λdt}`≠1) + hiển thị lệch fps (§0.4) |
| trả `Color`, không tự set renderer | SRP — caller gán `sprite.color = ...`; ghép với squash/shake là hệ tầng 3 (`SatisfyingClear`) |
| `[AggressiveInlining]` | thân mỏng (1 envelope + 1 blend) → nội tuyến |

- [ ] **Step 1: Thêm 2 hàm vào class** (sau các envelope)

```csharp
        /// <summary>
        /// Flash 1-phát theo xung chuẩn hóa: tự tính cường độ từ tiến độ t rồi blend. Về đúng màu nền tại t=1.
        /// Caller giữ elapsed (cộng Time.deltaTime), truyền t = elapsed/duration mỗi frame.
        /// </summary>
        /// <remarks>flash = <see cref="PulseEnvelope"/>(t, peakAt) × <see cref="Flash"/> (§0.1). Dừng khi t≥1.</remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Color FlashPulse(Color baseColor, Color flashColor, float t, float peakAt = 0.5f,
            bool linear = false)
            => Flash(baseColor, flashColor, PulseEnvelope(t, peakAt), linear);

        /// <summary>
        /// Flash "nạp chồng" theo exp-decay: giảm intensity 1 bước (độc-lập-framerate) rồi blend. 1 call/frame.
        /// Lúc hit: đặt intensity=1f (nạp lại). Không cần biết thời lượng — tự tắt dần.
        /// </summary>
        /// <remarks>
        /// Thứ tự BẮT BUỘC: blend <paramref name="intensity"/> HIỆN TẠI trước, advance SAU (§0.4 mục 2) —
        /// đảo lại làm frame hit hụt + hiển thị lệch fps. Zero-GC nhờ ref (không box).
        /// </remarks>
        /// <param name="intensity">ref: cường độ hiện tại (blend bằng giá trị này); hàm advance nó cho frame sau. Set =1f khi vừa hit.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Color FlashDecayStep(Color baseColor, Color flashColor, ref float intensity, float decay,
            float dt, bool linear = false)
        {
            Color r = Flash(baseColor, flashColor, intensity, linear); // blend cường độ HIỆN TẠI (§0.4)
            intensity = DecayEnvelope(intensity, decay, dt);           // advance cho frame sau, độc-lập-fps (§0.4)
            return r;
        }
```

- [ ] **Step 2: Kiểm chứng (§0.5)**

| Input | Kỳ vọng |
|---|---|
| `FlashPulse(black, white, 0f)` | `black` (`e=0`) |
| `FlashPulse(black, white, 1f)` | `black` (về nền, `e=0`) |
| `FlashPulse(black, white, 0.5f, 0.5f)` | `white` (đỉnh, `e=1`) |
| `FlashDecayStep(black, white, ref i=1f, 8f, dt)` | **`white`** (blend `i=1` hiện tại trước); `i` advance `→e^(−8·dt)` sau |
| lặp tới khi elapsed = `0.0866f` (half-life) | giá trị hiển thị `≈0.5` — khớp `e^(−8·elapsed)` **mọi fps** (§0.4) |
| gọi `FlashDecayStep` lặp nhiều frame | `i→0`, màu → nền (tắt dần) |

- [ ] **Step 3: Commit**

```bash
git add Assets/Horcrux/Runtime/Utilities/PhysXHelper/ColorFlash.cs
git commit -m "feat(physx): ColorFlash - FlashPulse + FlashDecayStep combos"
```

---

## Ghi chú thực thi

- **Hai kiểu dùng, hai vòng lặp:**
  - *Pulse (hit 1 phát):* lúc hit `elapsed=0f`; mỗi frame `elapsed += Time.deltaTime; sprite.color = ColorFlash.FlashPulse(baseCol, Color.white, elapsed/duration);` đến khi `elapsed≥duration` thì `sprite.color = baseCol`.
  - *Decay (nạp chồng):* giữ field `float intensity`; lúc hit `intensity = 1f`; mỗi frame `sprite.color = ColorFlash.FlashDecayStep(baseCol, Color.white, ref intensity, decay, Time.deltaTime);`. Bắn liên hoàn → mỗi viên đặt lại `intensity=1f`, flash tự chồng.
- **Chọn gamma hay linear:** mặc định gamma (rẻ, đủ đẹp cho flash ngắn). Bật `linear:true` khi flash mạnh/nền HDR cần độ sáng chuẩn (§0.2) — truyền **literal** để nhánh fold.
- **Giữ màu gốc:** caller cache `baseColor` **trước** khi flash (đừng đọc `sprite.color` đang flash làm nền → tích lũy sai). Flash luôn blend từ nền gốc, không từ màu frame trước (trừ chủ đích).
- **Chỉ sinh `Color`, không áp thẳng:** trả `Color`; caller gán `SpriteRenderer.color`/`Image.color`/`material.SetColor`. Ghép flash với squash/shake/hitstop là orchestrator tầng 4 (`SatisfyingClear` = `ColorFlash` + `StaggerHelper` + suck-in + burst).
- **`MaterialPropertyBlock` cho nhiều instance:** flash hàng loạt sprite dùng chung material → set qua `MaterialPropertyBlock` (khỏi tạo material instance, zero-GC) — ngoài phạm vi plan (class chỉ sinh giá trị).
- **File `.meta`:** Unity tự sinh khi import — commit kèm nếu repo giữ GUID ổn định.
- **Kiểm chứng:** chạy tay qua script tạm hoặc nhẩm theo kiểm mốc §0.5 — **không** tạo file test. Xóa script tạm trước khi commit.
- **Muốn test tự động sau này:** dựng NUnit EditMode theo bảng kiểm chứng (đặc biệt: độc-lập-framerate `1×dt = 2×½dt`; half-life `DecayEnvelope(1,λ,ln2/λ)=0.5`; pulse mốc `e(0)=e(1)=0,e(peak)=1`; linear midtone `≈0.735`) — ngoài phạm vi plan này.
- **Cập nhật roadmap:** sau khi xong, đánh dấu `ColorFlash` ✅ trong `Pendings.md` (Tầng 2, mục 16) — là nguyên liệu cho `SatisfyingClear` (#42, orchestrator tầng 4).
