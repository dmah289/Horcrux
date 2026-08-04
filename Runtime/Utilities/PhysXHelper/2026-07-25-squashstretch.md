# SquashStretch Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.
>
> **Tự chứa, để tự code lại:** §0 dẫn giải toán (không nhảy bước), mỗi task có code dán-được + trỏ về §0.8 để kiểm. Mọi con số là **đo thực float32**, không ước lượng. Mỗi khái niệm giải **một lần** — chỗ khác trỏ `§x`.

**Goal:** biến dạng nén/giãn **bảo toàn thể tích**: một trục biến dạng → (các) trục vuông góc bù lại sao cho tích các hệ số scale `= 1`. Static, stateless; trả `Vector3` gán thẳng `transform.localScale`.

**Architecture:** 1 file, 1 công thức lõi $c = s^{-1/n}$ (§0.2). Ba helper chỉ khác nhau ở **cách sinh `s`** rồi cùng gọi lõi (§0.5) → công thức bù không lặp ở đâu.

**Stack:** C# (Unity), `Unity.Mathematics` + `UnityEngine.Vector3` — nhất quán `Interpolator.cs`, `DampedOscillator.cs`. Thuần toán, không Addressables/UniTask.

| Contract | Giá trị |
|---|---|
| Namespace | `Horcrux.Runtime.Utilities.PhysXHelper` |
| Phụ thuộc | Tầng 2 ← `Tweening.Easing.Easer`, ← `Utilities/Common` (`AxisType`, `CoordinateSystem`) — **đã có cả hai** |
| Zero-GC | thuần `float`/`Vector3`; không `new` ref-type, LINQ, closure, string; không field instance |
| SOLID | class chỉ **tính scale** — không xoay, không ghi transform; mở rộng qua enum/overload |
| Tái dùng | không viết lại easing, không khai báo lại enum trục/hệ trục |

---

# §0. Nền toán học

> **Số đo tái lập được:** mọi con số "đo" ở §0.5–0.7 sinh bằng cách cài lại **đúng** công thức của repo (`BackEase`/`ElasticEase`/`BounceEase`; `math.rsqrt` = `1f/sqrt`) ở **float32** rồi quét đều miền — không lấy từ tài liệu ngoài. Tự kiểm: quét `s` (hoặc `t`) bước nhỏ, so $\prod \sigma_i$ với `1`.

## 0.1. Bản chất

Vật thể "sống động" thì **biến dạng** khi va chạm/tăng tốc: đáp đất → bẹp (squash), bay nhanh → kéo dài (stretch). Mắt thấy "tự nhiên" khi **thể tích giữ nguyên** — như cục bột nặn: ấn dẹt thì phình ngang, lượng bột không đổi.

```
      nghỉ              squash (s<1)            stretch (s>1)
   ┌───────┐        ┌───────────────┐              ┌─────┐
   │       │        │               │ ← c = phình  │     │ ← c = co
   │  1×1  │  ───►  └───────────────┘              │     │
   │       │                ↑ s = nén              │     │ ← s = giãn
   └───────┘                                       └─────┘
     ∏ = 1                ∏ = s·c = 1              ∏ = s·c = 1
```

| Thành phần | Vai trò |
|---|---|
| **Trục chính** `s` | trục *chủ động* biến dạng: `s<1` nén, `s>1` giãn |
| **Trục bù** `c` | (các) trục vuông góc *phình/co ngược* để giữ thể tích |
| **Ràng buộc** | tích mọi hệ số scale `= 1` — nguồn của mọi công thức dưới |

Toàn hệ chỉ là một bài: **phình bao nhiêu cho vừa?**

## 0.2. Suy công thức bù

Ký hiệu dùng xuyên suốt §0:

| Ký hiệu | Nghĩa | Miền |
|---|---|---|
| `s` | hệ số scale trục chính | kẹp về `[1e-4, 1e4]` (§0.6) |
| `c` | hệ số scale **mỗi** trục bù | suy từ `s` |
| `n` | số trục **bù** (không tính trục chính) | `{1, 2}` (§0.3) |
| $\sigma_x, \sigma_y, \sigma_z$ | ba thành phần `Vector3` trả về | |
| $V_0$ | thể tích lúc nghỉ (mọi scale `= 1`) | `1` |
| `m` | `minScale` — `s` lúc bẹp sâu nhất | `(0, 1)`, xem ngưỡng §0.5 |
| `e` | `eased` = `Easer.Evaluate(ease, t)` | có thể vượt `[0,1]` (§0.5) |
| `u` | `saturate(impact/maxImpact)` | `[0, 1]` |
| `k` | `stretchPerSpeed` | |

Hình thức hóa câu hỏi §0.1, từng bước:

**①** Thể tích sau scale = tích các hệ số — chính là định thức ma trận scale $\det(S) = \sigma_x \sigma_y \sigma_z$. Lúc nghỉ mọi hệ số `= 1` nên $V_0 = 1$.

**②** Gọi `n` = số trục vuông góc được bù, mỗi trục bù cùng hệ số `c`. Thể tích mới $= s \cdot c^{n}$. Đặt bằng $V_0$:

$$s \cdot c^{n} = 1$$

**③** Chia hai vế cho `s` (hợp lệ vì `s > 0`, xem §0.6):

$$c^{n} = \frac{1}{s}$$

**④** Lấy căn bậc `n` hai vế:

$$\boxed{\;c = s^{-1/n}\;}$$

**⑤ Kiểm lại** — thay ④ vào ②:

$$s \cdot \left(s^{-1/n}\right)^{n} = s \cdot s^{-1} = s^{0} = 1$$

