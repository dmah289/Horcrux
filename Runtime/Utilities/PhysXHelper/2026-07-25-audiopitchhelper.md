# AudioPitchHelper Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.
>
> **Plan này là artifact học tập + triển khai, tự chứa.** Người dùng tự code lại để rèn tư duy → §0 dẫn giải **đầy đủ toán học** (vì sao nhân chứ không cộng, biến đổi semitone→ratio), các task chứa **code hoàn chỉnh** kèm chú thích self-doc & lý do tối ưu. Không code test → mỗi task có **checklist kiểm chứng** (kiểm mốc + round-trip).

**Goal:** `AudioPitchHelper` — biến điệu cao độ (pitch) dạng static, stateless, thuần tính toán: quy khoảng cách âm nhạc (semitone / cents) về **hệ số nhân pitch** cho `AudioSource.pitch`, cộng 2 mapping ứng dụng (pitch ramp combo, random detune chống lặp âm).

**Architecture:** 1 file `AudioPitchHelper.cs`. Lõi là **một** phép chuyển `ratio = 2^(semitones/12)` (suy từ 12 bậc đều — equal temperament) + nghịch đảo + biến thể cents. Hai helper ứng dụng (ramp / detune) đều **sinh ra `semitones`** rồi quy về lõi → không lặp công thức.

**Tech Stack:** C# (Unity), `Unity.Mathematics` (`math.exp2/log2/min/max/clamp`) — nhất quán `Interpolator.cs`. Thuần toán — không Addressables/UniTask. (`pow/log` cố ý tránh — xem §0.3.)

## Global Constraints

| Ràng buộc | Giá trị |
|---|---|
| Namespace | `Horcrux.Runtime.Utilities.PhysXHelper` |
| Tầng phụ thuộc | Tầng 2, mục 18. **Thực tế độc lập** — chỉ `Unity.Mathematics`, không gọi helper khác. (`← InterpolationHelper` trong `Pendings.md` chỉ cho remap cường độ → thuộc `AudioFeedback`, không dùng ở đây.) |
| Zero-GC | thuần `float`/`int` (stack); không `new` reference-type, LINQ, closure, string; stateless (không field) |
| SOLID | 1 class = 1 trách nhiệm (quy đổi cao độ → hệ số pitch, **không** phát/điều khiển AudioSource); mở rộng qua overload, không sửa hàm cũ |
| Self-doc | tên nói rõ mục đích (`SemitonesToRatio`≠`Convert`); XML doc kèm công thức + "tại sao" ở mọi hàm public |
| Nguồn ngẫu nhiên | **tách khỏi toán** — detune nhận `signedUnit ∈ [−1,1]` do caller cấp (vd `Random.Range(-1f,1f)`); hàm thuần, không tự sinh random |
| Công thức chuẩn | `ratio = 2^(semitones/12)`; nghịch đảo `semitones = 12·log₂(ratio)`; cents `ratio = 2^(cents/1200)` |
| Guard biên | `ratio ≤ 0` → kẹp `MinPositiveRatio` (chặn `log₂` ra NaN/−∞); `signedUnit` kẹp `[−1,1]`; ramp có trần `maxSemitones` |

---

## §0. Nền toán học (đọc trước khi code)

> Mục tiêu: hiểu **tại sao** pitch phải *nhân* chứ không *cộng*, và vì sao từ đó ra `2^(n/12)` / `log₂`. Đọc §0.1→0.4 theo thứ tự: hiện tượng → công thức lõi → biến thể → ứng dụng.

### 0.1. Bản chất — cao độ cảm nhận theo *log* tần số

Định luật Weber–Fechner: giác quan cảm nhận **theo tỉ lệ** kích thích, không theo hiệu. Với thính giác → cao độ cảm nhận ∝ **log tần số**. Hệ quả: hai nốt nghe cách nhau **cùng một quãng** (interval) khi tần số của chúng **cùng một tỉ lệ**, bất kể cao thấp:

