# Plan — `CharmFlyArc` bám đích đang di chuyển

Bản tạm: category-jam `Scripts/Entities/Booster/BoosterActor.cs` › `Launch`, `ApplyFlight`.
**Phụ thuộc:** không — gõ lúc nào cũng được. Chung file Horcrux `TransformExtensions.CharmTween.cs` với [FeelUtilities](Horcrux_FeelUtilities_Plan.md) GĐ6, thứ tự nào cũng được.
Kiểm: `bash "<horcrux>/Editor/CompileCheck/Tools~/verify.sh" --csproj com.horcrux.runtime.csproj,Horcrux.PlayModeTests.csproj --tests Horcrux.EditModeTests`; chuyển dự án `--csproj com.horcrux.runtime.csproj,CategoryJam.Gameplay.csproj --tests CategoryJam.EditModeTests,Horcrux.EditModeTests`; PlayMode chạy ở Window › General › Test Runner.

## Kịch bản

| Tình huống | Hệ làm gì | Caller thấy |
|---|---|---|
| đích đứng yên | như overload `Vector3`, thời lượng từ caller | cùng đường bay với overload cũ |
| đích trượt, lún, nảy suốt chuyến | đọc lại đích và tính lại điểm điều khiển mỗi frame | vật tới đúng chỗ đích ở frame cuối, cung không gãy |
| thời lượng do clip anim (Spine, Animator) | dùng `duration` caller đưa, không đọc `spec.speed` | vật chạm đúng frame clip tác động; `speed` 0 vẫn chạy |
| game dừng `timeScale`, `useUnscaledTime` false | tween theo thời gian scaled | vật dừng cùng lúc anim |
| đích bị `Destroy` giữa chừng | giữ chỗ cuối đã đọc | bay nốt tới đó, không exception, không dừng giữa trời |
| `controlOffset` 0 | điểm điều khiển ở giữa đoạn | đúng `LerpUnclamped(from, đích, ease(t))`, kể cả ease vọt quá 1 |
| huỷ `ct`, hay caller `Stop()` khi tắt, trả pool | tween dừng | vật đứng tại chỗ |

## Flow

```
caller (giữ Tween để Stop khi tắt, trả pool)                                 BoosterActor.Launch ở dự án
  │ CharmFlyArc(Vector3 target, spec, ct)                               có sẵn — đích chốt, thời lượng từ speed
  │ CharmFlyArc(Transform target, spec, duration, useUnscaledTime, ct)  mới — đích đọc mỗi frame
  ▼
TransformExtensions.CharmTween.cs ──► Tween.Custom(t qua spec.progressEase)
  └─ spell(t) mỗi frame: đích còn sống? lastTarget = đích.position
       ├─ ComputeControlPoint(from, lastTarget, offset, ratio)                BezierCurveHelper.cs
       └─ position = EvaluateQuadraticBezier(t, from, lastTarget, control)    BezierCurveHelper.cs
```

## Thuật toán

`controlOffset = 0` thì điểm điều khiển ở giữa đoạn, Bezier bậc hai là nội suy tuyến tính với mọi `t`, kể cả `t` > 1:
$B(t) = (1-t)^2F + 2t(1-t)\frac{F+T}{2} + t^2T = (1-t)F + tT$.

Bản mẫu: ca đường thẳng (`controlOffset` 0, đích trượt) là bản dự án đã chơi thử; ca cung cong bám đích chưa người dùng chạy thật — case 3, 4 là phép kiểm đầu tiên.

---

## GĐ1 — xong thì caller bay theo nhịp anim tới đích động bằng một lời gọi

```
caller ──CharmFlyArc(Transform)+──► Tween.Custom ──spell+ (đọc đích mỗi frame)──► ComputeControlPoint, EvaluateQuadraticBezier
       ──CharmFlyArc(Vector3)────► (không đổi)
```

`Runtime/Utilities/ExtensionMethods/Transform/TransformExtensions.CharmTween.cs` › sau `CharmFlyArc(this Transform self, Vector3 target, …)`, ngay trước `CharmSquashStretch`. Có sẵn:

```csharp
            void spell(float t) => self.position = BezierCurveHelper.EvaluateQuadraticBezier(t, from, target, controlPoint);
        }
```

Ngay sau:

```csharp

        /// <summary>
        /// Bay cung bezier tới một đích có thể đang di chuyển: đích và điểm điều khiển đọc lại mỗi frame, thời lượng do caller
        /// quyết (không đọc <c>spec.speed</c>). Đích bị huỷ giữa chừng thì bay nốt tới chỗ cuối đã đọc.
        /// </summary>
        /// <param name="useUnscaledTime">false khi chuyến bay phải cùng nhịp thứ chạy theo <c>Time.deltaTime</c> (anim Spine, Animator).</param>
        /// <remarks>Một closure mỗi lần gọi (bắt self, target, from, lastTarget, spec): dùng cho vật bay thưa, không cho hot path mỗi frame.</remarks>
        // duration, không spec.speed: nhịp do anim quyết. useUnscaledTime bắt buộc: quên là compile error, không lệch nhịp im lặng.
        public static Tween CharmFlyArc(this Transform self, Transform target, ArcFlightSpec spec, float duration,
            bool useUnscaledTime, CancellationToken ct)
        {
            Vector3 from = self.position;
            Vector3 lastTarget = target.position;

            // Không truyền self làm target của Tween.Custom: cùng hợp đồng overload cũ, caller Stop khi tắt, trả pool.
            return Tween.Custom(0f, 1f, duration, spell, spec.progressEase, useUnscaledTime: useUnscaledTime)
                .SetCancellationToken(ct);

            void spell(float t)
            {
                // So null của Unity, không ?.: bắt cả object đã Destroy; huỷ thì bay nốt tới chỗ cuối.
                if (target != null)
                    lastTarget = target.position;
                // Tính lại mỗi frame, không chốt lúc gọi: đích dời ngang thì vật quẹo gắt cuối chuyến.
                Vector3 controlPoint = BezierCurveHelper.ComputeControlPoint(from, lastTarget, spec.controlOffset, spec.controlAtRatio);
                self.position = BezierCurveHelper.EvaluateQuadraticBezier(t, from, lastTarget, controlPoint);
            }
        }
```

