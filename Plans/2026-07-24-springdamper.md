# Plan — `SpringDamper`: lò xo–giảm chấn độc lập framerate lên Horcrux

Ngày 2026-11-06. Plan gốc Horcrux, không có bản tạm trong dự án. Một giá trị (`float`, `Vector2`, `Vector3`) đuổi theo đích có
đà, có nảy, độc lập framerate: chủ giữ state (vị trí, vận tốc), mỗi frame gọi `Solve` tiến state về đích bằng **nghiệm đóng**
của phương trình lò xo–giảm chấn (đúng với mọi `dt`), hoặc **semi-implicit Euler** khi cần rẻ. Hai núm: tần số `f` (Hz) và tỉ số
giảm chấn `ζ`.

**Phân công:** giai đoạn 1–4 (Horcrux, code runtime) developer gõ, sau mỗi giai đoạn agent viết test của giai đoạn đó theo bảng
case rồi compile · giai đoạn 5 (tài liệu) agent làm.

**Chạy được, thấy được ở giai đoạn 1–4** = `com.horcrux.runtime` compile sạch và bảng case của giai đoạn xanh trong
`Tests/EditMode/SpringDamperTests.cs`. Horcrux không có host và dự án chưa có call site, nên không có giai đoạn "dự án chuyển
sang"; mọi giai đoạn commit **trong `Assets/Horcrux`**. Không có bản dự án nên các giai đoạn không có mục "Khác với bản dự án".

Không phụ thuộc Plan nào. Có lõi toán: `§0`.

---

## Ngữ cảnh đã chốt

| Mục | Chốt |
|---|---|
| Người dùng | mọi hệ có một giá trị phải **đuổi theo đích đổi liên tục** mà vẫn có đà và nảy: camera bám, snap nam châm, kéo có trở lực, giật lùi. `Plans/FeelingPlan.md` liệt kê `CameraFollowSmooth`, `LookAhead`, `MagneticSnap`, `DragResistance`, `RubberBandPull`, `Recoil`. **Dự án hiện tại chưa có caller** (bảng "Đã khảo sát") |
| Mục tiêu | state `(Position, Velocity)` tiến về `target` một bước `dt` **đúng tuyệt đối** với mọi `dt` (1 bước `T` = `n` bước `T/n`); ba chế độ theo `ζ`: nảy, về nhanh nhất không nảy, bò về; Euler bán ẩn opt-in; `Vector2`/`Vector3` cùng config, từng trục độc lập |
| Núm | `frequency` (Hz, số lần lắc mỗi giây khi không cản) và `dampingRatio` `ζ` (không thứ nguyên) — hai đại lượng người tune nhìn thấy; `m, k, c` không phơi ra |
| Chỗ đặt | `Runtime/Utilities/PhysXHelper/Spring/` — `SpringConfig.cs`, `SpringSolver.cs`, `FloatSpring.cs`, `Vector2Spring.cs`, `Vector3Spring.cs`; namespace `Horcrux.Runtime.Utilities.PhysXHelper` (cùng `DampedOscillator`) |
| Tên | `SpringConfig`, `SpringMode`, `SpringSolver`, `SpringMethod`, `FloatSpringState`/`FloatSpring`, `Vector2SpringState`/`Vector2Spring`, `Vector3SpringState`/`Vector3Spring`. Developer đổi tuỳ ý trước khi gõ |
| Nhịp | mỗi frame, mỗi giá trị một `Solve`: một `exp` và một cặp `sin/cos` (hay `cosh/sinh`) mỗi trục; hệ số không phụ thuộc `dt` (`ω₀`, `ζω₀`, `ω_d`, chế độ) tính **một lần** lúc tạo config |
| Zero-GC | toàn `struct`; state truyền `ref`, config truyền `in`; trả tuple value; không `new` reference-type |
| Ranh giới | thuần toán: không `MonoBehaviour`, không UniTask, không PrimeTween; chủ giữ state và đích, quyết nhịp gọi (`Update`, `LateUpdate`, `FixedUpdate`); không gọi tên type dự án |
| Guard biên | `dt ≤ 0` → trả nguyên state · `frequency ≤ 0` → config tắt, `Solve` trả nguyên · `ζ < 0` → clamp 0 · `|ζ − 1| ≤ 1e-4` → chế độ tới hạn |

**Cố ý KHÔNG làm**

| Không làm | Vì |
|---|---|
| Đích nằm trong state, API `SetTarget` | đích là tham số mỗi lần `Solve`: đổi đích giữa chừng chỉ là truyền số khác, vận tốc giữ nguyên nên chuyển động không gãy |
| Spring là `MonoBehaviour`, tự chạy `Update` | chủ đã có vòng lặp và nhịp riêng; struct ghép vào mọi vòng lặp, pool, mảng |
| Field `Zeta` trong config | không nhánh nào đọc `ζ` thô: nghiệm đóng dùng `ζω₀`, Euler dùng `2ζω₀ = 2·ZetaOmega` |
| Giới hạn tốc độ theo **độ dài** vector | ghép trục, phi tuyến; lò xo tuyến tính tách trục được (§0.6) — vào khi có người dùng thật |
| Ngủ khi đã tới đích (`IsSettled`, ngưỡng) | chưa chủ nào cần tắt update; chủ tự so `|Position − target|` và `|Velocity|` |

**Quyết định trái trực giác** — lý do viết ở giai đoạn sinh ra nó: `default(SpringConfig)` là lò xo tắt (GĐ1) · trong dải tới hạn
`ζ` coi là 1, `ZetaOmega = Omega0` (GĐ2) · nhánh cản mạnh viết bằng `cosh/sinh`, không bằng hai mũ (GĐ2) · Euler cập nhật vận tốc
trước vị trí (GĐ3) · vector không có toán riêng, gọi lõi scalar từng trục (GĐ4).

**Mở rộng sau**

- `SpringConfig.FromPhysical(stiffness, damping, mass)` và `ToPhysical(mass)` khi có nguồn số vật lý thật (tool ngoài, tài liệu
  vật lý): công thức đảo ở §0.2, không cần sửa solver.