✓ đúng với mọi `n`.

**Bước ② đã giả định `n` trục bù *bằng nhau*** — đó là lựa chọn, không phải hệ quả. Ràng buộc gốc chỉ đòi $\prod_i c_i = 1/s$ → vô số nghiệm:

| Cách chia lượng bù | Hình dáng thu được | |
|---|---|---|
| $c_1 = \dots = c_n$ | mặt phẳng vuông góc trục chính biến dạng **đẳng hướng**, đối xứng | ✓ **chọn** |
| thiên vị một trục | méo lệch; phải thêm tham số "hướng thiên vị" mà gameplay không cần | ✗ |

→ **Lựa chọn mô hình** cho game feel — vật lý không cấm cách dưới.

## 0.3. `n` = số trục bù → hai phép rẻ, không cần `pow`

`n` **không** phải số chiều không gian, mà là số trục *được phép bù* — do `CoordinateSystem` quyết định:

| `CoordinateSystem` | `n` | $c = s^{-1/n}$ | Code | Trục ngoài hệ |
|---|---|---|---|---|
| `XY` / `XZ` / `YZ` | 1 | $s^{-1}$ | `1f / s` | giữ `= 1` |
| `XYZ` | 2 | $s^{-1/2}$ | `math.rsqrt(s)` | — (cả 2 trục kia bù) |

Vì `n` chỉ nhận `{1,2}`, không cần hàm lũy thừa tổng quát:

| Cách tính $c = s^{-1/n}$ | Thực chất | |
|---|---|---|
| `math.pow(s, -1f/n)` | `exp` + `log` — đắt, lại thừa vì `n` cố định | ✗ |
| `1f / math.sqrt(s)` (`n=2`) | **giống hệt** `rsqrt` (xem cột dưới), chỉ dài dòng hơn | ✗ |
| `1f / s` (`n=1`) · `math.rsqrt(s)` (`n=2`) | 1 phép, diễn đạt thẳng số mũ `−1` / `−1/2` | ✓ **chọn** |

→ **Code không bao giờ gọi `math.pow`.** Lưu ý `math.rsqrt` của `Unity.Mathematics` *định nghĩa là* `1.0f / sqrt(x)` — nên nó không nhanh hơn `1f/math.sqrt` ở đường managed; nó thắng vì (a) diễn đạt trực tiếp $s^{-1/2}$, (b) Burst hạ được xuống intrinsic (§0.7).

**Cả hai chế độ đều bảo toàn thể tích** — trục ngoài mặt phẳng giữ đúng `1`, nên tích cả ba thành phần vẫn `= 1`:

$$\sigma_x \sigma_y \sigma_z = s \cdot \tfrac{1}{s} \cdot 1 = 1$$

Khác biệt không phải "diện tích vs thể tích", mà là **phân bố** lượng bù:

| | `XYZ` (`n=2`) | Mặt phẳng (`n=1`) |
|---|---|---|
| Bù | chia đều 2 trục, đẳng hướng | dồn hết 1 trục, dị hướng |
| `s=0.5` → `c` | `1.414` mỗi trục | `2.0` một trục |
| Dùng cho | mesh 3D | sprite / gameplay phẳng (trục thứ 3 vô nghĩa) |

→ Chọn theo **look muốn có**, không theo "game 2D hay 3D".

## 0.4. Trục nào bù — bảng tra

`coordinateSystem` cho *tập trục được bù*; `primaryAxis` cho *trục chủ động*; trục bù = phần còn lại của tập.

| Hệ | primary=X | primary=Y | primary=Z |
|---|---|---|---|
| `XY` | `(s, c, 1)` | `(c, s, 1)` | — thoái hóa |
| `XZ` | `(s, 1, c)` | — thoái hóa | `(c, 1, s)` |
| `YZ` | — thoái hóa | `(1, s, c)` | `(1, c, s)` |
| `XYZ` | `(s, c, c)` | `(c, s, c)` | `(c, c, s)` |

**Thoái hóa** = `primaryAxis` nằm ngoài mặt phẳng (vd `XY` + `Z`): không có trục đối tác → bài toán **vô định**, không nghiệm nào bảo toàn được. Xử lý:

| Lựa chọn | Hệ quả | |
|---|---|---|
| trả `Vector3.one` | no-op; `∏ = 1` vẫn đúng, không NaN | ✓ **chọn** |
| bù bừa một trục ngoài hệ | phá ngữ nghĩa `CoordinateSystem` (sprite bị scale trục depth) | ✗ |
| throw / assert | trả giá runtime cho lỗi lập trình, ở utility hot-path | ✗ |

→ **Fail-safe có chủ đích:** sai cấu hình thì thấy "không có gì xảy ra", không thấy artifact hình học.

## 0.5. Ba cách sinh `s`

Lõi §0.2–0.4 đã xong. Ba helper chỉ khác nhau ở chỗ **`s` từ đâu ra**:

| Helper | Sinh `s` | Miền `s` |
|---|---|---|
| **Impact** | `s = lerp(1, minScale, saturate(impact/maxImpact))` | `[minScale, 1]` |
| **Directional** | `s = 1 + clamp(speed·k, 0, maxStretch−1)` | `[1, maxStretch]` |
| **Time** | `s = lerp(minScale, 1, Easer.Evaluate(ease, t))` | `[minScale, s_peak]` |

**Vì sao cả ba đều tuyến tính?** Mapping chỉ cần đi qua đúng 2 mốc đầu–cuối, đơn điệu, và rẻ:

