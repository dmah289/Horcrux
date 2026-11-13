# Remote Config — `BaseRemoteConfigCollection`

Một `ScriptableObject` của dự án giữ mọi biến remote config. Mỗi biến là một `RemoteConfig<T>` mang `firebaseKey` và giá
trị mặc định author trong Inspector. Provider fetch xong thì collection đẩy giá trị vào từng biến; call site đọc có kiểu
đầy đủ, không cast. Horcrux giữ máy móc; dự án khai biến **trong assembly của chính nó**, không cần `.asmref`.

Code: `Runtime/Abstractions/Foundations/RemoteConfigSystem/` (`IRemoteConfig.cs`, `IRemoteConfigCollection.cs`,
`IRemoteConfigProvider.cs`) · `Runtime/Implementations/Foundations/RemoteConfigSystem/` (`RemoteConfig.cs`,
`BaseRemoteConfigCollection.cs`, `RemoteConfigCsvImporter.cs`) · `Vendors/Firebase/` (`FirebaseRemoteConfigProvider.cs`,
asmdef `com.horcrux.firebase`) · namespace `Horcrux.Runtime.Abstractions.RemoteConfigSystem`,
`Horcrux.Runtime.Implementations.RemoteConfigSystem`, `Horcrux.Vendors.Firebase`.

## Ngữ cảnh đã chốt

| Mục | Nội dung |
|---|---|
| Ranh giới | Horcrux giữ: quét field, wire provider, parse, cache, nút Editor, bản provider Firebase. Dự án giữ: interface và class collection, `[Service]`, `[CreateAssetMenu]`, đường dẫn Resources, chỗ gọi `Initialize` và fetch, timeout boot |
| Phụ thuộc | một chiều: dự án tham chiếu Horcrux; dự án chỉ gọi tên `IRemoteConfigProvider`, không gọi tên bản Firebase — gỡ gói Firebase thì asmdef vendor tắt, dự án vẫn compile |
| Tầm sống giá trị | apply một lần mỗi lượt fetch; giá trị đứng yên suốt phiên |
| Nguồn giá trị | remote → cache `PlayerPrefs` → Inspector (§3) |
| Định dạng đường truyền | chuỗi; kiểu không phải số, `bool`, `enum`, `string` thì JSON. CSV chỉ để author trong Editor |
| Cố ý KHÔNG làm | tự gọi `Initialize` · tự fetch · áp lại giá trị giữa phiên · gỡ cache khi key biến khỏi server · test runtime |

## Trước khi chạy

Dựng từ 0 theo thứ tự; mỗi bước kèm chỗ hỏng nếu thiếu.

1. **Provider.** Dự án có gói `com.google.firebase.remote-config` thì không viết gì: asmdef `com.horcrux.firebase` bật qua
   define `HORCRUX_FIREBASE_REMOTE_CONFIG` và `FirebaseRemoteConfigProvider` tự đăng ký `[Service(typeof(IRemoteConfigProvider))]`.
   Vendor khác: tự cài `IRemoteConfigProvider` theo hợp đồng §6, mang `[Service(typeof(IRemoteConfigProvider))]`.
2. **Interface collection.** Trong assembly dự án:
   ```csharp
   public partial interface IGameRemoteConfig : IRemoteConfigCollection, IService<IGameRemoteConfig> { }
   ```
3. **Class collection.** Cùng namespace với interface:
   ```csharp
   [Service(typeof(IGameRemoteConfig), ResourcePath = RESOURCE_PATH)]
   [CreateAssetMenu(fileName = "GameRemoteConfig", menuName = "Configs/GameRemoteConfig")]
   public partial class GameRemoteConfig : BaseRemoteConfigCollection, IGameRemoteConfig
   {
       private const string RESOURCE_PATH = "Config/GameRemoteConfig";
   }
   ```
   `partial` để chia biến theo nhóm ra nhiều file (`GameRemoteConfig.Level.cs`…), mỗi nhóm một cặp file class + interface.
4. **Asset.** Project window → **Create** → **Configs** → **GameRemoteConfig**, đặt ở
   `Assets/…/Resources/Config/GameRemoteConfig.asset`. Đường dẫn sau `Resources/`, bỏ đuôi, phải bằng `RESOURCE_PATH`.
