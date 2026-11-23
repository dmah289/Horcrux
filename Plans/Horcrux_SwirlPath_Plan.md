# Plan — `SwirlPath` lên Horcrux

Ngày 2026-11-23. Nguồn: category-jam `Assets/_CategoryJam/Scripts/Director/FanSwirl.cs` (`FanSwirlTuning`, `FanSwirl`), call site
`ShuffleBooster` (cú xoáy của booster Fan), test `Tests/EditMode/FanSwirlTests.cs`. Một nhóm vật cuộn vào quanh một tâm rồi bung về
đúng chỗ cũ sau một số vòng nguyên; vật gần tâm quay nhiều vòng hơn vật xa nên cả nhóm không xoay như một tấm cứng.

**Phân công:** GĐ1–2 developer gõ, agent viết test theo bảng case rồi compile · GĐ3 (tài liệu), GĐ4 (category-jam chuyển sang bản
Horcrux, xoá `FanSwirl`) agent làm. **Chạy được, thấy được** ở GĐ1–2 = compile sạch + bảng case xanh trong
`Tests/EditMode/SwirlPathTests.cs`; thấy trong game ở GĐ4. GĐ1–3 commit trong `Assets/Horcrux`, GĐ4 commit ở dự án kèm con trỏ
submodule. Không phụ thuộc Plan nào.

## Ngữ cảnh đã chốt

- **Người dùng:** cú xáo trong game tile, match-3 (vật cuộn vào, đổi hình ở đỉnh xoáy, bung về ô) · bài solitaire gom về giữa rồi chia
  lại · xu, ngọc xoáy quanh rương trước khi nổ ra. Caller tự lo vật (bản sao, đổi hình, trả pool); utility chỉ trả tư thế.
- **Mục tiêu:** một hàm thuần trả tư thế (vị trí, góc nghiêng, hệ số cỡ) của một vật ở tiến độ `t` trên đường xoáy, cùng một hàm chọn
  số vòng theo khoảng cách tới tâm. Hành vi trùng bản dự án ở mọi `t` (bảng case dời nguyên từ `FanSwirlTests`).
- **Chỗ đặt, tên:** `Runtime/Utilities/PhysXHelper/SwirlPath.cs`, namespace `Horcrux.Runtime.Utilities.PhysXHelper`, assembly
  `com.horcrux.runtime` (không thêm reference). `SwirlSpec` theo khuôn `ArcFlightSpec`, `SquashStretchSpec`. Tên đổi tuỳ ý trước khi gõ.
- **Không làm:**
  - Tween, chạy theo thời gian: caller đã có `Tween.Custom` cho cả nhóm; một tween cho n vật rẻ hơn n tween, và caller cần chèn việc
    vào giữa (đổi hình lúc qua `TurnPoint`).
  - Ease chọn được cho từng pha: InQuad vào, OutQuad ra là hình dạng "hút vào rồi bật ra"; đổi ease là đổi cử chỉ, chưa có caller cần.
  - Mặt phẳng khác XY: quay quanh trục Z như mọi helper trong `PhysXHelper`.
- **Quyết định trái trực giác:**
  - **Số vòng là số nguyên:** chỉ vòng nguyên mới đưa vật về đúng chỗ cũ. Lệch nhịp giữa vật gần và xa đến từ *số* vòng khác nhau, không
    từ tốc độ góc lẻ.
  - **Đỉnh xoáy là cú bật:** bán kính đổi chiều ngay tại `TurnPoint` và tốc độ góc tụt (vào nhanh dần, ra chậm dần). Vị trí liền, vận tốc
    không liền — cố ý, đó là nhịp "bị hút rồi văng ra".
  - **Vật nằm đúng tâm đứng yên** (bán kính 0 nhân mọi góc vẫn là 0): không cần nhánh riêng.
  - `SwirlSpec` là **class** có giá trị mặc định, không struct: struct dựng bằng code có field 0 (`InDuration = 0` làm `TurnPoint` chia 0).
- **Mở rộng sau:** ease chọn được mỗi pha · độ trễ so le theo khoảng cách (khi có demo cần) · mặt phẳng tuỳ ý.

**Đã khảo sát**

