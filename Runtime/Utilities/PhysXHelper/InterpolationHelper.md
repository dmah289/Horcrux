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

Đây là phần nêu **vấn đề** mà cả mục 4 giải quyết. Hiểu 4.0 thì 4.1 mới "sáng".

#### Đoạn code "trông có vẻ đúng"

```csharp
void Update() {
    current = Mathf.Lerp(current, target, 0.1f);   // ❌ tốc độ hội tụ phụ thuộc framerate
}
```
Nhìn thì hợp lý: mỗi frame nhích `current` 10% khoảng cách tới `target` — xa đi nhanh, gần đi
chậm, đúng cảm giác mượt. **Nhưng nó ẩn lỗi chí mạng: kết quả phụ thuộc FPS của máy.**

#### Chuyện gì thực sự xảy ra sau mỗi lần Lerp

`Lerp(current, target, 0.1)` đi 10% từ `current` tới `target` → **khoảng cách còn lại nhân với
`0.9`** (còn 90%). Gọi khoảng cách ban đầu là `D`:

| Sau frame | Khoảng cách còn lại |
|---|---|
| 1 | `D × 0.9` |
| 2 | `D × 0.9²` |
| 3 | `D × 0.9³` |
| n | `D × 0.9ⁿ` |

`n` chính là **số frame** — mà số frame trong 1 giây thì **tùy máy**.

#### Con số cụ thể: 60 FPS vs 30 FPS (cùng 1 giây thực)

- **60 FPS**: `Update` chạy 60 lần → còn lại `D × 0.9⁶⁰ ≈ D × 0.0018` → đi được **99.82%**.
- **30 FPS**: `Update` chạy 30 lần → còn lại `D × 0.9³⁰ ≈ D × 0.042` → đi được **95.8%**.

Tỉ số sót lại `0.042 / 0.0018 ≈ 23` → **máy 30 FPS còn cách target xa gấp ~23 lần máy 60 FPS.**
Hậu quả: máy mạnh camera "dính" sát, máy yếu camera "lờ đờ" — gameplay khác nhau tùy phần cứng.

#### Vì sao "nhân thêm dt" KHÔNG cứu được

Phản xạ đầu tiên là nhân `k` với `deltaTime`:
```csharp
current = Mathf.Lerp(current, target, k * Time.deltaTime);   // ❌ VẪN sai
```
Cách này **giảm** lỗi nhưng **không chữa gốc**. Mỗi frame nhân với `(1 − k·Δt)`, số lần nhân =
`1/Δt`, nên sau 1 giây tổng còn lại là `(1 − k·Δt)^(1/Δt)` — **lũy thừa của Δt, không tuyến tính**:

| FPS | Δt | `(1 − 0.1·Δt)^(1/Δt)` sau 1s |
|---|---|---|
| 60 | 0.0167 | ≈ 0.9048 |
| 30 | 0.0333 | ≈ 0.9046 |

Chênh lệch nhỏ hơn nhiều nên nhiều người tưởng "đủ" — nhưng vẫn **không bằng nhau tuyệt đối**, và
phình to khi `k` lớn hoặc FPS dao động (lag spike). Không thể sửa một quy trình nhân-lặp bằng cách
chỉnh hệ số nhân.

#### Gốc rễ

`Lerp` mỗi frame là **xấp xỉ rời rạc** — cắt đường cong thành nấc thang, mà số nấc phụ thuộc FPS.
Muốn kết quả **không đổi theo FPS**, cần **nghiệm chính xác của đường cong liên tục**, đánh giá đúng
theo `Δt` đã trôi — chứ không lắp ghép từng nấc. Đó là điều 4.1 đưa ra.

### 4.1. Bản chất: phương trình vi phân phân rã mũ

#### Khái niệm: "phương trình vi phân phân rã mũ" nghĩa là gì?

Tách 3 phần của cái tên:

**"Phương trình vi phân" (differential equation)** — phương trình thông thường (`x = 2y + 1`) tìm
một **con số**. Phương trình vi phân tìm cả một **hàm số** `x(t)`, và điều kiện ràng buộc lại nằm ở
**đạo hàm** của hàm đó (tốc độ nó thay đổi). Nói cách khác: thay vì cho biết "giá trị là bao nhiêu",
nó cho biết "giá trị thay đổi **nhanh chậm ra sao tại mỗi thời điểm**", rồi ta phải truy ngược ra
bản thân hàm. Ví dụ đời thường: "vận tốc xe luôn bằng 2× quãng đã đi" là một ràng buộc trên đạo hàm
→ giải ra sẽ biết vị trí xe theo thời gian.

**"Phân rã" (decay)** — một đại lượng **giảm dần về 0** theo thời gian (ngược với "tăng trưởng").
Ở đây đại lượng phân rã là **khoảng cách còn lại tới target** `(x − target)`: ban đầu lớn, càng về
sau càng nhỏ, tiến về 0.

**"Mũ" (exponential)** — kiểu phân rã **không tuyến tính**: không trừ đi một lượng cố định mỗi giây,
mà **nhân với một tỉ lệ cố định** mỗi giây. Hệ quả: mỗi khoảng thời gian bằng nhau thì khoảng cách
còn lại co lại theo cùng một **tỉ lệ** (ví dụ cứ 0.1s lại còn một nửa), tạo ra đường cong `e^{−λt}`
lao dốc lúc đầu rồi thoải dần — chứ không phải đường thẳng.

> **Ví dụ quen thuộc của phân rã mũ:** chất phóng xạ (cứ mỗi chu kỳ bán rã còn một nửa), cốc cà phê
> nguội dần (chênh lệch nhiệt độ với phòng giảm theo hàm mũ — định luật Newton), tụ điện xả điện.
> Bám camera về target là **cùng một khuôn toán học** với các hiện tượng đó.

Ghép lại: **phương trình vi phân phân rã mũ** = một ràng buộc trên đạo hàm, mà nghiệm của nó là một
đại lượng giảm về 0 theo hàm mũ. Dưới đây ta dựng nó từ trực giác.

#### Hành vi cần mô tả

Hành vi ta muốn mô tả: **càng xa target đi càng nhanh, càng gần đi càng chậm** — lao nhanh lúc
đầu, nhẹ nhàng dừng lúc gần tới. Làm sao viết thành toán?

#### Bước 1: Dịch câu nói thành phương trình

"Tốc độ thay đổi" của `x` là **đạo hàm** `dx/dt`. "Khoảng cách còn lại" là `(x − target)`.
Câu "tốc độ tỉ lệ với khoảng cách còn lại":
$$\frac{dx}{dt} = -\lambda\,(x - x_{target})$$

- `dx/dt` = vận tốc của `x`.
- `(x − target)` = còn cách target bao xa (có dấu).
- `λ` (lambda) = hằng số "độ hung hăng" — λ lớn kéo mạnh, λ nhỏ kéo yếu.
- **Dấu trừ** = mấu chốt: nếu `x` ở **trên** target (`x−target > 0`) thì cần đi **xuống** (vận tốc
  âm) → dấu trừ đảo lại. Nó luôn kéo `x` **ngược hướng** độ lệch = **về phía** target. Cơ chế tự
  hiệu chỉnh.

Đây là **phương trình vi phân**: ẩn số là *cả một hàm* `x(t)`, ràng buộc qua đạo hàm của chính nó.

#### Bước 2: Giải ra — hàm nào thỏa mãn?

Cần hàm mà đạo hàm bằng `−λ` lần chính nó. Hàm **duy nhất** có tính chất "đạo hàm bằng chính nó
nhân hằng số" là **hàm mũ** `e`:
$$\frac{d}{dt}e^{-\lambda t} = -\lambda\, e^{-\lambda t}$$

Nên nghiệm là:
$$\boxed{\,x(t) = x_{target} + (x_0 - x_{target})\,e^{-\lambda t}\,}$$

