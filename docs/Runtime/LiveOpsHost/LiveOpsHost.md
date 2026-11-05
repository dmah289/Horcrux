# LiveOpsHost — nhịp 1 Hz và vòng đời module live-ops

Module live-ops là component tự chứa. Host là một boot step làm ba việc: chờ tới lượt mình trong chuỗi boot, gọi
`Initialize` mỗi module một lần, rồi gọi `Tick` mỗi giây với **cùng một `now`** cho mọi module. Kèm theo là luồng
mất tiến độ khi thua (`ALiveOpsLoseFlow`) và view cảnh báo mất tiến độ.

Code: `Runtime/Abstractions/Composites/LiveOps/` (`ILiveOpsModule.cs`, `ILiveOpsHost.cs`, `ALiveOpsModule.cs`,
`LiveOpsWindow.cs`, `ALiveOpsLoseFlow.cs`, `LiveOpsLossHelper.cs`) · `Runtime/Implementations/Composites/LiveOps/`
(`LiveOpsHost.cs`, `LiveOpsHostInitializer.cs`, `WeeklySchedule.cs`, `LiveOpsLossWarningView.cs`,
`LiveOpsLossWarningSlot.cs`) · namespace `Horcrux.Runtime.Abstractions.LiveOps`, `Horcrux.Runtime.Implementations.LiveOps`
· test: `Tests/EditMode/WeeklyScheduleTests.cs`. `Priority`, `HomeFlow` và trình diễn lúc về Home ở `LiveOpsHomeFlow.md`.

## Ngữ cảnh đã chốt

| Mục | Nội dung |
|---|---|
| Người dùng | developer của game và của các live-ops thêm sau |
| Ranh giới | Horcrux giữ nhịp, cửa sổ thời gian, khung module, luật cờ mất treo. Dự án giữ luật của từng module, save, config, cầu nối báo sự kiện level |
| Trạng thái | `Inactive · Running · Finished`. Base chỉ giữ enum và `SetState`; nghĩa của `Finished` do module định |
| Lịch | tuần, neo `(dayOfWeek, hourUtc, minuteUtc)` + `durationSeconds`; hàm thuần, test EditMode |
| Nhịp | 1 Hz, `UniTask.Delay` Realtime trong host; module không có `Update` |
| Thời gian | chỉ `ITimeService.UtcNowUnix`; không `DateTime.Now` |
| Save | module tự giữ entry của mình trong collection của dự án; host không đọc save |
| Config | module nhận model đã ép kiểu từ cầu nối của dự án; host không biết remote config |
| Cố ý KHÔNG làm | `try/catch` quanh `Tick` · tick thích ứng · lịch chu kỳ N ngày · hook cheat-time (§8) |

## Trước khi chạy

Dựng từ 0 theo thứ tự; mỗi bước kèm chỗ hỏng nếu thiếu. `BootstrapService` dựng theo `Bootstrap.md`, `TimeService`
theo `TimeSystem.md`.

### 1. Host

1. Tạo GameObject **con của GameObject `BootstrapService`**, Add Component `LiveOpsHost` và `LiveOpsHostInitializer`.
2. Trên `LiveOpsHostInitializer`, ô **Time Service** để **Service**.
3. Ô **Order** của `LiveOpsHost` lớn hơn **Order** của `SaveBootstep`.
4. Kéo `LiveOpsHost` vào ô **Steps** của `BootstrapService`.

| Thiếu | Hỏng ở đâu |
|---|---|
| `LiveOpsHostInitializer` | `Init` không được gọi, `timeService` là `null`: NRE ở dòng `now` đầu tiên của `RunAsync`, trong chuỗi boot, nơi `.Forget()` chỉ để lại một dòng đỏ. Chuỗi boot vẫn chạy tiếp; host chết cả phiên |
| `TimeService` trong scene | như trên — ô **Time Service** không có gì để nhận |
| `LiveOpsHost` trong **Steps** | `InitializeAsync` không chạy → không module nào `Initialize`, không tick, widget im lặng không hiện |
| **Order** sau `SaveBootstep` | module `Initialize` trên save chưa load: `Refresh` đọc giá trị mặc định — rollover và `State` sai |
| **`FindFromScene` thấy được scene chứa host** — scene nạp bằng Addressables là nạp **sau** khi InitArgs khởi tạo | `ServiceInjector` chỉ `LogWarning "Service Not Found"` rồi trả `null`; `ILiveOpsHost.Service` và ô Init để Service ném NRE ở caller đầu |
| `BootstrapService` được game gọi `InitializeAsync()` (`Bootstrap.md` › Trước khi chạy) | không step nào chạy: save không load, host không `Initialize`, không tick |

