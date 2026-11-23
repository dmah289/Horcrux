# Plan — `CharmFlyArc` bám đích đang di chuyển

Ngày 2026-11-23. Nguồn: category-jam `Assets/_CategoryJam/Scripts/Entities/Booster/BoosterActor.cs` (`Launch`, `ApplyFlight`): vật
booster bay từ nút tới một `Transform` có thể đang trượt (box trượt vào chỗ), thời lượng bằng tổng clip Spine bay, đích đọc lại mỗi
frame. Horcrux `CharmFlyArc` hiện chốt đích là `Vector3` lúc gọi và tự tính thời lượng từ `speed`, nên bản dự án phải tự viết đường bay.

**Phân công:** GĐ1 developer gõ, agent viết test theo bảng case rồi compile · GĐ2 (tài liệu), GĐ3 (category-jam dùng overload mới)
agent làm. **Chạy được, thấy được** ở GĐ1 = compile sạch + bảng case xanh trong `Tests/PlayMode/CharmFlyArcTests.cs`; thấy trong game ở
GĐ3. GĐ1–2 commit trong `Assets/Horcrux`, GĐ3 commit ở dự án kèm con trỏ submodule. Không phụ thuộc Plan nào.

## Ngữ cảnh đã chốt

- **Người dùng:** vật bay vào một chủ đang trượt, lún, căn lại (box trượt vào chỗ, kệ đang dời) · vật thu thập bay về icon HUD đang
  nảy · vật bay theo nhịp một anim của hoạ sĩ (Spine, Animator) nên thời lượng do anim quyết.
- **Mục tiêu:** thêm **một overload** `CharmFlyArc(Transform target, ArcFlightSpec spec, float duration, bool useUnscaledTime, ct)`:
  đích đọc lại mỗi frame, cung tính lại theo đích hiện tại, thời lượng do caller đưa. Overload `Vector3` cũ giữ nguyên.
- **Chỗ đặt:** `Runtime/Utilities/ExtensionMethods/Transform/TransformExtensions.CharmTween.cs`, cạnh overload cũ. Không file mới.
- **Không làm:**
  - Overload `Transform` + `speed`: chưa có caller. Thêm sau là một hàm, không đổi chữ ký nào.
  - Field mới trong `ArcFlightSpec` (thời lượng, chọn thang thời gian): spec là hình dạng cung dùng chung; thời lượng và thang thời gian
    là của từng lần gọi.
- **Quyết định trái trực giác:**
  - Overload này **không đọc `spec.speed`**: thời lượng là đầu vào, vì nguồn nhịp nằm ngoài (clip anim); tính từ quãng thì vật tới
    đích lệch frame clip tác động bắt đầu.
  - `useUnscaledTime` là tham số bắt buộc, không mặc định `true` như overload cũ: vật bay cùng nhịp anim scaled phải dừng khi game
    dừng `timeScale`, cùng lúc anim dừng; quên tham số là compile error, không phải lệch nhịp im lặng.
  - **Điểm điều khiển tính lại mỗi frame** từ `from` và đích hiện tại: cung giữ hình dạng tương đối khi đích dời, không cong gãy.
  - **Đích bị huỷ giữa chừng thì bay nốt tới chỗ cuối đã đọc**: không ném, không dừng giữa không trung.
  - `controlOffset = 0` cho đường thẳng **đúng** `Lerp(from, đích, ease(t))`: bezier bậc hai có điểm điều khiển ở giữa đoạn thẳng
    là nội suy tuyến tính, $B(t) = (1-t)^2F + 2t(1-t)\frac{F+T}{2} + t^2T = (1-t)F + tT$.
- **Mở rộng sau:** overload `Transform` + `speed` khi có caller bay tốc độ đều tới đích động.

**Đã khảo sát**

