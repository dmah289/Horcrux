# Plan — `ComboTracker` lên Horcrux

Ngày 2026-11-06. Nguồn: category-jam `Assets/_CategoryJam/Scripts/Director/ComboTracker.cs` (class thuần do `GameDirector` sở hữu),
HUD `Scripts/Hud/ComboPill.cs` (vẽ bằng `ProgressBar.PlayAsync` có sẵn). Lõi là bộ đếm có cửa sổ hết hạn là **hàm của count**; phản
hồi (tiếng, hình) là của dự án, gọi cạnh nhau trong một hàm của nó, không đi qua bridge.

**Phân công:** GĐ1–3 developer gõ, agent viết test theo bảng case rồi compile · GĐ4 (tài liệu), GĐ5 (dự án chuyển sang bản Horcrux,
xoá bản tạm) agent làm. **Chạy được, thấy được** ở GĐ1–3 = compile sạch + bảng case xanh trong `Tests/PlayMode/ComboTrackerTests.cs`
(PlayMode vì cửa sổ là tween PrimeTween, phải có frame chạy); thấy trong game ở GĐ5. GĐ1–4 commit trong `Assets/Horcrux`, GĐ5 commit
ở dự án kèm con trỏ submodule. Không phụ thuộc Plan nào, không có lõi toán.

---

## Ngữ cảnh đã chốt

- **Người dùng:** dự án có chuỗi hành động nối nhau trong một cửa sổ thời gian (đóng nắp box, ghép ba, nổ chuỗi); chủ là director của
  level, sống và chết cùng level.
- **Chỗ đặt:** `Runtime/Utilities/Combo/ComboTracker.cs`, namespace `Horcrux.Runtime.Utilities.Combo`; class thuần do một
  `MonoBehaviour` của dự án sở hữu, `Dispose` cùng chủ. Tên `ComboTracker`, `AddPoint`, `Reset`, `Changed` đổi tuỳ ý trước khi gõ.
- **Hành vi:** `Count` tăng mỗi điểm, cửa sổ mở lại dài `windowSeconds(Count)`, hết cửa sổ về 0; một event `Changed(count,
  windowSeconds)` cho HUD và phản hồi. `windowSeconds` trả `float.PositiveInfinity` là không hết giờ: chỉ đứt khi chủ gọi `Reset`
  (combo theo lượt).
- **Thời gian scaled** qua `Tween.Delay`: `timeScale` 0 (hitstop, pause) thì cửa sổ đứng theo, app vào nền thì PrimeTween không tick;
  HUD dùng `ProgressBar` tắt `Use Unscaled Time` để khớp (GĐ5).
- **Không làm:** `IComboSystem`, `ComboSystem` bơm tick từ Ticker (GĐ2; hai stub xoá ở GĐ1) · bridge sang Feedback, `ComboMeter`, demo
  driver (GĐ1) · cửa sổ dạng struct trong Horcrux (GĐ1) · hệ số nhân, bậc (`tierMinCounts`), `Multiplier` (hàm thuần của `Count`, dự
  án tính ở chỗ dùng; thêm sau không đổi tracker) · event qua `EventBus` Horcrux (GĐ1) · phân biệt đứt do hết giờ với do `Reset` (GĐ3).
- **Đã khảo sát:** `ComboTracker.cs` dự án (lấy cả cơ chế; bỏ `Subscription<BoxLidClosed>`, `DecayDuration`,
  `EventBus<ComboChanged>.Publish`) · `GameDirector.cs` (vòng đời chủ: `new` ở `Init`, `Reset` ở `StartLevelFlow`, `Dispose` ở
  `OnDestroy`) · `ComboPill.cs` (cách vẽ, ở lại dự án) · `Box.cs` `HandleLidClosed`, `ShowCombo` · `GameplayEvents.cs` (`BoxLidClosed`,
  `ComboChanged` đều `internal`) · Horcrux `IComboSystem`, `ComboSystem` (stub rỗng, không ai tham chiếu) · `ProgressBar.PlayAsync`
  (`useUnscaledTime` mặc định `true`) · `EventBus.IsOwnerDestroyed` (chỉ tự gỡ listener là `UnityEngine.Object`) · PrimeTween
  `Tween.Delay<T>` (`Stop()` trên `Tween` mặc định là no-op).

