# Plan — `TadaCurve`, `AnimationRegistry`, `ProgressBar.Play` và hai sửa extension Horcrux theo kinh nghiệm dự án

Bản tạm: category-jam A `Entities/Tile/TileFinaleCurve.cs`, `TileFeelTuning.cs` › `TileFinaleTuning` · B `Director/AnimatingSystem.cs`, `IAnimate.cs` · C `Hud/ComboPill.cs` (CTS quanh `PlayAsync`) · D `TileView` (`SetAlpha` mỗi frame bay); kết luận audit ở `Scripts/Feel.md`. Bốn cơ chế độc lập; không phụ thuộc Plan nào, trước hay sau `Horcrux_HopCurve_DipFlash_Plan.md` đều được.
Chung file, thứ tự nào cũng được: `TransformExtensions.CharmTween.cs` với `Horcrux_CharmFlyArcFollow_Plan.md` · `docs/Runtime/CharmFlyArc/CharmFlyArc.md` với CharmFlyArcFollow (tạo `.html`), ArchedControlPoint, UniTextSetFraction · category-jam `Scripts/Feel.md` › Nghiệm thu với SwirlPath, HopCurve_DipFlash.
Kiểm: `bash "<horcrux>/Editor/CompileCheck/Tools~/verify.sh" --csproj com.horcrux.runtime.csproj,Horcrux.EditModeTests.csproj,Horcrux.PlayModeTests.csproj --tests Horcrux.EditModeTests`; PlayMode chạy ở Window › General › Test Runner.

## Cơ chế A — `TadaCurve`: hình dạng nhịp tada

### Kịch bản

| Tình huống | Hệ làm gì | Caller/người chơi thấy |
|---|---|---|
| host dựng tween `0 → Duration(spec)`, Linear; mỗi frame `Evaluate(time, fromScale, spec)` | nhấc (OutQuad) → lắc 5 nhịp giảm dần kèm phồng → lấy đà (InOutSine) | tile cuối nhấc lên, lắc 5 nhịp, nhún rồi bay |
| bóng mỗi frame | `Shadow01(pose.Lift, spec)` | bóng nằm lại chỗ cũ, co theo độ nhấc |
| hết `Duration`, nối sang chuyến bay | `EndLift(spec)`, `Shadow01(EndLift)` | thân và bóng không nhảy khung ở chỗ nối |
| Sequence chèn tiếng, rung | `InsertCallback(WiggleStart(spec), …)` | tiếng vào đúng lúc thân bắt đầu lắc |
| `time` âm · quá `Duration` | `Clamp01` | tư thế xuất phát · giữ tư thế cuối |
| `fromScale` 1 (rời ô không qua cú đè) | pha nhấc lerp từ 1 | bắt đầu ở cỡ 1 |
| `LiftHeight` 0 (tada chỉ lắc) | `Shadow01` trả 0 | không `NaN` |
| `WiggleDegrees` 0 | — | không lắc |
| `WindupDip > LiftHeight` | không kẹp | thân chìm dưới chỗ cũ lúc cất cánh — lỗi núm, tooltip báo |

### Flow

```
host (dự án)                                        Runtime/Utilities/Tweening/Tada/
[SerializeField] TadaSpec spec ──in──────────────►  TadaCurve (static, không state)                TadaCurve.cs · TadaSpec.cs
Tween.Custom(0 → Duration(spec), Linear) ────────►    Duration
mỗi frame StepFinale(time) ──Evaluate(time, s₀)──►    Evaluate ──► TadaPose (Lift, Angle, Scale)   TadaPose.cs
   ghi gốc đổi cỡ, thân nhấc và xoay quanh tâm ◄──────────────────┘
   bóng ──Shadow01(pose.Lift)──────────────────►      Shadow01
hết Duration: chuyến bay ──EndLift · Shadow01(EndLift)──► EndLift
dựng Sequence ──InsertCallback(WiggleStart)────►      WiggleStart → tiếng, rung
                                                      WiggleKeys (6 mốc), OutQuad, InOutSine (private)
```

### Thuật toán

Ký hiệu (field `TadaSpec`): L `LiftHeight` · T_l `LiftDuration` · S_l `LiftScale` · A `WiggleDegrees` · T_b `WiggleBeatDuration` · B `BeatScaleBump` · S_w `WindupScale` · D `WindupDip` · T_w `WindupDuration` · s₀ `fromScale` · K = (0, 1, −1, 0.66, −0.33, 0), năm mốc chọn bằng mắt · e_q(x) = x(2 − x) · e_s(x) = ½(1 − cos πx).

