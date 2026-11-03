# Persistence System

Code: `Runtime/Abstractions/Foundations/Persistence/` · `Runtime/Implementations/Foundations/Persistence/` ·
`Runtime/Implementations/Composites/SaveBootstep*.cs`

Mỗi thứ lưu được là một `PersistenceDataEntry<T>`: khoá · giá trị mặc định · giá trị đang chạy · **payload kho đang
giữ**. Mọi entry là field của **một** `ScriptableObject` dẫn xuất `BasePersistenceDataCollection`. Collection quét
field, load lúc boot, autosave, và là **chỗ duy nhất** gọi `PlayerPrefs.SetString` / `PlayerPrefs.Save()`.

**Bảo đảm: mất tối đa MỘT chu kỳ autosave.** Swipe-kill trên Android và hệ thu hồi RAM **không** chạy
`OnApplicationQuit`; tín hiệu tin được cuối cùng là lúc vào background. Autosave là lưới chính, flush lúc vào
background là chốt sổ, flush lúc quit là thêm được thì tốt. Hệ không hứa "không mất gì".

## Hai phía

Phụ thuộc một chiều: dự án → SDK. Model save của dự án không bị biên dịch vào `com.horcrux.runtime`, không cần `.asmref`.

| SDK giữ | Dự án khai |
|---|---|
| `IPersistenceDataEntry` · `IPersistenceDataCollection` — interface thuần, **không** derive `IService<>` | `IMySaveCollection : IPersistenceDataCollection, IService<IMySaveCollection>` — một property cho mỗi entry |
| `PersistenceDataEntry<T>` · `BasePersistenceDataCollection` (abstract) · `MarkedPersistence` | `MySaveCollection : BasePersistenceDataCollection` — một field `[MarkedPersistence]` cho mỗi entry, model save là class dữ liệu thuần |
| hai host `SaveDriver` · `SaveBootstep` (+ `SaveBootstepInitializer`) | `[Service]` · `[CreateAssetMenu]` · đường dẫn Resources · **chọn đúng một host** |

## Luồng dữ liệu

```
authoring ─► Inspector của asset: key · defaultValue

boot      ─► host gọi Initialize() MỘT lần
              ScanEntries ─► Setup từng entry ─► ResetDerivedState ─► LoadAllEntries ─► OnEntriesLoaded
              ─► IsInitialized = true ─► có entry Seeded? LogWarning + FlushInternal(null)

chơi      ─► entry.Value = x        ─► OnValueChanged(old, x)      không chạm đĩa
              entry.Value.field = x  ─► KHÔNG event                 không chạm đĩa, flush kế vẫn thấy
              đọc: entry.Value hoặc implicit operator T (entry null → default)

flush     ─► autosave mỗi autosaveIntervalSeconds · vào background · quit · FlushNow()
              ① mọi entry: payload = WritePayload() ─► khác StoredPayload? SetString   (RAM của PlayerPrefs)
              ② có entry vừa ghi ─► PlayerPrefs.Save() MỘT lần ─► rồi mới CacheStoredPayload
```

**Giá.** Serialize thuộc nhịp flush, không thuộc nhịp đổi giá trị; mỗi flush serialize **mọi** entry vì đó là cách
duy nhất thấy model bị sửa tại chỗ — serialize **là** lần duyệt object, và sản phẩm của nó chính là chuỗi cần ghi. So
tham chiếu rẻ hơn nhưng chỉ bắt được đường qua setter. Đo trên desktop CoreCLR với một model save cỡ vừa: 4–13 µs và
2–16 KB rác cho một lượt serialize + so chuỗi; `Save()` cùng tick đắt hơn vài bậc. Nhịp 10 s và lúc vào background,
không phải hot path. Reflection quét field cache một lần mỗi phiên. Không gì chạy theo frame.

## Load một entry — ba kết cục

```
HasKey("persistence_" + key)?
 ├─ không ─────────────────────────────► Seeded   storedPayload = null → flush cuối Initialize ghi mặc định
 └─ có ─► payload rỗng? ─ có ──────────► Seeded
                        └ không ─► ReadPayload
                                    ├─ ok ─────► Loaded   value = payload (chuỗi "null" → CloneDefault)
                                    │                     storedPayload = payload · OnValueChanged(mặc định, value)
                                    └─ ném ────► Failed   value = mặc định · CacheStoredPayload(WritePayload())
                                                          LogError + LogException · đĩa KHÔNG bị chạm
```