## Luồng mô tả — một chuỗi combo từ điểm đầu tới khi đứt

```
chủ (director của level) new tracker với công thức cửa sổ
        ▼
một cú ghi điểm: chủ gọi AddPoint ──► Count + 1, cửa sổ = windowSeconds(Count) ──► Changed(Count, cửa sổ)   GĐ1
        │                                                                             chủ: tiếng, HUD vẽ thanh rút
        ▼
thời gian trôi (scaled): cửa sổ cũ huỷ, cửa sổ mới chạy ──► hết cửa sổ: Count 0 ──► Changed(0, 0)          GĐ2
        │                                                                             chủ chết: Dispose huỷ cửa sổ
        ▼
chủ cắt chuỗi khi vào level: Reset ──► Count 0, cửa sổ huỷ ──► Changed(0, 0)                                GĐ3
        ▼
tài liệu module (GĐ4) · dự án dùng bản Horcrux, một hàm của dự án gọi AddPoint rồi tiếng, HUD nghe Changed (GĐ5)
```

## Bản đồ hệ cuối

```
                 AddPoint / Reset / Dispose                         Delay(this, window, static …) · Stop
  chủ (MonoBehaviour dự án) ──────────────────► ComboTracker ◄────────────────────────────────────► PrimeTween
        ▲                                        │ Count · _windowSeconds(Count) · _window
        │ Changed(count, windowSeconds)          │ OnWindowElapsed: Count 0
        └────────────────────────────────────────┘

  chủ: trong một hàm, cạnh AddPoint — tiếng, hình của dự án; đẩy Changed lên bus của dự án cho HUD
  HUD: count 0 → thanh rỗng, nhãn tắt · count n → ProgressBar.PlayAsync(window, 0, window, "Combo xn", ct)
```

---

## Giai đoạn 1 — Một cú ghi điểm: `Count` tăng, cửa sổ tính theo `Count`, báo chủ

**Mục tiêu:** chủ `new ComboTracker(windowSeconds)`, gọi `AddPoint` mỗi điểm; tracker cộng `Count`, tính cửa sổ của count vừa đạt và
phát `Changed(Count, window)`. Xong giai đoạn này HUD vẽ được "Combo xN" với thanh dài `window`.

**Vì sao bây giờ:** ghi điểm là tương tác đầu tiên của mọi chủ; cửa sổ chưa cần đồng hồ vì chưa có gì xảy ra khi nó hết — đồng hồ
sinh ra ở giai đoạn có hành vi "hết giờ".

### `Runtime/Utilities/Combo/ComboTracker.cs` — file mới

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

**Khác với bản dự án**

| Chỗ | Bản dự án | Bản Horcrux | Vì |
|---|---|---|---|
| công thức cửa sổ | property `DecayDuration` đọc `Count` trong class | `Func<int, float>` nhận ở ctor, gọi một lần mỗi điểm | công thức là của dự án; tracker chỉ biết "hàm của count" |
| ctor | đăng ký `EventBus<BoxLidClosed>` | chỉ nhận công thức | gọi tên domain; chủ gọi `AddPoint` từ handler của nó (GĐ5) |
| báo đổi | `EventBus<ComboChanged>.Publish` | event C# `Changed` | event của dự án `internal`; chủ publish trong handler `Changed` (GĐ5) |

**Quyết định** — `Changed` mang cả `windowSeconds`, không chỉ `count`: HUD vẽ thanh rút cần độ dài cửa sổ ngay lúc điểm vào, và chỉ
tracker biết công thức. Phương án bỏ: HUD gọi lại công thức — hai nơi giữ công thức. — Công thức gọi **một lần** mỗi điểm, giữ vào local
(GĐ2 dùng lại cho delay): kết quả không đổi trong một lần `AddPoint`. — Công thức là `Func<int, float>` của chủ, không phải struct
`lerp(w0, wMin, n/nMin)` trong Horcrux: người dùng đầu dùng công thức khác. Mở rộng sau: dự án cần tune trên Inspector thì thêm struct
`[Serializable]` (giây ở combo 1, giây tối thiểu, combo đạt tối thiểu) có `Evaluate(count)`, tracker không đổi. — Tracker chỉ phát
`Changed`, không bridge sang Feedback, không `ComboMeter`, không demo driver: phản hồi nhiều kênh của một khoảnh khắc gọi cạnh nhau
trong **cùng một hàm** của chủ (GĐ5); meter là `ProgressBar.PlayAsync(window, 0, window, label, ct)` có sẵn.

