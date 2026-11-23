# Plan — `BezierCurveHelper.ComputeArchedControlPoint`: cung Bezier bậc hai luôn vồng về +Y

Bản tạm: category-jam `Scripts/Entities/Tile/TileSuckCurve.cs` › `DashPoint` (caller `Tile.StepSuckDash`). Không phụ thuộc Plan nào. Chung file: `Tile.cs`, `TileSuckCurve.cs`, `TileSuckCurveTests.cs` với `Horcrux_HopCurve_DipFlash_Plan.md` › Chuyển dự án (thứ tự nào cũng compile, gợi ý Plan này trước); `docs/Runtime/CharmFlyArc/CharmFlyArc.md` với `Horcrux_CharmFlyArcFollow_Plan.md` — gõ sau Plan đó.
Kiểm: `bash "Assets/Horcrux/Editor/CompileCheck/Tools~/verify.sh" --csproj com.horcrux.runtime.csproj --tests Horcrux.EditModeTests`; chuyển dự án `--csproj CategoryJam.Gameplay.csproj,com.horcrux.runtime.csproj --tests CategoryJam.EditModeTests,Horcrux.EditModeTests`.

## Kịch bản

| Tình huống | Hệ làm gì | Caller thấy |
|---|---|---|
| vật bay sang phải vào đích, `controlLift` > 0 | trung điểm dây cung nhấc `controlLift` theo +Y | cung vồng lên, đỉnh cao `controlLift / 2` trên dây cung |
| vật bay sang trái, cùng `controlLift` | như trên — không dùng pháp tuyến dây cung | vẫn vồng lên (`ComputeControlPoint` có sẵn ở ca này võng xuống) |
| đích đang nhún, trượt; caller đọc lại mỗi frame | tính lại điểm điều khiển từ đích mới | cung bám đích, không giật |
| `controlLift` = 0 | điểm điều khiển là trung điểm | đường thẳng đi đều |
| `controlLift` < 0 | nhấc âm | cung võng xuống |
| hai đầu lệch độ cao | trung điểm nằm giữa hai đầu, rồi nhấc | cung vẫn đối xứng quanh dây cung nghiêng |

## Flow

```
caller (from cố định; mỗi frame: k = ease(v), target đọc lại)              Tile.StepSuckDash ở dự án
  ├─ ComputeArchedControlPoint(from, target, controlLift) ──control──┐     BezierCurveHelper.cs (mới)
  └─ EvaluateQuadraticBezier(k, from, target, control) ◄─────────────┘     BezierCurveHelper.cs (có sẵn)
        └──► position
ComputeControlPoint (pháp tuyến dây cung) ◄── CharmFlyArc — không đổi        BezierCurveHelper.cs
```

## Thuật toán

`C = M + h·ŷ`, `M = (A + B)/2`:
`B(t) = (1−t)²A + 2t(1−t)C + t²B = [(1−t)²A + 2t(1−t)M + t²B] + 2t(1−t)·h·ŷ = lerp(A, B, t) + 2t(1−t)·h·ŷ`.

| Bất biến | Vì |
|---|---|
| cung = dây cung + `2t(1−t)·h` theo +Y | Bezier có điểm điều khiển ở trung điểm là đường thẳng đi đều |
| đỉnh ở `t = 0.5`, cao `h/2` | `2·0.25·h` |
| phía vồng không phụ thuộc chiều bay | pháp tuyến không xuất hiện |

Bản mẫu: bảng case tính tay từng dòng · thân chép nguyên văn biểu thức `DashPoint` đang chạy.

---

## GĐ1 — xong thì caller nào cũng dựng được cung vồng lên ở cả hai chiều bay bằng một lời gọi

```
caller ──mỗi frame──► ComputeArchedControlPoint+ ──control──► EvaluateQuadraticBezier ──► position
```

`Runtime/Utilities/PhysXHelper/BezierCurveHelper.cs` › sau `ComputeControlPoint`, trước `ApproximateQuadraticBezierLength`. Có sẵn quanh chỗ thêm:

```csharp
            return Vector3.Lerp(from, target, controlAtRatio) + perpendicularDirection * controlOffset;
        }

        public static float ApproximateQuadraticBezierLength(Vector3 from, Vector3 target, Vector3 controlPoint)
```

Chèn giữa `}` và `public static float ApproximateQuadraticBezierLength`:

