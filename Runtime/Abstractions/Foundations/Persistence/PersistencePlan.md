# Persistence Implementation Plan — SaveEntry + BaseSaveCollection + PlayerPrefs + Newtonsoft JSON

> **Loại tài liệu:** Plan — developer tự code lại để nắm logic. `.md` thiết kế + `.html` viết **sau** khi có source.
>
> **For agentic workers:** REQUIRED SUB-SKILL: superpowers:subagent-driven-development hoặc superpowers:executing-plans. Steps dùng checkbox (`- [ ]`).

**Goal:** Lưu tiến độ người chơi **có kiểu**, **quản lý tập trung tại một nơi nhìn thấy được**, **không mất khi app bị kill** (tối đa một chu kỳ autosave), **không god-blob**, và **không một type nào của dự án bị biên dịch vào assembly SDK**. Mọi thứ persist được — từ một cờ `bool` tới cả model tiến độ — đều là một `SaveEntry<T>`, và mọi `SaveEntry<T>` đều là field của **một** ScriptableObject duy nhất: một class dẫn xuất từ `BaseSaveCollection`, khai trong assembly của dự án. Collection lo quét, kiểm trùng key, load, autosave, flush và bước `PlayerPrefs.Save()`; entry chỉ giữ giá trị + cờ dirty + sự kiện on-change.

**Architecture:** 3 tầng, tổng **9 file** — 6 trong SDK (3 contract + 2 impl + 1 composite), 3 trong assembly của dự án.

```
Contract   (ISaveEntry, SaveEntry<T>,       key + default + dirty + on-change · CHỈ nói bằng chuỗi payload
            ISaveCollection)                 cửa Initialize/FlushAll/Flush · interface thuần, KHÔNG derive IService
            SDK, Abstractions

Máy móc    (BaseSaveCollection               abstract · quét field · kiểm key · load · autosave · flush hai giai
            + SaveDriver)                    đoạn · PlayerPrefs.Save() một lần mỗi lượt · hỏng → default + log
            SDK, Implementations

Domain     (IGameSave + GameSave)            model + key + giá trị mặc định + property có kiểu + [Service],
            dự án, com.FelixFelicis          khai lúc authoring — không type nào của nó lộ vào SDK
                                             partial: mỗi feature một cặp file class + interface
```

**Tech Stack:** C#, UniTask, `Sisus.Init` (`[Service]`), Odin Inspector (`[Button]`, `[GUIColor]` — DLL precompiled, mọi asmdef tự reference), Newtonsoft.Json (package `com.unity.nuget.newtonsoft-json`), PlayerPrefs, `System.Reflection`. Unity 6000.3. **Không** đụng asmdef của SDK, **không** thêm package nào, **không** Addressables, **không** toán.

## Global Constraints

| Ràng buộc | Giá trị |
|---|---|
| Namespace | SDK: `Horcrux.Runtime.Abstractions.Persistence` · `Horcrux.Runtime.Implementations.Persistence`. Dự án: namespace của chính nó (`FelixFelicis.Save` trong repo này) — **không** nằm dưới `Horcrux.*` |
| Ngôn ngữ trong code | Comment và XML doc viết **tiếng Anh**, khớp với toàn bộ `.cs` đang có trong SDK |
| Hiệu năng | Gán `Value` hoặc `MarkDirty` chạy theo nhịp **tương tác** — chỉ set cờ + phát event, **không** serialize. Serialize dồn về nhịp flush (autosave mặc định 5 giây + pause/quit) và chỉ chạm entry đang dirty. Reflection quét field và load: **một lần mỗi phiên**, có cache |
| Ngân sách dữ liệu | Tổng payload **vài chục KB**. `PlayerPrefs.Save()` ghi lại **toàn bộ** kho prefs mỗi lượt flush có entry dirty; vượt ngưỡng thì PlayerPrefs không còn là chỗ đúng nữa — xem mục "Ba giới hạn của cách lưu này". Con số thật lấy bằng nút `Print all payloads` ở Task 4 |
| SOLID | Collection chỉ biết `ISaveEntry`, không biết kiểu nào bên trong · entry không biết chỗ lưu, chỉ nói bằng chuỗi payload · hệ SDK **nhận vào** entry của mình, không tự khai · không type nào trong SDK mang ngữ nghĩa game, và không type nào của dự án được gọi tên trong SDK |
| Ranh giới assembly | SDK không tham chiếu assembly của dự án, và **không** có `.asmref` nào kéo file dự án vào SDK. Chiều phụ thuộc đúng một hướng: dự án → SDK |
| Editor-first | Key, giá trị mặc định và chu kỳ autosave đều là **cấu hình**, phơi ra Inspector. Kiểm trùng key chạy được **lúc authoring**, không cần Play |
| An toàn | Đọc fail → giữ giá trị mặc định + log, **không throw** · ghi fail → **giữ dirty**, log, chu kỳ sau thử lại · try/catch quanh **từng** callback `Changed` · autosave loop nhận `destroyCancellationToken` |
| Bất biến | ① key là **hợp đồng wire format** — chuỗi điền trong Inspector, không suy từ tên type hay tên field ② dirty reset ở **đúng một nơi** (collection), và chỉ **SAU** khi `PlayerPrefs.Save()` thành công ③ serialize chỉ xảy ra trong nhịp flush ④ **`PlayerPrefs.SetString` chỉ xuất hiện đúng một chỗ trong cả hệ** — không có cửa ghi thứ hai ⑤ không có đường "chạy no-op âm thầm": key trùng, key rỗng, field chưa gán, collection rỗng, ghi/đọc fail — đều có log |

## Ngữ cảnh đã chốt

| Nhóm | Chốt |
|---|---|
| **Ai gọi** | Dự án khai mọi `SaveEntry` trên `GameSave` — một `sealed partial class` dẫn xuất từ `BaseSaveCollection`, nằm trong assembly của dự án — và phơi chúng ra qua `IGameSave` (cùng khuôn với `RemoteConfigSystem.md`) · gameplay và UI gán `Value`, hoặc mutate model rồi `MarkDirty()`, hoặc subscribe `Changed` · hệ SDK **nhận vào** entry của mình qua Init, không tự khai · `FlushAll` hệ tự gọi (autosave + pause/quit); game gọi `Flush(entry)` để chốt sổ một entry sớm |
| **Mục tiêu** | Một nơi duy nhất nhìn thấy mọi thứ game lưu · trùng key bắt được **lúc authoring** · mọi giá trị persist được đều nằm trong vòng flush, không có ngoại lệ · quên khởi tạo không làm mất tiến độ · thêm entry mới không sửa SDK và không đụng entry khác · **model của dự án không bị biên dịch vào assembly SDK**, nên submodule commit được một mình |
| **Ngân sách** | Gán hoặc `MarkDirty`: mỗi tương tác, phải rẻ — chỉ cờ + event · serialize + `PlayerPrefs.Save()`: mỗi chu kỳ autosave, pause, quit, và mỗi lần `Flush(entry)` · reflection + load: một lần mỗi phiên. Không hot path mỗi frame |
| **Ranh giới** | SDK: `ISaveEntry`, `SaveEntry<T>`, `ISaveCollection`, `BaseSaveCollection`, `SaveDriver`, `SaveBootStep`, nút Editor, và `KeyPrefix` (wire format). Dự án: model, key, giá trị mặc định, interval, `IGameSave`, `GameSave`, một dòng `[Service]`, `[CreateAssetMenu]`, đường dẫn Resources, và kéo asset vào `SaveBootStep` |
| **Hướng phát triển thật** | Chuyển kho sang file trên đĩa khi chạm một trong ba giới hạn ở mục ngay dưới. Entry chỉ nói bằng **chuỗi payload** và không biết `PlayerPrefs` tồn tại, nên việc đó sửa nội bộ `BaseSaveCollection` — không đụng entry, không đụng code game |

**Những gì cố ý KHÔNG làm, kèm lý do** (*xoá nó đi thì hỏng ở đâu*):