5. **Mỗi biến.** Hai chỗ, luôn đi cùng nhau:
   ```csharp
   public partial interface IGameRemoteConfig
   {
       RemoteConfig<int> MaxRetryCount { get; }
   }

   public partial class GameRemoteConfig
   {
       [Splitter("Level")]                       // tuỳ chọn — tiêu đề nhóm trong Inspector
       [MarkedRemoteConfig]
       [SerializeField] private RemoteConfig<int> maxRetryCount;

       public RemoteConfig<int> MaxRetryCount => maxRetryCount;
   }
   ```
   Chọn asset, trong Inspector điền **Firebase Key**, **Allow Fetching**, **Value** của biến đó.
6. **Màn boot.** Gọi `Initialize`, rồi fetch và chờ xong mới vào game:
   ```csharp
   IGameRemoteConfig.Service.Initialize();
   await remoteConfigProvider.FetchAndActivateAsync(TimeSpan.FromSeconds(2), destroyCancellationToken);
   ```
   `remoteConfigProvider` nhận qua InitArgs hoặc `IRemoteConfigProvider.Service`. Thứ tự hai lời gọi không quan trọng (§2).
7. **Console Firebase.** Khai cùng key, giá trị cùng định dạng §4. Chạy tool `RemoteConfigDiff` (`docs/Editor/RemoteConfigDiff.md`)
   để so asset với template console.

| Thiếu | Hỏng ở đâu |
|---|---|
| bước 1 | `Initialize` log `No IRemoteConfigProvider found in the scene…`; mọi biến đứng ở giá trị Inspector |
| bước 2, interface derive thêm `IService<>` qua `IRemoteConfigCollection` | lỗi biên dịch CS0229 ở mọi `.Service` (§9) |
| bước 3, `[Service]` khai ở lớp cha thay vì class dự án | không đăng ký: `IGameRemoteConfig.Service` ném ở lần chạm đầu |
| bước 4, sai đường dẫn hay thiếu asset | `IGameRemoteConfig.Service` ném ở lần chạm đầu |
| bước 5, thiếu `[MarkedRemoteConfig]` | biến không vào `RemoteConfigs`, **không log**: đứng ở giá trị Inspector mãi |
| bước 5, field `public` | quét chỉ lấy field non-public: biến bị bỏ, **không log** |
| bước 5, field khai `private` ở lớp cha của class dự án | quét không thấy private của lớp cha: biến bị bỏ, **không log** |
| bước 5, thiếu property trên interface | call site không đọc được biến qua `IGameRemoteConfig` |
| bước 5, **Firebase Key** trống hay sai | mỗi lượt apply log `Can't find remote config value for key …`, biến rơi về cache rồi Inspector |
| bước 6, không gọi `Initialize` | `RemoteConfigs` rỗng, không wire provider: mọi biến đứng ở giá trị Inspector, không log |
| bước 6, không chờ fetch | call site đọc trước apply: giá trị Inspector, rồi đổi giữa chừng khi apply |
| bước 7, key thiếu trên console | `LogError` mỗi lần boot, biến rơi về cache rồi Inspector |

## 1. API

Truy cập qua `IGameRemoteConfig.Service` (thiếu thì ném) hoặc `IGameRemoteConfig.TryGet(out var rc)`.

| Ai gọi | Thành viên | Ghi chú |
|---|---|---|
| màn boot | `void Initialize()` | quét field, wire provider; provider đã fetch thì apply ngay. Gọi lại được (§5) |
| màn boot | `UniTask FetchAndActivateAsync(TimeSpan timeout, CancellationToken ct)` | trên provider. Người gọi chọn `timeout` vì đó là luật boot của game |
| call site | `RemoteConfig<T>` qua property interface | `implicit operator T` (biến null → `default`) hoặc `.Value` |
| call site | `bool AllRemoteConfigsApplied` | `true` sau lượt apply đầu; `Initialize` đặt lại `false`. Nghĩa là **đã chạy apply**, không phải remote đã về |
| dự án | `protected virtual void OnRemoteConfigsApplied()` | chạy một lần sau khi apply xong mọi biến. `override` thì **gọi `base`**, không thì `AllRemoteConfigsApplied` mãi `false` |
| dự án | `protected virtual void OnDestroy()` | gỡ subscription. `override` thì **gọi `base`** |

