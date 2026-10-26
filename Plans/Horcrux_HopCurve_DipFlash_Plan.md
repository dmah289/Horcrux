# Plan — `HopCurve`, `DipSpec`, `FlashSpec`, `SinkSpec`, `PopSpec` và hai hàm `Interpolator` lên Horcrux

Ngày 2026-10-26, bổ sung cùng ngày. Nguồn: bản tạm trong dự án category-jam, đợt feeling gameplay (dự án
`docs/plans/Gameplay_Feel_Plan.md`, `Gameplay_Feel_Impl_Plan.md`):

- `Assets/_CategoryJam/Gameplay/Entities/Tile/TileFlightCurve.cs` — hình dạng cú bay và cú nhảy lên hàng trước của tile.
- `Assets/_CategoryJam/Gameplay/Entities/Box/BoxFeelTuning.cs` — `DipTuning`, `FlashTuning`, `BoxFeelCurve.DipScaleY`, ba núm `LidSettle*`.
- `Assets/_CategoryJam/Gameplay/Entities/Box/BoxAnimation.cs` — `InsertFlash` (private static), nắp lún khi chạm, khung nảy.
- `Assets/_CategoryJam/Gameplay/Entities/TileSlot/CacheTileSlot.cs` — `SlotSpringTuning`, `PlaySpring`.
- `Assets/_CategoryJam/Gameplay/Entities/Box/BoxReceiveTuning.cs` — ba núm `FramePunch*`, `CounterPopTuning`.
- `Assets/_CategoryJam/Gameplay/Entities/Shelf/ShelfPromoteTuning.cs` — `LandPitch`.

Mọi thứ trong plan đã có hai người dùng thật và đã chơi thử. Không cái nào gọi tên type của dự án ngoài hai class núm
`TileFlightTuning`, `TileRiseTuning`; bản Horcrux nhận số thay cho class núm.

**Phân công:**
- Task 1 (Horcrux, code): developer gõ.
- Task 2 (test Horcrux), Task 3 (tài liệu module), Task 4 (dự án đổi sang bản Horcrux và xoá bản tạm): agent làm.

Horcrux là submodule: Task 1–3 commit **trong `Assets/Horcrux`**, Task 4 commit ở repo dự án kèm con trỏ submodule.

---

## Ngữ cảnh đã chốt

| Mục | Chốt |
|---|---|
| Người dùng | dự án này: `Tile.StepFlight`, `Tile.StepRise`, `BoxAnimation.PlayReceive`, `BoxAnimation.PlayComplete`, `CacheTileSlot.PlaySpring`, `CategoryBox` bộ đếm, `ShelfPromoteTuning.LandPitch` · foods_jam `CacheHole.PlaySpringAnimation` cùng hình lún vị trí · dự án sau có vật nhảy thấp có bóng, vật nhận lún, sprite loé, số nảy |
| Mục tiêu | đưa **toán thuần**, **struct núm** và hai extension một lời gọi lên Horcrux, dự án gọi thẳng; cảm giác chơi **không đổi một giá trị nào**: mọi biểu thức chép nguyên văn, kể cả thứ tự phép nhân (float) |
| Chỗ đặt | `HopCurve` cạnh `SquashStretch` ở `Runtime/Utilities/PhysXHelper/` (toán thuần, không tween) · `Interpolator.SmoothStep`, `Interpolator.OffsetFromCenter` thêm vào class có sẵn · `DipSpec`, `FlashSpec`, `SinkSpec`, `PopSpec` ở `Runtime/Utilities/Tweening/` namespace `Horcrux.Runtime.Utilities.Tweening` như `SquashStretchSpec`, `ArcFlightSpec` · `InsertFlash` là extension của `PrimeTween.Sequence`, `CharmSinkLocalY` và `CharmPop` là extension của `Transform`, cả hai ở `Runtime/Utilities/ExtensionMethods/` |
| Tên | đề xuất, **developer đổi tuỳ ý trước khi gõ**: `HopCurve` (cú nhảy thấp, không phải đạn đạo), hậu tố `Spec` theo `SquashStretchSpec` thay `Tuning` của dự án. Tên field trong các struct **giữ nguyên** theo bản tạm, xem quyết định trái trực giác |
| Hợp đồng `CharmSinkLocalY`, `CharmPop` | như `CharmJump`: trả `Sequence` hoặc `Tween`, **không nhận `ct`**, thời gian scaled. Cả hai được nhét vào một `Sequence` lớn hơn (nắp lún nằm trong sequence đóng box, khung nảy nằm trong sequence nhận tile) mà PrimeTween không cho tween con mang token |

**Cố ý KHÔNG làm**

