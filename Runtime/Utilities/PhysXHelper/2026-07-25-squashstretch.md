# SquashStretch Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.
>
> **Plan này là artifact học tập + triển khai, tự chứa.** Người dùng tự code lại để rèn tư duy → §0 dẫn giải **đầy đủ toán học** (biến đổi + lý do dùng công thức), các task chứa **code hoàn chỉnh** kèm chú thích self-doc & lý do tối ưu. Không code test → mỗi task có **checklist kiểm chứng** (kiểm mốc toán học).

**Goal:** `SquashStretch` — biến dạng nén/giãn **bảo toàn thể tích** dạng static, stateless, thuần tính toán: một trục biến dạng → (các) trục vuông góc bù lại để thể tích không đổi. Trả `Vector3` gán thẳng `transform.localScale`.

**Architecture:** 1 file `SquashStretch.cs`. Lõi là **một** công thức bù `c = s^(−1/n)` (suy từ bảo toàn thể tích), đặc biệt hóa thành 2D (area) / 3D (volume). Ba helper mapping (impact / directional / time) đều **quy về** lõi này → không lặp công thức. Time-driven tái dùng `Easer` (Tầng 0, đã có).

**Tech Stack:** C# (Unity), `Unity.Mathematics` (`math.rsqrt/max`) + `UnityEngine` (`Vector3`, `Mathf`) — nhất quán `Interpolator.cs`. Thuần toán — không Addressables/UniTask. (`pow` cố ý tránh — xem §0.3.)

## Global Constraints

| Ràng buộc | Giá trị |
|---|---|
| Namespace | `Horcrux.Runtime.Utilities.PhysXHelper` |
| Tầng phụ thuộc | Tầng 2, `← Easing` (`Horcrux.Runtime.Tweening.Easing.Easer` — **đã xong**). Không đụng hệ chưa làm (`Pendings.md` §Tầng 2, mục 11) |
| Zero-GC | thuần `float`/`Vector3` (struct, stack); không `new` reference-type, LINQ, closure, string; stateless (không field) |
| SOLID | 1 class = 1 trách nhiệm (tính scale biến dạng, **không** xoay/ghi transform); mở rộng qua overload/enum, không sửa hàm cũ |
| Self-doc | tên nói rõ mục đích (`GetVolumePreservingScale`≠`Process`); XML doc kèm công thức + "tại sao" ở mọi hàm public |
| Tái dùng | `Easer.Evaluate(EaseType, t)` từ `Tweening.Easing` — **không** viết lại easing |
| Công thức chuẩn | bảo toàn thể tích `s·cⁿ = 1` → `c = s^(−1/n)`; 2D→`1/s`, 3D→`1/√s` |
| Guard biên | `primaryScale ≤ 0` → kẹp về `MinPositiveScale` (chặn `pow/rsqrt/÷` ra NaN/∞); `maxImpact ≤ 0` → không nén; `maxStretch < 1` → không giãn |

---

## §0. Nền toán học (đọc trước khi code)

> Mục tiêu: hiểu **tại sao** từng công thức. Hệ này nhẹ — lõi chỉ **một** công thức bù, ba helper là các *mapping* đơn giản đổ về lõi. Đọc xong bạn tự dựng lại được cả file.

### 0.1. Bản chất — squash & stretch là gì

Vật thể "sống động" khi va chạm/tăng tốc thì **biến dạng** chứ không cứng đơ: nhân vật đáp đất → bẹp xuống (squash), bay nhanh → kéo dài (stretch). Bí quyết để mắt thấy "tự nhiên" là **giữ nguyên thể tích** — như cục bột nặn: ấn dẹt xuống thì phình ngang ra, lượng bột không đổi.

| Thành phần | Vai trò |
|---|---|
| **Trục chính** (primary) | trục *chủ động* biến dạng — nén (`s<1`) hoặc giãn (`s>1`) |
| **Trục bù** (compensating) | (các) trục vuông góc *phình/co ngược lại* để giữ thể tích |
| **Bảo toàn thể tích** | ràng buộc "tích các hệ số scale = 1" — nguồn của mọi công thức dưới |