```csharp
int a = IGameRemoteConfig.Service.MaxRetryCount;         // implicit operator T
int b = IGameRemoteConfig.Service.MaxRetryCount.Value;   // tường minh

protected override void OnRemoteConfigsApplied()
{
    base.OnRemoteConfigsApplied();
    // parse JSON thành model, bắn event…
}
```

## 2. Luồng

```
[server]
   │ FetchAndActivateAsync(timeout, ct)              ← màn boot gọi một lần
   ▼
IRemoteConfigProvider ── IsFetched = true, bắn OnFetched (mọi đường ra)
   ▼
BaseRemoteConfigCollection.OnRemoteConfigFetched()
   ├─▶ RemoteConfig<T>.ApplyRemoteValue(provider)          ← mỗi biến, theo thứ tự khai field
   │      allowFetching == false ⇒ thoát, giữ Inspector
   │      provider.TryGetRemoteValue(firebaseKey) → TryParseValueFromString
   │        OK ⇒ value = parsed · PlayerPrefs[firebaseKey] = chuỗi raw · fetched = true
   │        trượt ⇒ LogError → PlayerPrefs[firebaseKey] → parse
   │                  OK ⇒ value = cache · trượt ⇒ LogError, giữ value
   └─▶ OnRemoteConfigsApplied()  → AllRemoteConfigsApplied = true
          ▼
   call site: IGameRemoteConfig.Service.MaxRetryCount
```

**Thứ tự khởi tạo không quan trọng:** `Initialize` trước thì subscribe rồi đợi `OnFetched`; fetch xong trước thì
`Initialize` thấy `IsFetched == true` và apply ngay trong lời gọi đó.

## 3. Giá trị nào thắng

`allowFetching = true`: dừng ở bậc đầu parse được. `allowFetching = false`: luôn Inspector, không đọc remote, không đọc
cache — cách test một giá trị tại máy.

```
1. Remote       TryGetRemoteValue có key, chuỗi không rỗng, parse OK → dùng, ghi chuỗi RAW vào PlayerPrefs[firebaseKey]
2. PlayerPrefs  cache của lần chạy trước, parse OK                     → dùng
3. Inspector    value đang giữ                                          → giữ nguyên
```

| Tình huống | Giá trị chạy |
|---|---|
| máy lần đầu, mất mạng | Inspector |
| đã từng fetch, lần này mất mạng hay quá hạn | Firebase activate bản phiên trước nếu có, không thì cache `PlayerPrefs` |
| server gửi chuỗi sai định dạng | cache `PlayerPrefs`; cache tốt **không bị ghi đè** vì chỉ ghi khi parse thành công |
| key bị xoá khỏi console | máy đã từng fetch giữ giá trị cũ trong `PlayerPrefs` **mãi**, không về Inspector; máy mới dùng Inspector |
| server gửi chuỗi rỗng | cache rồi Inspector |
| sửa **Value** trong Inspector, Play trong Editor | remote hay cache vẫn đè; tắt **Allow Fetching** để thấy giá trị vừa sửa |

## 4. Parse theo kiểu `T`

Cùng một hàm `TryParseValueFromString` cho remote, cache, nút `ImportJson` và tool `RemoteConfigDiff`.

| `T` | Cách parse | Chú ý |
|---|---|---|
| `string` | nguyên chuỗi | |
| `enum` | `Enum.Parse` | phân biệt hoa thường; nhận cả số, kể cả số không khai trong enum |
| `int` · `long` | `Parse` | |
| `bool` | `bool.Parse` | nhận `true`/`false` không phân biệt hoa thường; `1`/`0` là trượt |
| `float` · `double` | `Parse` với `InvariantCulture` | máy locale dấu phẩy vẫn đúng; server phải dùng dấu chấm |
| còn lại | `JsonConvert.DeserializeObject<T>` | khớp tên property không phân biệt hoa thường, không đổi tên: đổi tên field thì key cũ trên server rơi thành `default` **im lặng**. Key thừa bị bỏ qua |