| Hiện tượng | Tần số | Tỉ lệ | Cảm nhận |
|---|---|---|---|
| A4 → A5 | 440 → 880 | ×2 | lên 1 octave |
| A5 → A6 | 880 → 1760 | ×2 | **cùng** 1 octave |
| +100 Hz ở trầm | 100 → 200 | ×2 | 1 octave |
| +100 Hz ở cao | 1000 → 1100 | ×1.1 | quãng nhỏ xíu |

→ **Cộng cùng lượng Hz nghe KHÔNG đều** (cột 4 khác nhau); **nhân cùng tỉ lệ nghe đều**. Hai hệ quả trực tiếp: quãng = phép **nhân** ratio (→ §0.2), và đo quãng = phép **log** (→ nghịch đảo §0.3).

| Thành phần | Vai trò |
|---|---|
| **Octave** (quãng tám) | đơn vị gốc: tần số **×2** |
| **Semitone** (nửa cung) | octave chia **12 bậc đều nhau theo tỉ lệ** (equal temperament) |
| **Cents** | semitone chia tiếp **100** phần → 1 octave = 1200 cents (detune tinh vi) |
| **ratio** | hệ số nhân đặt thẳng vào `AudioSource.pitch` (1 = gốc, 2 = cao 1 octave) |

**`AudioSource.pitch` là hệ số tốc độ phát** (1 = bình thường). Nhân pitch với `ratio` = dịch giọng lên/xuống đúng `ratio` đó → mọi helper ở đây trả về **hệ số nhân**, caller gán `source.pitch = basePitch * ratio`.

### 0.2. Nguyên lý — suy `ratio = 2^(n/12)` từ 12 bậc đều

"12 bậc **đều nhau theo tỉ lệ**" (§0.1) nghĩa là mỗi semitone nhân thêm **cùng** một hằng `q`. Đi hết 12 semitone = 1 octave = ×2:

$$q^{12} = 2 \quad\Rightarrow\quad q = 2^{1/12}$$

Đi `n` semitone = nhân `q` đúng `n` lần:

$$\boxed{\;r = q^{n} = 2^{\,n/12}\;}$$

**Kiểm tái lập:** `q^12 = (2^(1/12))^12 = 2^1 = 2` ✓ đúng 1 octave. `n=0→r=1` (giữ nguyên), `n>0→r>1` (lên), `n<0→r<1` (xuống).

> **Lựa chọn mô hình — equal temperament (ET), không just intonation:** ET chia octave thành 12 phần **bằng nhau theo tỉ lệ** (`q` hằng). Quãng "thuần" vật lý (just intonation) lại là **tỉ số nguyên nhỏ** — vd quãng năm thuần `3/2 = 1.5`, còn ET cho `2^(7/12) ≈ 1.4983` (lệch ~1.96 cents). Chọn ET vì: (1) pitch-shift chỉ cần **một** `ratio` cho mọi nốt, không cần bảng tỉ số theo từng quãng; (2) đồng đều mọi phía nên detune/ramp đối xứng đẹp. Just intonation chỉ cần khi làm nhạc cụ ảo đòi hòa âm thuần — ngoài phạm vi game feel.

### 0.3. Công thức đầy đủ + nghịch đảo + cents (và vì sao dùng `exp2`/`log2`)

| Chuyển đổi | Công thức | Phép rẻ tương đương |
|---|---|---|
| semitone → ratio | $r = 2^{\,n/12}$ | `math.exp2(n / 12)` |
| ratio → semitone (nghịch đảo) | $n = 12\log_2 r$ | `12 * math.log2(r)` |
| cents → ratio | $r = 2^{\,c/1200}$ | `math.exp2(c / 1200)` |

