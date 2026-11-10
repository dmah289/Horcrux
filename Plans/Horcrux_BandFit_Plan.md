# Plan — `BandFit` lên Horcrux

Ngày 2026-11-06. Nguồn: category-jam `Scripts/Director/LevelBoundsFitter.cs` (`FitScale`, `ContainerPosition`) và
`Tests/EditMode/LevelBoundsFitterTests.cs`, gọi từ `LevelLoader.AdjustContainerScale`, `LevelLoader.PositionShelfContainer`. Lên
Horcrux hai hàm thuần: scale đồng nhất để một `Bounds` lọt bề rộng màn và dải cao tuỳ chọn (chừa lề, kẹp dưới), và vị trí container
đặt tâm bounds giữa dải. Phần tính bounds từ data level ở lại dự án.

**Phân công:** GĐ1–2 developer gõ, agent viết test theo bảng case rồi compile · GĐ3 (tài liệu), GĐ4 (dự án chuyển sang bản
Horcrux) agent làm. **Chạy được, thấy được** ở GĐ1–2 = compile sạch + bảng case xanh trong `Tests/EditMode/BandFitTests.cs`, không
dựng object; thấy trong game ở GĐ4. GĐ1–3 commit trong `Assets/Horcrux`, GĐ4 commit ở dự án kèm con trỏ submodule. Không phụ thuộc
Plan nào; `Runtime/Utilities/Layout/` chưa có, `Horcrux_ViewportAnchor_Plan.md` cũng đặt file ở đó — plan nào gõ trước thì tạo thư
mục. Có `§0`.

## Ngữ cảnh đã chốt

- **Người dùng:** game đặt bàn chơi world vào dải giữa HUD trên và HUD dưới, camera ortho giữ trọn khung thiết kế
  (`Horcrux_ViewportAnchor_Plan.md` GĐ1: bề rộng trên màn cao hơn thiết kế, chiều cao trên màn rộng hơn); bàn chơi là
  một container scale đồng nhất, bounds tính ở scale 1.
- **Chỗ đặt:** `Runtime/Utilities/Layout/BandFit.cs`, namespace `Horcrux.Runtime.Utilities.Layout`. Tên `BandFit`, `FitScale`,
  `ContainerPosition` đổi tuỳ ý trước khi gõ.
- **Mục tiêu:** hai hàm thuần trên `Bounds`, `Vector2` và số — test không dựng gì. Ba hằng dự án (`0.743`, `0.37`, `0.4`) thành
  tham số `preferredScale`, `minScale`, `margin`: mỗi game một bộ. Bounds, màn, dải, lề cùng đơn vị world; scale áp cho x và y.
- **Không làm:** struct gom tham số (hai hàm ba số; game thứ hai nhận từ Inspector thì thêm struct `[Serializable]` và overload,
  hàm cũ giữ) · lề riêng ngang, dọc (một lề đủ; thêm là tham số tuỳ chọn) · tính bounds từ renderer (bounds tính từ data trước
  spawn; nhận `Bounds` là ranh giới đúng) · treo đỉnh bounds lên đỉnh dải — dự án nguồn đã chơi thử cả hai và chọn căn giữa;
  `Giả định (cần xác nhận):` lý do là cảm giác chơi, commit dự án `e35e7cb5` chỉ ghi "quay lại căn giữa". Phương án bỏ ở GĐ2.
- **Đã khảo sát:** `LevelBoundsFitter.cs` (lấy `FitScale`, `ContainerPosition` nguyên thứ tự phép tính; bỏ
  `TryGetLevelLocalBounds`, `CellExtents`, hằng ô kệ, nén hàng: schema level) · `LevelBoundsFitterTests.cs` (dòng `FitScale` của
  `SingleAtEdge_FitsByItsBody`, ba test `ContainerPosition_*`, mốc dải `3.42`, `−7.23`) · `LevelLoader.cs` (cách gọi:
  `screenWidth = orthographicSize × aspect × 2`, `bandHeight` là `Mathf.Abs` hiệu y hai anchor hoặc `null` khi thiếu anchor, gọi
  cùng frame dựng kệ trước lần vẽ đầu) · Horcrux `Runtime/Utilities/` (chưa có hàm fit) · `Horcrux_UIFitters_Plan.md`, Unity
  `AspectRatioFitter` (không hợp: làm trên `RectTransform`).

## Luồng mô tả — một bàn chơi vào dải

