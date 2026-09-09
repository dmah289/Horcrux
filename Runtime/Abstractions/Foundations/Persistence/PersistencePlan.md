# Persistence Plan — SaveEntry + BaseSaveCollection + PlayerPrefs + Newtonsoft JSON

> **Loại tài liệu:** Plan — developer tự code lại. `Persistence.md` + `.html` viết **sau** khi có source.
> Thực thi theo checkbox `- [ ]`; agent chạy plan dùng `superpowers:executing-plans` hoặc `superpowers:subagent-driven-development`.

## Mục tiêu

| Tiêu chí | Nghĩa |
|---|---|
| **Có kiểu, một nơi nhìn thấy** | mọi thứ persist được — từ một `bool` tới cả model — là một `SaveEntry<T>`; mọi entry là field của **một** ScriptableObject dẫn xuất từ `BaseSaveCollection`, khai trong assembly của dự án |
| **Mất tối đa một chu kỳ autosave** | Android kill không chạy `OnApplicationQuit` (§0.2) — autosave chu kỳ là lưới đỡ chính, flush ở pause là chốt sổ |
| **Không god-blob** | mỗi entry một key, serialize riêng, chỉ entry dirty mới bị chạm |
| **SDK không mang domain** | không type nào của dự án bị biên dịch vào assembly SDK → submodule commit được một mình (§0.10) |

Collection lo quét field, kiểm trùng key, load, autosave, flush và `PlayerPrefs.Save()`. Entry chỉ giữ giá trị + cờ dirty + event `Changed`.

## Kiến trúc — 3 tầng, 9 file

```
Contract   ISaveEntry · SaveEntry<T> · ISaveCollection      key + default + dirty + Changed · chỉ nói bằng chuỗi payload
           SDK, Abstractions                                 interface thuần, KHÔNG derive IService

Máy móc    BaseSaveCollection · SaveDriver                   abstract · quét field · kiểm key · load · autosave
           SDK, Implementations                              flush hai giai đoạn · PlayerPrefs.Save() một lần mỗi lượt

Composite  SaveBootStep                                      load trong pha boot · flush có thứ tự ở pause/quit
           SDK, Implementations/Composites

Domain     IGameSave · GameSave (+ DemoSaveDriver)           model + key + default + property có kiểu + [Service]
           dự án, com.FelixFelicis                           partial: mỗi feature một cặp file class + interface
```

**Tech stack:** C# · UniTask · `Sisus.Init` (`[Service]`) · Odin (`[Button]`, `[GUIColor]`, `[ShowInInspector]`, `[ReadOnly]`) · Newtonsoft.Json (`com.unity.nuget.newtonsoft-json`) · PlayerPrefs · `System.Reflection` · Unity 6000.3. **Không** sửa asmdef SDK, **không** thêm package, **không** Addressables, **không** toán.

## Ràng buộc toàn cục

| Ràng buộc | Giá trị |
|---|---|
| Namespace | SDK: `Horcrux.Runtime.Abstractions.Persistence` · `Horcrux.Runtime.Implementations.Persistence`. Dự án: namespace riêng (`FelixFelicis.Save`), **không** dưới `Horcrux.*` |
| Ngôn ngữ | Comment và XML doc tiếng Anh, khớp mọi `.cs` trong SDK |
| Nhịp | Gán `Value` / `MarkDirty`: **mỗi tương tác** — chỉ set cờ + phát event. Serialize + `PlayerPrefs.Save()`: **mỗi chu kỳ autosave**, pause, quit, `Flush(entry)`. Reflection + load: **một lần mỗi phiên**, có cache |
| Ranh giới assembly | Chiều phụ thuộc đúng một hướng dự án → SDK. Không `.asmref` nào kéo file dự án vào SDK. Collection chỉ biết `ISaveEntry`; entry không biết `PlayerPrefs` |
| Editor-first | Key, giá trị mặc định, chu kỳ autosave là cấu hình trong Inspector. Kiểm trùng key chạy được **lúc authoring** |
| An toàn | Đọc fail → giữ mặc định + log, không throw · ghi fail → giữ dirty, log, chu kỳ sau thử lại · try/catch quanh **từng** callback `Changed` · autosave nhận `destroyCancellationToken` |
| Bất biến | ① key là **wire format** — chuỗi điền trong Inspector, không suy từ tên type/field ② dirty reset ở **một nơi** (collection), **sau** khi `PlayerPrefs.Save()` thành công ③ serialize chỉ xảy ra trong flush ④ `PlayerPrefs.SetString` xuất hiện **đúng một chỗ** runtime ⑤ không có đường no-op âm thầm — key trùng, key rỗng, field chưa gán, collection rỗng, ghi/đọc fail đều có log |

## Ngữ cảnh đã chốt

| Nhóm | Chốt |
|---|---|
| **Ai gọi** | Dự án khai mọi entry trên `GameSave` (`sealed partial : BaseSaveCollection`) và phơi qua `IGameSave` — cùng khuôn `RemoteConfigSystem.md`. Gameplay/UI gán `Value`, hoặc mutate model rồi `MarkDirty()`, hoặc subscribe `Changed`. `FlushAll` hệ tự gọi; game gọi `Flush(entry)` để chốt sổ sớm. Hệ SDK lấy giá trị save bằng cách nào: **chưa chốt** — xem "Cần developer phân xử" ở cuối |
| **Mục tiêu** | bảng Mục tiêu ở trên + trùng key bắt lúc authoring · quên khởi tạo không mất tiến độ · thêm entry không sửa SDK |
| **Ngân sách** | bảng Ràng buộc, hàng "Nhịp". Không hot path mỗi frame. Tổng payload **vài chục KB** — vượt thì PlayerPrefs hết đúng chỗ (bảng "Ba giới hạn") |
| **Ranh giới** | SDK: contract, `BaseSaveCollection`, `SaveDriver`, `SaveBootStep`, nút Editor, `KeyPrefix`. Dự án: model, key, default, interval, `IGameSave`, `GameSave`, `[Service]`, `[CreateAssetMenu]`, đường dẫn Resources, kéo asset vào `SaveBootStep` |
| **Hướng phát triển thật** | Đổi kho sang file trên đĩa khi chạm một trong ba giới hạn. Entry chỉ nói bằng chuỗi payload nên việc đó sửa nội bộ `BaseSaveCollection` — không đụng entry, không đụng code game |

**Cố ý KHÔNG làm** — *xoá nó đi thì hỏng ở đâu*:

| Không làm | Vì sao |
|---|---|
| `partial class` trong SDK + `.asmref` phía dự án | `partial` phải cùng assembly → model của dự án bị kéo vào SDK; chiều ngược là circular reference (§0.10 ①) |
| `ISaveCollection` derive `IService<ISaveCollection>` | `IGameSave` derive cả hai → hai thành viên `Service` → CS0229 (§0.10 ②) |
| Đăng ký `ISaveCollection` thành service thứ hai | Chỗ duy nhất SDK cần collection là `SaveBootStep`, mà nó nhận reference kéo thả. Một dòng `[Service]` là đủ, đúng như `GameRemoteConfigCollection` |
| `Prefs<T>` riêng cho giá trị lẻ | Giá trị lẻ và model chỉ khác **một bước** (biến thành chuỗi). Bộ máy thứ hai đặt giá trị lẻ ra ngoài cờ dirty và `PlayerPrefs.Save()` — ngoài chính bảo đảm của hệ |
| `ISerializer` · `ISaveStore` | Mỗi cái đúng một implementation. Ranh giới "entry chỉ nói bằng chuỗi payload" đã đủ để đổi kho sau, tốn 0 dòng |
| `Register()` lúc runtime | Đăng ký lúc chạy là gốc của bốn chỗ hở: không liệt kê được, không kiểm trùng được, quên gọi thì im lặng |
| Kiểm hợp lệ nền trong `OnValidate` | Lúc đang thêm entry thì key **luôn** rỗng — cảnh báo hiện toàn thời gian vào đúng lúc chưa sửa được |
| Xoá save theo tiền tố | `PlayerPrefs` không có API liệt kê khoá; chỉ xoá được khoá đang khai, khoá mồ côi sống sót — nút xoá nói thẳng điều đó |
| Ghi nguyên tử (`.tmp` + rename) | Ứng dụng không tự ghi file; nền tảng ghi cả kho một lượt |
| Crypto · Cloud sync · Migration version | Không repo nào dùng crypto thật · backend chưa chuẩn chung · chưa model nào đổi schema |

**Khảo sát tái sử dụng:**

| Cái có sẵn | Kết luận |
|---|---|
| `IService<T>` (`Abstractions/Foundations/IService.cs`) | **Dùng lại ở phía dự án**: `IGameSave` derive, `ISaveCollection` không. Save là hệ bắt buộc — thiếu asset phải nổ ngay lần Play đầu, nên `IService` (throw), không `IOptionalService` |
| `BaseRemoteConfigCollection` + `RemoteConfig<T>` (`RemoteConfigSystem.md`) | **Dùng lại khuôn**, không dùng lại code: abstract base SDK + `sealed partial` dự án + attribute + reflection quét field + một dòng `[Service]`. Ba chỗ cố ý đảo và hai chỗ thêm so với khuôn: bảng cuối "Ghi chú thực thi" |
| `SplitterAttribute` (`Runtime/Utilities/Attribute/`) | **Dùng lại** — `[Splitter("Economy")]` kẻ tiêu đề nhóm trong Inspector |
| `BaseBootStep` + `BootstrapRunner` | **Dùng lại** cho `SaveBootStep`. Runner fan-out pause/quit **ngược thứ tự** — thứ magic method của MonoBehaviour lẻ không hứa được |
| `EventBus` · `MonoSingleton` | **Không dùng** — `Changed` là event nội bộ một entry; đăng ký qua `[Service]` |
| `ISaveUnit.cs` | **Xoá** — contract không còn implementation nào |

## Ba giới hạn của PlayerPrefs

| Giới hạn | Nghĩa | Nhận ra ở đâu |
|---|---|---|
| Cả kho là **một khối** | một lần hỏng là mất **mọi** khoá của game, kể cả của hệ không liên quan | ràng buộc thường trực |
| Mỗi flush ghi lại **toàn bộ** kho | càng nhiều dữ liệu, mỗi 5 giây càng đắt. Cũng là lý do `Flush(entry)` không rẻ hơn `FlushAll()` ở phần chạm đĩa | tổng payload vượt **vài chục KB** |
| Cả kho nằm trong RAM suốt phiên | phí bộ nhớ thường trực cho thứ chỉ đọc một lần lúc boot | cùng ngưỡng |

Con số thật: nút `Print all payloads` (Task 4) in độ dài từng payload và tổng.