| Dạng mapping | Hệ quả | |
|---|---|---|
| `lerp` (bậc nhất) | thỏa cả 3 tiêu chí, 1 phép `mad` | ✓ **chọn** |
| nhồi phi tuyến vào mapping | trộn 2 trách nhiệm — độ cong đã là việc của `Easer` | ✗ |

**Guard của từng mapping** (guard của *lõi* nằm ở §0.6):

| Guard | Bỏ thì sao |
|---|---|
| `maxImpact > 0` trước phép chia | `impact/0` → `∞`/`NaN`; `saturate(NaN)=1` (§0.6) → bẹp **sâu nhất** thay vì không bẹp |
| `saturate` (không chỉ chia) | `impact > maxImpact` → `s < minScale` bẹp quá sàn; `impact < 0` → `s > 1` **giãn thay vì nén** |
| `max(maxStretch−1, 0)` **trước** `clamp` | `maxStretch < 1` → clamp vào `[0, số âm]` → `s < 1` = **squash, ngược hẳn ý định** |
| `clamp` biên dưới `0` | `speed < 0` hay `k < 0` → `s < 1`, nén khi đang bay nhanh |

### `math.lerp` phải là bản unclamped — điểm mong manh nhất

`Unity.Mathematics.lerp(a,b,t) = a + t*(b-a)` — **không kẹp `t`**. `Mathf.Lerp` thì **kẹp** `t ∈ [0,1]`. Ở mapping Time, khác biệt này quyết định hệ còn "stretch" hay không:

| `eased` (OutBack @ `t=0.5801`) | `math.lerp(0.6, 1, e)` | `Mathf.Lerp` |
|---|---|---|
| `1.100004` | **`1.040002`** → `s>1`, **giãn** ✓ | `1.0` → mất vọt lố ✗ |

→ `Mathf.Lerp` biến animation thành **squash-only**. Đã dùng `math.lerp` thì đừng "sửa" thành `Mathf.Lerp`.

### Vọt lố đến đâu — dữ liệu đo

Từ `s = m + e(1−m)` với `m = minScale`, `e = eased`, suy ra biên độ:

$$s_{\text{peak}} = 1 + (e_{\max} - 1)(1 - m)$$

Quét 200 001 mẫu/curve (công thức lấy nguyên từ `BackEase`/`ElasticEase`/`BounceEase` của repo):

| EaseType | $e_{\max}$ @ `t` | $e_{\min}$ | `s_peak` (`m=0.6`) | `m` tối thiểu để `s>0` |
|---|---|---|---|---|
| `OutBack` | `1.100004` @ `0.5801` | `0` | `1.0400` | — |
| `InOutBack` | `1.100151` @ `0.7594` | `−0.100151` | `1.0401` | `0.0910` |
| `InBack` | `1.000000` @ `1.0` | `−0.100004` | `1.0000` | `0.0909` |
| `OutElastic` | **`1.373098`** @ `0.1347` | `0` | **`1.1492`** | — |
| `InOutElastic` | `1.118348` @ `0.5960` | `−0.118348` | `1.0473` | `0.1058` |
| `InElastic` | `1.000000` @ `1.0` | **`−0.373098`** | `1.0000` | **`0.2717`** |
| `In`/`Out`/`InOutBounce` | `1.000000` @ `1.0` | `0` | `1.0000` | — |

Ba điều bảng này lật lại trực giác thường gặp:

- **Bounce KHÔNG vượt `[0,1]`** → chọn Bounce là *không có pha stretch*, chỉ squash rồi nảy về nghỉ.
- **`OutElastic` vọt lố mạnh hơn `OutBack`** (`1.373` vs `1.100` — gấp ~3.7× phần vượt), không nhẹ hơn.
- **Họ `In*` không bao giờ cho `s>1`** ($e_{\max}=1$); chúng chỉ *undershoot* → bẹp sâu **hơn** `minScale`.

Vì $s_{\text{peak}}$ tỉ lệ `(1−m)`: muốn squash nhẹ mà stretch rõ → `OutElastic`, hoặc chồng thêm `GetDirectionalStretch`.

### Undershoot — điều kiện an toàn của `minScale`

Cột cuối bảng trên suy từ `s = m + e(1−m) ≤ 0`:

$$e_{\min} \le \frac{-m}{1-m} \quad\Longleftrightarrow\quad m \le \frac{\lvert e_{\min} \rvert}{1 + \lvert e_{\min} \rvert}$$

Curve nguy hiểm nhất là `InElastic` ($e_{\min} = -0.373098$) → ngưỡng `0.2717`. Kiểm:

| `m` | `s` tại $e_{\min}$ của `InElastic` | |
|---|---|---|
| `0.60` | `0.450761` | ✓ |
| `0.28` | `0.011369` | ✓ sát biên |
| `0.20` | **`−0.098478`** | ✗ guard §0.6 kích hoạt → **pop** thành mảnh 1 frame |

→ **Quy tắc dùng:** giữ `minScale > 0.28` thì không curve nào trong `Easer` đẩy `s ≤ 0`. Cần nhỏ hơn → tránh họ `*Elastic`. Guard §0.6 chặn `NaN`, **không** chặn được cú pop thị giác.

## 0.6. Guard biên của lõi

Lõi có phép chia và `rsqrt` → phải chặn miền vào. Hai hố:

| Input | Nếu không guard | Hệ quả |
|---|---|---|
| `s ≤ 0` | `rsqrt(≤0)` → `NaN`, `1/0` → `∞` | `localScale` NaN → Unity reject transform |
| `s = +∞` | `1/∞ = 0`, `rsqrt(∞) = 0` | `localScale = (0, ∞, 0)`, `∏ = NaN` |

