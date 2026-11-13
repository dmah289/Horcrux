# User Segmentation

Code: `Runtime/Abstractions/Composites/UserSegmentation/` · `Vendors/Adjust/AdjustCampaignBootStep.cs`

Mỗi user ở đúng **một tập**. Tập là `segment_id` kiểu `int` lấy từ dữ liệu của dự án; `0` nghĩa là chưa quyết. Campaign
Adjust lúc cài là input **ban đầu**: nó chỉ đổi tập khi chưa luật hành vi nào quyết tập. Mua IAP thật là luật hành vi, chuyển
user sang tập "đã mua". Tập đổi được ở mọi phiên. Consumer (Ads, IAP, kinh tế) đọc
`CurrConfig`: hàng của tập hiện tại trong **pool**, đã ép kiểu thành DTO của dự án. Hàng của tập "đã mua" kế thừa mọi ô
`-1` từ hàng của tập ngay trước đó.

Thêm tập là thêm hàng dữ liệu (một hàng pool, một hàng keyword), không build.

## Ngữ cảnh đã chốt

| Mục | Chốt |
|---|---|
| Người dùng | code Ads, IAP, kinh tế của dự án đọc config theo tập; developer vận hành hai bảng (pool, keyword) trên remote config; dự án sau bê nguyên lõi |
| Ranh giới | lõi không gọi tên type dự án, không đọc remote config, không biết SDK wrapper nào. Lõi chỉ biết hai **vai**: `default` (tập rơi về) và `purchased` (tập sau khi mua), qua hai id dự án trả về. Dự án khai hai DTO và đưa model đã ép kiểu vào host |
| Chỗ đặt | `Abstractions/Composites/` vì dựng trên Persistence, Bootstrap, EventBus. Entry save là partial của `BasePersistenceDataCollection` nằm **trong thư mục hệ**: tầng Foundations không được tham chiếu type Composites |
| Vendor | `com.horcrux.adjust` tham chiếu thẳng `AdjustSdk.Scripts`, chỉ biên dịch khi có `com.adjust.sdk` ≥ 5.0.0 (`versionDefines` → `HORCRUX_ADJUST`). Thiếu package thì hệ vẫn chạy: mọi user ở tập `default` |

## Hai phía

| Horcrux giữ | Dự án khai |
|---|---|
| `AUserSegmentationHost` (boot step, toàn bộ cơ chế) · `AUserSegmentationHost<TConfig>` (tra và cache config) | host con: hai id vai, pool, bảng keyword, kèm một `Initializer` |
| `IUserSegmentConfig<TSelf>` · `IUserSegmentKeywordRule` | DTO hàng pool (có `InheritFrom`) · DTO hàng keyword |
| `UserSegmentDecider` · `UserSegmentKeywordMatcher` · `UserSegmentInheritance` · `UserSegmentConfigTable` — static thuần | — |
| `UserSegmentState` + entry `userSegmentState` trong base collection | `Key` của entry trên asset collection |
| `UserSegmentChanged` (EventBus) | listener nếu cần (tracking) |
| `AdjustCampaignBootStep` | Adjust SDK đã được wrapper của dự án init; gọi `ReportPurchase()` khi mua thật |

## Luồng dữ liệu

```
boot     BootstrapService, theo Order
         ├─ SaveBootstep             collection.Initialize(): entry userSegmentState nạp từ PlayerPrefs
         ├─ host.InitializeAsync     state = entry.Value
         │                           ValidateConfigs()  ─► LogError từng dòng dữ liệu sai, không sửa
         │                           Decider.OnBoot     ─► Commit(force)
         └─ AdjustCampaignBootStep   không phải phiên 1? xong
                                     GetAttribution mỗi 0.5 s, chặn boot tối đa firstSessionWaitSeconds
                                     chưa có: poll nền tối đa lateArrivalPollSeconds
                                     có attribution ─► host.ReportCampaign(Campaign ?? "")
                                                         Matcher.TryMatch ─► Decider.OnCampaign ─► Commit
                                     hết hạn ─► log, thôi

chơi     mua thật ─► host.ReportPurchase() ─► Decider.OnPurchase ─► Commit

Commit   entry.Value = state                     luôn: SessionCount, LastCampaign có thể đổi mà tập không đổi
         có thay đổi hoặc force ─► OnStateApplied ─► CurrConfig = hàng của tập
                                                     tập purchased: hàng purchased.InheritFrom(hàng prev)
         có thay đổi ─► Debug.Log một dòng ─► EventBus<UserSegmentChanged>.Publish

đọc      consumer ─► host.CurrConfig             field cache, không cấp phát
```

