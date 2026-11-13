# Plan — `SlicedFill` lên Horcrux

Nguồn: bản tạm của một dự án game, dùng cho thanh số lượng trên kệ và thanh tiến độ trên box, đã chơi thử. Utility thuần: bốn hàm
static tính và ghi bề rộng ruột của thanh vẽ bằng `SpriteRenderer` Sliced.

**Phân công:** GĐ1 developer gõ, agent viết test theo bảng case rồi compile · GĐ2 (dự án chuyển sang bản Horcrux) agent làm. GĐ1 commit
trong `Assets/Horcrux`, GĐ2 commit ở dự án kèm con trỏ submodule. Không phụ thuộc Plan nào, không có `§0`: hai công thức một dòng nằm ở GĐ1.

## Ngữ cảnh đã chốt

- **Người dùng:** component world vẽ thanh bằng `SpriteRenderer` Sliced, ví dụ `HealthBar` trên đầu quái, thanh tiến độ trên vật thể.
- **Chỗ đặt, tên:** `Runtime/Utilities/Sprites/SlicedFill.cs`, namespace `Horcrux.Runtime.Utilities`. Utility thuần, không phụ
  thuộc hệ nào.
- **Cố ý không làm:** tween — caller tự `Tween.Custom` rồi gọi `Apply` mỗi frame · trục dọc — chưa có người dùng.
- **Sprite ruột phải pivot trái:** đổi `size.x` thì mép trái đứng yên. Pivot giữa thì ruột co hai phía, lệch khỏi khung.
- **Đổi `size.x`, không đổi `localScale.x`:** Sliced giữ hai đầu bo nguyên cỡ khi đổi `size`; đổi scale thì bo méo.

## Đã khảo sát

| Nơi | Kết luận |
|---|---|
| `Runtime/Utilities/UI/ProgressBar` | chỉ cho UI `RectTransform`/`Image`, không cho `SpriteRenderer` |
| Phần còn lại của Horcrux | không có bản world nào |

## Luồng mô tả

```
Awake    : full = fill.size.x ; min = SlicedFill.MinWidth(fill.sprite)        (chụp một lần)
Dữ liệu đổi (curr, total):
         width = SlicedFill.FillWidth(curr, total, full)
         SlicedFill.Apply(fill, width, min)
                 └─ VisibleWidth: 0 → ẩn ; (0, min) → nâng lên min ; ≥ min → giữ
Có tween : Tween.Custom(from, to, ..., w => SlicedFill.Apply(fill, w, min))
```

Chụp `full` ở `Awake`, không đọc lại `fill.size.x` về sau: `Apply` ghi đè `size.x`.

## Bản đồ hệ cuối

| Thành phần | Ai gọi | Việc |
|---|---|---|
| `SlicedFill.FillWidth` | caller mỗi lần dữ liệu đổi | tỉ lệ curr/total → bề rộng |
| `SlicedFill.MinWidth` | caller một lần ở `Awake` | hai đầu bo của sprite, theo unit |
| `SlicedFill.VisibleWidth` | `Apply`; caller khi cần số mà không ghi | 0 → 0 · dương → không hẹp hơn `MinWidth` |
| `SlicedFill.Apply` | caller (trực tiếp hoặc trong tween) | ghi `size.x`, bật/tắt renderer |

## Giai đoạn 1 — `SlicedFill`

**Mục tiêu:** thanh Sliced chạy đúng ở mọi tỉ lệ, kể cả 0 và rất nhỏ. **Vì sao trước:** là toàn bộ hệ.

File mới `Runtime/Utilities/Sprites/SlicedFill.cs`:

```csharp
using UnityEngine;

namespace Horcrux.Runtime.Utilities
{
    /// <summary>
    /// Ruột thanh SpriteRenderer Sliced, sprite pivot trái: đổi size.x thì mép trái đứng yên, hai đầu bo không méo.
    /// </summary>
    public static class SlicedFill
    {
        #region API

        /// <summary>Bề rộng ruột cho curr trên total; total ≤ 0 thì rỗng.</summary>
        public static float FillWidth(int curr, int total, float fullWidth)
            => total <= 0 ? 0f : fullWidth * Mathf.Clamp01((float)curr / total);

        /// <summary>Bề rộng vẽ được: 0 thì ẩn; dương thì không hẹp hơn hai đầu bo của Sliced.</summary>
        public static float VisibleWidth(float width, float minWidth)
            => width <= 0f ? 0f : Mathf.Max(width, minWidth);

        /// <summary>Hai đầu bo của sprite, theo unit: hẹp hơn thì Sliced chồng hai đầu lên nhau.</summary>
        public static float MinWidth(Sprite sprite)
        {
            var border = sprite.border;
            return (border.x + border.z) / sprite.pixelsPerUnit;
        }

        /// <summary>Ghi bề rộng vẽ được lên ruột; 0 thì tắt renderer, giữ nguyên size.</summary>
        public static void Apply(SpriteRenderer fill, float width, float minWidth)
        {
            var visible = VisibleWidth(width, minWidth);
            fill.enabled = visible > 0f;
            if (visible > 0f)
                fill.size = new Vector2(visible, fill.size.y);
        }

        #endregion API
    }
}
```