| Pha | Khoảng, biến | Lift | Angle | Scale |
|---|---|---|---|---|
| nhấc | `0 ≤ t < T_l`, `u = e_q(clamp01(t / T_l))` | `L·u` | 0 | `s₀ + (S_l − s₀)·u` |
| lắc | `t′ = t − T_l < 5T_b`, `i = min(⌊t′ / T_b⌋, 4)`, `u = clamp01((t′ − i·T_b) / T_b)` | `L` | `A·[K_i + (K_{i+1} − K_i)·e_s(u)]` | `S_l + B·sin(πu)` |
| lấy đà | `t″ = t − T_l − 5T_b`, `w = e_s(clamp01(t″ / T_w))` | `L − D·w` | 0 | `S_l + (S_w − S_l)·w` |

`Shadow01 = clamp01(Lift / L)` (L = 0 → 0) · `EndLift = L − D` · `WiggleStart = T_l` · `Duration = T_l + 5T_b + T_w`.

| Mốc (liền mạch) | Lift | Angle | Scale | Từ |
|---|---|---|---|---|
| t = 0 | 0 | 0 | s₀ | e_q(0) = 0 |
| t = T_l (cuối nhấc = đầu lắc) | L | A·K₀ = 0 | S_l | e_q(1) = 1; sin 0 = 0 |
| ranh nhịp i → i+1 | L | A·K_{i+1} ở cả hai phía | S_l | e_s(1) = 1, e_s(0) = 0; sin π = 0 |
| t = T_l + 5T_b (đầu lấy đà) | L | A·K₅ = 0 | S_l | w = 0 |
| t ≥ `Duration` | L − D | 0 | S_w | clamp01 giữ w = 1 |

### GĐ1 — xong thì một host ghi được cả nhịp tada lên thân và gốc của nó

```
  host ──Tween.Custom(0 → Duration+(spec))──► mỗi frame: Evaluate+(time, s₀, spec) ──► TadaPose+ ──► host ghi gốc, thân
```

`Runtime/Utilities/Tweening/Tada/TadaSpec.cs` — file mới:

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

`Runtime/Utilities/Tweening/Tada/TadaPose.cs` — file mới:

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

`Runtime/Utilities/Tweening/Tada/TadaCurve.cs` — file mới:

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
                // LerpUnclamped: u, w đã kẹp [0, 1] nên không vọt; không kẹp hai lần.
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

        // Inline, không qua Easer.Evaluate(EaseType, t): bớt một switch mỗi frame cho cùng số.
        private static float OutQuad(float x) => x * (2f - x);

        private static float InOutSine(float x) => 0.5f * (1f - Mathf.Cos(Mathf.PI * x));
    }
}
```

**Editor setup:**

1. Project window: tạo thư mục `Runtime/Utilities/Tweening/Tada/` và ba file trên trong đó (để có `.meta`).

`Tests/EditMode/TadaCurveTests.cs` (agent) — `Default()` trả `new TadaSpec(0.35f, 0.15f, 1.2f, 6f, 0.08f, 0.04f, 1.14f, 0.05f, 0.07f)` (mặc định C# của `TileFinaleTuning`); dung sai `1e-4f`, góc `1e-3f`; 14 case đầu chuyển nguyên văn từ `TileFinaleCurveTests` của dự án:

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

### GĐ2 — xong thì host nối liền tada với chuyến bay không nhảy khung, chèn tiếng đúng mốc lắc

```
  host ──mỗi frame tada──► Evaluate → pose ──Shadow01+(pose.Lift)──► bóng co
       ──hết Duration──► EndLift+ → độ cao xuất phát chuyến bay; Shadow01+(EndLift) → bóng lúc cất cánh
       ──dựng Sequence──► InsertCallback(WiggleStart+) → tiếng, rung đúng lúc bắt đầu lắc
```

`TadaCurve.cs` › thêm sau `Duration`, trước `Evaluate`:

```csharp
        public static float Duration(in TadaSpec spec)
            => spec.LiftDuration + BEAT_COUNT * spec.WiggleBeatDuration + spec.WindupDuration;

        /// <summary>Giây nhịp lắc đầu bắt đầu, tính từ lúc tada bắt đầu: nhấc xong thì lắc.</summary>
        // Hàm, không để caller cộng LiftDuration: mốc thuộc chỗ chia pha, đổi thứ tự pha thì mốc dời theo.
        public static float WiggleStart(in TadaSpec spec) => spec.LiftDuration;

        /// <summary>Độ cao thân lúc cất cánh: chuyến bay bắt đầu từ đây.</summary>
        // Không kẹp âm: WindupDip > LiftHeight là lỗi núm, tooltip của WindupDip báo ở Inspector.
        public static float EndLift(in TadaSpec spec) => spec.LiftHeight - spec.WindupDip;

        /// <summary>Độ co bóng theo độ nhấc: nhấc hết LiftHeight thì bóng co như ở đỉnh chuyến bay.</summary>
        // LiftHeight 0 hợp lệ (tada chỉ lắc): chia 0 ra NaN lan vào scale bóng.
        public static float Shadow01(float lift, in TadaSpec spec)
            => spec.LiftHeight > 0f ? Mathf.Clamp01(lift / spec.LiftHeight) : 0f;