### 2. Module

1. Viết class kế thừa `ALiveOpsModule` (mẫu ở §4) và một `*Initializer` cho nó, trong đó có ô `ILiveOpsHost`.
2. Add Component module vào GameObject **đang active** trong scene sống suốt phiên, cùng **đúng một** Initializer của
   module; ô **Target** trỏ vào module, ô `ILiveOpsHost` để **Service**.

| Thiếu | Hỏng ở đâu |
|---|---|
| Initializer, hoặc ô `ILiveOpsHost` trống | `LiveOpsHost` của module `null` → NRE ở `Start`, không một dòng log trước đó |
| GameObject active | `Start` chưa chạy → chưa `Register` → module không bao giờ `Initialize`, `State` mãi `Inactive` |
| **Initializer thừa** với **Target** trống | InitArgs `AddComponent` một module thứ hai lúc `Awake`; nó đăng ký host trước bản trong scene và không ai cấu hình nó. *Đã sai một lần:* lộ bằng NRE trong `Refresh` của module có `GetInstanceID()` âm |
| `base.Start()` / `base.OnDestroy()` khi override | thiếu `base.Start()` → không `Register`, như GameObject inactive · thiếu `base.OnDestroy()` → `Modules` giữ module đã destroy; host vẫn tick nó, `Refresh` chạm API Unity thì `MissingReferenceException` mỗi giây |

### 3. Mất tiến độ khi thua (tuỳ chọn)

1. Module có tiến độ mất được khi thua: viết class kế thừa `ALiveOpsLoseFlow` (mẫu ở §5), tạo nó trong `Init` của module
   và trả về qua `LoseFlow`.
2. Cờ `HasPendingLoss` nằm trong save của module và **ghi xuống đĩa ngay** trong setter.
3. `Refresh` xoá cờ khi sang chu kỳ mới.
4. Một cầu nối của dự án báo sự kiện level vào `LiveOpsLossHelper`:

```csharp
public sealed class LoseFlowBridge : MonoBehaviour
{
    [SerializeField] private LiveOpsHost liveOpsHost;

    // Gọi từ luồng level của game.
    public void OnReviveOffered() => LiveOpsLossHelper.BeginPendingLoss(liveOpsHost.Modules);
    public void OnRevived() => LiveOpsLossHelper.CancelPendingLoss(liveOpsHost.Modules);
    public void OnLevelLost() => LiveOpsLossHelper.CommitLoss(liveOpsHost.Modules);
    // Bỏ level giữa chừng có tính là thua không là luật của game.
    public void OnLevelAbandoned() => LiveOpsLossHelper.CommitLoss(liveOpsHost.Modules);
}
```

| Thiếu | Hỏng ở đâu |
|---|---|
| `LoseFlow` tạo trước `Start` của module | `LoseFlow` còn `null` lúc `Initialize` → `RecoverPendingLoss` bỏ qua module, im lặng: mất treo từ phiên trước không bao giờ áp |
| Ghi cờ ngay | kill app khi popup hồi sinh đang mở → cờ chưa xuống đĩa → lần boot sau không áp mất: người chơi thoát app để né thua |
| Xoá cờ khi sang chu kỳ | cờ cũ sống qua chu kỳ mới → `Initialize` áp mất lên tiến độ chu kỳ mới |
| Cầu nối gọi `CommitLoss` | thua mà không mất gì; cờ treo tới lần boot sau mới áp |

### 4. Cảnh báo mất tiến độ (tuỳ chọn)

