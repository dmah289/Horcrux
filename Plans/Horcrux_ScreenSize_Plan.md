# Plan — `ScreenSizeExtensions`: bề ngang pixel của rect, scale để rect có bề ngang pixel cho trước

Ngày 2026-11-23. Nguồn: category-jam `Assets/_CategoryJam/Bridge/PopupBoosterUnlock.cs` (`OnClickShowMe` đọc bề ngang pixel của icon
popup), `Bridge/BoosterUnlockTutorial.cs` (`FlyIconAsync` tính scale để icon bay rộng đúng bằng icon popup lúc rời và icon nút lúc chạm);
đã chơi thử. Hai công thức chép tay `rect.width × lossyScale.x` và `W ÷ (rect.width × parent.lossyScale.x)` chỉ đúng trên canvas Overlay
không xoay; bản Horcrux đi qua phép chiếu của camera canvas.

**Phân công:** GĐ1 developer gõ, agent viết test theo bảng case rồi compile · GĐ2 (tài liệu) · GĐ3 (dự án chuyển sang bản Horcrux, xoá
Plan) agent làm.
**Chạy được, thấy được:** GĐ1 compile sạch + bảng case xanh trong `Tests/PlayMode/ScreenSizeExtensionsTests.cs` (cần canvas, camera;
Test Runner, PlayMode); thấy trong game ở GĐ3.
**Lệnh kiểm** (chạy từ gốc dự án): GĐ1–GĐ2 `bash "<horcrux>/Editor/CompileCheck/Tools~/verify.sh" --csproj
com.horcrux.runtime.csproj,Horcrux.PlayModeTests.csproj --tests Horcrux.EditModeTests` · GĐ3 `--csproj
com.horcrux.runtime.csproj,Assembly-CSharp.csproj --tests CategoryJam.EditModeTests,Horcrux.EditModeTests` (`Bridge/` không có asmdef,
nằm trong `Assembly-CSharp`). PlayMode không qua tool: chạy trong Window › General › Test Runner.
**Commit:** GĐ1, GĐ2 trong `Assets/Horcrux`; GĐ3 ở dự án kèm con trỏ submodule.
**Phụ thuộc:** không.
**Đã kiểm trước khi ghi:** csc của Rider, `-langversion:9`, trên `UnityEngine.CoreModule`/`UIModule` của Editor 6000.0.73f1 và
`Library/ScriptAssemblies`: `ScreenSizeExtensions.cs` và ba dòng mới của GĐ3 (bọc trong một class thử) — 0 lỗi. Bản mẫu: chạy hai hàm
bằng eval trong Editor trên `RectTransform` tạm (`HideAndDontSave`, xoá sau lượt): Overlay rect 100, cha 2, riêng 1.5 → 300 · scale riêng
0 → `LocalScaleForScreenWidth(300)` = 1.5 · khứ hồi 3 lượt (W = 1, 57.3, 400) lệch tối đa 3.8e-6 px · đối chứng với công thức chép tay
50 bộ seed 12345 (rect 10–510, cha 0.2–3.2, riêng 0.1–3.1, vị trí ngẫu nhiên) lệch tối đa 2.7e-4 px · xoay 90° → 0 · camera ortho size 5
(màn 640×480) cha 0.01: `ScreenWidth(rect, cam)` = 48 bằng hiệu hai mép qua `cam.WorldToScreenPoint`, còn `ScreenWidth(rect, null)` = 1.
`§0`: không — một phép chiếu, ghi ở GĐ1.

## Ngữ cảnh đã chốt

- **Người dùng:** icon bay giữa hai canvas (popup → nút HUD, phần thưởng → thanh tiền tệ) cần khớp cỡ pixel hai đầu · bất kỳ chỗ nào hỏi
  "rect này rộng bao nhiêu pixel trên màn hình".
- **Mục tiêu:** hai hàm thuần, tên nói câu hỏi; đúng cả canvas Overlay lẫn canvas Camera; dự án đổi hai chỗ chép tay sang, cảm giác
  không đổi (lệch dưới 1e-3 px).
