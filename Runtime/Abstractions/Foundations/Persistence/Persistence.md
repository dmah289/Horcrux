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

| SDK giữ | Dự án khai |
|---|---|
| `IPersistenceDataEntry` · `IPersistenceDataCollection` — interface thuần, **không** derive `IService<>` | `IGamePersistenceDataCollection : IPersistenceDataCollection, IService<chính mình>` — một property có kiểu cho mỗi entry |
| `PersistenceDataEntry<T>` · `BasePersistenceDataCollection` · `MarkedPersistence` | `GamePersistenceDataCollection : BasePersistenceDataCollection` — một field `[MarkedPersistence]` cho mỗi entry, cùng model save là class dữ liệu thuần |
| `SaveDriver` · `SaveBootstep` — hai host | `[Service]`, `[CreateAssetMenu]`, đường dẫn Resources, và **chọn host nào** — bốn thứ SDK không khai hộ được, vì chỉ dự án biết object nào sống suốt phiên |

## Ba nhịp

```
authoring   ─► key · defaultValue điền trong Inspector của asset

boot        ─► Initialize()   một lần, do host gọi   (kịch bản 1 và 2)
                 scan field ─► Setup ─► LoadAllEntries ─► seed nếu thiếu khoá

đọc/ghi     ─► entry.Value = x ─► MarkDirty() ─► OnValueChanged      (không chạm đĩa)
               đọc: entry.Value, hoặc implicit operator T

flush       ─► autosave mỗi autosaveIntervalSeconds · cạnh vào background · quit · FlushNow
                 ① entry dirty: WritePayload() ─► PlayerPrefs.SetString    (vẫn ở RAM)
                 ② PlayerPrefs.Save() MỘT lần ─► đĩa ─► rồi mới ClearDirty()
```

Serialize thuộc nhịp **flush**, không thuộc nhịp đổi giá trị. Reflection quét field có cache một lần
mỗi phiên. Không có gì chạy theo frame.

## Bảy kịch bản

Bảy đường mà giá trị thật sự đi qua, theo thứ tự thời gian. Mọi thứ ngoài bảy cái này nằm ở "Ca biên".

### 1. Cài lần đầu — không có gì trên đĩa

1. Host gọi `Initialize()`. `ScanEntries` gom mọi field `[MarkedPersistence]`; `Setup` từng entry:
   `value = CloneDefault()`, dirty tắt, listener về null.
2. `LoadAllEntries` hỏi `PlayerPrefs.HasKey("persistence_" + key)` → **không khoá nào có** → mỗi entry
   `MarkDirty()` và đếm là `Seeded`.
3. `IsInitialized = true`; vì `seededCount > 0` nên một dòng `LogWarning` *"N/M entries had no stored
   key"* rồi `FlushInternal(null)`: mọi entry dirty → `SetString` → **một** `Save()` → `ClearDirty`.

Mọi khoá có mặt trên đĩa với đúng `Default Value` **trước khi** người chơi chạm vào gì. Giá là một
`PlayerPrefs.Save()` thêm ở boot, chỉ ở lần cài đầu và ở bản update có entry mới.

### 2. Mở lại app — save đã có

1. `Initialize()` chạy y hệt tới `LoadAllEntries`.
2. Mỗi entry: `HasKey` đúng, payload không rỗng → `ReadPayload` → `JsonConvert.Deserialize` → gán
   `value`, **tắt dirty**, phát `OnValueChanged`. Kết cục `Loaded`.
3. `seededCount == 0` → `Initialize` thoát, **không** flush. Đĩa không bị chạm.

Lượt `OnValueChanged` ở bước 2 chưa có người nghe: `Setup` vừa xoá sạch listener, và mọi subscribe của
game đứng **sau** `Initialize()`.

### 3. Người chơi ăn coin — đổi giá trị rồi autosave

1. `…Service.Coin.Value = coin + 10` → setter gán `value`, `MarkDirty()` bật cờ và phát
   `OnValueChanged` → view cập nhật ngay. **Không** serialize, **không** chạm `PlayerPrefs`.
2. Vòng `RunAutosaveAsync` chờ `UniTask.Delay(autosaveIntervalSeconds, DelayType.Realtime)` — hết nhịp
   thì `FlushInternal(null)`. `Realtime` là chủ ý: ngồi ở popup pause với `timeScale = 0` vẫn được lưu.
3. Chỉ entry dirty đi qua `WritePayload()` → `SetString`; entry sạch bị bỏ qua.
4. Có ít nhất một entry vừa ghi → `Save()` một lần → rồi mới `ClearDirty()` từng entry.

Chốt sổ sớm (mua IAP xong) thì `Coin.FlushNow()` — chỉ tiết kiệm phần serialize các entry khác, vì
`Save()` luôn ghi cả kho.

### 4. Mutate model tại chỗ — đường duy nhất phải tự mark

```csharp
PlayerProgress p = IGamePersistenceDataCollection.Service.Progress.Value;
p.coins += 5;                                              // entry KHÔNG biết gì
IGamePersistenceDataCollection.Service.Progress.MarkDirty();  // ⚠️ thiếu dòng này là mất trắng
```

Entry chỉ mark khi đi qua **setter**. `Value` trả về tham chiếu model, nên sửa xuyên qua nó là đường
vòng. `FlushNow()` cũng không cứu: `FlushInternal` bỏ qua entry không dirty, cố tình — dirty-tracking
không có ngoại lệ nào.

### 5. Vào background rồi swipe-kill

1. Người chơi vuốt ra home. Unity bắn `OnApplicationPause(true)` **và** `OnApplicationFocus(false)` —
   cả hai, trên cả Android lẫn iOS.