```
chủ có Bounds bàn chơi (scale 1), bề rộng màn, dải giữa hai HUD (hoặc không có dải)
                              ▼
      scale đồng nhất: lọt bề rộng trừ hai lề · lọt dải trừ hai lề · không quá
      scale ưa thích · không dưới scale tối thiểu                                   GĐ1
                              ▼
      vị trí container: tâm bounds về giữa ngang và giữa dải                         GĐ2
                              ▼
      tài liệu module (GĐ3) · dự án gọi bản Horcrux, xoá bản tạm (GĐ4)
```

## Bản đồ hệ cuối

```
  chủ bàn chơi (cùng frame dựng, trước lần vẽ đầu)
     │ bounds, screenWidth, bandHeight?, margin, preferredScale, minScale
     ▼
  BandFit.FitScale ───────────► scale ───────► chủ gán localScale (s, s, 1)
     │
     │ bounds, scale, bandTopY, bandBottomY
     ▼
  BandFit.ContainerPosition ──► (x, y) ──────► chủ gán position
```

---

## §0 — Công thức

**Hiện tượng.** Bàn chơi vẽ ở scale 1 có cỡ tuỳ level. Màn có bề rộng world `W` (bằng bề rộng thiết kế trên màn cao, rộng hơn trên tablet); HUD trên và HUD dưới chừa
một dải cao `H`. Camera và anchor hai mép co cùng luật với HUD, nên `H` theo unit giữ nguyên trên màn rộng hơn thiết kế; camera chỉ giữ
bề rộng thì `H` co theo tỉ số hai aspect và bàn chơi tụt về `s_min` (tablet 3:2: dải còn nửa, scale kẹt sàn).
Cần một scale `s` để bàn lọt cả bề rộng lẫn dải, mỗi bên chừa lề `m`; không to quá `s₀` (cỡ art đã chốt) và không nhỏ dưới `s_min`
(còn đọc được, chấp nhận tràn).

| Ký hiệu | Là gì |
|---|---|
| `B_w`, `B_h`, `B_cx`, `B_cy` | bề rộng, chiều cao, tâm của bounds ở scale 1 |
| `W` | bề rộng màn world (`orthographicSize × aspect × 2` với camera ortho) |
| `H` | chiều cao dải; không có dải thì bỏ số hạng của nó |
| `m` | lề mỗi bên, cùng đơn vị |
| `s₀`, `s_min` | scale ưa thích, scale tối thiểu |

**Scale** — ràng buộc "lọt" là `B_w·s ≤ W − 2m` và `B_h·s ≤ H − 2m`, mỗi cái cho một trần; lấy trần thấp nhất rồi kẹp:

$$s = \max\big(s_{min},\ \min(s_0,\ \tfrac{W - 2m}{B_w},\ \tfrac{H - 2m}{B_h})\big)$$

Bỏ số hạng khi: không có dải · chiều đó của `B` ≤ 0.01 (bounds một điểm) · `W − 2m` hay `H − 2m` ≤ 0 (lề nuốt hết màn hay dải, không
còn gì để lọt).

**Vị trí** — tâm bounds sau scale nằm ở `B_c·s` so với container; container đặt tại đích trừ đi đó, đích là `x = 0` và giữa dải:

$$x = -B_{cx}\,s,\qquad y = \tfrac{\text{bandTopY} + \text{bandBottomY}}{2} - B_{cy}\,s$$

Từ phương trình ra code: ① `s = s₀` → ② `s = min(s, (W − 2m)/B_w)` nếu `B_w > 0.01` và `W − 2m > 0` → ③ `s = min(s, (H − 2m)/B_h)`
nếu có `H`, `B_h > 0.01` và `H − 2m > 0` → ④ `s = max(s, s_min)` → ⑤ `(x, y)` theo công thức trên.

Hệ quả kiểm được: level ràng theo chiều cao có `B_h·s = H − 2m`, tâm ở giữa dải nên mép trên cách đỉnh dải đúng `m`, mép dưới cách đáy
đúng `m`. Level thấp hơn dải hở hai đầu bằng nhau, mỗi đầu `(H − B_h·s)/2 > m`.

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

## Giai đoạn 1 — Scale lọt bề rộng và dải

**Mục tiêu:** chủ đưa bounds, bề rộng màn, dải (hoặc không) và ba tham số, nhận một scale đồng nhất. Xong giai đoạn này chủ gán được
`localScale` cho container và bàn chơi không tràn màn.

**Vì sao bây giờ:** scale là số đầu tiên chủ cần, trước cả vị trí: vị trí tính trên bounds đã scale.

