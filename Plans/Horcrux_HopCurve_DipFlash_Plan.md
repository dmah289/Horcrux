# Plan — `HopCurve`, `DipSpec`, `FlashSpec`, `SinkSpec`, `PopSpec`, `LaunchSpec` và hai hàm `Interpolator` lên Horcrux

Bản tạm: category-jam `Scripts/Entities/Tile/TileFlightCurve.cs` (caller `Tile.cs`, `Cache/BoosterFlight.cs`), `Box/BoxFeelTuning.cs`, `BoxAnimation.cs`, `BoxReceiveTuning.cs`, `CategoryBox.cs`, `Shelf/SingleShelfAmountBar.cs`, `ShelfPromoteTuning.cs`, `TileSlot/CacheTileSlot.cs`.
**Phụ thuộc:** không — gõ lúc nào cũng được; bốn họ độc lập A · B · C · D, [FeelUtilities](Horcrux_FeelUtilities_Plan.md) cùng khuôn "struct núm + class toán", trước hay sau đều compile. Plan chờ: [UIProxyFlight](Horcrux_UIProxyFlight_Plan.md) GĐ2 dùng `HopCurve.ApexT`, `ArcY` của GĐ1 ở đây.
Kiểm: `bash "<horcrux>/Editor/CompileCheck/Tools~/verify.sh" --csproj com.horcrux.runtime.csproj,Horcrux.PlayModeTests.csproj --tests Horcrux.EditModeTests`; chuyển dự án `--csproj com.horcrux.runtime.csproj,CategoryJam.Gameplay.csproj --tests CategoryJam.EditModeTests,Horcrux.EditModeTests`; PlayMode chạy ở Window › General › Test Runner.

---

## Họ A — `HopCurve`: hình dạng cú nhảy thấp và chuyến bay hai pha

### Kịch bản

| Tình huống | Hệ làm gì | Caller thấy |
|---|---|---|
| vật bay từ kệ trên gần hay kệ dưới xa vào ô | `duration` = quãng / tốc độ, kẹp hai đầu; `apexT` để pha rơi chiếm đúng `fallDuration` giây cuối | cú rơi giống hệt mọi chuyến |
| chuyến ngắn hơn `2 · fallDuration` | `apexT` kẹp ở `MIN_APEX_T` 0.5 | pha rơi co lại, pha lên không ngắn hơn pha rơi |
| `duration` ≤ 0 | `ApexT` trả `MIN_APEX_T` | không chia 0 |
| cỡ trong chuyến | OutQuad lên `peak` giữa pha lên, `SmoothStep` về `to` | vật chui vào ô, không đập vào |
| nghiêng | `LeanToward × LeanRamp` | mép trước thấp như thẻ bị búng; cất cánh, đáp phẳng; `\|dx\|` < `minDx` không nghiêng |
| chuyến bắt đầu khi thân đang nhấc (sau tada) | `StartLiftOffset` tan theo cung | nối liền, 0 từ đỉnh |
| bóng, thân tan cuối chuyến | `Shadow01` từ `start01`; `FadeOutTail` InQuad sau `fadeStart` | bóng co theo cung; thân tan lúc chìm |
| hàng sau nhảy lên hàng trước | `LiftWithAnticipation` nhún `−sinkDepth` rồi parabol; `EaseOutUntil` cho cỡ, màu | nhún lấy đà rồi nhảy; to, sáng nhanh lúc đầu |
| đáp, pitch theo chỉ số ô | `Interpolator.OffsetFromCenter` | ba tiếng clack lên dần trái → phải, ô giữa đúng pitch gốc |
| bay trên toạ độ màn hình (khay UI) | `ApexT`, `ArcY` cùng hình, đơn vị pixel | khay rơi vào ô như tile |
| `lossyScaleY` ≤ 0 | `PeakToLocal` chia 1 | giữ nguyên số |

### Flow

```
dự án: vòng Step (Tween.Custom Linear, một t tuyến tính)                         Tile.StepFlight / StepRise ở dự án
  ├─ dựng chuyến: quãng → DurationForSpeed · ApexT(duration, fallDuration)       HopCurve.cs
  ├─ mỗi frame: đường đất (caller lerp) + ArcY − ground.y ──► PeakToLocal ──► thân nhấc (local y)
  │    ├─ FlightScale ──► cỡ gốc        └─ Interpolator.SmoothStep               Interpolator.cs
  │    ├─ LeanToward × LeanRamp ──► góc thân   └─ Interpolator.SmoothStep
  │    ├─ StartLiftOffset ──► nối từ độ cao tada · Shadow01 ──► bóng · FadeOutTail ──► alpha thân
  ├─ cú lên hàng trước: ProgressAfter ──► k ──► EaseOutUntil (cỡ, màu) · LiftWithAnticipation (SmoothStep → Lift)
  └─ đáp: chỉ số ô, sức chứa ──► Interpolator.OffsetFromCenter ──► pitch          ShelfPromoteTuning.LandPitch ở dự án
HopCurve: static, không state; hằng MIN_APEX_T; mọi mốc cảm giác là tham số caller truyền
```

### Thuật toán

$$apexT = max(MIN_APEX_T, 1 − fallDuration / duration)$$

$$Arc01(t) = u(2 − u),\ u = t / apexT \quad (t ≤ apexT) \qquad Arc01(t) = 1 − v^2,\ v = (t − apexT)/(1 − apexT) \quad (t > apexT)$$

$$ArcY(t) = lerp(t ≤ apexT\ ?\ y_0 : y_1,\ y_1 + fallHeight,\ Arc01(t))$$

| Ký hiệu | Nghĩa |
|---|---|
| `t` | tuyến tính 0 → 1 trên `duration`; mọi kênh đọc cùng t để cùng nhịp |
| `duration` | giây cả chuyến; `DurationForSpeed` = quãng / tốc độ, kẹp hai đầu |
| `fallDuration` | giây của pha rơi, núm tuyệt đối của dự án |
| `MIN_APEX_T = 0.5` | pha lên không ngắn hơn pha rơi |
| `fallHeight` | đỉnh cao hơn ô bao nhiêu, cùng đơn vị `y0`, `y1` (world unit, pixel) |

| Bất biến | Vì |
|---|---|
| cung trơn ở đỉnh dù hai pha dài khác nhau | OutQuad kết thúc với đạo hàm 0, InQuad bắt đầu với đạo hàm 0 |
| pha rơi giống nhau mọi chuyến | `(1 − apexT) · duration = fallDuration` khi chưa chạm kẹp ⇒ `v = s / fallDuration` (`s` giây sau đỉnh); độ cao trên ô `fallHeight · (1 − v²)` không phụ thuộc `y0`, quãng, `duration` |
| `FlightScale` không đập | OutQuad từ `from` lên `peak` tại `peakT = apexT · peakAt`, `SmoothStep` về `to`: vận tốc đổi cỡ 0 ở `peakT` và ở `t = 1` |
| `SmoothStep` phẳng hai đầu | `S(t) = 3t² − 2t³`, `S'(t) = 6t(1 − t)`; đối xứng `S(t) + S(1 − t) = 1` |
| `Lift` khớp `JumpArc` | `4·peak·t(1 − t)` = `JumpArc.EvaluateHeight(0, K, t)`, `K = ComputeArcCoefficient(0, 0, peak) = 4·peak` |
| `LiftWithAnticipation` liền | trên cung `lerp(−sink, 0, k) + Lift(k, peak)`: `k = 0` cho `−sink`, đỉnh `k = 0.5` là `peak − sink/2` |
| `OffsetFromCenter` đối xứng | `(i − (n − 1)/2) · step`: n lẻ phần tử giữa 0, n chẵn hai phần tử giữa ±step/2 |