`Failed` không seed vì ghi đè payload hỏng là xoá bản sao duy nhất của bằng chứng. Coi mặc định là "đã lưu" nên flush
bỏ qua; người chơi đổi gì thì payload khác đi và lượt flush kế ghi đè — dữ liệu sống thắng bằng chứng chết.

## Kịch bản

| # | Kịch bản | Diễn biến | Kết quả |
|---|---|---|---|
| 1 | Cài lần đầu | mọi entry `Seeded` → `LogWarning` *"N/M entries had no stored key"* → flush | mọi khoá có trên đĩa với `Default Value` trước khi người chơi chạm gì. Giá: một `Save()` ở boot |
| 2 | Mở lại app | mọi entry `Loaded` → `seededCount == 0` → không flush | đĩa không bị chạm. `OnValueChanged` của lượt load chưa có người nghe (Setup vừa xoá listener) |
| 3 | Đổi giá trị rồi autosave | setter phát event → view cập nhật; `UniTask.Delay(…, DelayType.Realtime)` hết nhịp → flush | `Realtime`: ngồi ở popup pause `timeScale = 0` vẫn lưu. Chốt sổ sớm (mua IAP) thì `FlushNow()` |
| 4 | Sửa model tại chỗ | `entry.Value.coins += 5` — không qua setter | **được lưu** ở flush kế (so payload thấy), **không** phát event. View sống bằng event thì ghi qua setter |
| 5 | Vào background rồi swipe-kill | Unity bắn cả `OnApplicationPause(true)` lẫn `OnApplicationFocus(false)`; host gộp thành một cạnh → `FlushAll()` | quit không chạy nhưng đã chốt sổ. Chết **giữa lúc chơi** (thu hồi RAM, crash native) thì mất phần từ autosave gần nhất — toàn bộ khoảng hở hệ thừa nhận |
| 6 | Update thêm entry | entry cũ `Loaded`, entry mới `Seeded` → `LogWarning "1/M"` → flush chỉ ghi entry mới | từ phiên thứ hai, dòng warning này là **bất thường**: khoá vừa mất khỏi đĩa hoặc `Key` vừa bị đổi |
| 7a | Đọc hỏng | kết cục `Failed` (sơ đồ trên) | entry khác trong cùng lượt không bị kéo theo |
| 7b | Ghi hỏng — `WritePayload` ném | `LogError` nêu khoá + `LogException`, `StoredPayload` không đổi; entry khác vẫn `SetString` và `Save()` | kêu lại **mỗi chu kỳ** tới khi sửa kiểu — im lặng là một entry không bao giờ lưu |

## Ca biên

| Ca | Kết quả |
|---|---|
| `FlushAll()` trước `Initialize()` | `entries` rỗng → không ghi gì; mặc định không đè save thật, nhưng game chạy trên default trong RAM |
| `FlushNow()` trên entry chưa qua `Setup` (thiếu `[MarkedPersistence]`, hoặc trước `Initialize()`) | `LogError` *"No collection owns …"*. Đọc/ghi `Value` vẫn chạy im lặng trong RAM |
| `Flush(entry)` với entry null / không thuộc collection | `LogError` rồi thoát |
| Quit hoặc vào background khi `BootstrapService` chưa init xong (`SaveBootstep`) | service không fan-out → không flush, không reset cờ. Chấp nhận: chưa có dữ liệu đáng mất. `SaveDriver` không có cổng này |
| Entry bị `ScanEntries` loại (khoá rỗng, khoá trùng — giữ field đầu, field sai kiểu) | mỗi ca một `LogError` nêu tên field; `Initialize()` bỏ qua con số loại, chỉ `ValidateKeys` tổng kết |
| Sửa `PlayerPrefs` bằng tay lúc Play | không reload giữa phiên; Play lại. Xem/sửa bằng tool `Editor/PlayerPrefsEditor` |
| Listener ném exception | mỗi delegate bọc `try/catch` riêng → `LogError` nêu khoá, listener khác vẫn chạy |

## Hợp đồng payload

`WritePayload` / `ReadPayload` / `CloneDefault` dùng `JsonConvert` **không settings**. Hai luật, hỏng hai kiểu:

| Luật | Vi phạm thì | Kiểm |
|---|---|---|
| **1 · `T` không chạm kiểu `UnityEngine`** — kể cả nằm sâu trong field, property, phần tử | flush **ném** | nút `ValidateKeys` (theo kiểu, bắt cả container rỗng) |
| **2 · cùng dữ liệu → cùng chuỗi** | mọi flush thấy "đổi" → `Save()` cả kho mỗi chu kỳ, trên Android là `commit()` đồng bộ main thread, **không một dòng log** | developer tự đảm bảo; chưa có nút |