Nhịp: mọi phép tính chạy theo sự kiện (boot, campaign, mua), không gì chạy theo frame. `Configs` và `KeywordRules` đọc lại
ở mỗi sự kiện, không cache trong host. `InheritFrom` cấp phát một hàng mới nên kết quả được cache vào `CurrConfig`.

## Luật

| Sự kiện | Điều kiện | Kết quả |
|---|---|---|
| `OnBoot` | `CurrSegmentId == 0` | chuyển tới `default`, lý do `Initial` |
| `OnBoot` | luôn, sau dòng trên | `SessionCount++` |
| `OnCampaign(camp)` | luôn, trước mọi dòng dưới | `LastCampaign = camp` |
| `OnCampaign(camp)` | `!AcceptsCampaign`: `LastChangeReason` không phải `None`, `Initial`, `CampaignResolved` | không đổi |
| `OnCampaign(camp)` | tập khớp `== purchased` | coi như không khớp rồi áp dòng dưới |
| `OnCampaign(camp)` | còn lại | chuyển tới tập khớp, không khớp hay camp rỗng thì `default`; lý do `CampaignResolved` kèm keyword và cờ `IsAmbiguous` |
| `OnPurchase` | luôn | chuyển tới `purchased`, lý do `Purchase` |
| chuyển tới `X`, lý do `R` | `X == curr` | không đổi; `prev` và lý do giữ nguyên |
| chuyển tới `X`, lý do `R` | còn lại | `prev = curr`, `curr = X`, `LastChangeReason = R`, thay đổi `R` |
| đọc config | `curr != purchased` | hàng của `curr` |
| đọc config | `curr == purchased` | `hàng purchased.InheritFrom(hàng prev)`; `prev == 0` thì hàng `default` |

**Input ban đầu và hành vi.** `LastChangeReason` lưu luật đã đặt tập hiện tại. `AcceptsCampaign` đúng khi lý do là `None`
(save cũ hơn field này), `Initial` hay `CampaignResolved`: tập vẫn do input ban đầu quyết. Mọi lý do khác, kể cả lý do thêm
sau này, là hành vi và thắng campaign. Mọi lần đổi tập đi qua một hàm `MoveTo` ghi `prev`, `curr` và lý do cùng lúc, nên
`PrevSegmentId` luôn là tập ngay trước lần đổi gần nhất. Kế thừa của `purchased` đọc đúng nó: lần đổi cuối trước khi ở
`purchased` luôn là lần vào `purchased`.

**Phiên.** Một phiên là một lần mở app: tiến trình khởi động, `BootstrapService` cold boot đúng một lần, host chạy `OnBoot`
và `SessionCount++`. Phiên kết thúc khi tiến trình chết: user vuốt tắt app, hệ điều hành kill, hay crash. State lưu qua
Persistence nên lần mở sau đọc lại `SessionCount` của lần trước: `SessionCount` là số lần mở app kể từ khi cài.
Không mở phiên mới: xuống nền rồi trở lại (`OnGoToBackground`), chuyển scene hay load level (`ReinitializeAsync`), ở nền lâu
bao nhiêu cũng vậy.