Giá trị có cấu trúc khai `RemoteConfig<MyData>` với class dữ liệu thuần, không `RemoteConfig<string>` rồi tự parse — parse
hai nơi che lỗi khỏi log của hệ. JSON hỏng thì hệ tự `LogError` và giữ cache; consumer không `try/catch`.

## 5. Vòng đời

| Lúc | Chuyện gì xảy ra |
|---|---|
| `Initialize()` | `AllRemoteConfigsApplied = false` → quét field (cache `FieldInfo` một lần mỗi phiên) → `ResetFetchedState()` từng biến → gỡ subscription trên provider cũ → `IRemoteConfigProvider.TryGet` → subscribe → `IsFetched` thì apply ngay |
| `Initialize()` lần hai | an toàn: gỡ subscription cũ **trước** khi ghi đè field provider. Giá trị đã apply giữ nguyên, không về Inspector |
| `OnFetched` | apply mọi biến → `OnRemoteConfigsApplied()` |
| `OnDestroy` | gỡ subscription |

Không có gì chạy theo frame.

## 6. Provider

| Thành viên | Hợp đồng |
|---|---|
| `FetchAndActivateAsync(timeout, ct)` | chỉ lời gọi đầu có tác dụng · chờ tối đa `timeout` (giờ thật) rồi activate bản mới nhất đang giữ · fetch về muộn để phiên sau · `IsFetched = true` rồi bắn `OnFetched` đúng một lần ở **mọi** đường ra: xong, lỗi, quá hạn, huỷ |
| `TryGetRemoteValue(key, out value)` | `false` khi server không khai key; chuỗi rỗng server trả về thì `true` để `RemoteConfig<T>` xử lý ca rỗng |
| `IsFetched` · `event Action OnFetched` | collection dùng cặp này để không phụ thuộc thứ tự khởi tạo |

`FirebaseRemoteConfigProvider` là class thuần, không `MonoBehaviour`. InitArgs `new` nó **một lần** trước `Awake` của
scene đầu (`LazyInit` mặc định `false`), nên màn boot và collection cầm cùng một object.

| Đường ra của bản Firebase | Kết quả |
|---|---|
| fetch xong trong `timeout` | activate, mọi key server khai trả `true` |
| quá `timeout` | `LogWarning` `Fetch timed out…`; activate bản đang giữ; fetch chạy tiếp, về muộn thì để phiên sau |
| `FetchAsync` lỗi | `LogWarning` `Fetch failed…`; activate bản đang giữ |
| dependency Firebase không dùng được | `LogError` `Firebase can't be used…`; không activate, mọi `TryGetRemoteValue` trả `false` → cache rồi Inspector |
| exception khác ở bước chuẩn bị | `TimeoutWithoutException` của UniTask nuốt exception và trả như quá hạn: log `Fetch timed out…` |
| `ct` bị huỷ | cũng log `Fetch timed out…`. Đã lấy được instance Firebase thì `ActivateAsync` **ném** `OperationCanceledException` sau khi bắn `OnFetched`; chưa lấy được thì kết thúc không ném |
| lời gọi thứ hai | trả về **ngay**, không chờ lời gọi đầu: chỗ khác cần chờ thì đọc `IsFetched` hay nghe `OnFetched` |

`TryGetRemoteValue` chỉ trả `true` khi nguồn là `ValueSource.RemoteValue`; default in-app của Firebase không tính.

## 7. Inspector và nút Editor

| Ô trên mỗi biến | Nghĩa |
|---|---|
| **Firebase Key** | key trên server; cũng là key `PlayerPrefs` |
| **Allow Fetching** | tắt = chốt giá trị Inspector |
| **Value** | mặc định, và là giá trị đang chạy sau apply |
| **Fetched** | chỉ đọc, không serialize: `true` = giá trị đến từ remote ở lượt vừa rồi |
| **Value To Import** | ô dán JSON/CSV, `[NonSerialized]` |

| Nút | Ở đâu | Làm gì |
|---|---|---|
| **Copy Json To Clipboard** | mỗi biến | copy **Value** ra JSON indent — dán thẳng lên console |
| **Import Json** | mỗi biến | parse **Value To Import** bằng luật §4, ghi vào **Value**; hỏng thì `LogError`, không ghi |
| **Copy Csv To Clipboard** | mỗi biến | copy **Value** ra CSV, header trước |
| **Import Csv** | mỗi biến | parse **Value To Import** như CSV (§8) — **all-or-nothing** |
| **Search Firebase Key Usage** | collection | log field **đầu tiên** mang key; không thấy thì không log gì |
| **Enable All Fetching** | collection | bật **Allow Fetching** cho mọi biến |

