# Plan — `SpendGate`: một cửa trả giá, thiếu thì ví quyết

Bản tạm: category-jam `Assets/_Kelsey/Core/Runtime/GameCommon/ICoinService.cs` › `CheckCoinEnoughAndConsume`, gọi ở 4 nút trả coin. Không phụ thuộc Plan nào; `PopupAddBox`, `PopupOfferRevive`, `LiveSystem_PopupRefillLiveOffer` chung với Chuyển dự án của `Horcrux_Ads_Plan.md` — chuyển Plan này trước.
Kiểm: `bash "<horcrux>/Editor/CompileCheck/Tools~/verify.sh" --csproj com.horcrux.runtime.csproj,Assembly-CSharp.csproj --tests Horcrux.EditModeTests`.

## Kịch bản

| Tình huống | Hệ làm gì | Caller thấy |
|---|---|---|
| nút trả giá, số dư đủ (kể cả bằng đúng giá) | `wallet.Consume(amount, context)` — ví ghi trừ, tracking | `true` → chạy hiệu ứng mua |
| số dư thiếu | `wallet.OnShortage(amount, context)`, không trừ | `false` → thôi; ví đã mở shop, popup nạp; số dư không đổi |
| giá 0 hay âm | `LogError`, không gọi ví | `false` — cấu hình hỏng, trừ âm thành cộng tiền |
| ngữ cảnh là struct nhiều trường (placement, nhóm tracking, placement của shop) | truyền `in` tới cả hai nhánh | ví nhận nguyên giá trị |
| trả cho thứ có trần, chỗ nhận đã đầy | không kiểm — caller kiểm trước khi gọi | caller báo đầy, không trừ, không mở shop |

## Flow

```
nút trả giá (dự án) ──TrySpend(amount, in context)──► SpendWalletExtensions.cs
   ◄── bool: true → hiệu ứng · false → thôi          │ amount <= 0 ─────► LogError, false
                                                     │ Balance < amount ─► wallet.OnShortage, false
                                                     └ đủ ──────────────► wallet.Consume, true
                                                                │
ví mỗi loại tài nguyên (dự án) ── implements ──► ISpendWallet<TContext>                   ISpendWallet.cs
   Balance · Consume → service tiền (ghi trừ, tracking) · OnShortage → shop, popup nạp
```

## Thuật toán

Mỗi lần gọi đúng một trong ba: `LogError` (amount ≤ 0) · `OnShortage`, `Balance` không đổi · `Consume`; trả `true` ⇔ đã gọi `Consume`.
Bản mẫu: ví giả trừ `Balance` thật, số dư {0, 1, 99, 100, 101, 1000} × giá {−5, 0, 1, 99, 100, 101}: 36 lượt, 0 sai · đối chứng `<` thành `<=` cho 12 sai.

---

## GĐ1 — xong thì dự án viết được ví và gọi cửa trả giá

```
ISpendWallet<TContext>+ ◄── SpendWalletExtensions.TrySpend+ ◄── SpendGateTests (ví giả)
```

`Runtime/Abstractions/Foundations/Spend/SpendWalletExtensions.cs` — file mới, gõ cửa (caller của ví) trước:

```csharp
using UnityEngine;

namespace Horcrux.Runtime.Abstractions.Spend
{
    // Extension, không service: luật không giữ state, trả lời hoàn toàn từ tham số.
    public static class SpendWalletExtensions
    {
        // in TContext: ngữ cảnh là struct nhiều trường, truyền tham chiếu không chép.
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

`Runtime/Abstractions/Foundations/Spend/ISpendWallet.cs` — file mới:

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

        // Trên ví, không là tham số TrySpend: thiếu làm gì là luật của loại tài nguyên, không của từng nút.
        /// <summary>Gọi khi thiếu, số dư chưa đổi: mở cửa hàng, popup nạp.</summary>
        void OnShortage(int amount, in TContext context);
    }
}
```

`Tests/EditMode/SpendGateTests.cs` (agent; ví giả đếm lần gọi và **trừ `Balance` thật** trong `Consume` — không trừ thì case "số dư không đổi" không đỏ được):

| Input | Kỳ vọng |
|---|---|
| Balance 100, amount 100 | `true`, `Consume` 1, `OnShortage` 0, Balance 0 — đỏ nếu `<` thành `<=` |
| Balance 99, amount 100 | `false`, `Consume` 0, `OnShortage` 1, Balance 99 |
| amount 0 · amount −5 | `false`, không gọi gì, `LogError` (`LogAssert.Expect`) |
| ngữ cảnh là struct hai trường | ví nhận đúng giá trị đã truyền ở cả hai nhánh |

---

## Chuyển dự án (agent)

