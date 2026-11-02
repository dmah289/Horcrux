# Plan — `UserSegmentation` lên Horcrux

Ngày 2026-11-02. Hệ phân tập user theo campaign Adjust và theo mua IAP, cho category-jam và mọi dự án sau.
Thiết kế đã duyệt: `docs/superpowers/specs/2026-11-02-user-segmentation-design.md` của dự án; Plan này tự chứa,
developer gõ xong không phải mở file đó.

**Phân công:** Task 1–4 (code runtime Horcrux) developer gõ · Task 5 (test EditMode) và Task 6 (tài liệu hệ) agent làm
sau khi code có · tool Editor và phần nối category-jam ở plan riêng `docs/superpowers/plans/2026-11-02-user-segmentation-linking.md`,
agent làm.

Horcrux là submodule: mọi task commit **trong `Assets/Horcrux`**, mỗi task một mốc compile sạch và một commit riêng.
Mọi thứ trong Plan chỉ **thêm**: không file Horcrux nào đang có bị sửa, trừ một partial mới của `BasePersistenceDataCollection`.

---

## Ngữ cảnh đã chốt

| Mục | Chốt |
|---|---|
| Người dùng | code Ads, IAP, kinh tế của dự án đọc config theo tập qua một hợp đồng; developer vận hành hai bảng trên console; dự án sau bê nguyên lõi, kể cả dự án không có Zego |
| Mục tiêu | mỗi user ở đúng một tập; tập quyết bởi campaign Adjust lúc cài, chuyển sang tập "đã mua" khi mua IAP; config của tập là một object đã ép kiểu, tập "đã mua" kế thừa ô `-1` từ tập ngay trước |
| Ranh giới | lõi không gọi tên type dự án, không đọc remote config, không biết Zego; dự án khai enum, DTO và đưa model đã ép kiểu vào host (tiền lệ `LiveOpsHost`: "module nhận model đã ép kiểu, host không biết RC") |
| Chỗ đặt | `Runtime/Abstractions/Composites/UserSegmentation/` (composite: dựng trên Persistence, Bootstrap, EventBus) · entry save trong partial `BasePersistenceDataCollection.UserSegmentation.cs` đặt cùng thư mục hệ (khuôn `TimeService`, khác chỗ đặt vì entry mang type của hệ) · bước Adjust trong `Vendors/Adjust/` với asmdef tự tắt theo package (khuôn `Vendors/Firebase`) |
| Phụ thuộc vendor | developer cho phép Horcrux tham chiếu thẳng `AdjustSdk.Scripts`; không bridge. Dự án thiếu `com.adjust.sdk` thì assembly vendor không biên dịch, hệ vẫn chạy: mọi user ở tập mặc định |
| Tên | `AUserSegmentationHost`, `UserSegmentDecider`, `UserSegmentKeywordMatcher`, `UserSegmentConfigInheritance`, `UserSegmentState`, `UserSegmentChanged`, `AdjustCampaignBootStep`. Developer đổi tuỳ ý trước khi gõ; agent sync plan theo code thật |

**Cố ý KHÔNG làm**

| Không làm | Vì |
|---|---|
| Interface cho nguồn campaign | implementation thứ hai không có thật; bước Adjust gọi vào host qua ô kéo thả kiểu `AUserSegmentationHost` |
| Host tự đọc `IRemoteConfigCollection` | host sẽ phải biết tên key của dự án; tiền lệ LiveOps đã chọn dự án đưa model vào |
| Ưu tiên giữa nhiều keyword | spec dự án để sau; bản này tất định: **hàng đầu trong bảng khớp thì thắng**, đổi ưu tiên là đảo hàng trên console |
| Đường ra khỏi tập "đã mua" | spec dự án: không có |
| Tính lại config khi remote về muộn | dự án fetch remote có thời hạn **trước** bootstrap; bản về muộn để phiên sau (luật RemoteConfigSystem) |
| Chuyển `TSegment` ↔ `int` không boxing | nhịp là boot và mỗi lần báo camp hay mua, không phải mỗi frame; `Convert.ToInt32` đủ |

**Quyết định trái trực giác**

| Chỗ | Vì |
|---|---|
| Attribution về là khoá camp ngay, kể cả `Campaign` rỗng | Adjust đã trả lời xong; organic thật cũng về dạng này. Cửa sổ phiên chỉ dành cho user Adjust chưa bao giờ trả lời |
| `-1` nghĩa là kế thừa, không có cột `inherit` | kế thừa là thuộc tính của từng ô; CSV một cột một số |
| `prevSegmentId` chỉ ghi lúc mua, không ghi lúc phân lại theo camp | nó chỉ phục vụ kế thừa của tập "đã mua" |
| `resegmentMaxSessions < 1` kẹp về 1 | giá trị 0 sẽ khoá ngay lúc boot đầu, **trước** khi Adjust kịp về, mọi user thành mặc định vĩnh viễn |
| Base không generic giữ toàn bộ cơ chế, lớp generic chỉ đổi kiểu và tra config | bước Adjust và tool Editor cần một kiểu kéo thả được; test Decider không cần generic |
| Base gọi `OnStateApplied()` cả khi không có thay đổi lúc boot | cache `CurrSegment`/`CurrConfig` phải đúng ở mọi phiên, không chỉ phiên có đổi tập |
| `Application.isEditor` thay `#if UNITY_EDITOR` trong bước Adjust | `Adjust.GetAttribution` trong Editor return không gọi callback; kiểm runtime cùng cách SDK kiểm, không cắt đôi hàm async |

**Mở rộng sau**

