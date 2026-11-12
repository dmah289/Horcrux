# Plan — Ads lên Horcrux: `AdService` (rewarded, interstitial, banner) và bộ chuyển Zego MAX

Ngày 2026-11-12. Nguồn: category-jam, bản tạm `Assets/_Core/Ads/IAdService.cs` (contract), `Assets/_Modules/Sdk/Ads/ZegoAdService.cs`
(bản cài), luật kịch bản của dự án ở `Assets/_Game/Scripts/Ads/` (`InterstitialScenarios`, `RewardedScenarios`, `AdLevelGate`,
`GameplayBanner`), tài liệu `Assets/_Modules/Sdk/Ads/Ads.md`. Bản tạm đang chạy ở bốn nút rewarded (Free của popup thêm mạng, Free
của popup hồi sinh, Get x2 của popup thắng, nút xem quảng cáo của popup mở thêm box), hai kịch bản interstitial (sau popup thắng,
trước khi chơi lại) và banner đáy màn gameplay.

**Phân công:** developer gõ GĐ1–5. Agent viết test theo bảng case rồi compile, làm GĐ6 (tài liệu) và GĐ7 (chuyển dự án sang bản
Horcrux, xoá bản tạm). Ở GĐ1–4, **"chạy được, thấy được"** nghĩa là compile sạch và bảng case xanh trong
`Tests/PlayMode/AdServiceTests.cs`, chạy với vendor giả. GĐ5 chỉ cần compile sạch, vì sự kiện MAX là static event, chỉ máy thật bắn
được. Phần thấy trong game nằm ở GĐ7. GĐ1–6 commit trong `Assets/Horcrux`; GĐ7 commit ở dự án, kèm con trỏ submodule. Plan không
phụ thuộc Plan nào khác: dải đáy chừa cho banner là `BottomInset` của `Horcrux_ViewportAnchor_Plan.md` GĐ2; chưa gõ Plan đó thì GĐ7
giữ bản tạm `BannerSpace` của dự án. Không có `§0` vì hệ không có toán.

> **Gõ liền GĐ1–5 rồi mới Play game.** `AdService` nhận `IAdVendor` qua InitArgs, mà bản cài vendor tới GĐ5 mới có. Giả định (cần
> xác nhận ở GĐ7): Play giữa chừng thì InitArgs báo thiếu dependency lúc boot, hoặc không tạo service.

## Ngữ cảnh đã chốt

- **Người dùng:**
  - popup của dự án gọi `ShowRewardedAsync` khi người chơi bấm nút xem quảng cáo nhận thưởng;
  - luật kịch bản của dự án gọi `ShowInterstitialAsync` ở chỗ chuyển cảnh, sau khi tự quyết mốc level và giãn cách;
  - luật banner của dự án gọi `ShowBanner` khi vào màn đã qua mốc, `HideBanner` khi rời màn hay mở màn cần trống đáy (Shop), và
    đọc `BannerHeightPixels` một lần lúc vào màn để chừa dải đáy;
  - dự án gắn một listener hiện chữ báo "không có quảng cáo";
  - dự án sau dùng Zego MAX thì không phải viết thêm gì; dự án dùng SDK khác thì chỉ tự cài `IAdVendor`.
- **Mục tiêu:**
  - Rewarded: một lời gọi `await` trả về `Earned` / `Skipped` / `NotAvailable` sau khi quảng cáo đã **đóng**. Dữ liệu được ghi lúc
    SDK báo nhận thưởng; phần trình diễn chạy sau khi `await` trả về.
  - Interstitial: một lời gọi `await` xong khi quảng cáo đóng, hoặc xong ngay khi bỏ qua. Caller không cần biết vì sao bỏ.
  - Banner: dự án chỉ nói **muốn** hay **không muốn** banner. Service tự ẩn nó khi quảng cáo toàn màn chiếu, khi đã remove ads, và
    giữ ý muốn tới lúc SDK nhận lệnh được.
- **Một service cho mọi quảng cáo toàn màn** (developer đã chốt). Rewarded và interstitial dùng chung hai thứ: cờ "đang chiếu"
  (mỗi lúc một quảng cáo) và đồng hồ giãn cách (mốc là lúc quảng cáo toàn màn gần nhất **đóng**). Tách hai service thì hai thứ
  này phải có chủ thứ ba và hai service phải báo nhau. Banner ở cùng service vì nó đọc chính cờ "đang chiếu" đó.
- **Chỗ đặt và tên** (developer đã chốt):
  - Contract nằm ở `Runtime/Abstractions/Foundations/Ads/`, namespace `Horcrux.Runtime.Abstractions.Ads`: `IAdService`,
    `RewardedAdResult`, `IAdVendor`.
  - Lõi nằm ở `Runtime/Implementations/Foundations/Ads/AdService.cs`, namespace `Horcrux.Runtime.Implementations.Ads`.
  - Bộ chuyển nằm ở `Vendors/Zego/ZegoAdVendor.cs`, namespace `Horcrux.Vendors.Zego`. Asmdef `com.horcrux.zego` tự bật khi dự án có
    package `com.zego.advertisement.max`, giống `Vendors/Firebase`.
  - Tài liệu ở `docs/Runtime/AdsSystem/`.
- **Ranh giới:**

  | Bên | Giữ gì |
  |---|---|
  | Lõi | **khi nào** chiếu: mỗi lúc một quảng cáo, đồng hồ giãn cách, rewarded đợi sẵn sàng 2 s, chốt kết quả, đợi thưởng về muộn 0,5 s, bỏ thưởng về sau khi đã chốt; banner: một cửa đối chiếu ý muốn của dự án với cờ chiếu, remove ads, SDK nhận lệnh |
  | Vendor | **chiếu thế nào**, báo lúc quảng cáo đóng, trạng thái remove ads của SDK, lúc SDK bắt đầu nhận lệnh banner, chiều cao banner |
  | Dự án | placement, chữ báo lỗi, phần thưởng, trình diễn, khởi tạo SDK, **luật kịch bản**: mốc level của interstitial, nút rewarded và banner, số giây giãn cách, lượt nào không chiếu, màn nào muốn banner, chừa dải đáy |

  Phương án bỏ:
  - Dồn hết vào bộ chuyển Zego. Ít file hơn, nhưng không test được ngoài máy thật.
  - Service tự đọc mốc level và giãn cách. Service phải biết level và tập user của dự án; dự án sau có cách phân tập khác thì phải
    sửa Horcrux.
- **Quyết định trái trực giác** — đọc trước khi tối ưu:
  - **Giãn cách interstitial tính từ lúc quảng cáo toàn màn gần nhất đóng, kể cả rewarded.** Người chơi vừa xem x2 rồi rời popup
    thắng thì interstitial tự bỏ, không cần luật riêng ở dự án.
  - **Interstitial đầu phiên hiện được ngay.** Chưa có mốc đóng nào thì không đợi.
  - **Đồng hồ thật, không lưu qua phiên.** Tắt app mở lại thì interstitial đầu tiên hiện được ngay.
  - **Số giây giãn cách là tham số của lời gọi, không phải cấu hình của service.** Số này đổi theo tập user và remote config; dự án
    giữ nó, service chỉ so.
  - **Interstitial bỏ qua im lặng, không bắn sự kiện.** Người chơi không bấm gì, nên không có gì phải báo.
  - **Remove ads kiểm trước khi kiểm sẵn sàng.** Nếu ngược lại, người đã mua bị báo cho SDK là "mất cơ hội chiếu", làm sai số liệu
    fill.
  - **Caller huỷ token không huỷ lượt chiếu.** Quảng cáo đã hiện thì không huỷ được. Lượt chiếu chạy tới lúc đóng để cờ và đồng hồ
    đúng, thưởng không bị lạc; caller chỉ nhận `OperationCanceledException`.
  - **`onEarned` có thể chạy khi quảng cáo còn che màn.** Ở đó chỉ ghi dữ liệu. Lợi ích: người chơi tắt app ngay lúc đó vẫn
    không mất thưởng.
  - **Thưởng về sau khi lượt đã chốt `Skipped` thì bỏ.** Lúc đó popup đã mở lại nút, cấp thưởng lúc này là cấp ngoài luồng.
  - **Vendor Zego nghe sự kiện đóng của cả Interstitial trong lượt rewarded.** Khi remote key
    `compare_with_inter_when_show_rewarded_permission` = `"1"`, Zego chiếu interstitial thay rewarded nếu eCPM cao hơn và báo "nhận
    thưởng" lúc interstitial đóng. Chỉ nghe Rewarded thì lượt chiếu treo, và mọi nút xem quảng cáo hỏng tới khi tắt app.
  - **Kiểm sẵn sàng cùng frame ngay trước `Show`.** Zego gặp rewarded chưa sẵn thì thoát im lặng và không bắn sự kiện nào.
  - **Banner là ý muốn, không phải lệnh.** `ShowBanner` chỉ ghi ý muốn. Banner hiện khi cả ba đúng: không có quảng cáo toàn màn,
    chưa remove ads, SDK nhận lệnh được. Mỗi mốc đổi một điều kiện gọi cùng một cửa đối chiếu; cửa chỉ gửi SDK khi kết quả đổi.
  - **Lệnh banner tới trước khi MAX khởi tạo xong bị Zego bỏ im lặng.** Vendor báo `BannerAvailable` **một frame** sau sự kiện khởi
    tạo của MAX: Zego tạo banner trong handler của chính sự kiện đó, thứ tự handler không định.
  - **`BannerHeightPixels` có trước khi banner tải.** Chiều cao adaptive chỉ phụ thuộc bề ngang màn, nên dự án chừa dải đáy lúc vào
    màn. Dải giữ nguyên tới hết màn dù banner ẩn (Shop, quảng cáo toàn màn, mua remove ads): bàn chơi neo world một lần lúc dựng,
    dời HUD mà world đứng yên thì hai bên lệch nhau.
  - **Mua remove ads giữa phiên: dự án gọi `HideBanner` ở cửa ghi remove ads.** Vendor không có sự kiện đổi remove ads; lần
    `ShowBanner` sau đó vẫn bị `IsAdsRemoved` chặn. Remove ads chặn interstitial và banner, không chặn rewarded.