- **`exp2(x) = 2^x` / `log2` là intrinsic cơ số 2 trực tiếp** — 1 lời gọi, không ghép `pow(2,·)` (tổng quát, chậm hơn) hay `log(·)/log(2)` (2 lời gọi + 1 chia + sai số phụ). Nhất quán `Interpolator.cs` (đã dùng `math.exp2`).
- **Chia `/12`, `/1200` là hằng compile-time** → precompute nghịch đảo (`1/12`, `1/1200`) rồi **nhân** trong hàm (§Task 1), không chia lúc chạy.
- **Cents chỉ là semitone thang mịn** (100 cents = 1 semitone): `2^(c/1200) = 2^((c/100)/12)`. Giữ hàm riêng `CentsToRatio` cho rõ ý đồ gọi, vẫn 1 lời gọi `exp2`.

**Nghịch đảo `12·log₂r`:** giải `r = 2^(n/12)` ngược lấy `n` → `log₂r = n/12` → `n = 12·log₂r` (đúng "đo quãng = log", §0.1). Dùng để (a) hiển thị "đang cao hơn gốc mấy nửa cung", (b) **round-trip** `n→r→n` chứng minh cặp hàm khớp (§0.5).

### 0.4. Hai mapping ứng dụng → lõi (mỗi cái sinh ra `semitones`)

Cả hai chỉ khác ở chỗ **`semitones` từ đâu ra**, rồi đều gọi `SemitonesToRatio` (§0.2) và nhân `basePitch`:

| Helper | Mô hình sinh `semitones` | Ý nghĩa |
|---|---|---|
| **Pitch ramp** (combo) | `semitones = min(step · semitonesPerStep, maxSemitones)` | combo càng dài (`step` tăng) → pitch tăng dần, có **trần** `maxSemitones` (chống chói/chipmunk). `step=0` → gốc |
| **Random detune** | `semitones = clamp(signedUnit, −1, 1) · rangeSemitones` | lệch ngẫu nhiên **đối xứng** quanh gốc (`±rangeSemitones`) chống lặp âm nhàm. `signedUnit=0` → gốc |

> **Vì sao detune đối xứng trong log-space là đúng?** `±n` semitone cho ratio `2^(±n/12)` = **nghịch đảo của nhau** → lên `n` và xuống `n` là **cùng quãng âm nhạc**, nghe cân. Nếu cộng/trừ tuyến tính lên tần số sẽ lệch (§0.1).

### 0.5. Kiểm mốc lõi (xác nhận công thức đúng trước khi code)

Chỉ kiểm 3 hàm lõi §0.2–0.3; mapping ứng dụng kiểm ở Task 2.

| Mốc | Kỳ vọng | Kiểm |
|---|---|---|
| `SemitonesToRatio(0)` | `1` (giữ nguyên) | `2^0=1` |
| `SemitonesToRatio(12)` | `2` (lên 1 octave) | `2^1` |
| `SemitonesToRatio(-12)` | `0.5` (xuống 1 octave) | `2^-1` |
| `SemitonesToRatio(1)` | `≈ 1.05946` (nửa cung) | `2^(1/12)` |
| `SemitonesToRatio(7)` | `≈ 1.4983` (quãng năm) | `2^(7/12)` |
| `CentsToRatio(1200)` | `2` (1200 cents = 1 octave) | `2^1` |
| `CentsToRatio(100)` | `≈ 1.05946` (= 1 semitone) | khớp `SemitonesToRatio(1)` |
| `RatioToSemitones(2)` | `12` | `12·log₂2` |
| `RatioToSemitones(1)` | `0` | `12·log₂1` |
| **round-trip** `RatioToSemitones(SemitonesToRatio(7))` | `≈ 7` | cặp nghịch đảo (§0.3) |
| `RatioToSemitones(0)` / âm | `12·log₂(1e-6) ≈ −239.2` (hữu hạn, không NaN/−∞) | kẹp `MinPositiveRatio` |

---

## Bản đồ triển khai

