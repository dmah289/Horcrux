# SpringDamper Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.
>
> **Plan này là artifact học tập + triển khai, tự chứa.** Người dùng tự code lại để rèn tư duy → §0 dẫn giải **đầy đủ toán học** (biến đổi + lý do dùng công thức), các task chứa **code hoàn chỉnh** kèm chú thích self-doc & lý do tối ưu. Không code test (theo lựa chọn ở spec §4) → mỗi task có **checklist kiểm chứng** (kiểm mốc toán học).

**Goal:** `SpringDamper` — bộ giải lò xo-giảm chấn framerate-independent (analytic + semi-implicit Euler) cho float/Vector2/Vector3, dạng struct state + static solver zero-GC.

**Architecture:** 5 file trong `Spring/`. Lõi toán scalar viết **một lần** trong `SpringSolver`; `FloatSpring` bọc state 1 chiều; `Vector2/3Spring` gọi lõi scalar **per-axis** (không lặp toán). Tham số hóa `frequency + dampingRatio` + converter physical; precompute hệ số không-phụ-thuộc-dt trong `SpringConfig`.

**Tech Stack:** C# (Unity), `Unity.Mathematics` (`math.exp/sin/cos/cosh/sinh`), `UnityEngine.Mathf` cho setup. Thuần toán — không Addressables/UniTask.

## Global Constraints

| Ràng buộc | Giá trị |
|---|---|
| Namespace | `Horcrux.Runtime.Utilities.PhysXHelper` (mọi file) |
| Zero-GC hot path | toàn `struct`; `Solve` nhận `ref`/`in`; không `new` reference-type, LINQ, closure, string |
| SOLID | 1 file = 1 trách nhiệm; toán viết 1 lần (SpringSolver); mở rộng qua `enum`, không sửa struct |
| Self-doc | tên nói rõ mục đích; XML doc kèm công thức + "tại sao" ở API public |
| Precompute | hệ số nặng phần không phụ thuộc dt (`ω₀, ζω₀, ω_d, mode`) tính 1 lần trong `SpringConfig` |
| ODE chuẩn | `ÿ + 2ζω₀·ẏ + ω₀²·y = 0`, `y = x − target`, `ω₀ = 2π·f` |
| Guard biên | `dt ≤ 0` → no-op; `f ≤ 0` → tắt; `ζ < 0` → clamp 0; `|ζ−1| ≤ 1e-4` → critical |
| Spec nguồn | `docs/superpowers/specs/2026-07-24-springdamper-design.md` |

---

## §0. Nền toán học (đọc trước khi code)

> Mục tiêu: hiểu **tại sao** từng công thức, không chỉ chép. Đọc xong bạn có thể tự dựng lại solver từ đầu.

### 0.1. Bản chất vật lý — vì sao lò xo là mô hình đúng cho "bám mượt"

Ta muốn một giá trị `x` (vị trí camera, scale nút bấm, giá trị UI…) **tự đuổi theo** `target` một cách sống động: có gia tốc, có đà, giảm tốc mượt khi tới nơi. Đúng là hành vi của một **vật gắn lò xo**.

> **Ký hiệu chấm (dot notation) — đọc trước cho quen.** Dấu chấm trên đầu = đạo hàm theo **thời gian** `t`:
> | Viết | Đọc là | Ý nghĩa vật lý | Đơn vị (nếu `x` là mét) |
> |---|---|---|---|
> | `x` | vị trí | đang ở đâu | m |
> | `ẋ` (x-chấm) | `dx/dt` = **vận tốc** | đổi vị trí nhanh chậm | m/s |
> | `ẍ` (x-hai-chấm) | `d²x/dt²` = **gia tốc** | đổi vận tốc nhanh chậm | m/s² |
>
> Về sau dùng biến `y` thì `ẏ, ÿ` hiểu y hệt. Đây chỉ là cách viết gọn của đạo hàm, không có gì mới.

Hình dung vật khối lượng `m` nối với `target` bằng lò xo, nhúng trong chất lỏng nhớt:

| Thành phần | Vai trò trong game feel |
|---|---|
| **Lò xo** | càng xa target kéo càng mạnh → tạo lực đưa `x` về, sinh **đà** (overshoot nếu yếu cản) |
| **Chất lỏng nhớt (damping)** | cản lại tỉ lệ tốc độ → **triệt đà**, quyết định nảy hay không nảy |
| **Khối lượng** | quán tính → chuyển động liên tục, không giật cục |

Hai lực tác dụng lên vật, theo đúng vật lý phổ thông:

| Lực | Biểu thức | Bản chất & vì sao có dấu `−` |
|---|---|---|
| **Lò xo (định luật Hooke)** | `F_s = −k·(x − target)` | Lực đàn hồi tỉ lệ **độ biến dạng** `(x−target)`. `x` ở **trên** target (độ lệch dương) → lò xo kéo **xuống** (lực âm) → luôn hướng **về** target. `k` (N/m) = độ cứng. |
| **Cản nhớt (viscous damping)** | `F_d = −c·ẋ` | Lực cản của chất lỏng tỉ lệ **vận tốc** `ẋ`, luôn **ngược** hướng chuyển động → dấu `−`. `c` (N·s/m) = hệ số giảm chấn. |

Định luật II Newton nói **tổng lực = khối lượng × gia tốc** (`ΣF = m·ẍ`). Cộng hai lực trên lại rồi thay vào:

$$m\ddot{x} = \underbrace{-k(x - target)}_{\text{lò xo kéo về}} \;\underbrace{-\,c\dot{x}}_{\text{cản hãm đà}}$$

**Đọc phương trình này:** vế trái là gia tốc (×`m`), vế phải là hai lực. Nó nói *"tại mỗi thời điểm, gia tốc bị quyết định bởi vị trí hiện tại `x` và vận tốc hiện tại `ẋ`"*. Biết `x, ẋ` lúc này → tính được `ẍ` → biết sẽ tăng tốc thế nào ở khoảnh khắc kế → suy ra cả chuyển động. Đó chính là thứ solver làm.

> **Vì sao gọi là phương trình vi phân bậc 2:** ẩn cần tìm là cả một *hàm* `x(t)` (chứ không phải một con số), và ràng buộc lại đặt lên **đạo hàm** của nó (`ẍ, ẋ`). "Bậc 2" vì đạo hàm cấp cao nhất xuất hiện là `ẍ` (cấp 2). Giải nó = tìm công thức `x(t)` thỏa mãn ràng buộc — đó là việc của §0.3–0.4.

