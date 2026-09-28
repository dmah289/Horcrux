# LiveOps Host — Plan (bản tối thiểu)

> **Loại tài liệu:** Plan — **developer viết code lõi; agent viết test, chạy và báo kết quả.** Plan chỉ ghi
> bảng case, không dán code test. Module đầu tiên: Collection (`Assets/LiveOps/Collection/Collection_Plan.md`). Phần còn lại ở §5.

**Mục tiêu:** module live-ops là component tự chứa; host chỉ làm ba việc — chờ save load, gọi `Initialize` một lần, gọi `Tick` mỗi giây với **cùng một `now`** cho mọi module.

**Kiến trúc:** 7 file. Composite vì dựa trên Persistence + Time + EventBus.

```
Abstractions/Composites/LiveOps/     ILiveOpsModule.cs   (LiveOpsModuleState · LiveOpsSecondTick · ILiveOpsModule)
                                     LiveOpsWindow.cs · ILiveOpsHost.cs · ALiveOpsModule.cs
Implementations/Composites/LiveOps/  WeeklySchedule.cs · LiveOpsHost.cs · LiveOpsHostInitializer.cs
```

`ALiveOpsModule` ở `Abstractions/` dù có thân: là bộ khung mọi module kế thừa — cùng lý do `BasePersistenceDataCollection`
có thân mà vẫn ở `Abstractions/`. `WeeklySchedule` ở `Implementations/` (developer chốt lúc gõ, 28/09): hàm thuần nhưng là
một cách tính lịch cụ thể, `CycleSchedule` sau này đứng cạnh nó. `LiveOpsWindow` tách file riêng.

## Ngữ cảnh đã chốt

| Chốt | Nội dung |
|---|---|
| Trạng thái | `Inactive · Running · Finished`. Base chỉ giữ enum và `SetState`; nghĩa của Finished do module định (Collection: đạt step cuối giữa tuần) |
| Lịch | tuần, neo `(dayOfWeek, hourUtc, minuteUtc)` + `durationSeconds`; hàm thuần, test EditMode |
| Nhịp | 1 Hz, `UniTask.Delay` Realtime trong host; module không có `Update` |
| Thời gian | chỉ `ITimeService.UtcNowUnix`; không `DateTime.Now` |
| Save | module tự giữ entry của mình trong collection của dự án; host chỉ chờ `IsInitialized` |
| Config | module nhận model đã ép kiểu (`RemoteConfig<T>` của dự án, bridge truyền vào); host không biết RC |

## §1 Contract

`ILiveOpsModule.cs`:

```csharp
using Horcrux.Runtime.Utilities.EventBus;

namespace Horcrux.Runtime.Abstractions.Composites.LiveOps
{
    public enum LiveOpsModuleState
    {
        Inactive, Running, Finished
    }

    public readonly struct LiveOpsSecondTick : IEvent
    {
        public readonly long NowUnix;
        
        public LiveOpsSecondTick(long nowUnix)
        {
            NowUnix = nowUnix;
        }
    }
    
    public interface ILiveOpsModule
    {
         string ModuleId { get; }
         LiveOpsModuleState State { get; }
         
         void Initialize(long nowUnix);
         void Tick(long nowUnix);
    }
}
```

`LiveOpsWindow.cs`:

```csharp
using System;

namespace Horcrux.Runtime.Abstractions.Composites.LiveOps
{
    /// <summary>
    /// [StartUnix, EndUnix)
    /// </summary>
    public readonly struct LiveOpsWindow
    {
        public readonly long StartUnix;
        public readonly long EndUnix;

        public LiveOpsWindow(long startUnix, long endUnix)
        {
            StartUnix = startUnix;
            EndUnix = endUnix;
        }
        
        public bool Contains(long nowUnix)
            => nowUnix >= StartUnix && nowUnix < EndUnix;

        public long SecondsLeft(long nowUnix)
            => Math.Max(0, EndUnix - nowUnix);
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
using Horcrux.Runtime.Abstractions.Composites.LiveOps;
using UnityEngine;

namespace Horcrux.Runtime.Implementations.Composites.LiveOps
{
    public static class WeeklySchedule
    {
        public const long WeekSeconds = 604800;
        private const long DaySeconds = 86400;
        private const int EpochDayOfWeek = 4;

        public static LiveOpsWindow Resolve(long nowUnix, int anchorDayOfWeek,
            int anchorHourUtc, int anchorMinuteUtc, long durationSeconds)
        {
            long firstAnchorUnix = ((anchorDayOfWeek - EpochDayOfWeek + 7) % 7) * DaySeconds
                + anchorHourUtc * 3600L + anchorMinuteUtc * 60L;

            long sinceFirstAnchor = ((nowUnix - firstAnchorUnix) % WeekSeconds + WeekSeconds) % WeekSeconds;

            long start = nowUnix - sinceFirstAnchor;
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

## §3 `ALiveOpsModule`

```csharp
using UnityEngine;

namespace Horcrux.Runtime.Abstractions.Composites.LiveOps
{
    public abstract class ALiveOpsModule : MonoBehaviour, ILiveOpsModule
    {
        #region Properties
        
        protected abstract ILiveOpsHost LiveOpsHost { get; }
        
        public abstract string ModuleId { get; }
        
        public LiveOpsModuleState State { get; private set; }
        
        public long SecondsLeft => State == LiveOpsModuleState.Running
            ? Window.SecondsLeft(LastUnix) : 0;
        
        protected bool IsInitialized { get; private set; }
        
        protected LiveOpsWindow Window { get;  private set; }
        
        protected long LastUnix { get; private set; }
        
        #endregion

        #region Unity Callbacks

        protected virtual void Start()
        {
            LiveOpsHost.Register(this);
        }

        protected virtual void OnDestroy()
        {
            LiveOpsHost.Unregister(this);
        }

        #endregion

        #region API
        
        public void Initialize(long nowUnix)
        {
            IsInitialized = true;
            Evaluate(nowUnix);
        }

        /// <summary>
        ///  first evaluation and every tick
        /// </summary>
        public void Tick(long nowUnix)
        {
            Evaluate(nowUnix);
        }
        
        #endregion

        #region Class Methods

        protected abstract void Refresh(long nowUnix);
        
        protected abstract LiveOpsWindow ResolveWindow(long nowUnix);

        protected virtual void OnStateChanged(LiveOpsModuleState oldState, 
            LiveOpsModuleState newState) {}
        
        protected void SetState(LiveOpsModuleState nextState)
        {
            if (nextState == State)
                return;
            
            LiveOpsModuleState oldState = State;
            State = nextState;
            OnStateChanged(oldState, nextState);
        }
        
        private void Evaluate(long nowUnix)
        {
            LastUnix = nowUnix;
            Window = ResolveWindow(nowUnix);
            Refresh(nowUnix);
        }

