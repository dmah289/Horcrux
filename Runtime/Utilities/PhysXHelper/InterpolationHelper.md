# InterpolationHelper — Spec & bản chất toán học

Đặc tả `InterpolationHelper` để tự code lại. Item **Tầng 0 #3** trong `Pendings.md` —
nền toán học thuần, zero-dependency.

## Nguyên tắc phạm vi (đọc trước) — CHỈ viết hàm `Mathf` KHÔNG có

Với các hàm số học thuần, code tự viết sinh ra **cùng mã máy** như `Mathf` sau khi JIT/IL2CPP
inline (static method, không virtual → không có "call overhead" thực sự). Tự bọc lại chỉ thêm
một lớp phải maintain, **không** nhanh hơn. Vì vậy class này **chỉ chứa 4 nhóm hàm mà `Mathf`
thiếu**. Mọi thứ trùng `Mathf` → gọi thẳng, không wrap.

### Dùng thẳng `Mathf` — KHÔNG viết lại

| Nhu cầu | Dùng | Ghi chú |
|---|---|---|
| Kẹp `[0,1]` | `Mathf.Clamp01(x)` | y hệt |
| Lerp (clamp t) | `Mathf.Lerp(a,b,t)` | |
| Lerp không clamp | `Mathf.LerpUnclamped(a,b,t)` | cho overshoot |
| Bước tuyến tính có trần | `Mathf.MoveTowards(cur,tgt,maxDelta)` | y hệt |
| Smoothstep bậc 3 có remap | `Mathf.SmoothStep(from,to,t)` | Hermite bậc 3 sẵn |
| InverseLerp **có clamp** | `Mathf.InverseLerp(a,b,v)` | kẹp `[0,1]`, trả `0` khi `a==b` |

### Phải tự viết — vì `Mathf` KHÔNG có

1. `InverseLerpUnclamped` — `Mathf.InverseLerp` tự clamp, mất giá trị ngoài `[0,1]`.
2. `Remap` / `RemapClamped` — không tồn tại trong `Mathf`.
3. `SmootherStep` — `Mathf` chỉ có bậc 3, không có bậc 5.
4. `ExpDecay` & anh em — nội suy độc lập framerate, không tồn tại trong `Mathf`. ⭐

## Convention bắt buộc (CLAUDE.md + PhysXHelper)

- `namespace Horcrux.Runtime.Utilities.PhysXHelper`
- `public static class InterpolationHelper` — toàn bộ hàm thuần, **stateless**.
- **Zero-GC**: chỉ `float`/`struct`, không `new`/LINQ/closure/boxing.
- `[MethodImpl(MethodImplOptions.AggressiveInlining)]` cho hàm 1–2 dòng gọi trong hot path
  (đây là lý do **hợp lệ duy nhất** để có wrapper riêng: `Mathf` không sửa được để ép inline).
- XML doc đầy đủ: công thức + miền giá trị + lưu ý biên.

## Ranh giới với `SpringDamper` (SOLID)

Class này **chỉ chứa hàm không trạng thái**. Mọi thứ cần **lưu velocity giữa các frame**
(spring critically-damped, `SmoothDamp` có `ref velocity`) thuộc **`SpringDamper`** (Tầng 1) —
**không** đặt ở đây. "spring-lerp" trong `Pendings.md` tách 2 tầng: phần stateless (exp-decay)
ở đây; phần stateful (spring thật) ở `SpringDamper`. Gộp chung vi phạm single-responsibility.

---

## 1. `InverseLerpUnclamped(a, b, v)` — nghịch đảo của Lerp

**Bản chất:** cho `v`, hỏi "nó ở tỉ lệ `t` nào giữa `a` và `b`?". Giải `v = a+(b−a)t`:
$$t = \frac{v - a}{b - a}$$

- **Khác `Mathf.InverseLerp`:** không clamp → cho phép `t<0` / `t>1` khi `v` ngoài `[a,b]`.
  Cần cho remap có overshoot, hoặc extrapolate.
- **Cạm bẫy chia 0:** `a == b` → mẫu = 0. Guard, trả `0f` (quy ước, khớp hành vi Mathf).

```csharp
public static float InverseLerpUnclamped(float a, float b, float v)
{
    float d = b - a;
    return d != 0f ? (v - a) / d : 0f;
}
```

