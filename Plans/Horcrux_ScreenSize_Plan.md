# Plan — `ScreenSizeExtensions`: bề ngang pixel của rect, scale để rect có bề ngang pixel cho trước

Ngày 2026-11-23. Nguồn: category-jam `Assets/_CategoryJam/Bridge/PopupBoosterUnlock.cs` (`OnClickShowMe` đọc bề ngang pixel của
icon popup), `Bridge/BoosterUnlockTutorial.cs` (`FlyIconAsync` tính scale để icon bay rộng đúng bằng icon popup lúc rời và icon nút
lúc chạm). Hai công thức chép tay `rect.width × lossyScale.x` và `W ÷ (rect.width × parent.lossyScale.x)` — chỉ đúng trên canvas
Overlay không xoay.

**Phân công:** GĐ1 developer gõ, agent viết test theo bảng case rồi compile · GĐ2 (tài liệu, dự án dùng bản Horcrux) agent làm.
**Chạy được, thấy được** ở GĐ1 = compile sạch + bảng case xanh trong `Tests/PlayMode/ScreenSizeExtensionsTests.cs` (cần canvas, camera);
thấy trong game ở GĐ2. GĐ1 commit trong `Assets/Horcrux`, GĐ2 commit ở dự án kèm con trỏ submodule. Không phụ thuộc Plan nào. Không có
`§0`: một phép chiếu, ghi ở GĐ1.

## Ngữ cảnh đã chốt

- **Người dùng:** icon bay giữa hai canvas (popup → nút HUD, phần thưởng → thanh tiền tệ) cần khớp cỡ pixel hai đầu · bất kỳ chỗ nào hỏi
  "rect này rộng bao nhiêu pixel trên màn hình".
- **Mục tiêu:** hai hàm thuần, tên nói câu hỏi; đúng cả canvas Overlay lẫn canvas Camera; dự án đổi hai chỗ chép tay sang, cảm giác
  không đổi (lệch dưới 1e-3 px).
- **Chỗ đặt:** `Runtime/Utilities/ExtensionMethods/ScreenSizeExtensions.cs`, namespace `Horcrux.Runtime.Utilities.ExtensionMethods`.
- **Tên:** `ScreenSizeExtensions`, `ScreenWidth`, `LocalScaleForScreenWidth` đổi tuỳ ý trước khi gõ.
- **Không làm:** bề ngang pixel của một đoạn world (`camera.WorldWidthOnScreen`) — người dùng duy nhất là `CacheTileSlots`, và
  `Horcrux_UIProxyFlight_Plan.md` đưa phép quy đổi đó vào trong `UIProxyFlight.FlyAsync`. **Mở rộng sau:** người dùng thứ hai ngoài
  `UIProxyFlight` xuất hiện thì thêm `WorldWidthOnScreen(this Camera, Vector3 center, float worldWidth)` vào file này và để `FlyAsync`
  gọi nó · chiều cao, khung hai chiều (đã có `TutorialAnchor.GetScreenFrame` ở dự án, chưa người dùng thứ hai).
- **Đã khảo sát:**

| Nguồn | Lấy gì | Không lấy, vì sao |
|---|---|---|
| `PopupBoosterUnlock.OnClickShowMe` | câu hỏi "bề ngang pixel" | `rect.width * lossyScale.x` chỉ đúng trên Overlay không xoay |
| `BoosterUnlockTutorial.FlyIconAsync` | câu hỏi "scale nào cho bề ngang pixel W"; scale lúc tính đang là 0 (icon chờ nở) | tỉ số theo scale hiện tại — chia 0 |
| dự án `TutorialAnchor.GetScreenFrame` | `RectTransformUtility.WorldToScreenPoint(camera, …)`, camera `null` cho Overlay | khung hai chiều có bo góc: câu hỏi khác |
| Horcrux `ExtensionMethods/` | chỗ đặt | chưa có hàm cỡ màn hình |
| `Horcrux_UIProxyFlight_Plan.md` | ranh giới: quy đổi world → pixel nằm ở `FlyAsync` | — |

## Luồng mô tả

```
caller có rect trên một canvas (camera của canvas, null cho Overlay)
  rect.ScreenWidth(cam) ──► bề ngang pixel ở scale hiện tại                       GĐ1
  rect.LocalScaleForScreenWidth(W, cam) ──► scale đều để rect rộng W pixel        GĐ1
dự án: icon popup đọc bề ngang · icon bay tính scale hai đầu ─────────────────────── GĐ2 (agent)
```

## Bản đồ hệ cuối