---

## §0 — Mười sự thật quyết định hình dạng code

Năm sự thật nền tảng · bốn bug có thật trong repo · một ranh giới C# + Unity. Đọc trước khi viết.

### 0.1 PlayerPrefs là một file, hệ điều hành ghi hộ

| Platform | Thực chất | Ai xoá |
|---|---|---|
| Android | `SharedPreferences` — XML trong `/data/data/<package>/shared_prefs/`, parse **một lần** rồi giữ RAM | gỡ app · Clear Data |
| iOS | `NSUserDefaults` — plist trong `Library/Preferences/` | gỡ app |
| Windows Editor | registry `HKCU\Software\<Company>\<Product>` | regedit |

Hệ quả: hai giới hạn đầu ở bảng trên, và **không gian khoá phẳng, dùng chung**: `RemoteConfig<T>` ghi cache bằng key thô không tiền tố, SDK ads/analytics cũng ghi vào đó. Tiền tố `"save."` là thứ chặn một firebase key trùng tên đè lên tiến độ — và là wire format: đổi sau khi ship là mọi save thành mồ côi.

### 0.2 Android kill không chạy `OnApplicationQuit`

Swipe-kill hoặc hệ thu hồi RAM: tín hiệu tin được cuối cùng là `OnApplicationPause(true)` (Android) hoặc `OnApplicationFocus(false)` (iOS) — runner gom cả hai thành `OnGoToBackground(true)`. Hợp đồng vì thế là **"mất tối đa MỘT chu kỳ autosave"**: autosave là lưới chính, pause là chốt sổ, quit là thêm-được-thì-tốt.

Unity không đảm bảo thứ tự magic method giữa MonoBehaviour → `SaveDriver` flush ở pause có thể chạy **trước** một hệ khác ghi dữ liệu trong pause hook của nó. Driver là lưới an toàn **không thứ tự**; đường có thứ tự là `SaveBootStep` (Task 6, runner fan-out ngược). Chạy cả hai vô hại: lượt sau không thấy gì dirty, không chạm đĩa.

### 0.3 `SetString` chưa lưu — `PlayerPrefs.Save()` mới lưu

`SetString` sửa bản RAM của Unity; xuống đĩa là `PlayerPrefs.Save()` cộng hai lần persist tự động của nền tảng ở pause và quit sạch. Thiếu `Save()` trong autosave thì hệ **trông như** đúng (pause vẫn ghi, restart Play vẫn thấy) nhưng kill không qua pause là mất hết.

Hình dạng code: `FlushAll` **hai giai đoạn** — ① `SetString` từng entry dirty, **giữ** cờ dirty · ② `PlayerPrefs.Save()` một lần, rồi `ClearDirty` cho các entry vừa qua ①. Giới hạn nền tảng: `PlayerPrefs` không đảm bảo ném exception khi persist hỏng, nên nhánh "ghi fail giữ dirty" bắt ít ca hơn ta muốn.

### 0.4 Dirty: game set, collection reset **sau** khi xuống đĩa

Reset trước khi ghi thì một lần ghi lỗi là mất **im lặng**: cờ đã tắt, không ai ghi lại.

*Đã sai một lần — color-loop `PlayerSaveLoadService.Save()`:*

```csharp
if (force || _isDirty) { _isDirty = false; }              // reset TRƯỚC khi ghi
var bytes = MemoryPackSerializer.Serialize(data);          // thân serialize + ghi nằm NGOÀI if
SaveToDevice(bytes);                                       // → lần nào gọi cũng ghi
```

Hình dạng đúng: `ClearDirty()` là method của contract **chỉ collection gọi**, chạy sau `PlayerPrefs.Save()` trong cùng một `try` — persist ném thì cờ cả lượt còn nguyên.

### 0.5 Serialize thuộc nhịp flush, không thuộc nhịp đổi giá trị

Gán `Value` / `MarkDirty` chỉ set cờ + phát `Changed`; `JsonConvert` dồn về `FlushAll`, chỉ chạm entry dirty.

*Đã sai một lần — color-loop `GameDataManager`:* mỗi thay đổi bất kỳ field → `LateUpdate` frame đó `JsonUtility.ToJson` cả god-blob 25+ field + `PlayerPrefs.Save()` ngay trong frame.

*Cùng họ — khung save "sạch" của color-loop:* `AssignService()` không có caller → autosave loop chạy mà không lưu gì, **không log gì**. Mọi đường không-làm-gì-được của collection phải **kêu lên** (bất biến ⑤).

### 0.6 Payload hỏng → giá trị mặc định + log, không throw

Chuỗi trong PlayerPrefs có thể hỏng (build cũ ghi hình dạng khác, chỉnh tay khi debug, persist đứt nửa chừng); `DeserializeObject` ném exception.

*Đã sai một lần — color-loop `PlayerSaveLoadService.Load()`:* không try/catch quanh `Deserialize` → exception **mỗi lần boot**, save thành brick vĩnh viễn.

Luật: try/catch quanh đọc + deserialize **từng entry**; hỏng thì giữ mặc định, `LogError` nêu đúng key, entry khác không bị kéo theo, flush kế ghi đè bằng dữ liệu lành.

### 0.7 ScriptableObject sống qua các lần Play — bốn thứ phải reset

Domain reload tắt → mọi field `[NonSerialized]` mang giá trị phiên trước sang phiên sau:

| Sống sót | Sở hữu | Triệu chứng | Ai reset |
|---|---|---|---|
| Listener của `Changed` | entry | trỏ vào GameObject đã huỷ → `MissingReferenceException` ở lần đổi đầu tiên | `ResetRuntimeState()` |
| Giá trị runtime | entry | xoá save rồi Play vẫn thấy giá trị cũ — chỉ ở Editor, biến mất trên build | `ResetRuntimeState()` |
| Cờ dirty | entry | flush đầu phiên ghi thứ không ai đổi | `ResetRuntimeState()` |
| **Cache subclass dựng từ entry** | **dự án** | lookup, `HashSet`, cờ "đã parse" mang số phiên trước — entry không với tới được | hook `ResetDerivedState()` **trước** load + `OnEntriesLoaded()` **sau** load |

Hook "trước" không bỏ được: chỉ có hook "sau" thì lượt load không chạm entry nào (lần chạy đầu) vẫn để cache cũ sống.

*Đã sai một lần — Remote Config, cùng khuôn, thiếu hook "trước":* `GameRemoteConfigCollection._hasAppliedRemoteValues` chỉ bật trong `OnRemoteConfigsApplied()`, không ai tắt → mang trạng thái phiên trước; một hệ khác phải viết workaround và ghi lý do vào comment. Một cờ không reset được đã mất tư cách làm điều kiện chờ.

### 0.8 Asset là dữ liệu authoring — tiến độ người chơi không được chạm vào

Hai trục Unity dán vào nhau (Inspector vẽ **từ** dữ liệu serialize); Odin tách chúng ra:

| Trục | Ai quyết | `value`, `isDirty` |
|---|---|---|
| Ghi vào `.asset` | `[SerializeField]` / `[NonSerialized]` | **không** — dữ liệu người chơi sẽ vào git |
| Hiện trong Inspector | `[ShowInInspector]` vẽ bất kể serialize | **có** — sáu ca nghiệm thu kiểm `IsDirty` ở đó |
| Sửa tay được | `[ReadOnly]` | **không** — sửa tay bỏ qua setter: không `MarkDirty`, không `Changed`, flush không ghi, không log (bất biến ⑤). Cửa sửa tay đúng là nút `ImportPayload` |

Hai bẫy:

- ⚠️ **`[SerializeField]` bọc `#if UNITY_EDITOR`**: Editor ghi asset kèm field, build strip field → đọc asset lệch byte → **crash native** `Read N bytes but expected M bytes`, không stack trace C#. Comment còn trong `RemoteConfig.cs`; `RemoteConfigSystem.md` xếp vào bảng Bẫy.
- **`defaultValue` phải sao chép, không trả thẳng**: nó là object sống trong asset; gán `value = defaultValue` là để game mutate vào asset. Round-trip qua serializer một lần mỗi entry lúc `Initialize` — bản sao đúng cho mọi `T`, kể cả struct chứa `List`.

`RemoteConfig<T>` cố ý làm ngược (`[SerializeField] value`) để developer thấy giá trị fetch trong asset — lý do đó không còn đúng khi thứ lưu là tiến độ người chơi.

### 0.9 Quên `Initialize()` thì mất dữ liệu không hồi được

`BaseRemoteConfigCollection.Initialize()` do dự án gọi ở `ServiceInit` — một dòng mỗi host phải nhớ viết. Giá của việc quên **bất đối xứng**:

| Hệ | Quên gọi thì |
|---|---|
| Remote Config | rơi về giá trị author. Sai nhưng **hồi được** — fetch sau đúng |
| Save | entry mang mặc định, flush kế **ghi đè mặc định lên save thật**. **Không hồi được** |

Bảo đảm mà giá phá là không hồi được thì không đứng trên trí nhớ người viết dòng wire. Nên `Initialize()` có **hai lối vào, một thân idempotent**: gọi tường minh từ `SaveBootStep` (trả chi phí ở màn loading, lỗi lộ trong pha boot), hoặc `EnsureInitialized()` ở mọi cửa công khai tự lo.

### 0.10 Ranh giới assembly — vì sao khuôn là kế thừa, không phải `partial`

Bốn sự thật đã kiểm bằng phép thử chạy được:

| # | Sự thật | Hệ quả lên code |
|---|---|---|
| ① | Mọi phần của một `partial` type phải **cùng assembly**. Nửa dự án đi vào SDK bằng `.asmref` thì type nó gọi tên (`SaveEntry<PlayerProgress>`) phải tra được từ SDK → `PlayerProgress` và cả chuỗi phụ thuộc bị kéo vào; chiều ngược là circular reference | Subclass **là** type của dự án, chỉ gọi tên xuống SDK → kế thừa |
| ② | `IService<out T>` khai `Service` **static** trong interface. Interface thừa hưởng **hai** instantiation thì `Service` nhập nhằng — lỗi biên dịch (snippet dưới) | `ISaveCollection` thuần; chỉ `IGameSave` derive `IService<>`. Không gì trong SDK resolve `ISaveCollection` qua service locator — `SaveBootStep` nhận reference kéo thả |
| ③ | `ServiceAttribute` khai `Inherited = false` — `[Service]` trên base **không** áp cho subclass; `Service.Get<>()` ném ở lần chạm đầu | `[Service]`, `[CreateAssetMenu]`, đường dẫn Resources đều thuộc dự án |
| ④ | `GetType().GetFields(NonPublic \| Instance)` thấy private field của subclass (kể cả ở file `partial` khác — cùng type), **không** thấy private field khai trên base — bỏ qua **không lỗi** | Base không tự khai entry; cần thì field phải `protected`. Convention field entry `private + [SerializeField]` → scan chỉ `NonPublic`, thêm `Public` là mở cửa im lặng cho hình dạng convention cấm |