- **Cố ý KHÔNG làm:**
  - giới hạn số lần xem rewarded, số interstitial mỗi phiên — chưa có yêu cầu;
  - màn "Ad break" đếm ngược trước interstitial;
  - vòng chờ hiện trên UI trong 2 s đợi rewarded — nút chỉ bị chặn; popup tự thêm nếu cần;
  - tắt tiếng game khi quảng cáo chạy;
  - cheat bỏ qua quảng cáo;
  - hạn giờ cho một lượt chiếu — SDK không báo đóng là lỗi SDK; thêm hạn giờ thì phải đoán một con số mà mọi quảng cáo dài đều
    vượt;
  - interstitial đợi sẵn sàng — người chơi đang chuyển cảnh, bắt họ đợi để xem quảng cáo là tệ hơn mất một lượt;
  - sửa lỗi của Zego ở chế độ compare: interstitial hiển thị lỗi cũng bị tính là nhận thưởng (mục "Lưu ý" ở GĐ5);
  - banner trên đỉnh màn, MREC, collapsible — chưa có thiết kế nào dùng;
  - sự kiện đổi chiều cao banner — dải chốt lúc vào màn, không ai nghe nó giữa màn.
- **Mở rộng sau:**
  - **Thu dải đáy khi banner tải lỗi.** Cần vendor báo tải xong, tải lỗi; hiện dải luôn bằng chiều cao dự kiến.
  - **Compare chọn được interstitial khi rewarded chưa sẵn.** `IsRewardedVideoReady` của Zego chỉ hỏi rewarded, nên lõi trả
    `NotAvailable` dù interstitial có thể lấp chỗ. Làm khi bật key đó.
  - **Giãn cách theo Day Active, kịch bản theo tập user** (`SystemPlan.md` §21): thuộc lớp luật của dự án, truyền số giây vào
    `ShowInterstitialAsync`; service không đổi.

## Đã khảo sát

| Nguồn | Lấy gì | Không lấy, vì sao |
|---|---|---|
| bản tạm `ZegoAdService` | cả cơ chế: cờ chiếu và đồng hồ chung, đợi 2 s bằng đồng hồ thật, lượt chiếu thuộc service, đợi thưởng muộn 0,5 s, bỏ thưởng về sau khi đã chốt, remove ads kiểm trước; banner: ý muốn + một cửa đối chiếu, chỉ gửi khi kết quả đổi, đợi MAX khởi tạo thêm một frame, chiều cao adaptive × mật độ, Editor 168 px | gọi `INotificationService` của dự án → đổi thành sự kiện `RewardedNotAvailable` · rewarded chỉ nghe Rewarded → nghe thêm Interstitial · gọi thẳng Zego → tách qua `IAdVendor` · cửa banner nằm trong bản cài → lên lõi để test được với vendor giả |
| bản tạm `InterstitialScenarios`, `RewardedScenarios`, `AdLevelGate` (dự án) | hình dạng lời gọi: dự án quyết mốc level, đọc giãn cách, truyền placement; popup hỏi mốc lúc mở để ẩn nút rewarded | cả lớp: mốc level, tập user, lượt play test là domain dự án, ở lại dự án |
| bản tạm `GameplayBanner`, `BannerSpace` (dự án) | hình dạng lời gọi banner: chừa dải đáy lúc vào màn bằng `BannerHeightPixels`, `ShowBanner` khi qua mốc, `HideBanner` ở Menu và Shop, dải giữ tới hết màn | mốc `banner_level`, Shop: domain dự án · `BannerSpace` (nâng mép dưới safe area cho HUD và world) lên Horcrux theo `Horcrux_ViewportAnchor_Plan.md` GĐ2 |
| `SystemPlan.md` §13 | impl chỉ phát sự kiện, không chạm UI; thưởng idempotent theo lượt chiếu; banner lộ chiều cao (GĐ4) | `IRemoveAdsService`, IAP: chưa có caller; remove ads lúc này đọc từ SDK qua vendor |
| `SystemPlan.md` §21 | giãn cách interstitial là số từ remote, đổi theo tập user | `MonetizationRules`, Day Active: dự án đã có hệ phân tập riêng |
| Zego `IAdService` (`com.zego.core`) | `IsRewardedVideoReady`, `ShowRewardedVideo(placement, onReceive)`, `IsInterstitialReady`, `ShowInterstitial(placement, onClose)`, `AdStatic.RaiseAdShowFailNotReady` | `AdFullscreenClosedEvent`: rewarded chỉ bắn khi đóng, không bắn khi hiển thị lỗi hay khi chạy nhánh compare |
| Zego `AdServiceBase`, `InterstitialDefault`, `AdUnit` | `ShowInterstitial` gọi `onClose` cả khi remove ads, chưa sẵn, đóng, hiển thị lỗi; trả `false` khi không chiếu | — |
| Zego `IRemoveAdsStatus` | `Exist && Global.IsRemoveAds`; dự án đăng ký bản cài | — |
| Zego `AdMaxDefault` | nhánh compare chiếu interstitial bằng `interstitialAd.Show(onReceive)` | — |
| `MaxSdkCallbacks` | `Rewarded`/`Interstitial` `.OnAdHiddenEvent`, `.OnAdDisplayFailedEvent`, mặc định bắn trên main thread · `OnSdkInitializedEvent` | — |
| `MaxSdk`, `MaxSdkUtils` | `IsInitialized()` · `GetAdaptiveBannerHeight()` (dp trên Android, point trên iOS, Editor trả 50) × `GetScreenDensity()` | — |
| Zego `IAdService` banner | `ShowBanner()`, `HideBanner()`; banner dựng trong handler `OnSdkInitializedEvent` của Zego, lệnh tới trước đó bị bỏ im lặng | `IsBannerShowing`: lõi tự nhớ thứ đã gửi |
| Horcrux `Vendors/Firebase` | khuôn asmdef `versionDefines` + `defineConstraints`, service thuần `[Service(typeof(...))]` | — |
| Horcrux `Bootstrap` | — | bước boot cho `Init` của Zego: `BootstrapService` chạy sau fetch remote config, còn MAX phải khởi tạo trước để có thêm thời gian tải quảng cáo |
| InitArgs `ServiceAttribute` | service thuần nhận service khác qua `IInitializable<T>` | — |

## Luồng mô tả

**Rewarded — một lần bấm "xem quảng cáo nhận thưởng":**

```
popup gọi ShowRewardedAsync(placement, onEarned, ct) ──► đang chiếu quảng cáo? → NotAvailable ngay        GĐ1
                    │
                    ▼
     đợi vendor.IsRewardedReady ≤ 2 s (đồng hồ thật)
        hết giờ → vendor.ReportRewardedNotReady · RewardedNotAvailable(placement) → listener dự án        GĐ2
                    │ sẵn
                    ▼
     await vendor.ShowRewardedAsync(placement, OnReward)                                              GĐ1
        OnReward (SDK báo nhận thưởng) → onEarned đúng một lần
        trả false (hiển thị lỗi)       → RewardedNotAvailable(placement)                               GĐ2
        trả true  (đã đóng)            → đặt mốc giãn cách                                              GĐ3
                                        → chưa có thưởng thì đợi ≤ 0,5 s → Earned | Skipped             GĐ1
```

**Interstitial — một lần chuyển cảnh có luật quảng cáo:**

```
dự án (đã qua mốc level) gọi ShowInterstitialAsync(placement, minIntervalSeconds, ct)                  GĐ3
     đang chiếu · remove ads · chưa đủ minIntervalSeconds từ mốc đóng → xong ngay
     vendor chưa sẵn → vendor.ReportInterstitialNotReady → xong ngay
     await vendor.ShowInterstitialAsync(placement)
        trả true (đóng hoặc hiển thị lỗi) → đặt mốc giãn cách
        trả false (SDK không chiếu)       → không đặt mốc
     → caller đi tiếp
```

**Banner — một màn có banner:**

```
dự án vào màn đã qua mốc: đọc BannerHeightPixels, chừa dải đáy, gọi ShowBanner                          GĐ4
     ShowBanner | HideBanner ──► ghi ý muốn ──► ApplyBanner
     quảng cáo toàn màn sắp hiện, vừa đóng ──► ApplyBanner                                               GĐ4
     vendor.BannerAvailable (MAX khởi tạo xong + 1 frame) ──► ApplyBanner                                GĐ5
ApplyBanner: ý muốn · không quảng cáo toàn màn · chưa remove ads → hiện, ngược lại ẩn
     SDK chưa nhận lệnh, hoặc kết quả bằng lần gửi trước → không gửi; khác → vendor.ShowBanner | HideBanner
dự án rời màn, mở Shop, ghi remove ads ──► HideBanner
```

Sau đó: Zego MAX cho cả ba (GĐ5) · tài liệu (GĐ6) · dự án dùng bản Horcrux, listener chữ báo lỗi, xoá bản tạm (GĐ7).

## Bản đồ hệ cuối

