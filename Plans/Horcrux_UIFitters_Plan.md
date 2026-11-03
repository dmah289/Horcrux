# Plan — `DesignWidthFitter` và `UICoverFitter` lên Horcrux

Ngày 2026-11-03. Nguồn: bản tạm trong dự án `Assets/_CategoryJam/Bridge/DesignWidthFitter.cs` (cột Shop, khung popup) và
`Assets/_CategoryJam/Bridge/Menu/HomeBackgroundFitter.cs` (ảnh nền phòng của Home), dựng trong ba task reskin Home, Shop,
popup và đã xem ở 1080×2340, 1080×2520, 1536×2048.

**Phân công:**
- Task 1, 2 (Horcrux, code): developer gõ.
- Task 3 (dự án dùng bản Horcrux), Task 4 (tài liệu module), Task 5 (test): agent làm.

Horcrux là submodule: Task 1, 2, 4, 5 commit **trong `Assets/Horcrux`**, Task 3 commit ở repo dự án kèm con trỏ submodule.

---

## Ngữ cảnh đã chốt

| Mục | Chốt |
|---|---|
| Người dùng | dự án này: `ShopPanel` (cột), `Popups/Parts/PopupFrame` (khung popup) dùng `DesignWidthFitter`; prefab Home dùng `HomeBackgroundFitter` · dự án sau có UI dọc dựng 1:1 theo demo cỡ cố định và chạy trên cả điện thoại dài lẫn tablet |
| Mục tiêu | khối UI giữ bề rộng thiết kế, thu nhỏ đều khi khung hẹp hơn · ảnh nền UI phủ kín khung, đổi ảnh tablet khi khung rộng hơn ảnh chính, con đặt theo pixel ảnh; cả hai không làm bẩn asset khi đổi Game view |
| Chỗ đặt | `Runtime/Utilities/UI/`, namespace `Horcrux.Runtime.Utilities.UI`, cạnh các component UI đang có. Không gọi type nào của dự án (bản tạm đã đạt). Developer đặt `Runtime/Utilities/Layout/` cạnh `ViewportCover` (Plan ViewportAnchor) cũng được: không đổi gì khác |
| Tên | `DesignWidthFitter` giữ · `HomeBackgroundFitter` đổi thành **`UICoverFitter`**: "Home" là người dùng đầu, "cover" là cơ chế, "UI" phân biệt với `ViewportCover` cho sprite world. Field `tablet` giữ tên (đổi tên là đổi key YAML trong prefab Home). Developer đổi tuỳ ý trước khi gõ |
| Giữ GUID | **chuyển file trong Project window của Unity** rồi mới sửa: prefab trỏ script bằng GUID (`DesignWidthFitter` `36b62d2860b4cfb44907c8900a9178c9`, `HomeBackgroundFitter` `8e63ae2e0a997f64db2c23a1da699cf2`); chuyển bằng Project window thì component trên prefab giữ nguyên |

**Cố ý KHÔNG làm**

| Không làm | Vì |
|---|---|
| Base class chung cho hai fitter | khác lý do thay đổi (một giữ bề rộng khối, một phủ ảnh); chung mỗi khung `DrivenRectTransformTracker` + theo dõi cỡ cha, vài dòng |
| `DesignWidthFitter` phóng to khi khung rộng hơn | khối nằm giữa, hai bên là nền: phóng to làm art vỡ nét và lệch tỉ lệ so với phần còn lại của màn |
| `UICoverFitter` nhiều hơn hai ảnh | chưa có người dùng; thêm sau là mảng sprite kèm tỉ lệ ngưỡng, field `tablet` giữ nghĩa |
| Dùng `AspectRatioFitter` (`EnvelopeParent`) cho ảnh nền | nó giãn rect theo khung nên con đặt theo pixel ảnh trôi khỏi chi tiết vẽ sẵn khi tỉ lệ đổi; không đổi ảnh theo tỉ lệ |
| Theo dõi cỡ cha bằng callback thay cho `Update` | `OnRectTransformDimensionsChange` chỉ gọi trên object đổi cỡ; con neo giữa không nhận khi cha đổi bề rộng |