**Luật 1.** Newtonsoft đọc cả public field lẫn **property có getter**, nên chạm property tính toán của struct toán Unity:

| `T` | `JsonConvert.SerializeObject` (đo CoreCLR 6.0.21) |
|---|---|
| `Vector3`, `Vector2` | `JsonSerializationException: Self referencing loop detected for property 'normalized'` |
| `Quaternion`, `Color` | cùng bẫy (`normalized`, `linear`) — chưa đo ngoài Unity vì gọi native |
| `Vector2Int` | không ném, payload mang rác: `{"x":3,"y":4,"magnitude":5.0,"sqrMagnitude":25}` |
| `int`, `bool`, `string`, class/struct tự khai | sạch |

Cấm struct toán (`Vector2/3/4`, `Vector2Int`, `Quaternion`, `Color`, `Rect`, `Bounds`, `Matrix4x4`…) và `Object` engine
(`GameObject`, `Transform`, `AnimationCurve`…). Cần vị trí thì khai struct riêng — hình dạng save là hợp đồng của dự án:

```csharp
[Serializable]
public struct SavedPosition
{
    public float x, y, z;
}
```

Lộ lúc nào: kiểu engine nằm trong `Default Value` → `CloneDefault()` ném ở `Initialize()`, đỏ mọi lần Play. Nằm trong
container **rỗng lúc mặc định** (`List<Vector3>`) → chỉ ném ở flush đầu sau khi runtime nhồi dữ liệu (kịch bản 7b) — ca
duy nhất lọt xuống build.

**Luật 2 — kiểu dễ vỡ:**

| Kiểu | Vì sao | Thay bằng |
|---|---|---|
| `HashSet<T>`, `Dictionary<K,V>` | không hứa thứ tự duyệt | `List<T>` sắp trước khi ghi |
| `DateTime` | `Kind` khác → chuỗi khác cho cùng mốc | `long` ticks hoặc epoch seconds UTC |
| property trả giá trị mới mỗi lần đọc | Newtonsoft ghi mọi property có getter | `[JsonIgnore]` hoặc bỏ khỏi model |

## Trước khi chạy

Hệ không tự setup: không tạo GameObject, không tra asset, không guard ô phải điền. Dựng từ 0 bằng sáu bước theo thứ tự.

**1 · Interface dự án** — assembly của dự án, không dưới `Horcrux.*`:

```csharp
public partial interface IMySaveCollection
    : IPersistenceDataCollection, IService<IMySaveCollection>
{
    PersistenceDataEntry<int> Coin { get; }
    PersistenceDataEntry<PlayerProgress> Progress { get; }
}
```

**2 · Collection dự án:**

```csharp
[Service(typeof(IMySaveCollection), ResourcePath = RESOURCE_PATH)]
[Service(typeof(BasePersistenceDataCollection), ResourcePath = RESOURCE_PATH)]
[CreateAssetMenu(fileName = "PersistenceDataCollection", menuName = "Configs/PersistenceDataCollection")]
public sealed partial class MySaveCollection : BasePersistenceDataCollection, IMySaveCollection
{
    // Must match where the asset sits under a Resources folder, or the service never resolves.
    private const string RESOURCE_PATH = "Config/PersistenceDataCollection";
}
```

Hai dòng `[Service]` cùng `ResourcePath` nên `Resources.Load` trả **một** instance: dòng interface cho game code
(`IMySaveCollection.Service`), dòng base cho `SaveDriver` và `TimeService`. `IPersistenceDataCollection` **không** đăng
ký — không host nào nhận nó, đăng ký là bày một đường lấy collection không ai dùng. Rồi **Create → Configs →
PersistenceDataCollection**, đặt asset dưới `Resources/` sao cho đường sau `Resources/` khớp `RESOURCE_PATH`. `partial`
để chia entry theo nhóm ra nhiều file — mỗi nhóm một cặp class + interface, cùng namespace, **cùng assembly**.

**3 · Thêm entry — hai chỗ luôn đi cùng nhau:**

```csharp
public partial interface IMySaveCollection
{
    PersistenceDataEntry<int> Coin { get; }
}

public partial class MySaveCollection
{
    [Splitter("Economy")]
    [MarkedPersistence]
    [SerializeField] private PersistenceDataEntry<int> coin;

    public PersistenceDataEntry<int> Coin => coin;
}
```