```
popup (dự án) ─────────ShowRewardedAsync────┐
luật kịch bản (dự án) ─ShowInterstitialAsync┤
luật banner (dự án) ───ShowBanner · HideBanner · BannerHeightPixels
                                            ▼
                              AdService : IAdService                       [Service], InitArgs tạo
                               │  cờ đang chiếu · mốc đóng gần nhất · ý muốn banner · banner đã gửi
                               │  Init(IAdVendor) ◄──────────── ZegoAdVendor : IAdVendor        [Service]
                               │                                 ├─ Zego IAdService: ShowRewardedVideo · ShowInterstitial · ShowBanner · HideBanner
                               │                                 ├─ Zego IRemoveAdsStatus
                               │                                 ├─ MaxSdkCallbacks Rewarded|Interstitial (lượt rewarded)
                               │                                 └─ MaxSdkCallbacks.OnSdkInitializedEvent → BannerAvailable · MaxSdkUtils (chiều cao)
                               └─ event RewardedNotAvailable(placement) ──► AdNotAvailableToast (dự án, Service.unity)
                                                                              └─ INotificationService.ShowNotification
luật banner (dự án) : IBottomInsetProvider ──► BottomInset (Horcrux Layout) ──► panel safe area HUD · ViewportCamera
LoadingController (dự án) ──► Zego IAdService.Init()      prefab Zego.Advertisement.MaxDefault ở scene đầu
```

Lõi không biết Zego; vendor không biết placement nào dẫn tới thưởng gì; dự án không gọi tên Zego ở chỗ xem quảng cáo. `Zego.IAdService`
trùng tên với `IAdService` của Horcrux: chỉ bộ chuyển Zego cầm cả hai, nó gọi bản Zego qua alias.

---

## Giai đoạn 1 — Rewarded: chiếu một lượt và chốt kết quả

**Mục tiêu:** vendor sẵn sàng thì `ShowRewardedAsync` chiếu, đợi quảng cáo đóng rồi trả `Earned` hoặc `Skipped`. `onEarned` chạy
đúng một lần, kể cả khi SDK báo thưởng sau báo đóng hoặc báo hai lần. Gọi lần hai trong lúc lượt trước chưa đóng thì bị từ chối.

**Vì sao bây giờ:** đây là hợp đồng bốn popup rewarded dựa vào. Ca biên chỗ này (thứ tự thưởng–đóng, thưởng trùng, huỷ giữa chừng) là nơi mất
tiền thật. Cờ "đang chiếu" sinh ở đây; GĐ3 cho interstitial dùng chung nó.

```
test (vendor giả) ──ShowRewardedAsync──► AdService ──ShowRewardedAsync──► FakeVendor
                                           ▲  OnReward ◄────────────────────┘ (test bắn trước/sau đóng)
                                           └─ Earned | Skipped | NotAvailable (chưa sẵn, hiển thị lỗi, đang chiếu)
```

### `Runtime/Abstractions/Foundations/Ads/RewardedAdResult.cs` — file mới

```csharp
namespace Horcrux.Runtime.Abstractions.Ads
{
    public enum RewardedAdResult
    {
        /// <summary>The SDK granted the reward; onEarned has run.</summary>
        Earned,
        /// <summary>The player closed the ad before the reward.</summary>
        Skipped,
        /// <summary>No ad was shown: none ready in time, display failed, or another ad was showing.</summary>
        NotAvailable,
    }
}
```

Enum này không vào save hay asset, nên không cần đánh số tường minh.

### `Runtime/Abstractions/Foundations/Ads/IAdVendor.cs` — file mới

```csharp
using System;
using Cysharp.Threading.Tasks;

namespace Horcrux.Runtime.Abstractions.Ads
{
    /// <summary>
    /// What <c>AdService</c> needs from an ad SDK. Implemented in a vendor assembly that compiles only when the SDK is installed;
    /// a project on another SDK implements it with <c>[Service(typeof(IAdVendor))]</c>.
    /// </summary>
    public interface IAdVendor
    {
        bool IsRewardedReady { get; }

        /// <summary>
        /// Called only right after <see cref="IsRewardedReady"/> returned true in the same frame. Completes when the ad closes:
        /// true after it was displayed, false when display failed. <paramref name="onReward"/> may run before or after that.
        /// </summary>
        UniTask<bool> ShowRewardedAsync(string placement, Action onReward);
    }
}
```

Interface này nằm ở Abstractions vì dự án dùng SDK khác phải gọi tên nó để tự cài. `ShowRewardedAsync` cố ý không nhận token:
quảng cáo đã hiện thì không huỷ được, và lõi phải biết lúc nó đóng.

### `Runtime/Abstractions/Foundations/Ads/IAdService.cs` — file mới

```csharp
using System;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace Horcrux.Runtime.Abstractions.Ads
{
    /// <summary>
    /// Fullscreen ads, one at a time. Cancelling a call's token stops the caller's await only; a displayed ad still runs to
    /// its close.
    /// </summary>
    public interface IAdService : IService<IAdService>
    {
        /// <summary>
        /// Shows a rewarded ad and waits until it closes. <paramref name="onEarned"/> runs when the SDK grants the reward,
        /// possibly while the ad still covers the screen: write data there, play effects after the await returns Earned.
        /// </summary>
        UniTask<RewardedAdResult> ShowRewardedAsync(string placement, Action onEarned, CancellationToken ct);
    }
}
```

`IService<T>` nằm ở namespace cha `Horcrux.Runtime.Abstractions` nên không cần `using`. Nó cho caller `IAdService.Service` và
`TryGet`.

### `Runtime/Implementations/Foundations/Ads/AdService.cs` — file mới

```csharp
using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Horcrux.Runtime.Abstractions.Ads;
using Sisus.Init;
using UnityEngine;

namespace Horcrux.Runtime.Implementations.Ads
{
    /// <summary>
    /// Decides when a fullscreen ad shows and what the caller gets back; the vendor shows it and reports the close.
    /// One ad at a time, owned by the service: the caller cancelling does not abandon a displayed ad.
    /// </summary>
    [Service(typeof(IAdService))]
    public sealed class AdService : IAdService, IInitializable<IAdVendor>
    {
        #region Properties

        private enum FullscreenAd
        {
            None,
            Rewarded,
        }

        private const string LogPrefix = "[AdService]: ";
        // SDKs do not guarantee the reward callback comes before the close callback.
        private const float RewardAfterCloseSeconds = 0.5f;

        private static readonly Func<AdService, bool> IsRewardEarned = static s => s._isEarned;

        private IAdVendor _vendor;
        private FullscreenAd _showingAd;
        private bool _isEarned;
        private Action _onEarned;

        #endregion

        #region API

        public UniTask<RewardedAdResult> ShowRewardedAsync(string placement, Action onEarned, CancellationToken ct)
        {
            if (_showingAd != FullscreenAd.None)
            {
                Debug.LogWarning(LogPrefix + $"an ad is showing, ignored rewarded {placement}");
                return UniTask.FromResult(RewardedAdResult.NotAvailable);
            }

            // The show belongs to the service: ct ends the caller's await, the show runs on to its close.
            return RunRewardedAsync(placement, onEarned).AttachExternalCancellation(ct);
        }

        #endregion

        #region Class Methods

        private async UniTask<RewardedAdResult> RunRewardedAsync(string placement, Action onEarned)
        {
            _showingAd = FullscreenAd.Rewarded;
            _isEarned = false;
            _onEarned = onEarned;
            try
            {
                if (!_vendor.IsRewardedReady)
                    return RewardedAdResult.NotAvailable;

                bool isDisplayed = await _vendor.ShowRewardedAsync(placement, OnReward);
                if (!isDisplayed)
                    return RewardedAdResult.NotAvailable;

                if (!_isEarned)
                    await WaitRealtimeAsync(IsRewardEarned, RewardAfterCloseSeconds, CancellationToken.None);

                return _isEarned ? RewardedAdResult.Earned : RewardedAdResult.Skipped;
            }
            finally
            {
                _showingAd = FullscreenAd.None;
                _onEarned = null;
            }
        }

        // Late (after the show settled Skipped) or duplicate SDK callbacks grant nothing.
        private void OnReward()
        {
            if (_showingAd != FullscreenAd.Rewarded || _isEarned)
                return;

            _isEarned = true;
            try
            {
                _onEarned?.Invoke();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        // Real clock: popups may run while timeScale = 0.
        private async UniTask<bool> WaitRealtimeAsync(Func<AdService, bool> condition, float seconds, CancellationToken ct)
        {
            float deadline = Time.realtimeSinceStartup + seconds;
            while (!condition(this))
            {
                if (Time.realtimeSinceStartup >= deadline)
                    return false;

                await UniTask.Yield(PlayerLoopTiming.Update, ct);
            }

            return true;
        }

        #endregion

        #region DI

        public void Init(IAdVendor vendor) => _vendor = vendor;

        #endregion
    }
}
```

Chỗ dễ gõ sai:
- **`_showingAd` đặt trong `RunRewardedAsync`, không đặt trong `ShowRewardedAsync`.** `try/finally` của hàm mở lượt phải bọc trọn
  thân, để mọi đường ra (trả sớm, ném, huỷ) đều gỡ cờ đúng một lần. GĐ2 đặt bước đợi vào trong `try` này, nên cờ phủ luôn cả lúc
  đợi.
- **`OnReward` so với `FullscreenAd.Rewarded`, không so khác `None`.** GĐ3 thêm `Interstitial`: thưởng về muộn lúc interstitial đang
  chiếu cũng phải bị bỏ.
- **`if (!_vendor.IsRewardedReady)` là code tạm, chỉ để chặng này chạy được.** GĐ2 thay nó bằng bước đợi 2 s.
- **Bước đợi thưởng muộn dùng `CancellationToken.None`, không dùng `ct` của caller.** Caller huỷ thì lượt chiếu vẫn phải chốt
  đúng.
- **Delegate khai `static readonly` với lambda `static`.** Lambda bắt biến ngoài sẽ thành lỗi biên dịch thay vì sinh rác mỗi
  lần đợi.

