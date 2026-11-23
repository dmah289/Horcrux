# Plan — `BandFit`: scale và vị trí để bounds bàn chơi lọt dải giữa hai HUD

Bản tạm: category-jam `Scripts/Director/LevelBoundsFitter.cs` › `FitScale`, `ContainerPosition`, gọi từ `LevelLoader.AdjustContainerScale`, `PositionShelfContainer`. Không phụ thuộc Plan nào; chung thư mục `Runtime/Utilities/Layout/` với `Horcrux_ViewportAnchor_Plan.md` — Plan gõ trước tạo thư mục.
Kiểm: `bash "<horcrux>/Editor/CompileCheck/Tools~/verify.sh" --csproj com.horcrux.runtime.csproj,Horcrux.EditModeTests.csproj --tests Horcrux.EditModeTests`; chuyển dự án `--csproj CategoryJam.Gameplay.csproj,com.horcrux.runtime.csproj --tests CategoryJam.EditModeTests,Horcrux.EditModeTests`.

## Kịch bản

| Tình huống | Hệ làm gì | Caller thấy |
|---|---|---|
| bàn chơi rộng hơn màn trừ lề | trần theo bề rộng thấp nhất | scale co, bàn chơi cách hai mép màn đúng lề |
| bàn chơi cao hơn dải trừ lề | trần theo chiều cao thấp nhất | scale co, mép trên cách đỉnh dải và mép dưới cách đáy dải đúng lề |
| bàn chơi nhỏ hơn cả màn lẫn dải | không trần nào thấp hơn `preferredScale` | scale = `preferredScale`, không phóng to |
| bàn chơi quá to | trần dưới `minScale`, kẹp sau cả hai chiều | scale = `minScale`, tràn nhìn thấy |
| không có dải (thiếu một anchor) | `bandHeight` null, bỏ ràng chiều cao | chỉ bề rộng ràng |
| bounds một điểm hay mỏng một chiều (≤ 0.01) | chiều đó không ràng | không chia 0 |
| lề nuốt hết màn hay dải | chiều đó không ràng | `preferredScale` hay trần chiều kia; tràn lộ cấu hình lề sai |
| `minScale > preferredScale` | kẹp dưới thắng | `minScale` — cấu hình lỗi của chủ |
| tâm bounds lệch (kệ Single một bên) | container dời ngược tâm đã scale | tâm bàn chơi ở `x = 0` và giữa dải |
| bàn chơi thấp hơn dải | căn tâm vào giữa dải | hở trên = hở dưới |
| dải truyền đảo thứ tự | trung bình hai mốc | cùng `y` |

## Flow

```
chủ bàn chơi (cùng frame dựng, trước lần vẽ đầu)
   │ bounds (scale 1), screenWidth, bandHeight?, margin, preferredScale, minScale
   ▼
BandFit.FitScale ──────────► scale ──► chủ gán localScale (s, s, 1)       Runtime/Utilities/Layout/BandFit.cs
   │ bounds, scale, bandTopY, bandBottomY
   ▼
BandFit.ContainerPosition ─► (x, y) ─► chủ gán position                    BandFit.cs
```

## Thuật toán

| Ký hiệu | Là gì |
|---|---|
| `B_w`, `B_h`, `B_cx`, `B_cy` | bề rộng, chiều cao, tâm của bounds ở scale 1 |
| `W` | bề rộng màn world (`orthographicSize × aspect × 2` với camera ortho) |
| `H` | chiều cao dải; không có dải thì bỏ số hạng của nó |
| `m` | lề mỗi bên, cùng đơn vị |
| `s₀`, `s_min` | scale ưa thích, scale tối thiểu |

$$s = \max\big(s_{min},\ \min(s_0,\ \tfrac{W - 2m}{B_w},\ \tfrac{H - 2m}{B_h})\big)$$

$$x = -B_{cx}\,s,\qquad y = \tfrac{\text{bandTopY} + \text{bandBottomY}}{2} - B_{cy}\,s$$

