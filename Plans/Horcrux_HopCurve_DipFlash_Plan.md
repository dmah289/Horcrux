# Plan — `HopCurve`, `DipSpec`, `FlashSpec`, `SinkSpec`, `PopSpec`, `LaunchSpec` và hai hàm `Interpolator` lên Horcrux

Ngày 2026-11-06. Nguồn: category-jam, `Assets/_CategoryJam/Scripts/Entities/Tile/TileFlightCurve.cs` (hình dạng chuyến bay hai pha tới
ô và cú nhảy lên hàng trước; call site ở `Entities/Tile/Tile.cs`), `Entities/Box/BoxFeelTuning.cs` (`DipTuning`, `FlashTuning`,
`LaunchTuning`, `BoxFeelCurve.DipScaleY`, `BoxFeelCurve.LaunchScaleY`), `Entities/Box/BoxAnimation.cs` (`InsertFlash`,
`InsertReceiveAccents`, `ApplyDip`, `ApplyLandCompress`), `Entities/Box/BoxReceiveTuning.cs` (ba núm `FramePunch*`, `CounterPopTuning`),
`Entities/Box/CategoryBox.cs` và `Entities/Shelf/SingleShelfAmountBar.cs` (số nảy), `Entities/TileSlot/CacheTileSlot.cs`
(`SlotSpringTuning`, `PlaySpring`), `Entities/Shelf/ShelfPromoteTuning.cs` (`LandPitch`). Bốn họ độc lập: **hình dạng cú nhảy** (toán
thuần) · **lún và loé của vật nhận** · **lún vị trí và nảy một pha** (hai extension `Transform`) · **nén rồi vọt đi**. Các class núm của
tile (`TileFlightTuning`, `TileRiseTuning`, `TilePressTuning`) và `BoxFeelCurve.LidStart` ở lại dự án.

**Phân công:** GĐ1–GĐ8 (Horcrux, code runtime) developer gõ, sau mỗi giai đoạn agent viết test của giai đoạn đó theo bảng case rồi
compile · GĐ9 (tài liệu) và GĐ10 (category-jam chuyển sang bản Horcrux, xoá bản tạm) agent làm.

**Chạy được, thấy được ở GĐ1–GĐ8** = `com.horcrux.runtime` compile sạch và bảng case của giai đoạn xanh trong `Tests/EditMode/` (toán
thuần, struct) hoặc `Tests/PlayMode/` (extension chạy tween); Horcrux không có host nên "thấy trong game" là GĐ10. Horcrux là submodule:
GĐ1–GĐ9 commit **trong `Assets/Horcrux`**, GĐ10 commit ở repo dự án kèm con trỏ submodule.

Không phụ thuộc Plan nào; `Plans/Horcrux_FeelUtilities_Plan.md` (`TadaCurve`, `TadaSpec`) theo cùng khuôn "struct núm + class toán" của
Plan này, làm trước hay sau đều compile sạch. Có lõi toán: `§0` dẫn giải chuyến bay hai pha và cỡ theo pha; công thức một dòng ghi
"Toán → code" tại giai đoạn của nó.

---

## Ngữ cảnh đã chốt

| Mục | Chốt |
|---|---|
| Người dùng | category-jam: `Tile.StepFlight`, `Tile.StepRise`, `Tile.StepFinale`, `Tile.FlyToAsync`, `Tile.AnimateToFrontRow`, `BoxAnimation.PlayReceive`, `BoxAnimation.PlayComplete`, `CacheTileSlot.PlaySpring`, `CategoryBox.UpdateCounter`, `SingleShelfAmountBar.PlayPop`, `ShelfPromoteTuning.LandPitch` · foods_jam `CacheHole.PlaySpringAnimation` cùng hình lún vị trí · dự án sau có vật nhảy thấp có bóng, vật bay hai pha vào ô, vật nhận lún, vật vọt đi sau khi nén, sprite loé, số nảy |
| Mục tiêu | đưa **toán thuần**, **struct núm** và các extension một lời gọi lên Horcrux, dự án gọi thẳng; cảm giác chơi **không đổi một giá trị nào**: mọi biểu thức chép nguyên văn, kể cả thứ tự phép nhân (float) |
| Chỗ đặt | `HopCurve` cạnh `SquashStretch` ở `Runtime/Utilities/PhysXHelper/` (toán thuần, không tween) · `Interpolator.SmoothStep`, `Interpolator.OffsetFromCenter` thêm vào class có sẵn cùng thư mục · `DipSpec`, `FlashSpec`, `SinkSpec`, `PopSpec`, `LaunchSpec` ở `Runtime/Utilities/Tweening/`, namespace `Horcrux.Runtime.Utilities.Tweening` như `SquashStretchSpec`, `ArcFlightSpec` · `InsertFlash` là extension của `PrimeTween.Sequence` ở `Runtime/Utilities/ExtensionMethods/SequenceExtensions.cs` · `CharmSinkLocalY`, `CharmPop` là partial của `TransformExtensions` ở `Runtime/Utilities/ExtensionMethods/Transform/TransformExtensions.Accent.cs` |
| Tên | `HopCurve` (cú nhảy thấp, không phải đạn đạo); hậu tố `Spec` theo `SquashStretchSpec` thay `Tuning` của dự án; `LaunchSpec` developer chốt. Tên **field** trong các struct giữ nguyên theo bản tạm (quyết định trái trực giác). Developer đổi tên type tuỳ ý trước khi gõ |
| Hợp đồng `CharmSinkLocalY`, `CharmPop` | như `CharmJump`: trả `Sequence`, **không nhận `ct`**, thời gian scaled. Cả hai được nhét vào một `Sequence` lớn hơn (khung nảy nằm trong sequence nhận tile) mà PrimeTween không cho tween con mang token |
| Đường bay hai pha | lên Horcrux trọn: `ApexT`, `Arc01`, `ArcY`, `StartLiftOffset`, `Shadow01`, `FlightScale`, hằng `MIN_APEX_T`; `TileFlightCurve.cs` xoá hẳn ở GĐ10 |
| Số | số trên prefab và mặc định C# của dự án là nháp của developer, không phải mốc; GĐ10 đọc lại từ prefab lúc làm, không chép từ Plan |

**Cố ý KHÔNG làm**

| Không làm | Vì |
|---|---|
| Đưa `TileFlightTuning`, `TileRiseTuning`, `TilePressTuning` lên Horcrux | chứa bóng, lắc, tan thân, vệt — quyết định của tile nhựa 2.5D, không phải của cú nhảy; Horcrux nhận số |
| Driver tween cho cú nhảy (một `Tween.Custom` lái mọi kênh) | driver ghi lên thân, bóng, màu của **dự án**; mỗi dự án một bộ kênh. Horcrux giữ hình dạng, dự án giữ vòng `Step` |
| Driver tween cho lún và nén (`Tween DipScale(Transform, …)`) | `Tween.Custom<T>` cần một class giữ state (scale nghỉ) để callback `static`; bọc lại là sinh class trung gian cho một dòng `ApplyScaleY` |
| Gộp `HopCurve.Lift` vào `JumpArc` | `JumpArc` lo hai đầu lệch cao; `Lift` là ca hai đầu ngang nhau, đọc được trong một dòng. Test giữ hai bản khớp nhau |
| `HopCurve.HoldThenEaseIn`, `HopCurve.PeakForDistance` | không có người dùng: cỡ chuyến bay là `FlightScale` (OutQuad lên đỉnh rồi SmoothStep về đích), cú bay tới ô vượt đích một số cố định `FallHeight` |
| `BoxFeelCurve.LidStart` lên Horcrux | lịch nắp bay vào so với cú lún tile cuối là dàn cảnh của box; ở lại dự án cùng hai case test của nó |
| Field `UpEase` trong `SinkSpec` | cả hai người dùng về nghỉ bằng OutBack; thêm núm khi có người dùng về khác |
| `CharmPop` bằng `Tween.PunchScale` của PrimeTween | khác hình: punch dao động quanh nghỉ nhiều chu kỳ, pop là **một** lượt lên rồi về. Ai cần rung quanh nghỉ thì dùng thẳng PrimeTween |
| `CharmPop` bằng `Tween.Scale(…, startDelay: Delay)` | `startDelay` của PrimeTween lặp mỗi cycle, đỉnh đứng yên giữa hai lượt; trễ nằm trong `Sequence` bằng `ChainDelay` |
| Lún vị trí cho nắp box | nắp dừng ngay tại vị trí đóng; cú lún dành cho cả box (`LaunchSpec`) |
| Đưa nở ba nhịp `TileView.PlayPopIn` lên | foods_jam có bản giống hệt nhưng hình này bóp méo thân nhựa, đang chờ làm lại ở dự án; đưa lên là đóng băng thứ sắp đổi |
| Đưa lắc xoay `TileView.PlayReject`, `PlayLandRock` lên | hai bản khác chia thời gian (1:2:1 với 1:1:1) và biên độ dội (1.0 với 0.66); struct chung mang năm số. Chờ người dùng thứ ba |

**Quyết định trái trực giác** — lý do viết ở giai đoạn sinh ra nó: `SmoothStep` clamp đầu vào (GĐ2) · `LeanRamp`, `LeanToward`,
`FlightScale` nhận mốc và ngưỡng làm tham số bắt buộc, `MIN_APEX_T` là hằng (GĐ1, GĐ2) · `OffsetFromCenter` ở `Interpolator` (GĐ3) ·
field các struct PascalCase khác `SquashStretchSpec` (GĐ4) · `InsertFlash` bật tắt renderer (GĐ5) · `SinkSpec.DownEase` là núm, dời vị trí
không bóp scale, `CharmSinkLocalY` nhận `restLocalY` (GĐ6) · `CharmPop` trả `Sequence` dù một tween (GĐ7) · `LaunchSpec.EvaluateStretchScaleY`
chỉ cho pha bật (GĐ8).

**Mở rộng sau**

- `HorizontalGrid` của dự án dùng `Interpolator.OffsetFromCenter`: bản của nó viết `-(n − 1) · space / 2 + i · space`, thứ tự phép tính
  khác nên có thể lệch 1 ulp; đổi khi có trace riêng cho bố cục.
- Lắc xoay một chiều (`TileView.PlayReject`, `PlayLandRock`): lên Horcrux khi có người dùng thứ ba hoặc khi developer chọn đổi sang
  `DampedOscillator`, việc đó đổi cảm giác.
