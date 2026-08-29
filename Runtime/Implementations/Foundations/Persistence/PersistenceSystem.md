# Persistence Implementation Plan

> **Loại tài liệu:** Plan — developer tự code lại để nắm logic. `.md` thiết kế + `.html` viết **sau** khi có source.
>
> **For agentic workers:** REQUIRED SUB-SKILL: superpowers:subagent-driven-development hoặc superpowers:executing-plans. Steps dùng checkbox (`- [ ]`).

**Goal:** Lưu tiến độ người chơi **có kiểu**, **không mất khi app bị kill** (tối đa một chu kỳ autosave), **không god-blob**: nhiều `SaveUnit` độc lập đăng ký vào một `SaveRegistry` — registry lo load/autosave/flush/atomic-write, unit chỉ giữ model + dirty + on-change; format serialize thay được qua `ISerializer`; giá trị lẻ đi đường `Prefs<T>`.

**Architecture:** 3 tầng, tổng **10 file** (4 contract + 3 impl + 1 typed-prefs + 2 demo).

```
Contract  (ISaveUnit, SaveUnit<TModel>,       key + dirty + on-change · format thay được ·
           ISerializer, ISaveService)          cửa Register/FlushAll
Registry  (SaveRegistry + 2 ISerializer impl)  load lúc Register · autosave chu kỳ · flush pause/quit ·
                                               atomic write · corrupt → default + log
Game      (các unit cụ thể + Prefs<T> lẻ)     model + const key, đăng ký lúc boot
```

**Tech Stack:** C#, UniTask, `Sisus.Init` (`[Service]`), `System.Buffers`, Newtonsoft.Json (package `com.unity.nuget.newtonsoft-json` — DLL precompiled, mọi asmdef tự reference), PlayerPrefs (chỉ `Prefs<T>`), MemoryPack (tuỳ chọn qua versionDefines). **Không** Addressables, không toán.

## Global Constraints

| Ràng buộc | Giá trị |
|---|---|
| Namespace | `Horcrux.Runtime.Abstractions.Persistence` · `Horcrux.Runtime.Implementations.Persistence` |
| Hiệu năng | `MarkDirty` chạy theo nhịp **tương tác** — chỉ set cờ + phát event, **không serialize**. Serialize chỉ trong nhịp flush (autosave mặc định 5s + pause/quit), ghi vào **một** `ArrayBufferWriter<byte>` tái dùng (SystemPlan §0.4b: `IBufferWriter<byte>` ra, `ReadOnlySpan<byte>` vào). Load 1 lần lúc `Register`, sync — file save cỡ KB, đo được chậm mới chuyển async |
| SOLID | Registry chỉ biết `ISaveUnit` + `ISerializer`, không biết model nào (D) · unit không biết đĩa, không biết format (S) · không type nào mang ngữ nghĩa game (SystemPlan §0.1) |
| Editor-first | Chu kỳ autosave + chọn serializer là **cấu hình**, phơi ra Inspector của registry |
| An toàn | Load fail → model default + log, **không throw** (save hỏng không được chặn người chơi) · ghi fail → **giữ dirty**, log, chu kỳ sau thử lại · try/catch quanh **từng** callback `Changed` (SystemPlan §0.4a) · autosave loop nhận `destroyCancellationToken` |
| Bất biến | ① key là **hợp đồng wire format** — const string tường minh, không suy từ tên type ② dirty reset ở **đúng một nơi** (registry), và chỉ **SAU** khi ghi đĩa thành công ③ serialize chỉ xảy ra trong nhịp flush ④ không có đường "chạy no-op âm thầm": key trùng, registry rỗng, ghi/đọc fail — đều có log |

## Ngữ cảnh đã chốt

Nguồn thiết kế: `SystemPlan.md` mục 2 + §0.3 + §A.3 (đã duyệt 2026-08-29). Nguồn khảo sát: `color-loop` — `PlayerSaveLoadService.cs` (khung chết, 3 lỗi cấu trúc → §0) · `GameDataManager.cs` (phản ví dụ god-blob) · `water-flow` — `KPrefs.cs` (tư tưởng cache + on-change, bản sống khỏe duy nhất).

| Nhóm | Chốt |
|---|---|
| **Ai gọi** | Mỗi hệ game sở hữu unit của mình: `new` unit + `ISaveService.Service.Register(unit)` lúc boot (ứng viên tự nhiên: trong `InitializeAsync` của một `BootStep`) · gameplay/UI mutate `unit.Value` rồi `MarkDirty()`, subscribe `Changed` · giá trị lẻ ("đã rate chưa") qua `PrefsBool`/`PrefsInt`… · `FlushAll` registry tự gọi (autosave + pause/quit); game chỉ gọi thêm khi cần chốt sổ sớm (ngay sau IAP) |
| **Mục tiêu** | 4 nghiệm thu của SystemPlan mục 2: thêm unit mới không sửa SDK/unit khác · kill app mất ≤ 1 chu kỳ, file corrupt vẫn vào được game · đổi serializer round-trip đúng giá trị, không sửa code unit · không có đường no-op âm thầm |
| **Ngân sách** | Ghi giá trị: mỗi tương tác (phải rẻ — chỉ cờ + event) · serialize + I/O: mỗi chu kỳ autosave và pause/quit · load: một lần mỗi unit lúc boot. Không hot path mỗi frame |
| **Ranh giới** | SDK: contract + registry + 2 serializer + typed-prefs. Game: model + const key + thời điểm Register + giá trị interval + chọn serializer. Registry **không** biết Bootstrap (Foundation zero-dep) — phối hợp thứ tự flush với hệ khác là việc wire phía game (§0.1) |
| **Hướng mở rộng thật** (đều additive) | Crypto = decorator quanh `ISerializer` · cloud = `ICloudSyncable` tách riêng theo ISP + snapshot trên registry + merge rule · migration version cho model · `PrefsDateTime` + `ForceRefresh` + cờ `syncToServer` khi cloud về |
| **Cố ý KHÔNG làm + lý do** (*xoá đi thì hỏng ở đâu*) | ① **Crypto** — cả 4 repo không dùng thật (Goods-Jam có Rijndael/TripleDES nhưng dead code, key placeholder). ② **Cloud sync** — nhu cầu thật nhưng backend chưa chuẩn chung. *(① ② là quyết định user đã chốt trong SystemPlan "Ngữ cảnh đã chốt" 2026-08-29.)* ③ **Migration version** — chưa có model nào đổi schema. ④ **Load async** — file KB, chưa đo được chậm. ⑤ **Backup file corrupt trước khi ghi đè** — nghiệm thu chỉ đòi "corrupt vẫn vào được game". ⑥ **`Flush(unit)` lẻ** — chưa call site nào cần flush một unit riêng |

**Ba quyết định user đã chốt (2026-08-29):**

