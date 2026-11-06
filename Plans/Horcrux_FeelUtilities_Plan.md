# Plan — `TadaCurve`, `AnimationRegistry`, `ProgressBar.Play` và hai sửa extension Horcrux theo kinh nghiệm dự án

Ngày 2026-11-06. Nguồn: category-jam, audit hiệu năng đợt feeling 2026-10-31 (kết luận ở `Assets/_CategoryJam/Scripts/Feel.md`);
developer chốt cùng ngày đưa **toàn bộ** lên Horcrux, kể cả thứ mới có một người dùng:

- **A** `TadaCurve` ← `Assets/_CategoryJam/Scripts/Entities/Tile/TileFinaleCurve.cs` (`TileFinaleCurve`, `FinalePose`),
  `Entities/Tile/TileFeelTuning.cs` (`TileFinaleTuning`) — nhịp tada của tile cuối.
- **B** `AnimationRegistry` ← `Assets/_CategoryJam/Scripts/Director/AnimatingSystem.cs`, `Director/IAnimate.cs` — sổ "có gì đang diễn".
- **C, D** sửa Horcrux `Runtime/Utilities/UI/ProgressBar.cs`,
  `Runtime/Utilities/ExtensionMethods/Transform/TransformExtensions.CharmTween.cs`,
  `Runtime/Utilities/ExtensionMethods/UniTextExtensionMethods.cs`, `Runtime/Utilities/ExtensionMethods/SpriteRenderExtensions.cs` —
  dự án làm tốt hơn: `ComboPill` cần handle dừng thay token, `TileView` gọi `SetAlpha` mỗi frame bay, tween một `float` không cần
  closure.

**Phân công:** GĐ1–GĐ7 developer gõ, agent viết test theo bảng case rồi compile · GĐ8 (tài liệu), GĐ9 (category-jam chuyển sang
bản Horcrux, xoá bản tạm) agent làm. **Chạy được, thấy được** ở GĐ1–GĐ7 = compile sạch + bảng case xanh (`Tests/EditMode/`,
`Tests/PlayMode/`); thấy trong game ở GĐ9. GĐ1–GĐ8 commit trong `Assets/Horcrux`, GĐ9 commit ở dự án kèm con trỏ submodule. Không
phụ thuộc Plan nào: trước hay sau `Horcrux_HopCurve_DipFlash_Plan.md` đều compile sạch. Lõi toán chỉ ở `TadaCurve` (`§0`).

## Ngữ cảnh đã chốt

- **Mục tiêu:** cảm giác chơi **không đổi một giá trị nào** — công thức tada chép nguyên văn; ba sửa Horcrux giữ chữ ký public đang
  có, bỏ cấp phát hoặc bỏ guard.
- **Người dùng** (đủ tên ở "Ai gọi" mỗi giai đoạn): tada — `Tile` · registry — `AnimatingSystem` và 7 class implement `IAnimate` ·
  `Play` — `ComboPill` · `SetAlpha(SpriteRenderer)` — `TileView` mỗi frame bay và bốn chỗ khác · `CharmSquashStretch` chưa có caller
  (grep dự án và Horcrux: 0).
- **Chỗ đặt:** `Runtime/Utilities/Tweening/Tada/` (`TadaSpec.cs`, `TadaPose.cs`, `TadaCurve.cs`), namespace
  `Horcrux.Runtime.Utilities.Tweening` như `SquashStretchSpec` · `IAnimating.cs`, `AnimationRegistry.cs` ở `Runtime/Utilities/Common/`,
  namespace `Horcrux.Runtime.Utilities.Common` · `Play` thêm vào `ProgressBar.cs` · tài liệu `docs/Runtime/Tada/`,
  `docs/Runtime/AnimationRegistry/`.
- **Tên** (developer chốt 2026-10-31): `TadaCurve`, `TadaSpec`, `TadaPose`, `AnimationRegistry`, `IAnimating`. Field của `TadaSpec`
  **giữ nguyên** tên field của `TileFinaleTuning`: key YAML khối `finale` trên `Tile.prefab`, `GoldenTile.prefab` không đổi.
- **Không làm:** driver tween cho tada (thân, bóng, scale là kênh của dự án; Horcrux giữ hình dạng) · mẫu lắc là núm (GĐ1) ·
  `ParticleSystem.ExpireAlive` (không caller: `TileView.StopFlightTrail` để vệt tan bằng `Stop(StopEmitting)`; khi cần thì đặt cạnh
  `StopAndAwaitCompletion` trong `ParticleSystemExtensions.cs`) · `CharmFlyArc`,
  `CharmCount` 0 B (GĐ6) · `AnimationRegistry` là `MonoBehaviour` hay service (GĐ3) · đổi hay bỏ `PlayAsync` (7 case giữ; `PlayAsync`
  gọi `Play` rồi gắn token) · cache chuỗi số (dự án dùng `SetNumber` của Horcrux; nhãn hai số `a/b` giữ cache có trần, GĐ9).
- **Mở rộng sau:** `CharmFlyArc`, `CharmCount` 0 B khi có caller mỗi frame (GĐ6) · `AnimationRegistry` đếm thay quét khi có số đo (GĐ3).
- **Đã khảo sát:**
  - A: `TileFinaleCurve.cs`, `TileFinaleTuning` bản 2026-11-06 (công thức, `BEAT_COUNT`, năm mốc lắc, hai ease inline, 9 field và
    mặc định; tooltip về `Tile`, GĐ9) · `Tile.cs`, `Tile.Editor.cs` (thứ tự gọi `Duration`, `Evaluate`, `Shadow01`, `EndLift`) ·
    `TileFinaleCurveTests.cs` (19 case, chuyển nguyên văn) · Horcrux `DampedOscillator`, `HarmonicOscillator` (không hợp, `§0`),
    `Easer.Evaluate` (không hợp, GĐ1).
  - B: `AnimatingSystem.cs`, `IAnimate.cs` và 7 class (`Register` chống trùng, quét bỏ `null`, `Clear` ở `OnDisable` và
    `HandleLevelDisposed`; `BaseManager` là domain dự án) · Horcrux `Utilities/Common/` (chỗ đặt; `DeferredSet` không cần, GĐ3).
  - C: `Hud/ComboPill.cs` (giữ CTS quanh `PlayAsync` — caller của `Play`), `Director/ComboTracker.cs` (đã dùng `Tween.Delay`, không
    chạm) · `ProgressBar.cs` bản 2026-11-05 (`PlayAsync`, `TryWriteGoalAndLabel`, `WriteFill`; phần khác không chạm) ·
    `Tests/PlayMode/ProgressBarTests.cs` (7 case `PlayAsync` phải PASS sau GĐ5).
  - D: `TransformExtensions.CharmTween.cs`, `UniTextExtensionMethods.cs` (biến mỗi closure bắt) · `PhysXHelper/SquashStretch.cs`
    (`GetSquashStretch` = `GetVolumePreservingScale(lerp(a, b, t), …)`, không kẹp) · `TransformExtensions.Jump.cs` › `SetJumpY`
    (khuôn callback không bắt biến, GĐ6) · `CharmJumpTests.CharmJump_DoesNotAllocate`, `AllocationMeter` (khung đo 0 B) ·
    `SpriteRenderExtensions.cs` (`SetAlpha(Graphic)` là bản khác, không chạm) · `docs/Runtime/CharmFlyArc/CharmFlyArc.md` §2 (GĐ8).
  - Chung: PrimeTween `Tween.Custom<T>(target, …)`, `Tween.Delay<T>`, `SetCancellationToken` trả `Tween`, `isAlive`, `Stop()` no-op
    trên `default` · `UniTextExtensionMethods.SetNumber`, `UniTextSetNumberTests` (`SingleShelfAmountBar`, GĐ9) · category-jam
    `docs/tools/feel_trace/trace.cs` (mốc trace trước–sau GĐ9).

---

## §0 — Lõi toán nhịp tada (`TadaCurve`)

Ba pha nối tiếp trên trục thời gian `t` (giây), mỗi pha một hàm ease có sẵn. Năm mốc góc và độ phồng **chọn bằng mắt**, không suy từ
vật lý: số này cho cảm giác "nhựa cứng reo lên rồi lấy đà".

| Pha | Vai trò | Kênh ghi |
|---|---|---|
| Nhấc | tách thân khỏi chỗ cũ, báo "tile này khác các tile trước" | `Lift` 0 → L, `Scale` s₀ → S_l |
| Lắc 5 nhịp | tada: biên độ giảm dần, phồng ở giữa mỗi nhịp lúc góc đổi nhanh nhất | `Angle`, `Scale` quanh S_l |
| Lấy đà | hạ nhẹ, co nhẹ để chuyến bay bật lên | `Lift` L → L − D, `Scale` S_l → S_w |