- Nở ba nhịp `TileView.PlayPopIn`: sau khi dự án làm lại cho vật cứng, nếu foods_jam vẫn dùng hình cũ thì xét lại.
- Struct tư thế nghỉ (`Capture` ở Awake, `Restore` khi về pool) thay các field `_restScale`, `_originalLocalScale`, `_restLocalY` rời ở
  bốn class: biến luật "tween trên giá trị nghỉ" thành kiểu; developer quyết.
- Nhịp tada của tile cuối và sổ "đang diễn": `Plans/Horcrux_FeelUtilities_Plan.md`.

---

## Đã khảo sát

| Nguồn | Lấy gì | Không lấy, vì sao |
|---|---|---|
| `TileFlightCurve.cs` của dự án (bản 2026-11-06) | 16 hàm và 5 hằng; `Tile.cs` gọi 14 hàm ở `FlyToAsync`, `BuildFlightState`, `StepFlight`, `StepFinale`, `AnimateToFrontRow`, `StepRise`; `Lift` chỉ còn qua `RiseLift` | bốn hằng cảm giác `LEAN_RISE_END`, `LEAN_FALL_START`, `LEAN_MIN_DX`, `SCALE_PEAK_OF_RISE` thành `private const` của `Tile` (GĐ10) |
| `BoxFeelTuning.cs`, `BoxReceiveTuning.cs`, `BoxAnimation.cs` | ba struct núm, hai hàm đường cong, `InsertFlash` private static, khung nảy trong `InsertReceiveAccents(Sequence, float, BoxReceiveTuning)` chạy cho cả `receive` lẫn `finalReceive` | `BoxFeelCurve.LidStart` (bảng "Cố ý KHÔNG làm") · `ApplyLandCompress` (hai dòng trên cùng t, GĐ8) · `BoxCompleteTuning`, `BoxAppearTuning` |
| `CacheTileSlot.cs`, `CategoryBox.cs`, `SingleShelfAmountBar.cs`, `ShelfPromoteTuning.cs` | `PlaySpring` (InSine xuống, OutBack về quanh `_restLocalY` đọc ở Awake) · hai bản `Sequence.Create().ChainDelay(…).Chain(Tween.Scale(… 2, Yoyo))` giống hệt · `LandPitch` | `PlayLastSlotWarning`, `PlayPopOut`, bộ đếm chuỗi, thanh ruột |
| `Tests/EditMode/TileFlightCurveTests.cs`, `BoxFeelCurveTests.cs` của dự án | 82 dòng `[Test]`/`[TestCase]` (4 là `LandPitch_CentersOnMiddleSlot`) và 11 dòng (2 là `LidStart`) — chuyển sang Horcrux ở từng giai đoạn | 4 + 2 case ở lại dự án |
| Horcrux `PhysXHelper/Interpolator.cs` | có `SmootherStep` (quintic), chưa có cubic; `OffsetFromCenter` cùng họ ánh xạ chỉ số → giá trị như `Remap` | — |
| Horcrux `PhysXHelper/SquashStretch.cs` | `BoxAnimation.ApplyScaleY` đã dùng `GetVolumePreservingScale`; `DipSpec`, `LaunchSpec` chỉ trả hệ số y | `GetSquashStretch(t, start, end)`: một pha lerp, không có pha về |
| Horcrux `ExtensionMethods/Transform/SquashStretchSpec.cs`, `ArcFlightSpec.cs` | namespace `Horcrux.Runtime.Utilities.Tweening` và hậu tố `Spec` | hai file nằm ở `ExtensionMethods/Transform/` dù namespace là `Tweening`; Plan đặt spec mới ở `Runtime/Utilities/Tweening/` (hiện chỉ có `Easings/`), dời hai file cũ là việc riêng · `SquashStretchSpec` một pha (start → end, một ease) khác `DipSpec` hai pha |
| Horcrux `ExtensionMethods/Transform/TransformExtensions.CharmTween.cs` | — | `CharmSquashStretch`, `CharmFlyArc` trả `Tween`, nhận `ct`, `useUnscaledTime: true`: hợp đồng khác (bảng "Ngữ cảnh"); `CharmFlyArc` là Bezier theo tốc độ, không phải hai pha với pha rơi cố định |
| Horcrux `ExtensionMethods/Transform/TransformExtensions.Jump.cs`, `JumpArc.cs`, `docs/Runtime/CharmJump/CharmJump.md` | hợp đồng trả `Sequence`, không `ct`; file partial riêng cho mỗi họ extension; test chéo `Lift(t, peak)` = `JumpArc.EvaluateHeight(0, ComputeArcCoefficient(0, 0, peak), t)` | không thay `Lift` (bảng "Cố ý KHÔNG làm") |
| Horcrux `Tweening/Easings/Easer.cs` (namespace `Horcrux.Runtime.Tweening.Easing`) | — | `DipSpec`, `LaunchSpec` dùng `PrimeTween.Easing.Evaluate` như bản tạm: cùng một hàm ease với tween đang chạy, khác bản thì lún lệch tween |
| Horcrux `ExtensionMethods/` | chưa có extension nào cho `PrimeTween.Sequence`; dự án cũng không | — |
| Horcrux `docs/Runtime/Interpolator/` | chỉ có `Interpolator.html` (mục 3 đã vẽ SmoothStep cubic để so với `SmootherStep`, mục 9 bảng API) | thư mục chưa có `Interpolator.md`; dựng `.md` là việc riêng ngoài Plan |
| Horcrux `Tests/PlayMode/CharmJumpTests.cs`, `AllocationMeter.cs` | khung PlayMode: `PrimeTweenConfig.SetTweensCapacity`, `isPaused`, đo GC bằng `GC.Alloc` recorder | — |
| PrimeTween `Sequence.cs` | `Insert(float, Tween)`, `Insert(float, Sequence)`, `InsertCallback<T>(float, T, Action<T>)`, `ChainDelay(float)` đều có; `Easing.Evaluate(float, Ease)` static | `startDelay` lặp mỗi cycle (bảng "Cố ý KHÔNG làm") |
| foods_jam `CacheHole.PlaySpringAnimation` | người dùng thứ hai ngoài dự án của hình lún vị trí: xuống InSine rồi về OutBack, hai bộ số | — |
| foods_jam, mahjong-jam `DOPunchScale` | — | khác hình `CharmPop` (bảng "Cố ý KHÔNG làm") |

---

## §0 — Lõi toán: chuyến bay hai pha và cỡ theo pha

**Hiện tượng.** Vật bay từ chỗ cũ tới ô: gốc đi đường đất (lerp OutQuad giữa hai điểm, caller làm), thân **nhấc** trên đường đất
theo một cung, lên đỉnh trên đích rồi **rơi** vào ô. Người chơi so sánh mọi cú đáp với nhau, nên cú rơi phải giống nhau dù bay từ kệ
trên gần hay kệ dưới xa.

| Thành phần | Vai trò |
|---|---|
| `t` tuyến tính 0 → 1 trên `duration` | nhịp chung; mọi kênh đọc cùng t để cùng nhịp |
| `apexT` | mốc đỉnh trên t; sau nó là pha rơi |
| `Arc01(t, apexT)` | hình cung chuẩn hoá: 0 ở hai đầu, 1 ở đỉnh |
| `ArcY` | cung chuẩn hoá nhân vào độ cao thật: từ `y0` lên `y1 + fallHeight` rồi về `y1` |
| `FlightScale` | cỡ nhìn thấy: to lên khi rời chỗ, co về cỡ ô lúc đáp |

**Vì sao mô hình đúng.** Hai pha ghép ở đỉnh bằng hai ease có **vận tốc 0 tại chỗ ghép**: OutQuad kết thúc với đạo hàm 0, InQuad bắt
đầu với đạo hàm 0. Cung vì thế trơn ở đỉnh dù hai pha dài khác nhau; đây là lý do chọn cặp OutQuad–InQuad thay parabol một khối
(parabol buộc đỉnh ở giữa, không cho pha rơi cố định).

**Phương trình.**

$$apexT = max(MIN_APEX_T, 1 − fallDuration / duration)$$

$$Arc01(t) = u(2 − u),\ u = t / apexT \quad (t ≤ apexT) \qquad Arc01(t) = 1 − v^2,\ v = (t − apexT)/(1 − apexT) \quad (t > apexT)$$

$$ArcY(t) = lerp(t ≤ apexT\ ?\ y_0 : y_1,\ y_1 + fallHeight,\ Arc01(t))$$

| Ký hiệu | Nghĩa |
|---|---|
| `duration` | giây của cả chuyến; `DurationForSpeed` = quãng / tốc độ, kẹp hai đầu |
| `fallDuration` | giây của pha rơi, **núm tuyệt đối** của dự án |
| `MIN_APEX_T = 0.5` | pha lên không ngắn hơn pha rơi; chuyến ngắn hơn `2 · fallDuration` thì pha rơi co lại |
| `fallHeight` | đỉnh cao hơn ô bao nhiêu world unit |

**Vì sao pha rơi giống nhau mọi chuyến.** Gọi `s` là giây sau đỉnh. `(1 − apexT) · duration = fallDuration` khi chưa chạm kẹp, nên
`v = s / fallDuration`, và độ cao trên ô là `fallHeight · (1 − v²)`: chỉ phụ thuộc `s`, `fallDuration`, `fallHeight` — không phụ thuộc
`y0`, quãng đường, `duration`. Test `ArcY_FallIsTheSameForEveryFlight` của dự án khoá điều này trên ba chuyến khác xa nhau.

**Từ phương trình ra code** (`HopCurve`, GĐ1): ① `ApexT` trả `MIN_APEX_T` khi `duration ≤ 0` để không chia 0 · ② `ProgressAfter(t, start)
= clamp01((t − start)/(1 − start))` là `v` dùng chung cho pha rơi và cho cung nhảy sau lấy đà · ③ `Arc01` trả 0 ở `t ≤ 0` và `t ≥ 1` trước
khi rẽ nhánh · ④ `ArcY` chọn đầu `y0` hay `y1` theo `t ≤ apexT` rồi `LerpUnclamped` với `Arc01`.