| Không làm | Vì |
|---|---|
| Đưa `TileFlightTuning`, `TileRiseTuning` lên Horcrux | chứa bóng, lắc, tan thân — quyết định của tile nhựa 2.5D, không phải của cú nhảy; Horcrux nhận số |
| Driver tween cho cú nhảy (một `Tween.Custom` lái mọi kênh) | driver ghi lên thân, bóng, màu của **dự án**; mỗi dự án một bộ kênh. Horcrux giữ hình dạng, dự án giữ vòng `Step` |
| Driver tween cho lún (`Tween DipScale(Transform, …)`) | `Tween.Custom<T>` cần một class giữ state (scale nghỉ) để callback `static`; bọc lại là sinh class trung gian cho một dòng `ApplyScaleY` |
| Gộp `HopCurve.Lift` vào `JumpArc` | `JumpArc` lo hai đầu lệch cao; `Lift` là ca hai đầu ngang nhau, đọc được trong một dòng. Test giữ hai bản khớp nhau |
| Field `UpEase` trong `SinkSpec` | cả ba người dùng về nghỉ bằng OutBack; thêm núm khi có người dùng thứ hai về khác |
| `CharmPop` thay `Tween.PunchScale` của PrimeTween | khác hình: punch dao động quanh nghỉ, pop bật tức thì lên đỉnh rồi về một nhịp OutBack. Ai cần rung quanh nghỉ thì dùng thẳng PrimeTween |
| Đưa nở ba nhịp `TileView.PlayPopIn` lên | foods_jam có bản giống hệt nhưng hình này bóp méo thân nhựa, đang chờ làm lại ở dự án; đưa lên là đóng băng thứ sắp đổi |
| Đưa lắc xoay `PlayReject`, `PlayLandRock` lên | hai bản khác chia thời gian (1:2:1 với 1:1:1) và biên độ dội (1.0 với 0.66); struct chung mang năm số. Chờ người dùng thứ ba |

**Quyết định trái trực giác**

| Chỗ | Vì |
|---|---|
| Field của `DipSpec`, `FlashSpec`, `SinkSpec`, `PopSpec` PascalCase (`ScaleY`, `PeakAlpha`, `Depth`, `Scale`…), khác `SquashStretchSpec` camelCase | tên field là **key YAML** trên `CategoryBox.prefab`, `CacheTileSlot.prefab`; đổi kiểu struct thì Unity giữ giá trị vì khớp theo tên field, đổi tên field thì giá trị rơi về 0 im lặng. Casing PascalCase cũng đúng luật "public field của data class" |
| `LeanRamp`, `LeanToward` nhận mốc và ngưỡng làm tham số **bắt buộc** | 0.3 · 0.75 · 0.05 là số cảm giác của tile nhựa, không phải của cơ chế; mặc định trong Horcrux là đóng băng nháp của một dự án thành chuẩn |
| `SmoothStep` clamp đầu vào | bản tạm clamp; `LeanRamp` gọi với `(1 − t) / (1 − fallStart)` có thể vượt 1 ở biên |
| `SinkSpec.DownEase` là núm, không cố định | ô cache xuống InSine như khay bị đè, nắp xuống OutQuad như vật rơi chạm đáy. Bẫy: thiếu key `DownEase` trong YAML thì giá trị là 0, tức `Ease.Default`, PrimeTween đọc thành OutQuad. Task 4 ghi key thẳng vào prefab |
| `SinkSpec` dời vị trí, không bóp scale | luật vật cứng không biến dạng: khay nhựa và nắp lún xuống, không bẹp ra. Cần bẹp thì dùng `DipSpec` |
| `CharmPop` lấy trễ bằng `startDelay` của chính tween | trễ là một phần của cú nảy, nằm trong `PopSpec` để một nguồn. Khung nảy hiện chèn ở `at + FramePunchDelay`; sau Task 4 chèn ở `at` với `startDelay`. Trong cả hai cách tween ở trạng thái "trước" cho tới mốc nảy nên không ghi scale; Task 2 kiểm bằng test, Task 4 kiểm bằng trace |
| `CharmSinkLocalY` nhận `restLocalY`, không đọc `localPosition.y` lúc gọi | lún gọi lại khi đang lún thì đọc lúc gọi sẽ trôi mốc nghỉ xuống dần; luật "tween trên giá trị nghỉ đọc ở Awake" |
| `InsertFlash` bật renderer lúc bắt đầu loé, tắt khi alpha về 0 | sprite loé nằm sẵn trên vật ở alpha 0, vẫn là một quad trong suốt vẽ mỗi frame. Dự án đã đo 2026-10-26: tắt `frame_flash` và vệt sáng nghỉ trên 2 box bớt 1 batch, 2 SetPass; trace đối chứng 0 chênh. Mốc tắt là `(at + Rise) + Fall`, cùng thứ tự phép cộng với lúc tween về 0 kết thúc |
| `OffsetFromCenter` ở `Interpolator` | hàm một dòng, cùng họ ánh xạ chỉ số sang giá trị như `Remap`; developer chuyển sang class khác tuỳ ý |

---

## Đã khảo sát

