# Persistence Implementation Plan — SaveCollection + PlayerPrefs + Newtonsoft JSON

> **Loại tài liệu:** Plan — developer tự code lại để nắm logic. `.md` thiết kế + `.html` viết **sau** khi có source.
>
> **For agentic workers:** REQUIRED SUB-SKILL: superpowers:subagent-driven-development hoặc superpowers:executing-plans. Steps dùng checkbox (`- [ ]`).

**Goal:** Lưu tiến độ người chơi **có kiểu**, **quản lý tập trung tại một nơi nhìn thấy được**, **không mất khi app bị kill** (tối đa một chu kỳ autosave), **không god-blob**. Mọi thứ persist được — từ một cờ `bool` tới cả model tiến độ — đều là một `SaveEntry<T>`, và mọi `SaveEntry<T>` đều là field của một ScriptableObject duy nhất: `SaveCollection`. Collection lo quét, kiểm trùng key, load, autosave, flush và bước `PlayerPrefs.Save()`; entry chỉ giữ giá trị + cờ dirty + sự kiện on-change.

**Architecture:** 3 tầng, tổng **10 file** (3 contract + 3 impl + 1 composite + 3 demo).

```
Contract   (ISaveEntry, SaveEntry<T>,      key + default + dirty + on-change · CHỈ nói bằng chuỗi payload
            ISaveCollection)                cửa Initialize/FlushAll/Flush
Collection (SaveCollection + SaveDriver)    quét field · kiểm key · load · autosave · flush hai giai đoạn ·
                                            PlayerPrefs.Save() một lần mỗi lượt · hỏng → default + log
Game       (partial class + partial iface)  model + key + giá trị mặc định, khai lúc authoring
```

**Tech Stack:** C#, UniTask, `Sisus.Init` (`[Service]`), Odin Inspector (`[Button]`, `[GUIColor]` — DLL precompiled, mọi asmdef tự reference), Newtonsoft.Json (package `com.unity.nuget.newtonsoft-json`), PlayerPrefs, `System.Reflection`. Unity 6000.3. **Không** đụng asmdef của SDK, **không** thêm package nào, **không** Addressables, **không** toán.

## Global Constraints

| Ràng buộc | Giá trị |
|---|---|
| Namespace | `Horcrux.Runtime.Abstractions.Persistence` · `Horcrux.Runtime.Implementations.Persistence` |
| Ngôn ngữ trong code | Comment và XML doc viết **tiếng Anh**, khớp với toàn bộ `.cs` đang có trong SDK |
| Hiệu năng | Gán `Value` hoặc `MarkDirty` chạy theo nhịp **tương tác** — chỉ set cờ + phát event, **không** serialize. Serialize dồn về nhịp flush (autosave mặc định 5 giây + pause/quit) và chỉ chạm entry đang dirty. Reflection quét field và load: **một lần mỗi phiên**, có cache |
| Ngân sách dữ liệu | Tổng payload **vài chục KB**. `PlayerPrefs.Save()` ghi lại **toàn bộ** kho prefs mỗi lượt flush có entry dirty; vượt ngưỡng thì PlayerPrefs không còn là chỗ đúng nữa — xem mục "Ba giới hạn của cách lưu này". Con số thật lấy bằng nút `Print all payloads` ở Task 4 |
| SOLID | Collection chỉ biết `ISaveEntry`, không biết kiểu nào bên trong · entry không biết chỗ lưu, chỉ nói bằng chuỗi payload · hệ SDK **nhận vào** entry của mình, không tự khai · không type nào trong hệ mang ngữ nghĩa game |
| Editor-first | Key, giá trị mặc định và chu kỳ autosave đều là **cấu hình**, phơi ra Inspector. Kiểm trùng key chạy được **lúc authoring**, không cần Play |
| An toàn | Đọc fail → giữ giá trị mặc định + log, **không throw** · ghi fail → **giữ dirty**, log, chu kỳ sau thử lại · try/catch quanh **từng** callback `Changed` · autosave loop nhận `destroyCancellationToken` |
| Bất biến | ① key là **hợp đồng wire format** — chuỗi điền trong Inspector, không suy từ tên type hay tên field ② dirty reset ở **đúng một nơi** (collection), và chỉ **SAU** khi `PlayerPrefs.Save()` thành công ③ serialize chỉ xảy ra trong nhịp flush ④ **`PlayerPrefs.SetString` chỉ xuất hiện đúng một chỗ trong cả hệ** — không có cửa ghi thứ hai ⑤ không có đường "chạy no-op âm thầm": key trùng, key rỗng, field chưa gán, collection rỗng, ghi/đọc fail — đều có log |

## Ngữ cảnh đã chốt

| Nhóm | Chốt |
|---|---|
| **Ai gọi** | Game khai mọi `SaveEntry` trong partial class của `SaveCollection` (cặp partial class + partial interface, y khuôn `RCVariableCollection`) · gameplay và UI gán `Value`, hoặc mutate model rồi `MarkDirty()`, hoặc subscribe `Changed` · hệ SDK **nhận vào** entry của mình qua Init, không tự khai · `FlushAll` hệ tự gọi (autosave + pause/quit); game gọi `Flush(entry)` để chốt sổ một entry sớm |
| **Mục tiêu** | Một nơi duy nhất nhìn thấy mọi thứ game lưu · trùng key bắt được **lúc authoring** · mọi giá trị persist được đều nằm trong vòng flush, không có ngoại lệ · quên khởi tạo không làm mất tiến độ · thêm entry mới không sửa SDK và không đụng entry khác |
| **Ngân sách** | Gán hoặc `MarkDirty`: mỗi tương tác, phải rẻ — chỉ cờ + event · serialize + `PlayerPrefs.Save()`: mỗi chu kỳ autosave, pause, quit, và mỗi lần `Flush(entry)` · reflection + load: một lần mỗi phiên. Không hot path mỗi frame |
| **Ranh giới** | SDK: `SaveEntry<T>`, `SaveCollection`, `SaveDriver`, `SaveBootStep` tuỳ chọn, nút Editor. Game: model, key, giá trị mặc định, interval, và toàn bộ phần partial |
| **Hướng phát triển thật** | Chuyển kho sang file trên đĩa khi chạm một trong ba giới hạn ở mục ngay dưới. Entry chỉ nói bằng **chuỗi payload** và không biết `PlayerPrefs` tồn tại, nên việc đó sửa nội bộ `SaveCollection` — không đụng entry, không đụng code game |

**Những gì cố ý KHÔNG làm, kèm lý do** (*xoá nó đi thì hỏng ở đâu*):

| Không làm | Vì sao |
|---|---|
| Khái niệm `Prefs<T>` riêng cho giá trị lẻ | Giá trị lẻ và model chỉ khác nhau ở **một bước** — cách biến thành chuỗi. Dựng bộ máy thứ hai cho một bước là đặt giá trị lẻ ra **ngoài** cờ dirty và ngoài `PlayerPrefs.Save()`, tức là ra ngoài chính bảo đảm mà hệ tuyên bố |
| `ISerializer` | Hệ này có **đúng một** format. Interface cho một implementation là một lớp phải đọc mà đầu kia không có ai đứng |
| `ISaveStore` | Đúng một chỗ lưu đang có thật. Ranh giới "entry chỉ nói bằng chuỗi payload" đã đủ để đổi chỗ lưu sau này mà không đụng entry — đó là chỗ đáng phòng xa, và nó tốn 0 dòng |
| `Register()` gọi từ runtime | Khai báo là việc lúc authoring. Đăng ký lúc chạy là thứ sinh ra cả bốn chỗ hở: không ai liệt kê được, không ai kiểm trùng được, quên gọi thì im lặng |
| Kiểm hợp lệ chạy nền trong `OnValidate` | Trong lúc đang thêm entry thì key **luôn** rỗng và dữ liệu **luôn** chưa hợp lệ — cảnh báo sẽ hiện gần như toàn thời gian, vào đúng lúc chưa thể sửa. Đúng mà vô ích |
| Xoá save theo tiền tố | `PlayerPrefs` **không có API liệt kê khoá**, nên chỉ xoá được những khoá mà collection đang khai. Khoá mồ côi do đổi tên entry sống sót — ghi ra ở nút xoá, không giả vờ là đã xoá hết |
| Ghi nguyên tử (`.tmp` + đổi tên) | Ứng dụng không tự ghi file; cả kho prefs được nền tảng ghi một lượt |
| Crypto | Cả bốn repo khảo sát không dùng thật |
| Cloud sync | Nhu cầu thật nhưng backend chưa chuẩn chung |
| Migration version cho model | Chưa có model nào đổi schema |