2. Host gộp hai tín hiệu thành một cạnh: `SaveDriver` bằng cờ `inBackground` của nó, `SaveBootstep` bằng
   cờ `isInBackground` của `BootstrapRunner`. Cái tới trước lật cờ, cái thứ hai là no-op.
3. Cạnh vào background → `FlushAll()`. Đây là **chốt sổ**: mọi thứ từ lần autosave gần nhất tới giờ.
4. Swipe-kill trong app switcher. `OnApplicationQuit` **không** chạy — nhưng bước 3 đã lưu rồi.

Tiến trình chết **giữa lúc đang chơi** (hệ thu hồi RAM, crash native) thì không cạnh nào bắn, và thứ mất
là phần thay đổi kể từ lần autosave gần nhất. Đó là toàn bộ khoảng hở mà bảo đảm của hệ thừa nhận.

### 6. Bản update thêm một entry mới

1. `PlayerPrefs` giữ nguyên các khoá cũ.
2. `LoadAllEntries`: entry cũ `Loaded` (giữ tiến độ), entry mới `Seeded`.
3. `LogWarning` *"1/M entries had no stored key"* → `FlushInternal(null)` chỉ ghi entry mới — entry cũ đã
   `ClearDirty` ngay trong `ReadPayload`, nên không bị ghi đè.

Dòng `LogWarning` này ở phiên thứ hai trở đi là **tín hiệu bất thường**: một khoá vừa biến mất khỏi đĩa,
hoặc `Key` vừa bị đổi trong Inspector — xem bất biến "Khoá là wire format".

### 7. Payload hỏng, hoặc ghi payload lỗi

Hai chiều hỏng, xử lý khác nhau:

| Chiều | Chuyện gì xảy ra |
|---|---|
| **Đọc hỏng** — `ReadPayload` ném (build cũ ghi hình dạng khác, chỉnh tay khi debug, persist đứt nửa chừng) | `LogError` nêu đúng khoá + `LogException`, kết cục `Failed`: entry giữ mặc định, **không** `MarkDirty`. Đĩa **không** bị chạm — ghi đè lên payload hỏng là xoá bản sao duy nhất của thứ duy nhất còn đọc ra được nguyên nhân. Entry khác trong cùng lượt không bị kéo theo |
| **Ghi hỏng** — `WritePayload` ném (thường là `T` chạm kiểu `UnityEngine`, xem "Hợp đồng payload") | `LogError` nêu đúng khoá, entry **ở lại dirty và thử lại ở lượt flush sau**, các entry khác trong cùng lượt vẫn `SetString` và vẫn `Save()` bình thường |

Entry ghi hỏng sẽ kêu lại mỗi chu kỳ autosave cho tới khi sửa kiểu — cố ý, vì im lặng ở đây nghĩa là
một entry không bao giờ lưu mà không ai biết.

## Ca biên

| Ca | Kết quả |
|---|---|
| `FlushAll()` **trước** `Initialize()` | `entries` còn rỗng → không ghi gì. Quên `Initialize()` **không** làm mặc định đè lên save thật; cái mất là dữ liệu chưa được load và game chạy trên default trong RAM |
| `FlushNow()` trên entry chưa qua `Setup` (quên `[MarkedPersistence]`, hoặc gọi trước `Initialize()`) | `_owner` null → `LogError` *"No collection owns …"* rồi thoát. Đọc/ghi `Value` trên entry đó vẫn chạy im lặng trong RAM |
| Quit hoặc vào background giữa lúc cold boot chưa xong, với `SaveBootstep` | `BootstrapRunner` chỉ fan-out `OnAppQuit` và `OnGoToBackground` khi `IsInitialized` của **runner** đã bật → **không flush**, và ca quit cũng không reset cờ. Chấp nhận có chủ đích: ca đó chưa có dữ liệu đáng mất. `SaveDriver` không có cổng này |
| `ReadPayload` nhận đúng chuỗi `"null"` | `JsonConvert` trả null → rơi về `CloneDefault()`, nhưng kết cục vẫn là `Loaded` (không `MarkDirty`) → mặc định đó **không** được ghi lại xuống đĩa |
| Entry bị `ScanEntries` loại (khoá rỗng, khoá trùng, field sai kiểu) | mỗi ca một `LogError` nêu tên field. `Initialize()` **bỏ qua** số bị loại mà `ScanEntries` trả về — chỉ nút `ValidateKeys` đọc con số đó và tổng kết |
| Sửa `PlayerPrefs` bằng tay lúc đang Play | không có đường reload giữa phiên: `LoadAllEntries` chỉ chạy trong `Initialize()`. Phải Play lại. Xem/sửa/xoá bằng tool `Assets/Horcrux/Editor/PlayerPrefsEditor` |

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
— **mọi** lần Play, thấy đỏ ngay trong Editor. Nhưng struct nằm trong một container **rỗng lúc mặc
định** (`List<Vector3>` chưa có phần tử) thì `Initialize()` qua sạch, và chỉ ném ở lần flush đầu sau khi
runtime nhồi dữ liệu vào — nhánh "ghi hỏng" của kịch bản 7. Đó là ca duy nhất lọt được xuống build.

**Kiểm trước khi build:** nút `ValidateKeys` duyệt `T` theo **kiểu**, không theo giá trị, nên bắt được cả
`List<Vector3>` rỗng — xem "Nút Editor".

## Cách dùng

