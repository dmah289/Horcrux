# Plan — `ComboTracker` lên Horcrux

Ngày 2026-11-02. Thay plan `ComboSystem.md` (đã xoá; 11 file, 4 tầng, phụ thuộc Ticker và Feedback) bằng đúng hình đã chạy trong category-jam:
`Assets/_CategoryJam/Scripts/Director/ComboTracker.cs` (84 dòng) và `Hud/ComboPill.cs` (vẽ bằng `ProgressBar.PlayAsync` có sẵn).
Developer chốt 2026-11-02: thu về hình này. Lõi là một bộ đếm với cửa sổ hết hạn là **hàm của count**; mọi phản hồi (tiếng, rung,
hình) là của dự án, gọi cạnh nhau trong một hàm của nó, không đi qua bridge.

**Phân công:** Task 1 (Horcrux, code runtime) developer gõ · Task 2 (tài liệu) và Task 3 (test PlayMode) agent viết sau khi code có.
Dự án chuyển sang bản Horcrux là việc riêng khi developer yêu cầu.

Horcrux là submodule: mọi task commit **trong `Assets/Horcrux`**.

---

## Ngữ cảnh đã chốt

| Mục | Chốt |
|---|---|
| Người dùng | dự án có chuỗi hành động nối nhau trong một cửa sổ thời gian (đóng nắp box, ghép ba, nổ chuỗi); chủ là director của level, sống và chết cùng level |
| Mục tiêu | `Count` tăng mỗi điểm, cửa sổ mở lại theo `windowSeconds(Count)`, hết cửa sổ về 0; một event `Changed(count, windowSeconds)` cho HUD và phản hồi của dự án |
| Chỗ đặt | `Runtime/Utilities/Combo/ComboTracker.cs`, namespace `Horcrux.Runtime.Utilities.Combo`; class thuần do một `MonoBehaviour` của dự án sở hữu (có mốc `Dispose`) |
| Thời gian | **scaled** qua `Tween.Delay` của PrimeTween: hitstop hay pause của dự án đặt `timeScale` 0 thì cửa sổ đứng theo — ý của người dùng đầu; HUD vẽ bằng `ProgressBar` tắt `useUnscaledTime` để khớp |
| Tên | `ComboTracker`, `AddPoint`, `Reset`, `Changed`. Developer đổi tuỳ ý trước khi gõ |

**Cố ý KHÔNG làm**

| Không làm | Vì |
|---|---|
| `IComboSystem`, `ComboSystem` MonoBehaviour bơm tick từ Ticker | `Tween.Delay` đã là đồng hồ có handle dừng; không cần Ticker, không cần `Update` |
| Bridge sang Feedback, `ComboMeter` riêng, demo driver | phản hồi nhiều kênh của một khoảnh khắc gọi cạnh nhau trong **cùng một hàm** của dự án; meter là `ProgressBar` có sẵn với `PlayAsync(window, 0, window, label, ct)` |
| Hệ số nhân, bậc (`tierMinCounts`), `Multiplier` | người dùng đầu không có; là hàm thuần của `Count`, dự án tính ở chỗ dùng. Thêm sau là hàm trên `Count`, không đổi tracker |
| Cửa sổ dạng struct `lerp(w0, wMin, n/nMin)` trong Horcrux | người dùng đầu dùng công thức khác; tracker nhận `Func<int, float>`, công thức là của dự án. Có người dùng thứ hai cần núm Inspector thì thêm struct `[Serializable]` có `Evaluate`, tracker không đổi |
| Event qua `EventBus` của Horcrux | event của dự án là `internal` trong assembly dự án; tracker phát C# event, lớp nối của dự án đẩy lên bus của nó |
| Đóng băng cửa sổ khi app vào nền | `Tween.Delay` scaled: `timeScale` 0 lúc pause của dự án đã đóng băng; app nền thì PrimeTween không tick |

**Quyết định trái trực giác**

