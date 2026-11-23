# Plan — `BezierCurveHelper.ComputeArchedControlPoint`: cung Bezier bậc hai luôn vồng về +Y

Ngày 2026-11-23. Nguồn: category-jam `Assets/_CategoryJam/Scripts/Entities/Tile/TileSuckCurve.cs` (`DashPoint`, call site
`Tile.StepSuckDash`), đã chơi thử. Một hàm static thêm vào class có sẵn: điểm điều khiển Bezier bậc hai là trung điểm dây cung nhấc theo
trục lên của thế giới, nên cung vồng lên dù vật bay sang trái hay sang phải.

**Phân công:** GĐ1 developer gõ, agent viết test theo bảng case rồi compile · GĐ2 (tài liệu), GĐ3 (dự án chuyển sang bản Horcrux, xoá
bản tạm, xoá Plan) agent làm.
**Chạy được, thấy được:** GĐ1 compile sạch + bảng case xanh trong `Tests/EditMode/BezierCurveHelperTests.cs`; thấy trong game ở GĐ3.
**Lệnh kiểm** (chạy từ gốc dự án): GĐ1 `bash "Assets/Horcrux/Editor/CompileCheck/Tools~/verify.sh" --csproj com.horcrux.runtime.csproj
--tests Horcrux.EditModeTests`; GĐ3 `… --csproj CategoryJam.Gameplay.csproj,com.horcrux.runtime.csproj --tests
CategoryJam.EditModeTests,Horcrux.EditModeTests`.
**Commit:** GĐ1, GĐ2 trong `Assets/Horcrux`; GĐ3 ở dự án kèm con trỏ submodule.
**Phụ thuộc:** không.
**Chung file:** `Tile.cs` (`using Horcrux.Runtime.Utilities.PhysXHelper;`), `TileSuckCurve.cs`, `TileSuckCurveTests.cs` —
`Horcrux_HopCurve_DipFlash_Plan.md` GĐ10 xoá `DashDuration` cùng file; thứ tự nào cũng compile sạch, gợi ý GĐ3 ở đây trước HopCurve
GĐ10. `docs/Runtime/CharmFlyArc/CharmFlyArc.md` — `Horcrux_CharmFlyArcFollow_Plan.md` GĐ2 sửa §1 và tạo `CharmFlyArc.html`; gõ GĐ2 ở
đây sau Plan đó để sửa `.md` và `.html` một lần.
**Đã kiểm trước khi ghi:** csc Roslyn (Rider 2026.2, `-langversion:9 -t:library`) trên `UnityEngine.CoreModule.dll` 6000.0.73f1 +
`netstandard.dll` 2.1: `BezierCurveHelper.cs` hiện có cộng hàm GĐ1, sạch. Bảng case GĐ1 tính tay từng dòng. Thân hàm chép nguyên văn
biểu thức `DashPoint` đang chạy trong dự án. `§0`: không — công thức một dòng, phép dẫn nằm ở GĐ1.

## Ngữ cảnh đã chốt

- **Người dùng:** vật lao vào một đích theo cung, đích đọc lại mỗi frame và có thể nằm bên trái hay bên phải chỗ xuất phát — vật bị hút
  vào hộp, tiền bay từ hai mép màn vào ví HUD, lá bài bay từ tay trái và tay phải vào chồng giữa bàn.
- **Mục tiêu:** cung vồng về **một phía cố định của thế giới** (+Y) bất kể chiều bay. `ComputeControlPoint` có sẵn lệch theo pháp tuyến
  `(−dy, dx)` của dây cung: cùng `controlOffset` dương, bay sang phải thì vồng lên, bay sang trái thì võng xuống.
- **Ranh giới:** chỉ thêm một hàm; `ComputeControlPoint`, `EvaluateQuadraticBezier` giữ nguyên. Caller ghép điểm điều khiển với
  `EvaluateQuadraticBezier` và tự giữ vòng Step.
- **Chỗ đặt, tên:** `Runtime/Utilities/PhysXHelper/BezierCurveHelper.cs`, cạnh `ComputeControlPoint`, namespace
  `Horcrux.Runtime.Utilities.PhysXHelper` — thư mục của các hàm toán thuần có sẵn. "Arched" nói hình dạng (vòm). Tham số
  `controlLift`: độ nhấc của điểm điều khiển, không phải độ cao đỉnh.
- **Cố ý KHÔNG làm, kèm lý do:**
  - Tham số `controlAtRatio`: người dùng duy nhất đặt đỉnh ở giữa; thêm thì biểu thức phải đổi sang `Lerp`, mất phép bằng nhau tuyệt
    đối với bản tạm.
  - Tham số trục `up`: người dùng duy nhất truyền `Vector3.up`.
  - Lựa chọn vồng theo trục trong `ArcFlightSpec`/`CharmFlyArc`: chưa có caller của `CharmFlyArc` cần vồng một phía (Đã khảo sát).
  - Hàm gộp "điểm trên cung vồng": caller ghép `ComputeArchedControlPoint` với `EvaluateQuadraticBezier`, mỗi frame đọc lại đích.