## 2. `Remap` / `RemapClamped` — ánh xạ khoảng

**Bản chất:** chuẩn hóa `v` từ khoảng vào `[0,1]` (InverseLerp) rồi bung ra khoảng ra (Lerp):
$$\mathrm{remap}(v) = o_{\min} + (o_{\max}-o_{\min})\cdot\frac{v - i_{\min}}{i_{\max}-i_{\min}}$$

Ứng dụng: cường độ va chạm `[0,50] m/s` → âm lượng `[0.2,1]`; health `[0,maxHp]` → màu.

```csharp
// Không clamp: v ngoài input → kết quả ngoài output (extrapolate).
public static float Remap(float v, float inMin, float inMax, float outMin, float outMax)
    => Mathf.LerpUnclamped(outMin, outMax, InverseLerpUnclamped(inMin, inMax, v));

// Clamp kết quả về [outMin,outMax] — dùng khi v có thể vượt biên input.
public static float RemapClamped(float v, float inMin, float inMax, float outMin, float outMax)
    => Mathf.LerpUnclamped(outMin, outMax, Mathf.Clamp01(InverseLerpUnclamped(inMin, inMax, v)));
```

> Tái dùng `Mathf.LerpUnclamped`/`Mathf.Clamp01` — không viết lại phần đã có.

### Tối ưu hot-loop: precompute nghịch đảo ở caller

Chi phí nặng nhất của `Remap` là **phép chia** trong `InverseLerpUnclamped` (chia tốn ~10–20×
phép nhân trên nhiều kiến trúc). Helper stateless **không cache được** — nhưng nếu caller lặp
với `inMin/inMax` **cố định** (map cùng một khoảng cho hàng nghìn phần tử/frame), hãy tính
`invRange = 1f/(inMax−inMin)` **một lần** rồi nhân. Cung cấp overload nhận sẵn hệ số:

```csharp
/// <summary>Remap khi caller đã precompute invRange = 1/(inMax−inMin). Đổi chia thành nhân.</summary>
public static float RemapPrecomputed(float v, float inMin, float invRange, float outMin, float outMax)
    => Mathf.LerpUnclamped(outMin, outMax, (v - inMin) * invRange);

// caller (hot-loop, cùng khoảng input):
// float invRange = 1f / (inMax - inMin);          // 1 phép chia duy nhất, ngoài vòng lặp
// for (int i = 0; i < n; i++)
//     out[i] = InterpolationHelper.RemapPrecomputed(src[i], inMin, invRange, oMin, oMax);
```

> Caller tự chịu trách nhiệm `inMax != inMin` (nếu bằng thì `invRange` = ∞). Overload này bỏ
> guard chia 0 **có chủ đích** — nó là đường "đã biết an toàn, cần tốc độ". Dùng `Remap`
> thường cho đường có guard.

## 3. `SmootherStep(t)` — Hermite bậc 5 (Ken Perlin)

`Mathf.SmoothStep` là Hermite **bậc 3** (`3t²−2t³`): chỉ triệt tiêu **vận tốc** ở biên (C¹).
`SmootherStep` là **bậc 5**, triệt tiêu thêm **gia tốc** ở biên (C²) → mượt hơn, không "giật
gia tốc" (jerk). Perlin tạo ra để tránh artifact ở ranh giới ô lưới noise.

$$S_2(t) = 6t^5 - 15t^4 + 10t^3 = t^3\big(t(6t - 15) + 10\big)$$

Chứng minh ràng buộc biên:
- `S₂'(t) = 30t⁴ − 60t³ + 30t² = 30t²(t−1)²` → **vận tốc** = 0 tại `t=0` và `t=1`.
- `S₂''(t) = 120t³ − 180t² + 60t = 60t(2t−1)(t−1)` → **gia tốc** = 0 tại `t=0` và `t=1`.

```csharp
public static float SmootherStep(float t)
{
    t = Mathf.Clamp01(t);
    return t * t * t * (t * (t * 6f - 15f) + 10f); // Horner: ít phép nhân, tránh Pow
}
```

