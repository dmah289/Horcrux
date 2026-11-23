# Plan — `SwirlPath`: một nhóm vật xoáy vào quanh tâm rồi bung về đúng chỗ cũ

Bản tạm: category-jam `Scripts/Director/FanSwirl.cs` › `FanSwirlTuning`, `FanSwirl` (caller `ShuffleBooster`, test `Tests/EditMode/FanSwirlTests.cs`). Không phụ thuộc Plan nào. Chung file: category-jam `Scripts/Feel.md` › Nghiệm thu, dòng "Máy kiểm" với `Horcrux_FeelUtilities_Plan.md` GĐ9, `Horcrux_HopCurve_DipFlash_Plan.md` › Chuyển dự án — mỗi Plan chỉ đổi tên test của mình, thứ tự nào cũng được.
Kiểm: `bash "Assets/Horcrux/Editor/CompileCheck/Tools~/verify.sh" --csproj com.horcrux.runtime.csproj --tests Horcrux.EditModeTests`; chuyển dự án `--csproj CategoryJam.Gameplay.csproj,com.horcrux.runtime.csproj --tests CategoryJam.EditModeTests,Horcrux.EditModeTests`.

## Kịch bản

| Tình huống | Hệ làm gì | Caller thấy |
|---|---|---|
| nhóm vật quanh một tâm, caller chạy một `Tween.Custom` 0→1 trong `spec.TotalDuration` | mỗi frame mỗi vật: `Evaluate` ra vị trí, góc nghiêng, hệ số cỡ | cả nhóm cuộn vào, co lại, nghiêng; rồi bung ra |
| vật gần tâm và vật xa | `TurnsAt` cho vật gần nhiều vòng nguyên hơn | nhóm không xoay cứng như một tấm |
| `t` qua `TurnPoint` | bán kính đổi chiều, tốc độ góc tụt | cú bật "hút rồi văng ra"; caller đổi hình vật ở đây |
| `t = 1` | đủ số vòng nguyên, bán kính về 1 | mọi vật đúng chỗ cũ, nghiêng 0, cỡ 1 |
| vật nằm đúng tâm | bán kính 0 nhân mọi góc | đứng yên ở tâm |
| chỉ một vật, ở tâm (`maxRadius` 0) | `TurnsAt` = 1 | không chia 0 |
| `t` ngoài 0..1 | kẹp | tư thế ở đầu mút |

## Flow

```
caller (chọn tâm, gom vật, chạy tween, đổi hình ở đỉnh xoáy, trả bản sao)       ShuffleBooster.ApplySwirl ở dự án
  ├─ lúc bắt đầu, mỗi vật: r = |start − tâm| ──► SwirlPath.TurnsAt(r, rMax, spec) ──int──► lưu
  └─ Tween.Custom(0→1, spec.TotalDuration), mỗi frame mỗi vật:
        SwirlPath.Evaluate(tâm, start, t, turns, spec)                                SwirlPath.cs
          ├─ t < TurnPoint: QuadEase.In — cuộn vào, co về InnerRadius                QuadEase.cs (có sẵn, internal)
          ├─ t ≥ TurnPoint: QuadEase.Out — bung ra, đủ vòng nguyên
          └─► Pose: Position · LeanDegrees (góc Z) · Scale (nhân cỡ gốc)
SwirlSpec ([Serializable] class, núm trên prefab) ──► TotalDuration, TurnPoint        SwirlPath.cs
```

## Thuật toán

Toạ độ cực quanh tâm: chỉ bán kính (co rồi nở) và góc (đủ vòng nguyên) đổi theo thời gian; đầu và cuối trùng vì bán kính về hệ số 1, góc về bội 360°.

| Ký hiệu | Nghĩa | Code |
|---|---|---|
| $c$, $p_0$, $\vec o = p_0 - c$ | tâm, chỗ cũ, độ lệch | `center`, `start`, `start - center` |
| $t \in [0,1]$ | tiến độ, tuyến tính theo thời gian | `t` |
| $\tau = \frac{T_{in}}{T_{in} + T_{out}}$ | mốc đỉnh xoáy | `TurnPoint` |
| $k$, $k_s$ | hệ số bán kính, cỡ ở đỉnh | `InnerRadius`, `InnerScale` |
| $q$ | phần vòng đã quay khi tới đỉnh | `InTurnPart` |
| $N$, $E$ | số vòng nguyên của vật, vòng thêm ở tâm | `turns`, `ExtraTurnsAtCenter` |
| $\sigma$ | $-1$ cùng chiều kim đồng hồ, $+1$ ngược | `sign` |
| $e$, $s$, $\varphi$ | ease, độ co, phần vòng | `eased`, `squeeze01`, `turn01` |

① Pha vào $u = t/\tau$, $e = u^2$ (InQuad); pha ra $u = \frac{t - \tau}{1 - \tau}$, $e = u(2-u) = 1 - (1-u)^2$ (OutQuad).

