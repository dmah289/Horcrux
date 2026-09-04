# Persistence V1 Implementation Plan — PlayerPrefs + Newtonsoft JSON

> **Loại tài liệu:** Plan — developer tự code lại để nắm logic. `.md` thiết kế + `.html` viết **sau** khi có source.
>
> **For agentic workers:** REQUIRED SUB-SKILL: superpowers:subagent-driven-development hoặc superpowers:executing-plans. Steps dùng checkbox (`- [ ]`).

**Goal:** Lưu tiến độ người chơi **có kiểu**, **không mất khi app bị kill** (tối đa một chu kỳ autosave), **không god-blob** — bằng đúng hai thứ project đã có sẵn: **PlayerPrefs** và **Newtonsoft JSON**. Nhiều `SaveUnit` độc lập đăng ký vào một `SaveRegistry`; registry lo load, autosave, flush, và bước `PlayerPrefs.Save()`; unit chỉ giữ model + cờ dirty + sự kiện on-change. Giá trị lẻ đi đường `Prefs<T>`.

**Architecture:** 3 tầng, tổng **7 file** (3 contract + 1 registry + 1 typed-prefs + 2 demo).

```
Contract  (ISaveUnit, SaveUnit<TModel>,      key + dirty + on-change · payload là một chuỗi JSON ·
           ISaveService)                      cửa Register/FlushAll
Registry  (SaveRegistry)                      load lúc Register · autosave chu kỳ · flush pause/quit ·
                                              PlayerPrefs.Save() một lần mỗi lượt · hỏng → default + log
Game      (các unit cụ thể + Prefs<T> lẻ)    model + const key, đăng ký lúc boot
```

**Đường đi của một giá trị, kèm nhịp của từng chặng:**

```
game mutate model  →  MarkDirty()                      [nhịp tương tác]  chỉ bật cờ + phát Changed
                            │
                            │  KHÔNG serialize ở đây
                            ▼
   autosave 5s / pause / quit  →  FlushAll()            [nhịp chu kỳ]
                            │
                            ├─ mỗi unit dirty:  JsonConvert.SerializeObject(Value)  →  chuỗi
                            │                   PlayerPrefs.SetString("save."+Key, chuỗi)
                            │                   ↑ mới nằm trong RAM của Unity, CHƯA xuống đĩa
                            │
                            ├─ PlayerPrefs.Save()       ← chỗ DUY NHẤT chạm đĩa
                            │
                            └─ ClearDirty() cho mọi unit vừa ghi   ← chỉ sau khi Save() xong
```

**Tech Stack:** C#, UniTask, `Sisus.Init` (`[Service]`), Newtonsoft.Json (package `com.unity.nuget.newtonsoft-json` — project đang có bản `3.2.2`; DLL precompiled nên mọi asmdef tự reference), PlayerPrefs. **Không** đụng asmdef, **không** thêm package nào, **không** Addressables, **không** toán, **không** `System.Buffers`.

## Global Constraints

| Ràng buộc | Giá trị |
|---|---|
| Namespace | `Horcrux.Runtime.Abstractions.Persistence` · `Horcrux.Runtime.Implementations.Persistence` |
| Ngôn ngữ trong code | Comment và XML doc viết **tiếng Anh**, khớp với toàn bộ `.cs` đang có trong SDK |
| Hiệu năng | `MarkDirty` chạy theo nhịp **tương tác** — chỉ set cờ + phát event, **không** serialize. Serialize dồn về nhịp flush (autosave mặc định 5 giây + pause/quit) và chỉ chạm unit đang dirty. Load một lần mỗi unit lúc `Register`, sync |
| Ngân sách dữ liệu | Model **cỡ vài KB** mỗi unit. `PlayerPrefs.Save()` ghi lại **toàn bộ** kho prefs mỗi lượt flush có unit dirty — vài KB thì không đáng kể; vượt vài chục KB thì PlayerPrefs không còn là chỗ đúng nữa — xem mục "Ba giới hạn của cách lưu này". Con số thật đo bằng ContextMenu "In payload đang lưu" ở Task 4 |
| SOLID | Registry chỉ biết `ISaveUnit`, không biết model nào · unit không biết chỗ lưu · không type nào trong hệ mang ngữ nghĩa game |
| Editor-first | Chu kỳ autosave là **cấu hình**, phơi ra Inspector của registry |
| An toàn | Đọc fail → model default + log, **không throw** (save hỏng không được chặn người chơi vào game) · ghi fail → **giữ dirty**, log, chu kỳ sau thử lại · try/catch quanh **từng** callback `Changed` · autosave loop nhận `destroyCancellationToken` |
| Bất biến | ① key là **hợp đồng wire format** — const string tường minh, không suy từ tên type ② dirty reset ở **đúng một nơi** (registry), và chỉ **SAU** khi `PlayerPrefs.Save()` thành công ③ serialize chỉ xảy ra trong nhịp flush ④ không có đường "chạy no-op âm thầm": key trùng, registry rỗng, ghi/đọc fail — đều có log |

## Ngữ cảnh đã chốt

Nguồn thiết kế: `SystemPlan.md` mục 2 + §0.3 + §A.3. Nguồn khảo sát: `color-loop` — `PlayerSaveLoadService.cs` (khung chết, 3 lỗi cấu trúc → §0) · `GameDataManager.cs` (phản ví dụ god-blob, và là bản JSON-vào-PlayerPrefs làm sai đúng những chỗ plan này chặn) · `water-flow` — `KPrefs.cs` (tư tưởng cache + on-change).