Hệ này **không tự setup hộ**: không tự tạo GameObject, không tự tra tìm asset, không guard cho ô người
dựng phải điền. Bù lại, dựng lại từ 0 chỉ cần đúng sáu bước dưới, theo thứ tự. Tên dưới đây là tên thật
của dự án này; dự án khác đổi tên tương ứng, hình dạng giữ nguyên.

### 1. Interface của dự án (một lần cho cả dự án)

```csharp
// IGamePersistenceDataCollection.cs — assembly Runtime.Game, KHÔNG nằm dưới Horcrux.*
namespace Runtime.Game.PersistenceDataCollection
{
    public partial interface IGamePersistenceDataCollection
        : IPersistenceDataCollection, IService<IGamePersistenceDataCollection>
    {
        PersistenceDataEntry<int> Coin { get; }
        PersistenceDataEntry<PlayerProgress> Progress { get; }
    }
}
```

`IPersistenceDataCollection` **không** derive `IService<>` (xem bảng Bẫy) — chỉ interface của dự án
derive `IService<chính mình>`.

### 2. Collection của dự án (một lần cho cả dự án)

```csharp
// GamePersistenceDataCollection.cs
[Service(typeof(IGamePersistenceDataCollection), ResourcePath = RESOURCE_PATH)]
[Service(typeof(BasePersistenceDataCollection),  ResourcePath = RESOURCE_PATH)]
[CreateAssetMenu(fileName = "PersistenceDataCollection", menuName = "Configs/PersistenceDataCollection")]
public sealed partial class GamePersistenceDataCollection
    : BasePersistenceDataCollection, IGamePersistenceDataCollection
{
    // Must match where the asset sits under a Resources folder, or the service never resolves.
    private const string RESOURCE_PATH = "Config/PersistenceDataCollection";
}
```

Hai dòng `[Service]`, hai người đọc khác nhau — cùng `ResourcePath` nên `Resources.Load` trả **một**
instance cho cả hai:

| Dòng | Ai đọc nó |
|---|---|
| `IGamePersistenceDataCollection` | game code, qua `IGamePersistenceDataCollection.Service` |
| `BasePersistenceDataCollection` | `SaveDriver` — `MonoBehaviour<T>` tự nhận service vào `Init` (bước 4) · `TimeService`, để với tới `LastSeenUtcSeconds` |

**`IPersistenceDataCollection` không đăng ký làm service.** Cả hai host đều nhận
`BasePersistenceDataCollection`, nên một dòng `[Service]` cho interface là đăng ký một kiểu **không ai
nhận** — người đọc sau thấy nó rồi tưởng còn một đường lấy collection nữa, và phải mở cả hai host ra mới
biết là không. Kiểu không có người nhận thì không đăng ký.

`partial` để chia entry theo nhóm ra nhiều file (`GamePersistenceDataCollection.Economy.cs`,
`.Progress.cs`…) — mỗi nhóm một cặp file class + interface, cùng namespace, **cùng assembly**.

Rồi `Create` → menu vừa khai → đặt asset vào
`Assets/_TheGame/Runtime/Game/Resources/Config/PersistenceDataCollection.asset`. Đường dẫn **sau**
`Resources/` phải khớp `RESOURCE_PATH`, sai là `Service` ném exception ở lần chạm đầu tiên.

### 3. Thêm một entry — hai chỗ, luôn đi cùng nhau

```csharp
// interface: hợp đồng mà call site đọc
public partial interface IGamePersistenceDataCollection
{
    PersistenceDataEntry<int> Coin { get; }
}
```

```csharp
// class: chỗ Unity serialize và Inspector vẽ
public partial class GamePersistenceDataCollection
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

Một feature cần đọc nhiều entry riêng thì tách **interface**, không tách asset: khai
`IEconomySave { PersistenceDataEntry<int> Coin { get; } }` rồi cho `IGamePersistenceDataCollection`
derive thêm nó. ⚠️ `IEconomySave` **không** derive `IService<>`; muốn tra riêng thì thêm một dòng
`[Service(typeof(IEconomySave), ResourcePath = RESOURCE_PATH)]` và đọc qua `IService<IEconomySave>.Service`.

### 4. Chọn đúng MỘT host và cấp asset cho nó

Xem bảng "Hai host" dưới để chọn. Kéo tay component host vào scene, rồi cấp
`BasePersistenceDataCollection` cho nó bằng **một** trong hai đường:

| Đường | Làm gì | Dùng được cho |
|---|---|---|
| **Service** | không làm gì thêm — collection đã khai `[Service(typeof(BasePersistenceDataCollection), …)]` ở bước 2, InitArgs tự đưa vào `Init` | chỉ `SaveDriver`, vì nó là `MonoBehaviour<T>`. **Đây là đường dự án này đang chạy** |
| **Initializer** | Inspector của host → mục **Init** → **Add Initializer** (hoặc chuột phải header component → **Generate Initializer**) → kéo asset vào ô argument | cả hai host. **Bắt buộc với `SaveBootstep`**: nó kế thừa `BaseBootStep` nên là MonoBehaviour thường, InitArgs chỉ tự inject `IInitializable<T>` cho thứ bản thân là service |

Host là chỗ gọi `Initialize()` và chỗ giữ token cho vòng autosave. SDK **không** tự gọi.

### 5. Đọc và ghi

```csharp
int coin = IGamePersistenceDataCollection.Service.Coin;               // implicit operator T
int same = IGamePersistenceDataCollection.Service.Coin.Value;         // tường minh

IGamePersistenceDataCollection.Service.Coin.Value = coin + 10;        // set → MarkDirty → OnValueChanged