Kiểm tra ở các mốc thời gian:

| Thời điểm | `e^{−λt}` | `x(t)` | Ý nghĩa |
|---|---|---|---|
| `t = 0` | `e⁰ = 1` | `x₀` | Bắt đầu đúng vị trí ban đầu ✅ |
| `t → ∞` | `e^{−∞} = 0` | `x_target` | Cuối cùng chạm đúng target ✅ |
| ở giữa | giảm 1→0 | tiến dần | Nhanh lúc đầu, chậm lúc cuối ✅ |

`e^{−λt}` là **phần khoảng cách còn sót lại**: bắt đầu 100% (=1), rơi về 0 theo cấp số nhân. Đó là
lý do tên gọi **exponential decay** — độ lệch tan biến theo hàm mũ.

#### Bước 3: Từ hàm liên tục → dùng trong game (rời rạc hóa)

Công thức trên cho **mọi** `t`, nhưng game chạy theo từng frame với bước nhỏ `Δt` (`deltaTime`).
Mẹo: coi vị trí frame trước `x_old` là "điểm xuất phát mới" `x₀`, áp công thức cho đúng 1 khoảng `Δt`:
$$x_{new} = x_{target} + (x_{old} - x_{target})\,e^{-\lambda\,\Delta t}$$

Đây chính là dòng code (`a = x_old`, `b = x_target`, `decay = λ`, `dt = Δt`):
```csharp
b + (a - b) * math.exp(-decay * dt)
```

#### Bước 4: Vì sao "độc lập framerate" — điều then chốt

Tính chất vàng của hàm mũ:
$$e^{-\lambda \Delta t_1} \cdot e^{-\lambda \Delta t_2} = e^{-\lambda(\Delta t_1 + \Delta t_2)}$$

Dịch ra: **chạy 2 frame nhỏ liên tiếp** (`Δt₁` rồi `Δt₂`) cho kết quả **y hệt** **1 frame lớn**
bằng tổng. Nên dù 30 FPS (bước lớn, ít lần) hay 120 FPS (bước nhỏ, nhiều lần), sau cùng 1 giây thực
→ tới **đúng cùng một chỗ**. `Mathf.Lerp` mỗi frame (mục 4.0) không có tính chất này vì nó là xấp
xỉ rời rạc, còn đây là **nghiệm giải tích chính xác**.

> **Trực giác cho λ:** λ là "tốc độ tan" của khoảng cách. Con số dễ hình dung hơn là **half-life**
> `t½ = ln(2)/λ` — thời gian đi được **nửa** quãng còn lại. λ=12 → t½ ≈ 0.058s (rất nhanh);
> λ=2 → t½ ≈ 0.35s (chậm rãi).

### 4.2. Vì sao dùng `math.exp` thay `Mathf.Exp`

Phần này **không phải toán**, mà là **cách máy tính hàm `e^x`**. Cùng kết quả toán học, khác nhau
về hiệu năng.

#### `Mathf.Exp` đi đường vòng qua `double`

Ruột của `Mathf.Exp` (Unity):
```csharp
public static float Exp(float power) => (float)Math.Exp((double)power);
```
Nó làm 3 việc:
1. **`float → double`**: nống số `float` 32-bit lên `double` 64-bit.
2. **`Math.Exp`**: tính `e^x` ở **độ chính xác kép** (64-bit) — nặng hơn vì xử lý gấp đôi số bit.
3. **`double → float`**: ép kết quả 64-bit ngược về 32-bit.

Bạn chỉ cần độ chính xác `float`, nhưng phải trả giá cho cả hành trình `float→double→tính→float` —
2 lần ép kiểu + tính ở độ chính xác không cần thiết.

#### `math.exp` (Unity.Mathematics) tính thẳng bằng `float`