1. Trên popup hiện lúc sắp thua (popup hồi sinh): Add Component `LiveOpsLossWarningView`. Ô `ILiveOpsHost` trong khối
   Init để **Service**.
2. Nhóm **References**: **Content** — gốc khối cảnh báo · **Rows** — các hàng ô · **Slots** — các ô, xếp theo hàng
   (hàng 1 trước) · **Warning Desc Txt** — dòng mô tả.
3. Mỗi ô: GameObject có `LiveOpsLossWarningSlot`, điền **Root**, **Icon**, **Badge Label**.
4. Nhóm **Configs**: **Shared Multiple Warning Desc** — mô tả khi từ hai live-ops trở lên đang bị đe doạ.

| Thiếu | Hỏng ở đâu |
|---|---|
| **Rows** rỗng | `DivideByZeroException` ở `OnEnable` |
| Số **Slots** chia hết cho số **Rows** | hàng bật theo `slots.Length / rows.Length` ô mỗi hàng: hàng hiện lệch với ô dùng |
| Đủ **Slots** | `LogError` "…only N slots, so the lowest priorities are left out. Add slots." — live-ops priority thấp nhất không hiện |
| View được **bật lại** mỗi lần popup hiện | view chỉ đọc lúc `OnEnable`: object luôn active thì hiện danh sách cũ |
| View bật **trước** `CommitLoss` | sau khi áp mất, `IsProgressAtStake` là `false` → `IsShown` `false`, khối cảnh báo tắt |

## §1 Bề mặt API

**`ILiveOpsHost`** — service, `[Service(typeof(ILiveOpsHost), FindFromScene = true)]` trên `LiveOpsHost`.

| Thành viên | Làm gì |
|---|---|
| `IReadOnlyList<ILiveOpsModule> Modules` | module đã đăng ký, theo thứ tự `Register` — **chưa sắp**; cần thứ tự thì sắp bằng `LiveOpsModulePriority.ByPriority` |
| `Register(ILiveOpsModule)` | thêm vào list; host đã chạy loop thì `Initialize` ngay |
| `Unregister(ILiveOpsModule)` | bỏ khỏi list |

**`ALiveOpsModule`** — `MonoBehaviour, ILiveOpsModule`.

| Thành viên | Ai | Nghĩa |
|---|---|---|
| `protected abstract ILiveOpsHost LiveOpsHost` | module trả | host nhận trong `Init` của module |
| `abstract string ModuleId` | module trả | id ổn định; dùng trong log và khi bằng `Priority` |
| `protected abstract LiveOpsWindow ResolveWindow(long nowUnix)` | module trả | cửa sổ chu kỳ chứa (hoặc vừa qua) `nowUnix`; base gán vào `Window` |
| `protected abstract void Refresh(long nowUnix)` | module viết | áp luật: rollover, `SetState`. Chạy sau khi `Window` đã gán |
| `protected virtual void OnStateChanged(old, new)` | module tuỳ | sau mỗi lần `State` đổi |
| `virtual int Priority` · `virtual ALiveOpsHomeFlow HomeFlow` | module tuỳ | mặc định `0` · `null` — xem `LiveOpsHomeFlow.md` |
| `virtual ALiveOpsLoseFlow LoseFlow` | module tuỳ | mặc định `null`: module không mất gì khi thua |
| `LiveOpsModuleState State` | đọc | `Inactive` mặc định tới lần `SetState` đầu |
| `long SecondsLeft` | đọc | `Window.SecondsLeft(LastUnix)` khi `Running`, ngược lại `0` |
| `protected bool IsInitialized` | đọc | host đã gọi `Initialize` chưa |
| `protected LiveOpsWindow Window` · `protected long LastUnix` | đọc | cửa sổ và `now` của lần đánh giá gần nhất |
| `protected void SetState(LiveOpsModuleState)` | module gọi | đổi `State`, gọi `OnStateChanged`, rồi `RequestHomeFlow()`. Cùng trạng thái thì không làm gì |
| `protected void RequestHomeFlow()` | module gọi | publish `LiveOpsHomeFlowRequested` |
| `Initialize(long)` · `Tick(long)` | host gọi | cùng một thân `Evaluate`; `Initialize` lật `IsInitialized` trước và gọi `LiveOpsLossHelper.RecoverPendingLoss(this)` sau |