Kẹp **hai đầu** về `[1e-4, 1e4]`. Chọn khoảng **nghịch đảo-đối xứng** ($\text{MaxScale} = 1/\text{MinScale}$) để hệ số bù cũng nằm trong chính khoảng đó, không thể tràn:

| `s ∈` | `n=1` → `c = 1/s` | `n=2` → `c = 1/√s` |
|---|---|---|
| `[1e-4, 1e4]` | `[1e-4, 1e4]` khít biên | `[1e-2, 1e2]` trong biên |

**Thứ tự `min(max(...))` là cố ý, không dùng `math.clamp`.** `Unity.Mathematics` định nghĩa `min(x,y) = IsNaN(y) || x<y ? x : y` — **NaN ở đối số thứ hai bị bỏ qua**. Vì `math.clamp(x,lo,hi) = max(lo, min(hi, x))`, phép `min(hi, NaN)` trả `hi` → NaN leo lên **trần**. Đo thực:

| `primaryScale` | `max(Min, s)` | `math.clamp` | `min(max(s,Min),Max)` ← chọn |
|---|---|---|---|
| `+∞` | **`∞`** ✗ | `1e4` ✓ | `1e4` ✓ |
| `NaN` | `1e-4` ✓ | **`1e4`** ✗ phình 10⁴× | `1e-4` ✓ |
| `1e9` | `1e9` ✗ vô nghĩa | `1e4` ✓ | `1e4` ✓ |
| `-∞` / `-3` / `0` | `1e-4` ✓ | `1e-4` ✓ | `1e-4` ✓ |

→ Chỉ `min(max(...))` đúng **cả hai** cực: chặn `+∞`, và đẩy `NaN` về **sàn** (mảnh vô hình, vô hại) thay vì trần.

## 0.7. Sai số float — "bảo toàn" chính xác đến đâu

`math.rsqrt` của `Unity.Mathematics` là `1.0f / sqrt(x)` — **chính xác float đầy đủ**, không phải fast-approx kiểu Quake. Nhưng hai lần làm tròn (`sqrt`, rồi chia) vẫn để lại sai số. Quét `s` trên `[1e-4, 1e4]`, 120 001 mẫu:

| Chế độ | Tích kiểm | max $\lvert \prod - 1 \rvert$ | ulp | `s` xấu nhất |
|---|---|---|---|---|
| `n=1` | $s \cdot c$ | `5.96e-8` | **0.50** | `1.000e-4` |
| `n=2` | $s \cdot c^{2}$ | `2.38e-7` | **2.00** | `1.42e-4` |

Mẫu (`n=2`): `s=0.5` → `c=1.414214`, `∏=0.9999999` (0.50 ulp) · `s=1.5` → `c=0.8164966`, `∏=0.9999999` (1.00 ulp) · `s ∈ {1, 0.25, 0.6, 1e-4, 1e4}` → `∏=1` đúng khít.

→ Phát biểu đúng: **bảo toàn trong 2 ulp**, không phải "tuyệt đối". `2.4e-7` là vô hình về thị giác, nhưng **đừng assert `∏ == 1f`** — so `abs(∏ − 1) < 1e-6`.

> **Lưu ý Burst:** trong job Burst với `FloatMode.Fast`, `rsqrt` có thể bị hạ xuống lệnh xấp xỉ `rsqrtps` (~12 bit) → sai số nhảy lên ~`1e-3`. Bảng trên đúng cho đường managed/IL2CPP. Cần chính xác trong Burst → `FloatMode.Strict`.

## 0.8. Kiểm mốc — bảng duy nhất

Mọi task kiểm theo bảng này (cột **T** = task). Trục/hệ mặc định `AxisType.Y, CoordinateSystem.XYZ` nếu không ghi khác.