Hai nút collection khai ở lớp cha nhưng đọc field qua reflection, nên chạy đúng trên mọi class dẫn xuất.

## 8. CSV — chỉ để author trong Editor

`RemoteConfigCsvImporter` (`internal static`, `#if UNITY_EDITOR`) mang giá trị qua lại với spreadsheet; JSON vẫn là định
dạng đường truyền.

| Mục | Luật |
|---|---|
| Hình dạng nhận | một scalar · một object phẳng (một dòng) · `List<>` của scalar hay object phẳng (nhiều dòng) |
| Kiểu cell | `enum` · `string` · `int` · `long` · `float` · `double` · `bool` |
| Cột | field Unity serialize: `public`, hoặc `private` + `[SerializeField]`, không `static`, không `[NonSerialized]` |
| Object có field không phải cell | không có dạng CSV; báo tên field, bảo dùng JSON |
| Class dòng | cần constructor không tham số |
| Header | dòng dữ liệu đầu được nhận là header khi số cell bằng số cột và mọi cell khớp tên cột (không phân biệt hoa thường); header cho đổi thứ tự cột |
| Bỏ qua | dòng trống, dòng mở đầu `#` hay `//` |
| Tách cell | `Split(',')`, **không hỗ trợ dấu nháy**: chuỗi chứa dấu phẩy làm lệch số cột → huỷ cả lần import |
| Số | `InvariantCulture`; xuất `float`/`double` bằng định dạng `"R"` |
| `enum` | không phân biệt hoa thường; chỉ nhận giá trị đã khai (trừ `[Flags]`) — chặt hơn parse runtime §4 |
| Cell `""`, `-`, `None` ở cột không phải `string` | dòng "không áp dụng": `List<>` bỏ dòng đó và báo số dòng; giá trị một dòng thì huỷ import |
| Lỗi | báo số dòng và tên cột; không ghi gì |

## 9. Guard và bẫy

Đọc trước khi "sửa cho gọn" — mỗi dòng là một chỗ đã sai hoặc chắc chắn sẽ sai.

| Chỗ | Sự thật |
|---|---|
| **`IRemoteConfigCollection` không derive `IService<>`** | interface dự án derive cả nó lẫn `IService<chính mình>` sẽ thừa hưởng **hai** static `Service` → mọi `.Service` là **CS0229 ambiguity**. Code chỉ cần contract thì viết `IService<IRemoteConfigCollection>.Service` — `IService<T>.Service` là `Sisus.Init.Service.Get<T>()` nên chạy với bất kỳ `T` |
| **`[Service]` không di truyền** | `ServiceAttribute` khai `Inherited = false`: khai ở lớp cha là không ai đọc. `AllowMultiple = true` nên một class đăng ký được nhiều service type; cùng `ResourcePath` vẫn ra một instance vì `Resources.Load` trả cùng asset |
| **Quét chỉ thấy field non-public của chính class và non-private của lớp cha** | `GetType().GetFields(NonPublic \| Instance)`: field `public` bị bỏ, field `private` của lớp cha bị bỏ. Biến khai ở lớp cha phải `protected`. Không log |
| **`OnDestroy` và `OnRemoteConfigsApplied` phải qua `base`** | lớp con khai `OnDestroy` không `override` chỉ bị warning CS0114, Unity gọi bản lớp con, listener chết nằm lại trên provider. `OnRemoteConfigsApplied` không gọi `base` thì `AllRemoteConfigsApplied` mãi `false` |
| **Field mark nhưng không phải `IRemoteConfig`** | `Initialize` log tên field rồi `continue`, các biến khác vẫn apply. Hai nút collection thì cast thẳng và ném |
| **Ô import là `[NonSerialized]`** | `[SerializeField]` bọc `#if UNITY_EDITOR` làm Editor ghi field vào asset mà build strip nó → layout lệch → **crash native** khi đọc asset (`Read N bytes but expected M bytes`). Hiện trong Inspector mà không serialize: `[NonSerialized, ShowInInspector]` |
| **Play trong Editor ghi giá trị remote lên chính asset** | collection load qua `Resources` là asset thật; apply đổi **Value** trong memory, thoát Play vẫn còn. Sửa một ô khác của asset rồi lưu thì giá trị remote thành mặc định. Soát diff asset trước khi commit |
| **`implicit operator T` bất đối xứng khi so sánh** | `variable == null` so **tham chiếu** (literal `null` không có kiểu, `RemoteConfig<T>` không khai `operator ==`). So với toán hạng **có kiểu `T`** thì `T.op_Equality` vào tập ứng viên và variable bị convert: với `RemoteConfig<string>` có `Value` null, `variable == null` ra `false`, `variable == s` (`s` là `string` null) ra `true` |
| **Đổi hình JSON của key đã phát hành** | build đang chạy vẫn đọc key đó theo hình cũ: parse hỏng, rơi về cache rồi Inspector. Đổi hình thì sang key mới; xoá key cũ khi không build nào còn đọc. Gốc giá trị có cấu trúc là object, không mảng trần: thêm khoá sau vẫn đọc được ở build cũ vì key thừa bị bỏ qua |
| **Một lần parse hỏng ra nhiều dòng log** | `TryParseValueFromString` tự `LogError`, `ApplyRemoteValue` log thêm một dòng mỗi bậc trượt |

