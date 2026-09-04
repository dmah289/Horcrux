# Persistence V2 Implementation Plan — file trên đĩa + `ISerializer`

> **Loại tài liệu:** Plan — developer tự code lại để nắm logic. `.md` thiết kế + `.html` viết **sau** khi có source.
>
> **For agentic workers:** REQUIRED SUB-SKILL: superpowers:subagent-driven-development hoặc superpowers:executing-plans. Steps dùng checkbox (`- [ ]`).

**Goal:** Lưu tiến độ người chơi **có kiểu**, **không mất khi app bị kill** (tối đa một chu kỳ autosave), **không god-blob**: nhiều `SaveUnit` độc lập đăng ký vào một `SaveRegistry` — registry lo load/autosave/flush/atomic-write, unit chỉ giữ model + dirty + on-change; format serialize thay được qua `ISerializer`; giá trị lẻ đi đường `Prefs<T>`.

Hệ Persistence có **hai bản plan thay thế nhau**, chọn một: bản **V1** (`PersistenceV1.md`) lưu vào PlayerPrefs, bản **V2** này lưu ra file. So sánh và bảng nâng cấp ở mục "Chọn bản này hay bản V1" bên dưới.

**Architecture:** 3 tầng, tổng **10 file** (4 contract + 3 impl + 1 typed-prefs + 2 demo).

```
Contract  (ISaveUnit, SaveUnit<TModel>,       key + dirty + on-change · format thay được ·
           ISerializer, ISaveService)          cửa Register/FlushAll
Registry  (SaveRegistry + 2 ISerializer impl)  load lúc Register · autosave chu kỳ · flush pause/quit ·
                                               atomic write · corrupt → default + log
Game      (các unit cụ thể + Prefs<T> lẻ)     model + const key, đăng ký lúc boot
```

**Tech Stack:** C#, UniTask, `Sisus.Init` (`[Service]`), `System.Buffers`, Newtonsoft.Json (package `com.unity.nuget.newtonsoft-json` — project đang có bản `3.2.2`; DLL precompiled, mọi asmdef tự reference), PlayerPrefs (`Prefs<T>` và bước chuyển dữ liệu từ bản V1), MemoryPack (tuỳ chọn qua versionDefines). **Yêu cầu** Api Compatibility Level **.NET Standard 2.1** — `File.Move(src, dest, overwrite)` chỉ có từ mức này (§0.2); project đang đặt đúng mức đó. **Không** Addressables, không toán.

## Global Constraints

| Ràng buộc | Giá trị |
|---|---|
| Namespace | `Horcrux.Runtime.Abstractions.Persistence` · `Horcrux.Runtime.Implementations.Persistence` |
| Ngôn ngữ trong code | Comment và XML doc viết **tiếng Anh**, khớp với toàn bộ `.cs` đang có trong SDK |
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

## Chọn bản này hay bản V1

Hai bản là **hai lựa chọn thay thế nhau**, không chạy cùng lúc trong một build: chúng khai cùng tên `ISaveUnit`, `SaveUnit<TModel>`, `ISaveService`, `SaveRegistry` nên đặt cạnh nhau là đụng tên.

| | **V1 — `PersistenceV1.md`** | **V2 — bản này** |
|---|---|---|
| Chỗ lưu model | PlayerPrefs, một entry một unit, giá trị là chuỗi JSON | file `persistentDataPath/Saves/<key>.sav`, một file một unit |
| Format | Newtonsoft JSON, cố định | thay được qua `ISerializer` — Newtonsoft JSON hoặc MemoryPack |
| Số file | 7 | 10 |
| Đụng asmdef | không | có (reference `MemoryPack` + `versionDefines`) |
| Bán kính thiệt hại khi hỏng | kho prefs hỏng là mất **mọi** khoá của game | file hỏng chỉ mất **một** unit |
| Chi phí mỗi lượt flush | ghi lại toàn bộ kho prefs | ghi lại từng file của unit dirty |
| Chống đứt giữa lúc ghi | không có đường can thiệp — nền tảng lo | ghi ra `.tmp` rồi đổi tên (§0.2) |

**Chọn V2 khi:** payload vượt vài chục KB · cần một unit hỏng không kéo theo unit khác · cần MemoryPack cho file nhỏ và nhanh.

### Lên V2 từ V1

Ranh giới giữ bằng mọi giá: **phần API mà code game chạm tới giống hệt nhau ở hai bản.**

| Thứ game chạm | Chữ ký, giống nhau ở V1 và V2 |
|---|---|
| `SaveUnit<TModel>` | `string Key { get; }` · `TModel Value { get; }` · `bool IsDirty { get; }` · `void MarkDirty()` · `event Action Changed` · `protected SaveUnit(string key)` |
| `ISaveService` | `void Register(ISaveUnit unit)` · `void FlushAll()` · truy cập tĩnh `ISaveService.Service` |
| `Prefs<T>` và 4 bản chuyên biệt | `string Key` · `T Value { get; set; }` · `bool HasValue` · `void Delete()` · `event Action<T> Changed` |