```csharp
public interface ISaveCollection : IService<ISaveCollection> { }   // nếu giữ IService ở contract
public interface IGameSave : ISaveCollection, IService<IGameSave> { }
var x = IGameSave.Service;
// error CS0229: Ambiguity between 'IService<ISaveCollection>.Service' and 'IService<IGameSave>.Service'
```

Bẫy đi kèm kế thừa: magic method của Unity (`OnDestroy`, `OnDisable`…) khai trên subclass **che** bản của base, dọn dẹp của base im lặng không chạy. Hôm nay collection không dùng magic method nào; nếu thêm thì khai `protected virtual`.

---

## Bản đồ triển khai

| Task | Ở đâu | File | Nội dung |
|---|---|---|---|
| 1 | SDK | `Abstractions/Foundations/Persistence/` — `ISaveEntry.cs` · `SaveEntry.cs` · `ISaveCollection.cs`; **xoá** `ISaveUnit.cs` | 3 contract |
| 2 | SDK | `Implementations/Foundations/Persistence/BaseSaveCollection.cs` | lõi collection |
| 3 | SDK | `Implementations/Foundations/Persistence/SaveDriver.cs` | autosave + pause/quit |
| 4 | SDK | `BaseSaveCollection.cs` (thêm khối `#if UNITY_EDITOR`) | 3 nút Editor |
| 5 | **Dự án** | `IGameSave.cs` · `GameSave.cs` · `DemoSaveDriver.cs` trong `com.FelixFelicis` | cặp file dự án + demo + nghiệm thu |
| 6 | SDK | `Implementations/Composites/Persistence/SaveBootStep.cs` | flush có thứ tự |

Thứ tự **1 → 2 → 3 → 4 → 5 → 6**. Task 4 sửa file của Task 2.

**Task 1–4 chỉ nghiệm thu bằng compile sạch; Task 5 là chỗ hệ chạy được lần đầu** — SDK không có entry nào (một entry là domain), nên không tạo được asset, không đăng ký được service. Task 6 sau Task 5 vì phép kiểm thứ tự flush cần một entry thật.

---

### Task 1: 3 contract

**Files:**
- Create: `Assets/Horcrux/Runtime/Abstractions/Foundations/Persistence/ISaveEntry.cs` · `SaveEntry.cs` · `ISaveCollection.cs`
- **Delete:** `Assets/Horcrux/Runtime/Abstractions/Foundations/Persistence/ISaveUnit.cs` (+ `.meta`)

**Interfaces:**
- Consumes: `IService<T>` · `Newtonsoft.Json.JsonConvert` · Odin `[ShowInInspector]`, `[ReadOnly]` ở scope runtime (`Sirenix.OdinInspector.Attributes.dll` là assembly runtime; `RemoteConfig<T>` đã dùng ngoài `#if UNITY_EDITOR`) · `[Button]`, `[MultiLineProperty]` trong `#if UNITY_EDITOR`.
- Produces: `ISaveEntry` (2 property + 4 method) · `SaveEntry<T>` + nút `ImportPayload` · `ISaveCollection` (1 property + 3 method, **không** derive `IService<>`).

**Quyết định thiết kế:**

| Quyết định | Lý do |
|---|---|
| Một `SaveEntry<T>` cho cả giá trị lẻ và model | Hai cỡ chỉ khác cách biến thành chuỗi; kiểu thứ hai đặt giá trị lẻ ra ngoài cờ dirty và `PlayerPrefs.Save()` |
| `int`/`bool`/`float` đi qua JSON, không dùng `GetInt`/`GetFloat` | Parse một lần lúc boot, `ToString` một lần mỗi flush — không đo nổi; đổi lại một đường đọc, một đường ghi |
| `ISaveEntry` non-generic + `SaveEntry<T>` generic | Collection cần `List<ISaveEntry>` đồng nhất; game không implement `ISaveEntry` trực tiếp |
| Bốn method của `ISaveEntry` cài **explicit** | `ResetRuntimeState`, `ReadPayload`, `WritePayload`, `ClearDirty` chỉ collection gọi — explicit làm chúng biến mất khỏi IntelliSense của game, chặn bằng cấu trúc |
| Entry chỉ nói bằng chuỗi payload | Đổi kho sang file sau này chỉ sửa collection — phòng xa tốn 0 dòng |
| `[NonSerialized] value` · `[SerializeField] defaultValue` | Chia theo **nguồn**: default do developer đặt → asset, git; value do người chơi sinh → máy người chơi (§0.8) |
| `value`, `isDirty` thêm `[ShowInInspector, ReadOnly]` | Không serialize **và** vẫn nhìn thấy; `[ReadOnly]` vì sửa tay bỏ qua setter → no-op âm thầm (§0.8) |
| `CloneDefault()` round-trip, **không** nhánh | Deep copy đúng cho mọi `T`; `null` round-trip ra `null` = `default(T)` — nhánh thứ hai trả cùng giá trị nên chỉ là chỗ phải giữ đúng mãi |
| Check null ở `ReadPayload`, **không** ở `CloneDefault` | Kho chứa được chuỗi `null` hợp lệ → không exception nào tới catch của `LoadAll`; reference type đọc về null im lặng, không dirty → **null vĩnh viễn**. Đây là cửa duy nhất giữ "không null khi đã author default" |
| `Value` có setter **và** `MarkDirty()` | Gán là nhịp của giá trị lẻ; mutate rồi báo là nhịp của model |
| `implicit operator T` | Tiền lệ `RemoteConfig<T>`, call site gọn: `if (collection.HasRated)`. Bẫy bất đối xứng khi so sánh — bảng dưới |
| Giữ guard `entry != null` trong operator | Ca null duy nhất là field chưa gán, mà `ScanEntries` đã `LogError` ở `Validate keys` **và** `Initialize`. Giữ để khớp `RemoteConfig<T>`; biết rằng nó đổi `NullReferenceException` thành `default(T)` im lặng — chấp nhận **chỉ vì** ca đó đã kêu hai lần |
| Nút `ImportPayload` trên từng entry, chỉ Play mode | Tái lập bug ở một tiến độ cụ thể. Đi qua setter nên vẫn `MarkDirty` + `Changed`; `SetString` vẫn một cửa (bất biến ④). Ngoài Play thì `value` là `[NonSerialized]` — import vào field sẽ biến mất |
| Không nút xoá từng entry · không đường CSV | Chưa có nhu cầu gọi tên được; `Delete all save data` phủ · save là dữ liệu người chơi, không phải dữ liệu author |
| `Changed` là `event Action<T>`, fan-out `GetInvocationList` + try/catch từng listener | Đăng ký thưa; một listener ném không kéo cả hệ. Alloc theo nhịp tương tác, không theo frame |
| `ReadPayload` cũng bắn `Changed` | "Value đổi thì `Changed` bắn" là một luật không ngoại lệ |
| `ISaveCollection` thuần — không `partial`, không `IService<>` | §0.10 ① ②. Property có kiểu nằm trên `IGameSave` (Task 5) |

**Chính sách null của Newtonsoft không đồng nhất theo kiểu** — đo bằng `Newtonsoft.Json.dll` của project (`Library/PackageCache/com.unity.nuget.newtonsoft-json@*/Runtime/`) trên `dotnet`:

| Gọi | Kết quả |
|---|---|
| `SerializeObject((Progress)null)` | chuỗi `null` — payload **hợp lệ** |
| `DeserializeObject<T>("null")`, `T` là class · `string` · `List<>` · `Dictionary<,>` | `null`, không throw |
| `DeserializeObject<T>("null")`, `T` là `int` · `bool` · struct | **throw** `JsonSerializationException` |
| `DeserializeObject<Progress>("")`, `("   ")` | `null`, không throw |
| Round-trip null không guard, mọi `T` hàng 2 | `null` = `default(T)` |
| Round-trip `defaultValue` có thật, mutate bản clone | gốc không đổi; `ReferenceEquals` hai `List` bên trong `False` — deep copy thật |

→ reference type để null đi qua **im lặng** nên `ReadPayload` phải bắt · value type thì catch của `LoadAll` bắt hộ · hàng 5 là lý do `CloneDefault` không nhánh, hàng 6 là lý do nó tồn tại.

**`loaded == null` trên `T` không ràng buộc không cần cast `(object)`** — đo ở C# 9: biên dịch sạch, 0 warning, kết quả y hệt `(object)loaded == null`; compiler box rồi so tham chiếu nên value type luôn `False`. `CS0019` chỉ nổ khi `where T : struct`.

**Bẫy `implicit operator T` — bất đối xứng khi so sánh.** `entry == null` là so **tham chiếu** (literal `null` không có kiểu, `SaveEntry<T>` không khai `operator ==`, tập candidate rỗng). So với toán hạng **kiểu `T`** thì `T.op_Equality` vào tập, entry bị convert:

```csharp
var e = new SaveEntry<string>();   // entry có thật, value == null
string s = null;
e == null    // false — so tham chiếu
e == s       // TRUE  — so chuỗi: (string)e là null
```

Guard `entry != null` bên trong operator cũng là so tham chiếu nên không đệ quy. **Phép kiểm** (Roslyn kèm Unity: `Editor/Data/DotNetSdkRoslyn/csc.dll` trên `Editor/Data/NetCoreRuntime/dotnet.exe`): `e == null` → `False` · `e == s` → `True` · `SaveEntry<int> == null` biên dịch được, ra `False`.

- [ ] **Step 1: `ISaveEntry.cs`**

```csharp
namespace Horcrux.Runtime.Abstractions.Persistence
{
    /// <summary>What the collection needs from one saved value. Game code declares <see cref="SaveEntry{T}"/>, never this.</summary>
    /// <remarks>Speaks in payload strings only, so the store can change without touching an entry.</remarks>
    public interface ISaveEntry
    {
        /// <summary>Wire-format key authored in the Inspector; stored under the collection prefix plus this.</summary>
        string Key { get; }

        /// <summary>Has changes not yet on storage. The game sets it; only the collection clears it.</summary>
        bool IsDirty { get; }

        /// <summary>Collection only, at Initialize: drops listeners and restores a fresh copy of the default.</summary>
        void ResetRuntimeState();

        /// <summary>Collection only, at Initialize: applies the stored payload. Throwing keeps the default.</summary>
        void ReadPayload(string payload);

        /// <summary>Collection only, during a flush: the current value as one payload string.</summary>
        string WritePayload();

        /// <summary>Collection only, after storage accepted the write — the single place dirty clears.</summary>
        void ClearDirty();
    }
}
```