- `FillWidth`: `Clamp01` chặn curr vượt total và âm; `total ≤ 0` trả 0 thay vì chia cho 0.
- `MinWidth`: `border.x + border.z` là trái + phải theo pixel, chia `pixelsPerUnit` ra unit. Bề rộng dưới số này làm Sliced chồng hai đầu bo.
- `Apply` giữ `size.y` và không đụng `size` khi ẩn: lần hiện lại chỉ ghi `x`.
- Caller đọc `sprite.border` qua `MinWidth` một lần; sprite đổi lúc chạy thì gọi lại.

**Editor setup:** không. Phía caller: `SpriteRenderer.drawMode` = **Sliced**, sprite có border trái/phải > 0 và pivot **trái**, `size.x`
lúc dựng là bề rộng đầy. Thiếu pivot trái: ruột co về tâm. Thiếu Sliced: `size` bị bỏ qua, thanh không đổi.

**Kiểm:** compile sạch; test EditMode `Horcrux.EditModeTests` (agent viết sau khi đọc code thật):

| Input | Kỳ vọng |
|---|---|
| `FillWidth(0, 10, 2)` · `FillWidth(5, 10, 2)` · `FillWidth(10, 10, 2)` | 0 · 1 · 2 |
| `FillWidth(15, 10, 2)` · `FillWidth(-3, 10, 2)` | 2 · 0 (chặn biên) |
| `FillWidth(3, 0, 2)` · `FillWidth(3, -1, 2)` | 0 · 0 (không chia 0) |
| `VisibleWidth(0, 0.4)` · `VisibleWidth(-1, 0.4)` | 0 · 0 |
| `VisibleWidth(0.1, 0.4)` · `VisibleWidth(0.4, 0.4)` · `VisibleWidth(1, 0.4)` | 0.4 · 0.4 · 1 |
| `MinWidth` sprite border 18/0/18/0, 100 ppu | 0.36 |
| `Apply` width 0 | `enabled` false, `size` không đổi |
| `Apply` width 1, minWidth 0.4 | `enabled` true, `size.x` 1, `size.y` giữ |
| `Apply` width 0.1, minWidth 0.4 | `size.x` 0.4 |

**Mốc commit:** compile sạch + bảng case xanh → commit trong `Assets/Horcrux`.

## Giai đoạn 2 — dự án chuyển sang bản Horcrux (agent)

**Mục tiêu:** dự án dùng bản Horcrux, bản tạm biến mất. **Vì sao cuối:** cần bản Horcrux compile được và test xanh trước.

- Đổi `using` từ namespace của bản tạm sang `Horcrux.Runtime.Utilities` ở mọi caller (thanh số lượng kệ, thanh tiến độ box, view
  thiết kế level trong tool Editor).
- Xoá file bản tạm cùng `.meta`.
- Chuyển test của bản tạm sang `Horcrux.EditModeTests`, xoá bản cũ.
- Cập nhật tài liệu dự án nhắc bản tạm (bảng "chờ Horcrux", chỗ ghi đường dẫn bản tạm).

**Kiểm:** grep tên type kèm namespace cũ ra 0; compile sạch; test xanh. **Mốc commit:** một commit ở dự án kèm con trỏ submodule.

## Use case

Thanh máu trên đầu quái (world) · thanh tiến độ trên vật thể: box, kệ.

## File → giai đoạn

| File | GĐ |
|---|---|
| `Runtime/Utilities/Sprites/SlicedFill.cs` | 1 |
| Test EditMode `SlicedFillTests` | 1 (viết), 2 (chuyển) |
| Caller và bản tạm của dự án | 2 |
