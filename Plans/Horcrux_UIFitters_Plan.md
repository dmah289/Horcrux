# Plan — `DesignWidthFitter` giữ bề rộng thiết kế của khối UI, `UICoverFitter` phủ ảnh nền kín khung

Bản tạm: category-jam `Bridge/DesignWidthFitter.cs` (`Shop/ShopPanel.prefab` › `Viewport/Content`, root `Popups/Parts/PopupFrame.prefab`), `Bridge/Menu/HomeBackgroundFitter.cs` (`Scenes/Menu.unity` › `Page3UI/Content/Home/bg_room`).
**Phụ thuộc:** không — gõ lúc nào cũng được; hai component độc lập.
Kiểm: `bash "<horcrux>/Editor/CompileCheck/Tools~/verify.sh" --csproj com.horcrux.runtime.csproj,Horcrux.EditModeTests.csproj --tests Horcrux.EditModeTests`; chuyển dự án `--csproj Assembly-CSharp.csproj,CategoryJam.Gameplay.csproj,com.horcrux.runtime.csproj --tests CategoryJam.EditModeTests,Horcrux.EditModeTests`.

## Kịch bản

### `DesignWidthFitter`

| Tình huống | Hệ làm gì | Người chơi thấy |
|---|---|---|
| máy demo, canvas rộng đúng `width` | `sizeDelta.x = width`, scale 1 | khối đúng pixel như demo |
| máy dài hơn demo (canvas match height hẹp hơn `width`) | scale = cha / `width` | khối thu đều, vừa bề ngang |
| tablet, máy ngắn (canvas rộng hơn) | scale chặn ở 1 | khối giữ cỡ, nằm theo anchor, hai bên là nền |
| khối có `height`, cha thấp hơn (popup toàn màn trên tablet) | scale = min theo cả chiều cao | khối nằm trọn trong khung, lề hai bên |
| cha đổi cỡ (CanvasScaler, safe area, xoay, kéo Game view) | `Update` thấy bề rộng (và chiều cao nếu có `height`) cha lệch → `Fit` | khối đúng cỡ ở frame kế |
| đổi cha | `OnTransformParentChanged` → `Fit` | đúng ngay, không chờ `Update` |
| cha chưa có cỡ, hay không phải `RectTransform` | thoát, không ghi, không log | khối giữ giá trị cũ |
| tắt component | gỡ driven | ô Width, Scale sửa tay được |
| đổi Game view trong Editor | giá trị driven không lưu | prefab không có diff |

### `UICoverFitter`

| Tình huống | Hệ làm gì | Người chơi thấy |
|---|---|---|
| khung cùng tỉ lệ ảnh chính | rect = cỡ ảnh, scale khít | nền kín, con trên đúng bóng vẽ sẵn |
| khung cao hơn ảnh (máy dài) | scale theo chiều cao, hai bên tràn | nền kín, cắt đều hai mép |
| khung thấp hơn ảnh | scale theo bề rộng, trên dưới tràn | nền kín, cắt trần và sàn |
| khung rộng hơn tỉ lệ ảnh chính (tablet), có ảnh tablet | `overrideSprite` = ảnh tablet, rect = cỡ ảnh tablet | ảnh rộng, không cắt trần sàn; con giữ chỗ |
| như trên, không có ảnh tablet | ảnh chính phóng theo bề rộng | trần và sàn bị cắt |
| khung hẹp lại | `overrideSprite` về null | ảnh chính |
| ảnh chính trống, cha chưa có cỡ | thoát, không ghi, không log | nền không hiện |
| rect bị ghi từ ngoài | `OnRectTransformDimensionsChange` → `Fit` | rect về cỡ ảnh |
| ảnh chính đổi lúc chạy, cha không đổi | không `Fit` | áp khi tắt bật component |
| tắt component, hay script mất | gỡ driven, `overrideSprite` null · script mất: scene nạp Scale 0 trong file | ảnh chính · nền không hiện, không lỗi đỏ |

## Flow