Ký hiệu (field của `TadaSpec`): L = `LiftHeight` · T_l = `LiftDuration` · S_l = `LiftScale` · A = `WiggleDegrees` · T_b =
`WiggleBeatDuration` · B = `BeatScaleBump` · S_w = `WindupScale` · D = `WindupDip` · T_w = `WindupDuration` · s₀ = `fromScale` (cỡ lúc
bắt đầu so với cỡ ở chỗ cũ) · K = (0, 1, −1, 0.66, −0.33, 0) · OutQuad e_q(x) = x(2 − x) · InOutSine e_s(x) = ½(1 − cos πx).

Pha nhấc, 0 ≤ t < T_l, u = e_q(clamp01(t / T_l)):

$$ \text{Lift} = L \cdot u, \qquad \text{Angle} = 0, \qquad \text{Scale} = s_0 + (S_l - s_0) \cdot u $$

Pha lắc, t′ = t − T_l, 0 ≤ t′ < 5 T_b, i = min(⌊t′ / T_b⌋, 4), u = clamp01((t′ − i T_b) / T_b):

$$ \text{Lift} = L, \qquad \text{Angle} = A \cdot \big[K_i + (K_{i+1} - K_i) \cdot e_s(u)\big], \qquad \text{Scale} = S_l + B \cdot \sin(\pi u) $$

Pha lấy đà, t″ = t − T_l − 5 T_b, w = e_s(clamp01(t″ / T_w)):

$$ \text{Lift} = L - D \cdot w, \qquad \text{Angle} = 0, \qquad \text{Scale} = S_l + (S_w - S_l) \cdot w $$

Hai hàm phụ: bóng co theo độ nhấc `Shadow01 = clamp01(Lift / L)` (L = 0 → 0, không chia 0); độ cao lúc cất cánh `EndLift = L − D`.

Liền mạch ở mọi chỗ nối — mốc → kỳ vọng (test GĐ1, GĐ2 khoá từng dòng):

| Mốc | Lift | Angle | Scale | Vì |
|---|---|---|---|---|
| t = 0 | 0 | 0 | s₀ | e_q(0) = 0 |
| t = T_l (cuối nhấc = đầu lắc) | L | A·K₀ = 0 | S_l | e_q(1) = 1; sin 0 = 0 |
| ranh nhịp i → i+1 | L | A·K_{i+1} ở cả hai phía | S_l | e_s(1) = 1, e_s(0) = 0; sin π = 0 |
| t = T_l + 5 T_b (đầu lấy đà) | L | A·K₅ = 0 | S_l | w = 0 |
| t ≥ `Duration` = T_l + 5 T_b + T_w | L − D | 0 | S_w | clamp01 giữ w = 1 |

Lệch so với vật lý: InOutSine cho vận tốc góc 0 ở mỗi ranh nhịp — lắc đổi chiều mượt nhưng không có quán tính thật của một vật đàn
hồi; dao động tắt dần (`DampedOscillator`) là mô hình khác và đổi cảm giác, nên không dùng. `LerpUnclamped` ở mọi pha: `u`, `w` đã
kẹp [0, 1] nên không vọt, giữ `LerpUnclamped` để không kẹp hai lần.

---

## Cơ chế A — `TadaCurve`: hình dạng nhịp tada

### Luồng mô tả

```
host dựng tween 0 → Duration(spec), Linear                                     GĐ1
            ▼
mỗi frame: time ──► Evaluate(time, fromScale, spec) ──► TadaPose (Lift, Angle, Scale)
            │       host ghi: gốc đổi cỡ, thân nhấc và xoay quanh tâm            GĐ1
            │       bóng nằm lại, co theo Shadow01(pose.Lift, spec)              GĐ2
            ▼
hết Duration: chuyến bay nối từ EndLift(spec), bóng từ Shadow01(EndLift)        GĐ2
            ▼
tài liệu (GĐ8) · dự án: Tile.StepFinale, BuildFlightState dùng bản Horcrux (GĐ9)
```

### Bản đồ hệ cuối

```
  host (dự án)                                  Runtime/Utilities/Tweening/Tada/
  ────────────                                  ────────────────────────────────
  [SerializeField] TadaSpec spec  ──in──►  TadaCurve (static, không state)
  Tween.Custom(0 → Duration(spec))             Duration · Evaluate → TadaPose · Shadow01 · EndLift
  StepFinale(time): pose = Evaluate(…)         WiggleKeys (6 mốc), OutQuad, InOutSine (private)
  ghi thân, gốc, bóng của mình
```

---

## Giai đoạn 1 — Núm, tư thế, ba pha theo thời gian

**Mục tiêu:** từ một `TadaSpec` và `time`, trả `TadaPose` ba kênh; `Duration` cho host dựng tween. Xong giai đoạn này một host ghi
được cả nhịp tada lên thân và gốc của nó.

**Vì sao bây giờ:** đây là lõi của cơ chế; bóng và chuyến bay (GĐ2) chỉ đọc lại `Lift` mà pha này sinh ra.

**Ai gọi:** test; trong dự án `Tile.BuildFlightMotion` (tween `0 → finaleDuration` gọi `StepFinale(time)`), `Tile.StepFinale` (GĐ9).

### `Runtime/Utilities/Tweening/Tada/TadaSpec.cs` — file mới

```csharp
using System;
using UnityEngine;

namespace Horcrux.Runtime.Utilities.Tweening
{
    /// <summary>Núm nhịp tada: nhấc khỏi chỗ cũ, lắc 5 nhịp giảm dần kèm phồng, lấy đà rồi để caller cho bay. Hình ở TadaCurve.</summary>
    [Serializable]
    public struct TadaSpec
    {
        [Tooltip("Thân nhấc khỏi chỗ cũ bao nhiêu unit world; bóng nằm lại ở chỗ cũ.")]
        [Min(0f)] public float LiftHeight;
        [Tooltip("Số giây nhấc (OutQuad).")]
        [Min(0.01f)] public float LiftDuration;
        [Tooltip("Cỡ khi đã nhấc, so với cỡ ở chỗ cũ.")]
        [Range(1f, 1.4f)] public float LiftScale;
        [Tooltip("Biên độ nhịp lắc đầu (độ) quanh tâm; mẫu 1, −1, 0.66, −0.33, 0.")]
        [Range(0f, 12f)] public float WiggleDegrees;
        [Tooltip("Số giây mỗi nhịp lắc (InOutSine); 5 nhịp.")]
        [Min(0.01f)] public float WiggleBeatDuration;
        [Tooltip("Phồng thêm so với LiftScale ở giữa mỗi nhịp lắc, lúc thân lắc nhanh nhất.")]
        [Range(0f, 0.2f)] public float BeatScaleBump;
        [Tooltip("Cỡ lúc cất cánh sau khi lấy đà, so với cỡ ở chỗ cũ.")]
        [Range(1f, 1.4f)] public float WindupScale;
        [Tooltip("Thân hạ bao nhiêu unit world khi lấy đà. Không vượt LiftHeight, nếu không thân chìm dưới chỗ cũ lúc cất cánh.")]
        [Min(0f)] public float WindupDip;
        [Tooltip("Số giây lấy đà (InOutSine).")]
        [Min(0.01f)] public float WindupDuration;

        public TadaSpec(float liftHeight, float liftDuration, float liftScale, float wiggleDegrees, float wiggleBeatDuration,
            float beatScaleBump, float windupScale, float windupDip, float windupDuration)
        {
            LiftHeight = liftHeight;
            LiftDuration = liftDuration;
            LiftScale = liftScale;
            WiggleDegrees = wiggleDegrees;
            WiggleBeatDuration = wiggleBeatDuration;
            BeatScaleBump = beatScaleBump;
            WindupScale = windupScale;
            WindupDip = windupDip;
            WindupDuration = windupDuration;
        }
    }
}
```

### `Runtime/Utilities/Tweening/Tada/TadaPose.cs` — file mới

```csharp
namespace Horcrux.Runtime.Utilities.Tweening
{
    /// <summary>Tư thế một mốc của nhịp tada: độ nhấc của thân (world, trên chỗ cũ), góc thân (độ), cỡ gốc so với cỡ ở chỗ cũ.</summary>
    public readonly struct TadaPose
    {
        public readonly float Lift;
        public readonly float Angle;
        public readonly float Scale;

        public TadaPose(float lift, float angle, float scale)
        {
            Lift = lift;
            Angle = angle;
            Scale = scale;
        }
    }
}
```

### `Runtime/Utilities/Tweening/Tada/TadaCurve.cs` — file mới

