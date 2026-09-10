# Persistence — PersistenceDataEntry + BasePersistenceDataCollection + PlayerPrefs

> **Loại tài liệu:** tài liệu hệ. Plan gốc là `PersistencePlan.md` — đọc plan khi cần biết *vì sao*
> chọn hình dạng này; đọc file này khi cần **dựng** và **dùng** hệ.

Mỗi thứ lưu được là một `PersistenceDataEntry<T>` — giữ giá trị, giá trị mặc định, một khoá, và cờ
dirty. Mọi entry là field của **một** ScriptableObject dẫn xuất `BasePersistenceDataCollection`;
collection lo quét field, kiểm khoá, load lúc khởi động, autosave theo chu kỳ, và là **chỗ duy nhất**
gọi `PlayerPrefs.SetString` / `PlayerPrefs.Save()`.

## Đường đi của dữ liệu

```
authoring          runtime đọc/ghi                     lưu
─────────          ───────────────                     ───
key (string)   ┐
defaultValue   ├─► Initialize()                        (một lần, do host gọi)
               │     ScanEntries    reflection quét field [MarkedPersistence]
               │     Setup(this)    value = CloneDefault()
               │     LoadAll()      PlayerPrefs.GetString("persistence_" + key) ─► ReadPayload
               │
game code      ├─► entry.Value = x  ─► MarkDirty()  ─► OnValueChanged
               │   (đọc: entry.Value, hoặc implicit operator T)
               │
autosave/pause └─► FlushAll()
                     ① mọi entry dirty: WritePayload() ─► PlayerPrefs.SetString   (vẫn ở RAM)
                     ② PlayerPrefs.Save() một lần  ─►  đĩa  ─►  rồi mới ClearDirty()
```

## Trước khi chạy

Hệ này **không tự setup hộ**: không tự tạo GameObject, không tự tra tìm asset, không guard cho ô
người dựng phải điền. Bù lại, dựng lại từ 0 chỉ cần đúng các bước dưới, theo thứ tự.

| # | Bước | Thiếu bước này thì hỏng ở đâu |
|---|---|---|
| 1 | **Khai collection của dự án**: một `sealed partial class GameSave : BasePersistenceDataCollection` trong assembly của **dự án** (không phải trong Horcrux), kèm `[CreateAssetMenu]` | Không có gì để tạo asset; và khai trong Horcrux thì kéo type của dự án vào SDK — submodule không commit được một mình |
| 2 | **Khai từng entry là field private của class đó**, mỗi field một `[MarkedPersistence]`: `[MarkedPersistence, SerializeField] private PersistenceDataEntry<int> coin;` | Reflection quét `GetType()` với `NonPublic` + `Instance` — **không thấy** field private khai ở class base. Field thiếu attribute thì entry không tồn tại với hệ: đọc/ghi vẫn chạy trong RAM, không bao giờ xuống đĩa, **không một dòng log** |
| 3 | **Tạo asset** từ menu `[CreateAssetMenu]` vừa khai | Không có asset thì không có gì kéo vào host ở bước 6 |
| 4 | **Điền `Key` cho từng entry** trong Inspector — chuỗi tự đặt, không suy từ tên field | Khoá rỗng bị `ScanEntries` loại kèm `LogError`; entry đó không lưu gì. Xem "Khoá là wire format" dưới |
| 5 | **Điền `Default Value` cho từng entry**, và **`Autosave Interval Seconds`** trên chính asset collection (mặc định 10s, chặn dưới `[Min(5f)]`) | Default để trống thì người chơi mới bắt đầu bằng giá trị mặc định của kiểu — `0`, `false`, list rỗng — mà ô trống **không phân biệt được** với ô cố ý điền số đó, nên không có gì báo |
| 6 | **Chọn đúng MỘT host** và kéo asset vào nó — xem "Hai host" dưới. Trong Inspector của host: mục **Init** → **Add Initializer** (hoặc chuột phải header component → **Generate Initializer**) → kéo asset collection vào ô argument | Không wire thì `saveCollection` là null → `NullReferenceException` ở `Initialize()`. Ở `SaveDriver` thì đỏ console và `Start()` dừng; ở `SaveBootstep` thì `BootstrapRunner` **fail-open** — đỏ một dòng rồi cả phiên chạy tiếp **không có** persistence |
| 7 | **Đặt `SaveDriver` trên object sống suốt phiên** (nếu chọn host này) | Scene chứa nó unload là `destroyCancellationToken` hủy → autosave **chết im lặng**, UniTask coi hủy là bình thường nên không log gì. Chỉ còn flush lúc pause/quit |
| 8 | **Bấm `Validate keys`** trên asset trước khi Play | Nút này kiểm khoá trùng, khoá rỗng, field gắn attribute mà chưa gán entry, **và** kiểu payload (mục dưới). Không bấm thì cả bốn ca chỉ báo lúc `Initialize()` chạy — hoặc muộn hơn nữa |