② $$\text{vào: } s = e,\ \varphi = q\,e \qquad\qquad \text{ra: } s = 1 - e,\ \varphi = q + (1-q)\,e$$

③ $$p = c + R_z\!\left(360^\circ\,\sigma N \varphi\right)\,\vec o\,\big(1 + (k-1)s\big), \quad \text{nghiêng} = \sigma\,L\,s, \quad \text{cỡ} = 1 + (k_s - 1)s$$

④ $$N = 1 + \operatorname{round}\!\left(E\,\big(1 - \min(1, r/r_{max})\big)\right), \qquad r_{max} = 0 \Rightarrow N = 1$$

| Mốc | Kỳ vọng | ✓ |
|---|---|---|
| $t = 0$ | $s = 0, \varphi = 0$: đúng chỗ cũ, nghiêng 0, cỡ 1 | case 1 |
| $t = 1$ | $s = 0, \varphi = 1$: góc $360^\circ N$ → đúng chỗ cũ, mọi $N$, cả hai chiều | case 1 |
| $t = \tau$ | hai pha cho cùng $s = 1$, $\varphi = q$: bán kính $k\,r$, cỡ $k_s$, nghiêng $L$ — vị trí liền, vận tốc gãy | case 2, 4 |
| $\vec o = 0$ | mọi $t$ ra tâm | case 3 |
| $\sigma = -1$, vật bên phải tâm, $t$ nhỏ | $y < c_y$ (đi xuống) | case 5 |
| $r = r_{max}$ · $r = 0$ · $r = r_{max}/2$, $E = 2$ | $N = 1$ · $3$ · $2$ | case 6 |

Bản mẫu thuần C# (quay bằng cos/sin) chạy cả bảng case: biểu thức viết tay của bản tạm 79/79 xanh, `QuadEase` 79/79 xanh, chênh vị trí ≤ 5.7e-6 trên 2000 bước × 4 vật · đối chứng số vòng lẻ (+0.5): 24 kiểm đỏ, đúng ở `t = 1`.

---

## GĐ1 — xong thì cả nhóm xoáy vào, bung về đúng chỗ, vật gần tâm lệch nhịp với vật xa

```
caller ──lúc bắt đầu──► TurnsAt+ ──int──► lưu mỗi vật
       ──mỗi frame────► Evaluate+ ──► QuadEase.In / Out ──► Pose+
```

`Runtime/Utilities/PhysXHelper/SwirlPath.cs` — file mới (assembly `com.horcrux.runtime`, không thêm reference):

```csharp
using System;
using Horcrux.Runtime.Tweening.Easing;
using UnityEngine;

namespace Horcrux.Runtime.Utilities.PhysXHelper
{
    /// <summary>Núm của đường xoáy: thời lượng hai pha, hình dạng ở đỉnh xoáy, số vòng, chiều quay.</summary>
    // Class, không struct: struct dựng bằng code có InDuration 0, chia 0. Tên field như bản tạm: prefab giữ số.
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
            // Hệ số, không Vector3: caller nhân vào cỡ gốc từng vật, các vật khác cỡ.
            public readonly float Scale;

            public Pose(Vector3 position, float leanDegrees, float scale)
            {
                Position = position;
                LeanDegrees = leanDegrees;
                Scale = scale;
            }
        }

        /// <summary>Số vòng nguyên của vật cách tâm <paramref name="radius"/>, khi vật xa nhất cách <paramref name="maxRadius"/>.</summary>
        // Round, không floor: vật giữa nhóm nhận nửa số vòng thêm. Caller lưu một lần, khoảng cách đầu không đổi.
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
                // QuadEase, không Easer.Evaluate: ease cố định, khỏi một switch mỗi frame mỗi vật.
                var eased = QuadEase.In(t / turnPoint);
                squeeze01 = eased;
                turn01 = spec.InTurnPart * eased;
            }
            else
            {
                // Vận tốc gãy tại turnPoint là cố ý: nhịp bị hút rồi văng ra.
                var eased = QuadEase.Out((t - turnPoint) / (1f - turnPoint));
                squeeze01 = 1f - eased;
                turn01 = Mathf.Lerp(spec.InTurnPart, 1f, eased);
            }

            var sign = spec.Clockwise ? -1f : 1f;
            var radius = Mathf.Lerp(1f, spec.InnerRadius, squeeze01);
            // Vật ở đúng tâm: start - center = 0 nhân mọi góc vẫn 0, không cần nhánh riêng.
            var offset = Quaternion.Euler(0f, 0f, sign * 360f * turns * turn01) * (start - center) * radius;

            return new Pose(
                center + offset,
                sign * spec.LeanDegrees * squeeze01,
                Mathf.Lerp(1f, spec.InnerScale, squeeze01));
        }
    }
}
```

`Tests/EditMode/SwirlPathTests.cs` (agent, namespace `Horcrux.Tests`, `EPS = 1e-3`, tâm `(1, −2, 0)`, bốn chỗ cũ `(4, −2)`, `(−3, 1.5)`, `(1, 3)`, `(1.2, −2.1)` — có vật rất gần tâm):