### `DesignWidthFitter`

```
  Unity ──OnEnable / Update / OnTransformParentChanged / OnDisable──► DesignWidthFitter              Runtime/Utilities/UI/DesignWidthFitter.cs
                                                                            │ Fit: đọc parent.rect (width, height)
                                                                            ▼
                                     RectTransform khối ◄── sizeDelta.x = width, localScale (driven)
                                     (chiều cao: ContentSizeFitter / anchor dọc / author đặt = height)
```

### `UICoverFitter`

```
  Unity ──OnEnable / Update / OnRectTransformDimensionsChange / OnTransformParentChanged / OnDisable──► UICoverFitter   Runtime/Utilities/UI/UICoverFitter.cs
                                                     ┌──────────────────────────────────────────────────────┤ Fit: đọc parent.rect.size, sprite.rect
                                                     ▼                                                      ▼
                                    Image: sprite (ảnh chính, author) · overrideSprite (tablet, driven)    RectTransform: sizeDelta = cỡ ảnh, localScale = phủ kín (driven)
                                                                                                             └── con: anchor giữa, toạ độ theo pixel ảnh, phóng cùng ảnh
```

## Thuật toán

| Hàm | Công thức | Ý nghĩa |
|---|---|---|
| khối, scale | `min(1, cha.x / width[, cha.y / height])` | vừa trục chật hơn, không phóng; canvas match height nên máy dài có `cha.x < width`, tablet có `cha.x > width` |
| nền, chọn ảnh | tablet khi `cha.x / cha.y > chính.x / chính.y` | khung rộng hơn ảnh chính: ảnh tablet cùng cao, rộng hơn nên scale nhỏ hơn, cắt ít hơn |
| nền, scale | `max(cha.x / ảnh.x, cha.y / ảnh.y)` | trục thiếu hơn khít, trục kia tràn, cắt đều hai mép (pivot giữa) |
| nền, rect | `sizeDelta = ảnh.rect.size` | con ở `(x, y)` pixel từ tâm ảnh nằm đúng pixel đó ở mọi scale |

---

## GĐ1 — xong thì khối 1080 trong khung 1000 thu còn 0.926, trong khung 1536 giữ 1

```
  Unity ──OnEnable+──► DesignWidthFitter+ ──đọc parent.rect.width──► cha
          OnDisable+        │ Fit+: sizeDelta.x = width, scale = min(1, cha / width), driven
                            ▼
                        RectTransform khối
```

`Runtime/Utilities/UI/DesignWidthFitter.cs` — file mới:

```csharp
using UnityEngine;

namespace Horcrux.Runtime.Utilities.UI
{
    /// <summary>
    /// Khối UI giữ đúng bề rộng thiết kế để art nằm đúng pixel như demo. Khung cha hẹp hơn thì thu nhỏ đều cả khối; rộng hơn
    /// (tablet) thì khối giữ cỡ, nằm theo anchor, hai bên là nền. Bề rộng và scale khai driven, chiều cao để layout lo; đổi Game
    /// view không làm bẩn prefab.
    /// </summary>
    // ExecuteAlways: kéo Game view ngoài Play phải Fit; UICapture.Canvas tắt bật component để Fit lại khi chụp.
    [ExecuteAlways]
    [RequireComponent(typeof(RectTransform))]
    public class DesignWidthFitter : MonoBehaviour
    {
        // Tên field là key YAML trong prefab, scene đã gắn: đổi tên là mất giá trị.
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

            // Tracker là struct, Add lặp thì cộng dồn: Clear trước giữ đúng một đăng ký.
            _tracker.Clear();
            // Driven: giá trị không lưu vào file, đổi Game view không tạo diff, Inspector khoá ô.
            _tracker.Add(this, _rect, DrivenTransformProperties.SizeDeltaX | DrivenTransformProperties.Scale);

            // Không phóng to khung rộng: art vỡ nét, lệch tỉ lệ phần còn lại; hai bên là nền.
            var scale = Mathf.Min(1f, area / width);
            // Chỉ ghi khi khác: ghi sizeDelta làm bẩn layout của cả cây.
            if (_rect.sizeDelta.x != width) _rect.sizeDelta = new Vector2(width, _rect.sizeDelta.y);
            if (_rect.localScale.x != scale) _rect.localScale = new Vector3(scale, scale, 1f);
        }

        #endregion
    }
}
```

