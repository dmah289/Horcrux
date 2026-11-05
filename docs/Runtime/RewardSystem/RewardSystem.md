# Reward — một cửa `Grant` cho mọi nguồn thưởng

Mọi nguồn thưởng gọi một hàm `Grant(reward, placement)`. Dự án cắm một `IRewardHandler` cho mỗi `typeId`; handler ghi
số vào ví của dự án. Horcrux không biết coin hay booster là gì. `Grant` đồng bộ, không anim.

Code: `Runtime/Abstractions/Foundations/Reward/IRewardService.cs` (`RewardData`, `IRewardHandler`, `IRewardService`) ·
`Runtime/Implementations/Foundations/Reward/RewardService.cs` · namespace `Horcrux.Runtime.Abstractions.Reward`,
`Horcrux.Runtime.Implementations.Reward` · test: `Tests/EditMode/RewardServiceTests.cs`.

## Ngữ cảnh đã chốt

| Mục | Nội dung |
|---|---|
| Ranh giới | Horcrux giữ: struct dữ liệu, bảng `typeId → handler`, guard. Dự án giữ: danh sách `typeId`, handler ghi vào ví, analytics theo `placement`, anim và popup nhận thưởng |
| Định danh loại | `int typeId` dự án khai `const`; Horcrux không có enum loại thưởng |
| Thứ tự | caller grant **trước** anim; anim chỉ trình diễn số đã vào ví |
| Lỗi | input sai → `LogError` rồi bỏ qua; service không ném giữa chuỗi trình diễn |
| Cố ý KHÔNG làm | async · anim, popup, icon · hàng đợi chờ handler · unregister · `try/catch` quanh handler |

## Trước khi chạy

Dựng từ 0 theo thứ tự; mỗi bước kèm chỗ hỏng nếu thiếu.

1. **Service.** Trong scene sống suốt phiên, tạo GameObject, Add Component `RewardService`. Không cần Initializer, không
   kéo vào ô **Steps** của `BootstrapService`.
2. **Loại thưởng.** Dự án khai mỗi loại một `const int`:
   ```csharp
   public static class RewardTypes
   {
       public const int Coin = 1;
       public const int Booster = 2;
   }
   ```
3. **Handler.** Mỗi loại một handler, đăng ký trong `Start`, cùng scene sống suốt phiên với service:
   ```csharp
   public sealed class CoinRewardHandler : MonoBehaviour, IRewardHandler
   {
       private void Start() => IRewardService.Service.Register(RewardTypes.Coin, this);

       public void Grant(in RewardData reward, string placement)
       {
           // Ghi reward.Amount vào ví; placement tới analytics của ví.
       }
   }
   ```
   Một handler phục vụ nhiều loại: gọi `Register` mỗi `typeId` một lần, trong `Grant` rẽ nhánh theo `reward.TypeId`.
4. **Caller.** Grant trước, anim sau:
   ```csharp
   IRewardService.Service.Grant(new RewardData(RewardTypes.Coin, 50), "level_complete");
   ```

| Thiếu | Hỏng ở đâu |
|---|---|
| `RewardService` trong scene | InitArgs `LogWarning "Service Not Found: There is no 'RewardService' found in the active scene …"`; `IRewardService.Service` ném `NullReferenceException` ở caller đầu — thường là `Start` của handler |
| **`FindFromScene` thấy được scene chứa service** — scene nạp bằng Addressables là nạp **sau** khi InitArgs khởi tạo (`LiveOpsHost.md` › Trước khi chạy) | như trên: `ServiceInjector` trả `null`, handler không đăng ký được |
| `Register` của handler cho `typeId` đó | `[RewardService]: No handler for type N at P.` lúc grant; thưởng không vào ví |
| handler cùng đời sống với service | xem §4 dòng "handler ở scene ngắn hơn" |
| grant trước anim | người chơi thoát giữa anim thì thưởng chưa ghi, mất |

## 1. API