```
PhysXHelper/
└── AudioPitchHelper.cs   1 file, 2 task tăng dần
     ├── Task 1  const + SemitonesToRatio + RatioToSemitones + CentsToRatio (lõi quy đổi)   §0.2, §0.3
     └── Task 2  GetRampedPitch + GetDetunedPitch (ứng dụng)                                 §0.4
```
Thứ tự: **1 → 2**. Task 2 *modify* file Task 1 tạo (thêm hàm, không sửa hàm cũ → Open/Closed).

---

### Task 1: `SemitonesToRatio` + `RatioToSemitones` + `CentsToRatio` — lõi quy đổi cao độ

**Files:**
- Create: `Assets/Horcrux/Runtime/Utilities/PhysXHelper/AudioPitchHelper.cs`

**Interfaces:**
- Consumes: — (chỉ `Unity.Mathematics`).
- Produces:
  - `static float SemitonesToRatio(float semitones)`
  - `static float RatioToSemitones(float ratio)`
  - `static float CentsToRatio(float cents)`
  - `private const float InvSemitonesPerOctave`, `SemitonesPerOctave`, `InvCentsPerOctave`, `MinPositiveRatio`

**Bản đồ toán → code:** `r = 2^(n/12)` (§0.2) → `exp2(n·(1/12))`; `n = 12·log₂r` (§0.3) → guard `r>0`; cents `2^(c/1200)` (§0.3) → `exp2(c·(1/1200))`.

**Self-doc & tối ưu:**

| Quyết định | Lý do |
|---|---|
| `static`, thuần `float`, không field | stateless, zero-GC, thread-safe, dễ test |
| `math.exp2`/`math.log2` (không `math.pow`/`math.log`) | intrinsic cơ số 2 trực tiếp: 1 lời gọi, khỏi `pow(2,·)` (chậm hơn) hay `log(·)/log(2)` (2 gọi + 1 chia) — rẻ + ít sai số (§0.3) |
| `const InvSemitonesPerOctave = 1f/12f` rồi **nhân** | chia là hằng compile-time → precompute nghịch đảo, hot path chỉ nhân (§0.3) |
| `SemitonesPerOctave = 12f` cho chiều nghịch | `12·log₂r` — hằng có tên, self-doc thay số ma thuật |
| `math.max(ratio, MinPositiveRatio)` trước `log2` | `ratio ≤ 0` → `log₂` ra `NaN/−∞`; kẹp về ε dương nhỏ, liên tục |
| `CentsToRatio` tách riêng (dù = semitone/100) | rõ ý đồ gọi (fine detune), vẫn 1 lời gọi `exp2` |
| `[AggressiveInlining]` cả 3 | wrapper mỏng (1 phép nhân + 1 lời gọi math) → nội tuyến khỏi phí gọi hàm (như `Interpolator.cs`) |

- [ ] **Step 1: Tạo file với code Task 1**