**Điểm cốt lõi:** nghịch đảo trực giác — muốn *nén* một trục thì phải *phình* trục kia đúng lượng để bù. Toàn bộ hệ chỉ là giải bài "phình bao nhiêu cho vừa".

### 0.2. Nguyên lý — suy công thức bù từ bảo toàn thể tích

Hình thức hóa bài "phình bao nhiêu cho vừa" (§0.1): **thể tích = tích mọi hệ số scale** (chính là định thức ma trận scale `det(S)` = tỉ lệ thể tích). Nghỉ: mọi scale `= 1` → `V₀ = 1`.

Biến dạng trục chính hệ số `s`, và `n` trục vuông góc mỗi trục bù hệ số `c`. Thể tích mới `= s · cⁿ`. Bắt buộc bằng `V₀`:

$$s \cdot c^{n} = 1$$

Giải ra `c` — chia hai vế cho `s` rồi lấy căn bậc `n`:

$$c^{n} = \frac{1}{s} \quad\Rightarrow\quad \boxed{\;c = s^{-1/n}\;}$$

> **Vì sao chia đều cho `n` trục?** Ràng buộc chỉ đòi `∏cᵢ = 1/s` — vô số nghiệm. Ta chọn **tất cả bằng nhau** (`c₁ = … = cₙ = c`): mặt phẳng vuông góc trục chính biến dạng **đẳng hướng**, không thiên vị hướng nào. Đây là **lựa chọn mô hình** cho game feel (đẹp mắt, đối xứng), không phải nghiệm duy nhất.

**Phép kiểm tái lập:** thay `c = s^(−1/n)` vào `s·cⁿ`: `s · (s^(−1/n))ⁿ = s · s^(−1) = s⁰ = 1` ✓ — đúng bảo toàn.

### 0.3. Đặc biệt hóa 2D / 3D (và vì sao code không gọi `pow`)

`n` = số trục **bù** (không tính trục chính):

| Chiều | `n` | `c = s^(−1/n)` | Phép rẻ tương đương | Trục giữ nguyên |
|---|---|---|---|---|
| **2D** (area, sprite) | 1 | `s^(−1)` = `1/s` | phép chia (reciprocal) | trục thứ 3 (depth) `= 1` |
| **3D** (volume, mesh) | 2 | `s^(−1/2)` = `1/√s` | `math.rsqrt(s)` (nhanh) | — (cả 2 trục kia đều bù) |

→ Chốt tối ưu: `n∈{1,2}` biến `pow(s,−1/n)` (đắt) thành phép ở cột phải — code rẽ nhánh theo `mode`, **không bao giờ** gọi `math.pow`.

**2D bù trục nào?** Sprite nằm mặt phẳng XY, trục Z là depth (vô nghĩa với biến dạng 2D) → giữ `Z = 1`. Trục bù là trục còn lại của cặp XY. Quy tắc: `partner = (primary == X) ? Y : X`; trục thứ ba `= 1`.

### 0.4. Ba mapping → lõi (mỗi cái là một cách sinh ra `s`)

Cả ba helper chỉ khác nhau ở chỗ **`s` từ đâu ra**, rồi đều gọi lõi §0.2–0.3:

| Helper | Mô hình sinh `s` | Ý nghĩa |
|---|---|---|
| **Impact → squash** | `s = lerp(1, minScale, saturate(impact/maxImpact))` | va càng mạnh → `s` càng nhỏ (bẹp sâu), kẹp đáy `minScale`. `s ∈ [minScale, 1]` |
| **Directional stretch** | `s = 1 + clamp(speed·k, 0, maxStretch−1)` | đi càng nhanh → `s` càng lớn (kéo dài dọc hướng đi), trần `maxStretch`. `s ≥ 1` |
| **Time-driven** | `s = lerpUnclamped(minScale, 1, Easer.Evaluate(ease, t))` | `t=0` bẹp (`minScale`) → `t=1` về nghỉ (`1`); ease `OutBack` cho **vọt lố** `s>1` (giãn) rồi lắng |

