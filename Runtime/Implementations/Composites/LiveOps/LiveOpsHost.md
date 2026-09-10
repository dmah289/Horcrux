# LiveOps Host — Plan (bản tối thiểu)

> **Loại tài liệu:** Plan — developer tự code. Module đầu tiên: Collection (`Assets/LiveOps/Collection/Collection_Plan.md`). Phần còn lại ở §5.

**Mục tiêu:** module live-ops là component tự chứa; host chỉ làm ba việc — chờ save load, gọi `Initialize` một lần, gọi `Tick` mỗi giây với **cùng một `now`** cho mọi module.

**Kiến trúc:** 8 file. Composite vì dựa trên Persistence + Time + EventBus.

```
Abstractions/Composites/LiveOps/     LiveOpsModuleState.cs · LiveOpsWindow.cs · ILiveOpsModule.cs
                                     ILiveOpsHost.cs · LiveOpsSecondTick.cs
Implementations/Composites/LiveOps/  WeeklySchedule.cs · LiveOpsModuleBase.cs · LiveOpsHost.cs
```

## Ngữ cảnh đã chốt

| Chốt | Nội dung |
|---|---|
| Trạng thái | `Inactive · Running · Finished`. Base chỉ giữ enum và `SetState`; nghĩa của Finished do module định (Collection: đạt step cuối giữa tuần) |
| Lịch | tuần, neo `(dayOfWeek, hourUtc, minuteUtc)` + `durationSeconds`; hàm thuần, test EditMode |
| Nhịp | 1 Hz, `UniTask.Delay` Realtime trong host; module không có `Update` |
| Thời gian | chỉ `ITimeService.UtcNowUnix`; không `DateTime.Now` |
| Save | module tự giữ entry của mình trong collection của dự án; host chỉ chờ `IsInitialized` |
| Config | module tự nhận (JSON string từ bridge); host không biết RC |

## §1 Contract

```csharp
namespace Horcrux.Runtime.Abstractions.LiveOps
{
    public enum LiveOpsModuleState { Inactive, Running, Finished }

    public readonly struct LiveOpsWindow
    {
        public readonly long StartUnix;
        public readonly long EndUnix;                                    // exclusive
        public LiveOpsWindow(long startUnix, long endUnix) { StartUnix = startUnix; EndUnix = endUnix; }
        public bool Contains(long now) => now >= StartUnix && now < EndUnix;
        public long SecondsLeft(long now) => Math.Max(0, EndUnix - now);
    }

    public interface ILiveOpsModule
    {
        string ModuleId { get; }
        LiveOpsModuleState State { get; }
        void Initialize(long nowUnix);      // once, after save is loaded
        void Tick(long nowUnix);            // every second while registered
    }

    public interface ILiveOpsHost : IService<ILiveOpsHost>
    {
        void Register(ILiveOpsModule module);
        void Unregister(ILiveOpsModule module);
    }

    public readonly struct LiveOpsSecondTick : IEvent     // Horcrux EventBus, for views
    {
        public readonly long NowUnix;
        public LiveOpsSecondTick(long nowUnix) => NowUnix = nowUnix;
    }
}
```

## §2 `WeeklySchedule` — hàm thuần

```csharp
public static class WeeklySchedule
{
    public const long WeekSeconds = 604800;
    private const int EpochDayOfWeek = 4;            // 1970-01-01 is Thursday

    /// <summary>Window of the cycle containing <paramref name="nowUnix"/>; anchor is the cycle start.</summary>
    public static LiveOpsWindow Resolve(long nowUnix, int anchorDayOfWeek, int anchorHourUtc,
                                        int anchorMinuteUtc, long durationSeconds);
}
```

```
anchorOffset = ((anchorDayOfWeek − EpochDayOfWeek + 7) % 7) * 86400 + anchorHourUtc * 3600 + anchorMinuteUtc * 60
sinceAnchor  = ((nowUnix − anchorOffset) % WeekSeconds + WeekSeconds) % WeekSeconds      ← mod dương
start        = nowUnix − sinceAnchor
end          = start + Clamp(durationSeconds, 1, WeekSeconds)
```

Mốc kiểm (anchor Monday 08:01 UTC, duration 604740). `2026-09-07` là Thứ Hai, `00:00Z = 1788739200`.

| `nowUnix` | Nghĩa | `StartUnix` | `Contains` |
|---|---|---|---|
| 1788771600 | Mon 09:00:00 | 1788768060 (Mon 08:01) | true |
| 1788768060 | Mon 08:01:00 | 1788768060 | true |
| 1788768059 | Mon 08:00:59 | 1788163260 (Mon tuần trước) | false — `End` = Mon 08:00:00 |
| 1788768000 | Mon 08:00:00 | 1788163260 | false |
| 1788739200 − 60 | Sun 23:59:00 | 1788163260 | true, `SecondsLeft` = 28860 |
| bất kỳ, `durationSeconds = 9e9` | duration vượt tuần | — | `End − Start` = 604800 |

