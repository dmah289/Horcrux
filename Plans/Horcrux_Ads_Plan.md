# Plan — Ads lên Horcrux: `AdService` (rewarded, interstitial, banner) và bộ chuyển Zego MAX

Bản tạm: category-jam `Assets/_Core/Ads/IAdService.cs` (contract), `Assets/_Modules/Sdk/Ads/ZegoAdService.cs` (bản cài); luật kịch bản `Assets/_Game/Scripts/Ads/` ở lại dự án.
**Phụ thuộc:** không — gõ lúc nào cũng được. Trong Plan gõ liền GĐ1–5 rồi mới Play: `AdService` nhận `IAdVendor` qua InitArgs, vendor tới GĐ5 mới có (giả định cần xác nhận: Play giữa chừng thì InitArgs báo thiếu dependency lúc boot, hoặc không tạo service).
Kiểm: `bash "<horcrux>/Editor/CompileCheck/Tools~/verify.sh" --csproj com.horcrux.runtime.csproj,Horcrux.PlayModeTests.csproj --tests Horcrux.EditModeTests`; GĐ1–4 bảng case `Tests/PlayMode/AdServiceTests.cs` (vendor giả) ở Window › General › Test Runner; GĐ5 thêm `com.horcrux.zego.csproj`, chỉ compile — sự kiện MAX là static event, chỉ máy thật bắn.

## Kịch bản

| Tình huống | Hệ làm gì | Caller / người chơi thấy |
|---|---|---|
| bấm nút rewarded, quảng cáo sẵn, xem hết | chiếu; SDK báo nhận thưởng → `onEarned` đúng một lần; đợi quảng cáo đóng | `Earned` sau khi quảng cáo đóng; dữ liệu ghi lúc SDK báo (tắt app ngay lúc đó vẫn không mất thưởng), trình diễn sau `await` |
| SDK báo thưởng sau báo đóng | đợi thưởng ≤ 0,5 s thật | `Earned` |
| đóng sớm, không thưởng | hết 0,5 s | `Skipped`; popup giữ, nút bấm lại được |
| thưởng về sau khi đã chốt `Skipped`, hoặc về hai lần | bỏ — popup đã mở lại nút, cấp lúc này là ngoài luồng | `onEarned` không chạy thêm |
| bấm lúc quảng cáo chưa tải (vừa vào game, mất mạng) | đợi ≤ 2 s thật; hết giờ báo SDK mất cơ hội, bắn `RewardedNotAvailable(placement)` | `NotAvailable`; listener dự án hiện "không có quảng cáo"; nút bấm lại được |
| quảng cáo hiển thị lỗi | bắn `RewardedNotAvailable` | `NotAvailable` kèm chữ báo |
| bấm rewarded khi quảng cáo khác đang chiếu (bấm liền hai lần) | từ chối, log warning, không bắn sự kiện | `NotAvailable` ngay; chỉ một quảng cáo, thưởng một lần |
| caller huỷ token sau khi quảng cáo đã hiện | lượt chiếu chạy tới lúc đóng; cờ, đồng hồ, thưởng vẫn đúng | caller nhận `OperationCanceledException` |
| popup tự huỷ lúc đóng mà chờ interstitial bằng token của chính nó | như trên | `await` ném, `UniTaskVoid` nuốt, màn kế không nạp — token phải thuộc chủ bước **sau** lượt chiếu |
| `onEarned` ném · listener sự kiện ném | `LogException` | kết quả không đổi |
| Zego compare (`compare_with_inter_when_show_rewarded_permission` = `"1"`) chiếu interstitial thay rewarded | vendor nghe đóng của cả Interstitial | lượt chốt bình thường |
| chuyển cảnh đã qua mốc, interstitial sẵn, đủ giãn cách | chiếu; đóng thì đặt mốc | `await` xong khi quảng cáo đóng |
| interstitial đầu phiên, kể cả mở lại app | chưa có mốc đóng | hiện được ngay |
| chưa đủ `minIntervalSeconds` từ quảng cáo toàn màn gần nhất đóng, kể cả rewarded (xem x2 rồi rời popup thắng) | bỏ im lặng | xong ngay, không interstitial |
| đã mua remove ads | interstitial, banner bỏ, không báo SDK mất cơ hội | rewarded vẫn chạy |
| interstitial chưa sẵn | báo SDK mất cơ hội, không đợi | xong ngay |
| SDK không chiếu interstitial (vendor trả `false`) | không đặt mốc | lượt sau gọi được ngay |
| vào màn có banner | dự án đọc `BannerHeightPixels` (có trước khi banner tải), chừa dải đáy, `ShowBanner` | banner ở đáy, nút ngay trên |
| `ShowBanner` trước khi MAX khởi tạo xong | giữ ý muốn, áp khi `BannerAvailable` (MAX khởi tạo + 1 frame) | banner hiện muộn, không mất |
| quảng cáo toàn màn chiếu | ẩn banner ngay trước `Show`, hiện lại khi lượt xong | banner không đè quảng cáo |
| rewarded đợi 2 s rồi hết giờ | không lệnh banner | banner không chớp |
| `ShowBanner` lặp mỗi lần vào màn | so với lệnh đã gửi | một lệnh mỗi lần đổi |
| rời màn, mở màn cần trống đáy | dự án `HideBanner` | banner ẩn; dải đáy giữ tới hết màn |
| mua remove ads giữa màn | cửa ghi remove ads ở dự án gọi `HideBanner`; `ShowBanner` sau đó bị `IsAdsRemoved` chặn | banner ẩn ngay, không hiện lại |
| gỡ package `com.zego.advertisement.max` | define biến mất, asmdef `com.horcrux.zego` tắt | dự án vẫn compile; lúc chạy `IAdService` thiếu vendor |