| Nguồn | Lấy gì | Không lấy, vì sao |
|---|---|---|
| Horcrux `PhysXHelper/Interpolator.cs` | chỗ đặt `SmoothStep` cạnh `SmootherStep`; `FeelingPlan.md` đã ghi "còn thiếu SmoothStep cubic" | — |
| Horcrux `ExtensionMethods/Transform/JumpArc.cs` | test chéo: `Lift(t, peak)` = `JumpArc.EvaluateHeight(0, ComputeArcCoefficient(0, 0, peak), t)` | không thay `Lift` (bảng trên) |
| Horcrux `PhysXHelper/SquashStretch.cs` | `BoxAnimation.ApplyScaleY` đã dùng `GetVolumePreservingScale`; `DipSpec` chỉ trả hệ số y | — |
| Horcrux `Tweening/Easings/Easer.cs` | — | `DipSpec` dùng `PrimeTween.Easing.Evaluate` như bản tạm: cùng một hàm ease với tween đang chạy, khác bản thì lún lệch tween |
| Horcrux `ExtensionMethods/Transform/SquashStretchSpec.cs`, `ArcFlightSpec.cs` | namespace và hậu tố `Spec` | camelCase (bảng trên) |
| Horcrux `ExtensionMethods/Transform/TransformExtensions.Jump.cs`, `Tweening/CharmJump.md` | hợp đồng trả `Sequence`, không `ct`; file partial riêng cho mỗi họ extension | — |
| Horcrux `ExtensionMethods/` | chưa có extension nào cho `PrimeTween.Sequence`; dự án cũng không (grep `SequenceExtensions`: 0) | — |
| PrimeTween `Internal/UnmanagedTweenData.cs` `calcTFromElapsedTimeTotal` | trễ chèn (`waitDelay`) và `startDelay` đều trả trạng thái "trước" cho tới mốc chạy | — |
| foods_jam `CacheHole.PlaySpringAnimation` | người dùng thứ hai ngoài dự án của hình lún vị trí: `DOLocalMoveY` xuống InSine rồi về OutBack, hai bộ số | — |
| foods_jam, mahjong-jam `DOPunchScale` | — | khác hình `CharmPop` (bảng trên) |
| `HorizontalGrid` dự án | cùng công thức lệch từ tâm | không đổi trong plan này: viết `-(n − 1) · space / 2 + i · space`, thứ tự phép tính khác nên có thể lệch 1 ulp; đổi khi có trace riêng |

---

## Task 1 — Horcrux (developer gõ)

Thứ tự: 1.1 trước vì 1.2 gọi nó; 1.6, 1.7 trước 1.8. Xong mỗi bước compile sạch; dự án chưa đổi vẫn chạy như cũ vì chỉ **thêm**.

### 1.1 `Runtime/Utilities/PhysXHelper/Interpolator.cs` — thêm `SmoothStep` và `OffsetFromCenter`

Chèn ngay trên `SmootherStep` (dòng hiện tại có `/// <summary>Perlin's quintic smoothstep`):

```csharp
        /// <summary>Cubic smoothstep: zero velocity at both ends.</summary>
        /// <remarks>S(t) = 3t^2 - 2t^3 = t^2 (3 - 2t)</remarks>
        /// <param name="t">Progress, clamped to [0,1].</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float SmoothStep(float t)
        {
            t = Mathf.Clamp01(t);
            return t * t * (3f - 2f * t);
        }

        /// <summary>Offset of item <paramref name="index"/> in a row of <paramref name="count"/> items spaced by
        /// <paramref name="step"/>, centered on 0: the middle item is 0, the row is symmetric.</summary>
        /// <remarks>(index - (count - 1) / 2) * step</remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float OffsetFromCenter(int index, int count, float step) => (index - (count - 1) * 0.5f) * step;

```

### 1.2 `Runtime/Utilities/PhysXHelper/HopCurve.cs` — file mới

