# Plan — `CharmFlyArc` bám đích đang di chuyển

Ngày 2026-11-23. Nguồn: category-jam `Assets/_CategoryJam/Scripts/Entities/Booster/BoosterActor.cs` (`Launch`, `ApplyFlight`), đã chơi
thử: vật booster bay từ nút tới một `Transform` có thể đang trượt (box trượt vào chỗ), thời lượng bằng tổng clip Spine bay, đích đọc lại
mỗi frame. `CharmFlyArc` có sẵn chốt đích là `Vector3` lúc gọi và tự tính thời lượng từ `speed`; overload mới đọc đích mỗi frame, nhận
thời lượng từ caller.

**Phân công:** GĐ1 developer gõ, agent viết test theo bảng case rồi compile · GĐ2 (tài liệu) · GĐ3 (dự án chuyển sang bản Horcrux, xoá
Plan) agent làm.
**Chạy được, thấy được:** GĐ1 compile sạch + bảng case xanh trong `Tests/PlayMode/CharmFlyArcTests.cs` (Test Runner, PlayMode); thấy
trong game ở GĐ3.
**Lệnh kiểm** (chạy từ gốc dự án): GĐ1–GĐ2 `bash "<horcrux>/Editor/CompileCheck/Tools~/verify.sh" --csproj
com.horcrux.runtime.csproj,Horcrux.PlayModeTests.csproj --tests Horcrux.EditModeTests` · GĐ3 `--csproj
com.horcrux.runtime.csproj,CategoryJam.Gameplay.csproj --tests CategoryJam.EditModeTests,Horcrux.EditModeTests`. PlayMode không qua tool:
chạy trong Window › General › Test Runner.
**Commit:** GĐ1, GĐ2 trong `Assets/Horcrux`; GĐ3 ở dự án kèm con trỏ submodule.
**Phụ thuộc:** không.
**Chung file:** `TransformExtensions.CharmTween.cs` — `Horcrux_FeelUtilities_Plan.md` GĐ6 thêm `<remarks>` closure trên overload cũ; overload
mới mang cùng câu, nên gõ trước hay sau đều không đụng nhau (neo ở đây là cuối thân overload cũ). `docs/Runtime/CharmFlyArc/CharmFlyArc.md` —
`Horcrux_ArchedControlPoint_Plan.md` (tài liệu), `Horcrux_FeelUtilities_Plan.md` (§2), `Horcrux_UniTextSetFraction_Plan.md` (§3); GĐ2 ở đây
tạo `.html`, nên gõ ArchedControlPoint sau Plan này để sửa `.md` và `.html` một lần.
**Đã kiểm trước khi ghi:** csc của Rider, `-langversion:9`, trên `UnityEngine.CoreModule`/`UIElementsModule` của Editor 6000.0.73f1 và
`Library/ScriptAssemblies` (`com.horcrux.runtime`, `PrimeTween.Runtime`): bản sao `TransformExtensions.CharmTween.cs` có thêm overload mới,
cùng lời gọi GĐ3 của `Launch` (bọc trong một class thử) — 0 lỗi. Bản mẫu: ca đường thẳng (`controlOffset` 0, đích trượt) là bản dự án
đã chơi thử, và bezier có điểm điều khiển ở giữa đoạn bằng đúng `LerpUnclamped` (đẳng thức dưới). Ca cung cong bám đích chưa có người
dùng chạy thật; bảng case GĐ1 (case 3, 4) là phép kiểm đầu tiên của nó. `§0`: không — một đẳng thức, ghi ở Ngữ cảnh.

## Ngữ cảnh đã chốt

- **Người dùng:** vật bay vào một chủ đang trượt, lún, căn lại (hộp trượt vào chỗ, kệ đang dời) · vật thu thập bay về icon HUD đang nảy ·
  vật bay theo nhịp một anim của hoạ sĩ (Spine, Animator) nên thời lượng do anim quyết.