```
  caller ──ScreenWidth / LocalScaleForScreenWidth──► ScreenSizeExtensions (static)
                                                        └─ SpanOnScreen ──► RectTransformUtility.WorldToScreenPoint
```

---

## Giai đoạn 1 — Hai hàm

**Mục tiêu:** bề ngang pixel của rect ở scale hiện tại; scale cục bộ đều để rect rộng `W` pixel, không đọc scale hiện tại.

**Vì sao bây giờ:** giai đoạn duy nhất của Horcrux.

**Ai gọi:** test; trong dự án `PopupBoosterUnlock.OnClickShowMe` (`ScreenWidth`), `BoosterUnlockTutorial.FlyIconAsync` (`ScreenWidth`,
`LocalScaleForScreenWidth`) (GĐ2).

**Toán → code:** bề ngang trên màn hình của đoạn `[p, p + v]` là `screen(p + v).x − screen(p).x`, `screen` là phép chiếu của camera
canvas (Overlay: lấy x, y của điểm). `ScreenWidth`: `v` = cạnh ngang `rect.width` qua ma trận của **chính rect** (`TransformVector`) —
gồm scale của rect và mọi cha. `LocalScaleForScreenWidth`: `v` qua ma trận của **cha** (cỡ rect ở scale 1), `scale = W ÷ bề ngang của v`.

### `Runtime/Utilities/ExtensionMethods/ScreenSizeExtensions.cs` — file mới

```csharp
using UnityEngine;

namespace Horcrux.Runtime.Utilities.ExtensionMethods
{
    /// <summary>Bề ngang pixel của rect trên màn hình. canvasCamera là camera của canvas chứa rect; null cho Screen Space Overlay.</summary>
    public static class ScreenSizeExtensions
    {
        /// <summary>Bề ngang pixel của rect ở scale hiện tại.</summary>
        public static float ScreenWidth(this RectTransform rectTransform, Camera canvasCamera)
            => SpanOnScreen(rectTransform.position, rectTransform.TransformVector(rectTransform.rect.width, 0f, 0f), canvasCamera);

        /// <summary>Scale cục bộ đều để rect rộng screenWidth pixel; không đọc scale hiện tại (đang 0 lúc chờ nở vẫn đúng).</summary>
        public static float LocalScaleForScreenWidth(this RectTransform rectTransform, float screenWidth, Camera canvasCamera)
        {
            var parent = rectTransform.parent;
            var unscaledSpan = parent != null
                ? parent.TransformVector(rectTransform.rect.width, 0f, 0f)
                : new Vector3(rectTransform.rect.width, 0f, 0f);
            return screenWidth / SpanOnScreen(rectTransform.position, unscaledSpan, canvasCamera);
        }

        private static float SpanOnScreen(Vector3 start, Vector3 span, Camera canvasCamera)
            => RectTransformUtility.WorldToScreenPoint(canvasCamera, start + span).x
               - RectTransformUtility.WorldToScreenPoint(canvasCamera, start).x;
    }
}
```

**Khác với bản dự án**

| Chỗ | Bản dự án | Bản Horcrux | Vì |
|---|---|---|---|
| bề ngang pixel | `rect.width * lossyScale.x` | `ScreenWidth(rect, null)` | bản dự án chỉ đúng trên Overlay không xoay; bản Horcrux đi qua phép chiếu của canvas. Trên Overlay lệch dưới 1e-3 px do làm tròn float |
| scale cho bề ngang `W` | `W / (rect.width * parent.lossyScale.x)` | `LocalScaleForScreenWidth(rect, W, null)` | như trên |

**Quyết định** — `canvasCamera` bắt buộc, không mặc định `null`: quên camera của canvas Camera là sai lệch im lặng; caller Overlay viết
`null` là đọc được ngay giả định của mình. — `LocalScaleForScreenWidth` đọc ma trận của **cha**: caller hay đặt scale 0 trước khi nở,
tỉ số theo scale hiện tại chia 0. Phương án bỏ: `localScale.x × W ÷ ScreenWidth` — sai đúng ở ca hay gặp nhất. — Bề ngang đo theo trục
phải của rect (`TransformVector` theo cạnh ngang): rect xoay thì kết quả là hình chiếu ngang của cạnh đó, đúng thứ caller đặt cạnh nhau.

| Consumes | Produces |
|---|---|
| `RectTransformUtility.WorldToScreenPoint`, `Transform.TransformVector` | `ScreenWidth`, `LocalScaleForScreenWidth` |

