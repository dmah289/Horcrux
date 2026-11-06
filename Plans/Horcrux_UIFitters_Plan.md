# Plan — `DesignWidthFitter` và `UICoverFitter` lên Horcrux

Ngày 2026-11-06. Nguồn: category-jam, bản tạm `Assets/_CategoryJam/Bridge/DesignWidthFitter.cs` (gắn ở `Shop/ShopPanel.prefab` ›
`Viewport/Content` và root của `Popups/Parts/PopupFrame.prefab`) và `Assets/_CategoryJam/Bridge/Menu/HomeBackgroundFitter.cs` (gắn ở
`Scenes/Menu.unity` › `Page3UI/Content/Home/bg_room`), dựng khi reskin Home, Shop, popup theo demo 1080×2340. Hai component UGUI độc lập,
mỗi cái một luồng và một chuỗi giai đoạn: giữ bề rộng thiết kế của một khối · phủ ảnh nền kín khung.

**Phân công:** GĐ1–4 developer gõ, agent viết test theo bảng case rồi compile · GĐ5 (tài liệu), GĐ6 (dự án chuyển sang bản Horcrux, xoá
bản tạm) agent làm. **Chạy được, thấy được** ở GĐ1–4 = `bash Assets/Horcrux/Editor/CompileCheck/Tools~/verify.sh --csproj
com.horcrux.runtime.csproj,Horcrux.EditModeTests.csproj --tests Horcrux.EditModeTests` xanh với bảng case trong
`Tests/EditMode/UIFitterTests.cs`; thấy trong game ở GĐ6. GĐ1–5 commit trong `Assets/Horcrux`, GĐ6 commit ở dự án kèm con trỏ
submodule. Không phụ thuộc Plan nào. Không có `§0`: hai công thức một dòng (`min` cho thu, `max` cho phủ) dẫn giải ở GĐ1, GĐ3.

## Ngữ cảnh đã chốt

- **Người dùng:** cột Shop và khung popup dùng `DesignWidthFitter`; ảnh nền Home dùng `HomeBackgroundFitter` · dự án sau có UI dọc dựng
  1:1 theo demo cỡ cố định, chạy cả điện thoại dài lẫn tablet.
- **Mục tiêu:** khối UI giữ bề rộng thiết kế, thu đều khi khung hẹp hơn · ảnh nền phủ kín khung, đổi ảnh tablet khi khung rộng hơn ảnh
  chính, con đặt theo pixel ảnh · không làm bẩn asset khi đổi Game view.
- **Chỗ đặt, tên:** `Runtime/Utilities/UI/`, namespace `Horcrux.Runtime.Utilities.UI`, cạnh `MButton`, `ProgressBar`, `Tooltip` (đặt
  `Runtime/Utilities/Layout/` cạnh `ViewportCover` cũng được). `DesignWidthFitter` giữ tên; `HomeBackgroundFitter` → **`UICoverFitter`**
  (lý do ở GĐ3). Field `width`, `tablet` giữ tên: là key YAML trong hai prefab và scene. Tên đổi tuỳ ý trước khi gõ.
- **GUID:** file Horcrux là file mới, bản tạm sống tới GĐ6; lúc chuyển `.meta` bản Horcrux nhận lại GUID bản tạm (GĐ6).
- **Hợp đồng với tool chụp:** Horcrux `Editor/UIReskin/UICapture.Canvas` tắt rồi bật lại mọi behaviour `[ExecuteAlways]` dưới canvas để
  fitter đọc cỡ khung mới khi chụp ngoài Play — hai component giữ `[ExecuteAlways]` và `OnEnable → Fit()`.
- **Không làm:** base class chung (lý do ở GĐ3) · `DesignWidthFitter` phóng to, guard `Width ≤ 0` (lý do ở GĐ1) · `UICoverFitter` hơn hai
  ảnh, `AspectRatioFitter` (lý do ở GĐ3) · callback cỡ cha thay `Update` (lý do ở GĐ2).
- **Đã khảo sát:** hai bản tạm (lấy nguyên hành vi; bỏ chữ domain trong comment, biến `mobile`) · `ShopPanel.prefab`, `PopupFrame.prefab`,
  `Menu.unity` (anchor, pivot; rect driven lưu Size Delta Y 0, Scale 0) · Canvas Scaler `Menu`, `Game`, `Launcher` (1080×2340, Match
  Width Or Height = 1: máy dài có canvas hẹp hơn 1080) · `Arts/UI/Background/bg_mobile.png` 1080×2340, `bg_ipad.png` 1755×2340, Max
  Size 4096 · Horcrux `Runtime/Utilities/UI/` (chỗ đặt, region; chưa có fitter) · `Editor/UIReskin/UICapture.cs`, `PrefabAudit.cs`
  (hợp đồng chụp; `PrefabAudit.Report` cho nghiệm thu GĐ6) · Plan ViewportAnchor `ViewportCover` (lấy cách nói `max(view / sprite)`;
  không hợp: sprite world, tính một lần ở boot) · UGUI `ContentSizeFitter` (lo chiều cao khối), `CanvasScaler` (không hợp: scale cả canvas).