| Lần mở app | `OnBoot` | Attribution chưa về thì |
|---|---|---|
| 1, vừa cài | `SessionCount` 0 → 1, `curr = default` | bước Adjust chặn boot, chờ tối đa `firstSessionWaitSeconds`; chưa có thì poll nền tối đa `lateArrivalPollSeconds`; hết hạn thì log, thôi |
| 2 trở đi | `SessionCount` tăng 1 | bước Adjust thoát ngay; tập giữ nguyên |

Chỉ phiên 1 nhận campaign. `IsFirstSession` là `SessionCount == 1`: lần mở đầu tiên sau khi cài; bước Adjust gác bằng nó,
nên phiên 1 thoát sớm cũng không được poll lại. Bước Adjust poll tới khi có attribution kể cả khi user đã mua: tập không đổi,
nhưng `LastCampaign` có tên camp thật cho tracking.

**Khớp keyword.** Một hàng khớp khi camp chứa **bất kỳ** keyword nào của hàng (`IndexOf(keyword, OrdinalIgnoreCase) >= 0`).
Hàng đầu bảng khớp thì thắng; keyword báo ra là keyword đầu trong **mảng** khớp được, không theo thứ tự trong camp.
`IsAmbiguous` bật khi còn hàng **khác tập** cũng khớp; hai hàng cùng tập cùng khớp không tính. Keyword rỗng và hàng không có
keyword bị bỏ qua.

**Kế thừa.** `UserSegmentInheritance.Pick(target, prev)` trả `prev` khi `target == -1`, còn lại trả `target`. Số `0` là giá
trị thật, không kế thừa. DTO của dự án gọi `Pick` cho từng cột `int` trong `InheritFrom`.

**Tra hàng.** Thiếu hàng của tập thì dùng hàng `default` kèm `LogError`. Thiếu cả hàng `default` thì ném
`InvalidOperationException`. Config trả ra không bao giờ là hàng rỗng.

**Guard dữ liệu** (`UserSegmentConfigTable.Validate`, một lần lúc boot, mỗi lỗi một dòng `LogError`):

| Kiểm | Không kiểm, vì |
|---|---|
| pool không rỗng · hàng không `null` · `segment_id >= 1` · không hai hàng cùng id (hàng cuối thắng khi tra) · có hàng `default` | thiếu hàng `purchased`: lõi không biết vai này có bắt buộc với dự án không |
| bảng keyword không rỗng · hàng không `null` · có ít nhất một keyword · không keyword rỗng · id trỏ tới có hàng pool | ô `-1` ở hàng không phải `purchased` · hàng keyword trỏ vào `purchased`: cần nhìn cột hoặc vai, Simulator kiểm (`docs/Editor/UserSegmentationSimulator.md`) |

## Trước khi chạy

Hệ không tự setup. Dựng từ 0 theo thứ tự:

**1 · DTO hàng pool** — cột `int` nào cũng phải có một dòng `Pick`; đòi đủ khoá để hàng thiếu cột bị từ chối thay vì đọc ra 0:

```csharp
[Serializable]
[JsonObject(ItemRequired = Required.Always)]
public sealed class MySegmentConfig : IUserSegmentConfig<MySegmentConfig>
{
    public int segment_id;
    public int adStartLevel;
    public int startCoins;

    public int Id => segment_id;

    public MySegmentConfig InheritFrom(MySegmentConfig prev) => new MySegmentConfig
    {
        segment_id = segment_id,
        adStartLevel = UserSegmentInheritance.Pick(adStartLevel, prev.adStartLevel),
        startCoins = UserSegmentInheritance.Pick(startCoins, prev.startCoins)
    };
}
```

**2 · DTO hàng keyword:**

```csharp
[Serializable]
public sealed class MyKeywordRule : IUserSegmentKeywordRule
{
    public int segment_id;
    public List<string> keywords;

    public int SegmentId => segment_id;
    public IReadOnlyList<string> Keywords => keywords;
}
```

**3 · Host và Initializer** — id lấy từ dữ liệu của dự án; pool và bảng keyword đọc từ remote config đã ép kiểu:

```csharp
public sealed class MySegmentationHost : AUserSegmentationHost<MySegmentConfig>
{
    protected override int DefaultSegmentId => MySegmentIds.Default;
    protected override int PurchasedSegmentId => MySegmentIds.Purchased;
    protected override IReadOnlyList<MySegmentConfig> Configs => MyRemoteConfig.SegmentPool;
    protected override IReadOnlyList<IUserSegmentKeywordRule> KeywordRules => MyRemoteConfig.SegmentKeywords;
}

internal sealed class MySegmentationHostInitializer : Initializer<MySegmentationHost, BasePersistenceDataCollection>
{
#if UNITY_EDITOR
    private sealed class Init
    {
        public BasePersistenceDataCollection SaveCollection = default;
    }
#endif
}
```

**4 · Wire trong Editor:**

| Bước | Thiếu thì hỏng ở đâu |
|---|---|
| Asset collection của dự án, mục **User Segment**: điền `Key` (ví dụ `user_segment_state`), `Default Value` để nguyên số 0 và chuỗi rỗng. Khoá là wire format: không đổi sau khi ship | Persistence `LogError` "Field 'userSegmentState' has an empty key" lúc `Initialize`; đổi khoá sau ship là mọi user về chưa quyết |
| Scene boot: GameObject có `MySegmentationHost` và `MySegmentationHostInitializer`; ô **Save Collection** kéo asset collection | `Init` không chạy: host `LogError` "saveCollection was not injected", `IsReady` false, `CurrConfig` null |
| Cùng scene: GameObject có `AdjustCampaignBootStep`; ô **Segmentation** kéo host. Hai ô **First Session Wait Seconds**, **Late Arrival Poll Seconds** giữ mặc định nếu chưa có số đo | ô trống: `LogError` "segmentation is empty", không campaign nào về, mọi user ở `default` |
| List **Steps** của `BootstrapService` có cả ba bước, ô **Order**: `SaveBootstep` < host < `AdjustCampaignBootStep` | host trước save: `LogError` "SaveBootstep must have a lower order"; bước Adjust trước host: `LogError` "its order must be lower than this step"; host vắng khỏi list: `CurrConfig` null cả phiên |
| Remote config của dự án fetch xong (có thời hạn) **trước** khi `BootstrapService` chạy | phiên này chạy bằng giá trị asset; bản về muộn chỉ có hiệu lực phiên sau |
| Package `com.adjust.sdk` ≥ 5.0.0 có trong dự án, và wrapper Adjust của dự án init SDK | thiếu package: assembly `com.horcrux.adjust` không biên dịch, component thành "missing script"; SDK chưa init: không attribution nào về, poll phiên 1 hết hạn, user ở `default` |
| Code xác nhận mua của dự án gọi `host.ReportPurchase()`, chỉ cho giao dịch thật: không restore, không refund, không gói chỉ bỏ quảng cáo | user mua không sang tập `purchased` |

**Reset để thử lại từ chưa quyết:** ngoài Play, **Horcrux → User Segmentation → Simulator** → **Xoá save phân tập**; hoặc menu
**Horcrux → Player Prefs Editor**, xoá key `persistence_` + `Key` của entry.

## Consumer

| Đọc | Nghĩa |
|---|---|
| `CurrConfig` | hàng đã giải của tập hiện tại; `null` khi host chưa chạy hoặc setup thiếu. Consumer không tự đỡ `null`: setup thiếu phải nổ ở lần Play đầu |
| `IsReady` · `CurrSegmentId` · `PrevSegmentId` · `LastChangeReason` · `SessionCount` · `IsFirstSession` · `LastCampaign` | đọc thẳng từ state; trước khi host chạy trả 0, false, `None`, `null` |
| `EventBus<UserSegmentChanged>` | phát đồng bộ sau khi state đã ghi vào entry, chỉ khi tập đổi. Field: `PrevSegmentId` · `CurrentSegmentId` · `Reason` · `MatchedKeyword` (null với `Initial`, `Purchase`) · `IsAmbiguous` |