| Chỗ | Vì |
|---|---|
| `Reset` phát `Changed(0, 0)` | HUD cần biết chuỗi đã đứt do đổi level, không chỉ do hết giờ; phân biệt với hết giờ là của dự án nếu cần, qua event riêng sau |
| `Changed` khi hết cửa sổ phát `(0, 0)` | cùng một đường "về 0" cho HUD: tắt nhãn, làm rỗng thanh |
| `_window.Stop()` trước khi tạo delay mới trong `AddPoint` | hai delay sống song song thì cái cũ về 0 giữa chừng; một slot cho một thuộc tính |
| Giữ `Tween` (struct) làm handle thay vì `CancellationTokenSource` | `Stop` là huỷ; không cấp phát token mỗi điểm combo |
| Callback `static tracker => tracker.OnWindowElapsed()` | không closure: tracker là target của delay |

**Mở rộng sau**

- Struct núm cửa sổ `[Serializable]` (giây ở combo 1, giây tối thiểu, combo đạt tối thiểu) với `Evaluate(count)` khi có dự án tune trên Inspector.
- `Multiplier(count)` khi có dự án tính điểm theo combo.

---

## Đã khảo sát

| Nguồn | Lấy gì | Không lấy, vì sao |
|---|---|---|
| `ComboTracker.cs` của dự án | toàn bộ cơ chế | đăng ký `BoxLidClosed`, gọi `Box.ShowCombo`, publish `ComboChanged`: lớp nối dự án; công thức `DecayDuration` thành `Func` dự án truyền |
| `ComboPill.cs` của dự án | cách vẽ bằng `ProgressBar.PlayAsync` | ở lại dự án nguyên |
| `ComboSystem.md` (plan cũ) | luật "một state thật là `count`, mọi thứ khác là hàm của count"; "ba cơ chế đứt gộp về một hàm `windowSeconds(count)`" (+∞ là cửa sổ vô hạn: chỉ đứt khi gọi `Reset`) | Ticker, bridge, meter, demo, tier, multiplier |
| Horcrux `ProgressBar.PlayAsync` | meter | — |
| PrimeTween `Tween.Delay(target, duration, onComplete)` | đồng hồ | `useUnscaledTime` để mặc định false |

---

## Task 1 — Horcrux (developer gõ)

Xoá hai stub `Runtime/Implementations/Composites/Combo/ComboSystem.cs` và `Runtime/Abstractions/Composites/Combo/IComboSystem.cs`
trong Project window (kèm `.meta`, hai thư mục `Combo` rỗng xoá theo). Không file nào tham chiếu chúng.

### `Runtime/Utilities/Combo/ComboTracker.cs`

```csharp
using System;
using PrimeTween;

namespace Horcrux.Runtime.Utilities.Combo
{
    /// <summary>
    /// Đếm chuỗi: mỗi điểm cộng một và mở lại cửa sổ dài windowSeconds(Count), hết cửa sổ thì về 0. Class thuần do một
    /// MonoBehaviour sở hữu; Dispose cùng chủ.
    /// </summary>
    public sealed class ComboTracker : IDisposable
    {
        #region Properties

        private readonly Func<int, float> _windowSeconds;
        // Handle cửa sổ: Stop là huỷ, không cấp phát token huỷ mỗi điểm combo.
        private Tween _window;

        public int Count { get; private set; }

        /// <summary>(count, windowSeconds): count 0 và window 0 là chuỗi vừa đứt, do hết giờ hay Reset.</summary>
        public event Action<int, float> Changed;

        #endregion Properties

        /// <param name="windowSeconds">Giây cho tới khi về 0, theo count vừa đạt; PositiveInfinity là không hết giờ.</param>
        public ComboTracker(Func<int, float> windowSeconds) => _windowSeconds = windowSeconds;

        #region API

        public void AddPoint()
        {
            Count++;
            var window = _windowSeconds(Count);

            _window.Stop();
            if (!float.IsPositiveInfinity(window))
                _window = Tween.Delay(this, window, static tracker => tracker.OnWindowElapsed());

            Changed?.Invoke(Count, window);
        }

        public void Reset()
        {
            Count = 0;
            _window.Stop();
            Changed?.Invoke(0, 0f);
        }

        public void Dispose() => _window.Stop();

        #endregion API

        #region Class Methods

        private void OnWindowElapsed()
        {
            Count = 0;
            Changed?.Invoke(0, 0f);
        }

        #endregion Class Methods
    }
}
```