```csharp
using UnityEngine;

namespace Horcrux.Runtime.Utilities.Tweening
{
    /// <summary>Hình dạng nhịp tada theo thời gian: nhấc (OutQuad), 5 nhịp lắc giảm dần kèm phồng, lấy đà (InOutSine). Caller giữ vòng Step
    /// và ghi lên thân, bóng, gốc của mình.</summary>
    public static class TadaCurve
    {
        public const int BEAT_COUNT = 5;

        /// <summary>Góc ở ranh mỗi nhịp, theo WiggleDegrees; phần tử cuối là mốc về 0 của nhịp thứ 5.</summary>
        private static readonly float[] WiggleKeys = { 0f, 1f, -1f, 0.66f, -0.33f, 0f };

        public static float Duration(in TadaSpec spec)
            => spec.LiftDuration + BEAT_COUNT * spec.WiggleBeatDuration + spec.WindupDuration;

        /// <summary>fromScale là cỡ lúc bắt đầu so với cỡ ở chỗ cũ. Quá Duration thì giữ tư thế cuối.</summary>
        public static TadaPose Evaluate(float time, float fromScale, in TadaSpec spec)
        {
            var lift = spec.LiftHeight;
            if (time < spec.LiftDuration)
            {
                var u = OutQuad(Mathf.Clamp01(time / spec.LiftDuration));
                return new TadaPose(lift * u, 0f, Mathf.LerpUnclamped(fromScale, spec.LiftScale, u));
            }

            time -= spec.LiftDuration;
            var beat = spec.WiggleBeatDuration;
            if (time < BEAT_COUNT * beat)
            {
                var index = Mathf.Min((int)(time / beat), BEAT_COUNT - 1);
                var u = Mathf.Clamp01((time - index * beat) / beat);
                var angle = Mathf.LerpUnclamped(WiggleKeys[index], WiggleKeys[index + 1], InOutSine(u)) * spec.WiggleDegrees;
                // Phồng đỉnh ở giữa nhịp, lúc góc đổi nhanh nhất; 0 ở ranh nhịp nên cỡ liền mạch.
                var scale = spec.LiftScale + spec.BeatScaleBump * Mathf.Sin(Mathf.PI * u);
                return new TadaPose(lift, angle, scale);
            }

            time -= BEAT_COUNT * beat;
            var w = InOutSine(Mathf.Clamp01(time / spec.WindupDuration));
            return new TadaPose(lift - spec.WindupDip * w, 0f, Mathf.LerpUnclamped(spec.LiftScale, spec.WindupScale, w));
        }

        private static float OutQuad(float x) => x * (2f - x);

        private static float InOutSine(float x) => 0.5f * (1f - Mathf.Cos(Mathf.PI * x));
    }
}
```

**Khác với bản dự án**

| Chỗ | Bản dự án | Bản Horcrux | Vì |
|---|---|---|---|
| Núm | `sealed class TileFinaleTuning` với field initializer | `struct TadaSpec` với constructor 9 tham số | cụm núm chuyển động là struct `[Serializable]` như `DipSpec`, `SquashStretchSpec`; dự án truyền mặc định ở field `finale` (GĐ9) |
| Mốc lắc | `WiggleKey(int index)` là `switch` expression, `_ => 0f` | `WiggleKeys` mảng 6 phần tử, chỉ số `index + 1` | cùng giá trị ở mọi `index` 0–5; mảng đọc được một dòng, không nhánh |
| Tham số | `TileFinaleTuning tuning` (tham chiếu class) | `in TadaSpec spec` | struct 9 `float` truyền `in` không sao chép; call site không đổi (`TadaCurve.Evaluate(time, x, finale)`) |
| Tooltip | gọi tên `HeldScale`, `PeakScale`, "sprite 2.5D" | chỉ nghĩa của núm | luật ghép với press và flight là của tile, về `[Tooltip]` của field `finale` trong `Tile` (GĐ9) |

**Quyết định** — `Evaluate` là hàm của class toán nhận `in TadaSpec`, không là method của struct: struct núm chỉ giữ dữ liệu như
`DipSpec`; hình tada là của class toán, cùng khuôn `HopCurve`. Phương án bỏ: `spec.Evaluate(time)` — trộn núm serialize với hình dạng, hai
lý do thay đổi một struct. — `WiggleKeys` 6 phần tử cho 5 nhịp: mốc `index + 1` ở nhịp cuối là 0 (về thẳng); chép nguyên `WiggleKey(5) => 0`
của bản tạm. — Mẫu lắc `1, −1, 0.66, −0.33, 0` không là núm: năm mốc là một hình; người dùng thứ hai cần mẫu khác thì thêm overload
nhận mảng, không sửa bản này. Giá developer đã chấp nhận: `TadaCurve` mới có một người dùng, hình (nhấc → 5 nhịp lắc giảm dần kèm
phồng → lấy đà) đóng băng theo tile nhựa; dự án sau chỉnh được số, không chỉnh được mẫu. — `OutQuad`, `InOutSine` inline một dòng,
không gọi `Easer.Evaluate(EaseType, t)`: thêm một switch mỗi frame cho cùng số.

| Consumes | Produces |
|---|---|
| `UnityEngine.Mathf` (`Clamp01`, `LerpUnclamped`, `Min`, `Sin`, `Cos`) | `TadaSpec`, `TadaPose`, `TadaCurve.BEAT_COUNT`, `Duration`, `Evaluate` |

**Sơ đồ tương tác sau GĐ1** (`+` là phần mới)

```
  host ──Tween.Custom(0 → Duration+(spec))──► mỗi frame: Evaluate+(time, s₀, spec) ──► TadaPose+ ──► host ghi gốc, thân
```

**Editor setup:** tạo ba file trong Project window của Unity (để có `.meta`) trong thư mục mới `Runtime/Utilities/Tweening/Tada/`.

