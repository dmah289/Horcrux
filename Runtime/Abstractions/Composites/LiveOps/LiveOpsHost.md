# LiveOps Host — Plan (bản tối thiểu)

> **Loại tài liệu:** Plan — **developer viết code lõi; agent viết test, chạy và báo kết quả.** Plan chỉ ghi
> bảng case, không dán code test. Module đầu tiên: Collection (`Assets/LiveOps/Collection/Collection_Plan.md`). Phần còn lại ở §5.

**Mục tiêu:** module live-ops là component tự chứa; host chỉ làm ba việc — chờ save load, gọi `Initialize` một lần, gọi `Tick` mỗi giây với **cùng một `now`** cho mọi module.

**Kiến trúc:** 5 file. Composite vì dựa trên Persistence + Time + EventBus.

```
Abstractions/Composites/LiveOps/     ILiveOpsModule.cs   (LiveOpsModuleState · LiveOpsWindow · LiveOpsSecondTick · ILiveOpsModule)
                                     ILiveOpsHost.cs · WeeklySchedule.cs
Implementations/Composites/LiveOps/  LiveOpsModuleBase.cs · LiveOpsHost.cs
```

`WeeklySchedule` ở `Abstractions/` dù có thân: hàm thuần, không phụ thuộc Unity, và là thứ module (cũng ở tầng
trên) gọi thẳng — cùng lý do `BasePersistenceDataCollection` có thân mà vẫn ở `Abstractions/`.

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

`ILiveOpsModule.cs`:

```csharp
using System;
using Horcrux.Runtime.Utilities.EventBus;

namespace Horcrux.Runtime.Abstractions.Composites.LiveOps
{
    public enum LiveOpsModuleState { Inactive, Running, Finished }

    /// <summary>[StartUnix, EndUnix) of one cycle. Immutable; a module replaces it, never edits it.</summary>
    public readonly struct LiveOpsWindow
    {
        public readonly long StartUnix;
        public readonly long EndUnix;                                    // exclusive

        public LiveOpsWindow(long startUnix, long endUnix)
        {
            StartUnix = startUnix;
            EndUnix = endUnix;
        }

        public bool Contains(long now) => now >= StartUnix && now < EndUnix;
        public long SecondsLeft(long now) => Math.Max(0, EndUnix - now);
    }

    /// <summary>Published by the host once per second, after every module has ticked. For views.</summary>
    public readonly struct LiveOpsSecondTick : IEvent
    {
        public readonly long NowUnix;
        public LiveOpsSecondTick(long nowUnix) => NowUnix = nowUnix;
    }

    public interface ILiveOpsModule
    {
        string ModuleId { get; }
        LiveOpsModuleState State { get; }
        void Initialize(long nowUnix);      // once, after save is loaded
        void Tick(long nowUnix);            // every second while registered
    }
}
```

`ILiveOpsHost.cs`:

```csharp
namespace Horcrux.Runtime.Abstractions.Composites.LiveOps
{
    public interface ILiveOpsHost : IService<ILiveOpsHost>
    {
        void Register(ILiveOpsModule liveOpsModule);
        void Unregister(ILiveOpsModule liveOpsModule);
    }
}
```

## §2 `WeeklySchedule` — hàm thuần

```csharp
using System;

namespace Horcrux.Runtime.Abstractions.Composites.LiveOps
{
    /// <summary>Weekly cycle windows. Pure: same inputs, same window. See LiveOpsHost.md §2.</summary>
    public static class WeeklySchedule
    {
        public const long WeekSeconds = 604800;
        private const long DaySeconds = 86400;
        private const int EpochDayOfWeek = 4;            // 1970-01-01 is Thursday; DayOfWeek numbering, Sunday = 0

        /// <summary>Window of the cycle containing <paramref name="nowUnix"/>; the anchor is the cycle start.</summary>
        public static LiveOpsWindow Resolve(long nowUnix, int anchorDayOfWeek, int anchorHourUtc,
                                            int anchorMinuteUtc, long durationSeconds)
        {
            long anchorOffset = ((anchorDayOfWeek - EpochDayOfWeek + 7) % 7) * DaySeconds
                                + anchorHourUtc * 3600L + anchorMinuteUtc * 60L;

            // C# % keeps the sign of the dividend: before the first anchor after the epoch it would go negative.
            long sinceAnchor = ((nowUnix - anchorOffset) % WeekSeconds + WeekSeconds) % WeekSeconds;

            long start = nowUnix - sinceAnchor;
            long end = start + Math.Clamp(durationSeconds, 1, WeekSeconds);
            return new LiveOpsWindow(start, end);
        }
    }
}
```