### 0.2. Chuẩn hóa: 3 tham số vật lý → 2 tham số trực quan

**Vấn đề với `m, k, c`:** ba số vật lý thô, khó tune và **dư thừa** — hành vi lò xo thực chất chỉ phụ thuộc *hai* tỉ lệ, không phải ba số riêng lẻ. Ta rút gọn.

**Bước 1 — chia cho `m`** (đưa hệ số của `ẍ` về 1):
$$\ddot{x} + \tfrac{c}{m}\dot{x} + \tfrac{k}{m}(x - target) = 0$$

**Bước 2 — đổi biến `y = x − target`** (chuyển gốc tọa độ về target). Vì `target` là **hằng số** nên đạo hàm không đổi: `ẏ = ẋ`, `ÿ = ẍ`. Bài toán "đuổi theo target" thành "đưa `y` về 0":
$$\ddot{y} + \tfrac{c}{m}\dot{y} + \tfrac{k}{m}\,y = 0$$

**Bước 3 — đặt tên cho 2 tỉ lệ** `k/m` và `c/m`. Thay vì dùng số thô, ta đặt tên cho chúng sao cho **mỗi tên nói lên một ý nghĩa vật lý**. Có hai định nghĩa, dẫn giải từng cái:

**① `ω₀` — tần số tự nhiên**, định nghĩa `ω₀ ≡ √(k/m)`, tức `k/m = ω₀²`.

*Vì sao chính là tần số dao động?* Tưởng tượng **bỏ cản** (`c=0`) — lò xo lý tưởng không ma sát. Phương trình còn:
$$\ddot{y} + \tfrac{k}{m}\,y = 0 \quad\Longleftrightarrow\quad \ddot{y} = -\tfrac{k}{m}\,y$$
Đọc lên: *"gia tốc luôn ngược dấu và tỉ lệ với li độ"*. Chỉ có `sin/cos` thỏa tính chất này — thử $y = \cos(\omega t)$ thì $\ddot{y} = -\omega^2\cos(\omega t) = -\omega^2 y$, khớp khi $\omega^2 = k/m$. Vậy vật dao động qua lại với **tần số góc** $\omega = \sqrt{k/m}$. Đó là nhịp lắc "bẩm sinh" khi không ai cản → gọi là *tần số tự nhiên* `ω₀`.

*Vì sao `ω₀ = 2π·f`?* `ω₀` là tần số **góc** (radian/giây); designer lại nghĩ theo tần số **thường** `f` (số lần lắc trọn vẹn mỗi giây, đơn vị Hz). Một vòng lắc trọn = `2π` radian, nên `ω₀ = 2π·f`. Đây chỉ là đổi đơn vị, giống "vòng/phút → radian/giây".

**② `ζ` — tỉ số giảm chấn (damping ratio)**, định nghĩa `ζ ≡ c / (2√(km))`, tức `c/m = 2ζω₀`.

*Vì sao mẫu là `2√(km)`?* Ta muốn một con số **không thứ nguyên** (bỏ được đơn vị, chỉ còn "kiểu" chuyển động). `c` có đơn vị N·s/m; đại lượng `2√(km)` cũng có đúng đơn vị đó (gọi là *cản tới hạn* — mức cản vừa đủ để hết nảy), nên tỉ số `ζ` triệt đơn vị → thuần số. Chọn đúng mẫu này để mốc `ζ=1` rơi trúng ranh giới nảy/không-nảy (chứng minh ở §0.3). *Kiểm nhanh `c/m = 2ζω₀`:*
$$2\zeta\omega_0 = 2\cdot\frac{c}{2\sqrt{km}}\cdot\sqrt{\frac{k}{m}} = \frac{c}{\sqrt{km}}\cdot\sqrt{\frac{k}{m}} = \frac{c}{m} \;\checkmark$$

Thay `k/m = ω₀²` và `c/m = 2ζω₀` vào phương trình cuối Bước 2 → **dạng chuẩn** (mọi tài liệu điều khiển học đều dùng):

$$\boxed{\;\ddot{y} + 2\zeta\omega_0\,\dot{y} + \omega_0^2\,y = 0\;}$$

> **Lý do chọn API `frequency + dampingRatio`:** designer chỉ cần vặn 2 núm có nghĩa — `f` = "nhanh/chậm & cứng", `ζ` = "nảy nhiều/ít". Không cần biết `m,k,c`. Ai có sẵn số vật lý thì dùng converter (`FromPhysical`, Task 1) đảo ngược 3 bước trên.

### 0.3. Giải ODE → phương trình đặc trưng → vì sao có đúng 3 chế độ

**Ý tưởng giải:** ta cần hàm mà đạo hàm của nó lại ra chính nó (để các số hạng `ÿ, ẏ, y` triệt tiêu nhau). Hàm mũ `e^{rt}` là ứng viên **duy nhất** có tính chất đó. Thử `y = e^{rt}`:
$$\dot{y} = r\,e^{rt},\qquad \ddot{y} = r^2 e^{rt}$$

Thay vào dạng chuẩn, đặt `e^{rt}` làm nhân tử chung (nó `≠ 0` nên chia được):
$$e^{rt}\big(r^2 + 2\zeta\omega_0 r + \omega_0^2\big) = 0 \;\Rightarrow\; r^2 + 2\zeta\omega_0 r + \omega_0^2 = 0$$

Đây là **phương trình đặc trưng** — một pt bậc 2 theo `r`. Giải bằng công thức nghiệm (`ax²+bx+c` với `a=1, b=2ζω₀, c=ω₀²`):
$$r = \frac{-2\zeta\omega_0 \pm \sqrt{(2\zeta\omega_0)^2 - 4\omega_0^2}}{2}$$

**Rút gọn phần dưới căn** (bước hay bị bỏ qua) — rút `4ω₀²` ra ngoài rồi lấy căn:
$$\sqrt{4\zeta^2\omega_0^2 - 4\omega_0^2} = \sqrt{4\omega_0^2(\zeta^2-1)} = 2\omega_0\sqrt{\zeta^2-1}$$

Thay lại, `2` trên tử triệt với `2` dưới mẫu:
$$r = -\zeta\omega_0 \pm \omega_0\sqrt{\zeta^2 - 1}$$