IGamePersistenceDataCollection.Service.Coin.FlushNow();               // chốt sổ sớm một entry
IGamePersistenceDataCollection.Service.FlushAll();                    // chốt sổ sớm cả kho
```

Mutate model tại chỗ thì phải tự `MarkDirty()` — kịch bản 4.

Subscribe `OnValueChanged` **sau** khi `Initialize()` đã chạy — `Setup()` gán listener về null, nên đăng
ký trước đó là mất im lặng (bảng Bẫy).

### 6. Hai hook tùy chọn cho state của subclass

```csharp
protected override void ResetDerivedState() { /* chạy TRƯỚC LoadAllEntries — xoá cache phiên trước */ }
protected override void OnEntriesLoaded()  { /* chạy SAU LoadAllEntries — dựng lookup, parse tiếp… */ }
```

Cần cả hai: chỉ có hook "sau" thì một lượt load không chạm entry nào (lần chạy đầu, chưa có save) vẫn để
cache của phiên trước sống.

### Thiếu bước nào thì hỏng ở đâu

| Bỏ qua | Hỏng ở đâu |
|---|---|
| Khai collection trong Horcrux thay vì assembly dự án | kéo type của dự án vào SDK — submodule không commit được một mình |
| `[MarkedPersistence]` trên field entry | entry **không tồn tại** với hệ: đọc/ghi vẫn chạy trong RAM, không bao giờ xuống đĩa. Im lặng, trừ khi ai đó gọi `FlushNow()` trên nó — lúc đó `_owner` null và có `LogError` |
| Khai field entry ở class base thay vì class dẫn xuất | reflection quét `GetType()` với `NonPublic` + `Instance` — **không thấy** private field của base. Muốn khai ở base thì phải `protected` (xem `lastSeenUtcSeconds`) |
| `Key` để rỗng | `ScanEntries` loại entry đó kèm `LogError`; nó không lưu gì |
| `Default Value` để rỗng | người chơi mới bắt đầu bằng giá trị mặc định của kiểu (`0`, `false`, list rỗng) — mà ô trống **không phân biệt được** với ô cố ý điền số đó, nên không có gì báo. Mặc định đó **xuống đĩa ngay phiên đầu** (kịch bản 1), nên điền sai là điền sai vào save thật |
| Không cấp asset cho host — thiếu cả dòng `[Service(typeof(BasePersistenceDataCollection))]` lẫn Initializer | `saveCollection` là null → `NullReferenceException` ở `Initialize()`. Ở `SaveDriver` thì đỏ console và `Start()` dừng; ở `SaveBootstep` thì `BootstrapRunner` **fail-open** — đỏ một dòng rồi cả phiên chạy tiếp **không có** persistence |
| Đặt host trên object không sống suốt phiên | `destroyCancellationToken` chết theo scene unload → autosave **chết im lặng**, UniTask coi hủy là bình thường nên không log gì, chỉ còn flush lúc pause/quit. `SaveDriver.OnAwake` tự `DontDestroyOnLoad(gameObject)` nên nó đã tự chặn; `SaveBootstep` dựa vào `BootstrapRunner` cũng `DontDestroyOnLoad` |
| Wire **hai** host, hoặc gọi `Initialize()` thêm một lần ở chỗ khác | tiến độ chưa flush về mặc định, listener đã đăng ký bị xoá — **không một dòng log**. Sáu đường sinh ra nó: xem "Hai host" |
| Không bấm `ValidateKeys` trước khi Play | bốn lỗi authoring chỉ báo lúc `Initialize()` chạy, hoặc muộn hơn nữa — xem "Nút Editor" |

## Hai host — **đúng một** trong cả dự án

| | `SaveDriver` | `SaveBootstep` |
|---|---|---|
| Dùng khi | dự án **không** có Bootstrap | dự án có `BootstrapRunner` |
| Là gì | `MonoBehaviour<BasePersistenceDataCollection>` kéo tay vào scene, tự `DontDestroyOnLoad` ở `OnAwake` | `BaseBootStep, IInitializable<BasePersistenceDataCollection>`, chạy trong pha boot |
| Nhận collection | service (tự động) **hoặc** Initializer | **chỉ** Initializer — xem bước 4 |
| Gọi `Initialize()` | `Start()` | `InitializeAsync()` |
| Chạy autosave | `RunAutosaveAsync(destroyCancellationToken)` | `RunAutosaveAsync(destroyCancellationToken)` — **không** phải token của pha, runner refresh token đó mỗi lần load level |
| Flush | cạnh vào background (`OnApplicationFocus` / `OnApplicationPause`, một lần cho mỗi lần vào) + `OnApplicationQuit` | `OnGoToBackground(true)` + `OnAppQuit`, cả hai chỉ chạy **sau khi** runner init xong |
| Thứ tự với hệ khác | **không có thứ tự** — Unity không hứa thứ tự magic method giữa các MonoBehaviour, nên flush có thể chạy trước một hệ khác kịp ghi dữ liệu trong pause hook của nó | **có thứ tự** — runner fan-out pause/quit **ngược** thứ tự step, nên step khởi tạo sớm được flush muộn |

Cả hai gánh **đủ** trách nhiệm, nên chọn một là xong. Nhưng đây **không** phải lời khuyên về gọn gàng:
`Initialize()` **không có guard**, nên host thứ hai là **mất dữ liệu**.

### `Initialize()` lần thứ hai làm gì

Nó chạy lại từ đầu. `ScanEntries` dựng lại danh sách, rồi `Setup()` từng entry:

| Bước của `Setup` | Mất gì |
|---|---|
| `value = CloneDefault()` | mọi thay đổi của người chơi kể từ lần flush cuối — **về mặc định** |
| `isDirty = false` | cờ nói rằng có gì đó cần lưu — **tắt**, nên lượt flush kế không ghi lại |
| `OnValueChanged = null` | mọi listener đã đăng ký sau lần `Initialize` đầu — **xoá sạch**, view không còn nhận đổi giá trị |

Không một dòng log. Không cách nào phát hiện lúc chạy. Và `LoadAllEntries` sau đó đọc lại từ
`PlayerPrefs`, nên trên đĩa vẫn là dữ liệu cũ — nhìn vào save không thấy gì bất thường, chỉ tiến độ
trong RAM biến mất.

Đây là lý do luật này là **tuyệt đối**, không phải sở thích.

### Sáu đường sinh ra lần gọi thứ hai

| Đường | Kiểm bằng |
|---|---|
| Hai `SaveDriver` | tìm component `SaveDriver` trong **mọi** scene và prefab — phải đúng một, hoặc không có |
| Hai `SaveBootstep` | tìm component `SaveBootstep` — phải đúng một, hoặc không có |
| Một `SaveDriver` **và** một `SaveBootstep` | hai lần tìm ở trên, tổng lại phải đúng **một** |
| Cùng một `SaveBootstep` nằm **hai lần** trong list `steps` của `BootstrapRunner` | mở runner trong Inspector, đếm |
| Game code gọi `IPersistenceDataCollection.Initialize()` | grep `.Initialize()` trên kiểu save — chỉ hai host được gọi |
| Prefab mang host, instantiate nhiều lần lúc chạy | host phải là object **kéo tay vào scene**, không bao giờ là prefab spawn |

Không có cờ nào chặn hộ, và **cố ý** không thêm: một cờ `if (isInitialized) return;` che được lần gọi
thừa nhưng che luôn ca thật cần biết — hai host là hai vòng autosave chồng nhau, hai đường flush ở
pause/quit, và một hình dạng wire sai mà không ai sửa. Chặn ở **cấu trúc**: một object, kéo tay, đếm
được trong Editor.

### Dự án này

`GamePersistenceDataCollection` (namespace `Runtime.Game.PersistenceDataCollection`), asset ở
`Assets/_TheGame/Runtime/Game/Resources/Config/PersistenceDataCollection.asset`. Một `SaveDriver` trong
`Start.unity`, **không có Initializer** — nó nhận collection qua dòng
`[Service(typeof(BasePersistenceDataCollection), …)]`. Chưa có `SaveBootstep` và chưa có
`BootstrapRunner` trong scene nào. Ngoài `lastSeenUtcSeconds` mà base khai sẵn, collection chưa khai entry nào.

Đang chuyển sang **một `SaveBootstep` trong `Services.unity`** làm step đầu của `BootstrapRunner` — khi
dời thì `SaveDriver` phải **xoá khỏi `Start.unity`** trong cùng một lần sửa scene, không để lại rồi dọn
sau; và `SaveBootstep` phải có Initializer vì nó không tự nhận service được.

### Chia việc giữa host và collection

**Thân vòng lặp và `autosaveIntervalSeconds` ở collection**: cả hai host cần đúng một nhịp đó, bảo đảm
"mất tối đa một chu kỳ" là bảo đảm của collection, và con số nằm cạnh dữ liệu nó chi phối thì đọc một
chỗ ra hết. **Token ở lại host**, vì host là thứ có đời sống — một `ScriptableObject` không có mốc kết
thúc tin được, nên không được tự mở `CancellationTokenSource`: không ai đóng, và ở Editor tắt domain
reload thì mỗi lần Play là một loop nữa xếp lên loop cũ.

`RunAutosaveAsync` khai trên `IPersistenceDataCollection` và thân nằm ở base — hợp đồng ở interface để
entry và collection nói chuyện với nhau. Nhưng **hai host nhận `BasePersistenceDataCollection`**, không
nhận interface: `TimeService` phải với tới `lastSeenUtcSeconds`, entry mà chỉ base khai, nên dự án dùng
bộ này đằng nào cũng có một collection kế thừa base.

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
| `ValidateKeys` | collection | khoá trùng · khoá rỗng · field mark mà chưa gán hoặc sai kiểu · kiểu payload chạm `UnityEngine`. Sạch thì log một dòng kèm số entry; không sạch thì một dòng đỏ cho mỗi nhóm, kèm tỉ lệ |

`ValidateKeys` khai ở `BasePersistenceDataCollection` nhưng đọc field qua reflection, nên chạy đúng trên
mọi class dẫn xuất; nó dùng danh sách scratch riêng, không đụng `entries` đang chạy. Phần kiểm payload
duyệt `T` cùng mọi field và property public lồng bên trong rồi in đường đi tới kiểu engine đầu tiên gặp
được — `Entry 'route' stores SavedRoute.lastCheckpoint → Vector3`. Nó bỏ qua `[JsonIgnore]` và
`[NonSerialized]`, và không đào vào kiểu dưới namespace `System`, đúng như Newtonsoft. Thứ nó **không**
thấy được: field khai `object`, `dynamic`, hay một interface mà chỉ giá trị runtime mới là kiểu engine —
kiểu tĩnh không nói ra được điều đó, nên đừng khai save model bằng `object`.

Cả hai nút nằm trong `#if UNITY_EDITOR`.