**Quyết định trái trực giác**

| Chỗ | Vì |
|---|---|
| Bề rộng và scale khai **driven** (`DrivenRectTransformTracker`), ảnh tablet gán qua `overrideSprite` | đổi Game view trong Editor tính lại mà không ghi vào prefab, scene: không có diff rác, không đè giá trị author |
| `[ExecuteAlways]` và `Update` so cỡ cha mỗi frame | khung cha đổi do CanvasScaler, safe area, xoay; so một `float` (một `Vector2`) mỗi frame rẻ hơn mọi cách nghe sự kiện còn thiếu ca |
| `UICoverFitter` giữ rect **đúng cỡ ảnh** và phóng bằng `localScale` | con của nó (bục, khay, chữ) đặt theo pixel ảnh, phóng cùng ảnh nên luôn trùng chi tiết vẽ sẵn |
| Ảnh tablet phải là ảnh chính **nới thêm hai bên, cùng tâm, cùng chiều cao** | toạ độ con dùng chung cho cả hai ảnh; ảnh vẽ lệch tâm thì con trôi |
| `DesignWidthFitter` chỉ thu, `scale = min(1, khung / width)` | điện thoại dài hơn demo có canvas hẹp hơn bề rộng thiết kế khi CanvasScaler match height |

---

## Đã khảo sát

| Nguồn | Lấy gì | Không lấy, vì sao |
|---|---|---|
| Bản tạm trong dự án (2 file trên) | toàn bộ hành vi | chữ "1080 như demo", "Home", tên đồ vật trong comment: domain dự án |
| Horcrux `Runtime/Utilities/UI/` | chỗ đặt, namespace | chưa có fitter nào |
| Plan ViewportAnchor (`ViewportCover`) | cách nói "phủ kín" `max(view / sprite)` | chạy cho `SpriteRenderer` world, tính một lần ở boot; UI cần theo khung cha đổi trong Editor |
| UGUI `AspectRatioFitter`, `CanvasScaler`, `LayoutElement` | — | xem "Cố ý KHÔNG làm"; `CanvasScaler` scale cả canvas, không giữ một khối riêng |

---

## Task 1 — `DesignWidthFitter` (developer gõ)

1. Project window: kéo `Assets/_CategoryJam/Bridge/DesignWidthFitter.cs` vào `Assets/Horcrux/Runtime/Utilities/UI/`.
   *Thiếu thì:* file mới mang GUID mới, `ShopPanel` và `PopupFrame` báo script missing.
2. Thay toàn bộ nội dung:

```csharp
using UnityEngine;

namespace Horcrux.Runtime.Utilities.UI
{
    /// <summary>
    /// Khối UI giữ đúng bề rộng thiết kế để art nằm đúng pixel như demo. Khung cha hẹp hơn thì thu nhỏ đều cả khối; rộng hơn
    /// (tablet) thì khối giữ cỡ, nằm theo anchor, hai bên là nền. Bề rộng và scale khai driven, chiều cao để layout lo; đổi Game
    /// view không làm bẩn prefab.
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(RectTransform))]
    public class DesignWidthFitter : MonoBehaviour
    {
        [Tooltip("Bề rộng thiết kế theo đơn vị canvas, thường bằng bề rộng reference resolution của demo.")]
        [SerializeField] private float width = 1080f;

        private RectTransform _rect;
        private float _lastParentWidth;
        private DrivenRectTransformTracker _tracker;

        private void OnEnable() => Fit();

        private void OnDisable() => _tracker.Clear();

        private void OnTransformParentChanged() => Fit();

        private void Update()
        {
            // khung cha đổi bề rộng không gọi callback nào trên con có anchor giữa
            if (_rect != null && _rect.parent is RectTransform parent && parent.rect.width != _lastParentWidth)
                Fit();
        }

        private void Fit()
        {
            if (_rect == null) _rect = (RectTransform)transform;
            if (!(_rect.parent is RectTransform parent)) return;

            var area = parent.rect.width;
            _lastParentWidth = area;
            if (area <= 0f) return;

            _tracker.Clear();
            _tracker.Add(this, _rect, DrivenTransformProperties.SizeDeltaX | DrivenTransformProperties.Scale);

            var scale = Mathf.Min(1f, area / width);
            if (_rect.sizeDelta.x != width) _rect.sizeDelta = new Vector2(width, _rect.sizeDelta.y);
            if (_rect.localScale.x != scale) _rect.localScale = new Vector3(scale, scale, 1f);
        }
    }
}
```