**Cỡ theo pha** (`FlightScale`, GĐ2): OutQuad từ `from` lên `peak` tại `peakT = apexT · peakAt`, rồi `SmoothStep` từ `peak` về `to` ở
`t = 1`. Vận tốc đổi cỡ bằng 0 ở `peakT` (OutQuad kết thúc, SmoothStep bắt đầu đều 0) và ở `t = 1` (SmoothStep kết thúc): vật **chui**
vào ô, không đập vào. `peakAt` là tham số vì "đỉnh ở giữa pha lên" là số cảm giác của tile nhựa.

**Mốc kiểm** (`apexT = 0.7`, `fallHeight = 0.8`, `y0 = 0`):

| Hàm | Input | Kỳ vọng | ✓ |
|---|---|---|---|
| `ApexT` | (0.4, 0.12) · (0.2, 0.12) · (0, 0.12) | 0.7 · 0.5 (chạm kẹp) · 0.5 | test GĐ1 |
| `Arc01` | 0 · 0.35 · 0.7 · 0.85 · 1 | 0 · 0.75 · 1 · 0.75 · 0 | test GĐ1 |
| `ArcY` | t = 0 · 0.7 · 1 với `y1 = 2.2` | 0 · 3.0 · 2.2 | test GĐ1 |
| `ArcY` hai phía đỉnh | `apexT ± 1e-3` | thấp hơn đỉnh không quá 1e-3 (vận tốc 0) | test GĐ1 |
| `FlightScale(t, 0.7, 0.5, 1.08, 1.21, 0.9)` | 0 · 0.35 · 1 | 1.08 · 1.21 · 0.9; đạo hàm số ≈ 0 tại 0.35 và 1 | test GĐ2 |

Các hàm còn lại là công thức một dòng (SmoothStep cubic, parabol `4·peak·t(1 − t)`, ramp hai đầu, lệch từ tâm): "Toán → code" ghi tại giai
đoạn của chúng.

---

## Họ A — `HopCurve`: hình dạng cú nhảy thấp và chuyến bay hai pha

### Luồng mô tả

```
caller dựng chuyến: quãng → DurationForSpeed · ApexT(duration, fallDuration)                      GĐ1
            │
            ▼  mỗi frame, một t tuyến tính
đường đất (caller lerp) + ArcY − ground.y ──► độ cao thân (world) ──► PeakToLocal ──► SetAirPose    GĐ1
            │
            ├─► FlightScale (to lên rồi co về cỡ ô) · LeanToward × LeanRamp (nghiêng vào hướng đi)    GĐ2
            ├─► StartLiftOffset (nối từ độ cao tada) · Shadow01 (bóng co) · FadeOutTail (thân tan)    GĐ2
            │
cú lên hàng trước: LiftWithAnticipation (nhún lấy đà rồi cung Lift) · EaseOutUntil (to, sáng sớm)    GĐ3
đáp: pitch clack theo chỉ số ô ──► Interpolator.OffsetFromCenter                                    GĐ3
```

### Bản đồ hệ cuối

```
  dự án: vòng Step (Tween.Custom, t tuyến tính) ──► HopCurve (static, không state) ──float──► ghi lên thân, bóng, màu của dự án
                                                      ├─ Interpolator.SmoothStep (LeanRamp, FlightScale)
                                                      └─ hằng MIN_APEX_T; mọi mốc cảm giác là tham số caller truyền
  dự án: pitch theo ô ──► Interpolator.OffsetFromCenter
```

---

## Giai đoạn 1 — Cung và độ cao của chuyến bay hai pha

**Mục tiêu:** từ quãng đường và núm tốc độ ra `duration`, `apexT`; từ `t` ra độ cao thân trên đường đất. Xong giai đoạn này một vật bay
tới ô có cung đúng và pha rơi giống nhau mọi chuyến.

**Vì sao bây giờ:** đây là kênh đầu tiên mọi vòng `Step` ghi (vị trí thân); các kênh khác đọc `apexT` và `ProgressAfter` của nó.

**Ai gọi:** test của giai đoạn; trong dự án là `Tile.FlyToAsync` (`DurationForSpeed`), `Tile.BuildFlightState` (`ApexT`), `Tile.StepFlight`
(`ArcY`, `ProgressAfter`, `PeakToLocal`), `Tile.StepFinale` và `Tile.AnimateToFrontRow` (`PeakToLocal`) (GĐ10).

### `Runtime/Utilities/PhysXHelper/HopCurve.cs` — file mới

```csharp
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

        /// <summary>Độ cao nhìn thấy (world y) của thân: từ y0 lên y1 + fallHeight ở đỉnh rồi rơi về y1. Pha rơi chỉ phụ thuộc y1 và fallHeight
        /// nên cú rơi giống nhau dù bay từ đâu.</summary>
        public static float ArcY(float t, float y0, float y1, float fallHeight, float apexT)
            => Mathf.LerpUnclamped(t <= apexT ? y0 : y1, y1 + fallHeight, Arc01(t, apexT));
    }
}
```

**Khác với bản dự án**

| Chỗ | Bản dự án (`TileFlightCurve`) | Bản Horcrux | Vì |
|---|---|---|---|
| `Duration(distance, TileFlightTuning tuning)` | đọc `tuning.Speed`, `MinDuration`, `MaxDuration` | `DurationForSpeed(distance, speed, minDuration, maxDuration)` | Horcrux không biết class núm của dự án; thân `Mathf.Clamp` giữ nguyên văn |
| `ApexT`, `ProgressAfter`, `Arc01`, `ArcY`, `PeakToLocal` | — | cùng tên, cùng tham số, cùng thân | — |

**Quyết định** — `MIN_APEX_T` là hằng Horcrux, không phải tham số: "pha lên không ngắn hơn pha rơi" là cơ chế của cung hai pha, không
phải số cảm giác của một dự án. Phương án bỏ: tham số `minApexT` — mọi caller truyền 0.5.

| Consumes | Produces |
|---|---|
| `UnityEngine.Mathf` | `HopCurve.MIN_APEX_T`, `DurationForSpeed`, `PeakToLocal`, `ApexT`, `ProgressAfter`, `Arc01`, `ArcY` |

**Sơ đồ tương tác sau GĐ1** (`+` là phần mới)

```
  vòng Step của dự án ──t──► HopCurve+ ──ArcY+ − ground.y──► PeakToLocal+ ──► thân nhấc (local y)
                              └─ ApexT+ · ProgressAfter+ · Arc01+ · DurationForSpeed+
```

**Editor setup:** tạo file trong Project window của Unity (để có `.meta`).

**Kiểm:** compile sạch. `Tests/EditMode/HopCurveTests.cs`; chuyển nguyên văn các case `Duration_*`, `ApexT_*`, `ProgressAfter_*`,
`Arc01_*`, `ArcY_*`, `PeakToLocal_*` của `TileFlightCurveTests` (đổi tên type, `Duration` → `DurationForSpeed` với ba số của
`new TileFlightTuning()`: 28, 0.28, 0.42), thêm biên:

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

**Mốc commit:** `feat(hopcurve): cung và độ cao chuyến bay hai pha`.

---

## Giai đoạn 2 — Cỡ, nghiêng, bóng, tan thân theo pha; `Interpolator.SmoothStep`

**Mục tiêu:** các kênh còn lại của chuyến bay tới ô đọc cùng `t` và `apexT`: cỡ nhìn thấy (`FlightScale`), nghiêng (`LeanToward`,
`LeanRamp`), nối từ độ cao tada (`StartLiftOffset`), bóng (`Shadow01`), thân tan vào khung (`FadeOutTail`). `SmoothStep` cubic thêm vào
`Interpolator` vì `LeanRamp` và `FlightScale` cần nó. Xong giai đoạn này `Tile.StepFlight` có đủ hàm để trỏ về.

**Vì sao bây giờ:** cả năm kênh đọc `apexT`, `Arc01`, `ProgressAfter` của GĐ1; thiếu chúng chuyến bay đúng đường nhưng cỡ, nghiêng, bóng
vẫn của bản dự án.

**Ai gọi:** test của giai đoạn; `HopCurve.LeanRamp`, `FlightScale` gọi `Interpolator.SmoothStep` ngay trong giai đoạn; trong dự án là
`Tile.StepFlight`, `Tile.BuildFlightState` (`LeanToward`) (GĐ10).

### `Interpolator.cs` › thêm sau `RemapPrecomputed`, trước `SmootherStep`

Dòng có sẵn ngay dưới chỗ thêm: `/// <summary>Perlin's quintic smoothstep: zero velocity and acceleration at both ends (smoother than cubic smoothstep).</summary>`

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

```

**Toán → code:** `S(t) = 3t² − 2t³ = t²(3 − 2t)`; `S'(t) = 6t(1 − t)` bằng 0 ở hai đầu — cất cánh và đáp phẳng. Mốc: `S(0.5) = 0.5`, đối
xứng `S(t) + S(1 − t) = 1`.

### `HopCurve.cs` › thêm sau `ArcY`, trước `}` đóng class

```csharp
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

**Khác với bản dự án**

| Chỗ | Bản dự án | Bản Horcrux | Vì |
|---|---|---|---|
| `LeanToward(dx, degrees)` | so với hằng `LEAN_MIN_DX = 0.05` | tham số `minDx` | 0.05 là ngưỡng "đi gần thẳng đứng" của tile nhựa |
| `LeanRamp(t)` và `LeanRamp(t, fallStart)` | `LEAN_RISE_END = 0.3` là hằng; bản một tham số dùng `LEAN_FALL_START = 0.75` | một bản `LeanRamp(t, riseEnd, fallStart)` | hai mốc là số cảm giác; dự án giữ hằng và truyền (GĐ10) |
| `FlightScale(t, apexT, from, peak, to)` | `SCALE_PEAK_OF_RISE = 0.5` là hằng | tham số `peakAt` sau `apexT` | "đỉnh ở giữa pha lên" là số cảm giác |
| `BodyFade(fall, fadeStart)` | tên theo thân nhựa | `FadeOutTail(t, fadeStart)` | tên hình dạng, không tên bộ phận của dự án |
| `SmoothStep` | `private` trong `TileFlightCurve` | `Interpolator.SmoothStep` public | hai caller trong `HopCurve`; cạnh `SmootherStep` cùng họ |

**Quyết định** — `SmoothStep` clamp đầu vào: `LeanRamp` gọi với `(1 − t)/(1 − fallStart)` có thể vượt 1 ở biên; không clamp thì ramp
âm. Phương án bỏ: caller clamp — hai caller, hai chỗ quên. — Mốc và ngưỡng là tham số **bắt buộc**, không có mặc định: mặc định trong
Horcrux là đóng băng nháp của một dự án thành chuẩn; người dùng sau phải chọn số của mình.

| Consumes | Produces |
|---|---|
| GĐ1 `Arc01`, `ProgressAfter` · `Interpolator` | `Interpolator.SmoothStep` · `HopCurve.LeanToward`, `LeanRamp`, `FadeOutTail`, `StartLiftOffset`, `Shadow01`, `FlightScale` |

**Sơ đồ tương tác sau GĐ2**

```
  vòng Step ──t──► HopCurve ──FlightScale+──► cỡ gốc        ──LeanToward+ × LeanRamp+──► góc thân
                     │        ──ArcY + StartLiftOffset+──► thân nhấc   ──Shadow01+──► bóng   ──FadeOutTail+──► alpha thân
                     └─ LeanRamp, FlightScale ──► Interpolator.SmoothStep+
