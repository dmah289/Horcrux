# Ticker System Implementation Plan

> **Loại tài liệu:** Plan (`DOCS_SKILL` Phần C). `.md` thiết kế (Phần A) + `.html` (Phần B) viết **sau** khi có source.
>
> **For agentic workers:** REQUIRED SUB-SKILL: superpowers:subagent-driven-development hoặc superpowers:executing-plans. Steps dùng checkbox (`- [ ]`).

**Goal:** `ITicker` — **một** `Update` trung tâm phát 2 nhịp (mỗi frame · pause-changed), đăng ký/huỷ **zero-GC** và an toàn khi listener tự huỷ giữa lúc đang duyệt.

**Architecture:**

```
Abstractions/Foundations/
├── IOptionalService.cs      service TUỲ CHỌN — cố tình KHÔNG có accessor throw
└── Ticker/                  ITickable · IPauseAware · ITicker
Utilities/Common/
└── DeferredList.cs          list an toàn khi huỷ giữa lúc duyệt
Implementations/Foundations/Ticker/
└── TickerService.cs         MonoBehaviour DUY NHẤT trong SDK có Update
```

**Tech Stack:** C#, `Time.unscaledDeltaTime`, `Sisus.Init`. Không Addressables/UniTask.

## Global Constraints

| Ràng buộc | Giá trị |
|---|---|
| Namespace | `Horcrux.Runtime.Abstractions.Ticker` · `…Implementations.Ticker` · `…Implementations.Utilities.Common` |
| Zero-GC | Listener là **interface** + `List<T>` (không `event Action<float>`); không alloc trong `Update` |
| SOLID | 2 interface 1-method (ISP); `TickerService` không biết listener làm gì (D) |
| An toàn huỷ | `Remove` có hiệu lực **ngay** — listener vừa `OnDisable` không bị tick thêm 1 lần |
| Try/catch | quanh **từng** callback |
| Nhịp cơ sở | `unscaledDeltaTime` — combo không đóng băng khi `timeScale = 0` |
| Editor-first (§C.1) | Không có số cảm giác, không có tham chiếu cần gán — chỉ **một** bước Editor: đặt component vào scene bootstrap |

## Ngữ cảnh đã chốt

| Nhóm | Chốt |
|---|---|
| **Ai gọi** | `ITickable`: `ComboSystem`, `ComboMeter`, `HitstopChannel`, `CameraShakeChannel`. `IPauseAware`: `ComboSystem` (đóng băng cửa sổ combo). |
| **Mục tiêu** | Xoá `Update()` rải rác: N listener = **một** managed↔native transition/frame. Nghiệm thu: 0 B GC alloc/frame khi không ai đăng ký/huỷ. |
| **Ngân sách** | `OnTick` mỗi frame, ~5 listener. Đăng ký/huỷ là **hot path thật** (UI mở-đóng). Mobile 60 FPS. |
| **Ranh giới** | Ticker **chỉ** phát nhịp. Không giữ giờ UTC, không format, không đếm ngược. `offlineSeconds` dùng đồng hồ thiết bị — đủ để *đóng băng* state, **không** đủ để cấp thưởng theo giờ. |
| **Cố ý KHÔNG làm + lý do** | ① **Nhịp 1 Hz (`ISecondTickable`)** — không caller nào; countdown UI là thứ *sẽ* cần chứ chưa cần. Cắt nó cắt luôn bộ tích lũy + guard bắt kịp + 1 `DeferredList`. ② **`Destroyed` event** — không ai subscribe. ③ `event Action<float>` cho tick — mỗi `+=`/`-=` alloc mảng invocation-list mới, và đăng ký/huỷ ở đây là hot path. ④ Overload `Register` chung tên — class implement 2 nhịp sẽ nhập nhằng CS0121. ⑤ `ITimeService`/server time/`Countdown`/`TimeFormatter` — combo cần *khoảng* thời gian, không cần *mốc* tin được. |

---

## §0. Hai điều cần biết

### 0.1. Vì sao một nguồn tick — phép kiểm

Mỗi `MonoBehaviour.Update()` là một lần Unity gọi xuyên biên managed↔native, chi phí cố định **không phụ thuộc thân hàm**.