| Nhóm | Chốt |
|---|---|
| **Ai gọi** | Mỗi hệ game sở hữu unit của mình: `new` unit + `ISaveService.Service.Register(unit)` lúc boot (ứng viên tự nhiên: trong `InitializeAsync` của một `BootStep`) · gameplay/UI mutate `unit.Value` rồi `MarkDirty()`, subscribe `Changed` · giá trị lẻ ("đã rate chưa") qua `PrefsBool`/`PrefsInt`… · `FlushAll` registry tự gọi (autosave + pause/quit); game chỉ gọi thêm khi cần chốt sổ sớm (ngay sau IAP) |
| **Mục tiêu** | Thêm unit mới không sửa SDK và không sửa unit khác · kill app mất ≤ 1 chu kỳ · payload hỏng vẫn vào được game · không có đường no-op âm thầm |
| **Ngân sách** | Ghi giá trị: mỗi tương tác (phải rẻ — chỉ cờ + event) · serialize + `PlayerPrefs.Save()`: mỗi chu kỳ autosave và pause/quit · load: một lần mỗi unit lúc boot. Không hot path mỗi frame. Cỡ dữ liệu: vài KB mỗi unit |
| **Ranh giới** | SDK: contract + registry + typed-prefs. Game: model + const key + thời điểm Register + giá trị interval. Registry **không** biết Bootstrap (Foundation zero-dep) — phối hợp thứ tự flush với hệ khác là việc wire phía game (§0.2) |
| **Hướng phát triển thật** | Chuyển chỗ lưu sang file trên đĩa, khi chạm một trong ba giới hạn ở mục ngay dưới. Plan cho việc đó đã có sẵn (`PersistenceV2.md`): nó đọc được dữ liệu của bản này và không đòi sửa code game |

**Những gì cố ý KHÔNG làm, kèm lý do** (*xoá nó đi thì hỏng ở đâu*):

| Không làm | Vì sao |
|---|---|
| `ISerializer` và bản MemoryPack | Hệ này có **đúng một** format. Dựng interface cho một implementation là thêm một lớp phải đọc mà không ai đứng ở đầu kia — chỉ khi có implementation thứ hai **đang có thật** thì interface mới có tư cách |
| Ghi nguyên tử (`.tmp` + đổi tên) | Ứng dụng không tự ghi file, nên không có đường "kill giữa lúc ghi làm file cụt" ở tầng này. Cả kho prefs được nền tảng ghi một lượt |
| Buffer `IBufferWriter<byte>` tái dùng | `PlayerPrefs.SetString` nhận `string`, không nhận bytes — bộ máy buffer không có chỗ cắm vào |
| `EnsureReady()` khởi tạo trễ | Không có state nào cần dựng trước: `units` là field initializer nên có sẵn từ constructor, và `PlayerPrefs` dùng được bất cứ lúc nào. Một hệ game gọi `Register` trước `Awake` của registry vẫn chạy đúng |
| Crypto | Cả bốn repo khảo sát không dùng thật (Goods-Jam có Rijndael/TripleDES nhưng là dead code, key placeholder) |
| Cloud sync | Nhu cầu thật nhưng backend chưa chuẩn chung |
| Migration version cho model | Chưa có model nào đổi schema |
| `Flush(unit)` lẻ | Chưa call site nào cần flush riêng một unit |
| Backup payload hỏng trước khi ghi đè | Nghiệm thu chỉ đòi "hỏng vẫn vào được game" |

**Khảo sát tái sử dụng:** `IService<T>` đã có (`Abstractions/Foundations/IService.cs`) — dùng lại cho `ISaveService`. Save là hệ **bắt buộc**: thiếu registry trong scene là lỗi cấu hình, phải lộ sớm, nên dùng `IService` (throw) chứ không `IOptionalService`. Tiền lệ truy cập tĩnh đã có trong SDK: `ScreenshotTaker.cs` gọi `ILevelCheater.Service` với cùng khuôn. `EventBus` (Utilities) không dùng — `Changed` là event nội bộ một unit, listener wire trực tiếp. `MonoSingleton` không dùng — đăng ký qua `[Service]` như tiền lệ `BootstrapRunner`. `KPrefs` (water-flow) **không bê nguyên**: nó JSON-hoá cả `int`, lambda subscribe một event static không bao giờ unsubscribe, và khi chưa có key thì deserialize default **mỗi lần gọi** — lấy tư tưởng (cache đọc một lần + on-change), viết mới.

## Ba giới hạn của cách lưu này

Đây là chỗ PlayerPrefs hết đủ. Biết trước thì không phải phát hiện lúc đã ship:

| Giới hạn | Nghĩa cụ thể | Nhận ra ở đâu |
|---|---|---|
| Cả kho là **một khối** | một lần hỏng là mất **mọi** khoá của game cùng lúc, kể cả khoá của những hệ không liên quan | không có ngưỡng — đây là ràng buộc thường trực, cân nhắc ngay từ đầu |
| Mỗi lượt flush ghi lại **toàn bộ** kho | vài KB thì không đáng kể; càng nhiều dữ liệu thì mỗi 5 giây càng đắt | tổng payload vượt **vài chục KB** |
| Cả kho nằm trong RAM suốt phiên | trả phí bộ nhớ thường trực cho thứ chỉ đọc một lần lúc boot | cùng ngưỡng trên |

Con số thật lấy bằng ContextMenu "In payload đang lưu" ở Task 4 — nó in cả độ dài chuỗi.

Chạm giới hạn thì chỗ lưu phải chuyển sang file trên đĩa. `PersistenceV2.md` là plan cho việc đó, và nó viết sao cho **code game không phải sửa một dòng nào**. **Không cần đọc nó bây giờ** — plan này tự đứng được một mình.

---

## §0. Sáu ràng buộc thật

Không có toán. Sáu sự thật của nền tảng và ba bug có thật trong repo quyết định hình dạng code — đọc trước khi viết.