**`LiveOpsWindow`** — `readonly struct`, nửa mở `[StartUnix, EndUnix)`: `Contains(now)` · `SecondsLeft(now)` không âm.

**Event (EventBus):**

| Event | Bắn khi | Payload |
|---|---|---|
| `LiveOpsSecondTick` | mỗi giây, **sau** khi mọi module đã `Tick`; không bắn lúc `Initialize` | `NowUnix` — cùng `now` mọi module vừa nhận |
| `LiveOpsHomeFlowRequested` | sau mỗi `SetState` đổi trạng thái, hoặc module gọi `RequestHomeFlow()` | — |

**`LiveOpsLossHelper`** — static, chạy trên mọi module có `LoseFlow`; mỗi module bọc `try/catch` riêng.

| Hàm | Dùng khi |
|---|---|
| `BeginPendingLoss(modules)` | thua đã xảy ra nhưng còn gỡ được (popup hồi sinh hiện) |
| `CancelPendingLoss(modules)` | đã gỡ (hồi sinh) |
| `CommitLoss(modules)` | thua chốt |
| `RecoverPendingLoss(module)` | base tự gọi trong `Initialize` — consumer không gọi |
| `IsAnyProgressAtStake(modules)` · `CollectProgressAtStake(modules, atStake)` | hỏi có gì để mất; bản `Collect` sắp theo `Priority` |

## §2 `WeeklySchedule` — hàm thuần

```csharp
long firstAnchorUnix = ((anchorDayOfWeek - EpochDayOfWeek + 7) % 7) * DaySeconds
    + anchorHourUtc * 3600L + anchorMinuteUtc * 60L;

long sinceFirstAnchor = ((nowUnix - firstAnchorUnix) % WeekSeconds + WeekSeconds) % WeekSeconds;

long start = nowUnix - sinceFirstAnchor;
long end = start + Math.Clamp(durationSeconds, 1, WeekSeconds);
```

`EpochDayOfWeek = 4`: 1970-01-01 là Thứ Năm. `anchorDayOfWeek` theo `System.DayOfWeek`: Sunday 0 … Saturday 6.
Monday 08:01 là `(1, 8, 1)`. `start` là mốc neo gần nhất **≤ `now`**, nên giữa `end` và mốc kế (vùng chết) cửa sổ vẫn là
chu kỳ vừa qua, `Contains` là `false`. `% WeekSeconds` hai lần vì `%` của C# giữ dấu số bị chia.

Mốc kiểm (anchor Monday 08:01 UTC, duration 604740 = 7 ngày − 60 s). `2026-09-07` là Thứ Hai, `00:00Z = 1788739200`.

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

## §3 Một nhịp

```
BootstrapService ── InitializeAsync (theo Order, sau SaveBootstep)
        │
        ▼
LiveOpsHost.InitializeAsync ── RunAsync(destroyCancellationToken).Forget() ── trả CompletedTask ngay
        │
        ▼
RunAsync:  now = timeService.UtcNowUnix
           mỗi module đã Register:  Initialize(now)
                                       ├─ IsInitialized = true
                                       ├─ LastUnix = now · Window = ResolveWindow(now) · Refresh(now)
                                       │                                   └─ SetState? → OnStateChanged → LiveOpsHomeFlowRequested
                                       └─ LiveOpsLossHelper.RecoverPendingLoss(this)
           isReady = true
           lặp tới khi host bị destroy:
               Delay 1000 ms Realtime
               now = timeService.UtcNowUnix
               mỗi module:  Tick(now) ── LastUnix · Window · Refresh   (cùng thân với Initialize)
               EventBus<LiveOpsSecondTick>.Publish(now)

Module Start ── Register ──┬─ isReady = false → chờ vòng Initialize ở trên
                           └─ isReady = true  → Initialize(UtcNowUnix) ngay, trong Start
Module OnDestroy ── Unregister
```

App vào nền thì player loop dừng, không tick. Lúc quay lại, tick kế đọc `now` mới; rollover xử lý như mọi tick vì
`Initialize` và `Tick` cùng một luật.