Tập đổi được ở mọi phiên, giữa level cũng vậy. Consumer đọc `CurrConfig` lúc cần, không giữ bản sao; cần phản ứng thì nghe
`UserSegmentChanged`. Việc chỉ chạy một lần theo tập lúc đó (quà khởi đầu) là quyết định của consumer.

Log mỗi lần đổi tập, một dòng: `[UserSegmentation]: CampaignResolved: segment 1 -> 2 by keyword 'X', another segment's
keyword also matched`.

## Debug

Panel cheat do dự án dựng; host phơi cho nó các cửa đọc ở mục Consumer và hai lệnh chạm state `private` (cài explicit, cast
host sang `IUserSegmentationDebug`; `IsReady` false thì `LogError` rồi thoát):

| Lệnh | Làm | Caller phải |
|---|---|---|
| `ForceCampaign(camp)` | `TryMatch` rồi `Decider.OnForcedCampaign`: `LastCampaign = camp`, chuyển tới tập khớp hoặc `default` với lý do `CampaignResolved`, bỏ qua `AcceptsCampaign`, kể cả khi đang ở `purchased` · `Commit` như lệnh thật: tập đổi thì `CurrConfig` dựng lại, log, publish `CampaignResolved`; không đổi thì log một dòng | không gì; grant chỉ chạy lúc boot thì không chạy lại |
| `ResetState()` | state mới (chưa quyết), ghi vào entry, `FlushNow`, log một dòng. Không publish `UserSegmentChanged` | thoát app ngay: `CurrConfig` giữ hàng cũ, việc chỉ chạy lúc boot (grant của dự án) không chạy lại |

Lệnh còn lại của cheat là cửa ghi thật `ReportPurchase()`. Camp của cheat không đi qua `ReportCampaign`: lệnh thật bỏ qua camp
khi hành vi đã quyết tập, mà QA cần thử camp cả lúc đang ở `purchased`. Trong Editor Adjust không trả lời, nên cheat là đường
duy nhất thử tập theo camp.

## Quyết định trái trực giác