**Khảo sát tái sử dụng:**

| Cái có sẵn | Kết luận |
|---|---|
| `IService<T>` (`Abstractions/Foundations/IService.cs`) | **Dùng lại** cho `ISaveCollection`. Save là hệ **bắt buộc** — thiếu asset trong `Resources/Config/` là lỗi cấu hình, phải lộ ngay lần Play đầu, nên `IService` (throw) chứ không `IOptionalService` |
| `RCVariableCollection` + `RCVariable<T>` + `RegisteredRCVar` | **Dùng lại khuôn**, không dùng lại code: cùng cơ chế `[Service(ResourcePath)]` + partial class + attribute + reflection quét field. Ba chỗ cố ý đảo ngược lại nằm ở bảng trong Task 1 — lý do gốc của chúng là *"cho developer thấy giá trị fetch về ngay trong asset"*, lý do đó không còn đúng khi thứ được lưu là dữ liệu của người chơi |
| `BootStep` + `BootstrapRunner` | **Dùng lại** cho `SaveBootStep` tuỳ chọn ở nhánh Composites. `BootStep` đã có sẵn `OnAppPause`/`OnAppQuit`, và runner fan-out **ngược thứ tự** cho cả hai — đó chính là thứ magic method của một MonoBehaviour lẻ không hứa được |
| `EventBus` (Utilities) | **Không dùng** — `Changed` là event nội bộ một entry, listener wire trực tiếp |
| `MonoSingleton` | **Không dùng** — đăng ký qua `[Service]`, như tiền lệ `BootstrapRunner` và `RCVariableCollection` |
| `ISaveUnit.cs` đang có | **Xoá** — mảnh còn lại của kiến trúc cũ, không có implementation nào |

## Ba giới hạn của cách lưu này

Đây là chỗ PlayerPrefs hết đủ. Biết trước thì không phải phát hiện lúc đã ship:

| Giới hạn | Nghĩa cụ thể | Nhận ra ở đâu |
|---|---|---|
| Cả kho là **một khối** | một lần hỏng là mất **mọi** khoá của game cùng lúc, kể cả khoá của những hệ không liên quan | không có ngưỡng — ràng buộc thường trực |
| Mỗi lượt flush ghi lại **toàn bộ** kho | vài KB thì không đáng kể; càng nhiều dữ liệu thì mỗi 5 giây càng đắt. Đây cũng là lý do `Flush(entry)` **không** rẻ hơn `FlushAll()` ở phần chạm đĩa | tổng payload vượt **vài chục KB** |
| Cả kho nằm trong RAM suốt phiên | trả phí bộ nhớ thường trực cho thứ chỉ đọc một lần lúc boot | cùng ngưỡng trên |

Con số thật lấy bằng nút `Print all payloads` ở Task 4 — nó in độ dài từng payload và tổng.

---

## §0. Chín ràng buộc thật

Không có toán. Năm sự thật của nền tảng và bốn bug có thật trong repo quyết định hình dạng code — đọc trước khi viết.

### 0.1. PlayerPrefs cũng là một file trên đĩa — biết nó là file nào thì mới đặt đúng ranh giới

PlayerPrefs không phải một kho lưu trữ khác loại với file. Nó **là** file; khác biệt duy nhất là hệ điều hành ghi hộ thay vì ứng dụng tự ghi:

| Platform | PlayerPrefs thực chất là | Ai xoá nó |
|---|---|---|
| Android | `SharedPreferences` — một file XML trong `/data/data/<package>/shared_prefs/` | gỡ app · "Clear Data" trong Settings |
| iOS | `NSUserDefaults` — một file plist trong `Library/Preferences/` | gỡ app |
| Windows (Editor) | registry, `HKCU\Software\<Company>\<Product>` | xoá tay bằng regedit |

Ba hệ quả đi thẳng vào thiết kế:

1. **Toàn bộ kho là một khối.** Mỗi lần persist là ghi lại **cả** file, và một file hỏng là mất **mọi** khoá của game cùng lúc. Đây là cái giá lớn nhất của cách lưu này.
2. **Toàn bộ kho nằm trong RAM suốt phiên.** Trên Android, `SharedPreferences` được đọc và parse XML **một lần ở lần truy cập đầu tiên** rồi giữ trong bộ nhớ.
3. **Không gian khoá là phẳng và dùng chung với mọi hệ khác.** `RCVariable` trong chính SDK này ghi cache của remote config vào PlayerPrefs bằng **key thô, không tiền tố**; SDK quảng cáo và analytics cũng ghi vào đó. Tiền tố `"save."` vì thế không phải trang trí — nó là thứ chặn một firebase key trùng tên đè lên tiến độ người chơi. Đổi tiền tố này sau khi ship là mọi save đang có ngoài đời thành mồ côi.

### 0.2. Android kill không báo trước — `OnApplicationQuit` không phải chỗ dựa

Trên Android, người chơi swipe-kill hoặc hệ điều hành thu hồi RAM thì process chết **không chạy** `OnApplicationQuit`; tín hiệu tin được cuối cùng là `OnApplicationPause(true)`. Vì vậy hợp đồng của hệ là **"mất tối đa MỘT chu kỳ autosave"**, không phải "không bao giờ mất": autosave chu kỳ là lưới đỡ chính, flush ở pause là chốt sổ, quit chỉ là thêm-được-thì-tốt.

**Hệ quả lên thứ tự với hệ khác:** `SaveDriver` bắt magic method của chính nó, nhưng Unity **không đảm bảo thứ tự** magic method giữa các MonoBehaviour — nếu một hệ khác ghi dữ liệu trong pause hook của nó, flush của driver có thể chạy **trước** lần ghi đó. Driver vì thế là **lưới an toàn không có thứ tự**. Đường có thứ tự là `SaveBootStep` (Task 5): runner fan-out ngược nên mọi hệ trên nó đã ghi xong trước khi flush chạy. Dùng cả hai thì flush chạy hai lần và vô hại — lần sau không thấy entry nào dirty nên không chạm đĩa.

### 0.3. `SetString` chưa phải là lưu — `PlayerPrefs.Save()` mới là

`PlayerPrefs.SetString` chỉ sửa bản trong RAM của Unity. Thứ đưa cả kho xuống đĩa là `PlayerPrefs.Save()`, cộng hai lần persist tự động của nền tảng khi app vào pause và khi app quit sạch.

Bỏ `PlayerPrefs.Save()` khỏi vòng flush thì hệ vẫn **trông như** chạy đúng: file được ghi ở pause, restart Play vẫn thấy giá trị. Nhưng autosave chu kỳ khi đó là một vòng lặp **chỉ tốn công serialize mà không mua được gì** — kill app không qua pause là mất hết, đúng bằng lúc chưa có autosave.

Hệ quả lên hình dạng code: `FlushAll` là **hai giai đoạn**, không phải một vòng lặp. Giai đoạn một `SetString` cho từng entry dirty và **giữ nguyên cờ dirty**; giai đoạn hai gọi `PlayerPrefs.Save()` một lần cho cả lượt, rồi mới `ClearDirty` cho những entry đã đi qua giai đoạn một.

**Một giới hạn của nền tảng, ghi ra để biết:** `PlayerPrefs` không đảm bảo ném exception khi persist hỏng, nên nhánh "ghi fail thì giữ dirty" bắt được ít trường hợp hơn ta muốn. Đây là ràng buộc của nền tảng, không phải chỗ code vá được.

### 0.4. Dirty là hợp đồng hai chiều — game set, collection reset SAU khi đã xuống đĩa

Cờ dirty có đúng một người set (`Value` setter hoặc `MarkDirty` — game gọi) và đúng một người reset (collection — sau khi `PlayerPrefs.Save()` thành công). Reset trước khi xuống đĩa thì một lần ghi lỗi là dữ liệu **mất im lặng**: cờ đã tắt, không ai ghi lại nữa.