`anchorDayOfWeek` theo `System.DayOfWeek`: Sunday 0 … Saturday 6. Monday 08:01 là `(1, 8, 1)`.

Mốc kiểm (anchor Monday 08:01 UTC, duration 604740). `2026-09-07` là Thứ Hai, `00:00Z = 1788739200`.

| `nowUnix` | Nghĩa | `StartUnix` | `Contains` |
|---|---|---|---|
| 1788771600 | Mon 09:00:00 | 1788768060 (Mon 08:01) | true |
| 1788768060 | Mon 08:01:00 | 1788768060 | true |
| 1788768059 | Mon 08:00:59 | 1788163260 (Mon tuần trước) | false — `End` = Mon 08:00:00 |
| 1788768000 | Mon 08:00:00 | 1788163260 | false |
| 1788739200 − 60 | Sun 23:59:00 | 1788163260 | true, `SecondsLeft` = 28860 |
| bất kỳ, `durationSeconds = 9e9` | duration vượt tuần | — | `End − Start` = 604800 |
| −5 | trước epoch — mod phải ra dương | `Start ≤ −5` và `Start + WeekSeconds > −5` | — |

Tự tính mốc khác bằng `DateTimeOffset.ToUnixTimeSeconds()` trong test, không gõ tay.

## §3 `LiveOpsModuleBase`

```csharp
using Horcrux.Runtime.Abstractions.Composites.LiveOps;
using UnityEngine;

namespace Horcrux.Runtime.Implementations.Composites.LiveOps
{
    /// <summary>Skeleton of one live-ops module: state, window, one Refresh body. See LiveOpsHost.md §3.</summary>
    public abstract class LiveOpsModuleBase : MonoBehaviour, ILiveOpsModule
    {
        public abstract string ModuleId { get; }
        public LiveOpsModuleState State { get; private set; }
        public long SecondsLeft => State == LiveOpsModuleState.Running ? Window.SecondsLeft(LastNowUnix) : 0;

        protected LiveOpsWindow Window { get; set; }
        protected long LastNowUnix { get; private set; }

        public void Initialize(long nowUnix)
        {
            LastNowUnix = nowUnix;
            Refresh(nowUnix);
        }

        public void Tick(long nowUnix)
        {
            LastNowUnix = nowUnix;
            Refresh(nowUnix);
        }

        /// <summary>
        /// One body for the first evaluation and every tick: resolve the window, roll the cycle, set the state.
        /// Must assign <see cref="Window"/> — <see cref="SecondsLeft"/> reads it, and a default window counts down from 0 —
        /// then end with <see cref="SetState"/>.
        /// </summary>
        protected abstract void Refresh(long nowUnix);

        protected void SetState(LiveOpsModuleState next)
        {
            if (next == State)
                return;

            LiveOpsModuleState previous = State;
            State = next;
            OnStateChanged(previous, next);
        }

        protected virtual void OnStateChanged(LiveOpsModuleState previous, LiveOpsModuleState next) { }

        #region Unity callbacks
        // Start, not Awake: FindFromScene resolves the host once the scene is loaded; Awake may run before the host object exists.
        protected virtual void Start() => ILiveOpsHost.Service.Register(this);

        protected virtual void OnDestroy()
        {
            if (ILiveOpsHost.TryGet(out ILiveOpsHost host))
                host.Unregister(this);
        }
        #endregion
    }
}
```

