# DampedOscillator Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.
>
> **Plan này là artifact học tập + triển khai, tự chứa.** Người dùng tự code lại để rèn tư duy → §0 dẫn giải **đầy đủ toán học** (biến đổi + lý do dùng công thức), các task chứa **code hoàn chỉnh** kèm chú thích self-doc & lý do tối ưu. Không code test → mỗi task có **checklist kiểm chứng** (kiểm mốc toán học).

**Goal:** `DampedOscillator` — dao động tắt dần dạng static, stateless, thuần `float`: li độ + vận tốc + bao hình + thời gian ổn định, hai kiểu tham số tắt (`decay λ` / `halfLife`).

**Architecture:** 1 file `DampedOscillator.cs`. Công thức lõi là **HarmonicOscillator × bao mũ `e^(−λt)`** — closed-form, không lặp, không giải ODE nhiều nhánh như SpringDamper. Tái dùng `enum WaveStyle` (Sin/Cos) đã có trong `HarmonicOscillator.cs`.

**Tech Stack:** C# (Unity), `Unity.Mathematics` (`math.exp/sin/cos/log/PI`) — nhất quán với `Interpolator.cs`/`SpringSolver`. Thuần toán — không Addressables/UniTask.

## Global Constraints

| Ràng buộc | Giá trị |
|---|---|
| Namespace | `Horcrux.Runtime.Utilities.PhysXHelper` |
| Tầng phụ thuộc | Tầng 1, `← HarmonicOscillator`. **Không** đụng SpringDamper (`Pendings.md` §Tầng 1) |
| Zero-GC | thuần `float`; không `new` reference-type, LINQ, closure, string; stateless (không field) |
| SOLID | 1 class = 1 trách nhiệm (dao động tắt dần); mở rộng qua overload/`WaveStyle`, không sửa hàm cũ |
| Self-doc | tên nói rõ mục đích (`GetEnvelope`≠`Process`); XML doc kèm công thức + "tại sao" ở mọi hàm public |
| Tái dùng | `WaveStyle` enum lấy từ `HarmonicOscillator.cs` — **không** định nghĩa lại |
| Công thức chuẩn | `x(t) = A·e^(−λt)·cos(ωt+φ)` (hoặc sin), `ω = 2π·f` |
| Guard biên | `decay ≤ 0` → không tắt (suy về HarmonicOscillator); `halfLife ≤ 0` → coi như đã ổn định |

---

## §0. Nền toán học (đọc trước khi code)

> Mục tiêu: hiểu **tại sao** từng công thức. Hệ này nhẹ — chỉ **một** công thức đóng + đạo hàm của nó. Đọc xong bạn tự dựng lại được cả file.

### 0.1. Bản chất — dao động tắt dần là gì

Dao động điều hòa thường (`HarmonicOscillator`) lắc **mãi mãi** với biên độ không đổi `A`. Thực tế luôn có ma sát/lực cản → biên độ **rũ nhỏ dần** rồi tắt hẳn. Đó là dao động tắt dần: dây đàn gảy rồi im, jelly rung sau va chạm, UI nảy vào rồi đứng yên.

| Thành phần | Vai trò |
|---|---|
| **Dao động** `cos(ωt+φ)` | phần lắc qua lại — quyết định "nhịp" (tần số `f`) |
| **Bao hình** `e^(−λt)` | phần **co biên độ** theo thời gian — quyết định "tắt nhanh/chậm" (`λ`) |
| **Biên độ đầu** `A` | độ lớn lúc `t=0` |

**Điểm cốt lõi:** `x(t) = A·e^(−λt)·cos(ωt+φ)` = **HarmonicOscillator × bao `e^(−λt)`**. Bỏ bao (`λ=0` → `e^0=1`) thì quay về đúng dao động điều hòa.

### 0.2. Nguyên lý — vì sao biên độ tắt theo **hàm mũ**, không tuyến tính

Trực giác trước: lực cản (ma sát nhớt) tỉ lệ **vận tốc** → mỗi chu kỳ, hệ mất đi một **tỉ lệ cố định** của biên độ (ví dụ mất 10% mỗi chu kỳ), không phải một lượng cố định.