> **`lerpUnclamped` ở Time-driven — bắt buộc:** `Easer.Evaluate` với họ Back/Elastic/Bounce trả giá trị **vượt** `[0,1]` (theo doc `Easer`). Dùng `Lerp` kẹp sẽ **mất** phần vọt lố → mất đúng cái "đã" của squash-stretch. Phải `LerpUnclamped`.

### 0.5. Kiểm mốc (xác nhận công thức đúng trước khi code)

| Mốc | Kỳ vọng | Kiểm |
|---|---|---|
| `s=1` (mọi mode) | scale `(1,1,1)` — không biến dạng | ✓ (`c = 1^… = 1`) |
| 2D, `s=0.5`, primary=Y | `(2, 0.5, 1)`; area XY `= 2·0.5 = 1` | ✓ |
| 3D, `s=0.5`, primary=Y | `(1.414, 0.5, 1.414)`; vol `= 1.414²·0.5 = 2·0.5 = 1` | ✓ (`rsqrt(0.5)=√2`) |
| 3D, `s=2`, primary=Y | `(0.707, 2, 0.707)`; vol `= 0.707²·2 = 0.5·2 = 1` | ✓ (`rsqrt(2)=1/√2`) |
| Impact, `impact=0` | `s=1` → `(1,1,1)` | ✓ |
| Impact, `impact≥maxImpact` | `s=minScale` (bẹp sâu nhất) | ✓ |
| Directional, `speed=0` | `s=1` → `(1,1,1)` | ✓ |
| Time, `t=0` | `s=minScale` (bẹp) | ✓ (`Easer(…,0)=0`) |
| Time, `t=1`, ease=OutBack | `eased=1` → `s=1` (về nghỉ) | ✓ (easing endpoint chuẩn) |

---

## Bản đồ triển khai

```
PhysXHelper/
└── SquashStretch.cs   1 file, 3 task tăng dần
     ├── Task 1  enum Axis + VolumeMode + GetVolumePreservingScale (lõi)   §0.2, §0.3
     ├── Task 2  GetSquashFromImpact + GetDirectionalStretch              §0.4
     └── Task 3  GetSquashStretch (time-driven, ← Easer)                  §0.4
```
Thứ tự: **1 → 2 → 3**. Task 2 & 3 *modify* file Task 1 tạo (thêm hàm, không sửa hàm cũ → Open/Closed). Task 3 thêm dependency `Easer` — cô lập ở task cuối để 2 task đầu thuần toán, độc lập.

---

### Task 1: `GetVolumePreservingScale` + enum `Axis`/`VolumeMode` — lõi bảo toàn thể tích

**Files:**
- Create: `Assets/Horcrux/Runtime/Utilities/PhysXHelper/SquashStretch.cs`

**Interfaces:**
- Consumes: — (chỉ `Unity.Mathematics`, `UnityEngine`).
- Produces:
  - `enum Axis { X, Y, Z }`
  - `enum VolumeMode { Area2D, Volume3D }`
  - `static Vector3 GetVolumePreservingScale(float primaryScale, Axis primaryAxis = Axis.Y, VolumeMode mode = VolumeMode.Volume3D)`
  - `private const float MinPositiveScale`

**Bản đồ toán → code:** `c = s^(−1/n)` (§0.2) · đặc biệt hóa `1/s` (2D) / `rsqrt(s)` (3D) (§0.3) · quy tắc trục bù 2D (§0.3) · guard `s>0` (§Global Constraints).

**Self-doc & tối ưu:**