Field `private`: scan chỉ quét `NonPublic`, và `public` cho mọi caller gán đè tham chiếu entry. Điền `Key` (chuỗi tự
đặt, không suy từ tên field) và `Default Value` trong Inspector. Feature cần nhiều entry riêng thì tách **interface**
(`IEconomySave`, không derive `IService<>`; tra riêng thì thêm `[Service(typeof(IEconomySave), ResourcePath = …)]`),
không tách asset.

**4 · Đúng MỘT host, cấp asset cho nó** — chọn ở mục "Hai host", kéo tay vào scene:

| Đường cấp | Làm gì | Dùng cho |
|---|---|---|
| Service | không làm gì — dòng `[Service(typeof(BasePersistenceDataCollection))]` ở bước 2 đủ | chỉ `SaveDriver` (`MonoBehaviour<T>`) |
| Initializer | Inspector host → mục **Init** → kéo asset vào ô **Save Collection** (`SaveBootstepInitializer` có sẵn) | cả hai; **bắt buộc** với `SaveBootstep` vì nó không phải service |

**5 · Đọc và ghi:**

```csharp
int coin = IMySaveCollection.Service.Coin;                 // implicit operator T
IMySaveCollection.Service.Coin.Value = coin + 10;          // OnValueChanged(coin, coin + 10)
IMySaveCollection.Service.Coin.OnValueChanged += (oldValue, newValue) => { };
IMySaveCollection.Service.Coin.FlushNow();                 // chốt sổ sớm một entry
IMySaveCollection.Service.FlushAll();                      // chốt sổ sớm cả kho
```

Subscribe **sau** `Initialize()` — `Setup()` xoá listener.

**6 · Hook cho state của subclass (tùy chọn):**

```csharp
protected override void ResetDerivedState() { /* TRƯỚC LoadAllEntries — xoá cache phiên trước */ }
protected override void OnEntriesLoaded()  { /* SAU LoadAllEntries — dựng lookup */ }
```

Cần cả hai: chỉ có hook "sau" thì lượt load không chạm entry nào vẫn để cache phiên trước sống.

**Thiếu bước nào thì hỏng ở đâu:**

| Bỏ qua | Hỏng |
|---|---|
| Khai collection trong Horcrux | kéo type dự án vào SDK — submodule không commit được một mình |
| `[MarkedPersistence]` | entry không tồn tại với hệ: chạy trong RAM, không bao giờ xuống đĩa; chỉ `FlushNow()` mới kêu |
| Field entry `private` ở class base | `GetFields(NonPublic)` không thấy private của base; khai ở base thì `protected` |
| `Key` rỗng | `ScanEntries` loại kèm `LogError` |
| `Default Value` rỗng | người chơi mới nhận default của kiểu; không phân biệt được với cố ý, và xuống đĩa ngay phiên đầu |
| Không cấp asset cho host | `NullReferenceException` ở `Initialize()`. `SaveDriver`: `Start()` dừng. `SaveBootstep`: `BootstrapService` fail-open, cả phiên chạy **không** persistence |
| Host trên object không sống suốt phiên | token chết theo scene → autosave **chết im lặng**. `SaveDriver` tự `DontDestroyOnLoad`; `SaveBootstep` dựa `BootstrapService` |
| Hai host, hoặc gọi `Initialize()` thêm | mất tiến độ chưa flush, im lặng — mục "Hai host" |
| Không bấm `ValidateKeys` trước Play | lỗi authoring chỉ lộ lúc `Initialize()` hoặc muộn hơn |

## Hai host — đúng một trong cả dự án

| | `SaveDriver` | `SaveBootstep` |
|---|---|---|
| Dùng khi | không có Bootstrap | có `BootstrapService` |
| Là gì | `MonoBehaviour<BasePersistenceDataCollection>`, `DontDestroyOnLoad` ở `OnAwake` | `ABootStep, IInitializable<BasePersistenceDataCollection>` |
| Nhận collection | service hoặc Initializer | chỉ Initializer |
| `Initialize()` + autosave | `Start()` · `destroyCancellationToken` | `InitializeAsync()` · `destroyCancellationToken` — **không** token của pha, service huỷ nó mỗi lần load level |
| Flush | cạnh vào background (cờ `inBackground` gộp focus + pause) · `OnApplicationQuit` | `OnGoToBackground(true)` · `OnAppQuit`, chỉ sau khi `BootstrapService` init xong |
| Thứ tự với hệ khác | không có — Unity không hứa thứ tự magic method | có — fan-out vào background và quit **ngược** thứ tự step |
| Lúc quit | `FlushAll()` + `IsInitialized = false` | như trái |

**`Initialize()` không có guard, nên lần gọi thứ hai là mất dữ liệu im lặng:**