Bỏ số hạng khi: không có dải · chiều đó của `B` ≤ 0.01 · `W − 2m` hay `H − 2m` ≤ 0. Bất biến: ràng theo chiều cao thì `B_h·s = H − 2m`, hai đầu cách dải đúng `m`; thấp hơn dải thì hở mỗi đầu `(H − B_h·s)/2 > m`.

Mốc với `W` 10.5, dải `3.42 → −7.23` (`H` 10.65), `m` 0.4, `s₀` 0.743, `s_min` 0.37:

| Mốc | Kỳ vọng | ✓ |
|---|---|---|
| `B` 18.4×13.6 tâm (−1, 0.6) | `s` = min(0.743, 9.7/18.4, 9.85/13.6) = min(0.743, 0.527, 0.724) = 0.527 | co theo bề rộng |
| cùng `B`, `H` null | `s` = min(0.743, 0.527) = 0.527 | chiều cao không ràng |
| cùng `B`, vị trí | `y + 0.6·s` = −1.905 (giữa dải); hở trên = hở dưới = (10.65 − 13.6·0.527)/2 = 1.74 > `m` | level thấp hơn dải hở đều |
| `B` 13.4×18.6 tâm (0, −1) | `s` = min(0.743, 0.724, 0.530) = 0.530; `y` = −1.905 + 0.530 = −1.375; mép trên `y + 8.3·s` = 3.02 = 3.42 − `m`; mép dưới `y + (−10.3)·s` = −6.83 = −7.23 + `m` | co theo chiều cao, chạm hai lề |
| `B` 100×100 | min(…, 0.097) → kẹp 0.37 | kẹp dưới |
| `B` cỡ 0 | `s` = `s₀` | không chia 0 |
| `B` 6.4×3.6 (một kệ) | min(0.743, 1.52, 2.74) = 0.743 | không phóng to quá `s₀` |

---

## GĐ1 — xong thì chủ gán được `localScale` và bàn chơi không tràn màn

```
chủ ──bounds, screenWidth, bandHeight?, margin, preferredScale, minScale──► BandFit.FitScale+ ──scale──► chủ gán localScale
```

`Runtime/Utilities/Layout/BandFit.cs` — file mới:

```csharp
using UnityEngine;

namespace Horcrux.Runtime.Utilities.Layout
{
    /// <summary>
    /// Đặt một bounds vào dải giữa hai HUD: scale đồng nhất để bounds lọt bề rộng màn và dải cao tuỳ chọn, chừa lề, kẹp dưới.
    /// Toán thuần trên Bounds và số, không chạm Unity object.
    /// </summary>
    public static class BandFit
    {
        /// <summary>Bounds mỏng hơn ngưỡng này ở một chiều thì chiều đó không ràng scale: bounds một điểm không chia cho 0.</summary>
        private const float MinSize = 0.01f;

        /// <summary>
        /// Scale để <paramref name="bounds"/> lọt bề rộng màn <paramref name="screenWidth"/> và dải cao
        /// <paramref name="bandHeight"/> (null: không giới hạn chiều cao), mỗi chiều chừa lề hai bên; không quá
        /// <paramref name="preferredScale"/>, không dưới <paramref name="minScale"/>. Kẹp dưới sau cả hai chiều: bounds quá
        /// to vẫn giữ cỡ đọc được, chấp nhận tràn.
        /// </summary>
        /// <param name="margin">Lề chừa mỗi bên, cùng đơn vị với bounds và màn.</param>
        // bandHeight nullable, không float.MaxValue: số lớn giả vẫn đi qua phép trừ lề và phép chia.
        public static float FitScale(Bounds bounds, float screenWidth, float? bandHeight, float margin, float preferredScale, float minScale)
        {
            var scale = preferredScale;

            var availableWidth = screenWidth - margin * 2f;
            // Lề nuốt hết màn thì bỏ ràng, không trả minScale: tràn nhìn thấy, lộ cấu hình lề sai.
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
    }
}
```

`Tests/EditMode/BandFitTests.cs` (agent) — hằng `W 10.5`, dải `3.42 → −7.23`, `m 0.4`, `s₀ 0.743`, `s_min 0.37`; sai số `1e-3`:

| Case | Input | Kỳ vọng |
|---|---|---|
| co theo bề rộng | `B` 18.4×13.6 | 0.527 |
| không có dải | `bandHeight` null | 0.527 |
| co theo chiều cao | `B` 13.4×18.6 | 0.530 |
| nhỏ hơn cỡ ưa thích | `B` 6.4×3.6 | `preferredScale`: không phóng to |
| chạm đúng bề rộng | `B` rộng 9.7/0.743 | `preferredScale` (hai trần bằng nhau) |
| kẹp dưới | `B` 100×100 | 0.37 |
| bounds rỗng | `B` 0×0 | `preferredScale` |
| một chiều mỏng | `B` 0×18.6 | 0.530: chỉ chiều cao ràng |
| lề nuốt màn | `W` 0.5, `bandHeight` null | `preferredScale`: `availableWidth` âm không ràng |
| lề nuốt dải | `bandHeight` 0.6 | 0.527: chỉ bề rộng ràng |
| `minScale` trên `preferredScale` | `s_min` 0.8 | 0.8 — kẹp thắng |

---

## GĐ2 — xong thì bàn chơi tâm lệch vẫn đứng giữa màn và giữa hai HUD

```
chủ ──► BandFit.FitScale ──scale──► chủ ──bounds, scale, bandTopY, bandBottomY──► BandFit.ContainerPosition+ ──(x, y)──► chủ gán position
```

`BandFit.cs` › thêm sau `FitScale`, trước dấu đóng class:

```csharp
            return Mathf.Max(scale, minScale);
        }

        /// <summary>
        /// Vị trí world của container (đã scale <paramref name="scale"/>) để <paramref name="bounds"/> căn giữa ngang và giữa dải
        /// <paramref name="bandTopY"/>–<paramref name="bandBottomY"/>: bounds tâm lệch vẫn nằm giữa dải. Không có lề: lề đã
        /// vào scale, bounds cao đúng dải tự cách hai đầu một lề.
        /// </summary>
        // Hai mốc dải, không bandHeight hay tâm: cần biết dải ở đâu; chủ đang cầm sẵn hai anchor.
        public static Vector2 ContainerPosition(Bounds bounds, float scale, float bandTopY, float bandBottomY)
            => new(-bounds.center.x * scale, (bandTopY + bandBottomY) / 2f - bounds.center.y * scale);
```

`Tests/EditMode/BandFitTests.cs` (agent) — cùng hằng GĐ1:

| Case | Input | Kỳ vọng |
|---|---|---|
| tâm giữa dải | `B` 18.4×13.6 tâm (−1, 0.6), `s` từ `FitScale` | `y + 0.6·s` = (3.42 − 7.23)/2 |
| căn giữa ngang | cùng `B`, `s` 0.5 | `x + (−1)·0.5` = 0 |
| level cao đúng dải chạm hai lề | `B` 13.4×18.6 tâm (0, −1), `s` từ `FitScale` | `y + B.max.y·s` = 3.42 − 0.4; `y + B.min.y·s` = −7.23 + 0.4 |
| level thấp hơn dải hở đều | `B` 18.4×13.6, `s` từ `FitScale` | hở trên = hở dưới = (10.65 − 13.6·s)/2 |
| dải đảo | `bandTopY` −7.23, `bandBottomY` 3.42 | cùng `y`: trung bình không phụ thuộc thứ tự |
| bounds rỗng | `B` default, `s` 0.743 | (0, −1.905) |
| scale 0 | `s` 0 | (0, −1.905): không ném, tâm container về giữa dải |

---

## Chuyển dự án (agent)