| Quyết định | Lý do |
|---|---|
| `static`, thuần `float`/`Vector3`, không field | stateless, zero-GC, thread-safe, dễ test |
| trả `Vector3` (struct) | gán thẳng `transform.localScale`; struct → stack, không alloc heap |
| **không** gọi `math.pow` | đặc biệt hóa: 2D → `1/s` (chia), 3D → `math.rsqrt(s)` (§0.3) — rẻ hơn `pow` nhiều |
| `math.rsqrt` (không `1f/math.sqrt`) | rsqrt = `1/√` **chính xác** đủ float (không phải Quake fast-approx) → bảo toàn `s·c²=1` tuyệt đối; 1 lệnh thay chia+căn |
| `math.max(primaryScale, MinPositiveScale)` | `s≤0` làm `pow/rsqrt/÷` ra NaN/∞; kẹp về ε dương nhỏ, liên tục |
| `enum Axis` + `mode` thay hard-code trục | 1 hàm phủ va đáp đất (Y), va ngang (X), 2D lẫn 3D — Open/Closed |
| class = chỉ tính scale (không xoay/ghi transform) | SRP; directional để caller tự xoay object hướng vận tốc |

- [ ] **Step 1: Tạo file với code Task 1**

```csharp
using System.Runtime.CompilerServices;
using Unity.Mathematics;
using UnityEngine;

namespace Horcrux.Runtime.Utilities.PhysXHelper
{
    /// <summary>Trục chính chịu biến dạng squash/stretch; (các) trục còn lại bù để giữ thể tích.</summary>
    public enum Axis { X, Y, Z }

    /// <summary>Số chiều bảo toàn: Area2D = giữ diện tích (1 trục bù), Volume3D = giữ thể tích (2 trục bù).</summary>
    public enum VolumeMode { Area2D, Volume3D }

    /// <summary>
    /// Biến dạng nén/giãn bảo toàn thể tích (squash &amp; stretch): nén 1 trục thì (các) trục
    /// vuông góc phình bù → tích các hệ số scale = 1. Static, stateless, zero-GC.
    /// </summary>
    /// <remarks>
    /// Lõi: hệ số bù c = s^(−1/n) với s = scale trục chính, n = số trục bù (§0.2).
    /// Đặc biệt hóa: 2D (n=1) → c = 1/s; 3D (n=2) → c = 1/√s (§0.3).
    /// Class chỉ tính <see cref="Vector3"/> scale — caller tự gán localScale (và tự xoay nếu dùng directional).
    /// </remarks>
    public static class SquashStretch
    {
        // Kẹp sàn cho scale trục chính: s ≤ 0 làm pow/rsqrt/chia ra NaN/∞ (§Global Constraints).
        private const float MinPositiveScale = 1e-4f;

        /// <summary>
        /// Scale bảo toàn thể tích từ hệ số trục chính: nén/giãn primaryAxis theo primaryScale,
        /// (các) trục vuông góc phình/co bù (§0.2, §0.3).
        /// </summary>
        /// <remarks>
        /// Formula: c = s^(−1/n). Area2D → c = 1/s (giữ Z=1); Volume3D → c = 1/√s (cả 2 trục kia bù).
        /// primaryScale &lt; 1 → squash (bẹp); &gt; 1 → stretch (kéo dài); = 1 → không biến dạng.
        /// math.rsqrt = 1/√ chính xác (không phải fast-approx) → tích scale = 1 đúng tuyệt đối.
        /// </remarks>
        /// <param name="primaryScale">Hệ số scale trục chính s. Kẹp sàn về MinPositiveScale nếu ≤ 0.</param>
        /// <param name="primaryAxis">Trục chủ động biến dạng (vd Y cho nhảy/đáp đất, X cho va ngang).</param>
        /// <param name="mode">Area2D (sprite, giữ diện tích) hay Volume3D (mesh, giữ thể tích).</param>
        /// <returns>Scale gán thẳng transform.localScale; tích các thành phần theo mode = 1.</returns>
        public static Vector3 GetVolumePreservingScale(
            float primaryScale, Axis primaryAxis = Axis.Y, VolumeMode mode = VolumeMode.Volume3D)
        {
            float primary = math.max(primaryScale, MinPositiveScale); // guard s > 0 (§Global Constraints)
            float comp = mode == VolumeMode.Volume3D
                ? math.rsqrt(primary) // 3D: c = s^(−1/2) = 1/√s  (§0.3)
                : 1f / primary;       // 2D: c = s^(−1)   = 1/s   (§0.3)

            if (mode == VolumeMode.Volume3D)
            {
                switch (primaryAxis) // cả 2 trục kia đều bù
                {
                    case Axis.X: return new Vector3(primary, comp, comp);
                    case Axis.Y: return new Vector3(comp, primary, comp);
                    default:     return new Vector3(comp, comp, primary); // Z
                }
            }

            switch (primaryAxis) // Area2D: 1 trục bù, trục thứ 3 (depth) giữ = 1 (§0.3)
            {
                case Axis.X: return new Vector3(primary, comp, 1f); // partner Y
                case Axis.Y: return new Vector3(comp, primary, 1f); // partner X
                default:     return new Vector3(comp, 1f, primary); // Z → partner X
            }
        }
    }
}
```

