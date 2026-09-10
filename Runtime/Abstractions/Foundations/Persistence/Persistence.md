# Persistence System

Mỗi thứ lưu được là một `PersistenceDataEntry<T>`: một khoá, một giá trị mặc định, giá trị đang chạy và
cờ dirty. Mọi entry là field của **một** `ScriptableObject` dẫn xuất `BasePersistenceDataCollection`;
collection lo quét field, kiểm khoá, load lúc khởi động, autosave theo chu kỳ, và là **chỗ duy nhất**
gọi `PlayerPrefs.SetString` / `PlayerPrefs.Save()`.

SDK giữ máy móc, dự án khai entry **trong assembly của chính nó** — nên không model save nào bị biên
dịch vào `com.horcrux.runtime`, và không cần `.asmref`.

**Bảo đảm của hệ: mất tối đa MỘT chu kỳ autosave.** Swipe-kill trên Android và việc hệ thu hồi RAM đều
**không** chạy `OnApplicationQuit`; tín hiệu tin được cuối cùng là lúc app vào background. Nên autosave
chu kỳ là lưới đỡ chính, flush lúc vào background là chốt sổ, flush lúc quit là thêm được thì tốt. Đây
là toàn bộ hợp đồng — hệ không hứa "không mất gì".

## Hai phía

Phụ thuộc một chiều: dự án tham chiếu SDK, SDK không biết gì về dự án.

```
SDK — com.horcrux.runtime                  Dự án — assembly của game
───────────────────────────────            ──────────────────────────────────────────
IPersistenceDataEntry                      IGameSave
IPersistenceDataCollection                   : IPersistenceDataCollection
  interface thuần                            , IService<IGameSave>
  KHÔNG derive IService<>                    → 1 property có kiểu cho mỗi entry

PersistenceDataEntry<T>                    GameSave
  [Serializable] · key · defaultValue        : BasePersistenceDataCollection, IGameSave
  · value · isDirty · OnValueChanged           [Service(typeof(IGameSave),
  · nút ImportPayload                                    ResourcePath = "Config/…")]
                                             [CreateAssetMenu]
BasePersistenceDataCollection                → 1 field [MarkedPersistence] cho mỗi entry
  abstract ScriptableObject
  quét field · LoadAll · autosave            model save — class dữ liệu thuần
  · flush 2 giai đoạn · nút ValidateKeys     Resources/<ResourcePath>.asset

MarkedPersistence (attribute)              host: kéo asset vào SaveDriver
SaveDriver · SaveBootstep (hai host)             hoặc SaveBootstep, trong scene
```

Bốn thứ SDK **không** khai hộ được, luôn nằm phía dự án: `[Service]`, `[CreateAssetMenu]`, đường dẫn
Resources, và **host** — chỉ dự án biết object nào sống suốt phiên.

## Đường đi của một giá trị

```
authoring          runtime đọc/ghi                     lưu
─────────          ───────────────                     ───
key (string)   ┐
defaultValue   ├─► Initialize()                        (một lần, do host gọi)
               │     ScanEntries    reflection quét field [MarkedPersistence]
               │     Setup(this)    value = CloneDefault(), dirty = false, listener = null
               │     ResetDerivedState()                ← hook, dự án override
               │     LoadAll()      PlayerPrefs.GetString("persistence_" + key) ─► ReadPayload
               │     OnEntriesLoaded()                  ← hook, dự án override
               │
game code      ├─► entry.Value = x  ─► MarkDirty()  ─► OnValueChanged
               │   (đọc: entry.Value, hoặc implicit operator T)
               │
autosave/pause └─► FlushAll()
                     ① mọi entry dirty: WritePayload() ─► PlayerPrefs.SetString   (vẫn ở RAM)
                     ② PlayerPrefs.Save() một lần  ─►  đĩa  ─►  rồi mới ClearDirty()
```

Serialize thuộc nhịp **flush**, không thuộc nhịp đổi giá trị: gán `Value` chỉ bật cờ và phát event.
Reflection quét field có cache một lần mỗi phiên. Không có gì chạy theo frame.

## Ba giới hạn của PlayerPrefs

`PlayerPrefs` là **một file** do nền tảng ghi hộ: Android `SharedPreferences` (XML trong
`/data/data/<package>/shared_prefs/`), iOS `NSUserDefaults`, Editor Windows là registry
`HKCU\Software\<Company>\<Product>`. Cả file được parse **một lần** rồi giữ trong RAM.

| Giới hạn | Nghĩa | Nhận ra khi |
|---|---|---|
| Cả kho là **một khối** | một lần hỏng là mất **mọi** khoá của game, kể cả của hệ không liên quan | ràng buộc thường trực |
| Mỗi `Save()` ghi lại **toàn bộ** kho | càng nhiều dữ liệu thì mỗi chu kỳ càng đắt. Cũng là lý do `Flush(entry)` **không** rẻ hơn `FlushAll()` ở phần chạm đĩa | tổng payload vượt **vài chục KB** |
| Cả kho nằm trong RAM suốt phiên | phí bộ nhớ thường trực cho thứ chỉ đọc một lần lúc boot | cùng ngưỡng |