```csharp
math.exp(x)   // x là float, ra float, không detour qua double
```
Lợi ích:
1. **Không ép kiểu** — vào `float`, ra `float`.
2. **Tính ở 32-bit** — nhẹ hơn 64-bit.
3. **Burst-friendly** — gọi trong Job `[BurstCompile]` thì Burst biên dịch thành **lệnh SIMD vector
   hóa** (tính nhiều giá trị cùng lúc). `Mathf.Exp` không được vậy.

Project đã có sẵn `com.unity.mathematics` 1.3.2 → dùng được ngay.

#### Trung thực về mức lợi

- Kết quả **không giống nhau tuyệt đối tới từng bit** — `math.exp` thuần float có thể lệch vài ULP
  so với bản qua double. Với game **không nhìn thấy được**, nhưng đừng dùng ở chỗ cần khớp bit.
- Khác biệt tốc độ **chỉ đáng kể khi gọi dày** (chục nghìn lần/frame) hoặc trong Burst. Gọi vài chục
  lần/frame thì gần như không đo được → **benchmark trên máy thật** mới chốt được (xem ghi chú cuối file).

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

### 4.2b. `DecayFactor` — tách hệ số `t` để dùng với mọi kiểu

`DecayFactor` là "anh em" của `ExpDecay`: thay vì làm luôn phép nội suy, nó chỉ trả về **hệ số
`t`** để bạn tự cắm vào một hàm Lerp bất kỳ.

$$\text{DecayFactor}(\lambda, \Delta t) = 1 - e^{-\lambda\,\Delta t}$$

#### Nó từ đâu ra — tách từ `ExpDecay`

Biến đổi công thức `ExpDecay` về **đúng dạng Lerp** `a + (b−a)·t`. Đặt `k = e^{−λΔt}`:
$$x_{new} = b + (a-b)\,k = ak + b(1-k) = a + (b - a)(1 - k)$$

So với Lerp chuẩn `Lerp(a,b,t) = a + (b−a)·t`, hai cái khớp nhau khi:
$$t = 1 - e^{-\lambda\Delta t} = \text{DecayFactor}(\lambda, \Delta t)$$

→ **`DecayFactor` chính là giá trị `t` cắm vào Lerp để có exponential decay.** Tức là
`ExpDecay(a,b,λ,dt)` ≡ `Lerp(a, b, DecayFactor(λ,dt))`.

#### Vì sao `1 − e^{...}` chứ không phải `e^{...}`

Hai đại lượng bù nhau, đừng lẫn:

| Biểu thức | Ý nghĩa | Chạy từ |
|---|---|---|
| `e^{−λΔt}` | phần khoảng cách **còn lại** (chưa đi) | 1 → 0 |
| `1 − e^{−λΔt}` | phần khoảng cách **đã đi** = `t` của Lerp | 0 → 1 |

`ExpDecay` viết theo "phần còn lại" (nhân `a−b`); Lerp cần "phần đã đi" (nhân `b−a`). Dấu trừ đảo
chiều `(a−b)→(b−a)` chính là chỗ `e` biến thành `1−e`. Kiểm biên:
- `dt=0`: `1−e⁰ = 0` → Lerp trả `a` (chưa nhúc nhích) ✅
- `dt→∞`: `1−e^{−∞} = 1` → Lerp trả `b` (tới đích) ✅

#### Vì sao cần hàm riêng — dùng cho mọi kiểu dữ liệu

`ExpDecay` chỉ chạy với `float`. Nhưng `t` là **một số vô hướng** → dùng được với **bất kỳ** Lerp nào:

```csharp
float t = Interpolator.DecayFactor(12f, Time.deltaTime);   // tính 1 lần
pos   = Vector3.LerpUnclamped(pos, targetPos, t);          // vị trí
rot   = Quaternion.SlerpUnclamped(rot, targetRot, t);      // xoay
color = Color.LerpUnclamped(color, targetColor, t);        // màu
```

→ `ExpDecay` là bản đóng gói sẵn cho `float` (phổ biến nhất); `DecayFactor` là bản linh hoạt cho
Vector3/Quaternion/Color — tính hệ số một lần, áp cho mọi kiểu.

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