```

**Editor setup:** không.

**Kiểm:** compile sạch. `SmoothStep` vào `Tests/EditMode/InterpolatorTests.cs` (file mới, chưa có test cho `Interpolator`); các hàm
`HopCurve` thêm vào `HopCurveTests.cs`, chuyển nguyên văn case `LeanRamp_*`, `BodyFade_*`, `FlightScale_*`, `StartLiftOffset_*`,
`Shadow01_*`, `LeanToward_*` của dự án với hằng của test: `riseEnd 0.3`, `fallStart 0.75`, `minDx 0.05`, `peakAt 0.5` (thay
`TileFlightCurve.SCALE_PEAK_OF_RISE`).

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

**Mốc commit:** `feat(hopcurve): cỡ, nghiêng, bóng, tan theo pha; Interpolator.SmoothStep`.

---

## Giai đoạn 3 — Cú nhảy thấp lên hàng trước; `Interpolator.OffsetFromCenter`

**Mục tiêu:** cú nhảy từ hàng sau lên hàng trước: thân nhún lấy đà rồi nhấc theo parabol (`LiftWithAnticipation` trên `Lift`), cỡ và
màu đổi nhanh lúc đầu (`EaseOutUntil`), đáp kêu *clack* với pitch lên dần theo chỉ số ô (`OffsetFromCenter`). Xong giai đoạn này
`Tile.StepRise` và `ShelfPromoteTuning.LandPitch` có đủ hàm để trỏ về.

**Vì sao bây giờ:** cú lên hàng trước dùng `ProgressAfter` (GĐ1) và `SmoothStep` (GĐ2) để chia pha nhún và pha cung; pitch đáp là
mốc cuối của cùng cú nhảy.

**Ai gọi:** test của giai đoạn; `LiftWithAnticipation` gọi `Lift` và `ProgressAfter` ngay trong giai đoạn; trong dự án là
`Tile.StepRise` (`LiftWithAnticipation`, `EaseOutUntil`) và `ShelfPromoteTuning.LandPitch` → `Shelf` → `Tile.AnimateToFrontRow`
(`OffsetFromCenter`) (GĐ10).

### `HopCurve.cs` › thêm sau `FlightScale`, trước `}` đóng class

```csharp
        /// <summary>Parabol 0 → peak → 0, đỉnh ở t = 0.5; cùng JumpArc khi hai đầu ngang nhau (K = 4·peak).</summary>
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

**Toán → code:** `Lift = 4·peak·t(1 − t)` là `JumpArc.EvaluateHeight(0, K, t)` với `K = ComputeArcCoefficient(0, 0, peak) = 4·peak`.
`LiftWithAnticipation` trên cung: `lerp(−sink, 0, k) + Lift(k, peak)`, liên tục tại `k = 0` (= −sink) và đỉnh tại `k = 0.5` là
`peak − sink/2`.

### `Interpolator.cs` › thêm sau `SmoothStep`, trước `SmootherStep`

```csharp
        /// <summary>Offset of item <paramref name="index"/> in a row of <paramref name="count"/> items spaced by
        /// <paramref name="step"/>, centered on 0: the middle item is 0, the row is symmetric.</summary>
        /// <remarks>(index - (count - 1) / 2) * step</remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float OffsetFromCenter(int index, int count, float step) => (index - (count - 1) * 0.5f) * step;

```

**Toán → code:** `(i − (n − 1)/2) · step`; n lẻ thì phần tử giữa đúng 0, n chẵn thì hai phần tử giữa ±step/2. Thứ tự phép tính giữ
nguyên văn `(index - (count - 1) * 0.5f) * step` của `LandPitch`: pitch không ghi vào trace, chỉ test bằng nhau tuyệt đối khoá.

**Khác với bản dự án**

| Chỗ | Bản dự án | Bản Horcrux | Vì |
|---|---|---|---|
| `RiseLift(t, peak, sink, sinkEnd)` | tên theo cú lên hàng trước | `LiftWithAnticipation(t, peak, sinkDepth, anticipationEnd)` | tên hình dạng: lấy đà rồi nhảy |
| `RiseRamp(k, end)` | — | `EaseOutUntil(k, end)` | tên hình dạng |
| `ShelfPromoteTuning.LandPitch` | `1f + (slotIndex - (capacity - 1) * 0.5f) * PitchStep` nội tuyến | `1f + Interpolator.OffsetFromCenter(slotIndex, capacity, PitchStep)` (GĐ10) | phần lệch từ tâm là toán chung |

**Quyết định** — `OffsetFromCenter` ở `Interpolator`, không ở `HopCurve`: hàm ánh xạ chỉ số sang giá trị, cùng họ `Remap`, không
phải hình dạng cú nhảy; developer chuyển sang class khác tuỳ ý. — `Lift` public dù trong Horcrux chỉ `LiftWithAnticipation` gọi: test
chéo với `JumpArc` giữ hai bản khớp nhau.

| Consumes | Produces |
|---|---|
| GĐ1 `ProgressAfter` · GĐ2 `Interpolator.SmoothStep` | `HopCurve.Lift`, `LiftWithAnticipation`, `EaseOutUntil` · `Interpolator.OffsetFromCenter` |

**Sơ đồ tương tác sau GĐ3**

```
  vòng Step của cú lên hàng trước ──t──► HopCurve ──ProgressAfter──► k ──EaseOutUntil+──► cỡ, màu
                                                   ──LiftWithAnticipation+ (SmoothStep → Lift+)──► thân nhấc
  đáp ──chỉ số ô, sức chứa──► Interpolator.OffsetFromCenter+ ──► pitch
```

**Editor setup:** không.

**Kiểm:** compile sạch. Chuyển nguyên văn case `Lift_*`, `RiseLift_*`, `RiseRamp_*` của dự án vào `HopCurveTests.cs`;
`OffsetFromCenter` vào `InterpolatorTests.cs`.

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

**Mốc commit:** `feat(hopcurve): cú nhảy có lấy đà; Interpolator.OffsetFromCenter`.

---

## Họ B — `DipSpec`, `FlashSpec`, `InsertFlash`: vật nhận lún và loé

### Luồng mô tả

```
vật nhận cú chạm (tile đáp vào box)
            │
            ├─► DipSpec: hệ số y xuống ScaleY (OutQuad) rồi về 1 (OutBack) trên một Tween.Custom t tuyến tính     GĐ4
            │        caller: ApplyScaleY(spec.EvaluateScaleY(t)) nhân scale nghỉ, bù trục kia (SquashStretch)
            │
            └─► FlashSpec + sequence.InsertFlash(at, sprite, spec): bật renderer, alpha lên PeakAlpha rồi về 0, tắt    GĐ5
```

### Bản đồ hệ cuối

```
  dự án: [SerializeField] DipSpec dip ──Tween.Custom(0→1, dip.Duration, Linear)──► dip.EvaluateScaleY(t) ──► ApplyScaleY (dự án)
  dự án: [SerializeField] FlashSpec flash ──Sequence.InsertFlash(at, SpriteRenderer, flash)──► 2 InsertCallback + 2 Tween.Alpha trong sequence của caller
```

---

## Giai đoạn 4 — `DipSpec`: lún một trục giữ thể tích

**Mục tiêu:** struct núm ba số và hàm `EvaluateScaleY(t)` trả hệ số y trên cả `Duration`. Xong giai đoạn này mọi vật nhận lún có
một struct để serialize và một hàm để gọi trong `Tween.Custom`.

**Vì sao bây giờ:** là nhịp nhấn đầu tiên của vật nhận; không phụ thuộc họ A.

**Ai gọi:** test của giai đoạn; trong dự án là `BoxAnimation.ApplyDip` cho `receive.Dip` và `finalReceive.Dip` (GĐ10).

### `Runtime/Utilities/Tweening/DipSpec.cs` — file mới

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
            return t < downEnd
                ? Mathf.Lerp(1f, ScaleY, Easing.Evaluate(t / downEnd, Ease.OutQuad))
                : Mathf.LerpUnclamped(ScaleY, 1f, Easing.Evaluate((t - downEnd) / (1f - downEnd), Ease.OutBack));
        }
    }
}
```

**Toán → code:** `downEnd = Down / (Down + Up)`; pha xuống `lerp(1, ScaleY, OutQuad(t/downEnd))`, pha về `lerpUnclamped(ScaleY, 1,
OutBack(…))` — `Unclamped` vì OutBack vượt 1. Mốc: `t = 0 → 1`, `t = downEnd → ScaleY`, `t = 1 → 1`.

**Khác với bản dự án**

| Chỗ | Bản dự án | Bản Horcrux | Vì |
|---|---|---|---|
| `BoxFeelCurve.DipScaleY(t, in DipTuning)` | hàm static ngoài struct | `DipSpec.EvaluateScaleY(t)` method của struct | hàm chỉ đọc ba field của chính struct; một file mở để hiểu cú lún |
| `DipTuning` | `[Range(0.7f, 1f)]`, ba field, constructor, `Duration` | giống hệt, đổi tên | — |

**Quyết định** — field PascalCase (`ScaleY`, `DownDuration`, `UpDuration`), khác `SquashStretchSpec` camelCase: tên field là **key
YAML** trên prefab của dự án; đổi kiểu struct thì Unity giữ giá trị vì khớp theo tên field, đổi tên field thì giá trị rơi về 0 im
lặng. Áp cho cả năm struct của Plan. — Dùng `PrimeTween.Easing.Evaluate`, không dùng `Easer` của Horcrux: cùng hàm ease với tween đang
chạy ở caller.

| Consumes | Produces |
|---|---|
| `PrimeTween.Easing.Evaluate`, `Ease` · `UnityEngine.Mathf` | `DipSpec`, `DipSpec.Duration`, `DipSpec.EvaluateScaleY` |

**Sơ đồ tương tác sau GĐ4**

```
  caller ──Tween.Custom(0→1, dip.Duration, Linear)──► dip.EvaluateScaleY+(t) ──hệ số y──► caller nhân scale nghỉ, SquashStretch.GetVolumePreservingScale