| Hàm | Input (`apexT = 0.7`, `fallHeight = 0.8`, `y0 = 0`) | Kỳ vọng | ✓ |
|---|---|---|---|
| `ApexT` | (0.4, 0.12) · (0.2, 0.12) · (0, 0.12) | 0.7 · 0.5 (chạm kẹp) · 0.5 | GĐ1 |
| `Arc01` | 0 · 0.35 · 0.7 · 0.85 · 1 | 0 · 0.75 · 1 · 0.75 · 0 | GĐ1 |
| `ArcY` | t = 0 · 0.7 · 1 với `y1 = 2.2` | 0 · 3.0 · 2.2 | GĐ1 |
| `ArcY` hai phía đỉnh | `apexT ± 1e-3` | thấp hơn đỉnh không quá 1e-3 | GĐ1 |
| `FlightScale(t, 0.7, 0.5, 1.08, 1.21, 0.9)` | 0 · 0.35 · 1 | 1.08 · 1.21 · 0.9; đạo hàm số ≈ 0 tại 0.35 và 1 | GĐ2 |

Bản mẫu: mọi thân chép nguyên văn bản dự án, kể cả thứ tự phép nhân float; cảm giác không đổi khoá bằng trace 0 chênh ở Chuyển dự án.

---

### GĐ1 — xong thì có cung đúng, pha rơi giống nhau mọi chuyến

```
  vòng Step của dự án ──t──► HopCurve+ ──ArcY+ − ground.y──► PeakToLocal+ ──► thân nhấc (local y)
                              └─ ApexT+ · ProgressAfter+ · Arc01+ · DurationForSpeed+
```

`Runtime/Utilities/PhysXHelper/HopCurve.cs` — file mới:

```csharp
using UnityEngine;

namespace Horcrux.Runtime.Utilities.PhysXHelper
{
    /// <summary>
    /// Hình dạng cú nhảy thấp và cú bay hai pha tới ô, lái bằng một t tuyến tính: gốc đi đường đất, thân nhấc so với đường đất,
    /// nghiêng vào hướng đi, scale và alpha đổi theo pha. Mọi kênh đọc cùng t để cùng nhịp; caller giữ vòng Step và ghi lên object của mình.
    /// </summary>
    // Chỉ trả số, không driver tween: mỗi dự án ghi lên một bộ kênh thân, bóng, màu riêng.
    public static class HopCurve
    {
        /// <summary>Đỉnh của chuyến bay hai pha không sớm hơn nửa chuyến: pha lên không ngắn hơn pha rơi.</summary>
        // Hằng, không tham số: cơ chế của cung hai pha, không phải số cảm giác; mọi caller truyền 0.5.
        public const float MIN_APEX_T = 0.5f;

        /// <summary>Quãng đường đổi theo vị trí thì núm là tốc độ: thời lượng = quãng / tốc độ, kẹp hai đầu.</summary>
        public static float DurationForSpeed(float distance, float speed, float minDuration, float maxDuration)
            => Mathf.Clamp(distance / speed, minDuration, maxDuration);

        /// <summary>Độ cao world → local của gốc để thân nhấc trong không gian gốc; lossy không dương thì giữ nguyên.</summary>
        public static float PeakToLocal(float peakWorld, float lossyScaleY) => peakWorld / (lossyScaleY > 0f ? lossyScaleY : 1f);

        /// <summary>t của đỉnh chuyến bay hai pha: pha rơi chiếm đúng fallDuration giây cuối, không sớm hơn MIN_APEX_T.</summary>
        public static float ApexT(float duration, float fallDuration)
            => duration > 0f ? Mathf.Max(MIN_APEX_T, 1f - fallDuration / duration) : MIN_APEX_T;

        /// <summary>Tiến độ của pha sau mốc start: 0 tới start, rồi 0 → 1 tuyến tính ở t = 1. Pha rơi sau đỉnh, cung nhảy sau lấy đà.</summary>
        public static float ProgressAfter(float t, float start)
            => t <= start ? 0f : Mathf.Clamp01((t - start) / (1f - start));

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

        /// <summary>Độ cao nhìn thấy của thân, cùng đơn vị y0, y1: từ y0 lên y1 + fallHeight ở đỉnh rồi rơi về y1. Pha rơi chỉ phụ thuộc y1
        /// và fallHeight nên cú rơi giống nhau dù bay từ đâu.</summary>
        public static float ArcY(float t, float y0, float y1, float fallHeight, float apexT)
            => Mathf.LerpUnclamped(t <= apexT ? y0 : y1, y1 + fallHeight, Arc01(t, apexT));
    }
}
```

Editor setup:
1. Tạo file trong Project window của Unity để có `.meta`.

`Tests/EditMode/HopCurveTests.cs` (agent): chuyển nguyên văn case `Duration_*`, `ApexT_*`, `ProgressAfter_*`, `Arc01_*`, `ArcY_*`, `PeakToLocal_*` của `TileFlightCurveTests` (đổi tên type, `Duration` → `DurationForSpeed` với ba số của `new TileFlightTuning()`: 28, 0.28, 0.42), thêm biên:

| Case | Input | Kỳ vọng |
|---|---|---|
| `DurationForSpeed` | quãng 0 · 100 · 8.4 với (28, 0.28, 0.42) | 0.28 (kẹp dưới) · 0.42 (kẹp trên) · 0.3 |
| `PeakToLocal` | (1.2, 0.6) · (1.2, 1) · (1.2, 0) · (1.2, −1) | 2 · 1.2 · 1.2 · 1.2 |
| `ApexT` | (0.35, 0.12) · (0.5, 0.12) · (0.2, 0.12) · (0, 0.12) | 1 − 0.12/0.35 · 0.76 · 0.5 · 0.5 |
| `ProgressAfter(t, 0.7)` · `(t, 0.14)` | 0.5 · 0.7 · 0.85 · 1 · (0.57, 0.14) | 0 · 0 · 0.5 · 1 · 0.5 (±1e-4) |
| `Arc01(t, 0.7)` | 0 · 0.35 · 0.7 · 0.85 · 1 · −0.1 · 1.1 | 0 · 0.75 · 1 · 0.75 · 0 · 0 · 0 |
| `ArcY(t, 1, y1, 0.8, 0.7)` | t = 0 và 1, y1 ∈ {−3, 0, 2.2, 11.8} | 1 · y1 |
| `ArcY(apexT, 0, y1, 0.8, apexT)` | (y1, apexT) ∈ {(−3, 0.66), (0, 0.66), (2.2, 0.66), (11.8, 0.76)} | y1 + 0.8 (±1e-4) |
| `ArcY` pha rơi giống nhau | ba chuyến (dy, duration) ∈ {(2.2, 0.35), (11.8, 0.5), (−1, 0.42)}, `fallDuration` 0.12, s = 0 … 0.12 bước 0.03 | độ cao trên đích = 0.8 · (1 − (s/0.12)²) ở cả ba (±1e-4) |
| `ArcY` trơn ở đỉnh | `apexT ± 1e-3`, (y1, apexT) ∈ {(2.2, 0.66), (11.8, 0.76)} | thấp hơn đỉnh trong [0, 1e-3) |
| `ArcY` với `fallHeight = 0` | t = 0.5, 0.7, 1; y0 = 0, y1 = 2 | ≤ 2 ở mọi t, đúng 2 tại 0.7 và 1 |

---

### GĐ2 — xong thì cỡ, nghiêng, bóng, tan thân đọc cùng `t`, `Tile.StepFlight` đủ hàm để trỏ về

```
  vòng Step ──t──► HopCurve ──FlightScale+──► cỡ gốc        ──LeanToward+ × LeanRamp+──► góc thân
                     │        ──ArcY + StartLiftOffset+──► thân nhấc   ──Shadow01+──► bóng   ──FadeOutTail+──► alpha thân
                     └─ LeanRamp, FlightScale ──► Interpolator.SmoothStep+
```

`Interpolator.cs` › sau `RemapPrecomputed`, trước `SmootherStep`. Dòng có sẵn ngay dưới chỗ thêm: `/// <summary>Perlin's quintic smoothstep: zero velocity and acceleration at both ends (smoother than cubic smoothstep).</summary>`