**Phương án bỏ:**
- Cờ `bool _isShowing` thay enum `FullscreenAd` — GĐ3 cần biết quảng cáo đang chiếu là loại nào để `OnReward` bỏ thưởng lạc; hai
  `bool` loại trừ nhau là enum viết rời.
- `UniTaskCompletionSource` trong lõi để nghe sự kiện đóng — việc nghe đóng thuộc vendor, lõi chỉ `await`.
- Trả `bool` thay enum kết quả — `Skipped` và `NotAvailable` cho người chơi hai phản ứng khác nhau: ở yên popup, so với hiện chữ
  báo lỗi.

### Kiểm

Compile sạch. Agent viết `Tests/PlayMode/AdServiceTests.cs` với vendor giả. Vendor giả có `IsRewardedReady` gán được;
`ShowRewardedAsync` trả task của một `UniTaskCompletionSource<bool>` do test điều khiển, và giữ lại `onReward`.

| Input | Kỳ vọng |
|---|---|
| sẵn · thưởng → đóng | `Earned`, `onEarned` chạy 1 lần, trước lúc `await` trả |
| sẵn · đóng → 0,2 s sau thưởng | `Earned` |
| sẵn · đóng, không thưởng | `Skipped` sau ~0,5 s thật, `onEarned` 0 lần |
| thưởng tới sau khi lượt đã chốt `Skipped` | `onEarned` 0 lần |
| vendor gọi `onReward` hai lần | `onEarned` 1 lần |
| vendor trả `false` (hiển thị lỗi) | `NotAvailable`, `onEarned` 0 lần |
| chưa sẵn | `NotAvailable`, vendor không bị gọi `Show` |
| gọi lần hai khi lượt một chưa đóng | lượt hai trả `NotAvailable` ngay, log warning, vendor chỉ bị gọi `Show` một lần; lượt một vẫn trả đúng |
| caller huỷ `ct` sau khi `Show` | caller nhận `OperationCanceledException`; vendor thưởng rồi đóng thì `onEarned` vẫn chạy; lượt sau gọi được |
| `onEarned` ném | `LogException`, kết quả vẫn `Earned` |
| `timeScale = 0` lúc đợi thưởng muộn | vẫn hết giờ sau ~0,5 s thật |
| `onEarned = null` | `Earned`, không ném |

**Mốc commit (submodule):** `ads: contract IAdService, rewarded trong AdService`.

---

## Giai đoạn 2 — Rewarded chưa có quảng cáo: đợi 2 s, báo dự án

**Mục tiêu:** quảng cáo chưa sẵn lúc bấm thì đợi tối đa 2 s theo đồng hồ thật. Hết giờ mà vẫn chưa sẵn, hoặc hiển thị lỗi, thì
service bắn sự kiện `RewardedNotAvailable(placement)` để dự án hiện chữ báo một chỗ. Lần hết giờ còn được báo cho SDK để thống kê
lượt mất cơ hội.

**Vì sao bây giờ:** lúc vừa vào game, MAX thường chưa tải xong. Thiếu chặng này thì bấm nút sớm sẽ không có phản hồi gì. Đợi 2 s
cứu được những lượt quảng cáo sắp tải xong.

```
test ──ShowRewardedAsync──► AdService ──IsRewardedReady (mỗi frame ≤ 2 s)──► FakeVendor
                              │ hết giờ ──ReportRewardedNotReady──────────────►┘          [mới]
                              └─ RewardedNotAvailable(placement) ──► listener của test     [mới]
```

### `IAdVendor.cs` › dưới `ShowRewardedAsync` — thêm

```csharp
        /// <summary>Tells the SDK a rewarded opportunity was lost because no ad was ready (fill tracking).</summary>
        void ReportRewardedNotReady(string placement);
```

### `IAdService.cs` › trên `ShowRewardedAsync` — thêm

```csharp
        /// <summary>Raised with the placement when no rewarded ad was shown (not ready in time, display failed).</summary>
        event Action<string> RewardedNotAvailable;
```

Lượt bị từ chối vì đang có quảng cáo khác **không** bắn sự kiện này: quảng cáo đang chiếu thì hiện chữ "không có quảng cáo" là sai.

### `AdService.cs` › region `Properties` — thêm cạnh hằng cũ

```csharp
        private const float RewardedReadyWaitSeconds = 2f;

        private static readonly Func<AdService, bool> IsRewardedReady = static s => s._vendor.IsRewardedReady;
```

Và khai sự kiện, ngay trên region `API`:

```csharp
        public event Action<string> RewardedNotAvailable;
```

### `AdService.cs` › `ShowRewardedAsync` — đoạn cũ → đoạn mới

Token của caller giờ đi vào bước đợi, nên `RunRewardedAsync` nhận thêm `ct`:

```csharp
            return RunRewardedAsync(placement, onEarned).AttachExternalCancellation(ct);
```
→
```csharp
            return RunRewardedAsync(placement, onEarned, ct).AttachExternalCancellation(ct);
```

### `AdService.cs` › `RunRewardedAsync` — đoạn cũ → đoạn mới

```csharp
        private async UniTask<RewardedAdResult> RunRewardedAsync(string placement, Action onEarned)
        {
            _showingAd = FullscreenAd.Rewarded;
            _isEarned = false;
            _onEarned = onEarned;
            try
            {
                if (!_vendor.IsRewardedReady)
                    return RewardedAdResult.NotAvailable;

                bool isDisplayed = await _vendor.ShowRewardedAsync(placement, OnReward);
                if (!isDisplayed)
                    return RewardedAdResult.NotAvailable;
```
→
```csharp
        private async UniTask<RewardedAdResult> RunRewardedAsync(string placement, Action onEarned, CancellationToken ct)
        {
            _showingAd = FullscreenAd.Rewarded;
            _isEarned = false;
            _onEarned = onEarned;
            try
            {
                // Ready check and Show stay in one frame: the SDK silently skips a show that is not ready.
                if (!await WaitRealtimeAsync(IsRewardedReady, RewardedReadyWaitSeconds, ct))
                {
                    _vendor.ReportRewardedNotReady(placement);
                    RaiseRewardedNotAvailable(placement);
                    return RewardedAdResult.NotAvailable;
                }

                bool isDisplayed = await _vendor.ShowRewardedAsync(placement, OnReward);
                if (!isDisplayed)
                {
                    RaiseRewardedNotAvailable(placement);
                    return RewardedAdResult.NotAvailable;
                }
```

`ct` chỉ đi vào bước đợi sẵn sàng. Huỷ lúc đang đợi thì `UniTask.Yield` ném, `finally` gỡ cờ, `Show` không bao giờ được gọi.
Huỷ sau khi đã `Show` thì lượt chiếu vẫn chạy tới lúc đóng, như GĐ1.

### `AdService.cs` › region `Class Methods` — thêm dưới `OnReward`

```csharp
        // A throwing listener must not turn the result into an exception for the caller.
        private void RaiseRewardedNotAvailable(string placement)
        {
            try
            {
                RewardedNotAvailable?.Invoke(placement);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }
```

**Phương án bỏ:**
- Lõi tự hiện chữ báo lỗi — Horcrux không có hệ thông báo; chữ, ngôn ngữ và kiểu toast là của dự án.
- Mỗi popup tự hiện chữ khi nhận `NotAvailable` — cùng một phản ứng bị chép ở mọi caller.
- Số 2 s thành tham số của `ShowRewardedAsync` — chưa có call site nào truyền giá trị khác.

### Kiểm

| Input | Kỳ vọng |
|---|---|
| chưa sẵn, 1 s sau sẵn | chiếu; kết quả theo vendor; `RewardedNotAvailable` không bắn |
| chưa sẵn suốt 2 s | `NotAvailable` sau ~2 s thật; `ReportRewardedNotReady` 1 lần; sự kiện bắn 1 lần với đúng placement |
| vendor trả `false` | sự kiện bắn 1 lần; `ReportRewardedNotReady` 0 lần |
| sẵn ngay, chiếu xong | sự kiện 0 lần |
| huỷ `ct` lúc đang đợi | caller nhận `OperationCanceledException`; `Show` 0 lần; sự kiện 0 lần; lượt sau gọi được |
| gọi lần hai khi lượt một đang đợi | lượt hai trả `NotAvailable` ngay, sự kiện 0 lần |
| `timeScale = 0` lúc đợi | hết giờ sau ~2 s thật |
| listener ném | `LogException`; `ShowRewardedAsync` vẫn trả `NotAvailable` |
| sửa bảng GĐ1: dòng "chưa sẵn" | giờ là `NotAvailable` sau ~2 s, kèm sự kiện |

**Mốc commit (submodule):** `ads: rewarded đợi sẵn sàng 2 s, sự kiện RewardedNotAvailable`.

---

## Giai đoạn 3 — Interstitial: chung cờ chiếu và đồng hồ giãn cách

**Mục tiêu:** `ShowInterstitialAsync(placement, minIntervalSeconds, ct)` chiếu một interstitial khi không có quảng cáo nào đang
chiếu, người chơi chưa mua remove ads, đã đủ `minIntervalSeconds` kể từ lúc quảng cáo toàn màn gần nhất đóng, và vendor sẵn sàng.
Ngược lại thì xong ngay, im lặng. Rewarded đã hiện (Earned hay Skipped) cũng đặt mốc giãn cách.

**Vì sao bây giờ:** cờ chiếu đã có từ GĐ1; interstitial là người dùng thứ hai của nó. Đồng hồ giãn cách chỉ có nghĩa khi có
interstitial, nên sinh ở đây cùng hai người ghi (rewarded, interstitial) và người đọc (interstitial).