| # | Input | Kỳ vọng | T |
|---|---|---|---|
| **L1** | `s=1`, mọi hệ | `(1,1,1)` — không biến dạng ($c = 1^{-1/n} = 1$) | 1 |
| **L2** | `s=0.5`, `XY` | `(2, 0.5, 1)`; `∏=1` err `0` | 1 |
| **L3** | `s=0.5`, `XYZ` | `(1.414214, 0.5, 1.414214)`; `∏=1` err `5.96e-8` | 1 |
| **L4** | `s=2`, primary=X, `XYZ` | `(2, 0.7071068, 0.7071068)`; `∏=1` | 1 |
| **L5** | `s=0.5`, primary=Z, `XZ` | `(2, 1, 0.5)` | 1 |
| **L6** | `s=0.5`, primary=Z, `XY` | `(1,1,1)` — thoái hóa, no-op (§0.4) | 1 |
| **G1** | `s=-3`, `XY` | `(10000, 1e-4, 1)` — kẹp sàn, `∏=1` | 1 |
| **G2** | `s=+∞`, `XYZ` | `(0.01, 10000, 0.01)` — kẹp trần, hữu hạn | 1 |
| **G3** | `s=NaN`, `XYZ` | `(100, 1e-4, 100)` — NaN về **sàn**, không trần (§0.6) | 1 |
| **I1** | `Impact(0, 10, 0.6)` | `s=1` → `(1,1,1)` | 2 |
| **I2** | `Impact(5, 10, 0.6)` | `s=0.8` → `(1.118034, 0.8, 1.118034)` | 2 |
| **I3** | `Impact(10, 10, 0.6)` | `s=0.6` → `(1.290994, 0.6, 1.290994)` | 2 |
| **I4** | `Impact(999, 10, 0.6)` | `s=0.6` — kẹp, không bẹp sâu hơn | 2 |
| **I5** | `Impact(5, 0, 0.6)` | `s=1` — `maxImpact≤0` → không bẹp | 2 |
| **I6** | `Impact(-5, 10, 0.6)` | `s=1` — `saturate` chặn giãn | 2 |
| **D1** | `Directional(0, 0.1, 2)` | `s=1` → `(1,1,1)` | 2 |
| **D2** | `Directional(5, 0.1, 2)` | `s=1.5` → `(0.8164966, 1.5, 0.8164966)` | 2 |
| **D3** | `Directional(999, 0.1, 2)` | `s=2` — kẹp trần | 2 |
| **D4** | `Directional(5, 0.1, 0.5)` | `s=1` — `maxStretch<1` → không giãn, **không** squash | 2 |
| **D5** | `Directional(-5, 0.1, 2)` | `s=1` | 2 |
| **T1** | `Time(0, OutBack, 0.6)` | `s=0.6` → `(1.290994, 0.6, 1.290994)` (đo `f(0)=0`) | 3 |
| **T2** | `Time(1, OutBack, 0.6)` | `s=1` → `(1,1,1)` (đo `f(1)=1`) | 3 |
| **T3** | `Time(0.5, Linear, 0.6)` | `s=0.8` → `(1.118034, 0.8, 1.118034)` | 3 |
| **T4** | `Time(0.5801, OutBack, 0.6)` | `eased=1.1000` → `s=1.0400`: trục chính **giãn**, trục bù `<1` | 3 |
| **T5** | `Time(0.1347, OutElastic, 0.6)` | `eased=1.3731` → `s=1.1492` — vọt lố mạnh nhất | 3 |
| **T6** | `Time(t, OutBounce, 0.6)`, mọi `t` | `s ≤ 1` — không có pha stretch (§0.5) | 3 |
| **T7** | `Time(-5, OutBack, 0.6)` / `Time(99, …)` | `s=0.6` / `s=1` — `Easer` tự kẹp `t` | 3 |
| **T8** | `Time(0.8653, InElastic, 0.2)` | `s=−0.0985` → guard kẹp → `(100, 1e-4, 100)`; **`minScale` quá nhỏ** (§0.5) | 3 |

---

# Bản đồ triển khai

Luồng dữ liệu — ba nguồn sinh `s` đổ về **một** lõi, một đầu ra:

```
  impact, maxImpact, minScale ──┐
                                │
  speed, k, maxStretch ─────────┼──► s ──► GetVolumePreservingScale ──► Vector3
                                │          guard §0.6 → c = s^(-1/n)     (localScale)
  t, easeType, minScale ────────┘            §0.2  → trục bù §0.4
        └─► Easer.Evaluate
                              (§0.5)
```

```
Utilities/
├── Common/                     AxisType · CoordinateSystem + Is2D()      (đã có)
├── Tweening/Easings/           Easer.Evaluate(EaseType, t)               (đã có)
└── PhysXHelper/SquashStretch.cs
     ├── Task 1  GetVolumePreservingScale (lõi + guard)     §0.2–0.4, 0.6
     ├── Task 2  GetSquashFromImpact + GetDirectionalStretch      §0.5
     └── Task 3  GetSquashStretch (time-driven, ← Easer)          §0.5
```

Thứ tự **1 → 2 → 3**: task sau chỉ *thêm* hàm, không sửa hàm cũ (Open/Closed). `Easer` cô lập ở task cuối để hai task đầu thuần toán, không phụ thuộc gì.

> **Enum dùng chung — không khai báo lại trong file này.** `CoordinateSystem` mã hóa *mặt phẳng nào* (`XY`/`XZ`/`YZ`), thứ mà một enum cục bộ kiểu `VolumeMode {Area2D, Volume3D}` không biểu diễn nổi (§0.3–0.4); hệ khác (`GridSnapFeedback`, `Cascade`) cũng cần đúng khái niệm đó.

## 4 đảm bảo của code (áp cho cả 3 task)

| Đảm bảo | Ở hệ này cụ thể là |
|---|---|
| **Đúng đắn** | mọi nhánh thỏa $\prod \sigma_i = 1$ trong 2 ulp (§0.7); mỗi công thức đã kiểm mốc (§0.8) **trước** khi vào code; miền vào đóng kín kể cả `±∞`/`NaN` (§0.6) |
| **Tối ưu CPU** | 0 hàm siêu việt (§0.3); 1 phép chia/`rsqrt` tính trước, dùng chung 9 nhánh; 2 jump table thay chuỗi so sánh tuần tự; wrapper mỏng `AggressiveInlining` |
| **Giảm GC** | 0 B/call — `Vector3` là struct trên stack; không `new` ref-type, LINQ, closure, string; class không field |
| **Self-doc** | `GetVolumePreservingScale` ≠ `Process`; comment chỉ nói *tại sao* (thứ tự `min/max`, `unclamped by design`), không nhắc lại điều code đã nói |

---

## Task 1: `GetVolumePreservingScale` — lõi + guard

**Files:** Create `Assets/Horcrux/Runtime/Utilities/PhysXHelper/SquashStretch.cs`

**Interfaces**
- Consumes: `AxisType`, `CoordinateSystem`, `Is2D()` (`Utilities/Common`).
- Produces: `static Vector3 GetVolumePreservingScale(float primaryScale, AxisType primaryAxis, CoordinateSystem coordinateSystem)`; `private const float MinScale, MaxScale`.

