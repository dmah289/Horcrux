# Plan — `SwirlPath`: một nhóm vật xoáy vào quanh tâm rồi bung về đúng chỗ cũ

Ngày 2026-11-23. Nguồn: category-jam `Assets/_CategoryJam/Scripts/Director/FanSwirl.cs` (`FanSwirlTuning`, `FanSwirl`), call site
`ShuffleBooster` (cú xoáy của booster Fan), test `Tests/EditMode/FanSwirlTests.cs`, đã chơi thử. Vật cuộn vào quanh một tâm rồi bung về
đúng chỗ cũ sau một số vòng nguyên; vật gần tâm quay nhiều vòng hơn vật xa nên cả nhóm không xoay như một tấm cứng.

**Phân công:** GĐ1 developer gõ, agent viết test theo bảng case rồi compile · GĐ2 (tài liệu), GĐ3 (dự án chuyển sang bản Horcrux, xoá
bản tạm, xoá Plan) agent làm.
**Chạy được, thấy được:** GĐ1 compile sạch + bảng case xanh trong `Tests/EditMode/SwirlPathTests.cs`; thấy trong game ở GĐ3.
**Lệnh kiểm** (chạy từ gốc dự án): GĐ1 `bash "Assets/Horcrux/Editor/CompileCheck/Tools~/verify.sh" --csproj com.horcrux.runtime.csproj
--tests Horcrux.EditModeTests`; GĐ3 `… --csproj CategoryJam.Gameplay.csproj,com.horcrux.runtime.csproj --tests
CategoryJam.EditModeTests,Horcrux.EditModeTests`.
**Commit:** GĐ1, GĐ2 trong `Assets/Horcrux`; GĐ3 ở dự án kèm con trỏ submodule.
**Phụ thuộc:** không.
**Chung file:** category-jam `Scripts/Feel.md` › Nghiệm thu, dòng "Máy kiểm" — `Horcrux_FeelUtilities_Plan.md` GĐ9 và
`Horcrux_HopCurve_DipFlash_Plan.md` GĐ10 cũng đổi tên test trên dòng này; mỗi Plan chỉ đổi tên của mình, thứ tự nào cũng được.
**Đã kiểm trước khi ghi:** csc Roslyn (Rider 2026.2, `-langversion:9 -t:library`) trên `UnityEngine.CoreModule.dll` 6000.0.73f1 +
`netstandard.dll` 2.1, cùng bản chép `QuadEase.cs`: `SwirlPath.cs` của GĐ1, sạch. Bản mẫu thuần C# (quay quanh Z bằng cos/sin thay
`Quaternion.Euler`) chạy cả bảng case GĐ1 trên hai biến thể ease: biểu thức viết tay của bản tạm 79/79 kiểm xanh, `QuadEase` 79/79
xanh; chênh vị trí lớn nhất giữa hai biến thể 5.7e-6 trên 2000 bước × 4 vật. Đối chứng: số vòng lẻ (+0.5) cho 24 kiểm đỏ, đúng ở
`t = 1`. Bản tạm `FanSwirl`: `FanSwirlTests` 11 lượt xanh (2026-11-19). `§0`: có — đường cực hai pha, mốc kiểm ở bảng §0.

## Ngữ cảnh đã chốt

- **Người dùng:** cú xáo trong game tile, match-3 (vật cuộn vào, đổi hình ở đỉnh xoáy, bung về ô) · bài solitaire gom về giữa rồi chia
  lại · xu, ngọc xoáy quanh rương trước khi nổ ra.
- **Mục tiêu:** một hàm thuần trả tư thế (vị trí, góc nghiêng, hệ số cỡ) của một vật ở tiến độ `t` trên đường xoáy, cùng một hàm chọn
  số vòng theo khoảng cách tới tâm. Hành vi trùng bản tạm ở mọi `t` trong dung sai 1e-3 (bảng case dời từ test bản tạm).
- **Ranh giới:** utility chỉ trả tư thế; caller tự lo vật (bản sao, đổi hình, trả pool) và tự chạy tween.
- **Chỗ đặt, tên:** `Runtime/Utilities/PhysXHelper/SwirlPath.cs`, namespace `Horcrux.Runtime.Utilities.PhysXHelper`, assembly
  `com.horcrux.runtime` (không thêm reference) — thư mục của các hàm toán thuần có sẵn. `SwirlSpec` là class `[Serializable]`, field
  PascalCase: tên field trùng bản tạm nên YAML trên prefab giữ nguyên số. Tên đổi tuỳ ý trước khi gõ.