## Hợp đồng payload — `T` phải là dữ liệu thuần

`WritePayload` / `ReadPayload` / `CloneDefault` dùng `JsonConvert` **không settings**. Newtonsoft đọc
cả public **field** lẫn public **property có getter** — nên nó chạm tới property tính toán của struct
toán Unity, thứ mà serializer của Unity không bao giờ chạm. `Vector3.normalized` trả về `Vector3`, và
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
`Transform`, `AnimationCurve`…) — kể cả khi nó nằm sâu trong một field hoặc property của model. Cần
lưu vị trí thì khai struct của mình:

```csharp
[Serializable]
public struct SavedPosition
{
    public float x, y, z;
}
```

Đắt hơn một dòng, nhưng đúng hai chuyện: payload không mang thứ suy diễn lại được, và **hình dạng
save là hợp đồng của dự án**, không phải hình dạng một kiểu của engine.

**Lỗi này lộ ra ở đâu.** Struct nằm ngay trong `Default Value` thì `CloneDefault()` ném ở
`Initialize()` — **mọi** lần Play, không phụ thuộc cấu hình, thấy đỏ ngay trong Editor. Nhưng struct
nằm trong một container **rỗng lúc mặc định** (`List<Vector3>` chưa có phần tử) thì `Initialize()` qua
sạch, và nó chỉ ném ở **lần flush đầu sau khi runtime nhồi dữ liệu vào** — chỗ đó có try/catch từng
entry nên `LogError` nêu đúng khoá và entry khác vẫn lưu bình thường, còn entry này thì không bao giờ
lưu nữa. Đó là ca duy nhất của luật này lọt được qua Editor xuống build.

**Kiểm trước khi build.** Nút `Validate keys` trên asset kiểm luôn hợp đồng này: nó duyệt `T` cùng mọi
field và property public lồng bên trong, rồi in ra đường đi tới kiểu engine đầu tiên gặp được — ví dụ
`Entry 'route' stores SavedRoute.lastCheckpoint → Vector3`. Kiểm theo **kiểu**, không theo giá trị,
nên một `List<Vector3>` rỗng vẫn bị bắt — đúng cái ca vừa nói ở trên. Field `[JsonIgnore]` và
`[NonSerialized]` được bỏ qua, vì Newtonsoft cũng bỏ qua.

Chỗ nút này **không** thấy: field khai kiểu `object`, `dynamic`, hoặc một interface / base class mà chỉ
giá trị runtime mới là kiểu engine — kiểu tĩnh không nói ra được điều đó. Đừng khai save model bằng
`object`.

## Hai host — chọn một, không dùng cả hai

| | `SaveDriver` | `SaveBootstep` |
|---|---|---|
| Dùng khi | dự án **không** có Bootstrap | dự án có `BootstrapRunner` |
| Là gì | `MonoBehaviour<BasePersistenceDataCollection>` kéo tay vào scene | `BaseBootStep`, chạy trong pha boot |
| Gọi `Initialize()` | `Start()` | `InitializeAsync()` |
| Chạy autosave | `RunAutosaveAsync(destroyCancellationToken)` | `RunAutosaveAsync(destroyCancellationToken)` — **không** phải token của pha |
| Flush | cạnh vào background (`OnApplicationFocus` / `OnApplicationPause`, một lần cho mỗi lần vào) + `OnApplicationQuit` | `OnGoToBackground(true)` + `OnAppQuit` |