Chạm một trong ba thì đổi sang kho file trên đĩa. Việc đó sửa **nội bộ** `BasePersistenceDataCollection`
— entry chỉ nói bằng chuỗi payload, nên không đụng entry và không đụng code game.

`PlayerPrefs` cũng là **không gian khoá phẳng, dùng chung** với Firebase, ads, analytics và cache của
`RemoteConfig<T>` (ghi bằng key thô, không tiền tố). Tiền tố `"persistence_"` là thứ duy nhất chặn một
khoá SDK trùng tên đè lên tiến độ người chơi.

## Hợp đồng payload — `T` phải là dữ liệu thuần

`WritePayload` / `ReadPayload` / `CloneDefault` dùng `JsonConvert` **không settings**. Newtonsoft đọc cả
public **field** lẫn public **property có getter** — nên nó chạm tới property tính toán của struct toán
Unity, thứ mà serializer của Unity không bao giờ chạm. `Vector3.normalized` trả về `Vector3`, và
`normalized.normalized` bằng chính nó; Newtonsoft phát hiện vòng lặp bằng `Equals` trên giá trị boxed
nên với struct thì **bắt được** — `ReferenceLoopHandling` mặc định là `Error`, và nó ném.

Đo thật (Newtonsoft của project + `UnityEngine.CoreModule`, CoreCLR 6.0.21):

| `T` | `JsonConvert.SerializeObject` |
|---|---|
| `Vector3`, `Vector2` | `JsonSerializationException: Self referencing loop detected for property 'normalized'` |
| `Quaternion`, `Color` | cùng cái bẫy property tính toán (`normalized`, `linear`) — chưa đo được ngoài Unity vì chúng gọi native |
| `Vector2Int` | **không ném**, nhưng payload mang rác: `{"x":3,"y":4,"magnitude":5.0,"sqrMagnitude":25}` |
| `int`, `bool`, `string`, class/struct tự khai | sạch |

**Luật: `T` không chứa kiểu nào của `UnityEngine`** — struct toán (`Vector2/3/4`, `Vector2Int`,
`Quaternion`, `Color`, `Rect`, `Bounds`, `Matrix4x4`…) lẫn `Object` của engine (`GameObject`,
`Transform`, `AnimationCurve`…) — kể cả khi nó nằm sâu trong một field hoặc property của model. Cần lưu
vị trí thì khai struct của mình:

```csharp
[Serializable]
public struct SavedPosition
{
    public float x, y, z;
}
```

Đắt hơn một dòng, nhưng đúng hai chuyện: payload không mang thứ suy diễn lại được, và **hình dạng save
là hợp đồng của dự án**, không phải hình dạng một kiểu của engine.

**Lỗi này lộ ra ở đâu.** Struct nằm ngay trong `Default Value` thì `CloneDefault()` ném ở `Initialize()`
— **mọi** lần Play, không phụ thuộc cấu hình, thấy đỏ ngay trong Editor. Nhưng struct nằm trong một
container **rỗng lúc mặc định** (`List<Vector3>` chưa có phần tử) thì `Initialize()` qua sạch, và nó chỉ
ném ở **lần flush đầu sau khi runtime nhồi dữ liệu vào** — chỗ đó có try/catch từng entry nên `LogError`
nêu đúng khoá và entry khác vẫn lưu bình thường, còn entry này thì không bao giờ lưu nữa. Đó là ca duy
nhất của luật này lọt được qua Editor xuống build.

**Kiểm trước khi build.** Nút `ValidateKeys` trên asset kiểm luôn hợp đồng này: nó duyệt `T` cùng mọi
field và property public lồng bên trong, rồi in ra đường đi tới kiểu engine đầu tiên gặp được — ví dụ
`Entry 'route' stores SavedRoute.lastCheckpoint → Vector3`. Kiểm theo **kiểu**, không theo giá trị, nên
một `List<Vector3>` rỗng vẫn bị bắt — đúng cái ca vừa nói ở trên. Field `[JsonIgnore]` và
`[NonSerialized]` được bỏ qua, vì Newtonsoft cũng bỏ qua.

Chỗ nút này **không** thấy: field khai kiểu `object`, `dynamic`, hoặc một interface / base class mà chỉ
giá trị runtime mới là kiểu engine — kiểu tĩnh không nói ra được điều đó. Đừng khai save model bằng
`object`.

## Cách dùng

Hệ này **không tự setup hộ**: không tự tạo GameObject, không tự tra tìm asset, không guard cho ô người
dựng phải điền. Bù lại, dựng lại từ 0 chỉ cần đúng sáu bước dưới, theo thứ tự.

### 1. Interface của dự án (một lần cho cả dự án)