### 0.1. PlayerPrefs cũng là một file trên đĩa — biết nó là file nào thì mới đặt đúng ranh giới

PlayerPrefs không phải một kho lưu trữ khác loại với file. Nó **là** file; khác biệt duy nhất là hệ điều hành ghi hộ thay vì ứng dụng tự ghi:

| Platform | PlayerPrefs thực chất là | Ai xoá nó |
|---|---|---|
| Android | `SharedPreferences` — một file XML trong `/data/data/<package>/shared_prefs/` | gỡ app · "Clear Data" trong Settings |
| iOS | `NSUserDefaults` — một file plist trong `Library/Preferences/` | gỡ app |
| Windows (Editor) | registry, `HKCU\Software\<Company>\<Product>` | xoá tay bằng regedit |

Ba hệ quả đi thẳng vào thiết kế:

1. **Toàn bộ kho là một khối.** Mỗi lần persist là ghi lại **cả** file, và một file hỏng là mất **mọi** khoá của game cùng lúc — kể cả khoá của những hệ không liên quan. Đây là cái giá lớn nhất của cách lưu này, và là ranh giới ngân sách ở bảng Global Constraints.
2. **Toàn bộ kho nằm trong RAM suốt phiên.** Trên Android, `SharedPreferences` được đọc và parse XML **một lần ở lần truy cập đầu tiên** rồi giữ trong bộ nhớ. Nhét một model lớn vào đó là trả phí thường trực cho một thứ chỉ đọc một lần lúc boot.
3. **Khoá là hợp đồng wire format.** Payload của mỗi unit nằm ở khoá `"save." + Key`. Tiền tố `"save."` tồn tại để một `Prefs<T>` do game tự đặt tên không bao giờ đè lên payload của một unit — đây là cách chặn bằng cấu trúc, không phải bằng lời dặn. Đổi tiền tố này là mọi save đang có ngoài đời thành mồ côi.

### 0.2. Android kill không báo trước — `OnApplicationQuit` không phải chỗ dựa

Trên Android, người chơi swipe-kill hoặc hệ điều hành thu hồi RAM thì process chết **không chạy** `OnApplicationQuit`; tín hiệu tin được cuối cùng là `OnApplicationPause(true)`. Vì vậy hợp đồng của hệ là **"mất tối đa MỘT chu kỳ autosave"**, không phải "không bao giờ mất": autosave chu kỳ là lưới đỡ chính, flush ở pause là chốt sổ, quit chỉ là thêm-được-thì-tốt.

**Hệ quả lên thứ tự với hệ khác:** registry tự flush trong magic method `OnApplicationPause`/`OnApplicationQuit` của chính nó — nhưng Unity **không đảm bảo thứ tự** magic method giữa các MonoBehaviour, nên nếu một hệ khác ghi dữ liệu trong pause hook của nó (ví dụ chốt coin), flush của registry có thể chạy **trước** lần ghi đó. Flush trong registry vì thế là **lưới an toàn**, không phải flush có thứ tự; game cần thứ tự thì wire thêm một `BootStep` gọi `FlushAll()` trong `OnAppPause(true)` — fan-out **ngược** của `BootstrapRunner` đảm bảo hệ trên ghi xong trước. Flush hai lần vô hại: lần sau không thấy unit nào dirty.

### 0.3. `SetString` chưa phải là lưu — `PlayerPrefs.Save()` mới là

`PlayerPrefs.SetString` chỉ sửa bản trong RAM của Unity. Thứ đưa cả kho xuống đĩa là `PlayerPrefs.Save()` — và ngoài ra là hai lần persist tự động của nền tảng: khi app vào pause, và khi app quit sạch.

Bỏ `PlayerPrefs.Save()` khỏi vòng flush thì hệ vẫn **trông như** chạy đúng: file được ghi ở pause, restart Play vẫn thấy giá trị. Nhưng autosave chu kỳ khi đó là một vòng lặp **chỉ tốn công serialize mà không mua được gì**: kill app không qua pause là mất hết, đúng bằng lúc chưa có autosave. Nó là bản sao của bài học "khung chạy no-op âm thầm" ở §0.5, nguy hiểm hơn vì lần này còn tốn CPU.

Hệ quả lên hình dạng code: `FlushAll` là **hai giai đoạn**, không phải một vòng lặp. Giai đoạn một `SetString` cho từng unit dirty và **giữ nguyên cờ dirty**; giai đoạn hai gọi `PlayerPrefs.Save()` một lần cho cả lượt, rồi mới `ClearDirty` cho những unit đã đi qua giai đoạn một.

**Một giới hạn của nền tảng, ghi ra để biết:** `PlayerPrefs` không đảm bảo ném exception khi persist hỏng. Nên nhánh "ghi fail thì giữ dirty" ở đây bắt được ít trường hợp hơn — nó vẫn đúng khi có exception, nhưng một lần persist thất bại lặng lẽ thì hệ không biết. Đây là ràng buộc của nền tảng, không phải chỗ code vá được.

### 0.4. Dirty là hợp đồng hai chiều — game set, registry reset SAU khi đã xuống đĩa

Cờ dirty có đúng một người set (`MarkDirty` — game gọi sau khi mutate model) và đúng một người reset (registry — sau khi `PlayerPrefs.Save()` thành công). Reset trước khi xuống đĩa thì một lần ghi lỗi là dữ liệu **mất im lặng**: cờ đã tắt, không ai ghi lại nữa.

*Đã sai một lần — color-loop `PlayerSaveLoadService.Save()`:*

```csharp
if (force || _isDirty)
{
    _isDirty = false;                                  // reset TRƯỚC khi ghi
}
var bytes = MemoryPackSerializer.Serialize(data);      // và thân serialize+ghi nằm NGOÀI if
SaveToDevice(bytes);                                   // → dirty-check vô hiệu, lần nào gọi cũng ghi
```