- **Mục tiêu:** thêm **một overload** `CharmFlyArc(Transform target, ArcFlightSpec spec, float duration, bool useUnscaledTime, ct)`:
  đích đọc lại mỗi frame, cung tính lại theo đích hiện tại, thời lượng do caller đưa. Overload `Vector3` cũ giữ nguyên.
- **Ranh giới:** bay trong world (hoặc cùng một hệ toạ độ với đích); quy đổi giữa canvas và world là việc của caller.
- **Chỗ đặt:** `Runtime/Utilities/ExtensionMethods/Transform/TransformExtensions.CharmTween.cs`, cạnh overload cũ. Không file mới.
- **Cố ý KHÔNG làm, kèm lý do:**
  - overload `Transform` + `speed` — chưa có caller; thêm sau là một hàm, không đổi chữ ký nào.
  - field mới trong `ArcFlightSpec` (thời lượng, chọn thang thời gian) — spec là hình dạng cung dùng chung; thời lượng và thang thời gian
    là của từng lần gọi.
- **Quyết định trái trực giác:**
  - **Overload này không đọc `spec.speed`:** thời lượng là đầu vào, vì nguồn nhịp nằm ngoài (clip anim); tính từ quãng thì vật tới đích
    lệch frame clip tác động bắt đầu.
  - **`useUnscaledTime` là tham số bắt buộc** (overload cũ gắn cứng `true`): vật bay cùng nhịp anim scaled phải dừng khi game dừng
    `timeScale`, cùng lúc anim dừng; quên tham số là compile error, không phải lệch nhịp im lặng.
  - **Đích đọc lại mỗi frame:** chủ mới có thể đang trượt; chốt lúc cất cánh thì vật đáp chỗ cũ rồi giật. Giá là một lần đọc vị trí mỗi
    frame.
  - **Điểm điều khiển tính lại mỗi frame** từ `from` và đích hiện tại: cung giữ hình dạng tương đối khi đích dời, không cong gãy.
  - **Đích bị huỷ giữa chừng thì bay nốt tới chỗ cuối đã đọc:** không ném, không dừng giữa không trung.
  - **`controlOffset = 0` cho đường thẳng đúng `LerpUnclamped(from, đích, ease(t))`:** bezier bậc hai có điểm điều khiển ở giữa đoạn thẳng
    là nội suy tuyến tính với mọi `t`, kể cả ease vọt quá 1:
    $B(t) = (1-t)^2F + 2t(1-t)\frac{F+T}{2} + t^2T = (1-t)F + tT$.
- **Mở rộng sau:** overload `Transform` + `speed` khi có caller bay tốc độ đều tới đích động · 0 B mỗi lần gọi (bỏ closure) khi có caller
  gọi mỗi frame.

## Đã khảo sát

| Nguồn | Lấy | Không lấy, vì sao |
|---|---|---|
| bản tạm category-jam `BoosterActor` | đích `Transform` đọc mỗi frame · thời lượng từ clip · ease từ Inspector · thời gian scaled (cùng nhịp Spine) | `Lerp` riêng, `_from`, `_target`, `ApplyFlight` — thay bằng overload |
| Horcrux `CharmFlyArc(Vector3, …)` | `from` = vị trí lúc gọi · `ComputeControlPoint`, `EvaluateQuadraticBezier` · trả `Tween` + `SetCancellationToken` · không truyền `self` làm target của `Tween.Custom` | `speed → duration`; đích chốt lúc gọi |
| Horcrux `BezierCurveHelper` | hai hàm trên, gọi mỗi frame (vài phép nhân) | — |
| `Horcrux_FeelUtilities_Plan.md` GĐ6 | câu `<remarks>` về closure của `CharmFlyArc` | — |
| `Horcrux_UIProxyFlight_Plan.md` | — | bay trên canvas tới điểm màn hình chốt lúc gọi, đường `CharmJump`/`HopCurve`; bám đích ghi ở Mở rộng sau của Plan đó |

## Luồng — một chuyến bay tới đích động

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

## GĐ1 — Overload bám đích

**Mục tiêu:** vật tới đúng chỗ đích đang đứng ở frame cuối, dù đích dời suốt chuyến; thời lượng đúng số giây caller đưa. Xong GĐ thì
caller bay theo nhịp anim tới đích động bằng một lời gọi.