> Chỉ thêm bản `SmootherStep(t)` nhận `t∈[0,1]`. Nếu cần remap từ `[edge0,edge1]` như GLSL,
> gọi `SmootherStep(InverseLerpUnclamped(edge0, edge1, x))` tại call site — không cần overload riêng.

## 4. `ExpDecay` & anh em — nội suy độc lập framerate ⭐

Lý do class này được đánh ⭐. Giải quyết lỗi kinh điển: **lerp mỗi frame cho kết quả khác nhau
ở FPS khác nhau.**

### 4.0. Vì sao `Mathf.Lerp(current, target, k)` mỗi frame là SAI

```csharp
current = Mathf.Lerp(current, target, 0.1f);   // ❌ tốc độ hội tụ phụ thuộc framerate
```
Mỗi frame kéo 10% quãng còn lại. Nhưng số frame/giây đổi theo máy:
- 60 FPS: còn lại `(1−0.1)⁶⁰ ≈ 0.0018` sau 1 giây.
- 30 FPS: còn lại `(1−0.1)³⁰ ≈ 0.042` sau 1 giây → **chậm hơn ~23 lần**.

Nhân `k*dt` **vẫn sai** vì lũy thừa không tuyến tính theo số bước.

### 4.1. Bản chất: phương trình vi phân phân rã mũ

Hành vi "kéo về target với tốc độ tỉ lệ khoảng cách còn lại":
$$\frac{dx}{dt} = -\lambda\,(x - x_{target})
\;\Rightarrow\; x(t) = x_{target} + (x_0 - x_{target})\,e^{-\lambda t}$$

Rời rạc cho 1 frame `Δt`:
$$x_{new} = x_{target} + (x_{old} - x_{target})\,e^{-\lambda\,\Delta t}$$

**Vì sao độc lập framerate:** đây là **nghiệm đúng** của PT vi phân, nên ghép 2 frame
`Δt₁+Δt₂` cho kết quả **y hệt** 1 frame `(Δt₁+Δt₂)` nhờ `e^{-λΔt₁}·e^{-λΔt₂}=e^{-λ(Δt₁+Δt₂)}`.
`Mathf.Lerp` ngây thơ không có tính chất này.

### 4.2. Vì sao dùng `math.exp` thay `Mathf.Exp`

`Mathf.Exp(x)` là `(float)Math.Exp((double)x)` — detour `float→double→float`, tính ở độ chính
xác kép rồi ép về. `Unity.Mathematics.math.exp(x)` thuần `float` (project đã có
`com.unity.mathematics` 1.3.2), tránh detour và được Burst tối ưu nếu gọi trong Job.

```csharp
using Unity.Mathematics;

/// <summary>Kéo a về b độc lập framerate. decay (λ, đơn vị 1/s) ~ tốc độ hội tụ (1=chậm, 25≈tức thì).</summary>
public static float ExpDecay(float a, float b, float decay, float dt)
    => b + (a - b) * math.exp(-decay * dt);

/// <summary>Hệ số t = 1 − e^(−λΔt) để dùng với BẤT KỲ lerp nào (màu, quaternion, vector).</summary>
public static float DecayFactor(float decay, float dt)
    => 1f - math.exp(-decay * dt);
// dùng: color = Color.LerpUnclamped(color, target, InterpolationHelper.DecayFactor(12f, Time.deltaTime));
```

- **`decay` (λ):** hằng số tốc độ `1/giây`, dải thực tế `[1, 25]`.
- **Half-life** (đi hết nửa quãng): `t½ = ln(2)/λ ≈ 0.693/λ`.

### 4.3. (tùy chọn) `ExpDecayHalfLife` — tham số bằng half-life

Trực giác hơn cho designer: "bao lâu đi hết nửa đường". `e^{-λΔt} = 2^{-Δt/t½}`.
```csharp
public static float ExpDecayHalfLife(float a, float b, float halfLife, float dt)
{
    if (halfLife <= 0f) return b;                       // guard chia 0 / phân kỳ
    return b + (a - b) * math.exp2(-dt / halfLife);     // math.exp2 = 2^x thuần float
}
```

> **Lưu ý biên chung:** `decay ≤ 0` → không hội tụ. `dt` rất lớn (lag spike): `e^{-λΔt}→0` nên
> kéo thẳng về `b` — an toàn, **không overshoot** (khác spring). `halfLife` phải `> 0`.