Hai lỗi trong sáu dòng: reset-trước-khi-ghi, và khối `if` chỉ bọc mỗi việc reset cờ nên serialize + ghi chạy bất kể dirty. Hình dạng đúng trong plan này: `ClearDirty()` là method của contract mà **chỉ registry gọi**, và nó chạy sau `PlayerPrefs.Save()` trong cùng một `try` — persist ném exception thì nhảy vào `catch`, cờ của **cả lượt** còn nguyên, chu kỳ sau thử lại.

### 0.5. Serialize thuộc nhịp flush, không thuộc nhịp đổi giá trị

Mỗi lần coin đổi mà serialize cả model rồi ghi là trả giá theo nhịp **tương tác** cho một việc chỉ cần theo nhịp **chu kỳ**. `MarkDirty` vì thế chỉ set cờ + phát `Changed`; `JsonConvert.SerializeObject` dồn về `FlushAll`, và chỉ unit **dirty** mới bị chạm.

*Đã sai một lần — color-loop `GameDataManager`:* mỗi thay đổi bất kỳ field nào → `LateUpdate` frame đó `JsonUtility.ToJson` **cả god-blob 25+ field** + `PlayerPrefs.Save()` (chạm đĩa) ngay trong frame. Đây là phản ví dụ gần bản này nhất — cùng chỗ lưu, cùng cách serialize, sai ở đúng hai chỗ: một model duy nhất cho cả game, và nhịp serialize bám theo nhịp tương tác.

Cùng họ với nó là bài học **"khung chạy no-op âm thầm"**: khung save "sạch" của color-loop chết vì `AssignService()` không có caller — autosave loop chạy mà không lưu gì, và **không log gì**. Câu trả lời cấu trúc: mọi đường không-làm-gì-được của registry (key trùng, registry rỗng khi flush, ghi/đọc fail) đều phải **kêu lên** (bất biến ④).

### 0.6. Payload hỏng → model default + log, không throw

Chuỗi JSON trong PlayerPrefs có thể hỏng: một bản build cũ ghi model có hình dạng khác, một lần chỉnh tay khi debug, một lần persist đứt nửa chừng ở tầng nền tảng. `JsonConvert.DeserializeObject` gặp chuỗi hỏng thì ném exception.

*Đã sai một lần — color-loop `PlayerSaveLoadService`:* `Load()` không có try/catch quanh `Deserialize`, nên một payload hỏng là exception **mỗi lần boot** — save thành "brick" vĩnh viễn, người chơi không vào được game nữa.

Luật ở đây: try/catch quanh đọc + deserialize, hỏng thì giữ model default và `LogError` **nêu đúng key**. Người chơi mất tiến độ nhưng vào được game, và lần flush kế ghi đè bằng dữ liệu lành.

**Phép kiểm tái lập:** ContextMenu "Ghi rác vào payload" ở Task 4 ghi một chuỗi không phải JSON vào đúng khoá, dừng Play rồi Play lại — vẫn vào demo, log error nêu đúng `demo_progress`, giá trị về default.

---

## Bản đồ triển khai

| Task | File | Nội dung |
|---|---|---|
| 1 | `Abstractions/Foundations/Persistence/` — `ISaveUnit.cs` · `SaveUnit.cs` · `ISaveService.cs` | 3 contract |
| 2 | `Implementations/Foundations/Persistence/SaveRegistry.cs` | registry |
| 3 | `Implementations/Foundations/Persistence/Prefs.cs` | typed-prefs |
| 4 | `Implementations/Foundations/Persistence/Demo/` — `DemoSaveUnit.cs` · `DemoSaveDriver.cs` + scene demo | nghiệm thu |

Thứ tự: **1 → 2 → 3 → 4** (3 độc lập với 2, nhưng demo ở 4 dùng cả hai).

---

### Task 1: 3 contract

**Files:** 3 file mới trong `Assets/Horcrux/Runtime/Abstractions/Foundations/Persistence/`

**Interfaces:**
- Consumes: `IService<T>` (đã có ở `Abstractions/Foundations/IService.cs`) · `Newtonsoft.Json.JsonConvert`.
- Produces: `ISaveUnit` (2 property + 3 method) · `abstract class SaveUnit<TModel>` (game kế thừa — `Value`, `MarkDirty`, `Changed`) · `ISaveService : IService<ISaveService>` (2 method).

**Quyết định thiết kế:**