| `Setup()` làm | Mất |
|---|---|
| `value = CloneDefault()` | mọi thay đổi từ lần flush cuối |
| `storedPayload = null` | mốc so sánh; `LoadAllEntries` nạp lại cả hai từ đĩa — không còn dấu vết để ghi |
| `OnValueChanged = null` | mọi listener; view ngừng nhận thay đổi |

Đĩa vẫn giữ bản cũ nên soi save không thấy gì lạ. **Sáu đường sinh ra lần thứ hai** — kiểm trong Editor: hai
`SaveDriver` · hai `SaveBootstep` · một của mỗi loại (tổng mọi scene, prefab phải đúng một) · cùng `SaveBootstep` hai
lần trong list `steps` · game code gọi `.Initialize()` trên kiểu save · host là prefab spawn lúc chạy. **Cố ý không có
cờ chặn:** `if (isInitialized) return;` che luôn ca cần biết — hai vòng autosave, hai đường flush, wire sai không ai
sửa. Chặn bằng cấu trúc: một object kéo tay, đếm được.

**Chia việc:** vòng autosave và `autosaveIntervalSeconds` ở collection — cả hai host cần đúng một nhịp, bảo đảm "một
chu kỳ" là của collection. **Token ở host** — `ScriptableObject` không có mốc kết thúc, tự mở
`CancellationTokenSource` thì không ai đóng, và mỗi lần Play (tắt domain reload) xếp thêm một loop. Host nhận
`BasePersistenceDataCollection`, không nhận interface: `TimeService` cần `LastSeenUtcSeconds` chỉ base khai.

## Inspector và nút Editor

| Field | Serialize | Nghĩa |
|---|---|---|
| `key` | có | khoá logic; khoá thật `"persistence_" + key` |
| `defaultValue` | có | giá trị người chơi mới; **sao chép** vào `value` lúc `Setup` |
| `value` | không | giá trị đang chạy, `[ShowInInspector, ReadOnly]` |
| `storedPayload` | không | thứ flush coi như kho đã có; khác payload hiện tại = còn thay đổi chưa xuống đĩa |
| `payloadToImport` | không | ô JSON cho `ImportPayload`, chỉ Editor |
| `autosaveIntervalSeconds` (collection) | có | mặc định 10 s, `[Min(5f)]` |

`value`, `storedPayload` không serialize: tiến độ người chơi vào asset là vào git.

| Nút | Ở | Làm gì |
|---|---|---|
| `ImportPayload` | mỗi entry | parse `payloadToImport`, gán qua **setter** (có event, flush kế ghi). Chỉ Play Mode; rỗng hoặc parse lỗi thì báo, không đổi |
| `ValidateKeys` | collection | khoá trùng · khoá rỗng · field mark chưa gán hoặc sai kiểu · `T` chạm `UnityEngine`. Sạch: một dòng kèm số entry; bẩn: một dòng đỏ mỗi nhóm kèm tỉ lệ |

`ValidateKeys` dùng danh sách scratch, không đụng `entries`; in đường tới kiểu engine đầu tiên
(`Entry 'route' stores SavedRoute.lastCheckpoint → Vector3`), bỏ qua `[JsonIgnore]`, `[NonSerialized]` và nội tạng kiểu
`System.*` như Newtonsoft. Không thấy được field khai `object`, `dynamic`, interface — đừng khai model save bằng chúng.
Cả hai nút trong `#if UNITY_EDITOR`. Không có nút xoá save theo tiền tố: `PlayerPrefs` không liệt kê được khoá, khoá mồ
côi của build cũ sẽ sống sót.

## Event `OnValueChanged`

`event Action<T, T>` — `(oldValue, newValue)`. Bắn từ setter `Value` (gồm `ImportPayload`) và `ReadPayload`; sửa tại
chỗ không bắn.

| Ca | Đối số |
|---|---|
| `Value = x` | `(giá trị bị thay, x)` |
| `Value = x` khi đã là `x` | vẫn bắn, `old == new` — setter không so; listener tracking tự lọc |
| `ReadPayload` lúc load | `(bản clone mặc định, giá trị đọc)` — chưa ai nghe |
| `T` là class, sửa tại chỗ rồi gán lại cùng tham chiếu | hai đối số là **một object**; cần bản cũ thì gán object mới |

## Kiểm thử