**Phép kiểm tái lập:** scene A = 200 GameObject, mỗi cái một component có `Update()` **rỗng**. Scene B = 1 component gọi 200 lần vào một `List<ITickable>` thân rỗng. So `PlayerLoop` trong Profiler — B phải thấp hơn A rõ rệt. Nếu phép kiểm cho ra bằng nhau thì hệ này vô nghĩa.

### 0.2. `offlineSeconds` — hai chỗ dễ sai

```
offlineSeconds = max(0, (resumeUtc − pauseUtc) tính theo giây)
```

| Chi tiết | Vì sao |
|---|---|
| Mốc bằng `DateTime.UtcNow` | `Time.time` **đóng băng** khi app ở background; `DateTime.Now` lệch nếu người chơi đổi timezone giữa 2 phiên |
| Kẹp `≥ 0` | Người chơi lùi đồng hồ máy lúc background ⇒ hiệu số **âm** ⇒ mọi consumer tính sai (cửa sổ combo âm) |

---

## Bản đồ triển khai

| Task | File | Nội dung |
|---|---|---|
| 1 | `Abstractions/Foundations/IOptionalService.cs` · `Utilities/Common/DeferredList.cs` | 2 nền dùng chung |
| 2 | `Abstractions/Foundations/Ticker/` — `ITickable.cs` · `IPauseAware.cs` · `ITicker.cs` | contract |
| 3 | `Implementations/Foundations/Ticker/TickerService.cs` | `Update` duy nhất |

Thứ tự: **1 → 2 → 3**.

---

### Task 1: `IOptionalService<T>` + `DeferredList<T>`

**Files:**
- Create: `Assets/Horcrux/Runtime/Abstractions/Foundations/IOptionalService.cs`
- Create: `Assets/Horcrux/Runtime/Utilities/Common/DeferredList.cs`

**Interfaces:**
- Produces: `interface IOptionalService<out T>` — `static bool TryGet(out T)` · `sealed class DeferredList<T> where T : class` — `Count` · `this[int]` · `Add` · `Remove` · `Compact` · `Clear`

**Quyết định thiết kế:**

| Quyết định | Lý do |
|---|---|
| `IOptionalService` **không** khai `Service` | Consumer không thể viết nhánh throw dù muốn — **compiler** chặn. Đây là điều kiện để `ComboSystem` chạy được khi thiếu hệ Feedback |
| Không làm `Service => TryGet(…) ? s : default` | Trả `null` im lặng tệ hơn throw: NRE nổ xa nguyên nhân |
| `Remove` đặt **tombstone** (`null`), không `RemoveAt` | `RemoveAt` giữa lúc duyệt làm lệch chỉ số ⇒ bỏ sót listener. Tombstone giữ chỉ số ổn định, và huỷ **có hiệu lực ngay** |
| `Add` **ghi thẳng**, không hàng đợi | Thêm vào cuối `List` không ảnh hưởng phần tử đang duyệt ⇒ bỏ được cả một list + một nhánh |
| Dup-guard `Contains` trong `Add` | Một dòng, chặn bug **im lặng** tệ nhất của hệ: đăng ký 2 lần ⇒ cửa sổ combo cạn gấp đôi mà không có lỗi nào. `n ≤ 16` nên O(n) là vài phép so tham chiếu |
| `Compact` chỉ chạy khi có tombstone | Frame bình thường = **một** phép so `int` |

- [ ] **Step 1: `IOptionalService.cs`**

```csharp
namespace Horcrux.Runtime.Abstractions
{
    /// <summary>Marker cho service TUỲ CHỌN: thiếu nó là hợp lệ, consumer phải degrade chứ không throw.</summary>
    /// <remarks>
    /// Khác <see cref="IService{T}"/> ở đúng một điểm — CỐ TÌNH không khai accessor <c>Service</c>,
    /// nên consumer không thể viết nhánh throw dù muốn: compiler chặn, không phải code-review chặn.
    ///
    /// KHÔNG thêm <c>Service => TryGet(out var s) ? s : default</c> — trả null im lặng còn tệ hơn
    /// throw: NullReferenceException sẽ nổ ở chỗ khác, xa nguyên nhân thật.
    /// </remarks>
    public interface IOptionalService<out T>
    {
        public static bool TryGet(out T service) => Sisus.Init.Service.TryGet(out service);
    }
}
```