```csharp
using System.Runtime.CompilerServices;
using UnityEngine;

namespace Horcrux.Runtime.Utilities.PhysXHelper
{
    /// <summary>
    /// Hình dạng một cú nhảy thấp lái bằng một t tuyến tính: gốc đi đường đất, thân nhấc theo parabol, nghiêng vào hướng đi,
    /// scale và alpha đổi ở đoạn cuối. Mọi kênh đọc cùng t để cùng nhịp; caller giữ vòng Step và ghi lên object của mình.
    /// </summary>
    public static class HopCurve
    {
        /// <summary>Quãng đường đổi theo vị trí thì núm là tốc độ: thời lượng = quãng / tốc độ, kẹp hai đầu.</summary>
        public static float DurationForSpeed(float distance, float speed, float minDuration, float maxDuration)
            => Mathf.Clamp(distance / speed, minDuration, maxDuration);

        /// <summary>Đỉnh cung tỉ lệ quãng đường, kẹp hai đầu; đơn vị world.</summary>
        public static float PeakForDistance(float distance, float heightPerUnit, float minHeight, float maxHeight)
            => Mathf.Clamp(distance * heightPerUnit, minHeight, maxHeight);

        /// <summary>Độ cao world → local của gốc để thân nhấc trong không gian gốc; lossy không dương thì giữ nguyên.</summary>
        public static float PeakToLocal(float peakWorld, float lossyScaleY) => peakWorld / (lossyScaleY > 0f ? lossyScaleY : 1f);

        /// <summary>Parabol 0 → peak → 0, đỉnh ở t = 0.5; cùng JumpArc khi hai đầu ngang nhau (K = 4·peak).</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float Lift(float t, float peak) => peak * 4f * t * (1f - t);

        /// <summary>Góc để mép trước thấp như thẻ bị búng: dx âm (sang trái) cho góc dương. |dx| dưới minDx thì 0.</summary>
        public static float LeanToward(float dx, float degrees, float minDx) => Mathf.Abs(dx) < minDx ? 0f : -degrees * Mathf.Sign(dx);

        /// <summary>0 ở hai đầu; SmoothStep lên 1 tới riseEnd, giữ 1 tới fallStart, SmoothStep về 0 ở t = 1: cất cánh và đáp phẳng.</summary>
        public static float LeanRamp(float t, float riseEnd, float fallStart)
        {
            if (t <= 0f || t >= 1f) return 0f;
            if (t < riseEnd) return Interpolator.SmoothStep(t / riseEnd);
            if (t < fallStart) return 1f;
            return Interpolator.SmoothStep((1f - t) / (1f - fallStart));
        }

        /// <summary>Giữ from tới holdEnd rồi InQuad về to ở t = 1.</summary>
        public static float HoldThenEaseIn(float t, float holdEnd, float from, float to)
        {
            if (t <= holdEnd) return from;
            var k = (t - holdEnd) / (1f - holdEnd);
            return Mathf.Lerp(from, to, k * k);
        }

        /// <summary>1 tới fadeStart rồi InQuad về 0 ở t = 1.</summary>
        public static float FadeOutTail(float t, float fadeStart)
        {
            if (t <= fadeStart) return 1f;
            var k = (t - fadeStart) / (1f - fadeStart);
            return 1f - k * k;
        }

        /// <summary>t của cả cú (có lấy đà) → k của cung nhảy: 0 suốt pha lấy đà, rồi 0 → 1 tuyến tính.</summary>
        public static float AfterAnticipation(float t, float anticipationEnd)
            => t <= anticipationEnd ? 0f : Mathf.Clamp01((t - anticipationEnd) / (1f - anticipationEnd));

        /// <summary>Thân so với đường đất: hạ tới −sinkDepth ở anticipationEnd (lấy đà), rồi parabol từ −sinkDepth về 0 với đỉnh peak; 0 ở hai đầu.</summary>
        public static float LiftWithAnticipation(float t, float peak, float sinkDepth, float anticipationEnd)
        {
            if (t <= 0f || t >= 1f) return 0f;
            if (t <= anticipationEnd) return -sinkDepth * Interpolator.SmoothStep(t / anticipationEnd);
            var k = AfterAnticipation(t, anticipationEnd);
            return Mathf.Lerp(-sinkDepth, 0f, k) + Lift(k, peak);
        }

        /// <summary>OutQuad 0 → 1 xong ở k = end, giữ 1 sau đó: vật tới gần camera to và sáng nhanh lúc đầu.</summary>
        public static float EaseOutUntil(float k, float end)
        {
            var x = Mathf.Clamp01(k / end);
            return 1f - (1f - x) * (1f - x);
        }
    }
}
```

### 1.3 `Runtime/Utilities/Tweening/DipSpec.cs` — file mới

```csharp
using System;
using PrimeTween;
using UnityEngine;

namespace Horcrux.Runtime.Utilities.Tweening
{
    /// <summary>
    /// Lún một trục: hệ số ScaleY xuống trong DownDuration (OutQuad) rồi về 1 trong UpDuration (OutBack, vượt 1 một nhịp).
    /// Caller nhân hệ số lên scale nghỉ của vật và bù trục kia để giữ thể tích (SquashStretch).
    /// </summary>
    [Serializable]
    public struct DipSpec
    {
        [Range(0.5f, 1f)] public float ScaleY;
        [Min(0.01f)] public float DownDuration;
        [Min(0.01f)] public float UpDuration;

        public DipSpec(float scaleY, float downDuration, float upDuration)
        {
            ScaleY = scaleY;
            DownDuration = downDuration;
            UpDuration = upDuration;
        }

        public float Duration => DownDuration + UpDuration;

        /// <summary>t ∈ [0, 1] trên cả Duration → hệ số y; vượt 1 ở pha về vì OutBack.</summary>
        public float EvaluateScaleY(float t)
        {
            var downEnd = DownDuration / Duration;
            return t < downEnd
                ? Mathf.Lerp(1f, ScaleY, Easing.Evaluate(t / downEnd, Ease.OutQuad))
                : Mathf.LerpUnclamped(ScaleY, 1f, Easing.Evaluate((t - downEnd) / (1f - downEnd), Ease.OutBack));
        }
    }
}
```