Ba method wire-format (`WritePayload`, `ReadPayload`, `ClearDirty`) đổi chữ ký giữa hai bản, nhưng chúng được cài **explicit interface** trên lớp base nên code game không nhìn thấy và không gọi được. Bảng đổi file cụ thể:

| File | Khi lên V2 |
|---|---|
| `Abstractions/…/ISaveUnit.cs` | **sửa** — hai method payload đổi từ `string` sang `ISerializer` + `IBufferWriter<byte>` / `ReadOnlySpan<byte>` |
| `Abstractions/…/SaveUnit.cs` | **sửa** — thân hai method đó gọi serializer thay vì `JsonConvert` |
| `Abstractions/…/ISaveService.cs` | **không đụng** |
| `Implementations/…/Prefs.cs` | **không đụng** |
| `Implementations/…/SaveRegistry.cs` | **thay nội dung** — cùng đường dẫn, cùng tên class, nên GUID không đổi: component trên scene giữ nguyên và giá trị `autosaveIntervalSeconds` đã set không mất |
| `Abstractions/…/ISerializer.cs` · `Implementations/…/NewtonsoftJsonSerializer.cs` · `MemoryPackSaveSerializer.cs` | **thêm mới** |
| `com.horcrux.runtime.asmdef` | **sửa** — thêm reference `MemoryPack` + `versionDefines` (Task 2 Step 3) |
| `Demo/DemoSaveDriver.cs` | **sửa** — hai ContextMenu của bản V1 ("In payload đang lưu", "Ghi rác vào payload") đổi thành "Mở thư mục save" |
| Code game: model, const key, `MarkDirty`, `Changed`, `Register`, `Prefs<T>` | **không đụng một dòng** |
| Inspector của `[Save]` | thêm một ô `serializerKind` |

**Dữ liệu của người chơi đang ở bản V1 không mất:** registry của bản này có một bước chuyển một chiều, chạy đúng một lần cho mỗi unit lúc load — chi tiết ở Task 3.

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
| Ghi | file chính chỉ được thay bằng **một bản đã ghi trọn** | ghi ra `key.sav.tmp` → `Flush(flushToDisk: true)` → `File.Move(tmp, path, overwrite: true)` |
| Đọc | file hỏng → **model default + log, không throw** | try/catch quanh đọc + deserialize; save hỏng không được chặn người chơi vào game |

*Đã sai một lần — color-loop `PlayerSaveLoadService`:* `SaveToDevice` gọi `File.WriteAllBytes` thẳng vào file chính, và `Load()` không try/catch quanh `Deserialize` — một file cụt là exception **mỗi lần boot**, save thành "brick" vĩnh viễn.

**Đầu ghi có hai tầng đảm bảo, và chúng chống hai thứ khác nhau — đừng lẫn:**

| Tầng | Câu lệnh | Chống được | Vì sao thiếu nó thì hở |
|---|---|---|---|
| Bytes thật sự tới đĩa | `stream.Flush(flushToDisk: true)` | mất điện, tụt pin, kernel panic ngay sau lần ghi | Đóng `FileStream` chỉ đẩy bytes sang **hệ điều hành**; chúng còn nằm trong page cache. Process chết thì không sao — OS vẫn ghi nốt. Nhưng máy mất điện thì file đổi tên xong vẫn có thể rỗng hoặc cụt |
| Thay file nguyên tử | `File.Move(tmp, path, overwrite: true)` | app bị kill giữa lúc ghi | Người đọc thấy **hoặc** bản cũ **hoặc** bản mới, không bao giờ thấy bản đang ghi dở. Đây là bảo đảm của `rename()` ở tầng filesystem, không phải của thư viện |

**Vì sao là `File.Move(..., overwrite: true)` chứ không phải `File.Replace`:** `File.Replace` sinh ra cho ngữ nghĩa Windows — nó còn lo giữ ACL và tạo file backup, và trên nền Unix nó được cài đặt lại bằng đường khác. `File.Move` ba tham số ánh xạ thẳng xuống **một** lời gọi hệ thống: `rename()` trên POSIX (Android dùng ext4 hoặc f2fs, iOS dùng APFS — cả hai đều bảo đảm nguyên tử trong cùng một filesystem) và `MoveFileEx` với cờ thay-thế trên Windows. Một đường code duy nhất cho Editor và cả hai máy, và nhánh `if (File.Exists(path))` biến mất vì overload này tự xử cả hai trường hợp.