**Vì sao bây giờ:** đây là toàn bộ thay đổi runtime.

**Ai gọi:** test; trong dự án `BoosterActor.Launch` (GĐ3).

**Sơ đồ sau GĐ1** (`+` là phần mới)

```
  caller ──CharmFlyArc(Transform)+──► Tween.Custom ──spell+ (đọc đích mỗi frame)──► ComputeControlPoint, EvaluateQuadraticBezier
         ──CharmFlyArc(Vector3)────► (không đổi)
```

### `TransformExtensions.CharmTween.cs` › sau `CharmFlyArc(this Transform self, Vector3 target, …)`

Neo: kết thúc overload cũ, ngay trước `CharmSquashStretch`

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
        /// <remarks>Một closure mỗi lần gọi (bắt self, target, from, lastTarget, spec): dùng cho vật bay thưa, không cho hot path mỗi frame.</remarks>
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

| Consumes | Produces |
|---|---|
| `Tween.Custom`, `SetCancellationToken`, `BezierCurveHelper.ComputeControlPoint`, `EvaluateQuadraticBezier`, `ArcFlightSpec` | `CharmFlyArc(this Transform, Transform, ArcFlightSpec, float, bool, CancellationToken)` |

**Quyết định:**
- `target != null`, không `?.`: so null của Unity bắt cả object đã `Destroy`.
- Local function bắt `lastTarget`, `from`, `spec`: một closure mỗi lần gọi, nhịp *mỗi tương tác* (một chuyến bay), như overload cũ; câu
  `<remarks>` cùng chữ với overload cũ.
- Không truyền `self` làm target của `Tween.Custom`: cùng hợp đồng với overload cũ — caller dừng tween khi tắt hay trả pool vật (bẫy §4
  của tài liệu).
- *Bỏ:* chốt điểm điều khiển lúc gọi rồi chỉ đổi đích — đích dời ngang làm cung lệch về phía cũ, vật quẹo gắt ở cuối chuyến.

**Editor setup:** không.

**Kiểm:** lệnh kiểm GĐ1–GĐ2; agent viết `Tests/PlayMode/CharmFlyArcTests.cs` theo khuôn `CharmJumpTests` (tween `isPaused`, đặt
`progress`, đọc vị trí):

| # | Case | Kỳ vọng |
|---|---|---|
| 1 | đích đứng yên, `controlOffset 0.5`, `progress ∈ {0, 0.25, 0.5, 1}` | trùng overload `Vector3` cùng spec ở mọi mốc (±1e-4) |
| 2 | `controlOffset 0`, `Ease.Linear`, `progress 0.3` | trùng `Vector3.Lerp(from, đích, 0.3)` |
| 3 | `progress 0.5`, dời đích +3 theo x, `progress 1` | vật ở đích mới — đỏ nếu đọc đích một lần lúc gọi |
| 4 | `progress 0.5`, `Destroy(đích)`, chờ một frame, `progress 1` | vật ở vị trí đích lúc trước khi huỷ, không exception — đỏ nếu bỏ `if (target != null)` |
| 5 | `duration 0.4`, `speed = 0`, không pause, chờ tween xong | `Tween.duration == 0.4`, vật tới đích (đỏ nếu đọc `speed`: chia 0) |
| 6 | `useUnscaledTime false`, `Time.timeScale = 0`, chờ 0.2 s realtime | `progress` vẫn 0 (đỏ nếu gắn cứng `true`); trả `timeScale = 1` ở TearDown |

**Mốc commit:** `feat(tween): CharmFlyArc bám Transform đích, thời lượng do caller`.

---

## GĐ2 — Tài liệu (agent)

**Mục tiêu:** người dùng chọn đúng overload và biết overload mới không đọc `speed`.