## Luồng mô tả A — một khối giữ bề rộng thiết kế (`DesignWidthFitter`)

```
khối rộng 1080 trong prefab, gắn fitter ──► bật (prefab mở, scene nạp, Editor hay Play)
                                                   ▼
                              Fit: sizeDelta.x = width, scale = min(1, cha / width); khai driven;
                              tắt component thì trả driven                                          GĐ1
                                                   ▼
                              khung cha đổi (CanvasScaler, safe area, xoay, Game view) · đổi cha
                              ──► so bề rộng cha → Fit lại                                           GĐ2
```

## Luồng mô tả B — một ảnh nền phủ kín khung (`UICoverFitter`)

```
Image ảnh chính, con đặt theo pixel ảnh, gắn fitter ──► bật
                                                   ▼
                              Fit: khung rộng hơn tỉ lệ ảnh chính? → ảnh tablet qua overrideSprite
                              sizeDelta = cỡ ảnh, scale = max(khung.x / ảnh.x, khung.y / ảnh.y)
                              khai driven; tắt component thì trả driven và overrideSprite            GĐ3
                                                   ▼
                              khung cha đổi · rect chính nó đổi · đổi cha ──► so cỡ cha → Fit lại   GĐ4
                                                   ▼
                              tài liệu (GĐ5) · dự án dùng bản Horcrux, xoá bản tạm (GĐ6)
```

## Bản đồ hệ cuối

```
  Unity ──OnEnable / Update / OnTransformParentChanged / OnDisable──► DesignWidthFitter ──sizeDelta.x, localScale (driven)──► RectTransform khối
                                                                            ▲ đọc parent.rect.width                              │
                                                                            └────────────────────────────────── RectTransform cha ┘
                                                                                                                 (chiều cao: ContentSizeFitter / anchor dọc)

  Unity ──OnEnable / Update / OnRectTransformDimensionsChange / OnTransformParentChanged / OnDisable──► UICoverFitter
                                                     ┌──────────────────────────────────────────────────────┤
                                                     ▼                                                      ▼
                                    Image: sprite (ảnh chính, author) · overrideSprite (tablet, driven)    RectTransform: sizeDelta = cỡ ảnh, localScale = phủ kín (driven)
                                                                                                             └── con: anchor giữa, toạ độ theo pixel ảnh, phóng cùng ảnh
```

---

## Giai đoạn 1 — Khối giữ bề rộng lúc bật

**Mục tiêu:** component bật là khối có `sizeDelta.x = width`, `localScale = min(1, bề rộng cha / width)`; hai thứ đó driven; tắt component
thì trả driven. Xong giai đoạn này một khối 1080 trong khung 1000 thu còn 0.926, trong khung 1536 giữ 1.

**Vì sao bây giờ:** đây là toàn bộ phép tính; theo dõi cha đổi cỡ (GĐ2) chỉ là gọi lại nó.