- [ ] **Step 2: Kiểm chứng (chạy tay / nhẩm theo §0.5)**

| Input | Kỳ vọng |
|---|---|
| `GetVolumePreservingScale(1f, Axis.Y, VolumeMode.Volume3D)` | `(1, 1, 1)` |
| `GetVolumePreservingScale(0.5f, Axis.Y, VolumeMode.Area2D)` | `(2, 0.5, 1)` — area XY = 1 |
| `GetVolumePreservingScale(0.5f, Axis.Y, VolumeMode.Volume3D)` | `≈ (1.414, 0.5, 1.414)` — vol = 1 |
| `GetVolumePreservingScale(2f, Axis.X, VolumeMode.Volume3D)` | `≈ (2, 0.707, 0.707)` — vol = 1 |
| `GetVolumePreservingScale(-3f, Axis.Y, VolumeMode.Area2D)` | `(10000, 1e-4, 1)` — kẹp sàn, không NaN |

Unity biên dịch sạch (Console không lỗi).

- [ ] **Step 3: Commit**

```bash
git add Assets/Horcrux/Runtime/Utilities/PhysXHelper/SquashStretch.cs
git commit -m "feat(physx): SquashStretch - GetVolumePreservingScale (core, 2D/3D)"
```

---

### Task 2: `GetSquashFromImpact` + `GetDirectionalStretch` — mapping va chạm & vận tốc

**Files:**
- Modify: `Assets/Horcrux/Runtime/Utilities/PhysXHelper/SquashStretch.cs` (thêm 2 hàm vào class, không sửa hàm cũ)

**Interfaces:**
- Consumes: `GetVolumePreservingScale(float, Axis, VolumeMode)` — Task 1; `Axis`, `VolumeMode`.
- Produces:
  - `static Vector3 GetSquashFromImpact(float impact, float maxImpact, float minScale = 0.6f, Axis primaryAxis = Axis.Y, VolumeMode mode = VolumeMode.Volume3D)`
  - `static Vector3 GetDirectionalStretch(float speed, float stretchPerSpeed, float maxStretch = 2f, Axis primaryAxis = Axis.Y, VolumeMode mode = VolumeMode.Volume3D)`

**Bản đồ toán → code:** cả hai = mapping sinh `s` (§0.4) rồi gọi lõi Task 1. Impact: `s = lerp(1, minScale, saturate(impact/maxImpact))`. Directional: `s = 1 + clamp(speed·k, 0, maxStretch−1)`.

**Self-doc & tối ưu:**

| Quyết định | Lý do |
|---|---|
| cả hai delegate về `GetVolumePreservingScale` | DRY tuyệt đối — công thức bù ở 1 chỗ, helper chỉ sinh `s` |
| `[AggressiveInlining]` cả 2 wrapper | wrapper mỏng (vài phép + gọi lõi) → nội tuyến khỏi phí gọi hàm (như `Interpolator.cs`) |
| Impact: guard `maxImpact > 0` → `normalized 0` | tránh chia 0; `maxImpact≤0` coi như không va → `s=1` |
| `Mathf.Clamp01(impact/maxImpact)` | va vượt trần vẫn kẹp `s=minScale`, không bẹp âm |
| Directional: `maxExtra = max(maxStretch−1, 0)` rồi clamp | `maxStretch<1` → `maxExtra=0` → `s=1` (không giãn, đúng §Global Constraints); nếu clamp thẳng `maxStretch−1` âm sẽ ép `s<1` = **squash ngoài ý muốn** |
| directional **không** tự xoay object | SRP — caller xoay `primaryAxis` về hướng vận tốc; hàm chỉ cho *độ lớn* scale |
| `s = 1 + (minScale−1)·normalized` (khai triển lerp) | 1 phép nhân + cộng, không gọi `Mathf.Lerp` (rẻ, inline được) |