- [ ] **Step 2: `DeferredList.cs`**

```csharp
using System.Collections.Generic;

namespace Horcrux.Runtime.Implementations.Utilities.Common
{
    /// <summary>List cho phép huỷ phần tử NGAY TRONG lúc caller đang duyệt, không alloc sau khởi tạo.</summary>
    /// <remarks>
    /// Bài toán: listener thường tự huỷ đăng ký bên trong callback của chính nó (<c>OnDisable</c> gọi
    /// từ một tick). <c>RemoveAt</c> lúc đó làm lệch chỉ số của vòng <c>for</c> đang chạy ⇒ bỏ sót
    /// phần tử kế tiếp.
    ///
    /// Cách giải: <c>Remove</c> đặt tombstone (<c>null</c>) — có hiệu lực ngay, chỉ số không đổi;
    /// <c>Compact</c> gỡ tombstone ở đầu vòng duyệt kế.
    ///
    /// Khuôn duyệt bắt buộc ở phía caller:
    /// <code>
    /// list.Compact();
    /// for (int i = 0; i &lt; list.Count; i++)
    /// {
    ///     T item = list[i];
    ///     if (item == null) continue;          // tombstone
    ///     try { item.DoSomething(); } catch (Exception e) { Debug.LogException(e); }
    /// }
    /// </code>
    /// </remarks>
    public sealed class DeferredList<T> where T : class
    {
        private readonly List<T> _items;
        private int _tombstoneCount;

        /// <param name="capacity">
        /// Số phần tử dự kiến — pre-alloc để không resize lúc chạy. Sai số chỉ khiến resize một lần
        /// lúc khởi động, không ảnh hưởng hành vi.
        /// </param>
        public DeferredList(int capacity = 8) => _items = new List<T>(capacity);

        /// <summary>Số slot duyệt được — GỒM CẢ tombstone. Caller phải bỏ qua phần tử null.</summary>
        public int Count => _items.Count;

        public T this[int index] => _items[index];

        /// <summary>Thêm vào cuối. An toàn khi đang duyệt (không ảnh hưởng phần tử phía trước).</summary>
        /// <returns><c>false</c> nếu null hoặc đã có — chống đăng ký 2 lần → callback 2 lần.</returns>
        public bool Add(T item)
        {
            if (item == null || _items.Contains(item)) return false;

            _items.Add(item);
            return true;
        }

        /// <summary>Huỷ — có hiệu lực NGAY, kể cả khi caller đang duyệt.</summary>
        public bool Remove(T item)
        {
            if (item == null) return false;

            int index = _items.IndexOf(item);
            if (index < 0) return false;

            _items[index] = null;           // tombstone: giữ chỉ số ổn định cho vòng đang chạy
            _tombstoneCount++;
            return true;
        }

        /// <summary>Gỡ tombstone. Gọi một lần ngay trước mỗi vòng duyệt.</summary>
        public void Compact()
        {
            if (_tombstoneCount == 0) return;   // frame bình thường: một phép so int

            // Duyệt NGƯỢC: RemoveAt luôn rơi vào cuối list ⇒ O(1), không dịch mảng.
            for (int i = _items.Count - 1; i >= 0; i--)
            {
                if (_items[i] == null) _items.RemoveAt(i);
            }

            _tombstoneCount = 0;
        }

        public void Clear()
        {
            _items.Clear();
            _tombstoneCount = 0;
        }
    }
}
```

- [ ] **Step 3: Kiểm chứng**

| Kịch bản | Kỳ vọng |
|---|---|
| `Add(a)`, `Add(a)` | lần 2 trả `false`; `Count == 1` |
| có `[a,b,c]`; trong callback của `c` gọi `Remove(b)` | `this[1] == null` ngay; `a`, `c` vẫn được duyệt đủ |
| `Remove` phần tử không có | `false`, không throw |
| `Compact` khi không có tombstone | không duyệt list, không alloc |

- [ ] **Step 4: Commit** — `feat(sdk): add IOptionalService + DeferredList`

---

### Task 2: 3 contract