## §4 Viết một module

```csharp
public sealed class MyEventModule : ALiveOpsModule, IInitializable<ILiveOpsHost, IMySaveData>
{
    [SerializeField] private string moduleId = "my_event";
    [SerializeField] private Sprite lossIcon;

    private ILiveOpsHost liveOpsHost;
    private PersistenceDataEntry<MyEventSave> entry;
    private MyLoseFlow loseFlow;

    protected override ILiveOpsHost LiveOpsHost => liveOpsHost;
    public override string ModuleId => moduleId;
    public override ALiveOpsLoseFlow LoseFlow => loseFlow;

    public void Init(ILiveOpsHost host, IMySaveData save)
    {
        liveOpsHost = host;
        entry = save.MyEvent;                                // PersistenceDataEntry<MyEventSave>
        loseFlow = new MyLoseFlow(this, entry, lossIcon);    // trước Start: Initialize cần nó
    }

    protected override LiveOpsWindow ResolveWindow(long nowUnix)
        => WeeklySchedule.Resolve(nowUnix, 1, 8, 1, 604740); // Thứ Hai 08:01 UTC, 7 ngày − 60 s

    protected override void Refresh(long nowUnix)
    {
        if (entry.Value.cycleStartUnix != Window.StartUnix)  // chu kỳ mới
        {
            entry.Value.cycleStartUnix = Window.StartUnix;
            entry.Value.progress = 0;
            entry.Value.hasPendingLoss = false;
        }

        SetState(Window.Contains(nowUnix) ? LiveOpsModuleState.Running : LiveOpsModuleState.Inactive);
    }
}
```

| Luật | Vì |
|---|---|
| `Refresh` không tự tính cửa sổ — `ResolveWindow` trả, base gán `Window` | `SecondsLeft` đọc `Window`; `Refresh` quên gán thì countdown đứng `0m 0s` cả phiên, không lỗi nào nổ. Tách ra thì không trả về là không biên dịch được |
| Rollover so `Window.StartUnix` với mốc chu kỳ đã lưu | `Initialize` (mở app sau nhiều ngày) và `Tick` (app đang mở qua mốc) cùng đi qua đây; vùng chết vẫn là chu kỳ cũ nên không reset hai lần |
| Lệnh từ game tới trước `Initialize` thì kiểm `IsInitialized` | `Start` của module chạy lúc scene nạp, chuỗi boot chạy sau; lệnh từ game có thể tới sớm hơn cả hai. Chưa init thì `Window`, `State` là mặc định |
| Lệnh làm đổi trạng thái giữa hai tick thì gọi `Tick(now)` | `State` mới có ngay, không chờ tối đa 1 s |
| Không `Unregister` hay `DestroyImmediate` một module trong `Tick` | host lặp theo index: bỏ phần tử giữa vòng thì module kế bị bỏ qua tick đó. `Destroy` thường chạy cuối frame, an toàn |
| Widget countdown đọc `SecondsLeft` lúc bật, rồi cập nhật theo `LiveOpsSecondTick` | event không bắn lúc `Initialize`; chỉ nghe event thì widget trống tới tối đa 1 s sau khi bật |
| Module có tên event riêng thì publish trong `OnStateChanged` | `SetState` chỉ bắn `LiveOpsHomeFlowRequested`; base không mang vocabulary game |

## §5 Mất tiến độ khi thua

```csharp
public sealed class MyLoseFlow : ALiveOpsLoseFlow
{
    private readonly MyEventModule module;
    private readonly PersistenceDataEntry<MyEventSave> entry;
    private readonly Sprite icon;

    public MyLoseFlow(MyEventModule module, PersistenceDataEntry<MyEventSave> entry, Sprite icon)
    {
        this.module = module;
        this.entry = entry;
        this.icon = icon;
    }

    public override bool IsProgressAtStake => module.State == LiveOpsModuleState.Running && entry.Value.progress > 0;
    public override LiveOpsLossWarning Warning => new(icon, $"x{entry.Value.progress}", "You will lose your event progress!");

    protected override bool HasPendingLoss
    {
        get => entry.Value.hasPendingLoss;
        set
        {
            entry.Value.hasPendingLoss = value;
            entry.FlushNow();                                  // ghi ngay: kill app không được mất cờ
        }
    }

    protected override void ApplyLoss() => entry.Value.progress = 0;   // phải để IsProgressAtStake = false
}
```

