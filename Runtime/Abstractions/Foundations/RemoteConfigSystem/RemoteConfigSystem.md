# Remote Config System

Một `ScriptableObject` giữ toàn bộ biến remote config của dự án: mỗi biến là một `RemoteConfig<T>`
mang `firebaseKey` + giá trị mặc định, author trong Inspector. Provider fetch xong thì collection
đẩy giá trị vào từng biến; call site đọc **không cast, có kiểu đầy đủ**.

SDK giữ máy móc, dự án khai biến **trong assembly của chính nó** — nên không model nào của game bị
biên dịch vào `com.horcrux.runtime`, và không cần `.asmref`.

## Hai phía

Phụ thuộc một chiều: dự án tham chiếu SDK, SDK không biết gì về dự án.

```
SDK — com.horcrux.runtime                Dự án — assembly của game
──────────────────────────────           ──────────────────────────────────────────
IRemoteConfigCollection                  IGameRemoteConfig
  interface thuần                          : IRemoteConfigCollection
  KHÔNG derive IService<>                  , IService<IGameRemoteConfig>
                                           → 1 property có kiểu cho mỗi biến

BaseRemoteConfigCollection               GameRemoteConfig
  abstract ScriptableObject                : BaseRemoteConfigCollection, IGameRemoteConfig
  quét field · wire provider                 [Service(typeof(IGameRemoteConfig),
  · apply · 2 nút Editor                              ResourcePath = "Config/…")]
                                           [CreateAssetMenu]
RemoteConfig<T>                            → 1 field [MarkedRemoteConfig] cho mỗi biến
MarkedRemoteConfig  (attribute)
IRemoteConfigProvider  (contract)        IRemoteConfigProvider  (bản cài đặt thật)
RemoteConfigCsv  (Editor authoring)      Resources/<ResourcePath>.asset
```

Ba thứ SDK **không** khai hộ được, luôn nằm phía dự án: `[Service]`, `[CreateAssetMenu]`, và đường
dẫn Resources.

## Đường đi của một giá trị

```
[Firebase / server]
      │ fetch
      ▼
IRemoteConfigProvider                      ← dự án cài, đăng ký [Service]
      │ OnFetched   (hoặc IsFetched == true nếu đã fetch trước)
      ▼
BaseRemoteConfigCollection.OnRemoteConfigFetched()
      │
      ├──▶ RemoteConfig<T>.ApplyRemoteValue(provider)      ← mỗi biến một lần
      │       provider.TryGetRemoteValue(firebaseKey) → parse → value
      │                                                      └→ PlayerPrefs[firebaseKey] = raw
      │
      └──▶ OnRemoteConfigsApplied()                        ← hook, dự án override
                │
                ▼
        call site:  IGameRemoteConfig.Service.MaxRetryCount
```

**Thứ tự khởi tạo không quan trọng.** Collection init trước thì nó subscribe rồi đợi `OnFetched`;
provider fetch xong trước thì `Initialize()` thấy `IsFetched == true` và apply ngay tại đó.

## Giá trị nào thắng

`allowFetching = true` — ba bậc, dừng ở bậc đầu tiên parse được:

```
1. Remote      TryGetRemoteValue có key, chuỗi không rỗng, parse OK
               → dùng, và ghi chuỗi RAW vào PlayerPrefs[firebaseKey]
2. PlayerPrefs cache của lần chạy trước, parse OK  → dùng
3. Inspector   giá trị author trong asset          → giữ nguyên
```

`allowFetching = false` — thoát ngay ở đầu `ApplyRemoteValue`: luôn dùng giá trị Inspector, không
đọc remote, không đọc cache. Đây là cách test một giá trị tại máy mà remote không đè lên.

Cache chỉ được ghi khi parse **thành công**, nên một payload sai format trên server không phá cache
đang tốt. Mỗi bậc trượt đều `Debug.LogError` kèm key và giá trị thấy được.

## Kiểu T hỗ trợ

| T | Cách parse |
|---|---|
| `string` | dùng nguyên chuỗi |
| `enum` | `Enum.Parse` — nhận **tên** hoặc **số** |
| `int` · `long` · `bool` | `Parse` thẳng |
| `float` · `double` | `Parse` với `InvariantCulture` — máy locale dấu phẩy vẫn đúng |
| còn lại | `JsonConvert.DeserializeObject<T>` — struct, class, `List<>`, `Dictionary<>` |