**Files:** `Assets/Horcrux/Runtime/Abstractions/Foundations/Ticker/` — `ITickable.cs` · `IPauseAware.cs` · `ITicker.cs`

**Quyết định thiết kế:**

| Quyết định | Lý do |
|---|---|
| **2 interface 1-method**, không 1 interface 2-method | Combo cần cả hai; nhưng `HitstopChannel`/`CameraShakeChannel`/`ComboMeter` chỉ cần tick. Gộp = buộc 3 class viết method rỗng (ISP) |
| Tên **phân biệt theo nhịp**, không overload `Register` | Class implement 2 nhịp (`ComboSystem`) ⇒ `Register(this)` nhập nhằng, compile lỗi CS0121 |
| Tham số tên `unscaledDeltaTime` | Tên nói rõ hitstop không ảnh hưởng — khỏi tra tài liệu |
| `OnPauseChanged` mang **cả** `isPaused` và `offlineSeconds` | Mọi hệ theo thời gian cần đúng số này; tính riêng = lặp + lệch nhau |
| `ITicker : IService<>` (bắt buộc, không optional) | Thiếu ticker là **lỗi cấu hình** ⇒ phải throw sớm để lộ ra |

- [ ] **Step 1: 3 file**

```csharp
// ── ITickable.cs ────────────────────────────────────────────────────────────
namespace Horcrux.Runtime.Abstractions.Ticker
{
    /// <summary>Nhận nhịp MỖI FRAME.</summary>
    public interface ITickable
    {
        /// <param name="unscaledDeltaTime">
        /// Giây thực từ frame trước, KHÔNG bị <c>Time.timeScale</c> bóp — nhờ vậy hitstop/slow-mo/
        /// pause-gameplay không đóng băng logic đếm thời gian.
        /// </param>
        void OnTick(float unscaledDeltaTime);
    }
}

// ── IPauseAware.cs ─────────────────────────────────────────────────────────
namespace Horcrux.Runtime.Abstractions.Ticker
{
    /// <summary>Nhận thông báo app pause/resume, kèm thời lượng đã ở background.</summary>
    public interface IPauseAware
    {
        /// <param name="isPaused"><c>true</c> = vừa vào background; <c>false</c> = vừa quay lại.</param>
        /// <param name="offlineSeconds">
        /// Giây đã ở background, luôn ≥ 0 (đã kẹp — lùi đồng hồ máy không cho ra số âm).
        /// Bằng 0 khi <paramref name="isPaused"/> là true.
        /// Đo bằng đồng hồ THIẾT BỊ ⇒ đủ để đóng băng/khôi phục state, KHÔNG đủ tin để cấp thưởng.
        /// </param>
        void OnPauseChanged(bool isPaused, float offlineSeconds);
    }
}

// ── ITicker.cs ─────────────────────────────────────────────────────────────
namespace Horcrux.Runtime.Abstractions.Ticker
{
    /// <summary>Nguồn nhịp trung tâm: MỘT <c>Update</c> duy nhất trong SDK, phát 2 nhịp độc lập.</summary>
    /// <remarks>
    /// Vì sao không dùng <c>event Action&lt;float&gt;</c>: đăng ký/huỷ ở đây là hot path thật (mỗi
    /// popup/UI mở-đóng), mà mỗi <c>+=</c>/<c>-=</c> cấp phát một mảng invocation-list mới.
    /// Interface + List là 0 byte.
    ///
    /// Vì sao 2 cặp Add/Remove tên khác nhau thay vì overload <c>Register</c>: <c>ComboSystem</c>
    /// implement cả hai nhịp, nên <c>Register(this)</c> sẽ nhập nhằng (CS0121).
    ///
    /// Listener PHẢI huỷ đăng ký trong <c>OnDisable</c> — nếu không, ticker giữ reference chết và gọi
    /// vào object đã destroy.
    /// </remarks>
    public interface ITicker : IService<ITicker>
    {
        void AddTickListener(ITickable listener);
        void RemoveTickListener(ITickable listener);

        void AddPauseListener(IPauseAware listener);
        void RemovePauseListener(IPauseAware listener);
    }
}
```