Editor setup — trên object khối (cột, khung popup); nhãn theo Inspector:

| # | Thao tác | Thiếu thì hỏng ở đâu |
|---|---|---|
| 1 | Canvas gốc › **Canvas Scaler**: UI Scale Mode `Scale With Screen Size`, Reference Resolution bằng cỡ demo (1080×2340), Screen Match Mode `Match Width Or Height`, Match `1` (Height) | Match `0` (Width): canvas luôn rộng đúng reference, fitter không bao giờ thu — vô hại nhưng vô ích, máy dài thiếu chiều cao |
| 2 | Khối › **Rect Transform**: Anchors Min X = Max X = `0.5`, Pivot X `0.5` (neo giữa ngang); dọc tuỳ khối (cột Shop: Min Y = Max Y = `1`, Pivot Y `1`; khung popup: `0.5`, Pivot Y `1`) | anchor giãn ngang (Min X `0`, Max X `1`): Size Delta X là phần bù so với cha, fitter ghi `width` vào đó → khối rộng cha + `width` |
| 3 | Khối › **Design Width Fitter** › Width = Reference Resolution X (`1080`) | khác reference: scale 1 trên máy demo nhưng khối không đúng cỡ demo; `0`: khối rộng 0, scale 1 (chia cho 0 cho vô cực, `min` chặn 1) |
| 4 | Chiều cao: **Content Size Fitter** › Vertical Fit `Preferred Size`, **Horizontal Fit `Unconstrained`**; hoặc anchor dọc | Horizontal Fit khác `Unconstrained`: hai driver cùng ghi Size Delta X, bề rộng theo nội dung, không còn `width` |
| 5 | Cha là `RectTransform` (Viewport của ScrollRect, khung safe area) | cha không phải `RectTransform`: `Fit` thoát, không ghi gì, không log |
| 6 | Thứ tự thực thi: **không** đặt Script Execution Order | layout rebuild chạy ở cuối frame (`Canvas.willRenderCanvases`), sau mọi `Update`: bề rộng fitter ghi được LayoutGroup và ContentSizeFitter thấy trong cùng frame |
| 7 | Dấu hiệu đúng: Rect Transform hiện "Some values driven by DesignWidthFitter", ô Width và Scale mờ | không mờ: component tắt hoặc cha chưa có cỡ |

`Tests/EditMode/UIFitterTests.cs` (agent) — cha `new GameObject("parent", typeof(RectTransform))` với `sizeDelta` tường minh, con `AddComponent` (`OnEnable` chạy ngay nhờ `[ExecuteAlways]`); `width` đặt qua `SerializedObject` rồi tắt bật `enabled`, không mở API cho test:

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

---

## GĐ2 — xong thì khối đúng cỡ ở mọi frame, không chỉ frame bật

```
  Unity ──OnEnable / Update+ / OnTransformParentChanged+ / OnDisable──► DesignWidthFitter ──Fit khi cha / _lastParentWidth+ lệch──► RectTransform khối
```

`DesignWidthFitter.cs` › field › thêm sau `_rect`:

```csharp
        private RectTransform _rect;
        private float _lastParentWidth;
        private DrivenRectTransformTracker _tracker;
```

`DesignWidthFitter.cs` › `Unity Callbacks` › thêm sau `OnDisable`:

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

`DesignWidthFitter.cs` › `Fit` — cũ:

```csharp
            var area = parent.rect.width;
            if (area <= 0f) return;
```

Mới:

```csharp
            var area = parent.rect.width;
            // Ghi trước guard: cha rộng 0 không làm Update gọi Fit mỗi frame.
            _lastParentWidth = area;
            if (area <= 0f) return;
```