- Hook tính lại config khi remote về giữa phiên: một `public void RefreshConfigs()` trên lớp generic, dự án nối vào `IRemoteConfigProvider.OnFetched` khi có nhu cầu.
- Tập mới ở dự án: thêm giá trị enum **ở cuối**, một hàng mỗi bảng; lõi không đổi.
- Cheat đổi tập trên build khi dự án có SRDebugger: một category gọi `ReportPurchase()` và một lệnh đặt camp giả.

---

## Đã khảo sát

| Nguồn | Lấy gì | Không lấy, vì sao |
|---|---|---|
| `Abstractions/Foundations/Bootstrap/ABootStep.cs` | `InitializeAsync(ct)`, `Order` từ Inspector, fail-open của service | — |
| `Abstractions/Foundations/Persistence/BasePersistenceDataCollection.TimeService.cs` | khuôn partial khai entry Horcrux trong collection của dự án | — |
| `Implementations/Composites/SaveBootstep.cs` | thứ tự: collection `Initialize()` ở bước trước, host chỉ kiểm `IsInitialized` | — |
| `Utilities/EventBus/EventBus.cs` | `EventBus<T>.Publish(in T)` với `T : struct, IEvent`, đồng bộ, cô lập lỗi từng listener | — |
| `Vendors/Firebase/com.horcrux.firebase.asmdef` | `versionDefines` + `defineConstraints` để assembly tắt theo package | — |
| `Abstractions/Composites/LiveOps/LiveOpsHost.md` | "host nhận model đã ép kiểu, không biết RC"; host trừu tượng ở `Abstractions/` | 1 Hz loop, lịch tuần — không liên quan |
| `Library/PackageCache/com.adjust.sdk@…/Scripts/Adjust.cs` | `Adjust.GetAttribution(Action<AdjustAttribution>)`, assembly `AdjustSdk.Scripts`, namespace `AdjustSdk`; trong Editor return không callback | `GetAttributionWithTimeout`: timeout của SDK không huỷ được theo `ct` |
| `com.zego.adjust` `ZegoAdjust` | ý tưởng ô camp giả trong Editor | toàn bộ còn lại: trói vào Zego |
| `Assets/_Core/Diversity/` của dự án | — | port chết, enum tráo số; dự án xoá ở plan nối |

---

## Luật nghiệp vụ — nguồn duy nhất cho Task 2 và Task 5

Id tập là `int`, `0` nghĩa là chưa quyết. Dự án đặt tên và số (category-jam: BLD 1, IAP 2, Purchased 3, Ads 4).

| Sự kiện | Điều kiện | Kết quả |
|---|---|---|
| `OnBoot` | `currSegmentId == 0` | `curr = default`, thay đổi `Initial` |
| `OnBoot` | `!isCampaignLocked && sessionCount >= max(1, resegmentMaxSessions)` | `isCampaignLocked = true`, giữ tập |
| `OnBoot` | luôn, sau hai dòng trên | `sessionCount++` |
| `OnCampaign(camp)` | `isCampaignLocked` | `lastCampaign = camp`, không đổi |
| `OnCampaign(camp)` | `!isCampaignLocked && curr == purchased` | `lastCampaign = camp`, `isCampaignLocked = true`, không đổi |
| `OnCampaign(camp)` | `!isCampaignLocked` | `lastCampaign = camp`, `isCampaignLocked = true`; đích = tập khớp, không khớp hay camp rỗng → `default`; đích khác `curr` thì `curr = đích`, thay đổi `CampaignResolved` kèm keyword và cờ nhiều keyword |
| `OnPurchase` | `!isRuleEnabled` | không đổi |
| `OnPurchase` | `curr == purchased` | không đổi |
| `OnPurchase` | còn lại | `prev = curr` (curr là 0 thì `prev = default`), `curr = purchased`, `isCampaignLocked = true`, thay đổi `Purchase` |
| đọc config | `curr != purchased` | hàng của `curr` |
| đọc config | `curr == purchased` | `Resolve(hàng purchased, hàng prev)`; `prev == 0` thì hàng `default` |

Khớp keyword: `campaign.IndexOf(keyword, OrdinalIgnoreCase) >= 0`; hàng đầu khớp thắng; cờ nhiều keyword bật khi còn
hàng **khác tập** cũng khớp; keyword rỗng bỏ qua.

Guard dữ liệu, chạy một lần lúc boot, chỉ `LogError`, không sửa dữ liệu: mỗi giá trị enum có đúng một hàng config ·
enum không có giá trị `0` · hàng không phải purchased không chứa `-1` · keyword rỗng. Hàng thiếu khi tra thì dùng
hàng `default` kèm `LogError`; config trả ra **không bao giờ `null`**.

---

## Task 1 — Contract, model, hai hàm thuần

**Files (tạo):** `Runtime/Abstractions/Composites/UserSegmentation/`
`IUserSegmentKeywordRule.cs` · `IUserSegmentConfig.cs` · `UserSegmentState.cs` · `UserSegmentChanged.cs` ·
`UserSegmentKeywordMatcher.cs` · `UserSegmentConfigInheritance.cs`

**Produces:** mọi kiểu Task 2–4 dùng. Namespace `Horcrux.Runtime.Abstractions.Composites.UserSegmentation`.

`IUserSegmentKeywordRule.cs`

```csharp
using System;

namespace Horcrux.Runtime.Abstractions.Composites.UserSegmentation
{
    /// <summary>One row of the keyword table. Row order is priority: the first matching row wins.</summary>
    public interface IUserSegmentKeywordRule<TSegment> where TSegment : unmanaged, Enum
    {
        TSegment SegmentId { get; }
        string Keyword { get; }
    }
}
```

`IUserSegmentConfig.cs`

```csharp
using System;

namespace Horcrux.Runtime.Abstractions.Composites.UserSegmentation
{
    /// <summary>One row of the config table: the project DTO carries the fields, this only names the row.</summary>
    public interface IUserSegmentConfig<TSegment> where TSegment : unmanaged, Enum
    {
        TSegment Id { get; }
    }
}
```

