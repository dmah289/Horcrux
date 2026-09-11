# Time System — Plan (bản tối thiểu)

> **Loại tài liệu:** Plan — **developer viết code lõi; agent viết test, chạy và báo kết quả.** Plan chỉ ghi
> bảng case, không dán code test. Chỉ phần LiveOps Collection cần; phần còn lại ở §5.

**Mục tiêu:** một nguồn giờ UTC **không lùi** cho mọi logic theo thời gian. Không hệ nào gọi `DateTime.UtcNow` để tính logic.

**Kiến trúc:** 3 file + 1 field.

```
Abstractions/Foundations/Time/     ITimeService.cs · IServerTimeProvider.cs
Implementations/Foundations/Time/  TimeService.cs
Abstractions/Foundations/Persistence/BasePersistenceDataCollection.cs   + field lastSeenUtcSeconds
```

## Ngữ cảnh đã chốt

| Chốt | Nội dung |
|---|---|
| Nguồn giờ | giờ máy UTC + offset server (offset = 0 khi chưa có provider) |
| Chống lùi | mốc lớn nhất từng thấy, lưu qua Persistence; giờ máy lùi → trả mốc, đứng yên |
| Ai giữ mốc | `TimeService` — field nằm trong `BasePersistenceDataCollection` để mọi dự án có sẵn, không cần khai entry |
| Server time | contract có, impl để trống; `IsServerTimeTrusted = false` cho tới khi có provider |
| Caller | `LiveOpsHost` (mỗi giây, cấp `now` cho mọi module) · module khi cần `Refresh` ngoài nhịp (`CollectionModule`) |
| Host | **boot step**, không phải MonoBehaviour tự lo `Start`. `BootstrapRunner` trong `Services.unity` chạy `SaveBootstep` (0) → `TimeService` (10) → `LiveOpsHost` (20) theo `Order`. Runner tự lái trong `Start()` của chính nó; `GameManager` của game chạy độc lập, không liên quan |

## §1 Contract

```csharp
namespace Horcrux.Runtime.Abstractions.Time
{
    public interface ITimeService : IService<ITimeService>
    {
        /// <summary>Unix seconds, UTC. Never smaller than any value returned before, across sessions.</summary>
        long UtcNowUnix { get; }
        bool IsServerTimeTrusted { get; }     // true after a successful IServerTimeProvider fetch
    }

    public interface IServerTimeProvider : IService<IServerTimeProvider>
    {
        UniTask<long> FetchUtcNowUnixAsync(CancellationToken ct);   // 0 = failed
    }
}
```

## §2 Field trong `BasePersistenceDataCollection`

```csharp
[Splitter("Time")]
[MarkedPersistence, SerializeField]
protected internal PersistenceDataEntry<long> lastSeenUtcSeconds;   // owned by TimeService
```

| Quyết định | Vì |
|---|---|
| `protected internal`, không `protected` | `TimeService` cùng assembly nhưng không kế thừa → `protected` không đọc được. Scan `GetType().GetFields(NonPublic \| Instance)` trên type con vẫn thấy field kế thừa không-private |
| Key điền trong Inspector của asset con | `PersistenceDataEntry<T>` không có ctor nhận key; `ValidateKeys` bắt key trống |
| `long` giây, không `DateTime` | payload JSON thuần, so sánh rẻ |

## §3 `TimeService`

```csharp
[Service(typeof(ITimeService), FindFromScene = true)]
public sealed class TimeService : BaseBootStep, ITimeService, IInitializable<BasePersistenceDataCollection>
```

```
UtcNowUnix (get):
  device = DateTimeOffset.UtcNow.ToUnixTimeSeconds() + offsetSeconds
  if !save.IsInitialized → return device                    ← mốc chưa load, chưa được ghi
  guard = save.lastSeenUtcSeconds
  if device >= guard.Value + GuardWriteStepSeconds → guard.Value = device   ← ghi thưa: mỗi 60s một lần
  return Math.Max(device, guard.Value)

InitializeAsync(ct):                                        ← step của BootstrapRunner, Order sau SaveBootstep
  if IService<IServerTimeProvider>.TryGet(out provider) → ResyncAsync(provider, destroyCancellationToken).Forget()
  return UniTask.CompletedTask                              ← token riêng, không phải ct của pha

ResyncAsync:
  server = await provider.FetchUtcNowUnixAsync(ct)
  if server > 0 → offsetSeconds = server − deviceNow; IsServerTimeTrusted = true
```

| Quyết định | Vì |
|---|---|
| Ghi mốc mỗi `GuardWriteStepSeconds = 60` | mỗi giây một lần dirty là autosave ghi PlayerPrefs vô ích; lùi ≤ 60s không ai lợi được gì |
| Lùi giờ → đứng tại mốc | đơn giản, không cần trạng thái "đang bị lùi"; countdown chỉ đứng, không âm |
| Getter ghi mốc | hệ không có nhịp riêng; caller 1 Hz duy nhất là `LiveOpsHost`. Ghi chỉ xảy ra sau `IsInitialized` và thưa 60s nên getter vẫn rẻ |
| **Giữ `if !save.IsInitialized`** dù đã là boot step | `Order` chỉ xếp thứ tự **trong** chuỗi boot. `UtcNowUnix` là getter công khai, caller ngoài chuỗi gọi được bất cứ lúc nào — `CollectionBridge` ở scene Game chẳng hạn. Bỏ guard là đọc mốc `0` rồi ghi mốc theo nó |
| Không `event OffsetChanged` | không caller |

## §4 Trước khi chạy

| Bước | Thiếu thì hỏng ở đâu |
|---|---|
| Asset save của dự án: điền Key cho field `Last Seen Utc Seconds` (nhóm Time), ví dụ `time_last_seen_utc`; bấm **Validate keys** | `LogError` "empty key" lúc `Initialize`, mốc không lưu → không chống lùi |
| `Services.unity`: object `TimeService`, Init → kéo asset save | `IService<ITimeService>.Service` ném ở caller đầu tiên |
| Kéo `TimeService` vào list `steps` của `BootstrapRunner`, `Order` **sau** `SaveBootstep` | `ResyncAsync` không bao giờ chạy; giờ vẫn đúng nhờ guard trong getter, nhưng offset server không bao giờ áp |

## Kiểm

| Ca | Kỳ vọng | Ai chạy |
|---|---|---|
| Play lần đầu | `UtcNowUnix` ≈ giờ máy; sau 60s PlayerPrefs có `persistence_<key>` | **developer** — Play mode |
| Lùi giờ máy 1 ngày rồi mở lại | `UtcNowUnix` = mốc cuối, không lùi | **developer** — Play mode |
| Tiến giờ máy | `UtcNowUnix` theo giờ máy (không chống tiến — cần server time) | **developer** — Play mode |

Hệ này không có test tự động: luật chống lùi nằm trong getter của một MonoBehaviour và đọc
`save.IsInitialized`, nên cả ba ca đều cần Play mode và một lần đổi giờ máy. Phần của agent ở đây là
biên dịch và soát code theo §3.

## §5 Để sau

`Countdown` struct · `TimeFormatter` zero-GC · resync khi resume · chặn cấp thưởng khi `!IsServerTimeTrusted` · nhịp 1 Hz chuyển sang Ticker (4a) · impl `IServerTimeProvider` (Firebase/HTTP Date).