## Cách dùng

### 1. Provider (một lần cho cả dự án)

```csharp
[Service(typeof(IRemoteConfigProvider))]
public class FirebaseRemoteConfigProvider : IRemoteConfigProvider
{
    public event Action OnFetched;
    public bool IsFetched { get; private set; }

    public bool TryGetRemoteValue(string firebaseKey, out string value) { /* … */ }

    public async UniTask FetchAsync()
    {
        // …
        IsFetched = true;
        OnFetched?.Invoke();
    }
}
```

### 2. Collection (một lần cho cả dự án)

```csharp
// IGameRemoteConfig.cs
public partial interface IGameRemoteConfig : IRemoteConfigCollection, IService<IGameRemoteConfig> { }
```

```csharp
// GameRemoteConfig.cs
[Service(typeof(IGameRemoteConfig), ResourcePath = RESOURCE_PATH)]
[CreateAssetMenu(fileName = "GameRemoteConfig", menuName = "Configs/GameRemoteConfig")]
public partial class GameRemoteConfig : BaseRemoteConfigCollection, IGameRemoteConfig
{
    private const string RESOURCE_PATH = "Config/GameRemoteConfig";
}
```

`partial` để chia biến theo nhóm ra nhiều file (`GameRemoteConfig.Level.cs`, `.Monet.cs`…) — mỗi
nhóm một cặp file class + interface, cùng namespace.

Rồi `Create` → menu vừa khai → đặt asset vào `Assets/…/Resources/Config/GameRemoteConfig.asset`.
Đường dẫn **sau** `Resources/` phải khớp `RESOURCE_PATH`, sai là `Service` ném exception ở lần chạm
đầu tiên.

### 3. Thêm một biến — hai chỗ, luôn đi cùng nhau

```csharp
// interface: hợp đồng mà call site đọc
public partial interface IGameRemoteConfig
{
    RemoteConfig<int> MaxRetryCount { get; }
}
```

```csharp
// class: chỗ Unity serialize và Inspector vẽ
public partial class GameRemoteConfig
{
    [Splitter("Level")]                       // tùy chọn — kẻ tiêu đề nhóm trong Inspector
    [MarkedRemoteConfig]
    [SerializeField] private RemoteConfig<int> maxRetryCount;

    public RemoteConfig<int> MaxRetryCount => maxRetryCount;
}
```

Field để `private`: `public` là cho mọi caller gán được cả tham chiếu biến, việc chỉ authoring được
làm. Thiếu `[MarkedRemoteConfig]` thì biến **không vào** `RemoteConfigs` và **không có log nào** —
nó im lặng đứng ở giá trị Inspector mãi mãi.

Điền trong Inspector: `firebaseKey`, `allowFetching`, `value` mặc định.

### 4. Gọi Initialize một lần khi boot

```csharp
IGameRemoteConfig.Service.Initialize();
```

SDK không tự gọi. Gọi ở đâu cũng được, trước hay sau provider đều đúng.

### 5. Đọc

```csharp
int a = IGameRemoteConfig.Service.MaxRetryCount;         // implicit operator T
int b = IGameRemoteConfig.Service.MaxRetryCount.Value;   // tường minh

if (IGameRemoteConfig.TryGet(out var rc))                // đường an toàn khi có thể chưa đăng ký
    Use(rc.MaxRetryCount);
```

### 6. Phản ứng khi remote về (tùy chọn)

```csharp
protected override void OnRemoteConfigsApplied()
{
    // chạy MỘT lần sau khi đã apply xong mọi biến — chỗ parse JSON thành model, bắn event…
}
```

## Vòng đời

| Lúc | Chuyện gì xảy ra |
|---|---|
| `Initialize()` | quét field → `ResetFetchedState()` từng biến → gỡ subscription cũ → wire provider mới → nếu `IsFetched` thì apply ngay |
| `OnFetched` bắn | apply mọi biến → `OnRemoteConfigsApplied()` |
| `Initialize()` lần hai | an toàn: subscription cũ được gỡ **trước** khi field provider bị ghi đè, nên không để lại listener chết trên provider cũ |
| `OnDestroy` | gỡ subscription. Là `protected virtual` — dự án `override` thì **phải** gọi `base.OnDestroy()` |