- [ ] **Step 1: Thêm code Task 2 vào class `SquashStretch`** (đặt sau `GetVolumePreservingScale`)

```csharp
        /// <summary>
        /// Squash theo cường độ va chạm: va càng mạnh (impact) → bẹp càng sâu, kẹp đáy minScale (§0.4).
        /// Hợp nhảy/đáp đất, nút bấm, item pickup.
        /// </summary>
        /// <remarks>Formula: s = lerp(1, minScale, saturate(impact/maxImpact)); rồi bảo toàn thể tích (§0.2).</remarks>
        /// <param name="impact">Cường độ va chạm (vd tốc độ lúc chạm đất). ≤ 0 → không bẹp.</param>
        /// <param name="maxImpact">Ngưỡng va chạm cho bẹp sâu nhất. ≤ 0 → không bẹp (trả scale nghỉ).</param>
        /// <param name="minScale">Hệ số trục chính khi bẹp sâu nhất, trong (0,1). Vd 0.6 = còn 60% cao.</param>
        /// <param name="primaryAxis">Trục bị nén (thường Y khi đáp đất).</param>
        /// <param name="mode">Area2D (sprite) hay Volume3D (mesh).</param>
        /// <returns>Scale bảo toàn thể tích tương ứng độ va.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector3 GetSquashFromImpact(
            float impact, float maxImpact, float minScale = 0.6f,
            Axis primaryAxis = Axis.Y, VolumeMode mode = VolumeMode.Volume3D)
        {
            float normalized = maxImpact > 0f ? Mathf.Clamp01(impact / maxImpact) : 0f;
            float s = 1f + (minScale - 1f) * normalized; // = lerp(1, minScale, normalized) (§0.4)
            return GetVolumePreservingScale(s, primaryAxis, mode);
        }

        /// <summary>
        /// Stretch theo tốc độ: đi càng nhanh → kéo dài dọc trục chính, trần maxStretch (§0.4).
        /// Caller tự xoay object sao cho primaryAxis trùng hướng vận tốc.
        /// </summary>
        /// <remarks>Formula: s = 1 + clamp(speed·stretchPerSpeed, 0, maxStretch−1); rồi bảo toàn thể tích (§0.2).</remarks>
        /// <param name="speed">Độ lớn vận tốc (|v|). ≤ 0 → không giãn.</param>
        /// <param name="stretchPerSpeed">Hệ số quy tốc độ → lượng giãn thêm (1/tốc-độ).</param>
        /// <param name="maxStretch">Trần scale trục chính. &lt; 1 → coi như không giãn (s=1). Vd 2 = kéo dài tối đa gấp đôi.</param>
        /// <param name="primaryAxis">Trục kéo dài — caller xoay trùng hướng đi.</param>
        /// <param name="mode">Area2D (sprite) hay Volume3D (mesh).</param>
        /// <returns>Scale bảo toàn thể tích tương ứng tốc độ.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector3 GetDirectionalStretch(
            float speed, float stretchPerSpeed, float maxStretch = 2f,
            Axis primaryAxis = Axis.Y, VolumeMode mode = VolumeMode.Volume3D)
        {
            float maxExtra = math.max(maxStretch - 1f, 0f);                        // maxStretch<1 → 0 → không giãn (§Global Constraints)
            float extra = Mathf.Clamp(speed * stretchPerSpeed, 0f, maxExtra);      // giãn thêm, trần maxStretch
            float s = 1f + extra;                                                  // s ≥ 1 (§0.4)
            return GetVolumePreservingScale(s, primaryAxis, mode);
        }
```