| Nguồn | Lấy | Không lấy, vì sao |
|---|---|---|
| Horcrux `CharmFlyArc(Vector3, …)` | `from` = vị trí lúc gọi · `ComputeControlPoint`, `EvaluateQuadraticBezier` · trả `Tween` + `SetCancellationToken` | `speed → duration`; đích chốt lúc gọi |
| Horcrux `BezierCurveHelper` | hai hàm trên, gọi mỗi frame (vài phép nhân) | — |
| `BoosterActor` (dự án) | đích `Transform` đọc mỗi frame · thời lượng từ clip · ease từ Inspector · thời gian scaled (cùng nhịp Spine) | `Lerp` riêng, `_from`, `ApplyFlight` — thay bằng overload |
| Skill `async-presentation` › "Đích chuyến bay đọc lại mỗi frame" | luật nghiệm thu | — |
| `UIProxyFlight` (Plan khác) | — | bay trên canvas tới điểm màn hình, quy đổi hệ toạ độ là việc riêng |

## Luồng mô tả — một chuyến bay tới đích động

```
caller đặt vật ở điểm phóng ──► CharmFlyArc(đích Transform, spec, duration, unscaled?, ct)     GĐ1
        ▼  mỗi frame của tween (t đã qua spec.progressEase)
   đích còn sống? ──► lastTarget = đích.position
   control = ComputeControlPoint(from, lastTarget, offset, ratio) ──► position = bezier(t, from, lastTarget, control)
        ▼
t = 1: vật đứng đúng vị trí đích ở frame đó · huỷ ct: dừng tại chỗ
```

## Bản đồ hệ cuối

```
caller (vật bay; giữ Tween để Stop khi tắt, trả pool)
   │ CharmFlyArc(Vector3 target, spec, ct)                               có sẵn — đích chốt, thời lượng từ speed
   │ CharmFlyArc(Transform target, spec, duration, useUnscaledTime, ct)  mới — đích đọc mỗi frame
   ▼
TransformExtensions.CharmTween ──► Tween.Custom ──► BezierCurveHelper.ComputeControlPoint, EvaluateQuadraticBezier
```

---

## Giai đoạn 1 — Overload bám đích

**Mục tiêu:** vật tới đúng chỗ đích đang đứng ở frame cuối, dù đích dời suốt chuyến; thời lượng đúng số giây caller đưa.

**Vì sao bây giờ:** đây là toàn bộ thay đổi runtime.

### `TransformExtensions.CharmTween.cs` › sau `CharmFlyArc(this Transform self, Vector3 target, …)` — thêm

Neo: kết thúc overload cũ

```csharp
            void spell(float t) => self.position = BezierCurveHelper.EvaluateQuadraticBezier(t, from, target, controlPoint);
        }
```

Đoạn mới ngay sau:

```csharp

        /// <summary>
        /// Bay cung bezier tới một đích có thể đang di chuyển: đích và điểm điều khiển đọc lại mỗi frame, thời lượng do caller
        /// quyết (không đọc <c>spec.speed</c>). Đích bị huỷ giữa chừng thì bay nốt tới chỗ cuối đã đọc.
        /// </summary>
        /// <param name="useUnscaledTime">false khi chuyến bay phải cùng nhịp thứ chạy theo <c>Time.deltaTime</c> (anim Spine, Animator).</param>
        public static Tween CharmFlyArc(this Transform self, Transform target, ArcFlightSpec spec, float duration,
            bool useUnscaledTime, CancellationToken ct)
        {
            Vector3 from = self.position;
            Vector3 lastTarget = target.position;

            return Tween.Custom(0f, 1f, duration, spell, spec.progressEase, useUnscaledTime: useUnscaledTime)
                .SetCancellationToken(ct);

            void spell(float t)
            {
                if (target != null)
                    lastTarget = target.position;
                Vector3 controlPoint = BezierCurveHelper.ComputeControlPoint(from, lastTarget, spec.controlOffset, spec.controlAtRatio);
                self.position = BezierCurveHelper.EvaluateQuadraticBezier(t, from, lastTarget, controlPoint);
            }
        }
```

*Quyết định:*
- `target != null`, không `?.`: so null của Unity bắt cả object đã `Destroy`.
- Local function bắt `lastTarget`, `from`, `spec`: một closure mỗi lần gọi, nhịp *mỗi tương tác* (một chuyến bay), như overload cũ.
- Không truyền `self` làm target của `Tween.Custom`: giữ cùng hợp đồng với overload cũ — caller dừng tween khi tắt hay trả pool vật
  (bẫy §4 của tài liệu).