### `Runtime/Utilities/UI/DesignWidthFitter.cs` — file mới

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
        private DrivenRectTransformTracker _tracker;

        #region Unity Callbacks

        private void OnEnable() => Fit();

        private void OnDisable() => _tracker.Clear();

        #endregion

        #region Class Methods

        private void Fit()
        {
            if (_rect == null) _rect = (RectTransform)transform;
            if (!(_rect.parent is RectTransform parent)) return;

            var area = parent.rect.width;
            if (area <= 0f) return;

            _tracker.Clear();
            _tracker.Add(this, _rect, DrivenTransformProperties.SizeDeltaX | DrivenTransformProperties.Scale);

            var scale = Mathf.Min(1f, area / width);
            if (_rect.sizeDelta.x != width) _rect.sizeDelta = new Vector2(width, _rect.sizeDelta.y);
            if (_rect.localScale.x != scale) _rect.localScale = new Vector3(scale, scale, 1f);
        }

        #endregion
    }
}
```

**Công thức → code:** `scale = min(1, area / width)`. Khung hẹp hơn thiết kế thì `area / width < 1`, khối thu đúng tỉ lệ khung; khung
rộng hơn thì `min` chặn ở 1. Canvas Scaler match height nên máy dài hơn demo có canvas hẹp hơn `width`, máy ngắn hơn hay tablet có canvas
rộng hơn: hai vế của `min` là hai loại máy.

**Khác với bản dự án:** namespace, comment, `[Tooltip]`, region. Hành vi giữ nguyên.

**Quyết định** — bề rộng và scale khai driven qua `DrivenRectTransformTracker`: giá trị driven không ghi vào file khi lưu (`Content` của Shop lưu
Size Delta Y 0, `bg_room` lưu Scale 0 trong khi lúc chạy là số khác), nên đổi Game view trong Editor tính lại mà không tạo diff và không đè giá trị
author; Inspector khoá hai ô đó, developer không sửa tay thứ frame sau bị ghi đè. Phương án bỏ: ghi thẳng `sizeDelta`, `localScale` — mỗi lần
đổi Game view là một diff trên prefab. — `Clear` rồi `Add` lại mỗi lần `Fit`: tracker là struct, `Add` lặp cộng dồn; `Clear` trước là cách
rẻ nhất giữ đúng một đăng ký. — Chỉ ghi khi khác (`!=`): ghi `sizeDelta` làm bẩn layout của cả cây. — Chỉ thu, không phóng to khi
khung rộng hơn: khối nằm giữa, hai bên là nền; phóng to làm art vỡ nét và lệch tỉ lệ so với phần còn lại của màn. — Không guard
`Width ≤ 0`: lỗi setup lộ ngay trong Editor (khối rộng 0); code canh là giấu bước setup.

| Consumes | Produces |
|---|---|
| `RectTransform` cha (bề rộng), `RectTransform` của khối | `sizeDelta.x`, `localScale` của khối, driven |

**Sơ đồ tương tác sau GĐ1** (`+` là phần mới)

```
  Unity ──OnEnable+──► DesignWidthFitter+ ──đọc parent.rect.width──► cha
          OnDisable+        │ Fit+: sizeDelta.x = width, scale = min(1, cha / width), driven
                            ▼
                        RectTransform khối
```

**Editor setup** — trên object khối (cột, khung popup); nhãn theo Inspector:

| Bước | Thiếu thì hỏng ở đâu |
|---|---|
| Canvas gốc › **Canvas Scaler**: UI Scale Mode `Scale With Screen Size`, Reference Resolution bằng cỡ demo (1080×2340), Screen Match Mode `Match Width Or Height`, Match `1` (Height) | Match `0` (Width): canvas luôn rộng đúng reference, fitter không bao giờ thu — vô hại nhưng vô ích, máy dài thiếu chiều cao |
| Khối › **Rect Transform**: Anchors Min X = Max X = `0.5`, Pivot X `0.5` (neo giữa ngang); dọc tuỳ khối (cột Shop: Min Y = Max Y = `1`, Pivot Y `1`; khung popup: `0.5`, Pivot Y `1`) | anchor giãn ngang (Min X `0`, Max X `1`): Size Delta X là phần bù so với cha, fitter ghi `width` vào đó → khối rộng cha + `width` |
| Khối › **Design Width Fitter** › Width = Reference Resolution X (`1080`) | khác reference: scale 1 trên máy demo nhưng khối không đúng cỡ demo; `0`: khối rộng 0, scale 1 (chia cho 0 cho vô cực, `min` chặn 1) |
| Chiều cao: **Content Size Fitter** › Vertical Fit `Preferred Size`, **Horizontal Fit `Unconstrained`**; hoặc anchor dọc | Horizontal Fit khác `Unconstrained`: hai driver cùng ghi Size Delta X, bề rộng theo nội dung, không còn `width` |
| Cha là `RectTransform` (Viewport của ScrollRect, khung safe area) | cha không phải `RectTransform`: `Fit` thoát, không ghi gì, không log |
| Thứ tự thực thi: **không** đặt Script Execution Order | layout rebuild chạy ở cuối frame (`Canvas.willRenderCanvases`), sau mọi `Update`: bề rộng fitter ghi được LayoutGroup và ContentSizeFitter thấy trong cùng frame |
| Dấu hiệu đúng: Rect Transform hiện "Some values driven by DesignWidthFitter", ô Width và Scale mờ | không mờ: component tắt hoặc cha chưa có cỡ |

**Kiểm:** compile sạch. Test dựng cha `new GameObject("parent", typeof(RectTransform))` đặt `sizeDelta` tường minh, con `AddComponent`
(`[ExecuteAlways]` nên `OnEnable` chạy ngay trong EditMode); field `width` đặt qua `SerializedObject` rồi tắt bật `enabled` để `Fit` lại —
không mở API mới cho test.

| Case | Input | Kỳ vọng |
|---|---|---|
| khung bằng thiết kế | `width 1080`, cha rộng 1080 | `sizeDelta.x` 1080, scale 1 |
| khung hẹp | cha rộng 1000 | scale 0.9259, `sizeDelta.x` 1080, `sizeDelta.y` giữ nguyên |
| khung rộng | cha rộng 1536 | scale 1, không phóng |
| cha chưa có cỡ | cha rộng 0 | không ghi, không driven, không ném |
| cha không phải RectTransform | `new GameObject("parent")` | không ghi, không ném |
| ngoài dải | `width 0` | `sizeDelta.x` 0, scale 1 |
| tắt component | `enabled = false` | driven gỡ: đặt tay `sizeDelta.x` 500 giữ được |
| bật lại | `enabled = true` sau khi đặt tay | `sizeDelta.x` về 1080 |

**Mốc commit:** `feat(ui): DesignWidthFitter giữ bề rộng thiết kế lúc bật`.

---

## Giai đoạn 2 — Khung cha đổi cỡ, đổi cha

**Mục tiêu:** cha đổi bề rộng (CanvasScaler tính theo máy, script safe area co khung ở `Update`, xoay, đổi Game view) hay khối đổi cha
thì `Fit` lại. Xong giai đoạn này khối đúng cỡ ở mọi frame, không chỉ frame bật.

**Vì sao bây giờ:** `OnEnable` chạy trước khi CanvasScaler và safe area tính xong trong frame nạp; không có chặng này khối giữ cỡ của
frame đầu.

### `DesignWidthFitter.cs` › field › thêm sau `_rect`

```csharp
        private RectTransform _rect;
        private float _lastParentWidth;
        private DrivenRectTransformTracker _tracker;