- Nhánh cản mạnh với `s·dt > 88`: `cosh` tràn `float` thành `+∞`, nhân với `E → 0` ra `NaN`. Cần `ζ` lớn **và** `dt` lớn cùng lúc
  (ví dụ `f = 10, ζ = 10, dt = 2`). Khi gặp: viết lại nhánh over bằng hai mũ âm `e^(−(ζω₀−s)dt)`, `e^(−(ζω₀+s)dt)`, hoặc chặn `dt`.
- Ghép trục phi tuyến (giới hạn tốc độ theo độ dài, lực kéo theo khoảng cách) — hệ riêng gọi `SpringSolver` sau khi tự tính.

---

## Đã khảo sát

| Nguồn | Lấy gì | Không lấy, vì sao |
|---|---|---|
| Horcrux `Runtime/Utilities/PhysXHelper/DampedOscillator.cs` | namespace, kiểu `static class` thuần toán, `math.*` của `Unity.Mathematics`, `AggressiveInlining` ở wrapper mỏng; cùng họ ODE `ÿ + 2λẏ + ω₀²y = 0` với `λ = ζω₀` | là hàm **theo thời gian tuyệt đối** `x(t) = A·e^(−λt)·cos(ωt + φ)` quanh cân bằng cố định, `f` và `λ` tách rời (mô hình mô tả, không ràng buộc vật lý), không state, không đích, không đổi đích, chỉ một dạng cản yếu. `SpringDamper` **thêm**: state `(Position, Velocity)` bước theo `dt`, đích làm tham số đổi được mỗi frame, ba chế độ theo `ζ`, Euler, vector per-axis. Trùng **một phần** (toán nền §0.1–0.3), không trùng API |
| Horcrux `HarmonicOscillator.cs`, `WaveStyle.cs` | mốc kiểm chéo: `ζ = 0` phải trùng `GetHarmonicDisplacement(Cos, f, t, A)` | dao động không tắt theo `t`, không bước thời gian |
| Horcrux `docs/Runtime/DampedOscillator/DampedOscillator.html` | §3–4 dẫn Newton → ODE → phương trình đặc trưng → ba chế độ, cùng toán với §0.1–0.3; §5 đã viết "cần ràng buộc vật lý thì dùng `SpringDamper`" — tài liệu đó chờ hệ này | ký hiệu `λ` và demo; `§0` dùng `ζ`, `ω₀` theo chuẩn điều khiển học vì núm là `ζ` |
| Horcrux `Interpolator.cs` › `ExpDecay`, `DecayFactor` | cùng nguyên lý "mũ cộng số mũ → độc lập framerate"; mốc so sánh: `ExpDecay` không overshoot | bậc một: không đà, không nảy |
| Horcrux `Tests/EditMode/JumpArcTests.cs` | khuôn test: `namespace Horcrux.Tests`, hằng tolerance, `TestCase` theo mốc `§0` | — |
| Horcrux `SquashStretch.cs`, `Interpolator.cs` phần còn lại | — | biến dạng bảo toàn thể tích, remap, smoothstep — không bước thời gian |
| category-jam `Entities/TileSlot/CacheTileSlot.cs` › `SlotSpringTuning`, `PlaySpring`; `Tile.cs` › `PlaySmallSpring` | — | chuỗi PrimeTween `InSine` xuống rồi `OutBack` lên; "spring" chỉ ở tên, không giải số. `Plans/Horcrux_HopCurve_DipFlash_Plan.md` đưa nó về `CharmSinkLocalY`. **Không có caller lò xo giải số nào trong dự án** |
| `Plans/FeelingPlan.md` | danh sách hệ sẽ dùng `SpringDamper` (mục "Người dùng") | — |

---

## Luồng mô tả — một giá trị đuổi theo đích

```
chủ tạo SpringConfig.FromFrequency(f, ζ) một lần
   ──► mỗi frame FloatSpring.Solve(ref state, target, cfg, dt) ──► đọc state.Position
   ──► đổi target giữa chừng: truyền số khác, đà giữ                              GĐ1 (ζ < 1: có nảy)
                       │
                       ▼
       vặn ζ tới 1 và hơn: về đích không nảy (tới hạn), bò về (cản mạnh)          GĐ2
                       │
                       ▼
       nhiều vật, dt nhỏ và đều: chọn SpringMethod.SemiImplicit rẻ hơn             GĐ3
                       │
                       ▼
       vị trí 2D, 3D: Vector2Spring / Vector3Spring, cùng config, từng trục         GĐ4
                       │
                       ▼
       tài liệu module docs/Runtime/SpringDamper, xoá Plan                          GĐ5
```

## Bản đồ hệ cuối

```
  chủ (vòng lặp của dự án: Update / LateUpdate / FixedUpdate)
   │ FromFrequency(f, ζ) một lần      │ Solve(ref state, target, cfg, dt[, method]) mỗi frame     │ đọc state.Position
   ▼                                  ▼                                                           │
  SpringConfig ◄────── in ────── FloatSpring · Vector2Spring · Vector3Spring ◄────────────────────┘
  Omega0, ZetaOmega, OmegaD,           │ từng trục
  Mode, IsActive                       ▼
                                 SpringSolver.Solve(pos, vel, target, cfg, method, dt)
                                   ├── SolveAnalytic      nghiệm đóng ba chế độ (§0.4)
                                   └── SolveSemiImplicit  Euler bán ẩn (§0.5)
```

Ai sở hữu gì: chủ giữ `*SpringState` (vị trí, vận tốc) và đích; config bất biến, một config dùng chung cho mọi state cùng cảm
giác; solver không giữ gì.

---

## §0 — Nền toán

### §0.1 Hiện tượng: vật gắn lò xo trong chất lỏng nhớt

Một giá trị `x` (vị trí camera, scale nút, giá trị UI) tự đuổi theo `target`: tăng tốc khi xa, có đà, giảm tốc khi tới nơi.
Đây là chuyển động của một vật khối lượng `m` nối với `target` bằng lò xo, nhúng trong chất lỏng nhớt.