- [ ] **Step 2: Kiểm chứng — kiểm mốc (§0.5)**

| Input | Kỳ vọng |
|---|---|
| `GetSquashFromImpact(0, 10)` | `(1, 1, 1)` (impact=0 → s=1) |
| `GetSquashFromImpact(10, 10, 0.6f)` | `s=0.6` → `≈ (1.291, 0.6, 1.291)` (3D, vol=1) |
| `GetSquashFromImpact(999, 10, 0.6f)` | `s=0.6` (kẹp, không bẹp sâu hơn) |
| `GetSquashFromImpact(5, 0)` | `(1, 1, 1)` (maxImpact≤0 → không bẹp) |
| `GetDirectionalStretch(0, 0.1f)` | `(1, 1, 1)` (speed=0 → s=1) |
| `GetDirectionalStretch(5, 0.1f, 2f)` | `s=1.5` → `≈ (0.816, 1.5, 0.816)` (3D, vol=1) |
| `GetDirectionalStretch(999, 0.1f, 2f)` | `s=2` (kẹp trần maxStretch) |
| `GetDirectionalStretch(5, 0.1f, 0.5f)` | `s=1` → `(1,1,1)` (maxStretch<1 → không giãn, **không** squash) |

- [ ] **Step 3: Commit**

```bash
git add Assets/Horcrux/Runtime/Utilities/PhysXHelper/SquashStretch.cs
git commit -m "feat(physx): SquashStretch - GetSquashFromImpact + GetDirectionalStretch"
```

---

### Task 3: `GetSquashStretch` (time-driven) — compose `Easer`

**Files:**
- Modify: `Assets/Horcrux/Runtime/Utilities/PhysXHelper/SquashStretch.cs` (thêm `using` + 1 hàm, không sửa hàm cũ)

**Interfaces:**
- Consumes: `GetVolumePreservingScale(float, Axis, VolumeMode)` — Task 1; `Easer.Evaluate(EaseType, float)` + `EaseType` từ `Horcrux.Runtime.Tweening.Easing`.
- Produces:
  - `static Vector3 GetSquashStretch(float t, EaseType easeType, float minScale = 0.6f, Axis primaryAxis = Axis.Y, VolumeMode mode = VolumeMode.Volume3D)`

**Bản đồ toán → code:** §0.4 (time-driven) — `s = lerpUnclamped(minScale, 1, Easer.Evaluate(ease, t))` → lõi Task 1.

**Self-doc & tối ưu:**

| Quyết định | Lý do |
|---|---|
| tái dùng `Easer.Evaluate` | không viết lại easing (đã có Tầng 0); dependency cô lập ở task cuối |
| `Mathf.LerpUnclamped` (không `Lerp`) | ease Back/Elastic/Bounce vượt `[0,1]` → giữ **vọt lố** `s>1` (giãn); `Lerp` sẽ cắt mất (§0.4) |
| `t=0`→`minScale`, `t=1`→`1` | bắt đầu bẹp, kết thúc về nghỉ; `OutBack` cho nảy giãn giữa chừng |
| delegate về lõi | DRY — cùng công thức bảo toàn thể tích |
| không guard `t` | `Easer.Evaluate` đã tự kẹp `t∈[0,1]` (theo doc `Easer`) — không kẹp lại |

- [ ] **Step 1: Thêm `using` đầu file** (sau các `using` sẵn có)

```csharp
using Horcrux.Runtime.Tweening.Easing;
```

- [ ] **Step 2: Thêm `GetSquashStretch` vào cuối class `SquashStretch`**