1. **asmdef `com.horcrux.runtime` thêm reference `"MemoryPack"` + `versionDefines`** (define `HORCRUX_MEMORYPACK` khi project có package `com.cysharp.memorypack`) — impl MemoryPack chỉ compile khi package tồn tại; project không có package không bị ảnh hưởng (reference theo tên không resolve được thì Unity bỏ qua).
2. **Typed-prefs v1 hẹp hơn contract §A.3:** bỏ `syncToServer` (đăng ký cloud snapshot — cloud v1-out), `PrefsDateTime` (chưa call site thật; giá trị thời gian đang sống trong model unit dạng `long` ticks), `ForceRefresh` (chỉ cần sau khi apply cloud snapshot). Cả ba thêm lại đều additive (tham số optional / class mới / method mới).
3. **Impl JSON = Newtonsoft** (`JsonConvert`) — model không cần attribute, serialize được Dictionary và type con. SDK vì thế yêu cầu package `com.unity.nuget.newtonsoft-json` (color-loop đang có sẵn như dependency gián tiếp — thấy trong `packages-lock.json`; project thiếu thì thêm một dòng manifest); DLL precompiled nên **không** cần sửa asmdef.

**Khảo sát tái sử dụng:** `IService<T>` đã có (`Abstractions/Foundations/IService.cs`) — dùng lại cho `ISaveService` (save là hệ **bắt buộc**: thiếu là lỗi cấu hình, phải throw sớm — không dùng `IOptionalService`). `EventBus` (Utilities) không dùng — `Changed` là event nội bộ một unit, listener wire trực tiếp. `MonoSingleton` không dùng — đăng ký qua `[Service]` như tiền lệ `BootstrapRunner`. `KPrefs` (water-flow) **không bê nguyên**: JSON-hoá cả `int` (§A.3 đòi chuyên biệt theo type gọi thẳng `GetInt`), lambda subscribe `OnForceRefresh` static không bao giờ unsubscribe, và đọc khi chưa có key thì deserialize default **mỗi lần gọi** — extract tư tưởng (cache đọc một lần + on-change), viết mới theo contract §A.3.

---

## §0. Bốn ràng buộc thật

Không có toán. Bốn sự thật của nền tảng và ba bug có thật trong repo quyết định hình dạng code — đọc trước khi viết.

### 0.1. Android kill không báo trước — `OnApplicationQuit` không phải chỗ dựa

Trên Android, user swipe-kill hoặc hệ điều hành thu hồi RAM thì process chết **không chạy** `OnApplicationQuit`; tín hiệu tin được cuối cùng là `OnApplicationPause(true)`. Vì vậy hợp đồng của hệ là **"mất tối đa MỘT chu kỳ autosave"**, không phải "không bao giờ mất": autosave chu kỳ là lưới đỡ chính, flush ở pause là chốt sổ, quit chỉ là thêm-được-thì-tốt.

**Hệ quả lên thứ tự với hệ khác:** registry tự flush trong magic method `OnApplicationPause`/`OnApplicationQuit` của chính nó — nhưng Unity **không đảm bảo thứ tự** magic method giữa các MonoBehaviour, nên nếu một hệ khác ghi dữ liệu trong pause hook của nó (ví dụ chốt coin), flush của registry có thể chạy **trước** lần ghi đó. Flush trong registry vì thế là **lưới an toàn**, không phải flush có thứ tự; game cần thứ tự thì wire thêm một `BootStep` gọi `FlushAll()` trong `OnAppPause(true)` — fan-out **ngược** của `BootstrapRunner` đảm bảo hệ trên ghi xong trước (quyết định pause-đi-ngược user đã chốt ở plan Bootstrap). Flush hai lần vô hại: lần sau không thấy unit nào dirty.

### 0.2. Ghi file có thể đứt giữa chừng — không bao giờ ghi thẳng đè file chính

Kill đúng lúc đang ghi là file cụt trên đĩa. Ghi đè trực tiếp thì bản save cũ (đang lành) cũng chết theo. Cách xử ở **hai đầu**:

| Đầu | Luật | Cách |
|---|---|---|
| Ghi | file chính chỉ được thay bằng **một bản đã ghi trọn** | ghi ra `key.sav.tmp` → `File.Replace` (file chính đã có) / `File.Move` (chưa có) |
| Đọc | file hỏng → **model default + log, không throw** | try/catch quanh đọc + deserialize; save hỏng không được chặn người chơi vào game |

*Đã sai một lần — color-loop `PlayerSaveLoadService`:* `SaveToDevice` gọi `File.WriteAllBytes` thẳng vào file chính, và `Load()` không try/catch quanh `Deserialize` — một file cụt là exception **mỗi lần boot**, save thành "brick" vĩnh viễn.

**Phép kiểm tái lập:** mở `demo_progress.sav` bằng notepad, gõ rác vào, Play — vẫn vào demo, log error nêu đúng key, giá trị về default.

### 0.3. Dirty là hợp đồng hai chiều — game set, registry reset SAU khi ghi thành công

Cờ dirty có đúng một người set (`MarkDirty` — game gọi sau khi mutate model) và đúng một người reset (registry — **sau** khi I/O thành công). Reset trước I/O thì I/O lỗi là dữ liệu **mất im lặng**: cờ đã tắt, không ai ghi lại nữa.

*Đã sai một lần — color-loop `PlayerSaveLoadService.Save()`:*

```csharp
if (force || _isDirty)
{
    _isDirty = false;                                  // reset TRƯỚC khi ghi
}
var bytes = MemoryPackSerializer.Serialize(data);      // và thân serialize+ghi nằm NGOÀI if
SaveToDevice(bytes);                                   // → dirty-check vô hiệu, lần nào gọi cũng ghi
```

Hai lỗi trong 6 dòng: reset-trước-I/O, và khối `if` chỉ bọc mỗi việc reset cờ nên serialize + ghi chạy bất kể dirty. Hình dạng đúng trong plan này: `ClearDirty()` là method của contract mà **chỉ registry gọi**, đặt sau `WriteAtomic` trong cùng `try` — ghi fail thì nhảy vào `catch`, cờ còn nguyên, chu kỳ sau thử lại.

### 0.4. Serialize thuộc nhịp flush, không thuộc nhịp đổi giá trị

Mỗi lần coin đổi mà serialize cả model + I/O là trả giá theo nhịp tương tác cho một việc chỉ cần theo nhịp chu kỳ — mỗi phép tính phải khai được nhịp của nó. `MarkDirty` vì thế chỉ set cờ + phát `Changed`; serialize dồn về `FlushAll`, và chỉ unit **dirty** mới bị serialize.

*Đã sai một lần — color-loop `GameDataManager`:* mỗi thay đổi bất kỳ field nào → `LateUpdate` frame đó `JsonUtility.ToJson` **cả god-blob 25+ field** + `PlayerPrefs.Save()` (I/O đĩa) ngay trong frame.

Cùng họ với nó là bài học "khung chạy no-op âm thầm": khung save "sạch" của color-loop chết vì `AssignService()` không có caller — autosave loop chạy mà không lưu gì, **không log gì**. Câu trả lời cấu trúc: mọi đường không-làm-gì-được của registry (key trùng, registry rỗng khi flush, ghi/đọc fail) đều phải **kêu lên** (bất biến ④).