| Consumes | Produces |
|---|---|
| `System.Func` | `ComboTracker(Func<int, float>)`, `Count`, `AddPoint`, `Changed` |

**Sơ đồ tương tác sau GĐ1** (`+` là phần mới)

```
  chủ ──new(windowSeconds)+ / AddPoint+──► ComboTracker+ ──Changed(Count, window)+──► chủ (HUD, tiếng)
```

**Editor setup:** developer xoá trong Project window hai stub `Runtime/Abstractions/Composites/Combo/IComboSystem.cs` và
`Runtime/Implementations/Composites/Combo/ComboSystem.cs` (kèm `.meta`; hai thư mục `Combo` rỗng xoá theo). Không file nào tham chiếu
chúng. Thiếu bước này: hai type chết trùng chủ đề nằm cạnh hệ thật, người đọc sau mở nhầm.

**Kiểm:** compile sạch; `grep IComboSystem` trong `Assets/Horcrux` ra 0. Test ghi lại mọi `Changed` vào một `List<(int, float)>`.

| Case | Input | Kỳ vọng |
|---|---|---|
| điểm đầu | `_ => 1f`, `AddPoint` | `Count` 1, `Changed(1, 1)` một lần |
| hai điểm | `AddPoint` ×2 | `Count` 2, `Changed` lần hai là `(2, 1)` |
| cửa sổ theo count | `count => count`, `AddPoint` ×3 | `Changed` lần lượt `(1, 1)`, `(2, 2)`, `(3, 3)` |
| đọc `Count` trong handler | handler ghi `tracker.Count` | bằng tham số `count` của cùng lần phát |
| không ai nghe | không đăng ký `Changed`, `AddPoint` | không ném, `Count` 1 |
| công thức trả 0 | `_ => 0f` | `Changed(1, 0)` — tracker không phán xét giá trị, công thức là của dự án |

**Mốc commit:** `feat(combo): ComboTracker đếm điểm, cửa sổ theo count`.

---

## Giai đoạn 2 — Thời gian trôi: hết cửa sổ về 0 và báo, chủ chết thì huỷ cửa sổ

**Mục tiêu:** mỗi `AddPoint` huỷ cửa sổ đang chạy, mở cửa sổ mới bằng `Tween.Delay` (scaled); hết cửa sổ `Count` về 0, phát
`Changed(0, 0)`; `PositiveInfinity` không mở đồng hồ; `Dispose` huỷ cửa sổ khi chủ chết. Xong: chuỗi tự đứt khi người chơi ngừng tay.
**Vì sao bây giờ:** "hết giờ" là lý do tồn tại của cửa sổ; đồng hồ và cách huỷ nó sinh cùng lúc vì hai delay song song cắt ngang chuỗi mới.

### `ComboTracker.cs` › đầu file — đoạn cũ → đoạn mới

Cũ:

```csharp
using System;
```

Mới:

```csharp
using System;
using PrimeTween;
```

### `ComboTracker.cs` › khai báo class — đoạn cũ → đoạn mới

Cũ:

```csharp
    /// <summary>
    /// Đếm chuỗi: mỗi điểm cộng một và mở cửa sổ dài windowSeconds(Count). Class thuần do một MonoBehaviour của dự án sở hữu.
    /// </summary>
    public sealed class ComboTracker
```

Mới:

```csharp
    /// <summary>
    /// Đếm chuỗi: mỗi điểm cộng một và mở lại cửa sổ dài windowSeconds(Count), hết cửa sổ thì về 0. Class thuần do một
    /// MonoBehaviour sở hữu; Dispose cùng chủ.
    /// </summary>
    public sealed class ComboTracker : IDisposable
```

### `ComboTracker.cs` › `Properties` › thêm sau `_windowSeconds`, thay chú thích `Changed`