### 1.4 `Runtime/Utilities/Tweening/FlashSpec.cs` — file mới

```csharp
using System;
using UnityEngine;

namespace Horcrux.Runtime.Utilities.Tweening
{
    /// <summary>Loé một sprite: alpha lên PeakAlpha trong RiseDuration (OutQuad) rồi về 0 trong FallDuration (InQuad).</summary>
    [Serializable]
    public struct FlashSpec
    {
        [Range(0f, 1f)] public float PeakAlpha;
        [Min(0.01f)] public float RiseDuration;
        [Min(0.01f)] public float FallDuration;

        public FlashSpec(float peakAlpha, float riseDuration, float fallDuration)
        {
            PeakAlpha = peakAlpha;
            RiseDuration = riseDuration;
            FallDuration = fallDuration;
        }
    }
}
```

### 1.5 `Runtime/Utilities/ExtensionMethods/SequenceExtensions.cs` — file mới

```csharp
using Horcrux.Runtime.Utilities.Tweening;
using PrimeTween;
using UnityEngine;

namespace Horcrux.Runtime.Utilities.ExtensionMethods
{
    public static class SequenceExtensions
    {
        /// <summary>
        /// Chèn một cú loé vào sequence ở mốc at: bật renderer, alpha lên đỉnh rồi về 0, tắt renderer.
        /// Sprite nghỉ ở alpha 0 với renderer tắt: alpha 0 vẫn tốn một lượt vẽ trong suốt.
        /// </summary>
        public static Sequence InsertFlash(this Sequence sequence, float at, SpriteRenderer target, in FlashSpec flash)
        {
            sequence.InsertCallback(at, target, static renderer => renderer.enabled = true);
            sequence.Insert(at, Tween.Alpha(target, flash.PeakAlpha, flash.RiseDuration, Ease.OutQuad));
            sequence.Insert(at + flash.RiseDuration, Tween.Alpha(target, 0f, flash.FallDuration, Ease.InQuad));
            return sequence.InsertCallback(at + flash.RiseDuration + flash.FallDuration, target, static renderer => renderer.enabled = false);
        }
    }
}
```

### 1.6 `Runtime/Utilities/Tweening/SinkSpec.cs` — file mới

```csharp
using System;
using PrimeTween;
using UnityEngine;

namespace Horcrux.Runtime.Utilities.Tweening
{
    /// <summary>
    /// Lún theo vị trí: y xuống Depth trong DownDuration theo DownEase rồi về nghỉ trong UpDuration (OutBack, vượt một nhịp).
    /// Vật cứng dời chỗ, không bẹp; cần bẹp thì dùng DipSpec.
    /// </summary>
    [Serializable]
    public struct SinkSpec
    {
        [Min(0f)] public float Depth;
        [Min(0.01f)] public float DownDuration;
        [Min(0.01f)] public float UpDuration;
        [Tooltip("InSine: khay bị đè. OutQuad: vật rơi chạm đáy.")]
        public Ease DownEase;

        public SinkSpec(float depth, float downDuration, float upDuration, Ease downEase)
        {
            Depth = depth;
            DownDuration = downDuration;
            UpDuration = upDuration;
            DownEase = downEase;
        }

        public float Duration => DownDuration + UpDuration;
    }
}
```

### 1.7 `Runtime/Utilities/Tweening/PopSpec.cs` — file mới

```csharp
using System;
using UnityEngine;

namespace Horcrux.Runtime.Utilities.Tweening
{
    /// <summary>Nảy trên scale nghỉ: sau Delay bật lên nghỉ × Scale rồi về nghỉ trong Duration (OutBack).</summary>
    [Serializable]
    public struct PopSpec
    {
        [Range(1f, 1.5f)] public float Scale;
        [Min(0f)] public float Delay;
        [Min(0.01f)] public float Duration;

        public PopSpec(float scale, float delay, float duration)
        {
            Scale = scale;
            Delay = delay;
            Duration = duration;
        }
    }
}
```

### 1.8 `Runtime/Utilities/ExtensionMethods/Transform/TransformExtensions.Accent.cs` — file mới

```csharp
using Horcrux.Runtime.Utilities.Tweening;
using PrimeTween;
using UnityEngine;

namespace Horcrux.Runtime.Utilities.ExtensionMethods
{
    public static partial class TransformExtensions
    {
        /// <summary>Lún local y từ mốc nghỉ rồi về đúng mốc nghỉ. restLocalY đọc ở Awake, không đọc lúc gọi.</summary>
        public static Sequence CharmSinkLocalY(this Transform self, float restLocalY, in SinkSpec spec)
            => Sequence.Create()
                .Chain(Tween.LocalPositionY(self, restLocalY - spec.Depth, spec.DownDuration, spec.DownEase))
                .Chain(Tween.LocalPositionY(self, restLocalY, spec.UpDuration, Ease.OutBack));

        /// <summary>Nảy scale trên mốc nghỉ: chờ spec.Delay, bật lên restScale × spec.Scale, về restScale OutBack.</summary>
        public static Tween CharmPop(this Transform self, Vector3 restScale, in PopSpec spec)
            => Tween.Scale(self, restScale * spec.Scale, restScale, spec.Duration, Ease.OutBack, startDelay: spec.Delay);
    }
}
```