---

## Bản đồ triển khai

| Task | File | Nội dung |
|---|---|---|
| 1 | `Abstractions/Foundations/Persistence/` — `ISerializer.cs` · `ISaveUnit.cs` · `SaveUnit.cs` · `ISaveService.cs` | 4 contract |
| 2 | `Implementations/Foundations/Persistence/` — `NewtonsoftJsonSerializer.cs` · `MemoryPackSaveSerializer.cs` + sửa `com.horcrux.runtime.asmdef` | 2 serializer |
| 3 | `Implementations/Foundations/Persistence/SaveRegistry.cs` | registry |
| 4 | `Implementations/Foundations/Persistence/Prefs.cs` | typed-prefs |
| 5 | `Implementations/Foundations/Persistence/Demo/` — `DemoSaveUnit.cs` · `DemoSaveDriver.cs` + scene demo | nghiệm thu |

Thứ tự: **1 → 2 → 3 → 4 → 5** (4 độc lập với 2–3, nhưng demo ở 5 dùng cả hai).

---

### Task 1: 4 contract

**Files:** 4 file mới trong `Assets/Horcrux/Runtime/Abstractions/Foundations/Persistence/`

**Interfaces:**
- Consumes: `IService<T>` (đã có ở `Abstractions/Foundations/IService.cs`) · `System.Buffers`.
- Produces: `ISerializer` (2 method) · `ISaveUnit` (2 property + 3 method) · `abstract class SaveUnit<TModel>` (game kế thừa — `Value`, `MarkDirty`, `Changed`) · `ISaveService : IService<ISaveService>` (2 method).

**Quyết định thiết kế:**

| Quyết định | Lý do |
|---|---|
| `ISaveUnit` non-generic + `SaveUnit<TModel>` generic implement sẵn | Registry cần `List<ISaveUnit>` đồng nhất; typed nằm ở lớp base game kế thừa. Game **không** implement `ISaveUnit` trực tiếp — 3 method wire-format của nó là việc của base (đúng một chỗ, bất biến §3.8) |
| Key truyền qua **ctor**, kiểu `string` | Khoá wire format là hợp đồng với file trên đĩa (SystemPlan §0.4b: định danh ổn định ra ngoài dùng `const string`) — *đã sai một lần:* `PlayerSaveLoadService` đặt tên file bằng `typeof(T).Name`, đổi tên type là mất save |
| `SaveUnit` là **plain class**, không MonoBehaviour | Unit không cần Inspector, không cần lifecycle Unity — hệ game sở hữu nó `new` trực tiếp (factory hoặc owner thì đương nhiên `new`). Khác `BootStep`: bước boot cần serialize vào list Inspector, unit thì không |
| API đọc/ghi = property `Value` (getter) + `MarkDirty()` | Học `KPrefs.Value` (bản sống khỏe duy nhất) nhưng model là mutable class — mutate field rồi báo dirty là một nhịp; setter thay cả model chỉ cần khi cloud apply snapshot (mở rộng sau, thêm method là additive) |
| `Changed` là `event Action`, fan-out qua `GetInvocationList` + try/catch từng listener | Đăng ký thưa (SystemPlan §0.4b) → `event` hợp lệ; cô lập lỗi listener là luật §0.4a. Alloc của `GetInvocationList` theo nhịp tương tác, không theo frame — chấp nhận |
| `ReadPayload` cũng bắn `Changed` | "Value đổi thì `Changed` bắn" là **một** luật không ngoại lệ — load từ đĩa là một lần Value đổi; UI subscribe trước Register vẫn nhận đúng trạng thái |
| `ClearDirty()` nằm trên contract, XML doc ghi rõ "chỉ registry gọi" | §0.3 — reset một nơi. Không giấu được bằng access modifier (interface là public) nên nói rõ bằng contract; registry là caller duy nhất trong SDK |
| `ISerializer` nhận `IBufferWriter<byte>` (ghi) / `ReadOnlySpan<byte>` (đọc) | SystemPlan §0.4b — buffer pool được, không alloc `byte[]` mỗi lần lưu |
| `ISaveService` dùng `IService` (throw), không `IOptionalService` | Save là hệ **bắt buộc** — thiếu registry trong scene là lỗi cấu hình, phải lộ sớm (SystemPlan §0.2) |

- [ ] **Step 1: `ISerializer.cs`**

```csharp
using System;
using System.Buffers;

namespace Horcrux.Runtime.Abstractions.Persistence
{
    /// <summary>Format save (JSON, MemoryPack…) — registry cầm, unit không biết; đổi format không đụng logic game.</summary>
    public interface ISerializer
    {
        /// <summary>Ghi <paramref name="value"/> vào buffer tái dùng của registry — không alloc mảng theo mỗi lần lưu.</summary>
        void Serialize<T>(in T value, IBufferWriter<byte> writer);

        /// <summary>Dựng model từ bytes đã đọc. Bytes hỏng thì cứ throw — registry bắt và giữ model default (plan §0.2).</summary>
        T Deserialize<T>(ReadOnlySpan<byte> bytes);
    }
}
```

- [ ] **Step 2: `ISaveUnit.cs`**

```csharp
using System;
using System.Buffers;

namespace Horcrux.Runtime.Abstractions.Persistence
{
    /// <summary>Một cụm dữ liệu save độc lập. Game kế thừa <see cref="SaveUnit{TModel}"/>, không implement trực tiếp.</summary>
    public interface ISaveUnit
    {
        /// <summary>Khoá wire format — const string tường minh, KHÔNG suy từ tên type (đổi tên type không được mất save).</summary>
        string Key { get; }

        /// <summary>Có thay đổi chưa ghi xuống đĩa — game set qua MarkDirty, registry reset SAU khi ghi thành công.</summary>
        bool IsDirty { get; }

        /// <summary>Registry gọi trong nhịp flush: serialize model hiện tại vào buffer chung.</summary>
        void WritePayload(ISerializer serializer, IBufferWriter<byte> writer);

        /// <summary>Registry gọi một lần lúc Register khi file tồn tại. Throw ⇒ registry giữ model default.</summary>
        void ReadPayload(ISerializer serializer, ReadOnlySpan<byte> bytes);

        /// <summary>CHỈ registry gọi, sau khi ghi đĩa thành công — nơi duy nhất được reset dirty (plan §0.3).</summary>
        void ClearDirty();
    }
}
```

- [ ] **Step 3: `SaveUnit.cs`**