| Chỗ | Vì |
|---|---|
| Chỉ chờ campaign trong phiên 1 | campaign là input ban đầu; user không có mạng ở phiên 1 ở `default` tới khi có luật hành vi |
| Lưu `LastChangeReason`, không lưu cờ khoá | luật chỉ có một câu, campaign là input ban đầu; lý do của tập hiện tại trả lời câu đó cho mọi luật, kể cả luật chưa viết |
| `AcceptsCampaign` liệt kê lý do *là input*, không liệt kê lý do hành vi | lý do thêm sau mặc định là hành vi và thắng campaign: quên sửa hàm này không đẩy user đã có hành vi về tập theo camp |
| Campaign về sau hành vi chỉ ghi `LastCampaign` | tracking cần tên camp thật; đổi tập lúc đó đẩy người vừa mua khỏi `purchased` |
| `-1` nghĩa là kế thừa, không có cột `inherit` | kế thừa là thuộc tính của từng ô; bảng một cột một số |
| `PrevSegmentId` ghi ở mọi lần đổi, không riêng lúc mua | một nghĩa chung cho mọi luật; kế thừa của `purchased` vẫn đúng vì lần đổi cuối trước khi ở `purchased` là lần vào nó |
| Hàng keyword trỏ vào `purchased` bị coi là không khớp | chỉ mua thật mới vào `purchased`: một hàng gõ nhầm trên console sẽ cho user chưa trả tiền cấu hình của người đã mua |
| Thiếu cả hàng `default` thì ném, không trả hàng rỗng | hàng rỗng là kinh tế bằng 0 chạy im lặng; pool rỗng là lỗi authoring, phải nổ |
| Field public của `UserSegmentState` viết PascalCase | tên field là khoá JSON của save, đã chốt từ save đầu tiên |
| Poll hết hạn chỉ log, không ghi gì vào state | `LastCampaign` null đã nói "chưa có attribution"; ghi thêm là một nhãn không luật nào đọc |
| `ResetState` thay cả `_state` lẫn giá trị entry | Commit muộn (poll Adjust nền) ghi `_entry.Value = _state`; giữ `_state` cũ thì bản cũ đè bản reset |
| `ResetState` là explicit interface | gọi nhầm là mất save im lặng |
| Base không generic giữ toàn bộ cơ chế, lớp generic chỉ tra config | bước Adjust và tool Editor cần một kiểu kéo thả được; bảng keyword không mang kiểu dự án |
| Boot luôn gọi `OnStateApplied`, kể cả khi không có thay đổi | `CurrConfig` bắt đầu `null` mỗi lần mở app, phải dựng ở mọi phiên |
| Một hàng keyword mang **mảng** keyword | bảng trên console đọc một dòng một tập. Giá: CSV importer của RemoteConfigSystem chỉ nhận ô phẳng, nên bảng keyword nhập bằng Inspector hoặc nút **Import Json** trên asset remote config; pool vẫn đi CSV |
| Bước Adjust poll `GetAttribution` thay vì nhận callback | SDK không phát event khi wrapper khác của dự án giữ cấu hình init |
| Poll nền dùng `destroyCancellationToken`, không dùng token của boot | pha boot kết thúc ngay khi bước trả về |
| Bước Adjust không có nhánh Editor | trong Editor `GetAttribution` không gọi callback: phiên đầu chờ hết `firstSessionWaitSeconds`, poll nền vô ích rồi log; chậm, không sai. Camp giả cho Editor thuộc cheat |

## Cố ý không làm

| Không làm | Vì |
|---|---|
| Interface cho nguồn campaign | chưa có nguồn thứ hai; bước Adjust gọi host qua ô kéo thả |
| Host tự đọc remote config | host sẽ phải biết tên key của dự án |
| Ưu tiên giữa nhiều keyword | tất định theo thứ tự bảng; đổi ưu tiên là đảo hàng trên console |
| Cửa ghi cho luật hành vi | chưa có luật nào; chữ ký chờ luật đầu tiên (mục Mở rộng sau) |
| Tính lại config khi remote về giữa phiên | dự án fetch có thời hạn trước boot; bản về muộn để phiên sau |
| Enum tập | enum đóng pool: mỗi tập mới cần một lần build. Chỉ hai vai có nghĩa trong code, nên id là `int` trong dữ liệu |
| Cột tên tập trong contract | chỉ tracking của dự án cần tên; lõi log bằng id |

## Bẫy

- **Keyword là substring.** Keyword ngắn khớp chữ nằm giữa từ khác: `Ad` khớp `Broad_US`, `Lead_01`. Chọn keyword không xuất
  hiện trong tên camp khác, kiểm trên danh sách camp thật trước khi lên console. Kiểm bằng chạy `TryMatch`, không suy bằng
  mắt: `Ad` **không** khớp `Android` (chuỗi đó không chứa `ad`).
- **Pool rỗng hoặc thiếu hàng `default`.** Exception ném trong `InitializeAsync` sau khi state đã nạp: `IsReady` là true nhưng
  `CurrConfig` vẫn `null`. `BootstrapService` ghi lỗi bước rồi vào game; consumer đầu tiên đọc config ném NRE.
- **Lỗi "order must be lower" của bước Adjust** cũng hiện khi host thiếu Initializer: đọc dòng lỗi của host in trước nó.
- **Thêm một cột là bốn chỗ cùng lần:** field và dòng `Pick` · mọi hàng trong asset · mọi hàng trên console · danh sách cột trong
  test. DTO đòi đủ khoá nên một hàng console thiếu cột mới làm **cả pool** bị từ chối: game chạy bằng giá trị asset, chỉ một dòng
  `LogError` lúc boot. Lên console trước khi phát hành bản có cột đó.