```csharp
        /// <summary>Cubic smoothstep: zero velocity at both ends.</summary>
        /// <remarks>S(t) = 3t^2 - 2t^3 = t^2 (3 - 2t)</remarks>
        /// <param name="t">Progress, clamped to [0,1].</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float SmoothStep(float t)
        {
            // Clamp ở đây, không ở caller: LeanRamp đưa (1 − t)/(1 − fallStart) vượt 1 ở biên.
            t = Mathf.Clamp01(t);
            return t * t * (3f - 2f * t);
        }

```

`HopCurve.cs` › sau `ArcY`, trước `}` đóng class:

```csharp
        /// <summary>Góc để mép trước thấp như thẻ bị búng: dx âm (sang trái) cho góc dương. |dx| dưới minDx thì 0.</summary>
        // Mốc, ngưỡng mọi hàm dưới là tham số bắt buộc: mặc định đóng băng nháp một dự án thành chuẩn.
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
```

`SmoothStep` vào `Tests/EditMode/InterpolatorTests.cs` (agent, file mới); hàm `HopCurve` vào `HopCurveTests.cs` (agent): chuyển nguyên văn case `LeanRamp_*`, `BodyFade_*`, `FlightScale_*`, `StartLiftOffset_*`, `Shadow01_*`, `LeanToward_*` của dự án với `riseEnd 0.3`, `fallStart 0.75`, `minDx 0.05`, `peakAt 0.5` (thay `TileFlightCurve.SCALE_PEAK_OF_RISE`).

| Case | Input | Kỳ vọng |
|---|---|---|
| `SmoothStep` | 0 · 0.5 · 1 · −1 · 2 | 0 · 0.5 · 1 · 0 · 1 |
| `SmoothStep` đối xứng | t ∈ {0.1, 0.3} | `S(t) + S(1 − t) = 1` |
| `LeanRamp(t, 0.3, 0.75)` | 0 · 1 · 0.3 · 0.5 · 0.75 | 0 · 0 · 1 · 1 · 1 |
| `LeanRamp(t, 0.3, 0.75)` đơn điệu | dãy 0.05 bước tới 0.3; dãy 0.8 bước 0.05 tới 1 | tăng; giảm |
| `LeanRamp(t, 0.3, fallStart)`, fallStart ∈ {0.66, 0.8} | fallStart · (1 + fallStart)/2 · 1 | 1 · 0.5 · 0 |
| `LeanRamp` với `riseEnd = fallStart = 0.5` | 0.25 · 0.5 · 0.75 | 0.5 · 1 · 0.5 (không giữ đỉnh) |
| `LeanToward(dx, 10, 0.05)` | −2 · 2 · ±0.049 · (0.05, 6°) | 10 · −10 · 0 · −6 |
| `FadeOutTail(t, 0.85)` | 0 · 0.5 · 0.85 · 1 | 1 · 1 · 1 · 0 |
| `FadeOutTail` InQuad | (0.925, 0.85) · (0.75, 0.5) | 0.75 · 0.75 |
| `StartLiftOffset(t, 0.6, 0.3)` | 0 · 0.6 · 0.8 · 1 | 0.3 · 0 · 0 · 0 |
| `StartLiftOffset` | dãy 0 … 0.6 bước 0.05 · `startLift = 0` | không tăng · 0 ở mọi t |
| `Shadow01(t, 0.6, 0)` | t ∈ {0, 0.2, 0.6, 0.85, 1} | bằng `Arc01(t, 0.6)` tuyệt đối |
| `Shadow01(t, 0.6, 0.8)` | 0 · 0.6 · 1 | 0.8 · 1 · 0 |
| `FlightScale(t, 0.64, 0.5, 0.947, 1.06, to)` | 0 · 0.32 · (1, to ∈ {0.9, 0.67, 1.3}) | 0.947 · 1.06 · to |
| `FlightScale` hình dạng | 0.16 (giữa pha lên, OutQuad) · 0.66 (giữa pha về, SmoothStep) | giá trị theo công thức (chép số từ test dự án) |
| `FlightScale` đạo hàm số | tại `peakT` hai phía và tại 1, `eps = 1e-3` | lệch < 1e-3 |
| `FlightScale` với `to > peak` | t ∈ (peakT, 1), `(0.67, 0.75, 0.9)` | tăng đơn điệu; tại 0.66 là 0.825, tại 1 là 0.9 |
| `FlightScale` với `peakAt = 1` | t = apexT | đúng `peak` (đỉnh cỡ trùng đỉnh cung) |

---

### GĐ3 — xong thì có cú nhảy lấy đà lên hàng trước và pitch đáp theo chỉ số ô

```
  vòng Step của cú lên hàng trước ──t──► HopCurve ──ProgressAfter──► k ──EaseOutUntil+──► cỡ, màu
                                                   ──LiftWithAnticipation+ (SmoothStep → Lift+)──► thân nhấc
  đáp ──chỉ số ô, sức chứa──► Interpolator.OffsetFromCenter+ ──► pitch
```

`HopCurve.cs` › sau `FlightScale`, trước `}` đóng class:

```csharp
        /// <summary>Parabol 0 → peak → 0, đỉnh ở t = 0.5; cùng JumpArc khi hai đầu ngang nhau (K = 4·peak).</summary>
        // Public dù chỉ LiftWithAnticipation gọi: test chéo với JumpArc giữ hai bản khớp nhau.
        public static float Lift(float t, float peak) => peak * 4f * t * (1f - t);

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
```

`Interpolator.cs` › sau `SmoothStep`, trước `SmootherStep`:

```csharp
        /// <summary>Offset of item <paramref name="index"/> in a row of <paramref name="count"/> items spaced by
        /// <paramref name="step"/>, centered on 0: the middle item is 0, the row is symmetric.</summary>
        /// <remarks>(index - (count - 1) / 2) * step</remarks>
        // Thứ tự phép tính nguyên văn LandPitch: pitch không vào trace, chỉ test bằng nhau tuyệt đối khoá.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float OffsetFromCenter(int index, int count, float step) => (index - (count - 1) * 0.5f) * step;

```

Chuyển nguyên văn case `Lift_*`, `RiseLift_*`, `RiseRamp_*` của dự án vào `HopCurveTests.cs` (agent); `OffsetFromCenter` vào `InterpolatorTests.cs` (agent).

| Case | Input | Kỳ vọng |
|---|---|---|
| `Lift` | (0, 1.2) · (1, 1.2) · (0.5, 1.2) · (0.2, 0.9) so (0.8, 0.9) | 0 · 0 · 1.2 · bằng nhau |
| `Lift` so `JumpArc` | t ∈ {0, 0.25, 0.5, 0.9, 1}, peak 0.8 | bằng `JumpArc.EvaluateHeight(0, JumpArc.ComputeArcCoefficient(0, 0, 0.8), t)` |
| `LiftWithAnticipation(t, 1.1, 0.04, 0.14)` | 0 · 1 · 0.14 · 0.1401 · 0.57 | 0 · 0 · −0.04 · −0.04 (±1e-2) · 1.1 − 0.02 (±1e-3) |
| `LiftWithAnticipation` với `sinkDepth = 0` | t ∈ {0.14, 0.57} | 0 · 1.1 (chỉ còn cung) |
| `EaseOutUntil(k, 0.6)` | 0 · 0.3 · 0.6 · 1 | 0 · 0.75 · 1 · 1 |
| `OffsetFromCenter(i, 3, 0.03)` | i = 0 · 1 · 2 | −0.03 · 0 · 0.03 |
| `OffsetFromCenter` | (0, 1, 5) · (0, 4, 1) · (3, 4, 1) · (0, 0, 1) | 0 · −1.5 · 1.5 · 0.5 (count 0 không có nghĩa, không ném) |
| `OffsetFromCenter(i, n, s)` so `(i − (n − 1) * 0.5f) * s` | i ∈ 0…4, n ∈ 1…5, s ∈ {0.03, 0.7} | bằng nhau tuyệt đối |