```csharp
using System;
using System.Buffers;
using UnityEngine;

namespace Horcrux.Runtime.Abstractions.Persistence
{
    /// <summary>Base cho mọi save-unit: model typed + dirty + on-change. Game chỉ khai model và key.</summary>
    /// <remarks>
    /// Cách dùng: mutate field trong <see cref="Value"/> rồi gọi <see cref="MarkDirty"/> — serialize KHÔNG
    /// xảy ra ở đây mà dồn về nhịp flush của registry (plan §0.4). Unit dùng được ngay sau
    /// <c>ISaveService.Register</c> (load xảy ra ngay trong Register).
    /// </remarks>
    public abstract class SaveUnit<TModel> : ISaveUnit where TModel : class, new()
    {
        private readonly string key;

        /// <param name="key">Khoá wire format — truyền const string do unit khai; là hợp đồng với file trên đĩa.</param>
        protected SaveUnit(string key)
        {
            this.key = key;
            Value = new TModel();
        }

        public string Key => key;

        /// <summary>Model hiện tại — không bao giờ null; chưa có file trên đĩa thì là <c>new TModel()</c>.</summary>
        public TModel Value { get; private set; }

        public bool IsDirty { get; private set; }

        /// <summary>Bắn mỗi khi Value đổi: sau MarkDirty và sau khi load từ đĩa. Listener lỗi không kéo unit chết.</summary>
        public event Action Changed;

        /// <summary>Gọi sau khi mutate model. Chỉ set cờ + phát Changed — nhẹ, gọi mỗi tương tác là hợp lệ.</summary>
        public void MarkDirty()
        {
            IsDirty = true;
            RaiseChanged();
        }

        void ISaveUnit.WritePayload(ISerializer serializer, IBufferWriter<byte> writer)
            => serializer.Serialize(Value, writer);

        void ISaveUnit.ReadPayload(ISerializer serializer, ReadOnlySpan<byte> bytes)
        {
            Value = serializer.Deserialize<TModel>(bytes) ?? new TModel();
            IsDirty = false;                             // vừa đọc từ đĩa — đĩa và RAM đang khớp
            RaiseChanged();
        }

        void ISaveUnit.ClearDirty() => IsDirty = false;

        private void RaiseChanged()
        {
            var handlers = Changed;
            if (handlers == null) return;

            // Cô lập lỗi từng listener (SystemPlan §0.4a); alloc theo nhịp tương tác, không theo frame.
            foreach (Action handler in handlers.GetInvocationList())
            {
                try { handler(); }
                catch (Exception e) { Debug.LogException(e); }
            }
        }
    }
}
```

- [ ] **Step 4: `ISaveService.cs`**

```csharp
namespace Horcrux.Runtime.Abstractions.Persistence
{
    /// <summary>Registry save — service BẮT BUỘC: game nào cũng lưu tiến độ, thiếu là lỗi cấu hình (throw sớm).</summary>
    public interface ISaveService : IService<ISaveService>
    {
        /// <summary>Đăng ký unit và LOAD NGAY từ đĩa — unit dùng được ngay khi hàm trả về.</summary>
        /// <param name="unit">Key trùng unit đã đăng ký: log error, unit mới bị bỏ qua.</param>
        void Register(ISaveUnit unit);

        /// <summary>Ghi mọi unit dirty xuống đĩa ngay. Registry tự gọi ở autosave/pause/quit —
        /// game chỉ gọi thêm khi cần chốt sổ sớm (ví dụ ngay sau IAP thành công).</summary>
        void FlushAll();
    }
}
```

- [ ] **Step 5: Kiểm chứng** — compile sạch; chưa có hành vi chạy được (contract thuần) — hành vi kiểm ở Task 3 và 5.

- [ ] **Step 6: Commit** — `feat(sdk): add persistence contracts (save-unit, serializer, registry)`

---

### Task 2: 2 serializer + asmdef

**Files:**
- Create: `Assets/Horcrux/Runtime/Implementations/Foundations/Persistence/NewtonsoftJsonSerializer.cs`
- Create: `Assets/Horcrux/Runtime/Implementations/Foundations/Persistence/MemoryPackSaveSerializer.cs`
- Modify: `Assets/Horcrux/Runtime/com.horcrux.runtime.asmdef`

**Interfaces:**
- Consumes: `ISerializer` (Task 1) · `Newtonsoft.Json.JsonConvert` · `MemoryPack.MemoryPackSerializer` (khi có package).
- Produces: `NewtonsoftJsonSerializer : ISerializer` · `MemoryPackSaveSerializer : ISerializer` (chỉ khi `HORCRUX_MEMORYPACK`).

**Quyết định thiết kế:**

| Quyết định | Lý do |
|---|---|
| `ISerializer` có abstraction **ngay v1** | Có hai implementation **thật** ngay bây giờ (MemoryPack — color-loop, JSON — water-flow), không phải phòng xa |
| JSON = Newtonsoft | Quyết định 3 user đã chốt ở "Ngữ cảnh đã chốt" — model không cần attribute, Dictionary và type con serialize được; package đi qua `com.unity.nuget.newtonsoft-json`, DLL precompiled nên không cần sửa asmdef |
| MemoryPack qua `versionDefines`, bọc `#if HORCRUX_MEMORYPACK` | Quyết định 1 user đã chốt — SDK không được ép mọi project cài MemoryPack; define chỉ bật khi package có mặt |
| Tên class `MemoryPackSaveSerializer` · `NewtonsoftJsonSerializer` | Tránh đụng tên `MemoryPack.MemoryPackSerializer` và `Newtonsoft.Json.JsonSerializer` của thư viện — một từ một nghĩa trong toàn hệ |
| Chọn serializer là quyết định **một lần trước khi ship** | File đã ghi format A đọc bằng format B = corrupt-về-mặt-logic → default + log (không mất khả năng vào game, nhưng mất save). Đổi format sau ship cần migration — mở rộng sau |

- [ ] **Step 1: `NewtonsoftJsonSerializer.cs`**

```csharp
using System;
using System.Buffers;
using System.Text;
using Horcrux.Runtime.Abstractions.Persistence;
using Newtonsoft.Json;

namespace Horcrux.Runtime.Implementations.Persistence
{
    /// <summary>Serializer mặc định — Newtonsoft JSON: file đọc được bằng mắt khi debug save.</summary>
    /// <remarks>Model là data thuần (số, chuỗi, List, Dictionary) với field/property public — không cần
    /// attribute. Không nhét UnityEngine.Object hay Vector vào model: tham chiếu engine không thuộc save,
    /// và struct Unity có property tự tham chiếu (Vector3.normalized) làm serializer đệ quy vô hạn.</remarks>
    public sealed class NewtonsoftJsonSerializer : ISerializer
    {
        public void Serialize<T>(in T value, IBufferWriter<byte> writer)
        {
            string json = JsonConvert.SerializeObject(value);
            var span = writer.GetSpan(Encoding.UTF8.GetMaxByteCount(json.Length));
            writer.Advance(Encoding.UTF8.GetBytes(json.AsSpan(), span));
        }

        public T Deserialize<T>(ReadOnlySpan<byte> bytes)
            => JsonConvert.DeserializeObject<T>(Encoding.UTF8.GetString(bytes));
    }
}
```

- [ ] **Step 2: `MemoryPackSaveSerializer.cs`**