**Editor setup:** tạo file trong Project window của Unity (để có `.meta`), không cần kéo thả gì.

---

## Task 2 — Test Horcrux (agent)

EditMode: `Tests/EditMode/HopCurveTests.cs`, `DipSpecTests.cs`, `InterpolatorSmoothStepTests` và `OffsetFromCenter` (gộp vào file
nào có sẵn thì gộp). Chuyển các case thuần toán của dự án (`TileFlightCurveTests`, `BoxFeelCurveTests`), thêm case biên:

| Hàm | Input | Kỳ vọng |
|---|---|---|
| `SmoothStep` | 0 · 0.5 · 1 · −1 · 2 | 0 · 0.5 · 1 · 0 · 1 |
| `OffsetFromCenter(i, 3, 0.03)` | i = 0 · 1 · 2 | −0.03 · 0 · 0.03 |
| `OffsetFromCenter` | (0, 1, 5) · (0, 4, 1) · (3, 4, 1) | 0 · −1.5 · 1.5 |
| `OffsetFromCenter(i, n, s)` so với `(i − (n − 1) * 0.5f) * s` | i ∈ 0…4, n ∈ 1…5, s ∈ {0.03, 0.7} | bằng nhau tuyệt đối, khoá thứ tự phép tính mà `LandPitch` dựa vào |
| `Lift` | t 0 và 1; t 0.5; t 0.2 so với 0.8; t ∈ {0, 0.25, 0.5, 0.9, 1} | 0; peak; bằng nhau; bằng `JumpArc.EvaluateHeight(0, ComputeArcCoefficient(0, 0, peak), t)` |
| `LeanRamp(t, 0.3, 0.75)` | 0 · 1 · 0.3 · 0.5 · 0.75; dãy 0.05 bước tới 0.3 | 0 · 0 · 1 · 1 · 1; tăng đơn điệu |
| `LeanToward(dx, 10, 0.05)` | −2 · 2 · ±0.049 · 0.05 | 10 · −10 · 0 · −10 |
| `HoldThenEaseIn(t, 0.6, 1.1, 1.2)` | 0.3 · 0.6 · 1 | 1.1 · 1.1 · 1.2 |
| `FadeOutTail(t, 0.85)` | 0.5 · 0.85 · 1 | 1 · 1 · 0 |
| `AfterAnticipation(t, 0.14)` | 0.1 · 0.14 · 1 | 0 · 0 · 1 |
| `LiftWithAnticipation(t, 1.1, 0.04, 0.14)` | 0 · 1 · 0.14 · 0.57 | 0 · 0 · −0.04 · 1.1 − 0.02 (±1e-3) |
| `EaseOutUntil(k, 0.6)` | 0 · 0.3 · 0.6 · 1 | 0 · 0.75 · 1 · 1 |
| `PeakToLocal` | (1.2, 0.6) · (1.2, 0) · (1.2, −1) | 2 · 1.2 · 1.2 |
| `DurationForSpeed`, `PeakForDistance` | dưới kẹp, trong, trên kẹp | min · giá trị · max |
| `DipSpec(0.86, 0.05, 0.22).EvaluateScaleY` | 0 · downEnd · 1 · giữa pha xuống · 70 % pha về | 1 · 0.86 · 1 · trong (0.86, 1) · > 1 |
| `SinkSpec(0.12, 0.06, 0.18, InSine).Duration` | — | 0.24 |
| `new SinkSpec()` | `DownEase` | `Ease.Default`: test ghi lại cái bẫy YAML thiếu key |

PlayMode: `Tests/PlayMode/CharmAccentTests.cs`, cùng khung `CharmJumpTests` (thời gian scaled, đo GC bằng recorder như ở đó):

| Hàm | Kiểm | Kỳ vọng |
|---|---|---|
| `CharmSinkLocalY(rest 0.5, (0.12, 0.06, 0.18, InSine))` | y lúc 0.06 s · y sau `Duration` · tween hoàn tất | ≈ 0.38 · đúng 0.5 · `isAlive` false |
| `CharmSinkLocalY` chèn vào `Sequence` ở mốc 0.1 | y trước 0.1 s | đúng mốc nghỉ, chưa lún |
| `CharmPop(rest (2,2,2), (1.25, 0.06, 0.15))` | scale trước 0.06 s · ngay sau 0.06 s · sau 0.21 s | đúng (2,2,2) · ≈ (2.5,2.5,2.5) · đúng (2,2,2) |
| `CharmPop` chèn vào `Sequence` ở mốc 0 | scale trước `Delay` | đúng nghỉ: chứng minh `startDelay` trong sequence không ghi scale sớm |
| cả hai | GC mỗi lần gọi sau lượt khởi động | 0 B |