```csharp
// IGameSave.cs — assembly của dự án, KHÔNG nằm dưới Horcrux.*
public partial interface IGameSave : IPersistenceDataCollection, IService<IGameSave>
{
    PersistenceDataEntry<int> Coin { get; }
    PersistenceDataEntry<PlayerProgress> Progress { get; }
}
```

`IPersistenceDataCollection` **không** derive `IService<>` (xem bảng Bẫy) — chỉ interface của dự án
derive `IService<chính mình>`.

### 2. Collection của dự án (một lần cho cả dự án)

```csharp
// GameSave.cs
[Service(typeof(IGameSave), ResourcePath = RESOURCE_PATH)]
[CreateAssetMenu(fileName = "GameSave", menuName = "Configs/GameSave")]
public sealed partial class GameSave : BasePersistenceDataCollection, IGameSave
{
    // Must match where the asset sits under a Resources folder, or the service never resolves.
    private const string RESOURCE_PATH = "Config/GameSave";
}
```

`partial` để chia entry theo nhóm ra nhiều file (`GameSave.Economy.cs`, `.Progress.cs`…) — mỗi nhóm một
cặp file class + interface, cùng namespace, **cùng assembly**.

Rồi `Create` → menu vừa khai → đặt asset vào `Assets/…/Resources/Config/GameSave.asset`. Đường dẫn
**sau** `Resources/` phải khớp `RESOURCE_PATH`, sai là `Service` ném exception ở lần chạm đầu tiên.

### 3. Thêm một entry — hai chỗ, luôn đi cùng nhau

```csharp
// interface: hợp đồng mà call site đọc
public partial interface IGameSave
{
    PersistenceDataEntry<int> Coin { get; }
}
```

```csharp
// class: chỗ Unity serialize và Inspector vẽ
public partial class GameSave
{
    [Splitter("Economy")]                     // tùy chọn — kẻ tiêu đề nhóm trong Inspector
    [MarkedPersistence]
    [SerializeField] private PersistenceDataEntry<int> coin;

    public PersistenceDataEntry<int> Coin => coin;
}
```

Field để `private`: `public` là cho mọi caller gán được cả tham chiếu entry, việc chỉ authoring được
làm — và scan chỉ quét `NonPublic`, thêm `Public` là mở cửa im lặng cho hình dạng convention cấm.

Rồi điền trong Inspector: `Key` (chuỗi tự đặt, **không** suy từ tên field) và `Default Value`.

Một feature cần đọc nhiều entry riêng thì tách **interface**, không tách asset:
`interface IEconomySave { PersistenceDataEntry<int> Coin { get; } }`, rồi
`IGameSave : IPersistenceDataCollection, IEconomySave, IService<IGameSave>`. ⚠️ `IEconomySave` **không**
derive `IService<>`. Muốn tra riêng thì thêm
`[Service(typeof(IEconomySave), ResourcePath = RESOURCE_PATH)]` và đọc qua `IService<IEconomySave>.Service`
— cùng `ResourcePath` vẫn ra **một** instance vì `Resources.Load` trả cùng asset.

### 4. Chọn đúng MỘT host và wire asset vào nó

Xem bảng "Hai host" dưới để chọn. Trong Inspector của host: mục **Init** → **Add Initializer** (hoặc
chuột phải header component → **Generate Initializer**) → kéo asset collection vào ô argument.

Host là chỗ gọi `Initialize()` và chỗ giữ token cho vòng autosave. SDK **không** tự gọi.

### 5. Đọc và ghi

```csharp
int coin = IGameSave.Service.Coin;                    // implicit operator T
int same = IGameSave.Service.Coin.Value;              // tường minh

IGameSave.Service.Coin.Value = coin + 10;             // set → MarkDirty → OnValueChanged

PlayerProgress p = IGameSave.Service.Progress.Value;  // mutate model tại chỗ
p.coins += 5;
IGameSave.Service.Progress.MarkDirty();               // ⚠️ BẮT BUỘC, xem bảng Bẫy

IGameSave.Service.Coin.FlushNow();                    // chốt sổ sớm một entry
IGameSave.Service.FlushAll();                         // chốt sổ sớm cả kho
```

Subscribe `OnValueChanged` **sau** khi `Initialize()` đã chạy — `Setup()` gán listener về null, nên đăng
ký trước đó là mất im lặng (bảng Bẫy).

### 6. Hai hook tùy chọn cho state của subclass

```csharp
protected override void ResetDerivedState() { /* chạy TRƯỚC LoadAll — xoá cache của phiên trước */ }
protected override void OnEntriesLoaded()  { /* chạy SAU LoadAll — dựng lookup, parse tiếp… */ }
```

Cần cả hai: chỉ có hook "sau" thì một lượt load không chạm entry nào (lần chạy đầu, chưa có save) vẫn để
cache của phiên trước sống.

### Thiếu bước nào thì hỏng ở đâu