| Nguồn | Lấy | Không lấy, vì sao |
|---|---|---|
| `FanSwirl.cs` (dự án) | toàn bộ toán, `TurnsAt`, cấu trúc `Pose` | tên, chú thích mang domain "quạt", "tile" |
| `FanSwirlTests.cs` (dự án) | sáu case làm bảng case GĐ1–2 | — |
| `ShuffleBooster.ApplySwirl` (dự án) | khuôn caller: một `Tween.Custom` cho cả nhóm, đổi hình khi `t ≥ TurnPoint` | — |
| Horcrux `CharmFlyArc`, `BezierCurveHelper` | — | đường bezier một chiều tới đích, không quay quanh tâm |
| Horcrux `GeometryHelper`, `Interpolator`, `HarmonicOscillator` | — | điểm ngẫu nhiên trong vành khăn, remap, dao động: không có đường cực |
| PrimeTween | — | không có tween quỹ đạo quanh tâm |

## Luồng mô tả — một cú xoáy

```
caller chọn tâm, gom vật ──► mỗi vật: start, r = |start − tâm| ──► TurnsAt(r, rMax, spec) = số vòng      GĐ2
        ▼
Tween.Custom(0→1, spec.TotalDuration) ──► mỗi frame, mỗi vật: Evaluate(tâm, start, t, vòng, spec)        GĐ1
        │                                   └─► Pose: Position · LeanDegrees (góc Z) · Scale (nhân cỡ gốc)
        ├─ t < TurnPoint: cuộn vào, co về InnerRadius · t ≥ TurnPoint: bung ra, đủ vòng nguyên
        ▼
t = 1: mọi vật đúng chỗ cũ, góc 0, cỡ 1 ──► caller trả bản sao, hiện vật thật
```

## Bản đồ hệ cuối

```
caller (dự án: chọn tâm, gom vật, chạy tween, đổi hình ở đỉnh xoáy)
   │ TurnsAt(radius, maxRadius, spec) · Evaluate(center, start, t, turns, spec) · spec.TotalDuration · spec.TurnPoint
   ▼
SwirlPath (static, thuần) ──► Pose (readonly struct)
   ▲
SwirlSpec ([Serializable] class: thời lượng hai pha, bán kính và cỡ ở đỉnh, phần vòng lúc vào, vòng thêm ở tâm, chiều, góc nghiêng)
```

## §0 — Đường xoáy trong toạ độ cực

**Hiện tượng.** Vật bị hút xoáy vào gần tâm rồi văng ra về chỗ cũ. Viết trong toạ độ cực quanh tâm thì chỉ còn hai đại lượng theo thời
gian: **bán kính** (co rồi nở) và **góc** (quay đủ số vòng nguyên). Vị trí đầu và cuối trùng nhau vì bán kính về hệ số 1 và góc về bội
của 360°.

| Ký hiệu | Nghĩa |
|---|---|
| $c$, $p_0$ | tâm, chỗ cũ của vật; $\vec o = p_0 - c$ |
| $t \in [0,1]$ | tiến độ cả cú xoáy, tuyến tính theo thời gian |
| $\tau = \frac{T_{in}}{T_{in} + T_{out}}$ | `TurnPoint`: mốc đỉnh xoáy |
| $k$ | `InnerRadius`, hệ số bán kính ở đỉnh |
| $q$ | `InTurnPart`, phần vòng đã quay khi tới đỉnh |
| $N$ | số vòng nguyên của vật |
| $\sigma$ | $-1$ khi cùng chiều kim đồng hồ, $+1$ khi ngược |

① Mỗi pha có tiến độ riêng $u$ và một ease: pha vào $u = t/\tau$, $e = u^2$ (InQuad, nhanh dần vào tâm); pha ra
$u = \frac{t - \tau}{1 - \tau}$, $e = 1 - (1-u)^2$ (OutQuad, văng ra rồi chậm dần).

② Độ co $s$ (0 ở chỗ cũ, 1 ở đỉnh) và phần vòng $\varphi$:
$$\text{vào: } s = e,\ \varphi = q\,e \qquad\qquad \text{ra: } s = 1 - e,\ \varphi = q + (1-q)\,e$$

③ Tư thế:
$$p = c + R_z\!\left(360^\circ\,\sigma N \varphi\right)\,\vec o\,\big(1 + (k-1)s\big), \quad \text{nghiêng} = \sigma\,L\,s, \quad \text{cỡ} = 1 + (k_s - 1)s$$
với $L$ = `LeanDegrees`, $k_s$ = `InnerScale`.

④ Số vòng theo khoảng cách, $E$ = `ExtraTurnsAtCenter`, $r_{max}$ khoảng cách vật xa nhất:
$$N = 1 + \operatorname{round}\!\left(E\,\big(1 - \min(1, r/r_{max})\big)\right), \qquad r_{max} = 0 \Rightarrow N = 1$$

