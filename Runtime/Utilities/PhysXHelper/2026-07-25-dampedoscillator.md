# DampedOscillator Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.
>
> **Plan này là artifact học tập + triển khai, tự chứa.** Người dùng tự code lại để rèn tư duy → §0 dẫn giải **đầy đủ toán học** (biến đổi + lý do dùng công thức), các task chứa **code hoàn chỉnh** kèm chú thích self-doc & lý do tối ưu. Không code test → mỗi task có **checklist kiểm chứng** (kiểm mốc toán học).

---

## §0. Nền toán học (đọc trước khi code)

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

### 0.2b. Phương trình chi phối từ đâu ra — Newton, không phải tiên đề

Trước khi giải, phải biết phương trình **ở đâu chui ra**. Nó không phải định nghĩa áp đặt — nó là **Định luật II Newton** cho vật gắn lò xo có ma sát, viết gọn lại.

Xét vật khối lượng `m` trên lò xo, nhúng trong môi trường nhớt. `x` = độ lệch khỏi cân bằng. Ba lực tác dụng:

| Lực | Biểu thức | Dấu & lý do |
|---|---|---|
| **Đàn hồi** (lò xo kéo về) | $F_{\text{lò xo}} = -k\,x$ | Định luật Hooke. Dấu **−**: lệch phải (`x>0`) kéo về trái. `k` = độ cứng (N/m) |
| **Cản nhớt** (ma sát) | $F_{\text{cản}} = -c\,\dot{x}$ | Tỉ lệ **vận tốc** (đặc trưng cản nhớt). Dấu **−**: luôn ngược chiều chuyển động → hãm. `c` = hệ số cản (N·s/m) |
| **Quán tính** | $m\,\ddot{x}$ | vế "ma" của Newton |

Định luật II Newton $m\ddot{x} = \sum F$, chuyển hết về một vế:

$$m\ddot{x} = -k\,x - c\,\dot{x} \quad\Rightarrow\quad m\ddot{x} + c\,\dot{x} + k\,x = 0$$

Đây là **phương trình gốc thật** — quán tính + cản + đàn hồi. Chia cho `m` (`>0`) để số hạng $\ddot{x}$ về hệ số 1, rồi **đặt tên** hai cụm hệ số:

$$\ddot{x} + \frac{c}{m}\,\dot{x} + \frac{k}{m}\,x = 0, \qquad \omega_0^2 \equiv \frac{k}{m}, \qquad 2\lambda \equiv \frac{c}{m}$$

→ ra đúng phương trình §0.3. Hai phép đặt tên **không tùy tiện**, mỗi cái làm công thức sau gọn:

- **`ω₀² = k/m`** — bỏ cản (`c=0`) còn $\ddot{x}+\omega_0^2 x=0$, nghiệm $\cos(\omega_0 t)$ (thử: $\ddot{x}=-\omega_0^2 x$ ✓). Vậy $\omega_0=\sqrt{k/m}$ **là** tần số góc khi chưa cản → gọi "tần số tự nhiên". Lò xo cứng (`k↑`) / vật nhẹ (`m↓`) → lắc nhanh, đúng trực giác.
- **`2λ = c/m`** — số **2** để `λ` sau này trần trụi làm tốc độ tắt. Nghiệm bậc hai $r=\frac{-b\pm\sqrt{b^2-4ac}}{2a}$ với $b=2\lambda$: $\;r=\frac{-2\lambda\pm\sqrt{4\lambda^2-4\omega_0^2}}{2}=-\lambda\pm\sqrt{\lambda^2-\omega_0^2}$ — số 4 và 2 triệt tiêu sạch. Đặt `λ` (không có 2) thì bao phải viết $e^{-(\lambda/2)t}$ khắp nơi, xấu. Con số 2 chỉ để `λ` mang đúng nghĩa "tốc độ tắt của bao $e^{-\lambda t}$" (§0.2).

### 0.3. Từ phương trình chi phối → nghiệm đóng (suy ra, không áp đặt)

§0.2 giải thích phần **bao** `e^(−λt)`; §0.2b cho biết phương trình ở đâu ra. Còn *vì sao li độ = bao × cosin*? Suy ra từ chính phương trình đó:

$$\ddot{x} + 2\lambda\,\dot{x} + \omega_0^2\,x = 0$$

trong đó `ω₀` = tần số tự nhiên (khi chưa cản), `2λ` = hệ số cản đã chuẩn hóa (§0.2b). Giải theo 4 bước.

**Bước 1 — vì sao thử `x = e^{rt}`.** Phương trình tuyến tính, hệ số hằng: đạo hàm `x` không được đẻ ra dạng hàm mới, nếu không ba số hạng $\ddot{x}, \dot{x}, x$ không thể triệt tiêu nhau. Chỉ hàm mũ có tính chất "đạo hàm = chính nó nhân hằng" ($\frac{d}{dt}e^{rt}=r\,e^{rt}$) → mọi số hạng đều thành `(…)·e^{rt}`, giữ nguyên dạng. Nên `e^{rt}` là ứng viên nghiệm tự nhiên, còn `r` là ẩn cần tìm.