```csharp
#if HORCRUX_MEMORYPACK
using System;
using System.Buffers;
using Horcrux.Runtime.Abstractions.Persistence;

namespace Horcrux.Runtime.Implementations.Persistence
{
    /// <summary>Serializer MemoryPack — binary, nhanh, file nhỏ; model phải [MemoryPackable] partial.</summary>
    /// <remarks>Chỉ compile khi project có package com.cysharp.memorypack (define khai trong asmdef).</remarks>
    public sealed class MemoryPackSaveSerializer : ISerializer
    {
        public void Serialize<T>(in T value, IBufferWriter<byte> writer)
            => global::MemoryPack.MemoryPackSerializer.Serialize(writer, value);

        public T Deserialize<T>(ReadOnlySpan<byte> bytes)
            => global::MemoryPack.MemoryPackSerializer.Deserialize<T>(bytes);
    }
}
#endif
```

- [ ] **Step 3: sửa `com.horcrux.runtime.asmdef`** — thêm `"MemoryPack"` vào mảng `references` và thêm khối `versionDefines` (các field khác giữ nguyên):

```json
"references": [
    "InitArgs",
    "InitArgs.Services",
    "Unity.Addressables",
    "Unity.ResourceManager",
    "UniTask",
    "UniTask.Addressables",
    "Unity.Mathematics",
    "MemoryPack"
],
"versionDefines": [
    {
        "name": "com.cysharp.memorypack",
        "expression": "",
        "define": "HORCRUX_MEMORYPACK"
    }
]
```

- [ ] **Step 4: Kiểm chứng** (round-trip — chạy được ở Task 5 qua ContextMenu; tại đây kiểm compile + bảng kỳ vọng):

| Input | Kỳ vọng |
|---|---|
| Model `{coins=5, currentLevel=3, unlockedSkins=["đỏ"]}` → Serialize → Deserialize | model mới bằng giá trị từng field (chuỗi tiếng Việt UTF-8 nguyên vẹn) |
| Model default `new()` → round-trip | về đúng default |
| Model có `Dictionary<string,int>` và chuỗi tiếng Việt → round-trip Newtonsoft | về đúng giá trị |
| Trong color-loop (có MemoryPack): compile có `MemoryPackSaveSerializer`; project không có package: file bị `#if` loại, không lỗi compile | ✓ cả hai chiều |

- [ ] **Step 5: Commit** — `feat(sdk): add Newtonsoft + MemoryPack serializers (versionDefines)`

---

### Task 3: `SaveRegistry`

**Files:**
- Create: `Assets/Horcrux/Runtime/Implementations/Foundations/Persistence/SaveRegistry.cs`

**Interfaces:**
- Consumes: `ISaveUnit` · `ISerializer` · `ISaveService` (Task 1) · 2 serializer (Task 2) · `[Service]` của Sisus.Init · UniTask.
- Produces: `SaveRegistry : MonoBehaviour, ISaveService` — `Register(ISaveUnit)` · `FlushAll()`; cấu hình Inspector: `autosaveIntervalSeconds` · `serializerKind`.

**Quyết định thiết kế:**

| Quyết định | Lý do |
|---|---|
| `Register` **load ngay** (sync) | Không tồn tại trạng thái "đã đăng ký nhưng chưa load" — mọi code sau dòng Register đọc được giá trị thật, không cần phối hợp thứ tự thêm. File cỡ KB lúc boot, sync là bản đơn giản nhất còn đúng; async thêm sau nếu đo được chậm |
| `EnsureReady()` idempotent, gọi từ cả `Awake` lẫn `Register` | Unity không đảm bảo thứ tự `Awake` giữa registry và hệ game đăng ký sớm — chính bài toán Bootstrap; Persistence là Foundation zero-dep nên phải tự đứng được, không dựa runner |
| Autosave = UniTask loop + `destroyCancellationToken`, `DelayType.Realtime` | Loop chết theo GameObject (không `while(true)` sống sau destroy — cạm bẫy SystemPlan mục 2); Realtime vì autosave không được ngừng khi game pause bằng `timeScale = 0`. Không dùng Ticker — Foundation zero-dep, và nhịp giây không cần nguồn tick trung tâm |
| Autosave tick gọi thẳng `FlushAll` | Cửa hẹp là thân chung: autosave, pause, quit, gọi tay — cùng MỘT thân flush, không thể lệch nhau |
| Flush ở magic method của chính registry | §0.1 — lưới an toàn khi project không dùng Bootstrap; đây là MonoBehaviour duy nhất của hệ nên magic method sống đúng một chỗ. Flush có thứ tự (khi cần) đi đường `BootStep` phía game |
| Ghi fail → **giữ dirty** + log, chu kỳ sau thử lại | §0.3 — `ClearDirty` nằm sau `WriteAtomic` trong cùng `try` |
| Key trùng lúc Register → log error + bỏ unit mới | Hai unit một key là unit sau đè file unit trước (cạm bẫy SystemPlan mục 2); bất biến ④ — phải kêu lên. So sánh tuyến tính `List` đủ: số unit cỡ chục, chạy lúc boot |
| Flush khi 0 unit → `LogWarning` một lần | Bất biến ④ — khung của color-loop chết âm thầm vì autosave no-op không log (§0.4) |
| Buffer `ArrayBufferWriter<byte>` field, `Clear()` mỗi unit | SystemPlan mục 2 "ghi vào buffer dùng chung" — grow-only, không alloc theo chu kỳ |
| Enum `ESaveSerializer` nested private trong registry | Chỉ registry cần nó (config Inspector) — không phình namespace public khi chưa có người dùng thứ hai |

**Editor setup — bước thật:**

1. Scene entry của game: tạo GameObject `[Save]` → add `SaveRegistry`.
2. Inspector: đặt `autosaveIntervalSeconds` (mặc định 5) và `serializerKind` theo game — **chọn serializer một lần trước khi ship**.

- [ ] **Step 1: `SaveRegistry.cs`**