`docs/Runtime/CharmFlyArc/CharmFlyArc.md` › §1: bảng hai overload (khi nào dùng cái nào), các quyết định trái trực giác ở trên; §4 Bẫy
thêm hàng "overload `Transform` không đọc `speed`: spec có `speed` 0 vẫn chạy, đổi `speed` không đổi nhịp". **Tạo**
`docs/Runtime/CharmFlyArc/CharmFlyArc.html` (thư mục hiện chỉ có `.md`): trang cho developer theo nội dung `.md` hiện có cộng phần mới.

**Editor setup:** không. **Kiểm:** `.md` §1 có bảng hai overload, §4 có hàng mới; `.html` tồn tại và cùng nội dung.

**Mốc commit:** `docs(tween): CharmFlyArc overload Transform; CharmFlyArc.html`.

---

## GĐ3 — category-jam dùng overload mới, xoá Plan (agent)

**Mục tiêu:** `BoosterActor` bỏ đường bay tự viết, gọi overload mới; cảm giác không đổi.

`Assets/_CategoryJam/Scripts/Entities/Booster/BoosterActor.cs`:

| Chỗ | Cũ | Mới |
|---|---|---|
| using | — | `using Horcrux.Runtime.Utilities.ExtensionMethods;` · `using Horcrux.Runtime.Utilities.Tweening;` |
| field | `private Vector3 _from;` · `private Transform _target;` | bỏ cả hai |
| `Launch` | `_target = target;` · `_from = new Vector3(from.x, from.y, target.position.z);` · `transform.position = _from;` | `transform.position = new Vector3(from.x, from.y, target.position.z);` |
| `Launch` | `_flight = Tween.Custom(this, 0f, 1f, flightDuration, static (actor, t) => actor.ApplyFlight(t), _flightEase);` | `_flight = transform.CharmFlyArc(target, new ArcFlightSpec { controlAtRatio = 0.5f, progressEase = _flightEase }, flightDuration, useUnscaledTime: false, CancellationToken.None);` |
| method | `ApplyFlight` | xoá |

`_flightEase` giữ nguyên trong Inspector, prefab không đổi. Hành vi trùng bản cũ: `controlOffset 0` là `LerpUnclamped` (Ngữ cảnh đã chốt),
cùng thời gian scaled, cùng ease. Khác duy nhất: tween không còn gắn target `this`; `OnDisable` và `Hide` vẫn `Stop()` nó. Sau GĐ này
`BoosterActor` là caller đầu tiên của `CharmFlyArc` trong category-jam.

Xong thì **xoá Plan này** cùng `.meta`.

**Editor setup:** không.

**Kiểm:**
- `bash "<horcrux>/Editor/CompileCheck/Tools~/verify.sh" --csproj com.horcrux.runtime.csproj,CategoryJam.Gameplay.csproj --tests
  CategoryJam.EditModeTests,Horcrux.EditModeTests` xanh · grep `ApplyFlight` trong `Assets/_CategoryJam` ra 0.
- **Chơi thử** (developer): vào một level có booster Fan và Magnet. Bấm Fan: vật bay từ nút tới đúng chỗ như trước, clip tác động bắt đầu
  đúng lúc chạm. Bấm Magnet ngay khi một box vừa trượt vào: vật đáp đúng góc box. Khác trước: không gì. Dấu hiệu hỏng: vật đáp chỗ box cũ
  rồi giật · vật tới sớm hay muộn hơn clip tác động · vật bay vồng (spec có `controlOffset` khác 0).

**Mốc commit:** `refactor(booster): BoosterActor bay bằng CharmFlyArc bám đích của Horcrux` ở dự án kèm con trỏ submodule; xoá Plan trong
`Assets/Horcrux` cùng con trỏ đó.

## File → GĐ

| File | GĐ |
|---|---|
| `Runtime/Utilities/ExtensionMethods/Transform/TransformExtensions.CharmTween.cs` | 1 |
| `Tests/PlayMode/CharmFlyArcTests.cs` (agent) | 1 |
| `docs/Runtime/CharmFlyArc/CharmFlyArc.md`, `.html` (tạo) (agent) | 2 |
| category-jam `Scripts/Entities/Booster/BoosterActor.cs`; xoá Plan này | 3 |