**Khác với bản dự án, và vì sao**

| Chỗ | Bản dự án | Bản Horcrux | Vì |
|---|---|---|---|
| công thức cửa sổ | property `DecayDuration` trong class | `Func<int, float>` nhận ở ctor | công thức là của dự án; tracker chỉ biết "hàm của count" |
| đăng ký `BoxLidClosed`, `Box.ShowCombo` | trong tracker | lớp nối của dự án gọi `AddPoint` rồi `ShowCombo` cạnh nhau | gọi tên domain |
| `EventBus<ComboChanged>.Publish` | trong tracker | event C# `Changed`; dự án publish trong handler | event của dự án `internal` |
| cửa sổ vô hạn | không có | `PositiveInfinity` bỏ delay | luật "theo lượt: chỉ đứt khi `Reset`" từ plan cũ, không thêm nhánh nào khác |

Lớp nối của dự án (viết sau, ngoài plan): `new ComboTracker(static count => count <= 5 ? 11 - count : 5f * Mathf.Pow(0.98f, count - 6))`
trong `GameDirector`, `tracker.Changed += (c, w) => EventBus<ComboChanged>.Publish(new ComboChanged(c, w))`, handler `BoxLidClosed`
gọi `AddPoint()` rồi `box.ShowCombo(tracker.Count)`.

### Nghiệm thu Task 1

`com.horcrux.runtime.csproj` build sạch; `grep IComboSystem` và `grep ComboSystem.md` trong Horcrux ra 0 (`FeelingPlan.md` đã trỏ sang plan này; `Layout.md` của dự án nhắc "Horcrux ComboSystem" sửa khi dự án chuyển).

---

## Task 2 — Tài liệu `Runtime/Utilities/Combo/ComboTracker.md` (agent)

Luồng `AddPoint → window(Count) → Tween.Delay → OnWindowElapsed → Changed(0,0)` ASCII · bảng API · "Design decisions" là bảng trên ·
"Dùng trong dự án": chủ, công thức, HUD bằng `ProgressBar.PlayAsync(window, 0, window, label, ct)` với `Use Unscaled Time` tắt.

## Task 3 — Test PlayMode (agent)

`Tests/PlayMode/ComboTrackerTests.cs` (cần tween chạy).

| Case | Input | Kỳ vọng |
|---|---|---|
| điểm đầu | `AddPoint` với window 1 s | `Count` 1, `Changed(1, 1)` |
| hai điểm trong cửa sổ | `AddPoint` ×2 cách 0.2 s | `Count` 2, cửa sổ thứ nhất không về 0 |
| hết giờ | window 0.1 s, chờ 0.2 s | `Count` 0, `Changed(0, 0)` đúng một lần |
| cửa sổ theo count | `count => count` (1 s, 2 s) | sau điểm thứ hai, về 0 sau ~2 s, không phải 1 s |
| vô hạn | `_ => PositiveInfinity`, chờ 1 s | `Count` vẫn 1; `Reset` về 0 với `Changed(0, 0)` |
| `Reset` giữa cửa sổ | `AddPoint` rồi `Reset` rồi chờ hết window cũ | chỉ một `Changed(0, 0)` (delay cũ đã dừng) |
| `Dispose` giữa cửa sổ | `AddPoint` rồi `Dispose` rồi chờ | không `Changed` nào nữa |
| timeScale 0 | `Time.timeScale = 0` sau `AddPoint`, chờ real time | `Count` giữ; đặt lại 1 thì về 0 |