### `Runtime/Utilities/Layout/BandFit.cs` — file mới

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
    }
}
```

**Khác với bản dự án**

| Chỗ | Bản dự án | Bản Horcrux | Vì |
|---|---|---|---|
| ba hằng | `LEVEL_CONTAINER_SCALE`, `LEVEL_CONTAINER_MIN_SCALE`, `LEVEL_SCREEN_MARGIN` đọc trong thân | ba tham số cuối | mỗi game một bộ; hằng ở lại `LevelBoundsFitter` của dự án và truyền vào (GĐ4) |
| ngưỡng `0.01f` | số trong thân | `MinSize` | hai chỗ dùng cùng một ngưỡng |

Thứ tự phép tính giữ nguyên từng dòng.

**Quyết định** — `bandHeight` nhận `float?` thay vì chủ truyền `float.MaxValue`: không có dải là ca thật (màn không có HUD dưới,
chủ thiếu một anchor); `null` đọc được ở call site là "không có dải", số lớn giả vẫn đi qua phép trừ lề và phép chia. Phương án
bỏ: hai overload có và không có dải — hai thân gần trùng, một cờ `HasValue` rẻ hơn. — Lề nuốt hết màn (`availableWidth ≤ 0`) thì **bỏ ràng** thay vì trả `minScale`: không còn gì để lọt, scale ưa thích là câu trả
lời ít sai nhất; ca này chỉ xảy ra khi chủ cấu hình lề sai, và tràn nhìn thấy ngay. — Kẹp `minScale` **sau** cả hai chiều: bàn
chơi quá to vẫn giữ cỡ đọc được, chấp nhận tràn — tràn nhìn thấy, tile tí hon thì không chơi được.

| Consumes | Produces |
|---|---|
| `UnityEngine.Bounds`, `Mathf` | `BandFit.FitScale` |

**Sơ đồ tương tác sau GĐ1** (`+` là phần mới)

```
  chủ ──bounds, screenWidth, bandHeight?, margin, preferredScale, minScale──► BandFit.FitScale+ ──scale──► chủ gán localScale
```

**Editor setup:** không — hàm thuần, chủ gọi bằng code với số của mình.

**Kiểm:** compile sạch. Test khai hằng `W 10.5`, dải `3.42 → −7.23`, `m 0.4`, `s₀ 0.743`, `s_min 0.37`; sai số `1e-3`.

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
| `minScale` trên `preferredScale` | `s_min` 0.8 | 0.8 — kẹp thắng; ghi ở tài liệu là cấu hình lỗi của chủ |

**Mốc commit:** `feat(layout): BandFit.FitScale`.

---

## Giai đoạn 2 — Vị trí container giữa dải

**Mục tiêu:** với scale vừa có và hai mốc dải, chủ nhận vị trí world của container để tâm bounds nằm giữa ngang và giữa dải. Xong giai
đoạn này một bàn chơi tâm lệch (kệ Single một bên) vẫn đứng giữa màn và giữa hai HUD.

**Vì sao bây giờ:** scale đã có; vị trí là bước còn lại trước khi chủ gán transform. Hai mốc dải đi riêng (`bandTopY`, `bandBottomY`)
thay vì `bandHeight` của GĐ1 vì vị trí cần biết dải nằm **ở đâu**, scale chỉ cần biết dải **cao bao nhiêu**.

### `BandFit.cs` › thêm sau `FitScale`, trước dấu đóng class

```csharp
            return Mathf.Max(scale, minScale);
        }

        /// <summary>
        /// Vị trí world của container (đã scale <paramref name="scale"/>) để <paramref name="bounds"/> căn giữa ngang và giữa dải
        /// <paramref name="bandTopY"/>–<paramref name="bandBottomY"/>: bounds tâm lệch vẫn nằm giữa dải. Không có lề: lề đã
        /// vào scale, bounds cao đúng dải tự cách hai đầu một lề.
        /// </summary>
        public static Vector2 ContainerPosition(Bounds bounds, float scale, float bandTopY, float bandBottomY)
            => new(-bounds.center.x * scale, (bandTopY + bandBottomY) / 2f - bounds.center.y * scale);