### Tối ưu hot-loop: precompute nghịch đảo half-life

`ExpDecayHalfLife` chia `dt / halfLife` mỗi lần gọi. Với `halfLife` cố định trên nhiều đối tượng,
precompute `invHalfLife = 1f/halfLife` một lần rồi nhân:

```csharp
public static float ExpDecayHalfLifePrecomputed(float a, float b, float invHalfLife, float dt)
    => b + (a - b) * math.exp2(-dt * invHalfLife); // caller đảm bảo invHalfLife hữu hạn (halfLife > 0)
```

> **Không precompute được phần `math.exp(-decay*dt)`** của `ExpDecay`/`DecayFactor`: mũ phụ thuộc
> `dt` — thay đổi mỗi frame — nên `e^{-λΔt}` phải tính lại mỗi lần, không có nghịch đảo nào tách ra
> được. Đây là giới hạn bản chất, không phải thiếu tối ưu. (Nếu `dt` được **cố định**, ví dụ gọi
> trong `FixedUpdate`, thì caller có thể cache thẳng cả hệ số `1 − e^{-λ·fixedDt}` một lần và dùng
> `Mathf.LerpUnclamped` — lúc đó không cần `ExpDecay` nữa.)

---

## Bảng tổng hợp — chỉ những gì class này thực sự chứa

| Hàm | Bản chất | Vì sao không dùng Mathf |
|-----|----------|--------------------------|
| `InverseLerpUnclamped` | `(v−a)/(b−a)` | `Mathf.InverseLerp` tự clamp |
| `Remap` / `RemapClamped` | invLerp∘lerp | `Mathf` không có |
| `RemapPrecomputed` | `(v−iMin)·invRange` | hot-loop: chia→nhân, bỏ guard |
| `SmootherStep` | `6t⁵−15t⁴+10t³` | `Mathf` chỉ có bậc 3 |
| `ExpDecay` ⭐ | `b+(a−b)e^{−λΔt}` | `Mathf` không có; dùng `math.exp` |
| `DecayFactor` ⭐ | `1−e^{−λΔt}` | dùng với mọi lerp |
| `ExpDecayHalfLife` | `2^{−Δt/t½}` | tham số trực giác |
| `ExpDecayHalfLifePrecomputed` | `2^{−Δt·invT½}` | hot-loop: chia→nhân |

## Checklist triển khai

- [ ] 1 file `InterpolationHelper.cs`, `public static class`.
- [ ] `using UnityEngine;` (Mathf) + `using Unity.Mathematics;` (math.exp/exp2) +
      `using System.Runtime.CompilerServices;` (AggressiveInlining).
- [ ] Guard chia 0: `InverseLerpUnclamped` (a==b→0), `ExpDecayHalfLife` (halfLife≤0→b).
- [ ] `AggressiveInlining` cho `InverseLerpUnclamped`, `Remap*`, `DecayFactor`, các `*Precomputed`.
- [ ] Tái dùng `Mathf.LerpUnclamped`/`Mathf.Clamp01` trong `Remap` — không viết lại.
- [ ] Overload `*Precomputed` bỏ guard chia 0 **có chủ đích** (đường "đã biết an toàn") — ghi rõ
      trong XML doc rằng caller chịu trách nhiệm hệ số hữu hạn.
- [ ] **KHÔNG** thêm hàm trùng Mathf (Clamp01/Lerp/MoveTowards/SmoothStep b3/InverseLerp clamped).
- [ ] **KHÔNG** thêm spring có `ref velocity` — để dành `SpringDamper` (Tầng 1).
- [ ] Test biên: `Remap(inMin)=outMin`, `Remap(inMax)=outMax`; `SmootherStep(0)=0/(1)=1/(0.5)=0.5`;
      `ExpDecay` với `dt` gộp = `dt` tách (kiểm tính độc lập framerate).

> **Chốt hiệu năng (trung thực):** "nhanh hơn" của `math.exp` vs `Mathf.Exp` chỉ đáng kể khi gọi
> dày đặc / trong Burst. Với animation/UI thông thường, chênh lệch không đáng kể — **benchmark
> trên đúng target build (Mono/IL2CPP/Burst)** trước khi coi đó là tối ưu thực sự.
