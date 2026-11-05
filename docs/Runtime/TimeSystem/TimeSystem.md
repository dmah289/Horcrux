# Time — giờ UTC không lùi cho logic theo thời gian

`ITimeService.UtcNowUnix` trả Unix giây UTC lấy từ giờ máy, chặn lùi bằng một mốc lưu qua Persistence. Logic theo thời
gian (lịch, countdown, rollover) đọc giờ ở đây, không gọi `DateTime.UtcNow`. Không có server time.

Code: `Runtime/Abstractions/Foundations/Time/ITimeService.cs` · `Runtime/Implementations/Foundations/Time/TimeService.cs` ·
field mốc: `Runtime/Abstractions/Foundations/Persistence/BasePersistenceDataCollection.TimeService.cs` · namespace
`Horcrux.Runtime.Abstractions.Time`, `Horcrux.Runtime.Implementations.Time`.

## Ngữ cảnh đã chốt

| Mục | Nội dung |
|---|---|
| Nguồn giờ | `DateTimeOffset.UtcNow` của máy, không hiệu chỉnh |
| Chống lùi | mốc `lastSeenUtcSeconds` trong save; giờ máy dưới mốc → trả mốc |
| Độ mịn | mốc chỉ ghi khi giờ máy vượt mốc ≥ 60 s → lùi dưới 60 s lọt qua (§4) |
| Chống tiến | không — giờ máy tua tới được nhận nguyên |
| Ai giữ mốc | `TimeService`; field nằm sẵn trong `BasePersistenceDataCollection`, dự án chỉ điền Key |
| Caller | `LiveOpsHost` (mỗi giây cấp `now` cho module) · code cần giờ ngoài nhịp đó |
| Host | `MonoBehaviour<BasePersistenceDataCollection>` của InitArgs; không phải boot step, không có **Order** |
| Cố ý KHÔNG làm | server time · nhịp riêng · trạng thái "đang bị lùi" · API gỡ mốc |

## Trước khi chạy

Dựng từ 0 theo thứ tự; mỗi bước kèm chỗ hỏng nếu thiếu. Collection save dựng theo `Persistence.md`.

1. **Key cho mốc.** Chọn asset collection save của dự án → nhóm **Time Service** → `Last Seen Utc Seconds` → ô **Key**
   điền chuỗi tự đặt, ví dụ `time_last_seen_utc`; ô **Default Value** để `0`. Bấm **Validate Keys**.
2. **Collection đăng ký service base.** Lớp collection có dòng
   `[Service(typeof(BasePersistenceDataCollection), ResourcePath = RESOURCE_PATH)]` (`Persistence.md` › Trước khi chạy,
   bước 2). `TimeService` nhận asset qua đường này; không có Initializer.
3. **Service.** Trong scene sống suốt phiên, tạo GameObject, Add Component `TimeService`. Không kéo vào ô **Steps** của
   `BootstrapService`.
4. **Save được load.** Đúng một host save (`SaveDriver` hoặc `SaveBootstep`, `Persistence.md` › Hai host) gọi
   `Initialize()` của collection.
5. **Caller.** Đọc giờ mỗi lần cần, không cache qua nhiều frame:
   ```csharp
   long now = ITimeService.Service.UtcNowUnix;
   ```
   Hoặc nhận qua InitArgs như `LiveOpsHost`: `IInitializable<ITimeService>` + Initializer, ô **Time Service** để
   **Service**.

| Thiếu | Hỏng ở đâu |
|---|---|
| Key của `Last Seen Utc Seconds` | `[PersistenceDataCollection]: Field 'lastSeenUtcSeconds' has an empty key.` lúc `Initialize`; entry bị loại khỏi kho: mốc chỉ sống trong phiên, mở lại app là mất → lùi giờ giữa hai phiên lọt |
| **Default Value** `0` | giá trị lớn hơn giờ thật → lần chạy đầu `UtcNowUnix` đứng ở giá trị đó (§4 "mốc ở tương lai") |
| dòng `[Service(typeof(BasePersistenceDataCollection))]` | Editor: InitArgs ném `MissingInitArgumentsException` ở `Awake` của `TimeService`; bản build: collection `null` → `NullReferenceException` ở lần đọc `UtcNowUnix` đầu |
| `TimeService` trong scene | InitArgs `LogWarning "Service Not Found: There is no 'TimeService' found in the active scene …"`; `ITimeService.Service` ném `NullReferenceException` ở caller đầu |
| **`FindFromScene` thấy được scene chứa service** — scene nạp bằng Addressables là nạp **sau** khi InitArgs khởi tạo (`LiveOpsHost.md` › Trước khi chạy) | như trên |
| host save | `IsInitialized` mãi `false` → `UtcNowUnix` trả giờ máy trần, không chống lùi, **không log nào**. Lộ ở hệ đọc giờ (lịch nhảy lùi), không ở Time |