- **Quyết định trái trực giác:** đỉnh thật của cung chỉ cao **nửa** `controlLift` so với trung điểm dây cung. Nhận độ nhấc của điểm
  điều khiển, không nhận độ cao đỉnh, để giữ nguyên nghĩa của núm đã chỉnh trên prefab bản tạm; tên tham số nói đúng nghĩa đó.
- **Mở rộng sau:** `ArcFlightSpec` thêm `bool archedUp` (mặc định `false`, key YAML mới không phá asset cũ) khi `CharmFlyArc` có caller
  bay hai chiều · tham số `controlAtRatio` khi có caller đặt đỉnh lệch giữa.

## Đã khảo sát

| Nguồn | Lấy gì | Không lấy, vì sao |
|---|---|---|
| category-jam `TileSuckCurve.DashPoint` (bản tạm) | biểu thức điểm điều khiển `(start + end) * 0.5f + Vector3.up * arcHeight`, nguyên văn | tên, chữ ký gộp `k` — caller ghép hai hàm Horcrux |
| `BezierCurveHelper.EvaluateQuadraticBezier` | dùng nguyên; `DashPoint` gọi đúng hàm này | — |
| `BezierCurveHelper.ComputeControlPoint` | đặt hàm mới cạnh nó, cùng kiểu chữ ký | lệch theo pháp tuyến dây cung: phía vồng đổi theo chiều bay |
| `BezierCurveHelper.ApproximateQuadraticBezierLength` | — | người dùng tính thời lượng theo dây cung (quãng / tốc độ), không theo độ dài cung |
| `JumpArc.Evaluate` | — | parabol theo y trên đường thẳng, đỉnh bù chênh độ cao hai đầu; khác hình Bezier khi hai đầu lệch độ cao |
| `CharmFlyArc`, `ArcFlightSpec` | — | `Tween` tự chạy chỉ ghi vị trí; người dùng ghi vị trí, cỡ, nghiêng cùng một `k` trong `Sequence` của mình. category-jam: trước `Horcrux_CharmFlyArcFollow_Plan.md` GĐ3 `CharmFlyArc` không có caller; sau đó caller là `BoosterActor` (overload `Transform`), chưa cần đổi phía vồng |
| `Horcrux_CharmFlyArcFollow_Plan.md` | — | overload đích động tính lại `ComputeControlPoint` mỗi frame; cần vồng một phía thì đó là chỗ của "Mở rộng sau: `archedUp`" |
| `Horcrux_HopCurve_DipFlash_Plan.md` (`ArcY`) | — | cung một chiều theo y, hai pha lên rồi rơi, không phải Bezier hai chiều |

## Luồng

```
caller dựng chuyến: from = chỗ xuất phát
mỗi frame (v tuyến tính):
    k       = ease(v)
    target  = đích đọc lại                                            (đích có thể đang nhún, trượt)
    control = BezierCurveHelper.ComputeArchedControlPoint(from, target, controlLift)     GĐ1
    pos     = BezierCurveHelper.EvaluateQuadraticBezier(k, from, target, control)        có sẵn
```

## Bản đồ hệ cuối

| Thành phần | Ai gọi | Việc |
|---|---|---|
| `BezierCurveHelper.ComputeArchedControlPoint` (mới) | caller mỗi frame | trung điểm dây cung nhấc `controlLift` theo +Y |
| `BezierCurveHelper.EvaluateQuadraticBezier` (có sẵn) | caller mỗi frame | điểm trên cung tại `k` |
| `BezierCurveHelper.ComputeControlPoint` (có sẵn) | `CharmFlyArc` | điểm điều khiển lệch theo pháp tuyến dây cung — không đổi |

## Giai đoạn 1 — `ComputeArchedControlPoint`

**Mục tiêu:** Horcrux dựng được cung vồng lên ở cả hai chiều bay. Xong thì caller nào cũng thay được biểu thức tự viết bằng một lời gọi.
**Vì sao bây giờ:** là toàn bộ phần Horcrux; GĐ2, GĐ3 cần nó compile.

**Ai gọi:** test của giai đoạn; trong dự án `Tile.StepSuckDash` (GĐ3).

**Sơ đồ sau GĐ1**

```
caller ──mỗi frame──► ComputeArchedControlPoint+ ──control──► EvaluateQuadraticBezier ──► position
```

### `Runtime/Utilities/PhysXHelper/BezierCurveHelper.cs` › sau `ComputeControlPoint`, trước `ApproximateQuadraticBezierLength`