```csharp
using System.Runtime.CompilerServices;
using Unity.Mathematics;

namespace Horcrux.Runtime.Utilities.PhysXHelper
{
    /// <summary>
    /// Quy đổi cao độ âm nhạc (semitone / cents) sang hệ số nhân cho <c>AudioSource.pitch</c>.
    /// Static, stateless, zero-GC. Cao độ cảm nhận theo tỉ lệ tần số → dùng phép nhân, không cộng.
    /// </summary>
    /// <remarks>
    /// Lõi: ratio = 2^(semitones/12), suy từ octave = ×2 chia 12 bậc đều (§0.2).
    /// Nghịch đảo: semitones = 12·log₂(ratio) (§0.3). Cents: ratio = 2^(cents/1200) (§0.3).
    /// Class chỉ quy đổi — caller tự gán <c>source.pitch = basePitch * ratio</c>.
    /// </remarks>
    public static class AudioPitchHelper
    {
        private const float SemitonesPerOctave    = 12f;        // 12 nửa cung / octave
        private const float InvSemitonesPerOctave = 1f / 12f;   // precompute: hot path nhân thay chia (§0.3)
        private const float InvCentsPerOctave     = 1f / 1200f; // 1 octave = 1200 cents
        // Kẹp sàn cho ratio: ≤ 0 làm log₂ ra NaN/−∞ (§Global Constraints).
        private const float MinPositiveRatio      = 1e-6f;

        /// <summary>Đổi khoảng cách nửa cung → hệ số nhân pitch. n=0→1, n=12→2 (lên 1 octave), n=−12→0.5.</summary>
        /// <remarks>Formula: ratio = 2^(semitones/12) (§0.2).</remarks>
        /// <param name="semitones">Số nửa cung so với gốc (dương = lên, âm = xuống; nhận cả phân số).</param>
        /// <returns>Hệ số nhân đặt vào AudioSource.pitch (nhân với basePitch).</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float SemitonesToRatio(float semitones)
            => math.exp2(semitones * InvSemitonesPerOctave); // 2^(n/12)

        /// <summary>Đổi hệ số nhân pitch → số nửa cung (nghịch đảo <see cref="SemitonesToRatio"/>). ratio=2→12, ratio=1→0.</summary>
        /// <remarks>Formula: semitones = 12·log₂(ratio) (§0.3). ratio ≤ 0 → kẹp MinPositiveRatio.</remarks>
        /// <param name="ratio">Hệ số nhân pitch (&gt; 0).</param>
        /// <returns>Số nửa cung tương ứng.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float RatioToSemitones(float ratio)
            => SemitonesPerOctave * math.log2(math.max(ratio, MinPositiveRatio)); // 12·log₂r, guard r>0

        /// <summary>Đổi cents → hệ số nhân pitch (thang mịn: 100 cents = 1 nửa cung). c=1200→2, c=100→1.05946.</summary>
        /// <remarks>Formula: ratio = 2^(cents/1200) (§0.3). Dùng cho detune tinh vi.</remarks>
        /// <param name="cents">Số cents so với gốc (1 octave = 1200 cents).</param>
        /// <returns>Hệ số nhân đặt vào AudioSource.pitch.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float CentsToRatio(float cents)
            => math.exp2(cents * InvCentsPerOctave); // 2^(c/1200)
    }
}
```

- [ ] **Step 2: Kiểm chứng (chạy tay / nhẩm theo §0.5)**

| Input | Kỳ vọng |
|---|---|
| `SemitonesToRatio(0)` | `1` |
| `SemitonesToRatio(12)` | `2` |
| `SemitonesToRatio(-12)` | `0.5` |
| `SemitonesToRatio(7)` | `≈ 1.4983` (quãng năm) |
| `CentsToRatio(1200)` | `2` |
| `CentsToRatio(100)` | `≈ 1.05946` (khớp `SemitonesToRatio(1)`) |
| `RatioToSemitones(2)` | `12` |
| `RatioToSemitones(SemitonesToRatio(7))` | `≈ 7` (round-trip) |
| `RatioToSemitones(-5f)` | `≈ −239.2` (kẹp về 1e-6, hữu hạn, không NaN) |

Unity biên dịch sạch (Console không lỗi).

- [ ] **Step 3: Commit**

```bash
git add Assets/Horcrux/Runtime/Utilities/PhysXHelper/AudioPitchHelper.cs
git commit -m "feat(physx): AudioPitchHelper - semitone/cents <-> pitch ratio (core)"
```

---

### Task 2: `GetRampedPitch` + `GetDetunedPitch` — pitch ramp combo & random detune

**Files:**
- Modify: `Assets/Horcrux/Runtime/Utilities/PhysXHelper/AudioPitchHelper.cs` (thêm 2 hàm vào class, không sửa hàm cũ)

**Interfaces:**
- Consumes: `SemitonesToRatio(float)` — Task 1.
- Produces:
  - `static float GetRampedPitch(int step, float semitonesPerStep, float maxSemitones, float basePitch = 1f)`
  - `static float GetDetunedPitch(float signedUnit, float rangeSemitones, float basePitch = 1f)`

