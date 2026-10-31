# Plan — `TadaCurve`, `AnimationRegistry`, `ParticleSystem.ExpireAlive`, `ProgressBar.Play` và ba sửa Horcrux theo kinh nghiệm dự án

Ngày 2026-10-31. Nguồn: audit `docs/plans/Feeling_Perf_Audit.md` §4.2–4.4 của category-jam, developer chốt cùng ngày đưa **toàn bộ** lên
Horcrux, kể cả thứ mới có một người dùng.

- `Assets/_CategoryJam/Scripts/Entities/Tile/TileFinaleCurve.cs`, `TileFeelTuning.cs` (`TileFinaleTuning`) — nhịp tada của tile cuối.
- `Assets/_CategoryJam/Scripts/Entities/Tile/TileView.cs` `StopFlightTrail` — giết hạt sống lâu của dải sáng.
- `Assets/_CategoryJam/Scripts/Director/AnimatingSystem.cs`, `IAnimate.cs` — sổ "có gì đang diễn".
- Horcrux `Runtime/Utilities/ExtensionMethods/Transform/TransformExtensions.CharmTween.cs`, `TextMeshProExtensionMethods.CharmTween.cs`,
  `SpriteRenderExtensions.cs`, `Runtime/Utilities/UI/ProgressBar.cs` — ba chỗ dự án đã làm tốt hơn.

**Phân công:**
- Task 1–6 (Horcrux, code runtime): developer gõ.
- Task 7 (test), Task 8 (tài liệu), Task 9 (dự án đổi sang bản Horcrux, xoá bản tạm): agent làm.

Horcrux là submodule: Task 1–8 commit **trong `Assets/Horcrux`**, Task 9 commit ở repo dự án kèm con trỏ submodule. Plan này không phụ
thuộc `Horcrux_HopCurve_DipFlash_Plan.md`; làm trước hay sau đều compile sạch.

---

## Ngữ cảnh đã chốt

| Mục | Chốt |
|---|---|
| Người dùng | tada: `Tile.StepFinale`, `Tile.FlyToAsync`, nút Preview Finale · `ExpireAlive`: `TileView.StopFlightTrail` · registry: `AnimatingSystem` của dự án (18 chỗ gọi `IAnimate`) · `CharmSquashStretch`: chưa có caller (grep dự án và Horcrux: 0) · `SetAlpha`: `TileView` mỗi frame bay, `BoxAnimation`, `CacheTileSlot`, `BoxTileSlot` · `ProgressBar.Play`: `ComboPill` |
| Mục tiêu | cảm giác chơi **không đổi một giá trị nào**: công thức tada chép nguyên văn; ba sửa Horcrux giữ chữ ký public, bỏ cấp phát hoặc bỏ guard |
| Chỗ đặt | `Runtime/Utilities/Tweening/Tada/` (`TadaSpec.cs`, `TadaPose.cs`, `TadaCurve.cs`, `Tada.md` — utility có tài liệu thì xuống thư mục con mang tên nó) · `AnimationRegistry.cs`, `IAnimating.cs` ở `Runtime/Utilities/Common/` · `ExpireAlive` thêm vào `ParticleSystemExtensions.cs` · `Play` thêm vào `ProgressBar.cs` |
| Tên | `TadaCurve`, `TadaSpec`, `TadaPose`, `AnimationRegistry`, `IAnimating` — developer chốt 2026-10-31. Field của `TadaSpec` **giữ nguyên** tên của `TileFinaleTuning`: key YAML `finale` trên `Tile.prefab` |
| Giá đã nêu một lần | `TadaCurve` có **một** người dùng: hình (nhấc → 5 nhịp lắc giảm dần kèm phồng → lấy đà) đóng băng theo tile nhựa; dự án sau chỉnh được số, không chỉnh được mẫu lắc. Developer chấp nhận |

**Cố ý KHÔNG làm**