| Không làm | Vì sao |
|---|---|
| `partial class SaveCollection` trong SDK + `.asmref` phía dự án | Mọi phần của một `partial` phải cùng một assembly, nên nửa của dự án buộc phải biên dịch vào `com.horcrux.runtime` — và type nó **gọi tên** (`SaveEntry<PlayerProgress>`) cũng phải nằm trong đó, kéo theo cả chuỗi phụ thuộc của model. Kết quả: file nằm trong thư mục dự án nhưng domain thuộc về SDK, và một model cần thứ gì của `com.FelixFelicis` là bế tắc — chiều ngược lại là circular reference. Ràng buộc §0.10 |
| Cho `ISaveCollection` derive `IService<ISaveCollection>` | Interface của dự án derive cả nó lẫn `IService<IGameSave>` sẽ thừa hưởng **hai** thành viên `Service` → mọi lần đọc `IGameSave.Service` là lỗi biên dịch CS0229. Ràng buộc §0.10 ② |
| Đăng ký `ISaveCollection` thành service thứ hai | Chỗ duy nhất trong SDK cần collection là `SaveBootStep`, mà nó là MonoBehaviour nên **nhận reference kéo thả** được — "step này flush collection nào" là quyết định lúc authoring, không phải lúc chạy. Một dòng `[Service]` là đủ, đúng bằng `GameRemoteConfigCollection`, và bề mặt bẫy CS0229 biến mất khỏi phía dự án |
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
| `IService<T>` (`Abstractions/Foundations/IService.cs`) | **Dùng lại**, nhưng ở **phía dự án**: `IGameSave` derive nó, `ISaveCollection` thì không (§0.10). Save là hệ **bắt buộc** — thiếu asset trong `Resources/Config/` là lỗi cấu hình, phải lộ ngay lần Play đầu, nên `IService` (throw) chứ không `IOptionalService` |
| `BaseRemoteConfigCollection` + `RemoteConfig<T>` + `MarkedRemoteConfig` (`RemoteConfigSystem.md`) | **Dùng lại khuôn**, không dùng lại code: cùng cơ chế abstract base trong SDK + subclass `sealed partial` của dự án + attribute + reflection quét field + một dòng `[Service]` + `partial` chia entry theo feature. Hai hệ cố ý dùng **một** khuôn, nên bốn ràng buộc §0.10 là ràng buộc chung của cả hai, và mọi chỗ hai bên khác nhau phải là chỗ **cố ý** kèm lý do. Ba chỗ cố ý đảo ngược so với `RemoteConfig<T>` nằm ở bảng trong Task 1 — lý do gốc của chúng là *"cho developer thấy giá trị fetch về ngay trong asset"*, lý do đó không còn đúng khi thứ được lưu là dữ liệu của người chơi. Hai chỗ Persistence **thêm** so với khuôn cũ: cặp hook `ResetDerivedState`/`OnEntriesLoaded` (§0.7) và `EnsureInitialized` (§0.9) |
| `SplitterAttribute` (`Runtime/Utilities/Attribute/`) | **Dùng lại** — `[Splitter("Economy")]` kẻ tiêu đề nhóm trong Inspector, đúng thứ `GameRemoteConfigCollection` đang dùng để một asset 27 biến còn đọc được. Không cần thêm cơ chế gom nhóm nào khác |
| `BootStep` + `BootstrapRunner` | **Dùng lại** cho `SaveBootStep` tuỳ chọn ở nhánh Composites. `BootStep` đã có sẵn `OnAppPause`/`OnAppQuit`, và runner fan-out **ngược thứ tự** cho cả hai — đó chính là thứ magic method của một MonoBehaviour lẻ không hứa được |
| `EventBus` (Utilities) | **Không dùng** — `Changed` là event nội bộ một entry, listener wire trực tiếp |
| `MonoSingleton` | **Không dùng** — đăng ký qua `[Service]` trên class của dự án, cùng khuôn `BaseRemoteConfigCollection` |
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

## §0. Mười ràng buộc thật

Không có toán. Năm sự thật của nền tảng, bốn bug có thật trong repo, và một ranh giới của C# cộng Unity quyết định hình dạng code — đọc trước khi viết.

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
3. **Không gian khoá là phẳng và dùng chung với mọi hệ khác.** `RemoteConfig<T>` trong chính SDK này ghi cache của remote config vào PlayerPrefs bằng **key thô, không tiền tố**; SDK quảng cáo và analytics cũng ghi vào đó. Tiền tố `"save."` vì thế không phải trang trí — nó là thứ chặn một firebase key trùng tên đè lên tiến độ người chơi. Đổi tiền tố này sau khi ship là mọi save đang có ngoài đời thành mồ côi.

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

### 0.7. ScriptableObject không chết giữa hai lần Play — bốn thứ phải reset

Asset của một ScriptableObject được Unity load một lần và **giữ nguyên qua các lần Play trong Editor**. Với Enter Play Mode Settings tắt domain reload, mọi field `[NonSerialized]` mang giá trị của phiên trước sang phiên sau. Bốn thứ hỏng theo:

| Thứ sống sót | Ai sở hữu | Triệu chứng |
|---|---|---|
| Listener của `Changed` | entry | listener đăng ký ở phiên trước trỏ vào GameObject đã huỷ → `MissingReferenceException` ở lần đổi giá trị đầu tiên của phiên mới |
| Giá trị runtime | entry | xoá save ngoài đĩa rồi Play lại vẫn thấy giá trị cũ — chỉ xảy ra trong Editor, nên nó là bug lộ ra ở máy developer và **biến mất** trên build, loại khó tin nhất |
| Cờ dirty | entry | flush đầu phiên ghi lại thứ không ai đổi |
| **Cache do subclass dựng từ giá trị entry** | **dự án** | lookup, `HashSet`, cờ "đã parse xong" mang số liệu phiên trước — cùng loại khó tin nhất ở hàng trên, nhưng `ResetRuntimeState()` của entry **không** với tới được |

Ba hàng đầu chặn bằng `Initialize()` gọi `ResetRuntimeState()` cho **mọi** entry trước khi load — nó xoá listener, đưa giá trị về một bản sao mới của mặc định, và tắt dirty. Bắt từng hệ tự `-=` là chặn bằng kỷ luật, và sẽ vỡ ở hệ thứ hai.

Hàng thứ tư cần một cửa riêng, vì base không biết subclass cache những gì. Đó là cặp hook `ResetDerivedState()` (trước khi load, xoá cache) và `OnEntriesLoaded()` (sau khi load, dựng lại) ở Task 2.

*Đã sai một lần — hệ Remote Config, cùng khuôn, thiếu đúng cái hook thứ nhất:* `BaseRemoteConfigCollection.Initialize()` reset `ResetFetchedState()` cho từng biến nhưng không có hook nào cho subclass. Nên `GameRemoteConfigCollection` khai `private bool _hasAppliedRemoteValues`, chỉ được bật trong `OnRemoteConfigsApplied()` và **không ai tắt** — cờ mang trạng thái phiên trước sang phiên sau. Cái giá không phải một lần sửa: một hệ khác đã phải viết workaround quanh nó, và ghi lý do vào comment — *"không dùng `HasAppliedRemoteValues` — cờ đó quan sát được là mang trạng thái cũ qua các lần Play trong Editor, bật sớm khi fetch chưa về."* Một cờ không reset được đã làm một hệ hạ tầng mất tư cách làm điều kiện chờ.

Lưu ý về **thứ tự**: `OnRemoteConfigsApplied()` là hook "sau", và khuôn cũ **chỉ** có nó — đó chính là chỗ hở. Hook "sau" dựng lại cache, nhưng nếu load lần này không chạm tới một entry (chưa có gì trong kho) thì cache cũ vẫn nằm đó. Nên hook "trước" là cái không bỏ được, không phải cái tuỳ chọn.

### 0.8. Asset là dữ liệu authoring — tiến độ người chơi không được chạm vào nó

`[SerializeField]` trên một field nghĩa là Unity ghi giá trị của nó vào file `.asset` trong project. Hai chỗ phải cẩn thận, và cả hai đều là chỗ khuôn `RemoteConfig<T>` **cố ý** làm ngược:

1. **Giá trị runtime phải `[NonSerialized]`.** `RemoteConfig<T>` khai `[SerializeField] private T value` và `ApplyRemoteValue` ghi thẳng vào đó — chủ ý, để developer thấy giá trị fetch về ngay trong asset. Bê nguyên sang save là mỗi lần Play trong Editor, tiến độ người chơi được ghi vào asset của project rồi đi vào version control.

   **Nhưng "không serialize" không có nghĩa là "không nhìn thấy" — đó là hai trục, và Unity dán chúng vào nhau.** Unity vẽ Inspector *từ* dữ liệu đã serialize, nên `[NonSerialized]` trơn là ẩn hẳn. Tách hai trục bằng `[ShowInInspector]` của Odin: nó đọc field sống qua reflection và vẽ, không cần Unity biết field đó tồn tại.

   | Trục | Ai quyết | `value` và `isDirty` chọn gì |
   |---|---|---|
   | Có ghi vào file `.asset` không | `[SerializeField]` / `[NonSerialized]` | **không** — đây là dữ liệu người chơi |
   | Có hiện trong Inspector không | Unity chỉ vẽ field đã serialize · Odin `[ShowInInspector]` vẽ bất kể | **có** — nghiệm thu của hệ này bắt kiểm `IsDirty` ở sáu ca |
   | Sửa được bằng tay không | Odin `[ReadOnly]` | **không** — xem đoạn dưới |

   ⚠️ **Đường sai mà ai cũng nghĩ ra đầu tiên: `[SerializeField]` bọc trong `#if UNITY_EDITOR`.** Editor biên dịch field đó vào layout và ghi asset kèm nó; build strip field đi nên layout ngắn hơn; build đọc asset là lệch byte → **crash native** `Read N bytes but expected M bytes`. Không lộ ở Editor, chỉ nổ trên build, ở tầng không có stack trace C#. Đã có người trả tiền cho ca này rồi — comment còn nguyên trong `RemoteConfig.cs`, và `RemoteConfigSystem.md` đưa nó vào bảng Bẫy.

   ⚠️ **`[ShowInInspector]` trên private field làm nó SỬA ĐƯỢC — nên phải kèm `[ReadOnly]`.** Sửa `value` bằng tay trong Inspector là gán vào field mà **không** đi qua setter: không `MarkDirty()`, không bắn `Changed`. Giá trị đổi trên màn hình, rồi flush không thấy entry nào dirty nên không ghi gì, và không có dòng log nào — đúng loại đường "chạy no-op âm thầm" mà bất biến ⑤ cấm. `RemoteConfig<T>` để `fetched` sửa được là vô hại vì không logic nào đọc nó; `value` của save thì không. Đường sửa giá trị bằng tay đã có, và nó đi qua cửa đúng: nút `ImportPayload`.
2. **Giá trị mặc định phải được sao chép, không được trả thẳng.** `defaultValue` là một object sống trong asset. Gán `value = defaultValue` cho một model class là để game mutate thẳng vào asset. Cách chặn: một vòng round-trip qua serializer, chạy **một lần mỗi entry lúc `Initialize`** — đủ rẻ, và là bản sao đúng cho mọi `T`, kể cả struct có `List` bên trong.

### 0.9. Bước khởi tạo bắt buộc mà quên gọi thì mất dữ liệu — không được để nó phụ thuộc trí nhớ

`BaseRemoteConfigCollection.Initialize()` là bước SDK **không** tự gọi: dự án phải gọi một lần lúc boot, và trong repo này dòng đó nằm ở `ServiceInit.Initialization()` — một file của **dự án**, không của SDK. Khuôn đó chạy được, nhưng nó đặt một bảo đảm của hệ vào một dòng mà mỗi host mới phải tự nhớ viết lại.