```

`Tests/EditMode/TadaCurveTests.cs` (agent) — 5 case từ `TileFinaleCurveTests`, hai biên, case `WiggleStart` mới:

| Case | Input | Kỳ vọng |
|---|---|---|
| `WiggleStart` | `Default()` | 0.15 (= `LiftDuration`); `Evaluate(WiggleStart − 1e-4)` còn `Angle` 0, `Evaluate(WiggleStart + beat/2)` có `Angle` ≠ 0 |
| `EndLift` | `Default()` | 0.30 |
| `Shadow01` theo lift | lift 0, 0.35, 0.30, 0.5 | 0, 1, 0.30/0.35, 1 (kẹp) |
| `Shadow01` lift âm (ngoài dải) | −0.1 | 0 |
| `LiftHeight` 0 | spec với 0; lift 0 và 0.2 | 0 — không `NaN` |
| nối liền | `Shadow01(Evaluate(Duration).Lift)` so với `Shadow01(EndLift)` | bằng nhau |

---

## Cơ chế B — `AnimationRegistry`: sổ những thứ đang diễn

### Kịch bản

| Tình huống | Hệ làm gì | Caller/người chơi thấy |
|---|---|---|
| vật vào sân: `Register(vật)` | thêm nếu chưa có | đăng ký hai lần (khởi tạo và nạp lại level) vẫn một mục |
| host cần chờ: `IsAnyAnimating` | quét tuyến tính, 0 B | true khi còn một mục diễn; host `WaitUntil(!IsAnyAnimating)` rồi mở tương tác |
| mục `null` trong sổ | bỏ qua | không ném |
| `MonoBehaviour` đã huỷ mà chưa gỡ | vẫn hỏi `IsAnimating` (property thuần) | trả giá trị cuối, không ném — chủ phải `Unregister` khi vật rời sân |
| vật rời sân: `Unregister` | `List.Remove` | mục chưa đăng ký (đã bị `Clear`): no-op |
| đổi level: `Clear` | xoá sạch, không hỏi mục nào | sổ rỗng |
| người chơi | — | popup thắng chờ hết hoạt ảnh; booster chờ box bay xong rồi mới đẩy tile |

### Flow

```
chủ (manager của level) ──new · Register / Unregister / Clear──► AnimationRegistry        Runtime/Utilities/Common/AnimationRegistry.cs
                                                                  List<IAnimating>
vật : IAnimating { IsAnimating } ◄─────────── quét ───────────────┤                        Runtime/Utilities/Common/IAnimating.cs
host ──IsAnyAnimating──────────────────────────────────────────────┘ → WaitUntil(!IsAnyAnimating) rồi mở tương tác
```

### GĐ3 — xong thì một host chờ được mọi thứ đã đăng ký hết diễn

```
  chủ ──Register+(vật)──► AnimationRegistry+ ◄──IsAnyAnimating+── host chờ
                             └─ quét vật.IsAnimating (IAnimating+)
```

`Runtime/Utilities/Common/IAnimating.cs` — file mới:

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

`Runtime/Utilities/Common/AnimationRegistry.cs` — file mới:

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
                // for, không foreach: không giữ enumerator qua lời gọi IsAnimating của mục.
                for (var i = 0; i < _items.Count; i++)
                {
                    var item = _items[i];
                    // So tham chiếu, không so Unity: tránh lời gọi native mỗi mục mỗi lần hỏi; chủ đã có đường gỡ.
                    if (item != null && item.IsAnimating) return true;
                }

                return false;
            }
        }

        public void Register(IAnimating item)
        {
            // Chống trùng: đăng ký hai lần là ca thật; O(n) chỉ lúc vật vào sân.
            if (!_items.Contains(item))
                _items.Add(item);
        }
    }
}
```

**Editor setup:**

1. Project window: tạo hai file trên trong `Runtime/Utilities/Common/`.

`Tests/EditMode/AnimationRegistryTests.cs` (agent) — `FakeAnimating : IAnimating` class thuần với `IsAnimating` set được; `FakeAnimatingBehaviour : MonoBehaviour, IAnimating` (auto-property) cho ca huỷ:

| Case | Input | Kỳ vọng |
|---|---|---|
| rỗng | `new AnimationRegistry()` | `IsAnyAnimating` false, `Count` 0 |
| một mục đang diễn | `Register(a)`, `a.IsAnimating = true` | true; đặt false → false |
| mục đang diễn ở cuối danh sách (thứ tự) | `Register(a)`, `Register(b)`, `Register(c)`; chỉ `c` diễn | true |
| đăng ký trùng | `Register(a)` hai lần | `Count` 1 |
| mục `null` | `Register(null)` rồi `Register(a)` diễn | `Count` 2; `IsAnyAnimating` true, không ném |
| object huỷ giữa chừng | `FakeAnimatingBehaviour` đang diễn, `Register`, `Object.DestroyImmediate(go)` | `IsAnyAnimating` vẫn true, không ném — ghi tài liệu: chủ phải `Unregister` khi vật rời sân |
| nhiều mục không diễn | 100 mục `false` | false |

### GĐ4 — xong thì một chủ sống qua nhiều level không giữ vật của level cũ

```
  chủ ──Register / Unregister+ / Clear+──► AnimationRegistry ◄──IsAnyAnimating── host
```

`AnimationRegistry.cs` › thêm sau `Register`:

```csharp
        public void Register(IAnimating item)
        {
            if (!_items.Contains(item))
                _items.Add(item);
        }

        // Mục chưa đăng ký là no-op: vật xong sau khi level đã Clear vẫn gọi gỡ.
        public void Unregister(IAnimating item) => _items.Remove(item);

        public void Clear() => _items.Clear();
```

`Tests/EditMode/AnimationRegistryTests.cs` (agent):

| Case | Input | Kỳ vọng |
|---|---|---|
| gỡ mục đang diễn | `Register(a)` diễn, `Register(b)` không; `Unregister(a)` | `IsAnyAnimating` false, `Count` 1 |
| gỡ mục chưa đăng ký | `Unregister(x)` trên sổ có `a` | không ném, `Count` 1 |
| gỡ rồi đăng ký lại | `Unregister(a)`, `Register(a)` | `Count` 1, trạng thái theo `a.IsAnimating` |
| `Clear` | 3 mục, một đang diễn; `Clear()` | `Count` 0, `IsAnyAnimating` false |
| `Clear` sổ rỗng | `Clear()` | không ném |
| đăng ký sau `Clear` | `Clear()`, `Register(b)` diễn | true |

---

## Cơ chế C — `ProgressBar.Play`: trượt fill bằng handle

### Kịch bản

| Tình huống | Hệ làm gì | Caller/người chơi thấy |
|---|---|---|
| caller có mốc mới (combo tăng) | caller `Stop()` handle cũ; `Play(from, to, goal, label)` ghi goal, nhãn một lần, fill(`from`) ngay frame này | `Tween` lái fill mỗi frame; không cấp phát token mỗi lần |
| `Stop()` giữa chừng | tween dừng | fill đứng yên tại chỗ |
| `goal ≤ 0` | `LogError`, trả `default` | `Stop()` trên nó no-op |
| `label` null | giữ nhãn đang có | chỉ fill đổi |
| caller có token riêng: `PlayAsync(…, ct)` | `Play` rồi gắn token | 7 case cũ PASS; `goal` 0 → `CompletedTask` |
| người chơi | — | pill combo nạp đầy lại mỗi combo rồi trôi; hết cửa sổ về rỗng, nhãn tắt |

### Flow

```
caller ──Play(from, to, goal, label)──► ProgressBar ──Tween.Custom(this, from, to, …, static WriteFill)──► Tween (handle)
       ──PlayAsync(…, ct)────────────►   └─ Play rồi isAlive? SetCancellationToken(ct) → UniTask
       ──Stop() trên handle──────────► tween dừng tại chỗ, fill đứng yên
(Runtime/Utilities/UI/ProgressBar.cs)
```

### GĐ5 — xong thì một pill combo nạp lại thanh bằng handle, không cấp phát `CancellationTokenSource` mỗi combo

```
  caller ──Play+──► ProgressBar ──Tween.Custom──► Tween ──► caller Stop()
         ──PlayAsync──► Play+ ──isAlive? SetCancellationToken(ct)──► UniTask
```

`Runtime/Utilities/UI/ProgressBar.cs` › `API` › `PlayAsync` — cũ → mới, thân cũ thành `Play`, `PlayAsync` chỉ gắn token:

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
            // isAlive first: SetCancellationToken on a default tween logs a PrimeTween error; Tween converts implicitly to UniTask.
            return tween.isAlive ? tween.SetCancellationToken(ct) : UniTask.CompletedTask;
        }