**Kiểm:** compile sạch. `Tests/EditMode/TadaCurveTests.cs`: `Default()` trả `new TadaSpec(0.35f, 0.15f, 1.2f, 6f, 0.08f, 0.04f, 1.14f,
0.05f, 0.07f)` (mặc định C# của `TileFinaleTuning`); dung sai `1e-4f`, góc `1e-3f`; 14 case đầu chuyển nguyên văn từ `TileFinaleCurveTests`.

| Case | Input | Kỳ vọng |
|---|---|---|
| `Duration` | `Default()` | 0.15 + 5·0.08 + 0.07 |
| xuất phát | `Evaluate(0, 1.15, Default)` | Lift 0, Angle 0, Scale 1.15 |
| cuối nhấc | `Evaluate(LiftDuration, 1.15, Default)` | Lift 0.35, Angle 0, Scale 1.2 |
| ranh nhịp | `LiftDuration + beat·WiggleBeatDuration`, beat 1..5 | Angle 6, −6, 3.96, −1.98, 0; Lift 0.35; Scale 1.2 khi beat < 5 |
| giữa nhịp | `LiftDuration + (beat + 0.5)·WiggleBeatDuration`, beat 0, 1 | Scale 1.24; Angle 3, 0 |
| cuối lấy đà | `Evaluate(Duration, …)` | Lift 0.30, Angle 0, Scale 1.14 |
| quá `Duration` | `Duration + 1` | bằng tư thế cuối |
| liền mạch | mỗi 1 ms trên `Duration + 0.01` | \|ΔLift\| < 0.01, \|ΔAngle\| < 0.5, \|ΔScale\| < 0.01 giữa hai mẫu kề |
| biên độ | mỗi 2 ms trên `Duration` | \|Angle\| ≤ WiggleDegrees |
| time âm (ngoài dải dưới) | `Evaluate(−0.1, 1.15, Default)` | bằng xuất phát (Clamp01 về 0) |
| `WiggleDegrees` 0 | spec với 0 | Angle 0 ở mọi `time` |
| `fromScale` 1 (rời ô, không qua cú đè) | `Evaluate(0, 1, Default)` | Scale 1 |
| `BEAT_COUNT` | hằng | 5 |

**Mốc commit:** `feat(tada): TadaSpec, TadaPose, TadaCurve.Evaluate và Duration`.

---

## Giai đoạn 2 — Bóng nằm lại và cửa nối sang chuyến bay

**Mục tiêu:** `Shadow01` cho bóng co theo độ nhấc ở mỗi frame tada; `EndLift` cho chuyến bay bắt đầu từ độ cao tada để lại. Xong giai
đoạn này host nối liền tada với chuyến bay không nhảy khung.

**Vì sao bây giờ:** cả hai chỉ đọc `Lift` của GĐ1; bóng và cú cất cánh là hai kênh còn lại của một host thật.

**Ai gọi:** test; trong dự án `Tile.StepFinale` (`Shadow01(pose.Lift, finale)` mỗi frame), `Tile.FlyToAsync`
(`startLift = EndLift(finale)`), `Tile.BuildFlightState` (`StartShadow01 = Shadow01(startLift, finale)`) (GĐ9).

### `TadaCurve.cs` › thêm sau `Duration`, trước `Evaluate`

```csharp
        public static float Duration(in TadaSpec spec)
            => spec.LiftDuration + BEAT_COUNT * spec.WiggleBeatDuration + spec.WindupDuration;

        /// <summary>Độ cao thân lúc cất cánh: chuyến bay bắt đầu từ đây.</summary>
        public static float EndLift(in TadaSpec spec) => spec.LiftHeight - spec.WindupDip;

        /// <summary>Độ co bóng theo độ nhấc: nhấc hết LiftHeight thì bóng co như ở đỉnh chuyến bay.</summary>
        public static float Shadow01(float lift, in TadaSpec spec)
            => spec.LiftHeight > 0f ? Mathf.Clamp01(lift / spec.LiftHeight) : 0f;
```

**Khác với bản dự án:** không — hai hàm chép nguyên văn, đổi kiểu tham số.

**Quyết định** — `Shadow01` guard `LiftHeight > 0f`: `[Min(0f)]` cho phép 0 là giá trị hợp lệ (tada không nhấc, chỉ lắc), chia 0 cho
`NaN` lan vào scale bóng; đây là ca hợp lệ, không phải lỗi setup. — `EndLift` không kẹp âm: `WindupDip > LiftHeight` là lỗi núm, tooltip
của `WindupDip` nói rõ, Inspector là nơi người tune đang nhìn.

| Consumes | Produces |
|---|---|
| GĐ1 (`TadaSpec`) | `TadaCurve.EndLift`, `Shadow01` |

**Sơ đồ tương tác sau GĐ2**

```
  host ──mỗi frame tada──► Evaluate → pose ──Shadow01+(pose.Lift)──► bóng co
       ──hết Duration──► EndLift+ → độ cao xuất phát chuyến bay; Shadow01+(EndLift) → bóng lúc cất cánh
```

**Editor setup:** không.

**Kiểm:** compile sạch; thêm vào `TadaCurveTests.cs` (5 case từ `TileFinaleCurveTests` và hai biên).

| Case | Input | Kỳ vọng |
|---|---|---|
| `EndLift` | `Default()` | 0.30 |
| `Shadow01` theo lift | lift 0, 0.35, 0.30, 0.5 | 0, 1, 0.30/0.35, 1 (kẹp) |
| `Shadow01` lift âm (ngoài dải) | −0.1 | 0 |
| `LiftHeight` 0 | spec với 0; lift 0 và 0.2 | 0 — không `NaN` |
| nối liền | `Shadow01(Evaluate(Duration).Lift)` so với `Shadow01(EndLift)` | bằng nhau |

**Mốc commit:** `feat(tada): EndLift, Shadow01`.

---

## Cơ chế B — `AnimationRegistry`: sổ những thứ đang diễn

### Luồng mô tả

```
chủ (manager của một level) new AnimationRegistry; vật vào sân ──► Register(vật : IAnimating)     GĐ3
            ▼
host muốn mở tương tác, mở popup: hỏi IsAnyAnimating ──► quét, true khi còn một mục đang diễn      GĐ3
            ▼
vật rời sân (box xong, tile xong) ──► Unregister · đổi level ──► Clear                              GĐ4
            ▼
tài liệu (GĐ8) · dự án: AnimatingSystem giữ registry, 7 class đổi sang IAnimating (GĐ9)
```

### Bản đồ hệ cuối

```
  vật : IAnimating { IsAnimating }  ◄──quét──  AnimationRegistry (class thuần, List<IAnimating>)  ◄──Register / Unregister / Clear──  chủ
                                                   │ IsAnyAnimating, Count
                                                   ▼
                                                 host: WaitUntil(!IsAnyAnimating) rồi mở tương tác
```

---

## Giai đoạn 3 — Đăng ký và hỏi "còn gì diễn không"

**Mục tiêu:** `Register` một lần mỗi vật, `IsAnyAnimating` quét tuyến tính không cấp phát, `Count` để test và chủ kiểm. Xong giai đoạn này
một host chờ được mọi thứ đã đăng ký hết diễn.

**Vì sao bây giờ:** câu hỏi "còn gì diễn không" là lý do tồn tại của sổ; gỡ và xoá (GĐ4) chỉ có nghĩa khi đã có thứ trong sổ.

**Ai gọi:** test của giai đoạn; trong dự án `AnimatingSystem.Register` (từ `GameDirector.OnTileActivated`, `BoxesContainer`,
`CacheTileSlots`) và `AnimatingSystem.IsAnimating()` (GĐ9).

### `Runtime/Utilities/Common/IAnimating.cs` — file mới

```csharp
namespace Horcrux.Runtime.Utilities.Common
{
    /// <summary>Thứ có lúc đang diễn: host chờ mọi thứ đã đăng ký hết diễn rồi mới mở tương tác.</summary>
    public interface IAnimating
    {
        bool IsAnimating { get; }
    }
}
```

### `Runtime/Utilities/Common/AnimationRegistry.cs` — file mới

```csharp
using System.Collections.Generic;

namespace Horcrux.Runtime.Utilities.Common
{
    /// <summary>Sổ những thứ đang diễn của một chủ (một level, một màn): đăng ký một lần, hỏi "còn gì diễn không" mỗi lần cần chờ.
    /// Chủ Clear khi đổi level; chủ gỡ từng mục khi mục đó rời sân.</summary>
    public sealed class AnimationRegistry
    {
        private readonly List<IAnimating> _items = new();

        public int Count => _items.Count;

        /// <summary>Quét tuyến tính, không cấp phát; mục null (đã bị chủ bỏ mà chưa gỡ) bỏ qua.</summary>
        public bool IsAnyAnimating
        {
            get
            {
                for (var i = 0; i < _items.Count; i++)
                {
                    var item = _items[i];
                    if (item != null && item.IsAnimating) return true;
                }

                return false;
            }
        }

        public void Register(IAnimating item)
        {
            if (!_items.Contains(item))
                _items.Add(item);
        }
    }
}
```

**Khác với bản dự án**

| Chỗ | Bản dự án (`AnimatingSystem`) | Bản Horcrux | Vì |
|---|---|---|---|
| Quét | `foreach (var animate in _animates)` | vòng `for` chỉ số | `List<T>.Enumerator` là struct, cả hai không cấp phát; `for` không giữ enumerator qua lời gọi `IsAnimating` của mục |
| Kiểu phần tử | `IAnimate` của dự án | `IAnimating` của Horcrux | dự án xoá `IAnimate` (GĐ9) |
| `Register` | `if (!_animates.Contains(animate)) Add` | như vậy | nguyên văn |

**Quyết định** — `IsAnyAnimating` so `null` kiểu tham chiếu, không so Unity: `IAnimating` là interface nên `item != null` là so tham chiếu;
`MonoBehaviour` đã huỷ vẫn được hỏi `IsAnimating` — property thuần của dự án, không chạm native, trả giá trị cuối. Giữ để không đổi hành vi
của `AnimatingSystem`; chủ gỡ đăng ký khi vật rời sân (GĐ4). Phương án bỏ: `item is Object o ? o != null : item != null` — một so sánh native
mỗi mục mỗi lần hỏi, cho một ca chủ đã có đường gỡ. — `Register` chống trùng bằng `Contains` (O(n) lúc vật vào sân, nhịp "mỗi lần dữ liệu
đổi"): đăng ký hai lần là ca thật (`CacheTileSlots` đăng ký ở cả `Initialize` và `ReinitializeByLevelLoad`). — Class thuần, chủ
`new`: registry không có đời sống riêng; chủ (manager của dự án) giữ và `Clear` theo level. Phương án bỏ: `MonoBehaviour` hay service.
— `List` đủ: registry chỉ `Register/Unregister/Clear/quét`, không sửa trong lúc quét. Phương án bỏ: `DeferredSet`. — Quét tuyến tính
mỗi lần hỏi: dự án đăng ký mọi tile nên mỗi `WaitUntil` quét vài trăm interface call mỗi frame (memory perf-audit 2026-10-25, nhóm B).
Mở rộng sau: bộ đếm "đang diễn" tăng giảm khi mục đổi trạng thái — đổi hợp đồng `IAnimating`, chờ số đo.

| Consumes | Produces |
|---|---|
| `System.Collections.Generic.List<T>` | `IAnimating`, `AnimationRegistry.Register`, `IsAnyAnimating`, `Count` |

**Sơ đồ tương tác sau GĐ3**

```
  chủ ──Register+(vật)──► AnimationRegistry+ ◄──IsAnyAnimating+── host chờ
                             └─ quét vật.IsAnimating (IAnimating+)
```

**Editor setup:** tạo hai file trong Project window của Unity.

**Kiểm:** compile sạch. `Tests/EditMode/AnimationRegistryTests.cs`: `FakeAnimating : IAnimating` class thuần với `IsAnimating` set được;
`FakeAnimatingBehaviour : MonoBehaviour, IAnimating` (auto-property) cho ca huỷ.

| Case | Input | Kỳ vọng |
|---|---|---|
| rỗng | `new AnimationRegistry()` | `IsAnyAnimating` false, `Count` 0 |
| một mục đang diễn | `Register(a)`, `a.IsAnimating = true` | true; đặt false → false |
| mục đang diễn ở cuối danh sách (thứ tự) | `Register(a)`, `Register(b)`, `Register(c)`; chỉ `c` diễn | true |
| đăng ký trùng | `Register(a)` hai lần | `Count` 1 |
| mục `null` | `Register(null)` rồi `Register(a)` diễn | `Count` 2; `IsAnyAnimating` true, không ném |
| object huỷ giữa chừng | `FakeAnimatingBehaviour` đang diễn, `Register`, `Object.DestroyImmediate(go)` | `IsAnyAnimating` vẫn true, không ném — ghi tài liệu: chủ phải `Unregister` khi vật rời sân |
| nhiều mục không diễn | 100 mục `false` | false |

**Mốc commit:** `feat(common): IAnimating, AnimationRegistry.Register và IsAnyAnimating`.

---

## Giai đoạn 4 — Vật rời sân, đổi level

**Mục tiêu:** `Unregister` một vật, `Clear` cả sổ. Xong giai đoạn này một chủ sống qua nhiều level không giữ vật của level cũ.

**Vì sao bây giờ:** box xong và tile xong rời sân giữa level; dispose level phải xoá sạch một lần — hai đường ra còn thiếu của sổ.

**Ai gọi:** test của giai đoạn; trong dự án `AnimatingSystem.UnRegister` (từ `BoxesContainer.HandleBoxCompleted`,
`AnimatingSystem.HandleTileCompleted`), `OnDisable` và `HandleLevelDisposed` gọi `Clear` (GĐ9).

### `AnimationRegistry.cs` › thêm sau `Register`

```csharp
        public void Register(IAnimating item)
        {
            if (!_items.Contains(item))
                _items.Add(item);
        }

        public void Unregister(IAnimating item) => _items.Remove(item);

        public void Clear() => _items.Clear();
```

**Khác với bản dự án:** không — `UnRegister` và hai chỗ `_animates.Clear()` của `AnimatingSystem` ánh xạ một–một; tên `Unregister` theo
casing của Horcrux, lớp nối `AnimatingSystem.UnRegister` giữ tên cho 2 caller của nó (GĐ9).

**Quyết định** — `Unregister` mục chưa đăng ký là no-op (`List.Remove` trả false, không đọc): `HandleTileCompleted` gọi cho mọi tile xong,
kể cả tile đã bị `Clear` trước đó khi level dispose giữa chuyến bay. — `Clear` không hỏi `IsAnimating` của mục nào: dispose level không cần
biết ai đang diễn.

| Consumes | Produces |
|---|---|
| GĐ3 | `AnimationRegistry.Unregister`, `Clear` |

**Sơ đồ tương tác sau GĐ4**

```
  chủ ──Register / Unregister+ / Clear+──► AnimationRegistry ◄──IsAnyAnimating── host
```

**Editor setup:** không.

**Kiểm:** compile sạch; thêm vào `AnimationRegistryTests.cs`.

| Case | Input | Kỳ vọng |
|---|---|---|
| gỡ mục đang diễn | `Register(a)` diễn, `Register(b)` không; `Unregister(a)` | `IsAnyAnimating` false, `Count` 1 |
| gỡ mục chưa đăng ký | `Unregister(x)` trên sổ có `a` | không ném, `Count` 1 |
| gỡ rồi đăng ký lại | `Unregister(a)`, `Register(a)` | `Count` 1, trạng thái theo `a.IsAnimating` |
| `Clear` | 3 mục, một đang diễn; `Clear()` | `Count` 0, `IsAnyAnimating` false |
| `Clear` sổ rỗng | `Clear()` | không ném |
| đăng ký sau `Clear` | `Clear()`, `Register(b)` diễn | true |

**Mốc commit:** `feat(common): AnimationRegistry.Unregister, Clear`.

---

## Cơ chế C — `ProgressBar.Play`: trượt fill bằng handle

### Luồng mô tả

```
caller có điểm mốc mới (combo tăng): tween cũ còn chạy ──► caller Stop() handle cũ
            ▼
Play(from, to, goal, label) ──► ghi goal và nhãn một lần, ghi fill(from) ngay frame này,
            │                   trả Tween lái WriteFill mỗi frame                          GĐ5
            ▼
caller giữ Tween: Stop() khi mốc mới tới hay khi huỷ; PlayAsync(…, ct) cho caller có token
            ▼
dự án: ComboPill đổi CancellationTokenSource sang Tween (GĐ9)
```

### Bản đồ hệ cuối

```
  caller ──Play(from, to, goal, label)──► ProgressBar ──Tween.Custom(this, from, to, …, static WriteFill)──► Tween (handle)
         ──PlayAsync(…, ct)────────────►   └─ Play rồi SetCancellationToken(ct) → UniTask
         ──Stop() trên handle──────────► tween dừng tại chỗ, fill đứng yên
```

---

## Giai đoạn 5 — `ProgressBar.Play` trả handle, `PlayAsync` gọi vào nó

**Mục tiêu:** caller không có token riêng dừng được tween bằng `Stop()`; `PlayAsync` giữ chữ ký và 7 case đang có. Xong giai đoạn
này một pill combo nạp lại thanh mà không cấp phát `CancellationTokenSource` mỗi combo.

**Vì sao bây giờ:** độc lập với A và B; caller thật (`ComboPill`) đang tự dựng và `Dispose` CTS cho mỗi lần `PlayAsync`.

**Ai gọi:** `PlayAsync` (cùng file, ngay trong giai đoạn) và test; trong dự án `ComboPill.OnComboChanged` (GĐ9).

### `Runtime/Utilities/UI/ProgressBar.cs` › `API` › `PlayAsync` — đoạn cũ → đoạn mới

Cũ:

```csharp
        /// <summary>Label is written once at the start; only the fill width changes per frame.</summary>
        /// <param name="labelContent">Null keeps the current label text: fill only.</param>
        public UniTask PlayAsync(float from, float to, float goal, string labelContent, CancellationToken ct)
        {
            if (!TryWriteGoalAndLabel(goal, labelContent))
                return UniTask.CompletedTask;

            // The tween writes its first value on the next update; without this the old width shows for one frame.
            WriteFill(from);

            float duration = Mathf.Abs(to - from) / Mathf.Max(0.0001f, speed);
            return Tween.Custom(this, from, to, duration, static (bar, value) => bar.WriteFill(value), ease,
                useUnscaledTime: useUnscaledTime).SetCancellationToken(ct);
        }
```

Mới — thân cũ thành `Play`, `PlayAsync` chỉ gắn token:

```csharp
        /// <summary>Label is written once at the start; only the fill changes per frame. Returns the tween so the caller stops it by handle;
        /// default when goal is invalid (logged).</summary>
        /// <param name="labelContent">Null keeps the current label text: fill only.</param>
        public Tween Play(float from, float to, float goal, string labelContent)
        {
            if (!TryWriteGoalAndLabel(goal, labelContent))
                return default;

            // The tween writes its first value on the next update; without this the old width shows for one frame.
            WriteFill(from);

            float duration = Mathf.Abs(to - from) / Mathf.Max(0.0001f, speed);
            return Tween.Custom(this, from, to, duration, static (bar, value) => bar.WriteFill(value), ease,
                useUnscaledTime: useUnscaledTime);
        }

        /// <inheritdoc cref="Play"/>
        /// <param name="ct">Cancels the tween; prefer <see cref="Play"/> and Stop() when the caller has no token of its own.</param>
        public UniTask PlayAsync(float from, float to, float goal, string labelContent, CancellationToken ct)
        {
            var tween = Play(from, to, goal, labelContent);
            return tween.isAlive ? tween.SetCancellationToken(ct) : UniTask.CompletedTask;
        }
```

`tween.SetCancellationToken(ct)` trả `Tween`; `implicit operator UniTask(Tween)` của PrimeTween đổi sang `UniTask` ở nhánh ternary.

**Khác với bản dự án:** `ComboPill` dựng `CancellationTokenSource` mỗi combo rồi `Cancel`/`Dispose` ở combo kế và `OnDestroy`; với `Play`
caller giữ một `Tween` và `Stop()`.

**Quyết định** — `Play` trả `default` khi `goal <= 0`: cùng hành vi `PlayAsync` (`LogError` rồi không chạy); caller `Stop()` trên tween
`default` là no-op. Phương án bỏ: ném — `TryWriteGoalAndLabel` đã chọn log cho `Set`, hai cửa cùng luật. — `PlayAsync` kiểm `isAlive` trước
khi gắn token: `SetCancellationToken` trên tween `default` log lỗi của PrimeTween.

| Consumes | Produces |
|---|---|
| PrimeTween `Tween.Custom<T>`, `Tween.isAlive`, `Tween.SetCancellationToken` · `TryWriteGoalAndLabel`, `WriteFill` có sẵn | `ProgressBar.Play`; `PlayAsync` giữ chữ ký |

**Sơ đồ tương tác sau GĐ5**

```
  caller ──Play+──► ProgressBar ──Tween.Custom──► Tween ──► caller Stop()
         ──PlayAsync──► Play+ ──isAlive? SetCancellationToken(ct)──► UniTask
```

**Editor setup:** không.

**Kiểm:** compile sạch; thêm vào `Tests/PlayMode/ProgressBarTests.cs` (dựng bar bằng `SetField` như các case đang có).

| Case | Input | Kỳ vọng |
|---|---|---|
| `Play` trả handle sống | `Play(1, 0, 1, "x")` | `isAlive` true; `rect.width` = FullWidth ngay frame gọi |
| `Stop` giữa chừng | `Play`, chờ 0.2 s, `Stop()`, chờ 0.2 s | width đứng yên ở giá trị lúc dừng, trong (1, FullWidth − 1) |
| goal 0 | `Play(1, 0, 0, "x")` | trả `default`, `isAlive` false, một `LogError` khớp `\[ProgressBar\]: Goal of .* <= 0`; `Stop()` trên nó không ném |
| label null | `Set(1, 1, "before")`, `Play(1, 0, 1, null)` | `Text` vẫn "before" |
| hai `Play` liên tiếp, caller `Stop` cái trước | `t1 = Play(1, 0, 1, "a")`, `t1.Stop()`, `t2 = Play(1, 0, 1, "b")` | chỉ `t2` sống; sau drain width 0, `Text` "b" |
| `PlayAsync` | 7 case đang có | PASS không sửa |
| `PlayAsync` goal 0 | `PlayAsync(1, 0, 0, "x", None)` | trả ngay (`CompletedTask`), một `LogError` |

**Mốc commit:** `feat(ui): ProgressBar.Play trả Tween, PlayAsync gọi Play`.

---

## Cơ chế D — Hai sửa extension: không closure, không guard ở hot path

### Luồng mô tả

```
CharmSquashStretch(spec, ct): PrimeTween nội suy s từ startScale tới endScale theo ease
            ──► callback static (tr, s) ghi localScale = GetVolumePreservingScale(s)       GĐ6
SetAlpha(SpriteRenderer, a): đọc màu, ghi alpha, gán màu — ba dòng, không hỏi native       GĐ7
```

Không có bản đồ riêng: hai hàm static có sẵn, chữ ký giữ nguyên.

---

## Giai đoạn 6 — `CharmSquashStretch` 0 B mỗi lần gọi

**Mục tiêu:** `Tween.Custom<T>` nhận `Transform` làm target và nội suy thẳng `s`, callback `static` chỉ bù thể tích; `CharmFlyArc`,
`CharmCount` giữ thân, ghi giá closure trong XML doc.

**Vì sao bây giờ:** độc lập với A–C; cùng khuôn với `Play` (GĐ5): target vào `Tween.Custom<T>`, lambda `static`.

**Ai gọi:** test; chưa có caller trong dự án và Horcrux (grep: 0) — hành vi public giữ nguyên.

### `Runtime/Utilities/ExtensionMethods/Transform/TransformExtensions.CharmTween.cs` › `CharmSquashStretch` — đoạn cũ → đoạn mới

Cũ:

```csharp
        public static Tween CharmSquashStretch(this Transform self, SquashStretchSpec spec, CancellationToken ct)
        {
            return Tween.Custom(0f, 1f, spec.duration, spell, spec.ease, useUnscaledTime: true)
                .SetCancellationToken(ct);

            void spell(float t) => self.localScale = SquashStretch.GetSquashStretch(t, spec.startScale, spec.endScale,
                AxisType.Y, CoordinateSystem.XY);
        }
```

Mới:

```csharp
        /// <summary>Scale một trục từ spec.startScale tới spec.endScale theo spec.ease, trục kia bù giữ thể tích. 0 B mỗi lần gọi.</summary>
        public static Tween CharmSquashStretch(this Transform self, SquashStretchSpec spec, CancellationToken ct)
            => Tween.Custom(self, spec.startScale, spec.endScale, spec.duration,
                    static (tr, s) => tr.localScale = SquashStretch.GetVolumePreservingScale(s, AxisType.Y, CoordinateSystem.XY),
                    spec.ease, useUnscaledTime: true)
                .SetCancellationToken(ct);
```

Cùng công thức: `GetSquashStretch(ease(t), a, b)` = `GetVolumePreservingScale(lerp(a, b, ease(t)))`; PrimeTween tính `a + (b − a) · ease(t)`,
không kẹp, nên `OutBack`/`OutElastic` vẫn vọt.

### Cùng file › `CharmFlyArc` › thêm XML doc ngay trên chữ ký

```csharp
        /// <remarks>Một closure mỗi lần gọi (bắt from, target, controlPoint): dùng cho vật bay thưa, không cho hot path mỗi frame.</remarks>
        public static Tween CharmFlyArc(this Transform self, Vector3 target, ArcFlightSpec spec,
            CancellationToken ct)
```

### `Runtime/Utilities/ExtensionMethods/UniTextExtensionMethods.cs` › `CharmCount` › thêm sau `</summary>`

```csharp
        /// <remarks>Một closure và một StringBuilder mỗi lần gọi (bắt self, start, end, format): dùng cho số đếm thưa, không cho hot path mỗi frame.</remarks>
        public static Tween CharmCount(this UniTextBase self, int start, int end, string format, float duration,
            float startDelay = 0f, CancellationToken ct = default)
```

**Khác với bản dự án:** không có bản dự án; cùng cách `Tile.BuildFlightMotion` dùng `Tween.Custom(this, …, static (tile, t) => …)`.

**Quyết định** — PrimeTween nội suy `s`, không nội suy `t` rồi lerp trong callback: cùng số, bớt một `lerp` mỗi frame, và callback không
cần `spec`. Phương án bỏ: `static readonly Action<Transform, float>` như `SetJumpY` — lambda `static` inline cho cùng 0 B, ít hơn một
field. — `CharmFlyArc`, `CharmCount` giữ closure: cần ba `Vector3` hoặc một `format` và `StringBuilder` trong callback, không nhét
vào một target của `Tween.Custom<T>`; bọc class state là sinh một cấp phát khác. Mở rộng sau, khi có caller mỗi frame: một kiểu state
nhỏ pooled hoặc `Tween.Custom` nhận `TweenSettings<Vector3>` cho control point.

| Consumes | Produces |
|---|---|
| PrimeTween `Tween.Custom<T>` · `SquashStretch.GetVolumePreservingScale` | `CharmSquashStretch` (chữ ký giữ), hai `<remarks>` |

**Sơ đồ tương tác sau GĐ6**

```
  caller ──CharmSquashStretch(spec, ct)──► Tween.Custom(self, a → b, ease)+ ──static (tr, s)+──► GetVolumePreservingScale(s) → localScale
```

**Editor setup:** không.

**Kiểm:** compile sạch. `Tests/PlayMode/CharmSquashStretchTests.cs`, khung đo theo `CharmJumpTests.CharmJump_DoesNotAllocate`.

| Case | Input | Kỳ vọng |
|---|---|---|
| giữa chừng | spec `(0.8, 1.2, 0.3, OutQuad)`; ở một frame giữa chừng đọc `k = tween.interpolationFactor` (đã ease) và `localScale` | `localScale` = `GetVolumePreservingScale(lerp(0.8, 1.2, k), Y, XY)` ±1e-5 |
| kết thúc | sau 0.3 s | `(1/1.2, 1.2, 1)` ±1e-6 |
| start = end | spec `(1.2, 1.2, 0.3, Linear)` | `localScale` không đổi suốt tween |
| huỷ giữa chừng | `cts.Cancel()` ở 0.15 s | `localScale` đứng yên ở giá trị lúc huỷ, `await` ném `OperationCanceledException` |
| 0 B | warm-up một lần, rồi vòng gọi `CharmSquashStretch` | `Is.Not.AllocatingGCMemory()`; đối chứng: lambda bắt biến cùng khung `Is.AllocatingGCMemory()` |

**Mốc commit:** `perf(charm): CharmSquashStretch không closure; ghi giá closure của CharmFlyArc, CharmCount`.

---

## Giai đoạn 7 — `SetAlpha(SpriteRenderer)` bỏ guard

**Mục tiêu:** ba dòng đọc màu, ghi alpha, gán màu; không so `null` với native mỗi lần gọi.

**Vì sao bây giờ:** độc lập; caller nóng nhất (`TileView.SetAirPose` mỗi frame bay) đã tự guard ô tuỳ chọn của nó.

**Ai gọi:** `TileView.SetAirPose`, `SetBackgroundAlpha`, `SetAlpha`, `SetShadowVisible`; `BoxAnimation`, `BoxSlot.DimAdsRenderer`,
`BoxTileSlot`, `CacheTileSlot` — đều đang gọi, không đổi dòng nào.

### `Runtime/Utilities/ExtensionMethods/SpriteRenderExtensions.cs` › `SetAlpha` — đoạn cũ → đoạn mới

Cũ:

```csharp
        public static void SetAlpha(this SpriteRenderer renderer, float alpha)
        {
            if (renderer == null)
                return;

            Color color = renderer.color;
            color.a = alpha;
            renderer.color = color;
        }
```

Mới:

```csharp
        public static void SetAlpha(this SpriteRenderer renderer, float alpha)
        {
            Color color = renderer.color;
            color.a = alpha;
            renderer.color = color;
        }
```

**Khác với bản dự án:** `TileView.SetAlpha`, `SetColor` guard `_backgroundRenderer != null`, `linkBond != null` ở caller vì hai ô đó để
trống trên `GoldenTile`; `_renderer`, `_shadowRenderer` không guard.

**Quyết định** — bỏ guard: thiếu ô Inspector nổ ở lần Play đầu bằng `NullReferenceException` trỏ đúng dòng; guard chỉ che và là một lời gọi
native mỗi frame bay (`== null` của `UnityEngine.Object`). Ô tuỳ chọn là luật của từng view, guard ở view đó. Phương án bỏ: giữ guard —
"để nổ" không có vế nổ.

| Consumes | Produces |
|---|---|
| `SpriteRenderer.color` | `SetAlpha` (chữ ký giữ) |

**Sơ đồ tương tác sau GĐ7**

```
  view ──mỗi frame──► SetAlpha(renderer, a) ──đọc color, ghi a, gán+──► SpriteRenderer
```

**Editor setup:** không.

**Kiểm:** compile sạch. `Tests/PlayMode/SpriteRenderExtensionsTests.cs`.

| Case | Input | Kỳ vọng |
|---|---|---|
| ghi alpha | màu `(0.2, 0.4, 0.6, 1)`, `SetAlpha(0.5)` | `color` = `(0.2, 0.4, 0.6, 0.5)` |
| biên | `SetAlpha(0)`, `SetAlpha(1)` | `a` 0, 1; rgb giữ |
| ghi hai lần | `SetAlpha(0.5)` rồi `SetAlpha(0.25)` | `a` 0.25; rgb giữ |
| renderer null (vế nổ) | `((SpriteRenderer)null).SetAlpha(1)` | ném `NullReferenceException` |

**Mốc commit:** `perf(sprite): SetAlpha bỏ guard null`.

---

## Giai đoạn 8 — Tài liệu (agent)

`docs/Runtime/Tada/Tada.md` và `Tada.html`, `.md` mở đầu `Code: Runtime/Utilities/Tweening/Tada`: luồng *host dựng tween 0 → Duration →
Evaluate mỗi frame → TadaPose → host ghi thân, gốc, bóng* (ASCII của Cơ chế A) · bảng ba pha và `§0` (công thức, bảng mốc → kỳ vọng) · bảng
API (`Duration`, `Evaluate`, `EndLift`, `Shadow01`, `BEAT_COUNT`) · "Trước khi chạy": caller cần gốc và thân tách (`async-presentation`:
sprite 2.5D tách gốc và thân, bóng là con của gốc), núm `TadaSpec` phơi `[SerializeField]` trên prefab của host · ví dụ nối sang chuyến bay
(`EndLift`, `Shadow01(EndLift)`) · "Design decisions" gom **Quyết định** GĐ1–GĐ2 · ràng buộc giữa núm host phải tự giữ (`WindupDip ≤
LiftHeight`; `LiftScale`, `WindupScale` so với cỡ host dùng trước và sau tada).

`docs/Runtime/AnimationRegistry/AnimationRegistry.md` và `AnimationRegistry.html`, `.md` mở đầu `Code: Runtime/Utilities/Common`: luồng
*Register khi vào sân → IsAnyAnimating khi cần chờ → Unregister khi rời → Clear theo level* · bảng API · ai `Register`, ai `Unregister`, chủ
`Clear` · "Design decisions" GĐ3–GĐ4 · bẫy: đăng ký `MonoBehaviour` rồi huỷ mà không gỡ thì `IsAnimating` vẫn được hỏi trên object đã huỷ;
quét tuyến tính mỗi lần hỏi (dòng "Mở rộng sau").

`docs/Runtime/CharmFlyArc/CharmFlyArc.md` §2: dòng "`GetSquashStretch` nhận `t` đã ease" → PrimeTween nội suy `s` từ `startScale` tới
`endScale` theo `ease`, callback `static` gọi `GetVolumePreservingScale(s, Y, XY)`, 0 B mỗi lần gọi; §1 và §3 thêm một dòng giá closure của
`CharmFlyArc`, `CharmCount` (cùng chữ với `<remarks>` GĐ6). Thư mục này chưa có `.html`; tạo `.html` cho CharmFlyArc là việc riêng ngoài Plan.

`ProgressBar` chưa có `docs/Runtime/ProgressBar/`; Plan này không mở: XML doc của `Play`, `PlayAsync` (GĐ5) là chỗ caller đọc.

`Plans/FeelingPlan.md`: mục `Wobble`/`Jelly` thêm "lắc đặt mốc 5 nhịp đã có: `TadaCurve`".

Mỗi `.html` chọn từ `.md` của nó: kịch bản (tile cuối diễn rồi bay; host chờ hết diễn rồi mở tương tác), cách dùng ("Trước khi chạy" nguyên
vẹn, API consumer gọi), flow, lưu ý (bẫy object huỷ, núm ràng buộc nhau).

**Mốc commit:** `docs(tada,common): Tada.md, AnimationRegistry.md và .html; CharmFlyArc.md §2`.

---

## Giai đoạn 9 — category-jam dùng bản Horcrux, xoá bản tạm (agent)

**Mục tiêu:** dự án không còn `TileFinaleCurve`, `TileFinaleTuning`, `FinalePose`, `IAnimate`, `CancellationTokenSource` trong `Hud/`,
`s_amountTexts`; cảm giác chơi không đổi một khung nào.

**Thứ tự:** chạy test đối chiếu tada (bước 1) **trước** khi xoá bản tạm; quay mốc trace **trước** khi sửa file đầu tiên.

### 1. Test đối chiếu tạm — `Assets/_CategoryJam/Tests/EditMode/TadaEquivalenceSweep.cs`

Với hai bộ số (mặc định C# của `TileFinaleTuning` và một bộ bất kỳ khác), `fromScale` 1.15: mỗi 1 ms trên `Duration + 0.05`,
`TileFinaleCurve.Evaluate(t, s, tuning)` và `TadaCurve.Evaluate(t, s, spec)` bằng nhau tuyệt đối ở ba kênh; `Duration`, `EndLift`,
`Shadow01(lift)` với lift 0, 0.1, L, 2L bằng nhau. Chạy xanh rồi **xoá** cùng `TileFinaleCurve.cs` ở bước 2.

### 2. Tada

| File dự án | Đổi |
|---|---|
| `Entities/Tile/TileFinaleCurve.cs` | **xoá hẳn** (cả `.meta`) |
| `Entities/Tile/TileFeelTuning.cs` | xoá `TileFinaleTuning` và `<summary>` của nó |
| `Entities/Tile/Tile.cs` | `[SerializeField] private TileFinaleTuning finale = new();` → `[Tooltip("Tada tile cuối. LiftScale ≥ HeldScale của press, WindupScale ≤ PeakScale của flight, WiggleDegrees ≤ 12° (sprite 2.5D lộ cạnh).")] [SerializeField] private TadaSpec finale = new(0.35f, 0.15f, 1.2f, 6f, 0.08f, 0.04f, 1.14f, 0.05f, 0.07f);` · `FlyToAsync`: `TileFinaleCurve.Duration(finale)`, `TileFinaleCurve.EndLift(finale)` → `TadaCurve.` · `BuildFlightState`: `TileFinaleCurve.Shadow01(startLift, finale)` → `TadaCurve.Shadow01(…)` · `StepFinale`: `TileFinaleCurve.Evaluate(time, _flightState.FinaleScaleFrom, finale)` → `TadaCurve.Evaluate(…)`, `TileFinaleCurve.Shadow01(pose.Lift, finale)` → `TadaCurve.Shadow01(…)` (`pose` là `var`, kiểu `TadaPose` tự suy) · `finale.WindupScale` giữ · thêm `using Horcrux.Runtime.Utilities.Tweening;` |
| `Entities/Tile/Tile.Editor.cs` › `PreviewFinale` | `TileFinaleCurve.Duration(finale)` → `TadaCurve.Duration(finale)`; thêm `using Horcrux.Runtime.Utilities.Tweening;` |
| `Tests/EditMode/TileFinaleCurveTests.cs` | **xoá** (19 case đã ở Horcrux `TadaCurveTests`) |
| `Scripts/Feel.md` §7 và "Nghiệm thu" | `TileFinaleTuning` → `TadaSpec` (field `finale`), `TileFinaleCurve` → `TadaCurve` (Horcrux, `docs/Runtime/Tada/Tada.md`), `TileFinaleCurveTests` → Horcrux `TadaCurveTests` |

### 3. Registry

| File dự án | Đổi |
|---|---|
| `Director/IAnimate.cs` | **xoá** (cả `.meta`) |
| `Tile.cs`, `Box.cs`, `BoxesContainer.cs`, `PreviewBox.cs`, `CacheTileSlots.cs`, `ShoppingCart.cs`, `Shelf.cs` | `IAnimate` → `IAnimating` ở khai báo class; thêm `using Horcrux.Runtime.Utilities.Common;`; `IsAnimating` của từng class giữ nguyên |
| `Director/AnimatingSystem.cs` | đoạn cũ → đoạn mới bên dưới |

`AnimatingSystem.cs` — Cũ: field `List<IAnimate> _animates = new();`, `IsAnimating()` duyệt `foreach` bỏ `null`, `Register` với
`Contains`, `UnRegister` gọi `Remove`, `OnDisable` và `HandleLevelDisposed` gọi `_animates.Clear()`. Mới:

```csharp
	public class AnimatingSystem : BaseManager
	{
		private readonly AnimationRegistry _registry = new();

		private void OnDisable() => _registry.Clear();

		public bool IsAnimating() => _registry.IsAnyAnimating;

		public void Register(IAnimating item) => _registry.Register(item);

		public void UnRegister(IAnimating item) => _registry.Unregister(item);

		public override void HandleTileCompleted(Tile tile) => UnRegister(tile);

		public override void HandleLevelDisposed()
		{
			base.HandleLevelDisposed();
			_registry.Clear();
		}
	}
```

`using`: thêm `Horcrux.Runtime.Utilities.Common`; `System`, `System.Collections.Generic`, `UnityEngine` không còn chỗ dùng thì bỏ. Caller
(`GameDirector`, `BoxesContainer`, `CacheTileSlots`) không đổi dòng nào.

### 4. ComboPill

`Hud/ComboPill.cs` — Cũ: `CancellationTokenSource _drainCts`, `CancelDrain()` (Cancel, Dispose, null), `OnComboChanged` gọi `CancelDrain()`
rồi `bar.PlayAsync(e.WindowSeconds, 0f, e.WindowSeconds, Label(e.Count), _drainCts.Token).Forget()`, `OnDestroy` gọi `CancelDrain()`. Mới:

```csharp
        private Subscription<ComboChanged> _subscription;
        // Handle cửa sổ drain: Stop là huỷ, không cấp phát token mỗi combo.
        private Tween _drain;
```

```csharp
        private void OnDestroy()
        {
            _subscription.Dispose();
            _drain.Stop();
        }
```

```csharp
        private void OnComboChanged(ComboChanged e)
        {
            _drain.Stop();

            if (e.Count == 0)
            {
                ShowEmpty();
                return;
            }

            bar.SetLabelActive(true);
            _drain = bar.Play(e.WindowSeconds, 0f, e.WindowSeconds, Label(e.Count));
        }
```

Xoá `CancelDrain`, `using System.Threading;`, `using Cysharp.Threading.Tasks;`; thêm `using PrimeTween;`. `_labels` **giữ**: `ProgressBar`
nhận chuỗi, cache có trần bằng combo cao nhất. `Director/ComboTracker.cs` không chạm.

### 5. Số lớp của kệ

`Entities/Shelf/SingleShelfAmountBar.cs`: xoá `s_amountTexts`, `AmountText(int)`; hai dòng `_amountText.Text = AmountText(count);`
(`InitBarCount`, `UpdateCurrentAmount`) → `_amountText.SetNumber(count);`; thêm `using Horcrux.Runtime.Utilities.ExtensionMethods;` nếu
chưa có. `Entities/Box/CategoryBox.cs` giữ `s_counterTexts` (nhãn hai số).

**Editor setup:**
- Đóng Prefab Mode của `Tile.prefab`, `GoldenTile.prefab` trước khi compile bước 2 — Prefab Mode đang mở tự lưu đè YAML khi kiểu field
  serialize đổi; đóng rồi mở lại thấy khối `finale` nguyên giá trị.
- Xoá `TileFinaleCurve.cs`, `IAnimate.cs`, `TileFinaleCurveTests.cs`, `TadaEquivalenceSweep.cs` trong Project window (xoá cả `.meta`).

**Kiểm:**
- grep `TileFinaleCurve`, `TileFinaleTuning`, `FinalePose`, `IAnimate\b`, `_drainCts`, `CancelDrain`, `s_amountTexts`, `AmountText(`
  trong `Assets/_CategoryJam` = 0 · compile dự án sạch · EditMode, PlayMode Horcrux và EditMode dự án PASS.
- `git diff -- Assets/_CategoryJam/Addressables/Tile/Tile.prefab Assets/_CategoryJam/Addressables/Tile/GoldenTile.prefab` rỗng: key khối
  `finale` và 9 key con giữ nguyên tên nên YAML không đổi; số trong đó là nháp của developer, không phải mốc.
- **Trace 0 chênh** (`feel-tune`, Horcrux `Editor/FeelTrace/`, kịch bản `docs/tools/feel_trace/trace.cs` có bước tile cuối và bước magnet):
  quay mốc trước bước 2, quay lại sau bước 5, `feeltrace.py diff` 0 ở mọi khung — kể cả bước tada (`TadaCurve`) và bước chờ rảnh sau magnet
  (`IsAnyAnimating` quyết lúc mở tương tác).
- **Cần chơi thử** (developer):

| Vào đâu | Làm gì | Nhìn gì | Dấu hiệu hỏng |
|---|---|---|---|
| một level, chơi tới tile cuối (tap, hoặc từ cache, cart, nam châm) | để tile cuối được giao ô box | đúng một tile nhấc lên, bóng nằm lại, lắc 5 nhịp giảm dần, nhún rồi bay; vệt bật lúc rời chỗ; popup thắng sau khi hết hoạt ảnh | tile co lại lúc cất cánh · thân nhảy khung ở chỗ nối tada → bay · hai tile cùng diễn · popup thắng không chờ tada |
| Prefab Mode của `Tile.prefab`, Inspector của `Tile` | bấm **Preview Finale** | ba pha diễn tại chỗ trong Scene view rồi tile trả về nguyên | tile kẹt nghiêng hay to sau preview · nút báo lỗi |
| giữa tada | bấm Replay | level mới sạch | tile kẹt nghiêng, to, trong pool |
| HUD combo | đóng hai box liên tiếp trong cửa sổ; rồi để hết cửa sổ | pill "Combo x2" nạp đầy lại rồi trôi; hết cửa sổ về rỗng, nhãn tắt | pill không nạp lại · không về 0 · nhãn còn khi rỗng |
| Revive AddBoxAndClear3 | kích booster khi box đang hoàn thành | hệ chờ yên rồi mới đẩy tile sang cart | đẩy tile khi box còn bay |
| kệ Single có thanh đếm | tap tile hàng trước | số lớp giảm đúng và nảy | số không đổi · số sai |
| Console lúc vào level | — | không `NullReferenceException` trỏ `SetAlpha` | có: ô `SpriteRenderer` thiếu trên prefab (`TileView`, `BoxAnimation`, `BoxSlot`, `BoxTileSlot`, `CacheTileSlot`) |

**Mốc commit:** repo dự án `refactor(feel): tada, sổ đang diễn, pill combo dùng TadaCurve, AnimationRegistry, ProgressBar.Play của Horcrux`
kèm con trỏ submodule.

---

## File → giai đoạn

| File | GĐ1 | GĐ2 | GĐ3 | GĐ4 | GĐ5 | GĐ6 | GĐ7 | GĐ8 | GĐ9 |
|---|---|---|---|---|---|---|---|---|---|
| `Runtime/Utilities/Tweening/Tada/TadaSpec.cs`, `TadaPose.cs` | tạo | | | | | | | | |
| `Runtime/Utilities/Tweening/Tada/TadaCurve.cs` | tạo | `EndLift`, `Shadow01` | | | | | | | |
| `Runtime/Utilities/Common/IAnimating.cs` | | | tạo | | | | | | |
| `Runtime/Utilities/Common/AnimationRegistry.cs` | | | tạo | `Unregister`, `Clear` | | | | | |
| `Runtime/Utilities/UI/ProgressBar.cs` | | | | | `Play`, `PlayAsync` | | | | |
| `Runtime/Utilities/ExtensionMethods/Transform/TransformExtensions.CharmTween.cs` | | | | | | `CharmSquashStretch`, remark `CharmFlyArc` | | | |
| `Runtime/Utilities/ExtensionMethods/UniTextExtensionMethods.cs` | | | | | | remark `CharmCount` | | | |
| `Runtime/Utilities/ExtensionMethods/SpriteRenderExtensions.cs` | | | | | | | `SetAlpha` | | |
| `Tests/EditMode/TadaCurveTests.cs` (agent) | tạo | thêm case | | | | | | | |
| `Tests/EditMode/AnimationRegistryTests.cs` (agent) | | | tạo | thêm case | | | | | |
| `Tests/PlayMode/ProgressBarTests.cs` (agent) | | | | | thêm case | | | | |
| `Tests/PlayMode/CharmSquashStretchTests.cs`, `SpriteRenderExtensionsTests.cs` (agent) | | | | | | tạo | tạo | | |
| `docs/Runtime/Tada/Tada.md`, `.html` · `docs/Runtime/AnimationRegistry/AnimationRegistry.md`, `.html` · `docs/Runtime/CharmFlyArc/CharmFlyArc.md` · `Plans/FeelingPlan.md` (agent) | | | | | | | | tạo, sửa | |
| dự án: `Tile.cs`, `Tile.Editor.cs`, `TileFeelTuning.cs`, `AnimatingSystem.cs`, 7 class `IAnimate`, `ComboPill.cs`, `SingleShelfAmountBar.cs`, `Feel.md`; xoá `TileFinaleCurve.cs`, `IAnimate.cs`, `TileFinaleCurveTests.cs` | | | | | | | | | đổi |
