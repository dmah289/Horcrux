# Time System — Plan (bản tối thiểu)

> **Loại tài liệu:** Plan — **developer viết code lõi; agent viết test, chạy và báo kết quả.** Plan chỉ ghi
> bảng case, không dán code test. Chỉ phần LiveOps Collection cần; phần còn lại ở §5.

**Mục tiêu:** một nguồn giờ UTC **không lùi** cho mọi logic theo thời gian. Không hệ nào gọi `DateTime.UtcNow` để tính logic.

**Kiến trúc:** 2 file + 1 field.

```
Abstractions/Foundations/Time/     ITimeService.cs
Implementations/Foundations/Time/  TimeService.cs
Abstractions/Foundations/Persistence/BasePersistenceDataCollection.cs   + field lastSeenUtcSeconds
```

## Ngữ cảnh đã chốt

| Chốt | Nội dung |
|---|---|
| Nguồn giờ | giờ máy UTC, không hiệu chỉnh |
| Chống lùi | mốc lớn nhất từng thấy, lưu qua Persistence; giờ máy lùi → trả mốc, đứng yên |
| Ai giữ mốc | `TimeService` — field nằm trong `BasePersistenceDataCollection` để mọi dự án có sẵn, không cần khai entry |
| Server time | **không làm ở bản này** — không có caller nào cần. Contract `IServerTimeProvider`, offset và `IsServerTimeTrusted` chuyển hết sang `SystemPlan.md` §4b, xem §5 |
| Caller | `LiveOpsHost` (mỗi giây, cấp `now` cho mọi module) · module khi cần `Refresh` ngoài nhịp (`CollectionModule`) |
| Host | `MonoBehaviour<BasePersistenceDataCollection>` thường trong `Services.unity`, **không** phải boot step và **không** có `Order`. Gỡ server time xong thì bước boot không còn việc gì làm, mà thứ tự cũng không cần ai bảo đảm: guard `IsInitialized` nằm ngay trong getter. Chuỗi boot còn `SaveBootstep` (0) → `LiveOpsHost` (20) |

## §1 Contract

```csharp
namespace Horcrux.Runtime.Abstractions.Time
{
    public interface ITimeService : IService<ITimeService>
    {
        /// <summary>Unix seconds, UTC. Never smaller than any value returned before, across sessions.</summary>
        long UtcNowUnix { get; }
    }
}
```

## §2 Field trong `BasePersistenceDataCollection`

```csharp
[Splitter("Time Service")]
[MarkedPersistence, SerializeField]
protected internal PersistenceDataEntry<long> lastSeenUtcSeconds;   // owned by TimeService

public PersistenceDataEntry<long> LastSeenUtcSeconds => lastSeenUtcSeconds;
```

| Quyết định | Vì |
|---|---|
| `protected internal`, không `protected` | `TimeService` cùng assembly nhưng không kế thừa → `protected` không đọc được. Scan `GetType().GetFields(NonPublic \| Instance)` trên type con vẫn thấy field kế thừa không-private |
| Key điền trong Inspector của asset con | `PersistenceDataEntry<T>` không có ctor nhận key; `ValidateKeys` bắt key trống |
| `long` giây, không `DateTime` | payload JSON thuần, so sánh rẻ |

## §3 `TimeService`

```csharp
using System;
using Horcrux.Runtime.Abstractions.Persistence;
using Horcrux.Runtime.Abstractions.Time;
using Sisus.Init;

namespace Horcrux.Runtime.Implementations.Time
{
    /// <summary>The one UTC clock game logic reads. Never goes backwards, across sessions. See TimeSystem.md.</summary>
    [Service(typeof(ITimeService), FindFromScene = true)]
    public sealed class TimeService : MonoBehaviour<BasePersistenceDataCollection>, ITimeService
    {
        /// <summary>How far past the stored mark the device must reach before the mark is written again.</summary>
        private const long GuardWriteStepSeconds = 60;

        public long UtcNowUnix
        {
            get
            {
                long device = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

                // Nothing orders this getter: it is public and the save loads on its own schedule.
                // Before that the mark still reads 0, and trusting it would pin the clock to the epoch.
                if (!saveCollection.IsInitialized)
                    return device;

                PersistenceDataEntry<long> guard = saveCollection.LastSeenUtcSeconds;

                // Writing every second would dirty the entry for autosave with nothing to show for it.
                if (device >= guard.Value + GuardWriteStepSeconds)
                    guard.Value = device;

                // Device clock moved back: stand on the mark rather than hand out a smaller number.
                return Math.Max(device, guard.Value);
            }
        }

        #region DI
        private BasePersistenceDataCollection saveCollection;

        protected override void Init(BasePersistenceDataCollection argument)
        {
            saveCollection = argument;
        }
        #endregion
    }
}
```