*Bỏ:* chốt điểm điều khiển lúc gọi rồi chỉ đổi đích — đích dời ngang làm cung lệch về phía cũ, vật quẹo gắt ở cuối chuyến.

**Kiểm** — compile sạch; agent viết `Tests/PlayMode/CharmFlyArcTests.cs` theo khuôn `CharmJumpTests` (tween `isPaused`, đặt
`progress`, đọc vị trí):

| # | Case | Kỳ vọng |
|---|---|---|
| 1 | đích đứng yên, `controlOffset 0.5`, `progress ∈ {0, 0.25, 0.5, 1}` | trùng overload `Vector3` cùng spec ở mọi mốc (±1e-4) |
| 2 | `controlOffset 0`, `Ease.Linear`, `progress 0.3` | trùng `Vector3.Lerp(from, đích, 0.3)` |
| 3 | `progress 0.5`, dời đích +3 theo x, `progress 1` | vật ở đích mới |
| 4 | `progress 0.5`, `Destroy(đích)`, chờ một frame, `progress 1` | vật ở vị trí đích lúc trước khi huỷ, không exception |
| 5 | `duration 0.4`, không pause, chờ tween xong | `Tween.duration == 0.4` (không đọc `speed`; `speed = 0` vẫn chạy) |
| 6 | `useUnscaledTime false`, `Time.timeScale = 0`, chờ 0.2 s realtime | `progress` vẫn 0; trả `timeScale = 1` ở TearDown |

Đối chứng đỏ: case 3 đỏ nếu đọc đích một lần lúc gọi; case 4 đỏ nếu bỏ `if (target != null)`.

**Commit:** `feat(tween): CharmFlyArc bám Transform đích, thời lượng do caller`.

---

## Giai đoạn 2 — Tài liệu (agent)

`docs/Runtime/CharmFlyArc/CharmFlyArc.md` › §1: bảng hai overload (khi nào dùng cái nào), ba quyết định trái trực giác ở trên, bẫy
"overload Transform không đọc `speed`". `.html` cùng thư mục đồng bộ theo `.md`. Xoá Plan này cùng commit.
**Commit:** `docs(tween): CharmFlyArc overload Transform; xoá Plan`.

## Giai đoạn 3 — category-jam dùng overload mới (agent)

`Scripts/Entities/Booster/BoosterActor.cs`:

| Chỗ | Cũ | Mới |
|---|---|---|
| using | — | `using Horcrux.Runtime.Utilities.ExtensionMethods;` · `using Horcrux.Runtime.Utilities.Tweening;` |
| field | `_from`, `_target` | bỏ cả hai; `Launch` đặt thẳng `transform.position = new Vector3(from.x, from.y, target.position.z);` thay ba dòng gán `_target`, `_from`, `position` |
| `Launch` | `_flight = Tween.Custom(this, 0f, 1f, flightDuration, static (actor, t) => actor.ApplyFlight(t), _flightEase);` | `_flight = transform.CharmFlyArc(target, new ArcFlightSpec { controlAtRatio = 0.5f, progressEase = _flightEase }, flightDuration, useUnscaledTime: false, CancellationToken.None);` |
| method | `ApplyFlight` | xoá |

`_flightEase` giữ nguyên trong Inspector, prefab không đổi. Hành vi trùng bản cũ: `controlOffset 0` là `Lerp` (§ Ngữ cảnh đã chốt),
cùng thời gian scaled, cùng ease. **Kiểm:** grep `ApplyFlight` ra 0 · `verify.sh` xanh · developer chơi thử: bấm Fan, Magnet — vật bay từ
nút tới đúng chỗ như trước, Magnet đáp đúng góc box kể cả khi box vừa trượt vào. **Commit:** ở dự án, kèm con trỏ submodule.

## File → giai đoạn

| File | GĐ |
|---|---|
| `Runtime/Utilities/ExtensionMethods/Transform/TransformExtensions.CharmTween.cs` | 1 |
| `Tests/PlayMode/CharmFlyArcTests.cs` | 1 |
| `docs/Runtime/CharmFlyArc/CharmFlyArc.md`, `.html` | 2 |
| category-jam `Scripts/Entities/Booster/BoosterActor.cs` | 3 |