```csharp
using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Cysharp.Threading.Tasks;
using Horcrux.Runtime.Abstractions.Persistence;
using Sisus.Init;
using UnityEngine;

namespace Horcrux.Runtime.Implementations.Persistence
{
    /// <summary>Chủ vòng đời save: load lúc Register, autosave chu kỳ, flush ở pause/quit, ghi nguyên tử.</summary>
    /// <remarks>
    /// Hợp đồng: kill app mất tối đa MỘT chu kỳ autosave (plan §0.1 — OnApplicationQuit không tin được
    /// trên Android). Flush trong magic method của registry là lưới an toàn KHÔNG thứ tự; game cần
    /// flush sau khi hệ khác ghi trong pause hook của chúng thì gọi <see cref="FlushAll"/> từ một
    /// BootStep (fan-out ngược của BootstrapRunner). Flush hai lần vô hại — lần sau không thấy dirty.
    /// </remarks>
    [Service(typeof(ISaveService), FindFromScene = true)]
    public sealed class SaveRegistry : MonoBehaviour, ISaveService
    {
        [SerializeField, Min(1f), Tooltip("Chu kỳ autosave (giây). Kill app mất tối đa một chu kỳ này.")]
        private float autosaveIntervalSeconds = 5f;

        [SerializeField, Tooltip("Format save. Chọn MỘT LẦN trước khi ship — đổi sau khi ship là migration.")]
        private ESaveSerializer serializerKind = ESaveSerializer.NewtonsoftJson;

        private enum ESaveSerializer { NewtonsoftJson = 0, MemoryPack = 1 }

        private readonly List<ISaveUnit> units = new();
        private readonly ArrayBufferWriter<byte> payloadBuffer = new();  // buffer dùng chung, grow-only
        private ISerializer serializer;
        private string saveDirectory;
        private bool warnedEmptyOnce;

        private void Awake()
        {
            DontDestroyOnLoad(gameObject);
            EnsureReady();
        }

        private void Start()
            => AutosaveLoopAsync(destroyCancellationToken).Forget();

        // Lưới an toàn không thứ tự — flush CÓ thứ tự thuộc BootStep phía game (xem remarks class).
        private void OnApplicationPause(bool isPaused)
        {
            if (isPaused) FlushAll();
        }

        private void OnApplicationQuit() => FlushAll();

        public void Register(ISaveUnit unit)
        {
            if (unit == null)
            {
                Debug.LogError("[Save] Register(null) — quên tạo unit?", this);
                return;
            }

            EnsureReady();                               // hệ game có thể Register trước Awake của registry
            foreach (var existing in units)
            {
                if (existing.Key == unit.Key)
                {
                    Debug.LogError(
                        $"[Save] Key '{unit.Key}' đã có unit đăng ký — bỏ qua unit mới. " +
                        "Hai unit một key là unit sau đè file unit trước.", this);
                    return;
                }
            }

            units.Add(unit);
            LoadUnit(unit);
        }

        public void FlushAll()
        {
            if (units.Count == 0)
            {
                if (!warnedEmptyOnce)
                {
                    warnedEmptyOnce = true;              // khung save không được chạy no-op âm thầm (plan §0.4)
                    Debug.LogWarning("[Save] Flush khi chưa có unit nào đăng ký — quên Register?", this);
                }
                return;
            }

            foreach (var unit in units)
            {
                if (!unit.IsDirty) continue;
                try
                {
                    payloadBuffer.Clear();
                    unit.WritePayload(serializer, payloadBuffer);
                    WriteAtomic(PathForKey(unit.Key), payloadBuffer.WrittenSpan);
                    unit.ClearDirty();                   // reset CHỈ sau khi ghi thành công (plan §0.3)
                }
                catch (Exception e)
                {
                    Debug.LogError($"[Save] Ghi unit '{unit.Key}' thất bại — giữ dirty, chu kỳ sau thử lại.", this);
                    Debug.LogException(e, this);
                }
            }
        }

        private async UniTaskVoid AutosaveLoopAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                // Realtime: autosave không được ngừng khi game pause bằng timeScale = 0.
                await UniTask.Delay(TimeSpan.FromSeconds(autosaveIntervalSeconds), DelayType.Realtime,
                    cancellationToken: cancellationToken).SuppressCancellationThrow();
                if (cancellationToken.IsCancellationRequested) return;
                FlushAll();
            }
        }

        private void LoadUnit(ISaveUnit unit)
        {
            string path = PathForKey(unit.Key);
            try
            {
                if (!File.Exists(path)) return;          // lần chơi đầu — unit giữ model default
                byte[] bytes = File.ReadAllBytes(path);
                if (bytes.Length == 0) return;
                unit.ReadPayload(serializer, bytes);
            }
            catch (Exception e)
            {
                // Save hỏng không được chặn người chơi vào game (plan §0.2) — model default + log.
                Debug.LogError($"[Save] Đọc unit '{unit.Key}' thất bại — dùng model default; " +
                               "file sẽ bị ghi đè ở lần lưu tới.", this);
                Debug.LogException(e, this);
            }
        }

        // Không bao giờ ghi thẳng đè file chính — kill giữa chừng ghi là file cụt (plan §0.2).
        private static void WriteAtomic(string path, ReadOnlySpan<byte> payload)
        {
            string tempPath = path + ".tmp";
            using (var stream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None))
                stream.Write(payload);

            if (File.Exists(path)) File.Replace(tempPath, path, destinationBackupFileName: null);
            else File.Move(tempPath, path);
        }

        private string PathForKey(string key) => Path.Combine(saveDirectory, key + ".sav");

        private void EnsureReady()
        {
            if (serializer != null) return;
            saveDirectory = Path.Combine(Application.persistentDataPath, "Saves");
            Directory.CreateDirectory(saveDirectory);
            serializer = CreateSerializer(serializerKind);
        }

        private ISerializer CreateSerializer(ESaveSerializer kind)
        {
            switch (kind)
            {
#if HORCRUX_MEMORYPACK
                case ESaveSerializer.MemoryPack:
                    return new MemoryPackSaveSerializer();
#else
                case ESaveSerializer.MemoryPack:
                    Debug.LogError("[Save] Chọn MemoryPack nhưng project không có package " +
                                   "com.cysharp.memorypack — rơi về Newtonsoft JSON.", this);
                    return new NewtonsoftJsonSerializer();
#endif
                default:
                    return new NewtonsoftJsonSerializer();
            }
        }
    }
}
```

- [ ] **Step 2: Kiểm chứng** (bảng input → kỳ vọng; chưa kèm code test — Task 5 nghiệm thu bằng demo + thao tác tay):

| Input | Kỳ vọng |
|---|---|
| Register unit lần đầu (chưa có file) | không log lỗi, `Value` là model default, `IsDirty == false` |
| Mutate + `MarkDirty` → chờ hết 1 chu kỳ autosave | file `Saves/<key>.sav` xuất hiện, `IsDirty == false` |
| Register lại đúng key đó (unit thứ hai) | `LogError` nêu key, unit mới không được nạp, unit cũ vẫn hoạt động |
| File `<key>.sav` bị sửa thành rác trước khi Play | vào game bình thường, `LogError` nêu key, `Value` là default |
| `FlushAll` khi không có gì dirty | không I/O, không log |
| Flush khi 0 unit đăng ký | `LogWarning` đúng một lần cho cả phiên |
| Chọn `MemoryPack` ở project không có package | `LogError` + hoạt động tiếp bằng Newtonsoft JSON |
| Destroy registry giữa phiên | autosave loop dừng (token), không exception |

- [ ] **Step 3: Commit** — `feat(sdk): add SaveRegistry (load-on-register, autosave, atomic write)`

---

### Task 4: Typed-prefs `Prefs<T>`

**Files:**
- Create: `Assets/Horcrux/Runtime/Implementations/Foundations/Persistence/Prefs.cs`

**Interfaces:**
- Consumes: `PlayerPrefs` (engine).
- Produces: `abstract class Prefs<T>` (`Key` · `Value` · `HasValue` · `Delete()` · `event Action<T> Changed`) + 4 chuyên biệt `PrefsInt` · `PrefsBool` · `PrefsFloat` · `PrefsString`.

**Quyết định thiết kế:**