Cố ý **không** có nút xoá save theo tiền tố: `PlayerPrefs` không có API liệt kê khoá, nên chỉ xoá được
khoá đang khai — khoá mồ côi của bản build cũ sống sót, mà một nút xoá "gần đúng" thì tệ hơn không có.

## Kiểm thử

`Assets/Horcrux/Tests/EditMode/PersistenceSeedingTests.cs` — bốn case phủ đúng ba kết cục của
`EntryLoadResult`, chạy trên một `ProbeCollection` khai **một** entry riêng (`probe`) và mang thêm
`lastSeenUtcSeconds` kế thừa từ base. Mọi khẳng định nhắm riêng khoá của `probe`, nên entry kế thừa
không đụng vào ca nào — nhưng `SetUp` vẫn phải điền `Key` cho nó, xem bảng Bẫy:

| Case | Khẳng định |
|---|---|
| Chưa có khoá | khoá xuất hiện trên đĩa mang đúng mặc định, và dirty đã tắt sau khi ghi |
| Khoá có nhưng payload rỗng | y như chưa có khoá |
| Khoá đọc được | giá trị lưu **thắng** mặc định, và đĩa **không** bị chạm |
| Payload hỏng | rơi về mặc định, đĩa **giữ nguyên** chuỗi hỏng, và có đúng một `LogError` nêu khoá |