- [ ] **Step 2: Kiểm chứng** — `ComboSystem` implement **cả** `ITickable` và `IPauseAware` phải gọi được `AddTickListener(this)` và `AddPauseListener(this)` mà không lỗi nhập nhằng. Đây chính là ca mà overload `Register` sẽ vỡ.

- [ ] **Step 3: Commit** — `feat(sdk): add ticker contracts`

---

### Task 3: `TickerService`

**Files:** Create `Assets/Horcrux/Runtime/Implementations/Foundations/Ticker/TickerService.cs`

**Quyết định thiết kế:**

| Quyết định | Lý do |
|---|---|
| `[Service(FindFromScene = true)]` + `DontDestroyOnLoad` | Cùng khuôn `PoolManager`. Mất nhịp giữa lúc load scene = mất cửa sổ combo |
| **2** `DeferredList` typed, không 1 list `object` | List `object` phải `is`/cast mỗi phần tử mỗi frame |
| `Compact()` ngoài vòng lặp | Một lần/nhịp, không kiểm điều kiện mutate mỗi phần tử |
| `try/catch` quanh **từng** callback | 1 listener throw không kill các listener còn lại |
| `Debug.LogException`, không `LogError($"…{e}")` | Interpolation string là alloc; `LogException` giữ stack trace |
| Xử lý **cả** `OnApplicationPause` và `OnApplicationFocus` | Mobile bắn `Pause`; editor/desktop chỉ bắn `Focus`. Cờ `_isPaused` dedupe khi cả hai bắn |
| `_hasPausedOnce` guard | Frame đầu luôn có `Focus(true)` — không guard thì mọi listener nhận "resume giả" lúc khởi động |

**Editor setup (§C.1) — bước thật:**

1. Tạo GameObject `[Ticker]` ở **scene bootstrap** (scene load đầu tiên) → add `TickerService`.
2. Không có field nào cần gán.
3. Kiểm: vào Play Mode, một `ComboSystem` trong scene phải init được. Nếu Console báo không tìm thấy `ITicker` ⇒ chưa làm bước 1 (`FindFromScene = true` **không** tự sinh object).

- [ ] **Step 1: `TickerService.cs`**

```csharp
using System;
using Horcrux.Runtime.Abstractions.Ticker;
using Horcrux.Runtime.Implementations.Utilities.Common;
using Sisus.Init;
using UnityEngine;

namespace Horcrux.Runtime.Implementations.Ticker
{
    /// <summary>MonoBehaviour DUY NHẤT trong SDK được phép có <c>Update</c>. Phát 2 nhịp cho mọi hệ.</summary>
    /// <remarks>
    /// Mỗi <c>Update()</c> là một lần gọi xuyên biên managed↔native với chi phí cố định KHÔNG phụ
    /// thuộc thân hàm — xem §0.1 của plan để có phép kiểm bằng Profiler.
    ///
    /// Đặt component này ở scene bootstrap. <c>DontDestroyOnLoad</c> giữ nó sống xuyên scene, nên
    /// listener đăng ký ở scene gameplay phải tự huỷ khi scene đó unload.
    /// </remarks>
    [Service(typeof(ITicker), FindFromScene = true)]
    public sealed class TickerService : MonoBehaviour, ITicker
    {
        private readonly DeferredList<ITickable> _tickables = new(16);
        private readonly DeferredList<IPauseAware> _pauseListeners = new(4);

        private bool _isPaused;
        private bool _hasPausedOnce;
        private DateTime _pauseUtc;

        #region Unity Callbacks
        private void Awake() => DontDestroyOnLoad(this);

        private void Update()
        {
            float unscaledDeltaTime = Time.unscaledDeltaTime;   // đọc property engine 1 lần

            _tickables.Compact();

            for (int i = 0; i < _tickables.Count; i++)
            {
                ITickable listener = _tickables[i];
                if (listener == null) continue;                  // tombstone: vừa huỷ giữa vòng này

                // try/catch TỪNG callback: 1 listener lỗi không kill các listener còn lại.
                try { listener.OnTick(unscaledDeltaTime); }
                catch (Exception e) { Debug.LogException(e); }
            }
        }

        // Mobile đi qua đây; editor/desktop KHÔNG bắn Pause nên phải nghe cả Focus.
        private void OnApplicationPause(bool isPaused) => SetPaused(isPaused);
        private void OnApplicationFocus(bool hasFocus) => SetPaused(!hasFocus);

        private void OnDestroy()
        {
            _tickables.Clear();
            _pauseListeners.Clear();
        }
        #endregion

        private void SetPaused(bool isPaused)
        {
            if (_isPaused == isPaused) return;                    // dedupe Pause ↔ Focus
            _isPaused = isPaused;

            float offlineSeconds = 0f;

            if (isPaused)
            {
                _hasPausedOnce = true;
                _pauseUtc = DateTime.UtcNow;                      // UTC: Time.time đóng băng, Now lệch TZ
            }
            else
            {
                if (!_hasPausedOnce) return;                       // frame đầu luôn có Focus(true)

                double elapsed = (DateTime.UtcNow - _pauseUtc).TotalSeconds;
                // Kẹp ≥ 0: lùi đồng hồ máy lúc background cho ra hiệu số ÂM, và mọi consumer nhận
                // số âm sẽ tính sai (cửa sổ combo âm).
                offlineSeconds = elapsed > 0d ? (float)elapsed : 0f;
            }

            _pauseListeners.Compact();

            for (int i = 0; i < _pauseListeners.Count; i++)
            {
                IPauseAware listener = _pauseListeners[i];
                if (listener == null) continue;

                try { listener.OnPauseChanged(isPaused, offlineSeconds); }
                catch (Exception e) { Debug.LogException(e); }
            }
        }

        #region ITicker
        public void AddTickListener(ITickable listener) => _tickables.Add(listener);
        public void RemoveTickListener(ITickable listener) => _tickables.Remove(listener);

        public void AddPauseListener(IPauseAware listener) => _pauseListeners.Add(listener);
        public void RemovePauseListener(IPauseAware listener) => _pauseListeners.Remove(listener);
        #endregion
    }
}
```