*Đã sai một lần — color-loop `PlayerSaveLoadService.Save()`:*

```csharp
if (force || _isDirty)
{
    _isDirty = false;                                  // reset TRƯỚC khi ghi
}
var bytes = MemoryPackSerializer.Serialize(data);      // và thân serialize+ghi nằm NGOÀI if
SaveToDevice(bytes);                                   // → dirty-check vô hiệu, lần nào gọi cũng ghi
```

Hai lỗi trong sáu dòng: reset-trước-khi-ghi, và khối `if` chỉ bọc mỗi việc reset cờ nên serialize + ghi chạy bất kể dirty. Hình dạng đúng trong plan này: `ClearDirty()` là method của contract mà **chỉ collection gọi**, và nó chạy sau `PlayerPrefs.Save()` trong cùng một `try` — persist ném exception thì nhảy vào `catch`, cờ của **cả lượt** còn nguyên, chu kỳ sau thử lại.

### 0.5. Serialize thuộc nhịp flush, không thuộc nhịp đổi giá trị

Mỗi lần coin đổi mà serialize cả model rồi ghi là trả giá theo nhịp **tương tác** cho một việc chỉ cần theo nhịp **chu kỳ**. Gán `Value` và `MarkDirty` vì thế chỉ set cờ + phát `Changed`; `JsonConvert.SerializeObject` dồn về `FlushAll`, và chỉ entry **dirty** mới bị chạm.

*Đã sai một lần — color-loop `GameDataManager`:* mỗi thay đổi bất kỳ field nào → `LateUpdate` frame đó `JsonUtility.ToJson` **cả god-blob 25+ field** + `PlayerPrefs.Save()` (chạm đĩa) ngay trong frame. Cùng chỗ lưu, cùng cách serialize, sai ở đúng hai chỗ: một model duy nhất cho cả game, và nhịp serialize bám theo nhịp tương tác.

Cùng họ với nó là bài học **"khung chạy no-op âm thầm"**: khung save "sạch" của color-loop chết vì `AssignService()` không có caller — autosave loop chạy mà không lưu gì, và **không log gì**. Câu trả lời cấu trúc: mọi đường không-làm-gì-được của collection đều phải **kêu lên** (bất biến ⑤).

### 0.6. Payload hỏng → giá trị mặc định + log, không throw

Chuỗi JSON trong PlayerPrefs có thể hỏng: một bản build cũ ghi model có hình dạng khác, một lần chỉnh tay khi debug, một lần persist đứt nửa chừng ở tầng nền tảng. `JsonConvert.DeserializeObject` gặp chuỗi hỏng thì ném exception.

*Đã sai một lần — color-loop `PlayerSaveLoadService`:* `Load()` không có try/catch quanh `Deserialize`, nên một payload hỏng là exception **mỗi lần boot** — save thành "brick" vĩnh viễn, người chơi không vào được game nữa.

Luật ở đây: try/catch quanh đọc + deserialize **của từng entry**, hỏng thì giữ giá trị mặc định và `LogError` **nêu đúng key**. Người chơi mất một entry nhưng vào được game, các entry khác không bị kéo theo, và lần flush kế ghi đè bằng dữ liệu lành.

### 0.7. ScriptableObject không chết giữa hai lần Play — ba thứ phải reset

Asset của một ScriptableObject được Unity load một lần và **giữ nguyên qua các lần Play trong Editor**. Với Enter Play Mode Settings tắt domain reload, mọi field `[NonSerialized]` mang giá trị của phiên trước sang phiên sau. Ba thứ hỏng theo:

| Thứ sống sót | Triệu chứng |
|---|---|
| Listener của `Changed` | listener đăng ký ở phiên trước trỏ vào GameObject đã huỷ → `MissingReferenceException` ở lần đổi giá trị đầu tiên của phiên mới |
| Giá trị runtime | xoá save ngoài đĩa rồi Play lại vẫn thấy giá trị cũ — chỉ xảy ra trong Editor, nên nó là bug lộ ra ở máy developer và **biến mất** trên build, loại khó tin nhất |
| Cờ dirty | flush đầu phiên ghi lại thứ không ai đổi |

Chặn bằng cấu trúc: `Initialize()` gọi `ResetRuntimeState()` cho **mọi** entry trước khi load — nó xoá listener, đưa giá trị về một bản sao mới của mặc định, và tắt dirty. Bắt từng hệ tự `-=` là chặn bằng kỷ luật, và sẽ vỡ ở hệ thứ hai.

### 0.8. Asset là dữ liệu authoring — tiến độ người chơi không được chạm vào nó

`[SerializeField]` trên một field nghĩa là Unity ghi giá trị của nó vào file `.asset` trong project. Hai chỗ phải cẩn thận, và cả hai đều là chỗ khuôn `RCVariable<T>` **cố ý** làm ngược:

1. **Giá trị runtime phải `[NonSerialized]`.** `RCVariable<T>` khai `[SerializeField] private T value` và `ApplyRemoteValue` ghi thẳng vào đó — chủ ý, để developer thấy giá trị fetch về ngay trong asset. Bê nguyên sang save là mỗi lần Play trong Editor, tiến độ người chơi được ghi vào asset của project rồi đi vào version control.
2. **Giá trị mặc định phải được sao chép, không được trả thẳng.** `defaultValue` là một object sống trong asset. Gán `value = defaultValue` cho một model class là để game mutate thẳng vào asset. Cách chặn: một vòng round-trip qua serializer, chạy **một lần mỗi entry lúc `Initialize`** — đủ rẻ, và là bản sao đúng cho mọi `T`, kể cả struct có `List` bên trong.

### 0.9. Bước khởi tạo bắt buộc mà quên gọi thì mất dữ liệu — không được để nó phụ thuộc trí nhớ

*Đã sai một lần — chính repo này:* `RCVariableCollection.Initialize()` được tài liệu hướng dẫn game tự gọi, và grep cả `Assets` cho `.Initialize()` hiện **không ra caller nào** ngoài hai kết quả thuộc Odin. Hệ dựng xong, không ai bấm nút khởi động, và không có gì báo.

Với remote config, cái giá của việc quên là giá trị rơi về default. Với save, cái giá là **người chơi mất tiến độ** — và mất theo kiểu tệ nhất: entry chưa load nên mang giá trị mặc định, rồi flush kế tiếp ghi đè giá trị mặc định đó lên save thật.

Nên `Initialize()` ở hệ này có **hai lối vào cùng chạy một thân idempotent**: gọi tường minh từ `SaveBootStep` để trả chi phí load ở nơi có màn hình loading, hoặc không gọi thì `EnsureInitialized()` ở mọi cửa vào công khai tự lo. Đây là chỗ cố ý đi khác khuôn `RCVariableCollection`.

---

## Bản đồ triển khai

| Task | File | Nội dung |
|---|---|---|
| 1 | `Abstractions/Foundations/Persistence/` — `ISaveEntry.cs` · `SaveEntry.cs` · `ISaveCollection.cs`; **xoá** `ISaveUnit.cs` | 3 contract |
| 2 | `Implementations/Foundations/Persistence/SaveCollection.cs` | lõi collection |
| 3 | `Implementations/Foundations/Persistence/SaveDriver.cs` | autosave + pause/quit |
| 4 | `SaveCollection.cs` (thêm khối `#if UNITY_EDITOR`) | 3 nút Editor |
| 5 | `Implementations/Composites/Persistence/SaveBootStep.cs` | flush có thứ tự, tuỳ chọn |
| 6 | `Abstractions/.../Persistence/Demo/` + `Implementations/.../Persistence/Demo/` | demo + nghiệm thu |

Thứ tự: **1 → 2 → 3 → 4 → 5 → 6**. Task 4 sửa file của Task 2; Task 5 và 6 độc lập với nhau.

---

### Task 1: 3 contract