## 1. API

| Gọi | Ai gọi, khi nào | Hành vi |
|---|---|---|
| `ITimeService.UtcNowUnix` (`long`, get) | logic theo thời gian, mỗi lần cần giờ | Unix giây UTC (§2). Getter **có ghi**: đặt `LastSeenUtcSeconds.Value` khi giờ máy vượt mốc ≥ 60 s |
| `BasePersistenceDataCollection.LastSeenUtcSeconds` | chỉ `TimeService` | `PersistenceDataEntry<long>` giữ mốc |

## 2. Luồng

```
caller ── UtcNowUnix ──► TimeService
                           device = DateTimeOffset.UtcNow (giây)
                           ├─ save.IsInitialized ?   ──no───► return device             ← chưa chống lùi
                           ├─ device ≥ mốc + 60 ?    ──yes──► mốc.Value = device        ← dirty; host save ghi PlayerPrefs
                           └─ return max(device, mốc)
```

Mốc là giờ máy **lần ghi gần nhất**, không phải giá trị lớn nhất đã trả — kém nhau tới 59 s. Lần theo mốc `1000`:

| `device` | Ghi mốc | Trả |
|---|---|---|
| `1030` | không | `1030` |
| `1060` | mốc = `1060` | `1060` |
| `1075` | không | `1075` |
| `1065` — lùi 10 s | không | `1065` — nhỏ hơn `1075` đã trả |
| `900` — lùi xa | không | `1060` |

## 3. Vòng đời

| Giai đoạn | Chuyện gì xảy ra |
|---|---|
| Scene chứa service load | InitArgs tiêm collection vào `Init` ở `Awake` |
| Trước `Initialize()` của host save | trả giờ máy trần, không đọc, không ghi mốc |
| Sau `Initialize()` | mốc đọc từ PlayerPrefs; chống lùi bật |
| Mỗi lần đọc | có thể đặt mốc trong bộ nhớ; xuống PlayerPrefs ở lượt flush của host save — autosave (`autosaveIntervalSeconds`, mặc định 10 s), vào nền, thoát |
| Thoát app | host save `FlushAll` rồi đặt `IsInitialized = false` → từ đó trả giờ máy trần |

## 4. Guard và bẫy

| Chỗ | Sự thật |
|---|---|
| **Lùi dưới 60 s lọt** | mốc kém giá trị đã trả tới 59 s (§2); giờ máy lùi vào khoảng đó thì `UtcNowUnix` lùi theo. Logic so `now` với thời điểm đã lưu (hết hạn, lần nhận) chịu được; logic cần đơn điệu tuyệt đối thì tự giữ `max` |
| **Mốc ở tương lai làm giờ đứng** | giờ máy từng tua tới — cố ý, hoặc đồng hồ máy sai rồi được sửa — thì mốc nằm ở tương lai; giờ về đúng thì `UtcNowUnix` đứng tại mốc tới khi giờ thật đuổi kịp, countdown và lịch đứng theo. Không API gỡ; chỉ xoá key `persistence_<key>` khỏi PlayerPrefs |
| **Ngoài `IsInitialized` không chống lùi** | caller đọc trước `Initialize()` (`Awake`, boot step đứng trước host save) hay sau khi thoát nhận giờ máy trần |
| **App bị giết giữa hai lượt flush** | mốc mới chỉ ở bộ nhớ; phiên sau mốc cũ hơn thêm tối đa một chu kỳ autosave |
| **Tua tới không bị chặn** | cố ý; chặn cần server time (Mở rộng) |

## 5. Quyết định