```

### `DesignWidthFitter.cs` › `Unity Callbacks` › thêm sau `OnDisable`

```csharp
        private void OnDisable() => _tracker.Clear();

        private void OnTransformParentChanged() => Fit();

        private void Update()
        {
            // khung cha đổi bề rộng không gọi callback nào trên con có anchor giữa
            if (_rect != null && _rect.parent is RectTransform parent && parent.rect.width != _lastParentWidth)
                Fit();
        }
```

### `DesignWidthFitter.cs` › `Fit` — đoạn cũ → đoạn mới

Cũ:

```csharp
            var area = parent.rect.width;
            if (area <= 0f) return;
```

Mới — ghi cỡ đã đọc **trước** guard, để cha rộng 0 không làm `Update` gọi `Fit` mỗi frame:

```csharp
            var area = parent.rect.width;
            _lastParentWidth = area;
            if (area <= 0f) return;
```

**Khác với bản dự án:** không — sau giai đoạn này file khớp bản tạm trừ namespace, comment, `[Tooltip]`, region.

**Quyết định** — `Update` so một `float` mỗi frame thay vì nghe sự kiện: `OnRectTransformDimensionsChange` chỉ tới object đổi cỡ, con neo
giữa không nhận; `Canvas.preWillRenderCanvases` chạy mỗi frame như `Update` mà khó đọc hơn. So sánh không cấp phát, không gọi gì khi
không đổi. — `[ExecuteAlways]`: khung đổi khi developer kéo Game view ngoài Play; không có nó khối sai cỡ trong Editor tới khi Play.
Phương án bỏ: `UIBehaviour` để nhận `OnRectTransformDimensionsChange` của cha — không có callback cho cỡ cha.

| Consumes | Produces |
|---|---|
| GĐ1 | `Fit` chạy lại khi cha đổi bề rộng, khi đổi cha |

**Sơ đồ tương tác sau GĐ2**

```
  Unity ──OnEnable / Update+ / OnTransformParentChanged+ / OnDisable──► DesignWidthFitter ──Fit khi cha / _lastParentWidth+ lệch──► RectTransform khối