```

**Editor setup:** tạo file trong Project window (`.meta`); struct `[Serializable]` hiện trong Inspector ở mọi field kiểu `DipSpec`.

**Kiểm:** compile sạch. `Tests/EditMode/DipSpecTests.cs`; chuyển nguyên văn 5 case `DipScaleY_*` của `BoxFeelCurveTests`.

| Case | Input | Kỳ vọng |
|---|---|---|
| `new DipSpec(0.86, 0.05, 0.22)` | `Duration` · `EvaluateScaleY` tại 0 · `downEnd` · 1 | 0.27 · 1 · 0.86 · 1 |
| pha xuống | `downEnd / 2` | trong (0.86, 1), khác hai đầu |
| pha về OutBack | `downEnd + 0.7 · (1 − downEnd)` | > 1 |
| `DownDuration` rất nhỏ | (0.86, 0.01, 0.22), t = 0.01/0.23 | 0.86 (không chia 0, `Min` chặn 0 trong Inspector) |
| t ngoài dải | −0.1 · 1.1 | pha xuống `Lerp` clamp → 1; pha về `Evaluate(>1)`: ghi giá trị thực tế vào test làm mốc, caller không đưa t ngoài [0, 1] |
| `default(DipSpec)` | `Duration` | 0; `EvaluateScaleY` chia 0 → NaN: struct dựng bằng code phải qua constructor |

**Mốc commit:** `feat(tweening): DipSpec`.

---

## Giai đoạn 5 — `FlashSpec` và `Sequence.InsertFlash`

**Mục tiêu:** struct núm ba số và extension chèn cú loé vào `Sequence` ở mốc `at`: bật renderer, alpha lên `PeakAlpha` (OutQuad), về 0
(InQuad), tắt renderer. Xong giai đoạn này mọi sprite loé nằm sẵn trên vật dùng một lời gọi.

**Vì sao bây giờ:** loé đi cùng lún trong cùng sequence nhận; là extension `Sequence` đầu tiên của Horcrux nên file `SequenceExtensions.cs`
sinh ra ở đây.

**Ai gọi:** test của giai đoạn; trong dự án là `BoxAnimation.InsertReceiveAccents` (lòng khung) và `BoxAnimation.PlayComplete` (nắp) (GĐ10).

### `Runtime/Utilities/Tweening/FlashSpec.cs` — file mới

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

### `Runtime/Utilities/ExtensionMethods/SequenceExtensions.cs` — file mới

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

**Khác với bản dự án**

| Chỗ | Bản dự án | Bản Horcrux | Vì |
|---|---|---|---|
| `BoxAnimation.InsertFlash(Sequence, float, SpriteRenderer, in FlashTuning)` private static, trả `void` | — | extension của `Sequence`, trả `Sequence` để nối tiếp | bốn dòng thân giữ nguyên văn |

**Quyết định** — bật renderer lúc bắt đầu loé, tắt khi alpha về 0: sprite loé nằm sẵn trên vật ở alpha 0 vẫn là một quad trong suốt
vẽ mỗi frame; dự án đo được tắt hai sprite loé nghỉ trên hai box bớt 1 batch, 2 SetPass. Mốc tắt là `(at + Rise) + Fall`, cùng thứ tự
phép cộng với mốc tween về 0 kết thúc, để callback tắt không chạy trước frame cuối của tween. Phương án bỏ: caller tự bật tắt — hai
caller, hai chỗ quên tắt.

| Consumes | Produces |
|---|---|
| `PrimeTween.Sequence.Insert`, `InsertCallback<T>`, `Tween.Alpha(SpriteRenderer, …)` | `FlashSpec` · `Sequence.InsertFlash` |

**Sơ đồ tương tác sau GĐ5**

```
  caller ──sequence.InsertFlash+(at, sprite, flash)──► [at] enabled = true · Alpha → PeakAlpha (Rise, OutQuad)
                                                         [at + Rise] Alpha → 0 (Fall, InQuad) · [at + Rise + Fall] enabled = false
```

**Editor setup:** không — sprite loé của dự án đã nằm trong prefab với alpha 0 và renderer tắt.

**Kiểm:** compile sạch. PlayMode `Tests/PlayMode/CharmAccentTests.cs` (file mới, cùng khung `CharmJumpTests`: `SetTweensCapacity`,
`isPaused`, `GC.Alloc` recorder); `SpriteRenderer` dựng trên `GameObject` mới, alpha 0, `enabled = false`.

| Case | Kiểm | Kỳ vọng |
|---|---|---|
| chèn ở `at = 0.1`, `(0.6, 0.04, 0.08)` | `enabled` trước 0.1 s · tại 0.1 + 0.02 s · sau 0.1 + 0.12 s | false · true, alpha > 0 · false, alpha 0 |
| đỉnh | alpha tại 0.1 + 0.04 s | 0.6 (±1e-3) |
| `at = 0` | `enabled` ở frame đầu sau khi tạo | true |
| hai cú loé chồng (`at` 0 và 0.05, cùng sprite) | sau 0.05 + 0.12 s | `enabled` false, alpha 0 (callback sau thắng) |
| object huỷ giữa loé | `Destroy` sprite ở 0.02 s rồi chờ hết | không exception (PrimeTween bỏ tween trên target đã huỷ) |
| GC | mỗi lời gọi sau lượt khởi động | 0 B |

**Mốc commit:** `feat(tweening): FlashSpec, Sequence.InsertFlash`.

---

## Họ C — `SinkSpec`, `PopSpec`, `CharmSinkLocalY`, `CharmPop`: lún vị trí và nảy một pha

### Luồng mô tả

```
ô nhận vật (khay nhựa bị đè) ──► transform.CharmSinkLocalY(restLocalY, sink): y xuống Depth (DownEase) rồi về nghỉ (OutBack)    GĐ6
            │                      caller giữ restLocalY đọc ở Awake; gọi rời, không giữ handle
            ▼
khung, số đếm nảy sau một nhịp ──► transform.CharmPop(restScale, pop): chờ Delay, lên restScale × Scale rồi về (InOutSine Yoyo)   GĐ7
            │                      caller: chạy rời (số đếm) hoặc sequence.Insert(at, …) (khung trong sequence nhận)
            ▼
caller Stop() sequence trước khi gọi lại; về nghỉ là việc của caller (đặt lại scale, y)
```

### Bản đồ hệ cuối

```
  dự án: [SerializeField] SinkSpec sink ──tr.CharmSinkLocalY(restLocalY, sink)──► Sequence (2 Tween.LocalPositionY nối nhau)
  dự án: [SerializeField] PopSpec pop   ──tr.CharmPop(restScale, pop)──────────► Sequence (ChainDelay + Tween.Scale Yoyo 2 cycle)
  TransformExtensions.Accent.cs (partial) — trả Sequence, không ct, scaled time, như CharmJump
```

---

## Giai đoạn 6 — `SinkSpec` và `CharmSinkLocalY`

**Mục tiêu:** struct núm bốn field (độ sâu, hai thời lượng, ease xuống) và extension lún local y từ mốc nghỉ rồi về đúng mốc nghỉ.
Xong giai đoạn này ô cache của dự án và `CacheHole` của foods_jam có một hình lún dùng chung.

**Vì sao bây giờ:** là extension `Transform` đầu tiên của họ C, sinh ra file partial `TransformExtensions.Accent.cs`; không phụ thuộc họ A, B.

**Ai gọi:** test của giai đoạn; trong dự án là `CacheTileSlot.PlaySpring` cho `landSpring` (tile mới vào) và `shiftSpring` (dồn trái) (GĐ10).

### `Runtime/Utilities/Tweening/SinkSpec.cs` — file mới

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

### `Runtime/Utilities/ExtensionMethods/Transform/TransformExtensions.Accent.cs` — file mới

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
    }
}
```

**Khác với bản dự án**

| Chỗ | Bản dự án | Bản Horcrux | Vì |
|---|---|---|---|
| `SlotSpringTuning` | ba field, `Ease.InSine` cố định trong `PlaySpring` | thêm field `DownEase` | foods_jam cùng InSine; dự án sau có vật rơi chạm đáy xuống OutQuad |
| `CacheTileSlot.PlaySpring(in SlotSpringTuning)` private, không giữ handle | `Sequence.Create().Chain(…).Chain(…)` đọc `_restLocalY` | `CharmSinkLocalY(restLocalY, spec)` trả `Sequence` | hai dòng tween giữ nguyên văn; mốc nghỉ là tham số |

**Quyết định** — `DownEase` là núm: ô cache xuống InSine như khay bị đè; vật rơi chạm đáy xuống OutQuad. Bẫy: thiếu key `DownEase`
trong YAML thì giá trị là 0, tức `Ease.Default`, PrimeTween đọc thành OutQuad — GĐ10 ghi key thẳng vào prefab. — Dời vị trí, không bóp
scale: luật vật cứng không biến dạng; cần bẹp thì `DipSpec`, cần nén rồi vọt thì `LaunchSpec`. — `restLocalY` là tham số, không đọc
`localPosition.y` lúc gọi: lún gọi lại khi đang lún thì đọc lúc gọi trôi mốc nghỉ xuống dần; luật "tween trên giá trị nghỉ đọc ở
Awake". — Không `UpEase`: cả hai người dùng về OutBack.

| Consumes | Produces |
|---|---|
| `PrimeTween.Sequence.Create`, `Chain`, `Tween.LocalPositionY`, `Ease` | `SinkSpec`, `SinkSpec.Duration` · `Transform.CharmSinkLocalY` |