`Tests/EditMode/UIFitterTests.cs` (agent) — `Update` gọi qua reflection (`BindingFlags.NonPublic | Instance`), EditMode không chạy vòng `Update`:

| Case | Input | Kỳ vọng |
|---|---|---|
| cha đổi cỡ sau khi bật | cha 1080 → 900, gọi `Update` | scale 0.8333 |
| cha không đổi | gọi `Update` hai lần | giá trị giữ nguyên |
| frame đầu cha chưa có cỡ | cha 0 lúc bật, rồi 1080, gọi `Update` | `sizeDelta.x` 1080, scale 1, driven |
| đổi cha | `SetParent` sang cha rộng 1536 | scale 1 ngay, không chờ `Update` |
| cha về 0 giữa chừng | cha 1080 → 0, gọi `Update` | giá trị cũ giữ nguyên, không ném |

---

## GĐ2b — xong thì popup toàn màn trên tablet nằm trọn trong khung, khối chỉ có `width` chạy như GĐ2

```
  Unity ──Update (cha.x | cha.y+ lệch)──► DesignWidthFitter ──scale = min(1, cha.x / width, cha.y / height+)──► RectTransform khối
```

`DesignWidthFitter.cs` › field › thêm sau `width`:

```csharp
        [SerializeField] private float width = 1080f;
        [Tooltip("Chiều cao thiết kế: > 0 thì khối thu thêm cho vừa chiều cao cha (canvas thấp hơn trên tablet). 0: chỉ xét bề rộng.")]
        [SerializeField, Min(0f)] private float height;
```

và sau `_lastParentWidth`:

```csharp
        private float _lastParentWidth;
        private float _lastParentHeight;
```

`DesignWidthFitter.cs` › `Update` — cũ:

```csharp
            if (_rect != null && _rect.parent is RectTransform parent && parent.rect.width != _lastParentWidth)
                Fit();
```

Mới — chiều cao chỉ theo dõi khi khối có chiều cao thiết kế:

```csharp
            if (_rect != null && _rect.parent is RectTransform parent
                              && (parent.rect.width != _lastParentWidth || height > 0f && parent.rect.height != _lastParentHeight))
                Fit();
```

`DesignWidthFitter.cs` › `Fit` — hai chỗ thêm:

```csharp
            var area = parent.rect.width;
            // Ghi trước guard: cha rộng 0 không làm Update gọi Fit mỗi frame.
            _lastParentWidth = area;
            _lastParentHeight = parent.rect.height;
            if (area <= 0f) return;
```

```csharp
            // Không phóng to khung rộng: art vỡ nét, lệch tỉ lệ phần còn lại; hai bên là nền.
            var scale = Mathf.Min(1f, area / width);
            if (height > 0f && _lastParentHeight > 0f) scale = Mathf.Min(scale, _lastParentHeight / height);
```

Editor setup:

| # | Thao tác | Thiếu thì hỏng ở đâu |
|---|---|---|
| 1 | Khối popup toàn màn › Rect Transform neo giữa, Size Delta = `1080 × 2340` (chiều cao không driven, author đặt = Height) | khối không đúng cỡ demo |
| 2 | **Design Width Fitter** › Height `2340` | trên tablet khối tràn mép trên dưới, không log |

`Tests/EditMode/UIFitterTests.cs` (agent):

| Case | Input | Kỳ vọng |
|---|---|---|
| height 0 | `width 1080`, `height 0`, cha 1322×1762 | scale 1 (như GĐ2) |
| tablet | `height 2340`, cha 1322×1762 | scale 0.7530 |
| máy demo | `height 2340`, cha 1080×2340 | scale 1 |
| máy dài hẹp | `height 2340`, cha 1053×2340 | scale 0.975 (bề rộng chật hơn) |
| cha đổi chiều cao | cha 1322×2340 → 1322×1762, gọi `Update` | scale 1 → 0.7530 |

---

