# Plan — `BezierCurveHelper.ComputeArchedControlPoint` lên Horcrux

Nguồn: category-jam `Assets/_CategoryJam/Scripts/Entities/Tile/TileSuckCurve.cs` (`DashPoint`, call site `Tile.StepSuckDash`), cú hút
tile vào bình Pack, đã chơi thử. Một hàm static thêm vào class có sẵn: điểm điều khiển Bezier bậc hai nhấc theo trục lên của thế giới,
để cung luôn vồng lên dù vật bay sang trái hay sang phải.

**Phân công:** GĐ1 developer gõ, agent viết test theo bảng case rồi compile · GĐ2 (dự án chuyển sang bản Horcrux) và GĐ3 (tài liệu, xoá
Plan) agent làm. GĐ1, GĐ3 commit trong `Assets/Horcrux`; GĐ2 commit ở dự án; mỗi lần kèm con trỏ submodule. Không phụ thuộc Plan nào;
`Horcrux_HopCurve_DipFlash_Plan.md` GĐ10 cũng sửa `TileSuckCurve.cs` (xoá `DashDuration`), làm trước hay sau đều compile sạch. Không có
`§0`: một công thức một dòng nằm ở GĐ1.

## Ngữ cảnh đã chốt

- **Người dùng:** `Tile.StepSuckDash` — tile lao từ điểm lấy đà vào miệng bình, bình đứng bên phải thanh cache nên tile bay sang phải,
  lấy đà lùi sang trái. Dự án sau: vật bay vào đích HUD từ cả hai phía (tiền, sao, thẻ bay vào bộ bài).
- **Mục tiêu:** cung vồng về **một phía cố định của thế giới** (+Y) bất kể chiều bay. `ComputeControlPoint` có sẵn lệch theo pháp tuyến
  `(−dy, dx)` của dây cung: cùng `controlOffset` dương, bay sang phải thì vồng lên, bay sang trái thì võng xuống.
- **Chỗ đặt, tên:** thêm vào `Runtime/Utilities/PhysXHelper/BezierCurveHelper.cs`, cạnh `ComputeControlPoint`, namespace
  `Horcrux.Runtime.Utilities.PhysXHelper`. "Arched" nói hình dạng (vòm), không nói kịch bản hút.
- **Cảm giác không đổi một giá trị:** thân hàm chép nguyên văn biểu thức của `DashPoint`, kể cả thứ tự phép tính float
  (`(from + target) * 0.5f`, không `Vector3.Lerp(from, target, 0.5f)` — hai cách làm tròn khác nhau).
- **Cố ý KHÔNG làm, kèm lý do:**
  - tham số `controlAtRatio` — người dùng duy nhất đặt đỉnh ở giữa; thêm tham số thì phải đổi biểu thức sang `Lerp`, mất bằng nhau tuyệt đối.
  - tham số trục `up` — người dùng duy nhất truyền `Vector3.up`.
  - lựa chọn vồng theo trục trong `ArcFlightSpec`/`CharmFlyArc` — `CharmFlyArc` chưa có caller nào trong dự án.
  - hàm gộp "điểm trên cung vồng" — caller ghép `ComputeArchedControlPoint` với `EvaluateQuadraticBezier` có sẵn, mỗi frame đọc lại đích.
- **Quyết định trái trực giác:** đỉnh thật của cung cao **nửa** `height` so với trung điểm dây cung. `height` là độ nhấc của điểm điều
  khiển, không phải độ cao đỉnh; giữ vậy vì núm `ArcHeight` của dự án đã chỉnh trên prefab theo nghĩa này (NT9).
- **Mở rộng sau:** `ArcFlightSpec` thêm `bool archedUp` (mặc định `false`, key YAML mới không phá asset cũ) khi `CharmFlyArc` có caller
  bay hai chiều · tham số `controlAtRatio` khi có caller đặt đỉnh lệch.

