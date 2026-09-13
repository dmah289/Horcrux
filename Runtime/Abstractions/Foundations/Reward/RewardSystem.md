# Reward System — Plan (bản tối thiểu)

> **Loại tài liệu:** Plan — **developer viết code lõi; agent viết test, chạy và báo kết quả.** Plan chỉ ghi
> bảng case, không dán code test. Là phần 14c của Economy, tách riêng vì đứng độc lập được; phần còn lại ở §4.

**Mục tiêu:** một cửa `Grant(reward, placement)` cho mọi nguồn thưởng; game cắm handler theo loại, SDK không biết coin/booster là gì.

**Kiến trúc:** 2 file. Ba contract ở §1 nằm chung một file vì cùng một hệ và cộng lại chưa tới 30 dòng.

```
Abstractions/Foundations/Reward/     IRewardService.cs   (RewardData · IRewardHandler · IRewardService)
Implementations/Foundations/Reward/  RewardService.cs
```

## Ngữ cảnh đã chốt

| Chốt | Nội dung |
|---|---|
| Caller | `CollectionModule` — grant **trước** anim, anim là của module |
| Định danh loại | `int typeId` do game khai `const` (`1 coin · 2 infinite lives (phút) · 3..5 booster`) |
| Thiếu handler | `LogError`, bỏ qua — không throw giữa chuỗi trình diễn |
| Không có | popup nhận thưởng, fly anim, icon provider, async |

## §1 Contract

```csharp
namespace Horcrux.Runtime.Abstractions.Reward
{
    public readonly struct RewardData
    {
        public readonly int TypeId;
        public readonly int Amount;
        public RewardData(int typeId, int amount) { TypeId = typeId; Amount = amount; }
    }

    public interface IRewardHandler
    {
        void Grant(in RewardData reward, string placement);
    }

    public interface IRewardService : IService<IRewardService>
    {
        void Register(int typeId, IRewardHandler handler);   // duplicate typeId → LogError, keeps the first
        void Grant(in RewardData reward, string placement);  // no handler → LogError, skipped
    }
}
```

## §2 `RewardService`

```csharp
using System.Collections.Generic;
using Horcrux.Runtime.Abstractions.Reward;
using Sisus.Init;
using UnityEngine;

namespace Horcrux.Runtime.Implementations.Reward
{
    [Service(typeof(IRewardService), FindFromScene = true)]
    public class RewardService : MonoBehaviour, IRewardService
    {
        private readonly Dictionary<int, IRewardHandler> handlers = new();
        
        public void Register(int typeId, IRewardHandler handler)
        {
            if (handler == null)
            {
                Debug.LogError($"[RewardService]: Register null handler for type {typeId}.", this);
                return;
            }

            if (!handlers.TryAdd(typeId, handler))
                Debug.LogError($"[RewardService]: Type {typeId} already has a handler.", this);
        }

        public void Grant(in RewardData reward, string placement)
        {
            if (reward.Amount <= 0)
            {
                Debug.LogError($"[RewardService]: Invalid reward amount ({reward.Amount}) for type {reward.TypeId} at {placement}.", this);
                return;
            }

            if (!handlers.TryGetValue(reward.TypeId, out IRewardHandler handler))
            {
                Debug.LogError($"[RewardService]: No handler for type {reward.TypeId} at {placement}.", this);
                return;
            }
            
            handler.Grant(reward, placement);
        }
    }
}
```

| Quyết định | Vì |
|---|---|
| `Register` chặn `handler == null` | `TryAdd(typeId, null)` thành công, rồi `Grant` ném `NullReferenceException` đúng chỗ Chốt cấm throw. Chặn ở cửa vào để lỗi nổ lúc wiring, không phải lúc trao thưởng |
| `Register(typeId, handler)` thay `handler.TypeId` | một handler phục vụ nhiều typeId (3 loại booster) không phải viết 3 class |
| Sync, không `UniTask` | grant là ghi số vào ví; anim thuộc caller |
| Handler đăng ký ở `Start` | `FindFromScene` resolve service khi scene đã load; `Awake` có thể sớm hơn object service |
| **Không** phải `BaseBootStep` | hệ này không có gì để init: không đọc save, không nhịp, không ràng thứ tự với hai step kia. Thêm nó vào `BootstrapRunner` chỉ để có một `InitializeAsync` rỗng là nói dối rằng có việc init. Nó chỉ cần **tồn tại** trong scene để `FindFromScene` thấy |

## §3 Trước khi chạy

| Bước | Thiếu thì hỏng ở đâu |
|---|---|
| Scene sống suốt phiên: object `RewardService` | `IService<IRewardService>.Service` ném ở caller đầu |
| Nếu đặt ở `Services.unity`: xác nhận `FindFromScene` thấy được scene nạp bằng Addressables (xem `LiveOpsHost.md` mục "Trước khi chạy") | `ServiceInjector` chỉ `LogWarning "Service Not Found"` rồi trả `null` — handler `Register` xong vẫn không ai gọi `Grant` |
| Game: mỗi `typeId` một dòng `Register` trong `Start` của handler | `LogError "no handler for type N"` lúc grant, thưởng không vào ví |

## Kiểm

| # | Ca | Input | Kỳ vọng | Ai chạy |
|---|---|---|---|---|
| 1 | Grant tới handler đã đăng ký | `Register(7, h)` · `Grant(new RewardData(7, 30), "test")` | `h` nhận `TypeId 7`, `Amount 30`, `placement "test"`, đúng **một** lần | **agent** |
| 2 | Grant typeId không có handler | service trống · `Grant(new RewardData(99, 1), "test")` | đúng một `LogError`, không exception | **agent** |
| 3 | Grant amount ≤ 0 | `Register(1, h)` · `Grant(new RewardData(1, 0), "test")` | đúng một `LogError`, `h` **không** được gọi | **agent** |
| 4 | Register trùng typeId | `Register(1, a)` · `Register(1, b)` · `Grant(new RewardData(1, 5), "t")` | `LogError` ở lần register thứ hai; `a` được gọi, `b` không | **agent** |
| 5 | Grant thật trong game | handler của game đã `Register` | ví đổi đúng amount, placement tới analytics của ví | **developer** — Play mode |

Log mở bằng `[RewardService]: ` nên test khớp dòng lỗi bằng tên hệ, không khớp nguyên văn câu.

## §4 Để sau

`GrantAsync` + fly anim · `ShowClaimPopupAsync` · `IRewardIconProvider` · `Register` trả `IDisposable` để unregister · Currency/Lives service (14a, 14b).
