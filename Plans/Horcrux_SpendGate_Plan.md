# Plan — `SpendGate`: một cửa trả giá, thiếu thì ví quyết

Ngày 2026-11-23. Nguồn: category-jam `Assets/_Kelsey/Core/Runtime/GameCommon/ICoinService.cs` (`CheckCoinEnoughAndConsume`), đang
chạy ở 4 nút trả coin. Luật *đủ thì trừ, thiếu thì báo ví, không trừ* nằm ở một hàm; thiếu thì làm gì (mở shop, popup nạp) là việc
của ví — mỗi loại tài nguyên một ví do dự án viết.

**Phân công:** GĐ1 developer gõ, agent viết test theo bảng case rồi compile · GĐ2 (tài liệu) và GĐ3 (dự án chuyển sang bản Horcrux,
xoá bản tạm, xoá Plan) agent làm.
**Chạy được, thấy được:** GĐ1 compile sạch + bảng case xanh trong `Tests/EditMode/SpendGateTests.cs`; thấy trong game ở GĐ3.
**Lệnh kiểm** (chạy từ gốc dự án): `bash "<horcrux>/Editor/CompileCheck/Tools~/verify.sh" --csproj
com.horcrux.runtime.csproj,Assembly-CSharp.csproj --tests Horcrux.EditModeTests`.
**Commit:** GĐ1, GĐ2 trong `Assets/Horcrux`; GĐ3 ở dự án kèm con trỏ submodule.
**Phụ thuộc:** không. **Chung file:** `PopupAddBox`, `PopupOfferRevive`, `LiveSystem_PopupRefillLiveOffer` — `Horcrux_Ads_Plan.md` GĐ7
cũng sửa — làm GĐ3 Plan này trước Ads GĐ7.
**Đã kiểm trước khi ghi:** file GĐ1 compile bằng csc (C# 9, `-warn:4`, 0 lỗi 0 cảnh báo) trên `UnityEngine.CoreModule` 6000.0.73f1;
ví coin GĐ3 compile trên `Kelsey.dll`, `Core.dll` của dự án. Bản mẫu chạy `TrySpend` trên ví giả trừ `Balance` thật, vét số dư
{0, 1, 99, 100, 101, 1000} × giá {−5, 0, 1, 99, 100, 101}: 36 lượt, 0 sai (không lượt nào vừa trừ vừa báo thiếu; thiếu thì số dư
không đổi; ngữ cảnh tới ví nguyên vẹn). Đối chứng: đổi `<` thành `<=` cho 12 sai. `§0`: không, hệ không có toán.

---

## Ngữ cảnh đã chốt

- **Người dùng:** mọi nút trả giá bằng tài nguyên đếm được (tiền, năng lượng, vé) — mua vật phẩm, hồi sinh, nạp mạng. Nút gọi
  `TrySpend` rồi đọc `bool`: `true` thì chạy hiệu ứng, `false` thì thôi (ví đã lo phần thiếu).
- **Mục tiêu:** một chỗ giữ luật *đủ thì trừ, thiếu thì báo ví, không trừ*.
- **Ranh giới:** Horcrux giữ luật kiểm và hợp đồng ví. Dự án giữ số dư, ghi trừ, tracking, và việc làm khi thiếu.
- **Chỗ đặt, tên:** `Runtime/Abstractions/Foundations/Spend/`, namespace `Horcrux.Runtime.Abstractions.Spend` — chỉ interface và
  extension, không service, không `[Service]`. Ngữ cảnh một lần trả (placement, nhóm tracking) là **type parameter** `TContext` dự
  án khai; Horcrux không biết chuỗi hay enum nào. Tài liệu `docs/Runtime/SpendGate/`.
- **Cố ý không làm:**
  - Bảng `typeId → ví` như `RewardService`: mỗi nút biết mình trả bằng gì; tra theo số chỉ thêm một chỗ hỏng im lặng (thiếu đăng ký).
  - Kiểm chỗ nhận đầy trước khi trừ: bên nhận bỏ qua im lặng khi đầy thì người chơi mất giá. Call site duy nhất có trần (nạp mạng)
    đã kiểm đầy trước khi gọi `TrySpend`; thêm tham số khi caller thứ hai cần.
  - Async, anim, popup: ví nhận `OnShortage` và tự quyết; trình diễn thuộc consumer.
  - `string placement` như `IRewardService.Grant`: tracking cần nhiều trường (placement, nhóm, placement của shop); chuỗi buộc dự án
    nhét rồi tách lại.
  - Giá trả sau theo kết cục (cọc mạng: thua mới mất, rời giữa chừng được): khác hình — trừ hoãn tới lúc tất toán, cọc nằm trong
    save. Thuộc service mạng, `SystemPlan.md` §14b.
- **Quyết định trái trực giác:**
  - `OnShortage` nằm trên ví, không phải tham số của `TrySpend`: thiếu tiền làm gì là luật của **loại tài nguyên** (coin → shop
    coin), không của từng nút; tham số thì mỗi nút tự nhớ truyền. Phần riêng từng nút (placement của shop) đi trong `TContext`.
  - Service tiền không gọi tên service shop: ví của dự án nối hai bên; luật đặt trong service tiền thì service tiền phải gọi service
    shop — chiều phụ thuộc ngược.
  - Extension trên interface, không class service: luật không giữ state, trả lời hoàn toàn từ tham số. *Bỏ:* `virtual` trên ví để dự
    án đè luật kiểm — luật đổi thì là luật khác, không phải biến thể.
- **Mở rộng sau:** kiểm chỗ nhận (`Func<bool> canReceive`) khi caller thứ hai trả giá cho thứ có trần · nhiều tài nguyên một lần trả
  (gói giá coin + vé).

## Đã khảo sát

| Nguồn | Lấy gì | Không lấy, vì sao |
|---|---|---|
| category-jam `ICoinService.CheckCoinEnoughAndConsume` (bản tạm, default interface method) | luật đủ–thiếu đang chạy ở 4 nút | đặt trong service tiền, gọi `IShopService.ShowPopupShop()` |
| category-jam 4 popup trả coin | sau `false`, 3 nút gọi `Event_IAP_TRACK.SetPlacement` (placement IAP cho shop vừa mở); hồi sinh không gọi | việc riêng từng nút rải ở caller — chuyển vào ngữ cảnh, ví làm |
| Horcrux `Foundations/Reward` (`RewardData`, `IRewardHandler`, `RewardService`) | hình dạng vế cộng; `Amount <= 0` thì `LogError` | bảng handler theo `typeId`, `string placement` — lý do ở Cố ý không làm |
| `SystemPlan.md` §14a (`ICurrencyService.TryConsume(amount, string placement)`, helper `RequireCoin` ở game) | cửa trả `bool`, service tiền không mở UI | thiếu-tiền ở helper từng nút và `string placement` — §14a trỏ sang Plan này |
| `SystemPlan.md` §14b (cọc mạng) | ranh: giá theo kết cục không đi qua cửa này | — |

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
ISpendWallet<TContext>  ◄── implements ── ví mỗi loại tài nguyên (Balance, Consume → service tiền, OnShortage → shop)
SpendWalletExtensions.TrySpend  ◄── gọi ── các nút trả giá
```

---

## GĐ1 — Cửa trả giá trong Horcrux

**Mục tiêu:** `TrySpend` chạy trên ví giả; compile sạch, bảng case xanh. Xong thì dự án viết được ví và gọi cửa.

**Vì sao bây giờ:** hai file nhỏ, không phụ thuộc gì; caller thật là test ngay GĐ này, nút của dự án ở GĐ3.

**Sơ đồ sau GĐ** (mới: cả hai khối):

```
[mới] ISpendWallet<TContext> ◄── [mới] SpendWalletExtensions.TrySpend ◄── SpendGateTests (ví giả)
```

**Consumes:** `UnityEngine.Debug`. **Produces:** `ISpendWallet<TContext>`, `TrySpend`.

### `Runtime/Abstractions/Foundations/Spend/SpendWalletExtensions.cs` — file mới

Gõ cửa (caller của ví) trước:

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

### `Runtime/Abstractions/Foundations/Spend/ISpendWallet.cs` — file mới

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

**Quyết định:** `in TContext` — ngữ cảnh là struct nhiều trường, truyền tham chiếu không chép. *Bỏ:* trả enum kết quả (đủ, thiếu,
giá hỏng) — không caller nào rẽ nhánh theo lý do.

**Editor setup:** không.

**Kiểm** (agent viết `Tests/EditMode/SpendGateTests.cs`; ví giả đếm lần gọi và **trừ `Balance` thật** trong `Consume` — không trừ thì
case "số dư không đổi" không đỏ được):

| Input | Kỳ vọng |
|---|---|
| Balance 100, amount 100 | `true`, `Consume` 1, `OnShortage` 0, Balance 0 |
| Balance 99, amount 100 | `false`, `Consume` 0, `OnShortage` 1, Balance 99 |
| amount 0 · amount −5 | `false`, không gọi gì, `LogError` (`LogAssert.Expect`) |
| ngữ cảnh là struct hai trường | ví nhận đúng giá trị đã truyền ở cả hai nhánh |

Case đỏ được: đổi `<` thành `<=` thì case Balance 100 đỏ.

**Mốc commit:** `feat(spend): SpendGate — TrySpend trên ISpendWallet`.

---

## GĐ2 — Tài liệu (agent)

**Mục tiêu:** dự án sau viết ví và gọi cửa mà không mở code.

**Vì sao bây giờ:** API đã chốt ở GĐ1; GĐ3 xoá Plan.

**Sơ đồ sau GĐ:** không đổi code.

`docs/Runtime/SpendGate/SpendGate.md` và `.html`, dòng `Code: Runtime/Abstractions/Foundations/Spend/`. Gồm: luồng, cách viết một ví
(khung ví ví dụ tên trung tính, ngữ cảnh là struct), bảng kết quả → caller làm gì, các quyết định trái trực giác, "Mở rộng sau".
Lưu ý: cửa không kiểm chỗ nhận — caller trả cho thứ có trần kiểm trước khi gọi; giá theo kết cục không đi qua cửa này.

**Editor setup:** không.

**Kiểm:** chữ ký trong `.md` khớp GĐ1. Mốc commit: `docs(spend): tài liệu SpendGate`.

---

## GĐ3 — Dự án dùng bản Horcrux (agent)

**Mục tiêu:** 4 nút trả coin đi qua `TrySpend`; service tiền không còn gọi service shop; xoá bản tạm và Plan này.

**Sơ đồ sau GĐ:**

```
4 popup ──► TrySpend ──► [mới] CoinWallet ── Consume ──► ICoinService.ConsumeCoin
                                     └──── OnShortage ──► Event_IAP_TRACK.SetPlacement ─► IShopService.ShowPopupShop
[xoá] ICoinService.CheckCoinEnoughAndConsume
```

1. File mới `Assets/_CategoryJam/Bridge/CoinWallet.cs` (Assembly-CSharp, cùng assembly với cả 4 popup):

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

   Nguồn instance: `CoinWallet.Instance` (static readonly). Ví không giữ state, đọc `ICoinService.Service`, `IShopService.Service`
   lúc gọi; không đăng ký service vì không chỗ nào cần thay ví.

   **Thứ tự `SetPlacement` rồi mở shop** (developer chốt): bản tạm mở shop trước rồi caller mới đặt placement. Shop ghi
   `iap_track _show` ở `OnEnable` (`IapTracking_OpenShop_OnEnable`), nên với bản tạm event `_show` của shop mở từ 3 popup này mang
   placement cũ; sau GĐ3 nó mang `_popup_add_box`, `_popup_booster`, `_popup_refill_live`. Đây là đổi dữ liệu tracking ghi ra, có chủ ý.

2. Thay ở từng popup (giữ guard đầu hàm như cũ):

   | File | Đoạn có sẵn | Đoạn mới |
   |---|---|---|
   | `Assets/_CategoryJam/Bridge/PopupAddBox.cs` › `OnClickCoin` | `if (!ICoinService.Service.CheckCoinEnoughAndConsume(Price, Placement, GROUP_PLACEMENT._level))` `{ Event_IAP_TRACK.SetPlacement(Event_IAP_TRACK.PLACEMENT._popup_add_box); return; }` | `if (!CoinWallet.Instance.TrySpend(Price, new CoinSpend(Placement, GROUP_PLACEMENT._level, Event_IAP_TRACK.PLACEMENT._popup_add_box)))` `return;` |
   | `Assets/_CategoryJam/Bridge/PopupBooster.cs` › `OnClickCoin` | `if (!ICoinService.Service.CheckCoinEnoughAndConsume(_price, _coinPlacement, GROUP_PLACEMENT._by_booster))` `{ Event_IAP_TRACK.SetPlacement(Event_IAP_TRACK.PLACEMENT._popup_booster); return; }` | `if (!CoinWallet.Instance.TrySpend(_price, new CoinSpend(_coinPlacement, GROUP_PLACEMENT._by_booster, Event_IAP_TRACK.PLACEMENT._popup_booster)))` `return;` |
   | `Assets/_Game/Scripts/Feature/Revive/PopupOfferRevive.cs` › `OnClickRevive` | `if (!ICoinService.Service.CheckCoinEnoughAndConsume(_price, "revive", GROUP_PLACEMENT._revive))` `return;` | `if (!CoinWallet.Instance.TrySpend(_price, new CoinSpend("revive", GROUP_PLACEMENT._revive, null)))` `return;` |
   | `Assets/_Modules/LiveSystem/Scripts/LiveSystem_PopupRefillLiveOffer.cs` › `OnClickCoin` | `if (!_coinService.CheckCoinEnoughAndConsume(cost, "refill_live", GROUP_PLACEMENT._lives))` `{ Event_IAP_TRACK.SetPlacement(Event_IAP_TRACK.PLACEMENT._popup_refill_live); }` `else { … }` | `if (CoinWallet.Instance.TrySpend(cost, new CoinSpend("refill_live", GROUP_PLACEMENT._lives, Event_IAP_TRACK.PLACEMENT._popup_refill_live)))` `{ … }` (thân nhánh `else` cũ giữ nguyên) |

   Popup nạp mạng giữ `RejectIfFull()` trước cửa — đó là phần kiểm chỗ nhận.
3. Xoá `CheckCoinEnoughAndConsume` khỏi `Assets/_Kelsey/Core/Runtime/GameCommon/ICoinService.cs`.
4. Xoá `Plans/Horcrux_SpendGate_Plan.md` và `.meta`.

**Editor setup:** không.

**Kiểm:** grep dự án không còn `CheckCoinEnoughAndConsume`; `verify.sh` xanh với `Assembly-CSharp.csproj`.

**Kịch bản chơi thử** (Editor, Play):
1. Cheat Economy › Set Coin 0. Mở lần lượt popup thêm box, popup booster (mua bằng coin), popup hồi sinh (thua một màn), popup nạp
   mạng (mạng chưa đầy), bấm nút coin: shop mở, số coin vẫn 0, popup vẫn mở. Hỏng: số coin âm, hay hiệu ứng mua chạy.
2. Set Coin 10000, bấm lại từng nút: coin trừ đúng giá, shop không mở, thứ mua tới tay (box mở, booster cộng, chơi tiếp, mạng đầy).
3. Nạp mạng khi mạng đầy: toast báo đầy, coin không trừ, shop không mở — như trước.
4. Khác trước: không gì người chơi thấy. Tracking (xem log event nếu dự án có snapshot): `iap_track _show` của shop mở từ bước 1
   mang placement của popup vừa bấm.

**Mốc commit dự án:** `refactor(coin): nút trả coin đi qua SpendGate của Horcrux`, kèm con trỏ submodule.

---

## File → GĐ

| File | GĐ |
|---|---|
| `Runtime/Abstractions/Foundations/Spend/SpendWalletExtensions.cs` | 1 |
| `Runtime/Abstractions/Foundations/Spend/ISpendWallet.cs` | 1 |
| `Tests/EditMode/SpendGateTests.cs` | 1 (agent) |
| `docs/Runtime/SpendGate/SpendGate.{md,html}` | 2 |
| dự án `Assets/_CategoryJam/Bridge/CoinWallet.cs` (mới), `PopupAddBox.cs`, `PopupBooster.cs` | 3 |
| dự án `Assets/_Game/Scripts/Feature/Revive/PopupOfferRevive.cs`, `Assets/_Modules/LiveSystem/Scripts/LiveSystem_PopupRefillLiveOffer.cs` | 3 |
| dự án `Assets/_Kelsey/Core/Runtime/GameCommon/ICoinService.cs` | 3 |
| xoá `Plans/Horcrux_SpendGate_Plan.md` | 3 |