`UserSegmentState.cs`

```csharp
using System;

namespace Horcrux.Runtime.Abstractions.Composites.UserSegmentation
{
    /// <summary>Save model. Field names are the wire format; 0 in <see cref="currSegmentId"/> means undecided.</summary>
    [Serializable]
    public sealed class UserSegmentState
    {
        public int currSegmentId;
        /// <summary>Segment the user was in right before the purchased segment; only written on purchase.</summary>
        public int prevSegmentId;
        /// <summary>True once attribution answered, a purchase happened, or the resegment window closed.</summary>
        public bool isCampaignLocked;
        public int sessionCount;
        public string lastCampaign;
    }
}
```

`UserSegmentChanged.cs`

```csharp
using Horcrux.Runtime.Utilities.EventBus;

namespace Horcrux.Runtime.Abstractions.Composites.UserSegmentation
{
    public enum UserSegmentChangeReason
    {
        Initial = 1,
        CampaignResolved = 2,
        Purchase = 3
    }

    /// <summary>Published synchronously after the new state is written to the save entry.</summary>
    public readonly struct UserSegmentChanged : IEvent
    {
        public readonly int PrevSegmentId;
        public readonly int CurrSegmentId;
        public readonly UserSegmentChangeReason Reason;
        /// <summary>Keyword that decided the segment; null for Initial and Purchase.</summary>
        public readonly string MatchedKeyword;
        /// <summary>True when a row of another segment also matched the campaign.</summary>
        public readonly bool IsAmbiguous;

        public UserSegmentChanged(int prevSegmentId, int currSegmentId, UserSegmentChangeReason reason,
            string matchedKeyword, bool isAmbiguous)
        {
            PrevSegmentId = prevSegmentId;
            CurrSegmentId = currSegmentId;
            Reason = reason;
            MatchedKeyword = matchedKeyword;
            IsAmbiguous = isAmbiguous;
        }
    }
}
```

`UserSegmentKeywordMatcher.cs`

```csharp
using System;
using System.Collections.Generic;

namespace Horcrux.Runtime.Abstractions.Composites.UserSegmentation
{
    public static class UserSegmentKeywordMatcher
    {
        /// <summary>First row whose keyword the campaign contains (ordinal, ignore case) wins. Empty keywords are skipped.</summary>
        public static bool TryMatch<TSegment>(string campaign, IReadOnlyList<IUserSegmentKeywordRule<TSegment>> rules,
            out TSegment segment, out string keyword, out bool isAmbiguous)
            where TSegment : unmanaged, Enum
        {
            segment = default;
            keyword = null;
            isAmbiguous = false;

            if (string.IsNullOrEmpty(campaign) || rules == null)
                return false;

            bool found = false;

            for (int i = 0; i < rules.Count; i++)
            {
                IUserSegmentKeywordRule<TSegment> rule = rules[i];

                if (rule == null || string.IsNullOrEmpty(rule.Keyword))
                    continue;

                if (campaign.IndexOf(rule.Keyword, StringComparison.OrdinalIgnoreCase) < 0)
                    continue;

                if (!found)
                {
                    found = true;
                    segment = rule.SegmentId;
                    keyword = rule.Keyword;
                    continue;
                }

                if (!EqualityComparer<TSegment>.Default.Equals(rule.SegmentId, segment))
                {
                    isAmbiguous = true;
                    break;
                }
            }

            return found;
        }
    }
}
```

`UserSegmentConfigInheritance.cs`

```csharp
using System.Reflection;

namespace Horcrux.Runtime.Abstractions.Composites.UserSegmentation
{
    /// <summary>A public int field equal to <see cref="InheritValue"/> takes the value of the same field on the previous segment's row.</summary>
    public static class UserSegmentConfigInheritance
    {
        public const int InheritValue = -1;

        /// <summary>Returns a new row: every public field copied from <paramref name="target"/>, int fields equal to -1 taken from <paramref name="prev"/>.</summary>
        public static TConfig Resolve<TConfig>(TConfig target, TConfig prev) where TConfig : class, new()
        {
            FieldInfo[] all = FieldCache<TConfig>.All;
            FieldInfo[] ints = FieldCache<TConfig>.Ints;
            TConfig result = new TConfig();

            for (int i = 0; i < all.Length; i++)
                all[i].SetValue(result, all[i].GetValue(target));

            if (prev == null)
                return result;

            for (int i = 0; i < ints.Length; i++)
            {
                if ((int)ints[i].GetValue(result) == InheritValue)
                    ints[i].SetValue(result, ints[i].GetValue(prev));
            }

            return result;
        }

        /// <summary>True when any public int field equals -1. Rows other than the purchased one must return false.</summary>
        public static bool HasInheritValue<TConfig>(TConfig row) where TConfig : class
        {
            FieldInfo[] ints = FieldCache<TConfig>.Ints;

            for (int i = 0; i < ints.Length; i++)
            {
                if ((int)ints[i].GetValue(row) == InheritValue)
                    return true;
            }

            return false;
        }

        /// <summary>Public int fields of the row type, for Editor tools drawing the grid.</summary>
        public static FieldInfo[] IntFieldsOf<TConfig>() => FieldCache<TConfig>.Ints;

        private static class FieldCache<TConfig>
        {
            public static readonly FieldInfo[] All = typeof(TConfig).GetFields(BindingFlags.Public | BindingFlags.Instance);
            public static readonly FieldInfo[] Ints = FilterInts(All);

            private static FieldInfo[] FilterInts(FieldInfo[] fields)
            {
                int count = 0;
                for (int i = 0; i < fields.Length; i++)
                    if (fields[i].FieldType == typeof(int)) count++;

                FieldInfo[] result = new FieldInfo[count];
                int w = 0;
                for (int i = 0; i < fields.Length; i++)
                    if (fields[i].FieldType == typeof(int)) result[w++] = fields[i];

                return result;
            }
        }
    }
}
```