```

`Tests/PlayMode/ProgressBarTests.cs` (agent) — dựng bar bằng `SetField` như các case đang có:

| Case | Input | Kỳ vọng |
|---|---|---|
| `Play` trả handle sống | `Play(1, 0, 1, "x")` | `isAlive` true; `rect.width` = FullWidth ngay frame gọi |
| `Stop` giữa chừng | `Play`, chờ 0.2 s, `Stop()`, chờ 0.2 s | width đứng yên ở giá trị lúc dừng, trong (1, FullWidth − 1) |
| goal 0 | `Play(1, 0, 0, "x")` | trả `default`, `isAlive` false, một `LogError` khớp `\[ProgressBar\]: Goal of .* <= 0`; `Stop()` trên nó không ném |
| label null | `Set(1, 1, "before")`, `Play(1, 0, 1, null)` | `Text` vẫn "before" |
| hai `Play` liên tiếp, caller `Stop` cái trước | `t1 = Play(1, 0, 1, "a")`, `t1.Stop()`, `t2 = Play(1, 0, 1, "b")` | chỉ `t2` sống; sau drain width 0, `Text` "b" |
| `PlayAsync` | 7 case đang có | PASS không sửa |
| `PlayAsync` goal 0 | `PlayAsync(1, 0, 0, "x", None)` | trả ngay (`CompletedTask`), một `LogError` |

---

## Cơ chế D — Hai sửa extension: không closure, không guard ở hot path

### Kịch bản

| Tình huống | Hệ làm gì | Caller/người chơi thấy |
|---|---|---|
| `CharmSquashStretch(spec, ct)` | PrimeTween nội suy `s` từ `startScale` tới `endScale` theo `ease`, callback `static` bù thể tích | cùng scale như trước, 0 B mỗi lần gọi; `OutBack`/`OutElastic` vẫn vọt |
| `startScale = endScale` | — | `localScale` không đổi suốt tween |
| huỷ `ct` giữa chừng | tween dừng | scale đứng yên, `await` ném `OperationCanceledException` |
| `CharmFlyArc`, `CharmCount` | giữ closure, `<remarks>` ghi giá | caller biết không dùng cho hot path mỗi frame |
| `SetAlpha(SpriteRenderer, a)` mỗi frame bay | đọc màu, ghi alpha, gán màu | không lời gọi native `== null` |
| renderer `null` (ô Inspector thiếu) | — | `NullReferenceException` trỏ đúng dòng ở lần Play đầu |

### Flow

```
caller ──CharmSquashStretch(spec, ct)──► Tween.Custom(self, a → b, ease) ──static (tr, s)──► GetVolumePreservingScale(s) → localScale
         ExtensionMethods/Transform/TransformExtensions.CharmTween.cs                       PhysXHelper/SquashStretch.cs
view ──mỗi frame──► SetAlpha(renderer, a) ──đọc color, ghi a, gán──► SpriteRenderer
         ExtensionMethods/SpriteRenderExtensions.cs
```

### GĐ6 — xong thì gọi `CharmSquashStretch` không cấp phát, chữ ký giữ nguyên

```
  caller ──CharmSquashStretch(spec, ct)──► Tween.Custom(self, a → b, ease)+ ──static (tr, s)+──► GetVolumePreservingScale(s) → localScale
```

`Runtime/Utilities/ExtensionMethods/Transform/TransformExtensions.CharmTween.cs` › `CharmSquashStretch` — cũ → mới:

```csharp
        public static Tween CharmSquashStretch(this Transform self, SquashStretchSpec spec, CancellationToken ct)
        {
            return Tween.Custom(0f, 1f, spec.duration, spell, spec.ease, useUnscaledTime: true)
                .SetCancellationToken(ct);

            void spell(float t) => self.localScale = SquashStretch.GetSquashStretch(t, spec.startScale, spec.endScale,
                AxisType.Y, CoordinateSystem.XY);
        }
```

```csharp
        /// <summary>Scale một trục từ spec.startScale tới spec.endScale theo spec.ease, trục kia bù giữ thể tích. 0 B mỗi lần gọi.</summary>
        public static Tween CharmSquashStretch(this Transform self, SquashStretchSpec spec, CancellationToken ct)
            // Nội suy thẳng s = lerp(a, b, ease(t)), không kẹp: cùng số GetSquashStretch, callback static không cần spec.
            => Tween.Custom(self, spec.startScale, spec.endScale, spec.duration,
                    static (tr, s) => tr.localScale = SquashStretch.GetVolumePreservingScale(s, AxisType.Y, CoordinateSystem.XY),
                    spec.ease, useUnscaledTime: true)
                .SetCancellationToken(ct);
```

Cùng file › `CharmFlyArc` › thêm XML doc ngay trên chữ ký:

```csharp
        /// <remarks>Một closure mỗi lần gọi (bắt from, target, controlPoint): dùng cho vật bay thưa, không cho hot path mỗi frame.</remarks>
        public static Tween CharmFlyArc(this Transform self, Vector3 target, ArcFlightSpec spec,
            CancellationToken ct)