**Bản đồ toán → code:** cả hai = mapping sinh `semitones` (§0.4) rồi `SemitonesToRatio` × `basePitch`. Ramp: `semitones = min(step·perStep, maxSemitones)`. Detune: `semitones = clamp(signedUnit, −1, 1)·rangeSemitones`.

**Self-doc & tối ưu:**

| Quyết định | Lý do |
|---|---|
| cả hai delegate về `SemitonesToRatio` | DRY tuyệt đối — công thức `2^(n/12)` ở 1 chỗ, helper chỉ sinh `semitones` |
| `[AggressiveInlining]` cả 2 wrapper | wrapper mỏng (vài phép + gọi lõi) → nội tuyến khỏi phí gọi hàm |
| Ramp: `math.min(step·perStep, maxSemitones)` | trần chống pitch chói/chipmunk khi combo dài; `step=0` → `0` semitone → gốc (§0.4) |
| Ramp nhận `step` kiểu `int` | combo là bộ đếm rời rạc; `int·float` không alloc, tự nới `float` |
| Detune: `math.clamp(signedUnit, −1f, 1f)` | caller cấp random `[−1,1]`; kẹp phòng vượt biên → detune không vọt ngoài `±rangeSemitones` |
| Detune **đối xứng** qua `signedUnit·range` | `±n` cho ratio nghịch đảo → lên/xuống cùng quãng, nghe cân (§0.4) |
| nguồn random **ở caller** | SRP + thuần/ dễ test: hàm là ánh xạ tất định, random tách ra (`Random.Range(-1f,1f)`) |
| trả `float` (hệ số nhân), không tự set `AudioSource` | SRP — class quy đổi, caller gán `source.pitch` |

- [ ] **Step 1: Thêm code Task 2 vào class `AudioPitchHelper`** (đặt sau `CentsToRatio`)

```csharp
        /// <summary>
        /// Pitch cho combo/chuỗi liên hoàn: bước càng cao (step) → pitch tăng dần, có trần maxSemitones (§0.4).
        /// Hợp combo counter, nhặt coin liên hoàn — tiếng "ting" cao dần gây nghiện.
        /// </summary>
        /// <remarks>Formula: semitones = min(step·semitonesPerStep, maxSemitones); pitch = basePitch·2^(semitones/12).</remarks>
        /// <param name="step">Chỉ số bước trong chuỗi (0 = bước đầu → pitch gốc). ≥ 0.</param>
        /// <param name="semitonesPerStep">Mỗi bước lên bao nhiêu nửa cung (vd 1 = mỗi combo +1 semitone).</param>
        /// <param name="maxSemitones">Trần tổng nửa cung (chống pitch chói khi combo dài). Vd 12 = tối đa +1 octave.</param>
        /// <param name="basePitch">Pitch gốc để nhân (mặc định 1 = AudioSource.pitch bình thường).</param>
        /// <returns>Hệ số pitch gán thẳng AudioSource.pitch.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float GetRampedPitch(int step, float semitonesPerStep, float maxSemitones, float basePitch = 1f)
        {
            float semitones = math.min(step * semitonesPerStep, maxSemitones); // tăng dần, kẹp trần (§0.4)
            return basePitch * SemitonesToRatio(semitones);
        }

        /// <summary>
        /// Lệch pitch ngẫu nhiên nhẹ, đối xứng quanh gốc — chống lặp âm nhàm chán khi phát liên tiếp (§0.4).
        /// Nguồn ngẫu nhiên do caller cấp (vd UnityEngine.Random.Range(-1f, 1f)) → hàm thuần, dễ test.
        /// </summary>
        /// <remarks>Formula: semitones = clamp(signedUnit, −1, 1)·rangeSemitones; pitch = basePitch·2^(semitones/12).</remarks>
        /// <param name="signedUnit">Giá trị ngẫu nhiên trong [−1, 1]; 0 = không lệch. Kẹp nếu vượt biên.</param>
        /// <param name="rangeSemitones">Biên độ lệch tối đa (nửa cung) mỗi phía. Vd 0.5 = ±nửa của nửa cung.</param>
        /// <param name="basePitch">Pitch gốc để nhân (mặc định 1).</param>
        /// <returns>Hệ số pitch gán thẳng AudioSource.pitch.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float GetDetunedPitch(float signedUnit, float rangeSemitones, float basePitch = 1f)
        {
            float semitones = math.clamp(signedUnit, -1f, 1f) * rangeSemitones; // đối xứng ±range (§0.4)
            return basePitch * SemitonesToRatio(semitones);
        }
```