```

**Khác với bản dự án:** không — chữ ký và thân giống nguyên văn; chỉ XML doc đổi.

**Quyết định** — căn tâm bounds vào giữa dải. Phương án bỏ: treo mép trên bounds một lề dưới đỉnh dải — level thấp hơn dải đứng sát HUD
trên, hở toàn bộ xuống đáy; dự án nguồn đã chơi thử và chọn căn giữa (giả định ở "Ngữ cảnh đã chốt"). Game cần treo đỉnh
(bàn chơi kề thanh khay ở đỉnh) thì thêm hàm riêng `y = bandTopY − m − B_max,y·s`, bản tạm ở commit dự án `9a8f3a92`;
`ContainerPosition` giữ. — Hai mốc dải truyền riêng, không truyền `bandCenterY`: chủ đang cầm hai anchor, phép cộng chia hai
ở trong hàm thì call site không phải tính gì.

| Consumes | Produces |
|---|---|
| GĐ1 (`scale` từ `FitScale`) | `BandFit.ContainerPosition` |

**Sơ đồ tương tác sau GĐ2**

```
  chủ ──► BandFit.FitScale ──scale──► chủ ──bounds, scale, bandTopY, bandBottomY──► BandFit.ContainerPosition+ ──(x, y)──► chủ gán position
```

**Editor setup:** không.

**Kiểm:** compile sạch; cùng hằng GĐ1.

| Case | Input | Kỳ vọng |
|---|---|---|
| tâm giữa dải | `B` 18.4×13.6 tâm (−1, 0.6), `s` từ `FitScale` | `y + 0.6·s` = (3.42 − 7.23)/2 |
| căn giữa ngang | cùng `B`, `s` 0.5 | `x + (−1)·0.5` = 0 |
| level cao đúng dải chạm hai lề | `B` 13.4×18.6 tâm (0, −1), `s` từ `FitScale` | `y + B.max.y·s` = 3.42 − 0.4; `y + B.min.y·s` = −7.23 + 0.4 |
| level thấp hơn dải hở đều | `B` 18.4×13.6, `s` từ `FitScale` | hở trên = hở dưới = (10.65 − 13.6·s)/2 |
| dải đảo | `bandTopY` −7.23, `bandBottomY` 3.42 | cùng `y`: trung bình không phụ thuộc thứ tự |
| bounds rỗng | `B` default, `s` 0.743 | (0, −1.905) |
| scale 0 | `s` 0 | (0, −1.905): không ném, tâm container về giữa dải |

**Mốc commit:** `feat(layout): BandFit.ContainerPosition`.

---

## Giai đoạn 3 — Tài liệu `docs/Runtime/BandFit/BandFit.md` và `BandFit.html` (agent)

`.md` mở đầu `Code: Runtime/Utilities/Layout`. Mục: `§0` ở trên (hiện tượng, công thức, năm bước, bảng mốc) · hình ASCII dải giữa hai HUD với lề và hai ca level thấp hơn dải, level cao
đúng dải · bảng API hai hàm · "Design decisions" gom "Không làm" ở "Ngữ cảnh đã chốt" và các **Quyết định** của GĐ1–2 ·
"Dùng trong dự án": chủ tính `screenWidth` từ camera ortho, `bandHeight` từ hai anchor (`null` khi thiếu), gọi cùng frame dựng bàn chơi,
giữ ba tham số là hằng của mình · ca `minScale > preferredScale` và lề nuốt màn là cấu hình lỗi của chủ. Không có "Trước khi chạy": hàm
thuần, không wire. `.html` chọn từ `.md`: kịch bản (ba cỡ level vào một dải), cách dùng (chủ tính gì, gọi lúc nào), flow, lưu ý
(hai cấu hình lỗi).

**Mốc commit:** `docs(layout): BandFit.md`.

---

## Giai đoạn 4 — Dự án gọi bản Horcrux, xoá bản tạm (agent)

**Mục tiêu:** `LevelBoundsFitter` chỉ còn hằng và phần bounds từ data; `LevelLoader` gọi `BandFit` với ba hằng của `LevelBoundsFitter`;
test vị trí chuyển về Horcrux. Hành vi quan sát được không đổi: cùng công thức, cùng hằng.

### `Scripts/Director/LevelBoundsFitter.cs` › `API` — xoá

Xoá `FitScale` và `ContainerPosition` cùng XML doc của chúng. Giữ `LEVEL_CONTAINER_SCALE`, `LEVEL_CONTAINER_MIN_SCALE`,
`LEVEL_SCREEN_MARGIN` (doc của hằng không trỏ hàm nào). Sửa summary class: bỏ vế "scale đồng nhất… và vị trí container", còn "bounds của
level tính từ data (đơn vị container, scale 1) và hằng fit bàn chơi".

### `Scripts/Director/LevelLoader.cs` › `AdjustContainerScale` — đoạn cũ → đoạn mới

Cũ:

```csharp
                    scale = LevelBoundsFitter.FitScale(bounds, cam.orthographicSize * cam.aspect * 2f, bandHeight);