Cả hai gánh **đủ** trách nhiệm, nên chạy cả hai không hỏng dữ liệu (`Initialize()` idempotent, flush
hai giai đoạn chạy trùng vẫn đúng) — nhưng là hai nhịp autosave chồng nhau, không có lý do để làm vậy.

Vì sao thân vòng lặp và `autosaveIntervalSeconds` nằm ở **collection** chứ không ở host: cả hai host
cần đúng một nhịp đó, mà bảo đảm "mất tối đa một chu kỳ" là bảo đảm của collection. Còn **token** ở
lại host, vì host là thứ có đời sống — một `ScriptableObject` không có mốc kết thúc tin được, nên nó
không được tự mở `CancellationTokenSource`: không ai đóng, và ở Editor tắt domain reload thì mỗi lần
Play là một loop nữa xếp lên loop cũ.

## Bất biến

| Bất biến | Vì sao, và nó nằm ở đâu |
|---|---|
| **Dirty chỉ tắt sau khi storage nhận** | `FlushInternal` hai giai đoạn: ghi hết payload → `PlayerPrefs.Save()` một lần → **rồi mới** `ClearDirty()`. Tắt trước là mất tiến độ mà không có gì cho thấy: `SetString` chỉ đụng bản RAM của PlayerPrefs, `Save()` mới là thứ chạm đĩa |
| **`ClearDirty` không gọi được từ game code** | explicit interface implementation trên `PersistenceDataEntry<T>` — chỉ collection với tay tới, và chỉ sau `Save()` |
| **Một `PlayerPrefs.Save()` cho cả lượt** | Trên Android nó là `SharedPreferences.commit()`: ghi **cả kho**, **đồng bộ trên main thread**. Vì vậy chu kỳ autosave có `[Min(5f)]`, và vì vậy giai đoạn hai chỉ chạy khi có entry vừa ghi |
| **Khoá là wire format** | Khoá thật trên đĩa là `"persistence_" + Key`. Đổi `Key` hoặc đổi `KeyPrefix` sau khi ship = mọi save đã có thành mồ côi, người chơi mất tiến độ. Không đổi theo bất kỳ lần refactor tên nào |
| **Không có đường không-làm-gì âm thầm** | khoá trùng, khoá rỗng, field chưa gán entry, đọc payload hỏng, ghi payload hỏng — mỗi ca một `LogError` nêu đúng khoá. `PlayerPrefs` không có khoá, hoặc payload rỗng, thì giữ giá trị mặc định (đúng, không phải lỗi) |

## Bẫy đã biết

- **Mutate model tại chỗ mà không `MarkDirty()` thì không lưu gì.** `entry.Value = x` tự mark; nhưng
  `entry.Value.coins += 5` trên một model class thì entry không biết. `FlushNow()` cũng **không** cứu
  được: nó bỏ qua entry không dirty, cố tình — dirty-tracking không có ngoại lệ nào.
- **`LoadAll` chỉ chạy trong `Initialize()`.** Không có đường reload giữa phiên; sửa PlayerPrefs bằng
  tay lúc đang Play thì phải Play lại. Xem/sửa/xoá PlayerPrefs bằng tool
  `Assets/Horcrux/Editor/PlayerPrefsEditor`.
- **`PlayerPrefs` là một không gian khoá phẳng, dùng chung** với Firebase, ads, analytics và
  `RemoteConfig<T>`. Tiền tố `"persistence_"` là thứ duy nhất chặn một khoá SDK trùng tên đè lên tiến độ.
- **Autosave chạy bằng `DelayType.Realtime`** — vẫn tick khi game đang `timeScale = 0`. Đó là chủ ý:
  người chơi ngồi ở popup pause vẫn được lưu.