- [ ] **Step 2: Kiểm chứng**

| # | Input | Kỳ vọng |
|---|---|---|
| 1 | 1 `ITickable`, 60 frame @16.7ms | `OnTick` gọi đúng 60 lần, tổng `dt ≈ 1.0s` |
| 2 | listener gọi `RemoveTickListener(this)` trong `OnTick` | các listener sau nó **vẫn** được gọi đủ |
| 3 | listener throw | listener kế tiếp vẫn được gọi |
| 4 | pause 10s rồi resume | `OnPauseChanged(true, 0)` → `(false, ~10)` |
| 5 | vào Play Mode, không pause | **không** có `OnPauseChanged` nào |
| 6 | lùi đồng hồ 1h lúc background | `offlineSeconds == 0` |
| 7 | `timeScale = 0` | `OnTick` **vẫn** chạy với `dt > 0` |
| 8 | Profiler: 16 listener, không đăng ký/huỷ | **0 B** GC Alloc/frame |

- [ ] **Step 3: Cập nhật `PendingSystems.md` §4** — trỏ plan này; ghi rõ plan chỉ làm 2 nhịp (`ITickable`, `IPauseAware`).

- [ ] **Step 4: Commit** — `feat(sdk): add central TickerService`

---

## Ghi chú thực thi

- **Hệ dùng tiếp:** `ComboSystem.md`, `FeedbackSystem.md`.
- **Mở rộng sau** (đều **additive**):
  - **Nhịp 1 Hz** (`ISecondTickable` + 1 cặp Add/Remove): tích lũy `dt` rồi phát khi vượt 1s. Hai chi tiết đã biết trước, ghi lại để không phải tìm lại: (a) dùng phép **trừ** ngưỡng `acc -= 1f` trong `while`, **không** gán `acc = 0` — gán 0 bỏ phần dư nên nhịp trôi chậm dần (thử `dt = 0.6`: gán-0 cho 1 nhịp/1.2s, chậm 20%); (b) **kẹp số nhịp mỗi frame** (~4) rồi xoá phần nợ, vì frame đầu sau khi resume 3 giờ có thể trả `dt` rất lớn ⇒ `while` không trần sẽ treo app.
  - `ITimeService` + `IServerTimeProvider` (chống tua giờ) · `Countdown` struct + `TimeFormatter` · `Destroyed` event · `AddLateTickListener`.