| Gọi | Ai gọi, khi nào | Hành vi |
|---|---|---|
| `new RewardData(int typeId, int amount)` | caller | `readonly struct`, hai field `TypeId`, `Amount` |
| `IRewardService.Register(int typeId, IRewardHandler handler)` | handler, trong `Start` | `handler` null → `LogError`, bỏ · `typeId` đã có → `LogError`, **giữ bản đầu** |
| `IRewardService.Grant(in RewardData reward, string placement)` | caller, trước anim | `Amount ≤ 0` → `LogError`, bỏ · không có handler → `LogError`, bỏ · còn lại gọi `handler.Grant(reward, placement)` |
| `IRewardHandler.Grant(in RewardData reward, string placement)` | service | nhận nguyên `reward` và `placement` của caller, cùng call stack |

`placement` là chuỗi tự do cho analytics; service không kiểm, `null` đi thẳng tới handler.

## 2. Luồng

```
handler.Start ── Register(typeId, this) ──► RewardService
                                              ├─ handler null ?   ──yes──► LogError "Register null handler …", return
                                              ├─ typeId đã có ?   ──yes──► LogError "Type N already has a handler.", giữ bản đầu
                                              └─ handlers[typeId] = handler

caller ── Grant(reward, placement) ──► RewardService
                                         ├─ Amount ≤ 0 ?            ──yes──► LogError "Invalid reward amount …", return
                                         ├─ handlers[TypeId] có ?   ──no───► LogError "No handler for type …", return
                                         └─ handler.Grant(reward, placement)     ← đồng bộ, cùng frame
                                                   │
                                                   ▼
                                    ví của dự án đổi số · analytics theo placement
caller ── Grant trả về ──► anim, popup nhận thưởng
```

`Amount` được kiểm **trước** `typeId`: `typeId` lạ với `Amount 0` chỉ ra dòng "Invalid reward amount".

## 3. Vòng đời

| Giai đoạn | Chuyện gì xảy ra |
|---|---|
| Scene chứa service load | `RewardService` có mặt, bảng rỗng; không init, không đọc save |
| `Start` của handler | `Register` thêm một dòng vào bảng |
| Mỗi `Grant` | tra bảng, gọi handler, trả về trong cùng frame |
| Handler destroy | **vẫn nằm trong bảng** — không có unregister |
| Service destroy | bảng mất theo object |

Grant trước `Register` của handler không được xếp hàng: thưởng mất, chỉ còn dòng log.

## 4. Guard và bẫy

| Chỗ | Sự thật |
|---|---|
| **Grant trước `Start` của handler** | grant trong `Awake`, hay luồng boot chạy trước `Start`, ra `No handler for type …`; thưởng mất, không hàng đợi. Grant từ luồng chạy sau `Start` của mọi handler |
| **Handler ở scene ngắn hơn service** | scene unload: bảng giữ handler đã destroy; grant gọi vào nó, chạm API Unity thì `MissingReferenceException`. Scene load lại: `Register` ra `Type N already has a handler.` và **giữ bản đã chết**. Handler sống cùng scene với service |
| **Handler ném exception** | service không `try/catch`: exception lên tới caller, phần sau `Grant` của caller (anim, popup) không chạy. Handler gặp input sai thì log, không ném |
| **`Register` null bị chặn ở cửa** | `TryAdd(typeId, null)` thành công, `Grant` sau đó mới ném `NullReferenceException` — đúng chỗ Ngữ cảnh đã chốt cấm ném. Chặn ở `Register` để lỗi nổ lúc wiring |
| **Thứ tự kiểm** | `Amount` trước `typeId` (§2) |

## 5. Quyết định

