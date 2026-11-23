# Plan — `ScreenSizeExtensions`: bề ngang pixel của rect, scale để rect rộng W pixel

Bản tạm: category-jam `Bridge/PopupBoosterUnlock.cs` › `OnClickShowMe`, `Bridge/BoosterUnlockTutorial.cs` › `FlyIconAsync` (chép tay).
**Phụ thuộc:** không — gõ lúc nào cũng được.
Kiểm: `bash "<horcrux>/Editor/CompileCheck/Tools~/verify.sh" --csproj com.horcrux.runtime.csproj,Horcrux.PlayModeTests.csproj --tests Horcrux.EditModeTests`; PlayMode chạy ở Window › General › Test Runner.

## Kịch bản

| Tình huống | Hệ làm gì | Caller thấy |
|---|---|---|
| rect trên canvas Overlay, hỏi bề ngang | chiếu hai mép cạnh ngang lên màn hình, trừ x | số pixel, đúng cả khi cha có scale |
| rect trên canvas Screen Space Camera | như trên, qua camera của canvas | số pixel khớp `cam.WorldToScreenPoint` hai mép |
| icon đang scale 0, chờ nở tới rộng W pixel | đo cỡ rect qua ma trận của cha, không đọc scale riêng | scale cục bộ cần đặt; không chia 0 |
| rect xoay 90° | chiếu cạnh ngang, giờ đứng dọc | ≈ 0 |
| rect không có cha | `NullReferenceException` ở `parent` | lỗi setup lộ ngay |

## Flow

```
caller (rect, camera của canvas | null cho Overlay)
  ├─ rect.ScreenWidth(cam) ───────────────┐            ScreenSizeExtensions.cs
  └─ rect.LocalScaleForScreenWidth(W, cam) ┤
                                           ▼
                        SpanOnScreen(start, span, cam) ──► RectTransformUtility.WorldToScreenPoint
```

## Thuật toán

Bề ngang trên màn hình của đoạn `[p, p + v]` = `screen(p + v).x − screen(p).x`; `screen` là phép chiếu của camera canvas (Overlay: lấy x, y).

| Hàm | `v` | Kết quả |
|---|---|---|
| `ScreenWidth` | `rect.width` qua ma trận **của rect** — gồm scale riêng và mọi cha | bề ngang |
| `LocalScaleForScreenWidth` | `rect.width` qua ma trận **của cha** — cỡ rect ở scale 1 | `W ÷ bề ngang(v)` |

Bản mẫu (eval trong Editor): khứ hồi W ∈ {1, 57.3, 400} lệch ≤ 3.8e-6 px · so công thức chép tay 50 bộ seed 12345 lệch ≤ 2.7e-4 px.

---

## GĐ1 — xong thì caller hỏi được cỡ pixel trên cả canvas Overlay lẫn Camera

```
caller ──ScreenWidth+ / LocalScaleForScreenWidth+──► SpanOnScreen+ ──► RectTransformUtility.WorldToScreenPoint
```

`Runtime/Utilities/ExtensionMethods/ScreenSizeExtensions.cs` — file mới:

```csharp
using UnityEngine;

namespace Horcrux.Runtime.Utilities.ExtensionMethods
{
    /// <summary>Bề ngang pixel của rect trên màn hình. canvasCamera là camera của canvas chứa rect; null cho Screen Space Overlay.</summary>
    public static class ScreenSizeExtensions
    {
        /// <summary>Bề ngang pixel của rect ở scale hiện tại.</summary>
        // canvasCamera không mặc định null: quên camera của canvas Camera là sai lệch im lặng.
        public static float ScreenWidth(this RectTransform rectTransform, Camera canvasCamera)
            => SpanOnScreen(rectTransform.position, rectTransform.TransformVector(rectTransform.rect.width, 0f, 0f), canvasCamera);

        /// <summary>Scale cục bộ đều để rect rộng screenWidth pixel; không đọc scale hiện tại (đang 0 lúc chờ nở vẫn đúng). Rect phải có cha.</summary>
        public static float LocalScaleForScreenWidth(this RectTransform rectTransform, float screenWidth, Camera canvasCamera)
        {
            // Ma trận của cha, không phải tỉ số theo scale hiện tại: icon chờ nở đang scale 0.
            var unscaledSpan = rectTransform.parent.TransformVector(rectTransform.rect.width, 0f, 0f);
            return screenWidth / SpanOnScreen(rectTransform.position, unscaledSpan, canvasCamera);
        }

        private static float SpanOnScreen(Vector3 start, Vector3 span, Camera canvasCamera)
            => RectTransformUtility.WorldToScreenPoint(canvasCamera, start + span).x
               - RectTransformUtility.WorldToScreenPoint(canvasCamera, start).x;
    }
}
```

`Tests/PlayMode/ScreenSizeExtensionsTests.cs` (agent):

| Case | Input | Kỳ vọng |
|---|---|---|
| Overlay | rect 100, scale cha 2, scale riêng 1.5, `ScreenWidth(rect, null)` | 300 (±1e-3) |
| Overlay, scale 0 | rect 100, scale cha 2, scale riêng 0, `LocalScaleForScreenWidth(rect, 300, null)` | 1.5 — đỏ nếu tính theo scale hiện tại |
| khứ hồi | `localScale = LocalScaleForScreenWidth(rect, W, null)` rồi `ScreenWidth(rect, null)` | W (±1e-3), W ∈ {1, 57.3, 400} |
| canvas Camera | `ScreenSpaceCamera`, camera ortho size 5, rect 100 ở `planeDistance` 10 | `ScreenWidth(rect, cam)` bằng `cam.WorldToScreenPoint` hai mép (±1e-3); `ScreenWidth(rect, null)` khác |
| xoay 90° | rect xoay z 90° | ≈ 0 |
| bằng bản dự án | Overlay không xoay, 50 bộ (rect, scale cha, scale riêng) seed cố định | lệch với `rect.width * lossyScale.x` < 1e-3 |

---

## Chuyển dự án (agent)

| Chỗ | Đổi |
|---|---|
| `docs/Runtime/ScreenSize/ScreenSize.md`, `.html` | tạo: hai câu hỏi → hai hàm · camera nào truyền · vì sao đọc ma trận cha · rect xoay · ranh giới với `UIProxyFlight` |
| `PopupBoosterUnlock.OnClickShowMe` | bề ngang icon popup qua `ScreenWidth(null)` |
| `BoosterUnlockTutorial.FlyIconAsync` | scale lúc rời và lúc chạm qua `LocalScaleForScreenWidth`; bỏ `parentScale` chép tay |
| Plan này | xoá cùng `.meta` |
| chơi thử | level mở khoá booster → popup → Show me! → icon rời popup đúng cỡ ảnh, chạm nút đúng cỡ icon nút; hỏng: icon phình hay co ở hai đầu |

## Không làm · Mở rộng sau

| | Phương án | Vì sao |
|---|---|---|
| bỏ | `localScale.x × W ÷ ScreenWidth` | sai đúng ở ca hay gặp nhất, scale đang 0 |
| bỏ | guard rect không cha | rect trong canvas luôn có cha; rect gốc là canvas, scale do `CanvasScaler` đặt |
| bỏ | `WorldWidthOnScreen(this Camera, …)` | người dùng duy nhất là `UIProxyFlight.FlyAsync`, tự quy đổi bằng ba dòng |
| sau | `WorldWidthOnScreen(this Camera, Vector3 center, float worldWidth)` vào file này | khi có người dùng thứ hai ngoài `UIProxyFlight` |
| sau | chiều cao, khung hai chiều | khi có người dùng; khung bo góc đã có ở `TutorialAnchor.GetScreenFrame` |
