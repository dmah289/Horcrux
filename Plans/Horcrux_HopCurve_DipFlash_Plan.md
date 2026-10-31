# Plan — `HopCurve`, `DipSpec`, `FlashSpec`, `SinkSpec`, `PopSpec`, `LaunchSpec` và hai hàm `Interpolator` lên Horcrux

Ngày 2026-10-26, đồng bộ lại với code 2026-10-31. Nguồn: bản tạm trong dự án category-jam, đợt feeling gameplay (dự án
`docs/plans/Gameplay_Feel_Plan.md`, `Gameplay_Feel_Impl_Plan.md`) và các commit sau đợt 5 tới `9a8f3a92`:

- `Assets/_CategoryJam/Scripts/Entities/Tile/TileFlightCurve.cs` — hình dạng cú bay hai pha tới ô và cú nhảy lên hàng trước của tile.
- `Assets/_CategoryJam/Scripts/Entities/Box/BoxFeelTuning.cs` — `DipTuning`, `FlashTuning`, `LaunchTuning`, `BoxFeelCurve.DipScaleY`, `BoxFeelCurve.LaunchScaleY`.
- `Assets/_CategoryJam/Scripts/Entities/Box/BoxAnimation.cs` — `InsertFlash` (private static), `InsertReceiveAccents` (khung nảy), `ApplyLandCompress`.
- `Assets/_CategoryJam/Scripts/Entities/TileSlot/CacheTileSlot.cs` — `SlotSpringTuning`, `PlaySpring`.
- `Assets/_CategoryJam/Scripts/Entities/Box/BoxReceiveTuning.cs` — ba núm `FramePunch*`, `CounterPopTuning`.
- `Assets/_CategoryJam/Scripts/Entities/Box/CategoryBox.cs`, `Entities/Shelf/SingleShelfAmountBar.cs` — bộ đếm nảy, số lớp nảy (`CounterPopTuning`).
- `Assets/_CategoryJam/Scripts/Entities/Shelf/ShelfPromoteTuning.cs` — `LandPitch`.

Mọi thứ trong plan đã chơi thử và developer chốt số (2026-10-31). Không cái nào gọi tên type của dự án ngoài các class núm
`TileFlightTuning`, `TileRiseTuning`; bản Horcrux nhận số thay cho class núm.

**Phân công:**
- Task 1 (Horcrux, code): developer gõ.
- Task 2 (test Horcrux), Task 3 (tài liệu module), Task 4 (dự án đổi sang bản Horcrux và xoá bản tạm): agent làm.

Horcrux là submodule: Task 1–3 commit **trong `Assets/Horcrux`**, Task 4 commit ở repo dự án kèm con trỏ submodule.

---

## Ngữ cảnh đã chốt

| Mục | Chốt |
|---|---|
| Người dùng | dự án này: `Tile.StepFlight`, `Tile.StepRise`, `Tile.FlyToAsync`, `BoxAnimation.PlayReceive`, `BoxAnimation.PlayComplete`, `CacheTileSlot.PlaySpring`, `CategoryBox` bộ đếm, `SingleShelfAmountBar` số lớp, `ShelfPromoteTuning.LandPitch` · foods_jam `CacheHole.PlaySpringAnimation` cùng hình lún vị trí · dự án sau có vật nhảy thấp có bóng, vật bay hai pha vào ô, vật nhận lún, vật vọt đi sau khi nén, sprite loé, số nảy |
| Mục tiêu | đưa **toán thuần**, **struct núm** và các extension một lời gọi lên Horcrux, dự án gọi thẳng; cảm giác chơi **không đổi một giá trị nào**: mọi biểu thức chép nguyên văn, kể cả thứ tự phép nhân (float) |
| Chỗ đặt | `HopCurve` cạnh `SquashStretch` ở `Runtime/Utilities/PhysXHelper/` (toán thuần, không tween) · `Interpolator.SmoothStep`, `Interpolator.OffsetFromCenter` thêm vào class có sẵn · `DipSpec`, `FlashSpec`, `SinkSpec`, `PopSpec`, `LaunchSpec` ở `Runtime/Utilities/Tweening/` namespace `Horcrux.Runtime.Utilities.Tweening` như `SquashStretchSpec`, `ArcFlightSpec` · `InsertFlash` là extension của `PrimeTween.Sequence`, `CharmSinkLocalY` và `CharmPop` là extension của `Transform`, cả hai ở `Runtime/Utilities/ExtensionMethods/` |
| Tên | `HopCurve` (cú nhảy thấp, không phải đạn đạo), hậu tố `Spec` theo `SquashStretchSpec` thay `Tuning` của dự án; `LaunchSpec` developer chốt 2026-10-31. Tên field trong các struct **giữ nguyên** theo bản tạm, xem quyết định trái trực giác |
| Hợp đồng `CharmSinkLocalY`, `CharmPop` | như `CharmJump`: trả `Sequence`, **không nhận `ct`**, thời gian scaled. Cả hai được nhét vào một `Sequence` lớn hơn (khung nảy nằm trong sequence nhận tile) mà PrimeTween không cho tween con mang token |
| Đường bay hai pha | **lên Horcrux** (developer chốt 2026-10-31, số đã chốt): `ApexT`, `Arc01`, `ArcY`, `StartLiftOffset`, `Shadow01`, `FlightScale`, hằng `MIN_APEX_T`. `TileFlightCurve.cs` xoá hẳn |

**Cố ý KHÔNG làm**