Mốc: compile sạch, mở `ShopPanel` thấy component còn nguyên, Inspector báo `Width 1080`.

## Task 2 — `UICoverFitter` (developer gõ)

1. Project window: kéo `Assets/_CategoryJam/Bridge/Menu/HomeBackgroundFitter.cs` vào `Assets/Horcrux/Runtime/Utilities/UI/`, đổi
   tên file thành `UICoverFitter.cs` trong Project window. *Thiếu thì:* prefab Home báo script missing.
2. Thay toàn bộ nội dung:

```csharp
using UnityEngine;
using UnityEngine.UI;

namespace Horcrux.Runtime.Utilities.UI
{
    /// <summary>
    /// Ảnh nền UI phủ kín khung cha (envelope), chọn ảnh chính hoặc ảnh tablet theo tỉ lệ khung. Rect giữ đúng cỡ gốc của ảnh và
    /// phóng bằng localScale, nên con của nó đặt theo pixel của ảnh và luôn trùng chi tiết vẽ sẵn ở mọi cỡ màn.
    /// Ảnh tablet phải là ảnh chính nới thêm hai bên, cùng tâm, cùng chiều cao: toạ độ con dùng chung cho cả hai.
    /// Ảnh chính là sprite trên Image; ảnh tablet gán qua overrideSprite, sizeDelta và scale khai driven như AspectRatioFitter,
    /// nên đổi Game view trong Editor không làm bẩn scene.
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(Image))]
    public class UICoverFitter : MonoBehaviour
    {
        [Tooltip("Ảnh dùng khi khung rộng hơn tỉ lệ ảnh chính. Để trống thì luôn dùng ảnh chính.")]
        [SerializeField] private Sprite tablet;

        private RectTransform _rect;
        private Image _image;
        private Vector2 _lastParentSize;
        private DrivenRectTransformTracker _tracker;

        private void OnEnable() => Fit();

        private void OnDisable()
        {
            _tracker.Clear();
            if (_image != null) _image.overrideSprite = null;
        }

        private void OnRectTransformDimensionsChange() => Fit();

        private void OnTransformParentChanged() => Fit();

        private void Update()
        {
            // khung cha đổi kích thước không gọi OnRectTransformDimensionsChange của con có anchor giữa
            if (_rect != null && _rect.parent is RectTransform parent && parent.rect.size != _lastParentSize)
                Fit();
        }

        private void Fit()
        {
            if (_rect == null) _rect = (RectTransform)transform;
            if (_image == null) _image = GetComponent<Image>();
            var main = _image.sprite;
            if (!(_rect.parent is RectTransform parent) || main == null) return;

            var area = parent.rect.size;
            _lastParentSize = area;
            if (area.x <= 0f || area.y <= 0f) return;

            _tracker.Clear();
            _tracker.Add(this, _rect, DrivenTransformProperties.SizeDelta | DrivenTransformProperties.Scale);

            // khung rộng hơn ảnh chính thì dùng ảnh tablet để không phải phóng to cắt mất mép trên dưới
            var mainSize = main.rect.size;
            var useTablet = tablet != null && area.x / area.y > mainSize.x / mainSize.y;
            var sprite = useTablet ? tablet : main;
            var size = sprite.rect.size;
            var scale = Mathf.Max(area.x / size.x, area.y / size.y);

            // getter overrideSprite trả sprite đang hiển thị (override, không có thì sprite gốc)
            if (_image.overrideSprite != sprite) _image.overrideSprite = useTablet ? tablet : null;
            if (_rect.sizeDelta != size) _rect.sizeDelta = size;
            if (_rect.localScale.x != scale) _rect.localScale = new Vector3(scale, scale, 1f);
        }
    }
}
```