- [ ] **Step 2: `SaveEntry.cs`**

```csharp
using System;
using Newtonsoft.Json;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Horcrux.Runtime.Abstractions.Persistence
{
    /// <summary>One saved value of any size — a flag, a counter, or a whole model. Declared as a field on the project's save collection.</summary>
    /// <remarks>Payload is JSON: plain data only, no UnityEngine.Object, no Unity structs.</remarks>
    [Serializable]
    public class SaveEntry<T> : ISaveEntry
    {
        [SerializeField, Tooltip("Wire format. Renaming it orphans every save already on a player's device.")]
        private string key;

        [SerializeField, Tooltip("Value before anything is stored. Authored here, never written at runtime.")]
        private T defaultValue;

        // Player data: never serialized into the asset, still visible, never hand-edited (bypasses the setter).
        [NonSerialized, ShowInInspector, ReadOnly] private T value;
        [NonSerialized, ShowInInspector, ReadOnly] private bool isDirty;

        public string Key => key;

        public bool IsDirty => isDirty;

        /// <summary>The live value. Assigning replaces it and marks dirty; to mutate a model in place, call MarkDirty after.</summary>
        public T Value
        {
            get => value;
            set
            {
                this.value = value;
                MarkDirty();
            }
        }

        /// <summary>Fires on every change: assignment, MarkDirty, and load. A throwing listener cannot kill the entry.</summary>
        public event Action<T> Changed;

        /// <summary>Call after mutating the value in place. Sets the flag and fires Changed; nothing is serialized.</summary>
        public void MarkDirty()
        {
            isDirty = true;
            RaiseChanged();
        }

        public static implicit operator T(SaveEntry<T> entry) => entry != null ? entry.value : default;

        void ISaveEntry.ResetRuntimeState()
        {
            Changed = null;            // listeners from last play session point at destroyed objects
            value = CloneDefault();
            isDirty = false;
        }

        void ISaveEntry.ReadPayload(string payload)
        {
            T loaded = JsonConvert.DeserializeObject<T>(payload);
            // A stored "null" is valid JSON and reads back clean, so nothing later would overwrite it.
            value = loaded == null ? CloneDefault() : loaded;
            isDirty = false;
            RaiseChanged();
        }

        string ISaveEntry.WritePayload() => JsonConvert.SerializeObject(value);

        void ISaveEntry.ClearDirty() => isDirty = false;

        /// <summary>A fresh copy of the authored default, so the game never mutates the asset.</summary>
        // Round-trip deep-copies every T, a struct holding a List included; "null" comes back as default(T).
        private T CloneDefault()
            => JsonConvert.DeserializeObject<T>(JsonConvert.SerializeObject(defaultValue));

        private void RaiseChanged()
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

#if UNITY_EDITOR
        // Editor input box only: neither authored nor player data, so shown without being stored.
        [NonSerialized, ShowInInspector, MultiLineProperty]
        private string payloadToImport;

        /// <summary>Replaces the live value with the pasted payload, to reproduce a bug at an exact progress. Play mode only.</summary>
        [Button]
        private void ImportPayload()
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning($"[Save] Import into '{key}' needs Play mode — the live value only exists then.");
                return;
            }

            if (string.IsNullOrEmpty(payloadToImport))
            {
                Debug.LogWarning($"[Save] Import into '{key}' skipped: the payload box is empty.");
                return;
            }

            try
            {
                Value = JsonConvert.DeserializeObject<T>(payloadToImport);   // setter marks dirty and fires Changed
                Debug.Log($"[Save] Imported into '{key}'. It reaches storage on the next flush.");
            }
            catch (Exception e)
            {
                Debug.LogError($"[Save] Import into '{key}' failed — the value is unchanged.");
                Debug.LogException(e);
            }
        }
#endif
    }
}
```

- [ ] **Step 3: `ISaveCollection.cs`**

```csharp
using System.Collections.Generic;

namespace Horcrux.Runtime.Abstractions.Persistence
{
    /// <summary>The one place every saved value lives. A project derives its own service interface from this one.</summary>
    /// <remarks>Must NOT derive IService: a project interface deriving both gets two Service members (CS0229).</remarks>
    public interface ISaveCollection
    {
        /// <summary>Every entry that passed validation. Empty until Initialize has run.</summary>
        IReadOnlyList<ISaveEntry> Entries { get; }

        /// <summary>Scans, validates, resets and loads every entry, then starts the autosave driver. Idempotent; the first touch of any member runs it.</summary>
        void Initialize();

        /// <summary>Stores every dirty entry now. Called by autosave, pause and quit.</summary>
        void FlushAll();

        /// <summary>Stores one entry now, for a value that must not wait for the cycle. Not cheaper on disk than FlushAll.</summary>
        /// <param name="entry">Must be owned by this collection; anything else is refused with an error.</param>
        void Flush(ISaveEntry entry);
    }
}
```

- [ ] **Step 4: Kiểm chứng** — compile sạch; `grep -rn "ISaveUnit" Assets` không ra kết quả.

- [ ] **Step 5: Commit** — `feat(sdk): replace save-unit contracts with SaveEntry`

---

### Task 2: `BaseSaveCollection` — lõi

**Files:**
- Create: `Assets/Horcrux/Runtime/Implementations/Foundations/Persistence/BaseSaveCollection.cs`

**Interfaces:**
- Consumes: `ISaveEntry` · `ISaveCollection` · `PlayerPrefs` · `System.Reflection`.
- Produces: attribute `RegisteredSave` · `abstract class BaseSaveCollection : ScriptableObject, ISaveCollection` — `Initialize()` · `FlushAll()` · `Flush(ISaveEntry)` · `Entries` · `AutosaveIntervalSeconds` · `const string KeyPrefix` · `protected virtual ResetDerivedState()` · `protected virtual OnEntriesLoaded()`.

**Quyết định thiết kế:**

| Quyết định | Lý do |
|---|---|
| ScriptableObject, không MonoBehaviour | Không cần GameObject ở scene entry, sống qua đổi scene, key hiện trong Inspector của một asset. Cùng khuôn `BaseRemoteConfigCollection` |
| `abstract`, **không** mang `[Service]`, `[CreateAssetMenu]`, đường dẫn Resources | §0.10 ③; `[CreateAssetMenu]` trên abstract là menu item luôn thất bại |
| `KeyPrefix` ở lại SDK | Wire format, không phải cấu hình: mỗi dự án tự đặt là mời mỗi dự án tự tạo một lần đổi không hồi được |
| `GetEntryFields()` quét `NonPublic \| Instance` | Khớp `BaseRemoteConfigCollection`; §0.10 ④ |
| `Initialize()` public **và** `EnsureInitialized()` ở mọi cửa công khai | §0.9 — hai lối vào, một thân idempotent |
| `ScanEntries()` là **một** thân cho `Initialize` và nút `Validate keys` | Hai bản kiểm sẽ lệch, kiểu nút báo sạch mà runtime bỏ entry |
| Kiểm ba thứ: field chưa gán · key rỗng · trùng key — mỗi lỗi `LogError` nêu **tên field** | Tên field là thứ developer nhìn thấy trong Inspector; key thôi không đủ để tìm |
| Trùng key → **bỏ entry sau**, giữ entry trước | Entry trước là cái đã có dữ liệu ngoài đời |
| `FlushAll()` và `Flush(entry)` cùng gọi `FlushInternal(only)` | Cửa hẹp chạy trong thân cửa rộng — không thể một ngày `PlayerPrefs.Save()` chỉ còn ở một bên |
| `Flush(entry)` từ chối entry không sở hữu | Ghi entry chưa đăng ký là tạo khoá không ai load lại lúc boot — thường là dấu hiệu quên `[RegisteredSave]` |
| `PlayerPrefs.Save()` gọi **một lần** mỗi lượt, chỉ khi có entry vừa ghi | `Save()` ghi cả kho; flush rỗng không được chạm đĩa |
| `pendingClear` là field tái dùng, `Clear()` trong `finally` | Không alloc theo chu kỳ; lượt sau bắt đầu từ rỗng kể cả khi persist ném |
| Cache `List<FieldInfo>`, vòng lặp tay | Chạy một lần mỗi phiên nên LINQ cũng đủ rẻ; viết như `BaseRemoteConfigCollection` để hai bản đối chiếu được |
| Cặp hook `ResetDerivedState()` / `OnEntriesLoaded()` | §0.7 — phải đủ **cả hai** nửa |
| `Entries` không hứa thứ tự khai báo | `Type.GetFields` không đảm bảo thứ tự |
| Flush khi 0 entry → `LogWarning` một lần | Hệ save không được no-op im lặng (bất biến ⑤) |

**Editor setup:** không có — type abstract không tạo được asset; asset thuộc Task 5.

- [ ] **Step 1: `BaseSaveCollection.cs`**