        #endregion
    }
}
```

| Quyết định | Vì |
|---|---|
| `Initialize` và `Tick` cùng một thân `Evaluate` | rollover chu kỳ lúc app đang mở và lúc mở app là một luật; hai thân sẽ lệch |
| **Module trả về cửa sổ, base gán — `Window` có setter `private`** | `SecondsLeft` đọc thẳng `Window.SecondsLeft(LastUnix)`, nên một `Refresh` quên gán `Window` cho countdown đứng `0m 0s` cả phiên mà **không lỗi nào nổ**. Khi `Refresh` vừa phải tính vừa phải nhớ gán thì cái quên đó là chuyện của kỷ luật; tách `ResolveWindow` ra thì module **không còn cửa để quên**, vì không trả về gì là không biên dịch được |
| **`IsInitialized`** | module đọc dữ liệu do một boot step **trước** nó nạp. `Start` của module chạy lúc scene nạp, còn chuỗi boot chạy sau đó, và lệnh từ game có thể tới sớm hơn cả hai. Cờ nằm ở base vì mọi module đều gặp đúng khe hở này, và nó trả lời đúng một câu: *host đã gọi `Initialize` chưa* |
| Không generic `<TSave, TConfig>` | một module; host không cần biết type save/config |
| **`MonoBehaviour` thuần + `protected abstract ILiveOpsHost LiveOpsHost`**, không `MonoBehaviour<ILiveOpsHost>` | InitArgs chỉ tự gọi **một** khe `Init` cho mỗi component, mà mọi module đều cần nhiều hơn host (save, đồng hồ, reward). Base chiếm khe đó là module phải gắn `*Initializer` cho phần còn lại — và `InitArgs.TryGet` **bỏ qua** service tra được cho khe `MonoBehaviour<T>` ngay khi thấy một Initializer gắn lên client (`HasCustomInitArguments`): host null im lặng, NRE ở `Start`. Property abstract đẩy host vào chính `Init` của module: vẫn không service-locator, module vẫn nhận host giả được, và module quên host thì **không biên dịch được**. *Đã sai một lần:* `CollectionModule` với Initializer ba ô, `liveOpsHost` null ở `Start`, không một dòng log |
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
    [Service(typeof(ILiveOpsHost), FindFromScene = true)]
    public class LiveOpsHost : BaseBootStep, ILiveOpsHost, IInitializable<ITimeService>
    {
        private const int TickMilliseconds = 1000;

        private readonly List<ILiveOpsModule> modules = new();
        private bool isReady;

        #region API

        public override UniTask InitializeAsync(CancellationToken ct)
        {
            RunAsync(destroyCancellationToken).Forget();
            return UniTask.CompletedTask;
        }

        public void Register(ILiveOpsModule liveOpsModule)
        {
            modules.Add(liveOpsModule);
            
            if(isReady)
                liveOpsModule.Initialize(timeService.UtcNowUnix);
        }

        public void Unregister(ILiveOpsModule liveOpsModule)
        {
            modules.Remove(liveOpsModule);
        }

        #endregion

        #region Class Methods

        private async UniTaskVoid RunAsync(CancellationToken ct)
        {
            long now = timeService.UtcNowUnix;

            for (int i = 0; i < modules.Count; i++)
                modules[i].Initialize(now);
            isReady = true;

            while (!ct.IsCancellationRequested)
            {
                await UniTask.Delay(TickMilliseconds, DelayType.Realtime, cancellationToken: ct);
                
                now = timeService.UtcNowUnix;
                for(int i = 0; i < modules.Count; i++)
                    modules[i].Tick(now);
                
                EventBus<LiveOpsSecondTick>.Publish(new LiveOpsSecondTick(now));
            }
        }

        #endregion

        #region DI

        private ITimeService timeService;
        public void Init(ITimeService argument)
        {
            timeService = argument;
        }

        #endregion
    }
}
```

Kèm một `LiveOpsHostInitializer : Initializer<LiveOpsHost, ITimeService>` trong cùng thư mục — `LiveOpsHost`
kế thừa `BaseBootStep` nên không dùng được `MonoBehaviour<T>`, đó là ca mà InitArgs bảo khai class
`*Initializer` riêng.

```csharp
using Sisus.Init;
using Horcrux.Runtime.Abstractions.Time;

namespace Horcrux.Runtime.Implementations.Composites.LiveOps
{
	/// <summary>
	/// Initializer for the <see cref="LiveOpsHost"/> component.
	/// </summary>
	internal sealed class LiveOpsHostInitializer : Initializer<LiveOpsHost, ITimeService>
	{
		#if UNITY_EDITOR
		/// <summary>
		/// This section can be used to customize how the Init arguments will be drawn in the Inspector.
		/// <para>
		/// The Init argument names shown in the Inspector will match the names of members defined inside this section.
		/// </para>
		/// <para>
		/// Any PropertyAttributes attached to these members will also affect the Init arguments in the Inspector.
		/// </para>
		/// </summary>
		private sealed class Init
		{
			public ITimeService TimeService = default;
		}
		#endif
	}
}
```