Có sẵn quanh chỗ thêm:

```csharp
            return Vector3.Lerp(from, target, controlAtRatio) + perpendicularDirection * controlOffset;
        }

        public static float ApproximateQuadraticBezierLength(Vector3 from, Vector3 target, Vector3 controlPoint)
```

Đoạn mới, chèn giữa `}` và `public static float ApproximateQuadraticBezierLength`:

```csharp

        /// <summary>
        /// Trung điểm dây cung nhấc controlLift theo +Y thế giới: cung vồng lên dù bay sang trái hay phải.
        /// Đỉnh cung cao controlLift / 2 trên trung điểm.
        /// </summary>
        public static Vector3 ComputeArchedControlPoint(Vector3 from, Vector3 target, float controlLift)
            => (from + target) * 0.5f + Vector3.up * controlLift;
```

**Toán → code.** Với điểm điều khiển `C = M + h·ŷ`, `M = (A + B)/2`:
`B(t) = (1−t)²A + 2t(1−t)C + t²B = [(1−t)²A + 2t(1−t)M + t²B] + 2t(1−t)·h·ŷ`. Ngoặc vuông là `lerp(A, B, t)` (Bezier có điểm điều khiển
nằm giữa dây cung là đường thẳng đi đều), nên cung = dây cung + `2t(1−t)·h` theo +Y. Đỉnh ở `t = 0.5`: `2·0.25·h = h/2`. Pháp tuyến không
xuất hiện, nên chiều bay không đổi phía vồng.

| Consumes | Produces |
|---|---|
| — | `BezierCurveHelper.ComputeArchedControlPoint(from, target, controlLift)` |

**Quyết định, phương án bỏ, vì sao:**
- Đặt trong `BezierCurveHelper`, không trong `HopCurve` (Plan khác): cùng họ với `ComputeControlPoint`, caller tìm điểm điều khiển ở một
  chỗ.
- `(from + target) * 0.5f`, không `Vector3.Lerp(from, target, 0.5f)`: hai cách làm tròn float khác nhau; biểu thức của bản tạm giữ cảm
  giác không đổi một giá trị.
- Expression-bodied, không `MethodImpl(AggressiveInlining)`: một phép cộng, JIT tự inline.
- Trục cố định `Vector3.up`: thế giới 2D của Horcrux nằm trên mặt XY như `ComputeControlPoint`.

**Editor setup:** không.

**Kiểm:** compile sạch; test EditMode `Tests/EditMode/BezierCurveHelperTests.cs` (namespace `Horcrux.Tests`, agent viết sau khi đọc code
thật, sai số `1e-5`):

| Input | Kỳ vọng |
|---|---|
| `ComputeArchedControlPoint((0,0,0), (4,0,0), 0.6)` | `(2, 0.6, 0)` |
| `ComputeArchedControlPoint((4,0,0), (0,0,0), 0.6)` | `(2, 0.6, 0)` — bay sang trái vẫn vồng lên |
| `ComputeControlPoint((4,0,0), (0,0,0), 0.6)` (đối chứng) | `(2, −0.6, 0)` — bản pháp tuyến võng xuống; đỏ nếu ai đổi `ComputeControlPoint` sang trục cố định |
| `ComputeArchedControlPoint((0,0,0), (4,2,0), 0)` | `(2, 1, 0)` — trung điểm |
| `ComputeArchedControlPoint((0,0,0), (4,0,0), −0.6)` | `(2, −0.6, 0)` — `controlLift` âm võng xuống |
| `EvaluateQuadraticBezier(0.5, (0,0,0), (4,0,0), ComputeArchedControlPoint(…, 0.6))` | `(2, 0.3, 0)` — đỉnh cao `controlLift / 2` |
| `EvaluateQuadraticBezier(0.25, (0,0,0), (4,2,0), ComputeArchedControlPoint(…, 0))` | `(1, 0.5, 0)` — `controlLift` 0 là đường thẳng |
| bằng nhau tuyệt đối: 1000 bộ `(A, B, h, t)` từ `System.Random(20261123)` | `EvaluateQuadraticBezier(t, A, B, ComputeArchedControlPoint(A, B, h))` `==` `EvaluateQuadraticBezier(t, A, B, (A + B) * 0.5f + Vector3.up * h)` chép trong test, so `==` từng thành phần; đỏ nếu thân đổi sang `Lerp` |

Ba case cuối thay ba case `DashPoint_*` của bản tạm (GĐ3 xoá). Case hai đầu mút của bản tạm không chuyển: hai đầu Bezier không phụ thuộc
điểm điều khiển nên hàm mới sai vẫn xanh; case bằng nhau tuyệt đối đã phủ.

**Mốc commit:** `feat(bezier): ComputeArchedControlPoint` trong `Assets/Horcrux`, kèm con trỏ submodule ở dự án.