## Đã khảo sát

| Nguồn | Lấy gì | Không lấy, vì sao |
|---|---|---|
| `BezierCurveHelper.EvaluateQuadraticBezier` | dùng nguyên — biểu thức trùng từng phép tính với thân cũ của `DashPoint` | — |
| `BezierCurveHelper.ComputeControlPoint` | đặt hàm mới cạnh nó, cùng kiểu chữ ký | lệch theo pháp tuyến dây cung: phía vồng đổi theo chiều bay |
| `BezierCurveHelper.ApproximateQuadraticBezierLength` | — | cú hút tính thời lượng theo dây cung (`DurationForSpeed`), không theo độ dài cung |
| `JumpArc.Evaluate` | — | parabol theo y trên đường thẳng, đỉnh bù chênh độ cao hai đầu (`Δy/4`); khác hình Bezier khi hai đầu lệch độ cao |
| `CharmFlyArc`, `ArcFlightSpec` | — | trả `Tween` tự chạy, đích chốt lúc gọi; cú hút đọc lại đích mỗi frame trong `Sequence` của tile |
| `Horcrux_HopCurve_DipFlash_Plan.md` | — | cung một chiều (`ArcY`), không phải Bezier hai chiều |

## Luồng mô tả

```
Tile.BuildSuckMotion : back = điểm lấy đà (dự án)
Tile.StepSuckDash(v) mỗi frame:
    k       = Easing.Evaluate(v, suck.DashEase)
    target  = _slot.transform.position                         (đọc lại mỗi frame: bình có thể đang nhún)
    control = BezierCurveHelper.ComputeArchedControlPoint(back, target, suck.ArcHeight)     GĐ1
    pos     = BezierCurveHelper.EvaluateQuadraticBezier(k, back, target, control)           có sẵn
```

## Bản đồ hệ cuối

| Thành phần | Ai gọi | Việc |
|---|---|---|
| `BezierCurveHelper.ComputeArchedControlPoint` (mới) | `Tile.StepSuckDash` mỗi frame | trung điểm dây cung nhấc `height` theo +Y |
| `BezierCurveHelper.EvaluateQuadraticBezier` (có sẵn) | `Tile.StepSuckDash` mỗi frame | điểm trên cung tại `k` |
| `TileSuckCurve.DashPoint` | — | xoá ở GĐ2: chỉ còn chuyển lời gọi sang hai hàm trên |

## Giai đoạn 1 — `ComputeArchedControlPoint`

**Mục tiêu:** Horcrux dựng được cung vồng lên ở cả hai chiều bay. **Vì sao trước:** là toàn bộ phần Horcrux; GĐ2 cần nó compile.

**Ai gọi:** test của giai đoạn; trong dự án là `Tile.StepSuckDash` (GĐ2).

`Runtime/Utilities/PhysXHelper/BezierCurveHelper.cs` › sau `ComputeControlPoint`, trước `ApproximateQuadraticBezierLength`. Có sẵn quanh
chỗ thêm:

```csharp
            return Vector3.Lerp(from, target, controlAtRatio) + perpendicularDirection * controlOffset;
        }

        public static float ApproximateQuadraticBezierLength(Vector3 from, Vector3 target, Vector3 controlPoint)
```

Đoạn mới, chèn giữa `}` và `public static float ApproximateQuadraticBezierLength`:

```csharp

        /// <summary>
        /// Trung điểm dây cung nhấc height theo +Y thế giới: cung vồng lên dù bay sang trái hay phải.
        /// Đỉnh cung cao height / 2 trên trung điểm.
        /// </summary>
        public static Vector3 ComputeArchedControlPoint(Vector3 from, Vector3 target, float height)
            => (from + target) * 0.5f + Vector3.up * height;
```

