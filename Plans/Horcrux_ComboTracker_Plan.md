# Plan — `ComboTracker` lên Horcrux

Bản tạm: category-jam `Scripts/Director/ComboTracker.cs` (class thuần do `GameDirector` sở hữu); HUD `Scripts/Hud/ComboPill.cs` ở lại dự án. Không phụ thuộc Plan nào.
Kiểm: `bash "<horcrux>/Editor/CompileCheck/Tools~/verify.sh" --csproj com.horcrux.runtime.csproj,Horcrux.PlayModeTests.csproj --tests Horcrux.EditModeTests`; PlayMode chạy ở Window › General › Test Runner.

## Kịch bản

| Tình huống | Hệ làm gì | Caller/người chơi thấy |
|---|---|---|
| chủ gọi `AddPoint` | `Count + 1`, cửa sổ = `windowSeconds(Count)`, huỷ cửa sổ cũ, mở cửa sổ mới | `Changed(Count, window)`: HUD "Combo xN", thanh đầy rồi rút |
| điểm mới trong cửa sổ | delay cũ dừng trước khi mở delay mới | chuỗi không đứt giữa chừng |
| ngừng tay tới hết cửa sổ | `Count` 0 | `Changed(0, 0)`: thanh rỗng, nhãn tắt |
| `windowSeconds` trả `PositiveInfinity` (combo theo lượt) | không mở đồng hồ | chỉ đứt khi chủ gọi `Reset` |
| `windowSeconds` trả 0 | delay 0 | `Changed(1, 0)` rồi `Changed(0, 0)` ở frame kế |
| `timeScale` 0 (hitstop, pause) · app vào nền | `Tween.Delay` scaled đứng · PrimeTween không tick | cửa sổ đứng; HUD khớp khi `ProgressBar` tắt `Use Unscaled Time` |
| vào level, kể cả chơi lại: `Reset` | `Count` 0, huỷ cửa sổ, phát kể cả khi đang 0 | `Changed(0, 0)`: pill rỗng, không mang combo cũ |
| chủ chết: `Dispose` | huỷ cửa sổ | không `Changed` nào nữa |
| không ai nghe `Changed` | — | không ném |

## Flow

```
chủ (MonoBehaviour dự án)
  ├─ new(windowSeconds) · AddPoint · Reset · Dispose ──► ComboTracker              Runtime/Utilities/Combo/ComboTracker.cs
  │                                                        │ Count · _windowSeconds(Count) · _window
  │                                                        ├─ Stop · Delay(this, window, static …) ──► PrimeTween
  │                                                        │◄── OnWindowElapsed: Count 0 ──────────────┘
  └◄── Changed(count, windowSeconds) ──────────────────────┘
        chủ: tiếng, hình gọi cạnh AddPoint trong cùng một hàm; đẩy Changed lên bus của dự án cho HUD
        HUD: count 0 → thanh rỗng, nhãn tắt · count n → ProgressBar.PlayAsync(window, 0, window, "Combo xn", ct)
```

---

## GĐ1 — xong thì HUD vẽ được "Combo xN" với thanh dài `window`

```
  chủ ──new(windowSeconds)+ / AddPoint+──► ComboTracker+ ──Changed(Count, window)+──► chủ (HUD, tiếng)
```

`Runtime/Utilities/Combo/ComboTracker.cs` — file mới:

```csharp
using System;

namespace Horcrux.Runtime.Utilities.Combo
{
    /// <summary>
    /// Đếm chuỗi: mỗi điểm cộng một và mở cửa sổ dài windowSeconds(Count). Class thuần do một MonoBehaviour của dự án sở hữu.
    /// </summary>
    public sealed class ComboTracker
    {
        #region Properties

        private readonly Func<int, float> _windowSeconds;

        public int Count { get; private set; }

        /// <summary>(count, windowSeconds) sau mỗi điểm; người nghe vẽ cửa sổ mới dài windowSeconds.</summary>
        // Mang cả windowSeconds: HUD vẽ thanh rút cần độ dài ngay lúc điểm vào, chỉ tracker biết công thức.
        public event Action<int, float> Changed;

        #endregion Properties

        /// <param name="windowSeconds">Giây của cửa sổ theo count vừa đạt; công thức là của dự án.</param>
        public ComboTracker(Func<int, float> windowSeconds) => _windowSeconds = windowSeconds;

        #region API

        public void AddPoint()
        {
            Count++;
            Changed?.Invoke(Count, _windowSeconds(Count));
        }

        #endregion API
    }
}
```

**Editor setup:**