---

## Họ B — `DipSpec`, `FlashSpec`, `InsertFlash`: vật nhận lún và loé

### Kịch bản

| Tình huống | Hệ làm gì | Caller thấy |
|---|---|---|
| vật nhận cú chạm (tile đáp vào box) | `DipSpec.EvaluateScaleY(t)` trên một `Tween.Custom` Linear: hệ số y xuống `ScaleY` (OutQuad) rồi về 1 (OutBack) | lún rồi bật về vượt 1 một nhịp; caller nhân scale nghỉ, bù trục kia |
| `default(DipSpec)` dựng bằng code | `Duration` 0, chia 0 | NaN — phải qua constructor |
| loé lòng khung, nắp | `sequence.InsertFlash(at, sprite, flash)`: bật renderer, alpha lên `PeakAlpha` (OutQuad), về 0 (InQuad), tắt renderer | loé trong sequence của caller; sprite nghỉ không tốn lượt vẽ |
| hai cú loé chồng trên một sprite | callback sau thắng | cuối cùng tắt, alpha 0 |
| sprite bị huỷ giữa loé | PrimeTween bỏ tween trên target đã huỷ | không exception |

### Flow

```
vật nhận cú chạm
  ├─ [SerializeField] DipSpec dip ──Tween.Custom(0→1, dip.Duration, Linear)──► dip.EvaluateScaleY(t)    DipSpec.cs
  │       └─► caller: ApplyScaleY nhân scale nghỉ, bù trục kia (SquashStretch)                       BoxAnimation.ApplyDip ở dự án
  └─ [SerializeField] FlashSpec flash ──sequence.InsertFlash(at, SpriteRenderer, flash)                FlashSpec.cs, SequenceExtensions.cs
          └─► 2 InsertCallback (bật, tắt) + 2 Tween.Alpha trong sequence của caller                  BoxAnimation.InsertReceiveAccents, PlayComplete
```

### Thuật toán

| Mảnh | Công thức | Mốc |
|---|---|---|
| `EvaluateScaleY` | `downEnd = Down / (Down + Up)`; pha xuống `lerp(1, ScaleY, OutQuad(t/downEnd))`, pha về `lerpUnclamped(ScaleY, 1, OutBack(…))` | `0 → 1`, `downEnd → ScaleY`, `1 → 1` |
| mốc tắt loé | `(at + Rise) + Fall` | trùng mốc tween về 0 kết thúc |

---

### GĐ4 — xong thì mọi vật nhận lún có một struct để serialize và một hàm gọi trong `Tween.Custom`

```
  caller ──Tween.Custom(0→1, dip.Duration, Linear)──► dip.EvaluateScaleY+(t) ──hệ số y──► caller nhân scale nghỉ, SquashStretch.GetVolumePreservingScale
```

`Runtime/Utilities/Tweening/DipSpec.cs` — file mới:

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
    // Field PascalCase, khác SquashStretchSpec: tên field là key YAML prefab dự án, đổi tên thì về 0 im lặng.
    public struct DipSpec
    {
        [Range(0.7f, 1f)] public float ScaleY;
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
            // PrimeTween.Easing, không Easer của Horcrux: cùng hàm ease với tween đang chạy ở caller.
            return t < downEnd
                ? Mathf.Lerp(1f, ScaleY, Easing.Evaluate(t / downEnd, Ease.OutQuad))
                : Mathf.LerpUnclamped(ScaleY, 1f, Easing.Evaluate((t - downEnd) / (1f - downEnd), Ease.OutBack));
        }
    }
}
```

Editor setup:
1. Tạo file trong Project window để có `.meta`; field kiểu `DipSpec` hiện trong Inspector.

`Tests/EditMode/DipSpecTests.cs` (agent): chuyển nguyên văn 5 case `DipScaleY_*` của `BoxFeelCurveTests`.

| Case | Input | Kỳ vọng |
|---|---|---|
| `new DipSpec(0.86, 0.05, 0.22)` | `Duration` · `EvaluateScaleY` tại 0 · `downEnd` · 1 | 0.27 · 1 · 0.86 · 1 |
| pha xuống | `downEnd / 2` | trong (0.86, 1), khác hai đầu |
| pha về OutBack | `downEnd + 0.7 · (1 − downEnd)` | > 1 |
| `DownDuration` rất nhỏ | (0.86, 0.01, 0.22), t = 0.01/0.23 | 0.86 (không chia 0, `Min` chặn 0 trong Inspector) |
| t ngoài dải | −0.1 · 1.1 | pha xuống `Lerp` clamp → 1; pha về `Evaluate(>1)`: ghi giá trị thực tế vào test làm mốc, caller không đưa t ngoài [0, 1] |
| `default(DipSpec)` | `Duration` | 0; `EvaluateScaleY` chia 0 → NaN: struct dựng bằng code phải qua constructor |

---

### GĐ5 — xong thì caller chèn cú loé vào `Sequence` bằng một lời gọi

```
  caller ──sequence.InsertFlash+(at, sprite, flash)──► [at] enabled = true · Alpha → PeakAlpha (Rise, OutQuad)
                                                         [at + Rise] Alpha → 0 (Fall, InQuad) · [at + Rise + Fall] enabled = false
```

`Runtime/Utilities/Tweening/FlashSpec.cs` — file mới:

```csharp
using System;
using UnityEngine;

namespace Horcrux.Runtime.Utilities.Tweening
{
    /// <summary>Loé một sprite: alpha lên PeakAlpha trong RiseDuration (OutQuad) rồi về 0 trong FallDuration (InQuad).</summary>
    [Serializable]
    // Field PascalCase: key YAML trên prefab dự án (như DipSpec).
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

`Runtime/Utilities/ExtensionMethods/SequenceExtensions.cs` — file mới (extension `Sequence` đầu tiên của Horcrux):

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
            // (at + Rise) + Fall cùng thứ tự cộng với tween về 0: tắt không chạy trước frame cuối.
            return sequence.InsertCallback(at + flash.RiseDuration + flash.FallDuration, target, static renderer => renderer.enabled = false);
        }
    }
}
```

Editor setup: không — sprite loé của dự án đã nằm trong prefab với alpha 0 và renderer tắt.

`Tests/PlayMode/CharmAccentTests.cs` (agent, file mới, khung `CharmJumpTests`: `SetTweensCapacity`, `isPaused`, `GC.Alloc` recorder); `SpriteRenderer` dựng trên `GameObject` mới, alpha 0, `enabled = false`:

| Case | Kiểm | Kỳ vọng |
|---|---|---|
| chèn ở `at = 0.1`, `(0.6, 0.04, 0.08)` | `enabled` trước 0.1 s · tại 0.1 + 0.02 s · sau 0.1 + 0.12 s | false · true, alpha > 0 · false, alpha 0 |
| đỉnh | alpha tại 0.1 + 0.04 s | 0.6 (±1e-3) |
| `at = 0` | `enabled` ở frame đầu sau khi tạo | true |
| hai cú loé chồng (`at` 0 và 0.05, cùng sprite) | sau 0.05 + 0.12 s | `enabled` false, alpha 0 (callback sau thắng) |
| object huỷ giữa loé | `Destroy` sprite ở 0.02 s rồi chờ hết | không exception (PrimeTween bỏ tween trên target đã huỷ) |
| GC | mỗi lời gọi sau lượt khởi động | 0 B |

---

## Họ C — `SinkSpec`, `PopSpec`, `CharmSinkLocalY`, `CharmPop`: lún vị trí và nảy một pha

### Kịch bản

| Tình huống | Hệ làm gì | Caller thấy |
|---|---|---|
| ô nhận vật (khay nhựa bị đè), ô dồn trái | `CharmSinkLocalY(restLocalY, sink)`: y xuống `Depth` theo `DownEase`, về `restLocalY` (OutBack) | khay lún rồi bật về đúng chỗ; gọi rời, không giữ handle |
| gọi lại khi đang lún | caller `Stop()` rồi gọi; mốc nghỉ là tham số đọc ở Awake | không trôi mốc nghỉ |
| `DownEase` thiếu key YAML | giá trị 0 = `Ease.Default`, PrimeTween đọc thành OutQuad | ô xuống "bật" thay vì "bị đè" |
| khung, số đếm nảy sau một nhịp | `CharmPop(restScale, pop)`: chờ `Delay`, lên `rest × Scale` rồi về, mỗi lượt nửa `Duration` (InOutSine Yoyo) | một lượt lên rồi về, không rung quanh nghỉ |
| khung nảy trong sequence nhận | `sequence.Insert(at, frame.CharmPop(…))` | từng frame trùng bản tay `Insert(at + Delay, Tween.Scale(…))` |
| `Depth = 0` · `Scale = 1` | tween không đổi giá trị | đứng yên, không exception |

### Flow

```
ô nhận vật ──► [SerializeField] SinkSpec sink ──tr.CharmSinkLocalY(restLocalY, sink)            TransformExtensions.Accent.cs, SinkSpec.cs
                 └─► Sequence: LocalPositionY → rest − Depth (Down, DownEase) ──► → rest (Up, OutBack)    CacheTileSlot.PlaySpring ở dự án