**Toán → code.** Với điểm điều khiển `C = M + h·ŷ`, `M = (A + B)/2`:
`B(t) = (1−t)²A + 2t(1−t)C + t²B = [(1−t)²A + 2t(1−t)M + t²B] + 2t(1−t)·h·ŷ`. Ngoặc vuông là `lerp(A, B, t)` (Bezier có điểm điều khiển
nằm giữa dây cung là đường thẳng đi đều), nên cung = dây cung + `2t(1−t)·h` theo +Y. Đỉnh ở `t = 0.5`: `2·0.25·h = h/2`. Pháp tuyến không
xuất hiện, nên chiều bay không đổi phía vồng.

**Quyết định, phương án bỏ, vì sao:** đặt trong `BezierCurveHelper`, không trong `HopCurve` (Plan khác) — cùng họ với `ComputeControlPoint`,
caller tìm điểm điều khiển ở một chỗ · expression-bodied, không `MethodImpl(AggressiveInlining)` — một phép cộng, JIT tự inline · trục cố
định `Vector3.up` — thế giới 2D của Horcrux nằm trên mặt XY như `ComputeControlPoint`.

**Editor setup:** không.

**Kiểm:** compile sạch; test EditMode `Tests/EditMode/BezierCurveHelperTests.cs` (agent viết sau khi đọc code thật, sai số `1e-5`):

| Input | Kỳ vọng |
|---|---|
| `ComputeArchedControlPoint((0,0,0), (4,0,0), 0.6)` | `(2, 0.6, 0)` |
| `ComputeArchedControlPoint((4,0,0), (0,0,0), 0.6)` | `(2, 0.6, 0)` — bay sang trái vẫn vồng lên |
| `ComputeControlPoint((4,0,0), (0,0,0), 0.6)` (đối chứng) | `(2, −0.6, 0)` — bản pháp tuyến võng xuống; case này đỏ nếu ai đổi `ComputeControlPoint` mà không đọc Plan |
| `ComputeArchedControlPoint((0,0,0), (4,2,0), 0)` | `(2, 1, 0)` — trung điểm |
| `ComputeArchedControlPoint((0,0,0), (4,0,0), −0.6)` | `(2, −0.6, 0)` — `height` âm võng xuống |
| `EvaluateQuadraticBezier(t, (−2,1,0), (3,2,0), ComputeArchedControlPoint(…, 0.5))` tại `t` 0 · 1 | `(−2,1,0)` · `(3,2,0)` |
| `EvaluateQuadraticBezier(0.5, (0,0,0), (4,0,0), ComputeArchedControlPoint(…, 0.6))` | `(2, 0.3, 0)` — đỉnh cao `h/2` |
| `EvaluateQuadraticBezier(0.25, (0,0,0), (4,2,0), ComputeArchedControlPoint(…, 0))` | `(1, 0.5, 0)` — `height` 0 là đường thẳng |
| bằng nhau tuyệt đối: 1000 bộ `(A, B, h, t)` từ `System.Random(20261123)` | `EvaluateQuadraticBezier(t, A, B, ComputeArchedControlPoint(A, B, h))` `==` biểu thức cũ của `DashPoint` chép trong test, so `==` từng thành phần |

Bốn case cuối thay ba case `DashPoint_*` của `TileSuckCurveTests` (GĐ2 xoá ở dự án).

**Mốc commit:** `feat(bezier): ComputeArchedControlPoint` trong `Assets/Horcrux`, kèm con trỏ submodule ở dự án.

## Giai đoạn 2 — dự án dùng bản Horcrux (agent)

**Mục tiêu:** cú hút gọi thẳng Horcrux, `DashPoint` biến mất. **Vì sao bây giờ:** GĐ1 compile và test xanh.

`Entities/Tile/Tile.cs` › `StepSuckDash`:

```csharp
            // Đích đọc lại mỗi frame như StepFlight.
            transform.position = TileSuckCurve.DashPoint(_suckState.Back, _slot.transform.position, suck.ArcHeight, k);
```

→