Test authoring `key` và `defaultValue` qua `SerializedObject` — cùng cửa mà Inspector đi, nên nó kiểm cả
đường serialize của Unity, không chỉ logic C#.

## Bất biến

| Bất biến | Vì sao, và nó nằm ở đâu |
|---|---|
| **Dirty chỉ tắt sau khi storage nhận** | `FlushInternal` hai giai đoạn: ghi hết payload → `PlayerPrefs.Save()` một lần → **rồi mới** `ClearDirty()`. Tắt trước là mất tiến độ mà không có gì cho thấy: `SetString` chỉ đụng bản RAM của `PlayerPrefs`, `Save()` mới là thứ chạm đĩa |
| **`ClearDirty` không gọi được từ game code** | explicit interface implementation trên `PersistenceDataEntry<T>` — chỉ collection với tay tới, và chỉ sau `Save()` |
| **`SetString` / `Save()` chỉ xuất hiện ở một chỗ runtime** | `FlushInternal`. Cửa ghi thứ hai là một đường đi ra ngoài cờ dirty và ngoài bảo đảm của hệ |
| **Khoá có mặt từ phiên đầu, và chỉ ghi khi chưa có** | `LoadAllEntries` seed đúng entry mà storage **không** giữ gì đọc được. Entry đã có khoá thì không bao giờ bị seed, nên `Initialize` không ghi mặc định đè lên save thật được. Payload **hỏng** cũng không bị seed — kịch bản 7 |
| **Một `PlayerPrefs.Save()` cho cả lượt** | trên Android nó là `SharedPreferences.commit()`: ghi **cả kho**, **đồng bộ trên main thread**. Vì vậy chu kỳ autosave có `[Min(5f)]`, và vì vậy giai đoạn hai chỉ chạy khi có entry vừa ghi |
| **Đúng một save host trong cả dự án** | `Initialize()` không có guard, nên lần gọi thứ hai `Setup()` lại mọi entry — im lặng. Chặn bằng cấu trúc, không bằng cờ: xem "Hai host" |
| **Khoá là wire format** | khoá thật trên đĩa là `"persistence_" + Key`, và định dạng payload là JSON. Đổi `Key`, đổi `KeyPrefix`, hay đổi hình dạng model sau khi ship = mọi save đã có thành mồ côi, người chơi mất tiến độ, **không có gì báo**. Không đổi theo bất kỳ lần refactor tên nào |
| **Không có đường không-làm-gì âm thầm** | khoá trùng, khoá rỗng, field mark mà chưa gán hoặc sai kiểu, đọc payload hỏng, ghi payload hỏng, `Flush` một entry không thuộc collection, `FlushNow` trên entry chưa có chủ — mỗi ca một `LogError` nêu đúng khoá hoặc tên field |

## Bẫy và quyết định thiết kế

Đọc mục này trước khi "sửa cho gọn" — mỗi dòng là một chỗ đã sai hoặc chắc chắn sẽ sai.