**Chìa khóa nằm ở `√(ζ² − 1)`** — biểu thức dưới căn đổi dấu quanh `ζ=1`, chia ra **3 trường hợp** khác nhau về bản chất nghiệm:

| Chế độ | ĐK | `ζ²−1` | Nghiệm `r` | Đại lượng phụ | Hành vi vật lý |
|---|---|---|---|---|---|
| **Under-damped** | `ζ<1` | âm → căn ảo | phức `−ζω₀ ± iω_d` | `ω_d = ω₀√(1−ζ²)` | cản yếu → vật **vọt qua** target rồi nảy lui, biên độ tắt dần |
| **Critically** | `ζ=1` | 0 → căn triệt | kép `−ω₀` | — | cản vừa đủ → về đích **nhanh nhất mà KHÔNG nảy** |
| **Over-damped** | `ζ>1` | dương → căn thực | 2 thực âm | `s = ω₀√(ζ²−1)` | cản quá mạnh → vật **bò** về đích, ì, chậm |

> **Vì sao `ζ=1` là ranh giới nảy/không nảy:** `ζ<1` cho `r` **phức** → phần ảo `iω_d` sinh ra `sin/cos` → **dao động** (nảy). `ζ≥1` cho `r` **thực** → chỉ còn hàm mũ/hyperbolic → **không dao động**. Đúng chỗ căn đổi từ ảo sang thực. `ω_d` = "tần số dao động thực tế", luôn **nhỏ hơn** `ω₀` vì bị cản làm chậm.

### 0.4. Nghiệm đóng từng chế độ (lõi solver Analytic)

**Mục tiêu code:** viết hàm `(y₀, v₀) → (y, v) sau Δt`, tức bước tiến lò xo đúng một khoảng thời gian, **chính xác tuyệt đối** (không xấp xỉ).

**Quy trình chung cho cả 3 chế độ** (làm quen 1 lần, áp cho cả 3):
1. **Nghiệm tổng quát** — từ dạng nghiệm `r`, viết `y(t)` với 2 hằng số tự do `A, B` (pt bậc 2 → 2 hằng số).
2. **Ghim `A, B`** bằng 2 điều kiện đầu: `y(0) = y₀` và `ẏ(0) = v₀`.
3. **Đạo hàm** `y(t)` ra `v(t) = ẏ(t)`.
4. **Kiểm mốc** `Δt=0` (phải ra `(y₀,v₀)`) và `Δt→∞` (phải ra `(0,0)`).

Ký hiệu dùng chung: `E = e^{−ζω₀Δt}`.

> **"Bao hình" (envelope) là gì:** nghiệm luôn có dạng `E(Δt) × [dao động/đa thức]`. Phần trong ngoặc lượn lên xuống (hoặc tăng), còn `E = e^{−ζω₀Δt}` là hàm mũ **giảm dần** bọc lấy nó, ép biên độ co về 0. Vẽ ra: `E` và `−E` là hai đường cong ôm trên–dưới, dao động nằm gọn giữa chúng → gọi là "đường bao". `ζω₀` càng lớn → bao co càng nhanh → tắt càng sớm. **Mọi chế độ đều share chung `E` này** (vì phần thực của `r` luôn là `−ζω₀`), nên ta tính `E` một lần rồi nhân vào.

#### ● Under-damped (`ζ<1`) — trường hợp phổ biến nhất trong game

**① Nghiệm tổng quát.** Với `ζ<1`, `ζ²−1 < 0` nên `√(ζ²−1) = √(−(1−ζ²)) = i√(1−ζ²)`. **Đặt tên** phần thực bên trong là `ω_d ≡ ω₀√(1−ζ²)` (tần số dao động thực). Hai nghiệm:
$$r = -\zeta\omega_0 \pm i\,\omega_d$$

Nghiệm tổng quát là `y = C₁e^{r₁t} + C₂e^{r₂t}`. Tách phần thực ra khỏi mũ (`e^{a+b}=e^a e^b`):
$$y = e^{-\zeta\omega_0 t}\big(C_1 e^{i\omega_d t} + C_2 e^{-i\omega_d t}\big)$$

**Vì sao mũ ảo → `cos/sin`:** công thức Euler $e^{i\theta}=\cos\theta + i\sin\theta$. Tổ hợp hai mũ ảo liên hợp (với `C₁,C₂` chọn sao cho `y` thực) rút gọn thành $A\cos(\omega_d t)+B\sin(\omega_d t)$. Đó là lý do cản yếu sinh **dao động** — phần ảo biến thành hàm lượng giác tuần hoàn:
$$y(t) = \underbrace{e^{-\zeta\omega_0 t}}_{E:\text{ bao hình}}\big[A\cos(\omega_d t) + B\sin(\omega_d t)\big]$$

**② Ghim `A, B`** — thay `t=0`:

| Điều kiện | Phép tính | Kết quả |
|---|---|---|
| `y(0) = y₀` | `E=1, cos0=1, sin0=0` → `y(0)=A` | `A = y₀` |
| `ẏ(0) = v₀` | đạo hàm rồi cho `t=0` (xem ③) → `v₀ = −ζω₀A + ω_d B` | `B = (v₀ + ζω₀y₀)/ω_d` |

**③ Đạo hàm ra `v = ẏ`.** Viết `y = E·f` với $E = e^{-\zeta\omega_0 t}$, $f = A\cos(\omega_d t)+B\sin(\omega_d t)$. Quy tắc tích $(E f)' = E'f + Ef'$:
- $E' = -\zeta\omega_0\,E$ (đạo hàm hàm mũ)
- $f' = -A\omega_d\sin(\omega_d t) + B\omega_d\cos(\omega_d t)$