**Giá phải trả của `fsync`:** một lần mỗi unit dirty mỗi chu kỳ flush — vài mili giây với file cỡ KB, ở nhịp 5 giây, không phải hot path. Máy đang chịu tải I/O nặng thì một lần `fsync` có thể lên vài chục mili giây; nó chạy trên main thread trong `FlushAll`, nên nếu về sau đo được giật ở đúng nhịp autosave thì đây là chỗ nhìn đầu tiên.

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
| 3 | `Implementations/Foundations/Persistence/SaveRegistry.cs` | registry + bước nhập dữ liệu một chiều từ bản V1 |
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
    /// <summary>The save format (JSON, MemoryPack…). The registry holds it, the unit never sees it — changing format leaves game logic alone.</summary>
    public interface ISerializer
    {
        /// <summary>Writes <paramref name="value"/> into the registry's reused buffer — no array allocated per save.</summary>
        void Serialize<T>(in T value, IBufferWriter<byte> writer);

        /// <summary>Rebuilds the model from bytes. Broken bytes may throw — the registry catches and keeps the default model.</summary>
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
    /// <summary>One independent block of save data. Game code derives <see cref="SaveUnit{TModel}"/> rather than implementing this.</summary>
    public interface ISaveUnit
    {
        /// <summary>Wire-format key — an explicit const string, never derived from a type name.</summary>
        string Key { get; }

        /// <summary>Has changes not yet on disk. The game sets it via MarkDirty; only the registry clears it.</summary>
        bool IsDirty { get; }

        /// <summary>Registry calls this during a flush: serialize the current model into the shared buffer.</summary>
        void WritePayload(ISerializer serializer, IBufferWriter<byte> writer);

        /// <summary>Registry calls this once at Register when a file exists. Throwing leaves the default model in place.</summary>
        void ReadPayload(ISerializer serializer, ReadOnlySpan<byte> bytes);

        /// <summary>REGISTRY ONLY, after the write succeeded — the single place dirty is cleared.</summary>
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
    /// <summary>Base for every save unit: typed model, dirty flag, on-change event. A game unit declares only a model and a key.</summary>
    /// <remarks>
    /// Usage: mutate a field inside <see cref="Value"/>, then call <see cref="MarkDirty"/>. Serializing does NOT
    /// happen there — it is deferred to the registry's flush. The unit is usable the moment
    /// <c>ISaveService.Register</c> returns, because loading happens inside Register.
    /// </remarks>
    public abstract class SaveUnit<TModel> : ISaveUnit where TModel : class, new()
    {
        private readonly string key;

        /// <param name="key">Wire-format key — a const string owned by the unit; it names the file on disk.</param>
        protected SaveUnit(string key)
        {
            this.key = key;
            Value = new TModel();
        }

        public string Key => key;

        /// <summary>The current model — never null; with no file on disk yet it is <c>new TModel()</c>.</summary>
        public TModel Value { get; private set; }

        public bool IsDirty { get; private set; }

        /// <summary>Fires whenever Value changes: after MarkDirty, and after a load. A throwing listener cannot kill the unit.</summary>
        public event Action Changed;

        /// <summary>Call after mutating the model. Sets a flag and fires Changed — cheap enough for every interaction.</summary>
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
            IsDirty = false;                             // just read from disk — disk and memory agree
            RaiseChanged();
        }

        void ISaveUnit.ClearDirty() => IsDirty = false;

        private void RaiseChanged()
        {
            var handlers = Changed;
            if (handlers == null) return;

            // Isolate a throwing listener; allocates per interaction, not per frame.
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
    /// <summary>The save registry — a REQUIRED service: every game stores progress, so a missing one is a setup error.</summary>
    public interface ISaveService : IService<ISaveService>
    {
        /// <summary>Registers the unit and LOADS it immediately — the unit is usable as soon as this returns.</summary>
        /// <param name="unit">A key already taken: logs an error and drops the new unit.</param>
        void Register(ISaveUnit unit);

        /// <summary>Stores every dirty unit now. The registry calls this on autosave, pause and quit —
        /// the game only calls it to close the books early, such as right after a successful purchase.</summary>
        void FlushAll();
    }
}
```

- [ ] **Step 5: Kiểm chứng** — compile sạch; chưa có hành vi chạy được (contract thuần) — hành vi kiểm ở Task 3 và 5.

- [ ] **Step 6: Commit** — `feat(sdk): add persistence contracts (save-unit, serializer, save-service)`

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
    /// <summary>The default serializer — Newtonsoft JSON, so a save file stays readable while debugging.</summary>
    /// <remarks>The model must be plain data: numbers, strings, List, Dictionary, with public fields or
    /// properties and no attributes needed. Never put a UnityEngine.Object or a Unity struct in it — engine
    /// references do not belong in a save, and Vector3.normalized makes the serializer recurse forever.</remarks>
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
    /// <summary>The MemoryPack serializer — binary, fast, small files; the model must be [MemoryPackable] partial.</summary>
    /// <remarks>Compiles only when the project has com.cysharp.memorypack; the define is declared in the asmdef.</remarks>
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
- Consumes: `ISaveUnit` · `ISerializer` · `ISaveService` (Task 1) · 2 serializer (Task 2) · `[Service]` của Sisus.Init · UniTask · `PlayerPrefs` và `System.Text` (chỉ cho bước nhập dữ liệu từ bản V1).
- Produces: `SaveRegistry : MonoBehaviour, ISaveService` — `Register(ISaveUnit)` · `FlushAll()` · `const string LegacyPrefsKeyPrefix`; cấu hình Inspector: `autosaveIntervalSeconds` · `serializerKind`.

**Quyết định thiết kế:**

| Quyết định | Lý do |
|---|---|
| `Register` **load ngay** (sync) | Không tồn tại trạng thái "đã đăng ký nhưng chưa load" — mọi code sau dòng Register đọc được giá trị thật, không cần phối hợp thứ tự thêm. File cỡ KB lúc boot, sync là bản đơn giản nhất còn đúng; async thêm sau nếu đo được chậm |
| `EnsureReady()` idempotent, gọi từ cả `Awake` lẫn `Register` | Unity không đảm bảo thứ tự `Awake` giữa registry và hệ game đăng ký sớm — chính bài toán Bootstrap; Persistence là Foundation zero-dep nên phải tự đứng được, không dựa runner |
| Autosave = UniTask loop + `destroyCancellationToken`, `DelayType.Realtime` | Loop chết theo GameObject (không `while(true)` sống sau destroy — cạm bẫy SystemPlan mục 2); Realtime vì autosave không được ngừng khi game pause bằng `timeScale = 0`. Không dùng Ticker — Foundation zero-dep, và nhịp giây không cần nguồn tick trung tâm |
| Autosave tick gọi thẳng `FlushAll` | Cửa hẹp là thân chung: autosave, pause, quit, gọi tay — cùng MỘT thân flush, không thể lệch nhau |
| Flush ở magic method của chính registry | §0.1 — lưới an toàn khi project không dùng Bootstrap; đây là MonoBehaviour duy nhất của hệ nên magic method sống đúng một chỗ. Flush có thứ tự (khi cần) đi đường `BootStep` phía game |
| Ghi fail → **giữ dirty** + log, chu kỳ sau thử lại | §0.3 — `ClearDirty` nằm sau `WriteAtomic` trong cùng `try` |
| `WriteAtomic` dùng `File.Move(tmp, path, overwrite: true)`, không `File.Replace` | §0.2 — overload ba tham số là **một** lời gọi `rename()` trên POSIX và `MoveFileEx` có cờ thay-thế trên Windows, nên nguyên tử ở cả Editor lẫn Android lẫn iOS bằng cùng một đường code; nhánh `File.Exists` biến mất. `File.Replace` mang ngữ nghĩa Windows (giữ ACL, sinh file backup) và trên Unix đi đường khác. Overload này cần **.NET Standard 2.1** — project đang đặt đúng mức đó |
| `WriteAtomic` gọi `stream.Flush(flushToDisk: true)` trước khi đổi tên | §0.2 — đổi tên nguyên tử chỉ đảm bảo "cũ hoặc mới", không đảm bảo bytes đã rời page cache. Không có `fsync` thì mất điện ngay sau lần ghi cho ra file đã đổi tên nhưng rỗng. Giá: một `fsync` mỗi unit dirty mỗi chu kỳ, vài mili giây với file cỡ KB |
| Key trùng lúc Register → log error + bỏ unit mới | Hai unit một key là unit sau đè file unit trước (cạm bẫy SystemPlan mục 2); bất biến ④ — phải kêu lên. So sánh tuyến tính `List` đủ: số unit cỡ chục, chạy lúc boot |
| Flush khi 0 unit → `LogWarning` một lần | Bất biến ④ — khung của color-loop chết âm thầm vì autosave no-op không log (§0.4) |
| Buffer `ArrayBufferWriter<byte>` field, `Clear()` mỗi unit | SystemPlan mục 2 "ghi vào buffer dùng chung" — grow-only, không alloc theo chu kỳ |
| Enum `ESaveSerializer` nested private trong registry | Chỉ registry cần nó (config Inspector) — không phình namespace public khi chưa có người dùng thứ hai |
| Nhập dữ liệu từ bản V1 đi qua **chính `ReadPayload`**, không thêm method nào vào `ISaveUnit` | Chuỗi JSON của bản V1 chuyển thành bytes UTF-8 rồi đưa qua đúng cửa đọc đang có, với một `NewtonsoftJsonSerializer` dựng tại chỗ. Cửa hẹp là thân chung của cửa rộng: không có đường đọc thứ hai để lệch, và khi xoá bước nhập thì contract không còn vết nào |
| Bước nhập chạy **chỉ khi chưa có file**, và **không** có ô bật/tắt ở Inspector | Có file nghĩa là đã nhập xong (hoặc chưa từng ở bản V1) — không cần cờ trạng thái riêng, chính sự tồn tại của file là cờ. Cài mới thì `PlayerPrefs.HasKey` trả false, chi phí bằng một lần tra khoá mỗi unit lúc boot. Thêm một ô Inspector là thêm một cấu hình mà không ai có lý do để tắt |
| Bước nhập luôn đọc bằng **Newtonsoft**, kể cả khi `serializerKind` là MemoryPack | Bản V1 chỉ ghi được JSON. Đọc payload đó bằng serializer đang cấu hình sẽ là "đọc format A bằng format B" — corrupt về mặt logic, và ở đây nó có nghĩa là mất save của người chơi cũ. Ghi ra thì dùng format đang cấu hình |
| Xoá khoá PlayerPrefs **sau** khi `WriteAtomic` xong, không phải trước | Cùng luật với dirty ở §0.3: không bỏ nguồn cho tới khi đích đã lành. Ghi hỏng thì nhảy vào `catch`, khoá cũ còn nguyên, lần boot sau thử lại |
| `LegacyPrefsKeyPrefix` là **bản sao** của tiền tố khai trong plan V1, và để `public` | Hai bản không cùng tồn tại trong một build nên không có nguồn chung để suy ra — đây là hợp đồng wire format với dữ liệu đã nằm trên máy người chơi: đổi một bên mà quên bên kia là mọi save cũ thành mồ côi, không có gì báo. `public` vì có caller thứ hai thật: ContextMenu dựng dữ liệu bản V1 ở Task 5, và để nó ở một chỗ thì hai nơi không thể viết lệch |

**Editor setup — bước thật:**

1. Scene entry của game: tạo GameObject `[Save]` → add `SaveRegistry`.
2. Inspector: đặt `autosaveIntervalSeconds` (mặc định 5) và `serializerKind` theo game — **chọn serializer một lần trước khi ship**.

- [ ] **Step 1: `SaveRegistry.cs`**

```csharp
using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using Cysharp.Threading.Tasks;
using Horcrux.Runtime.Abstractions.Persistence;
using Sisus.Init;
using UnityEngine;

namespace Horcrux.Runtime.Implementations.Persistence
{
    /// <summary>Owns the save lifecycle: loads at Register, autosaves on a timer, flushes on pause and quit, writes atomically.</summary>
    /// <remarks>
    /// The contract is "lose at most ONE autosave cycle": on Android a swipe-kill never runs OnApplicationQuit,
    /// so pause is the last signal we trust. Flushing from this component's own magic methods is a safety net
    /// with NO ordering guarantee. A game that must flush after another system writes during its own pause hook
    /// should call <see cref="FlushAll"/> from a BootStep instead, riding the runner's reverse fan-out.
    /// Flushing twice is harmless — the second pass finds nothing dirty.
    /// </remarks>
    [Service(typeof(ISaveService), FindFromScene = true)]
    public sealed class SaveRegistry : MonoBehaviour, ISaveService
    {
        /// <summary>Prefix the PlayerPrefs-backed version put in front of every unit key. Goes away with the import below.</summary>
        public const string LegacyPrefsKeyPrefix = "save.";

        [SerializeField, Min(1f), Tooltip("Autosave period in seconds. A killed app loses at most one of these.")]
        private float autosaveIntervalSeconds = 5f;

        [SerializeField, Tooltip("Save format. Pick it ONCE before shipping — changing it after ship needs a migration.")]
        private ESaveSerializer serializerKind = ESaveSerializer.NewtonsoftJson;

        private enum ESaveSerializer { NewtonsoftJson = 0, MemoryPack = 1 }

        private readonly List<ISaveUnit> units = new();
        private readonly ArrayBufferWriter<byte> payloadBuffer = new();  // shared, grow-only
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

        // Unordered safety net — an ordered flush belongs to a game-side BootStep (see class remarks).
        private void OnApplicationPause(bool isPaused)
        {
            if (isPaused) FlushAll();
        }

        private void OnApplicationQuit() => FlushAll();

        public void Register(ISaveUnit unit)
        {
            if (unit == null)
            {
                Debug.LogError("[Save] Register(null) — forgot to construct the unit?", this);
                return;
            }

            EnsureReady();                               // a game system may Register before this Awake runs
            foreach (var existing in units)
            {
                if (existing.Key == unit.Key)
                {
                    Debug.LogError(
                        $"[Save] Key '{unit.Key}' already has a unit registered — dropping the new one. " +
                        "Two units on one key means the second overwrites the first.", this);
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
                    warnedEmptyOnce = true;              // a save system must never no-op in silence
                    Debug.LogWarning("[Save] Flush with no unit registered — forgot to Register?", this);
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
                    unit.ClearDirty();                   // clears ONLY after the write landed
                }
                catch (Exception e)
                {
                    Debug.LogError($"[Save] Writing unit '{unit.Key}' failed — it stays dirty and retries next cycle.", this);
                    Debug.LogException(e, this);
                }
            }
        }

        private async UniTaskVoid AutosaveLoopAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                // Realtime: autosave must keep ticking while the game is paused with timeScale = 0.
                await UniTask.Delay(TimeSpan.FromSeconds(autosaveIntervalSeconds), DelayType.Realtime,
                    cancellationToken: cancellationToken).SuppressCancellationThrow();
                if (cancellationToken.IsCancellationRequested) return;
                FlushAll();
            }
        }

        private void LoadUnit(ISaveUnit unit)
        {
            string path = PathForKey(unit.Key);
            if (!File.Exists(path))
            {
                ImportFromPlayerPrefs(unit);             // still on the PlayerPrefs-backed version? bring it over
                return;
            }

            try
            {
                byte[] bytes = File.ReadAllBytes(path);
                if (bytes.Length == 0) return;
                unit.ReadPayload(serializer, bytes);
            }
            catch (Exception e)
            {
                // A broken save must never stop the player from entering the game.
                Debug.LogError($"[Save] Reading unit '{unit.Key}' failed — using the default model; " +
                               "the file is overwritten on the next save.", this);
                Debug.LogException(e, this);
            }
        }

        // One-way bridge from the PlayerPrefs-backed version: runs at most once per unit, only when no file
        // exists yet. Delete this method and its single call site once no player is left on that version.
        private void ImportFromPlayerPrefs(ISaveUnit unit)
        {
            string legacyKey = LegacyPrefsKeyPrefix + unit.Key;
            if (!PlayerPrefs.HasKey(legacyKey)) return;  // fresh install — nothing to bring over

            try
            {
                string payload = PlayerPrefs.GetString(legacyKey);
                if (string.IsNullOrEmpty(payload)) return;

                // That payload is always JSON, even when this registry is configured for MemoryPack.
                unit.ReadPayload(new NewtonsoftJsonSerializer(), Encoding.UTF8.GetBytes(payload));

                payloadBuffer.Clear();
                unit.WritePayload(serializer, payloadBuffer);   // write it back out in the configured format
                WriteAtomic(PathForKey(unit.Key), payloadBuffer.WrittenSpan);
                unit.ClearDirty();

                PlayerPrefs.DeleteKey(legacyKey);        // drop the source ONLY once the file is on disk
                PlayerPrefs.Save();
                Debug.Log($"[Save] Imported unit '{unit.Key}' from PlayerPrefs into a file.", this);
            }
            catch (Exception e)
            {
                // Keep the PlayerPrefs entry so the next boot retries; this session runs on the default model.
                Debug.LogError($"[Save] Importing unit '{unit.Key}' from PlayerPrefs failed — " +
                               "the old entry is kept and the next boot tries again.", this);
                Debug.LogException(e, this);
            }
        }

        // Never write straight over the live file — a kill mid-write leaves a truncated one.
        private static void WriteAtomic(string path, ReadOnlySpan<byte> payload)
        {
            string tempPath = path + ".tmp";
            using (var stream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                stream.Write(payload);
                stream.Flush(flushToDisk: true);         // fsync — closing alone only hands the bytes to the OS
            }

            // One rename() on POSIX, MoveFileEx with replace on Windows: a reader sees the old file or the
            // new one, never a half-written one. Same single call whether or not the target already exists.
            File.Move(tempPath, path, overwrite: true);
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
                    Debug.LogError("[Save] MemoryPack is selected but the project has no " +
                                   "com.cysharp.memorypack package — falling back to Newtonsoft JSON.", this);
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
| Mutate + `MarkDirty` → chờ hết 1 chu kỳ autosave | file `Saves/<key>.sav` xuất hiện, `IsDirty == false`, thư mục **không còn** file `.tmp` nào |
| Register lại đúng key đó (unit thứ hai) | `LogError` nêu key, unit mới không được nạp, unit cũ vẫn hoạt động |
| File `<key>.sav` bị sửa thành rác trước khi Play | vào game bình thường, `LogError` nêu key, `Value` là default |
| `FlushAll` khi không có gì dirty | không I/O, không log |
| Flush khi 0 unit đăng ký | `LogWarning` đúng một lần cho cả phiên |
| Chọn `MemoryPack` ở project không có package | `LogError` + hoạt động tiếp bằng Newtonsoft JSON |
| Destroy registry giữa phiên | autosave loop dừng (token), không exception |
| Có khoá PlayerPrefs `save.<key>` của bản V1, chưa có file `.sav` | `Log` báo đã nhập, `Value` mang đúng giá trị của bản V1, file `.sav` xuất hiện ngay lúc `Register`, khoá PlayerPrefs biến mất |
| Chạy lại lần thứ hai sau khi đã nhập | không log gì thêm, đọc thẳng từ file — bước nhập không chạy lại |
| Có **cả** khoá PlayerPrefs lẫn file `.sav` | file thắng, khoá PlayerPrefs không bị đọc và cũng không bị xoá |
| Khoá PlayerPrefs chứa rác, chưa có file | `LogError` nêu đúng key, `Value` là default, **khoá cũ vẫn còn** (chưa xoá vì chưa ghi được gì) |
| Cài mới hoàn toàn (chưa từng chạy bản V1) | không log gì, chi phí đúng bằng một `PlayerPrefs.HasKey` mỗi unit |
| `serializerKind = MemoryPack`, có khoá PlayerPrefs của bản V1 | nhập thành công: đọc bằng JSON, file `.sav` ghi ra bằng MemoryPack |

- [ ] **Step 3: Commit** — `feat(sdk): add SaveRegistry (load-on-register, autosave, atomic write, prefs import)`

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
    /// <summary>A single small value in PlayerPrefs, too small to deserve a model ("has rated"). Big models belong to a SaveUnit.</summary>
    /// <remarks>Reads are cached after the first one, so reading Value in Update is not a performance bug.
    /// Setting writes straight to PlayerPrefs without forcing PlayerPrefs.Save() — the platform persists on
    /// pause, and pushing the whole store to disk on every set would pay a per-interaction price.</remarks>
    public abstract class Prefs<T>
    {
        private readonly T defaultValue;
        private T cachedValue;
        private bool isCached;

        /// <param name="key">The PlayerPrefs key — a const string; it is a wire-format contract.</param>
        /// <param name="defaultValue">Returned until something is set.</param>
        protected Prefs(string key, T defaultValue)
        {
            Key = key;
            this.defaultValue = defaultValue;
        }

        public string Key { get; }

        public bool HasValue => PlayerPrefs.HasKey(Key);

        /// <summary>Fires after every set and after <see cref="Delete"/> — UI can bind straight to it.</summary>
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

        /// <summary>Reads PlayerPrefs directly. The base calls it only when the key exists, and only once per set or Delete.</summary>
        protected abstract T Read();

        protected abstract void Write(T value);

        private void RaiseChanged(T value)
        {
            var handlers = Changed;
            if (handlers == null) return;

            // Isolate a throwing listener; allocates per interaction, not per frame.
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

    /// <summary>Stored as int 0/1 — PlayerPrefs has no bool.</summary>
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
    /// <summary>Demo model — plain data, public fields. In a real game each block of save data looks like this.</summary>
    public sealed class DemoSaveModel
    {
        public int coins;
        public int currentLevel = 1;
        public List<string> unlockedSkins = new();
    }

    /// <summary>Demo unit — the whole cost of adding a new save block is this much: a model, a const key, a thin class.</summary>
    public sealed class DemoSaveUnit : SaveUnit<DemoSaveModel>
    {
        public const string SaveKey = "demo_progress";   // wire format — a const, never derived from the type name

        public DemoSaveUnit() : base(SaveKey) { }
    }
}
```

- [ ] **Step 2: `DemoSaveDriver.cs`**

```csharp
using System.IO;
using Horcrux.Runtime.Abstractions.Persistence;
using UnityEngine;

namespace Horcrux.Runtime.Implementations.Persistence.Demo
{
    /// <summary>Acceptance driver: mutate the model and watch load, autosave and flush through the log. Not for a real game.</summary>
    public sealed class DemoSaveDriver : MonoBehaviour
    {
        private readonly DemoSaveUnit unit = new();
        private readonly PrefsBool hasRated = new("demo_has_rated");

        private void Start()
        {
            unit.Changed += LogState;
            ISaveService.Service.Register(unit);         // loading happens inside Register, so the Changed
                                                         // callback prints last session's values, not defaults
            LogState();
        }

        private void OnDestroy() => unit.Changed -= LogState;

        [ContextMenu("Add 10 coins")]
        private void AddCoins()
        {
            unit.Value.coins += 10;
            unit.MarkDirty();                            // no serializing here — that waits for the flush
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

        /// <summary>Recreates the PlayerPrefs-backed layout so the one-way import can be exercised without shipping it first.</summary>
        [ContextMenu("Dựng dữ liệu bản V1 (kiểm bước nhập)")]
        private void SeedLegacyPayload()
        {
            PlayerPrefs.SetString(SaveRegistry.LegacyPrefsKeyPrefix + DemoSaveUnit.SaveKey,
                "{\"coins\":123,\"currentLevel\":7,\"unlockedSkins\":[\"đỏ\"]}");
            PlayerPrefs.Save();
            File.Delete(Path.Combine(Application.persistentDataPath, "Saves", DemoSaveUnit.SaveKey + ".sav"));
            Debug.Log("[DemoSave] Đã dựng dữ liệu bản V1 và xoá file — dừng Play rồi Play lại.", this);
        }

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
| Làm gì | ① chuột phải driver → "Add 10 coins" ×3, chờ quá 5 giây, "Mở thư mục save" · ② dừng Play → Play lại · ③ dừng Play, mở `demo_progress.sav` bằng notepad — thấy JSON đọc được; gõ rác vào giữa, lưu → Play · ④ "Register trùng key (kiểm log)" · ⑤ "Add 10 coins" rồi bấm nút Pause của Editor · ⑥ "Toggle prefs" → dừng → Play lại · ⑦ đổi `serializerKind` sang `MemoryPack` → Play · ⑧ đưa `serializerKind` về `NewtonsoftJson`, bấm "Dựng dữ liệu bản V1 (kiểm bước nhập)" → dừng Play → Play lại · ⑨ Play lại lần nữa ngay sau ca ⑧ |
| Nhìn cái gì | ① log `dirty=true` ngay khi add, file `demo_progress.sav` xuất hiện sau ≤5s · ② log đầu tiên đã là `coins=30` (không phải 0) — load ngay trong Register · ③ vẫn vào demo, LogError nêu `demo_progress`, `coins=0` default · ④ LogError "Key 'demo_progress' already has a unit registered" · ⑤ file cập nhật ngay lúc pause (flush), không cần chờ chu kỳ · ⑥ `hasRated` giữ giá trị qua phiên · ⑦ LogError đọc fail (file JSON cũ không phải MemoryPack) + vào game bằng default — và ở đây thấy vì sao "chọn serializer một lần trước khi ship" · ⑧ log "Imported unit 'demo_progress' from PlayerPrefs into a file", `coins=123 level=7`, file `demo_progress.sav` xuất hiện lại · ⑨ **không** có log nhập nữa, vẫn `coins=123` — bước nhập không chạy lần hai |
| Khác trước ra sao | So `GameDataManager` color-loop: thêm cụm save mới ở đây = 1 model + 1 class mỏng, không đụng SDK và không đụng cụm khác; corrupt không crash boot |
| Dấu hiệu hỏng | coins về 0 sau restart bình thường (mất save — hỏng load hoặc flush) · `dirty=true` còn mãi sau khi file đã ghi (ClearDirty không chạy) · corrupt file làm exception đỏ không bắt / không vào được demo (§0.2 vỡ) · file `.tmp` còn sót lại sau flush thành công (WriteAtomic không hoàn tất) · ca ⑧ `coins=0` thay vì `123` (bước nhập không chạy, dữ liệu người chơi cũ mất) · ca ⑨ log nhập hiện lại (khoá PlayerPrefs không được xoá — mỗi lần boot sẽ đè mất tiến độ mới bằng dữ liệu cũ) |

- [ ] **Step 5: Commit** — `feat(sdk): add persistence demo + acceptance scene`

> `SystemPlan.md` bảng "Hệ đã có plan chi tiết" (hàng 2 Persistence) trỏ sang **hai** file plan của hệ này — không còn việc tài liệu nào trong task.

---

## Ghi chú thực thi

- **Nghiệm thu cuối = kịch bản Task 5 Step 4** — map với 4 mục Nghiệm thu của SystemPlan mục 2: thêm-unit-không-sửa-SDK (Step 1 Task 5 là bằng chứng sống), kill-app-mất-≤-1-chu-kỳ (ca ① ⑤), corrupt-vẫn-vào-game (ca ③ ⑦), no-op-phải-lộ (ca ④ + LogWarning registry rỗng). Round-trip đổi serializer chạy qua ca ② với từng serializer: giá trị sống qua đĩa và trở lại đúng. Bước nhập dữ liệu từ bản V1 nghiệm thu bằng ca ⑧ và ⑨.
- **Xoá bước nhập dữ liệu khi nó hết việc.** `ImportFromPlayerPrefs`, hằng `LegacyPrefsKeyPrefix`, một dòng gọi trong `LoadUnit`, và ContextMenu "Dựng dữ liệu bản V1" — bốn chỗ, xoá trong một lần sửa. Thời điểm: khi không còn người chơi nào ở bản dùng PlayerPrefs, nhận biết bằng telemetry hoặc bằng việc bản đó chưa từng phát hành. Project chưa từng chạy bản V1 thì xoá được **ngay khi implement**, và khi đó Task 3 bớt một method.
- **Sau khi implement xong:** viết `Persistence.md` (tài liệu thiết kế §5.1) cạnh `Implementations/Foundations/Persistence/` — điều kiện ⑤ của "Xong" (SystemPlan §0.6). Chuyển các dòng "đã sai một lần" (§0.2–0.4 của plan này) vào mục quyết định thiết kế của nó.
- **Hệ dùng tiếp:** Audio §8 (`IAudioSettings` lưu volume), Haptics §9 (`IHapticSettings`), Economy §14 (coin/lives), Rating §18 ("đã rate chưa" — `PrefsBool`), LiveOps §20 (tiến độ event — unit riêng mỗi module). Game wire một `BootStep` "Save" nếu cần flush có thứ tự với hệ khác (§0.1).
- **Mở rộng sau** (đều additive, không đổi chữ ký đang có): crypto = decorator `ISerializer` (class mới bọc serializer thật) · cloud = interface `ICloudSyncable` riêng theo ISP + snapshot dictionary trên registry + merge rule version/level/timestamp (chống "thiết bị mới data rỗng đè thiết bị cũ") · migration = field `version` trong model + hook `OnDeserialized` · `PrefsDateTime`/`ForceRefresh`/`syncToServer` về cùng đợt cloud · load async = overload `RegisterAsync` khi đo được boot chậm.