**Toán → code**

| §0 | Dòng code |
|---|---|
| $c = s^{-1}$ · $c = s^{-1/2}$ (§0.3) | `1f / primaryScale` · `math.rsqrt(primaryScale)` |
| Bảng trục bù (§0.4) | `switch (coordinateSystem)` → `switch (primaryAxis)` |
| Thoái hóa (§0.4) | `default: return Vector3.one` |
| Kẹp hai đầu, NaN→sàn (§0.6) | `math.min(math.max(primaryScale, MinScale), MaxScale)` |

**Quyết định code** (lý do toán đã ở §0, đây chỉ là quyết định *cách viết*)

| Quyết định | Lý do |
|---|---|
| `static`, không field, trả struct `Vector3` | stateless → zero-GC, thread-safe; gán thẳng `localScale` |
| gán lại vào `primaryScale` sau khi kẹp | mọi nhánh dùng đúng giá trị đã guard — không thể lỡ đọc bản chưa kẹp |
| tính `compScale` **trước** `switch` | 1 phép chia/`rsqrt` dùng chung, thay vì lặp trong 9 nhánh |
| `switch(coordinateSystem)` lồng `switch(primaryAxis)` | 2 jump table trên enum liền kề, thay chuỗi `if (== XY) … if (== YZ) …` so sánh tuần tự |
| `Is2D()` chọn `n`, `switch` chọn trục | `n=1 hay 2` đọc được bằng tên hàm; chọn trục là bảng tra — mỗi việc một công cụ đúng |

- [ ] **Step 1: Tạo file**

```csharp
using System.Runtime.CompilerServices;
using Horcrux.Runtime.Implementations.Utilities.Common;
using Unity.Mathematics;
using UnityEngine;

namespace Horcrux.Runtime.Utilities.PhysXHelper
{
    /// <summary>
    /// Deform to preserve volume. Product of scale factors = 1.
    /// <remarks>Formula : c = s^(-1/n). s = Scale of the primary axis, n = Number of complement axes.</remarks>
    /// </summary>
    public static class SquashStretch
    {
        private const float MinScale = 1e-4f;
        private const float MaxScale = 1e4f;

        /// <summary>
        /// Calculate volume-preserving scale derived from the on main axis.
        /// </summary>
        /// <param name="primaryScale">Scale factor of the primary axis.</param>
        /// <param name="primaryAxis">Primary axis to scale</param>
        /// <param name="coordinateSystem">Coordinate system to calculate the complement.</param>
        /// <returns>Volume-preserving scale.</returns>
        public static Vector3 GetVolumePreservingScale(float primaryScale, 
            AxisType primaryAxis, CoordinateSystem coordinateSystem)
        {
            // min(max(..)) not math.clamp: clamp is max(lo, min(hi, x)) and math.min(hi, NaN) returns hi,
            // so NaN would land on the ceiling (10^4x blow-up). This order sends NaN to the floor
            // (invisible sliver) while the outer min still caps +∞ before it reaches localScale.
            primaryScale = math.min(math.max(primaryScale, MinScale), MaxScale);
            float compScale = coordinateSystem.Is2D() ? 1f / primaryScale
                : math.rsqrt(primaryScale);

            switch (coordinateSystem)
            {
                case CoordinateSystem.XY:
                    switch (primaryAxis)
                    {
                        case AxisType.X : return new Vector3(primaryScale, compScale, 1f);
                        case AxisType.Y : return new Vector3(compScale, primaryScale, 1f);
                        default: return Vector3.one;
                    }

                case CoordinateSystem.XZ:
                    switch (primaryAxis)
                    {
                        case AxisType.X : return new Vector3(primaryScale, 1f, compScale);
                        case AxisType.Z : return new Vector3(compScale, 1f, primaryScale);
                        default: return Vector3.one;
                    }

                case CoordinateSystem.YZ:
                    switch (primaryAxis)
                    {
                        case AxisType.Y : return new Vector3(1f, primaryScale, compScale);
                        case AxisType.Z : return new Vector3(1f, compScale, primaryScale);
                        default: return Vector3.one;
                    }

                default: // XYZ
                    switch (primaryAxis)
                    {
                        case AxisType.X : return new Vector3(primaryScale, compScale, compScale);
                        case AxisType.Y : return new Vector3(compScale, primaryScale, compScale);
                        default: return new Vector3(compScale, compScale, primaryScale);
                    }
            }
        }
    }
}
```

> Không cần `return` sau `switch`: mọi nhánh trong đều `return` và mọi `switch` đều có `default` → C# chứng minh được điểm cuối hàm không thể tới.

- [ ] **Step 2: Kiểm** — §0.8 hàng **L1–L6** (lõi) và **G1–G3** (guard). Unity biên dịch sạch.

- [ ] **Step 3: Commit**

```bash
git add Assets/Horcrux/Runtime/Utilities/PhysXHelper/SquashStretch.cs
git commit -m "feat(physx): SquashStretch - GetVolumePreservingScale (core, 2D plane/XYZ)"
```

---

## Task 2: `GetSquashFromImpact` + `GetDirectionalStretch`

**Files:** Modify `SquashStretch.cs` (thêm 2 hàm, không sửa hàm cũ)

**Interfaces**
- Consumes: `GetVolumePreservingScale` — Task 1.
- Produces: `GetSquashFromImpact(float impact, float maxImpact, float minScale, AxisType, CoordinateSystem)`; `GetDirectionalStretch(float speed, float stretchPerSpeed, float maxStretch, AxisType, CoordinateSystem)`.