`Tests/EditMode/PersistenceSeedingTests.cs` — 12 case. Fixture `ProbeCollection` khai năm entry (`int` ×2 · `string` ·
model có `List<int>` · `List<Vector3>`) cộng `lastSeenUtcSeconds` kế thừa; `SetUp` điền `Key` cho **tất cả** qua
`SerializedObject` — cùng cửa Inspector. Mỗi case chỉ khẳng định trên khoá của nó.

| Case | Khẳng định |
|---|---|
| Chưa có khoá · khoá có payload rỗng | khoá lên đĩa mang mặc định, `StoredPayload` khớp |
| Khoá đọc được | giá trị lưu thắng mặc định, đĩa không bị chạm |
| Payload hỏng | về mặc định, một `LogError` nêu khoá; `FlushAll()` vẫn không chạm chuỗi hỏng |
| Payload hỏng rồi người chơi đổi | flush kế ghi đè bằng giá trị mới |
| Kho giữ `"null"` | `Value` về mặc định, flush ghi mặc định |
| Sửa field tại chỗ | flush ghi payload mới |
| Không đổi gì · gán lại đúng giá trị cũ | flush không `SetString`: chuỗi lạ ghi tay vẫn nguyên |
| `FlushNow()` một entry | chỉ khoá đó bị ghi |
| `WritePayload` ném, flush hai lần | mỗi lượt một `LogError`, `StoredPayload` không đổi |
| Gán `Value` hai lần, lần hai trùng | mỗi lần một event mang `(old, new)` |

Biên đã phủ: rỗng · một phần tử · payload hỏng · `"null"` · hai entry một lượt · flush lặp.

## Bất biến

| Bất biến | Ở đâu, vì sao |
|---|---|
| `StoredPayload` chỉ cập nhật **sau** `Save()` | `FlushInternal` hai giai đoạn. Cập nhật trước là nói dối "đã lưu" và thay đổi không bao giờ được thử lại |
| Chuỗi ghi xuống và chuỗi nhớ lại là **một** biến | gọi `WritePayload()` lần hai là hai nguồn buộc khớp; `T` không ổn định thì lệch im lặng, ghi mãi |
| `CacheStoredPayload` không gọi được từ game code | explicit interface implementation |
| Điều kiện ghi nằm cạnh lệnh ghi | phép so payload ở `FlushInternal`, cửa ghi duy nhất |
| `SetString` / `Save()` chỉ ở `FlushInternal` | cửa thứ hai là đường ngoài phép so và ngoài bảo đảm |
| Chỉ seed khi kho không có gì đọc được | `Initialize` không ghi mặc định đè save thật; payload hỏng không seed |
| Một `Save()` mỗi lượt, chỉ khi có entry vừa ghi | Android `commit()` ghi cả kho đồng bộ main thread; vì vậy `[Min(5f)]` |
| Đúng một host | `Initialize()` không guard |
| **Khoá là wire format** | đổi `Key`, `KeyPrefix`, hay hình dạng model sau khi ship = save mồ côi, không gì báo |
| Không đường không-làm-gì âm thầm | khoá trùng/rỗng, field sai, đọc/ghi hỏng, `Flush` entry lạ, `FlushNow` không chủ — mỗi ca một `LogError` nêu khoá hoặc field |

## Bẫy và quyết định

Đọc trước khi "sửa cho gọn".