**Sơ đồ tương tác sau GĐ6**

```
  caller ──tr.CharmSinkLocalY+(restLocalY, sink)──► Sequence: LocalPositionY → rest − Depth (Down, DownEase) ──► LocalPositionY → rest (Up, OutBack)
```

**Editor setup:** không — field `SinkSpec` hiện trong Inspector; `DownEase` chọn từ enum `Ease`.

**Kiểm:** compile sạch. `SinkSpec` vào `Tests/EditMode/AccentSpecTests.cs` (file mới); `CharmSinkLocalY` vào `CharmAccentTests.cs`.

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

**Mốc commit:** `feat(tweening): SinkSpec, CharmSinkLocalY`.

---

## Giai đoạn 7 — `PopSpec` và `CharmPop`

**Mục tiêu:** struct núm ba số và extension nảy một pha trên scale nghỉ sau một trễ. Xong giai đoạn này ba chỗ nảy của dự án (khung
box, bộ đếm box, số lớp kệ) đọc một hình.

**Vì sao bây giờ:** cùng họ "nhịp nhấn của vật nhận" với GĐ6 và cùng file partial; bộ đếm nảy sau khung một nhịp nên trễ là một phần
của hình.

**Ai gọi:** test của giai đoạn; trong dự án là `BoxAnimation.InsertReceiveAccents` (chèn vào sequence nhận), `CategoryBox.UpdateCounter`,
`SingleShelfAmountBar.PlayPop` (chạy rời) (GĐ10).

### `Runtime/Utilities/Tweening/PopSpec.cs` — file mới

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

### `TransformExtensions.Accent.cs` › thêm sau `CharmSinkLocalY`, trước `}` đóng class

```csharp
        /// <summary>Nảy một pha trên mốc nghỉ: chờ spec.Delay, lên restScale × spec.Scale rồi về restScale, mỗi lượt nửa Duration (InOutSine).
        /// Trả Sequence vì startDelay của PrimeTween lặp mỗi cycle. Chèn vào sequence khác bằng Insert(at, …).</summary>
        public static Sequence CharmPop(this Transform self, Vector3 restScale, in PopSpec spec)
            => Sequence.Create()
                .ChainDelay(spec.Delay)
                .Chain(Tween.Scale(self, restScale, restScale * spec.Scale, spec.Duration * 0.5f, Ease.InOutSine, 2, CycleMode.Yoyo));
```

**Khác với bản dự án**

| Chỗ | Bản dự án | Bản Horcrux | Vì |
|---|---|---|---|
| `CounterPopTuning` (`CategoryBox`, `SingleShelfAmountBar`) | ba field cùng tên | `PopSpec`, đổi tên type | — |
| ba núm rời `FramePunchScale`, `FramePunchDelay`, `FramePunchDuration` trong `BoxReceiveTuning` | `sequence.Insert(at + Delay, Tween.Scale(…, 2, Yoyo))` | `sequence.Insert(at, frame.CharmPop(rest, FramePunch))` (GĐ10) | ba số là một hình; trễ chuyển từ mốc `Insert` vào `ChainDelay` của sequence con — PrimeTween chạy sequence lồng ở mốc chèn nên scale mỗi frame bằng nhau (test) |
| `CategoryBox.UpdateCounter`, `SingleShelfAmountBar.PlayPop` | `Sequence.Create().ChainDelay(…).Chain(Tween.Scale(…))` | `CharmPop(rest, spec)` | hai dòng giữ nguyên văn |

**Quyết định** — `CharmPop` trả `Sequence` dù chỉ có một tween: trễ là một phần của cú nảy, nằm trong `PopSpec` để một nguồn;
PrimeTween chỉ cho trễ-không-lặp qua `ChainDelay` (`startDelay` lặp mỗi cycle, đỉnh đứng yên giữa hai lượt). Khung box chèn bằng
`sequence.Insert(at, …)`, hai chỗ kia chạy rời; cả ba đọc một hình. — Không dùng `Tween.PunchScale`: khác hình (bảng "Cố ý KHÔNG làm").

| Consumes | Produces |
|---|---|
| `PrimeTween.Sequence.ChainDelay`, `Tween.Scale(Transform, Vector3, Vector3, float, Ease, int, CycleMode)` | `PopSpec` · `Transform.CharmPop` |

**Sơ đồ tương tác sau GĐ7**

```
  caller ──tr.CharmPop+(restScale, pop)──► Sequence: ChainDelay(Delay) ──► Scale rest → rest × Scale, Duration/2, InOutSine, 2 cycle Yoyo
  sequence cha ──Insert(at, …CharmPop(…))──► sequence con chạy từ mốc at
```

**Editor setup:** không.

**Kiểm:** compile sạch. `CharmPop` vào `CharmAccentTests.cs`.

| Case | Kiểm | Kỳ vọng |
|---|---|---|
| `CharmPop(rest (2,2,2), (1.25, 0.06, 0.15))` | scale trước 0.06 s · tại 0.06 + 0.075 s · sau 0.06 + 0.15 s · `isAlive` | (2,2,2) · ≈ (2.5,2.5,2.5) · đúng (2,2,2) · false |
| `Delay = 0` | scale tại 0 · tại `Duration/2` | nghỉ · đỉnh |
| `Scale = 1` | scale mọi lúc | nghỉ (không exception) |
| chèn `seq.Insert(at, tr.CharmPop(rest, pop))` so bản tay `seq.Insert(at + Delay, Tween.Scale(tr, rest, rest × Scale, Duration/2, InOutSine, 2, Yoyo))` | scale mỗi frame của hai `Transform` trong cùng một `Sequence` cha, `at = 0.03` | bằng nhau tuyệt đối: thay thế ở `InsertReceiveAccents` không đổi một khung |
| gọi lại giữa chừng (`Stop()`, đặt lại scale nghỉ, gọi) | scale sau `Delay + Duration` | đúng nghỉ |
| GC | mỗi lời gọi sau lượt khởi động | 0 B |

**Mốc commit:** `feat(tweening): PopSpec, CharmPop`.

---

## Họ D — `LaunchSpec`: nén rồi vọt đi

### Luồng mô tả

```
vật nhận cú chạm (nắp đóng xuống box) tại mốc touch
            │
            ├─► [touch, DownDuration] một Tween.Custom OutQuad: nén y tới ScaleY và nhún tới −SinkDepth trên CÙNG t (caller, hai dòng)
            ├─► giữ đáy HoldDuration
            └─► [touch + LaunchDelay] vọt: Tween.Custom Linear trên StretchDuration → spec.EvaluateStretchScaleY(t): từ ScaleY lên
                 StretchScaleY (OutQuad, StretchRiseDuration) rồi về 1 (InOutSine, StretchSettleDuration); caller tween vị trí bay   GĐ8
```

### Bản đồ hệ cuối

```
  dự án: [SerializeField] LaunchSpec launch ──LaunchDelay, StretchDuration, DownDuration──► mốc trong sequence của caller
                                             ──EvaluateStretchScaleY(t)──► ApplyScaleY (dự án)
                                             ──ScaleY, SinkDepth──► pha nén và nhún của caller (LerpUnclamped(1, ScaleY, t), −SinkDepth · t)
```

---

## Giai đoạn 8 — `LaunchSpec`

**Mục tiêu:** struct núm bảy field, hai mốc dẫn xuất (`LaunchDelay`, `StretchDuration`) và `EvaluateStretchScaleY(t)` cho pha bật.
Xong giai đoạn này vật nhận cú chạm rồi vọt đi có một struct để lên lịch và một hàm cho pha giãn.

**Vì sao bây giờ:** là họ cuối, độc lập với A–C; `BoxAnimation.PlayComplete` là người dùng duy nhất hiện có.

**Ai gọi:** test của giai đoạn; trong dự án là `BoxAnimation.PlayComplete` (ba mốc, `EvaluateStretchScaleY`) và `BoxAnimation.ApplyLandCompress`
(`ScaleY`, `SinkDepth`) (GĐ10).

### `Runtime/Utilities/Tweening/LaunchSpec.cs` — file mới

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

**Toán → code:** `peakEnd = Rise / (Rise + Settle)`; pha lên `lerp(ScaleY, StretchScaleY, OutQuad)` bắt đầu đúng ở hệ số nén cuối
(liên tục với pha nén), pha về `lerp(StretchScaleY, 1, InOutSine)`. Mốc: `0 → ScaleY`, `peakEnd → StretchScaleY`, `1 → 1`.

**Khác với bản dự án**

| Chỗ | Bản dự án | Bản Horcrux | Vì |
|---|---|---|---|
| `BoxFeelCurve.LaunchScaleY(t, in LaunchTuning)` | hàm static ngoài struct | `LaunchSpec.EvaluateStretchScaleY(t)` | chỉ đọc field của chính struct (như `DipSpec`) |
| tooltip `LaunchTuning` | "khi nắp chạm", "Box_anim nhún … nhỏ hơn ~0.13 thì chỉ ghim đáy" | "khi chạm", "Vật nhún xuống bấy nhiêu unit local" | ngưỡng 0.13 là của mặt phẳng box trong dự án |

**Quyết định** — chỉ pha bật có hàm; pha nén và nhún không: chúng là `LerpUnclamped(1, ScaleY, t)` và `−SinkDepth · t` trên **cùng
một t đã ease** của một `Tween.Custom` (OutQuad) — hai dòng ở caller (`ApplyLandCompress`), không có công thức riêng; tách ra là nhân
bất biến "cùng t" ra hai nơi. Phương án bỏ: `EvaluateCompress(t, out scaleY, out sinkY)` — một hàm cho hai phép nhân.

| Consumes | Produces |
|---|---|
| `PrimeTween.Easing.Evaluate`, `Ease` · `UnityEngine.Mathf` | `LaunchSpec`, `LaunchDelay`, `StretchDuration`, `EvaluateStretchScaleY` |

**Sơ đồ tương tác sau GĐ8**

```
  caller ──[touch] Tween.Custom(OutQuad, DownDuration)──► caller: LerpUnclamped(1, ScaleY, t) · −SinkDepth · t   (hai dòng của caller)
         ──[touch + LaunchDelay+] Tween.Custom(Linear, StretchDuration+)──► launch.EvaluateStretchScaleY+(t) ──► ApplyScaleY
```