"Mất cùng một **tỉ lệ** sau mỗi khoảng thời gian bằng nhau" chính là định nghĩa của **phân rã mũ** — giống lãi kép âm, giống chất phóng xạ. Hàm duy nhất có tính chất "đạo hàm tỉ lệ chính nó" là `e^{−λt}`:

$$\frac{d}{dt}e^{-\lambda t} = -\lambda\,e^{-\lambda t} \quad\Rightarrow\quad \text{tốc độ giảm luôn tỉ lệ giá trị hiện tại}$$

Nên biên độ co theo `e^(−λt)`, không theo đường thẳng. `λ` (1/s) = **tốc độ tắt**: lớn → co nhanh. Đây cũng đúng cơ chế `Interpolator.ExpDecay` (`§DecayFactor`) — cùng một họ hàm mũ.

### 0.3. Từ phương trình chi phối → nghiệm đóng (suy ra, không áp đặt)

§0.2 mới giải thích phần **bao** `e^(−λt)`. Còn *vì sao li độ = bao × cosin*? Suy ra từ phương trình dao động tắt dần (dao động điều hòa cộng lực cản ∝ vận tốc):

$$\ddot{x} + 2\lambda\,\dot{x} + \omega_0^2\,x = 0$$

trong đó `ω₀` = tần số tự nhiên (khi chưa cản), `2λ` = hệ số cản đã chuẩn hóa. Giải như mọi ODE tuyến tính — thử `x = e^{rt}` → phương trình đặc trưng `r² + 2λr + ω₀² = 0`:

$$r = -\lambda \pm \sqrt{\lambda^2 - \omega_0^2}$$

**Trường hợp có dao động** (cản yếu, `λ < ω₀`): dưới căn **âm** → nghiệm **phức** `r = −λ ± iω_d`, với **tần số quan sát** `ω_d ≡ √(ω₀² − λ²)`. Qua công thức Euler `e^{iθ}=cosθ+isinθ` (dẫn giải đầy đủ ở §0.4 của plan SpringDamper cùng thư mục), tổ hợp hai mũ phức liên hợp rút gọn thành lượng giác:

$$x(t) = \underbrace{e^{-\lambda t}}_{\text{bao — §0.2}}\big[A_1\cos(\omega_d t) + A_2\sin(\omega_d t)\big] = A\,e^{-\lambda t}\cos(\omega_d t + \varphi)$$

→ **li độ = bao × cosin** giờ được *suy ra*: phần bao `e^(−λt)` đúng như §0.2, phần lắc là cosin ở tần số `ω_d`. (Chọn `sin` thay `cos` chỉ là đổi mốc pha `φ`.)

> **Lựa chọn mô hình — vì sao code tách rời `f` và `λ`:** trong ODE thật, `ω_d = √(ω₀²−λ²)` **ràng buộc** tần số theo độ tắt (tắt càng mạnh → lắc càng chậm, và `λ ≥ ω₀` thì hết dao động). Nhưng cho **game feel**, designer muốn vặn *nhịp lắc* và *độ tắt* **độc lập**. Nên ta phơi thẳng tần số **quan sát** `f` (đặt `ω = 2π·f`, đóng vai `ω_d`) và để `λ` là núm riêng — một mô hình **mô tả** (giống `HarmonicOscillator` phơi `f` trực tiếp), không mô phỏng khối lượng–lò xo. Ai cần đúng vật lý ràng buộc `m,k,c` thì dùng `SpringDamper`. Từ đây gọi tần số góc là `ω` cho gọn.

**Công thức chốt** (dạng code dùng — `ω` là tần số góc quan sát):

$$\boxed{\;x(t) = A\,e^{-\lambda t}\cos(\omega t + \varphi)\;}\quad(\text{Cos})$$
$$\boxed{\;x(t) = A\,e^{-\lambda t}\sin(\omega t + \varphi)\;}\quad(\text{Sin})$$