| Quyết định | Vì |
|---|---|
| `Initialize` và `Tick` cùng một thân `Refresh` | rollover tuần lúc app đang mở và lúc mở app là một luật; hai thân sẽ lệch |
| `Window` là property của base, `Refresh` phải gán | `SecondsLeft` đọc thẳng `Window.SecondsLeft(LastNowUnix)`. Module giữ kết quả `Resolve` trong một biến cục bộ thì `Window` đứng ở `default` — `EndUnix = 0` — và mọi countdown hiện `0m 0s` cả phiên, không lỗi nào nổ |
| Không generic `<TSave, TConfig>` | một module; host không cần biết type save/config |
| Module là MonoBehaviour | giữ prefab/asset của UI; scene Services sống suốt phiên |
| Host là boot step, không phải MonoBehaviour tự lo `Start` | `BootstrapRunner` trong `Services.unity` xếp `SaveBootstep` (0) → `LiveOpsHost` (20) bằng `Order`, nên host không phải hỏi cờ nào để biết save đã load. Thứ tự do runner bảo đảm, không do cờ. (`TimeService` **không** ở trong chuỗi này — nó là `MonoBehaviour<T>` thường, tự hỏi `IsInitialized` trong getter, xem `TimeSystem.md`) |
| `SetState` không publish EventBus | tên event là của module; base không mang vocabulary game |
| `ILiveOpsModule` **không** derive `IService<>` | module thứ hai sẽ khai `IXxxModule : ILiveOpsModule, IService<IXxxModule>`. Nếu `ILiveOpsModule` cũng derive `IService<ILiveOpsModule>` thì hai `static Service` cùng tên gặp nhau → CS0229, và chỉ lộ ra lúc có module thứ hai. Đây đúng là lý do `IPersistenceDataCollection` cũng không derive nó (xem `Persistence.md`) |

## §4 `LiveOpsHost`

```csharp
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Horcrux.Runtime.Abstractions.Bootstrap;
using Horcrux.Runtime.Abstractions.Composites.LiveOps;
using Horcrux.Runtime.Abstractions.Time;
using Horcrux.Runtime.Utilities.EventBus;
using Sisus.Init;

namespace Horcrux.Runtime.Implementations.Composites.LiveOps
{
    /// <summary>Gives every module the same clock: Initialize once after save, Tick once a second. See LiveOpsHost.md §4.</summary>
    [Service(typeof(ILiveOpsHost), FindFromScene = true)]
    public class LiveOpsHost : BaseBootStep, ILiveOpsHost
    {
        private const int TickMilliseconds = 1000;

        private readonly List<ILiveOpsModule> modules = new();
        private bool isReady;

        public override UniTask InitializeAsync(CancellationToken ct)
        {
            // Own lifetime, not the phase token: the runner cancels that one at every level load.
            // Not awaited: the loop never ends, and the boot chain must move on.
            RunAsync(destroyCancellationToken).Forget();
            return UniTask.CompletedTask;
        }

        public void Register(ILiveOpsModule liveOpsModule)
        {
            modules.Add(liveOpsModule);

            // Late joiner (a scene loaded after boot): give it the first evaluation the others already had.
            if (isReady)
                liveOpsModule.Initialize(ITimeService.Service.UtcNowUnix);
        }

        public void Unregister(ILiveOpsModule liveOpsModule) => modules.Remove(liveOpsModule);

        private async UniTaskVoid RunAsync(CancellationToken ct)
        {
            // Save is loaded: Order puts this step after SaveBootstep.
            long now = ITimeService.Service.UtcNowUnix;
            for (int i = 0; i < modules.Count; i++)
                modules[i].Initialize(now);
            isReady = true;

            while (!ct.IsCancellationRequested)
            {
                await UniTask.Delay(TickMilliseconds, DelayType.Realtime, cancellationToken: ct);

                // One read per tick: two modules must never disagree on which second it is.
                now = ITimeService.Service.UtcNowUnix;
                for (int i = 0; i < modules.Count; i++)
                    modules[i].Tick(now);

                EventBus<LiveOpsSecondTick>.Publish(new LiveOpsSecondTick(now));
            }
        }
    }
}
```