`Tests/PlayMode/CharmFlyArcTests.cs` (agent, theo khuôn `CharmJumpTests`: tween `isPaused`, đặt `progress`, đọc vị trí):

| # | Case | Kỳ vọng |
|---|---|---|
| 1 | đích đứng yên, `controlOffset 0.5`, `progress ∈ {0, 0.25, 0.5, 1}` | trùng overload `Vector3` cùng spec ở mọi mốc (±1e-4) |
| 2 | `controlOffset 0`, `Ease.Linear`, `progress 0.3` | trùng `Vector3.Lerp(from, đích, 0.3)` |
| 3 | `progress 0.5`, dời đích +3 theo x, `progress 1` | vật ở đích mới — đỏ nếu đọc đích một lần lúc gọi |
| 4 | `progress 0.5`, `Destroy(đích)`, chờ một frame, `progress 1` | vật ở vị trí đích lúc trước khi huỷ, không exception — đỏ nếu bỏ `if (target != null)` |
| 5 | `duration 0.4`, `speed = 0`, không pause, chờ tween xong | `Tween.duration == 0.4`, vật tới đích (đỏ nếu đọc `speed`: chia 0) |
| 6 | `useUnscaledTime false`, `Time.timeScale = 0`, chờ 0.2 s realtime | `progress` vẫn 0 (đỏ nếu gắn cứng `true`); trả `timeScale = 1` ở TearDown |

---

## Chuyển dự án (agent)

| Chỗ | Đổi |
|---|---|
| thứ tự với Plan khác | `docs/Runtime/CharmFlyArc/CharmFlyArc.md` chung với [ArchedControlPoint](Horcrux_ArchedControlPoint_Plan.md), [FeelUtilities](Horcrux_FeelUtilities_Plan.md) (§2), [UniTextSetFraction](Horcrux_UniTextSetFraction_Plan.md) (§3) — Plan này tạo `.html`, chuyển trước ArchedControlPoint |
| `docs/Runtime/CharmFlyArc/CharmFlyArc.md` §1 | bảng hai overload (khi nào dùng cái nào) · đích, điểm điều khiển đọc mỗi frame · đích huỷ bay nốt · `useUnscaledTime` bắt buộc |
| `CharmFlyArc.md` §4 Bẫy | hàng "overload `Transform` không đọc `speed`: spec có `speed` 0 vẫn chạy, đổi `speed` không đổi nhịp" |
| `docs/Runtime/CharmFlyArc/CharmFlyArc.html` | tạo: trang cho developer theo `.md` hiện có cộng phần mới |
| `BoosterActor.cs` › using | thêm `using Horcrux.Runtime.Utilities.ExtensionMethods;`, `using Horcrux.Runtime.Utilities.Tweening;` |
| `BoosterActor.cs` › field | bỏ `_from`, `_target` |
| `BoosterActor.Launch` › vị trí đầu | `transform.position = new Vector3(from.x, from.y, target.position.z);` thay ba dòng gán `_target`, `_from` |
| `BoosterActor.Launch` › tween | `_flight = transform.CharmFlyArc(target, new ArcFlightSpec { controlAtRatio = 0.5f, progressEase = _flightEase }, flightDuration, useUnscaledTime: false, CancellationToken.None);` thay `Tween.Custom(this, …, ApplyFlight …)`; `_flightEase`, prefab giữ nguyên; `OnDisable`, `Hide` vẫn `Stop()` |
| `BoosterActor.ApplyFlight` | xoá; grep `ApplyFlight` trong `Assets/_CategoryJam` = 0 |
| Plan này | xoá cùng `.meta` |
| chơi thử | level có Fan, Magnet → Fan: vật bay từ nút tới đúng chỗ, clip tác động bắt đầu lúc chạm · Magnet ngay khi box vừa trượt vào: đáp đúng góc box; hỏng: đáp chỗ box cũ rồi giật, tới sớm/muộn hơn clip, bay vồng |

## Không làm · Mở rộng sau

| | Phương án | Vì sao |
|---|---|---|
| bỏ | thời lượng tính từ `spec.speed` và quãng | vật tới đích lệch frame clip tác động bắt đầu |
| bỏ | field thời lượng, thang thời gian trong `ArcFlightSpec` | spec là hình dạng cung dùng chung; thời lượng, thang thời gian thuộc từng lần gọi |
| bỏ | chốt đích lúc cất cánh | chủ mới đang trượt: vật đáp chỗ cũ rồi giật |
| bỏ | quy đổi canvas ↔ world trong hàm | bay trong một hệ toạ độ với đích; quy đổi là việc caller |
| bỏ | bám đích trên canvas | `Horcrux_UIProxyFlight_Plan.md` bay tới điểm màn hình chốt lúc gọi; bám đích ghi ở Mở rộng sau của Plan đó |
| sau | overload `Transform` + `speed` | khi có caller bay tốc độ đều tới đích động; thêm một hàm, không đổi chữ ký nào |
| sau | 0 B mỗi lần gọi (bỏ closure) | khi có caller gọi mỗi frame |