Cờ `HasPendingLoss` qua bốn bước:

```
                 BeginPendingLoss                    CancelPendingLoss
  (không cờ) ─── chỉ khi IsProgressAtStake ──▶ (cờ treo) ──────────────────▶ (không cờ)
       ▲                                           │
       │                                           ├─ CommitLoss ── xoá cờ ── IsProgressAtStake? ── ApplyLoss
       │                                           │
       │                                           └─ kill app ── boot ── Initialize ── RecoverPendingLoss
       │                                                                      ├─ xoá cờ
       └──────────────────────────────────────────────────────────────────── └─ ApplyLoss (không kiểm State)
```

| Bước | Luật |
|---|---|
| `BeginPendingLoss` | dựng cờ chỉ khi có gì để mất và chưa có cờ |
| `CancelPendingLoss` | xoá cờ nếu có |
| `CommitLoss` | xoá cờ **trước**, rồi `ApplyLoss` nếu còn gì để mất — thua lúc không có gì để mất không để cờ lại cho lần boot sau |
| `RecoverPendingLoss` | có cờ thì xoá rồi `ApplyLoss`, **không kiểm `State`**: lúc boot module chưa biết level, còn cờ cũ của chu kỳ trước đã bị `Refresh` xoá ngay trước đó |

`LiveOpsLossWarning(Icon, Badge, SingleWarningDesc)`: `Badge` rỗng thì ẩn nhãn badge; `SingleWarningDesc` dùng khi chỉ
một live-ops bị đe doạ. `LiveOpsLossWarningView` lúc `OnEnable` gom module có `IsProgressAtStake` (sắp theo `Priority`),
bật **Content** nếu có, bật hàng thứ `i` khi số ô dùng vượt `i × (Slots / Rows)`, điền ô, chọn mô tả: một live-ops →
`SingleWarningDesc` của nó, nhiều hơn → **Shared Multiple Warning Desc**. `IsShown` đọc được ngay sau khi bật view.

## §6 Quyết định

| Quyết định | Vì |
|---|---|
| `Initialize` và `Tick` cùng một thân `Evaluate` | rollover lúc app đang mở và lúc mở app là một luật; hai thân sẽ lệch |
| `IsInitialized` ở base | mọi module đọc dữ liệu do boot step trước nó nạp, nên đều gặp khe giữa `Start` và `Initialize`. Cờ trả lời đúng một câu: host đã gọi `Initialize` chưa |
| `MonoBehaviour` thuần + `protected abstract ILiveOpsHost LiveOpsHost`, không `MonoBehaviour<ILiveOpsHost>` | InitArgs chỉ tự gọi **một** khe `Init` mỗi component, mà module cần nhiều hơn host (save, đồng hồ, reward). Base chiếm khe đó thì module phải gắn `*Initializer` cho phần còn lại — và `InitArgs.TryGet` **bỏ qua** service cho khe `MonoBehaviour<T>` khi thấy Initializer trên client (`HasCustomInitArguments`): host null im lặng, NRE ở `Start`. Property abstract đẩy host vào `Init` của module: không service-locator, nhận host giả được, quên host thì không biên dịch. *Đã sai một lần:* module với Initializer ba ô, host null ở `Start`, không một dòng log |
| `ILiveOpsModule` không derive `IService<>` | module khai `IMyModule : ILiveOpsModule, IService<IMyModule>`; nếu base cũng derive `IService<ILiveOpsModule>` thì hai `static Service` cùng tên → CS0229, chỉ lộ lúc có module thứ hai. Cùng lý do với `IPersistenceDataCollection` (`Persistence.md`) |
| Host là boot step | `BootstrapService` xếp `SaveBootstep` → `LiveOpsHost` bằng **Order**; host không hỏi cờ nào để biết save đã load |
| `InitializeAsync` không await vòng lặp | await là chuỗi boot đứng mãi ở step này |
| Loop dùng `destroyCancellationToken` của host, không dùng `ct` của pha | `BootstrapService` huỷ token pha mỗi lần `ReinitializeAsync` (mỗi lần load level): loop 1 Hz sẽ chết ở level thứ hai |
| `IInitializable<ITimeService>` + `LiveOpsHostInitializer`, không `ITimeService.Service` | host đọc đồng hồ ở ba chỗ; inject thì phụ thuộc nằm trong chữ ký và hỏng lộ lúc wire. `LiveOpsHost` kế thừa `ABootStep` nên không dùng được `MonoBehaviour<T>` — ca InitArgs bảo khai `*Initializer` riêng |
| Không inject save vào host | không dòng nào trong host đọc save; module tự giữ entry |
| `Register` sau khi loop chạy thì `Initialize` ngay | module ở scene nạp sau boot không phải chờ tới tick kế mới có `State` |
| `Realtime` | countdown chạy khi `timeScale = 0` |
| Không `try/catch` quanh `Tick` | lỗi phải nổ lúc dev. **Giá:** một exception giết vòng `while` — mất nhịp 1 Hz tới hết phiên (§7) |
| Bước mất tiến độ bọc `try/catch` từng module | một module ném không được chặn module khác áp hay huỷ mất của nó |