## Giai đoạn 2 — Tài liệu (agent)

**Mục tiêu:** người đọc tài liệu `BezierCurveHelper` biết khi nào dùng điểm điều khiển vồng một phía. **Vì sao bây giờ:** API đã chốt ở
GĐ1.

`docs/Runtime/CharmFlyArc/CharmFlyArc.md` (tài liệu đang giữ `BezierCurveHelper`):
- §1, sau đoạn "Đường bay là bezier bậc hai" (và sau bảng hai overload nếu `Horcrux_CharmFlyArcFollow_Plan.md` GĐ2 đã thêm): một đoạn
  `ComputeArchedControlPoint` — trung điểm nhấc `controlLift` theo +Y, đỉnh `controlLift / 2`, dùng khi vật bay cả hai chiều và đích đọc
  lại mỗi frame; caller ghép với `EvaluateQuadraticBezier`.
- §4 Bẫy, hàng "Một `controlOffset` dùng cho cả bay sang trái lẫn sang phải": thêm "cần vồng một phía thì `ComputeArchedControlPoint`".
- `CharmFlyArc.html` hiện không có (`Horcrux_CharmFlyArcFollow_Plan.md` GĐ2 tạo). Đã có thì sửa cùng hai chỗ trong `.html`; chưa có thì
  không tạo trong Plan này.

Tài liệu trung tính: không tên type, kịch bản, con số của dự án nào.

**Kiểm:** đọc lại hai chỗ sửa; mọi tên member trỏ đúng code GĐ1.

**Mốc commit:** `docs(bezier): ComputeArchedControlPoint trong CharmFlyArc` trong `Assets/Horcrux`, kèm con trỏ submodule.

## Giai đoạn 3 — category-jam dùng bản Horcrux, xoá bản tạm, xoá Plan (agent)

**Mục tiêu:** cú hút vào bình Pack gọi thẳng Horcrux, `DashPoint` biến mất. **Vì sao bây giờ:** GĐ1 compile và test xanh.

`Assets/_CategoryJam/Scripts/Entities/Tile/Tile.cs` › `StepSuckDash`:

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

Thêm `using Horcrux.Runtime.Utilities.PhysXHelper;` vào khối `using Horcrux…` đầu file nếu chưa có (HopCurve GĐ10 cũng thêm dòng này).

| File | Đổi |
|---|---|
| `Entities/Tile/TileSuckCurve.cs` | xoá `DashPoint`; summary class: giữ "lao theo cung Bezier bậc hai vào miệng", thêm "cung dựng bằng `BezierCurveHelper.ComputeArchedControlPoint`"; bỏ `using Horcrux.Runtime.Utilities.PhysXHelper;` (sau khi xoá `DashPoint` file không còn gọi Horcrux) |
| `Tests/EditMode/TileSuckCurveTests.cs` | xoá ba case `DashPoint_StartsAndEndsOnEndpoints`, `DashPoint_MiddleRisesHalfArcAboveChord`, `DashPoint_ZeroArc_IsStraightLine` (GĐ1 đã phủ) |
| `Horcrux/Plans/Horcrux_ArchedControlPoint_Plan.md` (+ `.meta`) | xoá |

**Kiểm:** grep `DashPoint` trong `Assets/_CategoryJam` = 0; lệnh kiểm GĐ3 ở đầu Plan xanh (`TileSuckCurveTests`,
`BezierCurveHelperTests`). Cảm giác không đổi: case bằng nhau tuyệt đối của GĐ1 chứng minh từng điểm của cung trùng bản tạm.

**Chơi thử** (developer, không bắt buộc vì đã có phép bằng nhau tuyệt đối):

| Vào đâu | Làm gì | Nhìn gì | Khác trước | Dấu hiệu hỏng |
|---|---|---|---|---|
| level có booster Pack | bấm Pack khi thanh cache có tile | tile lùi lấy đà rồi lao theo cung vồng lên vào miệng bình, chìm vào | không khác | tile võng xuống dưới dây cung, đi thẳng, hay giật khi bình nhún |

**Mốc commit:** `refactor(tile): cú hút dùng BezierCurveHelper.ComputeArchedControlPoint` ở dự án; xoá Plan trong `Assets/Horcrux`;
kèm con trỏ submodule.

## File → GĐ

| File | GĐ |
|---|---|
| `Runtime/Utilities/PhysXHelper/BezierCurveHelper.cs` | 1 |
| `Tests/EditMode/BezierCurveHelperTests.cs` (agent) | 1 |
| `docs/Runtime/CharmFlyArc/CharmFlyArc.md` (+ `.html` nếu đã có) | 2 |
| dự án: `Tile.cs`, `TileSuckCurve.cs`, `TileSuckCurveTests.cs`; Horcrux: xoá Plan này | 3 |