| Quyết định | Vì |
|---|---|
| **Không** `IInitializable<BasePersistenceDataCollection>` | không dòng nào trong host đọc save: chờ `IsInitialized` đã bỏ, module tự giữ entry của mình. Inject vào rồi để đó là một field chết và một ô Init phải kéo trong scene. Cần lại thì thêm vào, cùng lúc với dòng code đọc nó |
| `Register` sau khi loop đã chạy thì `Initialize` ngay | module ở scene nạp sau boot (Home load thêm) không được chờ tới tick kế mới có `State`; `isReady` lật một lần sau vòng `Initialize` đầu |
| Không còn chờ `save.IsInitialized` | host là **boot step** với `Order` **sau** `SaveBootstep`, nên save đã load lúc `InitializeAsync` chạy. Thứ tự do runner bảo đảm, không do cờ |
| `InitializeAsync` **không** await vòng lặp | await là chuỗi boot đứng mãi ở step này. Nó bắn `.Forget()` rồi trả `CompletedTask` ngay |
| Loop dùng `destroyCancellationToken`, **không** dùng `ct` của pha | runner refresh token mỗi lần `ReinitializeAsync`, tức mỗi lần load level — loop 1 Hz sẽ chết ngay ở level thứ hai. Đúng kể cả khi game **chưa** gọi `ReinitializeAsync` lần nào: hôm nay không ai gọi, ngày mai có, và loop không được phụ thuộc vào điều đó |
| Token = `destroyCancellationToken` của host | host sống suốt phiên trong scene Services; loop chết đúng khi host chết |
| `Realtime` | countdown chạy khi `timeScale = 0` (Home pause level) |
| Không bọc `try/catch` quanh `Tick` | một module; lỗi phải nổ lúc dev, không nuốt. **Giá phải trả, ghi ra để không ai ngạc nhiên:** một exception giết luôn vòng `while`, nên mất hẳn nhịp 1 Hz **cho tới hết phiên** — countdown đứng, rollover không chạy, `LiveOpsSecondTick` im. `.Forget()` có log đúng một dòng đỏ; nối được dòng đó với "countdown đứng từ lúc đó" là việc của người đọc log. Thêm module thứ hai thì đổi (§5) |
| Module không được `Unregister` trong `Tick` | vòng lặp theo index; `OnDestroy` là cửa duy nhất |

## Trước khi chạy

| Bước | Thiếu thì hỏng ở đâu |
|---|---|
| **`BootstrapRunner` phải có người gọi `InitializeAsync()`.** Runner cố ý không tự boot — nó không biết game muốn boot lúc nào. Chỗ gọi phải đứng **sau** khi scene chứa runner đã load xong, vì `FindFromScene` chưa thấy gì trước đó; và `await` thay vì `.Forget()` để có thứ tự xác định với phần khởi động còn lại của game | **không step nào chạy**: save không load, host không `Initialize`, không tick — và không một dòng log nào nói vì sao |
| `Services.unity`: object `LiveOpsHost`. **Không có Init** — host không đọc save, chỉ đứng sau `SaveBootstep` bằng `Order` | thiếu object thì dòng dưới; không có gì để kéo vào Init |
| **`FindFromScene` phải thấy được `Services.unity`** — scene này nạp bằng Addressables, tức **sau** khi InitArgs khởi tạo; tiền lệ đang chạy được của dự án nằm ở scene đầu | `ServiceInjector` chỉ `LogWarning "Service Not Found"` rồi trả `null`; `IService.Service` ném NRE ở caller đầu |
| Kéo `LiveOpsHost` vào list `steps` của `BootstrapRunner`, `Order` **sau** `SaveBootstep` (`TimeService` và `RewardService` không phải step) | `InitializeAsync` không chạy → không module nào `Initialize`, không tick, widget im lặng không hiện |
| `TimeService` có trong scene (xem `TimeSystem.md`) | `ITimeService.Service` ném ở dòng `now` đầu tiên |
| Module: component kế thừa `LiveOpsModuleBase` đặt trong cùng scene | không ai `Register` → module không bao giờ `Initialize` |

## Kiểm

| Ca | Kỳ vọng | Ai chạy |
|---|---|---|
| EditMode `WeeklySchedule.Resolve` | bảng §2 — mỗi dòng một case | **agent** |
| Đổi giờ máy qua mốc anchor rồi mở app | module rollover một lần, `State` đúng | **developer** — Play mode |
| Đứng ở Home qua mốc `EndUnix` | tick kế → `Inactive`, `SecondsLeft` = 0, không số âm | **developer** — Play mode |

`LiveOpsModuleBase` và `LiveOpsHost` không có test tự động: cả hai là MonoBehaviour buộc vào nhịp Unity và
save đã load. Agent kiểm chúng bằng biên dịch; hành vi kiểm ở hai dòng Play mode trên.

## §5 Để sau

`IOptionalService<T>` · tick thích ứng theo `SecondsLeft` · `EActivationTiming` · `#define` per module · asset preload/unload · lịch chu kỳ N ngày (`CycleSchedule.Resolve` cạnh `WeeklySchedule`) · hook cheat-time · `try/catch` từng module khi có ≥ 2 module.