**Toán → code:** hai mapping sinh `s` ở §0.5 rồi gọi lõi. Guard của chúng: bảng "Guard của từng mapping" §0.5.

**Quyết định code**

| Quyết định | Lý do |
|---|---|
| cả hai delegate về lõi | công thức bù ở **một** chỗ; guard biên (§0.6) thừa hưởng miễn phí |
| `[AggressiveInlining]` | thân mỏng (2–3 phép + 1 call) → bỏ phí gọi hàm, như `Interpolator.cs` |
| `math.saturate`, `math.clamp` (không `Mathf.*`) | cùng ngữ nghĩa, giữ nhất quán `math.*` trong file |
| gọi `math.lerp` thay khai triển tay | biểu diễn thẳng công thức §0.5; compiler vẫn sinh một `mad` |
| `1f` không phải literal `1` | tránh suy diễn overload `lerp(int/double,…)` |
| không tự xoay object | SRP — hàm chỉ cho *độ lớn*; caller xoay `primaryAxis` về hướng vận tốc |

- [ ] **Step 1: Thêm sau `GetVolumePreservingScale`**

```csharp
        /// <summary>
        /// Squash based on impact intensity.
        /// </summary>
        /// <remarks>Formula: s = lerp(1, minScale, saturate(impact/maxImpact)).</remarks>
        /// <param name="impact">Impact intensity.</param>
        /// <param name="maxImpact">Impact intensity threshold for <see cref="minScale"/>>.</param>
        /// <param name="minScale">Min scale factor of primary axis.</param>
        /// <returns>Volume-reserving scale based on impact intensity.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector3 GetSquashFromImpact(float impact, float maxImpact, float minScale,
            AxisType primaryAxis, CoordinateSystem coordinateSystem)
        {
            float normalizedImpact = maxImpact > 0f ? math.saturate(impact / maxImpact) : 0f;
            float s = math.lerp(1f, minScale, normalizedImpact);
            return GetVolumePreservingScale(s, primaryAxis, coordinateSystem);
        }
        
        /// <summary>
        /// Stretch based on speed.
        /// </summary>
        /// <remarks>Formula: s = 1 + clamp(speed*stretchPerSpeed, 0, maxStretch-1)</remarks>
        /// <param name="speed"></param>
        /// <param name="stretchPerSpeed"></param>
        /// <param name="maxStretch"></param>
        /// <returns>Volume-reserving scale based on speed.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector3 GetDirectionalStretch(float speed, float stretchPerSpeed, float maxStretch,
            AxisType primaryAxis, CoordinateSystem coordinateSystem)
        {
            float maxExtra = math.max(maxStretch - 1f, 0f);
            float extra = math.clamp(speed * stretchPerSpeed, 0f, maxExtra);
            float s = 1f + extra;
            return GetVolumePreservingScale(s, primaryAxis, coordinateSystem);
        }
```

- [ ] **Step 2: Kiểm** — §0.8 hàng **I1–I6** và **D1–D5**.

- [ ] **Step 3: Commit**

```bash
git add Assets/Horcrux/Runtime/Utilities/PhysXHelper/SquashStretch.cs
git commit -m "feat(physx): SquashStretch - GetSquashFromImpact + GetDirectionalStretch"
```

---

## Task 3: `GetSquashStretch` (time-driven) — compose `Easer`

**Files:** Modify `SquashStretch.cs` (thêm `using` + 1 hàm)

**Interfaces**
- Consumes: `GetVolumePreservingScale` — Task 1; `Easer.Evaluate(EaseType, float)` + `EaseType`.
- Produces: `GetSquashStretch(float t, EaseType easeType, float minScale, AxisType, CoordinateSystem)`.

**Toán → code:** mapping Time ở §0.5; biên độ vọt lố $s_{\text{peak}} = 1 + (e_{\max}-1)(1-m)$.

**Quyết định code**

| Quyết định | Lý do |
|---|---|
| `math.lerp` (**unclamped**) + comment tại chỗ | vọt lố `e>1` **là** pha stretch; `Mathf.Lerp` cắt mất (§0.5). Comment khóa ý định vì đây là chỗ dễ bị "sửa cho đúng" |
| không guard `t` | `Easer.Evaluate` đã kẹp `t∈[0,1]` nội bộ — kẹp lại là phép chết |
| không guard `eased` | với `minScale > 0.2717` không curve nào đẩy `s ≤ 0` (§0.5) → guard sẽ là nhánh chết. Dưới ngưỡng là **trách nhiệm caller** |

- [ ] **Step 1: Thêm `using Horcrux.Runtime.Tweening.Easing;`**

- [ ] **Step 2: Thêm vào cuối class**

```csharp
        /// <summary>
        /// Squash-Stretch-Idle based on time progress (must use unclamped ease).
        /// </summary>
        /// <param name="t">Time progress, clamped by Easer.</param>
        /// <param name="minScale">Min scale factor of the primary axis.</param>
        /// <returns>Volume-preserving based on easing.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector3 GetSquashStretch(float t, EaseType easeType, float minScale,
            AxisType primaryAxis, CoordinateSystem coordinateSystem)
        {
            float eased = Easer.Evaluate(easeType, t);
            float s = math.lerp(minScale, 1f, eased); // unclamped by design: overshoot IS the stretch
            return GetVolumePreservingScale(s, primaryAxis, coordinateSystem);
        }
```

- [ ] **Step 3: Kiểm** — §0.8 hàng **T1–T8**.

- [ ] **Step 4: Commit**