| Mốc | Kỳ vọng | ✓ |
|---|---|---|
| $t = 0$ | $s = 0, \varphi = 0$: đúng chỗ cũ, nghiêng 0, cỡ 1 | GĐ1 case 1 |
| $t = 1$ | $s = 0, \varphi = 1$: góc $360^\circ N$ → đúng chỗ cũ, mọi $N$, cả hai chiều | GĐ1 case 1 |
| $t = \tau$ | hai pha cho cùng $s = 1$, $\varphi = q$: bán kính $k\,r$, cỡ $k_s$, nghiêng $L$ | GĐ1 case 2, 4 |
| $\vec o = 0$ | mọi $t$ ra tâm | GĐ1 case 3 |
| $\sigma = -1$, vật bên phải tâm, $t$ nhỏ | $y < c_y$ (đi xuống) | GĐ1 case 5 |
| $r = r_{max}$ · $r = 0$ · $r = r_{max}/2$, $E = 2$ | $N = 1$ · $3$ · $2$ | GĐ2 case 1 |

**Bản mẫu:** bản dự án `FanSwirl` cùng thuật toán, 11 lượt test xanh (2026-11-19, `verify.sh`); case liền mạch quét 2000 bước mỗi vật
trên bốn vật. Đối chứng đỏ: đổi $N$ thành số lẻ (1.5) thì case 1 đỏ ở $t = 1$.

---

## Giai đoạn 1 — Một vật xoáy vào rồi về chỗ cũ

**Mục tiêu:** caller có tâm, chỗ cũ, số vòng và spec; mỗi frame gọi `Evaluate` ra tư thế. Xong là một nhóm vật xoáy đều số vòng (chưa
lệch nhịp gần–xa).

**Vì sao bây giờ:** đây là lõi; chọn số vòng (GĐ2) chỉ là đầu vào của nó.

**Toán → code:** ①② nằm ở hai nhánh `if` trong `Evaluate` — `eased` là $e$, `squeeze01` là $s$, `turn01` là $\varphi$. ③ là ba dòng
cuối: `Quaternion.Euler(0, 0, …)` là $R_z$, `Mathf.Lerp(1, InnerRadius, squeeze01)` là $1 + (k-1)s$.

### `Runtime/Utilities/PhysXHelper/SwirlPath.cs` — file mới

```csharp
using System;
using UnityEngine;

namespace Horcrux.Runtime.Utilities.PhysXHelper
{
    /// <summary>Núm của đường xoáy: thời lượng hai pha, hình dạng ở đỉnh xoáy, chiều quay.</summary>
    [Serializable]
    public sealed class SwirlSpec
    {
        [Tooltip("Số giây vật cuộn vào quanh tâm (InQuad).")]
        [Min(0.01f)] public float InDuration = 0.3f;
        [Tooltip("Số giây vật bung ra về chỗ cũ (OutQuad).")]
        [Min(0.01f)] public float OutDuration = 0.17f;
        [Tooltip("Bán kính ở đỉnh xoáy, phần của khoảng cách vật tới tâm lúc đầu.")]
        [Range(0f, 1f)] public float InnerRadius = 0.4f;
        [Tooltip("Phần của một vòng quay đã đi hết lúc ở đỉnh xoáy; phần còn lại đi lúc bung ra.")]
        [Range(0f, 1f)] public float InTurnPart = 0.8f;
        [Tooltip("Chiều quay quanh tâm: bật là cùng chiều kim đồng hồ.")]
        public bool Clockwise = true;
        [Tooltip("Góc nghiêng lớn nhất của vật ở đỉnh xoáy (độ), về 0 khi đáp.")]
        [Range(0f, 45f)] public float LeanDegrees = 25f;
        [Tooltip("Cỡ vật ở đỉnh xoáy so với cỡ lúc đầu.")]
        [Range(0.3f, 1f)] public float InnerScale = 0.85f;

        public float TotalDuration => InDuration + OutDuration;
        public float TurnPoint => InDuration / TotalDuration;
    }

    /// <summary>
    /// Tư thế một vật trên đường xoáy quanh một tâm, toạ độ cực trong mặt phẳng XY: pha vào co bán kính về InnerRadius và quay
    /// InTurnPart số vòng; pha ra nở về bán kính cũ và quay nốt cho đủ số vòng nguyên, nên vật đáp đúng chỗ xuất phát.
    /// </summary>
    public static class SwirlPath
    {
        public readonly struct Pose
        {
            public readonly Vector3 Position;
            public readonly float LeanDegrees;
            public readonly float Scale;

            public Pose(Vector3 position, float leanDegrees, float scale)
            {
                Position = position;
                LeanDegrees = leanDegrees;
                Scale = scale;
            }
        }

        /// <param name="t">Tiến độ cả cú xoáy, 0..1, tuyến tính theo thời gian; ease mỗi pha nằm ở đây.</param>
        /// <param name="turns">Số vòng nguyên vật quay quanh tâm; vòng lẻ thì vật không về chỗ cũ.</param>
        public static Pose Evaluate(Vector3 center, Vector3 start, float t, int turns, SwirlSpec spec)
        {
            t = Mathf.Clamp01(t);
            var turnPoint = spec.TurnPoint;

            // squeeze01: 0 ở chỗ cũ, 1 ở đỉnh xoáy. turn01: phần vòng đã quay.
            float squeeze01, turn01;
            if (t < turnPoint)
            {
                var u = t / turnPoint;
                var eased = u * u;
                squeeze01 = eased;
                turn01 = spec.InTurnPart * eased;
            }
            else
            {
                var u = (t - turnPoint) / (1f - turnPoint);
                var eased = 1f - (1f - u) * (1f - u);
                squeeze01 = 1f - eased;
                turn01 = Mathf.Lerp(spec.InTurnPart, 1f, eased);
            }

            var sign = spec.Clockwise ? -1f : 1f;
            var radius = Mathf.Lerp(1f, spec.InnerRadius, squeeze01);
            var offset = Quaternion.Euler(0f, 0f, sign * 360f * turns * turn01) * (start - center) * radius;

            return new Pose(
                center + offset,
                sign * spec.LeanDegrees * squeeze01,
                Mathf.Lerp(1f, spec.InnerScale, squeeze01));
        }
    }
}
```