| Quyết định | Lý do |
|---|---|
| Đường riêng cạnh save-unit, backed bằng PlayerPrefs | SystemPlan §A.3: giá trị lẻ ("đã rate chưa") không đáng dựng model + đăng ký registry; model lớn đi PlayerPrefs là cạm bẫy ngược lại — hai đường cho hai cỡ dữ liệu |
| Chuyên biệt theo type, không JSON generic | §A.3: `PrefsInt` gọi thẳng `GetInt` — không serialize JSON cho một số nguyên (*KPrefs làm sai chỗ này*) |
| Cache sau lần đọc đầu, kể cả khi **chưa có key** | Đọc PlayerPrefs là native call; KPrefs khi chưa có key thì deserialize default **mỗi lần gọi** — bản này cache cả nhánh default |
| Set ghi thẳng PlayerPrefs, **không** gọi `PlayerPrefs.Save()` | Unity tự persist PlayerPrefs ở pause; ép I/O đĩa theo mỗi set là trả giá nhịp tương tác cho việc nhịp chu kỳ (cùng luật plan §0.4) |
| Không `syncToServer` / `PrefsDateTime` / `ForceRefresh` ở v1 | Giả định 2 ở "Ngữ cảnh đã chốt" — cả ba gắn cloud (v1-out); thêm lại additive |
| `Read()` abstract chỉ chạy khi `HasValue == true` | Nhánh default xử một nơi ở base — 4 chuyên biệt không lặp lại logic default (một tri thức một chỗ, §3.8) |

- [ ] **Step 1: `Prefs.cs`**

```csharp
using System;
using UnityEngine;

namespace Horcrux.Runtime.Implementations.Persistence
{
    /// <summary>Giá trị lẻ lưu PlayerPrefs, không đáng dựng model ("đã rate chưa"…). Model lớn thuộc SaveUnit.</summary>
    /// <remarks>Đọc cache sau lần đầu — đọc trong Update không thành bug hiệu năng. Set ghi thẳng
    /// PlayerPrefs (Unity tự persist ở pause), không ép PlayerPrefs.Save() theo mỗi set (plan §0.4).</remarks>
    public abstract class Prefs<T>
    {
        private readonly T defaultValue;
        private T cachedValue;
        private bool isCached;

        /// <param name="key">Khoá PlayerPrefs — const string, hợp đồng wire format.</param>
        /// <param name="defaultValue">Trả về khi chưa từng set.</param>
        protected Prefs(string key, T defaultValue)
        {
            Key = key;
            this.defaultValue = defaultValue;
        }

        public string Key { get; }

        public bool HasValue => PlayerPrefs.HasKey(Key);

        /// <summary>Bắn sau mỗi set và sau <see cref="Delete"/> — UI bám thẳng, không cần lớp trung gian.</summary>
        public event Action<T> Changed;

        public T Value
        {
            get
            {
                if (!isCached)
                {
                    cachedValue = HasValue ? Read() : defaultValue;
                    isCached = true;
                }
                return cachedValue;
            }
            set
            {
                Write(value);
                cachedValue = value;
                isCached = true;
                RaiseChanged(value);
            }
        }

        public void Delete()
        {
            PlayerPrefs.DeleteKey(Key);
            cachedValue = defaultValue;
            isCached = true;
            RaiseChanged(defaultValue);
        }

        /// <summary>Đọc thẳng PlayerPrefs — base chỉ gọi khi key tồn tại, và chỉ một lần cho tới set/Delete kế.</summary>
        protected abstract T Read();

        protected abstract void Write(T value);

        private void RaiseChanged(T value)
        {
            var handlers = Changed;
            if (handlers == null) return;

            // Cô lập lỗi từng listener (SystemPlan §0.4a); alloc theo nhịp tương tác — chấp nhận.
            foreach (Action<T> handler in handlers.GetInvocationList())
            {
                try { handler(value); }
                catch (Exception e) { Debug.LogException(e); }
            }
        }
    }

    public sealed class PrefsInt : Prefs<int>
    {
        public PrefsInt(string key, int defaultValue = 0) : base(key, defaultValue) { }
        protected override int Read() => PlayerPrefs.GetInt(Key);
        protected override void Write(int value) => PlayerPrefs.SetInt(Key, value);
    }

    /// <summary>Lưu int 0/1 — PlayerPrefs không có bool.</summary>
    public sealed class PrefsBool : Prefs<bool>
    {
        public PrefsBool(string key, bool defaultValue = false) : base(key, defaultValue) { }
        protected override bool Read() => PlayerPrefs.GetInt(Key) != 0;
        protected override void Write(bool value) => PlayerPrefs.SetInt(Key, value ? 1 : 0);
    }

    public sealed class PrefsFloat : Prefs<float>
    {
        public PrefsFloat(string key, float defaultValue = 0f) : base(key, defaultValue) { }
        protected override float Read() => PlayerPrefs.GetFloat(Key);
        protected override void Write(float value) => PlayerPrefs.SetFloat(Key, value);
    }

    public sealed class PrefsString : Prefs<string>
    {
        public PrefsString(string key, string defaultValue = "") : base(key, defaultValue) { }
        protected override string Read() => PlayerPrefs.GetString(Key);
        protected override void Write(string value) => PlayerPrefs.SetString(Key, value);
    }
}
```

- [ ] **Step 2: Kiểm chứng:**

| Input | Kỳ vọng |
|---|---|
| `new PrefsInt("k", 7).Value` khi chưa từng set | `7`, và lần get thứ hai không gọi lại PlayerPrefs (cache) |
| Set `Value = 3` → get | `3`; `Changed` bắn đúng một lần với `3` |
| `PrefsBool` set `true` → restart Play | `Value == true` (persist qua PlayerPrefs) |
| `Delete()` | `HasValue == false`, `Value` về default, `Changed` bắn với default |
| Listener của `Changed` throw | log exception, listener sau vẫn nhận |

- [ ] **Step 3: Commit** — `feat(sdk): add typed prefs (Prefs<T> + 4 specializations)`

---

### Task 5: Demo + nghiệm thu chơi thử

**Files:**
- Create: `Assets/Horcrux/Runtime/Implementations/Foundations/Persistence/Demo/DemoSaveUnit.cs`
- Create: `Assets/Horcrux/Runtime/Implementations/Foundations/Persistence/Demo/DemoSaveDriver.cs`
- Scene demo (Editor setup dưới) — không commit vào SDK nếu project có quy ước riêng về scene demo.

**Interfaces:**
- Consumes: `SaveUnit<TModel>` · `ISaveService` (Task 1) · `SaveRegistry` (Task 3) · `PrefsBool` (Task 4).
- Produces: chỉ demo — không hệ nào phụ thuộc.

- [ ] **Step 1: `DemoSaveUnit.cs`**

```csharp
using System.Collections.Generic;
using Horcrux.Runtime.Abstractions.Persistence;

namespace Horcrux.Runtime.Implementations.Persistence.Demo
{
    /// <summary>Model demo — data thuần, field public. Game thật: mỗi cụm dữ liệu một model thế này.</summary>
    public sealed class DemoSaveModel
    {
        public int coins;
        public int currentLevel = 1;
        public List<string> unlockedSkins = new();
    }

    /// <summary>Unit demo — toàn bộ chi phí thêm một cụm save mới là chừng này: model + const key + class mỏng.</summary>
    public sealed class DemoSaveUnit : SaveUnit<DemoSaveModel>
    {
        public const string SaveKey = "demo_progress";   // hợp đồng wire format — const, không suy từ tên type

        public DemoSaveUnit() : base(SaveKey) { }
    }
}
```