Cái quyết định hình dạng ở đây là **bất đối xứng về giá của việc quên**:

| Hệ | Quên gọi `Initialize()` thì sao |
|---|---|
| Remote Config | biến rơi về giá trị author trong asset. Sai, nhưng **hồi được**: fetch lần sau đúng, và không dữ liệu nào bị phá |
| Save | entry mang giá trị mặc định, rồi flush kế tiếp **ghi đè mặc định đó lên save thật**. Mất tiến độ người chơi, và mất **không hồi được** |

Một bảo đảm mà cái giá của việc phá là không hồi được thì không được đứng trên trí nhớ của người viết dòng wire (§3.8 — chặn bằng cấu trúc, không bằng kỷ luật).

Nên `Initialize()` ở hệ này có **hai lối vào cùng chạy một thân idempotent**: gọi tường minh từ `SaveBootStep` để trả chi phí load ở nơi có màn hình loading và để lỗi save lộ ra trong pha boot, hoặc không gọi thì `EnsureInitialized()` ở mọi cửa vào công khai tự lo. Đây là chỗ cố ý đi khác khuôn `BaseRemoteConfigCollection`, và lý do đi khác là bảng ngay trên.

### 0.10. Ranh giới assembly — bốn sự thật quyết định vì sao khuôn là kế thừa, không phải `partial`

Domain của dự án phải nằm trong assembly của dự án. Bốn sự thật dưới là lý do khuôn có hình dạng này; cả bốn đã kiểm bằng phép thử chạy được, đừng suy lại từ trực giác.

**① `partial` không đi qua được ranh giới assembly, và nó kéo theo cả model.** Mọi phần của một `partial` type phải được biên dịch trong **cùng một** assembly. Nên một `partial class SaveCollection` khai trong `com.horcrux.runtime` buộc nửa của dự án phải đi vào assembly đó — bằng `.asmref`. Nhưng nửa đó **gọi tên** `SaveEntry<PlayerProgress>`, và một type được gọi tên bên trong `com.horcrux.runtime` phải tra được từ danh sách references của assembly đó. `PlayerProgress` nằm trong `com.FelixFelicis`, mà `com.FelixFelicis` đã reference `com.horcrux.runtime` — thêm chiều ngược lại là **circular reference**, Unity từ chối biên dịch. Nên model, và mọi thứ model phụ thuộc, bị kéo vào assembly SDK theo. Kế thừa không có vấn đề này: subclass **là** một type của dự án, và nó chỉ gọi tên xuống phía SDK.

**② Contract của SDK không được derive `IService<>`.** `IService<out T>` khai `Service` là thành viên **static** ngay trong interface. C# **cho** đọc thành viên static của interface nền qua tên interface dẫn xuất — đó là lý do `ILevelCheater.Service` hiện nay biên dịch được. Nhưng một interface thừa hưởng **hai** instantiation khác nhau của `IService<>` thì cái tên `Service` nhập nhằng:

```csharp
public interface ISaveCollection : IService<ISaveCollection> { }              // nếu giữ IService ở contract
public interface IGameSave : ISaveCollection, IService<IGameSave> { }

var x = IGameSave.Service;
// error CS0229: Ambiguity between 'IService<ISaveCollection>.Service' and 'IService<IGameSave>.Service'
```

Đây là lỗi **biên dịch**, và nó nổ ở đúng thứ khuôn này tồn tại để giữ: call site không cast. Nên `ISaveCollection` là interface thuần, và chỉ `IGameSave` derive `IService<>` — giống `IRemoteConfigCollection` với `IGameRemoteConfigCollection`.

Hệ quả: **không gì trong SDK resolve `ISaveCollection` qua service locator.** Chỗ duy nhất trong SDK cần collection là `SaveBootStep`, và nó nhận reference kéo thả (Task 6) — nên phía dự án chỉ có **một** dòng `[Service]`, đúng bằng `GameRemoteConfigCollection`. `ISaveCollection` vẫn có người dùng: `IGameSave` derive nó, nên call site của dự án thấy `Initialize`, `FlushAll`, `Flush`, `Entries`. Nó là **hợp đồng để derive**, không phải service type để tra.

**③ `[Service]` không di truyền.** `ServiceAttribute` khai `AllowMultiple = true, Inherited = false`. `Inherited = false` nghĩa là một `[Service]` viết trên `BaseSaveCollection` **không** áp cho subclass: service không được đăng ký, và `Service.Get<>()` ném exception ở lần chạm đầu tiên. Nên `[Service]`, `[CreateAssetMenu]` và đường dẫn Resources đều thuộc dự án — đúng ba thứ mà `RemoteConfigSystem.md` cũng liệt kê là "SDK không khai hộ được".

**④ Reflection thấy private field của subclass, không thấy của base.** `GetType()` trả về type **thật lúc chạy**, nên `GetEntryFields()` viết trong `BaseSaveCollection` lấy đúng những `private` field mà `GameSave` khai — kể cả khi field đó khai ở một file `partial` khác, vì mọi phần `partial` là **cùng một** type. Mặt còn lại: `GetFields` **không** trả về private field khai trên chính base class. Nên `BaseSaveCollection` không thể tự khai một entry private rồi mong nó vào danh sách — nó sẽ bị bỏ qua **không có lỗi nào**. Hôm nay SDK không khai entry nào nên không ảnh hưởng; ngày nào cần thì field đó phải là `protected`.

Kèm theo là **convention của field entry: `private` + `[SerializeField]`**, giống `RemoteConfigSystem.md` ("field để `private`: `public` là cho mọi caller gán được cả tham chiếu biến, việc chỉ authoring được làm"). Vì convention đã chốt vậy, scan chỉ quét `NonPublic | Instance` — thêm `Public` là mở cửa cho một hình dạng field mà convention cấm, và mở im lặng.

**Một bẫy đi kèm, không có trong bản `partial`:** magic method của Unity bị subclass che. Với `partial` chỉ có một class nên không có gì che nhau; với kế thừa, một `OnDestroy` hay `OnDisable` khai trên `GameSave` sẽ che bản của base, Unity gọi bản của subclass, và việc dọn dẹp của base **không chạy** — im lặng. Hệ này hôm nay không dùng magic method nào trên collection, nên chưa chạm; nếu về sau thêm thì khai `protected virtual` để `override` là đường duy nhất.

---

## Bản đồ triển khai

| Task | Ở đâu | File | Nội dung |
|---|---|---|---|
| 1 | SDK | `Abstractions/Foundations/Persistence/` — `ISaveEntry.cs` · `SaveEntry.cs` · `ISaveCollection.cs`; **xoá** `ISaveUnit.cs` | 3 contract |
| 2 | SDK | `Implementations/Foundations/Persistence/BaseSaveCollection.cs` | lõi collection, abstract |
| 3 | SDK | `Implementations/Foundations/Persistence/SaveDriver.cs` | autosave + pause/quit |
| 4 | SDK | `BaseSaveCollection.cs` (thêm khối `#if UNITY_EDITOR`) | 3 nút Editor |
| 5 | **Dự án** | `IGameSave.cs` · `GameSave.cs` · `DemoSaveDriver.cs` trong `com.FelixFelicis` | cặp file của dự án + demo + nghiệm thu |
| 6 | SDK | `Implementations/Composites/Persistence/SaveBootStep.cs` | flush có thứ tự, tuỳ chọn |

Thứ tự: **1 → 2 → 3 → 4 → 5 → 6**. Task 4 sửa file của Task 2.

**Task 5 là chỗ hệ chạy được lần đầu, và đó là hệ quả trực tiếp của §0.10.** Không có subclass của dự án thì không tạo được asset, không đăng ký được service, và không có entry nào để quét — nên **Task 1–4 không nghiệm thu bằng Play được**, chỉ nghiệm thu bằng "compile sạch". Đây không phải thiếu sót của plan: SDK cố ý không tự chạy được một mình, vì tự chạy được nghĩa là nó đang mang một entry, và một entry là domain. Task 6 làm sau Task 5 vì phép kiểm thứ tự flush của nó cần một entry thật để nhìn.

---

### Task 1: 3 contract

**Files:**
- Create: `Assets/Horcrux/Runtime/Abstractions/Foundations/Persistence/ISaveEntry.cs`
- Create: `Assets/Horcrux/Runtime/Abstractions/Foundations/Persistence/SaveEntry.cs`
- Create: `Assets/Horcrux/Runtime/Abstractions/Foundations/Persistence/ISaveCollection.cs`
- **Delete:** `Assets/Horcrux/Runtime/Abstractions/Foundations/Persistence/ISaveUnit.cs` (+ `.meta`)

**Interfaces:**
- Consumes: `IService<T>` (`Abstractions/Foundations/IService.cs`) · `Newtonsoft.Json.JsonConvert` · Odin `[ShowInInspector]`, `[ReadOnly]` ở **scope runtime** (an toàn: `Sirenix.OdinInspector.Attributes.dll` là assembly runtime, và `RemoteConfig<T>` đã khai `[NonSerialized, ShowInInspector]` ngoài `#if UNITY_EDITOR`) + `[Button]`, `[MultiLineProperty]` trong khối `#if UNITY_EDITOR`.
- Produces: `ISaveEntry` (2 property + 4 method) · `SaveEntry<T>` (dự án khai) + 1 nút Editor `ImportPayload` · `interface ISaveCollection` (1 property + 3 method, **không** derive `IService<>`).

**Quyết định thiết kế:**