| Chỗ | Sự thật |
|---|---|
| Entry khai ở base = nghĩa vụ điền `Key` ở **mọi** collection con | `lastSeenUtcSeconds` có mặt ở mọi lớp con, kể cả fixture test. *Đã sai một lần:* thêm field vào base, bốn case test đỏ ở dòng `LogError`, không ở khẳng định — sửa chỗ dựng collection, không sửa guard |
| Listener đăng ký trước `Initialize()` bị xoá | `ScriptableObject` sống qua các lần Play khi tắt domain reload: listener phiên trước trỏ object đã huỷ → `MissingReferenceException`. Không phân biệt được listener cũ với mới, nên chặn bằng cấu trúc: `Initialize()` ở pha boot, subscribe sau |
| Bốn thứ sống qua các lần Play | `value`, `storedPayload` (Setup reset) · cache của subclass (cặp hook) · `isInitialized` — host đặt `false` lúc quit; `TimeService.UtcNowUnix` đọc cờ này để trả giờ máy khi save chưa load. Fail-open: quit không bắn thì cờ ở lại `true` |
| `[ReadOnly]` trên `value` | sửa tay bỏ qua setter: flush ghi số mới, view vẫn số cũ. Cửa sửa tay là `ImportPayload` |
| `storedPayload` không phải "chuỗi vừa ghi" | ở nhánh `Failed` nó là payload mặc định chưa từng xuống đĩa; `lastWrittenPayload` là tên sai đúng ca khó nhất |
| `defaultValue` phải sao chép | gán thẳng là để game mutate vào asset. `CloneDefault()` round-trip serializer, đúng mọi `T` |
| Quên `Initialize()` | Remote Config quên thì về giá trị author, hồi được; save quên thì mọi thứ trong RAM rồi mất |
| `[SerializeField]` trong `#if UNITY_EDITOR` | Editor ghi field build strip → lệch byte → **crash native** không stack trace. Muốn hiện không serialize: `[NonSerialized, ShowInInspector]` |
| `IPersistenceDataCollection` không derive `IService<>` | interface dự án derive cả hai thừa hưởng hai `static Service` → **CS0229** |
| `[Service]` không di truyền | `Inherited = false`: khai ở base là không ai đọc. Vì vậy `[Service]`, `[CreateAssetMenu]` thuộc dự án, base `abstract` |
| `partial` không qua ranh assembly | nửa dự án vào SDK bằng `.asmref` kéo cả model theo; chiều ngược là circular. Khuôn là **kế thừa** |
| `implicit operator T` khi so sánh | `entry == null` so tham chiếu. So với toán hạng kiểu `T` thì entry bị convert: `PersistenceDataEntry<string>` có `Value` null → `entry == null` là `false`, `entry == s` (`s` null) là `true` |
| Magic method ở subclass che bản base | collection hôm nay không dùng magic method; thêm thì `protected virtual` |
| `Flush(entry)` không rẻ hơn `FlushAll()` ở phần đĩa | `Save()` ghi cả kho; nó để chốt sổ sớm, không để tối ưu |
| *Đã sai một lần:* reset cờ dirty trước khi ghi | ghi luôn chạy, và một lần ghi lỗi mất im lặng vì cờ đã tắt |
| *Đã sai một lần:* serialize theo nhịp đổi giá trị | mỗi thay đổi → `ToJson` cả blob + `Save()` trong frame |
| *Đã sai một lần:* đường no-op im lặng | hàm gán service không caller → autosave chạy đều mà không lưu, không log |
| *Đã sai một lần:* deserialize không `try/catch` | exception mỗi boot, save brick vĩnh viễn |

**Ba giới hạn của PlayerPrefs** — một file nền tảng ghi hộ (Android `SharedPreferences`, iOS `NSUserDefaults`, Editor
Windows registry `HKCU\Software\<Company>\<Product>`), parse một lần rồi giữ RAM: cả kho **một khối** (hỏng một lần mất
mọi khoá) · mỗi `Save()` ghi **toàn bộ** · cả kho nằm RAM suốt phiên. Tổng payload vượt vài chục KB thì đổi sang kho
file — chỉ sửa nội bộ `FlushInternal` và `LoadEntry`. Không gian khoá phẳng chung với Firebase, ads, cache
`RemoteConfig<T>`; tiền tố `"persistence_"` là thứ duy nhất chặn khoá SDK đè tiến độ.

**Cố ý không làm:**

| Không làm | Vì sao |
|---|---|
| Cờ dirty song song phép so payload | hai cơ chế trả lời một câu hỏi phải khớp đời đời; cờ là thứ phải nhớ gọi |
| `HasUnstoredChanges` cho Inspector | serialize mỗi lần vẽ lại; ô `storedPayload` đủ để soi |
| Phép so payload trong entry · entry tự lo nhánh đọc hỏng | tách điều kiện khỏi lệnh ghi bắt đọc hai file |
| `Prefs<T>` cho giá trị lẻ | bộ máy thứ hai nằm ngoài phép so và ngoài `Save()` |
| `ISerializer` · `IPersistenceStore` | mỗi cái một implementation; "entry chỉ nói bằng chuỗi" đủ để đổi kho |
| `Register()` lúc runtime | không liệt kê, không kiểm trùng, quên gọi thì im lặng, không mốc load |
| Kiểm trong `OnValidate` | lúc thêm entry khoá luôn rỗng — cảnh báo thường trực |
| Ghi nguyên tử, crypto, cloud sync, migration | nền tảng ghi cả kho một lượt; chưa có nhu cầu thật |

**Bốn chỗ cố ý khác Remote Config** (cùng khuôn: abstract base · `sealed partial` + `partial interface` · attribute +
reflection · `[Service]` dưới interface dự án · `RESOURCE_PATH`; sửa khuôn một bên thì sửa cả hai; thêm chỗ thứ năm phải
ghi lý do ở đây):