| Không làm | Vì |
|---|---|
| Driver tween cho tada (`Tween.Custom` lái thân, bóng, scale) | thân, bóng, scale là kênh của dự án; Horcrux giữ hình dạng, dự án giữ `StepFinale` |
| Mẫu lắc là núm | năm mốc `1, −1, 0.66, −0.33, 0` là một hình; người dùng thứ hai cần mẫu khác thì thêm overload nhận mảng, không sửa bản này |
| `CharmFlyArc`, `CharmCount` viết lại 0 B | cần ba `Vector3` hoặc một `format` trong callback, không nhét vào một target của `Tween.Custom<T>`; bọc class state là sinh một cấp phát khác. Ghi giá trong XML doc, đổi khi có caller mỗi frame |
| `AnimationRegistry` là `MonoBehaviour` hay service | registry không có đời sống riêng; chủ (manager của dự án) giữ và `Clear` theo level |
| `ProgressBar.PlayAsync` đổi hay bỏ | caller cũ (`ProgressBarTests`) giữ nguyên; `Play` là cửa hẹp, `PlayAsync` là thân cũ gọi vào `Play` rồi gắn token |
| Cache chuỗi số (`IntTextCache`) | TMP `SetText("{0}", v)` ghi vào buffer nội bộ, không tạo `string`, in số nguyên không thập phân (`TMP_Text.cs:3480–3497`): dự án dùng thẳng, không cần Horcrux (Task 9) |

**Quyết định trái trực giác**

| Chỗ | Vì |
|---|---|
| `TadaCurve.Evaluate` nhận `in TadaSpec`, không là method của struct | struct núm chỉ giữ dữ liệu như `DipSpec`; hình tada là của class toán, cùng khuôn `HopCurve` |
| `WiggleKeys` có 6 phần tử cho 5 nhịp | mốc `index + 1` ở nhịp cuối là 0 (về thẳng), chép nguyên `WiggleKey(5) => 0` của bản tạm |
| `AnimationRegistry.IsAnyAnimating` so `null` kiểu tham chiếu, không so Unity | bản tạm so tham chiếu (`IAnimate` là interface); `MonoBehaviour` đã huỷ vẫn được hỏi `IsAnimating` — property thuần, không chạm native. Giữ để không đổi hành vi; chủ gỡ đăng ký ở `HandleTileCompleted` và `Clear` theo level |
| `ExpireAlive` nhận buffer từ caller | luật buffer đầu ra: không cấp phát theo số lần gọi; caller biết `maxParticles` của hệ mình |
| `SetAlpha` bỏ guard `null` | thiếu ô Inspector nổ ở lần Play đầu bằng `NullReferenceException` trỏ đúng dòng; guard chỉ che và là một lời gọi native mỗi frame |
| `ProgressBar.Play` trả `default` khi `goal <= 0` | cùng hành vi `PlayAsync` cũ (`CompletedTask`): `LogError` rồi không chạy; caller `Stop()` trên tween `default` là no-op |
| `ComboTracker` bỏ CTS bằng `Tween.Delay` của PrimeTween thay vì bộ đếm phiên | handle dừng được ngay, 0 B, thời gian scaled như `ignoreTimeScale: false`; bộ đếm phiên để `UniTask.Delay` chạy hết cửa sổ cũ không có chủ |

---

## Đã khảo sát