| Câu hỏi | Tiêu chí | Chốt |
|---|---|---|
| Ghi mốc mỗi lần đọc hay theo bước | mỗi lần ghi là một entry dirty, autosave ghi PlayerPrefs; `LiveOpsHost` đọc mỗi giây | bước `GuardWriteStepSecond = 60`; giá là lỗ hổng dưới 60 s (§4) |
| Lùi giờ: đứng tại mốc hay báo trạng thái | không caller nào đọc trạng thái "đang bị lùi"; countdown đứng thay vì âm | đứng tại mốc |
| Ai đặt nhịp ghi | hệ không có nhịp riêng; caller 1 Hz là `LiveOpsHost` | getter ghi — thưa 60 s và chỉ sau `IsInitialized` nên getter vẫn rẻ |
| `long` giây hay `DateTime` | payload JSON thuần, so sánh rẻ | `long` |
| Field `protected` hay `protected internal` | `TimeService` cùng assembly nhưng không kế thừa; scan `GetFields(NonPublic \| Instance)` trên type con vẫn thấy field kế thừa không-private | `protected internal` |
| Key qua ctor hay Inspector | `PersistenceDataEntry<T>` không có ctor nhận key; `ValidateKeys` bắt key trống | điền Key trên asset |
| `MonoBehaviour<T>` hay `ABootStep` | không có việc init bất đồng bộ; một step rỗng trong chuỗi boot chỉ chờ việc chưa có | `MonoBehaviour<T>` như `SaveDriver`: không trong **Steps**, không **Order**; thứ tự với save do guard trong getter lo |
| Giữ guard `IsInitialized` | không gì xếp `TimeService` sau save; getter công khai, gọi được bất cứ lúc nào | giữ — bỏ là đọc mốc chưa load |

## 6. Chẩn đoán

| Log / triệu chứng | Nghĩa | Sửa |
|---|---|---|
| `[PersistenceDataCollection]: Field 'lastSeenUtcSeconds' has an empty key.` | Key chưa điền | bước 1 |
| `MissingInitArgumentsException` trên `TimeService` (Editor) | collection không đăng ký service `BasePersistenceDataCollection` | bước 2 |
| `Service Not Found: There is no 'TimeService' found in the active scene …` | không có `TimeService` lúc InitArgs tìm | bước 3 |
| `NullReferenceException: No globally accessible Service of type ITimeService was found.` | `ITimeService.Service` gọi khi service không có | bước 3 |
| lùi giờ máy, logic lùi theo | save chưa load, hoặc lùi dưới 60 s | bước 4; §4 dòng đầu |
| giờ game đứng, countdown không chạy | mốc ở tương lai | §4 dòng hai |

## Mở rộng

Server time — thiết kế ở `Plans/SystemPlan.md` §4b: contract `IServerTimeProvider`, `offsetSeconds` cộng vào
`UtcNowUnix`, cờ `IsServerTimeTrusted`, `ResyncAsync` lúc boot và lúc resume, chặn cấp thưởng theo giờ khi chưa tin giờ.
Làm thì `TimeService` đổi base về `ABootStep` + `IInitializable<BasePersistenceDataCollection>` để `InitializeAsync` gọi
`ResyncAsync`, vào ô **Steps** với **Order** sau host save.

## Chữ ký

| Type | Thành viên |
|---|---|
| `Horcrux.Runtime.Abstractions.Time.ITimeService : IService<ITimeService>` | `long UtcNowUnix { get; }` |
| `Horcrux.Runtime.Implementations.Time.TimeService : MonoBehaviour<BasePersistenceDataCollection>, ITimeService` | `[Service(typeof(ITimeService), FindFromScene = true)]` · `private const int GuardWriteStepSecond = 60` · `protected override void Init(BasePersistenceDataCollection argument)` |
| `Horcrux.Runtime.Abstractions.Persistence.BasePersistenceDataCollection` (partial `.TimeService.cs`) | `[Splitter("Time Service")]` `[MarkedPersistence, SerializeField] protected internal PersistenceDataEntry<long> lastSeenUtcSeconds` · `public PersistenceDataEntry<long> LastSeenUtcSeconds` |

## Cấu trúc file

```
Runtime/Abstractions/Foundations/Time/
└── ITimeService.cs                                  contract
Runtime/Implementations/Foundations/Time/
└── TimeService.cs                                   getter chống lùi
Runtime/Abstractions/Foundations/Persistence/
└── BasePersistenceDataCollection.TimeService.cs     field mốc
```

## Tests

Không có test riêng: getter đọc thẳng `DateTimeOffset.UtcNow`, không tiêm đồng hồ được. `Tests/EditMode/PersistenceSeedingTests.cs`
điền key cho `lastSeenUtcSeconds` vì field kế thừa vào mọi collection — key trống làm scan log lỗi.