```csharp
        private readonly Func<int, float> _windowSeconds;
        // Handle cửa sổ: Stop là huỷ, không cấp phát token huỷ mỗi điểm combo.
        private Tween _window;

        public int Count { get; private set; }

        /// <summary>(count, windowSeconds): count 0 và window 0 là chuỗi vừa đứt.</summary>
        public event Action<int, float> Changed;
```

### `ComboTracker.cs` › `API` › `AddPoint` — đoạn cũ → đoạn mới

Cũ:

```csharp
        public void AddPoint()
        {
            Count++;
            Changed?.Invoke(Count, _windowSeconds(Count));
        }
```

Mới — huỷ cửa sổ cũ trước khi mở cửa sổ mới; `Changed` phát sau khi đồng hồ đã chạy, người nghe đọc `Count` thấy giá trị mới:

```csharp
        public void AddPoint()
        {
            Count++;
            var window = _windowSeconds(Count);

            _window.Stop();
            if (!float.IsPositiveInfinity(window))
                _window = Tween.Delay(this, window, static tracker => tracker.OnWindowElapsed());

            Changed?.Invoke(Count, window);
        }

        public void Dispose() => _window.Stop();
```

### `ComboTracker.cs` › thêm region `Class Methods` sau `#endregion API`

```csharp
        #region Class Methods

        private void OnWindowElapsed()
        {
            Count = 0;
            Changed?.Invoke(0, 0f);
        }

        #endregion Class Methods
```

**Khác với bản dự án**

| Chỗ | Bản dự án | Bản Horcrux | Vì |
|---|---|---|---|
| tên handle, callback | `_decay`, `OnDecayElapsed` | `_window`, `OnWindowElapsed` | "decay" là từ của một game; "cửa sổ" là khái niệm của tracker |
| cửa sổ vô hạn | không có | `PositiveInfinity` bỏ delay | combo theo lượt: chỉ đứt khi `Reset` (GĐ3); không thêm nhánh nào khác |
| `Dispose` | `_decay.Stop()` và gỡ `Subscription<BoxLidClosed>` | chỉ `_window.Stop()` | tracker không nghe bus; subscription thuộc chủ `MonoBehaviour`, bus tự gỡ (GĐ5) |

**Quyết định** — hết cửa sổ phát `Changed(0, 0)`: cùng một đường "về 0" cho HUD (tắt nhãn, làm rỗng thanh) với `Reset` ở GĐ3; phân
biệt lý do đứt là của dự án qua event riêng nếu cần. — `_window.Stop()` trước khi tạo delay mới: hai delay sống song song thì cái cũ về
0 giữa chừng; một handle cho một cửa sổ. — Handle là `Tween` struct: `Stop` là huỷ, không cấp phát `CancellationTokenSource` mỗi điểm.
Phương án bỏ: `CancellationTokenSource` + `UniTask.Delay` — một cấp phát và một `Dispose` mỗi điểm combo. — Callback `static tracker =>
tracker.OnWindowElapsed()` nhận tracker làm target của delay: không closure. — `Stop()` trên `Tween` mặc định là no-op, nên điểm đầu
tiên không cần kiểm `isAlive`. — Đồng hồ là `Tween.Delay` ngay trong tracker: delay đã là đồng hồ có handle dừng, không cần Ticker,
không cần `Update`. Phương án bỏ: `ComboSystem` `MonoBehaviour` bơm tick từ Ticker.

| Consumes | Produces |
|---|---|
| GĐ1 · PrimeTween `Tween.Delay<T>`, `Tween.Stop` | `IDisposable.Dispose`, `Changed(0, 0)` khi hết giờ, luật `PositiveInfinity` |

**Sơ đồ tương tác sau GĐ2**

```
  chủ ──AddPoint / Dispose+──► ComboTracker ──Stop+ · Delay(this, window)+──► PrimeTween
                                    ▲ OnWindowElapsed+ (Count 0)                 │
                                    └────────────────────────────────────────────┘
                               ──Changed(Count, window) / Changed(0, 0)+──► chủ
```

**Editor setup:** không.

**Kiểm:** compile sạch; test `[UnityTest]` chờ bằng thời gian scaled (`WaitForSeconds`), công thức hằng trừ ca ghi rõ.

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