| Không làm | Vì |
|---|---|
| Đưa `TileFlightTuning`, `TileRiseTuning`, `TilePressTuning` lên Horcrux | chứa bóng, lắc, tan thân, vệt — quyết định của tile nhựa 2.5D, không phải của cú nhảy; Horcrux nhận số |
| Driver tween cho cú nhảy (một `Tween.Custom` lái mọi kênh) | driver ghi lên thân, bóng, màu của **dự án**; mỗi dự án một bộ kênh. Horcrux giữ hình dạng, dự án giữ vòng `Step` |
| Driver tween cho lún và nén (`Tween DipScale(Transform, …)`) | `Tween.Custom<T>` cần một class giữ state (scale nghỉ) để callback `static`; bọc lại là sinh class trung gian cho một dòng `ApplyScaleY` |
| Gộp `HopCurve.Lift` vào `JumpArc` | `JumpArc` lo hai đầu lệch cao; `Lift` là ca hai đầu ngang nhau, đọc được trong một dòng. Test giữ hai bản khớp nhau |
| `HopCurve.HoldThenEaseIn`, `HopCurve.PeakForDistance` | không còn người dùng: scale chuyến bay đã đổi sang `FlightScale` (OutQuad lên đỉnh rồi SmoothStep về đích, commit dự án `fe452c81`), cú bay tới ô vượt đích một số cố định |
| Field `UpEase` trong `SinkSpec` | cả hai người dùng về nghỉ bằng OutBack; thêm núm khi có người dùng về khác |
| `CharmPop` bằng `Tween.PunchScale` của PrimeTween | khác hình: punch dao động quanh nghỉ nhiều chu kỳ, pop là **một** lượt lên rồi về. Ai cần rung quanh nghỉ thì dùng thẳng PrimeTween |
| `CharmPop` bằng `Tween.Scale(…, startDelay: Delay)` | `startDelay` của PrimeTween lặp mỗi cycle, đỉnh đứng yên giữa hai lượt; nên trễ nằm trong `Sequence` bằng `ChainDelay` |
| Lún vị trí cho nắp box | `f478f169` bỏ `LidSettle*`: nắp dừng ngay tại vị trí đóng, cú lún dành cho cả box (`LaunchSpec`) |
| Đưa nở ba nhịp `TileView.PlayPopIn` lên | foods_jam có bản giống hệt nhưng hình này bóp méo thân nhựa, đang chờ làm lại ở dự án; đưa lên là đóng băng thứ sắp đổi |
| Đưa lắc xoay `PlayReject`, `PlayLandRock` lên | hai bản khác chia thời gian (1:2:1 với 1:1:1) và biên độ dội (1.0 với 0.66); struct chung mang năm số. Chờ người dùng thứ ba |

**Quyết định trái trực giác**

| Chỗ | Vì |
|---|---|
| Field của `DipSpec`, `FlashSpec`, `SinkSpec`, `PopSpec`, `LaunchSpec` PascalCase (`ScaleY`, `PeakAlpha`, `Depth`, `Scale`, `HoldDuration`…), khác `SquashStretchSpec` camelCase | tên field là **key YAML** trên `CategoryBox.prefab`, `CacheTileSlot.prefab`, `SingleShelf.prefab`, `Tile.prefab`; đổi kiểu struct thì Unity giữ giá trị vì khớp theo tên field, đổi tên field thì giá trị rơi về 0 im lặng. Casing PascalCase cũng đúng luật "public field của data class" |
| `LeanRamp`, `LeanToward`, `FlightScale` nhận mốc và ngưỡng làm tham số **bắt buộc** (`riseEnd`, `fallStart`, `minDx`, `peakAt`) | 0.3 · 0.75 · 0.05 · 0.5 là số cảm giác của tile nhựa, không phải của cơ chế; mặc định trong Horcrux là đóng băng nháp của một dự án thành chuẩn. `MIN_APEX_T = 0.5` ở lại Horcrux vì là cơ chế: pha lên không được ngắn hơn pha rơi |
| `SmoothStep` clamp đầu vào | bản tạm clamp; `LeanRamp` gọi với `(1 − t) / (1 − fallStart)` có thể vượt 1 ở biên |
| `SinkSpec.DownEase` là núm, không cố định | ô cache xuống InSine như khay bị đè; dự án sau có vật rơi chạm đáy xuống OutQuad. Bẫy: thiếu key `DownEase` trong YAML thì giá trị là 0, tức `Ease.Default`, PrimeTween đọc thành OutQuad. Task 4 ghi key thẳng vào prefab |
| `SinkSpec` dời vị trí, không bóp scale | luật vật cứng không biến dạng: khay nhựa lún xuống, không bẹp ra. Cần bẹp thì dùng `DipSpec`; cần nén rồi vọt thì `LaunchSpec` |
| `CharmPop` trả `Sequence` dù chỉ có một tween | trễ là một phần của cú nảy, nằm trong `PopSpec` để một nguồn; PrimeTween chỉ cho trễ-không-lặp qua `ChainDelay`. Khung box chèn bằng `sequence.Insert(at, …CharmPop(…))`, hai chỗ kia chạy rời; cả ba chỉ đọc một hình |
| `CharmSinkLocalY` nhận `restLocalY`, không đọc `localPosition.y` lúc gọi | lún gọi lại khi đang lún thì đọc lúc gọi sẽ trôi mốc nghỉ xuống dần; luật "tween trên giá trị nghỉ đọc ở Awake" |
| `InsertFlash` bật renderer lúc bắt đầu loé, tắt khi alpha về 0 | sprite loé nằm sẵn trên vật ở alpha 0, vẫn là một quad trong suốt vẽ mỗi frame. Dự án đã đo 2026-10-26: tắt `frame_flash` và vệt sáng nghỉ trên 2 box bớt 1 batch, 2 SetPass; trace đối chứng 0 chênh. Mốc tắt là `(at + Rise) + Fall`, cùng thứ tự phép cộng với lúc tween về 0 kết thúc |
| `LaunchSpec.EvaluateStretchScaleY` chỉ cho pha bật; pha nén và nhún không có hàm | nén và nhún là `LerpUnclamped(1, ScaleY, t)` và `−SinkDepth · t` trên **cùng một t đã ease** của một `Tween.Custom` (OutQuad) — hai dòng ở caller, không có công thức riêng để lên Horcrux; tách ra là nhân bất biến "cùng t" ra hai nơi |
| `OffsetFromCenter` ở `Interpolator` | hàm một dòng, cùng họ ánh xạ chỉ số sang giá trị như `Remap`; developer chuyển sang class khác tuỳ ý |