- [ ] **Step 2: Kiểm chứng — kiểm mốc (§0.5)**

| Input | Kỳ vọng |
|---|---|
| `GetRampedPitch(0, 1f, 12f)` | `1` (step 0 → gốc) |
| `GetRampedPitch(3, 1f, 12f)` | `≈ 1.1892` (`2^(3/12)`) |
| `GetRampedPitch(12, 1f, 12f)` | `2` (đúng trần) |
| `GetRampedPitch(99, 1f, 12f)` | `2` (kẹp trần, không cao hơn) |
| `GetRampedPitch(4, 2f, 24f, 0.5f)` | `0.5·2^(8/12) ≈ 0.7937` (có basePitch) |
| `GetDetunedPitch(0f, 0.5f)` | `1` (không lệch) |
| `GetDetunedPitch(1f, 0.5f)` | `≈ 1.0293` (`2^(0.5/12)`) |
| `GetDetunedPitch(-1f, 0.5f)` | `≈ 0.9715` (nghịch đảo của trên → đối xứng) |
| `GetDetunedPitch(5f, 0.5f)` | `= GetDetunedPitch(1f, 0.5f)` (kẹp signedUnit) |

- [ ] **Step 3: Commit**

```bash
git add Assets/Horcrux/Runtime/Utilities/PhysXHelper/AudioPitchHelper.cs
git commit -m "feat(physx): AudioPitchHelper - GetRampedPitch + GetDetunedPitch"
```

---

## Ghi chú thực thi

- **Thứ tự:** 1 → 2 (mỗi task thêm hàm vào cùng file, không sửa hàm cũ → Open/Closed).
- **Chỉ quy đổi, không phát âm:** class trả *hệ số pitch*; caller gán `audioSource.pitch = GetRampedPitch(...)` rồi `Play()` — tách trách nhiệm (SRP). Việc map cường độ va chạm → âm lượng/pitch là `AudioFeedback` (hệ khác).
- **Ràng buộc engine:** Unity kẹp `AudioSource.pitch ∈ [−3, 3]`; giới hạn trên `3` ứng `12·log₂3 ≈ 19.02` semitone (≈ +1.585 octave). Helper **không** tự kẹp (nó thuần toán, không biết ngưỡng engine); chọn `maxSemitones ≤ 19` (`2^(19/12) ≈ 3.00`) để ramp không chạm trần engine. Đây là lý do ramp có tham số trần thay vì để vô hạn.
- **Nguồn ngẫu nhiên ở caller:** `GetDetunedPitch(Random.Range(-1f, 1f), 0.5f)` — giữ hàm tất định, dễ kiểm mốc.
- **File `.meta`:** Unity tự sinh khi import — commit kèm nếu repo giữ GUID ổn định.
- **Kiểm chứng:** chạy tay qua script tạm hoặc nhẩm theo kiểm mốc §0.5 — **không** tạo file test. Xóa script tạm trước khi commit.
- **Muốn test tự động sau này:** dựng NUnit EditMode theo bảng kiểm chứng (đặc biệt: round-trip `n→r→n`; đối xứng `GetDetunedPitch(±1)` cho ratio nghịch đảo; trần ramp) — ngoài phạm vi plan này.
- **Cập nhật roadmap:** sau khi xong, đánh dấu `AudioPitchHelper` ✅ trong `Pendings.md` (Tầng 2, mục 18) — mở khóa `GridSnapFeedback` (tick), `ChainReaction` (pitch ramp combo).