| Nguồn | Lấy gì | Không lấy, vì sao |
|---|---|---|
| Horcrux `PhysXHelper/DampedOscillator.cs`, `HarmonicOscillator.cs` | — | lắc tada là 5 mốc góc đặt tay nội suy InOutSine, không phải dao động tắt dần: đổi sang oscillator là đổi cảm giác |
| Horcrux `Tweening/Easings/Easer.cs` | — | bản tạm viết `OutQuad`, `InOutSine` inline một dòng; gọi `Easer.Evaluate(EaseType, t)` thêm một switch mỗi frame cho cùng số |
| Horcrux `ExtensionMethods/ParticleSystemExtensions.cs` | chỗ đặt `ExpireAlive` cạnh `StopAndAwaitCompletion` | — |
| Horcrux `Utilities/Common/DeferredSet.cs` | — | registry chỉ `Register/Unregister/Clear/quét`, không sửa trong lúc quét; `List` đủ |
| Horcrux `TransformExtensions.Jump.cs` `SetJumpY` | khuôn `static readonly Action<Transform, Vector3>` cho callback không bắt biến | `CharmSquashStretch` chỉ cần một `float`, dùng `Tween.Custom(target, from, to, …)` thẳng |
| Horcrux `Tests/PlayMode/CharmJumpTests.cs` | khung đo 0 B (`Is.Not.AllocatingGCMemory`) | — |
| PrimeTween `Tween.Delay<T>(T target, float duration, Action<T> onComplete, bool useUnscaledTime = false)` | thay `UniTask.Delay` + CTS ở `ComboTracker` | — |
| TMP `TMP_Text.SetText(string, float)`, `SetText(string, float, float)` | ghi vào `m_TextBackingArray`, không `string` | — |
| Dự án `Tests/EditMode/TileFinaleCurveTests.cs` (19 case) | chuyển nguyên văn sang Horcrux | — |

---

## Task 1 — `Runtime/Utilities/Tweening/Tada/` (developer gõ)

### 1.1 `TadaSpec.cs`

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

### 1.2 `TadaPose.cs`

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

### 1.3 `TadaCurve.cs`

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

        /// <summary>Độ cao thân lúc cất cánh: chuyến bay bắt đầu từ đây.</summary>
        public static float EndLift(in TadaSpec spec) => spec.LiftHeight - spec.WindupDip;

        /// <summary>Độ co bóng theo độ nhấc: nhấc hết LiftHeight thì bóng co như ở đỉnh chuyến bay.</summary>
        public static float Shadow01(float lift, in TadaSpec spec)
            => spec.LiftHeight > 0f ? Mathf.Clamp01(lift / spec.LiftHeight) : 0f;

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

## Task 2 — `Runtime/Utilities/ExtensionMethods/ParticleSystemExtensions.cs` (developer gõ)

Chèn sau `StopAndAwaitCompletion`:

```csharp
        /// <summary>
        /// Kết thúc đời mọi hạt đang sống ngay frame này, không đổi trạng thái phát. Dùng cho hạt sống lâu bám theo vật (dải sáng)
        /// mà StopEmitting không giết: dải thôi vẽ theo vật, đuôi tan theo lifetime của trail.
        /// </summary>
        /// <param name="buffer">Caller cấp, dài ít nhất Max Particles của hệ; dùng lại giữa các lần gọi.</param>
        public static void ExpireAlive(this ParticleSystem self, ParticleSystem.Particle[] buffer)
        {
            var count = self.GetParticles(buffer);
            for (var i = 0; i < count; i++)
                buffer[i].remainingLifetime = 0f;
            self.SetParticles(buffer, count);
        }
```

## Task 3 — `Runtime/Utilities/Common/IAnimating.cs`, `AnimationRegistry.cs` (developer gõ)

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

        public void Unregister(IAnimating item) => _items.Remove(item);

        public void Clear() => _items.Clear();
    }
}
```

## Task 4 — `TransformExtensions.CharmTween.cs`, `TextMeshProExtensionMethods.CharmTween.cs` (developer gõ)

`CharmSquashStretch` (dòng 40–47 hiện tại) → bỏ local function, PrimeTween nội suy `s`, callback chỉ bù thể tích:

```csharp
        /// <summary>Scale một trục từ spec.startScale tới spec.endScale theo spec.ease, trục kia bù giữ thể tích. 0 B mỗi lần gọi.</summary>
        public static Tween CharmSquashStretch(this Transform self, SquashStretchSpec spec, CancellationToken ct)
            => Tween.Custom(self, spec.startScale, spec.endScale, spec.duration,
                    static (tr, s) => tr.localScale = SquashStretch.GetVolumePreservingScale(s, AxisType.Y, CoordinateSystem.XY),
                    spec.ease, useUnscaledTime: true)
                .SetCancellationToken(ct);