**Mốc commit:** `feat(combo): cửa sổ hết hạn bằng Tween.Delay, Dispose cùng chủ`.

---

## Giai đoạn 3 — Chủ cắt chuỗi: `Reset` khi vào level

**Mục tiêu:** `Reset` đưa `Count` về 0, huỷ cửa sổ và phát `Changed(0, 0)`; chủ gọi lúc bắt đầu mỗi level (kể cả chơi lại). Xong giai đoạn
này tracker sống qua nhiều level của cùng một chủ mà HUD không mang combo cũ sang.

**Vì sao bây giờ:** chủ sống qua nhiều level nhưng tracker không biết level; đây là cửa ghi duy nhất còn thiếu để chủ cắt chuỗi chủ động,
và là cách đứt duy nhất của cửa sổ vô hạn (GĐ2).

### `ComboTracker.cs` › `API` › thêm sau `AddPoint`, trước `Dispose`

```csharp
        public void Reset()
        {
            Count = 0;
            _window.Stop();
            Changed?.Invoke(0, 0f);
        }
```

**Khác với bản dự án:** không — cùng thân, chỉ `Publish` thành `Changed`.

**Quyết định** — `Reset` luôn phát `Changed(0, 0)`, kể cả khi `Count` đang 0: HUD vào level mới phải về trạng thái rỗng dù chuỗi trước
đã tự đứt; chủ không phải nhớ "đã về 0 chưa". Phương án bỏ: chỉ phát khi `Count > 0` — HUD giữ state riêng để tự chặn, hai nơi giữ sự
thật. — `Reset` không phân biệt với hết giờ: HUD hiện có chỉ cần "về 0"; dự án cần phân biệt thì thêm event riêng, `Changed` giữ
nguyên.

| Consumes | Produces |
|---|---|
| GĐ2 | `Reset` |

**Sơ đồ tương tác sau GĐ3**

```
  chủ ──AddPoint / Reset+ / Dispose──► ComboTracker ──Stop · Delay──► PrimeTween
                                              ──Changed(0, 0)+──► chủ (HUD rỗng)
```

**Editor setup:** không.

**Kiểm:** compile sạch.

| Case | Input | Kỳ vọng |
|---|---|---|
| `Reset` giữa cửa sổ | `AddPoint` cửa sổ 0.2 s, `Reset`, chờ 0.4 s | đúng một `Changed(0, 0)`; `Count` 0 (delay cũ đã dừng) |
| `Reset` khi đang 0 | `new`, `Reset` | `Changed(0, 0)` một lần, không ném |
| `Reset` rồi điểm mới | `Reset`, `AddPoint` | `Count` 1, `Changed(1, window)` |
| `Reset` cửa sổ vô hạn | `_ => PositiveInfinity`, `AddPoint`, `Reset` | `Count` 0, `Changed(0, 0)` |
| `Reset` hai lần liền | `AddPoint`, `Reset`, `Reset` | hai `Changed(0, 0)` — chủ gọi hai lần thì HUD vẽ hai lần, tracker không khử trùng |

**Mốc commit:** `feat(combo): Reset`.

---

## Giai đoạn 4 — Tài liệu `docs/Runtime/ComboTracker/ComboTracker.md` và `.html` (agent)

`ComboTracker.md` mở đầu `Code: Runtime/Utilities/Combo/`. Mục: luồng `AddPoint → windowSeconds(Count) → Tween.Delay → OnWindowElapsed
→ Changed(0, 0)` ASCII (luồng mô tả ở trên) · bảng API (`ComboTracker(Func<int, float>)`, `Count`, `AddPoint`, `Reset`, `Dispose`,
`Changed`) · "Design decisions" gom các **Quyết định** của GĐ1–3 và dòng **Không làm** của Ngữ cảnh đã chốt · "Dùng trong dự án": chủ `MonoBehaviour` `new`
ở khởi tạo, `Reset` ở đầu level, `Dispose` ở `OnDestroy`; một hàm của chủ gọi `AddPoint` rồi phản hồi (tiếng, hình) cạnh nhau; HUD nghe
`Changed`: count 0 → thanh rỗng, nhãn tắt; count n → `ProgressBar.PlayAsync(window, 0, window, label, ct)` với `Use Unscaled Time` tắt
· ca `PositiveInfinity`, ca công thức trả 0. Không có "Trước khi chạy": tracker là class thuần, không wire tay; ô `Use Unscaled Time`
của `ProgressBar` ghi ở "Dùng trong dự án" như một bước kiểm.