| Quyết định | Lý do |
|---|---|
| Một `SaveEntry<T>` cho **cả** giá trị lẻ và model | Hai cỡ dữ liệu chỉ khác nhau ở cách biến thành chuỗi. Dựng kiểu thứ hai cho một bước là đặt giá trị lẻ ra ngoài cờ dirty và ngoài `PlayerPrefs.Save()` — nghĩa là ra ngoài chính bảo đảm "mất tối đa một chu kỳ" |
| `int`/`bool`/`float` đi qua JSON thay vì `GetInt`/`GetFloat` native | Giá trị đã cache trong RAM nên phép parse chạy **một lần lúc boot** và `ToString` **một lần mỗi lượt flush** — không đo nổi. Đổi lại toàn hệ chỉ còn một đường đọc, một đường ghi, một chỗ kiểm |
| `ISaveEntry` non-generic + `SaveEntry<T>` generic | Collection cần một `List<ISaveEntry>` đồng nhất; phần typed nằm ở lớp game khai. Game **không** implement `ISaveEntry` trực tiếp |
| Bốn method của `ISaveEntry` cài **explicit** | `ResetRuntimeState`, `ReadPayload`, `WritePayload`, `ClearDirty` chỉ collection được gọi. Explicit interface implementation làm chúng **biến mất** khỏi IntelliSense của game — chặn bằng cấu trúc, không bằng dòng doc "chỉ collection gọi" |
| Entry chỉ nói bằng **chuỗi payload**, không biết `PlayerPrefs` | Đây là chỗ duy nhất đáng phòng xa, và nó tốn 0 dòng: chuyển kho sang file sau này chỉ sửa nội bộ collection |
| `[NonSerialized] T value`, `[SerializeField] T defaultValue` | Ranh giới sở hữu chia theo **nguồn** của giá trị: `defaultValue` do developer đặt lúc authoring nên nó thuộc asset và thuộc git; `value` do người chơi sinh ra nên nó thuộc máy người chơi. Đảo ngược có chủ ý so với `RemoteConfig<T>` — lý do gốc của khuôn cũ là cho developer thấy giá trị fetch về trong asset, lý do đó không còn đúng khi thứ được lưu là tiến độ người chơi |
| `value` và `isDirty` thêm `[ShowInInspector, ReadOnly]` | Không serialize **và** vẫn nhìn thấy — hai trục khác nhau (§0.8). Sáu ca nghiệm thu của hệ này bắt developer kiểm `IsDirty`, ẩn nó đi là để log làm đường duy nhất. `[ReadOnly]` vì `[ShowInInspector]` trên private field làm nó sửa được, mà sửa tay là bỏ qua setter → không `MarkDirty`, không `Changed`, flush không ghi gì, **không log gì** (bất biến ⑤) |
| `CloneDefault()` round-trip qua serializer, một nhánh duy nhất | `defaultValue` sống trong asset; trả thẳng nó ra là để game mutate vào asset. Round-trip là bản sao đúng cho **mọi** `T` — kể cả struct chứa `List` — nên không cần nhánh riêng cho value type |
| `(object)x == null` thay vì `x == null` | `==` trên type parameter không ràng buộc **không compile được**. Cast về `object` là cách kiểm null hợp lệ duy nhất ở đây |
| `Value` có setter, **và** có `MarkDirty()` | Gán cả giá trị là nhịp tự nhiên của giá trị lẻ; mutate rồi báo là nhịp tự nhiên của model. Một kiểu phục vụ cả hai mà không cần biết mình đang là loại nào |
| `implicit operator T` | Tiền lệ `RemoteConfig<T>` đã có, và nó làm call site đọc gọn: `if (collection.HasRated)`. **Bẫy đi kèm:** `entry == null` so **giá trị** chứ không so entry — với `SaveEntry<string>` thì đó là `string == null`. Muốn kiểm chính field thì `ReferenceEquals(entry, null)`. Cùng bẫy `RemoteConfig<T>` đã ghi, vì cùng operator |
| Nút `ImportPayload` trên **từng entry**, chỉ chạy trong Play mode | Nhu cầu thật: tái lập bug ở một trạng thái tiến độ cụ thể, mà hiện không có đường nào ngoài chơi lại từ đầu. Đi qua setter `Value` nên nó vẫn `MarkDirty` + bắn `Changed`, và `PlayerPrefs.SetString` vẫn chỉ có **một** cửa duy nhất trong collection (bất biến ④). Chặn ngoài Play mode vì `value` là `[NonSerialized]` — import ở đó là ghi vào field sẽ biến mất |
| **Không** làm nút xoá riêng từng entry | Chưa có nhu cầu gọi được tên; `Delete all save data` ở Task 4 đang phủ. Ghi ở mục "Mở rộng sau" |
| **Không** làm đường CSV như `RemoteConfig<T>` | Save là dữ liệu **người chơi**, không phải dữ liệu author — đường qua spreadsheet không có người dùng |
| `Changed` là `event Action<T>`, fan-out qua `GetInvocationList` + try/catch từng listener | Đăng ký thưa nên `event` là đúng mức. Một listener ném exception không được kéo cả hệ chết. Alloc theo nhịp tương tác, không theo frame |
| `ReadPayload` cũng bắn `Changed` | "Value đổi thì `Changed` bắn" là **một** luật không ngoại lệ |
| `ISaveCollection` là interface **thuần** — không `partial`, không derive `IService<>` | `partial` không đi qua ranh giới assembly nên nửa của dự án sẽ kéo cả model vào SDK; và derive `IService<>` ở đây làm `IGameSave.Service` nhập nhằng, lỗi CS0229 (§0.10 ① và ②). Property có kiểu của dự án nằm trên `IGameSave` ở Task 5 |

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
using Sirenix.OdinInspector;
using UnityEngine;

namespace Horcrux.Runtime.Abstractions.Persistence
{
    /// <summary>One saved value of any size — a flag, a counter, or a whole progress model. Declared as a field on the project's save collection.</summary>
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

        // Runtime only, and visible without being stored — two separate axes.
        // NonSerialized: serializing these would write the player's progress into the project asset, and into git.
        //   A [SerializeField] wrapped in #if UNITY_EDITOR is NOT the way to get them on screen: the Editor would
        //   store the field while the build strips it, and reading the asset then dies natively on a byte mismatch.
        // ShowInInspector: the six acceptance cases of this system ask a developer to check IsDirty, so hiding it
        //   leaves the log as the only way to see it.
        // ReadOnly: a hand edit here would bypass the setter — no MarkDirty, no Changed, so the value changes on
        //   screen and the next flush finds nothing dirty and writes nothing, silently. ImportPayload is the door.
        [NonSerialized, ShowInInspector, ReadOnly] private T value;
        [NonSerialized, ShowInInspector, ReadOnly] private bool isDirty;

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

#if UNITY_EDITOR
        // Not authored data and not player data — just an Editor input box, so it shows without being stored.
        [NonSerialized, ShowInInspector, MultiLineProperty]
        private string payloadToImport;