1. Project window: xoá `Runtime/Abstractions/Composites/Combo/IComboSystem.cs` và `Runtime/Implementations/Composites/Combo/ComboSystem.cs` kèm `.meta`; hai thư mục `Combo` rỗng xoá theo. Không file nào tham chiếu chúng.

`Tests/PlayMode/ComboTrackerTests.cs` (agent) — ghi mọi `Changed` vào `List<(int, float)>`; thêm: grep `IComboSystem` trong `Assets/Horcrux` = 0.

| Case | Input | Kỳ vọng |
|---|---|---|
| điểm đầu | `_ => 1f`, `AddPoint` | `Count` 1, `Changed(1, 1)` một lần |
| hai điểm | `AddPoint` ×2 | `Count` 2, `Changed` lần hai là `(2, 1)` |
| cửa sổ theo count | `count => count`, `AddPoint` ×3 | `Changed` lần lượt `(1, 1)`, `(2, 2)`, `(3, 3)` |
| đọc `Count` trong handler | handler ghi `tracker.Count` | bằng tham số `count` của cùng lần phát |
| không ai nghe | không đăng ký `Changed`, `AddPoint` | không ném, `Count` 1 |
| công thức trả 0 | `_ => 0f` | `Changed(1, 0)` — tracker không phán xét giá trị, công thức là của dự án |

---

## GĐ2 — xong thì chuỗi tự đứt khi người chơi ngừng tay, chủ chết thì cửa sổ huỷ theo

```
  chủ ──AddPoint / Dispose+──► ComboTracker ──Stop+ · Delay(this, window)+──► PrimeTween
                                    ▲ OnWindowElapsed+ (Count 0)                 │
                                    └────────────────────────────────────────────┘
                               ──Changed(Count, window) / Changed(0, 0)+──► chủ
```

`ComboTracker.cs` › đầu file — cũ → mới:

```csharp
using System;
```

```csharp
using System;
using PrimeTween;
```

`ComboTracker.cs` › khai báo class — cũ → mới:

```csharp
    /// <summary>
    /// Đếm chuỗi: mỗi điểm cộng một và mở cửa sổ dài windowSeconds(Count). Class thuần do một MonoBehaviour của dự án sở hữu.
    /// </summary>
    public sealed class ComboTracker
```

```csharp
    /// <summary>
    /// Đếm chuỗi: mỗi điểm cộng một và mở lại cửa sổ dài windowSeconds(Count), hết cửa sổ thì về 0. Class thuần do một
    /// MonoBehaviour sở hữu; Dispose cùng chủ.
    /// </summary>
    public sealed class ComboTracker : IDisposable
```

`ComboTracker.cs` › `Properties` › thêm sau `_windowSeconds`, thay chú thích `Changed`:

```csharp
        private readonly Func<int, float> _windowSeconds;
        // Handle cửa sổ: Stop là huỷ, không cấp phát token huỷ mỗi điểm combo.
        private Tween _window;

        public int Count { get; private set; }

        /// <summary>(count, windowSeconds): count 0 và window 0 là chuỗi vừa đứt.</summary>
        public event Action<int, float> Changed;
```

`ComboTracker.cs` › `API` › `AddPoint` — cũ → mới:

```csharp
        public void AddPoint()
        {
            Count++;
            Changed?.Invoke(Count, _windowSeconds(Count));
        }
```

```csharp
        public void AddPoint()
        {
            Count++;
            var window = _windowSeconds(Count);

            // Một handle một cửa sổ: delay cũ còn sống sẽ cắt ngang chuỗi mới; Stop trên handle mặc định no-op.
            _window.Stop();
            if (!float.IsPositiveInfinity(window))
                // Tracker làm target, lambda static: không closure mỗi điểm.
                _window = Tween.Delay(this, window, static tracker => tracker.OnWindowElapsed());

            // Phát sau khi đồng hồ đã chạy: người nghe đọc Count thấy giá trị mới.
            Changed?.Invoke(Count, window);
        }

        public void Dispose() => _window.Stop();
```

`ComboTracker.cs` › thêm region `Class Methods` sau `#endregion API`:

```csharp
        #region Class Methods

        private void OnWindowElapsed()
        {
            Count = 0;
            // Cùng đường "về 0" với Reset: HUD không cần biết vì sao chuỗi đứt.
            Changed?.Invoke(0, 0f);
        }

        #endregion Class Methods
```

`Tests/PlayMode/ComboTrackerTests.cs` (agent) — `[UnityTest]` chờ bằng thời gian scaled (`WaitForSeconds`), công thức hằng trừ ca ghi rõ:

| Case | Input | Kỳ vọng |
|---|---|---|
| hết giờ | cửa sổ 0.1 s, chờ 0.3 s | `Count` 0; `Changed(0, 0)` đúng một lần sau `Changed(1, 0.1)` |
| điểm mới trong cửa sổ | cửa sổ 1 s, `AddPoint` tại 0 s và 0.6 s, đọc tại 1.2 s | `Count` 2 (cửa sổ đầu đã huỷ); về 0 tại ~1.6 s |
| cửa sổ theo count | `count => count`, `AddPoint` ×2 liền nhau | về 0 sau ~2 s, không phải 1 s |
| vô hạn | `_ => float.PositiveInfinity`, chờ 1 s | `Count` vẫn 1, không `Changed` nào thêm |
| cửa sổ 0 | `_ => 0f` | `Changed(1, 0)` rồi `Changed(0, 0)` ở frame kế |
| hết giờ rồi điểm mới | hết giờ xong `AddPoint` | `Count` 1, `Changed(1, window)` — chuỗi mới bắt đầu từ 1 |
| `Dispose` giữa cửa sổ | `AddPoint`, `Dispose`, chờ hết cửa sổ | không `Changed` nào nữa; `Count` giữ 1 (chủ đã chết, không ai đọc) |
| `Dispose` chưa điểm nào | `new` rồi `Dispose` | không ném (handle mặc định) |
| `timeScale` 0 | `AddPoint` cửa sổ 0.2 s, `Time.timeScale = 0`, chờ 0.5 s thời gian thật | `Count` 1; đặt lại 1 rồi chờ 0.3 s → về 0 |
| không ai nghe lúc hết giờ | gỡ `Changed` trước khi hết giờ | không ném, `Count` 0 |

---

## GĐ3 — xong thì tracker sống qua nhiều level của cùng một chủ mà HUD không mang combo cũ sang

```
  chủ ──AddPoint / Reset+ / Dispose──► ComboTracker ──Stop · Delay──► PrimeTween
                                              ──Changed(0, 0)+──► chủ (HUD rỗng)
```

`ComboTracker.cs` › `API` › thêm sau `AddPoint`, trước `Dispose`:

```csharp
        public void Reset()
        {
            Count = 0;
            _window.Stop();
            // Phát cả khi đang 0: HUD vào level mới luôn về rỗng, chủ không phải nhớ đã về 0 chưa.
            Changed?.Invoke(0, 0f);
        }
```

`Tests/PlayMode/ComboTrackerTests.cs` (agent):

| Case | Input | Kỳ vọng |
|---|---|---|
| `Reset` giữa cửa sổ | `AddPoint` cửa sổ 0.2 s, `Reset`, chờ 0.4 s | đúng một `Changed(0, 0)`; `Count` 0 (delay cũ đã dừng) |
| `Reset` khi đang 0 | `new`, `Reset` | `Changed(0, 0)` một lần, không ném |
| `Reset` rồi điểm mới | `Reset`, `AddPoint` | `Count` 1, `Changed(1, window)` |
| `Reset` cửa sổ vô hạn | `_ => PositiveInfinity`, `AddPoint`, `Reset` | `Count` 0, `Changed(0, 0)` |
| `Reset` hai lần liền | `AddPoint`, `Reset`, `Reset` | hai `Changed(0, 0)` — chủ gọi hai lần thì HUD vẽ hai lần, tracker không khử trùng |

---

## Chuyển dự án (agent)