| Thành phần | Vai trò trong cảm giác |
|---|---|
| lò xo | càng xa đích kéo càng mạnh → tạo đà, sinh overshoot khi cản yếu |
| chất lỏng nhớt | cản tỉ lệ vận tốc → triệt đà, quyết định nảy hay không |
| khối lượng | quán tính → chuyển động liên tục, không giật |

Dấu chấm là đạo hàm theo thời gian: `ẋ = dx/dt` vận tốc, `ẍ = d²x/dt²` gia tốc; với biến `y` cũng vậy.

| Lực | Biểu thức | Dấu `−` |
|---|---|---|
| đàn hồi (Hooke) | `F_s = −k·(x − target)` | lệch dương thì kéo âm: luôn hướng về đích; `k` (N/m) độ cứng |
| cản nhớt | `F_d = −c·ẋ` | luôn ngược chiều chuyển động; `c` (N·s/m) hệ số cản |

Định luật II Newton `ΣF = m·ẍ`:

$$m\ddot{x} = -k\,(x - target) - c\,\dot{x}$$

Gia tốc lúc này do vị trí và vận tốc lúc này quyết định; biết `(x, ẋ)` là biết cả chuyển động về sau — đó là việc của solver. Ẩn
là hàm `x(t)`, đạo hàm cao nhất là `ẍ`: phương trình vi phân bậc hai.

### §0.2 Chuẩn hoá: `(m, k, c)` → `(ω₀, ζ)`

Ba số vật lý dư một bậc tự do: hành vi chỉ phụ thuộc hai tỉ lệ `k/m`, `c/m`.

**① Chia cho `m`:**

$$\ddot{x} + \tfrac{c}{m}\dot{x} + \tfrac{k}{m}(x - target) = 0$$

**② Đổi biến `y = x − target`.** `target` là hằng trong một bước nên `ẏ = ẋ`, `ÿ = ẍ`; bài toán thành "đưa `y` về 0":

$$\ddot{y} + \tfrac{c}{m}\dot{y} + \tfrac{k}{m}\,y = 0$$

**③ Đặt tên hai tỉ lệ.**

| Đặt | Vì sao |
|---|---|
| `ω₀ ≡ √(k/m)`, tức `k/m = ω₀²` — **tần số tự nhiên** | bỏ cản (`c = 0`) còn `ÿ = −(k/m)·y`: gia tốc ngược dấu, tỉ lệ li độ; thử `y = cos(ωt)` được `ÿ = −ω²y`, khớp khi `ω² = k/m`. Vật lắc với tần số góc `√(k/m)` khi không ai cản |
| `ω₀ = 2π·f` | `ω₀` là rad/s; designer nghĩ theo `f` Hz (số lần lắc trọn mỗi giây); một vòng là `2π` rad — đổi đơn vị |
| `ζ ≡ c / (2√(km))`, tức `c/m = 2ζω₀` — **tỉ số giảm chấn** | `2√(km)` có cùng đơn vị với `c` (mức cản tới hạn) nên `ζ` không thứ nguyên; chọn mẫu này để `ζ = 1` rơi đúng ranh giới nảy/không nảy (§0.3) |

Kiểm `c/m = 2ζω₀`:

$$2\zeta\omega_0 = 2\cdot\frac{c}{2\sqrt{km}}\cdot\sqrt{\frac{k}{m}} = \frac{c}{\sqrt{km}}\cdot\sqrt{\frac{k}{m}} = \frac{c}{m}\;\checkmark$$

Thay vào ② → **dạng chuẩn**:

$$\boxed{\;\ddot{y} + 2\zeta\omega_0\,\dot{y} + \omega_0^2\,y = 0\;}$$

API phơi `frequency` và `dampingRatio`: `f` là "nhanh, cứng", `ζ` là "nảy nhiều hay ít". Đảo ngược khi có số vật lý (Mở rộng
sau): `ω₀ = √(k/m)`, `ζ = c/(2√(km))`, `f = ω₀/2π`; chiều ngược `k = ω₀²m`, `c = 2ζω₀m`.

### §0.3 Phương trình đặc trưng → ba chế độ

Cần hàm mà đạo hàm ra chính nó nhân hằng, để ba số hạng `ÿ, ẏ, y` triệt nhau: `y = e^{rt}`, `ẏ = r·e^{rt}`, `ÿ = r²·e^{rt}`. Thay
vào dạng chuẩn, `e^{rt} ≠ 0` nên chia đi:

$$r^2 + 2\zeta\omega_0\,r + \omega_0^2 = 0$$

Giải bậc hai (`a = 1`, `b = 2ζω₀`, `c = ω₀²`), rút `4ω₀²` khỏi căn:

$$r = \frac{-2\zeta\omega_0 \pm \sqrt{4\omega_0^2(\zeta^2 - 1)}}{2} = -\zeta\omega_0 \pm \omega_0\sqrt{\zeta^2 - 1}$$

Dấu của `ζ² − 1` chia ba chế độ:

| Chế độ | `ζ` | `ζ² − 1` | Nghiệm `r` | Đại lượng phụ | Chuyển động |
|---|---|---|---|---|---|
| cản yếu (under) | `< 1` | âm → căn ảo | phức `−ζω₀ ± i·ω_d` | `ω_d = ω₀√(1 − ζ²)` | vượt đích rồi nảy lui, biên độ tắt dần |
| tới hạn (critical) | `= 1` | 0 | kép `−ω₀` | — | về đích nhanh nhất mà không nảy |
| cản mạnh (over) | `> 1` | dương → căn thực | hai thực âm `−ζω₀ ± s` | `s = ω₀√(ζ² − 1)` | bò về đích, không nảy |

`ζ = 1` là ranh giới vì `r` phức sinh `sin/cos` (dao động), `r` thực chỉ còn mũ (không dao động). `ω_d < ω₀`: cản làm lắc chậm
lại. Phần thực của `r` luôn là `−ζω₀`: mọi chế độ chung một **bao hình** `E = e^{−ζω₀t}`.

### §0.4 Nghiệm đóng từng chế độ