| Quyết định | Lý do |
|---|---|
| `ISaveUnit` non-generic + `SaveUnit<TModel>` generic implement sẵn | Registry cần một `List<ISaveUnit>` đồng nhất; phần typed nằm ở lớp base mà game kế thừa. Game **không** implement `ISaveUnit` trực tiếp — ba method wire-format của nó là việc của base, nằm đúng một chỗ |
| Payload là `string`, không phải `byte[]` | `PlayerPrefs.SetString` nhận `string`. Đi qua `byte[]` rồi Base64 hoá là dài hơn, chậm hơn, và giết mất lợi thế lớn nhất của bản này: nhìn payload là đọc được ngay |
| `JsonConvert` gọi thẳng trong `SaveUnit<TModel>`, không qua interface | Bản này có đúng một format. Dựng `ISerializer` cho một implementation là thêm một lớp phải đọc mà đầu kia không có ai đứng |
| Key truyền qua **ctor**, kiểu `string` | Khoá wire format là hợp đồng với dữ liệu đã lưu ngoài đời — *đã sai một lần:* `PlayerSaveLoadService` đặt tên chỗ lưu bằng `typeof(T).Name`, đổi tên type là mất save của mọi người chơi |
| `SaveUnit` là **plain class**, không MonoBehaviour | Unit không cần Inspector, không cần lifecycle Unity — hệ game sở hữu nó và `new` trực tiếp |
| API đọc/ghi = property `Value` (chỉ getter) + `MarkDirty()` | Model là mutable class: mutate field rồi báo dirty là một nhịp tự nhiên. Setter thay cả model chỉ cần khi apply snapshot từ cloud — thêm sau là additive |
| `Changed` là `event Action`, fan-out qua `GetInvocationList` + try/catch từng listener | Đăng ký thưa nên `event` là đúng mức. Một listener ném exception không được kéo cả hệ chết. Alloc của `GetInvocationList` theo nhịp tương tác, không theo frame — chấp nhận |
| `ReadPayload` cũng bắn `Changed` | "Value đổi thì `Changed` bắn" là **một** luật không ngoại lệ — load là một lần Value đổi. UI subscribe trước `Register` vẫn nhận đúng trạng thái |
| `ClearDirty()` nằm trên contract, XML doc ghi rõ "chỉ registry gọi" | Reset một nơi. Interface là public nên không giấu được bằng access modifier — nói rõ bằng contract; registry là caller duy nhất trong SDK |
| `ISaveService` dùng `IService` (throw), không `IOptionalService` | Save là hệ **bắt buộc** — thiếu registry trong scene là lỗi cấu hình, phải lộ ngay lần Play đầu |

- [ ] **Step 1: `ISaveUnit.cs`**

```csharp
namespace Horcrux.Runtime.Abstractions.Persistence
{
    /// <summary>One independent block of save data. Game code derives <see cref="SaveUnit{TModel}"/> rather than implementing this.</summary>
    public interface ISaveUnit
    {
        /// <summary>Wire-format key — an explicit const string, never derived from a type name.</summary>
        string Key { get; }

        /// <summary>Has changes not yet stored. The game sets it via MarkDirty; only the registry clears it.</summary>
        bool IsDirty { get; }

        /// <summary>Registry calls this during a flush: the current model as one JSON string.</summary>
        string WritePayload();

        /// <summary>Registry calls this once at Register when an entry exists. Throwing leaves the default model in place.</summary>
        void ReadPayload(string payload);

        /// <summary>REGISTRY ONLY, after PlayerPrefs.Save() succeeded — the single place dirty is cleared.</summary>
        void ClearDirty();
    }
}
```

- [ ] **Step 2: `SaveUnit.cs`**

```csharp
using System;
using Newtonsoft.Json;
using UnityEngine;

namespace Horcrux.Runtime.Abstractions.Persistence
{
    /// <summary>Base for every save unit: typed model, dirty flag, on-change event. A game unit declares only a model and a key.</summary>
    /// <remarks>
    /// Usage: mutate a field inside <see cref="Value"/>, then call <see cref="MarkDirty"/>. Serializing does NOT
    /// happen there — it is deferred to the registry's flush. The unit is usable the moment
    /// <c>ISaveService.Register</c> returns, because loading happens inside Register.
    /// The payload is JSON in PlayerPrefs, so the model must be plain data: numbers, strings, List, Dictionary,
    /// with public fields or properties. Never put a UnityEngine.Object or a Unity struct in it — engine
    /// references do not belong in a save, and Vector3.normalized makes the serializer recurse forever.
    /// </remarks>
    public abstract class SaveUnit<TModel> : ISaveUnit where TModel : class, new()
    {
        private readonly string key;

        /// <param name="key">Wire-format key — a const string owned by the unit; it names the PlayerPrefs entry.</param>
        protected SaveUnit(string key)
        {
            this.key = key;
            Value = new TModel();
        }

        public string Key => key;

        /// <summary>The current model — never null; with nothing stored yet it is <c>new TModel()</c>.</summary>
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

        string ISaveUnit.WritePayload() => JsonConvert.SerializeObject(Value);

        void ISaveUnit.ReadPayload(string payload)
        {
            Value = JsonConvert.DeserializeObject<TModel>(payload) ?? new TModel();
            IsDirty = false;                             // just read back — memory and storage agree
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

- [ ] **Step 3: `ISaveService.cs`**

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

- [ ] **Step 4: Kiểm chứng** — compile sạch; chưa có hành vi chạy được (contract thuần) — hành vi kiểm ở Task 2 và Task 4.

- [ ] **Step 5: Commit** — `feat(sdk): add persistence contracts (save-unit, save-service)`

---

### Task 2: `SaveRegistry`

**Files:**
- Create: `Assets/Horcrux/Runtime/Implementations/Foundations/Persistence/SaveRegistry.cs`

**Interfaces:**
- Consumes: `ISaveUnit` · `ISaveService` (Task 1) · `PlayerPrefs` (engine) · `[Service]` của Sisus.Init · UniTask.
- Produces: `SaveRegistry : MonoBehaviour, ISaveService` — `Register(ISaveUnit)` · `FlushAll()` · `const string PayloadKeyPrefix`; cấu hình Inspector: `autosaveIntervalSeconds`.

**Quyết định thiết kế:**

| Quyết định | Lý do |
|---|---|
| `FlushAll` chia **hai giai đoạn**, `ClearDirty` nằm ở giai đoạn hai | §0.3 — `SetString` mới vào RAM, `PlayerPrefs.Save()` mới xuống đĩa. Reset cờ ngay sau `SetString` là reset trước khi dữ liệu an toàn |
| `PlayerPrefs.Save()` gọi **một lần** cho cả lượt, chỉ khi có ít nhất một unit vừa ghi | Save ghi lại toàn bộ kho, nên gọi n lần cho n unit là ghi cả kho n lần. Không unit nào dirty thì không gọi — flush rỗng phải thật sự không chạm đĩa |
| `pendingClear` là field `List<ISaveUnit>` tái dùng, `Clear()` trong `finally` | Danh sách này sống trong đúng một lượt flush; tái dùng thì không alloc theo chu kỳ, và `finally` đảm bảo lượt sau bắt đầu từ rỗng kể cả khi persist ném exception |
| Không có `EnsureReady()` | Bản này không có state khởi tạo trễ: `units` là field initializer nên có từ constructor, `PlayerPrefs` dùng được mọi lúc. Một hệ game `Register` trước `Awake` của registry vẫn chạy đúng |
| `Register` **load ngay**, đồng bộ | Không tồn tại trạng thái "đã đăng ký nhưng chưa load" — mọi dòng sau `Register` đọc được giá trị thật, không cần phối hợp thứ tự thêm |
| Autosave = UniTask loop + `destroyCancellationToken`, `DelayType.Realtime` | Loop chết theo GameObject, không có `while(true)` sống sót sau `Destroy`. Realtime vì autosave không được ngừng khi game pause bằng `timeScale = 0`. Không dùng Ticker — hệ này là Foundation zero-dep, và nhịp giây không cần nguồn tick trung tâm |
| Autosave tick gọi thẳng `FlushAll` | Autosave, pause, quit, gọi tay — cùng **một** thân flush, nên bốn đường không thể lệch nhau |
| Flush trong magic method của chính registry | §0.2 — lưới an toàn khi project không dùng Bootstrap; đây là MonoBehaviour duy nhất của hệ nên magic method sống đúng một chỗ. Flush **có thứ tự** thì đi đường `BootStep` phía game |
| Key trùng lúc `Register` → log error + bỏ unit mới | Hai unit một key là unit sau đè payload unit trước. So sánh tuyến tính trên `List` là đủ: số unit cỡ chục, và chỉ chạy lúc boot |
| Flush khi 0 unit → `LogWarning` đúng một lần | Khung save của color-loop chết âm thầm vì autosave no-op không log gì (§0.5) |
| `PayloadKeyPrefix` là `public const`, không `private` | Nó là hợp đồng wire format, và có caller thật ngoài registry: hai ContextMenu ở Task 4 ("In payload đang lưu" và "Ghi rác vào payload") phải trỏ đúng khoá mà registry đang ghi. Để nó ở một chỗ thì ba nơi không thể viết lệch nhau |

**Editor setup — bước thật:**

1. Scene entry của game: tạo GameObject `[Save]` → add component `SaveRegistry`.
2. Inspector: đặt `autosaveIntervalSeconds` (mặc định 5) theo game.

- [ ] **Step 1: `SaveRegistry.cs`**

```csharp
using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Horcrux.Runtime.Abstractions.Persistence;
using Sisus.Init;
using UnityEngine;