| # | Case | Kỳ vọng |
|---|---|---|
| 1 | `t ∈ {0, 1}`, mọi chỗ cũ, `Clockwise ∈ {true, false}`, `turns ∈ {1, 2, 3}` | vị trí lệch chỗ cũ < `EPS`, nghiêng 0, cỡ 1 |
| 2 | `t = TurnPoint`, `turns = 2` | khoảng cách tới tâm = `r · InnerRadius`, cỡ = `InnerScale`, \|nghiêng\| = `LeanDegrees` |
| 3 | chỗ cũ trùng tâm, `t` quét 0..1 bước 0.05 | luôn ở tâm |
| 4 | quét 2000 bước, `turns = 2`, mọi chỗ cũ | mỗi bước dời < `2πr / 2000 · 8 + EPS` (liền mạch qua `TurnPoint`) |
| 5 | `Clockwise`, chỗ cũ `(c.x + 2, c.y)`, `t = 0.3 · TurnPoint`, `turns = 1` | `y < c.y` |
| 6 | `ExtraTurnsAtCenter = 2`: `TurnsAt(5, 5)` · `(0, 5)` · `(2.5, 5)` · `(0, 0)` | 1 · 3 · 2 · 1 |
| 7 | `ExtraTurnsAtCenter = 0`: `TurnsAt(0, 5)` | 1 |

---

## Chuyển dự án (agent)

| Chỗ | Đổi |
|---|---|
| `docs/Runtime/SwirlPath/SwirlPath.md`, `.html` | tạo: luồng một cú xoáy · bảng núm `SwirlSpec` · Thuật toán rút gọn · khuôn caller (một `Tween.Custom` cho cả nhóm, đổi hình khi qua `TurnPoint`, gán `localRotation = Quaternion.Euler(0, 0, pose.LeanDegrees)`, cỡ gốc × `pose.Scale`, `finally` trả bản sao) · bẫy: số vòng lẻ không về chỗ cũ, gán cỡ tuyệt đối thay vì nhân cỡ gốc; trung tính, không tên dự án |
| `Scripts/Director/ShuffleBooster.cs` | thêm `using Horcrux.Runtime.Utilities.PhysXHelper;` · `FanSwirlTuning` → `SwirlSpec` · `FanSwirl.` → `SwirlPath.`; `_swirl` trên `GameplayRoot.prefab` giữ số (field `[Serializable]` lưu theo tên field) — YAML `_swirl` không đổi |
| `Scripts/Director/FanSwirl.cs`, `Tests/EditMode/FanSwirlTests.cs` (+ `.meta`) | xoá; grep `FanSwirl` trong `Assets/_CategoryJam` = 0 |
| `Scripts/Feel.md` §9 (tiêu đề, hàng `_swirl` bảng núm) | `FanSwirlTuning`, `FanSwirl` → `SwirlSpec`, `SwirlPath` (Horcrux, `docs/Runtime/SwirlPath/SwirlPath.md`) |
| `Scripts/Feel.md` › Nghiệm thu, dòng "Máy kiểm" | `FanSwirlTests` → Horcrux `SwirlPathTests` |
| `Structure.md` | "đường xoáy FanSwirl" → "đường xoáy `SwirlPath` (Horcrux)" |
| Plan này | xoá cùng `.meta` |
| chơi thử | level có Fan, kệ nhiều tile → bấm Fan → tile cuộn vào, đổi hình ở đỉnh, bung về đúng ô, tile gần tâm quay nhiều vòng hơn, không khác trước; hỏng: đáp lệch ô, xoay cứng một tấm, giật ở đỉnh, còn nghiêng hay sai cỡ khi đáp |

## Không làm · Mở rộng sau

| | Phương án | Vì sao |
|---|---|---|
| bỏ | tween, chạy theo thời gian trong utility | một tween cho n vật rẻ hơn n tween; caller cần chèn việc giữa chừng (đổi hình ở `TurnPoint`) |
| bỏ | tốc độ góc liên tục theo bán kính | chỉ vòng nguyên đưa vật về chỗ cũ |
| bỏ | trả `Matrix4x4` | caller nào cũng phải tách lại vị trí, góc, cỡ |
| bỏ | struct camelCase không mặc định như `ArcFlightSpec`, `SquashStretchSpec` | cần giá trị mặc định và giữ tên field bản tạm |
| bỏ | `CharmFlyArc`, `BezierCurveHelper` | đường một chiều tới đích, không quay quanh tâm |
| sau | ease chọn được mỗi pha | InQuad vào, OutQuad ra là cử chỉ "hút rồi bật"; khi có caller cần cử chỉ khác |
| sau | độ trễ so le theo khoảng cách | khi có demo cần |
| sau | mặt phẳng khác XY | hiện quay quanh Z như `BezierCurveHelper`; khi có caller 3D |