| Bỏ qua | Hỏng ở đâu |
|---|---|
| Khai collection trong Horcrux thay vì assembly dự án | kéo type của dự án vào SDK — submodule không commit được một mình |
| `[MarkedPersistence]` trên field entry | entry **không tồn tại** với hệ: đọc/ghi vẫn chạy trong RAM, không bao giờ xuống đĩa, **không một dòng log** |
| Khai field entry ở class base thay vì class dẫn xuất | reflection quét `GetType()` với `NonPublic` + `Instance` — **không thấy** private field của base. Muốn khai ở base thì phải `protected` |
| `Key` để rỗng | `ScanEntries` loại entry đó kèm `LogError`; nó không lưu gì |
| `Default Value` để rỗng | người chơi mới bắt đầu bằng giá trị mặc định của kiểu (`0`, `false`, list rỗng) — mà ô trống **không phân biệt được** với ô cố ý điền số đó, nên không có gì báo |
| Không wire asset vào host | `saveCollection` là null → `NullReferenceException` ở `Initialize()`. Ở `SaveDriver` thì đỏ console và `Start()` dừng; ở `SaveBootstep` thì `BootstrapRunner` **fail-open** — đỏ một dòng rồi cả phiên chạy tiếp **không có** persistence |
| Đặt `SaveDriver` trên object không sống suốt phiên | scene chứa nó unload là `destroyCancellationToken` hủy → autosave **chết im lặng**, UniTask coi hủy là bình thường nên không log gì. Chỉ còn flush lúc pause/quit |
| Không bấm `ValidateKeys` trước khi Play | khoá trùng, khoá rỗng, field mark mà chưa gán, kiểu payload chạm `UnityEngine` — cả bốn chỉ báo lúc `Initialize()` chạy, hoặc muộn hơn nữa |

## Hai host — chọn một, không dùng cả hai

| | `SaveDriver` | `SaveBootstep` |
|---|---|---|
| Dùng khi | dự án **không** có Bootstrap | dự án có `BootstrapRunner` |
| Là gì | `MonoBehaviour<BasePersistenceDataCollection>` kéo tay vào scene | `BaseBootStep`, chạy trong pha boot |
| Gọi `Initialize()` | `Start()` | `InitializeAsync()` |
| Chạy autosave | `RunAutosaveAsync(destroyCancellationToken)` | `RunAutosaveAsync(destroyCancellationToken)` — **không** phải token của pha, runner refresh token đó mỗi lần load level |
| Flush | cạnh vào background (`OnApplicationFocus` / `OnApplicationPause`, một lần cho mỗi lần vào) + `OnApplicationQuit` | `OnGoToBackground(true)` + `OnAppQuit` |
| Thứ tự với hệ khác | **không có thứ tự** — Unity không hứa thứ tự magic method giữa các MonoBehaviour, nên flush có thể chạy trước một hệ khác kịp ghi dữ liệu trong pause hook của nó | **có thứ tự** — runner fan-out pause/quit **ngược** thứ tự step, nên step khởi tạo sớm được flush muộn |

Cả hai gánh **đủ** trách nhiệm, nên chạy cả hai không hỏng dữ liệu (`Initialize()` idempotent, lượt flush
thứ hai không thấy gì dirty nên không chạm đĩa) — nhưng là hai nhịp autosave chồng nhau, không có lý do
để làm vậy.

Vì sao thân vòng lặp và `autosaveIntervalSeconds` nằm ở **collection** chứ không ở host: cả hai host cần
đúng một nhịp đó, mà bảo đảm "mất tối đa một chu kỳ" là bảo đảm của collection, còn con số nằm cạnh dữ
liệu nó chi phối thì đọc một chỗ ra hết. Còn **token** ở lại host, vì host là thứ có đời sống — một
`ScriptableObject` không có mốc kết thúc tin được, nên nó không được tự mở `CancellationTokenSource`:
không ai đóng, và ở Editor tắt domain reload thì mỗi lần Play là một loop nữa xếp lên loop cũ.

## Vòng đời

| Lúc | Chuyện gì xảy ra |
|---|---|
| `Initialize()` | `ScanEntries` (quét field, kiểm khoá) → `Setup(this)` từng entry: `value = CloneDefault()`, `isDirty = false`, **`OnValueChanged = null`** → `ResetDerivedState()` → `LoadAll()` → `OnEntriesLoaded()` |
| `Initialize()` lần hai | thoát ngay ở dòng đầu — **idempotent**: không load lại, không xoá listener đã đăng ký |
| `entry.Value = x` hoặc `MarkDirty()` | bật `isDirty`, phát `OnValueChanged` (mỗi callback một try/catch riêng — một handler ném không kéo theo handler khác). Không serialize, không chạm `PlayerPrefs` |
| Mỗi `autosaveIntervalSeconds` | `FlushAll()`. Chạy bằng `DelayType.Realtime` nên vẫn tick khi `timeScale = 0` — chủ ý: người chơi ngồi ở popup pause vẫn được lưu |
| Vào background · quit · `FlushNow()` · `FlushAll()` | flush hai giai đoạn: `SetString` mọi entry dirty → **một** `PlayerPrefs.Save()` → rồi mới `ClearDirty()`. Không entry nào dirty thì giai đoạn hai **không chạy** |
| `LoadAll` | mỗi entry: không có khoá trong `PlayerPrefs`, hoặc payload rỗng → **giữ mặc định** (đúng, không phải lỗi). Payload hỏng → `LogError` nêu đúng khoá, giữ mặc định, entry khác không bị kéo theo, flush kế ghi đè bằng dữ liệu lành |
| `FlushAll()` **trước** `Initialize()` | `entries` còn rỗng → không ghi gì. Quên `Initialize()` **không** ghi mặc định đè lên save thật; cái mất là dữ liệu chưa được load và game chạy trên default trong RAM |