| Câu hỏi | Tiêu chí | Chốt |
|---|---|---|
| `Register(typeId, handler)` hay `handler.TypeId` | một handler phục vụ nhiều loại không phải viết nhiều class | `Register(typeId, handler)` |
| Sync hay `UniTask` | grant là ghi số vào ví; anim thuộc caller | sync |
| Handler đăng ký ở `Awake` hay `Start` | `FindFromScene` resolve service khi scene đã load; `Awake` có thể sớm hơn object service | `Start` |
| `RewardService` có là `ABootStep` không | có gì để init: không đọc save, không nhịp, không ràng thứ tự với step khác | không — một `InitializeAsync` rỗng là nói dối rằng có việc init; service chỉ cần **tồn tại** trong scene để `FindFromScene` thấy |
| Thiếu handler thì ném hay log | không ném giữa chuỗi trình diễn | `LogError`, bỏ qua |

## 6. Chẩn đoán

| Log | Nghĩa | Sửa |
|---|---|---|
| `[RewardService]: No handler for type N at P.` | không handler nào `Register` cho `N`, hoặc grant chạy trước `Start` của handler | bước 3; §4 dòng đầu |
| `[RewardService]: Invalid reward amount (A) for type N at P.` | `Amount ≤ 0` | sửa số ở caller hoặc config |
| `[RewardService]: Type N already has a handler.` | hai handler cùng `typeId`, hoặc scene chứa handler load lại | §4 dòng hai |
| `[RewardService]: Register null handler for type N.` | caller `Register` với `null` | truyền handler thật |
| `Service Not Found: There is no 'RewardService' found in the active scene …` | không có `RewardService` lúc InitArgs tìm | bước 1 |
| `NullReferenceException: No globally accessible Service of type IRewardService was found.` | `IRewardService.Service` gọi khi service không có | bước 1 |

## Mở rộng

`GrantAsync` kèm fly anim · `ShowClaimPopupAsync` · `IRewardIconProvider` · `Register` trả `IDisposable` để unregister
(gỡ bẫy handler ở scene ngắn hơn service).

## Chữ ký

**`Horcrux.Runtime.Abstractions.Reward`**

| Type | Thành viên |
|---|---|
| `RewardData` | `readonly struct` · `readonly int TypeId` · `readonly int Amount` · `RewardData(int typeId, int amount)` |
| `IRewardHandler` | `void Grant(in RewardData reward, string placement)` |
| `IRewardService : IService<IRewardService>` | `void Register(int typeId, IRewardHandler handler)` · `void Grant(in RewardData reward, string placement)` |

**`Horcrux.Runtime.Implementations.Reward`** — `RewardService : MonoBehaviour, IRewardService`,
`[Service(typeof(IRewardService), FindFromScene = true)]`; bảng `Dictionary<int, IRewardHandler>`.

## Cấu trúc file

```
Runtime/Abstractions/Foundations/Reward/
└── IRewardService.cs    RewardData · IRewardHandler · IRewardService — chung file, cộng lại chưa tới 30 dòng

Runtime/Implementations/Foundations/Reward/
└── RewardService.cs     bảng typeId → handler, guard, log
```

## Tests

`Tests/EditMode/RewardServiceTests.cs`, EditMode. Log mở bằng `[RewardService]: ` nên test khớp dòng lỗi bằng tên hệ, không
khớp nguyên văn câu.

| Ca | Input | Kỳ vọng |
|---|---|---|
| Grant tới handler đã đăng ký | `Register(7, h)` · `Grant(new RewardData(7, 30), "test")` | `h` nhận `TypeId 7`, `Amount 30`, `placement "test"`, đúng **một** lần |
| Grant `typeId` không có handler | service trống · `Grant(new RewardData(99, 1), "test")` | đúng một `LogError`, không exception |
| Grant `Amount ≤ 0` | `Register(1, h)` · `Grant(new RewardData(1, 0), "test")` | đúng một `LogError`, `h` **không** được gọi |
| `Register` trùng `typeId` | `Register(1, a)` · `Register(1, b)` · `Grant(new RewardData(1, 5), "t")` | `LogError` ở lần thứ hai; `a` được gọi, `b` không |