namespace Horcrux.Runtime.Implementations.Persistence
{
    /// <summary>Owns the save lifecycle: loads at Register, autosaves on a timer, flushes on pause and quit.</summary>
    /// <remarks>
    /// Storage is PlayerPrefs — one entry per unit, holding the model as JSON. The contract is "lose at most ONE
    /// autosave cycle": on Android a swipe-kill never runs OnApplicationQuit, so pause is the last signal we trust.
    /// Flushing from this component's own magic methods is a safety net with NO ordering guarantee. A game that
    /// must flush after another system writes during its own pause hook should call <see cref="FlushAll"/> from a
    /// BootStep instead. Flushing twice is harmless — the second pass finds nothing dirty.
    /// </remarks>
    [Service(typeof(ISaveService), FindFromScene = true)]
    public sealed class SaveRegistry : MonoBehaviour, ISaveService
    {
        /// <summary>Sits in front of every unit key. Wire format: changing it orphans every save already out there.</summary>
        /// <remarks>Never give a <c>Prefs&lt;T&gt;</c> a key starting with this — it would overwrite a unit payload.</remarks>
        public const string PayloadKeyPrefix = "save.";

        [SerializeField, Min(1f), Tooltip("Autosave period in seconds. A killed app loses at most one of these.")]
        private float autosaveIntervalSeconds = 5f;

        private readonly List<ISaveUnit> units = new();
        private readonly List<ISaveUnit> pendingClear = new();   // written this pass, not yet on storage
        private bool warnedEmptyOnce;

        private void Awake() => DontDestroyOnLoad(gameObject);

        private void Start() => AutosaveLoopAsync(destroyCancellationToken).Forget();

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

            // Phase one: hand every dirty payload to PlayerPrefs. Dirty stays on — this is still memory.
            foreach (var unit in units)
            {
                if (!unit.IsDirty) continue;
                try
                {
                    PlayerPrefs.SetString(PayloadKeyPrefix + unit.Key, unit.WritePayload());
                    pendingClear.Add(unit);
                }
                catch (Exception e)
                {
                    Debug.LogError($"[Save] Writing unit '{unit.Key}' failed — it stays dirty and retries next cycle.", this);
                    Debug.LogException(e, this);
                }
            }

            if (pendingClear.Count == 0) return;