**Editor setup:** không.

**Kiểm:** compile sạch. Chuyển nguyên văn 4 case `LaunchScaleY_*` của `BoxFeelCurveTests` vào `Tests/EditMode/LaunchSpecTests.cs`.

| Case | Input | Kỳ vọng |
|---|---|---|
| `new LaunchSpec(0.87, 0.2, 0.15, 0.03, 1.06, 0.05, 0.15)` | `LaunchDelay` · `StretchDuration` | 0.23 · 0.2 |
| `EvaluateStretchScaleY` | 0 · `peakEnd` (0.25) · 1 · `peakEnd + 0.5 · (1 − peakEnd)` | 0.87 · 1.06 · 1 · trong (1, 1.06) |
| `HoldDuration = 0` | `LaunchDelay` | bằng `DownDuration` |
| `StretchScaleY = 1` | `EvaluateStretchScaleY` tại `peakEnd` và 1 | 1 · 1 (về đúng nghỉ, không giãn) |
| `default(LaunchSpec)` | `StretchDuration` | 0; `EvaluateStretchScaleY` chia 0 → NaN: dựng bằng code phải qua constructor |

**Mốc commit:** `feat(tweening): LaunchSpec`.

---

## Giai đoạn 9 — Tài liệu module (agent)

| Tài liệu | Nội dung |
|---|---|
| `docs/Runtime/HopCurve/HopCurve.md`, mở đầu `Code: Runtime/Utilities/PhysXHelper` | luồng mô tả họ A (ASCII) · bảng kênh → hàm → hình dạng → mốc kiểm · `§0` chuyển sang (vì sao pha rơi cố định, vì sao đỉnh không trước nửa đường, vì sao OutQuad–InQuad) · "Design decisions" gom **Quyết định** GĐ1–GĐ3 (mốc là tham số, `MIN_APEX_T` là hằng, `SmoothStep` clamp, `Lift` so `JumpArc`) · cách dự án dựng vòng `Step` (một `Tween.Custom` Linear, mọi kênh đọc cùng t) với tên trung tính |
| `docs/Runtime/HopCurve/HopCurve.html` | chọn từ `.md`: kịch bản (vật bay tới ô, vật nhảy lên bậc), cách dùng (mốc nào truyền, đọc gì mỗi frame), flow (cung hai pha vẽ đồ thị tĩnh), lưu ý (`peakAt`, `riseEnd`, `fallStart` là số cảm giác của từng game) |
| `docs/Runtime/CharmAccent/CharmAccent.md`, mở đầu `Code: Runtime/Utilities/Tweening, Runtime/Utilities/ExtensionMethods` | `DipSpec`, `FlashSpec`, `InsertFlash`, `SinkSpec`, `CharmSinkLocalY`, `PopSpec`, `CharmPop` (nhịp nhấn của vật nhận) và `LaunchSpec` (vật nhận rồi rời đi) · cùng khung `CharmJump.md`: hợp đồng trả `Sequence` không `ct`, ví dụ chèn vào `Sequence` cha · "Design decisions" gom **Quyết định** GĐ4–GĐ8 · bẫy: `DownEase` thiếu key YAML, `default(Spec)` chia 0, field PascalCase là key YAML, caller `Stop()` và đặt lại nghỉ trước khi gọi lại |
| `docs/Runtime/CharmAccent/CharmAccent.html` | chọn từ `.md`: kịch bản (vật nhận: lún, loé, khung nảy, số nảy; vật vọt đi), cách dùng (field serialize, một lời gọi), flow (timeline một cú nhận và một cú vọt), lưu ý |
| `docs/Runtime/Interpolator/Interpolator.html` | mục 3: `SmoothStep` cubic có thật trong code (đồ thị so sánh đã vẽ nó) · mục 9 bảng API: thêm `SmoothStep(t)` và `OffsetFromCenter(i, n, step)`. Thư mục chưa có `Interpolator.md`; dựng `.md` là việc riêng |
| `Plans/FeelingPlan.md` | các dòng trỏ "`Horcrux_HopCurve_DipFlash_Plan.md` Task 1.x" đổi thành "đã có: `docs/Runtime/HopCurve`, `docs/Runtime/CharmAccent`"; `Projectile/Ballistics` ghi thêm "cú bay hai pha vào ô: `HopCurve.ArcY`" |

Không có "Trước khi chạy": không wire tay, mọi spec là field serialize của component dự án.

**Mốc commit:** `docs(hopcurve,accent): HopCurve.md, CharmAccent.md và hai .html`.

---

## Giai đoạn 10 — category-jam dùng bản Horcrux, xoá bản tạm (agent)

**Mục tiêu:** `TileFlightCurve.cs`, `DipTuning`, `FlashTuning`, `LaunchTuning`, `BoxFeelCurve.DipScaleY`, `BoxFeelCurve.LaunchScaleY`,
`SlotSpringTuning`, `CounterPopTuning`, ba núm `FramePunch*`, `BoxAnimation.InsertFlash` xoá khỏi dự án; call site trỏ sang Horcrux;
prefab giữ nguyên giá trị. Cảm giác không đổi một khung.

**Trước khi sửa:** quay mốc trace bằng `docs/tools/feel_trace/trace.cs` trên code hiện tại (kịch bản phủ khung nảy và bộ đếm nảy, ô cache
nhún khi nhận và khi dồn trái, tile cuối đón mạnh rồi nắp chạm, nén–giãn khi vọt và loé nắp, chuyến bay hai pha, tada, thanh đếm kệ
Single), hai lần cùng chữ ký 0 chênh rồi mới sửa.

### `Entities/Tile/Tile.cs`

Thêm `using Horcrux.Runtime.Utilities.PhysXHelper;` và bốn hằng vào `Properties`, trước `[SerializeField] protected TileView _view;`:

```csharp
        private const float LEAN_RISE_END = 0.3f;
        /// <summary>Mốc trả nghiêng của cú lên hàng trước; chuyến bay tới ô trả nghiêng từ đỉnh.</summary>
        private const float LEAN_FALL_START = 0.75f;
        /// <summary>Dưới độ dời ngang này vật đi gần như thẳng đứng: không nghiêng.</summary>
        private const float LEAN_MIN_DX = 0.05f;
        /// <summary>Đỉnh scale của chuyến bay tới ô ở giữa pha đi lên.</summary>
        private const float SCALE_PEAK_OF_RISE = 0.5f;
```

| Call site | Cũ | Mới |
|---|---|---|
| `FlyToAsync` | `TileFlightCurve.Duration(distance, flight)` | `HopCurve.DurationForSpeed(distance, flight.Speed, flight.MinDuration, flight.MaxDuration)` |
| `BuildFlightState` | `TileFlightCurve.ApexT(duration, flight.FallDuration)` · `TileFlightCurve.LeanToward(to.x - from.x, flight.LeanDegrees)` | `HopCurve.ApexT(…)` · `HopCurve.LeanToward(to.x - from.x, flight.LeanDegrees, LEAN_MIN_DX)` |
| `StepFlight` | `TileFlightCurve.FlightScale(t, apexT, from, peak, to)` | `HopCurve.FlightScale(t, apexT, SCALE_PEAK_OF_RISE, from, peak, to)` |
| `StepFlight` | `ArcY`, `StartLiftOffset`, `ProgressAfter`, `Shadow01`, `PeakToLocal` | cùng tên ở `HopCurve`, cùng tham số |
| `StepFlight` | `TileFlightCurve.BodyFade(fall, flight.FallFadeStart)` · `TileFlightCurve.LeanRamp(t, apexT)` | `HopCurve.FadeOutTail(fall, flight.FallFadeStart)` · `HopCurve.LeanRamp(t, LEAN_RISE_END, apexT)` |
| `StepFinale` | `TileFlightCurve.PeakToLocal(pose.Lift, scale)` | `HopCurve.PeakToLocal(…)` |
| `AnimateToFrontRow` | `TileFlightCurve.PeakToLocal(rise.Peak, …)` · `TileFlightCurve.LeanToward(-from.x, rise.LeanDegrees)` | `HopCurve.PeakToLocal(…)` · `HopCurve.LeanToward(-from.x, rise.LeanDegrees, LEAN_MIN_DX)` |
| `StepRise` | `TileFlightCurve.ProgressAfter(t, rise.SinkEnd)` · `RiseRamp(k, rise.ScaleEnd)` · `RiseRamp(k, rise.TintEnd)` · `RiseLift(t, peak, rise.SinkDepth, rise.SinkEnd)` · `LeanRamp(k)` | `HopCurve.ProgressAfter(…)` · `HopCurve.EaseOutUntil(…)` ×2 · `HopCurve.LiftWithAnticipation(…)` · `HopCurve.LeanRamp(k, LEAN_RISE_END, LEAN_FALL_START)` |

`Entities/Tile/TileFlightCurve.cs` **xoá hẳn** (cả `.meta`); summary của `TileRiseTuning` trong `TileFeelTuning.cs`: "Hình dạng ở
`TileFlightCurve.Rise*`" → "Hình dạng ở `HopCurve.LiftWithAnticipation`, `EaseOutUntil`".

### `Entities/Box/`