Khác bản tạm: tên class, namespace, comment; biến `mobile` thành `main`. Hành vi giữ nguyên.

Mốc: compile sạch, mở prefab Home thấy component `UICoverFitter` với ô `Tablet` còn ảnh tablet.

Commit trong `Assets/Horcrux` sau khi Task 3 vá xong và compile sạch, để hai commit cùng một mốc chạy được.

---

## Task 3 — Dự án dùng bản Horcrux (agent)

- File `.cs` của dự án gọi tên hai type: hiện không có (chỉ prefab trỏ GUID), nên không thêm `using`.
- `Bridge/Bridge.md`, `Structure.md`, ba spec reskin ở `docs/superpowers/specs/`: đổi `HomeBackgroundFitter` thành `UICoverFitter`,
  đường dẫn `DesignWidthFitter` sang Horcrux; bỏ dòng `DesignWidthFitter` khỏi bảng file của Bridge.
- Nghiệm thu: grep `HomeBackgroundFitter`, `CategoryJam.Bridge.DesignWidthFitter` trên `.cs`, `.prefab`, `.unity`, `.md` toàn repo: 0;
  `PrefabAudit.Report` trên `Bridge/` không có `missing script`; chụp Home, Shop, một popup ở 1080×2340 và 1536×2048 trùng ảnh
  trước khi chuyển.

## Task 4 — Tài liệu module (agent)

`docs/Runtime/UIFitters/UIFitters.md` và `.html` (một cặp cho cả hai component), viết sau khi đọc code developer đã gõ:

- **Trước khi chạy:** `DesignWidthFitter` gắn trên khối có cha là `RectTransform` (thường là khung stretch của màn), anchor ngang
  ở giữa; ô `Width` bằng bề rộng reference resolution · chiều cao khối do layout (ContentSizeFitter) hay anchor dọc lo ·
  `UICoverFitter` gắn trên Image ảnh nền, anchor và pivot giữa; ô `Tablet` kéo ảnh nới hai bên hoặc để trống; con đặt theo pixel ảnh
  chính.
- **Design decisions:** các bảng "Cố ý KHÔNG làm", "Quyết định trái trực giác" ở trên.

## Task 5 — Test EditMode (agent)

`Assets/Horcrux/Tests/EditMode/UIFitterTests.cs`. Cha là `RectTransform` dựng trong test, đặt `sizeDelta` tường minh; component
gắn bằng `AddComponent` (`[ExecuteAlways]` nên `OnEnable` chạy trong EditMode).

| Case | Input | Kỳ vọng |
|---|---|---|
| khung bằng thiết kế | `width 1080`, cha rộng 1080 | sizeDelta.x 1080, scale 1 |
| khung hẹp | cha rộng 1000 | scale 0.9259, sizeDelta.x 1080 |
| khung rộng | cha rộng 1536 | scale 1 |
| cha đổi cỡ sau khi bật | cha 1080 → 900, gọi `Update` một lần (qua reflection hay vòng Editor) | scale 0.8333 |
| tắt component | `enabled = false` | tracker gỡ: sizeDelta, scale sửa tay được |
| cover máy dài | ảnh 1080×2340, cha 1080×2520 | scale 1.0769, sizeDelta 1080×2340 |
| cover máy thấp | cha 1080×1920 | scale 1 |
| đổi sang tablet | ảnh chính 1080×2340, tablet 1536×2340, cha 1536×2048 | `overrideSprite` là tablet, sizeDelta 1536×2340, scale 1 |
| không có tablet | như trên, `tablet` null | ảnh chính, scale 1.4222 |
| quay về ảnh chính | cha từ 1536×2048 về 1080×2340 | `overrideSprite` null, scale 1 |
| tắt cover | `enabled = false` | `overrideSprite` null |

Sprite dựng bằng `Sprite.Create` từ `Texture2D` đúng cỡ; field private đặt qua `SerializedObject`, không mở API mới cho test.