Không có đường reload giữa phiên: `LoadAll` chỉ chạy trong `Initialize()`. Sửa `PlayerPrefs` bằng tay lúc
đang Play thì phải Play lại. Xem/sửa/xoá `PlayerPrefs` bằng tool `Assets/Horcrux/Editor/PlayerPrefsEditor`.

## Inspector

Mỗi `PersistenceDataEntry<T>` vẽ ra:

| Field | Ghi vào `.asset` | Nghĩa |
|---|---|---|
| `key` | có | khoá logic. Khoá thật trên đĩa là `"persistence_" + key` |
| `defaultValue` | có | giá trị của người chơi mới. Được **sao chép** vào `value` lúc `Setup`, không dùng thẳng |
| `value` | **không** | giá trị đang chạy. `[ShowInInspector, ReadOnly]` — xem được, sửa tay không được |
| `isDirty` | **không** | `true` = có thay đổi chưa xuống đĩa |
| `payloadToImport` | **không** | ô dán JSON cho nút `ImportPayload`, chỉ tồn tại trong Editor |

Trên chính asset collection: `autosaveIntervalSeconds` — mặc định 10s, chặn dưới `[Min(5f)]`.

`value` và `isDirty` cố ý **không** serialize: tiến độ người chơi mà ghi vào asset là nó vào git. Đây là
chỗ Persistence cố ý làm ngược `RemoteConfig<T>` (bảng cuối mục Bẫy).

## Nút Editor

| Nút | Ở đâu | Làm gì |
|---|---|---|
| `ImportPayload` | mỗi entry | đọc `payloadToImport` như JSON rồi gán qua **setter** (nên có `MarkDirty` + `OnValueChanged`). Chỉ chạy trong Play Mode; payload rỗng hoặc parse fail thì báo và không đổi gì |
| `ValidateKeys` | collection | khoá trùng · khoá rỗng · field mark mà chưa gán hoặc sai kiểu · kiểu payload chạm `UnityEngine`. Sạch thì log một dòng kèm số entry |

`ValidateKeys` khai ở `BasePersistenceDataCollection` nhưng đọc field qua reflection, nên chạy đúng trên
mọi class dẫn xuất. Cả hai nút nằm trong `#if UNITY_EDITOR`.

Cố ý **không** có nút xoá save theo tiền tố: `PlayerPrefs` không có API liệt kê khoá, nên chỉ xoá được
khoá đang khai — khoá mồ côi của bản build cũ sống sót, mà một nút xoá "gần đúng" thì tệ hơn không có.

## Bất biến

| Bất biến | Vì sao, và nó nằm ở đâu |
|---|---|
| **Dirty chỉ tắt sau khi storage nhận** | `FlushInternal` hai giai đoạn: ghi hết payload → `PlayerPrefs.Save()` một lần → **rồi mới** `ClearDirty()`. Tắt trước là mất tiến độ mà không có gì cho thấy: `SetString` chỉ đụng bản RAM của `PlayerPrefs`, `Save()` mới là thứ chạm đĩa |
| **`ClearDirty` không gọi được từ game code** | explicit interface implementation trên `PersistenceDataEntry<T>` — chỉ collection với tay tới, và chỉ sau `Save()` |
| **`SetString` / `Save()` chỉ xuất hiện ở một chỗ runtime** | `FlushInternal`. Cửa ghi thứ hai là một đường đi ra ngoài cờ dirty và ngoài bảo đảm của hệ |
| **Một `PlayerPrefs.Save()` cho cả lượt** | trên Android nó là `SharedPreferences.commit()`: ghi **cả kho**, **đồng bộ trên main thread**. Vì vậy chu kỳ autosave có `[Min(5f)]`, và vì vậy giai đoạn hai chỉ chạy khi có entry vừa ghi |
| **Khoá là wire format** | khoá thật trên đĩa là `"persistence_" + Key`, và định dạng payload là JSON. Đổi `Key`, đổi `KeyPrefix`, hay đổi hình dạng model sau khi ship = mọi save đã có thành mồ côi, người chơi mất tiến độ, **không có gì báo**. Không đổi theo bất kỳ lần refactor tên nào |
| **Không có đường không-làm-gì âm thầm** | khoá trùng, khoá rỗng, field mark mà chưa gán hoặc sai kiểu, đọc payload hỏng, ghi payload hỏng, `Flush` một entry không thuộc collection — mỗi ca một `LogError` nêu đúng khoá hoặc tên field |