```

`Runtime/Utilities/ExtensionMethods/UniTextExtensionMethods.cs` › `CharmCount` › thêm sau `</summary>`:

```csharp
        /// <remarks>Một closure và một StringBuilder mỗi lần gọi (bắt self, start, end, format): dùng cho số đếm thưa, không cho hot path mỗi frame.</remarks>
        public static Tween CharmCount(this UniTextBase self, int start, int end, string format, float duration,
            float startDelay = 0f, CancellationToken ct = default)
```

`Tests/PlayMode/CharmSquashStretchTests.cs` (agent) — khung đo theo `CharmJumpTests.CharmJump_DoesNotAllocate`:

| Case | Input | Kỳ vọng |
|---|---|---|
| giữa chừng | spec `(0.8, 1.2, 0.3, OutQuad)`; ở một frame giữa chừng đọc `k = tween.interpolationFactor` (đã ease) và `localScale` | `localScale` = `GetVolumePreservingScale(lerp(0.8, 1.2, k), Y, XY)` ±1e-5 |
| kết thúc | sau 0.3 s | `(1/1.2, 1.2, 1)` ±1e-6 |
| start = end | spec `(1.2, 1.2, 0.3, Linear)` | `localScale` không đổi suốt tween |
| huỷ giữa chừng | `cts.Cancel()` ở 0.15 s | `localScale` đứng yên ở giá trị lúc huỷ, `await` ném `OperationCanceledException` |
| 0 B | warm-up một lần, rồi vòng gọi `CharmSquashStretch` | `Is.Not.AllocatingGCMemory()`; đối chứng: lambda bắt biến cùng khung `Is.AllocatingGCMemory()` |

### GĐ7 — xong thì view gọi `SetAlpha` mỗi frame bay không kèm lời gọi native nào

```
  view ──mỗi frame──► SetAlpha(renderer, a) ──đọc color, ghi a, gán+──► SpriteRenderer
```

`Runtime/Utilities/ExtensionMethods/SpriteRenderExtensions.cs` › `SetAlpha` — cũ → mới:

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

```csharp
        public static void SetAlpha(this SpriteRenderer renderer, float alpha)
        {
            // Không guard null: ô thiếu nổ đúng dòng ở lần Play đầu; guard là lời gọi native mỗi frame.
            Color color = renderer.color;
            color.a = alpha;
            renderer.color = color;
        }