| Quyết định | Vì sao |
|---|---|
| Reflection trên field `int` public, cache tĩnh theo `TConfig` | DTO của dự án là field public để RemoteConfig và CSV đọc được; cache dựng một lần mỗi kiểu, `Resolve` chỉ chạy khi đổi tập |
| `Resolve` trả bản sao, không sửa `target` | `target` là object trong `RemoteConfig<List<T>>` của dự án, sửa tại chỗ là ghi vào dữ liệu của hệ khác |
| `IsAmbiguous` chỉ bật khi hàng khác **tập** khớp | hai keyword của cùng một tập khớp là bình thường (`Blended` và `BLD`) |

**Nghiệm thu:** compile sạch (`compile-check`). Commit: `feat(segmentation): contract, state, matcher, inheritance`.

---

## Task 2 — `UserSegmentDecider`

**Files (tạo):** `Runtime/Abstractions/Composites/UserSegmentation/UserSegmentDecider.cs`

**Consumes:** `UserSegmentState`, `UserSegmentChangeReason` (Task 1).
**Produces:** `UserSegmentDecider.Change`, `OnBoot`, `OnCampaign`, `OnPurchase` — Task 3 gọi, Task 5 test.

```csharp
using System;

namespace Horcrux.Runtime.Abstractions.Composites.UserSegmentation
{
    /// <summary>Every business rule, as pure functions mutating the given state. Segment ids are ints; 0 means undecided.</summary>
    public static class UserSegmentDecider
    {
        public readonly struct Change
        {
            public readonly bool HasChange;
            public readonly int PrevSegmentId;
            public readonly int CurrSegmentId;
            public readonly UserSegmentChangeReason Reason;
            public readonly string MatchedKeyword;
            public readonly bool IsAmbiguous;

            public static readonly Change None = default;

            public Change(int prevSegmentId, int currSegmentId, UserSegmentChangeReason reason, string matchedKeyword, bool isAmbiguous)
            {
                HasChange = true;
                PrevSegmentId = prevSegmentId;
                CurrSegmentId = currSegmentId;
                Reason = reason;
                MatchedKeyword = matchedKeyword;
                IsAmbiguous = isAmbiguous;
            }
        }

        /// <summary>Cold boot: decide the default once, close the resegment window after N sessions, count the session.</summary>
        public static Change OnBoot(UserSegmentState state, int defaultSegmentId, int resegmentMaxSessions)
        {
            Change change = Change.None;

            if (state.currSegmentId == 0)
            {
                state.currSegmentId = defaultSegmentId;
                change = new Change(0, defaultSegmentId, UserSegmentChangeReason.Initial, null, false);
            }

            int maxSessions = Math.Max(1, resegmentMaxSessions);

            if (!state.isCampaignLocked && state.sessionCount >= maxSessions)
                state.isCampaignLocked = true;

            state.sessionCount++;
            return change;
        }

        /// <summary>Attribution answered. <paramref name="matchedSegmentId"/> is 0 when no row matched.</summary>
        public static Change OnCampaign(UserSegmentState state, string campaign, int matchedSegmentId, string matchedKeyword,
            bool isAmbiguous, int defaultSegmentId, int purchasedSegmentId)
        {
            state.lastCampaign = campaign;

            if (state.isCampaignLocked)
                return Change.None;

            state.isCampaignLocked = true;

            if (state.currSegmentId == purchasedSegmentId)
                return Change.None;

            int target = matchedSegmentId != 0 ? matchedSegmentId : defaultSegmentId;

            if (target == state.currSegmentId)
                return Change.None;

            int prev = state.currSegmentId;
            state.currSegmentId = target;
            return new Change(prev, target, UserSegmentChangeReason.CampaignResolved, matchedKeyword, isAmbiguous);
        }

        /// <summary>A real purchase (not restore, not refund, not a remove-ads-only pack: the project filters those).</summary>
        public static Change OnPurchase(UserSegmentState state, bool isRuleEnabled, int defaultSegmentId, int purchasedSegmentId)
        {
            if (!isRuleEnabled)
                return Change.None;

            if (state.currSegmentId == purchasedSegmentId)
                return Change.None;

            int prev = state.currSegmentId != 0 ? state.currSegmentId : defaultSegmentId;
            state.prevSegmentId = prev;
            state.currSegmentId = purchasedSegmentId;
            state.isCampaignLocked = true;
            return new Change(prev, purchasedSegmentId, UserSegmentChangeReason.Purchase, null, false);
        }
    }
}
```

| Quyết định | Vì sao |
|---|---|
| Decider nhận kết quả khớp đã tính, không nhận bảng keyword | giữ Decider không generic; khớp là việc của `UserSegmentKeywordMatcher`, host nối hai thứ |
| `lastCampaign` ghi cả khi đã khoá | tracking cần camp thật của user; Zego không cache nó ở đâu |
| Kiểm cửa sổ **trước** `sessionCount++` | phiên 1..N còn phân lại được, khoá ở đầu phiên N+1; với N = 3 khớp "hết 3 phiên" |

**Nghiệm thu:** compile sạch. Commit: `feat(segmentation): decider`.

---

## Task 3 — Entry save, host base và host generic

**Files (tạo):**
- `Runtime/Abstractions/Composites/UserSegmentation/BasePersistenceDataCollection.UserSegmentation.cs` — partial của class ở
  `Foundations/Persistence`, đặt trong thư mục hệ để tầng nền không tham chiếu type của Composites (khác `TimeService`, entry của nó là `long`)
- `Runtime/Abstractions/Composites/UserSegmentation/AUserSegmentationHost.cs`
- `Runtime/Abstractions/Composites/UserSegmentation/AUserSegmentationHost.Generic.cs`