        /// <summary>Replaces the live value with the pasted payload, so a bug can be reproduced at an exact progress.</summary>
        /// <remarks>Play mode only: outside it the live value is not loaded and is not stored, so an import there
        /// would write to a field that vanishes. Goes through MarkDirty like any other change, which keeps
        /// PlayerPrefs.SetString to the one door inside the collection.</remarks>
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
    /// <summary>The one place every saved value lives. A game project derives its own service interface from this one.</summary>
    /// <remarks>
    /// The project side owns the entries: it declares a partial interface deriving both this one and
    /// <see cref="IService{T}"/> closed over itself, holding one typed property per entry, and a sealed partial
    /// class deriving BaseSaveCollection that implements it. That is what keeps every model and key inside the
    /// project's own assembly while a call site still reads a value with no cast. Registered as a REQUIRED service
    /// by that class under its OWN interface: a missing asset is a setup error, not a runtime case.
    /// <para>
    /// This interface must NOT derive <see cref="IService{T}"/>. A project interface deriving both would inherit
    /// two static Service members, and every read of it fails to compile with CS0229, ambiguity. So this is a
    /// contract to DERIVE, never a service type to look up — nothing in the SDK resolves it, and SaveBootStep
    /// takes the collection as a serialized reference instead.
    /// </para>
    /// </remarks>
    public interface ISaveCollection
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

### Task 2: `BaseSaveCollection` — lõi

**Files:**
- Create: `Assets/Horcrux/Runtime/Implementations/Foundations/Persistence/BaseSaveCollection.cs`

**Interfaces:**
- Consumes: `ISaveEntry` · `ISaveCollection` (Task 1) · `PlayerPrefs` · `System.Reflection`.
- Produces: `RegisteredSave` (attribute) · `abstract class BaseSaveCollection : ScriptableObject, ISaveCollection` — `Initialize()` · `FlushAll()` · `Flush(ISaveEntry)` · `Entries` · `AutosaveIntervalSeconds` · `const string KeyPrefix` · `protected virtual ResetDerivedState()` · `protected virtual OnEntriesLoaded()`.

**Quyết định thiết kế:**

| Quyết định | Lý do |
|---|---|
| ScriptableObject, không MonoBehaviour trong scene | Không phải đặt GameObject vào scene entry, sống qua mọi lần đổi scene, và key hiện trong Inspector của một asset tra được bằng Project window. Cùng khuôn `BaseRemoteConfigCollection` |
| `abstract`, và **không** mang `[Service]`, `[CreateAssetMenu]`, hay đường dẫn Resources | `Inherited = false` nên `[Service]` viết ở đây không tới được subclass; `[CreateAssetMenu]` trên type abstract tạo ra một menu item luôn thất bại; đường dẫn asset là quyết định của dự án (§0.10 ③). Ba thứ đó nằm ở Task 5 |
| `KeyPrefix` **ở lại** SDK | Nó là **wire format**, không phải cấu hình của dự án: đổi nó là mọi save đang có trên máy người chơi thành mồ côi. Để mỗi dự án tự đặt là mời mỗi dự án tự tạo ra một lần đổi không hồi lại được |
| `GetEntryFields()` dùng `NonPublic \| Instance` | Khớp `BaseRemoteConfigCollection` — hai khî̀n phải quét giống nhau, lệch là hai luật từ một luật. Quét trên `GetType()` nên nó thấy private field mà subclass khai, kể cả khai ở file `partial` khác. Nó **không** thấy private field khai trên chính base — nên base không được tự khai entry (§0.10 ④) |
| `Initialize()` public **và** `EnsureInitialized()` lazy ở mọi cửa vào | Quên gọi thì entry mang giá trị mặc định và flush kế **ghi đè giá trị mặc định lên save thật**. Đây là chỗ cố ý khác khuôn cũ: hai lối vào, một thân idempotent |
| `ScanEntries()` là **một** thân dùng chung cho `Initialize` và nút `Validate keys` | Hai bản kiểm sẽ lệch nhau, và lệch kiểu nguy hiểm nhất: nút báo sạch trong khi runtime vẫn bỏ entry. Cùng một hàm thì không thể lệch |
| Kiểm ba thứ: field chưa gán · key rỗng · trùng key — mỗi lỗi một `LogError` nêu **tên field** | Nêu key thôi thì không đủ để mở Inspector tìm; tên field là thứ developer nhìn thấy trên màn hình |
| Trùng key → **bỏ entry sau**, giữ entry trước | Hai entry một key là entry sau đè payload entry trước. Giữ entry trước là giữ cái đã có dữ liệu ngoài đời |
| `FlushAll()` và `Flush(entry)` cùng gọi `FlushInternal(only)` | Hai đường làm gần cùng một việc thì đường hẹp phải chạy trong thân đường rộng — nếu không, một ngày `PlayerPrefs.Save()` chỉ còn ở một bên |
| `Flush(entry)` từ chối entry collection không sở hữu | Ghi một entry chưa đăng ký là tạo một khoá **không ai load lại lúc boot** — dữ liệu ghi ra rồi biến mất, không có gì báo. Đây thường là dấu hiệu quên `[RegisteredSave]` |
| `PlayerPrefs.Save()` gọi **một lần** cho cả lượt, chỉ khi có entry vừa ghi | Save ghi lại toàn bộ kho, nên gọi n lần cho n entry là ghi cả kho n lần. Không entry nào dirty thì không gọi — flush rỗng phải thật sự không chạm đĩa |
| `pendingClear` là field `List<ISaveEntry>` tái dùng, `Clear()` trong `finally` | Danh sách sống trong đúng một lượt flush; tái dùng thì không alloc theo chu kỳ, và `finally` đảm bảo lượt sau bắt đầu từ rỗng kể cả khi persist ném exception |
| Reflection quét field có cache `List<FieldInfo>`, vòng lặp tay | Chạy một lần mỗi phiên nên LINQ cũng đủ rẻ; chọn vòng lặp tay vì `BaseRemoteConfigCollection` viết thế, và hai bản của một khî̀n đọc cạnh nhau mới đối chiếu được |
| Cặp hook `ResetDerivedState()` / `OnEntriesLoaded()` | Base reset được trạng thái của **entry**, không với tới cache mà subclass dựng từ giá trị entry. Thiếu cửa này là cách hệ Remote Config đã sinh ra một cờ mang trạng thái phiên trước (§0.7). Phải đủ **cả hai** nửa: chỉ có hook "sau" thì lượt load không chạm entry nào — đúng lần chạy đầu — vẫn để cache cũ sống |
| `Entries` không hứa thứ tự khai báo | `Type.GetFields` không đảm bảo thứ tự. Hứa một thứ tự không có là mở đường cho code sau dựa vào nó |
| Flush khi 0 entry → `LogWarning` đúng một lần | Một hệ save không bao giờ được no-op trong im lặng |

**Editor setup:** không có. Type này `abstract` nên không tạo được asset từ nó — asset và mọi bước Inspector thuộc Task 5, sau khi dự án đã có subclass.

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
    /// <summary>Marks a SaveEntry field on the project's save collection as one the collection owns. A field without it is ignored.</summary>
    [AttributeUsage(AttributeTargets.Field)]
    public sealed class RegisteredSave : Attribute { }

    /// <summary>Owns every saved value: scans the declared entries, loads them, autosaves, and flushes on pause and quit.</summary>
    /// <remarks>
    /// Storage is PlayerPrefs — one entry per key, holding the value as JSON. The contract is "lose at most ONE
    /// autosave cycle": on Android a swipe-kill never runs OnApplicationQuit, so pause is the last signal we trust.
    /// <para>
    /// A game project derives one sealed class from this IN ITS OWN ASSEMBLY and declares the entries there, so no
    /// save model ever has to be compiled into this SDK. The derived class carries the three things missing here on
    /// purpose — the Service registration, the CreateAssetMenu entry and the Resources path — because
    /// ServiceAttribute is declared Inherited = false and a registration written here would never reach it. Nothing
    /// registers itself at runtime, which is what makes a duplicate key catchable while authoring rather than after
    /// shipping.
    /// </para>
    /// <para>
    /// The scan reads the runtime type, so it picks up the private fields of the derived class. It does NOT pick up
    /// private fields declared on this class: an entry added here would have to be protected, and one left private
    /// is dropped with no error at all.
    /// </para>
    /// </remarks>
    public abstract class BaseSaveCollection : ScriptableObject, ISaveCollection
    {
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

            ResetDerivedState();       // before the load: whatever the subclass cached is now from a dead session
            isInitialized = true;      // set before loading: nothing below may re-enter through a guarded member
            LoadAll();
            OnEntriesLoaded();         // after the load: every entry now holds what storage had
            EnsureDriver();
        }

        /// <summary>Override to drop anything cached from entry values. Runs on every Initialize, BEFORE the load.</summary>
        /// <remarks>
        /// A ScriptableObject outlives a play session, so a lookup or an "already parsed" flag left here carries last
        /// session's numbers into this one — wrong only on a developer's machine, the hardest kind to believe.
        /// Resetting the entries cannot reach this: only the subclass knows what it derived from them.
        /// The pair must be BOTH halves. Clearing only in OnEntriesLoaded leaves a stale cache alive whenever
        /// this load touches no entry, which is exactly the first run.
        /// </remarks>
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

        /// <summary>The entry fields the derived class declares, cached for the session.</summary>
        /// <remarks>NonPublic only, matching BaseRemoteConfigCollection: an entry field is private + [SerializeField]
        /// by convention, so scanning public as well would quietly accept a shape that convention forbids.</remarks>
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
| Subclass override `ResetDerivedState` + `OnEntriesLoaded`, log một dòng mỗi hook | thứ tự log đúng: `ResetDerivedState` → (load) → `OnEntriesLoaded`, và cả hai chạy **mỗi** lần `Initialize` |
| Subclass cache một lookup từ entry, Play → đổi giá trị → Stop → Play lại (domain reload tắt) | lookup dựng lại từ giá trị vừa load, **không** mang số của phiên trước |

- [ ] **Step 3: Commit** — `feat(sdk): add BaseSaveCollection (scan, validate, load, two-phase flush)`

---

### Task 3: `SaveDriver` — autosave, pause, quit

**Files:**
- Create: `Assets/Horcrux/Runtime/Implementations/Foundations/Persistence/SaveDriver.cs`

**Interfaces:**
- Consumes: `ISaveCollection` · `BaseSaveCollection` (Task 2) · UniTask.
- Produces: `SaveDriver : MonoBehaviour` — `internal static Spawn(BaseSaveCollection)`.

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