Quét field có cache một lần mỗi phiên; apply chạy một lần mỗi lượt fetch. Không có gì chạy theo frame.

## Inspector

Mỗi `RemoteConfig<T>` vẽ ra:

| Field | Nghĩa |
|---|---|
| `firebaseKey` | key trên Remote Config server |
| `allowFetching` | tắt = chốt giá trị Inspector, bỏ qua remote và cache |
| `value` | giá trị mặc định, và là giá trị đang chạy sau khi apply |
| `fetched` | chỉ để xem: `true` = giá trị này đến từ remote lần fetch vừa rồi |
| `valueToImport` | ô dán JSON/CSV cho hai nút import |

## Nút Editor

| Nút | Ở đâu | Làm gì |
|---|---|---|
| `CopyJsonToClipboard` | mỗi biến | copy giá trị hiện tại ra JSON |
| `ImportJson` | mỗi biến | đọc `valueToImport` như JSON, ghi vào `value` |
| `CopyCsvToClipboard` | mỗi biến | copy giá trị ra CSV, dòng header trước |
| `ImportCsv` | mỗi biến | đọc `valueToImport` như CSV — **all-or-nothing**, sheet bị từ chối thì `value` không đổi |
| `SearchFirebaseKeyUsage(key)` | collection | in ra tên field đang mang key đó |
| `EnableAllFetching` | collection | bật `allowFetching` cho mọi biến |

Hai nút của collection khai ở `BaseRemoteConfigCollection` nhưng đọc field qua reflection, nên chạy
đúng trên mọi class dẫn xuất.

## CSV — chỉ để author trong Editor

JSON vẫn là format trên đường truyền; CSV chỉ để mang giá trị qua lại với spreadsheet.

`RemoteConfigCsv` nhận đúng ba hình dạng: một **scalar**, một **object phẳng** (một dòng), hoặc
`List<>` của object phẳng (nhiều dòng). Cell hợp lệ: `enum` · `string` · `int` · `long` · `float` ·
`double` · `bool`. Cột lấy từ field Unity serialize được (`public`, hoặc `private` + `[SerializeField]`).

Object có field không phải cell → không có dạng CSV, báo đúng tên field và bảo dùng JSON. Dòng đầu
được nhận là header nếu tên cột khớp, và header cho phép **đổi thứ tự cột**. Dòng trống, dòng mở đầu
bằng `#` hoặc `//` bị bỏ qua.

## Bẫy và quyết định thiết kế

Đọc mục này trước khi "sửa cho gọn" — mỗi dòng là một chỗ đã sai hoặc chắc chắn sẽ sai.