```

Cùng công thức với bản cũ: `GetSquashStretch(t, a, b)` = `GetVolumePreservingScale(lerp(a, b, t))`, PrimeTween tính `a + (b − a) · ease(t)`.

`CharmFlyArc` (dòng 25–38) và `CharmCount` giữ thân; thêm vào XML doc của mỗi hàm một dòng:

```csharp
        /// <remarks>Một closure mỗi lần gọi (bắt from, target, controlPoint): dùng cho vật bay thưa, không cho hot path mỗi frame.</remarks>
```

## Task 5 — `SpriteRenderExtensions.cs` (developer gõ)

Dòng 9–10 `if (renderer == null) return;` **xoá**. Thân còn ba dòng đọc màu, ghi alpha, gán màu.

## Task 6 — `Runtime/Utilities/UI/ProgressBar.cs` (developer gõ)

Thay `PlayAsync` (dòng 72–85) bằng cặp:

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

**Editor setup:** tạo file mới trong Project window của Unity (để có `.meta`); `Tada.md` tạo ở Task 8.

---

## Task 7 — Test Horcrux (agent)

EditMode `Tests/EditMode/TadaCurveTests.cs`: chuyển nguyên văn 19 case của `TileFinaleCurveTests` (đổi `TileFinaleTuning` → `TadaSpec`
dựng bằng constructor với số mặc định C# của `TileFinaleTuning`: `(0.35f, 0.15f, 1.2f, 6f, 0.08f, 0.04f, 1.14f, 0.05f, 0.07f)`); thêm:

| Hàm | Input | Kỳ vọng |
|---|---|---|
| `Evaluate(t)` so với bản tạm `TileFinaleCurve.Evaluate(t)` | t mỗi 1 ms trên cả `Duration` (chạy trước khi Task 9 xoá bản tạm) | ba kênh bằng nhau tuyệt đối |
| `Evaluate` ở ranh nhịp 5 | `LiftDuration + 5 · beat − ε` | góc → 0 (mốc `WiggleKeys[5]`) |

EditMode `Tests/EditMode/AnimationRegistryTests.cs` (fake `IAnimating` class thuần):

| Case | Kỳ vọng |
|---|---|
| rỗng | `IsAnyAnimating` false, `Count` 0 |
| một mục đang diễn, một không | true; gỡ mục đang diễn → false |
| đăng ký hai lần cùng mục | `Count` 1 |
| mục null trong danh sách | bỏ qua, không ném |
| `Clear` | `Count` 0 |

PlayMode `Tests/PlayMode/CharmSquashStretchTests.cs`, `ParticleExpireTests.cs`, bổ sung `ProgressBarTests.cs`:

| Hàm | Kiểm | Kỳ vọng |
|---|---|---|
| `CharmSquashStretch((0.8, 1.2, 0.3, OutQuad))` | `localScale` tại 0.15 s so với `GetVolumePreservingScale(lerp(0.8, 1.2, OutQuad(0.5)))`; sau 0.3 s | bằng nhau ±1e-6; `(1/1.2, 1.2, 1)` |
| `CharmSquashStretch` | GC mỗi lần gọi sau khởi động | 0 B (khung `CharmJump_DoesNotAllocate`) |
| `ExpireAlive` | hệ 3 hạt đang sống, buffer 4 → gọi → `particleCount` frame sau; hệ vẫn `isPlaying` | 0; true |
| `ProgressBar.Play(1, 0, 1, "x")` | `isAlive`; `Stop()` giữa chừng → fill đứng yên; `Play` với goal 0 | true; đứng yên ở giá trị lúc dừng; trả `default`, `isAlive` false, một `LogError` |
| `PlayAsync` vẫn pass | 7 case đang có | PASS |

## Task 8 — Tài liệu module (agent)

- `Runtime/Utilities/Tweening/Tada/Tada.md`: luồng `time → Evaluate → TadaPose → caller ghi thân/bóng/gốc`, bảng ba pha và mốc kiểm,
  "Trước khi chạy": caller cần gì (mốc nghỉ của gốc, bóng tách khỏi thân), ví dụ nối sang chuyến bay (`EndLift`, `Shadow01`).
- `Runtime/Utilities/Common/AnimationRegistry.md`: ai `Register`, ai `Unregister`, `Clear` theo level; bẫy: đăng ký `MonoBehaviour` rồi huỷ
  mà không gỡ thì `IsAnimating` vẫn được hỏi trên object đã huỷ.
- `ParticleSystemExtensions`: một mục cho `ExpireAlive` trong tài liệu có sẵn (hoặc mở `ParticleSystemExtensions.md` nếu chưa có).
- `ProgressBar`: thêm `Play` vào bảng API; `PlayAsync` ghi "khi caller có token".
- `Plans/FeelingPlan.md`: `Wobble/Jelly` ghi thêm "lắc đặt mốc 5 nhịp đã có: `TadaCurve`"; mục `TrailFeedback` trỏ `ExpireAlive`.

## Task 9 — Dự án dùng bản Horcrux, xoá bản tạm (agent)

So với mốc trace 2026-10-31 (kịch bản mở rộng, có bước tada).

| File dự án | Đổi |
|---|---|
| `Entities/Tile/TileFinaleCurve.cs` | **xoá hẳn** (cả `.meta`) |
| `Entities/Tile/TileFeelTuning.cs` | xoá `TileFinaleTuning`; tooltip riêng của tile ("Không nhỏ hơn HeldScale của press…", "Không lớn hơn PeakScale của flight…") chuyển lên `[Tooltip]` của field `finale` trong `Tile` |
| `Entities/Tile/Tile.cs` | `[SerializeField] private TileFinaleTuning finale = new();` → `[SerializeField] private TadaSpec finale = new(0.35f, 0.15f, 1.2f, 6f, 0.08f, 0.04f, 1.14f, 0.05f, 0.07f);` · `TileFinaleCurve.Duration(finale)` → `TadaCurve.Duration(finale)`, `EndLift`, `Shadow01(lift, finale)` cùng tên · `StepFinale`: `TileFinaleCurve.Evaluate(time, _flightState.FinaleScaleFrom, finale)` → `TadaCurve.Evaluate(…)`, kiểu `FinalePose` → `TadaPose` · `Tile.Editor.cs` `TileFinaleCurve.Duration(finale)` → `TadaCurve.Duration(finale)` |
| `Entities/Tile/TileView.cs` | `StopFlightTrail` ba dòng `GetParticles`/vòng for/`SetParticles` → `_flightRibbon.ExpireAlive(s_ribbonParticles);` |
| `Director/IAnimate.cs` | **xoá**; 18 chỗ `IAnimate` → `IAnimating` (`Tile`, `Box`, `BoxesContainer`, `CacheTileSlots`, `ShoppingCart`, `PreviewBox`, `Shelf`, `AnimatingSystem`), thêm `using Horcrux.Runtime.Utilities.Common;` |
| `Director/AnimatingSystem.cs` | field `List<IAnimate> _animates` → `private readonly AnimationRegistry _registry = new();`; `IsAnimating() => _registry.IsAnyAnimating`; `Register/UnRegister` gọi registry; `OnDisable`, `HandleLevelDisposed` → `_registry.Clear()`; `HandleTileCompleted` giữ |
| `Hud/ComboPill.cs` | `CancellationTokenSource _drainCts` → `Tween _drain`; `OnComboChanged`: `_drain.Stop();` rồi `_drain = bar.Play(e.WindowSeconds, 0f, e.WindowSeconds, Label(e.Count));`; `OnDestroy` `_drain.Stop()`; xoá `CancelDrain`, `using System.Threading`, `Cysharp.Threading.Tasks`, thêm `using PrimeTween;` |
| `Director/ComboTracker.cs` | `CancellationTokenSource _decayCancellationTokenSource` → `Tween _decay`; `AddPoint`: `_decay.Stop(); _decay = Tween.Delay(this, DecayDuration, static tracker => tracker.OnDecayElapsed());` · `Reset`, `Dispose`: `_decay.Stop()` · `OnDecayElapsed()`: `Count = 0; Publish(new ComboChanged(0, 0f))` · xoá `RunDecayAsync`, `CancelDecay`, `using System.Threading`, `Cysharp.Threading.Tasks` |
| `Entities/Box/CategoryBox.cs` | xoá `s_counterTexts`; `UpdateCounter`: `_counterText.SetText("{0}/{1}", linked, total);` |
| `Entities/Shelf/SingleShelfAmountBar.cs` | xoá `s_amountTexts`, `AmountText`; hai chỗ `_amountText.text = AmountText(count)` → `_amountText.SetText("{0}", count)` |
| `Entities/Shelf/Shelf.cs` | `PlayLockAnimation`: `_lockedAmountText.text = remainingTurns.ToString()` → `_lockedAmountText.SetText("{0}", remainingTurns)` |
| `Hud/ComboPill.cs` `_labels` | **giữ**: `ProgressBar` nhận chuỗi, cache có trần bằng combo cao nhất |
| `Tests/EditMode/TileFinaleCurveTests.cs` | sang Horcrux (Task 7) rồi xoá |

**Nghiệm thu:**
- grep `TileFinaleCurve`, `TileFinaleTuning`, `FinalePose`, `IAnimate\b`, `s_counterTexts`, `s_amountTexts`, `_drainCts`,
  `_decayCancellationTokenSource` trong `Assets/_CategoryJam` = 0; compile sạch; EditMode, PlayMode Horcrux và EditMode dự án PASS.
- Eval trên `Tile.prefab` đã nạp: `finale` = (2, 0.3, 1.25, 12, 0.08, 0.04, 1.14, 0.05, 0.07) — YAML hiện tại của prefab (mốc, khác mặc định C#),
  key giữ nên không đổi; `GoldenTile.prefab` cũng có khối `finale`; `git diff` hai prefab rỗng.
- Nhãn: bộ đếm "3/5", số lớp "4", số lượt khoá "2" hiện y hệt (chụp một khung trước–sau).
- **Cảm giác không đổi:** trace sau Task 9 0 chênh với mốc ở mọi khung, kể cả bước tada và bước magnet (`IsAnyAnimating` quyết lúc mở tương tác).
- Combo: chơi thử đóng hai box liên tiếp — thanh nạp lại, hết cửa sổ về 0; không có `CancellationTokenSource` nào trong `Hud/`, `Director/ComboTracker.cs`.

---

## Mở rộng sau

- `CharmFlyArc`, `CharmCount` 0 B khi có caller mỗi frame: cần một kiểu state nhỏ pooled hoặc `Tween.Custom` nhận `TweenSettings<Vector3>` cho control point.
- `AnimationRegistry` quét tuyến tính mọi mục mỗi lần hỏi; dự án đăng ký mọi tile nên mỗi `WaitUntil` quét vài trăm interface call mỗi frame
  (memory perf-audit 2026-10-25, nhóm B). Đổi sang bộ đếm "đang diễn" tăng giảm khi mục đổi trạng thái là đổi hợp đồng `IAnimating`; chờ số đo.