`ComboTracker.html` theo `docs` › `.html`: Kịch bản (ghi điểm liên tiếp, ngừng tay, vào level mới, pause) · Cách dùng · Flow · Lưu ý
(`Use Unscaled Time`, `Dispose` cùng chủ, hai `Reset` liền là hai lần vẽ).

**Mốc commit:** `docs(combo): ComboTracker.md, ComboTracker.html`.

---

## Giai đoạn 5 — Dự án dùng bản Horcrux, xoá bản tạm (agent)

**Mục tiêu:** `GameDirector` sở hữu `Horcrux.Runtime.Utilities.Combo.ComboTracker` với công thức cửa sổ của dự án; một hàm của
`GameDirector` nghe `BoxLidClosed`, gọi `AddPoint` rồi `box.ShowCombo(count)` cạnh nhau; handler `Changed` đẩy `ComboChanged` lên bus
cho `ComboPill`. Xoá `Scripts/Director/ComboTracker.cs`. `ComboPill.cs`, `Box.cs`, `GameplayEvents.cs` không đổi dòng nào.

### `Scripts/Director/GameDirector.cs` › đầu file › thêm sau `using Horcrux.Runtime.Utilities.EventBus;`

```csharp
using Horcrux.Runtime.Utilities.EventBus;
using Horcrux.Runtime.Utilities.Combo;
```

### `GameDirector.cs` › `API` › `Init` — đoạn cũ → đoạn mới

Cũ:

```csharp
            _combo = new ComboTracker();
```

Mới:

```csharp
            _combo = new ComboTracker(ComboWindowSeconds);
            _combo.Changed += OnComboChanged;
```

`_combo?.Dispose()` trong `OnDestroy`, `_combo.Reset()` trong `StartLevelFlow`, `CurrentComboNumber => _combo?.Count ?? 0` giữ nguyên.

### `GameDirector.cs` › `Class Methods` › `RegisterEvents` › thêm sau `EventBus<TilePlayabilityChanged>.Subscribe(OnTilePlayabilityChanged);`

```csharp
            EventBus<TilePlayabilityChanged>.Subscribe(OnTilePlayabilityChanged);
            EventBus<BoxLidClosed>.Subscribe(OnBoxLidClosed);
```

Không giữ handle, cùng lý do với các dòng trên: `GameDirector` là `MonoBehaviour`, bus tự gỡ khi nó bị destroy. Nắp box chỉ đóng sau
`StartLevelAsync`, khi `Init` đã xong và `_combo` đã có.

### `GameDirector.cs` › `Class Methods` › thêm sau `OnTileMovedOnBox`, trước `OnShelfCleared`

```csharp
        // Một khoảnh khắc, mọi phản hồi cạnh nhau: đếm rồi tiếng của box theo count vừa đạt; HUD nghe qua ComboChanged.
        private void OnBoxLidClosed(BoxLidClosed e)
        {
            _combo.AddPoint();
            e.Box.ShowCombo(_combo.Count);
        }

        private void OnComboChanged(int count, float windowSeconds) =>
            EventBus<ComboChanged>.Publish(new ComboChanged(count, windowSeconds));

        /// <summary>Giây cho tới khi combo về 0: 10 s ở combo 1, bớt 1 s mỗi bậc tới combo 5, từ combo 6 là 5 s giảm 2% mỗi bậc.</summary>
        private static float ComboWindowSeconds(int count)
        {
            if (count <= 5)
                return 11 - count;

            const float baseDecay = 5f;
            var exponent = count - 6;
            return (float)(baseDecay * Math.Pow(0.98, exponent));
        }
```

Thân `ComboWindowSeconds` là thân `DecayDuration` của bản dự án với `Count` thành tham số: hành vi quan sát được không đổi; số là nháp
của developer, không phải mốc.

### Xoá