```csharp
using System;
using System.Collections.Generic;
using System.Reflection;
using Horcrux.Runtime.Abstractions.Persistence;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Horcrux.Runtime.Implementations.Persistence
{
    /// <summary>Marks a SaveEntry field on the project's save collection as owned by it. Fields without it are ignored.</summary>
    [AttributeUsage(AttributeTargets.Field)]
    public sealed class RegisteredSave : Attribute { }

    /// <summary>Owns every saved value: scans the declared entries, loads them, autosaves, flushes on pause and quit. Contract: lose at most one autosave cycle.</summary>
    /// <remarks>Derive one sealed class in the project's assembly; it carries [Service], [CreateAssetMenu] and the Resources path.</remarks>
    public abstract class BaseSaveCollection : ScriptableObject, ISaveCollection
    {
        /// <summary>Sits in front of every entry key. Wire format: changing it orphans every save already out there.</summary>
        // PlayerPrefs is one flat namespace shared with remote config caches, ad and analytics SDKs.
        public const string KeyPrefix = "save.";

        [SerializeField, Min(1f), Tooltip("Autosave period in seconds. A killed app loses at most one of these.")]
        private float autosaveIntervalSeconds = 5f;

        private readonly List<ISaveEntry> entries = new();
        private readonly List<ISaveEntry> pendingClear = new();   // written this pass, not yet on storage
        private List<FieldInfo> cachedEntryFields;
        private SaveDriver driver;
        private bool isInitialized;
        private bool warnedEmptyOnce;

        public float AutosaveIntervalSeconds => autosaveIntervalSeconds;

        public IReadOnlyList<ISaveEntry> Entries
        {
            get
            {
                EnsureInitialized();
                return entries;
            }
        }

        public void Initialize()
        {
            ScanEntries(entries);

            for (int i = 0; i < entries.Count; i++)
                entries[i].ResetRuntimeState();

            ResetDerivedState();       // before the load: subclass caches are from a dead session
            isInitialized = true;      // before the load: nothing below may re-enter a guarded member
            LoadAll();
            OnEntriesLoaded();         // after the load: every entry holds what storage had
            EnsureDriver();
        }

        /// <summary>Override to drop anything cached from entry values. Runs on every Initialize, BEFORE the load.</summary>
        /// <remarks>Both halves are required: clearing only after the load leaves first-run caches stale.</remarks>
        protected virtual void ResetDerivedState() { }

        /// <summary>Override to rebuild those caches. Runs once per Initialize, after every entry has its stored value.</summary>
        protected virtual void OnEntriesLoaded() { }

        public void FlushAll() => FlushInternal(null);

        public void Flush(ISaveEntry entry)
        {
            EnsureInitialized();

            if (entry == null)
            {
                Debug.LogError("[Save] Flush(null).", this);
                return;
            }

            if (!entries.Contains(entry))
            {
                // An entry the collection does not own would land under a key nothing loads back at boot.
                Debug.LogError($"[Save] Flush of '{entry.Key}', which this collection does not own — " +
                               $"is its field missing [{nameof(RegisteredSave)}], or did validation drop it?", this);
                return;
            }

            FlushInternal(entry);
        }

        private void EnsureInitialized()
        {
            if (isInitialized) return;
            Initialize();
        }

        /// <summary>The single scan behind Initialize and the Editor validation, so the button rejects exactly what runtime rejects.</summary>
        /// <returns>How many declared fields were rejected.</returns>
        private int ScanEntries(List<ISaveEntry> into)
        {
            into.Clear();
            List<FieldInfo> fields = GetEntryFields();
            var keyOwners = new Dictionary<string, string>(fields.Count);   // key -> field name; boot frequency
            int rejected = 0;

            for (int i = 0; i < fields.Count; i++)
            {
                FieldInfo field = fields[i];
                var entry = (ISaveEntry)field.GetValue(this);

                if (entry == null)
                {
                    Debug.LogError($"[Save] Field '{field.Name}' is marked [{nameof(RegisteredSave)}] but holds nothing.", this);
                    rejected++;
                    continue;
                }

                if (string.IsNullOrEmpty(entry.Key))
                {
                    Debug.LogError($"[Save] Field '{field.Name}' has an empty key — fill it in the Inspector.", this);
                    rejected++;
                    continue;
                }

                if (keyOwners.TryGetValue(entry.Key, out string owner))
                {
                    Debug.LogError($"[Save] Key '{entry.Key}' is on both '{owner}' and '{field.Name}'. " +
                                   $"Keeping '{owner}'; two entries on one key overwrite each other.", this);
                    rejected++;
                    continue;
                }

                keyOwners.Add(entry.Key, field.Name);
                into.Add(entry);
            }

            return rejected;
        }

        /// <summary>The entry fields the derived class declares, cached for the session.</summary>
        /// <remarks>NonPublic only, matching BaseRemoteConfigCollection: entry fields are private by convention.</remarks>
        private List<FieldInfo> GetEntryFields()
        {
            if (cachedEntryFields != null) return cachedEntryFields;

            FieldInfo[] allFields = GetType().GetFields(BindingFlags.NonPublic | BindingFlags.Instance);
            var entryFields = new List<FieldInfo>();

            for (int i = 0; i < allFields.Length; i++)
            {
                if (allFields[i].IsDefined(typeof(RegisteredSave), false))
                    entryFields.Add(allFields[i]);
            }

            return cachedEntryFields = entryFields;
        }

        private void LoadAll()
        {
            for (int i = 0; i < entries.Count; i++)
            {
                ISaveEntry entry = entries[i];
                string storageKey = KeyPrefix + entry.Key;

                if (!PlayerPrefs.HasKey(storageKey)) continue;      // first run — the entry keeps its default

                string payload = PlayerPrefs.GetString(storageKey);
                if (string.IsNullOrEmpty(payload)) continue;

                try
                {
                    entry.ReadPayload(payload);
                }
                catch (Exception e)
                {
                    // One broken entry must not block the player or take the other entries down.
                    Debug.LogError($"[Save] Reading entry '{entry.Key}' failed — using its default; " +
                                   "the stored value is overwritten on the next flush.", this);
                    Debug.LogException(e, this);
                }
            }
        }

        /// <summary>The one flush body. <paramref name="only"/> null flushes everything; otherwise just that entry.</summary>
        private void FlushInternal(ISaveEntry only)
        {
            EnsureInitialized();

            if (entries.Count == 0)
            {
                if (!warnedEmptyOnce)
                {
                    warnedEmptyOnce = true;              // a save system must never no-op in silence
                    Debug.LogWarning("[Save] Flush with no entry registered — is any field marked " +
                                     $"[{nameof(RegisteredSave)}], and did they pass validation?", this);
                }
                return;
            }

            // Phase one: hand every dirty payload to PlayerPrefs. Dirty stays on — this is still memory.
            for (int i = 0; i < entries.Count; i++)
            {
                ISaveEntry entry = entries[i];
                if (only != null && !ReferenceEquals(entry, only)) continue;
                if (!entry.IsDirty) continue;

                try
                {
                    PlayerPrefs.SetString(KeyPrefix + entry.Key, entry.WritePayload());
                    pendingClear.Add(entry);
                }
                catch (Exception e)
                {
                    Debug.LogError($"[Save] Writing entry '{entry.Key}' failed — it stays dirty and retries next cycle.", this);
                    Debug.LogException(e, this);
                }
            }

            if (pendingClear.Count == 0) return;

            // Phase two: the only call that reaches storage. Dirty clears once it lands, never before.
            try
            {
                PlayerPrefs.Save();
                for (int i = 0; i < pendingClear.Count; i++) pendingClear[i].ClearDirty();
            }
            catch (Exception e)
            {
                Debug.LogError("[Save] PlayerPrefs.Save() failed — every entry stays dirty and retries next cycle.", this);
                Debug.LogException(e, this);
            }
            finally
            {
                pendingClear.Clear();
            }
        }

        private void EnsureDriver()
        {
            if (driver != null) return;
            driver = SaveDriver.Spawn(this);
        }
    }
}
```

- [ ] **Step 2: Kiểm chứng** (bảng input → kỳ vọng; chạy thật ở Task 5):

| Input | Kỳ vọng |
|---|---|
| Play lần đầu, PlayerPrefs trống | không log lỗi, mọi entry mang mặc định, `IsDirty == false` |
| Gán `Value`, chờ hết một chu kỳ | `IsDirty == false`, `Print all payloads` thấy JSON đúng |
| Gán `Value`, **không** chờ | `IsDirty == true`, payload đang lưu vẫn là giá trị cũ |
| Hai field cùng key | `LogError` nêu **cả hai tên field**, field sau bị bỏ, field trước chạy bình thường |
| Field `[RegisteredSave]` key rỗng | `LogError` nêu tên field, field đó bị bỏ, field khác vẫn chạy |
| Payload bị ghi rác trước khi Play | vào game bình thường, `LogError` nêu đúng key, entry đó về mặc định, **entry khác load đúng** |
| `FlushAll` không gì dirty | không gọi `PlayerPrefs.Save()`, không log |
| `FlushAll` khi 0 entry hợp lệ | `LogWarning` đúng một lần cả phiên |
| `Flush(entry)` hợp lệ đang dirty | chỉ entry đó `SetString`, `Save()` một lần, chỉ entry đó hết dirty |
| `Flush(entry)` với `SaveEntry` mới `new` ngoài collection | `LogError` nêu key, không ghi gì |
| `FlushAll` trước khi ai gọi `Initialize` | tự initialize, load đúng, không `NullReferenceException` |
| `Initialize()` hai lần | như một lần: không nhân đôi entry, không driver thứ hai |
| Subclass override hai hook, log một dòng mỗi hook | thứ tự `ResetDerivedState` → (load) → `OnEntriesLoaded`, cả hai chạy **mỗi** `Initialize` |
| Subclass cache lookup từ entry; Play → đổi → Stop → Play (domain reload tắt) | lookup dựng lại từ giá trị vừa load, không mang số phiên trước |

- [ ] **Step 3: Commit** — `feat(sdk): add BaseSaveCollection (scan, validate, load, two-phase flush)`

---

### Task 3: `SaveDriver` — autosave, pause, quit

**Files:**
- Create: `Assets/Horcrux/Runtime/Implementations/Foundations/Persistence/SaveDriver.cs`

**Interfaces:**
- Consumes: `ISaveCollection` · `BaseSaveCollection` · UniTask.
- Produces: `SaveDriver : MonoBehaviour` — `internal static Spawn(BaseSaveCollection)`.

**Quyết định thiết kế:**

| Quyết định | Lý do |
|---|---|
| MonoBehaviour do collection tự dựng | ScriptableObject không có timer, pause hook, quit hook — ba thứ duy nhất driver tồn tại để cung cấp |
| Dựng **lazy** trong `Initialize`, không `[RuntimeInitializeOnLoadMethod]` | InitArgs khởi tạo service ở `BeforeSceneLoad`; một `BeforeSceneLoad` thứ hai chạy không xác định thứ tự so với nó |
| GameObject `[Save]` **hiện** trong Hierarchy, không `HideFlags` | Autosave đang chạy là thứ có thật; developer phải thấy được |
| `DelayType.Realtime` | Autosave không ngừng khi `timeScale = 0` |
| `destroyCancellationToken` | Loop chết theo GameObject; Unity 6 có sẵn, không tự nuôi `CancellationTokenSource` |
| Tick gọi thẳng `FlushAll` | Autosave, pause, quit, gọi tay đi chung một thân — bốn đường không lệch nhau |
| Giữ `ISaveCollection`, không tra service mỗi lần | Driver biết chủ của mình; tra lại là mở đường cho hai collection trong một phiên |

- [ ] **Step 1: `SaveDriver.cs`**