```

Mới:

```csharp
                    scale = BandFit.FitScale(bounds, cam.orthographicSize * cam.aspect * 2f, bandHeight,
                        LevelBoundsFitter.LEVEL_SCREEN_MARGIN, LevelBoundsFitter.LEVEL_CONTAINER_SCALE, LevelBoundsFitter.LEVEL_CONTAINER_MIN_SCALE);
```

### `LevelLoader.cs` › `PositionShelfContainer` — đoạn cũ → đoạn mới

Cũ:

```csharp
            var position = LevelBoundsFitter.ContainerPosition(m_levelLocalBounds, shelfContainer.transform.localScale.x,
                m_topAnchor.position.y, m_bottomAnchor.position.y);
```

Mới:

```csharp
            var position = BandFit.ContainerPosition(m_levelLocalBounds, shelfContainer.transform.localScale.x,
                m_topAnchor.position.y, m_bottomAnchor.position.y);
```

Thêm `using Horcrux.Runtime.Utilities.Layout;` ở `LevelLoader.cs`; `CategoryJam.Gameplay.asmdef` đã tham chiếu `com.horcrux.runtime`.

### `Tests/EditMode/LevelBoundsFitterTests.cs`

| Chỗ | Cũ | Mới |
|---|---|---|
| `SingleAtEdge_FitsByItsBody` | `LevelBoundsFitter.FitScale(bounds, 10.5f, 8.73f)` | `BandFit.FitScale(bounds, 10.5f, 8.73f, LevelBoundsFitter.LEVEL_SCREEN_MARGIN, LevelBoundsFitter.LEVEL_CONTAINER_SCALE, LevelBoundsFitter.LEVEL_CONTAINER_MIN_SCALE)` — case này kiểm bề rộng 22.4 của bounds, giữ ở dự án |
| `BandTopY`, `BandBottomY`, `ContainerPosition_CentresLevelInBand`, `ContainerPosition_CentresLevelHorizontally`, `ContainerPosition_HeightLimitedLevelTouchesBothMargins` | ba test vị trí | xoá — đã là ba hàng đầu bảng case GĐ2 trong `BandFitTests.cs` |

Thêm `using Horcrux.Runtime.Utilities.Layout;`; `CategoryJam.EditModeTests.asmdef` đã tham chiếu `com.horcrux.runtime`.

### `Scripts/Layout.md` › §1 — câu "fit vào dải giữa `mask_top` và `mask_bottom` bằng `LevelBoundsFitter` (…)"

→ "bằng Horcrux `BandFit` với hằng của `LevelBoundsFitter` (…)"; phần trong ngoặc giữ nguyên.

**Editor setup:** không — không prefab, scene nào trỏ hai hàm này.

**Kiểm:** grep `LevelBoundsFitter.FitScale`, `LevelBoundsFitter.ContainerPosition` toàn repo trả 0 · compile dự án sạch · test dự án và
Horcrux xanh · **cần chơi thử** (developer): Game view 1080×2340 rồi 1080×1920, vào một level 1 cột, một level 2 cột có kệ Single một bên
(hình level 8), một level 4 hàng; nhìn bàn chơi đứng giữa `mask_top`–`mask_bottom` và giữa màn ngay từ frame đầu, không chạm thanh
cache và booster bar; dấu hiệu hỏng: kệ hiện ở gốc rồi nhảy xuống · bàn chơi lệch ngang ở level tâm lệch · level 4 hàng tràn vào
thanh cache.

**Mốc commit:** repo dự án `refactor(layout): LevelLoader dùng BandFit của Horcrux` kèm con trỏ submodule.

---

## File → giai đoạn

| File | GĐ1 | GĐ2 | GĐ3 | GĐ4 |
|---|---|---|---|---|
| `Runtime/Utilities/Layout/BandFit.cs` | tạo, `FitScale` | `ContainerPosition` | | |
| `Tests/EditMode/BandFitTests.cs` (agent) | tạo | thêm case | | |
| `docs/Runtime/BandFit/BandFit.md`, `.html` (agent) | | | tạo | |
| dự án: `Director/LevelBoundsFitter.cs`, `Director/LevelLoader.cs`, `Tests/EditMode/LevelBoundsFitterTests.cs`, `Scripts/Layout.md` | | | | đổi |