**Consumes:** Task 1, Task 2; `ABootStep`, `BasePersistenceDataCollection`, `PersistenceDataEntry<T>`, `MarkedPersistence`,
`Splitter`, `EventBus<T>`.
**Produces:** `AUserSegmentationHost` (kiểu kéo thả cho bước Adjust và tool Editor) · `AUserSegmentationHost<TSegment, TConfig>`
(dự án kế thừa).

`BasePersistenceDataCollection.UserSegmentation.cs`

```csharp
using Horcrux.Runtime.Abstractions.Composites.UserSegmentation;
using Horcrux.Runtime.Utilities;
using UnityEngine;

namespace Horcrux.Runtime.Abstractions.Persistence
{
    public abstract partial class BasePersistenceDataCollection
    {
        [Splitter("User Segmentation")]
        [MarkedPersistence, SerializeField]
        protected internal PersistenceDataEntry<UserSegmentState> userSegmentState;

        public PersistenceDataEntry<UserSegmentState> UserSegmentState => userSegmentState;
    }
}
```

`AUserSegmentationHost.cs`

```csharp
using System.Threading;
using Cysharp.Threading.Tasks;
using Horcrux.Runtime.Abstractions.Bootstrap;
using Horcrux.Runtime.Abstractions.Persistence;
using Horcrux.Runtime.Utilities;
using Horcrux.Runtime.Utilities.EventBus;
using UnityEngine;

namespace Horcrux.Runtime.Abstractions.Composites.UserSegmentation
{
    /// <summary>
    /// Boot step owning the segment state. Two write gates: <see cref="ReportCampaign"/> and <see cref="ReportPurchase"/>.
    /// Must run after the step that initializes the save collection. Not generic so vendor steps and Editor tools can reference it.
    /// </summary>
    public abstract class AUserSegmentationHost : ABootStep
    {
        #region Properties

        private const string LogPrefix = "[UserSegmentation]: ";

        [Splitter("References")]
        [SerializeField] private BasePersistenceDataCollection saveCollection;

        private PersistenceDataEntry<UserSegmentState> _entry;
        private UserSegmentState _state;

        /// <summary>False until <see cref="InitializeAsync"/> ran with a valid collection.</summary>
        public bool IsReady => _state != null;
        public int CurrSegmentId => _state != null ? _state.currSegmentId : 0;
        public int PrevSegmentId => _state != null ? _state.prevSegmentId : 0;
        public bool IsCampaignLocked => _state != null && _state.isCampaignLocked;
        public bool IsFirstSession => _state != null && _state.sessionCount == 1;
        public string LastCampaign => _state?.lastCampaign;

        protected abstract int DefaultSegmentId { get; }
        protected abstract int PurchasedSegmentId { get; }
        protected abstract int ResegmentMaxSessions { get; }
        protected abstract bool IsPurchaseRuleEnabled { get; }

        #endregion

        #region Unity Callbacks

        public override UniTask InitializeAsync(CancellationToken ct)
        {
            Initialize();
            return UniTask.CompletedTask;
        }

        #endregion

        #region API

        /// <summary>Attribution answered. Pass empty for organic. Locks the campaign; ignored (but recorded) once locked.</summary>
        public void ReportCampaign(string campaign)
        {
            if (!IsReady)
            {
                Debug.LogError(LogPrefix + "ReportCampaign before InitializeAsync; check step order", this);
                return;
            }

            bool wasLocked = _state.isCampaignLocked;
            bool matched = TryMatchCampaign(campaign, out int segmentId, out string keyword, out bool isAmbiguous);
            UserSegmentDecider.Change change = UserSegmentDecider.OnCampaign(_state, campaign, matched ? segmentId : 0,
                keyword, isAmbiguous, DefaultSegmentId, PurchasedSegmentId);

            if (wasLocked)
                Debug.Log(LogPrefix + $"campaign '{campaign}' arrived after lock, segment stays {_state.currSegmentId}", this);

            Commit(change);
        }

        /// <summary>A real purchase. The project decides what counts (not restore, not refund, not remove-ads only).</summary>
        public void ReportPurchase()
        {
            if (!IsReady)
            {
                Debug.LogError(LogPrefix + "ReportPurchase before InitializeAsync; check step order", this);
                return;
            }

            if (!IsPurchaseRuleEnabled)
                Debug.Log(LogPrefix + "purchase rule disabled by config, segment stays", this);

            Commit(UserSegmentDecider.OnPurchase(_state, IsPurchaseRuleEnabled, DefaultSegmentId, PurchasedSegmentId));
        }

        #endregion

        #region Class Methods

        /// <summary>Project side: match the campaign against its keyword table. Return false when no row matched.</summary>
        protected abstract bool TryMatchCampaign(string campaign, out int segmentId, out string keyword, out bool isAmbiguous);

        /// <summary>Project side: LogError every invalid row once at boot. Never fix the data.</summary>
        protected abstract void ValidateConfigs();

        /// <summary>Called after boot and after every change: rebuild derived caches (current segment, resolved config).</summary>
        protected abstract void OnStateApplied();

        private void Initialize()
        {
            if (saveCollection == null)
            {
                Debug.LogError(LogPrefix + "saveCollection is empty; drag the project's persistence collection asset", this);
                return;
            }

            if (!saveCollection.IsInitialized)
            {
                Debug.LogError(LogPrefix + "save collection not initialized; SaveBootstep must have a lower order", this);
                return;
            }

            if (ResegmentMaxSessions < 1)
                Debug.LogWarning(LogPrefix + $"ResegmentMaxSessions = {ResegmentMaxSessions}, clamped to 1", this);

            _entry = saveCollection.UserSegmentState;
            _state = _entry.Value;

            ValidateConfigs();
            Commit(UserSegmentDecider.OnBoot(_state, DefaultSegmentId, ResegmentMaxSessions), forceApply: true);
        }

        private void Commit(in UserSegmentDecider.Change change, bool forceApply = false)
        {
            _entry.Value = _state;

            if (!change.HasChange && !forceApply)
                return;

            OnStateApplied();

            if (change.HasChange)
                EventBus<UserSegmentChanged>.Publish(new UserSegmentChanged(change.PrevSegmentId, change.CurrSegmentId,
                    change.Reason, change.MatchedKeyword, change.IsAmbiguous));
        }

        #endregion
    }
}
```