```
test ──ShowInterstitialAsync(placement, s)──► AdService ──IsAdsRemoved · IsInterstitialReady──► FakeVendor   [mới]
                                                │  cờ chiếu (GĐ1) · mốc đóng gần nhất                        [mới]
                                                │ chưa sẵn ──ReportInterstitialNotReady──────────────────────► │  [mới]
                                                └─ ShowInterstitialAsync ──────────────────────────────────► ┘  [mới]
test ──ShowRewardedAsync──► AdService: rewarded đóng → đặt mốc                                                [mới]
```

### `IAdVendor.cs` › dưới `ReportRewardedNotReady` — thêm

```csharp
        /// <summary>
        /// True when the player bought remove ads: interstitial and banner stay off, rewarded still shows. The service skips
        /// without reporting a lost opportunity.
        /// </summary>
        bool IsAdsRemoved { get; }

        bool IsInterstitialReady { get; }

        /// <summary>
        /// Called only right after <see cref="IsInterstitialReady"/> returned true in the same frame. Completes when the ad closes
        /// or fails to display: true; false when the SDK showed nothing.
        /// </summary>
        UniTask<bool> ShowInterstitialAsync(string placement);

        /// <summary>Tells the SDK an interstitial opportunity was lost because no ad was ready (fill tracking).</summary>
        void ReportInterstitialNotReady(string placement);
```

Remove ads nằm ở vendor vì trạng thái đó là của SDK quảng cáo (Zego có contract riêng cho nó, dự án đăng ký bản cài). Dự án dùng
SDK khác tự cài vendor nên tự trả từ hệ IAP của mình.

### `IAdService.cs` › dưới `ShowRewardedAsync` — thêm

```csharp
        /// <summary>
        /// Shows an interstitial and waits until it closes. Completes at once, silently, when an ad is showing, ads are removed,
        /// fewer than <paramref name="minIntervalSeconds"/> passed since the last fullscreen ad closed, or none is ready.
        /// </summary>
        UniTask ShowInterstitialAsync(string placement, float minIntervalSeconds, CancellationToken ct);
```

Interface giữ nguyên dòng summary của GĐ1 ("Fullscreen ads, one at a time…"): giờ nó đúng cho cả hai loại.

### `AdService.cs` › region `Properties` — thêm dưới `_showingAd`

```csharp
        // null: no fullscreen ad closed yet this session, the first interstitial need not wait.
        private float? _lastFullscreenClosedAt;
```

### `AdService.cs` › region `API` — thêm dưới `ShowRewardedAsync`

```csharp
        public UniTask ShowInterstitialAsync(string placement, float minIntervalSeconds, CancellationToken ct)
        {
            // Removed before ready: a player who bought remove ads is not a lost opportunity.
            if (_showingAd != FullscreenAd.None || _vendor.IsAdsRemoved || !HasIntervalPassed(minIntervalSeconds))
                return UniTask.CompletedTask;

            if (!_vendor.IsInterstitialReady)
            {
                _vendor.ReportInterstitialNotReady(placement);
                return UniTask.CompletedTask;
            }

            return RunInterstitialAsync(placement).AttachExternalCancellation(ct);
        }
```

### `AdService.cs` › `RunRewardedAsync` — đoạn cũ → đoạn mới

Rewarded đã hiện thì đặt mốc, trước bước đợi thưởng muộn:

```csharp
                if (!isDisplayed)
                {
                    RaiseRewardedNotAvailable(placement);
                    return RewardedAdResult.NotAvailable;
                }

                if (!_isEarned)
```
→
```csharp
                if (!isDisplayed)
                {
                    RaiseRewardedNotAvailable(placement);
                    return RewardedAdResult.NotAvailable;
                }

                MarkFullscreenClosed();
                if (!_isEarned)
```

### `AdService.cs` › region `Class Methods` — thêm dưới `RunRewardedAsync`

```csharp
        private async UniTask RunInterstitialAsync(string placement)
        {
            _showingAd = FullscreenAd.Interstitial;
            try
            {
                if (await _vendor.ShowInterstitialAsync(placement))
                    MarkFullscreenClosed();
            }
            finally
            {
                _showingAd = FullscreenAd.None;
            }
        }

        private bool HasIntervalPassed(float minIntervalSeconds) =>
            !_lastFullscreenClosedAt.HasValue ||
            Time.realtimeSinceStartup - _lastFullscreenClosedAt.Value >= minIntervalSeconds;

        private void MarkFullscreenClosed() => _lastFullscreenClosedAt = Time.realtimeSinceStartup;
```

### `AdService.cs` › region `Properties` › enum `FullscreenAd` — thêm dưới `Rewarded`

```csharp
            Interstitial,
```

Chỗ dễ gõ sai:
- **Mốc đặt bằng `Time.realtimeSinceStartup`, không `Time.time`.** Quảng cáo chạy lúc app gần như đứng; `Time.time` còn dừng khi
  `timeScale = 0`.
- **Kiểm sẵn sàng là bước cuối trước `RunInterstitialAsync`.** Như rewarded, sẵn sàng và `Show` phải cùng frame.
- **`RunInterstitialAsync` không nhận `ct`.** Interstitial không có bước đợi nào trước `Show`; token của caller chỉ cắt `await` của
  caller qua `AttachExternalCancellation`.

**Phương án bỏ:**
- Interval cấu hình trên service (`SetInterval`, `ForceCompleteInterval`) — số đổi theo tập user và remote, mỗi kịch bản có thể
  một số; tham số của lời gọi giữ nó ở đúng một chủ là dự án.
- Interstitial trả `bool` "đã chiếu" — chưa caller nào rẽ nhánh theo nó; thêm sau không đổi chữ ký cũ nếu cần.
- Interstitial đợi sẵn sàng như rewarded — xem "Cố ý KHÔNG làm".

### Kiểm

Vendor giả thêm `IsAdsRemoved`, `IsInterstitialReady` gán được; `ShowInterstitialAsync` trả task của một
`UniTaskCompletionSource<bool>` do test điều khiển; đếm số lần `Show` và `ReportInterstitialNotReady`.

| Input | Kỳ vọng |
|---|---|
| lần đầu phiên, sẵn, vendor trả `true` | `Show` 1 lần; task xong khi vendor xong |
| vừa đóng interstitial, gọi lại với `minIntervalSeconds = 0.2` sau 0,1 s thật | `Show` 0 lần, xong ngay |
| như trên nhưng sau 0,3 s thật | `Show` 1 lần |
| rewarded `Skipped` vừa đóng, gọi interstitial với 0,2 s ngay sau | `Show` 0 lần |
| rewarded `NotAvailable` (hiển thị lỗi) vừa xong, gọi interstitial | `Show` 1 lần — lỗi hiển thị rewarded không đặt mốc |
| vendor interstitial trả `false` rồi gọi lại ngay với 0,2 s | `Show` lần hai được gọi — `false` không đặt mốc |
| `IsAdsRemoved = true`, chưa sẵn | `Show` 0 lần, `ReportInterstitialNotReady` 0 lần |
| chưa sẵn | `Show` 0 lần, `ReportInterstitialNotReady` 1 lần với đúng placement |
| gọi interstitial khi rewarded đang chiếu | `Show` interstitial 0 lần; rewarded vẫn trả đúng |
| gọi rewarded khi interstitial đang chiếu | rewarded trả `NotAvailable` ngay, log warning, `RewardedNotAvailable` 0 lần |
| vendor bắn `onReward` của rewarded cũ lúc interstitial đang chiếu | `onEarned` 0 lần |
| caller huỷ `ct` khi interstitial đang chiếu | caller nhận `OperationCanceledException`; vendor xong thì mốc vẫn đặt, lượt sau bị giãn cách chặn |
| `timeScale = 0` khi đo giãn cách | vẫn tính theo giây thật |

**Mốc commit (submodule):** `ads: interstitial chung cờ chiếu và giãn cách với rewarded`.

---

## Giai đoạn 4 — Banner: ý muốn của dự án qua một cửa đối chiếu

**Mục tiêu:** dự án gọi `ShowBanner` / `HideBanner` để nói có muốn banner không, và đọc `BannerHeightPixels` để chừa dải đáy. Banner
chỉ hiện khi dự án muốn, không có quảng cáo toàn màn đang chiếu, chưa remove ads, và vendor đã nhận lệnh được. Ý muốn đến trước lúc
vendor sẵn sàng được giữ lại và áp khi vendor báo `BannerAvailable`. Lệnh tới vendor đúng bằng số lần quyết định đổi.

**Vì sao bây giờ:** banner đọc cờ "đang chiếu" và remove ads mà GĐ1 và GĐ3 đã dựng. Cửa đối chiếu đặt trong lõi để test được bằng
vendor giả; GĐ5 chỉ còn phần báo "SDK nhận lệnh được" của Zego.

```
test ──ShowBanner · HideBanner──► AdService: ý muốn ──► ApplyBanner ──IsBannerAvailable · IsAdsRemoved──► FakeVendor   [mới]
                                       ▲  cờ chiếu (GĐ1)              └──ShowBanner · HideBanner──────────────────► │  [mới]
test bắn BannerAvailable ─────────────┘◄──────────────────────────────────────────────────────────────────────────┘  [mới]
RunRewardedAsync · RunInterstitialAsync: trước Show và trong finally ──► ApplyBanner                                  [mới]
```

### `IAdVendor.cs` › dưới `ReportInterstitialNotReady` — thêm

```csharp
        /// <summary>Banner height in screen pixels, known before the banner loads.</summary>
        float BannerHeightPixels { get; }

        /// <summary>False until the SDK accepts banner commands; commands sent earlier are dropped.</summary>
        bool IsBannerAvailable { get; }

        /// <summary>Raised once, when <see cref="IsBannerAvailable"/> turns true.</summary>
        event Action BannerAvailable;

        void ShowBanner();

        void HideBanner();
```

### `IAdService.cs` › dưới `ShowInterstitialAsync` — thêm