## Flow

```
popup (dự án) ─────────ShowRewardedAsync────┐                                    IAdService.cs · RewardedAdResult.cs
luật kịch bản (dự án) ─ShowInterstitialAsync┤   mốc level, giãn cách, placement, màn nào muốn banner: dự án giữ
luật banner (dự án) ───ShowBanner · HideBanner · BannerHeightPixels
                                            ▼
                              AdService : IAdService                       AdService.cs — [Service], InitArgs tạo; quyết KHI NÀO chiếu
                               │  cờ đang chiếu · mốc đóng gần nhất · ý muốn banner · banner đã gửi
                               │  Init(IAdVendor) ◄──────────── ZegoAdVendor : IAdVendor        ZegoAdVendor.cs — [Service]; chiếu THẾ NÀO · IAdVendor.cs
                               │                                 ├─ Zego IAdService: ShowRewardedVideo · ShowInterstitial · ShowBanner · HideBanner
                               │                                 ├─ Zego IRemoveAdsStatus (dự án đăng ký)
                               │                                 ├─ MaxSdkCallbacks Rewarded|Interstitial (lượt rewarded)
                               │                                 └─ MaxSdkCallbacks.OnSdkInitializedEvent → BannerAvailable · MaxSdkUtils (chiều cao)
                               └─ event RewardedNotAvailable(placement) ──► listener dự án ──► thông báo "không có quảng cáo"
luật banner (dự án) : IBottomInsetProvider ──► BottomInset (Horcrux Layout) ──► panel safe area HUD · ViewportCamera
dự án ──► Zego IAdService.Init() trước fetch remote config        prefab Zego.Advertisement.MaxDefault ở scene đầu
```

## Thuật toán

```
ShowRewardedAsync(placement, onEarned, ct)
  đang chiếu quảng cáo ──► NotAvailable ngay (warning, không sự kiện)
  đợi IsRewardedReady ≤ 2 s thật ── hết giờ ──► ReportRewardedNotReady · RewardedNotAvailable ──► NotAvailable
  ApplyBanner (ẩn) · vendor.ShowRewardedAsync(placement, OnReward)        kiểm sẵn sàng và Show cùng frame
      false (hiển thị lỗi) ──► RewardedNotAvailable ──► NotAvailable
      true  (đã đóng)      ──► đặt mốc giãn cách · chưa thưởng thì đợi ≤ 0,5 s thật ──► Earned | Skipped
  OnReward: chỉ khi đang chiếu rewarded và chưa thưởng ──► onEarned
  finally: gỡ cờ · ApplyBanner

ShowInterstitialAsync(placement, minIntervalSeconds, ct)
  đang chiếu · remove ads · chưa đủ minIntervalSeconds từ mốc đóng ──► xong ngay, im lặng
  chưa sẵn ──► ReportInterstitialNotReady ──► xong ngay
  ApplyBanner · vendor.ShowInterstitialAsync ── true (đóng, hiển thị lỗi) ──► đặt mốc · false ──► không mốc
  finally: gỡ cờ · ApplyBanner

ApplyBanner — gọi khi ShowBanner, HideBanner, quảng cáo toàn màn bắt đầu và xong, BannerAvailable
  shouldShow = ý muốn ∧ không quảng cáo toàn màn ∧ ¬IsAdsRemoved
  shouldShow == đã gửi ∨ ¬IsBannerAvailable ──► không gửi · ngược lại ──► đã gửi = shouldShow, vendor ShowBanner | HideBanner
```