            // Phase two: the only call that reaches storage. Dirty clears once it lands, never before.
            try
            {
                PlayerPrefs.Save();
                foreach (var unit in pendingClear) unit.ClearDirty();
            }
            catch (Exception e)
            {
                Debug.LogError("[Save] PlayerPrefs.Save() failed — every unit stays dirty and retries next cycle.", this);
                Debug.LogException(e, this);
            }
            finally
            {
                pendingClear.Clear();
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
            string prefsKey = PayloadKeyPrefix + unit.Key;
            if (!PlayerPrefs.HasKey(prefsKey)) return;   // first run — the unit keeps its default model

            string payload = PlayerPrefs.GetString(prefsKey);
            if (string.IsNullOrEmpty(payload)) return;

            try
            {
                unit.ReadPayload(payload);
            }
            catch (Exception e)
            {
                // A broken save must never stop the player from entering the game.
                Debug.LogError($"[Save] Reading unit '{unit.Key}' failed — using the default model; " +
                               "the entry is overwritten on the next save.", this);
                Debug.LogException(e, this);
            }
        }
    }
}
```

- [ ] **Step 2: Kiểm chứng** (bảng input → kỳ vọng; chưa kèm code test — Task 4 nghiệm thu bằng demo và thao tác tay):

| Input | Kỳ vọng |
|---|---|
| Register unit lần đầu (chưa có entry) | không log lỗi, `Value` là model default, `IsDirty == false` |
| Mutate + `MarkDirty` → chờ hết một chu kỳ autosave | `IsDirty == false`, ContextMenu "In payload đang lưu" thấy JSON đúng giá trị |
| Mutate + `MarkDirty`, **không** chờ | `IsDirty == true`, payload đang lưu vẫn là giá trị cũ — chứng minh serialize không chạy theo nhịp tương tác |
| Register lại đúng key đó (unit thứ hai) | `LogError` nêu key, unit mới không được nạp, unit cũ vẫn hoạt động |
| Payload bị ghi rác trước khi Play | vào game bình thường, `LogError` nêu đúng key, `Value` là default |
| `FlushAll` khi không có gì dirty | không gọi `PlayerPrefs.Save()`, không log |
| Flush khi 0 unit đăng ký | `LogWarning` đúng một lần cho cả phiên, kể cả khi autosave gọi lại nhiều lần |
| Destroy registry giữa phiên | autosave loop dừng theo token, không exception |
| Register trước `Awake` của registry (hệ game chạy sớm hơn) | load đúng, không `NullReferenceException` |

- [ ] **Step 3: Commit** — `feat(sdk): add SaveRegistry (load-on-register, autosave, two-phase flush)`

---

### Task 3: Typed-prefs `Prefs<T>`

**Files:**
- Create: `Assets/Horcrux/Runtime/Implementations/Foundations/Persistence/Prefs.cs`

**Interfaces:**
- Consumes: `PlayerPrefs` (engine).
- Produces: `abstract class Prefs<T>` (`Key` · `Value` · `HasValue` · `Delete()` · `event Action<T> Changed`) + 4 bản chuyên biệt `PrefsInt` · `PrefsBool` · `PrefsFloat` · `PrefsString`.

**Quyết định thiết kế:**

| Quyết định | Lý do |
|---|---|
| Đường riêng cạnh save-unit, cùng nằm trên PlayerPrefs | Giá trị lẻ ("đã rate chưa") không đáng dựng một model và đăng ký vào registry. Hai đường cho hai cỡ dữ liệu — dù ở bản này cả hai cùng đáp xuống PlayerPrefs, chúng khác nhau ở chỗ có hay không có model, dirty và nhịp flush |
| Chuyên biệt theo type, không JSON generic | `PrefsInt` gọi thẳng `GetInt` — JSON hoá một số nguyên là trả giá parse cho thứ nền tảng đã có sẵn API (*`KPrefs` làm sai đúng chỗ này*) |
| Cache sau lần đọc đầu, kể cả nhánh **chưa có key** | Đọc PlayerPrefs là một lần gọi xuống native. `KPrefs` khi chưa có key thì deserialize default **mỗi lần gọi** — bản này cache cả nhánh default, nên đọc `Value` trong `Update` không thành bug hiệu năng |
| Set ghi thẳng PlayerPrefs, **không** gọi `PlayerPrefs.Save()` | Nền tảng tự persist ở pause; ép cả kho xuống đĩa theo mỗi lần set là trả giá nhịp tương tác cho việc nhịp chu kỳ. Registry ở Task 2 đã có một `Save()` theo chu kỳ, và nó cuốn theo cả những giá trị `Prefs<T>` vừa set |
| `Read()` abstract chỉ được base gọi khi `HasValue == true` | Nhánh default xử đúng một nơi ở base, nên bốn bản chuyên biệt không lặp lại logic default |
| Không `syncToServer` / `PrefsDateTime` / `ForceRefresh` | Cả ba gắn với cloud sync, mà cloud nằm ngoài phạm vi. Thêm lại đều là additive: tham số optional, class mới, method mới |

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
| `new PrefsInt("k", 7).Value` khi chưa từng set | `7`, và lần get thứ hai không gọi lại PlayerPrefs |
| Set `Value = 3` → get | `3`; `Changed` bắn đúng một lần với `3` |
| `PrefsBool` set `true` → dừng Play → Play lại | `Value == true` |
| `Delete()` | `HasValue == false`, `Value` về default, `Changed` bắn với default |
| Listener của `Changed` ném exception | log exception, listener đăng ký sau vẫn nhận được |

- [ ] **Step 3: Commit** — `feat(sdk): add typed prefs (Prefs<T> + 4 specializations)`

---

### Task 4: Demo + nghiệm thu chơi thử

**Files:**
- Create: `Assets/Horcrux/Runtime/Implementations/Foundations/Persistence/Demo/DemoSaveUnit.cs`
- Create: `Assets/Horcrux/Runtime/Implementations/Foundations/Persistence/Demo/DemoSaveDriver.cs`
- Scene demo (Editor setup dưới) — không commit vào SDK nếu project có quy ước riêng về scene demo.

**Interfaces:**
- Consumes: `SaveUnit<TModel>` · `ISaveService` (Task 1) · `SaveRegistry` (Task 2) · `PrefsBool` (Task 3).
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

        /// <summary>Prints the stored payload and its length — the real number behind the size budget.</summary>
        [ContextMenu("In payload đang lưu (xem cỡ dữ liệu)")]
        private void PrintPayload()
        {
            string payload = PlayerPrefs.GetString(SaveRegistry.PayloadKeyPrefix + DemoSaveUnit.SaveKey, "(chưa có)");
            Debug.Log($"[DemoSave] {payload.Length} ký tự — {payload}", this);
        }

        /// <summary>Breaks the stored payload on purpose, to prove a corrupt save still lets the player in.</summary>
        [ContextMenu("Ghi rác vào payload (kiểm corrupt)")]
        private void CorruptPayload()
        {
            PlayerPrefs.SetString(SaveRegistry.PayloadKeyPrefix + DemoSaveUnit.SaveKey, "{ rác");
            PlayerPrefs.Save();
            Debug.Log("[DemoSave] Đã ghi rác — dừng Play rồi Play lại để xem registry xử lý.", this);
        }

        private void LogState()
            => Debug.Log($"[DemoSave] coins={unit.Value.coins} level={unit.Value.currentLevel} " +
                         $"dirty={unit.IsDirty}", this);
    }
}
```