## Bẫy và quyết định thiết kế

Đọc mục này trước khi "sửa cho gọn" — mỗi dòng là một chỗ đã sai hoặc chắc chắn sẽ sai.

| Chỗ | Sự thật |
|---|---|
| **Mutate model tại chỗ mà không `MarkDirty()` thì không lưu gì** | `entry.Value = x` tự mark; nhưng `entry.Value.coins += 5` trên một model class thì entry không biết. `FlushNow()` cũng **không** cứu được: nó bỏ qua entry không dirty, cố tình — dirty-tracking không có ngoại lệ nào |
| **Listener đăng ký trước `Initialize()` bị xoá im lặng** | `Setup()` gán `OnValueChanged = null`, vì `ScriptableObject` sống qua các lần Play khi tắt domain reload: listener của phiên trước trỏ vào GameObject đã huỷ → `MissingReferenceException` ở lần đổi giá trị đầu tiên. Không detector nào phân biệt được listener phiên trước (phải xoá) với listener vừa đăng ký (bị xoá oan) — cả hai chỉ là `OnValueChanged != null`. Chặn duy nhất bằng **cấu trúc**: gọi `Initialize()` trong pha boot, mọi subscribe đứng sau nó |
| **Ba thứ nữa sống qua các lần Play** | `value` và `isDirty` — cả hai được `Setup()` reset. Thứ ba là **cache mà subclass dựng từ entry** (lookup, `HashSet`, cờ "đã parse"): entry không với tới được, nên có cặp hook `ResetDerivedState()` / `OnEntriesLoaded()`. *Đã sai một lần, ở Remote Config cùng khuôn nhưng thiếu hook "trước":* một cờ chỉ được bật, không ai tắt, mang trạng thái phiên trước sang phiên sau; một hệ khác phải viết workaround. Một cờ không reset được đã mất tư cách làm điều kiện chờ |
| **Quên `Initialize()` có giá bất đối xứng** | Remote Config quên gọi thì rơi về giá trị author — sai nhưng **hồi được**, lần fetch sau đúng. Save quên gọi thì entry mang mặc định và mọi thứ người chơi làm nằm trong RAM rồi mất — **không hồi được**. Bảo đảm mà giá phá là không hồi được thì không đứng trên trí nhớ người viết dòng wire: nên `Initialize()` là **idempotent** và host gọi nó trong pha boot, chỗ lỗi lộ ra sớm nhất |
| **Reset dirty trước khi ghi** | *đã sai một lần, `PlayerSaveLoadService.Save()` của color-loop:* `if (force \|\| _isDirty) { _isDirty = false; }` rồi thân serialize + ghi nằm **ngoài** `if` → lần nào gọi cũng ghi, và một lần ghi lỗi là mất im lặng vì cờ đã tắt |
| **Serialize theo nhịp đổi giá trị** | *đã sai một lần, `GameDataManager` của color-loop:* mỗi thay đổi bất kỳ field → `LateUpdate` frame đó `JsonUtility.ToJson` cả god-blob 25+ field + `PlayerPrefs.Save()` ngay trong frame. `JsonConvert` phải dồn về flush, và chỉ chạm entry dirty |
| **Đường no-op im lặng** | *đã sai một lần, khung save "sạch" của color-loop:* `AssignService()` không có caller → autosave loop chạy đều mà không lưu gì, **không một dòng log**. Mọi đường không-làm-gì-được phải kêu lên |
| **Deserialize không try/catch** | *đã sai một lần, `PlayerSaveLoadService.Load()` của color-loop:* không bọc `Deserialize` → exception **mỗi lần boot**, save thành brick vĩnh viễn. Payload trong `PlayerPrefs` hỏng được thật: build cũ ghi hình dạng khác, chỉnh tay khi debug, persist đứt nửa chừng |
| **`defaultValue` phải sao chép, không trả thẳng** | nó là object sống trong asset; gán `value = defaultValue` là để game mutate thẳng vào asset. `CloneDefault()` round-trip qua serializer một lần mỗi entry lúc `Initialize` — bản sao đúng cho mọi `T`, kể cả struct chứa `List` |
| **`[SerializeField]` bọc trong `#if UNITY_EDITOR`** | Editor ghi field đó vào asset mà build strip nó đi → đọc asset lệch byte → **crash native** (`Read N bytes but expected M bytes`), không stack trace C#. Muốn hiện trong Inspector mà không serialize thì `[NonSerialized, ShowInInspector]` |
| **`[ReadOnly]` trên `value` là cố ý** | sửa tay bỏ qua setter: không `MarkDirty`, không `OnValueChanged`, flush không ghi, không log — đúng định nghĩa một cửa no-op âm thầm. Cửa sửa tay đúng là nút `ImportPayload`, vì nó đi qua setter |
| **`IPersistenceDataCollection` không được derive `IService<>`** | interface dự án derive cả nó lẫn `IService<chính mình>` sẽ thừa hưởng **hai** thành viên static `Service` → mọi lần đọc `.Service` là **CS0229 ambiguity**, lỗi biên dịch. Trong SDK không chỗ nào resolve collection qua service locator — hai host nhận reference kéo thả |
| **`[Service]` không di truyền** | `ServiceAttribute` khai `Inherited = false`. Khai ở `BasePersistenceDataCollection` là khai vào chỗ không ai đọc: service không đăng ký, `Service.Get<>()` ném ở lần chạm đầu. Nên `[Service]` và `[CreateAssetMenu]` đều thuộc dự án — cũng là lý do base để `abstract`: một collection không có entry nào thì không có việc gì làm |
| **`partial` không băng qua được ranh giới assembly** | mọi phần của một `partial` type phải **cùng assembly**. Nếu nửa dự án đi vào SDK bằng `.asmref` thì type nó gọi tên (`PersistenceDataEntry<PlayerProgress>`) phải tra được từ SDK → `PlayerProgress` và cả chuỗi phụ thuộc bị kéo vào; chiều ngược là circular reference. Vì vậy khuôn là **kế thừa**: subclass là type của dự án, chỉ gọi tên xuống SDK |
| **`implicit operator T` bất đối xứng khi so sánh** | `entry == null` là so **THAM CHIẾU** và an toàn — `PersistenceDataEntry<T>` không khai `operator ==`, literal `null` không có kiểu, nên tập candidate operator rỗng. Nhưng so với toán hạng **có kiểu `T`** thì `T.op_Equality` vào tập và entry **bị convert**: với `PersistenceDataEntry<string>` mà `Value` là null, `entry == null` ra `false` còn `entry == s` (`s` là `string` null) ra `true`. Hai dòng trông như nhau, trả lời ngược nhau |
| **Magic method của Unity trên subclass che bản của base** | subclass đặt trùng tên `OnDestroy` / `OnDisable` là dọn dẹp của base im lặng không chạy. Hôm nay collection **không dùng magic method nào**; nếu thêm thì khai `protected virtual` để compiler nhắc |
| **`Flush(entry)` không rẻ hơn `FlushAll()` ở phần chạm đĩa** | `Save()` ghi cả kho. `Flush(entry)` chỉ tiết kiệm phần serialize, và nó tồn tại để **chốt sổ sớm** một giá trị quan trọng (mua IAP xong), không phải để tối ưu |