| Chỗ | Đổi |
|---|---|
| `docs/Runtime/SpendGate/SpendGate.md`, `.html` | tạo, `Code: Runtime/Abstractions/Foundations/Spend/`: luồng · cách viết một ví (tên trung tính, ngữ cảnh là struct) · bảng kết quả → caller làm gì · quyết định trái trực giác · Mở rộng sau · lưu ý: cửa không kiểm chỗ nhận, giá theo kết cục không qua cửa này |
| `Assets/_CategoryJam/Bridge/CoinWallet.cs` | tạo theo khung dưới (Assembly-CSharp, cùng assembly với 4 popup) |
| `Assets/_CategoryJam/Bridge/PopupAddBox.cs` › `OnClickCoin` | `CoinWallet.Instance.TrySpend(Price, new CoinSpend(Placement, GROUP_PLACEMENT._level, Event_IAP_TRACK.PLACEMENT._popup_add_box))`, `false` thì `return`; bỏ `SetPlacement` ở caller |
| `Assets/_CategoryJam/Bridge/PopupBooster.cs` › `OnClickCoin` | như trên với `_price, _coinPlacement, GROUP_PLACEMENT._by_booster, Event_IAP_TRACK.PLACEMENT._popup_booster` |
| `Assets/_Game/Scripts/Feature/Revive/PopupOfferRevive.cs` › `OnClickRevive` | như trên với `_price, "revive", GROUP_PLACEMENT._revive, null` |
| `Assets/_Modules/LiveSystem/Scripts/LiveSystem_PopupRefillLiveOffer.cs` › `OnClickCoin` | `if (CoinWallet.Instance.TrySpend(cost, new CoinSpend("refill_live", GROUP_PLACEMENT._lives, Event_IAP_TRACK.PLACEMENT._popup_refill_live)))` bọc thân nhánh `else` cũ, bỏ nhánh `SetPlacement`; giữ `RejectIfFull()` trước cửa |
| 4 popup | giữ guard đầu hàm như cũ |
| tracking | `iap_track _show` của shop mở khi thiếu coin mang `_popup_add_box`, `_popup_booster`, `_popup_refill_live` thay placement cũ — đổi dữ liệu ghi ra có chủ ý, developer chốt |
| `Assets/_Kelsey/Core/Runtime/GameCommon/ICoinService.cs` | xoá `CheckCoinEnoughAndConsume` |
| Plan này | xoá cùng `.meta` |
| chơi thử | Cheat Economy › Set Coin 0, bấm nút coin ở popup thêm box, booster, hồi sinh, nạp mạng (chưa đầy) → shop mở, coin vẫn 0, popup còn · Set Coin 10000 → trừ đúng giá, shop không mở, thứ mua tới tay · nạp mạng khi đầy → toast đầy, không trừ, không mở shop; hỏng: coin âm, hiệu ứng mua chạy khi thiếu |

   ```csharp
   using Core;
   using Horcrux.Runtime.Abstractions.Spend;
   using Kelsey;

   /// <summary>Một lần trả coin: placement, nhóm tracking, và placement IAP đặt cho shop khi thiếu (null: không đổi).</summary>
   public readonly struct CoinSpend
   {
       public readonly string Placement;
       public readonly GROUP_PLACEMENT Group;
       public readonly Event_IAP_TRACK.PLACEMENT? ShopPlacement;

       public CoinSpend(string placement, GROUP_PLACEMENT group, Event_IAP_TRACK.PLACEMENT? shopPlacement)
       {
           Placement = placement;
           Group = group;
           ShopPlacement = shopPlacement;
       }
   }

   /// <summary>Ví coin cho SpendGate: đủ thì trừ qua service coin, thiếu thì mở shop.</summary>
   public sealed class CoinWallet : ISpendWallet<CoinSpend>
   {
       // Static, không đăng ký service: ví không giữ state, không chỗ nào cần thay ví.
       public static readonly CoinWallet Instance = new();

       public int Balance => ICoinService.Service.GetCoin();

       public void Consume(int amount, in CoinSpend spend) =>
           ICoinService.Service.ConsumeCoin(amount, spend.Placement, spend.Group);

       public void OnShortage(int amount, in CoinSpend spend)
       {
           // Placement trước khi mở: shop ghi iap_track _show ở OnEnable, ngay trong lúc mở.
           if (spend.ShopPlacement is { } placement)
               Event_IAP_TRACK.SetPlacement(placement);

           IShopService.Service.ShowPopupShop();
       }
   }
   ```

## Không làm · Mở rộng sau

| | Phương án | Vì sao |
|---|---|---|
| bỏ | luật đủ–thiếu đặt trong service tiền | service tiền phải gọi service shop — chiều phụ thuộc ngược; ví của dự án nối hai bên |
| bỏ | `virtual` trên ví để dự án đè luật kiểm | luật đổi thì là luật khác, không phải biến thể |
| bỏ | trả enum kết quả (đủ, thiếu, giá hỏng) | không caller nào rẽ nhánh theo lý do |
| bỏ | bảng `typeId → ví` như `RewardService` | mỗi nút biết mình trả bằng gì; tra theo số thêm một chỗ hỏng im lặng (thiếu đăng ký) |
| bỏ | `string placement` như `IRewardService.Grant` | tracking cần nhiều trường; chuỗi buộc dự án nhét rồi tách lại |
| bỏ | async, anim, popup trong cửa | ví nhận `OnShortage` và tự quyết; trình diễn thuộc consumer |
| bỏ | giá trả sau theo kết cục (cọc mạng) | trừ hoãn tới lúc tất toán, cọc nằm trong save — thuộc service mạng, `SystemPlan.md` §14b |
| sau | kiểm chỗ nhận (`Func<bool> canReceive`) | khi caller thứ hai trả giá cho thứ có trần; caller duy nhất (nạp mạng) đã kiểm đầy trước khi gọi |
| sau | nhiều tài nguyên một lần trả (gói giá coin + vé) | khi có gói giá như vậy |