```

**Editor setup:** không thêm — GĐ1 đủ; safe area co khung ở `Update` của nó, fitter bắt kịp ở frame kế tiếp, không cần Script Execution Order.

**Kiểm:** compile sạch. `Update` gọi qua reflection (`BindingFlags.NonPublic | Instance`) — EditMode không chạy vòng `Update`.

| Case | Input | Kỳ vọng |
|---|---|---|
| cha đổi cỡ sau khi bật | cha 1080 → 900, gọi `Update` | scale 0.8333 |
| cha không đổi | gọi `Update` hai lần | giá trị giữ nguyên |
| frame đầu cha chưa có cỡ | cha 0 lúc bật, rồi 1080, gọi `Update` | `sizeDelta.x` 1080, scale 1, driven |
| đổi cha | `SetParent` sang cha rộng 1536 | scale 1 ngay, không chờ `Update` |
| cha về 0 giữa chừng | cha 1080 → 0, gọi `Update` | giá trị cũ giữ nguyên, không ném |

**Mốc commit:** `feat(ui): DesignWidthFitter theo cỡ cha mỗi frame`.

---

## Giai đoạn 3 — Ảnh nền phủ kín khung lúc bật

**Mục tiêu:** component bật là ảnh nền phủ kín khung cha: rect đúng cỡ ảnh, `localScale` phóng tới khít trục thiếu, khung rộng hơn tỉ lệ
ảnh chính thì hiện ảnh tablet qua `overrideSprite`; driven; tắt component thì trả driven và `overrideSprite`. Xong giai đoạn này một ảnh
1080×2340 trong khung 1080×2520 phóng 1.077, trong khung 1536×2048 đổi sang ảnh tablet.

**Vì sao bây giờ:** đây là toàn bộ phép tính của ảnh nền; theo dõi khung (GĐ4) chỉ gọi lại nó.

### `Runtime/Utilities/UI/UICoverFitter.cs` — file mới

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
        private DrivenRectTransformTracker _tracker;

        #region Unity Callbacks

        private void OnEnable() => Fit();

        private void OnDisable()
        {
            _tracker.Clear();
            if (_image != null) _image.overrideSprite = null;
        }

        #endregion

        #region Class Methods

        private void Fit()
        {
            if (_rect == null) _rect = (RectTransform)transform;
            if (_image == null) _image = GetComponent<Image>();
            var main = _image.sprite;
            if (!(_rect.parent is RectTransform parent) || main == null) return;

            var area = parent.rect.size;
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

        #endregion
    }
}
```

**Công thức → code:** phủ kín là `scale = max(khung.x / ảnh.x, khung.y / ảnh.y)` — trục nào thiếu nhiều hơn quyết định, trục kia tràn
và bị cắt đều hai mép (pivot giữa). Chọn tablet khi `khung.x / khung.y > ảnh chính.x / ảnh chính.y`: khung rộng hơn ảnh chính thì
trục thiếu là x, phóng theo x cắt trên dưới; ảnh tablet cùng cao, rộng hơn nên scale nhỏ hơn, cắt ít hơn. Rect = cỡ ảnh gốc nên con đặt
ở `(x, y)` pixel từ tâm ảnh luôn nằm trên đúng pixel đó, ở mọi scale.

**Khác với bản dự án:** tên class, namespace, comment, `[Tooltip]`, region; biến `mobile` thành `main`. Hành vi giữ nguyên.

**Quyết định** — rect đúng cỡ ảnh, phóng bằng `localScale`: con theo pixel ảnh phóng cùng ảnh; `AspectRatioFitter` giãn rect nên con
trôi. — Ảnh tablet qua `overrideSprite`, không ghi `sprite`: `sprite` là giá trị author, `overrideSprite` là lớp runtime, tắt
component thì `null` trả về ảnh chính không cần nhớ ảnh cũ. — Ảnh tablet phải cùng cao, cùng tâm, nới hai bên: toạ độ con dùng chung;
ảnh vẽ lệch tâm thì con trôi — điều kiện ghi ở Tooltip và tài liệu, không kiểm trong code. — Driven: như GĐ1. — Không guard ảnh chính
thiếu: nền không hiện là lỗi setup lộ ngay, như GĐ1. — Tên `UICoverFitter`, không `HomeBackgroundFitter`: "Home" là người dùng đầu,
"cover" là cơ chế, "UI" phân biệt với `ViewportCover` cho sprite world. — Không base class chung với `DesignWidthFitter`: khác lý do
thay đổi (giữ bề rộng khối, phủ ảnh); phần chung chỉ là khung `DrivenRectTransformTracker` + theo dõi cỡ cha, vài dòng. — Hai ảnh,
không hơn: chưa có người dùng; cần thì mảng sprite kèm tỉ lệ ngưỡng, field `tablet` giữ nghĩa là phần tử đầu.

| Consumes | Produces |
|---|---|
| `Image` (sprite chính), `RectTransform` cha (cỡ), `Sprite.rect` | `overrideSprite`, `sizeDelta`, `localScale`, driven |

**Sơ đồ tương tác sau GĐ3**

```
  Unity ──OnEnable+ / OnDisable+──► UICoverFitter+ ──đọc parent.rect.size, sprite.rect──► cha, Image
                                        │ Fit+: chọn ảnh, sizeDelta = cỡ ảnh, scale = max(...), driven
                                        ├──overrideSprite──► Image
                                        └──sizeDelta, localScale──► RectTransform ảnh ──► con theo pixel ảnh
```

**Editor setup** — trên object ảnh nền; nhãn theo Inspector:

| Bước | Thiếu thì hỏng ở đâu |
|---|---|
| **Image** › Source Image = ảnh chính; Image Type `Simple` | ảnh chính trống: `Fit` thoát, không log, nền không hiện |
| **Rect Transform**: Anchors Min = Max = `(0.5, 0.5)`, Pivot `(0.5, 0.5)`, Pos X = Pos Y = `0`; Width, Height, Scale để fitter ghi | anchor giãn: Size Delta là phần bù, rect = cha + cỡ ảnh, ảnh méo; pivot lệch: phóng quanh pivot, ảnh lệch tâm khung |
| Cha phủ vùng cần che (khung stretch toàn trang, ra **ngoài** safe area nếu nền phải phủ tai thỏ) | cha trong safe area: nền dừng ở mép safe area, hở dải trên dưới |
| **UI Cover Fitter** › Tablet = ảnh chính nới hai bên, **cùng chiều cao, cùng tâm** (1080×2340 ↔ 1755×2340); hoặc trống | trống: tablet dùng ảnh chính phóng theo bề rộng, cắt trần và sàn; khác cao hay lệch tâm: con trôi khỏi chi tiết vẽ sẵn khi đổi ảnh |
| Hai texture › **Max Size** ≥ cạnh dài của ảnh (2340 → `4096`) | `2048`: sprite co còn 945×2048, rect theo cỡ đã co, con đặt theo pixel ảnh gốc lệch ~12% |
| Con (bục, khay, chữ): Anchors `(0.5, 0.5)`, Pos = pixel từ tâm ảnh; là con của ảnh, **không** neo theo safe area | neo safe area: máy tai thỏ dời con khỏi bóng vẽ sẵn trên nền |
| Thứ tự thực thi: không đặt Script Execution Order | không có layout nào đọc rect ảnh nền; con đọc qua transform ở frame vẽ |
| Dấu hiệu đúng: Rect Transform hiện "Some values driven by UICoverFitter"; file scene lưu Scale 0, Size Delta 0 là bình thường | component tắt hay script mất: scene nạp giá trị trong file (scale 0) → nền không hiện, không lỗi đỏ — kiểm bằng `PrefabAudit.Report` (`missing script`) |

**Kiểm:** compile sạch. Sprite dựng bằng `Sprite.Create` từ `Texture2D` đúng cỡ (hay 1/10 cỡ — scale kỳ vọng không đổi); `tablet` đặt qua
`SerializedObject` rồi tắt bật `enabled`.

| Case | Input | Kỳ vọng |
|---|---|---|
| máy demo | ảnh 1080×2340, cha 1080×2340 | scale 1, `sizeDelta` 1080×2340, `overrideSprite` là ảnh chính |
| máy dài | cha 1080×2520 | scale 1.0769, `sizeDelta` 1080×2340 |
| máy thấp | cha 1080×1920 | scale 1 (x khít, y tràn) |
| đổi sang tablet | tablet 1755×2340, cha 1536×2048 | `overrideSprite` là tablet, `sizeDelta` 1755×2340, scale 0.8752 |
| không có tablet | như trên, `tablet` null | ảnh chính, scale 1.4222 |
| đúng tỉ lệ ảnh chính | cha 540×1170 | ảnh chính (không lớn hơn), scale 0.5 |
| ảnh chính trống | `Image.sprite` null | không ghi, không driven, không ném |
| cha chưa có cỡ | cha 0×0 | không ghi, không ném |
| tắt component | `enabled = false` khi đang tablet | `overrideSprite` null, driven gỡ |

**Mốc commit:** `feat(ui): UICoverFitter phủ kín khung, đổi ảnh tablet`.

---

## Giai đoạn 4 — Khung đổi cỡ, rect đổi, đổi cha

**Mục tiêu:** cha đổi cỡ (xoay, tablet, đổi Game view), rect chính nó đổi, hay đổi cha thì `Fit` lại; đổi ảnh theo tỉ lệ mới, quay về ảnh
chính khi khung hẹp lại. Xong giai đoạn này ảnh nền đúng ở mọi frame.

**Vì sao bây giờ:** như GĐ2 — `OnEnable` chạy trước khi CanvasScaler tính xong; developer kéo Game view trong Editor phải thấy ảnh đổi ngay.

### `UICoverFitter.cs` › field › thêm sau `_image`

```csharp
        private Image _image;
        private Vector2 _lastParentSize;
        private DrivenRectTransformTracker _tracker;
```

### `UICoverFitter.cs` › `Unity Callbacks` › thêm sau `OnDisable`

```csharp
        private void OnRectTransformDimensionsChange() => Fit();

        private void OnTransformParentChanged() => Fit();

        private void Update()
        {
            // khung cha đổi kích thước không gọi OnRectTransformDimensionsChange của con có anchor giữa
            if (_rect != null && _rect.parent is RectTransform parent && parent.rect.size != _lastParentSize)
                Fit();
        }
```