`InsertFlash`, PlayMode trong `CharmAccentTests.cs`: renderer tắt trước `at`, bật trong suốt pha loé, tắt sau
`at + Rise + Fall`; alpha theo thời gian Task 4 kiểm bằng trace.

## Task 3 — Tài liệu module (agent)

- `Runtime/Utilities/PhysXHelper/HopCurve.md`: bảng kênh → hàm → hình dạng, công thức, mốc kiểm.
- `Runtime/Utilities/Tweening/CharmAccent.md`: `SinkSpec`, `PopSpec`, `CharmSinkLocalY`, `CharmPop`, cùng khung `CharmJump.md`
  (hợp đồng không `ct`, ví dụ chèn vào `Sequence`, bẫy `DownEase` thiếu key, vì sao `restLocalY` là tham số).
- Một mục cho `DipSpec`, `FlashSpec`, `InsertFlash` trong cùng file `CharmAccent.md`: cả ba là nhịp nhấn của vật nhận.
- `Interpolator`: thêm `SmoothStep`, `OffsetFromCenter` vào tài liệu nếu class có tài liệu riêng.
- `Plans/FeelingPlan.md`: `SmoothStep` đã có; `Anticipation` có bản hình dạng ở `HopCurve.LiftWithAnticipation`; `Overshoot` có hai
  bản dùng sẵn `CharmPop`, `CharmSinkLocalY`; `ProgressPop` trỏ `CharmPop`.

## Task 4 — Dự án dùng bản Horcrux, xoá bản tạm (agent)

Trước khi sửa: quay mốc trace (Horcrux `Editor/FeelTrace/FeelTrace.md`, kịch bản `docs/tools/feel_trace/trace.cs`) ở hai
level, mỗi level hai lần cùng chữ ký.

| File dự án | Đổi |
|---|---|
| `Entities/Tile/Tile.cs` | `TileFlightCurve.X` → `HopCurve.Y` theo bảng dưới; ba hằng cảm giác thành `private const` trong `Tile` (`LEAN_RISE_END = 0.3f`, `LEAN_FALL_START = 0.75f`, `BODY_SCALE_START = 0.6f`, `LEAN_MIN_DX = 0.05f`) |
| `Entities/Tile/TileFlightCurve.cs` | xoá (Project window) |
| `Entities/Box/BoxFeelTuning.cs` | xoá `DipTuning`, `FlashTuning`, `BoxFeelCurve`; ba núm `LidSettleDepth`, `LidSettleDownDuration`, `LidSettleUpDuration` → `public SinkSpec LidSettle = new(0.05f, 0.04f, 0.16f, Ease.OutQuad);`; đổi tên file thành `BoxCompleteTuning.cs` (Project window) vì chỉ còn class đó |
| `Entities/Box/BoxReceiveTuning.cs` | kiểu field `DipTuning` → `DipSpec`, `FlashTuning` → `FlashSpec`; ba núm `FramePunchScale`, `FramePunchDelay`, `FramePunchDuration` → `public PopSpec FramePunch = new(1.10f, 0.03f, 0.18f);` giữ tooltip; xoá `CounterPopTuning` |
| `Entities/Box/BoxAnimation.cs` | `BoxFeelCurve.DipScaleY(t, dip)` → `dip.EvaluateScaleY(t)`; `InsertFlash(seq, at, r, f)` private → `seq.InsertFlash(at, r, f)`, xoá bản private; bỏ biến `lidSettledLocalPos` và hai dòng `Insert` nắp lún → `.Insert(touch, lidTransform.CharmSinkLocalY(lidClosedLocalPos.y, complete.LidSettle))`; khung nảy → `sequence.Insert(at, frame.transform.CharmPop(_frameRestScale, receive.FramePunch))` |
| `Entities/Box/CategoryBox.cs` | `CounterPopTuning counterPop` → `PopSpec counterPop`, tên field giữ; `Tween.Scale(… startDelay: counterPop.Delay)` → `_counterText.transform.CharmPop(_counterOriginalScale, counterPop)` |
| `Entities/TileSlot/CacheTileSlot.cs` | xoá `SlotSpringTuning`; `landSpring = new(0.12f, 0.06f, 0.18f, Ease.InSine)`, `shiftSpring = new(0.08f, 0.06f, 0.14f, Ease.InSine)` kiểu `SinkSpec`; `PlaySpring` → `transform.CharmSinkLocalY(_restLocalY, spring)`, vẫn không giữ handle như hiện tại |
| `Entities/Shelf/ShelfPromoteTuning.cs` | `LandPitch` → `1f + Interpolator.OffsetFromCenter(slotIndex, capacity, PitchStep)` |
| `Tests/EditMode/TileFlightCurveTests.cs`, `BoxFeelCurveTests.cs` | case thuần toán sang Horcrux (Task 2); còn lại `LandPitch_CentersOnMiddleSlot` → đổi file thành `ShelfPromoteTuningTests.cs` |