```csharp
        /// <summary>
        /// Squash→giãn→nghỉ theo tiến trình chuẩn hóa t: t=0 bẹp (minScale) → t=1 về nghỉ (1).
        /// Ease OutBack/Elastic cho vọt lố (s&gt;1, giãn) giữa chừng — cảm giác jelly (§0.4).
        /// </summary>
        /// <remarks>Formula: s = lerpUnclamped(minScale, 1, Easer.Evaluate(ease, t)); rồi bảo toàn thể tích (§0.2).</remarks>
        /// <param name="t">Tiến trình chuẩn hóa; Easer tự kẹp về [0,1].</param>
        /// <param name="easeType">Đường hồi phục. OutBack/OutElastic cho nảy giãn (vượt 1 → dùng LerpUnclamped).</param>
        /// <param name="minScale">Hệ số trục chính lúc bẹp nhất tại t=0, trong (0,1).</param>
        /// <param name="primaryAxis">Trục biến dạng chính.</param>
        /// <param name="mode">Area2D (sprite) hay Volume3D (mesh).</param>
        /// <returns>Scale bảo toàn thể tích tại tiến trình t.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector3 GetSquashStretch(
            float t, EaseType easeType, float minScale = 0.6f,
            Axis primaryAxis = Axis.Y, VolumeMode mode = VolumeMode.Volume3D)
        {
            float eased = Easer.Evaluate(easeType, t);          // kẹp t∈[0,1]; Back/Elastic vượt [0,1]
            float s = Mathf.LerpUnclamped(minScale, 1f, eased); // t=0→minScale, t=1→1, vọt lố→s>1 (§0.4)
            return GetVolumePreservingScale(s, primaryAxis, mode);
        }
```

- [ ] **Step 3: Kiểm chứng — kiểm mốc & vọt lố (§0.5)**

| Input | Kỳ vọng |
|---|---|
| `GetSquashStretch(0, EaseType.OutBack, 0.6f)` | `s=0.6` → `≈ (1.291, 0.6, 1.291)` (bẹp) |
| `GetSquashStretch(1, EaseType.OutBack, 0.6f)` | `s=1` → `(1, 1, 1)` (về nghỉ) |
| `GetSquashStretch(1, EaseType.Linear, 0.6f)` | `s=1` → `(1, 1, 1)` |
| `GetSquashStretch(0.7f, EaseType.OutBack, 0.6f)` | `eased>1` → `s>1` → trục chính giãn (`>1`), trục bù `<1` (vọt lố còn) |
| `GetSquashStretch(0.5f, EaseType.Linear, 0.6f)` | `eased=0.5` → `s=0.8` → `≈ (1.118, 0.8, 1.118)` (3D, vol=1) |

Unity biên dịch sạch.

- [ ] **Step 4: Commit**

```bash
git add Assets/Horcrux/Runtime/Utilities/PhysXHelper/SquashStretch.cs
git commit -m "feat(physx): SquashStretch - GetSquashStretch (time-driven, compose Easer)"
```

---

## Ghi chú thực thi

- **Thứ tự:** 1 → 2 → 3 (mỗi task thêm hàm vào cùng file, không sửa hàm cũ → Open/Closed).
- **`Axis` / `VolumeMode`:** đặt cùng file `SquashStretch.cs` (chưa tồn tại ở namespace — đã kiểm). Nếu sau này hệ khác cần `Axis`, tách ra file riêng rồi *xóa* định nghĩa ở đây (tránh trùng tên cùng namespace → lỗi biên dịch).
- **Directional cần xoay object:** hàm chỉ trả *độ lớn* scale; caller phải xoay `primaryAxis` về hướng vận tốc (`transform.rotation`) — tách trách nhiệm (SRP).
- **File `.meta`:** Unity tự sinh khi import — commit kèm nếu repo giữ GUID ổn định.
- **Kiểm chứng:** chạy tay qua script tạm hoặc nhẩm theo kiểm mốc §0.5 — **không** tạo file test. Xóa script tạm trước khi commit.
- **Muốn test tự động sau này:** dựng NUnit EditMode theo các bảng kiểm chứng mỗi task (đặc biệt: bảo toàn thể tích `∏scale=1` với `s` bất kỳ; vọt lố `OutBack` cho `s>1`) — ngoài phạm vi plan này.
- **Cập nhật roadmap:** sau khi xong, đánh dấu `SquashStretch` ✅ trong `Pendings.md` (Tầng 2, mục 11) — mở khóa `GridSnapFeedback`, `Cascade`/`FallSettle`.