- **Cố ý KHÔNG làm, kèm lý do:**
  - Tween, chạy theo thời gian: một tween cho n vật rẻ hơn n tween, và caller cần chèn việc vào giữa (đổi hình lúc qua `TurnPoint`).
  - Ease chọn được cho từng pha: InQuad vào, OutQuad ra là hình dạng "hút vào rồi bật ra"; đổi ease là đổi cử chỉ, chưa có caller cần.
  - Mặt phẳng khác XY: quay quanh trục Z, cùng mặt XY như `BezierCurveHelper`.
- **Quyết định trái trực giác:**
  - **Số vòng là số nguyên:** chỉ vòng nguyên mới đưa vật về đúng chỗ cũ. Lệch nhịp giữa vật gần và xa đến từ *số* vòng khác nhau, không
    từ tốc độ góc lẻ.
  - **Đỉnh xoáy là cú bật:** bán kính đổi chiều ngay tại `TurnPoint` và tốc độ góc tụt (vào nhanh dần, ra chậm dần). Vị trí liền, vận tốc
    không liền — cố ý, đó là nhịp "bị hút rồi văng ra".
  - **Vật nằm đúng tâm đứng yên** (bán kính 0 nhân mọi góc vẫn là 0): không cần nhánh riêng.
  - `SwirlSpec` là **class** có giá trị mặc định, không struct: struct dựng bằng code có field 0 (`InDuration = 0` làm `TurnPoint` chia 0).
- **Mở rộng sau:** ease chọn được mỗi pha · độ trễ so le theo khoảng cách (khi có demo cần) · mặt phẳng tuỳ ý.

## Đã khảo sát

| Nguồn | Lấy | Không lấy, vì sao |
|---|---|---|
| category-jam `FanSwirl.cs` (bản tạm) | toàn bộ toán, `TurnsAt`, cấu trúc `Pose`, tên field `FanSwirlTuning` | tên type, chú thích mang từ vựng dự án |
| category-jam `FanSwirlTests.cs` | sáu test làm bảng case GĐ1 | — |
| category-jam `ShuffleBooster.ApplySwirl` | khuôn caller: một `Tween.Custom` cho cả nhóm, đổi hình khi `t ≥ TurnPoint` | — |
| Horcrux `QuadEase.In/Out` (`Tweening/Easings/Curves`, internal, cùng assembly) | ease hai pha, gọi thẳng | `Easer.Evaluate(EaseType, t)`: thêm một switch mỗi frame mỗi vật cho ease đã cố định |
| Horcrux `ArcFlightSpec`, `SquashStretchSpec` | — | struct, field camelCase, không mặc định; `SwirlSpec` cần mặc định và giữ tên field bản tạm |
| Horcrux `CharmFlyArc`, `BezierCurveHelper` | — | đường bezier một chiều tới đích, không quay quanh tâm |
| Horcrux `GeometryHelper`, `Interpolator`, `HarmonicOscillator` | — | điểm ngẫu nhiên trong vành khăn, remap, dao động: không có đường cực |
| PrimeTween | — | không có tween quỹ đạo quanh tâm |

## Luồng

```
caller chọn tâm, gom vật ──► mỗi vật: start, r = |start − tâm| ──► TurnsAt(r, rMax, spec) = số vòng      GĐ1
        ▼
Tween.Custom(0→1, spec.TotalDuration) ──► mỗi frame, mỗi vật: Evaluate(tâm, start, t, vòng, spec)        GĐ1
        │                                   └─► Pose: Position · LeanDegrees (góc Z) · Scale (nhân cỡ gốc)
        ├─ t < TurnPoint: cuộn vào, co về InnerRadius · t ≥ TurnPoint: bung ra, đủ vòng nguyên
        ▼
t = 1: mọi vật đúng chỗ cũ, góc 0, cỡ 1 ──► caller trả bản sao, hiện vật thật
```

## Bản đồ hệ cuối