**Files:**
- Create: `Assets/Horcrux/Runtime/Abstractions/Foundations/Persistence/ISaveEntry.cs`
- Create: `Assets/Horcrux/Runtime/Abstractions/Foundations/Persistence/SaveEntry.cs`
- Create: `Assets/Horcrux/Runtime/Abstractions/Foundations/Persistence/ISaveCollection.cs`
- **Delete:** `Assets/Horcrux/Runtime/Abstractions/Foundations/Persistence/ISaveUnit.cs` (+ `.meta`)

**Interfaces:**
- Consumes: `IService<T>` (`Abstractions/Foundations/IService.cs`) · `Newtonsoft.Json.JsonConvert`.
- Produces: `ISaveEntry` (2 property + 4 method) · `SaveEntry<T>` (game khai) · `partial interface ISaveCollection : IService<ISaveCollection>` (1 property + 3 method).

**Quyết định thiết kế:**

| Quyết định | Lý do |
|---|---|
| Một `SaveEntry<T>` cho **cả** giá trị lẻ và model | Hai cỡ dữ liệu chỉ khác nhau ở cách biến thành chuỗi. Dựng kiểu thứ hai cho một bước là đặt giá trị lẻ ra ngoài cờ dirty và ngoài `PlayerPrefs.Save()` — nghĩa là ra ngoài chính bảo đảm "mất tối đa một chu kỳ" |
| `int`/`bool`/`float` đi qua JSON thay vì `GetInt`/`GetFloat` native | Giá trị đã cache trong RAM nên phép parse chạy **một lần lúc boot** và `ToString` **một lần mỗi lượt flush** — không đo nổi. Đổi lại toàn hệ chỉ còn một đường đọc, một đường ghi, một chỗ kiểm |
| `ISaveEntry` non-generic + `SaveEntry<T>` generic | Collection cần một `List<ISaveEntry>` đồng nhất; phần typed nằm ở lớp game khai. Game **không** implement `ISaveEntry` trực tiếp |
| Bốn method của `ISaveEntry` cài **explicit** | `ResetRuntimeState`, `ReadPayload`, `WritePayload`, `ClearDirty` chỉ collection được gọi. Explicit interface implementation làm chúng **biến mất** khỏi IntelliSense của game — chặn bằng cấu trúc, không bằng dòng doc "chỉ collection gọi" |
| Entry chỉ nói bằng **chuỗi payload**, không biết `PlayerPrefs` | Đây là chỗ duy nhất đáng phòng xa, và nó tốn 0 dòng: chuyển kho sang file sau này chỉ sửa nội bộ collection |
| `[NonSerialized] T value`, `[SerializeField] T defaultValue` | Đảo ngược có chủ ý so với `RCVariable<T>` — lý do gốc của khuôn cũ là cho developer thấy giá trị fetch về trong asset, lý do đó không còn đúng khi thứ được lưu là tiến độ người chơi |
| `CloneDefault()` round-trip qua serializer, một nhánh duy nhất | `defaultValue` sống trong asset; trả thẳng nó ra là để game mutate vào asset. Round-trip là bản sao đúng cho **mọi** `T` — kể cả struct chứa `List` — nên không cần nhánh riêng cho value type |
| `(object)x == null` thay vì `x == null` | `==` trên type parameter không ràng buộc **không compile được**. Cast về `object` là cách kiểm null hợp lệ duy nhất ở đây |
| `Value` có setter, **và** có `MarkDirty()` | Gán cả giá trị là nhịp tự nhiên của giá trị lẻ; mutate rồi báo là nhịp tự nhiên của model. Một kiểu phục vụ cả hai mà không cần biết mình đang là loại nào |
| `implicit operator T` | Tiền lệ `RCVariable<T>` đã có, và nó làm call site đọc gọn: `if (collection.HasRated)` |
| `Changed` là `event Action<T>`, fan-out qua `GetInvocationList` + try/catch từng listener | Đăng ký thưa nên `event` là đúng mức. Một listener ném exception không được kéo cả hệ chết. Alloc theo nhịp tương tác, không theo frame |
| `ReadPayload` cũng bắn `Changed` | "Value đổi thì `Changed` bắn" là **một** luật không ngoại lệ |
| `ISaveCollection` là `partial interface` | Game khai thêm property cho entry của mình, y khuôn `IRCVariableCollection` |

- [ ] **Step 1: `ISaveEntry.cs`**

```csharp
namespace Horcrux.Runtime.Abstractions.Persistence
{
    /// <summary>What the collection needs from one saved value. Game code declares <see cref="SaveEntry{T}"/> instead.</summary>
    /// <remarks>
    /// Every method here belongs to the collection alone, which is why <see cref="SaveEntry{T}"/> implements them
    /// explicitly. An entry never names a storage medium — it speaks in payload strings — and that is what lets
    /// the store move to a file later without touching an entry or a single line of game code.
    /// </remarks>
    public interface ISaveEntry
    {
        /// <summary>Wire-format key, authored in the Inspector. The stored entry lives under the collection's prefix plus this.</summary>
        string Key { get; }

        /// <summary>Has changes not yet on storage. The game sets it; only the collection clears it.</summary>
        bool IsDirty { get; }

        /// <summary>COLLECTION ONLY, at Initialize: drops listeners and returns the value to a fresh copy of the authored default.</summary>
        void ResetRuntimeState();

        /// <summary>COLLECTION ONLY, at Initialize: the stored payload for this key. Throwing leaves the default in place.</summary>
        void ReadPayload(string payload);

        /// <summary>COLLECTION ONLY, during a flush: the current value as one payload string.</summary>
        string WritePayload();

        /// <summary>COLLECTION ONLY, after storage accepted the write — the single place dirty is cleared.</summary>
        void ClearDirty();
    }
}
```

- [ ] **Step 2: `SaveEntry.cs`**

```csharp
using System;
using Newtonsoft.Json;
using UnityEngine;

namespace Horcrux.Runtime.Abstractions.Persistence
{
    /// <summary>One saved value of any size — a flag, a counter, or a whole progress model. Declared as a field on SaveCollection.</summary>
    /// <remarks>
    /// Small values and big models share this one type on purpose. They differ only in how the value becomes a
    /// string, and a second type built for that one step is what puts a value outside the dirty flag and outside
    /// the flush — outside the very guarantee the system promises.
    /// Assign <see cref="Value"/> to replace the whole value, or mutate a model in place and call
    /// <see cref="MarkDirty"/>. Neither serializes: that waits for the collection's flush.
    /// The payload is JSON, so a model must be plain data — numbers, strings, List, Dictionary, public fields.
    /// Never put a UnityEngine.Object or a Unity struct in one: engine references do not belong in a save, and
    /// Vector3.normalized makes the serializer recurse forever.
    /// </remarks>
    [Serializable]
    public class SaveEntry<T> : ISaveEntry
    {
        [SerializeField, Tooltip("Wire format. Renaming it orphans every save already on a player's device.")]
        private string key;

        [SerializeField, Tooltip("The value before anything is stored. Authored here; never written to at runtime.")]
        private T defaultValue;

        // Runtime only. Serializing these would write the player's progress into the project asset.
        [NonSerialized] private T value;
        [NonSerialized] private bool isDirty;

        public string Key => key;

        public bool IsDirty => isDirty;

        /// <summary>The live value. Assigning replaces it and marks dirty; to change a model in place, mutate it then call MarkDirty.</summary>
        public T Value
        {
            get => value;
            set
            {
                this.value = value;
                MarkDirty();
            }
        }

        /// <summary>Fires on every change — an assignment, a MarkDirty, and a load. A throwing listener cannot kill the entry.</summary>
        public event Action<T> Changed;

        /// <summary>Call after mutating the value in place. Sets a flag and fires Changed — cheap enough for every interaction.</summary>
        public void MarkDirty()
        {
            isDirty = true;
            RaiseChanged();
        }

        public static implicit operator T(SaveEntry<T> entry) => entry != null ? entry.value : default;

        void ISaveEntry.ResetRuntimeState()
        {
            // A ScriptableObject outlives a play session, so listeners registered last session still point at
            // destroyed objects. Clearing them here is structural; asking every system to unsubscribe is not.
            Changed = null;
            value = CloneDefault();
            isDirty = false;
        }

        void ISaveEntry.ReadPayload(string payload)
        {
            T loaded = JsonConvert.DeserializeObject<T>(payload);
            value = (object)loaded == null ? CloneDefault() : loaded;
            isDirty = false;                       // just read back — memory and storage agree
            RaiseChanged();
        }

        string ISaveEntry.WritePayload() => JsonConvert.SerializeObject(value);

        void ISaveEntry.ClearDirty() => isDirty = false;

        /// <summary>A fresh copy of the authored default. Handing out the field itself would let the game mutate the asset.</summary>
        /// <remarks>A round-trip is the one copy that is correct for every T, a struct holding a List included.
        /// It runs once per entry at Initialize, so the cost never reaches a play session.</remarks>
        private T CloneDefault()
        {
            // '== null' does not compile on an unconstrained type parameter; the cast to object does.
            if ((object)defaultValue == null) return default;
            return JsonConvert.DeserializeObject<T>(JsonConvert.SerializeObject(defaultValue));
        }

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
    }
}
```