*Quyết định:* `Pose` trả hệ số cỡ, không trả `Vector3` cỡ — caller nhân vào cỡ gốc của từng vật (vật khác cỡ, `lossyScale` khác nhau).
Góc trả bằng độ quanh Z, caller gán `localRotation = Quaternion.Euler(0, 0, pose.LeanDegrees)`. *Bỏ:* trả `Matrix4x4` — caller nào
cũng phải tách lại.

**Kiểm** — compile sạch; agent viết `Tests/EditMode/SwirlPathTests.cs` (namespace `Horcrux.Tests`), tâm `(1, −2, 0)`, bốn chỗ cũ
`(4, −2)`, `(−3, 1.5)`, `(1, 3)`, `(1.2, −2.1)` (có vật rất gần tâm):

| # | Case | Kỳ vọng |
|---|---|---|
| 1 | `t ∈ {0, 1}`, mọi chỗ cũ, `Clockwise ∈ {true, false}`, `turns ∈ {1, 2, 3}` | vị trí lệch chỗ cũ < 1e-3, nghiêng 0, cỡ 1 |
| 2 | `t = TurnPoint`, `turns = 2` | khoảng cách tới tâm = `r · InnerRadius`, cỡ = `InnerScale`, \|nghiêng\| = `LeanDegrees` |
| 3 | chỗ cũ trùng tâm, `t` quét 0..1 bước 0.05 | luôn ở tâm |
| 4 | quét 2000 bước, `turns = 2`, mọi chỗ cũ | mỗi bước dời < `2πr / 2000 · 8` (liền mạch qua `TurnPoint`) |
| 5 | `Clockwise`, chỗ cũ `(c.x + 2, c.y)`, `t = 0.3 · TurnPoint`, `turns = 1` | `y < c.y` |

**Commit:** `feat(swirl): SwirlPath.Evaluate — đường xoáy về chỗ cũ sau số vòng nguyên`.

---

## Giai đoạn 2 — Vật gần tâm quay nhiều vòng hơn

**Mục tiêu:** caller chọn số vòng mỗi vật theo khoảng cách tới tâm; cả nhóm không còn xoay như một tấm cứng mà vẫn đáp đúng chỗ.

**Vì sao bây giờ:** cùng một số vòng thì mọi vật giữ nguyên góc tương đối với nhau suốt cú xoáy — nhìn như cả khối xoay. Lệch nhịp chỉ
đến được từ số vòng (§0 quyết định trái trực giác), nên nó là một hàm chọn đầu vào cho GĐ1.

