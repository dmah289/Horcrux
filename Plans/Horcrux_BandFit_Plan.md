# Plan — `BandFit` lên Horcrux

Ngày 2026-11-02. Nguồn: category-jam, `Assets/_CategoryJam/Scripts/Director/LevelBoundsFitter.cs` (`FitScale`, `ContainerPosition`)
và test `Tests/EditMode/LevelBoundsFitterTests.cs` (ba case vị trí). Hai hàm thuần tính scale đồng nhất để một `Bounds` lọt bề rộng
màn và một dải cao tuỳ chọn, chừa lề, kẹp dưới, rồi treo đỉnh bounds lên đỉnh dải. Phần tính bounds từ data level
(`TryGetLevelLocalBounds`, hai cỡ kệ) bám schema dự án, ở lại.

**Phân công:** Task 1 (Horcrux, code runtime) developer gõ · Task 2 (tài liệu) và Task 3 (test EditMode) agent viết sau khi code có.
Dự án chuyển `LevelBoundsFitter` sang gọi `BandFit` là việc riêng khi developer yêu cầu.

Horcrux là submodule: Task 1–3 commit **trong `Assets/Horcrux`**.

---

## Ngữ cảnh đã chốt

| Mục | Chốt |
|---|---|
| Người dùng | mọi game đặt bàn chơi world vào dải giữa HUD trên và HUD dưới, bề rộng màn cố định theo `ViewportCamera` |
| Mục tiêu | hai hàm thuần, không chạm Unity object ngoài `Bounds`, `Vector2`: test không dựng gì |
| Chỗ đặt | `Runtime/Utilities/Layout/BandFit.cs`, namespace `Horcrux.Runtime.Utilities.Layout` |
| Tên | `BandFit` (fit vào một dải). `FitScale`, `ContainerPosition` giữ. Developer đổi tuỳ ý trước khi gõ |
| Tham số | ba hằng của dự án (`0.743`, `0.37`, `0.4`) thành tham số: `preferredScale`, `minScale`, `margin` — mỗi game một bộ, không có mặc định đúng cho mọi game |

**Cố ý KHÔNG làm**

| Không làm | Vì |
|---|---|
| Struct `BandFitSpec` gom tham số | hai hàm, ba số; call site đọc được theo tên tham số. Có game thứ hai truyền từ Inspector thì gom thành struct `[Serializable]` lúc đó, overload thêm, hàm cũ giữ |
| Lề riêng cho ngang và dọc | một lề là đủ ở người dùng đầu; thêm là tham số tuỳ chọn |
| Căn giữa dải thay vì treo đỉnh | luật "vùng co cho vừa một dải kề một vật ở mép dải: neo theo mép kề vật" — bàn chơi kề thanh khay ở đỉnh dải |
| Tính bounds từ renderer | bounds của người dùng đầu tính từ data level trước khi spawn; game khác có cách khác, đưa `Bounds` vào là ranh giới đúng |

**Quyết định trái trực giác**

| Chỗ | Vì |
|---|---|
| `bandHeight` là `float?` | không giới hạn chiều cao là ca thật (màn không có HUD dưới); `null` nói đúng "không có dải" thay vì số lớn giả |
| Ngưỡng `bounds.size > 0.01f` | bounds một điểm (level một ô, hay data rỗng) không được chia cho 0 ra vô cực |
| Kẹp `minScale` **sau** cả hai chiều | level quá to vẫn giữ cỡ tối thiểu đọc được, chấp nhận tràn: tràn nhìn thấy, tile tí hon thì không chơi được |
| Vị trí y treo đỉnh bounds một lề dưới `bandTopY` | level thấp hơn dải đứng sát vật ở đỉnh dải; level cao đúng dải (co theo chiều cao) tự động cách đáy một lề |

---

## Đã khảo sát

| Nguồn | Lấy gì | Không lấy, vì sao |
|---|---|---|
| `LevelBoundsFitter.cs` | `FitScale`, `ContainerPosition` | `TryGetLevelLocalBounds`, hai `*_CELL_EXTENTS`, `IsConveyorCell`: schema level |
| Horcrux `ViewportAnchor` plan | cùng thư mục, cùng đơn vị world | không có hàm fit |
| Unity `AspectRatioFitter` | — | chỉ `RectTransform` |

---

## §0 — Công thức

Bounds `B` (đơn vị container ở scale 1), bề rộng màn `W`, dải cao `H` (tuỳ chọn), lề `m`, scale ưa thích `s₀`, scale tối thiểu `s_min`:

$$s = \max\big(s_{min},\ \min(s_0,\ \tfrac{W - 2m}{B_w},\ \tfrac{H - 2m}{B_h})\big)$$

(bỏ số hạng của chiều không giới hạn, có `B` ≈ 0, hoặc có `W − 2m` / `H − 2m` ≤ 0 — lề nuốt hết màn hay dải). Vị trí container để tâm ngang về 0 và đỉnh bounds cách `bandTopY` một lề:

$$x = -B_{cx}\,s,\qquad y = \text{bandTopY} - m - B_{max,y}\,s$$

| Mốc | Kỳ vọng | ✓ |
|---|---|---|
| `B` 18.4×13.6 tâm (−1, 0), `W` 10.5, `H` 10.65, `m` 0.4, `s₀` 0.743 | `s` = min(0.743, 9.7/18.4, 9.85/13.6) = 0.527 | co theo bề rộng |
| cùng `B`, `H` null | `s` = min(0.743, 0.527) = 0.527 | chiều cao không ràng |
| `B` 13.4×18.6, cùng dải | `s` = min(0.743, 0.724, 0.530) = 0.530 | co theo chiều cao; `y + B_min,y·s` = đáy dải + `m` |
| `B` 100×100, `s_min` 0.37 | `s` = 0.37 | kẹp |
| `B` cỡ 0 | `s` = `s₀` | không chia 0 |

---

## Task 1 — Horcrux (developer gõ)

### `Runtime/Utilities/Layout/BandFit.cs`

```csharp
using UnityEngine;

namespace Horcrux.Runtime.Utilities.Layout
{
    /// <summary>
    /// Scale đồng nhất để một bounds lọt bề rộng màn và một dải cao tuỳ chọn, chừa lề, kẹp dưới; và vị trí treo đỉnh bounds
    /// lên đỉnh dải.
    /// </summary>
    public static class BandFit
    {
        /// <summary>Bounds mỏng hơn ngưỡng này ở một chiều thì chiều đó không ràng scale.</summary>
        private const float MinSize = 0.01f;

        /// <param name="bandHeight">Chiều cao dải; null là không giới hạn chiều cao.</param>
        /// <param name="margin">Lề chừa mỗi bên, cùng đơn vị với bounds và màn.</param>
        public static float FitScale(Bounds bounds, float screenWidth, float? bandHeight, float margin, float preferredScale, float minScale)
        {
            var scale = preferredScale;

            var availableWidth = screenWidth - margin * 2f;
            if (bounds.size.x > MinSize && availableWidth > 0f)
                scale = Mathf.Min(scale, availableWidth / bounds.size.x);

            if (bandHeight.HasValue)
            {
                var availableHeight = bandHeight.Value - margin * 2f;
                if (bounds.size.y > MinSize && availableHeight > 0f)
                    scale = Mathf.Min(scale, availableHeight / bounds.size.y);
            }

            return Mathf.Max(scale, minScale);
        }

        /// <summary>
        /// Vị trí world của container (đã scale) để bounds căn giữa ngang và mép trên cách bandTopY một lề: bounds thấp hơn dải
        /// đứng sát đỉnh dải thay vì trôi ra giữa.
        /// </summary>
        public static Vector2 ContainerPosition(Bounds bounds, float scale, float bandTopY, float margin)
            => new(-bounds.center.x * scale, bandTopY - margin - bounds.max.y * scale);
    }
}
```

**Khác với bản dự án:** ba hằng thành tham số; `LEVEL_CONTAINER_SCALE`, `LEVEL_CONTAINER_MIN_SCALE`, `LEVEL_SCREEN_MARGIN` ở lại
`LevelBoundsFitter` của dự án và truyền vào. Công thức và thứ tự phép tính giữ nguyên từng dòng.

### Nghiệm thu Task 1

`com.horcrux.runtime.csproj` build sạch.

---

## Task 2 — Tài liệu `Runtime/Utilities/Layout/BandFit.md` (agent)

Mục §0 ở trên (công thức, bảng mốc) · hình ASCII dải giữa hai HUD với lề · "Design decisions" là bảng trên · "Dùng trong dự án":
cách người dùng đầu tính `bandTopY`, `bandHeight` từ hai `ViewportAnchor` và truyền ba hằng của nó.

## Task 3 — Test EditMode (agent)

`Tests/EditMode/BandFitTests.cs`, không dựng object; tham số `margin 0.4`, `preferredScale 0.743`, `minScale 0.37`, `W 10.5`,
dải `3.42 → −7.23`.

| Case | Input | Kỳ vọng |
|---|---|---|
| co theo bề rộng | `B` 18.4×13.6 | `s` 0.527 (±1e-3) |
| không ràng chiều cao | `bandHeight` null | cùng `s` |
| co theo chiều cao | `B` 13.4×18.6 | `s` 0.530; `y + B.min.y·s` = −7.23 + 0.4 (±1e-3) |
| kẹp dưới | `B` 100×100 | 0.37 |
| bounds mỏng | `B` 0×0 | `preferredScale` |
| lề lớn hơn màn | `B` 18.4×13.6, `W` 0.5, `m` 0.4, `bandHeight` null | `availableWidth` âm không ràng, không dải → `preferredScale` |
| treo đỉnh | `B` tâm (−1, 0) 18.4×13.6, `s` từ FitScale | `y + B.max.y·s` = 3.42 − 0.4 |
| căn giữa ngang | `s` 0.5 | `x + B.center.x·s` = 0 |