```bash
git add Assets/Horcrux/Runtime/Utilities/PhysXHelper/SquashStretch.cs
git commit -m "feat(physx): SquashStretch - GetSquashStretch (time-driven, compose Easer)"
```

---

# Verify công thức ↔ code

Đối chiếu **từng số hạng** hộp công thức §0 với dòng code — không khẳng định suông:

| §0 | Code | Kiểm |
|---|---|---|
| $c = s^{-1}$ | `1f / primaryScale` | số mũ `−1` ↔ nghịch đảo ✓ |
| $c = s^{-1/2}$ | `math.rsqrt(primaryScale)` | số mũ `−1/2` ↔ `1/√` ✓ |
| $s \cdot c^{2} = 1$ | `new Vector3(comp, primary, comp)` | đúng **2** thành phần `comp` ↔ `n=2` ✓ |
| $s \cdot c^{1} = 1$ | `new Vector3(comp, primary, 1f)` | đúng **1** `comp` + một `1f` ↔ `n=1` ✓ |
| `s = lerp(1, m, u)` | `math.lerp(1f, minScale, normalizedImpact)` | thứ tự `1 → m`: `u=0`→`1` ✓ |
| `s = lerp(m, 1, e)` | `math.lerp(minScale, 1f, eased)` | thứ tự **ngược lại** `m → 1`: `t=0`→`m` ✓ |
| `s = 1 + clamp(v·k, 0, M−1)` | `1f + math.clamp(speed*stretchPerSpeed, 0f, maxExtra)` | `maxExtra = max(M−1, 0)` ↔ biên trên ✓ |

**Round-trip** (cặp nghịch đảo `s ↔ c`): `n=1` — `1/(1/s)` về `s` trong `0.50 ulp`; `n=2` — `1/rsqrt(s)²` về `s` trong `2.00 ulp` (§0.7).

**Kiểm mốc chéo** — không cần bảng riêng: §0.8 là bảng mốc **duy nhất** và cột `T` gắn mỗi mốc với task kiểm nó, nên giá trị biên ở §0 và ở task không thể lệch nhau *về mặt cấu trúc*. (Trước đây tách 2 bảng thì phải đối chiếu tay.)

**Đạo hàm số** — không áp dụng: hệ không có hàm đạo hàm nào (phép kiểm đó dùng ở `DampedOscillator`, xem doc hệ đó).

---

# Ghi chú thực thi

| Chủ đề | Lưu ý |
|---|---|
| **Chọn `minScale`** | time-driven: giữ `> 0.28`; nhỏ hơn thì tránh họ `*Elastic` (§0.5) |
| **Chọn ease** | cần stretch → `OutBack`/`OutElastic`. Bounce và họ `In*` **không** cho `s>1` (§0.5) |
| **Chồng nhiều hiệu ứng** | **nhân các `s`** rồi gọi lõi **một lần**: `GetVolumePreservingScale(s_impact * s_speed, …)`. Cộng hai `Vector3` kết quả sẽ phá bảo toàn — hợp các phép scale là phép **nhân** |
| **Directional** | caller phải xoay `primaryAxis` về hướng vận tốc; hàm chỉ trả độ lớn (SRP) |
| **Không tham số mặc định** | mặc định `(Y, XYZ)` sẽ *âm thầm* scale trục Z của sprite 2D — buộc nêu rõ để chặn cả lớp bug đó |
| **So sánh thể tích** | `abs(x*y*z - 1f) < 1e-6f`, không `== 1f` (§0.7) |
| **Namespace lệch (nợ kỹ thuật)** | `Utilities/Common/*.cs` khai `…Implementations.Utilities.Common`, còn `SKILL.md` quy định `Horcrux.Runtime.Utilities.*`. Đổi thì sửa đồng thời `AxisType.cs`, `CoordinateSystem.cs` + `using` ở đây |
| **Kiểm chứng** | nhẩm/chạy tay theo §0.8 — **không** tạo file test; xóa script tạm trước khi commit |
| **Test tự động (sau này)** | NUnit EditMode theo §0.8; thêm property test `∏=1` với `s` ngẫu nhiên trong `[1e-4,1e4]` |
| **`.meta`** | Unity tự sinh — commit kèm để GUID ổn định |
| **Roadmap** | xong thì đánh ✅ `SquashStretch` trong `Pendings.md` (Tầng 2, mục 11) → mở khóa `GridSnapFeedback`, `Cascade`/`FallSettle` |

---

# Performance

| Metric | Giá trị |
|---|---|
| Heap allocation / call | **0 B** — `Vector3` struct trên stack, không closure/boxing |
| Lõi | 1 `max` + 1 `min` + `Is2D` (≤3 cmp) + **1** chia *hoặc* **1** `rsqrt` + 2 jump table + 1 ctor |
| Hàm siêu việt (`pow`/`exp`/`log`) | **0** — `n∈{1,2}` đặc biệt hóa hết (§0.3) |
| 3 helper mapping | ≤ 4 phép vô hướng + 1 call đã inline |
| `GetSquashStretch` thêm | 1 `Easer.Evaluate` (1 jump table + ≤6 phép, tùy ease) |
| Sai số bảo toàn | ≤ `0.50 ulp` (`n=1`), ≤ `2.00 ulp` (`n=2`) trên `[1e-4,1e4]` (§0.7) |
| Miền an toàn | mọi `float` kể cả `±∞`/`NaN` → luôn trả `Vector3` hữu hạn (§0.6) |
| Trạng thái | stateless, thread-safe, gọi được từ job (lưu ý Burst §0.7) |
