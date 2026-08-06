# NewEventBus Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Thay EventBus cũ bằng bus không bỏ sót listener khi mutate giữa lúc dispatch, không leak khi object destroy, và huỷ đăng ký được cả lambda.

**Architecture:** `EventBus<T>` static generic giữ một `DeferredList<Action<T>>`. Xoá listener = đặt tombstone (`null`), không dồn chỉ số → vòng dispatch không lệch; `Compact` chỉ chạy khi đã ra khỏi mọi Publish lồng nhau. `Subscribe` trả `Subscription<T>` làm đường huỷ duy nhất; bus tự prune listener có `Target` là `UnityEngine.Object` đã destroy.

**Tech Stack:** C#, Unity 6000.3 (`apiCompatibilityLevel: 6`), `UnityEngine.Debug`. Không Addressables/UniTask/InitArgs — Utility static thuần.

## Ngữ cảnh đã chốt

| Nhóm | Chốt |
|---|---|
| **Ai gọi** | Caller thật đầu tiên: **Toast §11a** (`Runtime/PendingSystems.md:1189,1206`). Kế đó: impl vendor "chỉ phát event" (anti-pattern #2). Hiện **chưa có caller nào trong code** → xoá bản cũ an toàn, không cần shim |
| **Mục tiêu** | Publish/subscribe theo type · zero-alloc lúc dispatch · không sót/lặp listener khi mutate giữa dispatch · không leak khi MonoBehaviour destroy · dev **không** phải ghép tay từng subscribe với một unsubscribe |
| **Ngân sách** | **KHÔNG hot path** — ~10–50 dispatch/giây (UI, luồng game). Cho phép phép kiểm per-listener lúc dispatch |
| **Ranh giới** | Bus chỉ dispatch **tức thì**. Gộp cuối frame là việc của consumer (`Invalidate()` + `PlayerLoopTiming.LastUpdate`, `:1256`). `ITutorialSignalBus` cố ý là bus riêng (`:1907`) |
| **Hướng mở rộng thật** | Priority · handler nhận `in T` — cả hai thêm sau bằng **overload mới**, không sửa chữ ký cũ → "thêm sau rẻ → để lại" |

**Cố ý KHÔNG làm + lý do:**

| # | Không làm | Lý do |
|---|---|---|
| ① | **`priority`** | Chưa caller nào cần thứ tự. Bỏ luôn được phép chèn có thứ tự O(n). Thêm lại = overload, additive |
| ② | **`IEventBus<T>`** | Không có implementation thứ hai thật → §C.2 gọi đây là over-engineering. Generics vẫn giữ O/L ở tầng type |
| ③ | **Handler nhận `in T`** | Không hot path; `Action<T>` là thứ dev đoán đúng ngay. `Publish` **vẫn** nhận `in T` vì miễn phí |
| ④ | **Bus scoped (per-scene/per-match)** | Không nhu cầu |
| ⑤ | **Thread-safe / lock** | Main-thread only, tài liệu hoá |
| ⑥ | **Deferred/queued dispatch** | Thuộc consumer (xem Ranh giới) |
| ⑦ | **`EventListenerBehaviour` base class** | Loại trừ nhau với `MonoBehaviour<TDep>` của InitArgs (12 arity). `ToastService` cần DI nên không dùng được. Auto-prune phủ đúng ca đó mà không chiếm chỗ kế thừa |
| ⑧ | **Assembly test `Horcrux.Tests`** | Chốt không tạo → thay bằng bảng kiểm chứng + script verify (Task 4). Kéo theo: **không** thêm `InternalsVisibleTo` |
| ⑨ | **Hook `Action<Exception>`** | Không có consumer thứ hai cho policy lỗi |
| ⑩ | **Cap độ sâu đệ quy** | `dispatchDepth` đã có sẵn cho `Compact`, nhưng chưa input thật nào chạm biên đệ quy vô hạn |
| ⑪ | **Bỏ static ctor để giữ `beforefieldinit`** | `static EventBus()` tường minh khiến CLR phải kiểm type-init trước **mỗi** lần truy cập static (kể cả `Publish`). Thay bằng field-initializer sẽ giữ được `beforefieldinit`, nhưng: JIT elide check sau lần gọi đầu, và ở 10–50 dispatch/giây chi phí này không đo được. Static ctor tường minh nói rõ ý định hơn → giữ. **Đổi ý khi và chỉ khi** profiler chỉ vào đúng chỗ này |
| ⑫ | **`CollectionsMarshal.AsSpan` + `AggressiveInlining`** | Bỏ được lớp gọi `DeferredList[i]` → `List[i]`. Là kỹ thuật hot-path; ở đây chưa xác nhận hot path nên theo bảng chốt phạm vi thì không làm. Cũng sẽ buộc phơi `List<T>` nội bộ ra ngoài |
| ⑬ | **Facade non-generic `EventBus.Publish<T>(...)`** | Sẽ cho `EventBus.Publish(new ToastRequest{…})` suy luận được `T` (đúng như `:1206` viết), nhưng tạo class trùng tên với namespace `…Utilities.EventBus` (CA1724) và thành 2 đường làm cùng một việc. Chọn: giữ một đường `EventBus<T>.Publish`, và **sửa `:1206`** cho khớp |

## Global Constraints

| Ràng buộc | Giá trị |
|---|---|
| Namespace | `Horcrux.Runtime.Utilities.EventBus` · `Horcrux.Runtime.Utilities.Common` |
| Assembly | `com.horcrux.runtime` (mọi file trong `Assets/Horcrux/Runtime/`) |
| Từ vựng API | `IEvent` · `Publish` · `Subscribe` — theo `PendingSystems.md:1189`. **Không** dùng lại `IEventDTO`/`Raise`/`Register` |
| Event DTO | **bắt buộc `readonly struct`** implement `IEvent`. Listener nhận bản copy → field mutable bị sửa mất im lặng |
| Zero-GC | Không alloc trong `Publish`. `Subscription<T>` là `readonly struct`. `Subscribe` alloc đúng 1 delegate (method-group conversion, không tránh được) |
| An toàn mutate | Xoá listener = **tombstone**, không `RemoveAt` của `List`. `Compact` **chỉ** khi `dispatchDepth == 0` |
| Hành vi đã chốt | `Publish` chụp `Count` **một lần** → listener đăng ký giữa lúc Publish **không** được gọi ở lần đó |
| Try/catch | quanh **từng** callback (SKILL yêu cầu) → `Debug.LogException` |
| Thread | **main-thread only** — không lock |
| Đường huỷ công khai | **duy nhất** `Subscription<T>.Dispose()`. `Unsubscribe`/`Clear` là `internal` |
| Xoá bản cũ | `EventBinding.cs` · `IEventBusListener.cs` · `EventBus.md` (+ `.meta`) — anti-pattern #4: không để 2 bus song song |

---

## §0. Ba điều cần biết trước khi code

### §0.1. Vì sao static generic, không `Dictionary<Type, List<Delegate>>`

| Tiêu chí | `Dictionary<Type, List<Delegate>>` | `EventBus<T>` static generic | Thắng |
|---|---|---|---|
| Tìm listener list | hash `typeof(T)` → bucket → so sánh | **static field load**, không tìm gì | generic ✓ |
| Type safety | `List<Delegate>` → phải cast `(Action<T>)` lúc gọi | compile-time, không cast | generic ✓ |
| Sai kiểu | cast sai chỉ nổ **runtime** | không có đường sai | generic ✓ |
| Liệt kê mọi bus | được (duyệt dictionary) | **không được** → phải có Registry | dictionary ✓ |
| Code size IL2CPP | 1 bản dùng chung | 1 bản **cho mỗi `T`** → phình | dictionary ✓ |

**Phép kiểm tái lập:** 2 bản, mỗi bản 1 listener rỗng, `Publish` 1e6 lần trong `Stopwatch`. Bản generic phải nhanh hơn rõ rệt và `GC.GetTotalMemory` không tăng — nếu bằng nhau thì lợi thế generic ở đây là tưởng tượng.

**Trade-off đã nhận:** phình code size đổi lấy dispatch không lookup + type-safe. Vài chục event type → phình không đáng kể.

### §0.2. Vì sao tombstone, không dồn chỉ số — lõi của cả hệ

Đây là **bug bản cũ**, và là lý do tồn tại của `DeferredList`. Quy ước đã chốt ở `Implementations/Foundations/Ticker/TickerSystem.md` Task 1: *"`RemoveAt` giữa lúc duyệt làm lệch chỉ số ⇒ bỏ sót listener"*.

**Bản cũ — `RemoveAt` dồn chỉ số:**

```
listeners = [A, B, C]        use case: A "nghe một lần rồi tự huỷ"

i=0 → gọi A → A gọi Unsubscribe(A) → RemoveAt(0) → [B, C]
i=1 → đọc listeners[1] = C
                                     ↑ B BỊ BỎ SÓT — im lặng, không exception nào
```

Không có `InvalidOperationException` để lộ ra — đó chỉ xảy ra với enumerator, còn đây là vòng lặp chỉ số. Sai mà không báo.

**Bản mới — tombstone giữ chỉ số bất động:**

```
listeners = [A, B, C]        count chụp 1 lần = 3

i=0 → A  → A gọi Dispose → tombstone tại 0 → [·, B, C]   (· = null)
i=1 → B  → chạy ✓                                        ← không còn bị sót
i=2 → C  → chạy ✓
ra vòng → depth==0 && TombstoneCount>0 → Compact() → [B, C]
```

Hai điều kiện đi kèm, cả hai đều bắt buộc:

| Điều kiện | Vì sao |
|---|---|
| `Compact` chờ `dispatchDepth == 0` | `Compact` *dồn* chỉ số — đúng cái tombstone tồn tại để tránh. Một listener publish lồng cùng type mà cấp lồng `Compact` thì `i` của vòng ngoài trỏ sai. Một biến `int` là đủ, và đó cũng là thứ làm nested Publish an toàn |
| Chụp `Count` **một lần** | Đọc `Count` mỗi vòng → listener tự `Subscribe` làm vòng lặp **tự nuôi**, publish không bao giờ kết thúc. Chụp một lần → listener mới chờ lần Publish sau. Hành vi này **quan sát được**, phải ghi vào `.md` |

### §0.3. `struct` constraint mua được gì — và bản `.md` cũ sai chỗ nào

`EventBus.md` cũ dành phần dài nhất khẳng định: thiếu `struct` constraint thì IL sinh `box T` trước `callvirt Action<T>.Invoke`, và `constrained.` cứu boxing. **Cả hai đều sai với code này** — nêu ra để người code lại không lặp lại niềm tin đó:

| Khẳng định cũ | Thực tế |
|---|---|
| `Action<T>.Invoke` box `T` khi thiếu constraint | `Invoke` có signature `void Invoke(!0)` — tham số **đúng bằng `T`**. Truyền `T` vào chỗ nhận `T` không sinh `box`, có constraint hay không cũng vậy |
| `constrained.` cứu boxing ở đây | `constrained.` chỉ sinh khi gọi virtual/interface method **với chính `T` làm receiver** (`dto.SomeMethod()`). `IEvent` **rỗng** → không bao giờ xảy ra |

Boxing *sẽ* xảy ra ở thiết kế khác: `Publish(IEvent)` non-generic, listener `Action<IEvent>`, hoặc queue `List<IEvent>`. Không phải ở đây.

**Lợi ích thật của `where T : struct, IEvent`:**

| Lợi ích | Cơ chế |
|---|---|
| Không alloc event object mỗi publish | Chặn `T` là class → call site **không thể** `new SomeEvent()` per publish. Đây mới là chỗ thắng GC thật |
| `Publish()` không tham số luôn an toàn | `default(T)` là struct zero-init, không bao giờ `null` |
| Isolation giữa listener | Value semantics → listener nhận copy, không sửa được thứ listener sau nhìn thấy |
| Dispatch không lookup | JIT/IL2CPP specialize theo `T`, tránh shared-generic dictionary lookup |

**Kiểm mốc:** `EventBus<ToastRequest>.Publish()` với 1 listener → nhận `MessageKey == null`, `Seconds == 0f`, **không** NRE. Nổ NRE nghĩa là constraint viết sai.

---

## §1. Cách dùng đúng — hợp đồng với developer

Auto-prune phủ **destroy**, **không** phủ **disable**. Đây là chỗ dễ sai nhất của thiết kế này, phải nắm trước khi viết caller.

| Nhu cầu | Cách viết | Có cần giữ handle? |
|---|---|---|
| **Nghe suốt đời object** (thường gặp nhất) | `Awake()` → `EventBus<T>.Subscribe(OnFoo);` | **Không** — auto-prune dọn khi destroy |
| **Nghe theo lúc bật/tắt** (object pool, popup) | `OnEnable` → `sub = Subscribe(OnFoo);`<br>`OnDisable` → `sub.Dispose();` | **Có** |
| **Nghe một lần rồi thôi** | giữ handle, `sub.Dispose()` ngay trong callback | **Có** |
| **Lambda có capture** | `sub = Subscribe(e => …);` rồi `sub.Dispose()` | **Có, bắt buộc** — auto-prune không thấy closure |

```csharp
// ✅ ĐÚNG — nghe suốt đời, không cần huỷ
void Awake() => EventBus<ToastRequest>.Subscribe(OnToast);

// ✅ ĐÚNG — nghe theo bật/tắt
Subscription<ToastRequest> sub;
void OnEnable()  => sub = EventBus<ToastRequest>.Subscribe(OnToast);
void OnDisable() => sub.Dispose();

// ❌ SAI — Subscribe ở OnEnable mà không Dispose ở OnDisable
void OnEnable() => EventBus<ToastRequest>.Subscribe(OnToast);
//  → object bị disable VẪN nhận event (auto-prune chỉ thấy destroy, không thấy disable)
//  → enable lại: Subscribe lần hai bị dup-guard chặn + spam LogWarning mỗi lần
```

Dòng cảnh báo trùng đăng ký chính là tín hiệu bạn đã viết vào cái bẫy trên — đừng tắt nó, hãy sửa call site.

**Ba giới hạn phải ghi vào `EventBus.md`:**

| Giới hạn | Hệ quả thực tế |
|---|---|
| Main-thread only | Publish từ thread pool làm hỏng danh sách listener |
| Lambda có capture không được auto-prune | `Target` là closure, không phải `UnityEngine.Object` → phải giữ handle |
| Prune chỉ chạy **khi có Publish** | Event không bao giờ bắn nữa → entry chết còn nằm trong list. Vô hại ở cỡ UI, nhưng **không** được nói "không bao giờ leak" |

---

## §2. Cấu trúc file

| File | Trách nhiệm |
|---|---|
| `Runtime/Utilities/Common/DeferredList.cs` | **MỚI, dùng chung.** List an toàn khi phần tử bị huỷ giữa lúc duyệt. Ticker Task 1 dùng lại → 2 consumer thật |
| `Runtime/Utilities/EventBus/IEvent.cs` | Marker interface cho event DTO |
| `Runtime/Utilities/EventBus/EventBusRegistry.cs` | Sổ non-generic gom `Clear` của mọi `EventBus<T>` → reset toàn cục |
| `Runtime/Utilities/EventBus/EventBus.cs` | Static generic: `Publish` · `Subscribe` · `internal Unsubscribe/Clear` · dispatch loop |
| `Runtime/Utilities/EventBus/Subscription.cs` | `readonly struct` handle huỷ đăng ký |
| `Assets/Scripts/EventBusDeadListener.cs` | **Ngoài SDK.** Listener cho case 5 |
| `Assets/Scripts/EventBusVerification.cs` | **Ngoài SDK.** Chạy 8 case kiểm chứng, log PASS/FAIL |

**Xoá:** `EventBinding.cs` · `IEventBusListener.cs` · `EventBus.md` (+ `.meta`).

Thứ tự task **1 → 2 → 3 → 4**. Task 3 phải xoá bản cũ *trong cùng task*: `EventBus<T>` mới trùng tên + namespace + arity với bản cũ → duplicate type error nếu cùng tồn tại.

---

### Task 1: `DeferredList<T>`

**Files:**
- Create: `Assets/Horcrux/Runtime/Utilities/Common/DeferredList.cs`

**Interfaces:**
- Consumes: không gì (nền)
- Produces: `sealed class DeferredList<T> where T : class` — `int Count { get; }` · `int TombstoneCount { get; }` · `T this[int index] { get; }` · `bool Add(T item)` · `bool Remove(T item)` · `void RemoveAt(int index)` · `void Compact()` · `void Clear()`

**Bảng self-doc & tối ưu:**

| Quyết định | Lý do | Xoá đi thì hỏng ở đâu |
|---|---|---|
| Xoá = tombstone, không `List.RemoveAt` | §0.2 — dồn chỉ số làm caller bỏ sót phần tử | Case 1 & 2 sai ngay |
| `Add` ghi thẳng vào cuối | Thêm cuối không ảnh hưởng phần tử đang duyệt → bỏ được cả một hàng đợi lẫn một nhánh | — |
| Dup-guard trong `Add` | `TickerSystem.md`: *"chặn bug im lặng tệ nhất của hệ: đăng ký 2 lần"*. `n` nhỏ nên O(n) là vài phép so | Case 3 sai: callback chạy 2 lần mỗi Publish |
| **`EqualityComparer<T>.Default`, KHÔNG `==`** | Với `T : class`, `==` trong generic sinh `ceq` = **so tham chiếu**, bỏ qua `Delegate.op_Equality`. Mỗi `Subscribe(OnToast)` tạo `Action` instance mới → `==` không khớp → dup-guard **im lặng vô hiệu**. `EqualityComparer` gọi `Delegate.Equals` = so `Target` + `Method` | Case 3 sai, và sai theo cách rất khó thấy |
| **`RemoveAt(int)` tồn tại song song `Remove(T)`** | Vòng dispatch của `Publish` **đã biết `i`** khi phát hiện owner destroyed. Gọi `Remove(callback)` ở đó là quét lại O(n) với một virtual `Equals` mỗi phần tử — trong khi `RemoveAt(i)` là O(1). Đây là chỗ duy nhất `Remove(T)` bị gọi trong vòng lặp | Prune tốn O(n²) khi nhiều listener chết cùng lúc; và call site đọc khó hơn |
| **`Compact` dùng two-pointer, không `RemoveAt` ngược** | SKILL nêu "duyệt ngược khi xoá" cho việc xoá **một** phần tử. Compact xoá **nhiều**: `k` lần `List.RemoveAt` = `k` lần dồn mảng → O(n·k). Two-pointer là **một** lượt O(n) + **một** `RemoveRange` | Không hỏng, nhưng chậm hơn hẳn khi nhiều tombstone |
| `Compact` thoát sớm khi `TombstoneCount == 0` | Publish bình thường = **một** phép so `int` rồi thoát | Không hỏng, chỉ tốn vô ích |
| `TombstoneCount` phơi ra ngoài | `EventBus<T>.ListenerCount` cần trừ tombstone để báo số đúng; và `Publish` cần nó để quyết có `Compact` không | Không quyết được khi nào Compact → phải quét list |
| `capacity = 4` mặc định | Pre-alloc theo SKILL; hầu hết event có 1–3 listener | Không hỏng, chỉ resize thêm |

- [ ] **Step 1: Tạo `DeferredList.cs`**

```csharp
using System.Collections.Generic;

namespace Horcrux.Runtime.Utilities.Common
{
    /// <summary>
    /// List an toàn khi phần tử bị huỷ **giữa lúc** caller đang duyệt: xoá chỉ đặt tombstone
    /// (<c>null</c>) nên chỉ số của mọi phần tử còn lại bất động, không phần tử nào bị bỏ sót.
    /// Caller gọi <see cref="Compact"/> để dọn, và **chỉ** khi đã ra khỏi mọi vòng duyệt.
    /// </summary>
    /// <typeparam name="T">Kiểu tham chiếu — tombstone dùng <c>null</c> làm dấu.</typeparam>
    public sealed class DeferredList<T> where T : class
    {
        // So sánh qua comparer, KHÔNG qua `==`: với `T : class`, `==` trong generic sinh `ceq`
        // (so tham chiếu) và bỏ qua operator overload của kiểu thật. Mỗi method-group conversion
        // tạo delegate instance mới ⇒ so tham chiếu làm dup-guard vô hiệu.
        // EqualityComparer<T>.Default gọi Delegate.Equals ⇒ so Target + Method.
        static readonly EqualityComparer<T> Comparer = EqualityComparer<T>.Default;

        readonly List<T> items;

        /// <param name="capacity">Số phần tử dự kiến — pre-alloc để tránh resize.</param>
        public DeferredList(int capacity = 4) => items = new List<T>(capacity);

        /// <summary>Số slot, **tính cả** tombstone.</summary>
        public int Count => items.Count;

        /// <summary>Số slot đang là tombstone. <c>Count - TombstoneCount</c> = số phần tử thật.</summary>
        public int TombstoneCount { get; private set; }

        /// <summary><c>null</c> nếu slot là tombstone — caller **phải** kiểm null.</summary>
        public T this[int index] => items[index];

        /// <summary>Thêm vào cuối list.</summary>
        /// <param name="item">Bỏ qua nếu <c>null</c> hoặc đã có trong list (dup-guard).</param>
        /// <returns><c>true</c> nếu thực sự thêm.</returns>
        public bool Add(T item)
        {
            if (item == null || IndexOf(item) >= 0)
                return false;

            items.Add(item);          // ghi thẳng vào cuối: không ảnh hưởng phần tử đang duyệt
            return true;
        }

        /// <summary>Đặt tombstone tại vị trí của <paramref name="item"/>. Có hiệu lực **ngay**.</summary>
        /// <param name="item">Bỏ qua nếu <c>null</c> hoặc không có trong list.</param>
        /// <returns><c>true</c> nếu tìm thấy và đã đặt tombstone.</returns>
        public bool Remove(T item)
        {
            if (item == null)
                return false;

            int index = IndexOf(item);
            if (index < 0)
                return false;

            RemoveAt(index);
            return true;
        }

        /// <summary>
        /// Đặt tombstone tại <paramref name="index"/> — O(1), không quét.
        /// Dùng khi caller **đã biết** chỉ số (vòng dispatch của EventBus prune owner đã destroy).
        /// </summary>
        /// <param name="index">Chỉ số hợp lệ trong <see cref="Count"/>. Đã là tombstone thì không đếm hai lần.</param>
        public void RemoveAt(int index)
        {
            if (items[index] == null)
                return;

            items[index] = null;      // KHÔNG List.RemoveAt: dồn chỉ số làm caller bỏ sót — §0.2
            TombstoneCount++;
        }

        /// <summary>
        /// Dọn hết tombstone. Chỉ số **thay đổi** sau khi gọi, nên caller chỉ được gọi khi
        /// không còn vòng duyệt nào đang chạy.
        /// </summary>
        public void Compact()
        {
            if (TombstoneCount == 0)  // đường thường: một phép so int rồi thoát
                return;

            // Two-pointer: một lượt O(n) + một RemoveRange. Không dùng List.RemoveAt lặp —
            // xoá k phần tử = k lần dồn mảng ⇒ O(n·k).
            int write = 0;
            for (int read = 0; read < items.Count; read++)
            {
                if (items[read] == null)
                    continue;

                if (write != read)
                    items[write] = items[read];
                write++;
            }

            items.RemoveRange(write, items.Count - write);
            TombstoneCount = 0;
        }

        public void Clear()
        {
            items.Clear();
            TombstoneCount = 0;
        }

        int IndexOf(T item)
        {
            for (int i = 0; i < items.Count; i++)
            {
                if (items[i] != null && Comparer.Equals(items[i], item))
                    return i;
            }
            return -1;
        }
    }
}
```

- [ ] **Step 2: Chờ Unity compile**

Console phải **không** có lỗi. Báo `The type or namespace name 'Common' does not exist` → kiểm file nằm đúng `Assets/Horcrux/Runtime/Utilities/Common/`.

- [ ] **Step 3: Commit**

```bash
git add Assets/Horcrux/Runtime/Utilities/Common/DeferredList.cs Assets/Horcrux/Runtime/Utilities/Common/DeferredList.cs.meta
git commit -m "feat(common): DeferredList — tombstone remove an toan khi duyet"
```

---

### Task 2: `IEvent` + `EventBusRegistry`

Hai file độc lập, không phụ thuộc `EventBus<T>` → gộp một task, compile được ngay.

**Files:**
- Create: `Assets/Horcrux/Runtime/Utilities/EventBus/IEvent.cs`
- Create: `Assets/Horcrux/Runtime/Utilities/EventBus/EventBusRegistry.cs`

**Interfaces:**
- Consumes: không gì
- Produces: `interface IEvent { }` · `static class EventBusRegistry` với `internal static void Register(Action clearAction)`

**Bảng self-doc & tối ưu:**

| Quyết định | Lý do | Xoá đi thì hỏng ở đâu |
|---|---|---|
| `IEvent` thay `IEventDTO` | "DTO" là từ tầng data-transfer, không nói đúng bản chất "một việc vừa xảy ra". `:1189` đã viết `: IEvent` | Lệch với thiết kế Toast đã chốt |
| Interface **rỗng** | Chỉ cần làm constraint marker. Thêm member sẽ kéo `constrained.` + khả năng boxing thật (§0.3) | — |
| Registry `static class` **non-generic** | `EventBus<T>` là static generic: mỗi `T` có storage riêng và **không có cách nào liệt kê** các `T` đã instantiate. Registry là chỗ duy nhất gom được | Với *Disable Domain Reload*, listener phiên trước sống sang phiên sau, trỏ vào GameObject đã destroy → exception mỗi Publish |
| `SubsystemRegistration` | Pha `RuntimeInitializeOnLoadMethod` **sớm nhất** — trước `BeforeSceneLoad` và trước mọi `Awake` | Reset sau khi listener mới đã đăng ký → xoá oan |
| **Không** clear `ClearActions` sau reset | Static state các bus vẫn tồn tại nên phiên sau vẫn cần đúng những clear action này. Static ctor không chạy lại khi domain reload bị tắt | Phiên play thứ ba không được reset |
| `Register` là `internal` | Chỉ static ctor của `EventBus<T>` cùng assembly gọi | Không hỏng, chỉ phơi API không ai cần |
| `capacity = 16` | Số event type dự kiến cỡ vài chục | Không hỏng, chỉ resize |

- [ ] **Step 1: Tạo `IEvent.cs`**

```csharp
namespace Horcrux.Runtime.Utilities.EventBus
{
    /// <summary>
    /// Marker cho một việc **vừa xảy ra**, dùng làm payload của <see cref="EventBus{T}"/>.
    /// <para>
    /// Bắt buộc hiện thực bằng <c>readonly struct</c>: listener nhận một **bản copy**, nên field
    /// mutable sẽ bị sửa mất im lặng — listener sau không thấy thay đổi của listener trước.
    /// </para>
    /// <example>
    /// <code>
    /// public readonly struct ToastRequest : IEvent
    /// {
    ///     public readonly string MessageKey;
    ///     public readonly float  Seconds;
    /// }
    /// </code>
    /// </example>
    /// </summary>
    public interface IEvent { }
}
```

- [ ] **Step 2: Tạo `EventBusRegistry.cs`**

```csharp
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Horcrux.Runtime.Utilities.EventBus
{
    /// <summary>
    /// Sổ non-generic gom hành động <c>Clear</c> của mọi <see cref="EventBus{T}"/> đã dùng tới,
    /// để reset được **toàn bộ** bus khi vào Play Mode.
    /// <para>
    /// Vì sao cần: <c>EventBus&lt;T&gt;</c> là static generic — mỗi <c>T</c> có storage riêng và
    /// không có cách nào liệt kê các <c>T</c> đã instantiate. Với *Enter Play Mode Options →
    /// Disable Domain Reload*, listener của phiên play trước sống sót sang phiên sau và trỏ vào
    /// GameObject đã destroy. Đây là chỗ duy nhất gom được để dọn.
    /// </para>
    /// </summary>
    static class EventBusRegistry
    {
        static readonly List<Action> ClearActions = new(16);

        /// <summary>Gọi từ static ctor của <see cref="EventBus{T}"/> — đúng một lần mỗi <c>T</c>.</summary>
        /// <param name="clearAction">Hành động xoá sạch listener của một bus cụ thể.</param>
        internal static void Register(Action clearAction) => ClearActions.Add(clearAction);

        // SubsystemRegistration = pha RuntimeInitialize sớm nhất, trước BeforeSceneLoad và trước
        // mọi Awake ⇒ dọn xong rồi listener của phiên mới mới bắt đầu đăng ký.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetAllBuses()
        {
            for (int i = 0; i < ClearActions.Count; i++)
                ClearActions[i]();

            // KHÔNG clear ClearActions: static state các bus vẫn tồn tại khi domain reload bị tắt,
            // nên phiên play sau vẫn cần đúng những clear action này.
        }
    }
}
```

- [ ] **Step 3: Chờ Unity compile**

Console phải không có lỗi. `IEvent` chưa ai dùng nên không có warning.

- [ ] **Step 4: Commit**

```bash
git add Assets/Horcrux/Runtime/Utilities/EventBus/IEvent.cs Assets/Horcrux/Runtime/Utilities/EventBus/IEvent.cs.meta Assets/Horcrux/Runtime/Utilities/EventBus/EventBusRegistry.cs Assets/Horcrux/Runtime/Utilities/EventBus/EventBusRegistry.cs.meta
git commit -m "feat(eventbus): IEvent marker + EventBusRegistry reset toan cuc"
```

---

### Task 3: `EventBus<T>` + `Subscription<T>`, xoá bản cũ

`Subscription<T>.Dispose()` gọi `EventBus<T>.Unsubscribe`, và `Subscribe` trả `Subscription<T>` → phụ thuộc vòng, phải tạo cùng lúc. Bản cũ xoá trong **cùng** task vì trùng tên + namespace + arity → duplicate type error.

**Files:**
- Delete: `Assets/Horcrux/Runtime/Utilities/EventBus/EventBinding.cs` (+ `.meta`)
- Delete: `Assets/Horcrux/Runtime/Utilities/EventBus/IEventBusListener.cs` (+ `.meta`)
- Delete: `Assets/Horcrux/Runtime/Utilities/EventBus/EventBus.md` (+ `.meta`)
- Modify (ghi đè toàn bộ): `Assets/Horcrux/Runtime/Utilities/EventBus/EventBus.cs`
- Create: `Assets/Horcrux/Runtime/Utilities/EventBus/Subscription.cs`

**Interfaces:**
- Consumes: `DeferredList<T>` (Task 1) — `Count` · `TombstoneCount` · `this[int]` · `Add` · `RemoveAt(int)` · `Remove(T)` · `Compact` · `Clear`. `IEvent` + `EventBusRegistry.Register(Action)` (Task 2)
- Produces:
  - `static class EventBus<T> where T : struct, IEvent` — `static int ListenerCount { get; }` · `static Subscription<T> Subscribe(Action<T> callback)` · `static void Publish(in T e = default)` · `internal static void Unsubscribe(Action<T> callback)` · `internal static void Clear()`
  - `readonly struct Subscription<T> : IDisposable where T : struct, IEvent` — `internal Subscription(Action<T>)` · `void Dispose()`

**Bảng self-doc & tối ưu:**

| Quyết định | Lý do | Xoá đi thì hỏng ở đâu |
|---|---|---|
| Bỏ `EventBinding`, gộp dispatch vào `EventBus<T>` | Không còn interface, không còn bus scoped → class tách riêng là indirection thuần. Trách nhiệm còn lại là *dispatch listener của type `T`*; storage đã uỷ cho `DeferredList` | Không gọi được tên chỗ hỏng nào → nguyên tắc 6 nói bỏ |
| Xoá `IEventBusListener` | Không class nào implement, **không cơ chế nào gọi** `RegisterCallbacks`/`DeregisterCallbacks`. Nó gợi ý một hệ lifecycle không tồn tại | — (code chết) |
| `Subscribe` trả `Subscription<T>` | Bản cũ huỷ bằng chính delegate ⇒ lambda có capture **không bao giờ** huỷ được ⇒ leak vĩnh viễn | Case 6 sai |
| `Subscription<T>` là `readonly struct` | Zero-alloc; `IDisposable` trên struct nên `using` không box | Mỗi Subscribe thêm 1 alloc |
| `Unsubscribe`/`Clear` là `internal` | Giữ đúng **một** đường huỷ công khai. Bản cũ để `Clear()` public ⇒ ai cũng xoá sạch bus toàn cục | API có 2 đường cùng việc; `Clear` public là chân súng |
| Chụp `count` **một lần** | §0.2 — đọc `Count` mỗi vòng làm vòng lặp tự nuôi khi listener tự `Subscribe` | Case 4 sai; xấu nhất là treo |
| `dispatchDepth` + `Compact` khi về 0 | §0.2 — `Compact` dồn chỉ số, nested Publish sẽ làm vòng ngoài trỏ sai | Case 7 sai |
| `try/finally` quanh vòng lặp | Exception thoát ra (loại `catch (Exception)` không bắt) sẽ kẹt `dispatchDepth > 0` ⇒ `Compact` **không bao giờ** chạy nữa ⇒ tombstone tích tụ vĩnh viễn | Bus degrade âm thầm sau một lỗi lạ |
| Auto-prune `Target is Object && == null` | Bản cũ: MonoBehaviour destroy mà `OnDisable` không chạy ⇒ mỗi Publish ném `MissingReferenceException`, bị try/catch nuốt, spam mãi mãi | Case 5 sai |
| Prune bằng **`RemoveAt(i)`**, không `Remove(callback)` | Vòng lặp đã biết `i` → O(1). `Remove(callback)` sẽ quét lại O(n) với một virtual `Equals` mỗi phần tử, ngay trong vòng dispatch | Prune O(n²) khi nhiều listener chết cùng lúc |
| Prune **trước** khi gọi | Gọi rồi mới prune = đã nổ exception một lần | Vẫn spam 1 lần mỗi listener chết |
| `Publish(in T e = default)` | `in` tiết kiệm một copy struct vào frame `Publish`, miễn phí — rvalue vẫn truyền được. `= default` cho phép `Publish()` với event rỗng | Không hỏng; copy thêm 1 lần |
| Handler là `Action<T>`, **không** `in T` | Không hot path; `Action<T>` là thứ dev đoán đúng ngay. Thêm overload `in` sau là additive | — |
| `try/catch` **từng** callback | SKILL yêu cầu: 1 listener lỗi không kill listener khác | Case 8 sai |
| Dup-guard warning chỉ `#if UNITY_EDITOR` | Bug cần lộ lúc dev; build không cần tốn string format. Đây là tín hiệu dev đã viết `Subscribe` ở `OnEnable` mà quên `Dispose` (§1) | Bug trùng đăng ký im lặng như bản cũ |
| `ListenerCount` trừ tombstone | Script kiểm chứng cần số listener **thật**; `Count` thô tính cả tombstone gây nhầm | Case 2b/3/5c báo số sai |

- [ ] **Step 1: Xoá 3 file cũ**

```bash
cd Assets/Horcrux
git rm Runtime/Utilities/EventBus/EventBinding.cs Runtime/Utilities/EventBus/EventBinding.cs.meta
git rm Runtime/Utilities/EventBus/IEventBusListener.cs Runtime/Utilities/EventBus/IEventBusListener.cs.meta
git rm Runtime/Utilities/EventBus/EventBus.md Runtime/Utilities/EventBus/EventBus.md.meta
```

Console Unity lúc này **sẽ báo lỗi** (`EventBus.cs` cũ còn tham chiếu `EventBinding`) — đúng dự kiến, Step 2 sửa.

- [ ] **Step 2: Ghi đè toàn bộ `EventBus.cs`**

```csharp
using System;
using Horcrux.Runtime.Utilities.Common;
using UnityEngine;

namespace Horcrux.Runtime.Utilities.EventBus
{
    /// <summary>
    /// Bus publish/subscribe, một bus cho mỗi <typeparamref name="T"/>.
    /// <para>
    /// <b>Main-thread only</b> — không lock. Publish từ thread pool làm hỏng danh sách listener.
    /// </para>
    /// <para>
    /// Thứ tự gọi = **thứ tự đăng ký** (FIFO). Listener đăng ký *trong lúc* một
    /// <see cref="Publish"/> đang chạy **không** được gọi ở lần publish đó.
    /// </para>
    /// <para>
    /// Listener có <c>Target</c> là <see cref="UnityEngine.Object"/> đã destroy được tự dọn ở
    /// lần Publish kế. Việc đó **không** phủ object chỉ bị *disable*, cũng không phủ lambda có
    /// capture — hai ca đó phải giữ <see cref="Subscription{T}"/> và tự <c>Dispose</c>.
    /// </para>
    /// </summary>
    /// <typeparam name="T">Event DTO — bắt buộc <c>readonly struct</c> implement <see cref="IEvent"/>.</typeparam>
    public static class EventBus<T> where T : struct, IEvent
    {
        static readonly DeferredList<Action<T>> Listeners = new(4);

        // Số Publish đang lồng nhau. Compact CHỈ chạy khi về 0: Compact dồn chỉ số — đúng cái
        // tombstone tồn tại để tránh — nên nó làm lệch vòng duyệt của Publish đang chạy ở ngoài.
        static int dispatchDepth;

        static EventBus() => EventBusRegistry.Register(Clear);

        /// <summary>Số listener đang hoạt động, **không** tính tombstone chờ dọn.</summary>
        public static int ListenerCount => Listeners.Count - Listeners.TombstoneCount;

        /// <summary>Đăng ký nhận event. Xem hợp đồng dùng đúng ở §1 của plan.</summary>
        /// <param name="callback">
        /// Handler. Bỏ qua nếu <c>null</c>, hoặc nếu đã đăng ký (so theo <c>Target</c> + <c>Method</c>).
        /// </param>
        /// <returns>
        /// Handle để huỷ. Là <c>default</c> (Dispose không làm gì) nếu <paramref name="callback"/>
        /// null hoặc trùng.
        /// </returns>
        public static Subscription<T> Subscribe(Action<T> callback)
        {
            if (callback == null)
                return default;

            if (!Listeners.Add(callback))
            {
#if UNITY_EDITOR
                Debug.LogWarning(
                    $"[EventBus<{typeof(T).Name}>] Bỏ qua đăng ký trùng: " +
                    $"{callback.Method.DeclaringType?.Name}.{callback.Method.Name}. " +
                    "Thường là do Subscribe trong OnEnable mà không Dispose trong OnDisable.");
#endif
                return default;
            }

            return new Subscription<T>(callback);
        }

        /// <summary>Gọi mọi listener theo thứ tự đăng ký. Không cấp phát heap.</summary>
        /// <param name="e">Payload; mặc định là struct zero-init (luôn hợp lệ, không bao giờ null).</param>
        public static void Publish(in T e = default)
        {
            dispatchDepth++;
            try
            {
                // Chụp Count MỘT LẦN: listener tự Subscribe trong callback không làm vòng lặp
                // tự nuôi. Listener mới chờ lần Publish sau — §0.2.
                int count = Listeners.Count;

                for (int i = 0; i < count; i++)
                {
                    var callback = Listeners[i];

                    if (callback == null)               // tombstone: đã Unsubscribe
                        continue;

                    if (IsOwnerDestroyed(callback))
                    {
                        Listeners.RemoveAt(i);          // O(1): đã biết i, không quét lại
                        continue;                       // prune TRƯỚC khi gọi: không nổ lần nào
                    }

                    try
                    {
                        callback(e);
                    }
                    catch (Exception ex)
                    {
                        Debug.LogException(ex);         // 1 listener chết ≠ kill listener khác
                    }
                }
            }
            finally
            {
                dispatchDepth--;

                if (dispatchDepth == 0 && Listeners.TombstoneCount > 0)
                    Listeners.Compact();
            }
        }

        /// <summary>Huỷ đăng ký. Gọi qua <see cref="Subscription{T}.Dispose"/>, không gọi trực tiếp.</summary>
        internal static void Unsubscribe(Action<T> callback) => Listeners.Remove(callback);

        /// <summary>Xoá sạch listener. Gọi bởi <see cref="EventBusRegistry"/> khi vào Play Mode.</summary>
        internal static void Clear()
        {
            Listeners.Clear();
            dispatchDepth = 0;
        }

        /// <summary>
        /// <c>Target</c> của delegate là <see cref="UnityEngine.Object"/> đã destroy?
        /// <para>
        /// Chỉ phủ method group (<c>Subscribe(OnToast)</c>). Lambda **có capture** thì
        /// <c>Target</c> là closure chứ không phải <c>Object</c> ⇒ không được prune.
        /// </para>
        /// </summary>
        static bool IsOwnerDestroyed(Action<T> callback)
            => callback.Target is UnityEngine.Object owner && owner == null;
    }
}
```

- [ ] **Step 3: Tạo `Subscription.cs`**

```csharp
using System;

namespace Horcrux.Runtime.Utilities.EventBus
{
    /// <summary>
    /// Handle huỷ đăng ký — **đường huỷ công khai duy nhất** của <see cref="EventBus{T}"/>.
    /// <para>
    /// <c>readonly struct</c> nên không cấp phát heap. Vì handle giữ chính delegate đã đăng ký,
    /// lambda cũng huỷ được — điều bản cũ không làm được, vì nó so delegate do caller truyền lại
    /// mà mỗi lambda có capture tạo một closure mới.
    /// </para>
    /// </summary>
    /// <typeparam name="T">Loại event của bus đã đăng ký.</typeparam>
    public readonly struct Subscription<T> : IDisposable where T : struct, IEvent
    {
        readonly Action<T> callback;

        internal Subscription(Action<T> callback) => this.callback = callback;

        /// <summary>
        /// Huỷ đăng ký. An toàn khi gọi nhiều lần, và an toàn với <c>default</c> handle
        /// (do <c>Subscribe</c> trả về khi callback null hoặc trùng).
        /// </summary>
        public void Dispose() => EventBus<T>.Unsubscribe(callback);
    }
}
```

- [ ] **Step 4: Chờ Unity compile, xác nhận sạch lỗi**

Console phải **không** còn lỗi. Còn `The type or namespace name 'IEventDTO' could not be found` → có chỗ khác còn tham chiếu bản cũ:

```bash
grep -rn "IEventDTO\|EventBinding\|IEventBusListener" --include="*.cs" Assets/
```

Kỳ vọng: **không kết quả nào**.

- [ ] **Step 5: Commit**

```bash
git add -A Assets/Horcrux/Runtime/Utilities/EventBus/
git commit -m "feat(eventbus)!: bus moi tombstone-safe, handle unsubscribe, auto-prune

- Publish khong bo sot listener khi unsubscribe giua luc dispatch
- Subscribe tra Subscription handle: lambda huy duoc
- Tu prune listener co Target la Object da destroy
- Bo priority (chua caller), bo EventBinding, bo IEventBusListener
- IEventDTO/Raise/Register -> IEvent/Publish/Subscribe"
```

---

### Task 4: Script kiểm chứng 8 case

Thay cho assembly test (đã chốt không tạo). Nằm **ngoài** SDK, ở assembly game, nên không ship theo Horcrux.

**Files:**
- Create: `Assets/Scripts/EventBusDeadListener.cs`
- Create: `Assets/Scripts/EventBusVerification.cs`

**Interfaces:**
- Consumes: `EventBus<T>` · `Subscription<T>` · `IEvent` (Task 3)
- Produces: `readonly struct DeadOwnerEvent : IEvent` · `sealed class EventBusDeadListener : MonoBehaviour` — `static int Hits` · `void OnEvent(DeadOwnerEvent e)`

**Editor setup (§C.1):** tạo GameObject rỗng `_EventBusVerification` trong scene test, add component `EventBusVerification`, bấm Play, đọc Console. Không cần gán reference nào.

**Hai quyết định về cấu trúc script:**

| Quyết định | Lý do |
|---|---|
| **Mỗi case một struct event riêng** | Bus là static per-type, và `Clear()` là `internal` nên script ở assembly game **không** dọn được giữa các case. Dùng chung một event type ⇒ listener case trước còn sót ⇒ mọi khẳng định `ListenerCount` thành vô nghĩa |
| `EventBusDeadListener` ở **file riêng, top-level** | Unity cần MonoScript khớp tên file để `AddComponent<T>()` chạy. MonoBehaviour khai báo **lồng** trong class khác sẽ fail runtime: *"the script class cannot be found"*. `DeadOwnerEvent` đặt cùng file vì listener phải thấy được nó |

**Bảng kiểm chứng — input → kỳ vọng:**

| # | Input | Kỳ vọng | Bug bản cũ? |
|---|---|---|---|
| 1 | 3 listener; listener[0] tự `Dispose` trong callback | **cả 3 đều chạy** | ✔ bản cũ bỏ sót listener[1] |
| 2 | 3 listener; listener[2] huỷ listener[0] giữa Publish | cả 3 chạy đúng 1 lần; sau Compact còn 2 | ✔ |
| 3 | `Subscribe` cùng một method 2 lần | `ListenerCount == 1`, callback chạy **1 lần** | ✔ chạy 2 lần |
| 4 | listener[0] `Subscribe` thêm listener giữa Publish | listener mới **không** chạy lần này; chạy ở Publish kế | ✔ hành vi không xác định |
| 5 | MonoBehaviour đã destroy rồi Publish | không exception thoát ra; `ListenerCount` về 0 | ✔ spam MissingReferenceException mãi mãi |
| 6 | Subscribe lambda **có capture**, rồi `Dispose` handle | lambda không còn chạy | ✔ không huỷ được |
| 7 | listener publish lồng cùng type (1 cấp) | không sót, không lặp | ✔ |
| 8 | `Publish()` không tham số | listener nhận `default(T)`, không NRE | — |

Case 9 (registry reset) **không tự động hoá được trong một phiên play** — quy trình thủ công ở Step 4.

- [ ] **Step 1: Tạo `EventBusDeadListener.cs`**

```csharp
using Horcrux.Runtime.Utilities.EventBus;
using UnityEngine;

/// <summary>Event riêng cho case 5 — bus là static per-type nên mỗi case cần một type riêng.</summary>
public readonly struct DeadOwnerEvent : IEvent { }

/// <summary>
/// Listener cho case 5: đăng ký rồi bị <c>DestroyImmediate</c>, để kiểm rằng EventBus tự prune
/// delegate có <c>Target</c> là <see cref="UnityEngine.Object"/> đã destroy.
/// </summary>
public sealed class EventBusDeadListener : MonoBehaviour
{
    public static int Hits;

    public void OnEvent(DeadOwnerEvent e) => Hits++;
}
```

- [ ] **Step 2: Tạo `EventBusVerification.cs`**

```csharp
using Horcrux.Runtime.Utilities.EventBus;
using UnityEngine;

/// <summary>
/// Kiểm chứng 8 hành vi của <see cref="EventBus{T}"/> — thay cho assembly test.
/// Đặt lên một GameObject rỗng trong scene test rồi bấm Play, đọc Console.
/// </summary>
public sealed class EventBusVerification : MonoBehaviour
{
    // Mỗi case một type riêng ⇒ bus sạch tuyệt đối ⇒ mọi khẳng định ListenerCount là chính xác.
    readonly struct SelfDisposeEvent    : IEvent { }
    readonly struct DisposeEarlierEvent : IEvent { }
    readonly struct DupEvent            : IEvent { }
    readonly struct LateSubEvent        : IEvent { }
    readonly struct NestedEvent         : IEvent { }

    readonly struct LambdaEvent : IEvent
    {
        public readonly int Value;
        public LambdaEvent(int value) => Value = value;
    }

    readonly struct DefaultArgEvent : IEvent
    {
        public readonly int Value;
        public DefaultArgEvent(int value) => Value = value;
    }

    int passed;
    int failed;

    void Start()
    {
        Case1_SelfDisposeDuringPublish();
        Case2_DisposeEarlierListenerDuringPublish();
        Case3_DuplicateSubscribe();
        Case4_SubscribeDuringPublish();
        Case5_DestroyedOwnerPruned();
        Case6_CapturingLambdaUnsubscribes();
        Case7_NestedPublish();
        Case8_PublishWithoutArgs();

        Debug.Log($"=== EventBus verification: {passed} PASS, {failed} FAIL ===");
    }

    // Case 1 — bug kinh điển bản cũ: listener tự huỷ làm listener kế bị bỏ sót.
    void Case1_SelfDisposeDuringPublish()
    {
        int hitA = 0, hitB = 0, hitC = 0;
        Subscription<SelfDisposeEvent> subA = default;

        // Lambda capture BIẾN subA (không phải giá trị) ⇒ lúc nó chạy subA đã có handle thật.
        subA = EventBus<SelfDisposeEvent>.Subscribe(_ => { hitA++; subA.Dispose(); });
        EventBus<SelfDisposeEvent>.Subscribe(_ => hitB++);
        EventBus<SelfDisposeEvent>.Subscribe(_ => hitC++);

        EventBus<SelfDisposeEvent>.Publish();

        Check("Case 1: self-dispose khong lam bo sot listener ke",
            hitA == 1 && hitB == 1 && hitC == 1,
            $"A={hitA} B={hitB} C={hitC}, ky vong 1/1/1");

        EventBus<SelfDisposeEvent>.Publish();

        Check("Case 1b: sau Compact, A da bi go",
            hitA == 1 && hitB == 2 && hitC == 2,
            $"A={hitA} B={hitB} C={hitC}, ky vong 1/2/2");
    }

    // Case 2 — huỷ một listener ĐỨNG TRƯỚC vị trí đang duyệt.
    void Case2_DisposeEarlierListenerDuringPublish()
    {
        int hitA = 0, hitB = 0, hitC = 0;

        var subA = EventBus<DisposeEarlierEvent>.Subscribe(_ => hitA++);
        EventBus<DisposeEarlierEvent>.Subscribe(_ => hitB++);
        EventBus<DisposeEarlierEvent>.Subscribe(_ => { hitC++; subA.Dispose(); });

        EventBus<DisposeEarlierEvent>.Publish();

        Check("Case 2: huy listener dung truoc khong lam lech vong duyet",
            hitA == 1 && hitB == 1 && hitC == 1,
            $"A={hitA} B={hitB} C={hitC}, ky vong 1/1/1");

        Check("Case 2b: ListenerCount ve 2 sau Compact",
            EventBus<DisposeEarlierEvent>.ListenerCount == 2,
            $"ListenerCount={EventBus<DisposeEarlierEvent>.ListenerCount}, ky vong 2");
    }

    // Case 3 — dup-guard. CHẾT nếu DeferredList so bằng `==` thay vì comparer:
    // mỗi Subscribe(Case3Handler) tạo một Action instance mới, khác tham chiếu.
    int case3Hits;
    void Case3Handler(DupEvent e) => case3Hits++;

    void Case3_DuplicateSubscribe()
    {
        case3Hits = 0;

        EventBus<DupEvent>.Subscribe(Case3Handler);
        EventBus<DupEvent>.Subscribe(Case3Handler);   // trung -> bo qua + LogWarning trong Editor

        Check("Case 3: dang ky trung chi tinh 1 listener",
            EventBus<DupEvent>.ListenerCount == 1,
            $"ListenerCount={EventBus<DupEvent>.ListenerCount}, ky vong 1");

        EventBus<DupEvent>.Publish();

        Check("Case 3b: callback chi chay 1 lan",
            case3Hits == 1, $"hits={case3Hits}, ky vong 1");
    }

    // Case 4 — hành vi đã chốt: Publish chụp Count một lần.
    void Case4_SubscribeDuringPublish()
    {
        int hitLate = 0;
        bool added = false;

        EventBus<LateSubEvent>.Subscribe(_ =>
        {
            if (added)
                return;
            added = true;
            EventBus<LateSubEvent>.Subscribe(__ => hitLate++);
        });

        EventBus<LateSubEvent>.Publish();

        Check("Case 4: listener dang ky giua Publish KHONG chay lan do",
            hitLate == 0, $"hitLate={hitLate}, ky vong 0");

        EventBus<LateSubEvent>.Publish();

        Check("Case 4b: listener do chay o Publish ke",
            hitLate == 1, $"hitLate={hitLate}, ky vong 1");
    }

    // Case 5 — auto-prune owner đã destroy.
    void Case5_DestroyedOwnerPruned()
    {
        EventBusDeadListener.Hits = 0;

        var go = new GameObject("EventBusDeadListener");
        var listener = go.AddComponent<EventBusDeadListener>();
        EventBus<DeadOwnerEvent>.Subscribe(listener.OnEvent);

        EventBus<DeadOwnerEvent>.Publish();

        Check("Case 5: listener con song thi chay",
            EventBusDeadListener.Hits == 1,
            $"Hits={EventBusDeadListener.Hits}, ky vong 1");

        // DestroyImmediate, khong Destroy: Destroy() cho tới cuoi frame moi co hieu luc,
        // ma ta can kiem ngay trong Start().
        DestroyImmediate(go);

        EventBus<DeadOwnerEvent>.Publish();

        Check("Case 5b: owner destroyed -> khong chay, khong exception",
            EventBusDeadListener.Hits == 1,
            $"Hits={EventBusDeadListener.Hits}, ky vong 1");

        Check("Case 5c: entry da bi prune",
            EventBus<DeadOwnerEvent>.ListenerCount == 0,
            $"ListenerCount={EventBus<DeadOwnerEvent>.ListenerCount}, ky vong 0");
    }

    // Case 6 — bản cũ KHÔNG làm được: huỷ một lambda có capture.
    void Case6_CapturingLambdaUnsubscribes()
    {
        int captured = 0;
        var sub = EventBus<LambdaEvent>.Subscribe(e => captured += e.Value);  // capture `captured`

        EventBus<LambdaEvent>.Publish(new LambdaEvent(5));

        Check("Case 6: lambda co capture chay duoc",
            captured == 5, $"captured={captured}, ky vong 5");

        sub.Dispose();
        EventBus<LambdaEvent>.Publish(new LambdaEvent(7));

        Check("Case 6b: lambda co capture HUY duoc bang handle",
            captured == 5, $"captured={captured}, ky vong 5 (khong doi)");
    }

    // Case 7 — Publish lồng cùng type: Compact không được chạy ở cấp trong.
    void Case7_NestedPublish()
    {
        int hitA = 0, hitB = 0;
        bool reentered = false;
        Subscription<NestedEvent> subA = default;

        subA = EventBus<NestedEvent>.Subscribe(_ =>
        {
            hitA++;
            subA.Dispose();                        // tao tombstone o cap ngoai
            if (reentered)
                return;
            reentered = true;
            EventBus<NestedEvent>.Publish();       // long 1 cap
        });
        EventBus<NestedEvent>.Subscribe(_ => hitB++);

        EventBus<NestedEvent>.Publish();

        // Cap ngoai i=0: A chay (hitA=1), A tombstone, goi Publish long.
        //   Cap trong: i=0 tombstone -> bo qua; i=1 la B -> hitB=1. depth=1 nen KHONG Compact.
        // Cap ngoai i=1: B -> hitB=2. depth=0 -> Compact.
        Check("Case 7: nested publish khong sot khong lap",
            hitA == 1 && hitB == 2, $"A={hitA} B={hitB}, ky vong 1/2");
    }

    // Case 8 — struct constraint: default(T) luôn hợp lệ, không bao giờ null.
    void Case8_PublishWithoutArgs()
    {
        int seen = -1;
        EventBus<DefaultArgEvent>.Subscribe(e => seen = e.Value);

        EventBus<DefaultArgEvent>.Publish();      // khong tham so

        Check("Case 8: Publish() khong tham so -> default(T), khong NRE",
            seen == 0, $"seen={seen}, ky vong 0");
    }

    void Check(string label, bool ok, string detail)
    {
        if (ok)
        {
            passed++;
            Debug.Log($"PASS  {label}");
            return;
        }
        failed++;
        Debug.LogError($"FAIL  {label}  —  {detail}");
    }
}
```

- [ ] **Step 3: Chạy và đọc Console**

Kỳ vọng:
- Dòng cuối `=== EventBus verification: 15 PASS, 0 FAIL ===` (2+2+2+2+3+2+1+1 = 15 phép kiểm)
- Đúng **một** `LogWarning` từ Case 3 — **thiết kế là vậy**, không phải lỗi
- **Không** `MissingReferenceException` nào

Chẩn đoán khi FAIL:

| Triệu chứng | Nguyên nhân |
|---|---|
| Case 1 FAIL `A=1 B=0 C=1` | `DeferredList` đang dùng `List.RemoveAt` chứ không đặt tombstone (§0.2) |
| Case 3 FAIL `ListenerCount=2` | `DeferredList.IndexOf` so bằng `==` thay vì `Comparer.Equals` |
| Case 4 treo, không bao giờ xong | `Publish` đọc `Listeners.Count` mỗi vòng thay vì chụp một lần |
| Case 5b có `MissingReferenceException` | Thứ tự sai: gọi callback **trước** rồi mới prune |
| Case 5c FAIL `ListenerCount=1` | `IsOwnerDestroyed` thiếu `owner == null`, hoặc dùng `is null` (bỏ qua fake-null của Unity) |
| Case 7 FAIL `B=1` | `Compact` chạy ở cấp lồng — thiếu điều kiện `dispatchDepth == 0` |

- [ ] **Step 4: Kiểm chứng case 9 (registry reset) — thủ công**

1. **Edit ▸ Project Settings ▸ Editor ▸ Enter Play Mode Options**: bật, **bỏ tick** `Reload Domain`.
2. Play → chờ log → Stop.
3. Play lần thứ hai.

Kỳ vọng: lần hai ra **đúng cùng số PASS**, và **không** có `MissingReferenceException` từ listener phiên trước. Số PASS lệch → `EventBusRegistry.ResetAllBuses` không chạy; kiểm lại `RuntimeInitializeLoadType.SubsystemRegistration`.

- [ ] **Step 5: Commit**

```bash
git add Assets/Scripts/EventBusVerification.cs Assets/Scripts/EventBusVerification.cs.meta
git add Assets/Scripts/EventBusDeadListener.cs Assets/Scripts/EventBusDeadListener.cs.meta
git commit -m "test(eventbus): script kiem chung 8 case hanh vi"
```

---

## Sau khi xong

| Việc | Ghi chú |
|---|---|
| `EventBus.md` mới (DOCS_SKILL Phần A) | Bản cũ xoá ở Task 3. Bản mới lấy nền từ §0.1–§0.3 + hợp đồng dùng đúng §1, **không** lặp lại sai lầm IL/boxing của bản cũ. Bắt buộc có 3 giới hạn ở cuối §1 |
| Sửa `PendingSystems.md:1206` | Đang viết `EventBus.Publish(new ToastRequest{…})` — không compile vì không có facade non-generic (xem ⑬). Đổi thành `EventBus<ToastRequest>.Publish(new ToastRequest{…})` |
| Mở rộng sau (đừng làm trước khi có caller) | ① `Subscribe(cb, priority)` overload ② `Subscribe(RefHandler<T>)` nhận `in T` cho event hot ③ `SubscriptionBag` nếu xuất hiện nhu cầu huỷ theo nhóm |