| Quyết định | Vì |
|---|---|
| Ghi mốc mỗi `GuardWriteStepSeconds = 60` | mỗi giây một lần dirty là autosave ghi PlayerPrefs vô ích; lùi ≤ 60s không ai lợi được gì |
| Lùi giờ → đứng tại mốc | đơn giản, không cần trạng thái "đang bị lùi"; countdown chỉ đứng, không âm |
| Getter ghi mốc | hệ không có nhịp riêng; caller 1 Hz duy nhất là `LiveOpsHost`. Ghi chỉ xảy ra sau `IsInitialized` và thưa 60s nên getter vẫn rẻ |
| **`MonoBehaviour<T>`, không `BaseBootStep`** | Gỡ server time xong thì `InitializeAsync` rỗng, và một step rỗng trong `BootstrapRunner` là một mắt xích chỉ tồn tại để chờ việc chưa có. Base class trống ra thì dùng luôn `MonoBehaviour<T>` của Sisus — cùng kiểu với `SaveDriver`, và DI vào `Init` giống hệt. Hệ quả: `TimeService` **không** nằm trong list `steps`, **không** có `Order`, và `LiveOpsHost` chỉ cần đứng sau `SaveBootstep`. Không mất gì vì `UtcNowUnix` chưa bao giờ dựa vào thứ tự — nó tự hỏi `IsInitialized` |
| **Giữ `if !save.IsInitialized`** | Bỏ boot step rồi thì không còn cái gì xếp thứ tự cho `TimeService` nữa, nên guard này là thứ **duy nhất** đứng giữa caller sớm và một mốc bằng `0`. `UtcNowUnix` là getter công khai, caller ngoài chuỗi gọi được bất cứ lúc nào — `CollectionBridge` ở scene Game chẳng hạn. Bỏ guard là đọc mốc `0` rồi ghi mốc theo nó |

## §4 Trước khi chạy

| Bước | Thiếu thì hỏng ở đâu |
|---|---|
| Asset save của dự án: điền Key cho field `Last Seen Utc Seconds` (nhóm Time), ví dụ `time_last_seen_utc`; bấm **Validate keys** | `LogError` "empty key" lúc `Initialize`, mốc không lưu → không chống lùi |
| `Services.unity`: object `TimeService`, Init → kéo asset save. **Không** kéo vào list `steps` của `BootstrapRunner` — nó không phải step | `IService<ITimeService>.Service` ném ở caller đầu tiên |
| **Save phải được load** — `TimeService` không còn ở trong chuỗi boot, nhưng `SaveBootstep` thì có, và runner cố ý không tự boot. Dự án này gọi `await IBootstrapService.Service.InitializeAsync()` trong `StartGame.Starting()`, ngay sau khi scene chứa runner đã load xong | save không load → `IsInitialized` mãi `false` → `UtcNowUnix` **vẫn trả giờ máy** và không bao giờ chống lùi. Im lặng tuyệt đối: hỏng kiểu này không lộ ở hệ Time mà lộ ở Collection |
| Nếu dời `TimeService` khỏi scene đầu: xác nhận `FindFromScene` thấy được scene nạp bằng Addressables | không thấy thì `ServiceInjector` chỉ `LogWarning "Service Not Found"` rồi trả `null`; `ITimeService.Service` ném NRE ở caller đầu |

## Kiểm

| Ca | Kỳ vọng | Ai chạy |
|---|---|---|
| Play lần đầu | `UtcNowUnix` ≈ giờ máy; sau 60s PlayerPrefs có `persistence_<key>` | **developer** — Play mode |
| Lùi giờ máy 1 ngày rồi mở lại | `UtcNowUnix` = mốc cuối, không lùi | **developer** — Play mode |
| Tiến giờ máy | `UtcNowUnix` theo giờ máy — **không chống tiến**, và bản này cố ý không chống. Chống tua giờ cần server time, xem §5 | **developer** — Play mode |

Hệ này không có test tự động: luật chống lùi nằm trong getter của một MonoBehaviour và đọc
`save.IsInitialized`, nên cả ba ca đều cần Play mode và một lần đổi giờ máy. Phần của agent ở đây là
biên dịch và soát code theo §3.

## §5 Để sau

`Countdown` struct · `TimeFormatter` zero-GC · nhịp 1 Hz chuyển sang Ticker (4a).

**Server time** — đã gỡ khỏi bản này, thiết kế đầy đủ ở `SystemPlan.md` §4b: contract `IServerTimeProvider`,
`offsetSeconds` cộng vào `UtcNowUnix`, cờ `IsServerTimeTrusted`, `ResyncAsync` lúc boot và lúc resume, và
luật "chặn cấp thưởng theo giờ khi `!IsServerTimeTrusted`". Gỡ vì **chưa có caller**: không hệ nào ở bản
Collection này đọc cờ đó, nên giữ lại là một contract không ai gọi và một `offsetSeconds` luôn bằng 0.
Lúc làm lại thì `UtcNowUnix` mọc thêm `+ offsetSeconds`, và `TimeService` đổi base về `BaseBootStep`,
`IInitializable<BasePersistenceDataCollection>` để có `InitializeAsync` gọi `ResyncAsync` — đúng hình dạng
nó vừa bị gỡ khỏi. Kèm theo: kéo lại vào list `steps` với `Order` sau `SaveBootstep`.