```csharp
        /// <summary>Banner height in screen pixels, known before the banner loads: read it once to reserve the bottom band.</summary>
        float BannerHeightPixels { get; }

        /// <summary>
        /// Wants the bottom banner. It shows only while no fullscreen ad shows and ads are not removed; a wish made before the SDK
        /// accepts banners is applied then.
        /// </summary>
        void ShowBanner();

        void HideBanner();
```

Summary của interface đổi thành `Fullscreen ads, one at a time, and the bottom banner they hide. Cancelling a call's token stops the
caller's await only; a displayed ad still runs to its close.`

### `AdService.cs` › region `Properties` — thêm dưới `_lastFullscreenClosedAt`

```csharp
        private bool _isBannerRequested;
        // What the vendor was last told; only ApplyBanner writes it.
        private bool _isBannerShown;
```

### `AdService.cs` › region `API` — thêm dưới `ShowInterstitialAsync`

```csharp
        public float BannerHeightPixels => _vendor.BannerHeightPixels;

        public void ShowBanner()
        {
            _isBannerRequested = true;
            ApplyBanner();
        }

        public void HideBanner()
        {
            _isBannerRequested = false;
            ApplyBanner();
        }
```

### `AdService.cs` › `RunRewardedAsync` — đoạn cũ → đoạn mới

Banner ẩn ngay trước khi quảng cáo hiện, và trả lại ở mọi đường ra:

```csharp
                bool isDisplayed = await _vendor.ShowRewardedAsync(placement, OnReward);
```
→
```csharp
                ApplyBanner();
                bool isDisplayed = await _vendor.ShowRewardedAsync(placement, OnReward);
```

```csharp
            finally
            {
                _showingAd = FullscreenAd.None;
                _onEarned = null;
            }
```
→
```csharp
            finally
            {
                _showingAd = FullscreenAd.None;
                _onEarned = null;
                ApplyBanner();
            }
```

### `AdService.cs` › `RunInterstitialAsync` — đoạn cũ → đoạn mới

```csharp
            _showingAd = FullscreenAd.Interstitial;
            try
            {
                if (await _vendor.ShowInterstitialAsync(placement))
                    MarkFullscreenClosed();
            }
            finally
            {
                _showingAd = FullscreenAd.None;
            }
```
→
```csharp
            _showingAd = FullscreenAd.Interstitial;
            try
            {
                ApplyBanner();
                if (await _vendor.ShowInterstitialAsync(placement))
                    MarkFullscreenClosed();
            }
            finally
            {
                _showingAd = FullscreenAd.None;
                ApplyBanner();
            }
```

### `AdService.cs` › region `Class Methods` — thêm dưới `MarkFullscreenClosed`

```csharp
        // The one gate to the vendor's banner, called whenever one of its inputs changes.
        private void ApplyBanner()
        {
            bool shouldShow = _isBannerRequested && _showingAd == FullscreenAd.None && !_vendor.IsAdsRemoved;
            if (shouldShow == _isBannerShown || !_vendor.IsBannerAvailable)
                return;

            _isBannerShown = shouldShow;
            if (shouldShow)
                _vendor.ShowBanner();
            else
                _vendor.HideBanner();
        }
```

### `AdService.cs` › region `DI` — đoạn cũ → đoạn mới

```csharp
        public void Init(IAdVendor vendor) => _vendor = vendor;
```
→
```csharp
        public void Init(IAdVendor vendor)
        {
            _vendor = vendor;
            // Service and vendor live for the whole app: no unsubscribe.
            _vendor.BannerAvailable += ApplyBanner;
        }
```

Chỗ dễ gõ sai:
- **`ApplyBanner` của rewarded đứng ngay trước `_vendor.ShowRewardedAsync`, không ngay sau khi đặt `_showingAd`.** Trong 2 s đợi sẵn
  sàng banner còn hiện; lượt hết giờ trả `NotAvailable` mà banner không chớp. `finally` vẫn gọi: kết quả bằng lần gửi trước thì
  không gửi gì.
- **`ApplyBanner` trong `finally` đứng sau dòng gỡ `_showingAd`.** Đặt trước thì phép so vẫn thấy quảng cáo đang chiếu, banner ẩn tới
  lần gọi sau.
- **Phép so đứng trước phép kiểm `IsBannerAvailable`, và `_isBannerShown` chỉ ghi sau cả hai.** Chưa available thì giữ nguyên thứ
  đã gửi, để lúc `BannerAvailable` về phép so còn thấy chênh.
- **`_isBannerShown` là thứ đã gửi vendor, không phải ý muốn.** Ý muốn là `_isBannerRequested`; gộp hai cờ thì `HideBanner` lúc quảng
  cáo đang chiếu làm mất ý muốn của dự án.

**Phương án bỏ:**
- Vendor tự giữ lệnh tới khi SDK khởi tạo — ý muốn có hai chủ (lõi, vendor), và luật hoãn chỉ test được trên máy thật.
- Dự án tự ẩn, hiện banner quanh mỗi lời gọi quảng cáo toàn màn — mọi caller lặp cùng cặp lệnh; một chỗ quên là banner nằm đè lên
  quảng cáo.
- Gửi lệnh mỗi lần gọi, không so với lần gửi trước — dự án gọi `ShowBanner` ở mọi lần vào màn, lần đầu sau khi vendor sẵn sàng thì cả
  `BannerAvailable` lẫn `ShowBanner` cùng gửi; phép so giữ một lệnh cho mỗi lần đổi, test đếm được.
- Lõi nghe remove ads để tự ẩn — vendor không có sự kiện đổi; cửa ghi remove ads ở dự án gọi `HideBanner` (xem Ngữ cảnh đã chốt).

### Kiểm

Vendor giả thêm `BannerHeightPixels`, `IsBannerAvailable` gán được, một hàm bắn `BannerAvailable`, và đếm `ShowBanner`, `HideBanner`
theo thứ tự xen với `ShowRewardedAsync`, `ShowInterstitialAsync`.

| Input | Kỳ vọng |
|---|---|
| available · `ShowBanner` | `ShowBanner` 1 lần |
| available · `ShowBanner` hai lần | `ShowBanner` 1 lần |
| available · `HideBanner` khi chưa từng hiện | 0 lệnh |
| chưa available · `ShowBanner` · rồi bắn `BannerAvailable` | 0 lệnh trước sự kiện, `ShowBanner` 1 lần sau |
| chưa available · `ShowBanner` · `HideBanner` · rồi `BannerAvailable` | 0 lệnh |
| banner hiện · rewarded sẵn ngay, đóng | `HideBanner` trước `ShowRewardedAsync` của vendor; `ShowBanner` sau khi lượt trả |
| banner hiện · rewarded chưa sẵn suốt 2 s | 0 lệnh banner |
| banner hiện · interstitial chiếu, đóng | `HideBanner` trước `ShowInterstitialAsync`; `ShowBanner` sau |
| banner hiện · interstitial bị giãn cách bỏ | 0 lệnh banner |
| `ShowBanner` lúc rewarded đang chiếu | 0 lệnh trong lượt; `ShowBanner` 1 lần khi lượt trả |
| `HideBanner` lúc rewarded đang chiếu | 0 lệnh sau khi lượt trả (banner đã ẩn, ý muốn là ẩn) |
| `IsAdsRemoved = true` · `ShowBanner` | 0 lệnh |
| banner hiện · `IsAdsRemoved` thành true · `HideBanner` | `HideBanner` 1 lần; `ShowBanner` sau đó 0 lệnh |
| caller huỷ `ct` lúc rewarded đang chiếu | banner hiện lại khi vendor đóng, không phải lúc caller huỷ |
| `BannerHeightPixels` | trả đúng số của vendor |

**Mốc commit (submodule):** `ads: banner theo ý muốn của dự án, ẩn khi quảng cáo toàn màn`.

---

## Giai đoạn 5 — Bộ chuyển Zego MAX

**Mục tiêu:** dự án có package `com.zego.advertisement.max` thì asmdef `com.horcrux.zego` tự bật, và `ZegoAdVendor` tự đăng ký làm
`IAdVendor`. Dự án không viết dòng nào.

**Vì sao bây giờ:** lõi đã đủ hợp đồng cho cả ba loại. Chặng này nối lõi với SDK thật để GĐ7 chuyển dự án sang.

```
AdService ──ShowRewardedAsync──► ZegoAdVendor ──ShowRewardedVideo(placement, onReward)──► Zego IAdService
                                   ▲ true  ◄── MaxSdkCallbacks.Rewarded|Interstitial.OnAdHiddenEvent
                                   └ false ◄── MaxSdkCallbacks.Rewarded|Interstitial.OnAdDisplayFailedEvent
AdService ──ShowInterstitialAsync──► ZegoAdVendor ──ShowInterstitial(placement, onClose)──► Zego IAdService
                                   ▲ true ◄── onClose (đóng hoặc hiển thị lỗi) · false ◄── Zego trả false
AdService ──IsAdsRemoved──► Zego IRemoveAdsStatus (dự án đăng ký)
AdService ──ShowBanner · HideBanner──► ZegoAdVendor ──► Zego IAdService.ShowBanner · HideBanner
          ◄──BannerAvailable── ZegoAdVendor ◄── MaxSdkCallbacks.OnSdkInitializedEvent, đợi thêm 1 frame
          ──BannerHeightPixels──► ZegoAdVendor ──► MaxSdkUtils.GetAdaptiveBannerHeight × GetScreenDensity
```

### `Vendors/Zego/com.horcrux.zego.asmdef` — file mới

Tạo bằng Project window › Create › Assembly Definition, đặt tên `com.horcrux.zego`, rồi dán nội dung:

```json
{
    "name": "com.horcrux.zego",
    "rootNamespace": "",
    "references": [
        "com.horcrux.runtime",
        "InitArgs",
        "InitArgs.Services",
        "UniTask",
        "Zego",
        "MaxSdk.Scripts"
    ],
    "includePlatforms": [],
    "excludePlatforms": [],
    "allowUnsafeCode": false,
    "overrideReferences": false,
    "precompiledReferences": [],
    "autoReferenced": true,
    "defineConstraints": [
        "HORCRUX_ZEGO_ADS"
    ],
    "versionDefines": [
        {
            "name": "com.zego.advertisement.max",
            "expression": "4.0.0",
            "define": "HORCRUX_ZEGO_ADS"
        }
    ],
    "noEngineReferences": false
}
```

`Zego` là asmdef của `com.zego.core` (chứa `IAdService`, `IRemoveAdsStatus`, `AdStatic`, `EAdFormat`). `MaxSdk.Scripts` thuộc
`com.applovin.mediation.ads`, là dependency của `com.zego.advertisement.max`. Vì vậy chỉ cần kiểm một package. Gỡ package thì define
biến mất, asmdef tắt, dự án vẫn compile; lúc chạy thì `IAdService` thiếu vendor (cùng giả định ở đầu Plan).

### `Vendors/Zego/ZegoAdVendor.cs` — file mới

```csharp
using System;
using Cysharp.Threading.Tasks;
using Horcrux.Runtime.Abstractions.Ads;
using Sisus.Init;
using UnityEngine;
using Zego;
using ZegoAds = Zego.IAdService;

namespace Horcrux.Vendors.Zego
{
    /// <summary>
    /// Ads through Zego MAX. Zego reports only the reward for rewarded, so its close comes straight from MAX — interstitial
    /// too, since remote key compare_with_inter_when_show_rewarded_permission = "1" may show one instead. Banner commands
    /// before MAX initializes are dropped by Zego, hence <see cref="BannerAvailable"/>.
    /// </summary>
    [Service(typeof(IAdVendor))]
    public sealed class ZegoAdVendor : IAdVendor
    {
        #region Properties

        // Editor: MAX returns 50 × 1, too thin to see the band; a 1080 px wide phone gets about 64 dp × 2.625.
        private const float EditorBannerHeightPixels = 168f;

        private UniTaskCompletionSource<bool> _rewardedClosed;
        private UniTaskCompletionSource _interstitialClosed;

        // Exist first: Global logs an error every call while the Zego prefab is missing, and this is polled each frame.
        public bool IsRewardedReady => ZegoAds.Exist && ZegoAds.Global.IsRewardedVideoReady;

        public bool IsAdsRemoved => IRemoveAdsStatus.Exist && IRemoveAdsStatus.Global.IsRemoveAds;

        public bool IsInterstitialReady => ZegoAds.Exist && ZegoAds.Global.IsInterstitialReady;

        // MAX answers in dp (Android) or points (iOS); density turns it into pixels.
        public float BannerHeightPixels => Application.isEditor
            ? EditorBannerHeightPixels
            : Mathf.Max(0f, MaxSdkUtils.GetAdaptiveBannerHeight() * MaxSdkUtils.GetScreenDensity());

        public bool IsBannerAvailable { get; private set; }

        public event Action BannerAvailable;

        #endregion

        #region API

        // InitArgs builds the vendor at boot, before the project calls Zego Init; built lazily, MAX may already be up.
        public ZegoAdVendor()
        {
            if (MaxSdk.IsInitialized())
                MarkBannerAvailableNextFrameAsync().Forget();
            else
                MaxSdkCallbacks.OnSdkInitializedEvent += OnSdkInitialized;
        }

        public async UniTask<bool> ShowRewardedAsync(string placement, Action onReward)
        {
            _rewardedClosed = new UniTaskCompletionSource<bool>();
            SubscribeRewardedClose();
            try
            {
                ZegoAds.Global.ShowRewardedVideo(placement, onReward);
                return await _rewardedClosed.Task;
            }
            finally
            {
                UnsubscribeRewardedClose();
                _rewardedClosed = null;
            }
        }

        public async UniTask<bool> ShowInterstitialAsync(string placement)
        {
            _interstitialClosed = new UniTaskCompletionSource();
            try
            {
                // Zego calls onClose on close and on display failure alike; false means it showed nothing.
                if (!ZegoAds.Global.ShowInterstitial(placement, OnInterstitialClosed))
                    return false;

                await _interstitialClosed.Task;
                return true;
            }
            finally
            {
                _interstitialClosed = null;
            }
        }

        public void ReportRewardedNotReady(string placement) =>
            AdStatic.RaiseAdShowFailNotReady(placement, EAdFormat.RewardedVideo);

        public void ReportInterstitialNotReady(string placement) =>
            AdStatic.RaiseAdShowFailNotReady(placement, EAdFormat.Interstitial);

        // Called only after BannerAvailable: MAX is up, so the Zego prefab exists.
        public void ShowBanner() => ZegoAds.Global.ShowBanner();

        public void HideBanner() => ZegoAds.Global.HideBanner();

        #endregion

        #region Class Methods

        // Subscribed only while showing: MAX events are static, they outlive Play sessions when domain reload is off.
        private void SubscribeRewardedClose()
        {
            MaxSdkCallbacks.Rewarded.OnAdHiddenEvent += OnRewardedHidden;
            MaxSdkCallbacks.Rewarded.OnAdDisplayFailedEvent += OnRewardedDisplayFailed;
            MaxSdkCallbacks.Interstitial.OnAdHiddenEvent += OnRewardedHidden;
            MaxSdkCallbacks.Interstitial.OnAdDisplayFailedEvent += OnRewardedDisplayFailed;
        }

        private void UnsubscribeRewardedClose()
        {
            MaxSdkCallbacks.Rewarded.OnAdHiddenEvent -= OnRewardedHidden;
            MaxSdkCallbacks.Rewarded.OnAdDisplayFailedEvent -= OnRewardedDisplayFailed;
            MaxSdkCallbacks.Interstitial.OnAdHiddenEvent -= OnRewardedHidden;
            MaxSdkCallbacks.Interstitial.OnAdDisplayFailedEvent -= OnRewardedDisplayFailed;
        }

        private void OnRewardedHidden(string adUnitId, MaxSdkBase.AdInfo adInfo) => _rewardedClosed?.TrySetResult(true);

        private void OnRewardedDisplayFailed(string adUnitId, MaxSdkBase.ErrorInfo errorInfo, MaxSdkBase.AdInfo adInfo) =>
            _rewardedClosed?.TrySetResult(false);

        private void OnInterstitialClosed() => _interstitialClosed?.TrySetResult();

        // One shot: the event is static and outlives Play sessions when domain reload is off.
        private void OnSdkInitialized(MaxSdkBase.SdkConfiguration configuration)
        {
            MaxSdkCallbacks.OnSdkInitializedEvent -= OnSdkInitialized;
            MarkBannerAvailableNextFrameAsync().Forget();
        }

        // Zego builds its banner in its own handler of the same event; handler order is not defined.
        private async UniTaskVoid MarkBannerAvailableNextFrameAsync()
        {
            await UniTask.Yield();
            IsBannerAvailable = true;
            BannerAvailable?.Invoke();
        }

        #endregion
    }
}
```

Chỗ dễ gõ sai:
- **Alias `ZegoAds`, không viết `IAdService` trần hay `Zego.IAdService`.** File có cả `using Horcrux.Runtime.Abstractions.Ads;`
  lẫn `using Zego;`, nên `IAdService` trần là mơ hồ. Trong namespace `Horcrux.Vendors.Zego`, tên `Zego` trỏ về chính namespace này,
  nên `Zego.IAdService` cũng lỗi. Alias khai ở đầu file, ngoài namespace, nên `Zego` trong alias là namespace gốc của SDK.
- **`TrySetResult`, không `SetResult`.** MAX có thể bắn hidden sau display-failed của cùng một quảng cáo; lần bắn thứ hai phải là
  no-op. Zego gặp interstitial chưa sẵn thì gọi `onClose` **trong** `ShowInterstitial` rồi mới trả `false`: source đã có kết quả,
  hàm vẫn trả `false` đúng.
- **Đăng ký trước khi gọi `ShowRewardedVideo`.** Trong Editor, quảng cáo giả của MAX có thể đóng ngay trong lời gọi đó.
- **Interstitial không nghe MAX.** Zego gọi `onClose` ở mọi đường ra của nó; nghe thêm MAX là hai nguồn cho một sự kiện.
- **Đăng ký `OnSdkInitializedEvent` trong constructor, gỡ ngay trong handler.** InitArgs dựng service không lazy lúc boot, trước khi
  dự án gọi Zego `Init`, nên không lỡ sự kiện. `MaxSdk.IsInitialized()` phủ ca vendor dựng muộn.
- **`BannerAvailable` bắn sau một frame, không trong handler.** Lệnh `ShowBanner` gửi trước khi handler của Zego dựng banner thì bị bỏ
  im lặng: banner không bao giờ hiện, không log.
- **Chiều cao trong Editor là hằng.** MAX trong Editor trả 50 × 1, quá thấp để thấy dải chừa đáy khi dựng màn.

**Lưu ý — lỗi của Zego, không sửa ở đây:** ở chế độ compare, khi Zego chọn interstitial mà interstitial hiển thị lỗi, Zego vẫn gọi
`onReceive`. Kết quả: lõi chạy `onEarned` (thưởng đã ghi) rồi trả `NotAvailable`. Key đang mặc định `"0"`. Bật key thì báo đội Zego,
hoặc chấp nhận cấp nhầm ở ca này.