khung, số đếm ──► [SerializeField] PopSpec pop ──tr.CharmPop(restScale, pop)                    TransformExtensions.Accent.cs, PopSpec.cs
                 └─► Sequence: ChainDelay(Delay) ──► Tween.Scale rest ↔ rest × Scale, 2 cycle Yoyo
                 caller: chạy rời (CategoryBox.UpdateCounter, SingleShelfAmountBar.PlayPop) hoặc sequence.Insert(at, …) (BoxAnimation)
caller Stop() trước khi gọi lại; về nghỉ (đặt lại scale, y) là việc của caller
```

---

### GĐ6 — xong thì ô cache và mọi vật cứng bị đè có một hình lún vị trí dùng chung

```
  caller ──tr.CharmSinkLocalY+(restLocalY, sink)──► Sequence: LocalPositionY → rest − Depth (Down, DownEase) ──► LocalPositionY → rest (Up, OutBack)
```

`Runtime/Utilities/Tweening/SinkSpec.cs` — file mới:

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
    // Field PascalCase: key YAML trên prefab dự án (như DipSpec).
    public struct SinkSpec
    {
        [Min(0f)] public float Depth;
        [Min(0.01f)] public float DownDuration;
        [Min(0.01f)] public float UpDuration;
        [Tooltip("InSine: khay bị đè. OutQuad: vật rơi chạm đáy.")]
        // Thiếu key YAML thì 0 = Ease.Default, PrimeTween đọc thành OutQuad: chuyển dự án ghi key thẳng vào prefab.
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

`Runtime/Utilities/ExtensionMethods/Transform/TransformExtensions.Accent.cs` — file mới (partial):

```csharp
using Horcrux.Runtime.Utilities.Tweening;
using PrimeTween;
using UnityEngine;

namespace Horcrux.Runtime.Utilities.ExtensionMethods
{
    public static partial class TransformExtensions
    {
        /// <summary>Lún local y từ mốc nghỉ rồi về đúng mốc nghỉ. restLocalY đọc ở Awake, không đọc lúc gọi.</summary>
        // Như CharmJump: trả Sequence, không ct, scaled — được nhét vào Sequence cha, PrimeTween không cho tween con mang token.
        // restLocalY là tham số: đọc localPosition lúc gọi thì lún gọi lại khi đang lún trôi mốc nghỉ dần.
        public static Sequence CharmSinkLocalY(this Transform self, float restLocalY, in SinkSpec spec)
            => Sequence.Create()
                .Chain(Tween.LocalPositionY(self, restLocalY - spec.Depth, spec.DownDuration, spec.DownEase))
                .Chain(Tween.LocalPositionY(self, restLocalY, spec.UpDuration, Ease.OutBack));
    }
}
```

Editor setup: không — field `SinkSpec` hiện trong Inspector; `DownEase` chọn từ enum `Ease`.

`SinkSpec` vào `Tests/EditMode/AccentSpecTests.cs` (agent, file mới); `CharmSinkLocalY` vào `CharmAccentTests.cs` (agent):

| Case | Kiểm | Kỳ vọng |
|---|---|---|
| `new SinkSpec(0.12, 0.06, 0.18, InSine).Duration` | — | 0.24 |
| `new SinkSpec()` | `DownEase` | `Ease.Default` (0): ghi lại bẫy YAML thiếu key |
| `CharmSinkLocalY(rest 0.5, (0.12, 0.06, 0.18, InSine))` | y tại 0.06 s · y sau `Duration` · `isAlive` | ≈ 0.38 · đúng 0.5 · false |
| `rest = 0` | y tại 0.06 s | ≈ −0.12 |
| gọi lại khi đang lún (`Stop()` rồi gọi, y đang ở 0.4) | y sau `Duration` | đúng 0.5, không trôi |
| `Depth = 0` | y mọi lúc | 0.5 (tween không đổi giá trị, không exception) |
| chèn vào `Sequence` cha ở mốc 0.1 (`seq.Insert(0.1f, tr.CharmSinkLocalY(…))`) | y trước 0.1 s · y tại 0.1 + 0.06 s | 0.5 · ≈ 0.38 |
| GC | mỗi lời gọi sau lượt khởi động | 0 B |

---

### GĐ7 — xong thì khung box, bộ đếm box, số lớp kệ đọc một hình nảy

```
  caller ──tr.CharmPop+(restScale, pop)──► Sequence: ChainDelay(Delay) ──► Scale rest → rest × Scale, Duration/2, InOutSine, 2 cycle Yoyo
  sequence cha ──Insert(at, …CharmPop(…))──► sequence con chạy từ mốc at
```

`Runtime/Utilities/Tweening/PopSpec.cs` — file mới:

```csharp
using System;
using UnityEngine;