Cần hàm `(y₀, v₀) → (y, v)` sau `Δt`, chính xác, không xấp xỉ. Mỗi chế độ bốn bước: ① nghiệm tổng quát với hai hằng `A, B` ·
② ghim `A, B` bằng `y(0) = y₀`, `ẏ(0) = v₀` · ③ đạo hàm ra `v` · ④ kiểm `Δt = 0` ra `(y₀, v₀)`, `Δt → ∞` ra `(0, 0)`. Ký hiệu
`E = e^{−ζω₀Δt}`.

#### Cản yếu `ζ < 1`

**①** `√(ζ² − 1) = i√(1 − ζ²)`, đặt `ω_d = ω₀√(1 − ζ²)`; nghiệm tổng quát `C₁e^{r₁t} + C₂e^{r₂t}`, tách phần thực khỏi mũ:

$$y = e^{-\zeta\omega_0 t}\big(C_1 e^{i\omega_d t} + C_2 e^{-i\omega_d t}\big)$$

Công thức Euler $e^{i\theta} = \cos\theta + i\sin\theta$ với `C₁, C₂` liên hợp để `y` thực:

$$y(t) = E\,\big[A\cos(\omega_d t) + B\sin(\omega_d t)\big]$$

**②** `t = 0`: `E = 1, cos 0 = 1, sin 0 = 0` → `A = y₀`. Từ ③ tại `t = 0`: `v₀ = −ζω₀A + ω_d B` → `B = (v₀ + ζω₀y₀)/ω_d`.

**③** `y = E·f`, `f = A·cos + B·sin`; quy tắc tích với `E' = −ζω₀E`, `f' = −Aω_d·sin + Bω_d·cos`:

$$v = E\,(f' - \zeta\omega_0 f) = E\,\big[(B\omega_d - \zeta\omega_0 y_0)\cos(\omega_d t) - (y_0\omega_d + \zeta\omega_0 B)\sin(\omega_d t)\big]$$

Kết quả với `C = cos(ω_dΔt)`, `S = sin(ω_dΔt)`, `B = (v₀ + ζω₀y₀)/ω_d`:

$$\boxed{\;y = E\,(y_0 C + B S)\;}\qquad \boxed{\;v = E\,\big[(-\zeta\omega_0 y_0 + B\omega_d)\,C - (\zeta\omega_0 B + y_0\omega_d)\,S\big]\;}$$

**④** `Δt = 0`: `y = y₀`; `v = −ζω₀y₀ + Bω_d = −ζω₀y₀ + v₀ + ζω₀y₀ = v₀` ✓ · `Δt → ∞`: `E → 0` → `(0, 0)` ✓ · `ζ = 0`: `E = 1`,
`ω_d = ω₀`, `y = y₀cos(ω₀t) + (v₀/ω₀)sin(ω₀t)` — dao động điều hoà, trùng `HarmonicOscillator` ✓.

#### Cản mạnh `ζ > 1`

**①** căn thực, đặt `s = ω₀√(ζ² − 1)`; `r = −ζω₀ ± s`. Thay cơ sở `e^{±st}` bằng `cosh(st) = (e^{st} + e^{−st})/2`,
`sinh(st) = (e^{st} − e^{−st})/2` — cùng không gian nghiệm, cùng khuôn với cản yếu qua phép thế `cos → cosh`, `sin → sinh`,
`ω_d → s`:

$$y(t) = E\,\big[y_0\cosh(st) + B\sinh(st)\big],\qquad B = \frac{v_0 + \zeta\omega_0 y_0}{s}$$

Dạng tổng hai mũ `C₁e^{r₁t} + C₂e^{r₂t}` trừ nhau hai số chênh lệch lớn khi `ζ` lớn, mất chữ số; dạng hyperbolic gộp bao hình ra
ngoài, ổn định hơn với `float`.

**②③** `cosh' = s·sinh`, `sinh' = s·cosh`, cùng quy tắc tích; với `Ch = cosh(sΔt)`, `Sh = sinh(sΔt)`:

$$\boxed{\;y = E\,(y_0\,Ch + B\,Sh)\;}\qquad \boxed{\;v = E\,\big[(-\zeta\omega_0 y_0 + B s)\,Ch + (y_0 s - \zeta\omega_0 B)\,Sh\big]\;}$$

**④** `Δt = 0`: `Ch = 1, Sh = 0` → `y = y₀`, `v = −ζω₀y₀ + Bs = v₀` ✓. Không nổ dù `cosh/sinh` tăng theo `e^{sΔt}`: `ζ > √(ζ² − 1)`
(bình phương: `ζ² > ζ² − 1`) nên `ζω₀ > s`, bao hình co nhanh hơn hyperbolic giãn, tích về 0 ✓. Giới hạn số học: `float` tràn khi
`sΔt > 88` (Mở rộng sau).

#### Tới hạn `ζ = 1`

**①** `r = −ω₀` kép; nghiệm thứ hai phải nhân thêm `t` để đủ hai hằng:

$$y(t) = e^{-\omega_0 t}\,(A + Bt)$$

**②③** `A = y₀`. Đạo hàm với `E' = −ω₀E`:

$$\dot{y} = e^{-\omega_0 t}\big(B - \omega_0(A + Bt)\big)$$

`t = 0`: `v₀ = B − ω₀y₀` → `B = v₀ + ω₀y₀`. Đặt `coeff = v₀ + ω₀y₀`, rút gọn `v = E(v₀ + ω₀y₀ − ω₀y₀ − ω₀·coeff·Δt)`:

$$\boxed{\;y = E\,(y_0 + coeff\cdot\Delta t)\;}\qquad \boxed{\;v = E\,(v_0 - \omega_0\,coeff\cdot\Delta t)\;}\qquad E = e^{-\omega_0\Delta t}$$

**④** `Δt = 0`: `E = 1` → `(y₀, v₀)` ✓.

**Vì sao nghiệm đóng ổn định vô điều kiện:** cả ba là (hàm bị chặn hoặc tăng chậm hơn bao) × `E` giảm khi `ζ > 0`; không có phép
lặp tích luỹ sai số. `Δt` lớn (lag spike 2 giây) vẫn ra đúng điểm trên đường cong. Hàm mũ cộng số mũ: `E(a)·E(b) = E(a + b)` nên
chia `T` thành bao nhiêu bước cũng về cùng một chỗ — **độc lập framerate**, cùng nguyên lý `Interpolator.ExpDecay`.

### §0.5 Semi-implicit Euler

Nghiệm đóng tốn `exp` và `sin/cos` mỗi bước. Khi `dt` nhỏ, đều và vật nhiều, mô phỏng từng bước theo định nghĩa đạo hàm rẻ hơn.
Gia tốc rút từ dạng chuẩn:

$$a = \ddot{y} = -\omega_0^2\,y - 2\zeta\omega_0\,v$$
$$v_{new} = v + a\,\Delta t,\qquad y_{new} = y + v_{new}\,\Delta t$$

| Kiểu | Thứ tự | Ổn định |
|---|---|---|
| explicit Euler | `y += v·Δt` rồi `v += a·Δt` (vị trí dùng `v` cũ) | bơm năng lượng mỗi bước, dễ nổ |
| **semi-implicit** (dùng) | `v += a·Δt` rồi `y += v_new·Δt` (vị trí dùng `v` mới) | gần bảo toàn năng lượng cho hệ dao động |

| Đặc điểm | Nội dung |
|---|---|
| ngưỡng nổ | phân kỳ khi `ω₀·Δt` lớn (bước quá thô so chu kỳ); an toàn khi `ω₀·Δt` cỡ vài phần mười |
| tránh khi | `dt` dao động mạnh, `ω₀` cao → nghiệm đóng |
| dùng khi | `dt` cố định và nhỏ, nhiều vật, cần tiết kiệm `exp/sincos` |

### §0.6 Vector: từng trục độc lập

Mỗi trục có phương trình riêng `ẍ + 2ζω₀ẋ + ω₀²x = 0`, `ÿ + …`, `z̈ + …`; không số hạng nào trộn hai trục (`x·y`, `ẋ·z`): hệ
tuyến tính tách biến, chung `ω₀, ζ`. Code: gọi lõi scalar cho từng trục rồi ghép; không toán vector. Hiệu ứng phi tuyến theo độ dài
vector ghép trục — Mở rộng sau.

### §0.7 Mốc kiểm

| Mốc | Kỳ vọng |
|---|---|
| `Δt = 0`, mọi chế độ | `(y, v) = (y₀, v₀)` |
| `Δt → ∞`, `ζ > 0` | `(0, 0)`: `x → target` |
| `ζ = 0`, `v₀ = 0` | `y = y₀cos(ω₀t)` — trùng `HarmonicOscillator.GetHarmonicDisplacement(Cos, f, t, y₀)`, biên độ không co |
| `ζ < 1`, `v₀ = −ζω₀y₀` (`B = 0`) | `y = y₀·E·cos(ω_d t)` — trùng `DampedOscillator.GetDisplacement(Cos, ω_d/2π, ζω₀, t, y₀)`; `v` trùng `GetVelocity` |
| `ζ = 1`, `y₀ > 0`, `v₀ = 0` | `y` không đổi dấu, giảm đơn điệu |
| `ζ > 1` so `ζ = 1` cùng `(y₀, v₀)` | về chậm hơn, không đổi dấu |
| `ζ = 1 − 2·10⁻⁴` (under) so `ζ = 1` (critical) | liên tục qua ranh giới: lệch `< 10⁻³` |
| độc lập framerate (nghiệm đóng) | 1 bước `T` = 100 bước `T/100`, lệch `< 10⁻³` |
| Euler | 1 bước `T` ≠ 100 bước `T/100`; sai số giảm khi `dt` giảm |
| đạo hàm số | `v(t) ≈ (y(t+h) − y(t−h)) / 2h`, `h = 10⁻³` |
| vector | từng trục bằng đúng kết quả scalar cùng tham số |

---

## Giai đoạn 1 — Chủ tạo config, giá trị float đuổi theo đích có nảy

**Mục tiêu:** chủ gọi `SpringConfig.FromFrequency(f, ζ)` một lần, mỗi frame `FloatSpring.Solve(ref state, target, cfg, dt)`, đọc
`state.Position`; đổi `target` giữa chừng thì truyền số khác. Chế độ cản yếu `ζ < 1`, nghiệm đóng. Xong giai đoạn này một giá trị
đuổi theo đích, có đà, nảy rồi dừng, đúng với mọi `dt`.

**Vì sao bây giờ:** đây là toàn bộ luồng dùng hệ của một chủ; cản yếu là cảm giác lò xo mặc định và là nhánh có đủ ba hệ số
(`ζω₀`, `ω_d`, `E`) để mọi nhánh sau chỉ là thay `cos/sin`.

### `Runtime/Utilities/PhysXHelper/Spring/FloatSpring.cs` — file mới (call site)

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

### `Runtime/Utilities/PhysXHelper/Spring/SpringSolver.cs` — file mới

```csharp
using Unity.Mathematics;

namespace Horcrux.Runtime.Utilities.PhysXHelper
{
    /// <summary>Lõi giải một chiều: (pos, vel) sau dt giây khi đuổi theo target. Vector gọi vào đây từng trục.</summary>
    public static class SpringSolver
    {
        /// <summary>Giải ÿ + 2ζω₀ẏ + ω₀²y = 0 với y = pos − target, rồi đổi về pos.</summary>
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

### `Runtime/Utilities/PhysXHelper/Spring/SpringConfig.cs` — file mới

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
            if (frequency <= 0f) return default;                                    // IsActive false

            float omega0 = 2f * Mathf.PI * frequency;                               // ω₀ = 2πf
            float zeta = Mathf.Clamp(dampingRatio, 0f, 1f - CriticalEpsilon);       // tạm: GĐ2 mở ζ ≥ 1
            float omegaD = omega0 * Mathf.Sqrt(1f - zeta * zeta);                   // ω_d
            return new SpringConfig(zeta * omega0, omegaD, true);
        }
    }
}
```

**Toán → code:** `e` = `E` · `b` = `B` · dòng `y` = hộp `y` cản yếu §0.4 · dòng `v` = hộp `v` cản yếu §0.4 · `pos − target` và
`ny + target` = đổi biến §0.2 ②.

**Quyết định** — `target` là tham số, không nằm trong state: đổi đích không cần API, vận tốc không reset. Phương án bỏ: state giữ
`Target` và `SetTarget` — thêm một cửa ghi, và mọi chủ có đích đổi mỗi frame (camera bám) phải gọi hai hàm. — `default(SpringConfig)`
là lò xo tắt: field config chưa gán không ra `NaN`, giá trị chỉ đứng yên; `FromFrequency(≤ 0)` trả `default` nên hai đường "tắt"
là một. — Chặn `ζ` ở `1 − CriticalEpsilon` là code tạm: GĐ2 thay bằng ba nhánh, chặn này chỉ giữ GĐ1 không chia cho `ω_d ≈ 0`.
— `(float, float)` tuple thay `out`: value trên stack, một dòng gán ở call site.

| Consumes | Produces |
|---|---|
| `Unity.Mathematics` (`math.exp/sin/cos`, đã có trong `com.horcrux.runtime`), `UnityEngine.Mathf` lúc tạo config | `SpringConfig.FromFrequency`, `ZetaOmega`, `OmegaD`, `IsActive` · `SpringSolver.Solve`, `SolveAnalytic` · `FloatSpringState`, `FloatSpring.Solve` |

**Sơ đồ tương tác sau GĐ1** (`+` là phần mới)

```
  chủ ──FromFrequency+──► SpringConfig+ ──in──► FloatSpring.Solve+(ref state+, target, cfg, dt) ──► SpringSolver.Solve+ ──► SolveAnalytic+ (cản yếu)
                                                     ▲ đọc Position
```

**Editor setup:** không — thuần toán.

**Kiểm:** compile sạch. Test trong `Tests/EditMode/SpringDamperTests.cs`, `namespace Horcrux.Tests`; hằng `f = 2`, `ζ = 0.3` trừ khi
ghi khác; `ω₀ = 12.566`.

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

**Mốc commit:** `feat(spring): SpringConfig, SpringSolver cản yếu, FloatSpring`.

---

## Giai đoạn 2 — Vặn `ζ` tới 1 và hơn: không nảy, bò về

**Mục tiêu:** `FromFrequency` nhận mọi `ζ ≥ 0`, chọn `SpringMode`; `SolveAnalytic` giải đủ ba chế độ. Xong giai đoạn này designer
vặn `ζ = 1` được chuyển động về đích nhanh nhất không vượt, `ζ > 1` được chuyển động ì.

**Vì sao bây giờ:** `ζ` là núm thứ hai của chủ và `ζ ≥ 1` là nửa dải của nó; camera bám và UI không muốn vượt đích dùng đúng `ζ = 1`.

### `SpringConfig.cs` › thêm trước `struct SpringConfig`

```csharp
    /// <summary>Theo ζ: dưới 1 nảy, bằng 1 về nhanh nhất không nảy, trên 1 bò về.</summary>
    public enum SpringMode { UnderDamped, CriticallyDamped, OverDamped }
```

### `SpringConfig.cs` › field — thêm `Omega0` trước `ZetaOmega`, thay doc `OmegaD`, thêm `Mode` sau `OmegaD`

```csharp
        /// <summary>ω₀ = 2πf — tần số góc tự nhiên.</summary>
        public readonly float Omega0;

        /// <summary>ζω₀ — tốc độ co của bao hình e^(−ζω₀·dt).</summary>
        public readonly float ZetaOmega;

        /// <summary>Under: ω_d = ω₀√(1−ζ²) · Over: s = ω₀√(ζ²−1) · Critical: 0.</summary>
        public readonly float OmegaD;

        public readonly SpringMode Mode;
```

### `SpringConfig.cs` › `CriticalEpsilon`, constructor, `FromFrequency` — đoạn cũ → đoạn mới

Cũ:

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

Mới:

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

Cũ:

```csharp
            float omega0 = 2f * Mathf.PI * frequency;                               // ω₀ = 2πf
            float zeta = Mathf.Clamp(dampingRatio, 0f, 1f - CriticalEpsilon);       // tạm: GĐ2 mở ζ ≥ 1
            float omegaD = omega0 * Mathf.Sqrt(1f - zeta * zeta);                   // ω_d
            return new SpringConfig(zeta * omega0, omegaD, true);
```

Mới — ba nhánh theo dấu `ζ² − 1` (§0.3):

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

### `SpringSolver.cs` › `SolveAnalytic` — đoạn cũ → đoạn mới

Cũ: toàn bộ thân `SolveAnalytic` của GĐ1 (một nhánh cản yếu). Mới — nhánh cản yếu giữ nguyên từng dòng, thành `case` đầu; `E`
tính một lần cho cả ba nhánh:

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

**Toán → code:** nhánh over = hai hộp cản mạnh §0.4 với `ch, sh` · nhánh critical = hai hộp tới hạn §0.4 · `Mode` chọn lúc tạo
config theo bảng §0.3 nên `Solve` không so `ζ` mỗi frame.

**Quyết định** — dải tới hạn `ZetaOmega = Omega0`: một `E` cho ba nhánh mà nhánh tới hạn vẫn đúng công thức `e^{−ω₀Δt}`; sai số
so với `ζ` thật trong dải là `ω₀·10⁻⁴·dt`, dưới ngưỡng `float`. Phương án bỏ: giữ `ζ` thật và `E` lệch công thức tới hạn một chút —
hai công thức cho một nhánh. — `cosh/sinh` thay hai mũ: cùng khuôn với nhánh cản yếu, không trừ hai số lớn (§0.4). Giá là tràn
`float` khi `s·dt > 88` (Mở rộng sau). — `switch` trên enum, `default` là tới hạn: không nhánh nào rơi ra ngoài.

| Consumes | Produces |
|---|---|
| GĐ1 | `SpringMode` · `SpringConfig.Omega0`, `Mode` · `SolveAnalytic` ba chế độ |

**Sơ đồ tương tác sau GĐ2**

```
  chủ ──FromFrequency(f, ζ bất kỳ ≥ 0)+──► SpringConfig (Mode+, Omega0+) ──► FloatSpring.Solve ──► SpringSolver.Solve ──► SolveAnalytic
                                                                                                          ├── UnderDamped
                                                                                                          ├── OverDamped+
                                                                                                          └── CriticallyDamped+
```

**Editor setup:** không.

**Kiểm:** compile sạch; test GĐ1 vẫn xanh (nhánh cản yếu không đổi).

| Case | Input | Kỳ vọng |
|---|---|---|
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

**Mốc commit:** `feat(spring): ba chế độ theo ζ`.

---

## Giai đoạn 3 — Nhiều vật, `dt` nhỏ và đều: Euler bán ẩn opt-in

**Mục tiêu:** `SpringMethod { Analytic, SemiImplicit }`; `SpringSolver.Solve` nhận `method`, `FloatSpring.Solve` mặc định
`Analytic`. Xong giai đoạn này chủ có nhiều vật và `dt` cố định đổi được sang bước rẻ không `exp/sincos`.

**Vì sao bây giờ:** chọn phương pháp là quyết định của chủ sau khi đã có cảm giác đúng ở GĐ1–2; đặt mặc định `Analytic` trước
khi có wrapper vector để bốn wrapper cùng một chữ ký.

### `SpringSolver.cs` › thêm trước `class SpringSolver`

```csharp
    /// <summary>Analytic: nghiệm đóng, đúng mọi dt. SemiImplicit: Euler bán ẩn, rẻ hơn, chỉ khi dt nhỏ và đều.</summary>
    public enum SpringMethod { Analytic, SemiImplicit }
```

### `SpringSolver.cs` › `Solve` — đoạn cũ → đoạn mới

Cũ:

```csharp
        public static (float pos, float vel) Solve(float pos, float vel, float target, in SpringConfig cfg, float dt)
        {
            if (!cfg.IsActive || dt <= 0f) return (pos, vel);

            float y = pos - target;
            (float ny, float nv) = SolveAnalytic(y, vel, cfg, dt);
            return (ny + target, nv);
        }
```

Mới:

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

### `SpringSolver.cs` › thêm sau `SolveAnalytic`

```csharp
        /// <summary>Euler bán ẩn: vận tốc trước, vị trí dùng vận tốc mới. Phân kỳ khi ω₀·dt lớn.</summary>
        internal static (float, float) SolveSemiImplicit(float y, float v, in SpringConfig cfg, float dt)
        {
            float a = -cfg.Omega0 * cfg.Omega0 * y - 2f * cfg.ZetaOmega * v;   // a = −ω₀²y − 2ζω₀v
            v += a * dt;
            y += v * dt;
            return (y, v);
        }
```

### `FloatSpring.cs` › `Solve` — đoạn cũ → đoạn mới

Cũ:

```csharp
        public static void Solve(ref FloatSpringState s, float target, in SpringConfig cfg, float dt)
        {
            (s.Position, s.Velocity) = SpringSolver.Solve(s.Position, s.Velocity, target, cfg, dt);
        }
```

Mới:

```csharp
        public static void Solve(ref FloatSpringState s, float target, in SpringConfig cfg, float dt,
            SpringMethod method = SpringMethod.Analytic)
        {
            (s.Position, s.Velocity) = SpringSolver.Solve(s.Position, s.Velocity, target, cfg, method, dt);
        }
```

**Toán → code:** dòng `a` = §0.5 gia tốc với `2ζω₀ = 2·ZetaOmega` · hai dòng `+=` = thứ tự semi-implicit §0.5.

**Quyết định** — mặc định `Analytic`: chất lượng trước, rẻ là opt-in có ý thức; call site không đổi khi chưa cần. — Euler đọc
`ZetaOmega`, không cần `ζ` thô: config không có field `Zeta`. — `method` là tham số, không nằm trong config: một config dùng cho
cả vật cần đúng và vật cần rẻ. Phương án bỏ: `Mode`-kiểu field `Method` trong config — hai config cho một cảm giác.

| Consumes | Produces |
|---|---|
| GĐ2 | `SpringMethod` · `SpringSolver.Solve(…, method, dt)`, `SolveSemiImplicit` · `FloatSpring.Solve(…, method = Analytic)` |

**Sơ đồ tương tác sau GĐ3**

```
  chủ ──Solve(ref state, target, cfg, dt[, method+])──► FloatSpring ──► SpringSolver.Solve(…, method+, dt)
                                                                              ├── Analytic     → SolveAnalytic
                                                                              └── SemiImplicit → SolveSemiImplicit+
```

**Editor setup:** không.

**Kiểm:** compile sạch; test GĐ1–2 không sửa (mặc định `Analytic`).

| Case | Input | Kỳ vọng |
|---|---|---|
| mặc định | `FloatSpring.Solve(ref s, 10, cfg, dt)` không truyền `method` | trùng bit với `Analytic` tường minh |
| Euler hội tụ | `SemiImplicit`, `(0, 0)` → 10, `f = 2`, `ζ = 0.3`, 300 bước `dt = 1/60` (`ω₀·dt` = 0.21) | ≈ 10 (±0.05), `Velocity` ≈ 0 |
| Euler là xấp xỉ | Euler so Analytic tại `t = 1`, `dt = 1/60` | lệch > 0 và < 0.2 |
| sai số giảm theo `dt` | Euler `dt = 1/600` so `dt = 1/60`, cùng `t = 1`, so Analytic | lệch của `1/600` nhỏ hơn lệch của `1/60` |
| Euler phụ thuộc framerate | 1 bước `dt = 1` so 100 bước `dt = 0.01` | lệch > 1 |
| Euler nổ | `dt = 0.5` (`ω₀·dt` = 6.3), 20 bước từ `(0, 0)` → 10 | `|Position − 10|` tăng qua các bước, không hội tụ |
| guard | `SemiImplicit` với `dt = 0` · config tắt | state nguyên |
| Euler ba chế độ | `ζ ∈ {0.3, 1, 2}`, `dt = 1/60`, 300 bước | đều ≈ 10 (±0.05) |

**Mốc commit:** `feat(spring): SpringMethod, Euler bán ẩn opt-in`.

---

## Giai đoạn 4 — Vị trí 2D, 3D: cùng config, từng trục độc lập

**Mục tiêu:** `Vector2Spring.Solve(ref Vector2SpringState, Vector2 target, in cfg, dt, method)` và bản `Vector3`; mỗi trục gọi
`SpringSolver.Solve`. Xong giai đoạn này camera, vị trí vật, scale ba trục đuổi theo đích bằng một lời gọi.

**Vì sao bây giờ:** đa số người dùng ở bảng "Ngữ cảnh đã chốt" là vị trí 2D/3D; lõi scalar đã đủ ba chế độ và hai phương pháp
nên wrapper viết một lần, không sửa lại.

### `Runtime/Utilities/PhysXHelper/Spring/Vector3Spring.cs` — file mới

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

### `Runtime/Utilities/PhysXHelper/Spring/Vector2Spring.cs` — file mới

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

**Toán → code:** §0.6 — mỗi dòng `SpringSolver.Solve` là một phương trình trục.

**Quyết định** — gọi lõi scalar từng trục, không viết `SolveAnalytic` cho `Vector3`: sửa lõi một chỗ, ba kiểu hưởng; `Vector3`
là struct nên `new` nằm trên stack. Phương án bỏ: generic `Spring<T>` với `IVectorOps` — một interface cho ba kiểu đã biết trước.
— Không `AggressiveInlining`: thân ba lời gọi, không còn là wrapper mỏng.

| Consumes | Produces |
|---|---|
| GĐ3 | `Vector2SpringState`, `Vector2Spring.Solve` · `Vector3SpringState`, `Vector3Spring.Solve` |

**Sơ đồ tương tác sau GĐ4**

```
  chủ ──Solve(ref Vector3SpringState+, Vector3 target, cfg, dt[, method])──► Vector3Spring+ ──x──► SpringSolver.Solve
                                                                                             ──y──►
                                                                                             ──z──►
  (Vector2Spring+ hai trục, cùng hình)
```

**Editor setup:** không.

**Kiểm:** compile sạch.

| Case | Input | Kỳ vọng |
|---|---|---|
| trục = scalar | `Vector3` state `(1, −2, 3)`, vel `(0.5, 0, −1)`, target `(5, −3, 10)`, một bước `dt = 0.1` | mỗi trục **bằng đúng** (`==`) `SpringSolver.Solve` scalar cùng tham số trục đó |
| hội tụ 3 trục | `(0, 0, 0)` → `(5, −3, 10)`, 300 bước `dt = 1/60` | `Position` ≈ target (±1e-3 mỗi trục), `Velocity` ≈ 0 |
| trục đã ở đích | `Position.x = target.x`, `Velocity.x = 0`, hai trục kia chưa | trục `x` không đổi qua mọi bước |
| `Vector2` | `(0, 0)` → `(5, −3)` | hội tụ; trục = scalar |
| `dt = 0` · config tắt | bất kỳ | state nguyên |
| Euler qua wrapper | `method = SemiImplicit`, `dt = 1/60` | hội tụ ≈ target (±0.05) |
| `velocity` mặc định | `new Vector3SpringState(p)` | `Velocity` = `Vector3.zero` |

**Mốc commit:** `feat(spring): Vector2Spring, Vector3Spring per-axis`.

---

## Giai đoạn 5 — Tài liệu `docs/Runtime/SpringDamper/SpringDamper.md` và `SpringDamper.html` (agent)

`.md` mở đầu `Code: Runtime/Utilities/PhysXHelper/Spring`. Mục: luồng ASCII (luồng mô tả ở trên) · bảng chọn hệ — `SpringDamper`
(đích đổi được, có đà, bước theo `dt`) · `DampedOscillator` (dao động quanh cân bằng cố định theo `t`) · `Interpolator.ExpDecay`
(bậc một, không nảy) — với tiêu chí và kết luận · `§0` chuyển nguyên sang (đây là chỗ sống của toán sau khi Plan xoá) · bảng API
năm file · "Design decisions" gom các **Quyết định** GĐ1–4 · bẫy: Euler với `ω₀·dt` lớn, `cosh` tràn khi `s·dt > 88`, `default` là
tắt, dải tới hạn · Testing: trỏ `Tests/EditMode/SpringDamperTests.cs`. Không có "Trước khi chạy": thuần code, không wire.

`.html` chọn từ `.md`: kịch bản (giá trị đuổi theo đích, đổi đích giữa chừng, ba chế độ theo `ζ`) · cách dùng (tạo config một lần,
`Solve` mỗi frame, khi nào `SemiImplicit`) · flow · lưu ý. Demo nếu có: slider `f`, `ζ` vẽ `Position` theo thời gian từ `§0.4`,
số đọc từ `.md`.

`docs/Runtime/DampedOscillator/DampedOscillator.html` §5 đã nhắc `SpringDamper`: đổi thành link tới `SpringDamper.html`.

Xoá `Plans/2026-07-24-springdamper.md` cùng commit: hệ có tài liệu module thì Plan hết vòng đời.

**Mốc commit:** `docs(spring): SpringDamper.md, .html; xoá Plan`.

---

## File → giai đoạn

| File | GĐ1 | GĐ2 | GĐ3 | GĐ4 | GĐ5 |
|---|---|---|---|---|---|
| `Runtime/Utilities/PhysXHelper/Spring/FloatSpring.cs` | tạo | | `method` mặc định | | |
| `Runtime/Utilities/PhysXHelper/Spring/SpringSolver.cs` | tạo, cản yếu | ba chế độ | `SpringMethod`, `SolveSemiImplicit` | | |
| `Runtime/Utilities/PhysXHelper/Spring/SpringConfig.cs` | tạo | `SpringMode`, `Omega0`, `Mode` | | | |
| `Runtime/Utilities/PhysXHelper/Spring/Vector2Spring.cs` | | | | tạo | |
| `Runtime/Utilities/PhysXHelper/Spring/Vector3Spring.cs` | | | | tạo | |
| `Tests/EditMode/SpringDamperTests.cs` (agent) | tạo | thêm case | thêm case | thêm case | |
| `docs/Runtime/SpringDamper/SpringDamper.md`, `.html` (agent) | | | | | tạo |
| `docs/Runtime/DampedOscillator/DampedOscillator.html` (agent) | | | | | link §5 |
| `Plans/2026-07-24-springdamper.md` | | | | | xoá |