```
caller (chọn tâm, gom vật, chạy tween, đổi hình ở đỉnh xoáy)
   │ TurnsAt(radius, maxRadius, spec) · Evaluate(center, start, t, turns, spec) · spec.TotalDuration · spec.TurnPoint
   ▼
SwirlPath (static, thuần) ──► Pose (readonly struct)
   ▲        └─► QuadEase.In / Out (có sẵn, internal)
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
$u = \frac{t - \tau}{1 - \tau}$, $e = u(2-u) = 1 - (1-u)^2$ (OutQuad, văng ra rồi chậm dần).

② Độ co $s$ (0 ở chỗ cũ, 1 ở đỉnh) và phần vòng $\varphi$:
$$\text{vào: } s = e,\ \varphi = q\,e \qquad\qquad \text{ra: } s = 1 - e,\ \varphi = q + (1-q)\,e$$

③ Tư thế:
$$p = c + R_z\!\left(360^\circ\,\sigma N \varphi\right)\,\vec o\,\big(1 + (k-1)s\big), \quad \text{nghiêng} = \sigma\,L\,s, \quad \text{cỡ} = 1 + (k_s - 1)s$$
với $L$ = `LeanDegrees`, $k_s$ = `InnerScale`.

④ Số vòng theo khoảng cách, $E$ = `ExtraTurnsAtCenter`, $r_{max}$ khoảng cách vật xa nhất:
$$N = 1 + \operatorname{round}\!\left(E\,\big(1 - \min(1, r/r_{max})\big)\right), \qquad r_{max} = 0 \Rightarrow N = 1$$

| Mốc | Kỳ vọng | ✓ |
|---|---|---|
| $t = 0$ | $s = 0, \varphi = 0$: đúng chỗ cũ, nghiêng 0, cỡ 1 | case 1 |
| $t = 1$ | $s = 0, \varphi = 1$: góc $360^\circ N$ → đúng chỗ cũ, mọi $N$, cả hai chiều | case 1 |
| $t = \tau$ | hai pha cho cùng $s = 1$, $\varphi = q$: bán kính $k\,r$, cỡ $k_s$, nghiêng $L$ | case 2, 4 |
| $\vec o = 0$ | mọi $t$ ra tâm | case 3 |
| $\sigma = -1$, vật bên phải tâm, $t$ nhỏ | $y < c_y$ (đi xuống) | case 5 |
| $r = r_{max}$ · $r = 0$ · $r = r_{max}/2$, $E = 2$ | $N = 1$ · $3$ · $2$ | case 6 |

**Bản mẫu:** xem "Đã kiểm trước khi ghi" ở đầu Plan.

---

## Giai đoạn 1 — Nhóm vật xoáy về chỗ cũ, vật gần tâm quay nhiều vòng hơn

**Mục tiêu:** caller có tâm, chỗ cũ và spec; chọn số vòng mỗi vật bằng `TurnsAt`, mỗi frame gọi `Evaluate` ra tư thế. Xong là cả nhóm
xoáy vào, bung về đúng chỗ, vật gần tâm lệch nhịp với vật xa.

**Vì sao bây giờ:** đây là toàn bộ phần Horcrux. `TurnsAt` chỉ là đầu vào của `Evaluate` nhưng thiếu nó thì caller duy nhất không chuyển
được; gõ chung một mốc.

**Ai gọi:** test của giai đoạn; trong dự án `ShuffleBooster` (GĐ3).

**Sơ đồ sau GĐ1**

```
caller ──lúc bắt đầu──► TurnsAt+ ──int──► lưu mỗi vật
       ──mỗi frame────► Evaluate+ ──► QuadEase.In / Out ──► Pose+
```

**Toán → code:** ①② nằm ở hai nhánh `if` trong `Evaluate` — `QuadEase.In`/`Out` là $e$, `squeeze01` là $s$, `turn01` là $\varphi$. ③ là
ba dòng cuối: `Quaternion.Euler(0, 0, …)` là $R_z$, `Mathf.Lerp(1, InnerRadius, squeeze01)` là $1 + (k-1)s$. ④ là `TurnsAt`:
`Mathf.Clamp01(radius / maxRadius)` là $\min(1, r/r_{max})$, nhánh `maxRadius > 0` là ca một vật duy nhất nằm ở tâm.

### `Runtime/Utilities/PhysXHelper/SwirlPath.cs` — file mới

```csharp
using System;
using Horcrux.Runtime.Tweening.Easing;
using UnityEngine;

namespace Horcrux.Runtime.Utilities.PhysXHelper
{
    /// <summary>Núm của đường xoáy: thời lượng hai pha, hình dạng ở đỉnh xoáy, số vòng, chiều quay.</summary>
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
        [Tooltip("Số vòng thêm của vật sát tâm so với vật xa nhất (một vòng): vật gần quay nhanh hơn, cả nhóm không xoay cứng như một tấm.")]
        [Range(0, 3)] public int ExtraTurnsAtCenter = 1;
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