`AUserSegmentationHost.Generic.cs`

```csharp
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Horcrux.Runtime.Abstractions.Composites.UserSegmentation
{
    /// <summary>
    /// The project subclasses this with its segment enum (explicit values from 1, no 0) and its config row DTO,
    /// and hands over the typed tables it reads from its own remote config.
    /// </summary>
    public abstract class AUserSegmentationHost<TSegment, TConfig> : AUserSegmentationHost
        where TSegment : unmanaged, Enum
        where TConfig : class, IUserSegmentConfig<TSegment>, new()
    {
        #region Properties

        private const string LogPrefix = "[UserSegmentation]: ";

        private TSegment _currSegment;
        private TConfig _currConfig;

        public TSegment CurrSegment => _currSegment;

        /// <summary>Resolved row of the current segment; for the purchased segment, -1 fields are taken from the previous segment's row. Never null.</summary>
        public TConfig CurrConfig => _currConfig;

        protected abstract TSegment DefaultSegment { get; }
        protected abstract TSegment PurchasedSegment { get; }
        /// <summary>Row order is priority.</summary>
        protected abstract IReadOnlyList<IUserSegmentKeywordRule<TSegment>> KeywordRules { get; }
        protected abstract IReadOnlyList<TConfig> Configs { get; }

        protected sealed override int DefaultSegmentId => ToId(DefaultSegment);
        protected sealed override int PurchasedSegmentId => ToId(PurchasedSegment);

        #endregion

        #region Class Methods

        protected sealed override bool TryMatchCampaign(string campaign, out int segmentId, out string keyword, out bool isAmbiguous)
        {
            bool matched = UserSegmentKeywordMatcher.TryMatch(campaign, KeywordRules, out TSegment segment, out keyword, out isAmbiguous);
            segmentId = matched ? ToId(segment) : 0;
            return matched;
        }

        protected sealed override void OnStateApplied()
        {
            _currSegment = FromId(CurrSegmentId);
            TConfig target = FindConfigOrDefault(CurrSegmentId);

            if (CurrSegmentId != PurchasedSegmentId)
            {
                _currConfig = target;
                return;
            }

            int prevId = PrevSegmentId != 0 ? PrevSegmentId : DefaultSegmentId;
            _currConfig = UserSegmentConfigInheritance.Resolve(target, FindConfigOrDefault(prevId));
        }

        protected sealed override void ValidateConfigs()
        {
            IReadOnlyList<TConfig> configs = Configs;
            IReadOnlyList<IUserSegmentKeywordRule<TSegment>> rules = KeywordRules;
            Array values = Enum.GetValues(typeof(TSegment));

            for (int v = 0; v < values.Length; v++)
            {
                TSegment segment = (TSegment)values.GetValue(v);
                int id = ToId(segment);

                if (id == 0)
                {
                    Debug.LogError(LogPrefix + $"{typeof(TSegment).Name} must not define value 0: 0 means undecided", this);
                    continue;
                }

                int rowCount = 0;
                TConfig row = null;

                for (int i = 0; configs != null && i < configs.Count; i++)
                {
                    if (configs[i] != null && ToId(configs[i].Id) == id)
                    {
                        rowCount++;
                        row = configs[i];
                    }
                }

                if (rowCount == 0)
                    Debug.LogError(LogPrefix + $"no config row for segment {segment} ({id})", this);
                else if (rowCount > 1)
                    Debug.LogError(LogPrefix + $"{rowCount} config rows for segment {segment} ({id}); the last one wins", this);

                if (row != null && id != PurchasedSegmentId && UserSegmentConfigInheritance.HasInheritValue(row))
                    Debug.LogError(LogPrefix + $"segment {segment} ({id}) has a -1 field; only the purchased row may inherit", this);
            }

            for (int i = 0; rules != null && i < rules.Count; i++)
            {
                if (rules[i] == null || string.IsNullOrEmpty(rules[i].Keyword))
                    Debug.LogWarning(LogPrefix + $"keyword row {i} is empty and will be skipped", this);
            }
        }

        private TConfig FindConfigOrDefault(int id)
        {
            TConfig row = FindConfig(id);

            if (row != null)
                return row;

            Debug.LogError(LogPrefix + $"missing config row for segment {id}, using the default segment's row", this);
            row = FindConfig(DefaultSegmentId);

            if (row != null)
                return row;

            Debug.LogError(LogPrefix + "missing config row for the default segment too; using an empty row", this);
            return new TConfig();
        }

        private TConfig FindConfig(int id)
        {
            IReadOnlyList<TConfig> configs = Configs;
            TConfig found = null;

            for (int i = 0; configs != null && i < configs.Count; i++)
            {
                if (configs[i] != null && ToId(configs[i].Id) == id)
                    found = configs[i];
            }

            return found;
        }

        protected static int ToId(TSegment segment) => Convert.ToInt32(segment);
        protected static TSegment FromId(int id) => (TSegment)Enum.ToObject(typeof(TSegment), id);

        #endregion
    }
}
```