namespace Horcrux.Runtime.Utilities.Tweening
{
    /// <summary>Nảy một pha trên scale nghỉ: sau Delay, lên nghỉ × Scale rồi về nghỉ, mỗi lượt nửa Duration (InOutSine).</summary>
    [Serializable]
    // Field PascalCase: key YAML trên prefab dự án (như DipSpec).
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

`TransformExtensions.Accent.cs` › sau `CharmSinkLocalY`, trước `}` đóng class:

```csharp
        /// <summary>Nảy một pha trên mốc nghỉ: chờ spec.Delay, lên restScale × spec.Scale rồi về restScale, mỗi lượt nửa Duration (InOutSine).
        /// Trả Sequence vì startDelay của PrimeTween lặp mỗi cycle. Chèn vào sequence khác bằng Insert(at, …).</summary>
        public static Sequence CharmPop(this Transform self, Vector3 restScale, in PopSpec spec)
            => Sequence.Create()
                .ChainDelay(spec.Delay)
                .Chain(Tween.Scale(self, restScale, restScale * spec.Scale, spec.Duration * 0.5f, Ease.InOutSine, 2, CycleMode.Yoyo));
```

`CharmPop` vào `CharmAccentTests.cs` (agent):

| Case | Kiểm | Kỳ vọng |
|---|---|---|
| `CharmPop(rest (2,2,2), (1.25, 0.06, 0.15))` | scale trước 0.06 s · tại 0.06 + 0.075 s · sau 0.06 + 0.15 s · `isAlive` | (2,2,2) · ≈ (2.5,2.5,2.5) · đúng (2,2,2) · false |
| `Delay = 0` | scale tại 0 · tại `Duration/2` | nghỉ · đỉnh |
| `Scale = 1` | scale mọi lúc | nghỉ (không exception) |
| chèn `seq.Insert(at, tr.CharmPop(rest, pop))` so bản tay `seq.Insert(at + Delay, Tween.Scale(tr, rest, rest × Scale, Duration/2, InOutSine, 2, Yoyo))` | scale mỗi frame của hai `Transform` trong cùng một `Sequence` cha, `at = 0.03` | bằng nhau tuyệt đối: thay thế ở `InsertReceiveAccents` không đổi một khung |
| gọi lại giữa chừng (`Stop()`, đặt lại scale nghỉ, gọi) | scale sau `Delay + Duration` | đúng nghỉ |
| GC | mỗi lời gọi sau lượt khởi động | 0 B |

---

## Họ D — `LaunchSpec`: nén rồi vọt đi

### Kịch bản

| Tình huống | Hệ làm gì | Caller thấy |
|---|---|---|
| nắp đóng xuống box ở mốc `touch` | caller: một `Tween.Custom` OutQuad trong `DownDuration`, nén `LerpUnclamped(1, ScaleY, t)` và nhún `−SinkDepth · t` trên cùng t | đáy nhún trùng lúc nén sâu nhất |
| giữ đáy | `LaunchDelay = DownDuration + HoldDuration` | vật bật đúng mốc |
| bật | `Tween.Custom` Linear trên `StretchDuration` → `EvaluateStretchScaleY(t)`: từ `ScaleY` lên `StretchScaleY` (OutQuad) rồi về 1 (InOutSine); caller tự tween vị trí bay | nén lật thành giãn lúc vận tốc lớn nhất, liền với pha nén |
| `HoldDuration = 0` | `LaunchDelay = DownDuration` | bật ngay khi chạm đáy |
| `StretchScaleY = 1` | pha bật chỉ về 1 | không giãn |
| `default(LaunchSpec)` | `StretchDuration` 0, chia 0 | NaN — phải qua constructor |

### Flow

```
[SerializeField] LaunchSpec launch                                                     LaunchSpec.cs
  ├─ [touch] Tween.Custom(OutQuad, DownDuration) ──► caller: LerpUnclamped(1, ScaleY, t) · −SinkDepth · t   BoxAnimation.ApplyLandCompress ở dự án
  └─ [touch + LaunchDelay] Tween.Custom(Linear, StretchDuration) ──► launch.EvaluateStretchScaleY(t) ──► ApplyScaleY   BoxAnimation.PlayComplete ở dự án
```

### Thuật toán

`peakEnd = Rise / (Rise + Settle)`; pha lên `lerp(ScaleY, StretchScaleY, OutQuad)` bắt đầu đúng ở hệ số nén cuối (liền với pha nén), pha về `lerp(StretchScaleY, 1, InOutSine)`. Mốc: `0 → ScaleY`, `peakEnd → StretchScaleY`, `1 → 1`.

---

### GĐ8 — xong thì vật nhận rồi vọt đi có struct núm và hàm pha bật

```
  caller ──[touch] Tween.Custom(OutQuad, DownDuration)──► caller: LerpUnclamped(1, ScaleY, t) · −SinkDepth · t   (hai dòng của caller)
         ──[touch + LaunchDelay+] Tween.Custom(Linear, StretchDuration+)──► launch.EvaluateStretchScaleY+(t) ──► ApplyScaleY
```

`Runtime/Utilities/Tweening/LaunchSpec.cs` — file mới:

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
    // Field PascalCase: key YAML trên prefab dự án (như DipSpec).
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
        // Chỉ pha bật có hàm: nén, nhún là hai dòng caller trên cùng một t, giữ bất biến một chỗ.
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

`Tests/EditMode/LaunchSpecTests.cs` (agent): chuyển nguyên văn 4 case `LaunchScaleY_*` của `BoxFeelCurveTests`.

| Case | Input | Kỳ vọng |
|---|---|---|
| `new LaunchSpec(0.87, 0.2, 0.15, 0.03, 1.06, 0.05, 0.15)` | `LaunchDelay` · `StretchDuration` | 0.23 · 0.2 |
| `EvaluateStretchScaleY` | 0 · `peakEnd` (0.25) · 1 · `peakEnd + 0.5 · (1 − peakEnd)` | 0.87 · 1.06 · 1 · trong (1, 1.06) |
| `HoldDuration = 0` | `LaunchDelay` | bằng `DownDuration` |
| `StretchScaleY = 1` | `EvaluateStretchScaleY` tại `peakEnd` và 1 | 1 · 1 (về đúng nghỉ, không giãn) |
| `default(LaunchSpec)` | `StretchDuration` | 0; `EvaluateStretchScaleY` chia 0 → NaN: dựng bằng code phải qua constructor |

---

## Chuyển dự án (agent)

| Chỗ | Đổi |
|---|---|
| thứ tự với Plan khác | `Tile.cs`, `TileSuckCurve.cs`, `TileSuckCurveTests.cs` chung với Chuyển dự án của [ArchedControlPoint](Horcrux_ArchedControlPoint_Plan.md) · `BoosterFlight.cs`, `Scripts/Feel.md` với [UIProxyFlight](Horcrux_UIProxyFlight_Plan.md) · dòng "Máy kiểm" của `Feel.md` với [SwirlPath](Horcrux_SwirlPath_Plan.md), [FeelUtilities](Horcrux_FeelUtilities_Plan.md) |
| trace trước khi sửa | quay mốc bằng `docs/tools/feel_trace/trace.cs` trên code hiện tại, hai lần cùng chữ ký 0 chênh; kịch bản phủ khung, bộ đếm nảy, ô cache nhún khi nhận và khi dồn, tile cuối đón mạnh rồi nắp chạm, nén–giãn khi vọt và loé nắp, chuyến bay hai pha, tada, thanh đếm kệ Single, cú hút vào bình Pack rồi chìm, khay Extra Slot rơi vào ô |
| `docs/Runtime/HopCurve/HopCurve.md` | tạo, mở đầu `Code: Runtime/Utilities/PhysXHelper`: Flow họ A · bảng kênh → hàm → hình dạng → mốc kiểm · Thuật toán họ A · "Design decisions" từ comment và dòng `bỏ` của GĐ1–GĐ3 · cách dựng vòng `Step` (một `Tween.Custom` Linear, mọi kênh đọc cùng t), tên trung tính |
| `docs/Runtime/HopCurve/HopCurve.html` | tạo từ `.md`: kịch bản (vật bay tới ô, vật nhảy lên bậc), cách dùng (mốc nào truyền, đọc gì mỗi frame), đồ thị tĩnh cung hai pha, lưu ý `peakAt`, `riseEnd`, `fallStart` là số cảm giác của từng game |
| `docs/Runtime/CharmAccent/CharmAccent.md` | tạo, mở đầu `Code: Runtime/Utilities/Tweening, Runtime/Utilities/ExtensionMethods`: họ B, C, D cùng khung `CharmJump.md` (trả `Sequence` không `ct`, ví dụ chèn vào `Sequence` cha) · "Design decisions" từ GĐ4–GĐ8 · bẫy: `DownEase` thiếu key YAML, `default(Spec)` chia 0, field PascalCase là key YAML, caller `Stop()` và đặt lại nghỉ trước khi gọi lại |
| `docs/Runtime/CharmAccent/CharmAccent.html` | tạo từ `.md`: kịch bản (lún, loé, khung nảy, số nảy; vật vọt đi), cách dùng (field serialize, một lời gọi), timeline một cú nhận và một cú vọt, lưu ý |
| `docs/Runtime/Interpolator/Interpolator.html` | mục 3: `SmoothStep` cubic có thật trong code · mục 9 bảng API: thêm `SmoothStep(t)`, `OffsetFromCenter(i, n, step)`; không dựng `Interpolator.md` ở Plan này |
| `Plans/FeelingPlan.md` | dòng trỏ "`Horcrux_HopCurve_DipFlash_Plan.md` giai đoạn/Task …" → "đã có: `docs/Runtime/HopCurve`, `docs/Runtime/CharmAccent`"; `Projectile/Ballistics` thêm "cú bay hai pha vào ô: `HopCurve.ArcY`" |
| số trên prefab, mặc định C# | nháp của developer: đọc lại từ prefab lúc làm, không chép từ Plan |
| `Tile.cs` › using, `Properties` | thêm `using Horcrux.Runtime.Utilities.PhysXHelper;` nếu chưa có; trước `[SerializeField] protected TileView _view;` thêm `LEAN_RISE_END = 0.3f`, `LEAN_FALL_START = 0.75f` (summary "Mốc trả nghiêng của cú lên hàng trước; chuyến bay tới ô trả nghiêng từ đỉnh."), `LEAN_MIN_DX = 0.05f` (summary "Dưới độ dời ngang này vật đi gần như thẳng đứng: không nghiêng."), `SCALE_PEAK_OF_RISE = 0.5f` (summary "Đỉnh scale của chuyến bay tới ô ở giữa pha đi lên."), cả bốn `private const float` |
| `Tile.cs` › mọi call site | `TileFlightCurve.X` → `HopCurve.X` cùng tham số (`ApexT`, `ArcY`, `StartLiftOffset`, `ProgressAfter`, `Shadow01`, `PeakToLocal`) ở `BuildFlightState`, `StepFlight`, `StepFinale`, `AnimateToFrontRow`, `StepRise`, `SinkIntoSlot` |
| `Tile.FlyToAsync`, `BuildSuckMotion` | `Duration(distance, flight)` → `DurationForSpeed(distance, flight.Speed, flight.MinDuration, flight.MaxDuration)`; `TileSuckCurve.DashDuration(Vector3.Distance(back, to), suck)` → `HopCurve.DurationForSpeed(…, suck.Speed, suck.MinDuration, suck.MaxDuration)` |
| `Tile` › `LeanToward` (`BuildFlightState`, `AnimateToFrontRow`, `BuildSuckMotion`) | thêm tham số cuối `LEAN_MIN_DX` |
| `Tile.StepFlight` | `FlightScale(t, apexT, from, peak, to)` → `FlightScale(t, apexT, SCALE_PEAK_OF_RISE, from, peak, to)` · `BodyFade(fall, flight.FallFadeStart)` → `FadeOutTail(…)` · `LeanRamp(t, apexT)` → `LeanRamp(t, LEAN_RISE_END, apexT)` |
| `Tile.StepRise` | `RiseRamp` → `EaseOutUntil` (×2) · `RiseLift(t, peak, rise.SinkDepth, rise.SinkEnd)` → `LiftWithAnticipation(…)` · `LeanRamp(k)` → `LeanRamp(k, LEAN_RISE_END, LEAN_FALL_START)` |
| `TileSuckCurve.cs`, `TileSuckCurveTests.cs` | xoá `DashDuration` và case `DashDuration_IsDistanceOverSpeed_Clamped` (GĐ1 đã phủ); `WindupBack`, `LeanHold` ở lại; `DashPoint` còn thì ở lại (đã qua `Horcrux_ArchedControlPoint_Plan.md` thì không còn) |
| `TileFlightCurve.cs` | xoá hẳn cùng `.meta`; summary `TileRiseTuning` trong `TileFeelTuning.cs`: "Hình dạng ở `TileFlightCurve.Rise*`" → "Hình dạng ở `HopCurve.LiftWithAnticipation`, `EaseOutUntil`" |
| `BoxFeelTuning.cs` | xoá `DipTuning`, `FlashTuning`, `LaunchTuning`, `BoxFeelCurve.DipScaleY`, `LaunchScaleY`; giữ `BoxFeelCurve.LidStart`, `BoxCompleteTuning`; `Launch` kiểu `LaunchSpec`, `LidFlash` kiểu `FlashSpec` (cùng tên field, cùng mặc định); thêm `using Horcrux.Runtime.Utilities.Tweening;` |
| `BoxReceiveTuning.cs` | `Dip` → `DipSpec`, `Flash` → `FlashSpec`; ba núm `FramePunch*` → `[Tooltip("Khung phồng lên rồi về sau đế một nhịp: lực đi từ khung xuống đế rồi bật lại.")] public PopSpec FramePunch = new(1.10f, 0.03f, 0.18f);`; xoá `CounterPopTuning` |
| `BoxAnimation.cs` | `ApplyDip(float t, in DipSpec dip) => ApplyScaleY(dip.EvaluateScaleY(t))` · `BoxFeelCurve.LaunchScaleY(t, anim.complete.Launch)` → `anim.complete.Launch.EvaluateStretchScaleY(t)` · `InsertFlash(seq, at, sprite, spec)` → `seq.InsertFlash(at, sprite, spec)` (`PlayComplete`, `InsertReceiveAccents`), xoá `InsertFlash` private · hai dòng punch → `sequence.Insert(at, frame.transform.CharmPop(_frameRestScale, tuning.FramePunch));` · `ApplyLandCompress` giữ · thêm `using Horcrux.Runtime.Utilities.Tweening;` |
| `CategoryBox.cs` | `CounterPopTuning counterPop` → `PopSpec counterPop`; `UpdateCounter`: `_counterPopTween = _counterText.transform.CharmPop(_counterOriginalScale, counterPop);`, bỏ comment hai dòng về `ChainDelay`; thêm `using …ExtensionMethods;`, `using …Tweening;` |
| `BoosterFlight.cs` | `TileFlightCurve.ApexT`, `ArcY` → `HopCurve.` cùng tham số (`FlyAsync` nhánh `fallHeight > 0`, `SetDropIn`); comment `SetDropIn` trỏ `HopCurve`; thêm `using …PhysXHelper;`; `Horcrux_UIProxyFlight_Plan.md` đã chuyển trước thì bỏ dòng này |
| `SingleShelfAmountBar.cs` | `CounterPopTuning amountPop` → `PopSpec amountPop`; `PlayPop`: `_popSequence = text.CharmPop(_amountRestScale, amountPop);`, bỏ comment về `ChainDelay` |
| `CacheTileSlot.cs` | xoá `SlotSpringTuning`; `landSpring = new(0.12f, 0.06f, 0.18f, Ease.InSine)`, `shiftSpring = new(0.08f, 0.06f, 0.14f, Ease.InSine)` kiểu `SinkSpec`; `PlaySpring(in SinkSpec spring) => transform.CharmSinkLocalY(_restLocalY, spring);` vẫn không giữ handle |
| `ShelfPromoteTuning.cs` | `LandPitch` → `1f + Interpolator.OffsetFromCenter(slotIndex, capacity, PitchStep)`; thêm `using …PhysXHelper;` |
| `Tests/EditMode/TileFlightCurveTests.cs` | xoá sau khi GĐ1–GĐ3 đã chuyển 78 case; 4 case `LandPitch_CentersOnMiddleSlot` sang file mới `ShelfPromoteTuningTests.cs` |
| `Tests/EditMode/BoxFeelCurveTests.cs` | giữ 2 case `LidStart_*`, xoá 9 case đã chuyển (GĐ4, GĐ8) |
| `Scripts/Feel.md` §3, bảng §4, dòng "Máy kiểm" · `Structure.md` | `TileFlightCurve`, `BoxFeelCurve.DipScaleY`, `DipTuning`, `FlashTuning`, `CounterPopTuning`, `SlotSpringTuning`, `FramePunch*` → tên Horcrux; `BoxFeelCurve.LidStart` giữ |
| YAML, trước khi sửa | grep các key dưới trong `*.prefab`, `*.unity`, `*.asset` của `Assets/` để chắc không variant, scene nào override (hiện chỉ ba prefab dưới) |
| `Addressables/Box/CategoryBox.prefab` | `receive` **và** `finalReceive`: ba dòng `FramePunchScale`, `FramePunchDelay`, `FramePunchDuration` → khối `FramePunch:` với `Scale`, `Delay`, `Duration`, giữ giá trị từng khối (hai khối khác số); `Dip`, `Flash`, `complete.Launch`, `complete.LidFlash`, `counterPop` không đổi (key trùng field) |
| `Prefabs/TileSlot/CacheTileSlot.prefab` | `landSpring`, `shiftSpring`: thêm dòng `DownEase: 2` (InSine) mỗi khối |
| `Addressables/Shelf/SingleShelf.prefab` `amountPop` · `Addressables/Tile/Tile.prefab` `flight`, `rise` | không đổi |
| prefab sau khi sửa | mở Prefab Mode một lần để Unity ghi lại; `git diff` của `CategoryBox.prefab`, `CacheTileSlot.prefab` chỉ có các khối trên; eval trên object đã dựng: `landSpring.DownEase`, `shiftSpring.DownEase == Ease.InSine`, mọi spec trên đúng YAML lúc làm |
| kiểm | grep `TileFlightCurve`, `BoxFeelCurve.DipScaleY`, `BoxFeelCurve.LaunchScaleY`, `DipTuning`, `FlashTuning`, `LaunchTuning`, `SlotSpringTuning`, `CounterPopTuning`, `FramePunchScale` trong `Assets/_CategoryJam` (`.cs`, `.md`, `.prefab`) = 0; trace sau khớp mốc **0 chênh** mọi khung (`LandPitch` dựa test bằng nhau tuyệt đối GĐ3) |
| Plan này | xoá cùng `.meta` |
| chơi thử | level có kệ hai hàng và kệ Single (nút Inspector `BoxAnimation` › `Play Receive`, `Preview Complete` xem cú nhận, cú vọt không cần đủ tile): tap tile vào cache, dồn trái, lấp đầy box, dọn hàng trước, bấm Extra Slot → tile vồng rồi rơi một kiểu, ô cache nhún, box lún–loé–khung nảy–số nảy, tile cuối đón mạnh, nắp đóng, box nén rồi vọt, hàng sau nhún rồi nhảy, ba clack lên dần, số kệ Single nảy, khay rơi vào ô như tile; hỏng: tile đập vào ô, ô cache không về chỗ hay xuống "bật", khung nảy lệch nhịp đế, số đếm đứng yên |

## Không làm · Mở rộng sau

| | Phương án | Vì sao |
|---|---|---|
| bỏ | đưa `TileFlightTuning`, `TileRiseTuning`, `TilePressTuning` lên | quyết định của tile nhựa 2.5D; Horcrux nhận số |
| bỏ | `Duration(distance, TileFlightTuning)` | Horcrux không biết class núm của dự án; thân `Mathf.Clamp` giữ nguyên văn trong `DurationForSpeed` |
| bỏ | parabol một khối cho chuyến bay | buộc đỉnh ở giữa, không cho pha rơi cố định |
| bỏ | tham số `minApexT` | mọi caller truyền 0.5 |
| bỏ | một `Tween.Custom` của Horcrux lái mọi kênh cú nhảy | driver ghi lên thân, bóng, màu của dự án, mỗi dự án một bộ kênh |
| bỏ | hằng `LEAN_MIN_DX`, `LEAN_RISE_END`, `LEAN_FALL_START`, `SCALE_PEAK_OF_RISE` trong Horcrux; hai bản `LeanRamp` | số cảm giác của tile nhựa; dự án giữ hằng và truyền |
| bỏ | tên `BodyFade`, `RiseLift`, `RiseRamp` | tên bộ phận của dự án; Horcrux đặt tên hình dạng |
| bỏ | `SmoothStep` private trong `HopCurve` | hai caller; cạnh `SmootherStep` cùng họ |
| bỏ | `OffsetFromCenter` ở `HopCurve` | ánh xạ chỉ số sang giá trị, cùng họ `Remap`; developer chuyển class khác tuỳ ý |
| bỏ | gộp `Lift` vào `JumpArc` | `JumpArc` lo hai đầu lệch cao; `Lift` là ca hai đầu ngang nhau, đọc được trong một dòng |
| bỏ | `HoldThenEaseIn`, `PeakForDistance` | không người dùng |
| bỏ | `BoxFeelCurve.LidStart` | dàn cảnh của box, ở lại dự án cùng 2 case test |
| bỏ | `BoxFeelCurve.DipScaleY`, `LaunchScaleY` static ngoài struct | hàm chỉ đọc field của chính struct; một file mở để hiểu cú lún |
| bỏ | driver `Tween DipScale(Transform, …)` | `Tween.Custom<T>` cần class giữ scale nghỉ để callback `static`; bọc là sinh class trung gian cho một dòng `ApplyScaleY` |
| bỏ | `InsertFlash` private static trả `void` | extension trả `Sequence` để nối tiếp; bốn dòng thân giữ nguyên văn |
| bỏ | để sprite loé nghỉ ở alpha 0, renderer bật | quad trong suốt vẫn vẽ mỗi frame; tắt hai sprite trên hai box bớt 1 batch, 2 SetPass |
| bỏ | caller tự bật tắt renderer quanh cú loé | hai caller, hai chỗ quên tắt |
| bỏ | `UpEase` trong `SinkSpec` | cả hai người dùng về OutBack |
| bỏ | `Ease.InSine` cố định như `SlotSpringTuning` | foods_jam cùng InSine; vật rơi chạm đáy cần OutQuad |
| bỏ | lún bằng bóp scale | luật vật cứng không biến dạng; bẹp dùng `DipSpec`, nén rồi vọt dùng `LaunchSpec` |
| bỏ | hợp đồng `CharmSquashStretch`, `CharmFlyArc` (trả `Tween`, nhận `ct`, unscaled) cho `CharmSinkLocalY`, `CharmPop` | hai extension được nhét vào `Sequence` cha |
| bỏ | `CharmPop` bằng `Tween.PunchScale`, `DOPunchScale` | punch dao động nhiều chu kỳ quanh nghỉ; pop là một lượt lên rồi về |
| bỏ | `CharmPop` bằng `startDelay` | `startDelay` lặp mỗi cycle, đỉnh đứng yên giữa hai lượt |
| bỏ | ba núm rời `FramePunchScale`, `FramePunchDelay`, `FramePunchDuration` | ba số là một hình; trễ chuyển vào `ChainDelay` của sequence con, scale mỗi frame bằng nhau (test GĐ7) |
| bỏ | `EvaluateCompress(t, out scaleY, out sinkY)` | một hàm cho hai phép nhân; nhân bất biến "cùng t" ra hai nơi |
| bỏ | tooltip `LaunchTuning` "khi nắp chạm", ngưỡng ~0.13 | ngưỡng là của mặt phẳng box trong dự án |
| bỏ | lún vị trí cho nắp box | nắp dừng ngay khi đóng |
| bỏ | nở ba nhịp `TileView.PlayPopIn` | đang chờ làm lại ở dự án; xét lại nếu foods_jam vẫn giữ hình cũ |
| bỏ | lắc xoay `TileView.PlayReject`, `PlayLandRock` | hai bản khác chia thời gian và biên độ; đổi sang `DampedOscillator` là đổi cảm giác |
| sau | lắc xoay lên Horcrux | khi có người dùng thứ ba, hoặc developer chọn `DampedOscillator` |
| sau | `HorizontalGrid` của dự án dùng `OffsetFromCenter` | thứ tự phép tính khác, có thể lệch 1 ulp; đổi khi có trace riêng cho bố cục |
| sau | struct tư thế nghỉ (`Capture` ở Awake, `Restore` khi về pool) thay `_restScale`, `_originalLocalScale`, `_restLocalY` rời ở bốn class | biến luật "tween trên giá trị nghỉ đọc ở Awake" thành kiểu; developer quyết |
| sau | dời `SquashStretchSpec`, `ArcFlightSpec` từ `ExtensionMethods/Transform/` sang `Tweening/` | việc riêng |
| sau | `Interpolator.md` | thư mục chỉ có `.html`; dựng `.md` là việc riêng |