| Chỗ | Sự thật |
|---|---|
| **Một entry khai ở base là nghĩa vụ điền `Key` cho MỌI collection dẫn xuất** | `ScanEntries` quét cả field kế thừa, nên `lastSeenUtcSeconds` có mặt trên mọi lớp con — kể cả một `ProbeCollection` dựng tạm trong test. Chưa điền `Key` là một `LogError` mỗi lần `Initialize()`, và trong EditMode thì Unity fail luôn test phát log đó dù assertion vẫn đúng. *Đã sai một lần:* thêm field vào base xong, bốn ca `PersistenceSeedingTests` đỏ hết ở dòng log chứ không ở khẳng định nào. Đây là bất biến "không có đường không-làm-gì âm thầm" chạy đúng, không phải lỗi — cái phải sửa là chỗ dựng collection, không phải cái guard |
| **Mutate model tại chỗ mà không `MarkDirty()` thì không lưu gì** | kịch bản 4. `FlushNow()` không cứu được: nó bỏ qua entry không dirty, cố tình — dirty-tracking không có ngoại lệ nào |
| **Listener đăng ký trước `Initialize()` bị xoá im lặng** | `Setup()` gán `OnValueChanged = null`, vì `ScriptableObject` sống qua các lần Play khi tắt domain reload: listener của phiên trước trỏ vào GameObject đã huỷ → `MissingReferenceException` ở lần đổi giá trị đầu tiên. Không detector nào phân biệt được listener phiên trước (phải xoá) với listener vừa đăng ký (bị xoá oan) — cả hai chỉ là `OnValueChanged != null`. Chặn duy nhất bằng **cấu trúc**: gọi `Initialize()` trong pha boot, mọi subscribe đứng sau nó |
| **Bốn thứ sống qua các lần Play** | `value` và `isDirty` — cả hai được `Setup()` reset. Thứ ba là **cache mà subclass dựng từ entry** (lookup, `HashSet`, cờ "đã parse"): entry không với tới được, nên có cặp hook `ResetDerivedState()` / `OnEntriesLoaded()`. *Đã sai một lần, ở Remote Config cùng khuôn nhưng thiếu hook "trước":* một cờ chỉ được bật, không ai tắt, mang trạng thái phiên trước sang phiên sau; một hệ khác phải viết workaround. Một cờ không reset được đã mất tư cách làm điều kiện chờ. Thứ tư là **`isInitialized`**: `Setup()` không với tới, nên host tự đặt `IsInitialized = false` ở `OnApplicationQuit` / `OnAppQuit` — lý do duy nhất setter tồn tại trên hợp đồng. Chỉ cần ở Editor khi tắt Domain Reload (trên device process chết là cờ chết), và nó **fail-open**: quit không bắn thì cờ ở lại `true`. Hôm nay **chưa code nào đọc** cờ này |
| **Quên `Initialize()` có giá bất đối xứng** | Remote Config quên gọi thì rơi về giá trị author — **hồi được**, lần fetch sau đúng. Save quên gọi thì mọi thứ người chơi làm nằm trong RAM rồi mất — **không hồi được**. Vì vậy host gọi nó trong pha boot. Chiều ngược lại, gọi **hai** lần, cũng không hồi được: xem "Hai host" |
| **Reset dirty trước khi ghi** | *đã sai một lần, `PlayerSaveLoadService.Save()` của color-loop:* `if (force \|\| _isDirty) { _isDirty = false; }` rồi thân serialize + ghi nằm **ngoài** `if` → lần nào gọi cũng ghi, và một lần ghi lỗi là mất im lặng vì cờ đã tắt |
| **Serialize theo nhịp đổi giá trị** | *đã sai một lần, `GameDataManager` của color-loop:* mỗi thay đổi bất kỳ field → `LateUpdate` frame đó `JsonUtility.ToJson` cả god-blob 25+ field + `PlayerPrefs.Save()` ngay trong frame. `JsonConvert` phải dồn về flush, và chỉ chạm entry dirty |
| **Đường no-op im lặng** | *đã sai một lần, khung save "sạch" của color-loop:* `AssignService()` không có caller → autosave loop chạy đều mà không lưu gì, **không một dòng log**. Mọi đường không-làm-gì-được phải kêu lên |
| **Deserialize không try/catch** | *đã sai một lần, `PlayerSaveLoadService.Load()` của color-loop:* không bọc `Deserialize` → exception **mỗi lần boot**, save thành brick vĩnh viễn. Payload trong `PlayerPrefs` hỏng được thật: build cũ ghi hình dạng khác, chỉnh tay khi debug, persist đứt nửa chừng |
| **`defaultValue` phải sao chép, không trả thẳng** | nó là object sống trong asset; gán `value = defaultValue` là để game mutate thẳng vào asset. `CloneDefault()` round-trip qua serializer một lần mỗi entry lúc `Initialize` — bản sao đúng cho mọi `T`, kể cả struct chứa `List` |
| **`[SerializeField]` bọc trong `#if UNITY_EDITOR`** | Editor ghi field đó vào asset mà build strip nó đi → đọc asset lệch byte → **crash native** (`Read N bytes but expected M bytes`), không stack trace C#. Muốn hiện trong Inspector mà không serialize thì `[NonSerialized, ShowInInspector]` |
| **`[ReadOnly]` trên `value` là cố ý** | sửa tay bỏ qua setter: không `MarkDirty`, không `OnValueChanged`, flush không ghi, không log — đúng định nghĩa một cửa no-op âm thầm. Cửa sửa tay đúng là nút `ImportPayload`, vì nó đi qua setter |
| **`IPersistenceDataCollection` không được derive `IService<>`** | interface dự án derive cả nó lẫn `IService<chính mình>` sẽ thừa hưởng **hai** thành viên static `Service` → mọi lần đọc `.Service` là **CS0229 ambiguity**, lỗi biên dịch. Bản thân `IPersistenceDataCollection` vẫn **đăng ký được** làm service bằng một dòng `[Service]` trên class dự án — đó là hai chuyện khác nhau. Hôm nay **không** đăng ký, vì không host nào nhận interface (bước 2) |
| **`[Service]` không di truyền** | `ServiceAttribute` khai `Inherited = false`. Khai ở `BasePersistenceDataCollection` là khai vào chỗ không ai đọc: service không đăng ký, `Service.Get<>()` ném ở lần chạm đầu. Nên cả ba dòng `[Service]` và `[CreateAssetMenu]` đều thuộc dự án — cũng là lý do base để `abstract`: một collection không có entry nào thì không có việc gì làm |
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
attribute + reflection quét field · `[Service]` dưới interface của dự án · `RESOURCE_PATH` ·
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
| `IPersistenceDataEntry` | `string Key { get; }` · `bool IsDirty { get; }` · `void Setup(IPersistenceDataCollection)` · `void ReadPayload(string)` · `string WritePayload()` · `void MarkDirty()` · `void ClearDirty()` |
| `IPersistenceDataCollection` | `IReadOnlyList<IPersistenceDataEntry> Entries { get; }` · `bool IsInitialized { get; set; }` · `void Initialize()` · `void FlushAll()` · `void Flush(IPersistenceDataEntry)` · `UniTask RunAutosaveAsync(CancellationToken)` |
| `PersistenceDataEntry<T> : IPersistenceDataEntry` | `[Serializable]` · `T Value { get; set; }` · `event Action<T> OnValueChanged` · `void MarkDirty()` · `void FlushNow()` · `implicit operator T` (null → `default`) · nút `ImportPayload`. `ClearDirty` là explicit interface implementation — xem Bất biến |
| `MarkedPersistence : Attribute` | `[AttributeUsage(AttributeTargets.Field)]` — đánh dấu field để scan nhận |
| `BasePersistenceDataCollection : ScriptableObject, IPersistenceDataCollection` | `abstract` · thân `RunAutosaveAsync` (impl của interface) · `protected virtual void ResetDerivedState()` · `protected virtual void OnEntriesLoaded()` · nút `ValidateKeys` |
| `BasePersistenceDataCollection` — phần `partial` `.TimeService.cs` | `protected internal PersistenceDataEntry<long> lastSeenUtcSeconds` · `PersistenceDataEntry<long> LastSeenUtcSeconds { get; }` — entry **duy nhất** base tự khai, dành cho `TimeService` (xem `TimeSystem.md`; service đó chưa có code). `protected internal` chứ không `private`: `TimeService` cùng assembly mà không kế thừa, còn scan `GetFields(NonPublic \| Instance)` trên type con vẫn thấy field kế thừa không-private. `Key` và `Default Value` điền ở **asset con** như mọi entry khác |