| Quyết định | Vì sao |
|---|---|
| `_entry.Value = _state` ở mọi `Commit`, kể cả không đổi | gán qua setter bắn `OnValueChanged`; `sessionCount` và `lastCampaign` đổi mà không đổi tập vẫn phải tới đĩa |
| Hàng trùng `id`: hàng cuối thắng | tất định, và `ValidateConfigs` đã `LogError`; không chọn hàng đầu để khớp với cách `FindConfig` duyệt hết |
| `FindConfigOrDefault` không đệ quy | hai bậc là đủ: hàng của tập, rồi hàng default, rồi hàng rỗng kèm lỗi |
| `CurrConfig` là field cache, không tính trong getter | consumer có thể đọc nhiều lần trong một level; `Resolve` dùng reflection |

**Nghiệm thu:** compile sạch. Commit: `feat(segmentation): persistence entry, host base and generic host`.

---

## Task 4 — Bước Adjust trong `Vendors/Adjust`

**Files (tạo):** `Vendors/Adjust/com.horcrux.adjust.asmdef` · `Vendors/Adjust/AdjustCampaignBootStep.cs`

**Consumes:** `AUserSegmentationHost` (Task 3) · `AdjustSdk.Adjust.GetAttribution` · `AdjustSdk.AdjustAttribution.Campaign`.
**Produces:** component kéo vào chuỗi boot, `order` lớn hơn host.

`com.horcrux.adjust.asmdef`

```json
{
    "name": "com.horcrux.adjust",
    "rootNamespace": "",
    "references": [
        "com.horcrux.runtime",
        "AdjustSdk.Scripts",
        "UniTask"
    ],
    "includePlatforms": [],
    "excludePlatforms": [],
    "allowUnsafeCode": false,
    "overrideReferences": false,
    "precompiledReferences": [],
    "autoReferenced": true,
    "defineConstraints": [
        "HORCRUX_ADJUST"
    ],
    "versionDefines": [
        {
            "name": "com.adjust.sdk",
            "expression": "5.0.0",
            "define": "HORCRUX_ADJUST"
        }
    ],
    "noEngineReferences": false
}
```

`AdjustCampaignBootStep.cs`

```csharp
using System;
using System.Threading;
using AdjustSdk;
using Cysharp.Threading.Tasks;
using Horcrux.Runtime.Abstractions.Bootstrap;
using Horcrux.Runtime.Abstractions.Composites.UserSegmentation;
using Horcrux.Runtime.Utilities;
using UnityEngine;

namespace Horcrux.Vendors.Adjust
{
    /// <summary>
    /// Feeds the Adjust campaign into the segmentation host. Polls <see cref="AdjustSdk.Adjust.GetAttribution"/> because
    /// the SDK offers no event once another system (the project's Adjust wrapper) owns the init config.
    /// First session waits up to <see cref="firstSessionWaitSeconds"/>; later sessions only poll in the background.
    /// </summary>
    public sealed class AdjustCampaignBootStep : ABootStep
    {
        #region Properties

        private const string LogPrefix = "[AdjustCampaign]: ";
        private const float PollIntervalSeconds = 0.5f;

        [Splitter("References")]
        [SerializeField] private AUserSegmentationHost segmentation;

        [Splitter("Configs")]
        [SerializeField, Min(0f), Tooltip("First session: wait for attribution at most this long before entering the game")]
        private float firstSessionWaitSeconds = 3f;
        [SerializeField, Min(0f), Tooltip("Keep polling in the background at most this long per session while the campaign is not locked")]
        private float lateArrivalPollSeconds = 60f;
        [SerializeField, Tooltip("Editor has no Adjust: this campaign is reported instead, to test keywords")]
        private string editorCampaign;

        private AdjustAttribution _attribution;
        private bool _hasAttribution;

        #endregion

        #region Unity Callbacks

        public override async UniTask InitializeAsync(CancellationToken ct)
        {
            if (segmentation == null)
            {
                Debug.LogError(LogPrefix + "segmentation is empty; drag the segmentation host", this);
                return;
            }

            if (!segmentation.IsReady)
            {
                Debug.LogError(LogPrefix + "segmentation host not initialized; its order must be lower than this step", this);
                return;
            }

            if (segmentation.IsCampaignLocked)
                return;

            if (Application.isEditor)
            {
                segmentation.ReportCampaign(editorCampaign);
                return;
            }

            if (segmentation.IsFirstSession && await PollAsync(firstSessionWaitSeconds, ct))
                return;

            // Own lifetime, not the boot token: the boot phase ends when this step returns.
            PollInBackgroundAsync(destroyCancellationToken).Forget();
        }

        #endregion

        #region Class Methods

        /// <summary>True when attribution arrived and was reported within <paramref name="maxSeconds"/>.</summary>
        private async UniTask<bool> PollAsync(float maxSeconds, CancellationToken ct)
        {
            float deadline = Time.realtimeSinceStartup + maxSeconds;

            while (Time.realtimeSinceStartup < deadline)
            {
                if (segmentation.IsCampaignLocked)
                    return true;

                AdjustSdk.Adjust.GetAttribution(OnAttribution);
                await UniTask.Delay(TimeSpan.FromSeconds(PollIntervalSeconds), DelayType.Realtime, cancellationToken: ct);

                if (!_hasAttribution)
                    continue;

                segmentation.ReportCampaign(_attribution.Campaign ?? string.Empty);
                return true;
            }

            return false;
        }

        private async UniTaskVoid PollInBackgroundAsync(CancellationToken ct)
        {
            try
            {
                if (await PollAsync(lateArrivalPollSeconds, ct))
                    return;

                Debug.Log(LogPrefix + $"no attribution within {lateArrivalPollSeconds}s; next session retries", this);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception e)
            {
                Debug.LogException(e, this);
            }
        }

        private void OnAttribution(AdjustAttribution attribution)
        {
            _attribution = attribution;
            _hasAttribution = attribution != null;
        }

        #endregion
    }
}
```