- [ ] **Step 3: `ISaveCollection.cs`**

```csharp
using System.Collections.Generic;

namespace Horcrux.Runtime.Abstractions.Persistence
{
    /// <summary>The one place every saved value lives. A REQUIRED service: a missing asset is a setup error, not a runtime case.</summary>
    /// <remarks>
    /// A game project adds its own entries as partial declarations — a property here and a backing field on
    /// SaveCollection — the same two-file pattern RCVariableCollection uses. Both files are required: without
    /// the property, nothing outside the collection can reach the entry.
    /// </remarks>
    public partial interface ISaveCollection : IService<ISaveCollection>
    {
        /// <summary>Every entry that passed validation. Empty until Initialize has run.</summary>
        IReadOnlyList<ISaveEntry> Entries { get; }

        /// <summary>Scans, validates, resets and loads every entry, then starts the autosave driver. Idempotent.</summary>
        /// <remarks>Calling it is optional: the first touch of any member runs it. Call it from a boot step to
        /// pay the load cost while a loading screen is up, and to surface a broken save there.</remarks>
        void Initialize();

        /// <summary>Stores every dirty entry now. The system calls this on autosave, pause and quit.</summary>
        void FlushAll();

        /// <summary>Stores one entry now, for a value that must not wait for the cycle — right after a purchase.</summary>
        /// <param name="entry">Must be an entry this collection owns; anything else is refused with an error.</param>
        /// <remarks>Not cheaper than FlushAll on disk: PlayerPrefs rewrites the whole store either way. What it
        /// saves is serializing the other entries.</remarks>
        void Flush(ISaveEntry entry);
    }
}
```

- [ ] **Step 4: Kiểm chứng** — compile sạch; `ISaveUnit.cs` đã biến mất và `grep -rn "ISaveUnit" Assets` không ra kết quả nào. Chưa có hành vi chạy được (contract thuần).

- [ ] **Step 5: Commit** — `feat(sdk): replace save-unit contracts with SaveEntry`

---

### Task 2: `SaveCollection` — lõi

**Files:**
- Create: `Assets/Horcrux/Runtime/Implementations/Foundations/Persistence/SaveCollection.cs`

**Interfaces:**
- Consumes: `ISaveEntry` · `ISaveCollection` (Task 1) · `PlayerPrefs` · `[Service]` của Sisus.Init · `System.Reflection`.
- Produces: `RegisteredSave` (attribute) · `SaveCollection : ScriptableObject, ISaveCollection` — `Initialize()` · `FlushAll()` · `Flush(ISaveEntry)` · `Entries` · `AutosaveIntervalSeconds` · `const string KeyPrefix`.

**Quyết định thiết kế:**

| Quyết định | Lý do |
|---|---|
| ScriptableObject qua `[Service(ResourcePath)]`, không MonoBehaviour trong scene | Không phải đặt GameObject vào scene entry, sống qua mọi lần đổi scene, và key hiện trong Inspector của một asset tra được bằng Project window. Cùng khuôn `RCVariableCollection` |
| `Initialize()` public **và** `EnsureInitialized()` lazy ở mọi cửa vào | Quên gọi thì entry mang giá trị mặc định và flush kế **ghi đè giá trị mặc định lên save thật**. Đây là chỗ cố ý khác khuôn cũ: hai lối vào, một thân idempotent |
| `ScanEntries()` là **một** thân dùng chung cho `Initialize` và nút `Validate keys` | Hai bản kiểm sẽ lệch nhau, và lệch kiểu nguy hiểm nhất: nút báo sạch trong khi runtime vẫn bỏ entry. Cùng một hàm thì không thể lệch |
| Kiểm ba thứ: field chưa gán · key rỗng · trùng key — mỗi lỗi một `LogError` nêu **tên field** | Nêu key thôi thì không đủ để mở Inspector tìm; tên field là thứ developer nhìn thấy trên màn hình |
| Trùng key → **bỏ entry sau**, giữ entry trước | Hai entry một key là entry sau đè payload entry trước. Giữ entry trước là giữ cái đã có dữ liệu ngoài đời |
| `FlushAll()` và `Flush(entry)` cùng gọi `FlushInternal(only)` | Hai đường làm gần cùng một việc thì đường hẹp phải chạy trong thân đường rộng — nếu không, một ngày `PlayerPrefs.Save()` chỉ còn ở một bên |
| `Flush(entry)` từ chối entry collection không sở hữu | Ghi một entry chưa đăng ký là tạo một khoá **không ai load lại lúc boot** — dữ liệu ghi ra rồi biến mất, không có gì báo. Đây thường là dấu hiệu quên `[RegisteredSave]` |
| `PlayerPrefs.Save()` gọi **một lần** cho cả lượt, chỉ khi có entry vừa ghi | Save ghi lại toàn bộ kho, nên gọi n lần cho n entry là ghi cả kho n lần. Không entry nào dirty thì không gọi — flush rỗng phải thật sự không chạm đĩa |
| `pendingClear` là field `List<ISaveEntry>` tái dùng, `Clear()` trong `finally` | Danh sách sống trong đúng một lượt flush; tái dùng thì không alloc theo chu kỳ, và `finally` đảm bảo lượt sau bắt đầu từ rỗng kể cả khi persist ném exception |
| Reflection quét field có cache `List<FieldInfo>` | Chạy một lần mỗi phiên. LINQ ở đây không phải hot path, và đọc dễ hơn vòng lặp tay |
| `Entries` không hứa thứ tự khai báo | `Type.GetFields` không đảm bảo thứ tự. Hứa một thứ tự không có là mở đường cho code sau dựa vào nó |
| Flush khi 0 entry → `LogWarning` đúng một lần | Một hệ save không bao giờ được no-op trong im lặng |

**Editor setup — bước thật:**

1. Project window → chuột phải → `Create` → `Horcrux` → `SaveCollection`.
2. Đặt asset vào **`Assets/.../Resources/Config/SaveCollection.asset`** — đường dẫn phải khớp `ResourcePath`, sai là service không resolve được.
3. Inspector: đặt `Autosave Interval Seconds` (mặc định 5) theo game.

- [ ] **Step 1: `SaveCollection.cs`**

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Horcrux.Runtime.Abstractions.Persistence;
using Sisus.Init;
using UnityEngine;

namespace Horcrux.Runtime.Implementations.Persistence
{
    /// <summary>Marks a SaveEntry field on SaveCollection as one the collection owns. A field without it is ignored.</summary>
    [AttributeUsage(AttributeTargets.Field)]
    public sealed class RegisteredSave : Attribute { }

    /// <summary>Owns every saved value: scans the declared entries, loads them, autosaves, and flushes on pause and quit.</summary>
    /// <remarks>
    /// Storage is PlayerPrefs — one entry per key, holding the value as JSON. The contract is "lose at most ONE
    /// autosave cycle": on Android a swipe-kill never runs OnApplicationQuit, so pause is the last signal we trust.
    /// A game declares its entries as partial fields on this class; nothing registers itself at runtime, which is
    /// what makes a duplicate key catchable while authoring rather than after shipping.
    /// </remarks>
    [Service(typeof(ISaveCollection), ResourcePath = ResourcePath)]
    [CreateAssetMenu(menuName = "Horcrux/SaveCollection", fileName = "SaveCollection")]
    public partial class SaveCollection : ScriptableObject, ISaveCollection
    {
        private const string ResourcePath = "Config/SaveCollection";