```

`Tests/PlayMode/SpriteRenderExtensionsTests.cs` (agent):

| Case | Input | Kỳ vọng |
|---|---|---|
| ghi alpha | màu `(0.2, 0.4, 0.6, 1)`, `SetAlpha(0.5)` | `color` = `(0.2, 0.4, 0.6, 0.5)` |
| biên | `SetAlpha(0)`, `SetAlpha(1)` | `a` 0, 1; rgb giữ |
| ghi hai lần | `SetAlpha(0.5)` rồi `SetAlpha(0.25)` | `a` 0.25; rgb giữ |
| renderer null (vế nổ) | `((SpriteRenderer)null).SetAlpha(1)` | ném `NullReferenceException` |

---

## Chuyển dự án (agent)

| Chỗ | Đổi |
|---|---|
| `docs/Runtime/Tada/Tada.md`, `.html` | tạo, `.md` mở `Code: Runtime/Utilities/Tweening/Tada`: Flow A · Thuật toán A (ba pha, mốc liền mạch) · bảng API (`Duration`, `Evaluate`, `EndLift`, `Shadow01`, `WiggleStart`, `BEAT_COUNT`) · Trước khi chạy: host tách gốc và thân (`async-presentation`: sprite 2.5D, bóng là con của gốc), `TadaSpec` phơi `[SerializeField]` trên prefab host · ví dụ nối sang bay (`EndLift`, `Shadow01(EndLift)`) · Design decisions từ comment và khối Không làm · ràng buộc núm host tự giữ (`WindupDip ≤ LiftHeight`; `LiftScale`, `WindupScale` so với cỡ host trước và sau tada) |
| `docs/Runtime/AnimationRegistry/AnimationRegistry.md`, `.html` | tạo, `.md` mở `Code: Runtime/Utilities/Common`: luồng Register khi vào sân → `IsAnyAnimating` khi chờ → `Unregister` khi rời → `Clear` theo level · bảng API · ai `Register`, ai `Unregister`, chủ `Clear` · Design decisions · bẫy: `MonoBehaviour` huỷ mà chưa gỡ vẫn được hỏi; quét tuyến tính mỗi lần hỏi |
| mỗi `.html` trên | chọn từ `.md`: kịch bản (tile cuối diễn rồi bay; host chờ hết diễn rồi mở tương tác) · cách dùng (Trước khi chạy nguyên vẹn, API consumer gọi) · flow · lưu ý (object huỷ, núm ràng buộc nhau) |
| `docs/Runtime/CharmFlyArc/CharmFlyArc.md` | §2: "`GetSquashStretch` nhận `t` đã ease" → PrimeTween nội suy `s` từ `startScale` tới `endScale` theo `ease`, callback `static` gọi `GetVolumePreservingScale(s, Y, XY)`, 0 B · §1, §3: thêm một dòng giá closure của `CharmFlyArc`, `CharmCount` (cùng chữ `<remarks>` GĐ6) |
| `Plans/FeelingPlan.md` › `Wobble`/`Jelly` | thêm "lắc đặt mốc 5 nhịp đã có: `TadaCurve`" |
| thứ tự dự án | quay mốc trace (`feel-tune`, Horcrux `Editor/FeelTrace/`, kịch bản `docs/tools/feel_trace/trace.cs` có bước tile cuối và magnet) trước khi sửa file đầu tiên; test đối chiếu xanh trước khi xoá bản tạm |
| `Assets/_CategoryJam/Tests/EditMode/TadaEquivalenceSweep.cs` (tạm) | hai bộ số (mặc định C# `TileFinaleTuning`, một bộ khác), `fromScale` 1.15, mỗi 1 ms trên `Duration + 0.05`: ba kênh `TileFinaleCurve.Evaluate` = `TadaCurve.Evaluate` tuyệt đối; `Duration`, `EndLift`, `WiggleStart`, `Shadow01(lift)` với lift 0, 0.1, L, 2L bằng nhau; xanh rồi xoá cùng `.meta` |
| `Tile.prefab`, `GoldenTile.prefab` | đóng Prefab Mode trước khi compile phần tada (Prefab Mode mở tự lưu đè YAML khi kiểu field đổi); sau cùng `git diff` hai prefab rỗng — field `TadaSpec` cùng tên field `TileFinaleTuning` |
| `Entities/Tile/TileFinaleCurve.cs`, `Tests/EditMode/TileFinaleCurveTests.cs` | xoá cùng `.meta` (19 case đã ở `TadaCurveTests`) |
| `Entities/Tile/TileFeelTuning.cs` | xoá `TileFinaleTuning` và `<summary>` của nó |
| `Tile.cs` › field `finale` | `[Tooltip("Tada tile cuối. LiftScale ≥ HeldScale của press, WindupScale ≤ PeakScale của flight, WiggleDegrees ≤ 12° (sprite 2.5D lộ cạnh).")] [SerializeField] private TadaSpec finale = new(0.35f, 0.15f, 1.2f, 6f, 0.08f, 0.04f, 1.14f, 0.05f, 0.07f);` |
| `Tile.cs` › `FlyToAsync`, `BuildFlightState`, `BuildFlightMotion`, `StepFinale` · `Tile.Editor.cs` › `PreviewFinale` | `TileFinaleCurve.` → `TadaCurve.` (`Duration`, `EndLift`, `Shadow01`, `WiggleStart`, `Evaluate`); `finale.WindupScale` giữ; thêm `using Horcrux.Runtime.Utilities.Tweening;` |
| `Scripts/Feel.md` › §7, Nghiệm thu | `TileFinaleTuning` → `TadaSpec` (field `finale`), `TileFinaleCurve` → `TadaCurve` (Horcrux, `docs/Runtime/Tada/Tada.md`), `TileFinaleCurveTests` → Horcrux `TadaCurveTests` |
| `Director/IAnimate.cs` | xoá cùng `.meta` |
| `Tile.cs`, `Box.cs`, `BoxesContainer.cs`, `PreviewBox.cs`, `CacheTileSlots.cs`, `PackBooster.cs`, `Shelf.cs` | `IAnimate` → `IAnimating`; thêm `using Horcrux.Runtime.Utilities.Common;` |
| `Director/AnimatingSystem.cs` | giữ `private readonly AnimationRegistry _registry`; `IsAnimating()` → `_registry.IsAnyAnimating`; `Register`, `UnRegister` (tên giữ cho caller) chuyển tiếp; `HandleTileCompleted` → `UnRegister(tile)`; `OnDisable`, `HandleLevelDisposed` → `_registry.Clear()`; bỏ `using` thừa; caller không đổi |
| `Hud/ComboPill.cs` | `CancellationTokenSource _drainCts` → `Tween _drain`; `OnComboChanged`: `_drain.Stop()`, count 0 → `ShowEmpty()`, còn lại `SetLabelActive(true)` rồi `_drain = bar.Play(e.WindowSeconds, 0f, e.WindowSeconds, Label(e.Count))`; `OnDestroy`: `_subscription.Dispose(); _drain.Stop();`; xoá `CancelDrain`, `using System.Threading;`, `using Cysharp.Threading.Tasks;`; thêm `using PrimeTween;`; `_labels` giữ |
| `Entities/Shelf/SingleShelfAmountBar.cs` | xoá `s_amountTexts`, `AmountText(int)`; `InitBarCount`, `UpdateCurrentAmount` → `_amountText.SetNumber(count);`; thêm `using Horcrux.Runtime.Utilities.ExtensionMethods;` nếu chưa có |
| kiểm | grep `TileFinaleCurve`, `TileFinaleTuning`, `FinalePose`, `IAnimate\b`, `_drainCts`, `CancelDrain`, `s_amountTexts`, `AmountText(` trong `Assets/_CategoryJam` = 0 · test Horcrux và dự án PASS · `feeltrace.py diff` 0 ở mọi khung, kể cả bước tada và bước chờ rảnh sau magnet |
| Plan này | xoá cùng `.meta` |
| chơi thử | tile cuối: đúng một tile nhấc, bóng nằm lại, lắc 5 nhịp, nhún rồi bay, popup thắng chờ · Preview Finale trên `Tile.prefab` trả tile về nguyên · Replay giữa tada sạch · pill combo nạp lại, hết cửa sổ về rỗng · Revive AddBoxAndClear3 chờ box bay xong · số lớp kệ Single giảm đúng · Console không `NullReferenceException` ở `SetAlpha`; hỏng: tile co lúc cất cánh, thân nhảy khung tada → bay, hai tile cùng diễn, tile kẹt nghiêng hay to, NRE `SetAlpha` (ô thiếu trên `TileView`, `BoxAnimation`, `BoxSlot`, `BoxTileSlot`, `CacheTileSlot`) |

## Không làm · Mở rộng sau

| | Phương án | Vì sao |
|---|---|---|
| bỏ | driver tween cho tada trong Horcrux | thân, bóng, scale là kênh của host; Horcrux giữ hình dạng |
| bỏ | núm tada là `sealed class` có field initializer | cụm núm chuyển động là struct `[Serializable]` như `SquashStretchSpec`; host truyền mặc định ở field |
| bỏ | `spec.Evaluate(time)` — method của struct núm | trộn núm serialize với hình dạng, hai lý do thay đổi một struct; hình là của class toán như `HopCurve` |
| bỏ | mốc lắc là `switch` expression `WiggleKey(int)` | mảng 6 phần tử cùng giá trị, đọc một dòng, không nhánh |
| bỏ | mẫu lắc `1, −1, 0.66, −0.33, 0` là núm | năm mốc là một hình; dự án sau chỉnh số, không chỉnh mẫu |
| bỏ | lắc bằng `DampedOscillator`, `HarmonicOscillator` | mô hình dao động khác, đổi cảm giác; InOutSine cho đổi chiều mượt ở mỗi ranh nhịp |
| bỏ | `AnimationRegistry` là `MonoBehaviour` hay service | sổ không có đời sống riêng; chủ `new`, giữ và `Clear` theo level |
| bỏ | so null kiểu Unity `item is Object o ? o != null : item != null` | một so sánh native mỗi mục mỗi lần hỏi, cho ca chủ đã có đường gỡ |
| bỏ | `DeferredSet` cho sổ | sổ không sửa trong lúc quét |
| bỏ | `Play` ném khi `goal ≤ 0` | `TryWriteGoalAndLabel` đã chọn log cho `Set`; hai cửa cùng luật |
| bỏ | đổi hay bỏ `PlayAsync` | 7 case giữ; caller có token dùng tiếp |
| bỏ | `docs/Runtime/ProgressBar/` | XML doc của `Play`, `PlayAsync` là chỗ caller đọc |
| bỏ | `static readonly Action<Transform, float>` như `SetJumpY` | lambda `static` inline cùng 0 B, ít một field |
| bỏ | giữ guard null trong `SetAlpha(SpriteRenderer)` | "để nổ" không có vế nổ; ô tuỳ chọn là luật của từng view, guard ở view đó |
| bỏ | cache chuỗi số trong Horcrux | dự án dùng `SetNumber`; nhãn hai số `a/b` giữ cache có trần ở dự án |
| sau | overload `TadaCurve` nhận mảng mốc lắc | khi người dùng thứ hai cần mẫu khác; bản này không sửa |
| sau | bộ đếm "đang diễn" tăng giảm thay quét tuyến tính | khi có số đo (mỗi `WaitUntil` quét vài trăm interface call mỗi frame); đổi hợp đồng `IAnimating` |
| sau | `CharmFlyArc`, `CharmCount` 0 B: kiểu state nhỏ pooled hoặc `TweenSettings<Vector3>` cho control point | khi có caller mỗi frame; giờ callback cần ba `Vector3` hoặc `format` + `StringBuilder`, bọc class state là cấp phát khác |
| sau | `ParticleSystem.ExpireAlive` cạnh `StopAndAwaitCompletion` trong `ParticleSystemExtensions.cs` | khi có caller; vệt bay hiện tan bằng `Stop(StopEmitting)` |
