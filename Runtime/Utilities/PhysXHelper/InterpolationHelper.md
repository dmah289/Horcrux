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