- **Collection giả trong test** phải điền `Key` cho `userSegmentState` như mọi entry base tự khai (`Persistence.md`, mục
  Bẫy và quyết định).
- **"Phiên" ở đây không phải session của Adjust hay Firebase.** Hai SDK đó mặc định tính phiên mới khi app ở nền quá 30 phút;
  hệ này chỉ đếm lần mở app (cold boot). Số session trên dashboard thường lớn hơn `SessionCount`; "phiên 1" của hệ này là lần
  mở app đầu tiên sau khi cài.

## Kiểm thử

`Tests/EditMode/`, chạy bằng `compile-check`:

| File | Phủ |
|---|---|
| `UserSegmentKeywordMatcherTests` | guard `null`, rỗng · hoa thường · keyword thứ ba của hàng · thứ tự mảng thắng thứ tự camp · hai hàng cùng tập không ambiguous · hai tập khớp: thứ tự bảng thắng, ambiguous · keyword và hàng rỗng bị bỏ · keyword ngắn trên camp chứa `Android` |
| `UserSegmentInheritanceTests` | `Pick` với `-1`, giá trị thường, `-1` ở cả hai, `0` |
| `UserSegmentConfigTableTests` | `Find`: có, trùng id, vắng, `null` · `Validate`: từng dòng kiểm ở mục Luật, mỗi lỗi đúng một dòng, không ném với input `null` |
| `UserSegmentDeciderTests` | mọi dòng bảng Luật · `AcceptsCampaign` từng lý do, kể cả lý do chưa có tên · `PrevSegmentId` sau camp, mua, mua lại, mua lúc chưa quyết · camp sau camp, sau mua, save chưa có lý do · `OnForcedCampaign`: rời `purchased`, không khớp về `default`, cùng tập không đổi · wire format qua `PersistenceDataEntry`: `LastChangeReason` ghi dạng số, save cũ có `LockReason` đọc ra `None` |

`InheritFrom` là code dự án: test ở dự án, bằng reflection trên mọi field `int` public để bắt cột mới thiếu dòng `Pick`.
Host và bước Adjust không có test tự động (MonoBehaviour gắn chuỗi boot và SDK); kiểm bằng biên dịch.

## Chữ ký

Namespace `Horcrux.Runtime.Abstractions.UserSegmentation`:

| Type | Thành viên |
|---|---|
| `AUserSegmentationHost` | `abstract partial`, `ABootStep`, `IInitializable<BasePersistenceDataCollection>`, `IUserSegmentationDebug` (explicit) · `bool IsReady` · `int CurrSegmentId` · `int PrevSegmentId` · `UserSegmentChangeReason LastChangeReason` · `int SessionCount` · `bool IsFirstSession` · `string LastCampaign` · `void ReportCampaign(string)` · `void ReportPurchase()` · abstract `DefaultSegmentId` · `PurchasedSegmentId` · `IReadOnlyList<IUserSegmentKeywordRule> KeywordRules` |
| `AUserSegmentationHost<TConfig>` | `where TConfig : class, IUserSegmentConfig<TConfig>` · `TConfig CurrConfig` · abstract `IReadOnlyList<TConfig> Configs` |
| `IUserSegmentConfig` · `IUserSegmentConfig<TSelf>` | `int Id` · `TSelf InheritFrom(TSelf prev)` |
| `IUserSegmentKeywordRule` | `int SegmentId` · `IReadOnlyList<string> Keywords` |
| `UserSegmentState` | `[Serializable]` · `int CurrSegmentId` · `int PrevSegmentId` · `UserSegmentChangeReason LastChangeReason` · `int SessionCount` · `string LastCampaign` |
| `IUserSegmentationDebug` | `void ResetState()` · `void ForceCampaign(string campaign)` |
| `UserSegmentDecider` | `Change OnBoot(state, defaultId)` · `bool AcceptsCampaign(state)` · `Change OnCampaign(state, campaign, matchedId, keyword, isAmbiguous, defaultId, purchasedId)` · `Change OnPurchase(state, purchasedId)` · `Change OnForcedCampaign(state, campaign, matchedId, keyword, isAmbiguous, defaultId)` (chỉ cho cheat) · `readonly struct Change { HasChange, PrevSegmentId, CurrSegmentId, Reason, MatchedKeyword, IsAmbiguous; static None }` |
| `UserSegmentKeywordMatcher` | `bool TryMatch(string campaign, IReadOnlyList<IUserSegmentKeywordRule>, out int segmentId, out string keyword, out bool isAmbiguous)` |
| `UserSegmentInheritance` | `const int InheritValue = -1` · `int Pick(int target, int prev)` |
| `UserSegmentConfigTable` | `TConfig Find<TConfig>(IReadOnlyList<TConfig>, int id)` · `bool Validate<TConfig>(configs, rules, int defaultSegmentId, List<string> errors)` |
| `UserSegmentChanged` · `UserSegmentChangeReason` | `IEvent` · `None = 0`, `Initial = 1`, `CampaignResolved = 2`, `Purchase = 3`; số là wire format của save (`LastChangeReason`), chỉ thêm ở cuối |