**Sơ đồ tương tác sau GĐ1** (`+` là phần mới)

```
  caller ──ScreenWidth+ / LocalScaleForScreenWidth+──► SpanOnScreen+ ──► RectTransformUtility.WorldToScreenPoint
```

**Editor setup:** tạo file trong Project window (để có `.meta`).

**Kiểm:** compile sạch. `Tests/PlayMode/ScreenSizeExtensionsTests.cs` (agent):

| Case | Input | Kỳ vọng |
|---|---|---|
| Overlay | rect 100, scale cha 2, scale riêng 1.5, `ScreenWidth(rect, null)` | 300 (±1e-3) |
| Overlay, scale 0 | rect 100, scale cha 2, scale riêng 0, `LocalScaleForScreenWidth(rect, 300, null)` | 1.5 |
| không cha | rect gốc 100, `LocalScaleForScreenWidth(rect, 50, null)` | 0.5 |
| khứ hồi | `localScale = LocalScaleForScreenWidth(rect, W, null)` rồi `ScreenWidth(rect, null)` | W (±1e-3), W ∈ {1, 57.3, 400} |
| canvas Camera | canvas `ScreenSpaceCamera`, camera ortho size 5, rect 100 ở `planeDistance` 10 | `ScreenWidth(rect, cam)` bằng `cam.WorldToScreenPoint` hai mép (±1e-3); `ScreenWidth(rect, null)` khác — đối chứng tham số camera có tác dụng |
| xoay 90° | rect xoay z 90° | ≈ 0 (cạnh ngang đứng dọc) |
| bằng bản dự án | Overlay không xoay, 50 bộ (rect, scale cha, scale riêng) seed cố định | lệch với `rect.width * lossyScale.x` < 1e-3 |

**Mốc commit:** `feat(ui): ScreenSizeExtensions bề ngang pixel của rect`.

---

## Giai đoạn 2 — Tài liệu, dự án dùng bản Horcrux (agent)

| File | Cũ | Mới |
|---|---|---|
| `Bridge/PopupBoosterUnlock.cs` › `OnClickShowMe` | `ShowMeIconWidth = iconRect.rect.width * iconRect.lossyScale.x;` | `ShowMeIconWidth = iconRect.ScreenWidth(null);` (popup ở canvas Overlay); `using Horcrux.Runtime.Utilities.ExtensionMethods;` |
| `Bridge/BoosterUnlockTutorial.cs` › `FlyIconAsync` | `var parentScale = flyer.parent.lossyScale.x;` · `var fromScale = _flyFromWidth / (flyer.rect.width * parentScale);` · `var toScale = target.rect.width * target.lossyScale.x / (flyer.rect.width * parentScale);` | `var fromScale = flyer.LocalScaleForScreenWidth(_flyFromWidth, null);` · `var toScale = flyer.LocalScaleForScreenWidth(target.ScreenWidth(null), null);`; comment "Canvas Overlay: toạ độ thế giới là pixel màn…" thành "Canvas Overlay: camera null." |
| `docs/Runtime/ScreenSize/ScreenSize.md`, `.html` | — | mở đầu `Code: Runtime/Utilities/ExtensionMethods`; hai câu hỏi → hai hàm · `canvasCamera` null cho Overlay · vì sao đọc ma trận cha · lưu ý rect xoay |

**Kiểm:** grep `lossyScale.x / (flyer`, `iconRect.lossyScale.x` trong `Assets/_CategoryJam` = 0 · compile dự án sạch, EditMode dự án và
Horcrux xanh · **cần chơi thử** (developer): vào level mở khoá booster, bấm Show me!. Nhìn: icon hiện lại đúng cỡ ảnh trên popup, bay về
nút và chạm đúng cỡ icon nút. Dấu hiệu hỏng: icon phình hay co lúc rời popup hoặc lúc chạm nút.

**Mốc commit:** Horcrux `docs(ui): ScreenSize.md, .html` · dự án `refactor(tutorial): cỡ icon bay qua ScreenSizeExtensions của Horcrux`
kèm con trỏ submodule.

## File → giai đoạn

| File | GĐ1 | GĐ2 |
|---|---|---|
| `Runtime/Utilities/ExtensionMethods/ScreenSizeExtensions.cs` | tạo | |
| `Tests/PlayMode/ScreenSizeExtensionsTests.cs` (agent) | tạo | |
| `docs/Runtime/ScreenSize/` (agent) | | tạo |
| dự án: `PopupBoosterUnlock.cs`, `BoosterUnlockTutorial.cs` | | đổi |
