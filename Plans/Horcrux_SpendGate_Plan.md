# Plan — SpendGate: một cửa trả giá, thiếu thì chuyển cho ví quyết

Đích: `Runtime/Abstractions/Foundations/Spend/` · namespace `Horcrux.Runtime.Abstractions.Spend` · test
`Tests/EditMode/SpendGateTests.cs` (agent viết sau khi đọc code thật). Không phụ thuộc Plan nào.

## Ngữ cảnh đã chốt

| Mục | Chốt |
|---|---|
| Người dùng | mọi nút trả giá bằng tài nguyên đếm được (tiền, năng lượng, vé): mua vật phẩm, hồi sinh, nạp mạng |
| Mục tiêu | một chỗ giữ luật *đủ thì trừ, thiếu thì báo ví, không trừ* — mỗi nút chỉ gọi `TrySpend` rồi đọc `bool` |
| Ranh giới | Horcrux giữ luật kiểm và hợp đồng ví. Dự án giữ số dư, ghi trừ, tracking, và việc làm khi thiếu (mở cửa hàng, popup nạp) |
| Từ vựng | ngữ cảnh một lần trả (placement, nhóm tracking) là **type parameter** `TContext` dự án khai; Horcrux không biết chuỗi hay enum nào |
| Chỗ đặt | `Abstractions/Foundations/Spend/`: chỉ interface và extension, không service, không `[Service]` |

**Cố ý KHÔNG làm**

| Không làm | Vì |
|---|---|
| Bảng `typeId → ví` như `RewardService` | mỗi nút biết mình trả bằng gì; tra theo số chỉ thêm một cửa hỏng im lặng (thiếu đăng ký) |
| Kiểm chỗ nhận đầy trước khi trừ | luật có ở `code-design` › Cửa trả giá; chưa có call site nào cần — thêm tham số khi có |
| Async, anim, popup | ví nhận `OnShortage` và tự quyết; trình diễn thuộc consumer |
| `string placement` như `Grant` | tracking của dự án cần nhiều trường (placement, nhóm); chuỗi buộc dự án nhét rồi tách lại |

**Quyết định trái trực giác**

| Chỗ | Vì |
|---|---|
| `OnShortage` nằm trên ví, không phải tham số của `TrySpend` | thiếu tiền làm gì là luật của **loại tài nguyên** (coin → cửa hàng coin), không của từng nút; tham số thì mỗi nút tự nhớ truyền |
| Hệ tiền không gọi tên hệ cửa hàng | ví của dự án nối hai bên; luật đặt trong service tiền thì service tiền phải gọi service cửa hàng — chiều phụ thuộc ngược |

**Mở rộng sau:** kiểm chỗ nhận (`Func<bool> canReceive`) khi một nút trả giá cho thứ có trần · nhiều tài nguyên một lần trả (gói giá coin + vé).

## Đã khảo sát

| Nguồn | Lấy gì | Không lấy, vì sao |
|---|---|---|
| `Foundations/Reward` (`RewardData`, `IRewardHandler`) | hình dạng vế cộng; kiểm `Amount <= 0` thì `LogError` | bảng handler theo `typeId`, `string placement` — lý do ở bảng trên |
| Dự án: `ICoinService.CheckCoinEnoughAndConsume` | luật đủ–thiếu đang chạy đúng ở 4 nút | đặt trong service tiền, gọi `IShopService` |

## Luồng

```
nút bấm ──► wallet.TrySpend(amount, context)
               │ amount <= 0 ─► LogError, false
               │ Balance < amount ─► wallet.OnShortage(amount, context), false
               └ đủ ─► wallet.Consume(amount, context), true
nút đọc bool: true thì làm hiệu ứng; false thì thôi (ví đã lo phần thiếu)
```

## Bản đồ hệ cuối

```
Horcrux                                   Dự án
ISpendWallet<TContext>  ◄── implements ── CoinWallet (Balance, Consume → service tiền, OnShortage → cửa hàng)
SpendWalletExtensions.TrySpend  ◄── gọi ── các nút trả giá
```

## §0 — luật và phép kiểm

Không có toán. Bảo đảm: **không bao giờ vừa trừ vừa báo thiếu**, và **thiếu thì số dư không đổi**. Kiểm bằng ví giả đếm
số lần `Consume`, `OnShortage`.

## Giai đoạn 1 — cửa trả giá trong Horcrux

**Mục tiêu:** có `TrySpend` chạy được trên ví giả; compile sạch, bảng case xanh.