**Toán → code:** ④ là một dòng; `Mathf.Clamp01(radius / maxRadius)` là $\min(1, r/r_{max})$, nhánh `maxRadius > 0` là ca một vật
duy nhất nằm ở tâm.

### `SwirlPath.cs` › `SwirlSpec` › sau `InTurnPart` — thêm

```csharp
        [Tooltip("Số vòng thêm của vật sát tâm so với vật xa nhất (một vòng): vật gần quay nhanh hơn, cả nhóm không xoay cứng như một tấm.")]
        [Range(0, 3)] public int ExtraTurnsAtCenter = 1;
```

### `SwirlPath.cs` › `SwirlPath` › trước `Evaluate` — thêm

```csharp
        /// <summary>Số vòng nguyên của vật cách tâm <paramref name="radius"/>, khi vật xa nhất cách <paramref name="maxRadius"/>.</summary>
        public static int TurnsAt(float radius, float maxRadius, SwirlSpec spec) =>
            1 + (maxRadius > 0f ? Mathf.RoundToInt(spec.ExtraTurnsAtCenter * (1f - Mathf.Clamp01(radius / maxRadius))) : 0);
```

*Quyết định:* trả `int`, caller lưu một số mỗi vật lúc bắt đầu, không tính lại mỗi frame (khoảng cách đầu không đổi). `round`, không
`floor`: vật giữa nhóm nhận nửa số vòng thêm. *Bỏ:* tốc độ góc liên tục theo bán kính — vật không về chỗ cũ.

**Kiểm** — thêm vào `SwirlPathTests`:

| # | Case | Kỳ vọng |
|---|---|---|
| 1 | `ExtraTurnsAtCenter = 2`: `TurnsAt(5, 5)` · `(0, 5)` · `(2.5, 5)` · `(0, 0)` | 1 · 3 · 2 · 1 |
| 2 | `ExtraTurnsAtCenter = 0`: `TurnsAt(0, 5)` | 1 |

**Commit:** `feat(swirl): SwirlPath.TurnsAt — vòng thêm cho vật gần tâm`.

---

## Giai đoạn 3 — Tài liệu (agent)

`docs/Runtime/SwirlPath/SwirlPath.md` và `SwirlPath.html` theo skill `docs`: luồng một cú xoáy, bảng núm `SwirlSpec`, §0 rút gọn, khuôn
caller (một `Tween.Custom` cho cả nhóm, đổi hình khi qua `TurnPoint`, `finally` trả bản sao), bẫy (số vòng lẻ, gán cỡ tuyệt đối thay vì
nhân cỡ gốc). Xoá Plan này cùng commit. **Commit:** `docs(swirl): SwirlPath.md, .html; xoá Plan`.

## Giai đoạn 4 — category-jam dùng bản Horcrux (agent)

| File | Đổi |
|---|---|
| `Scripts/Director/ShuffleBooster.cs` | `using Horcrux.Runtime.Utilities.PhysXHelper;` · `FanSwirlTuning` → `SwirlSpec` · `FanSwirl.` → `SwirlPath.` |
| `Scripts/Director/FanSwirl.cs` (+ `.meta`) | xoá |
| `Tests/EditMode/FanSwirlTests.cs` (+ `.meta`) | xoá (đã dời sang Horcrux ở GĐ1–2) |
| `Scripts/Feel.md` §9, `Structure.md` | tên `FanSwirlTuning`, `FanSwirl` → `SwirlSpec`, `SwirlPath` (Horcrux) |

`_swirl` trên `GameplayRoot.prefab` giữ nguyên số: Unity lưu field của class `[Serializable]` theo tên field, không theo tên class, và
tên field trong `SwirlSpec` trùng bản dự án. **Kiểm:** grep `FanSwirl` trong `Assets/_CategoryJam` ra 0 · `verify.sh` xanh · YAML
`_swirl` trong prefab không đổi · developer chơi thử: bấm Fan, tile xoáy và về ô như trước. **Commit:** ở dự án, kèm con trỏ submodule.

## File → giai đoạn

| File | GĐ |
|---|---|
| `Runtime/Utilities/PhysXHelper/SwirlPath.cs` | 1, 2 |
| `Tests/EditMode/SwirlPathTests.cs` | 1, 2 |
| `docs/Runtime/SwirlPath/SwirlPath.md`, `.html` | 3 |
| category-jam `ShuffleBooster.cs`, `FanSwirl.cs`, `FanSwirlTests.cs`, `Feel.md`, `Structure.md` | 4 |