## 10. Quyết định

| Câu hỏi | Tiêu chí | Chốt |
|---|---|---|
| Một asset cho cả dự án hay mỗi biến một SO | mở một chỗ thấy cả bảng, import được; chi phí class + asset mỗi `bool` | một asset; lớp cha `abstract`, `[CreateAssetMenu]` ở class dự án |
| `IRemoteConfigCollection` có derive `IService<>` không | compile được ở call site | không — CS0229 (§9) |
| Ghi cache khi nào | payload hỏng trên server không được phá cache tốt | chỉ khi parse thành công, ghi chuỗi raw |
| Provider là `MonoBehaviour` hay class thuần | có gì để kéo thả không; đời sống | class thuần, sống cả phiên, InitArgs tạo |
| Ai chọn timeout fetch | luật boot thuộc game | người gọi, không phải provider |
| Áp lại giá trị giữa phiên | call site đọc giá trị ổn định | không; fetch về muộn để phiên sau |
| CSV hay JSON trên đường truyền | Firebase lưu chuỗi; JSON đủ mọi kiểu | JSON; CSV chỉ trong Editor |

## 11. Chẩn đoán

| Log | Nghĩa | Sửa |
|---|---|---|
| `No IRemoteConfigProvider found in the scene…` | `Initialize` không tìm thấy provider | bước 1 |
| `[RemoteConfig] … is marked MarkedRemoteConfig but holds no IRemoteConfig` | field mark không phải `RemoteConfig<T>` | đổi kiểu field hoặc bỏ attribute |
| `Can't find remote config value for key …` | provider không có key: console thiếu key, key sai, hoặc Firebase không dùng được | bước 5, bước 7 |
| `Remote config value for key … is empty` | server trả chuỗi rỗng | điền giá trị trên console |
| `Can't parse remote config value for key … with value … to type …` | chuỗi không parse được theo §4; dòng có `-> Using cached value in PlayerPrefs` là lượt apply | sửa giá trị trên console theo §4 |
| `Can't find remote config prefs value for key … -> Using default value set on Editor` | cache không có hoặc không parse được; biến giữ **Value** | không cần sửa nếu remote cũng trượt có chủ đích |
| `[FirebaseRemoteConfigProvider]: Fetch timed out. Using last session version.` | quá `timeout`, `ct` bị huỷ, hoặc exception ở bước chuẩn bị (§6) | tăng `timeout` hoặc chấp nhận bản phiên trước |
| `[FirebaseRemoteConfigProvider]: Fetch failed - …` | `FetchAsync` lỗi | xem mạng, cấu hình Firebase |
| `[FirebaseRemoteConfigProvider]: Firebase can't be used (…). Using default` | dependency Firebase hỏng; "default" ở đây là cache rồi Inspector | cài lại gói, file cấu hình Firebase |
| `Remote config … has no CSV form: …` | kiểu không có dạng CSV (§8) | dùng JSON |
| `Remote config … CSV import aborted, nothing written: …` | CSV bị từ chối; báo dòng, cột | sửa sheet theo §8 |