```csharp
            // Đích đọc lại mỗi frame như StepFlight.
            var target = _slot.transform.position;
            var control = BezierCurveHelper.ComputeArchedControlPoint(_suckState.Back, target, suck.ArcHeight);
            transform.position = BezierCurveHelper.EvaluateQuadraticBezier(k, _suckState.Back, target, control);
```

Thêm `using Horcrux.Runtime.Utilities.PhysXHelper;` vào khối `using Horcrux…` đầu file (hiện chưa có).

| File | Đổi |
|---|---|
| `Entities/Tile/TileSuckCurve.cs` | xoá `DashPoint`; summary class: "lao theo cung Bezier bậc hai vào miệng" giữ, thêm "cung dựng bằng `BezierCurveHelper.ComputeArchedControlPoint`"; bỏ `using Horcrux.Runtime.Utilities.PhysXHelper;` nếu không còn dùng |
| `Tests/EditMode/TileSuckCurveTests.cs` | xoá ba case `DashPoint_StartsAndEndsOnEndpoints`, `DashPoint_MiddleRisesHalfArcAboveChord`, `DashPoint_ZeroArc_IsStraightLine` (GĐ1 đã phủ) |
| `Horcrux/Plans/Horcrux_HopCurve_DipFlash_Plan.md` GĐ10 | dòng "`WindupBack`, `DashPoint`, `LeanHold` ở lại" bỏ `DashPoint` |

**Kiểm:** grep `DashPoint` trong `Assets/_CategoryJam` = 0; compile sạch; `CategoryJam.Tests.TileSuckCurveTests` và
`Horcrux.Tests.BezierCurveHelperTests` xanh. Cảm giác không đổi: case bằng nhau tuyệt đối của GĐ1 chứng minh từng điểm của cung trùng bản
cũ, không cần trace. **Không cần chơi thử.**

**Mốc commit:** `refactor(tile): cú hút dùng BezierCurveHelper.ComputeArchedControlPoint` ở dự án, kèm con trỏ submodule.

## Giai đoạn 3 — tài liệu (agent)

`docs/Runtime/CharmFlyArc/CharmFlyArc.md` (tài liệu đang giữ `BezierCurveHelper`):
- §1, sau đoạn "Đường bay là bezier bậc hai": một đoạn `ComputeArchedControlPoint` — trung điểm nhấc `height` theo +Y, đỉnh `h/2`, dùng khi
  vật bay cả hai chiều và đích đọc lại mỗi frame; caller ghép với `EvaluateQuadraticBezier`.
- §4 Bẫy, hàng "Một `controlOffset` dùng cho cả bay sang trái lẫn sang phải": thêm "cần vồng một phía thì `ComputeArchedControlPoint`".
- Chưa có `.html` cho tài liệu này; không tạo trong Plan này.

Xong thì **xoá Plan này** (`Horcrux_ArchedControlPoint_Plan.md` cùng `.meta`).

**Mốc commit:** `docs(bezier): ComputeArchedControlPoint trong CharmFlyArc.md`, xoá Plan, kèm con trỏ submodule.

## Use case

Tile bị hút vào bình đứng bên phải (category-jam) · tiền bay từ hai mép màn vào ví trên HUD · lá bài bay từ tay trái và tay phải vào chồng
giữa bàn — cả ba cần cung vồng lên dù xuất phát bên nào.

## File → giai đoạn

| File | GĐ |
|---|---|
| `Runtime/Utilities/PhysXHelper/BezierCurveHelper.cs` | 1 |
| `Tests/EditMode/BezierCurveHelperTests.cs` (agent) | 1 |
| dự án: `Tile.cs`, `TileSuckCurve.cs`, `TileSuckCurveTests.cs`; Horcrux `Plans/Horcrux_HopCurve_DipFlash_Plan.md` | 2 |
| `docs/Runtime/CharmFlyArc/CharmFlyArc.md` | 3 |
