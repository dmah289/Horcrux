# Reward System — Plan (bản tối thiểu)

> **Loại tài liệu:** Plan — developer tự code. Là phần 14c của Economy, tách riêng vì đứng độc lập được; phần còn lại ở §4.

**Mục tiêu:** một cửa `Grant(reward, placement)` cho mọi nguồn thưởng; game cắm handler theo loại, SDK không biết coin/booster là gì.

**Kiến trúc:** 4 file.

```
Abstractions/Foundations/Reward/     RewardData.cs · IRewardHandler.cs · IRewardService.cs
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
[Service(typeof(IRewardService), FindFromScene = true)]
public sealed class RewardService : MonoBehaviour, IRewardService
```

`Dictionary<int, IRewardHandler>`. `Grant`: `Amount <= 0` → `LogError` bỏ qua; `TryGetValue` → `handler.Grant(in reward, placement)`.

| Quyết định | Vì |
|---|---|
| `Register(typeId, handler)` thay `handler.TypeId` | một handler phục vụ nhiều typeId (3 loại booster) không phải viết 3 class |
| Sync, không `UniTask` | grant là ghi số vào ví; anim thuộc caller |
| Handler đăng ký ở `Start` | `FindFromScene` resolve service khi scene đã load; `Awake` có thể sớm hơn object service |

## §3 Trước khi chạy

| Bước | Thiếu thì hỏng ở đâu |
|---|---|
| Scene sống suốt phiên: object `RewardService` | `IService<IRewardService>.Service` ném ở caller đầu |
| Game: mỗi `typeId` một dòng `Register` trong `Start` của handler | `LogError "no handler for type N"` lúc grant, thưởng không vào ví |

## Kiểm

| Ca | Kỳ vọng |
|---|---|
| Grant type có handler | ví đổi đúng amount, placement tới analytics của ví |
| Grant type không handler | một dòng `LogError`, không exception |
| Register trùng | `LogError`, handler đầu giữ |

## §4 Để sau

`GrantAsync` + fly anim · `ShowClaimPopupAsync` · `IRewardIconProvider` · `Register` trả `IDisposable` để unregister · Currency/Lives service (14a, 14b).