---

## Đã khảo sát

| Nguồn | Lấy gì | Không lấy, vì sao |
|---|---|---|
| Horcrux `PhysXHelper/Interpolator.cs` | chỗ đặt `SmoothStep` cạnh `SmootherStep`; `FeelingPlan.md` đã ghi "còn thiếu SmoothStep cubic" | — |
| Horcrux `ExtensionMethods/Transform/JumpArc.cs` | test chéo: `Lift(t, peak)` = `JumpArc.EvaluateHeight(0, ComputeArcCoefficient(0, 0, peak), t)` | không thay `Lift` (bảng trên) |
| Horcrux `PhysXHelper/SquashStretch.cs` | `BoxAnimation.ApplyScaleY` đã dùng `GetVolumePreservingScale`; `DipSpec`, `LaunchSpec` chỉ trả hệ số y | — |
| Horcrux `Tweening/Easings/Easer.cs` | — | `DipSpec`, `LaunchSpec` dùng `PrimeTween.Easing.Evaluate` như bản tạm: cùng một hàm ease với tween đang chạy, khác bản thì lún lệch tween |
| Horcrux `ExtensionMethods/Transform/SquashStretchSpec.cs`, `ArcFlightSpec.cs` | namespace và hậu tố `Spec` | camelCase (bảng trên) |
| Horcrux `ExtensionMethods/Transform/TransformExtensions.Jump.cs`, `Tweening/CharmJump.md` | hợp đồng trả `Sequence`, không `ct`; file partial riêng cho mỗi họ extension | — |
| Horcrux `ExtensionMethods/` | chưa có extension nào cho `PrimeTween.Sequence`; dự án cũng không (grep `SequenceExtensions`: 0) | — |
| PrimeTween `Internal/UnmanagedTweenData.cs` `calcTFromElapsedTimeTotal`; `Sequence.Insert(float, Sequence)` | trễ chèn (`waitDelay`) trả trạng thái "trước" cho tới mốc chạy; sequence lồng được chèn ở mốc | `startDelay` lặp mỗi cycle (bảng trên) |
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
    /// Hình dạng cú nhảy thấp và cú bay hai pha tới ô, lái bằng một t tuyến tính: gốc đi đường đất, thân nhấc so với đường đất,
    /// nghiêng vào hướng đi, scale và alpha đổi theo pha. Mọi kênh đọc cùng t để cùng nhịp; caller giữ vòng Step và ghi lên object của mình.
    /// </summary>
    public static class HopCurve
    {
        /// <summary>Đỉnh của chuyến bay hai pha không sớm hơn nửa chuyến: pha lên không ngắn hơn pha rơi.</summary>
        public const float MIN_APEX_T = 0.5f;

        /// <summary>Quãng đường đổi theo vị trí thì núm là tốc độ: thời lượng = quãng / tốc độ, kẹp hai đầu.</summary>
        public static float DurationForSpeed(float distance, float speed, float minDuration, float maxDuration)
            => Mathf.Clamp(distance / speed, minDuration, maxDuration);

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

        /// <summary>1 tới fadeStart rồi InQuad về 0 ở t = 1.</summary>
        public static float FadeOutTail(float t, float fadeStart)
        {
            if (t <= fadeStart) return 1f;
            var k = (t - fadeStart) / (1f - fadeStart);
            return 1f - k * k;
        }

        /// <summary>Tiến độ của pha sau mốc start: 0 tới start, rồi 0 → 1 tuyến tính ở t = 1. Cung nhảy sau lấy đà, pha rơi sau đỉnh.</summary>
        public static float ProgressAfter(float t, float start)
            => t <= start ? 0f : Mathf.Clamp01((t - start) / (1f - start));

        /// <summary>Thân so với đường đất: hạ tới −sinkDepth ở anticipationEnd (lấy đà), rồi parabol từ −sinkDepth về 0 với đỉnh peak; 0 ở hai đầu.</summary>
        public static float LiftWithAnticipation(float t, float peak, float sinkDepth, float anticipationEnd)
        {
            if (t <= 0f || t >= 1f) return 0f;
            if (t <= anticipationEnd) return -sinkDepth * Interpolator.SmoothStep(t / anticipationEnd);
            var k = ProgressAfter(t, anticipationEnd);
            return Mathf.Lerp(-sinkDepth, 0f, k) + Lift(k, peak);
        }

        /// <summary>OutQuad 0 → 1 xong ở k = end, giữ 1 sau đó: vật tới gần camera to và sáng nhanh lúc đầu.</summary>
        public static float EaseOutUntil(float k, float end)
        {
            var x = Mathf.Clamp01(k / end);
            return 1f - (1f - x) * (1f - x);
        }

        /// <summary>t của đỉnh chuyến bay hai pha: pha rơi chiếm đúng fallDuration giây cuối, không sớm hơn MIN_APEX_T.</summary>
        public static float ApexT(float duration, float fallDuration)
            => duration > 0f ? Mathf.Max(MIN_APEX_T, 1f - fallDuration / duration) : MIN_APEX_T;

        /// <summary>Cung của chuyến bay hai pha, 0 ở hai đầu và 1 ở đỉnh: OutQuad lên tới apexT rồi InQuad rơi. Bóng co theo số này.</summary>
        public static float Arc01(float t, float apexT)
        {
            if (t <= 0f || t >= 1f) return 0f;
            if (t <= apexT)
            {
                var u = t / apexT;
                return u * (2f - u);
            }

            var v = ProgressAfter(t, apexT);
            return 1f - v * v;
        }

        /// <summary>Độ cao nhìn thấy (world y) của thân: từ y0 lên y1 + fallHeight ở đỉnh rồi rơi về y1. Pha rơi chỉ phụ thuộc y1 và fallHeight
        /// nên cú rơi giống nhau dù bay từ đâu.</summary>
        public static float ArcY(float t, float y0, float y1, float fallHeight, float apexT)
            => Mathf.LerpUnclamped(t <= apexT ? y0 : y1, y1 + fallHeight, Arc01(t, apexT));

        /// <summary>Độ cao xuất phát của thân (world) khi chuyến bay bắt đầu trên chỗ cũ: tan theo cung, 0 từ đỉnh. Cung phẳng ở đỉnh nên chỗ tan hết không gãy.</summary>
        public static float StartLiftOffset(float t, float apexT, float startLift)
            => t >= apexT ? 0f : startLift * (1f - Arc01(t, apexT));

        /// <summary>Độ co bóng theo cung nhưng bắt đầu từ start01: tới đỉnh lerp start01 → 1, rồi theo Arc01. start01 = 0 cho đúng Arc01.</summary>
        public static float Shadow01(float t, float apexT, float start01)
        {
            var arc = Arc01(t, apexT);
            return t > apexT ? arc : Mathf.Lerp(start01, 1f, arc);
        }

        /// <summary>Cỡ nhìn thấy của chuyến bay hai pha: OutQuad từ from lên peak tại t = apexT·peakAt, rồi SmoothStep về to lúc đáp.
        /// Tốc độ đổi cỡ bằng 0 ở đỉnh và lúc đáp nên vật chui vào ô, không đập vào.</summary>
        public static float FlightScale(float t, float apexT, float peakAt, float from, float peak, float to)
        {
            var peakT = apexT * peakAt;
            if (t <= peakT)
            {
                var u = t / peakT;
                return Mathf.LerpUnclamped(from, peak, u * (2f - u));
            }

            return Mathf.LerpUnclamped(peak, to, Interpolator.SmoothStep(ProgressAfter(t, peakT)));
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
    /// <summary>Nảy một pha trên scale nghỉ: sau Delay, lên nghỉ × Scale rồi về nghỉ, mỗi lượt nửa Duration (InOutSine).</summary>
    [Serializable]
    public struct PopSpec
    {
        [Range(1f, 1.5f)] public float Scale;
        [Tooltip("Chờ bấy nhiêu giây rồi mới nảy: sau đế một nhịp, cùng lúc khung nảy.")]
        [Min(0f)] public float Delay;
        [Tooltip("Tổng thời gian nảy: lượt lên và lượt về mỗi lượt một nửa.")]
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

        /// <summary>Nảy một pha trên mốc nghỉ: chờ spec.Delay, lên restScale × spec.Scale rồi về restScale, mỗi lượt nửa Duration (InOutSine).
        /// Trả Sequence vì startDelay của PrimeTween lặp mỗi cycle. Chèn vào sequence khác bằng Insert(at, …).</summary>
        public static Sequence CharmPop(this Transform self, Vector3 restScale, in PopSpec spec)
            => Sequence.Create()
                .ChainDelay(spec.Delay)
                .Chain(Tween.Scale(self, restScale, restScale * spec.Scale, spec.Duration * 0.5f, Ease.InOutSine, 2, CycleMode.Yoyo));
    }
}
```

### 1.9 `Runtime/Utilities/Tweening/LaunchSpec.cs` — file mới

```csharp
using System;
using PrimeTween;
using UnityEngine;

namespace Horcrux.Runtime.Utilities.Tweening
{
    /// <summary>
    /// Vật nhận cú chạm rồi vọt đi: nén y xuống ScaleY và nhún xuống SinkDepth trên cùng một t (OutQuad) trong DownDuration, giữ đáy
    /// HoldDuration, rồi lúc bật nén lật thành giãn tới StretchScaleY (OutQuad) và về 1 (InOutSine) trong lúc bay.
    /// Caller nhân hệ số y lên scale nghỉ, bù trục kia (SquashStretch), và tự tween vị trí bay.
    /// </summary>
    [Serializable]
    public struct LaunchSpec
    {
        [Tooltip("Nén y xuống bấy nhiêu khi chạm, cùng lúc vật nhún xuống SinkDepth.")]
        [Range(0.7f, 1f)] public float ScaleY;
        [Tooltip("Thời gian nén và nhún xuống (OutQuad), chung một đường cong nên đáy nhún trùng lúc nén sâu nhất.")]
        [Min(0.01f)] public float DownDuration;
        [Tooltip("Vật nhún xuống bấy nhiêu unit local.")]
        [Min(0f)] public float SinkDepth;
        [Tooltip("Giữ ở đáy bấy nhiêu giây rồi mới bật.")]
        [Min(0f)] public float HoldDuration;
        [Tooltip("Lúc bật, nén lật thành giãn tới scale y này rồi về 1.")]
        [Range(1f, 1.2f)] public float StretchScaleY;
        [Tooltip("Từ lúc bật tới đỉnh giãn (OutQuad): ngắn để giãn trùng lúc vận tốc lớn nhất.")]
        [Min(0.01f)] public float StretchRiseDuration;
        [Tooltip("Từ đỉnh giãn về 1 (InOutSine) trong lúc đang bay.")]
        [Min(0.01f)] public float StretchSettleDuration;

        public LaunchSpec(float scaleY, float downDuration, float sinkDepth, float holdDuration,
            float stretchScaleY, float stretchRiseDuration, float stretchSettleDuration)
        {
            ScaleY = scaleY;
            DownDuration = downDuration;
            SinkDepth = sinkDepth;
            HoldDuration = holdDuration;
            StretchScaleY = stretchScaleY;
            StretchRiseDuration = stretchRiseDuration;
            StretchSettleDuration = stretchSettleDuration;
        }

        /// <summary>Từ lúc chạm tới lúc vật bắt đầu bay lên.</summary>
        public float LaunchDelay => DownDuration + HoldDuration;

        public float StretchDuration => StretchRiseDuration + StretchSettleDuration;

        /// <summary>t ∈ [0, 1] trên StretchDuration kể từ lúc bật → hệ số y: từ nén lên đỉnh giãn rồi về 1.</summary>
        public float EvaluateStretchScaleY(float t)
        {
            var peakEnd = StretchRiseDuration / StretchDuration;
            return t < peakEnd
                ? Mathf.Lerp(ScaleY, StretchScaleY, Easing.Evaluate(t / peakEnd, Ease.OutQuad))
                : Mathf.Lerp(StretchScaleY, 1f, Easing.Evaluate((t - peakEnd) / (1f - peakEnd), Ease.InOutSine));
        }
    }
}
```

**Editor setup:** tạo file trong Project window của Unity (để có `.meta`), không cần kéo thả gì.

---

## Task 2 — Test Horcrux (agent)

EditMode: `Tests/EditMode/HopCurveTests.cs`, `DipSpecTests.cs`, `LaunchSpecTests.cs`, `InterpolatorSmoothStepTests` và `OffsetFromCenter`
(gộp vào file nào có sẵn thì gộp). Chuyển **nguyên văn** 82 case của `TileFlightCurveTests` (trừ `LandPitch_CentersOnMiddleSlot`, ở lại
dự án) và 9 case của `BoxFeelCurveTests`, đổi tên type và thêm tham số mốc theo bảng Task 4; thêm case biên:

| Hàm | Input | Kỳ vọng |
|---|---|---|
| `SmoothStep` | 0 · 0.5 · 1 · −1 · 2 | 0 · 0.5 · 1 · 0 · 1 |
| `OffsetFromCenter(i, 3, 0.03)` | i = 0 · 1 · 2 | −0.03 · 0 · 0.03 |
| `OffsetFromCenter` | (0, 1, 5) · (0, 4, 1) · (3, 4, 1) | 0 · −1.5 · 1.5 |
| `OffsetFromCenter(i, n, s)` so với `(i − (n − 1) * 0.5f) * s` | i ∈ 0…4, n ∈ 1…5, s ∈ {0.03, 0.7} | bằng nhau tuyệt đối, khoá thứ tự phép tính mà `LandPitch` dựa vào |
| `Lift` | t 0 và 1; t 0.5; t 0.2 so với 0.8; t ∈ {0, 0.25, 0.5, 0.9, 1} | 0; peak; bằng nhau; bằng `JumpArc.EvaluateHeight(0, ComputeArcCoefficient(0, 0, peak), t)` |
| `LeanRamp(t, 0.3, 0.75)` | 0 · 1 · 0.3 · 0.5 · 0.75; dãy 0.05 bước tới 0.3 | 0 · 0 · 1 · 1 · 1; tăng đơn điệu |
| `LeanRamp(t, 0.3, fallStart)`, fallStart ∈ {0.66, 0.8} | fallStart · (1 + fallStart) / 2 · 1 | 1 · 0.5 · 0 |
| `LeanToward(dx, 10, 0.05)` | −2 · 2 · ±0.049 · 0.05 | 10 · −10 · 0 · −10 |
| `FadeOutTail(t, 0.85)` | 0.5 · 0.85 · 1 | 1 · 1 · 0 |
| `ProgressAfter(t, 0.14)` · `ProgressAfter(t, 0.7)` | 0.1 · 0.14 · 1 · 0.7 · 0.85 | 0 · 0 · 1 · 0 · 0.5 |
| `LiftWithAnticipation(t, 1.1, 0.04, 0.14)` | 0 · 1 · 0.14 · 0.57 | 0 · 0 · −0.04 · 1.1 − 0.02 (±1e-3) |
| `EaseOutUntil(k, 0.6)` | 0 · 0.3 · 0.6 · 1 | 0 · 0.75 · 1 · 1 |
| `PeakToLocal` | (1.2, 0.6) · (1.2, 0) · (1.2, −1) | 2 · 1.2 · 1.2 |
| `DurationForSpeed` | dưới kẹp, trong, trên kẹp | min · giá trị · max |
| `ApexT` | (0.4, 0.12) · (0.2, 0.12) · (0, 0.12) | 0.7 · 0.5 · 0.5 (case dự án `ApexT_LeavesFallDurationAtTheEnd_NotBeforeHalf`) |
| `Arc01(t, 0.7)` | 0 · 0.7 · 1 · 0.35 · 0.85 | 0 · 1 · 0 · 0.75 (OutQuad) · 0.75 (InQuad) |
| `ArcY(t, 0, y1, 0.8, apexT)` | t = 0 · apexT · 1; hai chuyến y0 khác nhau | y0 · y1 + 0.8 · y1; pha rơi (t > apexT) bằng nhau tuyệt đối giữa hai chuyến |
| `StartLiftOffset(t, 0.7, 0.3)` | 0 · 0.35 · 0.7 · 1 | 0.3 · 0.3 × (1 − 0.75) · 0 · 0 |
| `Shadow01(t, 0.7, 0)` so với `Arc01(t, 0.7)` | t ∈ {0.2, 0.5, 0.7, 0.9} | bằng nhau tuyệt đối |
| `Shadow01(t, 0.7, 0.4)` | 0 · 0.7 · 0.85 | 0.4 · 1 · `Arc01(0.85, 0.7)` |
| `FlightScale(t, 0.7, 0.5, 1.08, 1.21, 0.9)` | 0 · 0.35 · 1; đạo hàm số tại 0.35 và 1; dãy t tăng | 1.08 · 1.21 · 0.9; ≈ 0; tăng tới 0.35 rồi giảm |
| `FlightScale(t, apexT, 0.5, from, peak, to)` với to > peak | t ∈ (peakT, 1) | tăng đơn điệu (case dự án `KeepsGrowingWhenTargetIsAbovePeak`) |
| `DipSpec(0.86, 0.05, 0.22).EvaluateScaleY` | 0 · downEnd · 1 · giữa pha xuống · 70 % pha về | 1 · 0.86 · 1 · trong (0.86, 1) · > 1 |
| `LaunchSpec(0.87, 0.2, 0.15, 0.03, 1.06, 0.05, 0.15)` | `LaunchDelay` · `StretchDuration` · `EvaluateStretchScaleY` tại 0 · 0.25 · 1 · 0.6 | 0.23 · 0.2 · 0.87 · 1.06 · 1 · trong (1, 1.06) |
| `SinkSpec(0.12, 0.06, 0.18, InSine).Duration` | — | 0.24 |
| `new SinkSpec()` | `DownEase` | `Ease.Default`: test ghi lại cái bẫy YAML thiếu key |

PlayMode: `Tests/PlayMode/CharmAccentTests.cs`, cùng khung `CharmJumpTests` (thời gian scaled, đo GC bằng recorder như ở đó):

| Hàm | Kiểm | Kỳ vọng |
|---|---|---|
| `CharmSinkLocalY(rest 0.5, (0.12, 0.06, 0.18, InSine))` | y lúc 0.06 s · y sau `Duration` · tween hoàn tất | ≈ 0.38 · đúng 0.5 · `isAlive` false |
| `CharmSinkLocalY` chèn vào `Sequence` ở mốc 0.1 | y trước 0.1 s | đúng mốc nghỉ, chưa lún |
| `CharmPop(rest (2,2,2), (1.25, 0.06, 0.15))` | scale trước 0.06 s · tại 0.06 + 0.075 s · sau 0.06 + 0.15 s · `isAlive` | đúng (2,2,2) · ≈ (2.5,2.5,2.5) · đúng (2,2,2) · false |
| `CharmPop` với `Delay = 0` | scale tại 0 · tại Duration/2 | nghỉ · đỉnh |
| `CharmPop` chèn `sequence.Insert(at, …)` so với bản tay `sequence.Insert(at + Delay, Tween.Scale(… 2, Yoyo))` | scale mỗi frame trong một `Sequence` cha | bằng nhau tuyệt đối: chứng minh thay thế ở `BoxAnimation.InsertReceiveAccents` không đổi một khung |
| cả hai | GC mỗi lần gọi sau lượt khởi động | 0 B |

`InsertFlash`, PlayMode trong `CharmAccentTests.cs`: renderer tắt trước `at`, bật trong suốt pha loé, tắt sau
`at + Rise + Fall`; alpha theo thời gian Task 4 kiểm bằng trace.

## Task 3 — Tài liệu module (agent)

- `Runtime/Utilities/PhysXHelper/HopCurve.md`: bảng kênh → hàm → hình dạng, công thức, mốc kiểm; mục riêng cho chuyến bay hai pha
  (vì sao pha rơi cố định, vì sao đỉnh không trước nửa đường).
- `Runtime/Utilities/Tweening/CharmAccent.md`: `SinkSpec`, `PopSpec`, `LaunchSpec`, `CharmSinkLocalY`, `CharmPop`, cùng khung `CharmJump.md`
  (hợp đồng không `ct`, ví dụ chèn vào `Sequence`, bẫy `DownEase` thiếu key, vì sao `restLocalY` là tham số, vì sao `CharmPop` là `Sequence`).
- Một mục cho `DipSpec`, `FlashSpec`, `InsertFlash` trong cùng file `CharmAccent.md`: cả ba là nhịp nhấn của vật nhận; `LaunchSpec` là vật nhận rồi rời đi.
- `Interpolator`: thêm `SmoothStep`, `OffsetFromCenter` vào tài liệu nếu class có tài liệu riêng.
- `Plans/FeelingPlan.md`: `SmoothStep` đã có; `Anticipation` có bản hình dạng ở `HopCurve.LiftWithAnticipation`; `Overshoot` có hai
  bản dùng sẵn `CharmPop`, `CharmSinkLocalY`; `ProgressPop` trỏ `CharmPop`; `Projectile/Ballistics` ghi thêm "cú bay hai pha vào ô: `HopCurve.ArcY`".

## Task 4 — Dự án dùng bản Horcrux, xoá bản tạm (agent)

Trước khi sửa: mốc trace đã quay 2026-10-31 bằng kịch bản mở rộng (`docs/tools/feel_trace/trace.cs`: thêm tile cuối diễn tada, magnet, kệ
khoá, kệ Single có thanh đếm), hai lần cùng chữ ký 0 chênh. Mọi plan Horcrux đợt này so với **cùng một mốc đó**.

| File dự án | Đổi |
|---|---|
| `Entities/Tile/Tile.cs` | `TileFlightCurve.X` → `HopCurve.Y` theo bảng dưới; bốn hằng cảm giác thành `private const` trong `Tile`: `LEAN_RISE_END = 0.3f`, `LEAN_FALL_START = 0.75f` (chỉ cú lên hàng trước), `LEAN_MIN_DX = 0.05f`, `SCALE_PEAK_OF_RISE = 0.5f` |
| `Entities/Tile/TileFlightCurve.cs` | **xoá hẳn** (cả `.meta`) |
| `Entities/Box/BoxFeelTuning.cs` | xoá `DipTuning`, `FlashTuning`, `LaunchTuning`, `BoxFeelCurve`; `BoxCompleteTuning.Launch` đổi kiểu `LaunchSpec` (cùng tên field, cùng giá trị mặc định), `LidFlash` → `FlashSpec`; đổi tên file thành `BoxCompleteTuning.cs` (Project window) vì chỉ còn class đó |
| `Entities/Box/BoxReceiveTuning.cs` | `Dip` → `DipSpec`, `Flash` → `FlashSpec`; ba núm `FramePunchScale`, `FramePunchDelay`, `FramePunchDuration` → `public PopSpec FramePunch = new(1.10f, 0.03f, 0.18f);` giữ tooltip; xoá `CounterPopTuning` |
| `Entities/Box/BoxAnimation.cs` | `ApplyDip`: `BoxFeelCurve.DipScaleY(t, dip)` → `dip.EvaluateScaleY(t)`; `BoxFeelCurve.LaunchScaleY(t, anim.complete.Launch)` → `anim.complete.Launch.EvaluateStretchScaleY(t)`; `InsertFlash(seq, at, r, f)` private → `seq.InsertFlash(at, r, f)`, xoá bản private; `InsertReceiveAccents`: hai dòng punch → `sequence.Insert(at, frame.transform.CharmPop(_frameRestScale, receive.FramePunch))`; `ApplyLandCompress` giữ nguyên (đọc `Launch.ScaleY`, `Launch.SinkDepth`) |
| `Entities/Box/CategoryBox.cs` | `CounterPopTuning counterPop` → `PopSpec counterPop`, tên field giữ; thân `UpdateCounter` từ `_counterPopTween = Sequence.Create().ChainDelay(…).Chain(…)` → `_counterPopTween = _counterText.transform.CharmPop(_counterOriginalScale, counterPop)` |
| `Entities/Shelf/SingleShelfAmountBar.cs` | `CounterPopTuning amountPop` → `PopSpec amountPop`; `PlayPop` → `_popSequence = text.CharmPop(_amountRestScale, amountPop)` |
| `Entities/TileSlot/CacheTileSlot.cs` | xoá `SlotSpringTuning`; `landSpring = new(0.12f, 0.06f, 0.18f, Ease.InSine)`, `shiftSpring = new(0.08f, 0.06f, 0.14f, Ease.InSine)` kiểu `SinkSpec`; `PlaySpring` → `transform.CharmSinkLocalY(_restLocalY, spring)`, vẫn không giữ handle như hiện tại |
| `Entities/Shelf/ShelfPromoteTuning.cs` | `LandPitch` → `1f + Interpolator.OffsetFromCenter(slotIndex, capacity, PitchStep)` |
| `Tests/EditMode/TileFlightCurveTests.cs`, `BoxFeelCurveTests.cs` | sang Horcrux (Task 2) rồi xoá; `LandPitch_CentersOnMiddleSlot` → file `ShelfPromoteTuningTests.cs` của dự án |

| Bản tạm (tại `9a8f3a92`) | Bản Horcrux |
|---|---|
| `TileFlightCurve.Duration(d, flight)` | `HopCurve.DurationForSpeed(d, flight.Speed, flight.MinDuration, flight.MaxDuration)` |
| `TileFlightCurve.ApexT(duration, flight.FallDuration)` | `HopCurve.ApexT(duration, flight.FallDuration)` |
| `TileFlightCurve.PeakToLocal(x, lossy)` | `HopCurve.PeakToLocal(x, lossy)` |
| `TileFlightCurve.LeanToward(dx, deg)` | `HopCurve.LeanToward(dx, deg, LEAN_MIN_DX)` |
| `TileFlightCurve.LeanRamp(t, apexT)` (chuyến bay) | `HopCurve.LeanRamp(t, LEAN_RISE_END, apexT)` |
| `TileFlightCurve.LeanRamp(k)` (cú lên hàng trước) | `HopCurve.LeanRamp(k, LEAN_RISE_END, LEAN_FALL_START)` |
| `TileFlightCurve.ProgressAfter(t, x)` | `HopCurve.ProgressAfter(t, x)` |
| `TileFlightCurve.Arc01` · `ArcY` · `StartLiftOffset` · `Shadow01` | cùng tên ở `HopCurve`, cùng tham số |
| `TileFlightCurve.FlightScale(t, apexT, from, peak, to)` | `HopCurve.FlightScale(t, apexT, SCALE_PEAK_OF_RISE, from, peak, to)` |
| `TileFlightCurve.BodyFade(fall, flight.FallFadeStart)` | `HopCurve.FadeOutTail(fall, flight.FallFadeStart)` |
| `TileFlightCurve.RiseLift(t, peak, sink, sinkEnd)` | `HopCurve.LiftWithAnticipation(t, peak, sink, sinkEnd)` |
| `TileFlightCurve.RiseRamp(k, end)` | `HopCurve.EaseOutUntil(k, end)` |
| `TileFlightCurve.Lift` | `HopCurve.Lift` (chỉ còn qua `LiftWithAnticipation`) |

**YAML** (agent sửa text rồi kiểm giá trị trên object đã dựng bằng eval, không tin YAML suông):

| Prefab | Trước | Sau |
|---|---|---|
| `CategoryBox.prefab` `receive` | ba dòng `FramePunchScale: 1.05`, `FramePunchDelay: 0.03`, `FramePunchDuration: 0.2` (prefab là mốc, khác mặc định C# 1.1/0.03/0.18) | khối `FramePunch:` với `Scale: 1.05`, `Delay: 0.03`, `Duration: 0.2` |
| `CategoryBox.prefab` `complete.Launch`, `complete.LidFlash`, `receive.Dip`, `receive.Flash`, `counterPop` | không đổi | không đổi: key trùng tên field của struct mới |
| `SingleShelf.prefab` `amountPop` | không đổi | không đổi |
| `CacheTileSlot.prefab` `landSpring`, `shiftSpring` | `Depth`, `DownDuration`, `UpDuration` | thêm dòng `DownEase: 2` (InSine) vào mỗi khối |

Trước khi sửa YAML: grep các key trên trong `*.prefab` và `*.unity` của `Assets/` để chắc không variant hay scene nào override.

**Nghiệm thu:**
- grep `TileFlightCurve`, `BoxFeelCurve`, `DipTuning`, `FlashTuning`, `LaunchTuning`, `SlotSpringTuning`, `CounterPopTuning`,
  `FramePunchScale` trong `Assets/_CategoryJam` = 0; compile sạch; EditMode và PlayMode Horcrux, EditMode dự án PASS.
- Eval trên object đã dựng: `CacheTileSlot.landSpring.DownEase == InSine`, `shiftSpring.DownEase == InSine`,
  `receive.FramePunch` = (1.05, 0.03, 0.2), `receive.Dip` = (0.95, 0.15, 0.15), `receive.Flash` = (0.35, 0.06, 0.2), `complete.Launch` = (0.87, 0.3, 0.4, 0, 1.07, 0.1, 0.15)
  đúng YAML hiện tại của prefab, `counterPop` = (1.12, 0.1, 0.3), `SingleShelf.prefab` `amountPop` = (1.15, 0, 0.15), `landSpring` = (0.12, 0.06, 0.18, InSine),
  `shiftSpring` = (0.08, 0.06, 0.14, InSine). Số đọc lại từ prefab lúc làm, không chép từ plan.
- `git diff` của `CategoryBox.prefab`, `CacheTileSlot.prefab` chỉ có các khối trong bảng YAML; mở và lưu prefab không sinh dòng khác.
- **Cảm giác không đổi:** trace sau Task 4 khớp mốc **0 chênh** trên mọi khung. Kịch bản phủ khung nảy và bộ đếm nảy (frame 0 → 8),
  ô cache nhún khi nhận (frame 90 → 94), nén–giãn khi vọt và loé nắp (từ frame 440), chuyến bay hai pha (mọi chuyến), tada (bước mới),
  thanh đếm kệ Single nảy (bước mới); pitch không ghi vào trace nên `LandPitch` dựa vào test bằng nhau tuyệt đối ở Task 2.

---

## Mở rộng sau

- `HorizontalGrid` dự án dùng `Interpolator.OffsetFromCenter`: thứ tự phép tính khác nên cần trace riêng cho bố cục.
- Lắc xoay một chiều (`TileView.PlayReject`, `PlayLandRock`): lên Horcrux khi có người dùng thứ ba hoặc khi developer chọn đổi sang
  `DampedOscillator`, việc đó đổi cảm giác.
- Nở ba nhịp `TileView.PlayPopIn`: sau khi dự án làm lại cho vật cứng, nếu foods_jam vẫn dùng hình cũ thì xét lại.
- Struct tư thế nghỉ (`Capture` ở Awake, `Restore` khi về pool) thay các field `_restScale`, `_originalLocalScale`, `_restLocalY`
  rời ở bốn class: biến luật "tween trên giá trị nghỉ" thành kiểu; developer quyết.
- Nhịp tada của tile cuối và sổ "đang diễn": `Plans/Horcrux_FeelUtilities_Plan.md`.