### `UICoverFitter.cs` › `Fit` — đoạn cũ → đoạn mới

Cũ:

```csharp
            var area = parent.rect.size;
            if (area.x <= 0f || area.y <= 0f) return;
```

Mới:

```csharp
            var area = parent.rect.size;
            _lastParentSize = area;
            if (area.x <= 0f || area.y <= 0f) return;
```

**Khác với bản dự án:** không — sau giai đoạn này file khớp bản tạm trừ tên, namespace, comment, `[Tooltip]`, region, `main`.

**Quyết định** — `Update` so một `Vector2` mỗi frame: như GĐ2. — `OnRectTransformDimensionsChange` giữ theo bản tạm: Unity gọi khi rect
của chính ảnh đổi, kể cả do `Fit` vừa ghi `sizeDelta`; lần gọi lại thấy mọi giá trị đã khớp, không ghi, dừng — không vòng lặp.
`Giả định (cần xác nhận):` ca thật callback này bắt mà `Update` không bắt (nguồn không ghi); giữ vì rẻ và không đổi kết quả. — Ảnh chính
đổi lúc chạy mà cha không đổi thì không `Fit`: chưa có người dùng; cần thì thêm một hàm gọi lại `Fit`, không đổi chữ ký.

| Consumes | Produces |
|---|---|
| GĐ3 | `Fit` chạy lại khi cha đổi cỡ, rect đổi, đổi cha |

**Sơ đồ tương tác sau GĐ4**

```
  Unity ──OnEnable / Update+ / OnRectTransformDimensionsChange+ / OnTransformParentChanged+ / OnDisable──► UICoverFitter ──Fit khi cha / _lastParentSize+ lệch──► Image, RectTransform
```

**Editor setup:** không thêm — GĐ3 đủ.

**Kiểm:** compile sạch; `Update` qua reflection như GĐ2.

| Case | Input | Kỳ vọng |
|---|---|---|
| máy dài sau khi bật | cha 1080×2340 → 1080×2520, gọi `Update` | scale 1.0769 |
| sang tablet rồi về | cha → 1536×2048, `Update`; → 1080×2340, `Update` | tablet, scale 0.8752; rồi `overrideSprite` null, scale 1 |
| cha không đổi | `Update` hai lần | giá trị giữ nguyên |
| frame đầu cha 0 | cha 0×0 lúc bật, rồi 1080×2340, `Update` | scale 1, driven |
| ảnh chính trống rồi có | `sprite` null lúc bật, gán ảnh, `Update` (cha không đổi) | vẫn không `Fit` — tắt bật mới áp; ghi ở tài liệu |
| đổi cha | `SetParent` sang cha 1536×2048 | tablet ngay, không chờ `Update` |
| rect bị ghi ngoài | đặt tay `sizeDelta` 10×10 khi đang bật | `OnRectTransformDimensionsChange` → `Fit` trả về cỡ ảnh |

**Mốc commit:** `feat(ui): UICoverFitter theo cỡ cha mỗi frame`.

---

## Giai đoạn 5 — Tài liệu `docs/Runtime/UIFitters/UIFitters.md` và `.html` (agent)

Một cặp cho cả hai component, viết sau khi đọc code developer đã gõ. `.md` mở đầu `Code: Runtime/Utilities/UI/` (hai file). Mục: hai luồng
mô tả ở trên (ASCII) · bảng field `Width`, `Tablet` · **"Trước khi chạy"** = hai bảng Editor setup của GĐ1 và GĐ3 nguyên vẹn, kèm Canvas
Scaler · "Design decisions" gom "Không làm" của Ngữ cảnh đã chốt và các **Quyết định** của GĐ1–4 · bẫy: đổi `sprite` lúc chạy
không tự `Fit`; file lưu Scale 0 là bình thường; Max Size texture · hợp đồng với `UICapture.Canvas`. `.html` lấy Kịch bản (máy dài, máy
thấp, tablet → ảnh nào, khối thu bao nhiêu), Cách dùng ("Trước khi chạy"), Flow, Lưu ý; tên trung tính, không kịch bản chơi thử.

**Mốc commit:** `docs(ui): UIFitters.md, UIFitters.html`.

---

## Giai đoạn 6 — Dự án dùng bản Horcrux, xoá bản tạm (agent)

**Mục tiêu:** hai prefab và scene trỏ sang script Horcrux mà không nối lại component, không mất `width: 1080` và `tablet`; bản tạm và tên
`HomeBackgroundFitter` không còn trong repo.

**Code dự án:** không file `.cs` nào gọi tên hai type (chỉ prefab, scene trỏ GUID) — không thêm `using`, không sửa code.

**Chuyển GUID** — làm khi Editor đóng, hoặc gộp cả ba bước trước một lần Refresh để prefab không trải qua trạng thái `missing` được lưu lại:

1. `git rm Assets/_CategoryJam/Bridge/DesignWidthFitter.cs Assets/_CategoryJam/Bridge/DesignWidthFitter.cs.meta
   Assets/_CategoryJam/Bridge/Menu/HomeBackgroundFitter.cs Assets/_CategoryJam/Bridge/Menu/HomeBackgroundFitter.cs.meta`.
2. `Assets/Horcrux/Runtime/Utilities/UI/DesignWidthFitter.cs.meta` › `guid:` → `36b62d2860b4cfb44907c8900a9178c9`;
   `UICoverFitter.cs.meta` › `guid:` → `8e63ae2e0a997f64db2c23a1da699cf2`.
3. Mở Editor: `ShopPanel.prefab` › `Viewport/Content`, root `PopupFrame.prefab` hiện `Design Width Fitter` với Width `1080`; `Menu.unity` ›
   `bg_room` hiện `UI Cover Fitter` với Tablet là `bg_ipad`. Field giữ tên nên YAML `width: 1080`, `tablet: {…}` nối lại nguyên giá trị.

**Tài liệu dự án:** `Bridge/Bridge.md` — bảng file: bỏ hàng `DesignWidthFitter`, hàng `Menu/*` bỏ "(bản tạm, lên Horcrux …)" và gọi
`UICoverFitter`; mục Reskin, cây Home (`bg_room [UICoverFitter]`), mục Popup, cây Shop: đổi tên, trỏ `Horcrux/Runtime/Utilities/UI/`.
`Structure.md` — ba dòng nêu `DesignWidthFitter`, `HomeBackgroundFitter`: đổi tên, ghi là component Horcrux.

**Editor setup:** không — anchor, Canvas Scaler, Max Size đã đúng trên ba object chủ (mục "Đã khảo sát").

**Kiểm (agent):** grep `HomeBackgroundFitter`, `CategoryJam.Bridge.DesignWidthFitter` toàn repo (`.cs`, `.prefab`, `.unity`, `.md`): 0 · hai
GUID chỉ còn ở hai `.meta` Horcrux và ba asset chủ · `PrefabAudit.Report("Assets/_CategoryJam/Bridge")` không có `missing script` ·
`UICapture.Canvas(CanvasMenu, 1080, 2340, …)` và `(1536, 2048)` chụp trước và sau khi chuyển: hai cặp PNG trùng byte ·
`bash Assets/Horcrux/Editor/CompileCheck/Tools~/verify.sh --csproj Assembly-CSharp.csproj,CategoryJam.Gameplay.csproj,com.horcrux.runtime.csproj
--tests CategoryJam.EditModeTests,Horcrux.EditModeTests` xanh.

**Cần chơi thử (developer):** Game view 1080×2340 rồi 1536×2048, scene `Menu` → Home: nền phòng phủ kín tới mép tai thỏ, tablet hiện
ảnh rộng không cắt trần sàn, bục và khay nằm đúng bóng vẽ sẵn → tab Shop: cột rộng 1080 ở máy demo, thu đều ở máy dài, nằm giữa hai dải
nền ở tablet → mở Pause hay Setting: khung popup cùng luật. Dấu hiệu hỏng: nền Home không hiện (scale 0 từ file) · cột Shop tràn mép hoặc
khung popup rộng hơn màn · bục lệch bóng · tablet thấy mép trên dưới bị cắt.

**Mốc commit:** repo dự án `refactor(ui): fitter UI dùng bản Horcrux, xoá bản tạm` kèm con trỏ submodule.

---

## File → giai đoạn

| File | GĐ1 | GĐ2 | GĐ3 | GĐ4 | GĐ5 | GĐ6 |
|---|---|---|---|---|---|---|
| `Runtime/Utilities/UI/DesignWidthFitter.cs` (+ `.meta`) | tạo | `Update`, `OnTransformParentChanged`, `_lastParentWidth` | | | | `.meta` nhận GUID tạm |
| `Runtime/Utilities/UI/UICoverFitter.cs` (+ `.meta`) | | | tạo | `Update`, `OnRectTransformDimensionsChange`, `OnTransformParentChanged`, `_lastParentSize` | | `.meta` nhận GUID tạm |
| `Tests/EditMode/UIFitterTests.cs` (agent) | tạo | thêm case | thêm case | thêm case | | |
| `docs/Runtime/UIFitters/UIFitters.md`, `.html` (agent) | | | | | tạo | |
| dự án: xoá `Bridge/DesignWidthFitter.cs`, `Bridge/Menu/HomeBackgroundFitter.cs`; sửa `Bridge/Bridge.md`, `Structure.md` | | | | | | đổi |