        internal static SaveDriver Spawn(BaseSaveCollection owner)
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

### Task 4: Ba nút Editor trên `BaseSaveCollection`

**Files:**
- Modify: `Assets/Horcrux/Runtime/Implementations/Foundations/Persistence/BaseSaveCollection.cs` — thêm khối `#if UNITY_EDITOR` ở cuối class.

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
| Ba nút ở lại base, không chuyển sang subclass của dự án | Chúng chỉ chạm `ScanEntries` và `KeyPrefix`, không chạm type nào của dự án. Odin vẽ `[Button]` khai ở base trên Inspector của asset dẫn xuất, nên chuyển sang dự án là bắt mỗi dự án chép lại ba nút giống nhau |
| Attribute Odin viết tên trần + `using Sirenix.OdinInspector;` ở đầu file | Khớp `BaseRemoteConfigCollection`, kể cả chi tiết `using` nằm ngoài `#if UNITY_EDITOR` trong khi `[Button]` chỉ dùng bên trong — một `using` không dùng tới trong build là vô hại, còn hai bản của một khuôn viết khác style thì đọc cạnh nhau không đối chiếu được |

- [ ] **Step 1: thêm vào cuối `BaseSaveCollection`, trước dấu `}` của class**

```csharp
#if UNITY_EDITOR
        [Button, GUIColor("cyan")]
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

### Task 5: Cặp file của dự án + demo + nghiệm thu

Đây là chỗ hệ chạy được lần đầu. Hai file đầu là **cặp file thật** mà mọi dự án dùng SDK này phải viết — không phải demo, không xoá được. File thứ ba là driver nghiệm thu, xoá được sau khi chạy xong.

Ở bản `partial` cũ, phần này buộc phải nằm trong SDK và plan phải gọi nó là *"ngoại lệ duy nhất cho luật SDK không khai entry"*. Giờ nó không còn là ngoại lệ: model và entry nằm trong assembly của dự án, đúng chỗ của chúng.

**Files** — cả ba nằm trong assembly của dự án (gợi ý: `Assets/FelixFelicis/Implements/Save/`):
- Create: `IGameSave.cs`
- Create: `GameSave.cs`
- Create: `DemoSaveDriver.cs` — xoá được sau khi nghiệm thu xong
- Scene demo (Editor setup dưới).

**Interfaces:**
- Consumes: `ISaveCollection` · `SaveEntry<T>` · `IService<T>` · `BaseSaveCollection` · `RegisteredSave`.
- Produces: `IGameSave` (1 property mỗi entry) · `GameSave` (sealed) · `DemoSaveDriver : MonoBehaviour`.

**Quyết định thiết kế:**

| Quyết định | Lý do |
|---|---|
| `IGameSave : ISaveCollection, IService<IGameSave>` | Derive contract để game gọi được `Flush(entry)` và `FlushAll()`; derive `IService<chính mình>` để có `Service` không nhập nhằng. Đây là nửa còn lại của §0.10 ②: contract không derive, dự án thì derive |
| **Một** dòng `[Service]`, dưới `IGameSave` | Khớp `GameRemoteConfigCollection`. Không gì trong SDK tra `ISaveCollection` qua service locator — `SaveBootStep` nhận reference kéo thả (Task 6). Bỏ được dòng thứ hai cũng là bỏ luôn bề mặt bẫy CS0229 khỏi phía dự án (§0.10 ②) |
| `sealed partial class` + `partial interface`, chia entry theo feature | Đúng khuôn `GameRemoteConfigCollection` đang chạy: một asset, nhưng khai báo nằm ở nhiều file. `partial` **trong cùng một assembly** không đụng ràng buộc §0.10 ① — cái bị cấm là `partial` **bắt qua** ranh giới SDK–dự án |
| `RESOURCE_PATH` viết SCREAMING_SNAKE | Khớp `GameRemoteConfigCollection.RESOURCE_PATH`. Hai file sẽ nằm cạnh nhau trong cùng dự án |
| `[Splitter("…")]` gom nhóm Inspector | Đồ có sẵn trong SDK (`Runtime/Utilities/Attribute/`), đang là thứ giữ cho asset 27 biến của Remote Config còn đọc được. Không dựng cơ chế gom nhóm thứ hai |
| `sealed` | Không có implementation thứ hai và không có lý do để có. Một chain ba tầng khi chưa ai cần là thêm một bậc người đọc phải leo |
| Hai file, không một | `IGameSave` là thứ mọi call site đọc; `GameSave` là chi tiết triển khai. Thêm một entry là sửa hai chỗ liền nhau, và IntelliSense ở call site chỉ thấy phần nó cần |
| Feature cần đọc có kiểu nhiều entry của riêng nó thì tách **interface**, không tách asset | `interface IEconomySave { SaveEntry<int> Coins { get; } … }`, rồi `IGameSave : ISaveCollection, IEconomySave, IService<IGameSave>`. Code Economy phụ thuộc `IEconomySave`, không thấy entry feature khác (`I` của SOLID). ⚠️ `IEconomySave` **không được** derive `IService<>` — `IGameSave` derive cả hai thì lại hai thành viên `Service`, CS0229. Muốn tra riêng thì thêm một dòng `[Service(typeof(IEconomySave), ResourcePath = RESOURCE_PATH)]` và đọc `IService<IEconomySave>.Service` |
| Feature tách interface xong thì đọc qua `IService<IEconomySave>.Service`, hoặc **nhận vào** qua Init | Hai đường đều giữ được `I`; chọn đường nào là việc của call site. Cách một hệ **SDK** lấy giá trị save thì khác — xem dòng cần phân xử ở "Ghi chú thực thi" |
| `DemoProgress` khai ngay trong `IGameSave.cs` | Ở demo thì gọn hơn. Một dự án thật đặt model ở file riêng trong thư mục của feature sở hữu nó — model là dữ liệu của feature, không phải của collection |
| Model là `class` dữ liệu thuần, public field | `JsonConvert` là đường duy nhất vào và ra. Không `UnityEngine.Object` (tham chiếu engine không thuộc về một save) và không struct của Unity (`Vector3.normalized` làm serializer đệ quy vô hạn) |

- [ ] **Step 1: `IGameSave.cs`**

```csharp
using System.Collections.Generic;
using Horcrux.Runtime.Abstractions;
using Horcrux.Runtime.Abstractions.Persistence;

namespace FelixFelicis.Save
{
    /// <summary>Every value this game saves, typed. Read one through IGameSave.Service.</summary>
    /// <remarks>
    /// Deriving IService here — and NOT on ISaveCollection — is what keeps Service unambiguous. Adding an entry
    /// means one property here and one field on GameSave; both are required, since nothing outside the class can
    /// reach a private field.
    /// <para>
    /// Partial so entries split by feature: one pair of files per feature, IGameSave.Economy.cs beside
    /// GameSave.Economy.cs, same namespace. That is what keeps a 30-entry collection reviewable, and keeps two
    /// people adding entries to different features out of each other's files.
    /// </para>
    /// </remarks>
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
    /// <remarks>
    /// One Service registration, under this project's own interface: nothing in the SDK looks ISaveCollection up,
    /// because SaveBootStep takes the collection as a serialized reference instead.
    /// Partial, matching IGameSave: each feature adds GameSave.Feature.cs next to IGameSave.Feature.cs.
    /// </remarks>
    [Service(typeof(IGameSave), ResourcePath = RESOURCE_PATH)]
    [CreateAssetMenu(menuName = "FelixFelicis/SaveCollection", fileName = "SaveCollection")]
    public sealed partial class GameSave : BaseSaveCollection, IGameSave
    {
        // Must match where the asset sits under a Resources folder, or the service never resolves.
        private const string RESOURCE_PATH = "Config/SaveCollection";