**Phương án bỏ:** nghe `AdFullscreenClosedEvent` qua EventBus của Zego cho rewarded. Sự kiện này chỉ bắn khi rewarded đóng, không bắn
khi hiển thị lỗi, cũng không bắn ở nhánh compare. Vẫn phải nghe MAX, nên nghe một nguồn cho cả hai.

### Kiểm

Compile sạch ở dự án có Zego. Nghiệm thu hành vi nằm ở GĐ7, trên máy thật hoặc trong Editor với quảng cáo giả của MAX.

**Mốc commit (submodule):** `ads: bộ chuyển Zego MAX cho rewarded, interstitial và banner`.

---

## Giai đoạn 6 — Tài liệu (agent)

`docs/Runtime/AdsSystem/AdsSystem.md` và `AdsSystem.html`, theo skill `docs`. Mục "Trước khi chạy" gồm:
- Prefab `Zego.Advertisement.MaxDefault` ở gốc scene đầu. Thiếu thì Zego `LogError` lúc boot, mọi rewarded trả `NotAvailable`,
  interstitial không bao giờ hiện.
- Gọi `Zego.IAdService.Global?.Init()` sớm nhất có thể, trước fetch remote config. Thiếu thì MAX không tải quảng cáo nào.
- Ad unit id và SDK key (menu Zego, `AppLovinSettings`).
- Bản cài `Zego.IRemoveAdsStatus` đăng ký lúc boot. Thiếu thì người đã mua remove ads vẫn bị báo "mất cơ hội" cho SDK (Zego vẫn
  không chiếu).
- Một listener nghe `RewardedNotAvailable`. Thiếu thì người chơi bấm nút, đợi 2 s, rồi không thấy gì.
- Ad unit id banner (menu Zego). Thiếu thì `ShowBanner` không hiện gì, không lỗi.
- Màn có banner chừa dải đáy bằng `BannerHeightPixels` **trước khi dựng màn**, qua `BottomInset` (tài liệu ViewportAnchor). Thiếu thì
  banner che nút sát đáy.
- Cửa ghi remove ads của dự án gọi `HideBanner`. Thiếu thì banner đang hiện còn tới lúc rời màn.

Ngoài ra: luồng rewarded, interstitial và banner, bảng kết quả → caller nên làm gì, cách viết lớp luật kịch bản ở dự án (mốc,
giãn cách, placement, màn nào muốn banner — ví dụ tên trung tính), các quyết định trái trực giác, lỗi Zego ở chế độ compare, và
"Mở rộng sau".

**Mốc commit (submodule):** `docs: AdsSystem`.

---

## Giai đoạn 7 — Dự án dùng bản Horcrux (agent)

1. **Bốn popup rewarded** — `LiveSystem_PopupRefillLiveOffer`, `PopupOfferRevive`, `PopupWin`, `PopupAddBox`: đổi
   `Service.Get<IAdService>()` thành `IAdService.Service`. Thêm `using Horcrux.Runtime.Abstractions.Ads;`; giữ `using Core;` nếu file
   còn dùng thứ khác trong đó. Placement giữ nguyên chuỗi `refill_live`, `revive`, `win_x2`, `add_box`.
2. **`InterstitialScenarios`, `RewardedScenarios`, `AdLevelGate`** ở lại dự án (mốc level, tập user, lượt play test là luật của dự
   án): `InterstitialScenarios` đổi `Service.Get<IAdService>()` thành `IAdService.Service`, thêm cùng `using`. Chỗ gọi ở
   `PopupWin.EndExit` và `MainController.RetryLevel` không đổi.
3. **Banner** — `GameplayBanner` (bốn chỗ `Service.Get<IAdService>()`) đổi thành `IAdService.Service`; `PersistentData.RemoveAds`
   đổi `Service.TryGet<Core.IAdService>(out var ads)` thành `IAdService.TryGet(out var ads)`. `PopupShop`, `MainController` gọi
   `GameplayBanner`, không đổi. Dải đáy: `Horcrux_ViewportAnchor_Plan.md` GĐ6 (đã gõ Plan đó) hoặc giữ `BannerSpace` của dự án.
4. **`AdNotAvailableToast`** (dự án, `Assets/_Modules/Sdk/Ads/`) là `MonoBehaviour<IAdService>`. Đăng ký `RewardedNotAvailable` ở
   `OnEnable` và gỡ ở `OnDisable`, cùng một file. Khi có sự kiện: `INotificationService.TryGet(out var n)` rồi
   `n.ShowNotification("Ad not available")`; thiếu service thì `LogError`.
5. **Xoá bản tạm:** `Assets/_Core/Ads/IAdService.cs` và `Assets/_Modules/Sdk/Ads/ZegoAdService.cs`. Không asset nào gắn hai file
   này, nên không cần giữ GUID.
6. **`Assets/_Modules/Sdk/Ads/Ads.md`** rút gọn còn phần của dự án: bảng placement rewarded → mốc → `onEarned` → sau khi đóng; bảng
   kịch bản interstitial → chỗ chèn → cột mốc level của tập user; banner theo màn, Shop, dải đáy; chỗ gọi `Init`; vị trí listener. Phần cơ chế trỏ sang
   `docs/Runtime/AdsSystem/AdsSystem.md`.
7. **Nghiệm thu:**
   - grep không còn `ZegoAdService` và `Core.IAdService`;
   - compile sạch;
   - Console lúc boot không có lỗi InitArgs về `IAdVendor`.

**Editor setup (developer):** mở `Service.unity`. Trên GameObject chứa `NotificationManager`, chọn Add Component › `AdNotAvailableToast`.
Lưu scene. Thiếu bước này thì hết quảng cáo, người chơi không thấy chữ báo nào.

**Chơi thử (developer)** — bản build có quảng cáo thật hoặc quảng cáo test:

| Kịch bản | Làm | Thấy | Dấu hiệu hỏng |
|---|---|---|---|
| Thêm mạng | hết mạng → Free → xem hết | +1 mạng, popup đóng | popup còn mở, hoặc mạng không tăng |
| Thêm mạng, đóng sớm | Free → đóng quảng cáo giữa chừng | popup còn, nút bấm lại được, mạng không đổi | popup đóng, hoặc nút kẹt |
| Hồi sinh | thua → Free → xem hết | chơi tiếp; nút X không phản ứng trong lúc xem | thua luôn sau khi xem |
| Get x2 | thắng → Get x2 → xem hết | coin bay đủ x2 sau khi quảng cáo đóng, rồi vào level kế **không** interstitial | HUD nhảy lúc quảng cáo còn che màn, hoặc interstitial ngay sau rewarded |
| Không mạng | tắt mạng → bấm bất kỳ nút rewarded nào | sau ~2 s hiện "Ad not available", nút bấm lại được | không có chữ, hoặc nút kẹt |
| Bấm liền hai lần | bấm Free hai lần thật nhanh | chỉ một quảng cáo | hai quảng cáo, hoặc thưởng hai lần |
| Win | tập Ads, level ≥ `inter_lv` → thắng → Continue | coin bay, popup đóng, interstitial, rồi level kế | kẹt màn sau popup thắng |
| Win liền | thắng tiếp level kế trong < `inter_cd` giây | không interstitial | interstitial lần hai |
| Retry | tập Ads, level ≥ `inter_retry_lv` → Retry từ popup thua, popup pause, lose flow | interstitial rồi loading | kẹt trước loading |
| Remove ads | đã mua remove ads → thắng, Retry, bấm rewarded | không interstitial, không banner; rewarded vẫn chạy | interstitial hiện |
| Mở thêm box | bấm slot box khoá → xem quảng cáo hết | popup đóng, slot mở sau khi quảng cáo đóng | slot mở lúc quảng cáo còn che màn, hoặc không mở |
| Banner | level ≥ `banner_level` → vào level | banner ở đáy, booster bar ngay trên banner | banner đè booster bar, hoặc dải trống mà không banner |
| Banner khởi động muộn | mở app, vào thẳng level có banner | banner hiện sau khi MAX khởi tạo xong | banner không bao giờ hiện trong phiên đó |
| Banner và quảng cáo toàn màn | có banner → xem rewarded, hoặc thắng ra interstitial | banner ẩn lúc quảng cáo chiếu, hiện lại khi đóng | banner nằm trên quảng cáo, hoặc mất tới level sau |
| Banner và Shop | có banner → mở Shop, đóng Shop | banner ẩn khi Shop mở, hiện lại khi đóng | banner che nút mua |
| Remove ads giữa level | có banner → mua remove ads trong Shop → đóng Shop | banner ẩn ngay, không hiện lại; dải đáy còn tới level sau | banner hiện lại |

**Mốc commit (dự án):** `Ads dùng bản Horcrux, xoá bản tạm`, kèm con trỏ `Assets/Horcrux`.

---

## File → giai đoạn

| File | GĐ |
|---|---|
| `Runtime/Abstractions/Foundations/Ads/RewardedAdResult.cs` | 1 |
| `Runtime/Abstractions/Foundations/Ads/IAdVendor.cs` | 1, 2, 3, 4 |
| `Runtime/Abstractions/Foundations/Ads/IAdService.cs` | 1, 2, 3, 4 |
| `Runtime/Implementations/Foundations/Ads/AdService.cs` | 1, 2, 3, 4 |
| `Tests/PlayMode/AdServiceTests.cs` (agent) | 1, 2, 3, 4 |
| `Vendors/Zego/com.horcrux.zego.asmdef` | 5 |
| `Vendors/Zego/ZegoAdVendor.cs` | 5 |
| `docs/Runtime/AdsSystem/AdsSystem.md`, `.html` (agent) | 6 |
| dự án: 4 popup, `InterstitialScenarios.cs`, `GameplayBanner.cs`, `PersistentData.cs`, `AdNotAvailableToast.cs`, `Service.unity`, xoá 2 file bản tạm, `Ads.md` (agent) | 7 |