### Cố ý không làm

| Không làm | Vì sao |
|---|---|
| `Prefs<T>` riêng cho giá trị lẻ | giá trị lẻ và model chỉ khác **một bước** (biến thành chuỗi). Bộ máy thứ hai đặt giá trị lẻ ra ngoài cờ dirty và ngoài `PlayerPrefs.Save()` — ngoài chính bảo đảm của hệ |
| `ISerializer` · `IPersistenceStore` | mỗi cái đúng một implementation. Ranh giới "entry chỉ nói bằng chuỗi payload" đã đủ để đổi kho sau, tốn 0 dòng |
| `Register()` lúc runtime | đăng ký lúc chạy là gốc của bốn chỗ hở: không liệt kê được, không kiểm trùng được, quên gọi thì im lặng, và không có mốc để load |
| Kiểm hợp lệ trong `OnValidate` | lúc đang thêm entry thì khoá **luôn** rỗng — cảnh báo hiện toàn thời gian vào đúng lúc chưa sửa được. Nên là nút bấm |
| Ghi nguyên tử (`.tmp` + rename) | ứng dụng không tự ghi file; nền tảng ghi cả kho một lượt |
| Crypto · cloud sync · migration version | chưa có nhu cầu thật: không repo nào dùng crypto thật, backend chưa chuẩn chung, chưa model nào đổi schema |

### Ba chỗ cố ý khác Remote Config

Hai hệ dùng chung khuôn: abstract base trong SDK · `sealed partial` + `partial interface` phía dự án ·
attribute + reflection quét field · một dòng `[Service]` dưới interface của dự án · `RESOURCE_PATH` ·
`[Splitter]` · magic method `protected virtual` · base không mang `[CreateAssetMenu]`. Sửa khuôn ở một
bên thì sửa cả bên kia. Danh sách khác biệt là **đóng** — thêm chỗ thứ tư phải ghi lý do vào đây.

| Chỗ khác | Lý do |
|---|---|
| Cặp hook `ResetDerivedState()` / `OnEntriesLoaded()` | Remote Config chỉ có hook "sau", và chính chỗ hở đó sinh ra cờ mang trạng thái phiên trước |
| `value` + `isDirty` **không** serialize, chỉ `defaultValue` serialize | Remote Config serialize giá trị fetch để developer thấy trong asset; Save lưu tiến độ người chơi, thứ không được vào git |
| `[ReadOnly]` trên `value` | `fetched` của Remote Config sửa tay thì vô hại; `value` của Save sửa tay là một cửa no-op âm thầm |