| Ký hiệu | Tên | Đơn vị | Ghi chú |
|---|---|---|---|
| `A` | biên độ đầu | — | độ lớn tại `t=0` (trước khi bao co) |
| `λ` (lambda) | hệ số tắt (decay) | 1/s | lớn → tắt nhanh; `λ=0` → không tắt |
| `ω` (omega) | tần số **góc** quan sát | rad/s | `ω = 2π·f`; đóng vai `ω_d`, tách rời `λ` (xem hộp trên) |
| `f` | tần số thường | Hz | số lần lắc trọn mỗi giây — núm designer vặn |
| `φ` (phi) | pha ban đầu | rad | dịch điểm bắt đầu của sóng |
| `t` | thời gian | s | kể từ lúc bắt đầu |

**Quy đổi `halfLife` ↔ `decay`.** Designer thường nghĩ "sau bao lâu biên độ còn **một nửa**" (nửa đời `h`) hơn là con số `λ` trừu tượng. Đặt `e^{-\lambda h} = \tfrac{1}{2}` rồi lấy `ln` hai vế:

$$-\lambda h = \ln\tfrac{1}{2} = -\ln 2 \quad\Rightarrow\quad \boxed{\;\lambda = \frac{\ln 2}{h}\;}$$

Nhất quán với `Interpolator.ExpDecayHalfLife`. Vì vậy mỗi hàm có 2 overload: nhận `decay` trực tiếp, hoặc nhận `halfLife` rồi quy đổi.

### 0.4. Đạo hàm ra vận tốc (chỗ dễ "nhảy bước" nhất — giải kỹ)

Vận tốc là đạo hàm li độ theo thời gian: `ẋ = dx/dt`. Viết `x = A·E·f` với **bao hình** `E = e^{-\lambda t}` và **phần lắc** `f`. Dùng **quy tắc tích** $(E f)' = E'f + E f'$:

**① Đạo hàm bao hình** (mục 0.2 đã có):
$$E' = \frac{d}{dt}e^{-\lambda t} = -\lambda\,e^{-\lambda t} = -\lambda E$$

**② Đạo hàm phần lắc** (đạo hàm hàm hợp, `(\omega t+\varphi)' = \omega`):

| Kiểu | `f` | `f'` |
|---|---|---|
| Cos | $\cos(\omega t+\varphi)$ | $-\omega\sin(\omega t+\varphi)$ |
| Sin | $\sin(\omega t+\varphi)$ | $+\omega\cos(\omega t+\varphi)$ |