Tự tính mốc khác bằng `DateTimeOffset.ToUnixTimeSeconds()` trong test, không gõ tay.

## §3 `LiveOpsModuleBase`

```csharp
public abstract class LiveOpsModuleBase : MonoBehaviour, ILiveOpsModule
{
    public abstract string ModuleId { get; }
    public LiveOpsModuleState State { get; private set; }
    protected LiveOpsWindow Window { get; set; }
    protected long LastNowUnix { get; private set; }
    public long SecondsLeft => State == LiveOpsModuleState.Running ? Window.SecondsLeft(LastNowUnix) : 0;

    public void Initialize(long nowUnix) { LastNowUnix = nowUnix; Refresh(nowUnix); }
    public void Tick(long nowUnix)       { LastNowUnix = nowUnix; Refresh(nowUnix); }

    /// <summary>One body for first evaluation and every tick: resolve window, roll cycle, set state.</summary>
    protected abstract void Refresh(long nowUnix);          // must call SetState at the end
    protected void SetState(LiveOpsModuleState next)        // no-op when unchanged, else OnStateChanged
    protected virtual void OnStateChanged(LiveOpsModuleState previous, LiveOpsModuleState next) { }

    protected virtual void Start()     => ILiveOpsHost.Service.Register(this);
    protected virtual void OnDestroy() { if (ILiveOpsHost.TryGet(out var host)) host.Unregister(this); }
}
```


| Quyết định | Vì |
|---|---|
| `Initialize` và `Tick` cùng một thân `Refresh` | rollover tuần lúc app đang mở và lúc mở app là một luật; hai thân sẽ lệch |
| Không generic `<TSave, TConfig>` | một module; host không cần biết type save/config |
| Module là MonoBehaviour | giữ prefab/asset của UI; scene Services sống suốt phiên |
| `SetState` không publish EventBus | tên event là của module; base không mang vocabulary game |

## §4 `LiveOpsHost`

```csharp
[Service(typeof(ILiveOpsHost), FindFromScene = true)]
public sealed class LiveOpsHost : MonoBehaviour<BasePersistenceDataCollection>, ILiveOpsHost
```

```
Start:      RunAsync(destroyCancellationToken).Forget()
RunAsync:   await UniTask.WaitUntil(() => save.IsInitialized, cancellationToken: ct)
            now = ITimeService.Service.UtcNowUnix
            for m in modules: m.Initialize(now); ready = true
            loop while !ct:
                await UniTask.Delay(1000, DelayType.Realtime, cancellationToken: ct)
                now = ITimeService.Service.UtcNowUnix
                for i in 0..modules.Count: modules[i].Tick(now)
                EventBus<LiveOpsSecondTick>.Publish(new(now))
Register:   modules.Add(m); if ready → m.Initialize(ITimeService.Service.UtcNowUnix)
Unregister: modules.Remove(m)
```

| Quyết định | Vì |
|---|---|
| Chờ `save.IsInitialized` thay vì thứ tự `Start` | thứ tự `Start` giữa object không xác định |
| Token = `destroyCancellationToken` của host | host sống suốt phiên trong scene Services; loop chết đúng khi host chết |
| `Realtime` | countdown chạy khi `timeScale = 0` (Home pause level) |
| Không bọc `try/catch` quanh `Tick` | một module; lỗi phải nổ lúc dev, không nuốt |
| Module không được `Unregister` trong `Tick` | vòng lặp theo index; `OnDestroy` là cửa duy nhất |

## Trước khi chạy

| Bước | Thiếu thì hỏng ở đâu |
|---|---|
| Scene Services: object `LiveOpsHost`, Init → kéo asset save của dự án | NRE trong `RunAsync` |
| `TimeService` có trong scene (xem `TimeSystem.md`) | `ITimeService.Service` ném sau khi save load |
| Module: component kế thừa `LiveOpsModuleBase` đặt trong cùng scene | không ai `Register` → module không bao giờ `Initialize` |

## Kiểm

| Ca | Kỳ vọng |
|---|---|
| EditMode `WeeklySchedule.Resolve` | bảng §2 |
| Đổi giờ máy qua mốc anchor rồi mở app | module rollover một lần, `State` đúng |
| Đứng ở Home qua mốc `EndUnix` | tick kế → `Inactive`, `SecondsLeft` = 0, không số âm |

## §5 Để sau

`IOptionalService<T>` · tick thích ứng theo `SecondsLeft` · `EActivationTiming` · `#define` per module · asset preload/unload · lịch chu kỳ N ngày (`CycleSchedule.Resolve` cạnh `WeeklySchedule`) · hook cheat-time · `try/catch` từng module khi có ≥ 2 module.