- [ ] **Step 2: `DemoSaveDriver.cs`**

```csharp
using Horcrux.Runtime.Abstractions.Persistence;
using UnityEngine;

namespace Horcrux.Runtime.Implementations.Persistence.Demo
{
    /// <summary>Driver nghiệm thu: mutate model, xem load/autosave/flush qua log. Không dùng trong game thật.</summary>
    public sealed class DemoSaveDriver : MonoBehaviour
    {
        private readonly DemoSaveUnit unit = new();
        private readonly PrefsBool hasRated = new("demo_has_rated");

        private void Start()
        {
            unit.Changed += LogState;
            ISaveService.Service.Register(unit);         // load ngay trong Register — LogState của Changed
                                                         // in giá trị của phiên trước, không phải default
            LogState();
        }

        private void OnDestroy() => unit.Changed -= LogState;

        [ContextMenu("Add 10 coins")]
        private void AddCoins()
        {
            unit.Value.coins += 10;
            unit.MarkDirty();                            // serialize KHÔNG ở đây — đợi nhịp autosave/flush
        }

        [ContextMenu("Level up")]
        private void LevelUp()
        {
            unit.Value.currentLevel++;
            unit.MarkDirty();
        }

        [ContextMenu("Flush ngay")]
        private void Flush() => ISaveService.Service.FlushAll();

        [ContextMenu("Register trùng key (kiểm log)")]
        private void RegisterDuplicate() => ISaveService.Service.Register(new DemoSaveUnit());

        [ContextMenu("Toggle prefs 'demo_has_rated'")]
        private void ToggleRated()
        {
            hasRated.Value = !hasRated.Value;
            Debug.Log($"[DemoSave] hasRated = {hasRated.Value}", this);
        }

        [ContextMenu("Mở thư mục save")]
        private void OpenSaveFolder()
            => Application.OpenURL("file://" + Application.persistentDataPath + "/Saves");

        private void LogState()
            => Debug.Log($"[DemoSave] coins={unit.Value.coins} level={unit.Value.currentLevel} " +
                         $"dirty={unit.IsDirty}", this);
    }
}
```

- [ ] **Step 3: Editor setup scene demo** (bước thật):

1. Scene mới `PersistenceDemo` → GameObject `[Save]` + `SaveRegistry` (interval 5, serializer `NewtonsoftJson`).
2. GameObject `[Demo]` + `DemoSaveDriver`.

- [ ] **Step 4: Kịch bản chơi thử** (nghiệm thu này cần Play mode, developer chạy):

| Mục | Nội dung |
|---|---|
| Vào đâu | Scene `PersistenceDemo`, bấm Play |
| Làm gì | ① chuột phải driver → "Add 10 coins" ×3, chờ quá 5 giây, "Mở thư mục save" · ② dừng Play → Play lại · ③ dừng Play, mở `demo_progress.sav` bằng notepad — thấy JSON đọc được; gõ rác vào giữa, lưu → Play · ④ "Register trùng key (kiểm log)" · ⑤ "Add 10 coins" rồi bấm nút Pause của Editor · ⑥ "Toggle prefs" → dừng → Play lại · ⑦ đổi `serializerKind` sang `MemoryPack` → Play |
| Nhìn cái gì | ① log `dirty=true` ngay khi add, file `demo_progress.sav` xuất hiện sau ≤5s · ② log đầu tiên đã là `coins=30` (không phải 0) — load ngay trong Register · ③ vẫn vào demo, LogError nêu `demo_progress`, `coins=0` default · ④ LogError "Key 'demo_progress' đã có unit đăng ký" · ⑤ file cập nhật ngay lúc pause (flush), không cần chờ chu kỳ · ⑥ `hasRated` giữ giá trị qua phiên · ⑦ LogError đọc fail (file JSON cũ không phải MemoryPack) + vào game bằng default — và ở đây thấy vì sao "chọn serializer một lần trước khi ship" |
| Khác trước ra sao | So `GameDataManager` color-loop: thêm cụm save mới ở đây = 1 model + 1 class mỏng, không đụng SDK và không đụng cụm khác; corrupt không crash boot |
| Dấu hiệu hỏng | coins về 0 sau restart bình thường (mất save — hỏng load hoặc flush) · `dirty=true` còn mãi sau khi file đã ghi (ClearDirty không chạy) · corrupt file làm exception đỏ không bắt / không vào được demo (§0.2 vỡ) · file `.tmp` còn sót lại sau flush thành công (WriteAtomic không hoàn tất) |

- [ ] **Step 5: Commit** — `feat(sdk): add persistence demo + acceptance scene`

> `SystemPlan.md` đã được cập nhật cùng lần viết plan này (bảng "Hệ đã có plan chi tiết", 📄 hàng 2, ghi chú plan ở §0.3 và §A.3) — không còn việc tài liệu nào trong task.

---

## Ghi chú thực thi

- **Nghiệm thu cuối = kịch bản Task 5 Step 4** — map với 4 mục Nghiệm thu của SystemPlan mục 2: thêm-unit-không-sửa-SDK (Step 1 Task 5 là bằng chứng sống), kill-app-mất-≤-1-chu-kỳ (ca ① ⑤), corrupt-vẫn-vào-game (ca ③ ⑦), no-op-phải-lộ (ca ④ + LogWarning registry rỗng). Round-trip đổi serializer chạy qua ca ② với từng serializer: giá trị sống qua đĩa và trở lại đúng.
- **Sau khi implement xong:** viết `Persistence.md` (tài liệu thiết kế §5.1) cạnh `Implementations/Foundations/Persistence/` — điều kiện ⑤ của "Xong" (SystemPlan §0.6). Chuyển các dòng "đã sai một lần" (§0.2–0.4 của plan này) vào mục quyết định thiết kế của nó.
- **Hệ dùng tiếp:** Audio §8 (`IAudioSettings` lưu volume), Haptics §9 (`IHapticSettings`), Economy §14 (coin/lives), Rating §18 ("đã rate chưa" — `PrefsBool`), LiveOps §20 (tiến độ event — unit riêng mỗi module). Game wire một `BootStep` "Save" nếu cần flush có thứ tự với hệ khác (§0.1).
- **Mở rộng sau** (đều additive, không đổi chữ ký đang có): crypto = decorator `ISerializer` (class mới bọc serializer thật) · cloud = interface `ICloudSyncable` riêng theo ISP + snapshot dictionary trên registry + merge rule version/level/timestamp (chống "thiết bị mới data rỗng đè thiết bị cũ") · migration = field `version` trong model + hook `OnDeserialized` · `PrefsDateTime`/`ForceRefresh`/`syncToServer` về cùng đợt cloud · load async = overload `RegisterAsync` khi đo được boot chậm.