| File | Đổi |
|---|---|
| `BoxFeelTuning.cs` | xoá `DipTuning`, `FlashTuning`, `LaunchTuning`, `BoxFeelCurve.DipScaleY`, `BoxFeelCurve.LaunchScaleY`; giữ `BoxFeelCurve.LidStart` và `BoxCompleteTuning`; `Launch` đổi kiểu `LaunchSpec`, `LidFlash` đổi kiểu `FlashSpec` (cùng tên field, cùng giá trị mặc định); thêm `using Horcrux.Runtime.Utilities.Tweening;` |
| `BoxReceiveTuning.cs` | `Dip` → `DipSpec`, `Flash` → `FlashSpec`; ba núm `FramePunchScale`, `FramePunchDelay`, `FramePunchDuration` → `[Tooltip("Khung phồng lên rồi về sau đế một nhịp: lực đi từ khung xuống đế rồi bật lại.")] public PopSpec FramePunch = new(1.10f, 0.03f, 0.18f);`; xoá `CounterPopTuning` |
| `BoxAnimation.cs` | `ApplyDip(float t, in DipSpec dip) => ApplyScaleY(dip.EvaluateScaleY(t))` · `BoxFeelCurve.LaunchScaleY(t, anim.complete.Launch)` → `anim.complete.Launch.EvaluateStretchScaleY(t)` · `InsertFlash(_completeSequence, touch, lidFlash, complete.LidFlash)` → `_completeSequence.InsertFlash(touch, lidFlash, complete.LidFlash)` · `InsertReceiveAccents`: `InsertFlash(sequence, at, frameFlash, tuning.Flash)` → `sequence.InsertFlash(at, frameFlash, tuning.Flash)`, hai dòng punch → `sequence.Insert(at, frame.transform.CharmPop(_frameRestScale, tuning.FramePunch));` · xoá `InsertFlash` private · `ApplyLandCompress` giữ nguyên · thêm `using Horcrux.Runtime.Utilities.Tweening;` |
| `CategoryBox.cs` | `CounterPopTuning counterPop` → `PopSpec counterPop` (tên field giữ); thân `UpdateCounter` từ `_counterPopTween = Sequence.Create().ChainDelay(…).Chain(…)` → `_counterPopTween = _counterText.transform.CharmPop(_counterOriginalScale, counterPop);`; bỏ comment hai dòng về `ChainDelay`; thêm `using Horcrux.Runtime.Utilities.ExtensionMethods;`, `using Horcrux.Runtime.Utilities.Tweening;` |

### `Entities/Shelf/`, `Entities/TileSlot/`

| File | Đổi |
|---|---|
| `SingleShelfAmountBar.cs` | `CounterPopTuning amountPop` → `PopSpec amountPop`; `PlayPop`: `_popSequence = text.CharmPop(_amountRestScale, amountPop);`, bỏ comment về `ChainDelay` |
| `CacheTileSlot.cs` | xoá `SlotSpringTuning`; `landSpring = new(0.12f, 0.06f, 0.18f, Ease.InSine)`, `shiftSpring = new(0.08f, 0.06f, 0.14f, Ease.InSine)` kiểu `SinkSpec`; `PlaySpring(in SinkSpec spring) => transform.CharmSinkLocalY(_restLocalY, spring);` vẫn không giữ handle |
| `ShelfPromoteTuning.cs` | `LandPitch` → `1f + Interpolator.OffsetFromCenter(slotIndex, capacity, PitchStep)`; thêm `using Horcrux.Runtime.Utilities.PhysXHelper;` |

### Test và tài liệu dự án

| File | Đổi |
|---|---|
| `Tests/EditMode/TileFlightCurveTests.cs` | xoá sau khi GĐ1–GĐ3 đã chuyển 78 case; 4 case `LandPitch_CentersOnMiddleSlot` sang file mới `ShelfPromoteTuningTests.cs` |
| `Tests/EditMode/BoxFeelCurveTests.cs` | giữ 2 case `LidStart_*`, xoá 9 case đã chuyển (GĐ4, GĐ8) |
| `Scripts/Feel.md` §3, bảng §4, dòng "Máy kiểm" · `Structure.md` | tên `TileFlightCurve`, `BoxFeelCurve.DipScaleY`, `DipTuning`, `FlashTuning`, `CounterPopTuning`, `SlotSpringTuning`, `FramePunch*` → tên Horcrux hoặc `HopCurve`; `BoxFeelCurve.LidStart` giữ |

### YAML (agent sửa text rồi kiểm giá trị trên object đã dựng bằng eval, không tin YAML suông)

Trước khi sửa: grep các key dưới trong `*.prefab`, `*.unity`, `*.asset` của `Assets/` để chắc không variant hay scene nào override
(hiện chỉ ba prefab dưới có).

| Prefab | Khối | Đổi |
|---|---|---|
| `Addressables/Box/CategoryBox.prefab` | `receive` **và** `finalReceive` | ba dòng `FramePunchScale`, `FramePunchDelay`, `FramePunchDuration` → khối `FramePunch:` với `Scale`, `Delay`, `Duration`, giữ nguyên giá trị từng khối (hai khối khác số) |
| `CategoryBox.prefab` | `receive.Dip`, `receive.Flash`, `finalReceive.Dip`, `finalReceive.Flash`, `complete.Launch`, `complete.LidFlash`, `counterPop` | không đổi: key trùng tên field của struct mới |
| `Addressables/Shelf/SingleShelf.prefab` | `amountPop` | không đổi |
| `Prefabs/TileSlot/CacheTileSlot.prefab` | `landSpring`, `shiftSpring` | thêm dòng `DownEase: 2` (InSine) vào mỗi khối |
| `Addressables/Tile/Tile.prefab` | `flight`, `rise` | không đổi: class núm ở lại dự án |

**Editor setup:** không kéo thả — mọi struct mới là field serialize cùng tên; mở prefab trong Prefab Mode một lần để Unity ghi lại và
kiểm `git diff` chỉ có các khối trong bảng.

**Cheat:** không — mọi kịch bản tới được trong một level; `BoxAnimation` có nút Inspector `Play Receive` (Play mode) và `Preview Complete`
để xem cú nhận và cú vọt không cần đủ tile.

**Kiểm:**
- grep `TileFlightCurve`, `BoxFeelCurve.DipScaleY`, `BoxFeelCurve.LaunchScaleY`, `DipTuning`, `FlashTuning`, `LaunchTuning`,
  `SlotSpringTuning`, `CounterPopTuning`, `FramePunchScale` trong `Assets/_CategoryJam` (`.cs`, `.md`, `.prefab`) = 0; compile sạch; EditMode
  và PlayMode Horcrux, EditMode dự án xanh.
- Eval trên object đã dựng: `CacheTileSlot.landSpring.DownEase == Ease.InSine`, `shiftSpring.DownEase == Ease.InSine`; `receive.FramePunch`,
  `finalReceive.FramePunch`, `receive.Dip`, `receive.Flash`, `finalReceive.Dip`, `finalReceive.Flash`, `complete.Launch`, `complete.LidFlash`,
  `counterPop`, `SingleShelf.amountPop`, `landSpring`, `shiftSpring` đúng YAML của prefab **lúc làm** (số đọc lại từ prefab, không chép từ Plan).
- `git diff` của `CategoryBox.prefab`, `CacheTileSlot.prefab` chỉ có các khối trong bảng YAML.
- **Cảm giác không đổi:** trace sau GĐ10 khớp mốc quay trước khi sửa **0 chênh** trên mọi khung; pitch không ghi vào trace nên `LandPitch`
  dựa vào test bằng nhau tuyệt đối ở GĐ3.
- **Cần chơi thử** (developer): vào một level có kệ hai hàng và kệ Single. Làm: tap tile vào cache, tap tiếp để tile dồn trái, đưa tile
  vào box tới khi box đầy, tap hết hàng trước của một kệ để hàng sau nhảy lên. Nhìn: tile bay vồng lên rồi rơi vào ô cùng một kiểu dù
  từ kệ nào; ô cache nhún khi nhận và khi dồn; box lún, lòng khung loé, khung nảy, số đếm nảy; tile cuối đón mạnh hơn, nắp đóng, box
  nén rồi vọt; tile hàng sau nhún rồi nhảy lên, ba tiếng clack lên dần trái sang phải; số lớp kệ Single nảy khi giảm. Dấu hiệu hỏng:
  tile đập vào ô (cỡ nhảy), ô cache lún rồi không về đúng chỗ, khung nảy sớm hơn hay muộn hơn đế, số đếm đứng yên, ô cache xuống
  "bật" thay vì "bị đè" (`DownEase` rơi về 0).

**Mốc commit:** repo dự án `refactor(feel): HopCurve và các Spec của Horcrux thay TileFlightCurve, BoxFeelTuning, SlotSpringTuning, CounterPopTuning`
kèm con trỏ submodule.

---

## File → giai đoạn

| File | GĐ1 | GĐ2 | GĐ3 | GĐ4 | GĐ5 | GĐ6 | GĐ7 | GĐ8 | GĐ9 | GĐ10 |
|---|---|---|---|---|---|---|---|---|---|---|
| `Runtime/Utilities/PhysXHelper/HopCurve.cs` | tạo | 6 hàm | 3 hàm | | | | | | | |
| `Runtime/Utilities/PhysXHelper/Interpolator.cs` | | `SmoothStep` | `OffsetFromCenter` | | | | | | | |
| `Runtime/Utilities/Tweening/DipSpec.cs` | | | | tạo | | | | | | |
| `Runtime/Utilities/Tweening/FlashSpec.cs` | | | | | tạo | | | | | |
| `Runtime/Utilities/ExtensionMethods/SequenceExtensions.cs` | | | | | tạo | | | | | |
| `Runtime/Utilities/Tweening/SinkSpec.cs` | | | | | | tạo | | | | |
| `Runtime/Utilities/ExtensionMethods/Transform/TransformExtensions.Accent.cs` | | | | | | tạo | `CharmPop` | | | |
| `Runtime/Utilities/Tweening/PopSpec.cs` | | | | | | | tạo | | | |
| `Runtime/Utilities/Tweening/LaunchSpec.cs` | | | | | | | | tạo | | |
| `Tests/EditMode/HopCurveTests.cs` (agent) | tạo | thêm case | thêm case | | | | | | | |
| `Tests/EditMode/InterpolatorTests.cs` (agent) | | tạo | thêm case | | | | | | | |
| `Tests/EditMode/DipSpecTests.cs`, `AccentSpecTests.cs`, `LaunchSpecTests.cs` (agent) | | | | tạo | | tạo | | tạo | | |
| `Tests/PlayMode/CharmAccentTests.cs` (agent) | | | | | tạo | thêm case | thêm case | | | |
| `docs/Runtime/HopCurve/`, `docs/Runtime/CharmAccent/` (`.md` + `.html`), `docs/Runtime/Interpolator/Interpolator.html`, `Plans/FeelingPlan.md` (agent) | | | | | | | | | tạo, sửa | |
| dự án: `Tile.cs`, `TileFeelTuning.cs`, xoá `TileFlightCurve.cs`, `BoxFeelTuning.cs`, `BoxReceiveTuning.cs`, `BoxAnimation.cs`, `CategoryBox.cs`, `SingleShelfAmountBar.cs`, `CacheTileSlot.cs`, `ShelfPromoteTuning.cs`, hai test, `Feel.md`, `Structure.md`, ba prefab | | | | | | | | | | đổi |