- **Ranh giới:** đo rect trên canvas. Bề ngang pixel của một đoạn world là việc của `Horcrux_UIProxyFlight_Plan.md` (`UIProxyFlight.FlyAsync`
  tự quy đổi, một người dùng).
- **Chỗ đặt, tên:** `Runtime/Utilities/ExtensionMethods/ScreenSizeExtensions.cs`, namespace `Horcrux.Runtime.Utilities.ExtensionMethods`.
  `ScreenSizeExtensions`, `ScreenWidth`, `LocalScaleForScreenWidth` — tên chốt khi gõ.
- **Cố ý KHÔNG làm, kèm lý do:**
  - `WorldWidthOnScreen(this Camera, …)` (bề ngang pixel của một đoạn world) — người dùng duy nhất là `UIProxyFlight.FlyAsync`, tự quy đổi
    bằng ba dòng.
  - nhánh rect không có cha trong `LocalScaleForScreenWidth` — rect trong canvas luôn có cha; rect gốc là chính canvas, scale do
    `CanvasScaler` đặt.
  - chiều cao, khung hai chiều — chưa người dùng thứ hai (khung có bo góc đã có ở `TutorialAnchor.GetScreenFrame`: ở category-jam, hoặc ở
    Horcrux nếu `Horcrux_Tutorial_Plan.md` GĐ1 đã gõ; câu hỏi khác).
- **Quyết định trái trực giác:**
  - **`canvasCamera` bắt buộc, không mặc định `null`:** quên camera của canvas Camera là sai lệch im lặng; caller Overlay viết `null` là
    đọc được ngay giả định của mình.
  - **`LocalScaleForScreenWidth` đọc ma trận của cha, không đọc scale hiện tại:** caller hay đặt scale 0 trước khi nở; tỉ số theo scale
    hiện tại chia 0.
  - **Bề ngang đo theo trục phải của rect:** rect xoay thì kết quả là hình chiếu ngang của cạnh đó (xoay 90° ra 0), đúng thứ caller đặt
    cạnh nhau.
- **Mở rộng sau:** `WorldWidthOnScreen(this Camera, Vector3 center, float worldWidth)` vào file này khi có người dùng thứ hai ngoài
  `UIProxyFlight`, và `FlyAsync` gọi nó · nhánh rect không cha khi có caller đo rect gốc · chiều cao, khung hai chiều.

## Đã khảo sát

| Nguồn | Lấy gì | Không lấy, vì sao |
|---|---|---|
| category-jam `PopupBoosterUnlock.OnClickShowMe` | câu hỏi "bề ngang pixel" | `rect.width * lossyScale.x` chỉ đúng trên Overlay không xoay |
| category-jam `BoosterUnlockTutorial.FlyIconAsync` | câu hỏi "scale nào cho bề ngang pixel W"; scale lúc tính đang là 0 (icon chờ nở) | tỉ số theo scale hiện tại — chia 0 |
| `TutorialAnchor.GetScreenFrame` (category-jam; Horcrux sau Plan Tutorial GĐ1) | `RectTransformUtility.WorldToScreenPoint(camera, …)`, camera `null` cho Overlay | khung hai chiều có bo góc: câu hỏi khác |
| Horcrux `ExtensionMethods/` | chỗ đặt | chưa có hàm cỡ màn hình |
| `Horcrux_UIProxyFlight_Plan.md` | ranh giới: quy đổi world → pixel nằm ở `FlyAsync`; cùng khái niệm "bề ngang trên màn hình" | phép đo đoạn world, một người dùng |

## Luồng

```
caller có rect trên một canvas (camera của canvas, null cho Overlay)
  rect.ScreenWidth(cam) ──► bề ngang pixel ở scale hiện tại                       GĐ1
  rect.LocalScaleForScreenWidth(W, cam) ──► scale đều để rect rộng W pixel        GĐ1
dự án: icon popup đọc bề ngang · icon bay tính scale hai đầu ─────────────────────── GĐ3 (agent)
```

## Bản đồ hệ cuối

```
  caller ──ScreenWidth / LocalScaleForScreenWidth──► ScreenSizeExtensions (static)
                                                        └─ SpanOnScreen ──► RectTransformUtility.WorldToScreenPoint
```