## Mở rộng

- **Provider khác:** cài `IRemoteConfigProvider` theo §6, mang `[Service(typeof(IRemoteConfigProvider))]`; dự án không
  đổi dòng nào.
- **Kiểu `T` mới cần parse riêng:** thêm nhánh trong `TryParseValueFromString`.
- **Tool `RemoteConfigDiff` gọi hàm private qua reflection** — `BaseRemoteConfigCollection.GetRemoteConfigFields` và
  `RemoteConfig<T>.TryParseValueFromString`, thuộc tính `Value`. Đổi tên hay chữ ký thì sửa tool cùng lần.

## Chữ ký

**`Horcrux.Runtime.Abstractions.RemoteConfigSystem`**

| Type | Thành viên |
|---|---|
| `IRemoteConfig` | `string FirebaseKey { get; }` · `bool AllowFetching { get; set; }` · `void ApplyRemoteValue(IRemoteConfigProvider)` · `void ResetFetchedState()` |
| `IRemoteConfigProvider : IService<IRemoteConfigProvider>` | `event Action OnFetched` · `bool IsFetched { get; }` · `UniTask FetchAndActivateAsync(TimeSpan timeout, CancellationToken ct)` · `bool TryGetRemoteValue(string firebaseKey, out string value)` |
| `IRemoteConfigCollection` | `IEnumerable<IRemoteConfig> RemoteConfigs { get; }` · `IRemoteConfigProvider RemoteConfigProvider { get; }` · `bool AllRemoteConfigsApplied { get; }` · `void Initialize()` |

**`Horcrux.Runtime.Implementations.RemoteConfigSystem`**

| Type | Thành viên |
|---|---|
| `RemoteConfig<T> : IRemoteConfig` | `[Serializable]` · `T Value { get; }` · `implicit operator T` (null → `default`) |
| `MarkedRemoteConfig : Attribute` | `sealed` · `[AttributeUsage(AttributeTargets.Field)]` |
| `BaseRemoteConfigCollection : ScriptableObject, IRemoteConfigCollection` | `abstract` · `void Initialize()` · `protected virtual void OnRemoteConfigsApplied()` · `protected virtual void OnDestroy()` · Editor: `void SearchFirebaseKeyUsage(string)` |
| `RemoteConfigCsvImporter` | `internal static` · `bool TryParse(Type, string csv, out object parsed, out string report)` · `bool TryFormat(Type, object, out string csv, out string error)` |

**`Horcrux.Vendors.Firebase`** — `FirebaseRemoteConfigProvider : IRemoteConfigProvider`, `[Service(typeof(IRemoteConfigProvider))]`.

**`Horcrux.Runtime.Abstractions`** — `IService<out T>`: `static T Service` · `static bool TryGet(out T)`.

## Cấu trúc file

```
Runtime/Abstractions/Foundations/RemoteConfigSystem/
├── IRemoteConfig.cs              hợp đồng một biến
├── IRemoteConfigCollection.cs    hợp đồng collection — interface THUẦN (§9)
└── IRemoteConfigProvider.cs      hợp đồng nguồn fetch

Runtime/Implementations/Foundations/RemoteConfigSystem/
├── RemoteConfig.cs               RemoteConfig<T> — giá trị, parse, cache PlayerPrefs, nút JSON/CSV
├── BaseRemoteConfigCollection.cs lớp cha + MarkedRemoteConfig — quét, wire, apply, 2 nút
└── RemoteConfigCsvImporter.cs    CSV ⇄ value, chỉ Editor

Vendors/Firebase/                 asmdef com.horcrux.firebase — bật khi có com.google.firebase.remote-config
└── FirebaseRemoteConfigProvider.cs
```

## Tests

Chưa có test cho hệ runtime. `Tests/EditMode/RemoteConfigDiffTests.cs` thuộc tool `RemoteConfigDiff`, gián tiếp chạy
quét field và `TryParseValueFromString`.