```csharp
using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Horcrux.Runtime.Abstractions.Persistence;
using UnityEngine;

namespace Horcrux.Runtime.Implementations.Persistence
{
    /// <summary>Gives the collection what a ScriptableObject cannot have: a timer, a pause hook and a quit hook.</summary>
    /// <remarks>A safety net with no ordering against other MonoBehaviours; SaveBootStep adds the ordered flush.</remarks>
    public sealed class SaveDriver : MonoBehaviour
    {
        private ISaveCollection collection;
        private float intervalSeconds;

        internal static SaveDriver Spawn(BaseSaveCollection owner)
        {
            // Visible on purpose: a running autosave loop is real, and a developer must be able to see it.
            var host = new GameObject("[Save]");
            DontDestroyOnLoad(host);

            var spawned = host.AddComponent<SaveDriver>();
            spawned.collection = owner;
            spawned.intervalSeconds = owner.AutosaveIntervalSeconds;
            return spawned;
        }

        private void Start() => AutosaveLoopAsync(destroyCancellationToken).Forget();

        private void OnApplicationPause(bool isPaused)
        {
            if (isPaused) collection.FlushAll();
        }

        private void OnApplicationQuit() => collection.FlushAll();

        private async UniTaskVoid AutosaveLoopAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                // Realtime: autosave must keep ticking while the game is paused with timeScale = 0.
                await UniTask.Delay(TimeSpan.FromSeconds(intervalSeconds), DelayType.Realtime,
                    cancellationToken: cancellationToken).SuppressCancellationThrow();

                if (cancellationToken.IsCancellationRequested) return;
                collection.FlushAll();
            }
        }
    }
}
```

- [ ] **Step 2: Kiểm chứng:**

| Input | Kỳ vọng |
|---|---|
| Play, mở Hierarchy | đúng **một** `[Save]` ở `DontDestroyOnLoad` |
| Đổi scene | `[Save]` còn nguyên, không sinh cái thứ hai |
| Gán giá trị rồi bấm Pause của Editor | payload cập nhật ngay, không chờ chu kỳ |
| `timeScale = 0`, chờ hết chu kỳ | autosave vẫn chạy |
| `Destroy` `[Save]` giữa phiên | loop dừng theo token, không exception |

- [ ] **Step 3: Commit** — `feat(sdk): add SaveDriver (autosave loop, pause and quit flush)`

---

### Task 4: Ba nút Editor trên `BaseSaveCollection`

**Files:**
- Modify: `BaseSaveCollection.cs` — thêm khối `#if UNITY_EDITOR` cuối class.

**Interfaces:**
- Consumes: `ScanEntries` · `KeyPrefix` · Odin `[Button]`, `[GUIColor]` · `UnityEditor.EditorUtility`.
- Produces: chỉ nút Inspector.

**Quyết định thiết kế:**

| Quyết định | Lý do |
|---|---|
| `ValidateKeys` gọi chính `ScanEntries` | Nút phải báo **đúng** thứ runtime sẽ bỏ |
| Ba nút, không `OnValidate` | Lúc đang thêm entry key luôn rỗng — cảnh báo nền hiện đúng lúc chưa sửa được |
| `DeleteAllSaveData` hỏi `DisplayDialog` | Xoá dữ liệu là thao tác khó đảo ngược |
| `DeleteAllSaveData` chỉ xoá khoá **đang khai**, và nói ra | `PlayerPrefs` không liệt kê được khoá; báo con số thật hơn để developer tin đã sạch |
| `PrintAllPayloads` in độ dài từng payload + tổng | Con số duy nhất trả lời "đã chạm vài chục KB chưa" |
| Cả ba chạy **ngoài Play mode** | Đọc field qua reflection, không đọc `entries` — trùng key lộ lúc đang dựng |
| Ba nút ở base, không ở subclass | Chỉ chạm `ScanEntries` và `KeyPrefix`; Odin vẽ `[Button]` của base trên asset dẫn xuất |
| `using Sirenix.OdinInspector;` ngoài `#if UNITY_EDITOR` | Khớp `BaseRemoteConfigCollection`; `using` không dùng trong build vô hại |

- [ ] **Step 1: thêm vào cuối `BaseSaveCollection`, trước `}` của class**

```csharp
#if UNITY_EDITOR
        [Button, GUIColor("cyan")]
        private void ValidateKeys()
        {
            // The very scan Initialize runs, so a clean result here means a clean load at runtime.
            var scratch = new List<ISaveEntry>();
            int rejected = ScanEntries(scratch);

            if (rejected == 0)
                Debug.Log($"[Save] {scratch.Count} entries — no duplicate key, no empty key, no unassigned field.", this);
            else
                Debug.LogError($"[Save] {rejected} of {scratch.Count + rejected} declared entries were rejected; see the errors above.", this);
        }

        [Button]
        private void PrintAllPayloads()
        {
            var scratch = new List<ISaveEntry>();
            ScanEntries(scratch);

            var report = new System.Text.StringBuilder("[Save] stored payloads\n");
            int total = 0;

            for (int i = 0; i < scratch.Count; i++)
            {
                string storageKey = KeyPrefix + scratch[i].Key;
                string payload = PlayerPrefs.HasKey(storageKey) ? PlayerPrefs.GetString(storageKey) : null;
                int length = payload?.Length ?? 0;
                total += length;
                report.AppendLine($"  {storageKey}  [{length} chars]  {payload ?? "(nothing stored yet)"}");
            }

            report.Append($"  TOTAL {total} chars across {scratch.Count} entries.");
            Debug.Log(report.ToString(), this);
        }

        [Button, GUIColor("red")]
        private void DeleteAllSaveData()
        {
            if (!UnityEditor.EditorUtility.DisplayDialog(
                    "Delete all save data?",
                    "Removes the stored value of every entry this collection declares. This cannot be undone.",
                    "Delete", "Cancel"))
                return;

            var scratch = new List<ISaveEntry>();
            ScanEntries(scratch);
            int deleted = 0;

            for (int i = 0; i < scratch.Count; i++)
            {
                string storageKey = KeyPrefix + scratch[i].Key;
                if (!PlayerPrefs.HasKey(storageKey)) continue;
                PlayerPrefs.DeleteKey(storageKey);
                deleted++;
            }

            PlayerPrefs.Save();

            // PlayerPrefs cannot list its keys, so values under keys no field declares any more are unreachable.
            Debug.Log($"[Save] Deleted {deleted} of {scratch.Count} declared entries. Values stored under keys no " +
                      "field declares any more are unreachable and stay. Enter Play mode to see defaults.", this);
        }
#endif
```

- [ ] **Step 2: Kiểm chứng** (không cần Play):

| Input | Kỳ vọng |
|---|---|
| `Validate keys` khi mọi thứ hợp lệ | một dòng `Log` nêu đúng số entry |
| Hai field cùng key → `Validate keys` | `LogError` nêu cả hai tên field, không cần Play |
| Xoá key một field → `Validate keys` | `LogError` nêu tên field đó |
| `Print all payloads` khi chưa từng Play | mọi dòng `(nothing stored yet)`, `TOTAL 0 chars` |
| Play, đổi giá trị, chờ flush, Stop → `Print all payloads` | JSON đúng giá trị và độ dài thật |
| `Delete all save data` → `Cancel` | không xoá, không log |
| `Delete all save data` → `Delete` → Play | mọi entry về mặc định |

- [ ] **Step 3: Commit** — `feat(sdk): add save collection editor buttons (validate, print, delete)`

---

### Task 5: Cặp file của dự án + demo + nghiệm thu

Hệ chạy được lần đầu ở đây. `IGameSave.cs` và `GameSave.cs` là **cặp file thật** mọi dự án dùng SDK phải viết; `DemoSaveDriver.cs` xoá được sau nghiệm thu.

**Files** — cả ba trong assembly của dự án (gợi ý `Assets/FelixFelicis/Implements/Save/`):
- Create: `IGameSave.cs` · `GameSave.cs` · `DemoSaveDriver.cs`
- Scene demo (Editor setup dưới).

**Interfaces:**
- Consumes: `ISaveCollection` · `SaveEntry<T>` · `IService<T>` · `BaseSaveCollection` · `RegisteredSave`.
- Produces: `IGameSave` (1 property mỗi entry) · `GameSave` (sealed) · `DemoSaveDriver : MonoBehaviour`.

**Quyết định thiết kế:**

| Quyết định | Lý do |
|---|---|
| `IGameSave : ISaveCollection, IService<IGameSave>` | Derive contract để game gọi `Flush`/`FlushAll`; derive `IService<chính mình>` để `Service` không nhập nhằng (§0.10 ②) |
| **Một** dòng `[Service]`, dưới `IGameSave` | Khớp `GameRemoteConfigCollection`; SDK không tra `ISaveCollection` |
| `sealed partial class` + `partial interface`, chia theo feature | Đúng khuôn `GameRemoteConfigCollection`. `partial` **trong cùng assembly** không đụng §0.10 ① |
| `RESOURCE_PATH` SCREAMING_SNAKE | Khớp `GameRemoteConfigCollection.RESOURCE_PATH` |
| `[Splitter("…")]` gom nhóm Inspector | Đồ có sẵn; không dựng cơ chế gom nhóm thứ hai |
| `sealed` | Không có implementation thứ hai |
| Hai file, không một | `IGameSave` là thứ call site đọc; `GameSave` là chi tiết. IntelliSense ở call site chỉ thấy phần nó cần |
| Feature cần đọc nhiều entry riêng → tách **interface**, không tách asset | `interface IEconomySave { SaveEntry<int> Coins { get; } }`, rồi `IGameSave : ISaveCollection, IEconomySave, IService<IGameSave>`. ⚠️ `IEconomySave` **không** derive `IService<>` (CS0229). Muốn tra riêng: thêm `[Service(typeof(IEconomySave), ResourcePath = RESOURCE_PATH)]`, đọc `IService<IEconomySave>.Service` |
| `DemoProgress` khai trong `IGameSave.cs` | Demo cho gọn; dự án thật đặt model trong thư mục của feature sở hữu nó |
| Model là `class` dữ liệu thuần, public field | `JsonConvert` là đường duy nhất. Không `UnityEngine.Object`; không struct Unity (`Vector3.normalized` làm serializer đệ quy vô hạn) |

- [ ] **Step 1: `IGameSave.cs`**

```csharp
using System.Collections.Generic;
using Horcrux.Runtime.Abstractions;
using Horcrux.Runtime.Abstractions.Persistence;

namespace FelixFelicis.Save
{
    /// <summary>Every value this game saves, typed. Read one through IGameSave.Service.</summary>
    /// <remarks>Adding an entry: one property here, one field on GameSave. Partial: one file pair per feature.</remarks>
    public partial interface IGameSave : ISaveCollection, IService<IGameSave>
    {
        SaveEntry<DemoProgress> DemoProgress { get; }
        SaveEntry<bool> DemoHasRated { get; }
    }

    /// <summary>Demo model — plain data, public fields. A real save model looks exactly like this, in its own file.</summary>
    [System.Serializable]
    public sealed class DemoProgress
    {
        public int coins;
        public int currentLevel = 1;
        public List<string> unlockedSkins = new();
    }
}
```

- [ ] **Step 2: `GameSave.cs`**