## §7 Chẩn đoán

| Triệu chứng | Nguyên nhân |
|---|---|
| Mọi countdown đứng, rollover không chạy, `LiveOpsSecondTick` im, từ một thời điểm | một module ném trong `Tick` hoặc `Initialize`: vòng `while` chết tới hết phiên. Dòng đỏ duy nhất là exception do `.Forget()` log — tìm nó ở thời điểm countdown đứng |
| Không module nào có `State`, ngay từ đầu | `LiveOpsHost` không trong **Steps**, hoặc NRE `timeService` (thiếu `LiveOpsHostInitializer`) |
| Một module `Inactive` mãi, module khác chạy | GameObject của module inactive nên chưa `Register`, hoặc override `Start` không gọi `base.Start()` |
| Countdown `0` dù đang trong chu kỳ | `State` không phải `Running` — `Refresh` chưa `SetState(Running)` |
| `[LiveOpsLossHelper]: {ModuleId} threw in {stepName}; the other modules still run.` | `LoseFlow` của module đó ném ở bước `stepName`; module khác vẫn chạy bước đó |
| `[LiveOpsLossWarningView]: N live-ops are at stake but there are only M slots, so the lowest priorities are left out. Add slots.` | thêm ô vào **Slots** |
| Thoát app khi popup hồi sinh mở, vào lại vẫn còn tiến độ | setter `HasPendingLoss` không ghi ngay, hoặc `LoseFlow` còn `null` lúc `Initialize` |

## Kiểm

| Ca | Kỳ vọng | Ai chạy |
|---|---|---|
| EditMode `WeeklyScheduleTests` | bảng §2 — mỗi dòng một test, 7 test | **agent** |
| Đổi giờ máy qua mốc anchor rồi mở app | module rollover một lần, `State` đúng | **developer** — Play mode |
| Đứng ở Home qua mốc `EndUnix` | tick kế → `Inactive`, `SecondsLeft` = 0, không số âm | **developer** — Play mode |
| Thua khi có tiến độ, kill app lúc popup hồi sinh mở, mở lại | tiến độ đã mất | **developer** — Play mode |

`ALiveOpsModule`, `LiveOpsHost` và `LiveOpsLossWarningView` không có test tự động: chúng là MonoBehaviour buộc vào nhịp
Unity và save đã load. Agent kiểm chúng bằng biên dịch; hành vi kiểm ở các dòng Play mode trên.

## §8 Để sau

`IOptionalService<T>` · tick thích ứng theo `SecondsLeft` · `EActivationTiming` · `#define` per module · asset
preload/unload · lịch chu kỳ N ngày (`CycleSchedule.Resolve` cạnh `WeeklySchedule`) · hook cheat-time · `try/catch` từng
module quanh `Tick`.