Bất biến: mỗi lúc ≤ 1 quảng cáo toàn màn · `onEarned` ≤ 1 lần mỗi lượt · mốc giãn cách là lúc quảng cáo toàn màn gần nhất đóng, đồng hồ thật, không lưu qua phiên · số lệnh banner tới vendor bằng số lần `shouldShow` đổi lúc vendor nhận lệnh được.

---

## GĐ1 — xong thì popup chiếu được rewarded và nhận `Earned` / `Skipped` / `NotAvailable`

```
test (vendor giả) ──ShowRewardedAsync──► AdService+ ──ShowRewardedAsync──► FakeVendor
                                           ▲  OnReward+ ◄───────────────────┘ (test bắn trước/sau đóng)
                                           └─ Earned | Skipped | NotAvailable (chưa sẵn, hiển thị lỗi, đang chiếu)
```

`Runtime/Abstractions/Foundations/Ads/RewardedAdResult.cs` — file mới:

```csharp
namespace Horcrux.Runtime.Abstractions.Ads
{
    // Never saved nor stored in an asset: no explicit values needed.
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

`Runtime/Abstractions/Foundations/Ads/IAdVendor.cs` — file mới:

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

        // No token: a displayed ad cannot be cancelled, and the service must learn when it closes.
        /// <summary>
        /// Called only right after <see cref="IsRewardedReady"/> returned true in the same frame. Completes when the ad closes:
        /// true after it was displayed, false when display failed. <paramref name="onReward"/> may run before or after that.
        /// </summary>
        UniTask<bool> ShowRewardedAsync(string placement, Action onReward);
    }
}
```

`Runtime/Abstractions/Foundations/Ads/IAdService.cs` — file mới:

```csharp
using System;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace Horcrux.Runtime.Abstractions.Ads
{
    // IService<T> sits in the parent namespace Horcrux.Runtime.Abstractions: no using needed; it gives IAdService.Service and TryGet.
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

`Runtime/Implementations/Foundations/Ads/AdService.cs` — file mới:

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

        // Static lambda: capturing a variable becomes a compile error instead of garbage on every wait.
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
            // Set here, inside the try/finally: every exit (early return, throw, cancel) clears the flag exactly once.
            _showingAd = FullscreenAd.Rewarded;
            _isEarned = false;
            _onEarned = onEarned;
            try
            {
                // Placeholder so this stage runs; replaced by the 2 s ready wait.
                if (!_vendor.IsRewardedReady)
                    return RewardedAdResult.NotAvailable;

                bool isDisplayed = await _vendor.ShowRewardedAsync(placement, OnReward);
                if (!isDisplayed)
                    return RewardedAdResult.NotAvailable;

                // Not the caller's ct: a cancelled caller must still get the show settled correctly.
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
            // Rewarded, not != None: a stray reward while an interstitial shows is dropped too.
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

`Tests/PlayMode/AdServiceTests.cs` (agent; vendor giả: `IsRewardedReady` gán được, `ShowRewardedAsync` trả task của một `UniTaskCompletionSource<bool>` test điều khiển, giữ lại `onReward`):

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

---

## GĐ2 — xong thì bấm lúc quảng cáo chưa tải được đợi 2 s, và dự án hiện chữ báo ở một chỗ

```
test ──ShowRewardedAsync──► AdService ──IsRewardedReady (mỗi frame ≤ 2 s)──► FakeVendor
                              │ hết giờ ──ReportRewardedNotReady+─────────────►┘
                              └─ RewardedNotAvailable+(placement) ──► listener của test
```

`IAdVendor.cs` › dưới `ShowRewardedAsync` — thêm:

```csharp
        /// <summary>Tells the SDK a rewarded opportunity was lost because no ad was ready (fill tracking).</summary>
        void ReportRewardedNotReady(string placement);
```

`IAdService.cs` › trên `ShowRewardedAsync` — thêm:

```csharp
        // Not raised when refused because another ad shows: "no ad available" would be wrong then.
        /// <summary>Raised with the placement when no rewarded ad was shown (not ready in time, display failed).</summary>
        event Action<string> RewardedNotAvailable;
```

`AdService.cs` › region `Properties` — thêm cạnh hằng cũ:

```csharp
        private const float RewardedReadyWaitSeconds = 2f;

        private static readonly Func<AdService, bool> IsRewardedReady = static s => s._vendor.IsRewardedReady;
```

`AdService.cs` › ngay trên region `API` — thêm:

```csharp
        public event Action<string> RewardedNotAvailable;