        [Splitter("Demo")]                 // optional: draws a group header in the Inspector
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

- [ ] **Step 4: Editor setup** (bước thật):

1. Project window → chuột phải → `Create` → `FelixFelicis` → `SaveCollection`.
2. Đặt asset vào `Assets/<…>/Resources/Config/SaveCollection.asset` — đường dẫn **sau** `Resources/` phải khớp `ResourcePath`, sai là service không resolve và `Service.Get<>()` ném exception.
3. Inspector của asset: điền key `demo_progress` cho `Demo Progress`, `demo_has_rated` cho `Demo Has Rated`; đặt giá trị mặc định muốn có; đặt `Autosave Interval Seconds` (mặc định 5).
4. Bấm `Validate keys` — phải sạch **trước khi** Play.
5. Scene mới `PersistenceDemo` → GameObject `[Demo]` + component `DemoSaveDriver`.

- [ ] **Step 5: Nghiệm thu khuôn kế thừa** — bốn thứ này chỉ Unity kiểm được, và cả bốn là hệ quả trực tiếp của §0.10:

| Kiểm | Cách làm | Kỳ vọng |
|---|---|---|
| Unity serialize được `SaveEntry<T>` khai trên subclass | mở asset trong Inspector | vẽ đủ `key` và `defaultValue` của cả hai entry, và cả hai **sửa được** |
| `value` và `isDirty` hiện mà không được ghi vào asset | mở asset ngoài Play mode | thấy cả hai, **xám / không sửa được** (`[ReadOnly]`). Play → đổi giá trị → Stop → mở lại: asset **không** bị Unity đánh dấu dirty, và `git status` không thấy `SaveCollection.asset` đổi. Thấy asset đổi nghĩa là `value` đã lỡ mang `[SerializeField]` — tiến độ người chơi đang đi vào version control |
| Đọc được `isDirty` ngay trên Inspector lúc Play | Play, `Add 10 coins`, nhìn asset | `Is Dirty` bật `true` ngay, rồi tự về `false` sau chu kỳ autosave — sáu ca nghiệm thu ở Step 7 kiểm bằng cửa sổ này thay vì đọc log |
| Nút Editor khai ở base hiện trên asset dẫn xuất | mở asset | thấy đủ ba nút `Validate keys`, `Print all payloads`, `Delete all save data` |
| Nút khai trên `SaveEntry<T>` hiện trong từng entry | mở asset, bung một entry | thấy ô `payloadToImport` và nút `Import payload`; bấm ngoài Play mode → `LogWarning`, không đổi gì |
| Entry khai ở file `partial` khác vẫn vào danh sách (§0.10 ④) | tách `demoHasRated` sang `GameSave.Demo.cs`, bấm `Validate keys` | vẫn báo đúng **2** entry — `partial` là cùng một type nên reflection thấy đủ |
| Scan thấy private field của subclass (§0.10 ④) | bấm `Validate keys` | báo đúng **2** entry |
| `IGameSave.Service` resolve được với **một** dòng `[Service]` (§0.10 ②③) | trong `Start` của `DemoSaveDriver`, đọc `IGameSave.Service` | trả về asset, không exception. Exception nghĩa là `RESOURCE_PATH` không khớp đường dẫn sau `Resources/` |
| `SaveBootStep` lấy được collection **không** qua service locator | mở GameObject có `SaveBootStep`, xem ô reference | ô đã trỏ vào asset; `grep -rn --include=*.cs "IService<ISaveCollection>" Assets/Horcrux` **không** ra kết quả nào |

- [ ] **Step 6: Nghiệm thu tự động** (agent tự chạy — năm thứ mắt người sót):

| Kiểm | Cách làm | Kỳ vọng |
|---|---|---|
| Round-trip mọi kiểu dùng thật | `SaveEntry<T>` với `int` · `bool` · `float` · `string` · một enum · `DemoProgress` có `List`: gán, `WritePayload`, `ReadPayload` vào một entry mới, so giá trị | bằng nhau từng field |
| **Không còn cửa ghi thứ hai** | `grep -rn "PlayerPrefs.Set" Assets/Horcrux/Runtime` | trong hệ Persistence ra **đúng một** kết quả runtime, nằm trong `FlushInternal`; kết quả trong nút Editor là cố ý và phải đếm riêng. `DemoSaveDriver` nằm ngoài `Assets/Horcrux` nên không lọt vào phép grep này — kiểm nó riêng |
| Không còn dấu vết kiến trúc cũ | `grep -rn "ISaveUnit\|SaveUnit\|Prefs<\|PrefsBool\|PrefsInt\|SaveRegistry" Assets` | không kết quả nào |
| **SDK không mang domain của dự án** | `grep -rn --include=*.cs "FelixFelicis\|DemoProgress\|IGameSave\|GameSave" Assets/Horcrux` và `find . -name "*.asmref"` | không kết quả nào. Giới hạn vào `*.cs` là cố ý: plan này nằm trong `Assets/Horcrux` và có nhắc các tên đó, nhưng tài liệu nói **về** dự án không phải SDK phụ thuộc dự án. Đây là phép kiểm của chính mục tiêu mới trong Goal, và là thứ duy nhất chứng minh submodule commit được một mình. **Thay tên theo host**: `FelixFelicis.Save` / `com.FelixFelicis` là tên của host viết plan này. Trong color-loop, namespace phía dự án theo lối đường dẫn (`_TheGame.Runtime.…`) — grep phải dùng tên của host đang triển khai, không dùng nguyên tên trong plan |
| `defaultValue` không bị mutate | lấy `Value`, mutate sâu vào nó (`coins = 999`, `unlockedSkins.Add`), gọi `Initialize()` lại, đọc `Value` | về đúng mặc định đã author; asset không bị đánh dấu dirty |
| Dirty không tắt trước khi xuống đĩa | dựng ca `PlayerPrefs.Save()` ném exception | mọi entry vừa ghi **còn** dirty, log nêu rõ, lượt sau thử lại |

- [ ] **Step 7: Kịch bản chơi thử** (nghiệm thu này cần Play mode, developer chạy):

| Mục | Nội dung |
|---|---|
| Vào đâu | Scene `PersistenceDemo`, bấm Play. **Để asset `SaveCollection` mở sẵn trong Inspector** — `Value` và `Is Dirty` của từng entry hiện ngay ở đó, nên phần lớn ca dưới nhìn được mà không phải đọc log |
| Làm gì | ① chuột phải `DemoSaveDriver` → `Add 10 coins` ×3, rồi bấm `Print all payloads` trên asset **ngay lập tức** · ② chờ quá 5 giây → `Print all payloads` lần nữa · ③ Stop rồi Play lại · ④ `Toggle 'has rated'` → Stop → Play lại · ⑤ `Add 10 coins` rồi `Flush ONLY progress` · ⑥ `Flush an entry the collection does not own` · ⑦ `Corrupt the progress payload` → Stop → Play lại · ⑧ `Add 10 coins` rồi bấm nút Pause của Editor · ⑨ trên asset, đổi key của `Demo Has Rated` thành `demo_progress` → `Validate keys` |
| Nhìn cái gì | ① log `dirty=True` ngay khi add, nhưng payload in ra vẫn là giá trị **cũ** · ② payload đã thành `coins=30`, log `dirty=False` · ③ log đầu tiên của phiên mới đã là `coins=30`, không phải `0` · ④ `rated` giữ nguyên qua phiên — **đây là ca thiết kế cũ làm mất, giờ nó nằm trong cùng vòng flush với model** · ⑤ chỉ payload của `demo_progress` đổi · ⑥ `LogError` nêu key và nhắc `[RegisteredSave]`, không ghi gì · ⑦ vẫn vào được demo, `LogError` nêu đúng `demo_progress`, `coins=0`, còn `rated` **vẫn đúng** · ⑧ payload cập nhật ngay lúc pause · ⑨ `LogError` nêu cả hai tên field, **ngay trong Editor, không cần Play** |
| Khác trước ra sao | Ở thiết kế cũ, giá trị lẻ như `rated` đi một đường riêng không có cờ dirty, và `PlayerPrefs.Save()` chỉ chạy khi có model dirty — nên ca ④ với app bị kill không qua pause là **mất giá trị**. Trùng key chỉ lộ lúc Play và chỉ giữa các model; ca ⑨ giờ lộ lúc authoring và phủ mọi entry |
| Dấu hiệu hỏng | coins về 0 sau restart bình thường (mất save) · `dirty=True` còn mãi sau khi payload đã đổi (`ClearDirty` không chạy) · ca ① payload đã đổi ngay khi vừa add (serialize đang bám nhịp tương tác) · ca ④ `rated` về mặc định (giá trị lẻ lại rơi ra ngoài vòng flush) · ca ⑦ exception đỏ không ai bắt, hoặc `rated` cũng mất theo (một entry hỏng kéo cả lượt) · ca ② payload không đổi sau khi chờ (thiếu `PlayerPrefs.Save()`) · ca ⑨ phải Play mới thấy lỗi · **ô `Value` trên Inspector sửa được** (thiếu `[ReadOnly]` → có cửa đổi giá trị mà không `MarkDirty`) · **Stop xong `git status` thấy `SaveCollection.asset` đổi** (`value` đã lỡ được serialize → tiến độ người chơi đang vào version control) |

- [ ] **Step 8: Commit** — `feat(game): add GameSave collection + persistence acceptance scene`

---

### Task 6: `SaveBootStep` — flush có thứ tự, và là đường wire chuẩn

**Files:**
- Create: `Assets/Horcrux/Runtime/Implementations/Composites/Persistence/SaveBootStep.cs`

**Interfaces:**
- Consumes: `BootStep` (`Abstractions/Foundations/Bootstrap/BootStep.cs`) · `BaseSaveCollection` (Task 2).
- Produces: `SaveBootStep : BootStep` — một field `[SerializeField]`, không API công khai nào.

**Hệ vẫn chạy được nếu thiếu step này** — `EnsureInitialized` và `SaveDriver` phủ hết. Nhưng nó là **đường wire chuẩn**, vì hai thứ chỉ nó cho được, và thứ hai mới là thứ quan trọng:

1. Flush **có thứ tự** ở pause và quit, cái mà magic method của một MonoBehaviour lẻ không hứa được.
2. `Initialize()` chạy **trong pha boot**, nên "subscribe `Changed` sau `Initialize`" đúng theo **cấu trúc** chứ không theo trí nhớ. Không có step này thì thời điểm `Initialize` phụ thuộc ai chạm collection trước, và một listener đăng ký trước đó bị `ResetRuntimeState()` xoá **im lặng** (§0.7 — xem bảng bẫy dưới).

**Quyết định thiết kế:**

| Quyết định | Lý do |
|---|---|
| Nằm ở nhánh **Composites**, không phải Foundations | Nó phụ thuộc cả Bootstrap lẫn Persistence. Lõi Persistence ở Foundations vẫn chạy được ở project không dùng Bootstrap |
| Nhận collection qua `[SerializeField]`, **không** tra service locator | "Step này flush collection nào" là quyết định lúc **authoring** (§3.3), và nó hiện ra trong Inspector chứ không nằm trong một lượt tra lúc chạy. Đổi lại, phía dự án chỉ còn **một** dòng `[Service]` — khớp `GameRemoteConfigCollection` — và bề mặt bẫy CS0229 biến mất |
| Kiểu field là `BaseSaveCollection`, không phải `ISaveCollection` | Unity chỉ serialize được reference tới `UnityEngine.Object`; một field kiểu interface không hiện ra ô kéo thả nào. `BaseSaveCollection : ScriptableObject` nên nó kéo được, và step vốn đã sống trong nhánh Implementations |
| Ô trống thì `LogError` rồi thoát, không ném | Thiếu wire là lỗi cấu hình lộ ngay pha boot — nhưng ném ở đây sẽ chặn cả chain boot vì một thứ mà `EnsureInitialized` vẫn cứu được. Log nêu tên GameObject là đủ đỏ để không ai bỏ qua |
| `InitializeAsync` gọi `Initialize()` | Trả chi phí reflection và load ở nơi có màn hình loading, làm lỗi save lộ ra trong pha boot thay vì giữa gameplay, và cố định thời điểm `Changed` bị reset |
| Chạy hai đường flush cùng lúc là chấp nhận được | Flush là idempotent: đường nào chạy trước cũng đúng, và đường chạy sau bắt nốt phần vừa sinh ra. Lượt không thấy gì dirty không chạm đĩa |
| Đặt `Order` **thấp** (init sớm) để flush **muộn** | Đã kiểm trong `BootstrapRunner`: pause và quit đều chạy `for (i = stepCount-1; i >= 0; i--)`, init chạy xuôi. Nên step init sớm nhất flush muộn nhất — chốt sổ sau khi mọi hệ đã ghi xong |

**Editor setup — bước thật:**

1. Trên GameObject có `BootstrapRunner`: add component `SaveBootStep`.
2. **Kéo asset `SaveCollection` vào ô `Collection`** — thiếu bước này thì step `LogError` rồi tự thoát ở pha boot.
3. Đặt `Order` **thấp** — vì fan-out pause/quit đi ngược, step init sớm sẽ flush **muộn nhất**, sau khi mọi hệ khác đã ghi xong.
4. Kéo `SaveBootStep` vào danh sách steps của `BootstrapRunner` nếu runner dùng danh sách tường minh.

- [ ] **Step 1: `SaveBootStep.cs`**

```csharp
using System.Threading;
using Cysharp.Threading.Tasks;
using Horcrux.Runtime.Abstractions.Bootstrap;
using UnityEngine;

namespace Horcrux.Runtime.Implementations.Persistence
{
    /// <summary>Moves the save load into boot, and gives pause and quit an ORDERED flush.</summary>
    /// <remarks>
    /// The runner walks steps backwards on pause and quit, so a step that initializes EARLY flushes LAST — after
    /// every system above it has written. That is the ordering a MonoBehaviour's own magic methods cannot promise.
    /// <para>
    /// Initializing here also fixes WHEN the collection resets its entries, and that reset drops every Changed
    /// listener. So this step is what makes "subscribe after Initialize" true by construction rather than by
    /// memory: without it, a listener wired before the first touch of the collection is dropped in silence.
    /// </para>
    /// <para>
    /// The collection comes in as a serialized reference, not from a service lookup: which collection this step
    /// flushes is an authoring decision, and it stays visible in the Inspector. The field is typed
    /// BaseSaveCollection because Unity only serializes UnityEngine.Object references — an interface field draws
    /// no slot at all.
    /// </para>
    /// This does not replace SaveDriver: the driver still runs the autosave timer, and the second flush of a pause
    /// finds nothing dirty and never reaches storage.
    /// </remarks>
    public sealed class SaveBootStep : BootStep
    {
        [SerializeField, Tooltip("The save collection to initialize at boot and flush on pause and quit.")]
        private BaseSaveCollection collection;

        public override UniTask InitializeAsync(CancellationToken ct)
        {
            // Pays the reflection scan and the load here, where a loading screen is up and a broken save is visible.
            if (HasCollection) collection.Initialize();
            return UniTask.CompletedTask;
        }

        public override void OnAppPause(bool isPaused)
        {
            if (isPaused && HasCollection) collection.FlushAll();
        }

        public override void OnAppQuit()
        {
            if (HasCollection) collection.FlushAll();
        }

        /// <summary>False with an error when the slot is empty — a setup mistake, surfaced without killing boot.</summary>
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
| Play với `SaveBootStep` trong chain | load xảy ra trong pha boot; `[Save]` xuất hiện ngay sau đó |
| Một `BootStep` khác `Order` cao hơn ghi vào một entry trong `OnAppPause` | giá trị đó **có** trong payload sau khi pause — chứng minh thứ tự đúng |
| Pause với cả driver lẫn step | flush chạy hai lần, lần sau không chạm đĩa (không log gì thêm) |
| Play **không** có `SaveBootStep` | hệ vẫn chạy đủ; load xảy ra ở lần chạm đầu tiên |
| Để trống ô `Collection` rồi Play | `LogError` nêu tên GameObject ngay pha boot, **boot vẫn đi tiếp**, và load rơi về `EnsureInitialized` ở lần chạm đầu tiên |
| Subscribe `Changed` trong `Start` của một MonoBehaviour, có step này trong chain | listener **còn sống** — `Initialize` đã chạy xong trong pha boot nên `ResetRuntimeState` không xoá nó |
| `grep -rn --include=*.cs "IService<ISaveCollection>" Assets/Horcrux` | không kết quả nào — SDK không tra collection qua service locator ở bất cứ đâu |

- [ ] **Step 3: Commit** — `feat(sdk): add SaveBootStep for ordered flush and boot-time load`

---

## Ghi chú thực thi

- **Nghiệm thu cuối = Step 5, 6 và 7 của Task 5.** Sáu mục tiêu ở "Ngữ cảnh đã chốt" map vào đó: một-nơi-nhìn-thấy-mọi-thứ (nút `Print all payloads` liệt kê đủ) · trùng-key-bắt-lúc-authoring (ca ⑨) · mọi-giá-trị-trong-vòng-flush (ca ④ cộng phép grep "không còn cửa ghi thứ hai") · quên-khởi-tạo-không-mất-tiến-độ (bảng Task 2, hàng "gọi `FlushAll` trước `Initialize`") · thêm-entry-không-sửa-SDK (`GameSave.cs` là bằng chứng sống — 2 dòng cho một entry mới, và không dòng nào trong `Assets/Horcrux`) · model-không-vào-SDK (hàng cuối bảng Step 6).
- **Sau khi implement xong:** viết `Persistence.md` (tài liệu thiết kế cho agent) cạnh `Implementations/Foundations/Persistence/`, rồi sinh `.html` từ nó. Chuyển mười mục `§0` sang mục quyết định thiết kế của tài liệu đó — bốn mục "đã sai một lần" (§0.4, §0.5, §0.6, §0.7) là loại tri thức không đọc ra được từ code, và §0.10 là loại người đọc sau sẽ **vô tình phá** nếu không thấy lý do viết sẵn.
- **`Persistence.md` soi theo cấu trúc mục của `RemoteConfigSystem.md`**: Hai phía · Đường đi của một giá trị · Vòng đời · Inspector · Nút Editor · **Bẫy và quyết định thiết kế** · Chữ ký · Cấu trúc file. Hai hệ là hai bản của một khuôn, nên giá trị lớn nhất của tài liệu là **đối chiếu được với nhau** — chỗ nào Persistence khác thì người đọc thấy ngay đó là chỗ cố ý.
- **Hai dòng bẫy phải có trong `Persistence.md`, và trong XML doc của chính API:**
  - `Changed` bị `ResetRuntimeState()` xoá, nên **mọi listener đăng ký trước `Initialize()` mất im lặng**. Không có detector tự động nào đúng được ở đây: trong Editor với domain reload tắt, listener của phiên trước (ca hợp lệ §0.7) và listener vừa bị xoá oan (ca bug) đều chỉ là `Changed != null`. Cách chặn duy nhất là cấu trúc — `SaveBootStep` cố định `Initialize` vào pha boot (Task 6).
  - `entry == null` so **giá trị**, không so entry, vì `implicit operator T` chạy trước phép so. Với `SaveEntry<string>` đó là `string == null`. Kiểm chính field thì `ReferenceEquals(entry, null)`.
- **Khoá `"save." + Key` và định dạng JSON của payload là hợp đồng ra ngoài.** Dữ liệu trên máy người chơi bám vào đúng hai thứ đó. Đổi tiền tố hay đổi định dạng sau khi ship là mọi save cũ thành mồ côi — không có gì báo, chỉ là một ngày tất cả người chơi cũ mở game lên thấy tiến độ về 0.
- **Hệ dùng tiếp:** Audio (volume), Haptics, Economy (coin và lives), Rating, LiveOps. Không hệ nào tự khai entry — dự án khai trên `GameSave` rồi nối vào, nên một hệ SDK vẫn bê lẻ sang project không dùng Persistence được.
- **⚠️ CẦN DEVELOPER PHÂN XỬ — một hệ SDK lấy giá trị save bằng cách nào.** Hai tài liệu đang nói ngược nhau, và cả hai đều là ranh giới do developer đặt nên plan này không tự chọn:

  | Nguồn | Nói gì | Hệ quả |
  |---|---|---|
  | Plan này, mục "Ai gọi" và "SOLID" | hệ SDK **nhận vào `SaveEntry<T>`** của mình qua Init | module compile-depend vào namespace Persistence; bê sang project không có Persistence là không biên dịch được |
  | `HapticSystem.md` · `AudioSystem.md` | module phơi **field thường** (`IsEnabled`, `IsSfxOn`), dự án đọc save rồi **set vào** lúc bootstrap; *"SDK cố tình không sở hữu hệ save"* | module không phụ thuộc gì thuộc Persistence; entry giữ bản gốc, field module là bản chiếu, glue một chiều qua `Changed` |

  Chốt bản nào thì sửa **dòng cũ** ở cả ba chỗ đang nhắc ("Ai gọi", "SOLID", và dòng "Hệ dùng tiếp" ngay trên), không thêm dòng thứ hai nói ngược. Hai tài liệu module đã qua vòng cắt phạm vi (`Haptics` cố ý loại `IHapticSettings` vì 0 implementation), nên bản của chúng có vẻ là bản mới hơn — nhưng đó là suy đoán, không phải chốt.
- **Khuôn "abstract base trong SDK + subclass của dự án" dùng chung với hệ Remote Config** — xem `Abstractions/Foundations/RemoteConfigSystem/RemoteConfigSystem.md`. Hai hệ cố ý giống nhau đến từng chi tiết: cùng ràng buộc §0.10 · **một** dòng `[Service]` dưới interface của dự án · `sealed partial class` + `partial interface` chia theo feature · `RESOURCE_PATH` · reflection quét `NonPublic | Instance` bằng vòng lặp tay · `[Splitter]` gom nhóm Inspector · magic method khai `protected virtual` · `abstract` base không mang `[CreateAssetMenu]`. Sửa một bên mà không sửa bên kia là làm hai khuôn từ một khuôn.
- **Ba chỗ Persistence cố ý đi khác khuôn Remote Config**, mỗi chỗ một lý do gọi được tên — đây là danh sách đóng, thêm chỗ thứ tư thì phải viết lý do vào đây:

  | Chỗ khác | Lý do |
  |---|---|
  | `EnsureInitialized()` ở mọi cửa vào công khai | quên `Initialize()` ở RC là giá trị về default, hồi được; ở Save là ghi đè default lên save thật, không hồi được (§0.9) |
  | Cặp hook `ResetDerivedState()` / `OnEntriesLoaded()` | RC chỉ có hook "sau" (`OnRemoteConfigsApplied`), và chính chỗ hở đó sinh ra cờ mang trạng thái phiên trước (§0.7) |
  | `[NonSerialized] value` + `[SerializeField] defaultValue` | RC cố ý serialize giá trị fetch về để developer thấy trong asset; ở Save thứ được lưu là tiến độ **người chơi**, ghi vào asset là đưa nó vào version control (§0.8). Khác ở **trục serialize**, không khác ở trục nhìn thấy: cả hai hệ đều phơi trạng thái runtime bằng `[NonSerialized, ShowInInspector]`, Save thêm `[ReadOnly]` vì `value` sửa tay được là một cửa no-op âm thầm còn `fetched` của RC thì vô hại |

- **Hai chỗ Remote Config nên học lại từ Persistence**, ghi ra để khỏi trôi mất — **không** thuộc phạm vi plan này, cần developer quyết trước khi làm: `IRemoteConfigCollection.RemoteConfigs` nên là `IReadOnlyList<>` thay vì `IEnumerable<>` (indexable, không boxing enumerator) · `RemoteConfig<T>` ghi cache PlayerPrefs bằng **key thô không tiền tố**, mà cache thì bỏ đi được, nên thêm tiền tố `"rc."` chỉ tốn một lượt fetch nguội và làm sạch cả không gian khoá dùng chung (§0.1).