```csharp

        /// <summary>
        /// Trung điểm dây cung nhấc controlLift theo +Y thế giới: cung vồng lên dù bay sang trái hay phải.
        /// Đỉnh cung cao controlLift / 2 trên trung điểm.
        /// </summary>
        // Nhận độ nhấc điểm điều khiển, không độ cao đỉnh: giữ nghĩa núm đã chỉnh trên prefab bản tạm.
        // Không Lerp(from, target, 0.5f): làm tròn float khác, mất bằng nhau tuyệt đối với bản tạm.
        public static Vector3 ComputeArchedControlPoint(Vector3 from, Vector3 target, float controlLift)
            => (from + target) * 0.5f + Vector3.up * controlLift;
```

`Tests/EditMode/BezierCurveHelperTests.cs` (agent, namespace `Horcrux.Tests`, sai số `1e-5`):

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

---

## Chuyển dự án (agent)

| Chỗ | Đổi |
|---|---|
| `docs/Runtime/CharmFlyArc/CharmFlyArc.md` §1 | sau đoạn "Đường bay là bezier bậc hai" (và sau bảng hai overload nếu đã có): đoạn `ComputeArchedControlPoint` — nhấc `controlLift` theo +Y, đỉnh `controlLift / 2`, dùng khi bay cả hai chiều và đích đọc lại mỗi frame, ghép với `EvaluateQuadraticBezier`; trung tính, không tên dự án |
| `CharmFlyArc.md` §4 Bẫy, hàng "Một `controlOffset` dùng cho cả bay sang trái lẫn sang phải" | thêm "cần vồng một phía thì `ComputeArchedControlPoint`" |
| `CharmFlyArc.html` | đã có thì sửa cùng hai chỗ; chưa có thì không tạo ở Plan này |
| `Tile.cs` › `StepSuckDash` | `TileSuckCurve.DashPoint(_suckState.Back, _slot.transform.position, suck.ArcHeight, k)` → `target` đọc một lần, `ComputeArchedControlPoint(_suckState.Back, target, suck.ArcHeight)` rồi `EvaluateQuadraticBezier(k, _suckState.Back, target, control)`; giữ comment "Đích đọc lại mỗi frame như StepFlight."; thêm `using Horcrux.Runtime.Utilities.PhysXHelper;` nếu chưa có |
| `TileSuckCurve.cs` | xoá `DashPoint` và `using Horcrux.Runtime.Utilities.PhysXHelper;`; summary class thêm "cung dựng bằng `BezierCurveHelper.ComputeArchedControlPoint`" |
| `Tests/EditMode/TileSuckCurveTests.cs` | xoá ba case `DashPoint_*` (GĐ1 đã phủ); grep `DashPoint` trong `Assets/_CategoryJam` = 0 |
| Plan này | xoá cùng `.meta` |
| chơi thử | level có booster Pack → bấm Pack khi thanh cache có tile → tile lùi lấy đà, lao theo cung vồng lên vào miệng bình, không khác trước; hỏng: võng xuống, đi thẳng, giật khi bình nhún |

## Không làm · Mở rộng sau

| | Phương án | Vì sao |
|---|---|---|
| bỏ | tham số `controlAtRatio` | người dùng duy nhất đặt đỉnh ở giữa; thêm thì phải sang `Lerp`, mất bằng nhau tuyệt đối với bản tạm |
| bỏ | tham số trục `up` | người dùng duy nhất truyền `Vector3.up`; thế giới 2D nằm trên mặt XY như `ComputeControlPoint` |
| bỏ | hàm gộp "điểm trên cung vồng" | caller ghép hai hàm, mỗi frame đọc lại đích |
| bỏ | đặt trong `HopCurve` | cùng họ `ComputeControlPoint`, caller tìm điểm điều khiển ở một chỗ |
| bỏ | `MethodImpl(AggressiveInlining)` | một phép cộng, JIT tự inline |
| bỏ | `JumpArc.Evaluate` | parabol theo y bù chênh độ cao hai đầu; khác hình Bezier khi hai đầu lệch cao |
| bỏ | chuyển case hai đầu mút của `DashPoint` sang test mới | hai đầu Bezier không phụ thuộc điểm điều khiển, hàm sai vẫn xanh; case bằng nhau tuyệt đối đã phủ |
| sau | `ArcFlightSpec` thêm `bool archedUp` (mặc định `false`) | khi `CharmFlyArc` có caller bay hai chiều; key YAML mới không phá asset cũ |
| sau | tham số `controlAtRatio` | khi có caller đặt đỉnh lệch giữa |