```

`AdService.cs` › `ShowRewardedAsync` — đoạn cũ → đoạn mới (token của caller đi vào bước đợi):

```csharp
            return RunRewardedAsync(placement, onEarned).AttachExternalCancellation(ct);
```
→
```csharp
            return RunRewardedAsync(placement, onEarned, ct).AttachExternalCancellation(ct);
```

`AdService.cs` › `RunRewardedAsync` — đoạn cũ → đoạn mới:

```csharp
        private async UniTask<RewardedAdResult> RunRewardedAsync(string placement, Action onEarned)
        {
            // Set here, inside the try/finally: every exit (early return, throw, cancel) clears the flag exactly once.
            _showingAd = FullscreenAd.Rewarded;
            _isEarned = false;
            _onEarned = onEarned;
            try
            {
                // Placeholder so this stage runs; replaced by the 2 s ready wait.
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
            // Set here, inside the try/finally: every exit (early return, throw, cancel) clears the flag exactly once.
            _showingAd = FullscreenAd.Rewarded;
            _isEarned = false;
            _onEarned = onEarned;
            try
            {
                // Ready check and Show stay in one frame: the SDK silently skips a show that is not ready.
                // ct reaches the ready wait only: cancelled here, Show is never called.
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

`AdService.cs` › region `Class Methods` — thêm dưới `OnReward`:

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

`Tests/PlayMode/AdServiceTests.cs` (agent):

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

---

## GĐ3 — xong thì luật kịch bản chèn được interstitial, chung cờ chiếu và giãn cách với rewarded

```
test ──ShowInterstitialAsync+(placement, s)──► AdService ──IsAdsRemoved+ · IsInterstitialReady+──► FakeVendor
                                                │  cờ chiếu (GĐ1) · mốc đóng gần nhất+
                                                │ chưa sẵn ──ReportInterstitialNotReady+──────────────────────► │
                                                └─ ShowInterstitialAsync+ ────────────────────────────────────► ┘
test ──ShowRewardedAsync──► AdService: rewarded đóng → đặt mốc+
```

`IAdVendor.cs` › dưới `ReportRewardedNotReady` — thêm:

```csharp
        // On the vendor: remove ads is the ad SDK's state; a vendor for another SDK reads its own IAP.
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

`IAdService.cs` › dưới `ShowRewardedAsync` — thêm:

```csharp
        /// <summary>
        /// Shows an interstitial and waits until it closes. Completes at once, silently, when an ad is showing, ads are removed,
        /// fewer than <paramref name="minIntervalSeconds"/> passed since the last fullscreen ad closed, or none is ready.
        /// </summary>
        UniTask ShowInterstitialAsync(string placement, float minIntervalSeconds, CancellationToken ct);
```

`AdService.cs` › region `Properties` — thêm dưới `_showingAd`:

```csharp
        // null: no fullscreen ad closed yet this session, the first interstitial need not wait.
        private float? _lastFullscreenClosedAt;
```

`AdService.cs` › region `API` — thêm dưới `ShowRewardedAsync`:

```csharp
        public UniTask ShowInterstitialAsync(string placement, float minIntervalSeconds, CancellationToken ct)
        {
            // Removed before ready: a player who bought remove ads is not a lost opportunity.
            if (_showingAd != FullscreenAd.None || _vendor.IsAdsRemoved || !HasIntervalPassed(minIntervalSeconds))
                // Silent: the player pressed nothing, so there is nothing to report.
                return UniTask.CompletedTask;

            // Last check before Show: ready and Show must fall in the same frame.
            if (!_vendor.IsInterstitialReady)
            {
                _vendor.ReportInterstitialNotReady(placement);
                return UniTask.CompletedTask;
            }

            return RunInterstitialAsync(placement).AttachExternalCancellation(ct);
        }
```

`AdService.cs` › `RunRewardedAsync` — đoạn cũ → đoạn mới (rewarded đã hiện thì đặt mốc, trước bước đợi thưởng muộn):

```csharp
                if (!isDisplayed)
                {
                    RaiseRewardedNotAvailable(placement);
                    return RewardedAdResult.NotAvailable;
                }

                // Not the caller's ct: a cancelled caller must still get the show settled correctly.
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
                // Not the caller's ct: a cancelled caller must still get the show settled correctly.
                if (!_isEarned)
```

`AdService.cs` › region `Class Methods` — thêm dưới `RunRewardedAsync`:

```csharp
        // No ct: nothing to wait for before Show; the caller's token only cuts its own await.
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

        // Real clock: ads run while the app nearly stalls, and Time.time stops at timeScale 0.
        private void MarkFullscreenClosed() => _lastFullscreenClosedAt = Time.realtimeSinceStartup;
```

`AdService.cs` › region `Properties` › enum `FullscreenAd` — thêm dưới `Rewarded`:

```csharp
            Interstitial,
```

`Tests/PlayMode/AdServiceTests.cs` (agent; vendor giả thêm `IsAdsRemoved`, `IsInterstitialReady` gán được, `ShowInterstitialAsync` trả task của một `UniTaskCompletionSource<bool>` test điều khiển, đếm `Show` và `ReportInterstitialNotReady`):

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

---

## GĐ4 — xong thì dự án bật tắt banner theo ý muốn, banner tự né quảng cáo toàn màn và remove ads

```
test ──ShowBanner+ · HideBanner+──► AdService: ý muốn+ ──► ApplyBanner+ ──IsBannerAvailable+ · IsAdsRemoved──► FakeVendor
                                       ▲  cờ chiếu (GĐ1)              └──ShowBanner+ · HideBanner+───────────────────► │
test bắn BannerAvailable+ ────────────┘◄──────────────────────────────────────────────────────────────────────────────┘
RunRewardedAsync · RunInterstitialAsync: trước Show và trong finally ──► ApplyBanner+
```

`IAdVendor.cs` › dưới `ReportInterstitialNotReady` — thêm:

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

`IAdService.cs` › dưới `ShowInterstitialAsync` — thêm:

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

`IAdService.cs` › summary của interface đổi thành `Fullscreen ads, one at a time, and the bottom banner they hide. Cancelling a call's token stops the caller's await only; a displayed ad still runs to its close.`

`AdService.cs` › region `Properties` — thêm dưới `_lastFullscreenClosedAt`:

```csharp
        private bool _isBannerRequested;
        // Kept apart from the wish: one flag would lose the project's wish on HideBanner during an ad.
        // What the vendor was last told; only ApplyBanner writes it.
        private bool _isBannerShown;
```

`AdService.cs` › region `API` — thêm dưới `ShowInterstitialAsync`:

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

`AdService.cs` › `RunRewardedAsync` — đoạn cũ → đoạn mới, hai chỗ:

```csharp
                bool isDisplayed = await _vendor.ShowRewardedAsync(placement, OnReward);
```
→
```csharp
                // Right before Show, not when the flag is set: banner stays during the 2 s wait, no flicker on timeout.
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
                // After clearing the flag: before it, the comparison still sees an ad showing.
                ApplyBanner();
            }
```

`AdService.cs` › `RunInterstitialAsync` — đoạn cũ → đoạn mới:

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

`AdService.cs` › region `Class Methods` — thêm dưới `MarkFullscreenClosed`:

```csharp
        // The one gate to the vendor's banner, called whenever one of its inputs changes.
        private void ApplyBanner()
        {
            bool shouldShow = _isBannerRequested && _showingAd == FullscreenAd.None && !_vendor.IsAdsRemoved;
            // Not available: keep what was sent, so the comparison still differs when BannerAvailable arrives.
            if (shouldShow == _isBannerShown || !_vendor.IsBannerAvailable)
                return;

            _isBannerShown = shouldShow;
            if (shouldShow)
                _vendor.ShowBanner();
            else
                _vendor.HideBanner();
        }
```

`AdService.cs` › region `DI` — đoạn cũ → đoạn mới:

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

`Tests/PlayMode/AdServiceTests.cs` (agent; vendor giả thêm `BannerHeightPixels`, `IsBannerAvailable` gán được, một hàm bắn `BannerAvailable`, đếm `ShowBanner`, `HideBanner` theo thứ tự xen với `ShowRewardedAsync`, `ShowInterstitialAsync`):

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

---

## GĐ5 — xong thì dự án có Zego MAX chạy cả ba loại mà không viết dòng nào

```
AdService ──ShowRewardedAsync──► ZegoAdVendor+ ──ShowRewardedVideo(placement, onReward)──► Zego IAdService
                                   ▲ true  ◄── MaxSdkCallbacks.Rewarded|Interstitial.OnAdHiddenEvent
                                   └ false ◄── MaxSdkCallbacks.Rewarded|Interstitial.OnAdDisplayFailedEvent
AdService ──ShowInterstitialAsync──► ZegoAdVendor+ ──ShowInterstitial(placement, onClose)──► Zego IAdService
                                   ▲ true ◄── onClose (đóng hoặc hiển thị lỗi) · false ◄── Zego trả false
AdService ──IsAdsRemoved──► Zego IRemoveAdsStatus (dự án đăng ký)
AdService ──ShowBanner · HideBanner──► ZegoAdVendor+ ──► Zego IAdService.ShowBanner · HideBanner
          ◄──BannerAvailable── ZegoAdVendor+ ◄── MaxSdkCallbacks.OnSdkInitializedEvent, đợi thêm 1 frame
          ──BannerHeightPixels──► ZegoAdVendor+ ──► MaxSdkUtils.GetAdaptiveBannerHeight × GetScreenDensity
```

**Editor setup**

1. Project window › `Vendors/Zego/` › Create › Assembly Definition, đặt tên `com.horcrux.zego`.
2. Dán nội dung dưới. `Zego` là asmdef của `com.zego.core` (`IAdService`, `IRemoveAdsStatus`, `AdStatic`, `EAdFormat`); `MaxSdk.Scripts` thuộc `com.applovin.mediation.ads`.

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

`Vendors/Zego/ZegoAdVendor.cs` — file mới:

```csharp
using System;
using Cysharp.Threading.Tasks;
using Horcrux.Runtime.Abstractions.Ads;
using Sisus.Init;
using UnityEngine;
using Zego;
// Alias outside the namespace: bare IAdService is ambiguous, and inside Horcrux.Vendors.Zego the name Zego means this namespace.
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
            // Before Show: in the Editor, MAX's test ad may close inside that call.
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
            // No MAX events here: Zego calls onClose on every exit, MAX too would be two sources.
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
            // Interstitial too: compare mode may show one instead; listening to Rewarded only hangs the show until restart.
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

        // TrySetResult: MAX may raise hidden after display-failed for the same ad; the second call is a no-op.
        private void OnRewardedHidden(string adUnitId, MaxSdkBase.AdInfo adInfo) => _rewardedClosed?.TrySetResult(true);

        private void OnRewardedDisplayFailed(string adUnitId, MaxSdkBase.ErrorInfo errorInfo, MaxSdkBase.AdInfo adInfo) =>
            _rewardedClosed?.TrySetResult(false);

        // TrySetResult: Zego may call onClose inside ShowInterstitial before returning false; the method still returns false.
        private void OnInterstitialClosed() => _interstitialClosed?.TrySetResult();

        // One shot: the event is static and outlives Play sessions when domain reload is off.
        private void OnSdkInitialized(MaxSdkBase.SdkConfiguration configuration)
        {
            MaxSdkCallbacks.OnSdkInitializedEvent -= OnSdkInitialized;
            MarkBannerAvailableNextFrameAsync().Forget();
        }

        // Zego builds its banner in its own handler of the same event; handler order is not defined.
        // A ShowBanner sent before Zego's handler is dropped silently: the banner never shows, nothing is logged.
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

---

## Chuyển dự án (agent)

| Chỗ | Đổi |
|---|---|
| thứ tự với Plan khác | dải đáy qua `BottomInset` của [ViewportAnchor](Horcrux_ViewportAnchor_Plan.md) GĐ2, chưa gõ thì giữ `BannerSpace` của dự án · 3 popup chung với Chuyển dự án của [SpendGate](Horcrux_SpendGate_Plan.md) — chuyển SpendGate trước |
| `docs/Runtime/AdsSystem/AdsSystem.md`, `.html` | tạo theo `docs`: luồng rewarded, interstitial, banner · bảng kết quả → caller làm gì · token của ai (chủ bước sau lượt chiếu, không phải popup sắp đóng) · cách viết lớp luật kịch bản ở dự án (mốc, giãn cách, placement, màn nào muốn banner — tên trung tính) · quyết định trái trực giác · lỗi Zego ở chế độ compare · Mở rộng sau |
| `AdsSystem.md` › Trước khi chạy | prefab `Zego.Advertisement.MaxDefault` ở gốc scene đầu — thiếu: Zego `LogError` lúc boot, mọi rewarded `NotAvailable`, interstitial không hiện |
| | `Zego.IAdService.Global?.Init()` sớm nhất, trước fetch remote config — thiếu: MAX không tải quảng cáo nào |
| | ad unit id, SDK key (menu Zego, `AppLovinSettings`), ad unit id banner — thiếu banner: `ShowBanner` không hiện gì, không lỗi |
| | bản cài `Zego.IRemoveAdsStatus` đăng ký lúc boot — thiếu: người đã mua remove ads bị báo "mất cơ hội" cho SDK (Zego vẫn không chiếu) |
| | một listener `RewardedNotAvailable` — thiếu: người chơi bấm, đợi 2 s, không thấy gì |
| | màn có banner chừa dải đáy bằng `BannerHeightPixels` qua `BottomInset` trước khi dựng màn — thiếu: banner che nút sát đáy |
| | cửa ghi remove ads gọi `HideBanner` — thiếu: banner đang hiện còn tới lúc rời màn |
| `LiveSystem_PopupRefillLiveOffer`, `PopupOfferRevive`, `PopupWin`, `PopupAddBox` | `Service.Get<IAdService>()` → `IAdService.Service`, thêm `using Horcrux.Runtime.Abstractions.Ads;`, giữ `using Core;` nếu còn dùng; placement giữ `refill_live`, `revive`, `win_x2`, `add_box`; làm sau Chuyển dự án của SpendGate |
| `InterstitialScenarios` | như trên; `RewardedScenarios`, `AdLevelGate` ở lại dự án (mốc level, tập user, lượt play test); `MainController.LeaveAfterWin` (chờ bằng token của `MainController`), `MainController.RetryLevel` không đổi |
| `GameplayBanner` | bốn chỗ `Service.Get<IAdService>()` → `IAdService.Service`; dải đáy qua `BottomInset` (Chuyển dự án của `Horcrux_ViewportAnchor_Plan.md`) hoặc giữ `BannerSpace`; `PopupShop`, `MainController` không đổi |
| `PersistentData.RemoveAds` | `Service.TryGet<Core.IAdService>(out var ads)` → `IAdService.TryGet(out var ads)` |
| `Assets/_Modules/Sdk/Ads/AdNotAvailableToast.cs` | tạo, `MonoBehaviour<IAdService>`: đăng ký `RewardedNotAvailable` ở `OnEnable`, gỡ ở `OnDisable`; có sự kiện thì `INotificationService.TryGet(out var n)` → `n.ShowNotification("Ad not available")`, thiếu service thì `LogError` |
| `Service.unity` (developer) | GameObject chứa `NotificationManager` › Add Component › `AdNotAvailableToast`, lưu scene — thiếu: hết quảng cáo mà không chữ báo |
| `Assets/_Core/Ads/IAdService.cs`, `Assets/_Modules/Sdk/Ads/ZegoAdService.cs` | xoá; không asset nào gắn, không cần giữ GUID |
| `Assets/_Modules/Sdk/Ads/Ads.md` | rút còn phần dự án: bảng placement rewarded → mốc → `onEarned` → sau khi đóng; bảng kịch bản interstitial → chỗ chèn → mốc level của tập user; banner theo màn, Shop, dải đáy; chỗ gọi `Init`; vị trí listener; cơ chế trỏ `docs/Runtime/AdsSystem/AdsSystem.md` |
| Plan này | xoá cùng `.meta` |
| chơi thử | build quảng cáo thật hoặc test, chạy bảng Kịch bản: Free ở popup thêm mạng, hồi sinh, Get x2 popup thắng, nút quảng cáo mở box · thắng, Retry ở tập Ads qua `inter_lv`, `inter_retry_lv`, thắng liền < `inter_cd` · level ≥ `banner_level`, mở Shop, mua remove ads giữa level · tắt mạng · Console lúc boot không lỗi InitArgs về `IAdVendor`; hỏng: popup kẹt, thưởng hai lần hay lúc quảng cáo còn che màn, interstitial ngay sau rewarded, kẹt màn sau popup thắng, banner đè quảng cáo hay booster bar |

## Không làm · Mở rộng sau

| | Phương án | Vì sao |
|---|---|---|
| bỏ | tách service rewarded và interstitial | cờ "đang chiếu" và đồng hồ giãn cách cần chủ thứ ba, hai service phải báo nhau; banner đọc chính cờ đó (developer chốt) |
| bỏ | dồn hết vào bộ chuyển Zego | ít file hơn nhưng không test được ngoài máy thật |
| bỏ | service tự đọc mốc level và giãn cách | phải biết level, tập user của dự án; dự án khác phân tập khác thì sửa Horcrux |
| bỏ | interval cấu hình trên service (`SetInterval`, `ForceCompleteInterval`) | số đổi theo tập user và remote, mỗi kịch bản có thể một số; tham số lời gọi giữ nó ở một chủ là dự án |
| bỏ | sự kiện khi interstitial bị bỏ | người chơi không bấm gì, không có gì phải báo |
| bỏ | cờ `bool _isShowing` thay enum `FullscreenAd` | GĐ3 cần biết loại đang chiếu để `OnReward` bỏ thưởng lạc; hai `bool` loại trừ nhau là enum viết rời |
| bỏ | `UniTaskCompletionSource` trong lõi để nghe đóng | việc nghe đóng thuộc vendor, lõi chỉ `await` |
| bỏ | rewarded trả `bool` | `Skipped` và `NotAvailable` cho hai phản ứng khác nhau: ở yên popup, hiện chữ báo lỗi |
| bỏ | lõi tự hiện chữ báo lỗi | Horcrux không có hệ thông báo; chữ, ngôn ngữ, kiểu toast là của dự án |
| bỏ | mỗi popup tự hiện chữ khi nhận `NotAvailable` | cùng một phản ứng chép ở mọi caller |
| bỏ | số 2 s thành tham số của `ShowRewardedAsync` | chưa call site nào truyền giá trị khác |
| bỏ | interstitial trả `bool` "đã chiếu" | chưa caller nào rẽ nhánh theo nó; thêm sau không đổi chữ ký cũ |
| bỏ | interstitial đợi sẵn sàng | người chơi đang chuyển cảnh, bắt đợi để xem quảng cáo tệ hơn mất một lượt |
| bỏ | hạn giờ cho một lượt chiếu | SDK không báo đóng là lỗi SDK; hạn giờ phải đoán một số mà mọi quảng cáo dài đều vượt |
| bỏ | vendor tự giữ lệnh banner tới khi SDK khởi tạo | ý muốn có hai chủ, luật hoãn chỉ test được trên máy thật |
| bỏ | dự án tự ẩn, hiện banner quanh mỗi lời gọi quảng cáo toàn màn | mọi caller lặp cùng cặp lệnh; một chỗ quên là banner đè quảng cáo |
| bỏ | gửi lệnh banner mỗi lần gọi, không so lần gửi trước | lần đầu sau khi vendor sẵn sàng cả `BannerAvailable` lẫn `ShowBanner` cùng gửi; phép so giữ một lệnh mỗi lần đổi, test đếm được |
| bỏ | lõi nghe remove ads để tự ẩn banner | vendor không có sự kiện đổi remove ads; cửa ghi remove ads ở dự án gọi `HideBanner` |
| bỏ | thu, dời dải đáy giữa màn khi banner ẩn | bàn chơi neo world một lần lúc dựng; dời HUD mà world đứng yên thì hai bên lệch |
| bỏ | sự kiện đổi chiều cao banner | dải chốt lúc vào màn, không ai nghe nó giữa màn |
| bỏ | nghe `AdFullscreenClosedEvent` của Zego cho rewarded | chỉ bắn khi rewarded đóng, không khi hiển thị lỗi, không ở nhánh compare; vẫn phải nghe MAX |
| bỏ | `versionDefines` kiểm cả `com.applovin.mediation.ads` | `MaxSdk.Scripts` là dependency của `com.zego.advertisement.max`, kiểm một package đủ |
| bỏ | gọi Zego `Init` qua `Bootstrap` của Horcrux | `BootstrapService` chạy sau fetch remote config; MAX phải khởi tạo trước để có thêm thời gian tải quảng cáo |
| bỏ | sửa lỗi Zego ở chế độ compare: interstitial hiển thị lỗi vẫn gọi `onReceive`, lõi chạy `onEarned` rồi trả `NotAvailable` | lỗi của Zego; key mặc định `"0"` — bật key thì báo đội Zego, hoặc chấp nhận cấp nhầm ở ca này |
| bỏ | `IRemoveAdsService`, IAP trong Horcrux | chưa có caller; remove ads lúc này đọc từ SDK qua vendor |
| bỏ | giới hạn số lần xem rewarded, số interstitial mỗi phiên | chưa có yêu cầu |
| bỏ | màn "Ad break" đếm ngược trước interstitial · tắt tiếng game khi quảng cáo chạy · cheat bỏ qua quảng cáo | chưa có yêu cầu |
| bỏ | vòng chờ trên UI trong 2 s đợi rewarded | nút chỉ bị chặn; popup tự thêm nếu cần |
| bỏ | banner trên đỉnh màn, MREC, collapsible | chưa có thiết kế nào dùng |
| sau | thu dải đáy khi banner tải lỗi | cần vendor báo tải xong, tải lỗi; hiện dải luôn bằng chiều cao dự kiến |
| sau | compare chọn được interstitial khi rewarded chưa sẵn | `IsRewardedVideoReady` của Zego chỉ hỏi rewarded nên lõi trả `NotAvailable`; làm khi bật key compare |
| sau | giãn cách theo Day Active, kịch bản theo tập user (`SystemPlan.md` §21) | thuộc lớp luật của dự án, truyền số giây vào `ShowInterstitialAsync`; service không đổi |