## GĐ3 — xong thì ảnh 1080×2340 trong khung 1080×2520 phóng 1.077, trong khung 1536×2048 đổi sang ảnh tablet

```
  Unity ──OnEnable+ / OnDisable+──► UICoverFitter+ ──đọc parent.rect.size, sprite.rect──► cha, Image
                                        │ Fit+: chọn ảnh, sizeDelta = cỡ ảnh, scale = max(...), driven
                                        ├──overrideSprite──► Image
                                        └──sizeDelta, localScale──► RectTransform ảnh ──► con theo pixel ảnh
```

`Runtime/Utilities/UI/UICoverFitter.cs` — file mới:

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
        // Tên field là key YAML trong scene đã gắn: đổi tên là mất giá trị.
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

            // overrideSprite, không ghi sprite: sprite là giá trị author; null trả về ảnh chính.
            // getter overrideSprite trả sprite đang hiển thị (override, không có thì sprite gốc)
            if (_image.overrideSprite != sprite) _image.overrideSprite = useTablet ? tablet : null;
            // Rect đúng cỡ ảnh, phóng bằng localScale: con đặt theo pixel ảnh phóng cùng ảnh.
            if (_rect.sizeDelta != size) _rect.sizeDelta = size;
            if (_rect.localScale.x != scale) _rect.localScale = new Vector3(scale, scale, 1f);
        }

        #endregion
    }
}
```

Editor setup — trên object ảnh nền; nhãn theo Inspector:

| # | Thao tác | Thiếu thì hỏng ở đâu |
|---|---|---|
| 1 | **Image** › Source Image = ảnh chính; Image Type `Simple` | ảnh chính trống: `Fit` thoát, không log, nền không hiện |
| 2 | **Rect Transform**: Anchors Min = Max = `(0.5, 0.5)`, Pivot `(0.5, 0.5)`, Pos X = Pos Y = `0`; Width, Height, Scale để fitter ghi | anchor giãn: Size Delta là phần bù, rect = cha + cỡ ảnh, ảnh méo; pivot lệch: phóng quanh pivot, ảnh lệch tâm khung |
| 3 | Cha phủ vùng cần che (khung stretch toàn trang, ra **ngoài** safe area nếu nền phải phủ tai thỏ) | cha trong safe area: nền dừng ở mép safe area, hở dải trên dưới |
| 4 | **UI Cover Fitter** › Tablet = ảnh chính nới hai bên, **cùng chiều cao, cùng tâm** (1080×2340 ↔ 1755×2340); hoặc trống | trống: tablet dùng ảnh chính phóng theo bề rộng, cắt trần và sàn; khác cao hay lệch tâm: con trôi khỏi chi tiết vẽ sẵn khi đổi ảnh |
| 5 | Hai texture › **Max Size** ≥ cạnh dài của ảnh (2340 → `4096`) | `2048`: sprite co còn 945×2048, rect theo cỡ đã co, con đặt theo pixel ảnh gốc lệch ~12% |
| 6 | Con (bục, khay, chữ): Anchors `(0.5, 0.5)`, Pos = pixel từ tâm ảnh; là con của ảnh, **không** neo theo safe area | neo safe area: máy tai thỏ dời con khỏi bóng vẽ sẵn trên nền |
| 7 | Thứ tự thực thi: không đặt Script Execution Order | không có layout nào đọc rect ảnh nền; con đọc qua transform ở frame vẽ |
| 8 | Dấu hiệu đúng: Rect Transform hiện "Some values driven by UICoverFitter"; file scene lưu Scale 0, Size Delta 0 là bình thường | component tắt hay script mất: scene nạp giá trị trong file (scale 0) → nền không hiện, không lỗi đỏ — kiểm bằng `PrefabAudit.Report` (`missing script`) |

`Tests/EditMode/UIFitterTests.cs` (agent) — sprite `Sprite.Create` từ `Texture2D` đúng cỡ (hay 1/10 cỡ, scale kỳ vọng không đổi); `tablet` qua `SerializedObject` rồi tắt bật `enabled`:

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

---

## GĐ4 — xong thì ảnh nền đúng ở mọi frame, đổi ảnh theo tỉ lệ mới và về ảnh chính khi khung hẹp lại

```
  Unity ──OnEnable / Update+ / OnRectTransformDimensionsChange+ / OnTransformParentChanged+ / OnDisable──► UICoverFitter ──Fit khi cha / _lastParentSize+ lệch──► Image, RectTransform