| Chỗ | Đổi |
|---|---|
| `docs/Runtime/ComboTracker/ComboTracker.md` | tạo, mở `Code: Runtime/Utilities/Combo/`: luồng `AddPoint → windowSeconds(Count) → Tween.Delay → OnWindowElapsed → Changed(0, 0)` · bảng API (`ComboTracker(Func<int, float>)`, `Count`, `AddPoint`, `Reset`, `Dispose`, `Changed`) · Design decisions từ comment và khối Không làm · Dùng trong dự án: chủ `new` ở khởi tạo, `Reset` đầu level, `Dispose` ở `OnDestroy`, một hàm gọi `AddPoint` rồi phản hồi, HUD nghe `Changed` → `ProgressBar.PlayAsync(window, 0, window, label, ct)` với `Use Unscaled Time` tắt · ca `PositiveInfinity`, ca công thức trả 0 |
| `docs/Runtime/ComboTracker/ComboTracker.html` | tạo theo `docs` › `.html`: Kịch bản (ghi điểm liên tiếp, ngừng tay, vào level mới, pause) · Cách dùng · Flow · Lưu ý (`Use Unscaled Time`, `Dispose` cùng chủ, hai `Reset` liền là hai lần vẽ) |
| `GameDirector.cs` › using | thêm `using Horcrux.Runtime.Utilities.Combo;` sau `…Utilities.EventBus;` |
| `GameDirector.Init` | `new ComboTracker(ComboWindowSeconds)`, `_combo.Changed += OnComboChanged`; `Dispose` ở `OnDestroy`, `Reset` ở `StartLevelFlow`, `CurrentComboNumber` giữ nguyên |
| `GameDirector.RegisterEvents` | thêm `EventBus<BoxLidClosed>.Subscribe(OnBoxLidClosed)` sau `TilePlayabilityChanged`, không giữ handle như các dòng trên |
| `GameDirector` › `Class Methods` sau `OnTileMovedOnBox` | `OnBoxLidClosed`: `_combo.AddPoint()` rồi `e.Box.ShowCombo(_combo.Count)` · `OnComboChanged` publish `ComboChanged(count, windowSeconds)` · `static float ComboWindowSeconds(int count)` = thân `DecayDuration` cũ, `Count` thành tham số |
| `Scripts/Director/ComboTracker.cs` | xoá cùng `.meta`; grep `class ComboTracker`, `Horcrux ComboSystem` trong `Assets/_CategoryJam` = 0 |
| `Scripts/Layout.md` | bảng mở đầu, hàng "Cố ý KHÔNG làm": bỏ vế "không dùng `Horcrux ComboSystem`" · đoạn "Luồng combo": `GameDirector.OnBoxLidClosed` gọi `AddPoint` của `ComboTracker` (Horcrux) rồi `ShowCombo`; `OnComboChanged` publish `ComboChanged` |
| `Scripts/Entities/Entities.md` › hàng "Nắp chạm thân" | `GameDirector.OnBoxLidClosed` cộng combo rồi gọi `box.ShowCombo(count)` |
| `Scripts/Feel.md` › "Combo" | `ComboTracker` (Horcrux) giữ cửa sổ bằng `Tween.Delay`, phát `Changed`; `GameDirector` publish `ComboChanged` |
| `Structure.md` › dòng `Director/` | sở hữu `ComboTracker` của Horcrux và class thuần `AutoPlayer` |
| prefab HUD chứa `ComboPill` | kiểm `ProgressBar` ở ô `Bar`: `Use Unscaled Time` tắt, `Speed` 1 |
| Plan này | xoá cùng `.meta` |
| chơi thử | đóng nắp hai box cách nhau dưới 10 s → "Combo x2" và tiếng, thanh đầy rồi rút; ngừng tay → nhãn tắt; nắp tiếp → "Combo x1", không tiếng; chơi lại giữa chuỗi → pill rỗng ngay; hỏng: thanh rỗng cắt ngang chuỗi, combo cũ sang level mới, tiếng ở x1, thanh rút khi pause |

## Không làm · Mở rộng sau

| | Phương án | Vì sao |
|---|---|---|
| bỏ | `IComboSystem`, `ComboSystem` `MonoBehaviour` bơm tick từ Ticker; giữ hai stub | `Tween.Delay` đã là đồng hồ có handle dừng; stub chết trùng chủ đề làm người đọc mở nhầm |
| bỏ | `CancellationTokenSource` + `UniTask.Delay` cho cửa sổ | một cấp phát và một `Dispose` mỗi điểm combo |
| bỏ | công thức là property `DecayDuration` đọc `Count` trong tracker | công thức là của dự án; tracker chỉ biết "hàm của count" |
| bỏ | struct cửa sổ `lerp(w0, wMin, n/nMin)` trong Horcrux | người dùng đầu dùng công thức khác |
| bỏ | HUD gọi lại công thức cửa sổ | hai nơi giữ công thức |
| bỏ | tracker tự nghe `EventBus<BoxLidClosed>`, publish `ComboChanged` qua `EventBus` | gọi tên domain; event của dự án `internal`, chủ publish trong handler `Changed` |
| bỏ | bridge sang Feedback, `ComboMeter`, demo driver | phản hồi của một khoảnh khắc gọi cạnh nhau trong một hàm của chủ; meter là `ProgressBar.PlayAsync` có sẵn |
| bỏ | hệ số nhân, bậc (`tierMinCounts`), `Multiplier` | hàm thuần của `Count`, dự án tính ở chỗ dùng; thêm sau không đổi tracker |
| bỏ | `Reset` chỉ phát khi `Count > 0` | HUD phải giữ state riêng để chặn, hai nơi giữ sự thật |
| sau | struct `[Serializable]` (giây ở combo 1, giây tối thiểu, combo đạt tối thiểu) có `Evaluate(count)` | khi dự án cần tune cửa sổ trên Inspector; tracker không đổi |
| sau | event riêng phân biệt đứt do hết giờ với do `Reset` | khi dự án cần; `Changed` giữ nguyên |