        /// <summary>Số vòng nguyên của vật cách tâm <paramref name="radius"/>, khi vật xa nhất cách <paramref name="maxRadius"/>.</summary>
        public static int TurnsAt(float radius, float maxRadius, SwirlSpec spec) =>
            1 + (maxRadius > 0f ? Mathf.RoundToInt(spec.ExtraTurnsAtCenter * (1f - Mathf.Clamp01(radius / maxRadius))) : 0);

        /// <param name="t">Tiến độ cả cú xoáy, 0..1, tuyến tính theo thời gian; ease mỗi pha nằm ở đây.</param>
        /// <param name="turns">Số vòng nguyên vật quay quanh tâm (<see cref="TurnsAt"/>); vòng lẻ thì vật không về chỗ cũ.</param>
        public static Pose Evaluate(Vector3 center, Vector3 start, float t, int turns, SwirlSpec spec)
        {
            t = Mathf.Clamp01(t);
            var turnPoint = spec.TurnPoint;

            // squeeze01: 0 ở chỗ cũ, 1 ở đỉnh xoáy. turn01: phần vòng đã quay.
            float squeeze01, turn01;
            if (t < turnPoint)
            {
                var eased = QuadEase.In(t / turnPoint);
                squeeze01 = eased;
                turn01 = spec.InTurnPart * eased;
            }
            else
            {
                var eased = QuadEase.Out((t - turnPoint) / (1f - turnPoint));
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

| Consumes | Produces |
|---|---|
| `QuadEase.In`, `QuadEase.Out` (có sẵn) | `SwirlSpec`, `SwirlPath.Pose`, `SwirlPath.TurnsAt`, `SwirlPath.Evaluate` |

**Quyết định, phương án bỏ, vì sao:**
- `Pose` trả hệ số cỡ, không trả `Vector3` cỡ: caller nhân vào cỡ gốc của từng vật (vật khác cỡ, `lossyScale` khác nhau). Góc trả bằng
  độ quanh Z, caller gán `localRotation = Quaternion.Euler(0, 0, pose.LeanDegrees)`. *Bỏ:* trả `Matrix4x4` — caller nào cũng phải tách lại.
- `QuadEase.In/Out` thay biểu thức viết tay: cùng hình, không switch như `Easer.Evaluate`. `QuadEase.Out` viết `u(2−u)`, bản tạm viết
  `1−(1−u)²`: làm tròn float khác nhau, chênh ≤ 5.7e-6 trên vị trí, dưới dung sai 1e-3 của bảng case.
- `TurnsAt` trả `int`, caller lưu một số mỗi vật lúc bắt đầu, không tính lại mỗi frame (khoảng cách đầu không đổi). `round`, không
  `floor`: vật giữa nhóm nhận nửa số vòng thêm. *Bỏ:* tốc độ góc liên tục theo bán kính — vật không về chỗ cũ.

**Editor setup:** không.

**Kiểm** — compile sạch; agent viết `Tests/EditMode/SwirlPathTests.cs` (namespace `Horcrux.Tests`), `EPS = 1e-3`, tâm `(1, −2, 0)`,
bốn chỗ cũ `(4, −2)`, `(−3, 1.5)`, `(1, 3)`, `(1.2, −2.1)` (có vật rất gần tâm):

| # | Case | Kỳ vọng |
|---|---|---|
| 1 | `t ∈ {0, 1}`, mọi chỗ cũ, `Clockwise ∈ {true, false}`, `turns ∈ {1, 2, 3}` | vị trí lệch chỗ cũ < `EPS`, nghiêng 0, cỡ 1 |
| 2 | `t = TurnPoint`, `turns = 2` | khoảng cách tới tâm = `r · InnerRadius`, cỡ = `InnerScale`, \|nghiêng\| = `LeanDegrees` |
| 3 | chỗ cũ trùng tâm, `t` quét 0..1 bước 0.05 | luôn ở tâm |
| 4 | quét 2000 bước, `turns = 2`, mọi chỗ cũ | mỗi bước dời < `2πr / 2000 · 8 + EPS` (liền mạch qua `TurnPoint`) |
| 5 | `Clockwise`, chỗ cũ `(c.x + 2, c.y)`, `t = 0.3 · TurnPoint`, `turns = 1` | `y < c.y` |
| 6 | `ExtraTurnsAtCenter = 2`: `TurnsAt(5, 5)` · `(0, 5)` · `(2.5, 5)` · `(0, 0)` | 1 · 3 · 2 · 1 |
| 7 | `ExtraTurnsAtCenter = 0`: `TurnsAt(0, 5)` | 1 |

**Mốc commit:** `feat(swirl): SwirlPath — đường xoáy về chỗ cũ sau số vòng nguyên, vòng thêm cho vật gần tâm` trong `Assets/Horcrux`,
kèm con trỏ submodule ở dự án.

---

## Giai đoạn 2 — Tài liệu (agent)

**Mục tiêu:** người dùng mới dựng được một cú xoáy chỉ từ tài liệu. **Vì sao bây giờ:** API đã chốt ở GĐ1.

`docs/Runtime/SwirlPath/SwirlPath.md` và `SwirlPath.html` (cặp như các hệ khác trong `docs/Runtime/`): luồng một cú xoáy, bảng núm
`SwirlSpec`, §0 rút gọn, khuôn caller (một `Tween.Custom` cho cả nhóm, đổi hình khi qua `TurnPoint`, `finally` trả bản sao), bẫy (số vòng
lẻ thì vật không về chỗ cũ, gán cỡ tuyệt đối thay vì nhân cỡ gốc). Tài liệu trung tính: không tên type, kịch bản, con số của dự án nào.

**Kiểm:** mọi tên member trong tài liệu trỏ đúng code GĐ1; `.html` mở được, khớp `.md`.

**Mốc commit:** `docs(swirl): SwirlPath.md, .html` trong `Assets/Horcrux`, kèm con trỏ submodule.

## Giai đoạn 3 — category-jam dùng bản Horcrux, xoá bản tạm, xoá Plan (agent)

**Mục tiêu:** booster Fan gọi thẳng Horcrux, `FanSwirl` biến mất. **Vì sao bây giờ:** GĐ1 compile và test xanh.

| File | Đổi |
|---|---|
| `Scripts/Director/ShuffleBooster.cs` | thêm `using Horcrux.Runtime.Utilities.PhysXHelper;` · `FanSwirlTuning` → `SwirlSpec` · `FanSwirl.` → `SwirlPath.` |
| `Scripts/Director/FanSwirl.cs` (+ `.meta`) | xoá |
| `Tests/EditMode/FanSwirlTests.cs` (+ `.meta`) | xoá (đã dời sang Horcrux `SwirlPathTests` ở GĐ1) |
| `Scripts/Feel.md` §9 (tiêu đề và hàng `_swirl` của bảng núm) | `FanSwirlTuning`, `FanSwirl` → `SwirlSpec`, `SwirlPath` (Horcrux, `docs/Runtime/SwirlPath/SwirlPath.md`) |
| `Scripts/Feel.md` › Nghiệm thu, dòng "Máy kiểm" | `FanSwirlTests` → Horcrux `SwirlPathTests` |
| `Structure.md` | "đường xoáy FanSwirl" → "đường xoáy `SwirlPath` (Horcrux)" |
| `Horcrux/Plans/Horcrux_SwirlPath_Plan.md` (+ `.meta`) | xoá |

`_swirl` trên `GameplayRoot.prefab` giữ nguyên số: Unity lưu field của class `[Serializable]` theo tên field, không theo tên class, và
tên field trong `SwirlSpec` trùng bản tạm.

**Kiểm:** grep `FanSwirl` trong `Assets/_CategoryJam` ra 0 · lệnh kiểm GĐ3 ở đầu Plan xanh · YAML `_swirl` trong prefab không đổi.

**Chơi thử** (developer):

| Vào đâu | Làm gì | Nhìn gì | Khác trước | Dấu hiệu hỏng |
|---|---|---|---|---|
| level có booster Fan, kệ nhiều tile | bấm Fan | tile cuộn vào quanh tâm, đổi hình ở đỉnh xoáy, bung về đúng ô; tile gần tâm quay nhiều vòng hơn | không khác (chênh vị trí ≤ 5.7e-6) | tile đáp lệch ô, cả khối xoay cứng như một tấm, giật ở đỉnh xoáy, tile còn nghiêng hay sai cỡ khi đáp |

**Mốc commit:** `refactor(booster): cú xoáy Fan dùng Horcrux SwirlPath` ở dự án; xoá Plan trong `Assets/Horcrux`; kèm con trỏ submodule.

## File → GĐ

| File | GĐ |
|---|---|
| `Runtime/Utilities/PhysXHelper/SwirlPath.cs` | 1 |
| `Tests/EditMode/SwirlPathTests.cs` (agent) | 1 |
| `docs/Runtime/SwirlPath/SwirlPath.md`, `.html` | 2 |
| category-jam `ShuffleBooster.cs`, `FanSwirl.cs`, `FanSwirlTests.cs`, `Feel.md`, `Structure.md`; Horcrux: xoá Plan này | 3 |