`Scripts/Director/ComboTracker.cs` + `.meta`.

### Tài liệu dự án nhắc bản tạm — sửa cùng lần

| File | Dòng | Sửa thành |
|---|---|---|
| `Scripts/Layout.md` › bảng mở đầu, hàng "Cố ý KHÔNG làm" | "không dùng `Horcrux ComboSystem`: luật combo khác" | bỏ vế này |
| `Scripts/Layout.md` › đoạn "Luồng combo" | `ComboTracker` (class thuần của `GameDirector`) cộng điểm | `GameDirector.OnBoxLidClosed` gọi `AddPoint` của `ComboTracker` (Horcrux) rồi `ShowCombo`; `OnComboChanged` publish `ComboChanged` |
| `Scripts/Entities/Entities.md` › hàng "Nắp chạm thân" | `ComboTracker` cộng combo rồi gọi `box.ShowCombo(count)` | `GameDirector.OnBoxLidClosed` cộng combo rồi gọi `box.ShowCombo(count)` |
| `Scripts/Feel.md` › gạch đầu dòng "Combo" | `ComboTracker` giữ cửa sổ decay bằng `Tween.Delay` … phát `ComboChanged` | `ComboTracker` (Horcrux) giữ cửa sổ bằng `Tween.Delay`, phát `Changed`; `GameDirector` publish `ComboChanged` |
| `Structure.md` › dòng `Director/` | sở hữu hai class thuần `ComboTracker`, `AutoPlayer` | sở hữu `ComboTracker` của Horcrux và class thuần `AutoPlayer` |

**Editor setup:** kiểm trên prefab HUD chứa `ComboPill`: `ProgressBar` ở ô `Bar` có `Use Unscaled Time` tắt, `Speed` 1 (tooltip của ô `Bar`
nói vậy). Thiếu: thanh rút theo thời gian thật trong khi cửa sổ đứng lúc `timeScale` 0, thanh rỗng trước khi combo đứt. Không đổi GUID
script nào: `GameDirector.cs`, `ComboPill.cs` ở lại dự án.

**Kiểm:** grep `class ComboTracker` trong `Assets/_CategoryJam` ra 0 · grep `Horcrux ComboSystem` trong `Assets/_CategoryJam` ra 0 ·
compile dự án sạch · test dự án xanh · **cần chơi thử** (developer): vào một level, đóng nắp hai box cách nhau dưới 10 s → pill "Combo
x2" và tiếng combo lúc nắp thứ hai chạm, thanh đầy rồi rút; ngừng tay tới khi thanh rỗng → nhãn tắt; đóng nắp tiếp → "Combo x1", không
tiếng (tiếng từ x2); chơi lại level giữa chuỗi → pill rỗng ngay. Dấu hiệu hỏng: thanh về rỗng cắt ngang chuỗi đang tăng (hai cửa sổ song
song) · pill mang số combo cũ sang level mới · tiếng combo phát ở x1 · thanh rút khi game đang pause.

**Mốc commit:** repo dự án `refactor(combo): GameDirector dùng ComboTracker của Horcrux` kèm con trỏ submodule.

---

## File → giai đoạn

| File | GĐ1 | GĐ2 | GĐ3 | GĐ4 | GĐ5 |
|---|---|---|---|---|---|
| `Runtime/Utilities/Combo/ComboTracker.cs` | tạo | `IDisposable`, `_window`, `AddPoint` mở đồng hồ, `Dispose`, `OnWindowElapsed` | `Reset` | | |
| `Runtime/Abstractions/Composites/Combo/IComboSystem.cs`, `Runtime/Implementations/Composites/Combo/ComboSystem.cs` | xoá (Project window) | | | | |
| `Tests/PlayMode/ComboTrackerTests.cs` (agent) | tạo | thêm case | thêm case | | |
| `docs/Runtime/ComboTracker/ComboTracker.md`, `ComboTracker.html` (agent) | | | | tạo | |
| dự án: `Scripts/Director/GameDirector.cs`, xoá `Scripts/Director/ComboTracker.cs`; `Scripts/Layout.md`, `Scripts/Entities/Entities.md`, `Scripts/Feel.md`, `Structure.md` | | | | | đổi |