        /// <summary>Sits in front of every entry key.</summary>
        /// <remarks>PlayerPrefs is one flat namespace shared with remote config caches, ad and analytics SDKs.
        /// This prefix is what keeps a same-named key of theirs from landing on a player's progress. Wire format:
        /// changing it orphans every save already out there.</remarks>
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

            isInitialized = true;      // set before loading: nothing below may re-enter through a guarded member
            LoadAll();
            EnsureDriver();
        }

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
                // Writing an entry the collection does not own creates a key nothing reads back at boot.
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

        /// <summary>The single scan behind both Initialize and the Editor validation, so the button clears exactly what runtime accepts.</summary>
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

        private List<FieldInfo> GetEntryFields()
        {
            return cachedEntryFields ??= GetType()
                .GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                .Where(field => field.IsDefined(typeof(RegisteredSave), false))
                .ToList();
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
                    // A broken entry must never stop the player from entering the game, and must not take the
                    // other entries down with it.
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

- [ ] **Step 2: Kiểm chứng** (bảng input → kỳ vọng; nghiệm thu chạy thật ở Task 6):

| Input | Kỳ vọng |
|---|---|
| Play lần đầu, chưa có gì trong PlayerPrefs | không log lỗi, mọi entry mang giá trị mặc định, `IsDirty == false` |
| Gán `Value` rồi chờ hết một chu kỳ autosave | `IsDirty == false`, `Print all payloads` thấy JSON đúng giá trị |
| Gán `Value`, **không** chờ | `IsDirty == true`, payload đang lưu vẫn là giá trị cũ — serialize không chạy theo nhịp tương tác |
| Hai field khai cùng một key | `LogError` nêu **cả hai tên field**, field sau bị bỏ, field trước hoạt động bình thường |
| Field `[RegisteredSave]` để key rỗng | `LogError` nêu tên field, field đó bị bỏ, các field khác vẫn chạy |
| Payload bị ghi rác trước khi Play | vào game bình thường, `LogError` nêu đúng key, entry đó về mặc định, **các entry khác vẫn load đúng** |
| `FlushAll` khi không có gì dirty | không gọi `PlayerPrefs.Save()`, không log |
| `FlushAll` khi 0 entry hợp lệ | `LogWarning` đúng một lần cho cả phiên |
| `Flush(entry)` với entry hợp lệ đang dirty | chỉ entry đó được `SetString`, `PlayerPrefs.Save()` chạy một lần, chỉ entry đó hết dirty |
| `Flush(entry)` với một `SaveEntry` mới `new` ngoài collection | `LogError` nêu key, **không** ghi gì |
| Gọi `FlushAll` trước khi ai đó gọi `Initialize` | tự initialize, load đúng, không `NullReferenceException` |
| Gọi `Initialize()` hai lần | kết quả y hệt một lần; không nhân đôi entry, không tạo driver thứ hai |

- [ ] **Step 3: Commit** — `feat(sdk): add SaveCollection (scan, validate, load, two-phase flush)`

---

### Task 3: `SaveDriver` — autosave, pause, quit

**Files:**
- Create: `Assets/Horcrux/Runtime/Implementations/Foundations/Persistence/SaveDriver.cs`

**Interfaces:**
- Consumes: `ISaveCollection` · `SaveCollection` (Task 2) · UniTask.
- Produces: `SaveDriver : MonoBehaviour` — `internal static Spawn(SaveCollection)`.

**Quyết định thiết kế:**

| Quyết định | Lý do |
|---|---|
| Driver là MonoBehaviour do collection tự dựng | ScriptableObject không có timer, không có pause hook, không có quit hook. Đây là ba thứ duy nhất driver tồn tại để cung cấp |
| Dựng **lazy** trong `Initialize`, không `[RuntimeInitializeOnLoadMethod]` | InitArgs khởi tạo service ở `BeforeSceneLoad`; một `BeforeSceneLoad` thứ hai của ta sẽ chạy **không xác định thứ tự** so với nó. Dựng lúc `Initialize` thì không có giả định thứ tự nào cả |
| GameObject **hiện trong Hierarchy**, tên `[Save]`, không `HideFlags` | Autosave đang chạy là thứ có thật; giấu nó đi là để developer không có cách nào thấy hệ save đang sống. Đổi lại một dòng trong Hierarchy |
| `DelayType.Realtime` | Autosave không được ngừng khi game pause bằng `timeScale = 0` |
| `destroyCancellationToken` | Loop chết theo GameObject, không có `while(true)` sống sót sau `Destroy`. Unity 6 có sẵn, không phải tự nuôi `CancellationTokenSource` |
| Autosave tick gọi thẳng `FlushAll` | Autosave, pause, quit và gọi tay đi chung **một** thân flush, nên bốn đường không thể lệch nhau |
| Driver giữ `ISaveCollection`, không tra service lại mỗi lần | Nó được collection dựng ra và biết chủ của mình; tra lại là mở đường cho hai collection khác nhau trong một phiên |

- [ ] **Step 1: `SaveDriver.cs`**

```csharp
using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Horcrux.Runtime.Abstractions.Persistence;
using UnityEngine;

namespace Horcrux.Runtime.Implementations.Persistence
{
    /// <summary>Gives the collection the three things a ScriptableObject cannot have: a timer, a pause hook and a quit hook.</summary>
    /// <remarks>
    /// Spawned by the collection and kept across scenes. Flushing from these magic methods is a safety net with
    /// NO ordering guarantee against other MonoBehaviours: a system that writes in its own pause hook may run
    /// after this one. A project that needs the order adds SaveBootStep as well — running both is harmless,
    /// because a flush that finds nothing dirty never reaches storage.
    /// </remarks>
    public sealed class SaveDriver : MonoBehaviour
    {
        private ISaveCollection collection;
        private float intervalSeconds;

        internal static SaveDriver Spawn(SaveCollection owner)
        {
            // Visible on purpose: an autosave loop is a real thing running, and a developer must be able to see it.
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
| Play, mở Hierarchy | thấy đúng **một** GameObject `[Save]`, nằm ở `DontDestroyOnLoad` |
| Đổi scene | `[Save]` còn nguyên, không sinh cái thứ hai |
| Gán giá trị rồi bấm nút Pause của Editor | payload cập nhật ngay, không phải chờ hết chu kỳ |
| Đặt `timeScale = 0` rồi chờ hết chu kỳ | autosave vẫn chạy |
| `Destroy` GameObject `[Save]` giữa phiên | loop dừng theo token, không exception |

- [ ] **Step 3: Commit** — `feat(sdk): add SaveDriver (autosave loop, pause and quit flush)`

---

### Task 4: Ba nút Editor trên `SaveCollection`

**Files:**
- Modify: `Assets/Horcrux/Runtime/Implementations/Foundations/Persistence/SaveCollection.cs` — thêm khối `#if UNITY_EDITOR` ở cuối class.

**Interfaces:**
- Consumes: `ScanEntries` · `KeyPrefix` (Task 2) · Odin `[Button]`, `[GUIColor]` · `UnityEditor.EditorUtility`.
- Produces: chỉ nút Inspector — không code runtime nào phụ thuộc.

**Quyết định thiết kế:**

| Quyết định | Lý do |
|---|---|
| `ValidateKeys` gọi chính `ScanEntries` | Nút phải báo **đúng** thứ runtime sẽ bỏ. Hai bản kiểm là hai bản sẽ lệch, và lệch về phía nút báo sạch trong khi runtime bỏ entry |
| Ba nút, không phải `OnValidate` | Trong lúc đang thêm entry thì key luôn rỗng; cảnh báo nền sẽ hiện gần như toàn thời gian, vào đúng lúc chưa thể sửa |
| `DeleteAllSaveData` hỏi trước bằng `DisplayDialog` | Xoá dữ liệu đã author là thao tác khó đảo ngược |
| `DeleteAllSaveData` chỉ xoá khoá của entry **đang khai**, và nói ra điều đó | `PlayerPrefs` không có API liệt kê khoá, nên khoá mồ côi do đổi tên entry không thể tìm được. Báo con số thật còn hơn để developer tin là đã sạch |
| `PrintAllPayloads` in cả độ dài từng payload và tổng | Đây là con số duy nhất trả lời được "đã chạm ngưỡng vài chục KB chưa" |
| Cả ba nút chạy được **ngoài Play mode** | Chúng đọc field qua reflection, không đọc `entries` — nên trùng key lộ ra lúc đang dựng, đúng lúc còn sửa rẻ |

- [ ] **Step 1: thêm vào cuối `SaveCollection`, trước dấu `}` của class**

```csharp
#if UNITY_EDITOR
        [Sirenix.OdinInspector.Button, Sirenix.OdinInspector.GUIColor("cyan")]
        private void ValidateKeys()
        {
            // Runs the very scan Initialize runs, so a clean result here means a clean load at runtime.
            var scratch = new List<ISaveEntry>();
            int rejected = ScanEntries(scratch);

            if (rejected == 0)
                Debug.Log($"[Save] {scratch.Count} entries — no duplicate key, no empty key, no unassigned field.", this);
            else
                Debug.LogError($"[Save] {rejected} of {scratch.Count + rejected} declared entries were rejected; see the errors above.", this);
        }

        [Sirenix.OdinInspector.Button]
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

        [Sirenix.OdinInspector.Button, Sirenix.OdinInspector.GUIColor("red")]
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

            // PlayerPrefs cannot list its keys, so a value stored under a key no field declares any more cannot
            // be found. Say the real number rather than let this read as "the store is now empty".
            Debug.Log($"[Save] Deleted {deleted} of {scratch.Count} declared entries. Values stored under keys no " +
                      "field declares any more are unreachable and stay. Enter Play mode to see defaults.", this);
        }
#endif
```

- [ ] **Step 2: Kiểm chứng** (không cần Play mode):

| Input | Kỳ vọng |
|---|---|
| Chọn asset, bấm `Validate keys` khi mọi thứ hợp lệ | một dòng `Log` nêu đúng số entry |
| Điền hai field cùng một key, bấm `Validate keys` | `LogError` nêu cả hai tên field **ngay lập tức**, không cần Play |
| Xoá key của một field, bấm `Validate keys` | `LogError` nêu tên field đó |
| Bấm `Print all payloads` khi chưa từng Play | mọi dòng ghi `(nothing stored yet)`, `TOTAL 0 chars` |
| Play, đổi giá trị, chờ flush, Stop, bấm `Print all payloads` | thấy JSON đúng giá trị và độ dài thật |
| Bấm `Delete all save data` rồi `Cancel` | không xoá gì, không log |
| Bấm `Delete all save data` rồi `Delete`, sau đó Play | mọi entry về giá trị mặc định |

- [ ] **Step 3: Commit** — `feat(sdk): add save collection editor buttons (validate, print, delete)`

---

### Task 5: `SaveBootStep` — flush có thứ tự, tuỳ chọn

**Files:**
- Create: `Assets/Horcrux/Runtime/Implementations/Composites/Persistence/SaveBootStep.cs`

**Interfaces:**
- Consumes: `BootStep` (`Abstractions/Foundations/Bootstrap/BootStep.cs`) · `ISaveCollection` (Task 1).
- Produces: `SaveBootStep : BootStep`.

**Quyết định thiết kế:**

| Quyết định | Lý do |
|---|---|
| Nằm ở nhánh **Composites**, không phải Foundations | Nó phụ thuộc cả Bootstrap lẫn Persistence. Lõi Persistence ở Foundations vẫn chạy được ở project không dùng Bootstrap |
| Là **tuỳ chọn**, không thay driver | Driver vẫn chạy autosave. Step này chỉ thêm đường flush **có thứ tự** cho pause và quit |
| `InitializeAsync` gọi `Initialize()` | Trả chi phí reflection và load ở nơi có màn hình loading, và làm lỗi save lộ ra trong pha boot thay vì giữa gameplay |
| Chạy hai đường flush cùng lúc là chấp nhận được | Flush là idempotent: đường nào chạy trước cũng đúng, và đường chạy sau bắt nốt phần vừa sinh ra. Lượt không thấy gì dirty không chạm đĩa |
| Đặt `Order` **thấp** (init sớm) để flush **muộn** | Fan-out pause và quit đi **ngược** chiều init. Step init sớm nhất sẽ flush muộn nhất — đó mới là thứ ta muốn: chốt sổ sau khi mọi hệ đã ghi xong |

**Editor setup — bước thật:**

1. Trên GameObject có `BootstrapRunner`: add component `SaveBootStep`.
2. Đặt `Order` **thấp** — vì fan-out pause/quit đi ngược, step init sớm sẽ flush **muộn nhất**, sau khi mọi hệ khác đã ghi xong.
3. Kéo `SaveBootStep` vào danh sách steps của `BootstrapRunner` nếu runner dùng danh sách tường minh.

- [ ] **Step 1: `SaveBootStep.cs`**

```csharp
using System.Threading;
using Cysharp.Threading.Tasks;
using Horcrux.Runtime.Abstractions.Bootstrap;
using Horcrux.Runtime.Abstractions.Persistence;

namespace Horcrux.Runtime.Implementations.Persistence
{
    /// <summary>Optional: moves the save load into boot, and gives pause and quit an ORDERED flush.</summary>
    /// <remarks>
    /// The runner walks steps backwards on pause and quit, so a step that initializes EARLY flushes LAST — after
    /// every system above it has written. That is the ordering a MonoBehaviour's own magic methods cannot promise.
    /// This does not replace SaveDriver: the driver still runs the autosave timer, and the second flush of a pause
    /// finds nothing dirty and never reaches storage.
    /// </remarks>
    public sealed class SaveBootStep : BootStep
    {
        public override UniTask InitializeAsync(CancellationToken ct)
        {
            // Pays the reflection scan and the load here, where a loading screen is up and a broken save is visible.
            ISaveCollection.Service.Initialize();
            return UniTask.CompletedTask;
        }

        public override void OnAppPause(bool isPaused)
        {
            if (isPaused) ISaveCollection.Service.FlushAll();
        }

        public override void OnAppQuit() => ISaveCollection.Service.FlushAll();
    }
}
```

- [ ] **Step 2: Kiểm chứng:**

| Input | Kỳ vọng |
|---|---|
| Play với `SaveBootStep` trong chain | load xảy ra trong pha boot; `[Save]` xuất hiện ngay sau đó |
| Một `BootStep` khác `Order` cao hơn ghi vào một entry trong `OnAppPause` | giá trị đó **có** trong payload sau khi pause — chứng minh thứ tự đúng |
| Pause với cả driver lẫn step | flush chạy hai lần, lần sau không chạm đĩa (không log gì thêm) |
| Play **không** có `SaveBootStep` | hệ vẫn chạy đủ; load xảy ra ở lần chạm đầu tiên |

- [ ] **Step 3: Commit** — `feat(sdk): add optional SaveBootStep for ordered flush`

---

### Task 6: Demo + nghiệm thu

Demo là **bản mẫu chạy được của khuôn hai file** mà game project sẽ dùng, đồng thời là chỗ nghiệm thu. Đây là ngoại lệ duy nhất cho luật "SDK không khai entry": nó là bộ nghiệm thu của chính SDK, và cả thư mục `Demo/` xoá được mà không ảnh hưởng gì.

**Files:**
- Create: `Assets/Horcrux/Runtime/Abstractions/Foundations/Persistence/Demo/ISaveCollection.Demo.cs`
- Create: `Assets/Horcrux/Runtime/Implementations/Foundations/Persistence/Demo/SaveCollection.Demo.cs`
- Create: `Assets/Horcrux/Runtime/Implementations/Foundations/Persistence/Demo/DemoSaveDriver.cs`
- Scene demo (Editor setup dưới).

- [ ] **Step 1: `ISaveCollection.Demo.cs`** — nửa partial interface

```csharp
namespace Horcrux.Runtime.Abstractions.Persistence
{
    /// <summary>Demo half of the two-file pattern a game project copies. Delete the Demo folders to drop it.</summary>
    public partial interface ISaveCollection
    {
        SaveEntry<DemoProgress> DemoProgress { get; }
        SaveEntry<bool> DemoHasRated { get; }
    }

    /// <summary>Demo model — plain data, public fields. A real save model looks exactly like this.</summary>
    [System.Serializable]
    public sealed class DemoProgress
    {
        public int coins;
        public int currentLevel = 1;
        public System.Collections.Generic.List<string> unlockedSkins = new();
    }
}
```

- [ ] **Step 2: `SaveCollection.Demo.cs`** — nửa partial class

```csharp
using Horcrux.Runtime.Abstractions.Persistence;
using UnityEngine;

namespace Horcrux.Runtime.Implementations.Persistence
{
    /// <summary>Demo half of the two-file pattern. A game project writes exactly this shape, in a folder carrying
    /// a com.horcrux.runtime.asmref so the partial lands in the same assembly.</summary>
    public partial class SaveCollection
    {
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
using UnityEngine;

namespace Horcrux.Runtime.Implementations.Persistence.Demo
{
    /// <summary>Acceptance driver: change values and watch load, autosave and flush through the log. Not for a real game.</summary>
    public sealed class DemoSaveDriver : MonoBehaviour
    {
        private ISaveCollection save;

        private void Start()
        {
            save = ISaveCollection.Service;
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
            PlayerPrefs.SetString(SaveCollection.KeyPrefix + save.DemoProgress.Key, "{ garbage");
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

- [ ] **Step 4: Editor setup** (bước thật):

1. Tạo asset `SaveCollection` và đặt vào `Resources/Config/SaveCollection.asset`.
2. Inspector của asset: điền key `demo_progress` cho `Demo Progress`, `demo_has_rated` cho `Demo Has Rated`; đặt giá trị mặc định muốn có.
3. Bấm `Validate keys` — phải sạch **trước khi** Play.
4. Scene mới `PersistenceDemo` → GameObject `[Demo]` + component `DemoSaveDriver`.

- [ ] **Step 5: Nghiệm thu tự động** (agent tự chạy — bốn thứ mắt người sót):

| Kiểm | Cách làm | Kỳ vọng |
|---|---|---|
| Round-trip mọi kiểu dùng thật | `SaveEntry<T>` với `int` · `bool` · `float` · `string` · một enum · `DemoProgress` có `List`: gán, `WritePayload`, `ReadPayload` vào một entry mới, so giá trị | bằng nhau từng field |
| **Không còn cửa ghi thứ hai** | `grep -rn "PlayerPrefs.Set" Assets/Horcrux/Runtime` | trong hệ Persistence ra **đúng một** kết quả runtime, nằm trong `FlushInternal`; kết quả trong `Demo/` và trong nút Editor là cố ý và phải đếm riêng |
| Không còn dấu vết kiến trúc cũ | `grep -rn "ISaveUnit\|SaveUnit\|Prefs<\|PrefsBool\|PrefsInt\|SaveRegistry" Assets` | không kết quả nào |
| `defaultValue` không bị mutate | lấy `Value`, mutate sâu vào nó (`coins = 999`, `unlockedSkins.Add`), gọi `Initialize()` lại, đọc `Value` | về đúng mặc định đã author; asset không bị đánh dấu dirty |
| Dirty không tắt trước khi xuống đĩa | dựng ca `PlayerPrefs.Save()` ném exception | mọi entry vừa ghi **còn** dirty, log nêu rõ, lượt sau thử lại |

- [ ] **Step 6: Kịch bản chơi thử** (nghiệm thu này cần Play mode, developer chạy):

| Mục | Nội dung |
|---|---|
| Vào đâu | Scene `PersistenceDemo`, bấm Play |
| Làm gì | ① chuột phải `DemoSaveDriver` → `Add 10 coins` ×3, rồi bấm `Print all payloads` trên asset **ngay lập tức** · ② chờ quá 5 giây → `Print all payloads` lần nữa · ③ Stop rồi Play lại · ④ `Toggle 'has rated'` → Stop → Play lại · ⑤ `Add 10 coins` rồi `Flush ONLY progress` · ⑥ `Flush an entry the collection does not own` · ⑦ `Corrupt the progress payload` → Stop → Play lại · ⑧ `Add 10 coins` rồi bấm nút Pause của Editor · ⑨ trên asset, đổi key của `Demo Has Rated` thành `demo_progress` → `Validate keys` |
| Nhìn cái gì | ① log `dirty=True` ngay khi add, nhưng payload in ra vẫn là giá trị **cũ** · ② payload đã thành `coins=30`, log `dirty=False` · ③ log đầu tiên của phiên mới đã là `coins=30`, không phải `0` · ④ `rated` giữ nguyên qua phiên — **đây là ca thiết kế cũ làm mất, giờ nó nằm trong cùng vòng flush với model** · ⑤ chỉ payload của `demo_progress` đổi · ⑥ `LogError` nêu key và nhắc `[RegisteredSave]`, không ghi gì · ⑦ vẫn vào được demo, `LogError` nêu đúng `demo_progress`, `coins=0`, còn `rated` **vẫn đúng** · ⑧ payload cập nhật ngay lúc pause · ⑨ `LogError` nêu cả hai tên field, **ngay trong Editor, không cần Play** |
| Khác trước ra sao | Ở thiết kế cũ, giá trị lẻ như `rated` đi một đường riêng không có cờ dirty, và `PlayerPrefs.Save()` chỉ chạy khi có model dirty — nên ca ④ với app bị kill không qua pause là **mất giá trị**. Trùng key chỉ lộ lúc Play và chỉ giữa các model; ca ⑨ giờ lộ lúc authoring và phủ mọi entry |
| Dấu hiệu hỏng | coins về 0 sau restart bình thường (mất save) · `dirty=True` còn mãi sau khi payload đã đổi (`ClearDirty` không chạy) · ca ① payload đã đổi ngay khi vừa add (serialize đang bám nhịp tương tác) · ca ④ `rated` về mặc định (giá trị lẻ lại rơi ra ngoài vòng flush) · ca ⑦ exception đỏ không ai bắt, hoặc `rated` cũng mất theo (một entry hỏng kéo cả lượt) · ca ② payload không đổi sau khi chờ (thiếu `PlayerPrefs.Save()`) · ca ⑨ phải Play mới thấy lỗi |

- [ ] **Step 7: Commit** — `feat(sdk): add persistence demo + acceptance scene`

---

## Ghi chú thực thi

- **Nghiệm thu cuối = Step 5 và Step 6 của Task 6.** Năm mục tiêu ở "Ngữ cảnh đã chốt" map vào đó: một-nơi-nhìn-thấy-mọi-thứ (nút `Print all payloads` liệt kê đủ) · trùng-key-bắt-lúc-authoring (ca ⑨) · mọi-giá-trị-trong-vòng-flush (ca ④ cộng phép grep "không còn cửa ghi thứ hai") · quên-khởi-tạo-không-mất-tiến-độ (bảng Task 2, hàng "gọi `FlushAll` trước `Initialize`") · thêm-entry-không-sửa-SDK (`SaveCollection.Demo.cs` là bằng chứng sống — 6 dòng cho một entry mới).
- **Sau khi implement xong:** viết `Persistence.md` (tài liệu thiết kế cho agent) cạnh `Implementations/Foundations/Persistence/`, rồi sinh `.html` từ nó. Chuyển chín mục `§0` sang mục quyết định thiết kế của tài liệu đó — bốn mục "đã sai một lần" (§0.4, §0.5, §0.6, §0.9) là loại tri thức không đọc ra được từ code.
- **Khoá `"save." + Key` và định dạng JSON của payload là hợp đồng ra ngoài.** Dữ liệu trên máy người chơi bám vào đúng hai thứ đó. Đổi tiền tố hay đổi định dạng sau khi ship là mọi save cũ thành mồ côi — không có gì báo, chỉ là một ngày tất cả người chơi cũ mở game lên thấy tiến độ về 0.
- **Hệ dùng tiếp:** Audio (volume), Haptics, Economy (coin và lives), Rating, LiveOps. Cả bốn **nhận vào** `SaveEntry<T>` của mình qua Init chứ không tự khai — game khai entry trong partial class rồi nối vào, nên một hệ SDK vẫn bê lẻ sang project không dùng Persistence được.