| Chỗ khác | Lý do |
|---|---|
| Cặp hook `ResetDerivedState` / `OnEntriesLoaded` | Remote Config chỉ có hook "sau" và đã sinh cờ mang trạng thái phiên trước |
| `value`, `storedPayload` không serialize | tiến độ người chơi không vào git |
| `[ReadOnly]` trên `value` | sửa tay làm màn hình và save lệch |
| So payload thay vì cờ `fetched` | Remote Config hỏi "đã fetch chưa" — sự kiện của chính nó; Persistence hỏi "người chơi đổi gì" — qua tham chiếu entry không quan sát được |

## Chữ ký

Namespace `Horcrux.Runtime.Abstractions.Persistence`:

| Type | Thành viên |
|---|---|
| `IPersistenceDataEntry` | `string Key { get; }` · `string StoredPayload { get; }` · `void Setup(IPersistenceDataCollection)` · `void ReadPayload(string)` · `string WritePayload()` · `void CacheStoredPayload(string)` |
| `IPersistenceDataCollection` | `IReadOnlyList<IPersistenceDataEntry> Entries { get; }` · `bool IsInitialized { get; set; }` · `void Initialize()` · `void FlushAll()` · `void Flush(IPersistenceDataEntry)` · `UniTask RunAutosaveAsync(CancellationToken)` |
| `PersistenceDataEntry<T>` | `[Serializable]` · `T Value { get; set; }` · `event Action<T, T> OnValueChanged` · `void FlushNow()` · `implicit operator T` · `Setup` / `ReadPayload` / `WritePayload` public cho collection · `CacheStoredPayload` explicit |
| `MarkedPersistence` | `[AttributeUsage(AttributeTargets.Field)]` |
| `BasePersistenceDataCollection` | `abstract`, `ScriptableObject` · `protected virtual void ResetDerivedState()` · `protected virtual void OnEntriesLoaded()` · nút `ValidateKeys` · `.TimeService.cs`: `protected internal PersistenceDataEntry<long> lastSeenUtcSeconds` + `LastSeenUtcSeconds` — entry duy nhất base tự khai, cho `TimeService` (`TimeSystem.md`); `protected internal` để `TimeService` cùng assembly với tới mà scan type con vẫn thấy |

Namespace `Horcrux.Runtime.Implementations.Persistence`: `SaveDriver` · `SaveBootstep` · `SaveBootstepInitializer`
(internal) — mục "Hai host".

## Cấu trúc file

```
Runtime/Abstractions/Foundations/Persistence/
├── IPersistenceDataEntry.cs · IPersistenceDataCollection.cs   interface thuần
├── PersistenceDataEntry.cs · .Editor.cs                       giá trị, payload kho giữ, event, JSON · ImportPayload
├── BasePersistenceDataCollection.cs                           scan, load, autosave, flush hai giai đoạn
├── BasePersistenceDataCollection.Editor.cs                    ValidateKeys + kiểm kiểu payload
└── BasePersistenceDataCollection.TimeService.cs               lastSeenUtcSeconds
Runtime/Implementations/Foundations/Persistence/SaveDriver.cs
Runtime/Implementations/Composites/SaveBootstep.cs · SaveBootstepInitializer.cs
Tests/EditMode/PersistenceSeedingTests.cs
docs/Runtime/Persistence/Persistence.md · Persistence.html
```

## Còn để mở

- **Hệ SDK lấy giá trị save bằng đường nào — chưa chốt.** Module nhận `PersistenceDataEntry<T>` qua Init (compile-depend
  vào Persistence), hoặc module phơi field thường và dự án glue một chiều qua `OnValueChanged` (`HapticSystem.md`,
  `AudioSystem.md` đang đi đường này). Chốt thì **sửa dòng này**.
- **Hệ sẽ dùng:** Audio, Haptics, Economy, Rating, LiveOps — dự án khai entry rồi nối. Ngoại lệ duy nhất base tự khai:
  `lastSeenUtcSeconds`, vì Time cần mốc chống lùi giờ ở mọi dự án.
- **Rẻ, thêm không sửa cũ:** nút xoá từng entry · kho file khi chạm giới hạn PlayerPrefs · migration version · cloud sync
  · crypto decorator quanh serializer.
- **`ValidateKeys` kiểm luật 2:** serialize `CloneDefault()` hai lần rồi so. Bắt `HashSet`/`Dictionary` có phần tử
  trong `Default Value`; không bắt container rỗng.
- **Remote Config nên học lại:** `RemoteConfigs` thành `IReadOnlyList<>`; cache `PlayerPrefs` có tiền tố riêng.