```

`UICoverFitter.cs` › field › thêm sau `_image`:

```csharp
        private Image _image;
        private Vector2 _lastParentSize;
        private DrivenRectTransformTracker _tracker;
```

`UICoverFitter.cs` › `Unity Callbacks` › thêm sau `OnDisable`:

```csharp
        // Giả định (cần xác nhận): bắt ca Update không bắt. Fit tự ghi gọi lại đây thì đã khớp, dừng.
        private void OnRectTransformDimensionsChange() => Fit();

        private void OnTransformParentChanged() => Fit();

        private void Update()
        {
            // khung cha đổi kích thước không gọi OnRectTransformDimensionsChange của con có anchor giữa
            if (_rect != null && _rect.parent is RectTransform parent && parent.rect.size != _lastParentSize)
                Fit();
        }
```

`UICoverFitter.cs` › `Fit` — cũ:

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

`Tests/EditMode/UIFitterTests.cs` (agent) — `Update` qua reflection như GĐ2:

| Case | Input | Kỳ vọng |
|---|---|---|
| máy dài sau khi bật | cha 1080×2340 → 1080×2520, gọi `Update` | scale 1.0769 |
| sang tablet rồi về | cha → 1536×2048, `Update`; → 1080×2340, `Update` | tablet, scale 0.8752; rồi `overrideSprite` null, scale 1 |
| cha không đổi | `Update` hai lần | giá trị giữ nguyên |
| frame đầu cha 0 | cha 0×0 lúc bật, rồi 1080×2340, `Update` | scale 1, driven |
| ảnh chính trống rồi có | `sprite` null lúc bật, gán ảnh, `Update` (cha không đổi) | vẫn không `Fit` — tắt bật mới áp |
| đổi cha | `SetParent` sang cha 1536×2048 | tablet ngay, không chờ `Update` |
| rect bị ghi ngoài | đặt tay `sizeDelta` 10×10 khi đang bật | `OnRectTransformDimensionsChange` → `Fit` trả về cỡ ảnh |

---

## Chuyển dự án (agent)

| Chỗ | Đổi |
|---|---|
| `docs/Runtime/UIFitters/UIFitters.md`, `.html` | tạo một cặp cho hai component, `Code: Runtime/Utilities/UI/`: Flow A, B · Thuật toán · bảng field `Width`, `Height`, `Tablet` · "Trước khi chạy" = Editor setup GĐ1, GĐ2b, GĐ3 · Không làm · bẫy: đổi `sprite` lúc chạy không tự `Fit`, file lưu Scale 0 là bình thường, Max Size texture · hợp đồng với `UICapture.Canvas` (giữ `[ExecuteAlways]`, `OnEnable → Fit`) |
| bản tạm `Bridge/DesignWidthFitter.cs`, `Bridge/Menu/HomeBackgroundFitter.cs` (+ `.meta`) | xoá, cùng lần Refresh với hàng dưới (Editor đóng) để prefab không lưu trạng thái `missing` |
| `DesignWidthFitter.cs.meta`, `UICoverFitter.cs.meta` | `guid:` ← `36b62d2860b4cfb44907c8900a9178c9`, `8e63ae2e0a997f64db2c23a1da699cf2` (GUID bản tạm) |
| `ShopPanel.prefab` › `Viewport/Content`, root `PopupFrame.prefab`, `Menu.unity` › `bg_room` | không sửa: field giữ tên nên YAML `width: 1080`, `tablet: {…}` nối lại; mở Editor thấy `Design Width Fitter` Width 1080, `UI Cover Fitter` Tablet `bg_ipad`; không `.cs` nào gọi tên hai type |
| `Bridge/Bridge.md` | bảng file bỏ hàng `DesignWidthFitter`; hàng `Menu/*` bỏ "(bản tạm, lên Horcrux …)", gọi `UICoverFitter`; mục Reskin, cây Home (`bg_room [UICoverFitter]`), mục Popup, cây Shop đổi tên, trỏ `Horcrux/Runtime/Utilities/UI/` |
| `Structure.md` | ba dòng nêu `DesignWidthFitter`, `HomeBackgroundFitter`: đổi tên, ghi là component Horcrux |
| kiểm | grep `HomeBackgroundFitter`, `CategoryJam.Bridge.DesignWidthFitter` trong `.cs`, `.prefab`, `.unity`, `.md` = 0 · hai GUID chỉ ở hai `.meta` Horcrux và ba asset chủ · `PrefabAudit.Report("Assets/_CategoryJam/Bridge")` không `missing script` · `UICapture.Canvas(CanvasMenu, 1080, 2340, …)` và `(1536, 2048)` trước–sau trùng byte |
| Plan này | xoá cùng `.meta` |
| chơi thử | Game view 1080×2340 rồi 1536×2048, `Menu` → Home: nền phủ tới mép tai thỏ, tablet hiện ảnh rộng không cắt trần sàn, bục và khay đúng bóng vẽ sẵn → tab Shop: cột rộng 1080 ở máy demo, thu đều ở máy dài, giữa hai dải nền ở tablet → Pause hay Setting: khung popup cùng luật; hỏng: nền Home không hiện (scale 0 từ file) · cột Shop tràn mép, khung popup rộng hơn màn · bục lệch bóng · tablet cắt mép trên dưới |

## Không làm · Mở rộng sau

| | Phương án | Vì sao |
|---|---|---|
| bỏ | base class chung hai fitter | khác lý do thay đổi; phần chung chỉ là tracker và theo dõi cỡ cha, vài dòng |
| bỏ | ghi thẳng `sizeDelta`, `localScale`, không driven | mỗi lần đổi Game view là một diff trên prefab |
| bỏ | guard `width ≤ 0` | khối rộng 0 lộ ngay trong Editor; code canh giấu bước setup |
| bỏ | `UIBehaviour` nhận `OnRectTransformDimensionsChange` của cha | không có callback cỡ cha tới con neo giữa |
| bỏ | `Canvas.preWillRenderCanvases` thay `Update` | cũng chạy mỗi frame, khó đọc hơn; so một số không cấp phát |
| bỏ | component riêng cho khối có chiều cao | cùng lý do thay đổi; `height` 0 giữ hành vi cũ, prefab đã gắn không đổi |
| bỏ | neo từng phần tử popup theo % chiều cao | khe co còn phần tử giữ cỡ; quầng sáng sau icon đè tiêu đề trên tablet |
| bỏ | `AspectRatioFitter` cho ảnh nền | giãn rect nên con đặt theo pixel ảnh trôi |
| bỏ | `CanvasScaler` thay fitter | scale cả canvas, không riêng một khối |
| bỏ | kiểm trong code ảnh tablet cùng cao, cùng tâm | điều kiện của art; ghi ở XML doc, Editor setup và tài liệu |
| bỏ | log khi ảnh chính trống | nền không hiện là lỗi setup lộ ngay |
| bỏ | tên `HomeBackgroundFitter` (bản dự án) | "Home" là người dùng đầu; "cover" là cơ chế, "UI" phân biệt với `ViewportCover` cho sprite world |
| sau | hơn hai ảnh: mảng sprite kèm tỉ lệ ngưỡng, `tablet` giữ nghĩa phần tử đầu | khi có người dùng |
| sau | `Fit` khi ảnh chính đổi lúc chạy mà cha không đổi: thêm hàm gọi lại `Fit`, không đổi chữ ký | khi có người dùng |