| Chỗ | Đổi |
|---|---|
| `docs/Runtime/BandFit/BandFit.md`, `.html` | tạo, `Code: Runtime/Utilities/Layout`: Thuật toán và bảng mốc · hình ASCII dải giữa hai HUD với lề, level thấp hơn dải và cao đúng dải · bảng API · Không làm · chủ tính `screenWidth` từ camera ortho, `bandHeight` từ hai anchor (`null` khi thiếu), gọi cùng frame dựng bàn chơi, ba tham số là hằng của chủ · lưu ý `minScale > preferredScale` và lề nuốt màn là cấu hình lỗi |
| `Director/LevelBoundsFitter.cs` | xoá `FitScale`, `ContainerPosition` cùng XML doc; giữ ba hằng `LEVEL_*`; summary class còn "bounds của level tính từ data (đơn vị container, scale 1) và hằng fit bàn chơi" |
| `Director/LevelLoader.cs` › `AdjustContainerScale` | gọi `BandFit.FitScale(bounds, cam.orthographicSize * cam.aspect * 2f, bandHeight, LEVEL_SCREEN_MARGIN, LEVEL_CONTAINER_SCALE, LEVEL_CONTAINER_MIN_SCALE)` (hằng của `LevelBoundsFitter`) |
| `LevelLoader.cs` › `PositionShelfContainer` | `LevelBoundsFitter.ContainerPosition` → `BandFit.ContainerPosition`, tham số giữ |
| `LevelLoader.cs`, `Tests/EditMode/LevelBoundsFitterTests.cs` | thêm `using Horcrux.Runtime.Utilities.Layout;` (asmdef đã tham chiếu `com.horcrux.runtime`) |
| `LevelBoundsFitterTests` › `SingleAtEdge_FitsByItsBody` | gọi `BandFit.FitScale(bounds, 10.5f, 8.73f, …ba hằng)`; giữ ở dự án vì kiểm bề rộng 22.4 của bounds từ data |
| `LevelBoundsFitterTests` › `BandTopY`, `BandBottomY`, ba test `ContainerPosition_*` | xoá — là ba hàng đầu bảng case GĐ2 |
| `Scripts/Layout.md` › §1 | "bằng `LevelBoundsFitter` (…)" → "bằng Horcrux `BandFit` với hằng của `LevelBoundsFitter` (…)" |
| grep | `LevelBoundsFitter.FitScale`, `LevelBoundsFitter.ContainerPosition` toàn repo = 0 |
| Plan này | xoá cùng `.meta` |
| chơi thử | Game view 1080×2340 rồi 1080×1920, level 1 cột, level 2 cột có kệ Single một bên (level 8), level 4 hàng → bàn chơi giữa `mask_top`–`mask_bottom` và giữa màn từ frame đầu, không chạm thanh cache, booster bar; hỏng: kệ hiện ở gốc rồi nhảy · lệch ngang · 4 hàng tràn thanh cache |

## Không làm · Mở rộng sau

| | Phương án | Vì sao |
|---|---|---|
| bỏ | ba hằng đọc trong thân (bản dự án) | mỗi game một bộ; hằng ở lại dự án và truyền vào |
| bỏ | hai overload có và không có dải | hai thân gần trùng; một cờ `HasValue` rẻ hơn |
| bỏ | kẹp `minScale` trước khi xét chiều cao | tile tí hon không chơi được; tràn thì nhìn thấy |
| bỏ | tính bounds từ renderer | bounds tính từ data trước spawn; nhận `Bounds` là ranh giới đúng |
| bỏ | treo mép trên bounds một lề dưới đỉnh dải | level thấp hơn dải sát HUD trên, hở cả đáy; dự án nguồn chơi thử chọn căn giữa — `Giả định (cần xác nhận):` lý do là cảm giác, commit `e35e7cb5` chỉ ghi "quay lại căn giữa" |
| sau | hàm treo đỉnh `y = bandTopY − m − B_max,y·s` | khi bàn chơi kề thanh khay ở đỉnh; bản tạm ở commit dự án `9a8f3a92`, `ContainerPosition` giữ |
| sau | struct `[Serializable]` gom ba tham số + overload, hàm cũ giữ | khi game thứ hai nhận tham số từ Inspector |
| sau | lề riêng ngang, dọc | tham số tuỳ chọn khi có game cần; một lề đủ |