| Chỗ | Sự thật |
|---|---|
| **`IRemoteConfigCollection` không được derive `IService<>`** | interface dự án derive cả nó lẫn `IService<chính mình>` sẽ thừa hưởng **hai** thành viên static `Service` → mọi lần đọc `.Service` là **CS0229 ambiguity**, lỗi biên dịch. Code chỉ cần contract thì viết `IService<IRemoteConfigCollection>.Service` — `IService<T>.Service` là `Service.Get<T>()` nên chạy với bất kỳ `T` |
| **`[Service]` không di truyền** | `ServiceAttribute` khai `Inherited = false`. Khai ở `BaseRemoteConfigCollection` là khai vào chỗ không ai đọc: service không đăng ký, `Service.Get<>()` ném ở lần chạm đầu. `AllowMultiple = true` nên một class dẫn xuất đăng ký được dưới nhiều service type, cùng một `ResourcePath` vẫn ra **một** instance vì `Resources.Load` trả cùng asset |
| **Reflection không thấy private field của base** | scan dùng `GetType().GetFields(NonPublic \| Instance)` nên lấy đúng private field của class dẫn xuất, nhưng **bỏ** private field khai trên `BaseRemoteConfigCollection`. Biến khai ở base phải là `protected`, để `private` là bị bỏ qua không lỗi |
| **`OnDestroy` phải qua `base`** | magic method của Unity: subclass đặt trùng tên là bản base bị che, `OnFetched` không được gỡ, một listener chết nằm lại trên provider. Đã khai `protected virtual` nên compiler nhắc nếu viết `new` thay vì `override` |
| **`== null` trên `RemoteConfig<string>` so CHUỖI, không so field** | `implicit operator T` biến `RemoteConfig<string>` thành `string` trước khi so, nên `variable == null` là `string == null`. Muốn kiểm tra chính field thì `ReferenceEquals(variable, null)` |
| **Ô import là `[NonSerialized]`** | `[SerializeField]` bọc trong `#if UNITY_EDITOR` làm Editor ghi field đó vào asset mà build strip nó đi → layout mismatch → **crash native** khi đọc asset (`Read N bytes but expected M bytes`). Muốn hiện trong Inspector mà không serialize thì `[NonSerialized, ShowInInspector]` |
| **Field mark nhưng null hoặc sai kiểu** | `Initialize()` log tên field rồi `continue`, không ném — các biến còn lại vẫn apply và provider vẫn được wire |
| **Không tạo asset trực tiếp từ base** | `abstract` là cố ý: một collection không có biến nào thì không có việc gì làm. `[CreateAssetMenu]` nằm ở class dẫn xuất |
| **Một asset cho cả dự án, không phải mỗi biến một SO** | mở một asset là thấy cả bảng cấu hình và import CSV vào được. Một class + một asset cho mỗi biến `bool` đắt hơn nhiều lần thứ nó mua |

## Chữ ký

**`Horcrux.Runtime.Abstractions.RemoteConfigSystem`**

| Type | Thành viên |
|---|---|
| `IRemoteConfig` | `string FirebaseKey { get; }` · `bool AllowFetching { get; set; }` · `void ApplyRemoteValue(IRemoteConfigProvider)` · `void ResetFetchedState()` |
| `IRemoteConfigProvider : IService<IRemoteConfigProvider>` | `event Action OnFetched` · `bool IsFetched { get; }` · `bool TryGetRemoteValue(string firebaseKey, out string value)` |
| `IRemoteConfigCollection` | `IEnumerable<IRemoteConfig> RemoteConfigs { get; }` · `IRemoteConfigProvider RemoteConfigProvider { get; }` · `void Initialize()` |

**`Horcrux.Runtime.Implementations.RemoteConfigSystem`**

| Type | Thành viên |
|---|---|
| `RemoteConfig<T> : IRemoteConfig` | `[Serializable]` · `T Value { get; }` · `implicit operator T` (null → `default`) |
| `MarkedRemoteConfig : Attribute` | `[AttributeUsage(AttributeTargets.Field)]` — đánh dấu field để scan nhận |
| `BaseRemoteConfigCollection : ScriptableObject, IRemoteConfigCollection` | `abstract` · `void Initialize()` · `protected virtual void OnRemoteConfigsApplied()` · `protected virtual void OnDestroy()` |
| `RemoteConfigCsv` | `internal static` · `bool TryParse(Type, string csv, out object, out string report)` · `bool TryFormat(Type, object, out string csv, out string error)` |

**`Horcrux.Runtime.Abstractions`** — `IService<out T>`: `static T Service` · `static bool TryGet(out T)`.

## Cấu trúc file

```
Runtime/Abstractions/Foundations/RemoteConfigSystem/
├── IRemoteConfig.cs              hợp đồng một biến
├── IRemoteConfigCollection.cs    hợp đồng collection — interface THUẦN, xem mục Bẫy
└── IRemoteConfigProvider.cs      hợp đồng nguồn fetch

Runtime/Implementations/Foundations/RemoteConfigSystem/
├── RemoteConfig.cs               RemoteConfig<T> — giữ giá trị, parse, cache PlayerPrefs, nút JSON/CSV
├── BaseRemoteConfigCollection.cs abstract base + MarkedRemoteConfig — scan, wire, apply, 2 nút
├── RemoteConfigCsv.cs            CSV ⇄ value, Editor authoring
└── RemoteConfigSystem.md         tài liệu này
```