| Quyết định | Vì sao |
|---|---|
| Poll `GetAttribution` thay vì `AttributionChangedDelegate` | delegate đặt trên `AdjustConfig` lúc `InitSdk`, đã thuộc về wrapper của dự án; poll chạy với mọi wrapper |
| Callback là method group ghi vào field, loop đọc field | SDK có thể không gọi callback khi chưa có attribution; await callback là treo vô hạn |
| `destroyCancellationToken` cho vòng nền | token boot đóng khi bước trả về; vòng nền sống cùng component, chết khi service bị huỷ |
| `Application.isEditor` | cùng cách SDK kiểm (`IsEditor()`); trong Editor callback không bao giờ tới |
| Phiên đầu chờ, phiên sau không | grant đầu phiên (coin, booster) chỉ xảy ra một lần và không bù; phiên sau đổi tập chỉ đổi config |
| Gọi `AdjustSdk.Adjust.GetAttribution` đầy đủ namespace | bên trong namespace `Horcrux.Vendors.Adjust`, tên `Adjust` trỏ về namespace, không phải class SDK |

**Nghiệm thu:** compile sạch với và không có `com.adjust.sdk` (tắt package tạm thời hoặc đổi `expression` để kiểm assembly tắt).
Commit: `feat(segmentation): adjust campaign boot step`.

---

## Task 5 — Test EditMode (agent, sau khi Task 1–3 có code thật)

`Tests/EditMode/UserSegmentKeywordMatcherTests.cs` · `UserSegmentConfigInheritanceTests.cs` · `UserSegmentDeciderTests.cs`.
Test dùng enum và DTO giả khai trong file test. Chỉ danh sách case; code test viết theo chữ ký thật.

| Đơn vị | Case |
|---|---|
| Matcher | camp `null` → false · rỗng → false · `iap_us` với keyword `IAP` → khớp (hoa thường) · `Blended_BLD` với hai hàng cùng tập → khớp, không ambiguous · `IAP_Ads` với hàng IAP trước Ads → IAP, ambiguous · `Ads_IAP` cùng bảng → vẫn IAP (thứ tự bảng, không thứ tự trong camp) · không khớp → false · hàng keyword rỗng bị bỏ · `CategoryJam_Android_US` với keyword `Ads` → false · với keyword `Ad` → true (chứng minh vì sao đổi) |
| Inheritance | mọi field `-1` → lấy hết từ prev · không field nào `-1` → giống target · prev cùng `-1` → giữ `-1` · field không phải `int` chép nguyên · trả object khác tham chiếu, target không đổi · `HasInheritValue` đúng/sai |
| Decider `OnBoot` | state rỗng → curr = default, `Initial`, sessionCount 1 · phiên 2, 3 chưa khoá · phiên 4 với max 3 → khoá, không có change · đã khoá từ trước → giữ · max 0 → kẹp 1: phiên 2 mới khoá, phiên 1 chưa |
| Decider `OnCampaign` | chưa khoá, khớp IAP → curr IAP, `CampaignResolved`, khoá · chưa khoá, không khớp, curr đã default → không change nhưng khoá · camp rỗng → như không khớp · đã khoá → không change, `lastCampaign` vẫn ghi · curr purchased chưa khoá → khoá, không change · ambiguous truyền tới Change |
| Decider `OnPurchase` | từ BLD → prev BLD, curr purchased, khoá, `Purchase` · cờ tắt → không change, state nguyên · mua lần hai → không change · curr 0 → prev = default · mua rồi camp về → không đổi |

Chạy bằng `compile-check` (test EditMode một lệnh). Commit: `test(segmentation): matcher, inheritance, decider`.

---

## Task 6 — Tài liệu hệ (agent)

`Runtime/Abstractions/Composites/UserSegmentation/UserSegmentation.md` theo `docs` › `.md`: luồng dữ liệu
camp → Decider → save → event → config; bảng luật; "Trước khi chạy" (dưới); bẫy đã biết. Commit cùng Task 5 hoặc riêng.

---

## Editor setup — mục "Trước khi chạy" của hệ

Hệ chỉ chạy khi dự án đã dựng đủ; thiếu bước nào thì host `LogError` rồi thoát, bootstrap fail-open vào game ở tập mặc định.

1. Dự án có `GamePersistenceDataCollection : BasePersistenceDataCollection` và asset của nó. Trên asset, mục **User Segmentation**:
   `Key` = `user_segment_state`, `Default Value` để nguyên số 0 và chuỗi rỗng. Thiếu key: Persistence `LogError` lúc `Initialize`.
2. Dự án có host kế thừa `AUserSegmentationHost<TSegment, TConfig>`, trả bốn model từ remote config của nó.
3. Scene boot: `BootstrapService` với ba bước con theo thứ tự `order`: `SaveBootstep` (1) → host (2) → `AdjustCampaignBootStep` (3).
   Kéo asset collection vào `saveCollection` của host và vào `SaveBootstepInitializer`; kéo host vào `segmentation` của bước Adjust.
   Sai thứ tự: host hoặc bước Adjust `LogError` "order must be lower".
4. Remote config của dự án đã fetch xong (có thời hạn) **trước** `bootstrap.InitializeAsync()`.
5. Test trong Editor: điền `editorCampaign` trên bước Adjust rồi Play; đổi camp thì xoá key `persistence_user_segment_state`
   (tool PlayerPrefsEditor của Horcrux) để boot lại từ chưa quyết.

---

## Cheat

Chưa có: dự án chưa có SRDebugger. Khi có, một category gồm hàng đọc `CurrSegmentId`, `IsCampaignLocked`, `LastCampaign`,
`sessionCount`, và hai lệnh `ReportPurchase()`, `ReportCampaign(camp)`.