```csharp
using Horcrux.Runtime.Abstractions.Persistence;
using Horcrux.Runtime.Implementations.Persistence;
using Horcrux.Runtime.Utilities;
using Sisus.Init;
using UnityEngine;

namespace FelixFelicis.Save
{
    /// <summary>This game's saved values. Lives in the game assembly so no save model reaches the SDK.</summary>
    /// <remarks>One Service registration, under this project's own interface. Partial, matching IGameSave.</remarks>
    [Service(typeof(IGameSave), ResourcePath = RESOURCE_PATH)]
    [CreateAssetMenu(menuName = "FelixFelicis/SaveCollection", fileName = "SaveCollection")]
    public sealed partial class GameSave : BaseSaveCollection, IGameSave
    {
        // Must match where the asset sits under a Resources folder, or the service never resolves.
        private const string RESOURCE_PATH = "Config/SaveCollection";

        [Splitter("Demo")]
        [RegisteredSave, SerializeField] private SaveEntry<DemoProgress> demoProgress;
        [RegisteredSave, SerializeField] private SaveEntry<bool> demoHasRated;

        public SaveEntry<DemoProgress> DemoProgress => demoProgress;
        public SaveEntry<bool> DemoHasRated => demoHasRated;
    }
}
```

- [ ] **Step 3: `DemoSaveDriver.cs`**

```csharp
using Horcrux.Runtime.Abstractions.Persistence;
using Horcrux.Runtime.Implementations.Persistence;   // BaseSaveCollection.KeyPrefix, for the corrupt-payload case
using UnityEngine;

namespace FelixFelicis.Save
{
    /// <summary>Acceptance driver: change values and watch load, autosave and flush through the log. Not for a real game.</summary>
    public sealed class DemoSaveDriver : MonoBehaviour
    {
        private IGameSave save;

        private void Start()
        {
            save = IGameSave.Service;
            save.DemoProgress.Changed += OnProgressChanged;
            LogState();
        }

        private void OnDestroy()
        {
            if (save != null) save.DemoProgress.Changed -= OnProgressChanged;
        }

        [ContextMenu("Add 10 coins")]
        private void AddCoins()
        {
            save.DemoProgress.Value.coins += 10;
            save.DemoProgress.MarkDirty();          // no serializing here — that waits for the flush
        }

        [ContextMenu("Level up")]
        private void LevelUp()
        {
            save.DemoProgress.Value.currentLevel++;
            save.DemoProgress.MarkDirty();
        }

        [ContextMenu("Toggle 'has rated'")]
        private void ToggleRated()
        {
            save.DemoHasRated.Value = !save.DemoHasRated.Value;      // assigning marks dirty on its own
            Debug.Log($"[DemoSave] hasRated = {save.DemoHasRated.Value}", this);
        }

        [ContextMenu("Flush everything now")]
        private void FlushEverything() => save.FlushAll();

        [ContextMenu("Flush ONLY progress")]
        private void FlushProgressOnly() => save.Flush(save.DemoProgress);

        [ContextMenu("Flush an entry the collection does not own (expect an error)")]
        private void FlushForeignEntry() => save.Flush(new SaveEntry<int>());

        /// <summary>Breaks the stored payload on purpose, to prove a corrupt entry still lets the player in.</summary>
        [ContextMenu("Corrupt the progress payload")]
        private void CorruptPayload()
        {
            PlayerPrefs.SetString(BaseSaveCollection.KeyPrefix + save.DemoProgress.Key, "{ garbage");
            PlayerPrefs.Save();
            Debug.Log("[DemoSave] Payload broken — stop Play and Play again to see it handled.", this);
        }

        private void OnProgressChanged(DemoProgress progress) => LogState();

        private void LogState()
            => Debug.Log($"[DemoSave] coins={save.DemoProgress.Value.coins} " +
                         $"level={save.DemoProgress.Value.currentLevel} " +
                         $"dirty={save.DemoProgress.IsDirty} rated={save.DemoHasRated.Value}", this);
    }
}
```

- [ ] **Step 4: Editor setup:**

1. Project window → `Create` → `FelixFelicis` → `SaveCollection`.
2. Đặt asset ở `Assets/<…>/Resources/Config/SaveCollection.asset` — phần sau `Resources/` phải khớp `RESOURCE_PATH`, sai là `Service.Get<>()` ném.
3. Inspector: key `demo_progress` cho `Demo Progress`, `demo_has_rated` cho `Demo Has Rated`; đặt default; `Autosave Interval Seconds` (mặc định 5).
4. Bấm `Validate keys` — phải sạch **trước khi** Play.
5. Scene mới `PersistenceDemo` → GameObject `[Demo]` + `DemoSaveDriver`.

- [ ] **Step 5: Nghiệm thu khuôn kế thừa** (chỉ Unity kiểm được; hệ quả của §0.10):

| Kiểm | Cách | Kỳ vọng |
|---|---|---|
| Unity serialize `SaveEntry<T>` trên subclass | mở asset | vẽ đủ `key`, `defaultValue` của hai entry, **sửa được** |
| `value`, `isDirty` hiện mà không ghi vào asset | mở asset ngoài Play; Play → đổi → Stop | thấy cả hai **xám**; asset không bị đánh dấu dirty; `git status` không thấy `SaveCollection.asset` đổi — thấy đổi là `value` đã lỡ `[SerializeField]` |
| `isDirty` đọc được lúc Play | Play, `Add 10 coins`, nhìn asset | `Is Dirty` bật `true` ngay, tự về `false` sau chu kỳ |
| Nút của base hiện trên asset dẫn xuất | mở asset | ba nút `Validate keys`, `Print all payloads`, `Delete all save data` |
| Nút của `SaveEntry<T>` hiện trong từng entry | bung một entry | ô `payloadToImport` + nút `Import payload`; bấm ngoài Play → `LogWarning` |
| Entry ở file `partial` khác vẫn vào danh sách (§0.10 ④) | tách `demoHasRated` sang `GameSave.Demo.cs` → `Validate keys` | vẫn **2** entry |
| `IGameSave.Service` resolve với **một** `[Service]` (§0.10 ② ③) | `Start` của `DemoSaveDriver` | trả asset, không exception — exception nghĩa là `RESOURCE_PATH` lệch |
| `SaveBootStep` lấy collection không qua service locator | xem ô reference; `grep -rn --include=*.cs "IService<ISaveCollection>" Assets/Horcrux` | ô trỏ vào asset; grep không ra gì |

- [ ] **Step 6: Nghiệm thu tự động** (agent chạy — thứ mắt người sót):

| Kiểm | Cách | Kỳ vọng |
|---|---|---|
| Round-trip mọi kiểu dùng thật | `SaveEntry<T>` với `int` · `bool` · `float` · `string` · enum · `DemoProgress` có `List`: gán → `WritePayload` → `ReadPayload` vào entry mới | bằng nhau từng field |
| **Không cửa ghi thứ hai** | `grep -rn "PlayerPrefs.Set" Assets/Horcrux/Runtime` | Persistence runtime: **đúng một** kết quả trong `FlushInternal`; kết quả trong nút Editor đếm riêng. `DemoSaveDriver` ngoài `Assets/Horcrux`, kiểm riêng |
| Không còn tên contract đã xoá | `grep -rn "ISaveUnit\|SaveUnit\|Prefs<\|PrefsBool\|PrefsInt\|SaveRegistry" Assets` | không kết quả |
| **SDK không mang domain** | `grep -rn --include=*.cs "FelixFelicis\|DemoProgress\|IGameSave\|GameSave" Assets/Horcrux` và `find . -name "*.asmref"` | không kết quả. Giới hạn `*.cs` vì plan này nằm trong `Assets/Horcrux` và nhắc các tên đó. **Thay tên theo host** — color-loop dùng namespace theo đường dẫn (`_TheGame.Runtime.…`) |
| `defaultValue` không bị mutate | lấy `Value`, mutate sâu (`coins = 999`, `unlockedSkins.Add`), `Initialize()` lại, đọc `Value` | về đúng mặc định; asset không dirty |
| Dirty không tắt trước khi xuống đĩa | dựng ca `PlayerPrefs.Save()` ném | mọi entry vừa ghi **còn** dirty, log rõ, lượt sau thử lại |

- [ ] **Step 7: Kịch bản chơi thử** (developer chạy):

| Mục | Nội dung |
|---|---|
| Vào đâu | Scene `PersistenceDemo`, Play. **Mở sẵn asset `SaveCollection` trong Inspector** — `Value` và `Is Dirty` hiện ngay đó |
| Làm gì | ① `Add 10 coins` ×3 → `Print all payloads` **ngay** · ② chờ >5s → `Print all payloads` · ③ Stop → Play · ④ `Toggle 'has rated'` → Stop → Play · ⑤ `Add 10 coins` → `Flush ONLY progress` · ⑥ `Flush an entry the collection does not own` · ⑦ `Corrupt the progress payload` → Stop → Play · ⑧ `Add 10 coins` → Pause của Editor · ⑨ đổi key `Demo Has Rated` thành `demo_progress` → `Validate keys` |
| Nhìn cái gì | ① `dirty=True` ngay, payload in ra vẫn **cũ** · ② payload `coins=30`, `dirty=False` · ③ log đầu phiên mới `coins=30` · ④ `rated` giữ qua phiên · ⑤ chỉ payload `demo_progress` đổi · ⑥ `LogError` nêu key, nhắc `[RegisteredSave]`, không ghi · ⑦ vào được demo, `LogError` nêu `demo_progress`, `coins=0`, `rated` **vẫn đúng** · ⑧ payload cập nhật ngay lúc pause · ⑨ `LogError` cả hai tên field, **không cần Play** |
| Khác trước ra sao | Giá trị lẻ như `rated` từng đi đường riêng không cờ dirty, `PlayerPrefs.Save()` chỉ chạy khi có model dirty → ca ④ với app bị kill là mất. Trùng key từng chỉ lộ lúc Play và chỉ giữa model → ca ⑨ giờ lộ lúc authoring, phủ mọi entry |
| Dấu hiệu hỏng | coins về 0 sau restart · `dirty=True` còn mãi (`ClearDirty` không chạy) · ① payload đổi ngay khi add (serialize bám nhịp tương tác) · ④ `rated` về mặc định · ⑦ exception đỏ không ai bắt, hoặc `rated` cũng mất · ② payload không đổi (thiếu `Save()`) · ⑨ phải Play mới thấy lỗi · **ô `Value` sửa được** (thiếu `[ReadOnly]`) · **`git status` thấy `SaveCollection.asset` đổi** (`value` bị serialize) |

- [ ] **Step 8: Commit** — `feat(game): add GameSave collection + persistence acceptance scene`

---