**③ Ghép lại** $(Ef)' = E'f + Ef' = -\lambda E f + E f' = E(f' - \lambda f)$, gộp `A·E` ra chung:

$$\boxed{\;\dot{x}_{\cos} = A\,e^{-\lambda t}\big[-\lambda\cos(\omega t+\varphi) - \omega\sin(\omega t+\varphi)\big]\;}$$
$$\boxed{\;\dot{x}_{\sin} = A\,e^{-\lambda t}\big[-\lambda\sin(\omega t+\varphi) + \omega\cos(\omega t+\varphi)\big]\;}$$

**Đọc công thức:** vận tốc = bao hình × (phần "trôi xuống do tắt" `−λ·(phần lắc)` + phần "lắc" `±ω·(phần vuông pha)`). Khi `λ=0` → chỉ còn phần lắc → đúng vận tốc dao động điều hòa.

### 0.5. Bao hình & thời gian ổn định

**Bao hình** tách riêng (dùng để vẽ vùng bao, hoặc điều chỉnh alpha theo độ tắt):
$$E(t) = A\,e^{-\lambda t}$$

**Thời gian ổn định `t*`** — khi nào coi như "đã dừng" để tắt update (tiết kiệm CPU). Định nghĩa: bao rơi xuống dưới tỉ lệ `threshold` so với `A` (vd 2%). Giải `e^{-\lambda t^*} = threshold`:

$$-\lambda t^* = \ln(threshold) \quad\Rightarrow\quad \boxed{\;t^* = \frac{-\ln(threshold)}{\lambda}\;}$$

`threshold ∈ (0,1)`, `ln(threshold) < 0` → tử `−ln(threshold) > 0` → `t* > 0` hợp lý. `λ → 0` → `t* → ∞` (không bao giờ tắt).

### 0.6. Kiểm mốc (xác nhận công thức đúng trước khi code)

| Mốc | Kỳ vọng | Kiểm |
|---|---|---|
| `t=0`, Cos, `φ=0` | `x = A·1·cos0 = A` (bắt đầu ở đỉnh) | ✓ |
| `t=0`, Sin, `φ=0` | `x = A·1·sin0 = 0` (bắt đầu ở cân bằng) | ✓ |
| `t→∞`, `λ>0` | `e^(−λt)→0` → `x→0` (tắt hẳn) | ✓ |
| `λ=0` | `e^0=1` → `x = A·cos(ωt+φ)` = HarmonicOscillator | ✓ |
| `ẋ(0)`, Cos, `φ=0` | `A(−λ·1 − ω·0) = −λA` (đang trôi xuống từ đỉnh) | ✓ |
| `ẋ(0)`, Sin, `φ=0` | `A(−λ·0 + ω·1) = ωA` (đang bật lên từ 0) | ✓ |
| `E(0)` | `A·e^0 = A` | ✓ |
| `t* @ threshold=0.5, λ=ln2` | `−ln(0.5)/ln2 = ln2/ln2 = 1s` | ✓ |

---

## Bản đồ triển khai

```
PhysXHelper/
└── DampedOscillator.cs   1 file, 3 task tăng dần
     ├── Task 1  GetDisplacement + GetEnvelope   (decay)   §0.3, §0.5
     ├── Task 2  GetVelocity + GetSettlingTime    (decay)   §0.4, §0.5
     └── Task 3  overload *HalfLife (×4) + const Ln2         §0.3 (quy đổi)
```
Thứ tự: **1 → 2 → 3**. Task 2 & 3 *modify* file Task 1 tạo (thêm hàm, không sửa hàm cũ → Open/Closed).

---

### Task 1: `GetDisplacement` + `GetEnvelope` — lõi dao động tắt dần

**Files:**
- Create: `Assets/Horcrux/Runtime/Utilities/PhysXHelper/DampedOscillator.cs`

**Interfaces:**
- Consumes: `enum WaveStyle { Sin, Cos }` — đã có sẵn trong `HarmonicOscillator.cs` (cùng namespace).
- Produces:
  - `static float GetEnvelope(float decay, float time, float amplitude = 1f)`
  - `static float GetDisplacement(WaveStyle waveStyle, float frequency, float decay, float time, float amplitude = 1f, float phaseShift = 0f)`

**Bản đồ toán → code:** §0.3 (công thức chốt) · §0.5 (bao hình) · §0.2 (guard `λ≤0` → không tắt).

**Self-doc & tối ưu:**

| Quyết định | Lý do |
|---|---|
| `static`, thuần `float`, không field | stateless, zero-GC, thread-safe, dễ test |
| `GetDisplacement` gọi `GetEnvelope` | DRY — bao hình tính 1 chỗ, dùng lại ở displacement/velocity |
| guard `decay > 0 ? … : amplitude` trong `GetEnvelope` | `λ≤0` suy về dao động điều hòa (e=1), không gọi `exp` vô ích |
| guard `time > 0` trong `GetEnvelope` | tránh `exp(−∞·0)=NaN` khi Task 3 truyền `λ` rất lớn; `t≤0` → `e^0=1` |
| `math.exp/sin/cos/PI` (Unity.Mathematics) | Burst-friendly, thuần `float`, nhất quán `Interpolator`/`SpringSolver` |
| `WaveStyle` tái dùng | không định nghĩa lại enum đã có |

- [ ] **Step 1: Tạo file với code Task 1**

```csharp
using System.Runtime.CompilerServices;
using Unity.Mathematics;

namespace Horcrux.Runtime.Utilities.PhysXHelper
{
    /// <summary>
    /// Dao động tắt dần (damped harmonic oscillation): dao động điều hòa nhân thêm
    /// bao mũ e^(−λt) → biên độ rũ dần về 0. Static, stateless, zero-GC.
    /// </summary>
    /// <remarks>
    /// Công thức: x(t) = A·e^(−λt)·cos(ωt+φ)  (hoặc sin), với ω = 2π·f.
    /// Là <see cref="HarmonicOscillator"/> khoác bao tắt dần — λ=0 thì suy về dao động điều hòa.
    /// decay λ (1/s): lớn → tắt nhanh. Overload *HalfLife: λ = ln2/halfLife.
    /// </remarks>
    public static class DampedOscillator
    {
        /// <summary>Biên độ bao hiện tại E = A·e^(−λt) — đường ôm trên/dưới của dao động (§0.5).</summary>
        /// <param name="decay">Hệ số tắt λ (1/s). ≤ 0 → không tắt → trả A.</param>
        /// <param name="time">Thời gian t (giây).</param>
        /// <param name="amplitude">Biên độ ban đầu A.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float GetEnvelope(float decay, float time, float amplitude = 1f)
        {
            if (decay <= 0f) return amplitude; // không tắt (suy về dao động điều hòa)
            if (time <= 0f) return amplitude;  // e^0 = 1; chặn NaN khi decay rất lớn (Task 3)
            return amplitude * math.exp(-decay * time); // E = A·e^(−λt)
        }

        /// <summary>
        /// Li độ tức thời của dao động tắt dần (§0.3).
        /// </summary>
        /// <remarks>Formula: x = A·e^(−λt)·cos(ωt+φ) [Cos] | A·e^(−λt)·sin(ωt+φ) [Sin]. ω = 2π·f.</remarks>
        /// <param name="waveStyle">Cos: bắt đầu ở +A (hợp "thả ra rồi rũ về"). Sin: bắt đầu ở 0 (cân bằng).</param>
        /// <param name="frequency">Tần số thường f (Hz). Dùng tính ω = 2π·f.</param>
        /// <param name="decay">Hệ số tắt λ (1/s). ≤ 0 → không tắt (suy về HarmonicOscillator).</param>
        /// <param name="time">Thời gian t (giây) kể từ lúc bắt đầu.</param>
        /// <param name="amplitude">Biên độ ban đầu A.</param>
        /// <param name="phaseShift">Pha ban đầu φ (radian).</param>
        /// <returns>Li độ tại t, nằm trong bao [−A·e^(−λt), +A·e^(−λt)].</returns>
        public static float GetDisplacement(
            WaveStyle waveStyle, float frequency, float decay, float time,
            float amplitude = 1f, float phaseShift = 0f)
        {
            float omega = 2f * math.PI * frequency;               // ω = 2π·f  (§0.3)
            float envelope = GetEnvelope(decay, time, amplitude); // A·e^(−λt)
            float phase = omega * time + phaseShift;              // ωt + φ

            return waveStyle == WaveStyle.Sin
                ? envelope * math.sin(phase)
                : envelope * math.cos(phase);
        }
    }
}
```

- [ ] **Step 2: Kiểm chứng (chạy tay / nhẩm theo §0.6)**

| Input | Kỳ vọng |
|---|---|
| `GetDisplacement(Cos, 1, 1, 0)` | `= 1` (A·cos0) |
| `GetDisplacement(Sin, 1, 1, 0)` | `= 0` (A·sin0) |
| `GetDisplacement(Cos, 1, 1, 100)` | `≈ 0` (bao đã tắt) |
| `GetDisplacement(Cos, 1, 0, t)` | `== HarmonicOscillator.GetHarmonicDisplacement(Cos, 1, t)` (λ=0) |
| `GetEnvelope(1, 0)` | `= 1` |
| `GetEnvelope(0.6931472, 1)` | `≈ 0.5` (`e^(−ln2)`) |
| `GetEnvelope(-5, 3)` | `= 1` (không tắt) |

Unity biên dịch sạch (Console không lỗi).

- [ ] **Step 3: Commit**

```bash
git add Assets/Horcrux/Runtime/Utilities/PhysXHelper/DampedOscillator.cs
git commit -m "feat(physx): DampedOscillator - GetDisplacement + GetEnvelope (decay)"
```

---

### Task 2: `GetVelocity` + `GetSettlingTime` — đạo hàm & ngưỡng dừng

**Files:**
- Modify: `Assets/Horcrux/Runtime/Utilities/PhysXHelper/DampedOscillator.cs` (thêm 2 hàm vào class, không sửa hàm cũ)

**Interfaces:**
- Consumes: `GetEnvelope(float, float, float)` — Task 1; `WaveStyle`.
- Produces:
  - `static float GetVelocity(WaveStyle waveStyle, float frequency, float decay, float time, float amplitude = 1f, float phaseShift = 0f)`
  - `static float GetSettlingTime(float decay, float threshold = 0.02f)`

**Bản đồ toán → code:** `GetVelocity` = 2 hộp công thức §0.4 · `GetSettlingTime` = §0.5.

**Self-doc & tối ưu:**

| Quyết định | Lý do |
|---|---|
| `GetVelocity` gọi lại `GetEnvelope` | DRY — cùng bao hình với displacement |
| `lambda = decay > 0 ? decay : 0` | khớp guard envelope: `λ≤0` → mất số hạng `−λ·(lắc)`, còn phần dao động |
| tính `math.sin/cos(phase)` 1 lần, gán `s,c` | không gọi lượng giác 2 lần |
| `GetSettlingTime` guard `decay≤0`/`threshold` biên | `λ≤0` → `+∞` (không dừng); `threshold≥1` → 0 (đã dưới ngưỡng) |
| `math.log` = ln tự nhiên | đúng cơ số của `e^(−λt)` |

- [ ] **Step 1: Thêm code Task 2 vào class `DampedOscillator`** (đặt sau `GetDisplacement`)

```csharp
        /// <summary>
        /// Vận tốc tức thời (đạo hàm li độ theo thời gian) của dao động tắt dần (§0.4).
        /// </summary>
        /// <remarks>
        /// Cos: ẋ = A·e^(−λt)·[−λ·cos(ωt+φ) − ω·sin(ωt+φ)] <br/>
        /// Sin: ẋ = A·e^(−λt)·[−λ·sin(ωt+φ) + ω·cos(ωt+φ)] <br/>
        /// Từ quy tắc tích (E·f)' = E'f + Ef', với E' = −λE.
        /// </remarks>
        /// <param name="waveStyle">Phải khớp waveStyle dùng cho displacement.</param>
        /// <param name="frequency">Tần số thường f (Hz). ω = 2π·f.</param>
        /// <param name="decay">Hệ số tắt λ (1/s). ≤ 0 → không tắt.</param>
        /// <param name="time">Thời gian t (giây).</param>
        /// <param name="amplitude">Biên độ ban đầu A.</param>
        /// <param name="phaseShift">Pha ban đầu φ (radian).</param>
        /// <returns>Vận tốc tại t (đơn vị: đơn vị-của-A trên giây).</returns>
        public static float GetVelocity(
            WaveStyle waveStyle, float frequency, float decay, float time,
            float amplitude = 1f, float phaseShift = 0f)
        {
            float lambda = decay > 0f ? decay : 0f;               // λ (khớp guard envelope)
            float omega = 2f * math.PI * frequency;               // ω = 2π·f
            float envelope = GetEnvelope(decay, time, amplitude); // A·e^(−λt)
            float phase = omega * time + phaseShift;
            float c = math.cos(phase);
            float s = math.sin(phase);

            return waveStyle == WaveStyle.Sin
                ? envelope * (-lambda * s + omega * c)  // §0.4 Sin
                : envelope * (-lambda * c - omega * s); // §0.4 Cos
        }

        /// <summary>
        /// Thời gian để bao biên độ rơi dưới tỉ lệ threshold so với A → coi như đã dừng.
        /// Dùng để tắt update, tiết kiệm CPU (§0.5).
        /// </summary>
        /// <remarks>Formula: t* = −ln(threshold)/λ. Giải từ e^(−λt*) = threshold.</remarks>
        /// <param name="decay">Hệ số tắt λ (1/s). ≤ 0 → không bao giờ dừng → +∞.</param>
        /// <param name="threshold">Ngưỡng tỉ lệ trong (0,1), vd 0.02 = còn 2% biên độ đầu.</param>
        /// <returns>Thời gian ổn định t* (giây), hoặc +∞ nếu không hội tụ.</returns>
        public static float GetSettlingTime(float decay, float threshold = 0.02f)
        {
            if (decay <= 0f) return float.PositiveInfinity;    // không tắt
            if (threshold <= 0f) return float.PositiveInfinity; // không bao giờ về 0 tuyệt đối
            if (threshold >= 1f) return 0f;                    // đã dưới ngưỡng ngay từ đầu
            return -math.log(threshold) / decay;              // t* = −ln(threshold)/λ
        }
```

- [ ] **Step 2: Kiểm chứng — kiểm mốc & đạo hàm số (§0.6)**

| Input | Kỳ vọng |
|---|---|
| `GetVelocity(Cos, 1, 2, 0)` | `= −2` (`−λA`, A=1) |
| `GetVelocity(Sin, 1, 2, 0)` | `= 2π ≈ 6.283` (`ωA`) |
| `GetVelocity(Cos, f, decay, t)` | `≈ (GetDisplacement(t+h) − GetDisplacement(t−h)) / 2h`, `h=1e-4` (đạo hàm số khớp) |
| `GetSettlingTime(0.6931472, 0.5)` | `= 1` (`−ln0.5/ln2`) |
| `GetSettlingTime(0)` | `= +∞` |
| `GetSettlingTime(5, 2)` | `= 0` (threshold≥1) |

- [ ] **Step 3: Commit**

```bash
git add Assets/Horcrux/Runtime/Utilities/PhysXHelper/DampedOscillator.cs
git commit -m "feat(physx): DampedOscillator - GetVelocity + GetSettlingTime (decay)"
```

---

### Task 3: overload `*HalfLife` (×4) — tham số hóa theo nửa đời

**Files:**
- Modify: `Assets/Horcrux/Runtime/Utilities/PhysXHelper/DampedOscillator.cs` (thêm const + 4 overload, không sửa hàm cũ)

**Interfaces:**
- Consumes: `GetDisplacement`, `GetVelocity`, `GetEnvelope`, `GetSettlingTime` — Task 1–2; `WaveStyle`.
- Produces:
  - `static float GetDisplacementHalfLife(WaveStyle waveStyle, float frequency, float halfLife, float time, float amplitude = 1f, float phaseShift = 0f)`
  - `static float GetVelocityHalfLife(WaveStyle waveStyle, float frequency, float halfLife, float time, float amplitude = 1f, float phaseShift = 0f)`
  - `static float GetEnvelopeHalfLife(float halfLife, float time, float amplitude = 1f)`
  - `static float GetSettlingTimeHalfLife(float halfLife, float threshold = 0.02f)`

**Bản đồ toán → code:** §0.3 quy đổi `λ = ln2/halfLife` → tất cả delegate về bản `decay` (Task 1–2). **Không toán mới.**

**Self-doc & tối ưu:**

| Quyết định | Lý do |
|---|---|
| overload delegate về bản `decay` | DRY tuyệt đối — 1 chỗ chứa công thức, overload chỉ quy đổi tham số |
| const `Ln2` (`static readonly`-style `const`) | hằng biết trước → không gọi `math.log(2)` mỗi call |
| guard `halfLife ≤ 0` → coi như đã ổn định | `h≤0` = tắt tức thì; trả 0 (displacement/velocity/envelope) hoặc 0 (settling) — tránh `+∞`/NaN |
| `AggressiveInlining` cho wrapper mỏng | overload chỉ 1 phép chia + gọi hàm → nội tuyến khỏi phí gọi |

- [ ] **Step 1: Thêm const `Ln2` đầu class** (ngay sau dòng `public static class DampedOscillator {`)

```csharp
        private const float Ln2 = 0.6931472f; // ln 2 — quy đổi halfLife → decay: λ = ln2/h (§0.3)
```

- [ ] **Step 2: Thêm 4 overload `*HalfLife` vào cuối class**

```csharp
        // ── Overload theo halfLife (giây): λ = ln2/halfLife (§0.3) ──────────────
        // halfLife = thời gian biên độ giảm còn một nửa. Trực quan hơn decay khi tune.
        // halfLife ≤ 0 → tắt tức thì → coi như đã ổn định (trả 0), tránh +∞/NaN.

        /// <summary><see cref="GetDisplacement"/> tham số theo nửa đời halfLife thay decay.</summary>
        /// <param name="halfLife">Thời gian (giây) biên độ giảm còn một nửa. ≤ 0 → coi như đã tắt.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float GetDisplacementHalfLife(
            WaveStyle waveStyle, float frequency, float halfLife, float time,
            float amplitude = 1f, float phaseShift = 0f)
            => halfLife <= 0f
                ? 0f
                : GetDisplacement(waveStyle, frequency, Ln2 / halfLife, time, amplitude, phaseShift);

        /// <summary><see cref="GetVelocity"/> tham số theo nửa đời halfLife thay decay.</summary>
        /// <param name="halfLife">Thời gian (giây) biên độ giảm còn một nửa. ≤ 0 → coi như đã tắt.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float GetVelocityHalfLife(
            WaveStyle waveStyle, float frequency, float halfLife, float time,
            float amplitude = 1f, float phaseShift = 0f)
            => halfLife <= 0f
                ? 0f
                : GetVelocity(waveStyle, frequency, Ln2 / halfLife, time, amplitude, phaseShift);

        /// <summary><see cref="GetEnvelope"/> tham số theo nửa đời halfLife thay decay.</summary>
        /// <param name="halfLife">Thời gian (giây) biên độ giảm còn một nửa. ≤ 0 → coi như đã tắt.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float GetEnvelopeHalfLife(float halfLife, float time, float amplitude = 1f)
            => halfLife <= 0f ? 0f : GetEnvelope(Ln2 / halfLife, time, amplitude);

        /// <summary><see cref="GetSettlingTime"/> tham số theo nửa đời halfLife thay decay.</summary>
        /// <param name="halfLife">Thời gian (giây) biên độ giảm còn một nửa. ≤ 0 → 0 (đã tắt).</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float GetSettlingTimeHalfLife(float halfLife, float threshold = 0.02f)
            => halfLife <= 0f ? 0f : GetSettlingTime(Ln2 / halfLife, threshold);
```

- [ ] **Step 3: Kiểm chứng — quy đổi & tương đương bản decay**

| Input | Kỳ vọng |
|---|---|
| `GetEnvelopeHalfLife(1, 1)` | `≈ 0.5` (sau đúng 1 nửa đời còn nửa biên độ) |
| `GetEnvelopeHalfLife(1, 2)` | `≈ 0.25` (2 nửa đời) |
| `GetDisplacementHalfLife(Cos, 2, 1, t)` | `== GetDisplacement(Cos, 2, 0.6931472, t)` (cùng λ) |
| `GetSettlingTimeHalfLife(1, 0.5)` | `= 1` (`t*` = 1 nửa đời tại threshold 0.5) |
| `GetDisplacementHalfLife(Cos, 2, 0, 1)` | `= 0` (halfLife≤0 → đã tắt) |
| `GetSettlingTimeHalfLife(-1)` | `= 0` |

Unity biên dịch sạch.

- [ ] **Step 4: Commit**

```bash
git add Assets/Horcrux/Runtime/Utilities/PhysXHelper/DampedOscillator.cs
git commit -m "feat(physx): DampedOscillator - overload *HalfLife (x4)"
```

---

## Ghi chú thực thi

- **Thứ tự:** 1 → 2 → 3 (mỗi task thêm hàm vào cùng file, không sửa hàm cũ → Open/Closed).
- **`WaveStyle`:** dùng enum có sẵn trong `HarmonicOscillator.cs` — **không** tạo lại (trùng tên trong cùng namespace sẽ lỗi biên dịch).
- **File `.meta`:** Unity tự sinh khi import — commit kèm nếu repo giữ GUID ổn định.
- **Kiểm chứng:** chạy tay qua script tạm hoặc nhẩm theo kiểm mốc §0.6 — **không** tạo file test. Xóa script tạm trước khi commit.
- **Muốn test tự động sau này:** dựng NUnit EditMode theo các bảng kiểm chứng mỗi task (đặc biệt: đạo hàm số khớp `GetVelocity`, tương đương `*HalfLife` ↔ `decay`) — ngoài phạm vi plan này.
- **Cập nhật roadmap:** sau khi xong, đánh dấu `DampedOscillator` ✅ trong `Pendings.md` (Tầng 1, mục 8) — mở khóa `Wobble`/`Jelly` và `GranularSettle`.
```