- [ ] **Step 3: Editor setup scene demo** (bước thật):

1. Scene mới `PersistenceDemo` → GameObject `[Save]` + component `SaveRegistry` (interval 5).
2. GameObject `[Demo]` + component `DemoSaveDriver`.

- [ ] **Step 4: Kịch bản chơi thử** (nghiệm thu này cần Play mode, developer chạy):

| Mục | Nội dung |
|---|---|
| Vào đâu | Scene `PersistenceDemo`, bấm Play |
| Làm gì | ① chuột phải lên `DemoSaveDriver` → "Add 10 coins" ×3, rồi "In payload đang lưu" **ngay lập tức** · ② chờ quá 5 giây → "In payload đang lưu" lần nữa · ③ dừng Play → Play lại · ④ "Ghi rác vào payload" → dừng Play → Play lại · ⑤ "Register trùng key (kiểm log)" · ⑥ "Add 10 coins" rồi bấm nút Pause của Editor · ⑦ "Toggle prefs" → dừng Play → Play lại |
| Nhìn cái gì | ① log `dirty=true` ngay khi add, nhưng payload in ra vẫn là giá trị **cũ** — serialize chưa chạy · ② payload đã thành `coins=30`, log `dirty=false` · ③ log đầu tiên của phiên mới đã là `coins=30`, không phải `0` — load xảy ra ngay trong `Register` · ④ vẫn vào được demo, `LogError` nêu đúng `demo_progress`, `coins=0` · ⑤ `LogError` "Key 'demo_progress' already has a unit registered" · ⑥ payload cập nhật ngay lúc pause, không phải chờ hết chu kỳ · ⑦ `hasRated` giữ nguyên giá trị qua phiên |
| Khác trước ra sao | So với `GameDataManager` của color-loop: ở đó mỗi lần đổi một field là JSON hoá cả god-blob 25+ field rồi chạm đĩa **ngay trong frame**; ở đây ca ① chứng minh nhịp tương tác không kéo theo serialize, và thêm một cụm save mới chỉ tốn một model + một class mỏng, không đụng SDK và không đụng cụm khác |
| Dấu hiệu hỏng | coins về 0 sau khi restart bình thường (mất save — hỏng load hoặc hỏng flush) · `dirty=true` còn mãi sau khi payload đã đổi (`ClearDirty` không chạy) · ca ① payload đã đổi ngay khi vừa add (serialize đang bám nhịp tương tác — §0.5 vỡ) · ca ④ exception đỏ không ai bắt, hoặc không vào được demo (§0.6 vỡ) · ca ② payload không đổi sau khi chờ (thiếu `PlayerPrefs.Save()` — §0.3 vỡ) |

- [ ] **Step 5: Commit** — `feat(sdk): add persistence demo + acceptance scene`

---

## Ghi chú thực thi

- **Nghiệm thu cuối = kịch bản Task 4 Step 4.** Bốn mục tiêu ở "Ngữ cảnh đã chốt" map vào đó như sau: thêm-unit-không-sửa-SDK (chính `DemoSaveUnit.cs` là bằng chứng sống — 12 dòng cho một cụm save mới) · kill-app-mất-≤-1-chu-kỳ (ca ② và ⑥) · payload-hỏng-vẫn-vào-game (ca ④) · no-op-phải-lộ (ca ⑤ cộng `LogWarning` khi registry rỗng).
- **Sau khi implement xong:** viết `Persistence.md` (tài liệu thiết kế cho agent) cạnh `Implementations/Foundations/Persistence/`, rồi sinh `.html` từ nó. Chuyển các dòng "đã sai một lần" ở §0.4, §0.5 và §0.6 sang mục quyết định thiết kế của tài liệu đó — đó là loại tri thức không đọc ra được từ code.
- **Hệ dùng tiếp:** Audio (`IAudioSettings` lưu volume), Haptics (`IHapticSettings`), Economy (coin và lives), Rating ("đã rate chưa" — `PrefsBool`), LiveOps (tiến độ event — một unit riêng cho mỗi module). Game wire thêm một `BootStep` "Save" nếu cần flush có thứ tự với hệ khác (§0.2).
- **Khoá `"save." + Key` và định dạng JSON của payload là hợp đồng ra ngoài.** Dữ liệu đã nằm trên máy người chơi bám vào đúng hai thứ đó, nên đổi tiền tố hay đổi định dạng sau khi ship là mọi save cũ thành mồ côi — không có gì báo, chỉ là một ngày tất cả người chơi cũ mở game lên thấy tiến độ về 0. Plan chuyển chỗ lưu sang file (`PersistenceV2.md`) cũng đọc đúng hai thứ này.