| Bản tạm | Bản Horcrux |
|---|---|
| `TileFlightCurve.Duration(d, flight)` | `HopCurve.DurationForSpeed(d, flight.Speed, flight.MinDuration, flight.MaxDuration)` |
| `TileFlightCurve.Peak(d, mult, flight)` | `HopCurve.PeakForDistance(d, flight.HeightPerUnit, flight.MinHeight, flight.MaxHeight) * mult` |
| `TileFlightCurve.PeakToLocal` · `Lift` | `HopCurve.PeakToLocal` · `HopCurve.Lift` |
| `TileFlightCurve.LeanToward(dx, deg)` | `HopCurve.LeanToward(dx, deg, LEAN_MIN_DX)` |
| `TileFlightCurve.LeanRamp(t)` | `HopCurve.LeanRamp(t, LEAN_RISE_END, LEAN_FALL_START)` |
| `TileFlightCurve.BodyScale(t, a, b)` | `HopCurve.HoldThenEaseIn(t, BODY_SCALE_START, a, b)` |
| `TileFlightCurve.BodyFade` · `RiseJump` · `RiseLift` · `RiseRamp` | `HopCurve.FadeOutTail` · `AfterAnticipation` · `LiftWithAnticipation` · `EaseOutUntil` |

**YAML** (agent sửa text rồi kiểm giá trị trên object đã dựng bằng eval, không tin YAML suông):

| Prefab | Trước | Sau |
|---|---|---|
| `CategoryBox.prefab` `receive` | ba dòng `FramePunchScale: 1.1`, `FramePunchDelay: 0.03`, `FramePunchDuration: 0.18` | khối `FramePunch:` với `Scale: 1.1`, `Delay: 0.03`, `Duration: 0.18` |
| `CategoryBox.prefab` `complete` | ba dòng `LidSettleDepth: 0.05`, `LidSettleDownDuration: 0.04`, `LidSettleUpDuration: 0.16` | khối `LidSettle:` với `Depth: 0.05`, `DownDuration: 0.04`, `UpDuration: 0.16`, `DownEase: 6` (OutQuad) |
| `CategoryBox.prefab` `counterPop` | không đổi | không đổi: key `Scale`, `Delay`, `Duration` trùng `PopSpec` |
| `CacheTileSlot.prefab` `landSpring`, `shiftSpring` | `Depth`, `DownDuration`, `UpDuration` | thêm dòng `DownEase: 2` (InSine) vào mỗi khối |

Trước khi sửa YAML: grep các key trên trong `*.prefab` và `*.unity` của `Assets/` để chắc không variant hay scene nào override.

**Nghiệm thu:**
- grep `TileFlightCurve`, `BoxFeelCurve`, `DipTuning`, `FlashTuning`, `SlotSpringTuning`, `CounterPopTuning`, `FramePunchScale`,
  `LidSettleDepth` trong `Assets/_CategoryJam` = 0; compile sạch; EditMode và PlayMode Horcrux, EditMode dự án PASS.
- Eval trên object đã dựng: `CacheTileSlot.landSpring.DownEase == InSine`, `shiftSpring.DownEase == InSine`,
  `BoxAnimation.complete.LidSettle.DownEase == OutQuad`, `receive.FramePunch` = (1.1, 0.03, 0.18).
- `git diff` của `CategoryBox.prefab`, `CacheTileSlot.prefab` chỉ có các khối trong bảng YAML; mở và lưu prefab không sinh dòng khác.
- **Cảm giác không đổi:** trace sau Task 4 khớp mốc **0 chênh** trên mọi khung ở cả hai level. Kịch bản đã phủ khung nảy và bộ đếm
  nảy (frame 0 → 8), nắp lún (frame 440), ô cache nhún khi nhận (frame 90 → 94) và khi dồn trái (frame 470), pitch không ghi vào
  trace nên `LandPitch` dựa vào test bằng nhau tuyệt đối ở Task 2.

---

## Mở rộng sau

- `HorizontalGrid` dự án dùng `Interpolator.OffsetFromCenter`: thứ tự phép tính khác nên cần trace riêng cho bố cục.
- Lắc xoay một chiều (`TileView.PlayReject`, `PlayLandRock`): lên Horcrux khi có người dùng thứ ba hoặc khi developer chọn đổi sang
  `DampedOscillator`, việc đó đổi cảm giác.
- Nở ba nhịp `TileView.PlayPopIn`: sau khi dự án làm lại cho vật cứng, nếu foods_jam vẫn dùng hình cũ thì xét lại.
- Struct tư thế nghỉ (`Capture` ở Awake, `Restore` khi về pool) thay các field `_restScale`, `_originalLocalScale`, `_restLocalY`
  rời ở bốn class: biến luật "tween trên giá trị nghỉ" thành kiểu; developer quyết.