**Bước 2 — thế vào để khử `t`.** Với `x = e^{rt}` thì $\dot{x} = r\,e^{rt}$, $\ddot{x} = r^2 e^{rt}$. Thay cả ba vào phương trình:

$$r^2 e^{rt} + 2\lambda\,r\,e^{rt} + \omega_0^2\,e^{rt} = 0 \;\Rightarrow\; \underbrace{e^{rt}}_{\neq\,0}\,(r^2 + 2\lambda r + \omega_0^2) = 0$$

Vì $e^{rt}$ không bao giờ bằng 0, chia đi → còn **phương trình đặc trưng** thuần đại số (biến `t` biến mất):

$$r^2 + 2\lambda r + \omega_0^2 = 0 \;\Rightarrow\; r = \frac{-2\lambda \pm \sqrt{4\lambda^2 - 4\omega_0^2}}{2} = -\lambda \pm \sqrt{\lambda^2 - \omega_0^2}$$

**Bước 3 — dấu biệt thức quyết định 3 chế độ.** Cụm dưới căn $\Delta = \lambda^2 - \omega_0^2$ (so cản `λ` với tần số tự nhiên `ω₀`) chia làm 3 kiểu chuyển động:

| Điều kiện | Δ | Nghiệm `r` | Chuyển động |
|---|---|---|---|
| `λ < ω₀` (cản yếu) | `< 0` | phức liên hợp | **dao động tắt dần** ← ta cần |
| `λ = ω₀` (cản tới hạn) | `= 0` | kép thực `−λ` | về cân bằng nhanh nhất, không lắc |
| `λ > ω₀` (cản mạnh) | `> 0` | hai thực âm | bò về cân bằng, không lắc |

Hệ chỉ **lắc** khi cản yếu. Ta xét đúng nhánh này.

**Bước 4 — nghiệm phức → lượng giác (qua Euler).** Cản yếu → `Δ<0` → rút `i` ra khỏi căn số âm: $\sqrt{\lambda^2-\omega_0^2}=\sqrt{-(\omega_0^2-\lambda^2)}=i\sqrt{\omega_0^2-\lambda^2}$. Đặt **tần số quan sát** $\omega_d \equiv \sqrt{\omega_0^2-\lambda^2}$ (số thực dương):

$$r = -\lambda \pm i\,\omega_d$$

Nghiệm tổng quát của ODE bậc 2 là tổ hợp hai mũ ứng hai `r`, tách chung $e^{-\lambda t}$ (phần thực) khỏi $e^{\pm i\omega_d t}$ (phần ảo):

$$x(t) = C_1 e^{(-\lambda + i\omega_d)t} + C_2 e^{(-\lambda - i\omega_d)t} = e^{-\lambda t}\big(C_1 e^{i\omega_d t} + C_2 e^{-i\omega_d t}\big)$$

Bung hai mũ ảo bằng **công thức Euler** $e^{\pm i\theta}=\cos\theta \pm i\sin\theta$ (với $\theta=\omega_d t$) rồi gom cos, sin:

$$C_1 e^{i\omega_d t} + C_2 e^{-i\omega_d t} = \underbrace{(C_1+C_2)}_{A_1}\cos\omega_d t + \underbrace{i(C_1-C_2)}_{A_2}\sin\omega_d t$$

`x` là li độ vật lý (số thực) → buộc `A₁, A₂` thực (điều này ép $C_2$ là liên hợp của $C_1$; chi tiết số phức không cần cho code). Vậy:

$$x(t) = \underbrace{e^{-\lambda t}}_{\text{bao — §0.2}}\big[A_1\cos(\omega_d t) + A_2\sin(\omega_d t)\big]$$

**Gộp về một cosin duy nhất.** Tổng "cos + sin cùng tần số" luôn viết lại thành một cosin lệch pha — khai triển $A\cos(\omega_d t+\varphi)=A\cos\varphi\cos\omega_d t - A\sin\varphi\sin\omega_d t$ rồi khớp hệ số: $A_1=A\cos\varphi,\;A_2=-A\sin\varphi$, suy ra $A=\sqrt{A_1^2+A_2^2}$ và $\tan\varphi=-A_2/A_1$. Kết quả:

$$\boxed{\;x(t) = A\,e^{-\lambda t}\cos(\omega_d t + \varphi)\;}$$

→ **li độ = bao × cosin** giờ được *suy ra trọn vẹn*: bao `e^(−λt)` đúng như §0.2, phần lắc là cosin ở tần số `ω_d`, `A` và `φ` do điều kiện đầu (vị trí + vận tốc lúc `t=0`) định. Chọn `sin` thay `cos` chỉ là đổi mốc pha `φ` (vì $\sin\theta=\cos(\theta-\tfrac{\pi}{2})$).

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

**Quy đổi `halfLife` ↔ `decay`.** Designer thường nghĩ "sau bao lâu biên độ còn **một nửa**" (nửa đời `h`) hơn là con số `λ` trừu tượng. Đặt $`e^{-\lambda h} = \frac{1}{2}`$ rồi lấy `ln` hai vế:

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