## Chữ ký

**`Horcrux.Runtime.Abstractions.Persistence`**

| Type | Thành viên |
|---|---|
| `IPersistenceDataEntry` | `string Key { get; }` · `bool IsDirty { get; }` · `void Setup(IPersistenceDataCollection)` · `void ReadPayload(string)` · `string WritePayload()` · `void ClearDirty()` |
| `IPersistenceDataCollection` | `IReadOnlyList<IPersistenceDataEntry> Entries { get; }` · `bool IsInitialized { get; }` · `void Initialize()` · `void FlushAll()` · `void Flush(IPersistenceDataEntry)` |
| `PersistenceDataEntry<T> : IPersistenceDataEntry` | `[Serializable]` · `T Value { get; set; }` · `event Action<T> OnValueChanged` · `void MarkDirty()` · `void FlushNow()` · `implicit operator T` (null → `default`) · nút `ImportPayload` |
| `MarkedPersistence : Attribute` | `[AttributeUsage(AttributeTargets.Field)]` — đánh dấu field để scan nhận |
| `BasePersistenceDataCollection : ScriptableObject, IPersistenceDataCollection` | `abstract` · `UniTask RunAutosaveAsync(CancellationToken)` · `protected virtual void ResetDerivedState()` · `protected virtual void OnEntriesLoaded()` · nút `ValidateKeys` |

**`Horcrux.Runtime.Implementations.Persistence`**

| Type | Vai trò |
|---|---|
| `SaveDriver : MonoBehaviour<BasePersistenceDataCollection>` | `Start` init + autosave · flush ở cạnh vào background và `OnApplicationQuit` |
| `SaveBootstep : BaseBootStep, IInitializable<BasePersistenceDataCollection>` | `InitializeAsync` init + autosave · flush ở `OnGoToBackground(true)` và `OnAppQuit` |

**`Horcrux.Runtime.Abstractions`** — `IService<out T>`: `static T Service` · `static bool TryGet(out T)`.

## Cấu trúc file

```
Runtime/Abstractions/Foundations/Persistence/
├── IPersistenceDataEntry.cs                  hợp đồng một entry
├── IPersistenceDataCollection.cs             hợp đồng collection — interface THUẦN, xem mục Bẫy
├── PersistenceDataEntry.cs                   PersistenceDataEntry<T> — giá trị, dirty, event, JSON
├── PersistenceDataEntry.Editor.cs            nút ImportPayload
├── BasePersistenceDataCollection.cs          abstract base + MarkedPersistence — scan, load,
│                                             autosave, flush hai giai đoạn
├── BasePersistenceDataCollection.Editor.cs   nút ValidateKeys + kiểm kiểu payload
└── Persistence.md                            tài liệu này

Runtime/Implementations/Foundations/Persistence/
├── SaveDriver.cs                             host cho dự án không có Bootstrap
└── SaveBootstep.cs                           host trong pha boot
```

Phía dự án: `IGameSave.cs` · `GameSave.cs` (+ file `partial` theo feature) · model save · asset trong
`Resources/<RESOURCE_PATH>.asset`.

## Còn để mở

- **Hệ SDK lấy giá trị save bằng cách nào — chưa chốt.** Hai đường: module **nhận
  `PersistenceDataEntry<T>`** qua Init (module compile-depend vào Persistence, bê sang dự án không có
  Persistence là không biên dịch), hoặc module phơi **field thường** (`IsEnabled`, `IsSfxOn`) và dự án
  đọc save rồi set vào lúc bootstrap, glue một chiều qua `OnValueChanged` (module không phụ thuộc
  Persistence). `HapticSystem.md` và `AudioSystem.md` đang đi đường thứ hai và nói rõ *"SDK cố tình
  không sở hữu hệ save"*. Chốt đường nào thì **sửa dòng này**, không thêm dòng thứ hai nói ngược lại.
- **Hệ sẽ dùng tiếp:** Audio (volume), Haptics, Economy (coin, lives), Rating, LiveOps. Không hệ nào tự
  khai entry — dự án khai trên collection của mình rồi nối vào.
- **`SaveBootstep` đang nằm ở `Implementations/Foundations/`** dù nó phụ thuộc Bootstrap; chỗ theo quy
  ước là `Implementations/Composites/`. Chưa dời.
- **Rẻ, thêm không sửa cũ:** nút xoá từng entry · kho file trên đĩa khi chạm một trong ba giới hạn ·
  migration version khi có model đổi schema · cloud sync khi backend chuẩn chung.
- **Hai chỗ Remote Config nên học lại từ Persistence:** `IRemoteConfigCollection.RemoteConfigs` nên là
  `IReadOnlyList<>` thay `IEnumerable<>`; cache `PlayerPrefs` của `RemoteConfig<T>` nên có tiền tố riêng
  (cache bỏ được, chỉ tốn một lượt fetch nguội).