---

## GĐ1 — Hai hàm

**Mục tiêu:** bề ngang pixel của rect ở scale hiện tại; scale cục bộ đều để rect rộng `W` pixel, không đọc scale hiện tại. Xong GĐ thì
caller hỏi được cỡ pixel trên cả canvas Overlay lẫn Camera.

**Vì sao bây giờ:** toàn bộ thay đổi runtime.

**Ai gọi:** test; trong dự án `PopupBoosterUnlock.OnClickShowMe` (`ScreenWidth`), `BoosterUnlockTutorial.FlyIconAsync` (`ScreenWidth`,
`LocalScaleForScreenWidth`) (GĐ3).

**Sơ đồ sau GĐ1** (`+` là phần mới)

```
  caller ──ScreenWidth+ / LocalScaleForScreenWidth+──► SpanOnScreen+ ──► RectTransformUtility.WorldToScreenPoint
```

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

        /// <summary>Scale cục bộ đều để rect rộng screenWidth pixel; không đọc scale hiện tại (đang 0 lúc chờ nở vẫn đúng). Rect phải có cha.</summary>
        public static float LocalScaleForScreenWidth(this RectTransform rectTransform, float screenWidth, Camera canvasCamera)
        {
            var unscaledSpan = rectTransform.parent.TransformVector(rectTransform.rect.width, 0f, 0f);
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
| bề ngang pixel | `rect.width * lossyScale.x` | `ScreenWidth(rect, null)` | bản dự án chỉ đúng trên Overlay không xoay; bản Horcrux đi qua phép chiếu của canvas. Trên Overlay lệch dưới 1e-3 px do làm tròn float (bản mẫu: 2.7e-4) |
| scale cho bề ngang `W` | `W / (rect.width * parent.lossyScale.x)` | `LocalScaleForScreenWidth(rect, W, null)` | như trên |

| Consumes | Produces |
|---|---|
| `RectTransformUtility.WorldToScreenPoint`, `Transform.TransformVector` | `ScreenWidth`, `LocalScaleForScreenWidth` |

**Quyết định** — ba quyết định trái trực giác ở Ngữ cảnh đã chốt. Phương án bỏ: `localScale.x × W ÷ ScreenWidth` — sai đúng ở ca hay gặp
nhất (scale đang 0). — Rect không có cha thì `NullReferenceException` ở `parent`: lỗi setup lộ ngay, không cần guard.

**Editor setup:** không (file tạo trong Project window để có `.meta`).

**Kiểm:** lệnh kiểm GĐ1–GĐ2. `Tests/PlayMode/ScreenSizeExtensionsTests.cs` (agent):

| Case | Input | Kỳ vọng |
|---|---|---|
| Overlay | rect 100, scale cha 2, scale riêng 1.5, `ScreenWidth(rect, null)` | 300 (±1e-3) |
| Overlay, scale 0 | rect 100, scale cha 2, scale riêng 0, `LocalScaleForScreenWidth(rect, 300, null)` | 1.5 — đỏ nếu tính theo scale hiện tại |
| khứ hồi | `localScale = LocalScaleForScreenWidth(rect, W, null)` rồi `ScreenWidth(rect, null)` | W (±1e-3), W ∈ {1, 57.3, 400} |
| canvas Camera | canvas `ScreenSpaceCamera`, camera ortho size 5, rect 100 ở `planeDistance` 10 | `ScreenWidth(rect, cam)` bằng `cam.WorldToScreenPoint` hai mép (±1e-3); `ScreenWidth(rect, null)` khác — đối chứng tham số camera có tác dụng |
| xoay 90° | rect xoay z 90° | ≈ 0 (cạnh ngang đứng dọc) |
| bằng bản dự án | Overlay không xoay, 50 bộ (rect, scale cha, scale riêng) seed cố định | lệch với `rect.width * lossyScale.x` < 1e-3 |

**Mốc commit:** `feat(ui): ScreenSizeExtensions bề ngang pixel của rect`.

---

## GĐ2 — Tài liệu (agent)

**Mục tiêu:** người dùng hệ chọn đúng hàm và đúng camera mà không đọc code.

`docs/Runtime/ScreenSize/ScreenSize.md`, `.html` — tạo. `.md` mở đầu `Code: Runtime/Utilities/ExtensionMethods/ScreenSizeExtensions.cs`;
hai câu hỏi → hai hàm · `canvasCamera` null cho Overlay, camera của canvas cho Screen Space Camera · vì sao đọc ma trận cha (scale 0) ·
lưu ý rect xoay · ranh giới với `UIProxyFlight` (đoạn world). `.html` cùng nội dung cho developer, đồng bộ theo `.md`.

**Editor setup:** không. **Kiểm:** hai file có đủ mục trên; tên API khớp code đã gõ.

**Mốc commit:** `docs(ui): ScreenSize.md, .html`.

---

## GĐ3 — category-jam dùng bản Horcrux, xoá Plan (agent)

**Mục tiêu:** hai chỗ chép tay gọi hàm Horcrux; cảm giác không đổi.

| File | Cũ | Mới |
|---|---|---|
| `Assets/_CategoryJam/Bridge/PopupBoosterUnlock.cs` › `OnClickShowMe` | `ShowMeIconWidth = iconRect.rect.width * iconRect.lossyScale.x;` | `ShowMeIconWidth = iconRect.ScreenWidth(null);` (popup ở canvas Overlay) |
| `PopupBoosterUnlock.cs` › using | — | `using Horcrux.Runtime.Utilities.ExtensionMethods;` |
| `Assets/_CategoryJam/Bridge/BoosterUnlockTutorial.cs` › `FlyIconAsync` | `var parentScale = flyer.parent.lossyScale.x;` · `var fromScale = _flyFromWidth / (flyer.rect.width * parentScale);` · `var toScale = target.rect.width * target.lossyScale.x / (flyer.rect.width * parentScale);` | `var fromScale = flyer.LocalScaleForScreenWidth(_flyFromWidth, null);` · `var toScale = flyer.LocalScaleForScreenWidth(target.ScreenWidth(null), null);`; dòng `var target = _button.IconRect;` giữa hai dòng giữ nguyên; comment "Canvas Overlay: toạ độ thế giới là pixel màn…" thành "Canvas Overlay: camera null." |
| `BoosterUnlockTutorial.cs` › using | có `Horcrux.Runtime.Utilities`, chưa có `…ExtensionMethods` | thêm `using Horcrux.Runtime.Utilities.ExtensionMethods;` |

Xong thì **xoá Plan này** cùng `.meta`.

**Editor setup:** không.

**Kiểm:**
- `bash "<horcrux>/Editor/CompileCheck/Tools~/verify.sh" --csproj com.horcrux.runtime.csproj,Assembly-CSharp.csproj --tests
  CategoryJam.EditModeTests,Horcrux.EditModeTests` xanh · grep `lossyScale.x / (flyer`, `iconRect.lossyScale.x` trong `Assets/_CategoryJam` = 0.
- **Chơi thử** (developer): vào level mở khoá booster, popup mở khoá hiện, bấm Show me!. Nhìn: icon hiện lại đúng cỡ ảnh trên popup, bay
  về nút và chạm đúng cỡ icon nút. Khác trước: không gì. Dấu hiệu hỏng: icon phình hay co lúc rời popup hoặc lúc chạm nút.

**Mốc commit:** `refactor(tutorial): cỡ icon bay qua ScreenSizeExtensions của Horcrux` ở dự án kèm con trỏ submodule; xoá Plan trong
`Assets/Horcrux` cùng con trỏ đó.

## File → GĐ

| File | GĐ |
|---|---|
| `Runtime/Utilities/ExtensionMethods/ScreenSizeExtensions.cs` | 1 |
| `Tests/PlayMode/ScreenSizeExtensionsTests.cs` (agent) | 1 |
| `docs/Runtime/ScreenSize/ScreenSize.md`, `.html` (agent) | 2 |
| category-jam `PopupBoosterUnlock.cs`, `BoosterUnlockTutorial.cs`; xoá Plan này | 3 |