### Task 6: `SaveBootStep` — flush có thứ tự, đường wire chuẩn

**Files:**
- Create: `Assets/Horcrux/Runtime/Implementations/Composites/Persistence/SaveBootStep.cs`

**Interfaces:**
- Consumes: `BaseBootStep` (`Abstractions/Foundations/Bootstrap/BaseBootStep.cs`) · `BaseSaveCollection`.
- Produces: `SaveBootStep : BaseBootStep` — một `[SerializeField]`, không API công khai.

Hệ vẫn chạy nếu thiếu step này (`EnsureInitialized` + `SaveDriver` phủ), nhưng chỉ nó cho được hai thứ: **flush có thứ tự** ở pause/quit, và **`Initialize()` cố định trong pha boot** — không có nó, thời điểm `Initialize` phụ thuộc ai chạm collection trước, và listener đăng ký trước đó bị `ResetRuntimeState()` xoá **im lặng**.

**Quyết định thiết kế:**

| Quyết định | Lý do |
|---|---|
| Nhánh **Composites** | Phụ thuộc cả Bootstrap lẫn Persistence; lõi Persistence ở Foundations vẫn chạy ở project không dùng Bootstrap |
| Nhận collection qua `[SerializeField]`, không service locator | "Step này flush collection nào" là quyết định lúc authoring, hiện trong Inspector. Phía dự án chỉ còn một `[Service]`, bẫy CS0229 biến mất |
| Field kiểu `BaseSaveCollection`, không `ISaveCollection` | Unity chỉ serialize reference `UnityEngine.Object`; field interface không có ô kéo thả |
| Ô trống → `LogError` rồi thoát, không ném | Lỗi cấu hình lộ ngay pha boot; ném sẽ chặn cả chain vì thứ `EnsureInitialized` vẫn cứu được |
| `InitializeAsync` gọi `Initialize()` | Trả chi phí reflection + load ở màn loading; lỗi save lộ trong boot; cố định thời điểm reset `Changed` |
| Hai đường flush cùng chạy | Idempotent: đường sau không thấy gì dirty, không chạm đĩa |
| `Order` **thấp** để flush **muộn** | `BootstrapRunner` init xuôi, pause/quit chạy `for (i = stepCount-1; i >= 0; i--)` → step init sớm nhất flush muộn nhất |

**Editor setup:**

1. Trên GameObject có `BootstrapRunner`: add `SaveBootStep`.
2. **Kéo asset `SaveCollection` vào ô `Collection`** — thiếu thì step `LogError` rồi thoát ở pha boot.
3. Đặt `Order` **thấp**.
4. Kéo `SaveBootStep` vào danh sách steps của `BootstrapRunner` nếu runner dùng danh sách tường minh.

- [ ] **Step 1: `SaveBootStep.cs`**

```csharp
using System.Threading;
using Cysharp.Threading.Tasks;
using Horcrux.Runtime.Abstractions.Bootstrap;
using UnityEngine;

namespace Horcrux.Runtime.Implementations.Persistence
{
    /// <summary>Moves the save load into boot and gives pause and quit an ORDERED flush: the runner walks steps backwards there, so an early step flushes last.</summary>
    /// <remarks>Also fixes when Initialize resets Changed, so "subscribe after boot" holds by construction.</remarks>
    public sealed class SaveBootStep : BaseBootStep
    {
        [SerializeField, Tooltip("The save collection to initialize at boot and flush on pause and quit.")]
        private BaseSaveCollection collection;

        public override UniTask InitializeAsync(CancellationToken ct)
        {
            // Pays the reflection scan and the load here, where a loading screen is up and a broken save is visible.
            if (HasCollection) collection.Initialize();
            return UniTask.CompletedTask;
        }

        public override void OnGoToBackground(bool inBackground)
        {
            if (inBackground && HasCollection) collection.FlushAll();
        }

        public override void OnAppQuit()
        {
            if (HasCollection) collection.FlushAll();
        }

        /// <summary>False with an error when the slot is empty — a setup mistake surfaced without killing boot.</summary>
        private bool HasCollection
        {
            get
            {
                if (collection != null) return true;

                Debug.LogError($"[Save] {nameof(SaveBootStep)} on '{name}' has no collection assigned — " +
                               "drag the save collection asset into its slot. Boot continues, but the load and " +
                               "the ordered flush are both skipped.", this);
                return false;
            }
        }
    }
}
```

- [ ] **Step 2: Kiểm chứng:**

| Input | Kỳ vọng |
|---|---|
| Play với `SaveBootStep` trong chain | load trong pha boot; `[Save]` xuất hiện ngay sau |
| `BaseBootStep` khác `Order` cao hơn ghi entry trong `OnGoToBackground` | giá trị đó **có** trong payload sau pause |
| Pause với cả driver lẫn step | flush hai lần, lần sau không chạm đĩa, không log thêm |
| Play **không** có `SaveBootStep` | hệ vẫn chạy; load ở lần chạm đầu |
| Ô `Collection` trống → Play | `LogError` nêu tên GameObject ngay boot, boot đi tiếp, load rơi về `EnsureInitialized` |
| Subscribe `Changed` trong `Start` của MonoBehaviour, có step | listener **còn sống** |
| `grep -rn --include=*.cs "IService<ISaveCollection>" Assets/Horcrux` | không kết quả |

- [ ] **Step 3: Commit** — `feat(sdk): add SaveBootStep for ordered flush and boot-time load`

---

## Ghi chú thực thi

**Nghiệm thu cuối = Task 5 Step 5, 6, 7.** Mục tiêu → bằng chứng: một-nơi-nhìn-thấy (`Print all payloads`) · trùng-key-lúc-authoring (ca ⑨) · mọi-giá-trị-trong-vòng-flush (ca ④ + grep "không cửa ghi thứ hai") · quên-khởi-tạo-không-mất (Task 2, hàng "`FlushAll` trước `Initialize`") · thêm-entry-không-sửa-SDK (`GameSave.cs`: 2 dòng cho một entry, 0 dòng trong `Assets/Horcrux`) · model-không-vào-SDK (Step 6, hàng "SDK không mang domain").

**Sau khi implement:** viết `Persistence.md` cạnh `Implementations/Foundations/Persistence/`, sinh `.html`. Soi theo cấu trúc `RemoteConfigSystem.md` (Hai phía · Đường đi của một giá trị · Vòng đời · Inspector · Nút Editor · Bẫy và quyết định thiết kế · Chữ ký · Cấu trúc file) để hai hệ đối chiếu được. Chuyển §0 sang mục quyết định thiết kế — bốn "đã sai một lần" (§0.4–0.7) không đọc ra được từ code; §0.10 là thứ người sau sẽ vô tình phá.

**Hai dòng bẫy phải có trong `Persistence.md` và XML doc:**
- `Changed` bị `ResetRuntimeState()` xoá → listener đăng ký trước `Initialize()` mất im lặng. Không detector nào phân biệt được listener phiên trước (hợp lệ) với listener bị xoá oan — cả hai chỉ là `Changed != null`. Chặn duy nhất bằng cấu trúc: `SaveBootStep`.
- `implicit operator T` bất đối xứng: `entry == null` so tham chiếu, `entry == s` (kiểu `T`) đi qua operator — bảng bẫy Task 1.

**Hợp đồng ra ngoài:** khoá `"save." + Key` và định dạng JSON payload. Đổi sau khi ship là mọi save cũ mồ côi, không có gì báo.

**Hệ dùng tiếp:** Audio (volume), Haptics, Economy (coin, lives), Rating, LiveOps. Không hệ nào tự khai entry — dự án khai trên `GameSave` rồi nối vào.

**⚠️ Cần developer phân xử — hệ SDK lấy giá trị save bằng cách nào.** Hai tài liệu nói ngược nhau, cả hai là ranh giới developer đặt:

| Nguồn | Nói gì | Hệ quả |
|---|---|---|
| Plan này, hàng "Ai gọi" | hệ SDK **nhận vào `SaveEntry<T>`** qua Init | module compile-depend vào Persistence; bê sang project không có Persistence là không biên dịch |
| `HapticSystem.md` · `AudioSystem.md` | module phơi **field thường** (`IsEnabled`, `IsSfxOn`), dự án đọc save rồi set vào lúc bootstrap — *"SDK cố tình không sở hữu hệ save"* | module không phụ thuộc Persistence; entry giữ bản gốc, glue một chiều qua `Changed` |

Chốt bản nào thì sửa **dòng cũ** ở "Ai gọi" và "Hệ dùng tiếp", không thêm dòng thứ hai. Hai tài liệu module đã qua vòng cắt phạm vi nên có vẻ mới hơn — suy đoán, không phải chốt.

**Khuôn chung với Remote Config** (`RemoteConfigSystem.md`): §0.10 · một `[Service]` dưới interface dự án · `sealed partial` + `partial interface` theo feature · `RESOURCE_PATH` · quét `NonPublic | Instance` vòng lặp tay · `[Splitter]` · magic method `protected virtual` · abstract base không `[CreateAssetMenu]`. Sửa một bên phải sửa bên kia.

**Ba chỗ Persistence cố ý khác Remote Config** — danh sách đóng, thêm chỗ thứ tư phải ghi lý do vào đây:

| Chỗ khác | Lý do |
|---|---|
| `EnsureInitialized()` ở mọi cửa công khai | quên `Initialize()` ở RC hồi được, ở Save thì không (§0.9) |
| Cặp hook `ResetDerivedState()` / `OnEntriesLoaded()` | RC chỉ có hook "sau" và chính chỗ hở đó sinh cờ mang trạng thái phiên trước (§0.7) |
| `[NonSerialized] value` + `[SerializeField] defaultValue` + `[ReadOnly]` | RC serialize giá trị fetch cho developer thấy; Save lưu tiến độ người chơi (§0.8). Khác ở trục serialize, không ở trục nhìn thấy; thêm `[ReadOnly]` vì `value` sửa tay là cửa no-op âm thầm, `fetched` của RC thì vô hại |

**Hai chỗ Remote Config nên học lại từ Persistence** — ngoài phạm vi plan, developer quyết: `IRemoteConfigCollection.RemoteConfigs` nên là `IReadOnlyList<>` thay `IEnumerable<>` · cache PlayerPrefs của `RemoteConfig<T>` nên có tiền tố `"rc."` (cache bỏ được, chỉ tốn một lượt fetch nguội — §0.1).

## Mở rộng sau

Rẻ, thêm không sửa cũ: nút xoá từng entry trên `SaveEntry<T>` · kho file trên đĩa khi chạm một trong ba giới hạn (sửa nội bộ `BaseSaveCollection`) · migration version khi có model đổi schema · cloud sync khi backend chuẩn chung.