Gộp `E` ra chung:
$$v = E\big(f' - \zeta\omega_0 f\big) = E\big[(-A\omega_d\sin + B\omega_d\cos) - \zeta\omega_0(A\cos + B\sin)\big]$$

Nhóm lại theo `cos` và `sin`, rồi thay `A = y₀`:
$$v = E\big[\underbrace{(B\omega_d - \zeta\omega_0 y_0)}_{\text{hệ số }\cos}\cos(ω_d t) - \underbrace{(y_0\omega_d + \zeta\omega_0 B)}_{\text{hệ số }\sin}\sin(ω_d t)\big]$$

**Kết quả** (đặt `C = cos(ω_dΔt)`, `S = sin(ω_dΔt)`, `B = (v₀+ζω₀y₀)/ω_d`):
$$\boxed{\;y = E\,(y_0 C + B S)\;}$$
$$\boxed{\;v = E\big[(-\zeta\omega_0 y_0 + B\omega_d)\,C - (\zeta\omega_0 B + y_0\omega_d)\,S\big]\;}$$

**④ Kiểm mốc:** `Δt=0` → `E=C=1, S=0` → `y=y₀`; `v = −ζω₀y₀ + Bω_d = −ζω₀y₀ + (v₀+ζω₀y₀) = v₀` ✓ · `Δt→∞` → `E→0` → `(0,0)` tức `x→target` ✓

#### ● Over-damped (`ζ>1`) — cản mạnh, không nảy

**① Nghiệm tổng quát.** Với `ζ>1`, `ζ²−1 > 0` → căn **thực**. **Đặt tên** `s ≡ ω₀√(ζ²−1)` (đối xứng với `ω_d` bên under, nhưng thực). Hai nghiệm thực:
$$r = -\zeta\omega_0 \pm s$$

*Về nguyên tắc* nghiệm là tổng 2 mũ `C₁e^{r₁t}+C₂e^{r₂t}`. Nhưng ta viết lại bằng **hàm hyperbolic**. Lý do đổi được: theo định nghĩa $\cosh(st)=\tfrac{e^{st}+e^{-st}}{2}$, $\sinh(st)=\tfrac{e^{st}-e^{-st}}{2}$, nên $e^{\pm st}$ là tổ hợp tuyến tính của `cosh/sinh` — cùng một không gian nghiệm, chỉ đổi cơ sở. Tách bao hình chung `e^{−ζω₀t}` (từ phần `−ζω₀` của `r`) ra ngoài, phần `±s` gói vào `cosh/sinh(st)`. Kết quả **giống hệt khuôn under-damped** với phép thế **`cos→cosh, sin→sinh, ω_d→s`**:
$$y(t) = e^{-\zeta\omega_0 t}\big[y_0\cosh(st) + B\sinh(st)\big],\qquad B = \tfrac{v_0+\zeta\omega_0 y_0}{s}$$

> **Vì sao dùng cosh/sinh thay vì `C₁e^{r₁t}+C₂e^{r₂t}`:** dạng tổng-2-mũ bị **triệt tiêu số học** (catastrophic cancellation) khi `ζ` lớn — hai mũ chênh lệch cực lớn, trừ nhau mất chữ số có nghĩa. Dạng hyperbolic gộp bao hình ra ngoài → ổn định float hơn hẳn. Cùng lý do `DecayFactor` cần cẩn thận với `1−e` ở Interpolator.

**② Đạo hàm ra `v`** (`cosh'=s·sinh`, `sinh'=s·cosh`, cùng quy tắc tích như under):
$$\boxed{\;y = E\,(y_0\,Ch + B\,Sh)\;}$$
$$\boxed{\;v = E\big[(-\zeta\omega_0 y_0 + B s)\,Ch + (y_0 s - \zeta\omega_0 B)\,Sh\big]\;}$$
với `Ch = cosh(sΔt)`, `Sh = sinh(sΔt)`.

**③ Kiểm mốc** `Δt=0`: `Ch=1, Sh=0` → `y=y₀`; `v = −ζω₀y₀ + Bs = v₀` ✓

> **Vì sao không nổ dù `cosh/sinh` tăng theo `e^{sΔt}`:** luôn có `ζω₀ > s` vì `ζ > √(ζ²−1)` (bình phương 2 vế: `ζ² > ζ²−1` luôn đúng). Nên bao hình `E=e^{−ζω₀Δt}` co nhanh hơn `cosh/sinh` giãn → **tích vẫn phân rã về 0**. ✓

#### ● Critically damped (`ζ=1`) — về đích nhanh nhất, không nảy

**① Nghiệm tổng quát.** `r=−ω₀` là nghiệm **kép**. Toán ODE: khi nghiệm đặc trưng trùng, nghiệm thứ hai phải nhân thêm `t` (nếu không sẽ thiếu 1 hằng số tự do). Nên dạng là `(A + Bt)e^{−ω₀t}`:
$$y(t) = e^{-\omega_0 t}\,(A + B t)$$

**② Ghim `A, B`:**

| Điều kiện | Kết quả |
|---|---|
| `y(0) = y₀` | `A = y₀` |
| `ẏ(0) = v₀` (xem đạo hàm dưới) → `v₀ = B − ω₀A` | `B = v₀ + ω₀y₀` |

Đạo hàm `y = e^{−ω₀t}(A+Bt)` bằng quy tắc tích, `E' = −ω₀E`:
$$\dot{y} = -\omega_0 e^{-\omega_0 t}(A+Bt) + e^{-\omega_0 t}\cdot B = e^{-\omega_0 t}\big(B - \omega_0(A+Bt)\big)$$
Cho `t=0`: `ẏ(0) = B − ω₀A = v₀`.

**③ Kết quả** (đặt `coeff = B = v₀ + ω₀y₀`):
$$\boxed{\;y = E\,(y_0 + coeff\cdot\Delta t)\;}\qquad \boxed{\;v = E\,(v_0 - \omega_0\,coeff\cdot\Delta t)\;}$$
với `E = e^{−ω₀Δt}`. Rút gọn `v`: `ẏ = E(B − ω₀(A+BΔt)) = E(v₀+ω₀y₀ − ω₀y₀ − ω₀·coeff·Δt) = E(v₀ − ω₀·coeff·Δt)`.

**④ Kiểm mốc** `Δt=0`: `E=1` → `y=y₀, v=v₀` ✓

> **Vì sao Analytic ổn định vô điều kiện (không bao giờ nổ):** cả 3 công thức đều là (đa thức/lượng giác bị chặn) × bao hình `E` **giảm** khi `ζ>0`. Không có phép **lặp** tích lũy sai số như Euler — mỗi bước tính thẳng nghiệm đúng tại `Δt`. Dù `Δt` khổng lồ (lag spike 2 giây), kết quả vẫn là điểm đúng trên đường cong. Cùng bản chất `Interpolator.ExpDecay` §Bước 4: hàm mũ **cộng số mũ** → chia thời gian kiểu gì cũng ra cùng một chỗ → **độc lập framerate tuyệt đối**.

### 0.5. Semi-implicit Euler (đối chiếu, rẻ)

Analytic đẹp nhưng tốn `exp + sin/cos` mỗi bước. Khi cần **rẻ** và `dt` nhỏ-ổn định, có cách xấp xỉ: thay vì giải chính xác, ta **mô phỏng từng bước nhỏ** theo đúng định nghĩa đạo hàm.

**Ý tưởng rời rạc hóa:** đạo hàm ≈ "đổi bao nhiêu trong `Δt`". Từ ODE tính gia tốc hiện tại, rồi tiến vận tốc & vị trí:
$$a = \ddot{y} = -\omega_0^2\,y - 2\zeta\omega_0\,v \quad(\text{rút từ dạng chuẩn})$$
$$v_{new} = v + a\,\Delta t \qquad y_{new} = y + \mathbf{v_{new}}\,\Delta t$$

**Điểm tinh tế — thứ tự cập nhật:**

| Kiểu | Công thức vị trí | Ổn định |
|---|---|---|
| Explicit Euler (SAI) | `y += v·Δt` rồi mới `v += a·Δt` (dùng `v` **cũ**) | dễ nổ, bơm năng lượng |
| **Semi-implicit** (dùng) | `v += a·Δt` trước, rồi `y += v_new·Δt` (dùng `v` **mới**) | ổn định hơn hẳn |

> **Vì sao "velocity-first" ổn hơn:** dùng vận tốc **đã cập nhật** để dời vị trí = thêm một chút "nhìn trước" (tính ẩn). Nó bảo toàn năng lượng gần đúng cho hệ dao động, thay vì bơm năng lượng lên mỗi bước như explicit. Đây là chuẩn cho physics game (còn gọi symplectic Euler).

| Đặc điểm | Nội dung |
|---|---|
| Ngưỡng nổ | phân kỳ khi `ω₀·Δt` lớn (bước quá thô so chu kỳ dao động). An toàn khi `ω₀·Δt` ≲ vài phần mười |
| Khi nào tránh | `dt` dao động mạnh (lag spike) hoặc `ω₀` cao → dùng Analytic |
| Khi nào dùng | `dt` cố định & nhỏ, cần tiết kiệm `exp/sincos`, hệ nhiều vật |

### 0.6. Mở rộng vector — vì sao chỉ cần chạy lõi scalar per-axis

Với Vector3, mỗi trục `x, y, z` có phương trình lò xo **riêng**:
$$\ddot{x} + 2\zeta\omega_0\dot{x} + \omega_0^2 x = 0,\quad \ddot{y} + \dots,\quad \ddot{z} + \dots$$

Nhìn kỹ: **không có số hạng nào trộn 2 trục** (không có `x·y`, không `ẋ·z`…). Đây là hệ **tuyến tính, tách biến** (decoupled) — mỗi trục tiến hóa độc lập, chỉ dùng chung `ω₀, ζ` (cùng `SpringConfig`).

> **Hệ quả code:** không cần toán vector mới. Gọi **đúng lõi scalar** `SpringSolver.Solve` cho từng trục rồi ghép lại. DRY tuyệt đối: sửa lõi → mọi kiểu (float/V2/V3) hưởng lợi. (Lưu ý: điều này đúng vì lò xo *tuyến tính*; các hiệu ứng phi tuyến như giới hạn tốc độ theo *độ dài vector* sẽ ghép trục — ngoài phạm vi v1.)

---

## Bản đồ triển khai

```
Spring/
├── SpringConfig.cs   Task 1  tham số + precompute + converter        (0.2)
├── SpringSolver.cs   Task 2  lõi toán scalar analytic + euler        (0.4, 0.5)
├── FloatSpring.cs    Task 3  state 1 chiều + API Solve
├── Vector2Spring.cs  Task 4  per-axis                                 (0.6)
└── Vector3Spring.cs  Task 4  per-axis                                 (0.6)
```
Thứ tự: **1 → 2 → 3/4** (2 cần 1; 3 và 4 đều cần 2, độc lập nhau).

---

### Task 1: `SpringConfig` — tham số + precompute + converter

**Files:**
- Create: `Assets/Horcrux/Runtime/Utilities/PhysXHelper/Spring/SpringConfig.cs`

**Interfaces:**
- Consumes: —
- Produces:
  - `enum SpringMode { UnderDamped, CriticallyDamped, OverDamped }`
  - `readonly struct SpringConfig` — field `float Omega0, Zeta, ZetaOmega, OmegaD; SpringMode Mode; bool IsActive`
  - `static SpringConfig FromFrequency(float frequency, float dampingRatio)`
  - `static SpringConfig FromPhysical(float stiffness, float damping, float mass = 1f)`
  - `(float stiffness, float damping) ToPhysical(float mass = 1f)`

**Bản đồ toán → code:** §0.2 (chuẩn hóa `k/m=ω₀²`, `c/m=2ζω₀`) · §0.3 (chọn mode + `ω_d`/`s`).

**Self-doc & tối ưu:**

| Quyết định | Lý do |
|---|---|
| `readonly struct` | immutable, zero-GC, không đổi sau khi tạo |
| precompute `ZetaOmega, OmegaD, Mode` trong factory | `Sqrt`/nhân tính **1 lần**, không lặp mỗi frame trong Solve |
| `IsActive` cờ sẵn | Solve chỉ check `bool`, không so `frequency` lại |
| `Mode` enum sẵn | Solve `switch` thẳng, không so `zeta` mỗi frame |
| `CriticalEpsilon` | tránh chia 0 ở `ω_d`/`s` khi `ζ≈1` |

- [ ] **Step 1: Tạo file với đầy đủ code**

```csharp
using System.Runtime.CompilerServices;
using UnityEngine;

namespace Horcrux.Runtime.Utilities.PhysXHelper
{
    public enum SpringMode { UnderDamped, CriticallyDamped, OverDamped }

    /// <summary>
    /// Tham số lò xo dạng chuẩn (frequency + dampingRatio) + hệ số precompute không phụ thuộc dt.
    /// Immutable: tạo qua factory, hệ số nặng (Sqrt) tính 1 lần.
    /// </summary>
    /// <remarks>
    /// ODE chuẩn: ÿ + 2ζω₀·ẏ + ω₀²·y = 0, với y = x − target.
    /// ω₀ = 2π·f (tần số tự nhiên), ζ = damping ratio (không thứ nguyên).
    /// </remarks>
    public readonly struct SpringConfig
    {
        public readonly float Omega0;     // ω₀ = 2π·f
        public readonly float Zeta;       // ζ  (đã clamp ≥ 0)
        public readonly float ZetaOmega;  // ζω₀  — precompute cho bao hình e^(−ζω₀·dt)
        public readonly float OmegaD;     // under: ω₀√(1−ζ²) | over: s=ω₀√(ζ²−1) | critical: 0
        public readonly SpringMode Mode;
        public readonly bool IsActive;    // false khi frequency ≤ 0 → Solve thành no-op

        private const float CriticalEpsilon = 1e-4f; // dải coi ζ = 1 (tránh chia 0 ở ω_d/s)

        private SpringConfig(float omega0, float zeta, float zetaOmega,
            float omegaD, SpringMode mode, bool isActive)
        {
            Omega0 = omega0; Zeta = zeta; ZetaOmega = zetaOmega;
            OmegaD = omegaD; Mode = mode; IsActive = isActive;
        }

        /// <summary>API chính (designer-friendly).</summary>
        /// <param name="frequency">Tần số f (Hz): cảm giác "độ cứng"/tốc độ dao động. ≤ 0 → lò xo tắt.</param>
        /// <param name="dampingRatio">ζ: &lt;1 nảy, =1 tới đích nhanh nhất không nảy, &gt;1 ì. Âm → clamp 0.</param>
        public static SpringConfig FromFrequency(float frequency, float dampingRatio)
        {
            if (frequency <= 0f) // lò xo tắt: không hội tụ, đánh dấu no-op
                return new SpringConfig(0f, 0f, 0f, 0f, SpringMode.CriticallyDamped, false);

            float omega0 = 2f * Mathf.PI * frequency;        // ω₀ = 2π·f  (§0.2)
            float zeta = dampingRatio < 0f ? 0f : dampingRatio; // ζ<0 = bơm năng lượng → nổ → clamp
            float zetaOmega = zeta * omega0;

            SpringMode mode;
            float omegaD;
            if (zeta < 1f - CriticalEpsilon)                 // under-damped (§0.3)
            {
                mode = SpringMode.UnderDamped;
                omegaD = omega0 * Mathf.Sqrt(1f - zeta * zeta);   // ω_d = ω₀√(1−ζ²)
            }
            else if (zeta > 1f + CriticalEpsilon)            // over-damped
            {
                mode = SpringMode.OverDamped;
                omegaD = omega0 * Mathf.Sqrt(zeta * zeta - 1f);   // s = ω₀√(ζ²−1)
            }
            else                                             // critical (ζ ≈ 1)
            {
                mode = SpringMode.CriticallyDamped;
                omegaD = 0f;
            }
            return new SpringConfig(omega0, zeta, zetaOmega, omegaD, mode, true);
        }

        /// <summary>Converter: tham số vật lý thô (k, c, m) → dạng chuẩn (§0.2 đảo ngược).</summary>
        public static SpringConfig FromPhysical(float stiffness, float damping, float mass = 1f)
        {
            // ω₀ = √(k/m); ζ = c / (2√(km)); f = ω₀ / 2π
            float omega0 = Mathf.Sqrt(stiffness / mass);
            float frequency = omega0 / (2f * Mathf.PI);
            float denom = 2f * Mathf.Sqrt(stiffness * mass);
            float zeta = denom > 0f ? damping / denom : 0f;
            return FromFrequency(frequency, zeta);
        }

        /// <summary>Converter ngược: dạng chuẩn → (k, c) để tra cứu/so sánh. k=ω₀²m, c=2ζω₀m.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public (float stiffness, float damping) ToPhysical(float mass = 1f)
        {
            float k = Omega0 * Omega0 * mass;
            float c = 2f * Zeta * Omega0 * mass;
            return (k, c);
        }
    }
}
```

- [ ] **Step 2: Kiểm chứng (chạy tay / nhẩm)**

| Input | Kỳ vọng |
|---|---|
| `FromFrequency(2, 0.5)` | `Mode=UnderDamped`, `Omega0≈12.566`, `OmegaD≈10.88` (`=ω₀√0.75`) |
| `FromFrequency(2, 1)` | `Mode=CriticallyDamped`, `OmegaD=0` |
| `FromFrequency(2, 2)` | `Mode=OverDamped`, `OmegaD≈21.77` (`=ω₀√3`) |
| `FromFrequency(0, 0.5)` | `IsActive=false` |
| `FromFrequency(2, -1)` | `Zeta=0` |
| `FromPhysical(k,c).ToPhysical()` | round-trip ≈ `(k,c)` (mass=1) |

  Unity biên dịch sạch (Console không lỗi).

- [ ] **Step 3: Commit**

```bash
git add Assets/Horcrux/Runtime/Utilities/PhysXHelper/Spring/SpringConfig.cs
git commit -m "feat(spring): SpringConfig - tham so + precompute + converter"
```

---

### Task 2: `SpringSolver` — lõi toán scalar (analytic + euler)

**Files:**
- Create: `Assets/Horcrux/Runtime/Utilities/PhysXHelper/Spring/SpringSolver.cs`

**Interfaces:**
- Consumes: `SpringConfig` (field `IsActive, Omega0, Zeta, ZetaOmega, OmegaD, Mode`) — Task 1.
- Produces:
  - `enum SpringMethod { Analytic, SemiImplicit }`
  - `static (float pos, float vel) Solve(float pos, float vel, float target, in SpringConfig cfg, SpringMethod method, float dt)`
  - `internal static (float, float) SolveAnalytic(float y0, float v0, in SpringConfig cfg, float dt)`
  - `internal static (float, float) SolveSemiImplicit(float y, float v, float omega0, float zeta, float dt)`

**Bản đồ toán → code:** `SolveAnalytic` = 3 hộp công thức §0.4 · `SolveSemiImplicit` = §0.5 · đổi biến `y=pos−target` rồi ngược lại = §0.2.

**Self-doc & tối ưu:**

| Quyết định | Lý do |
|---|---|
| `in SpringConfig` | readonly by-ref, **không copy** struct mỗi call |
| trả tuple `(float,float)` | value-type trên stack, zero-GC, không `out` rườm rà |
| tách `SolveAnalytic`/`SolveSemiImplicit` `internal` | test/đọc từng nhánh; đặt tên nói rõ phương pháp |
| guard `!IsActive \|\| dt≤0` ngay đầu | thoát sớm, không tính `exp` vô ích |
| `math.exp/sin/cos/...` (Unity.Mathematics) | thuần `float`, Burst-friendly, không cast `double` |
| tính `E` (bao hình) 1 lần, dùng chung | không gọi `exp` lặp trong nhánh |

- [ ] **Step 1: Tạo file với đầy đủ code**

```csharp
using Unity.Mathematics;

namespace Horcrux.Runtime.Utilities.PhysXHelper
{
    public enum SpringMethod { Analytic, SemiImplicit }

    /// <summary>
    /// Lõi giải lò xo 1 chiều (scalar). Vector2/3 gọi vào đây theo từng trục (§0.6).
    /// </summary>
    public static class SpringSolver
    {
        /// <summary>Tiến state lò xo một bước dt. Trả (position, velocity) mới.</summary>
        /// <remarks>Giải ÿ + 2ζω₀·ẏ + ω₀²·y = 0 với y = pos − target. Xem §0.4–0.5.</remarks>
        public static (float pos, float vel) Solve(
            float pos, float vel, float target,
            in SpringConfig cfg, SpringMethod method, float dt)
        {
            if (!cfg.IsActive || dt <= 0f) return (pos, vel); // guard: lò xo tắt / bước rỗng

            float y = pos - target; // đổi biến về khoảng cách còn lại (§0.2)

            (float ny, float nv) = method == SpringMethod.Analytic
                ? SolveAnalytic(y, vel, cfg, dt)
                : SolveSemiImplicit(y, vel, cfg.Omega0, cfg.Zeta, dt);

            return (ny + target, nv); // đổi biến ngược: x = y + target
        }

        /// <summary>Nghiệm giải tích — ổn định vô điều kiện với mọi dt (§0.4).</summary>
        internal static (float, float) SolveAnalytic(float y0, float v0, in SpringConfig cfg, float dt)
        {
            float zw = cfg.ZetaOmega;
            float e = math.exp(-zw * dt); // bao hình chung E = e^(−ζω₀·dt)

            switch (cfg.Mode)
            {
                case SpringMode.UnderDamped: // §0.4 ●under
                {
                    float wd = cfg.OmegaD;                 // ω_d
                    float c = math.cos(wd * dt);
                    float s = math.sin(wd * dt);
                    float b = (v0 + zw * y0) / wd;         // B = (v₀+ζω₀y₀)/ω_d

                    float y = e * (y0 * c + b * s);
                    float v = e * ((-zw * y0 + b * wd) * c - (zw * b + y0 * wd) * s);
                    return (y, v);
                }
                case SpringMode.OverDamped: // §0.4 ●over
                {
                    float sc = cfg.OmegaD;                 // s = ω₀√(ζ²−1)
                    float ch = math.cosh(sc * dt);
                    float sh = math.sinh(sc * dt);
                    float b = (v0 + zw * y0) / sc;         // B = (v₀+ζω₀y₀)/s

                    float y = e * (y0 * ch + b * sh);
                    float v = e * ((-zw * y0 + b * sc) * ch + (y0 * sc - zw * b) * sh);
                    return (y, v);
                }
                default: // CriticallyDamped — §0.4 ●critical
                {
                    float w0 = cfg.Omega0;
                    float coeff = v0 + w0 * y0;            // B = v₀+ω₀y₀
                    float y = e * (y0 + coeff * dt);
                    float v = e * (v0 - w0 * coeff * dt);
                    return (y, v);
                }
            }
        }

        /// <summary>Semi-implicit Euler — rẻ, có thể nổ khi ω₀·dt lớn (§0.5).</summary>
        internal static (float, float) SolveSemiImplicit(float y, float v, float omega0, float zeta, float dt)
        {
            float a = -(omega0 * omega0) * y - 2f * zeta * omega0 * v; // gia tốc a = −ω₀²y − 2ζω₀v
            v += a * dt;   // velocity trước
            y += v * dt;   // position sau (dùng v đã cập nhật)
            return (y, v);
        }
    }
}
```

- [ ] **Step 2: Kiểm chứng — kiểm mốc `dt→0` (bất biến quan trọng nhất)**

  Mọi mode, `SolveAnalytic(y0, v0, cfg, dt→0)` phải trả `≈ (y0, v0)`:

| Mode | Tại `dt=0` | Kết quả |
|---|---|---|
| Under | `e=1,c=1,s=0` | `y=y0`; `v=−zw·y0+b·wd = −zw·y0+(v0+zw·y0)=v0` ✓ |
| Over | `ch=1,sh=0` | `y=y0`; `v=−zw·y0+b·sc=v0` ✓ |
| Critical | `e=1` | `y=y0`, `v=v0` ✓ |

- [ ] **Step 3: Kiểm chứng — hội tụ & hành vi 3 mode**

  Vòng `for` ~5s, `dt=1/60`, `target=10`, `pos0=0`, `vel0=0`:

| ζ | Kỳ vọng | Tiêu chí |
|---|---|---|
| bất kỳ | `pos→10`, `vel→0` | #2 hội tụ |
| `1` | `pos` **không vượt** 10 (đơn điệu) | #3 critical |
| `0.3` | `pos` **vượt** 10 ≥1 lần, nảy giảm dần | #4 under |
| `2` | về 10 chậm hơn critical, không vượt | #5 over |

- [ ] **Step 4: Kiểm chứng — độc lập framerate (then chốt của Analytic)**

  Cùng `(pos0, vel0)`, chạy Analytic tới cùng `T=1s`:
  - A: 1 bước `dt=1` · B: 100 bước `dt=0.01`
  - Hai `pos` **trùng ~1e-4**. (Euler thì lệch rõ → đúng đặc tính.)

- [ ] **Step 5: Commit**

```bash
git add Assets/Horcrux/Runtime/Utilities/PhysXHelper/Spring/SpringSolver.cs
git commit -m "feat(spring): SpringSolver - loi toan scalar analytic + euler"
```

---

### Task 3: `FloatSpring` — state 1 chiều + API người dùng

**Files:**
- Create: `Assets/Horcrux/Runtime/Utilities/PhysXHelper/Spring/FloatSpring.cs`

**Interfaces:**
- Consumes: `SpringSolver.Solve(float, float, float, in SpringConfig, SpringMethod, float)` — Task 2; `SpringConfig`, `SpringMethod`.
- Produces:
  - `struct FloatSpringState { float Position; float Velocity; ctor(float position, float velocity = 0f) }`
  - `static void Solve(ref FloatSpringState s, float target, in SpringConfig cfg, float dt, SpringMethod method = SpringMethod.Analytic)`

**Self-doc & tối ưu:**

| Quyết định | Lý do |
|---|---|
| `struct` state | zero-GC, copy value an toàn, dễ pool/nhét vào array |
| `Solve(ref ...)` | cập nhật **in-place**, không trả struct mới (khỏi copy) |
| `method = Analytic` mặc định | chất lượng trước; muốn rẻ thì opt-in `SemiImplicit` |
| `AggressiveInlining` | wrapper mỏng → nội tuyến khỏi phí gọi hàm |

- [ ] **Step 1: Tạo file với đầy đủ code**

```csharp
using System.Runtime.CompilerServices;

namespace Horcrux.Runtime.Utilities.PhysXHelper
{
    /// <summary>State lò xo 1 chiều. Value-type: zero-GC, copy an toàn, dễ pool.</summary>
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
        /// <summary>Tiến lò xo về target một bước dt (in-place). Mặc định Analytic (chất lượng).</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Solve(ref FloatSpringState s, float target,
            in SpringConfig cfg, float dt, SpringMethod method = SpringMethod.Analytic)
        {
            (s.Position, s.Velocity) = SpringSolver.Solve(s.Position, s.Velocity, target, cfg, method, dt);
        }
    }
}
```

- [ ] **Step 2: Kiểm chứng — smoke test ref-update**

```csharp
var s = new FloatSpringState(0f);
var cfg = SpringConfig.FromFrequency(3f, 0.5f);
for (int i = 0; i < 300; i++) FloatSpring.Solve(ref s, 5f, cfg, 1f / 60f);
// Kỳ vọng: s.Position ≈ 5, s.Velocity ≈ 0 (cập nhật in-place qua ref).
```
  - `dt=0` → state không đổi (guard xuyên suốt từ SpringSolver).

- [ ] **Step 3: Commit**

```bash
git add Assets/Horcrux/Runtime/Utilities/PhysXHelper/Spring/FloatSpring.cs
git commit -m "feat(spring): FloatSpring - state 1 chieu + API Solve"
```

---

### Task 4: `Vector2Spring` + `Vector3Spring` — per-axis, tái dùng lõi

**Files:**
- Create: `Assets/Horcrux/Runtime/Utilities/PhysXHelper/Spring/Vector2Spring.cs`
- Create: `Assets/Horcrux/Runtime/Utilities/PhysXHelper/Spring/Vector3Spring.cs`

**Interfaces:**
- Consumes: `SpringSolver.Solve(float, float, float, in SpringConfig, SpringMethod, float)` — Task 2; `SpringConfig`, `SpringMethod`.
- Produces:
  - `struct Vector2SpringState { Vector2 Position; Vector2 Velocity; ctor(Vector2, Vector2 = default) }`
  - `static void Vector2Spring.Solve(ref Vector2SpringState, Vector2 target, in SpringConfig, float dt, SpringMethod = Analytic)`
  - `struct Vector3SpringState { Vector3 Position; Vector3 Velocity; ctor(Vector3, Vector3 = default) }`
  - `static void Vector3Spring.Solve(ref Vector3SpringState, Vector3 target, in SpringConfig, float dt, SpringMethod = Analytic)`

**Bản đồ toán → code:** §0.6 — mỗi trục độc lập, cùng `cfg`. **Không toán mới, chỉ gọi lõi scalar per-axis.**

**Self-doc & tối ưu:**

| Quyết định | Lý do |
|---|---|
| gọi `SpringSolver.Solve` mỗi trục | toán viết 1 lần (DRY); sửa lõi → mọi kiểu hưởng |
| `new Vector3(...)` cuối | Vector3 là **struct** → nằm stack, không GC |
| `Velocity = default` ctor | mặc định đứng yên, gọn |

- [ ] **Step 1: Tạo `Vector3Spring.cs`**

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
        /// <summary>Lò xo 3 trục độc lập, cùng config. Tái dùng lõi scalar — không lặp toán (§0.6).</summary>
        public static void Solve(ref Vector3SpringState s, Vector3 target,
            in SpringConfig cfg, float dt, SpringMethod method = SpringMethod.Analytic)
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

- [ ] **Step 2: Tạo `Vector2Spring.cs`**

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
        /// <summary>Lò xo 2 trục độc lập, cùng config. Tái dùng lõi scalar — không lặp toán (§0.6).</summary>
        public static void Solve(ref Vector2SpringState s, Vector2 target,
            in SpringConfig cfg, float dt, SpringMethod method = SpringMethod.Analytic)
        {
            var (px, vx) = SpringSolver.Solve(s.Position.x, s.Velocity.x, target.x, cfg, method, dt);
            var (py, vy) = SpringSolver.Solve(s.Position.y, s.Velocity.y, target.y, cfg, method, dt);
            s.Position = new Vector2(px, py);
            s.Velocity = new Vector2(vx, vy);
        }
    }
}
```

- [ ] **Step 3: Kiểm chứng — độc lập trục & khớp scalar**

  - `Vector3` target `(5, −3, 10)`, ~5s: mỗi trục hội tụ đúng thành phần, `Velocity→0`.
  - Chạy 1 trục Vector3 với cùng `(pos, vel, target, cfg, dt)` như `SpringSolver.Solve` scalar → **trùng khít** (chứng minh chỉ per-axis, không toán riêng).
  - `Vector2` tương tự `(5, −3)`.

- [ ] **Step 4: Commit**

```bash
git add Assets/Horcrux/Runtime/Utilities/PhysXHelper/Spring/Vector2Spring.cs Assets/Horcrux/Runtime/Utilities/PhysXHelper/Spring/Vector3Spring.cs
git commit -m "feat(spring): Vector2/Vector3 spring - per-axis tai dung loi"
```

---

## Ghi chú thực thi

- **Thứ tự:** 1 → 2 → 3/4 (3 và 4 độc lập, làm song song được sau khi 2 xong).
- **File `.meta`:** Unity tự sinh khi import — commit kèm nếu repo giữ GUID ổn định.
- **Kiểm chứng:** chạy tay qua script tạm hoặc nhẩm theo kiểm mốc — **không** tạo file test (spec §4). Xóa script tạm trước khi commit.
- **Muốn test tự động sau này:** dựng NUnit EditMode theo bảng tiêu chí §4 của spec — ngoài phạm vi plan này.
```