Namespace `Horcrux.Runtime.Abstractions.Persistence`: `BasePersistenceDataCollection.userSegmentState`
(`protected internal`) + `UserSegmentState`. Namespace `Horcrux.Vendors.Adjust`: `AdjustCampaignBootStep`.

## Cấu trúc file

```
Runtime/Abstractions/Composites/UserSegmentation/
├── AUserSegmentationHost.cs · AUserSegmentationHost.Generic.cs   cơ chế · tra và cache config
├── AUserSegmentationHost.Debug.cs                                 IUserSegmentationDebug: ép camp, reset cho cheat
├── IUserSegmentConfig.cs · IUserSegmentKeywordRule.cs             contract hai DTO
├── UserSegmentDecider.cs · UserSegmentKeywordMatcher.cs           luật · khớp keyword
├── UserSegmentInheritance.cs · UserSegmentConfigTable.cs          kế thừa · tra và guard dữ liệu
├── UserSegmentState.cs · UserSegmentChanged.cs                    save model · event
└── BasePersistenceDataCollection.UserSegmentation.cs              entry save
Vendors/Adjust/com.horcrux.adjust.asmdef · AdjustCampaignBootStep.cs
Editor/UserSegmentation/UserSegmentationSimulatorWindow.cs · UserSegmentationSimulatorCore.cs   tool Editor
Tests/EditMode/UserSegment*Tests.cs · UserSegmentationSimulatorTests.cs
```

## Mở rộng sau

- **Luật phân tập theo hành vi** — hướng đã chốt, chưa có luật. Dự án tự tính tập đích từ hành vi rồi gọi **một** cửa ghi chung
  trên host, ví dụ `ReportBehavior(int segmentId)` → `Decider.OnBehavior` → `MoveTo` với lý do mới thêm ở cuối
  `UserSegmentChangeReason`. Horcrux giữ cơ chế (save, dựng config, event), dự án giữ luật. Đã chốt: đổi được cả khi đang ở
  `purchased`; có hiệu lực ngay trong phiên, như mua; campaign về sau đó chỉ ghi `LastCampaign` (đã đúng nhờ
  `AcceptsCampaign`); sự kiện switch của tracking gửi thêm trường nguồn. Cần thêm: dòng bảng Luật, test, nút cheat nếu QA cần.

- Tính lại config khi remote về giữa phiên: một `public void RefreshConfigs()` trên lớp generic, dự án nối vào sự kiện fetch
  xong của remote config.
- Ô CSV dạng `a|b|c` cho field `List<string>` trong `RemoteConfigCsvImporter`, khi bảng keyword cần đi đường sheet.