| Quyết định | Vì |
|---|---|
| **`IInitializable<ITimeService>`, không `ITimeService.Service`** | host đọc đồng hồ ở ba chỗ, nên đây là phụ thuộc thật chứ không phải tiện tay. Inject thì phụ thuộc đó **nằm trong chữ ký** và hỏng lộ ra lúc wire; service-locator thì nó nấp trong thân hàm và hỏng lộ ra bằng NRE ở dòng `now` đầu tiên. Đổi lại phải có `LiveOpsHostInitializer` trên object |
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
| **Runner tự boot.** Nó gọi `InitializeAsync()` trong `Start` của chính nó, nên chuỗi chạy ngay khi scene chứa runner load xong và không có cửa "quên gọi". Ở dự án này `Start` đó nằm trên lớp con `GameBootstrapRunner` | thiếu runner trong scene thì **không step nào chạy**: save không load, host không `Initialize`, không tick |
| `Services.unity`: object `LiveOpsHost`, **kèm component `LiveOpsHostInitializer`** cấp `ITimeService` | thiếu Initializer thì `Init` không được gọi, `timeService` là `null`, và NRE nổ ở dòng `now` đầu tiên của `RunAsync` — tức ngay trong chuỗi boot, nơi `.Forget()` chỉ để lại một dòng đỏ |
| **`FindFromScene` phải thấy được `Services.unity`** — scene này nạp bằng Addressables, tức **sau** khi InitArgs khởi tạo; tiền lệ đang chạy được của dự án nằm ở scene đầu | `ServiceInjector` chỉ `LogWarning "Service Not Found"` rồi trả `null`; `IService.Service` ném NRE ở caller đầu |
| Kéo `LiveOpsHost` vào list `steps` của `BootstrapRunner`, `Order` **sau** `SaveBootstep` (`TimeService` và `RewardService` không phải step) | `InitializeAsync` không chạy → không module nào `Initialize`, không tick, widget im lặng không hiện |
| `TimeService` có trong scene (xem `TimeSystem.md`) | `ITimeService.Service` ném ở dòng `now` đầu tiên |
| Module: component kế thừa `ALiveOpsModule` đặt trong cùng scene, **kèm đúng MỘT `*Initializer` của module** có ô Target trỏ vào module và một ô `ILiveOpsHost` để **Service** | thiếu component → không ai `Register`, module không bao giờ `Initialize`; thiếu Initializer → `LiveOpsHost` null, NRE ở `Start` của module, không một dòng log trước đó; **Initializer thừa với Target trống → InitArgs `AddComponent` một module thứ hai lúc `Awake`, nó đăng ký host trước bản trong scene và không ai cấu hình nó** — *đã sai một lần*, lộ bằng NRE trong `Refresh` của module có `GetInstanceID()` âm |

## Kiểm

| Ca | Kỳ vọng | Ai chạy |
|---|---|---|
| EditMode `WeeklySchedule.Resolve` | bảng §2 — mỗi dòng một case | **agent** |
| Đổi giờ máy qua mốc anchor rồi mở app | module rollover một lần, `State` đúng | **developer** — Play mode |
| Đứng ở Home qua mốc `EndUnix` | tick kế → `Inactive`, `SecondsLeft` = 0, không số âm | **developer** — Play mode |

`ALiveOpsModule` và `LiveOpsHost` không có test tự động: cả hai là MonoBehaviour buộc vào nhịp Unity và
save đã load. Agent kiểm chúng bằng biên dịch; hành vi kiểm ở hai dòng Play mode trên.

## §5 Để sau

`IOptionalService<T>` · tick thích ứng theo `SecondsLeft` · `EActivationTiming` · `#define` per module · asset preload/unload · lịch chu kỳ N ngày (`CycleSchedule.Resolve` cạnh `WeeklySchedule`) · hook cheat-time · `try/catch` từng module khi có ≥ 2 module.