`Setup`, `ReadPayload` và `WritePayload` là `public` trên `PersistenceDataEntry<T>` — collection gọi
chúng, và không ca nào trong game code có lý do chạm tới.

**`Horcrux.Runtime.Implementations.Persistence`**

| Type | Vai trò |
|---|---|
| `SaveDriver : MonoBehaviour<BasePersistenceDataCollection>` | `OnAwake` `DontDestroyOnLoad` · `Start` init + autosave · flush ở cạnh vào background và `OnApplicationQuit` |
| `SaveBootstep : BaseBootStep, IInitializable<BasePersistenceDataCollection>` | `InitializeAsync` init + autosave · flush ở `OnGoToBackground(true)` và `OnAppQuit` |

**`Horcrux.Runtime.Abstractions`** — `IService<out T>`: `static T Service` · `static bool TryGet(out T)`.

## Cấu trúc file

```
Runtime/Abstractions/Foundations/Persistence/
├── IPersistenceDataEntry.cs · IPersistenceDataCollection.cs    interface THUẦN, xem mục Bẫy
├── PersistenceDataEntry.cs · .Editor.cs                        giá trị, dirty, event, JSON · nút Import
├── BasePersistenceDataCollection.cs                            scan, load, autosave, flush hai giai đoạn
├── BasePersistenceDataCollection.Editor.cs                     nút ValidateKeys + kiểm kiểu payload
├── BasePersistenceDataCollection.TimeService.cs                field lastSeenUtcSeconds
└── Persistence.md                                              tài liệu này

Runtime/Implementations/Foundations/Persistence/SaveDriver.cs   host cho dự án không có Bootstrap
Runtime/Implementations/Composites/SaveBootstep.cs              host trong pha boot — namespace vẫn
                                                                Implementations.Persistence
Tests/EditMode/PersistenceSeedingTests.cs                       ba kết cục của EntryLoadResult
```

Phía dự án trong repo này:
`Assets/_TheGame/Runtime/Game/Scripts/Data/Config/PersistenceDataCollection/` (interface + class, cộng
file `partial` theo feature và model save) và
`Assets/_TheGame/Runtime/Game/Resources/Config/PersistenceDataCollection.asset`.

## Còn để mở

- **Hệ SDK lấy giá trị save bằng cách nào — chưa chốt.** Hai đường: module **nhận
  `PersistenceDataEntry<T>`** qua Init (module compile-depend vào Persistence, bê sang dự án không có
  Persistence là không biên dịch), hoặc module phơi **field thường** (`IsEnabled`, `IsSfxOn`) và dự án
  đọc save rồi set vào lúc bootstrap, glue một chiều qua `OnValueChanged` (module không phụ thuộc
  Persistence). `HapticSystem.md` và `AudioSystem.md` đang đi đường thứ hai và nói rõ *"SDK cố tình
  không sở hữu hệ save"*. Chốt đường nào thì **sửa dòng này**, không thêm dòng thứ hai nói ngược lại.
- **Hệ sẽ dùng tiếp:** Audio (volume), Haptics, Economy (coin, lives), Rating, LiveOps. Dự án khai entry
  trên collection của mình rồi nối vào. **Một ngoại lệ duy nhất:** `lastSeenUtcSeconds` — Time cần mốc
  chống lùi giờ ở **mọi** dự án, nên base khai sẵn để không phải nhại lại mỗi nơi; ngoại lệ này đánh đổi
  lấy đúng một thứ là tính bê-sang-dự-án-khác.
- **Rẻ, thêm không sửa cũ:** nút xoá từng entry · kho file trên đĩa khi chạm một trong ba giới hạn ·
  migration version khi có model đổi schema · cloud sync khi backend chuẩn chung.
- **Hai chỗ Remote Config nên học lại từ Persistence:** `IRemoteConfigCollection.RemoteConfigs` nên là
  `IReadOnlyList<>` thay `IEnumerable<>`; cache `PlayerPrefs` của `RemoteConfig<T>` nên có tiền tố riêng
  (cache bỏ được, chỉ tốn một lượt fetch nguội).