**Vì sao trước:** mọi bước sau gọi nó.

`Runtime/Abstractions/Foundations/Spend/ISpendWallet.cs` (file mới):

```csharp
namespace Horcrux.Runtime.Abstractions.Spend
{
    /// <summary>
    /// Một loại tài nguyên trả được (tiền, năng lượng). TContext là thứ dự án cần ghi kèm mỗi lần trả (placement, nhóm tracking).
    /// </summary>
    public interface ISpendWallet<TContext>
    {
        int Balance { get; }

        /// <summary>Chỉ được gọi khi Balance đủ; ghi trừ và tracking.</summary>
        void Consume(int amount, in TContext context);

        /// <summary>Gọi khi thiếu, số dư chưa đổi: mở cửa hàng, popup nạp.</summary>
        void OnShortage(int amount, in TContext context);
    }
}
```

`Runtime/Abstractions/Foundations/Spend/SpendWalletExtensions.cs` (file mới):

```csharp
using UnityEngine;

namespace Horcrux.Runtime.Abstractions.Spend
{
    public static class SpendWalletExtensions
    {
        /// <summary>Đủ thì trừ và trả true; thiếu thì báo ví, không trừ, trả false.</summary>
        public static bool TrySpend<TContext>(this ISpendWallet<TContext> wallet, int amount, in TContext context)
        {
            if (amount <= 0)
            {
                // giá 0 hay âm là cấu hình hỏng: trừ âm thành cộng tiền
                Debug.LogError($"[SpendGate]: Invalid amount ({amount}).");
                return false;
            }
            if (wallet.Balance < amount)
            {
                wallet.OnShortage(amount, context);
                return false;
            }
            wallet.Consume(amount, context);
            return true;
        }
    }
}
```

*Quyết định:* extension trên interface, không class service — luật không giữ state, `code-design` › Đóng gói ở đâu: trả lời
từ tham số thì là static. *Bỏ:* `virtual` trên ví để dự án đè luật kiểm — luật đổi thì là luật khác, không phải biến thể.

**Kiểm** (agent viết test, ví giả đếm lần gọi):

| Input | Kỳ vọng |
|---|---|
| Balance 100, amount 100 | true, `Consume` 1 lần, `OnShortage` 0 |
| Balance 99, amount 100 | false, `Consume` 0, `OnShortage` 1, Balance 99 |
| amount 0 · amount −5 | false, không gọi gì, `LogError` (`LogAssert.Expect`) |
| context là struct hai trường | ví nhận đúng giá trị đã truyền ở cả hai nhánh |

**Commit:** `feat(spend): SpendGate — TrySpend trên ISpendWallet`.

## Giai đoạn 2 — dự án chuyển sang SpendGate

**Mục tiêu:** 4 nút trả coin đi qua `TrySpend`; service tiền không còn gọi service cửa hàng. Thấy trong game: y như trước.

Agent làm giai đoạn này sau khi developer gõ xong giai đoạn 1:

1. Dự án khai `readonly struct CoinSpend { string Placement; GROUP_PLACEMENT Group; }` và `CoinWallet : ISpendWallet<CoinSpend>`
   (`Balance` → `ICoinService.GetCoin()`, `Consume` → `ConsumeCoin(amount, Placement, Group)`, `OnShortage` →
   `IShopService.ShowPopupShop()`), ở lớp nối của dự án.
2. Thay `CheckCoinEnoughAndConsume(price, placement, group)` ở `PopupAddBox`, `PopupBooster`, `PopupOfferRevive`,
   `LiveSystem_PopupRefillLiveOffer` bằng `CoinWallet.TrySpend(price, new CoinSpend(placement, group))`.
3. Xoá `CheckCoinEnoughAndConsume` khỏi `ICoinService`. Nghiệm thu: grep không còn tên cũ, compile sạch.

**Kịch bản chơi thử:** mỗi nút, 0 coin → cửa hàng mở, số dư không đổi; đủ coin → trừ đúng giá, không mở cửa hàng.

## File → giai đoạn

| File | Giai đoạn |
|---|---|
| `Runtime/Abstractions/Foundations/Spend/ISpendWallet.cs` | 1 |
| `Runtime/Abstractions/Foundations/Spend/SpendWalletExtensions.cs` | 1 |
| `Tests/EditMode/SpendGateTests.cs` | 1 (agent) |
| Dự án: ví coin, 4 popup, `ICoinService` | 2 (agent) |
