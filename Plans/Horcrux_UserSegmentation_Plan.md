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
| Mục tiêu | mỗi user ở đúng một tập; tập quyết bởi campaign Adjust lúc cài, chuyển sang tập "đã mua" khi mua IAP; config của tập là một object đã ép kiểu, tập "đã mua" kế thừa ô `-1` từ tập ngay trước. **Pool tập mở**: tập là `segment_id` số nguyên trong hai bảng remote, thêm tập là thêm hàng dữ liệu, không build |
| Ranh giới | lõi không gọi tên type dự án, không đọc remote config, không biết Zego; lõi chỉ biết hai vai **default** và **purchased** qua hai id dự án trả về. Dự án khai DTO hàng pool (có `segment_id`) và DTO hàng keyword (`segment_id` + mảng `keywords`), đưa model đã ép kiểu vào host (tiền lệ `LiveOpsHost`: "module nhận model đã ép kiểu, host không biết RC") |
| Chỗ đặt | `Runtime/Abstractions/Composites/UserSegmentation/` (composite: dựng trên Persistence, Bootstrap, EventBus) · entry save trong partial `BasePersistenceDataCollection.UserSegmentation.cs` đặt cùng thư mục hệ (khuôn `TimeService`, khác chỗ đặt vì entry mang type của hệ) · bước Adjust trong `Vendors/Adjust/` với asmdef tự tắt theo package (khuôn `Vendors/Firebase`) |
| Phụ thuộc vendor | developer cho phép Horcrux tham chiếu thẳng `AdjustSdk.Scripts`; không bridge. Dự án thiếu `com.adjust.sdk` thì assembly vendor không biên dịch, hệ vẫn chạy: mọi user ở tập mặc định |
| Tên | `AUserSegmentationHost`, `UserSegmentDecider`, `UserSegmentKeywordMatcher`, `UserSegmentConfigInheritance`, `UserSegmentConfigTable`, `UserSegmentState`, `UserSegmentChanged`, `AdjustCampaignBootStep`. Developer đổi tuỳ ý trước khi gõ; agent sync plan theo code thật |

**Cố ý KHÔNG làm**

| Không làm | Vì |
|---|---|
| Interface cho nguồn campaign | implementation thứ hai không có thật; bước Adjust gọi vào host qua ô kéo thả kiểu `AUserSegmentationHost` |
| Host tự đọc `IRemoteConfigCollection` | host sẽ phải biết tên key của dự án; tiền lệ LiveOps đã chọn dự án đưa model vào |
| Ưu tiên giữa nhiều keyword | spec dự án để sau; bản này tất định: **hàng đầu trong bảng khớp thì thắng**, đổi ưu tiên là đảo hàng trên console |
| Đường ra khỏi tập "đã mua" | spec dự án: không có |
| Tính lại config khi remote về muộn | dự án fetch remote có thời hạn **trước** bootstrap; bản về muộn để phiên sau (luật RemoteConfigSystem) |
| Enum tập, ở lõi hay ở dự án | enum đóng pool: mỗi tập mới là một giá trị enum cộng một lần build; chỉ hai vai default và purchased có nghĩa trong code, ba tập còn lại chỉ khác nhau ở số. Id là `int` trong dữ liệu, `Validate` kiểm "id có hàng trong pool" thay cho "id có trong enum" |
| Cột `name` trong contract lõi | chỉ tracking của dự án cần tên; lõi log bằng id |

**Quyết định trái trực giác**

| Chỗ | Vì |
|---|---|
| Attribution về là khoá camp ngay, kể cả `Campaign` rỗng | Adjust đã trả lời xong; organic thật cũng về dạng này. Cửa sổ phiên chỉ dành cho user Adjust chưa bao giờ trả lời |
| `-1` nghĩa là kế thừa, không có cột `inherit` | kế thừa là thuộc tính của từng ô; CSV một cột một số |
| `PrevSegmentId` chỉ ghi lúc mua, không ghi lúc phân lại theo camp | nó chỉ phục vụ kế thừa của tập "đã mua" |
| Hàng keyword trỏ vào tập "đã mua" bị coi là không khớp | tập "đã mua" không có đường ra; một hàng gõ nhầm trên console sẽ nhốt user vĩnh viễn. `Validate` báo hàng đó, Decider bỏ qua |
| Thiếu hàng của tập **và** hàng mặc định thì ném exception, không trả hàng rỗng | hàng rỗng là kinh tế bằng 0 (inter từ level 1, booster miễn phí) chạy im lặng; asset là lưới đỡ nên bảng rỗng là lỗi authoring, phải nổ ở lần Play đầu |
| Field public của `UserSegmentState` viết PascalCase | tên field là khoá JSON của save; casing theo quyền truy cập, chốt trước khi có save đầu tiên |
| `resegmentMaxSessions < 1` kẹp về 1 | giá trị 0 sẽ khoá ngay lúc boot đầu, **trước** khi Adjust kịp về, mọi user thành mặc định vĩnh viễn |
| Base không generic giữ toàn bộ cơ chế kể cả khớp keyword, lớp generic chỉ tra config theo kiểu DTO | bước Adjust và tool Editor cần một kiểu kéo thả được; bảng keyword không mang kiểu dự án nên không có lý do nằm ở lớp generic |
| Base gọi `OnStateApplied()` cả khi không có thay đổi lúc boot | cache `CurrConfig` phải đúng ở mọi phiên, không chỉ phiên có đổi tập |
| Một hàng keyword mang **mảng** keyword, không mỗi keyword một hàng | bảng console đọc một dòng một tập; giá: CSV importer của RemoteConfigSystem chỉ nhận hàng phẳng nên bảng keyword nhập bằng Inspector hoặc ImportJson, pool vẫn đi CSV |
| `Application.isEditor` thay `#if UNITY_EDITOR` trong bước Adjust | `Adjust.GetAttribution` trong Editor return không gọi callback; kiểm runtime cùng cách SDK kiểm, không cắt đôi hàm async |

**Mở rộng sau**

- Hook tính lại config khi remote về giữa phiên: một `public void RefreshConfigs()` trên lớp generic, dự án nối vào `IRemoteConfigProvider.OnFetched` khi có nhu cầu.
- Tập mới: một hàng pool với `segment_id` chưa dùng và một hàng keyword trên console; không build, lõi và dự án không đổi.
- Ô CSV dạng `a|b|c` cho field `List<string>` trong `RemoteConfigCsvImporter`, khi bảng keyword cần đi đường sheet; hiện ba hàng gõ Inspector là đủ.
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
| `docs/Runtime/LiveOpsHost/LiveOpsHost.md` | "host nhận model đã ép kiểu, không biết RC"; host trừu tượng ở `Abstractions/` | 1 Hz loop, lịch tuần — không liên quan |
| `Implementations/Foundations/RemoteConfigSystem/RemoteConfigCsvImporter.cs` | `TryDescribe` từ chối hàng có field không phải ô phẳng ("needs the JSON import") → bảng keyword có mảng không đi CSV | — |
| `Library/PackageCache/com.adjust.sdk@…/Scripts/Adjust.cs` | `Adjust.GetAttribution(Action<AdjustAttribution>)`, assembly `AdjustSdk.Scripts`, namespace `AdjustSdk`; trong Editor return không callback | `GetAttributionWithTimeout`: timeout của SDK không huỷ được theo `ct` |
| `com.zego.adjust` `ZegoAdjust` | ý tưởng ô camp giả trong Editor | toàn bộ còn lại: trói vào Zego |
| `Assets/_Core/Diversity/` của dự án | — | port chết, enum tráo số; dự án xoá ở plan nối |

---

## Luật nghiệp vụ — nguồn duy nhất cho Task 2 và Task 5

Id tập là `int`, `0` nghĩa là chưa quyết. Dự án đặt số trong dữ liệu và chỉ ra hai vai `default`, `purchased` (category-jam: BLD 1 là
default, IAP 2, Purchased 3 là purchased, Ads 4).

| Sự kiện | Điều kiện | Kết quả |
|---|---|---|
| `OnBoot` | `CurrSegmentId == 0` | `curr = default`, thay đổi `Initial` |
| `OnBoot` | `!IsCampaignLocked && SessionCount >= max(1, resegmentMaxSessions)` | `IsCampaignLocked = true`, giữ tập |
| `OnBoot` | luôn, sau hai dòng trên | `SessionCount++` |
| `OnCampaign(camp)` | `IsCampaignLocked` | `LastCampaign = camp`, không đổi |
| `OnCampaign(camp)` | `!IsCampaignLocked && curr == purchased` | `LastCampaign = camp`, `IsCampaignLocked = true`, không đổi |
| `OnCampaign(camp)` | tập khớp `== purchased` | coi như không khớp (hàng dữ liệu sai, `Validate` đã báo) rồi áp dòng dưới |
| `OnCampaign(camp)` | `!IsCampaignLocked` | `LastCampaign = camp`, `IsCampaignLocked = true`; đích = tập khớp, không khớp hay camp rỗng → `default`; đích khác `curr` thì `curr = đích`, thay đổi `CampaignResolved` kèm keyword và cờ nhiều keyword |
| `OnPurchase` | `!isRuleEnabled` | không đổi |
| `OnPurchase` | `curr == purchased` | không đổi |
| `OnPurchase` | còn lại | `prev = curr` (curr là 0 thì `prev = default`), `curr = purchased`, `IsCampaignLocked = true`, thay đổi `Purchase` |
| đọc config | `curr != purchased` | hàng của `curr` |
| đọc config | `curr == purchased` | `Resolve(hàng purchased, hàng prev)`; `prev == 0` thì hàng `default` |

Khớp keyword: một hàng khớp khi camp chứa **bất kỳ** keyword nào trong mảng của nó (`campaign.IndexOf(keyword,
OrdinalIgnoreCase) >= 0`); hàng đầu khớp thắng, keyword báo ra là keyword đầu trong mảng khớp được; cờ nhiều keyword bật khi
còn hàng **khác tập** cũng khớp; keyword rỗng và hàng không có keyword bỏ qua.

Mỗi thay đổi tập host `Debug.Log` **một dòng**: lý do, tập cũ → mới, keyword, cờ nhiều keyword — rồi mới publish event.

Guard dữ liệu (`UserSegmentConfigTable.Validate`), chạy một lần lúc boot, chỉ `LogError` từng dòng, không sửa dữ liệu:
pool không rỗng · hàng pool không `null`, `segment_id >= 1` · không hai hàng cùng `segment_id` · có hàng cho `default` và cho
`purchased` · hàng không phải purchased không chứa `-1` · hàng keyword không `null`, có ít nhất một keyword, không keyword rỗng ·
hàng keyword không trỏ vào purchased và trỏ vào `segment_id` có hàng trong pool. Hàng thiếu khi tra thì dùng hàng `default` kèm
`LogError`; thiếu cả hàng `default` thì **ném** `InvalidOperationException`; config trả ra **không bao giờ `null`** và **không bao
giờ là hàng rỗng**.

---

## Task 1 — Contract, model, ba class thuần

**Files (tạo):** `Runtime/Abstractions/Composites/UserSegmentation/`
`IUserSegmentKeywordRule.cs` · `IUserSegmentConfig.cs` · `UserSegmentState.cs` · `UserSegmentChanged.cs` ·
`UserSegmentKeywordMatcher.cs` · `UserSegmentConfigInheritance.cs` · `UserSegmentConfigTable.cs`

**Produces:** mọi kiểu Task 2–4 dùng. Namespace `Horcrux.Runtime.Abstractions.Composites.UserSegmentation`.

`IUserSegmentKeywordRule.cs`

```csharp
using System.Collections.Generic;

namespace Horcrux.Runtime.Abstractions.Composites.UserSegmentation
{
    /// <summary>One row of the keyword table: a campaign containing any of <see cref="Keywords"/> selects <see cref="SegmentId"/>. Row order is priority.</summary>
    public interface IUserSegmentKeywordRule
    {
        int SegmentId { get; }
        IReadOnlyList<string> Keywords { get; }
    }
}
```

`IUserSegmentConfig.cs`

```csharp
namespace Horcrux.Runtime.Abstractions.Composites.UserSegmentation
{
    /// <summary>One row of the segment pool: the project DTO carries the fields, this only names the row. Ids start at 1; 0 means undecided.</summary>
    public interface IUserSegmentConfig
    {
        int Id { get; }
    }
}
```

`UserSegmentState.cs`

```csharp
using System;

namespace Horcrux.Runtime.Abstractions.Composites.UserSegmentation
{
    /// <summary>Save model. Field names are the wire format; 0 in <see cref="CurrSegmentId"/> means undecided.</summary>
    [Serializable]
    public sealed class UserSegmentState
    {
        public int CurrSegmentId;
        /// <summary>Segment the user was in right before the purchased segment; only written on purchase.</summary>
        public int PrevSegmentId;
        /// <summary>True once attribution answered, a purchase happened, or the resegment window closed.</summary>
        public bool IsCampaignLocked;
        public int SessionCount;
        public string LastCampaign;
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
        /// <summary>First row with a keyword the campaign contains (ordinal, ignore case) wins; <paramref name="keyword"/> is that row's first matching keyword.</summary>
        public static bool TryMatch(string campaign, IReadOnlyList<IUserSegmentKeywordRule> rules,
            out int segmentId, out string keyword, out bool isAmbiguous)
        {
            segmentId = 0;
            keyword = null;
            isAmbiguous = false;

            if (string.IsNullOrEmpty(campaign) || rules == null)
                return false;

            bool found = false;

            for (int i = 0; i < rules.Count; i++)
            {
                IUserSegmentKeywordRule rule = rules[i];

                if (rule == null || !TryMatchRule(campaign, rule.Keywords, out string matchedKeyword))
                    continue;

                if (!found)
                {
                    found = true;
                    segmentId = rule.SegmentId;
                    keyword = matchedKeyword;
                    continue;
                }

                if (rule.SegmentId != segmentId)
                {
                    isAmbiguous = true;
                    break;
                }
            }

            return found;
        }

        /// <summary>Empty keywords are skipped; a row without keywords never matches.</summary>
        private static bool TryMatchRule(string campaign, IReadOnlyList<string> keywords, out string matchedKeyword)
        {
            matchedKeyword = null;

            for (int i = 0; keywords != null && i < keywords.Count; i++)
            {
                string candidate = keywords[i];

                if (string.IsNullOrEmpty(candidate) || campaign.IndexOf(candidate, StringComparison.OrdinalIgnoreCase) < 0)
                    continue;

                matchedKeyword = candidate;
                return true;
            }

            return false;
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

`UserSegmentConfigTable.cs`

```csharp
using System.Collections.Generic;

namespace Horcrux.Runtime.Abstractions.Composites.UserSegmentation
{
    /// <summary>Lookup and validation over the segment pool and the keyword table. Pure: the host logs, the Editor simulator displays, tests call directly.</summary>
    public static class UserSegmentConfigTable
    {
        /// <summary>Row whose id equals <paramref name="id"/>; the last one when ids repeat (Validate reports that). Null when absent.</summary>
        public static TConfig Find<TConfig>(IReadOnlyList<TConfig> configs, int id) where TConfig : class, IUserSegmentConfig
        {
            TConfig found = null;

            for (int i = 0; configs != null && i < configs.Count; i++)
            {
                if (configs[i] != null && configs[i].Id == id)
                    found = configs[i];
            }

            return found;
        }

        /// <summary>Appends one line per invalid row to <paramref name="errors"/>. Never fixes the data. True when nothing was appended.</summary>
        public static bool Validate<TConfig>(IReadOnlyList<TConfig> configs, IReadOnlyList<IUserSegmentKeywordRule> rules,
            int defaultSegmentId, int purchasedSegmentId, List<string> errors)
            where TConfig : class, IUserSegmentConfig
        {
            int before = errors.Count;

            if (configs == null || configs.Count == 0)
                errors.Add("segment pool is empty");

            for (int i = 0; configs != null && i < configs.Count; i++)
            {
                TConfig row = configs[i];

                if (row == null)
                {
                    errors.Add($"pool row {i} is null");
                    continue;
                }

                if (row.Id < 1)
                    errors.Add($"pool row {i} has segment id {row.Id}; ids start at 1, 0 means undecided");

                for (int j = 0; j < i; j++)
                {
                    if (configs[j] != null && configs[j].Id == row.Id)
                    {
                        errors.Add($"pool rows {j} and {i} share segment id {row.Id}; the last one wins");
                        break;
                    }
                }

                if (row.Id != purchasedSegmentId && UserSegmentConfigInheritance.HasInheritValue(row))
                    errors.Add($"segment {row.Id} has a -1 field; only the purchased segment {purchasedSegmentId} may inherit");
            }

            if (Find(configs, defaultSegmentId) == null)
                errors.Add($"no pool row for the default segment {defaultSegmentId}");

            if (Find(configs, purchasedSegmentId) == null)
                errors.Add($"no pool row for the purchased segment {purchasedSegmentId}");

            for (int i = 0; rules != null && i < rules.Count; i++)
            {
                IUserSegmentKeywordRule rule = rules[i];

                if (rule == null)
                {
                    errors.Add($"keyword row {i} is null and will be skipped");
                    continue;
                }

                if (rule.Keywords == null || rule.Keywords.Count == 0)
                    errors.Add($"keyword row {i} (segment {rule.SegmentId}) has no keyword and never matches");

                for (int k = 0; rule.Keywords != null && k < rule.Keywords.Count; k++)
                {
                    if (string.IsNullOrEmpty(rule.Keywords[k]))
                        errors.Add($"keyword row {i} (segment {rule.SegmentId}) has an empty keyword at {k}; it is skipped");
                }

                if (rule.SegmentId == purchasedSegmentId)
                    errors.Add($"keyword row {i} points at the purchased segment {purchasedSegmentId}; a campaign never grants it, the row is ignored");
                else if (Find(configs, rule.SegmentId) == null)
                    errors.Add($"keyword row {i} points at segment {rule.SegmentId}, which has no pool row");
            }

            return errors.Count == before;
        }
    }
}
```

| Quyết định | Vì sao |
|---|---|
| Reflection trên field `int` public, cache tĩnh theo `TConfig` | DTO của dự án là field public để RemoteConfig và CSV đọc được; cache dựng một lần mỗi kiểu, `Resolve` chỉ chạy khi đổi tập |
| `Resolve` trả bản sao, không sửa `target` | `target` là object trong `RemoteConfig<List<T>>` của dự án, sửa tại chỗ là ghi vào dữ liệu của hệ khác |
| `IsAmbiguous` chỉ bật khi hàng khác **tập** khớp | hai keyword của cùng một tập khớp là bình thường (`Blended` và `BLD` nay cùng một hàng) |
| Tra hàng và kiểm hợp lệ là class static thuần, không nằm trong host | host, Simulator Editor và test cùng gọi một bản; hai bản luật kiểm buộc khớp nhau thì phải suy từ một nguồn |
| `Validate` ghi vào `List<string>` thay vì `Debug.LogError` | Simulator hiện danh sách, test so chuỗi; host là nơi duy nhất quyết log đi đâu |
| `IUserSegmentKeywordRule`, `IUserSegmentConfig`, `TryMatch` không generic; chỉ `TConfig` còn generic | id là `int` trong dữ liệu; `TConfig` còn vì consumer đọc field của DTO dự án và `Resolve` cần `new()`. `Find`/`Validate` suy `TConfig` từ tham số, không phải ghi type argument |
| `Validate` nhận cả `defaultSegmentId` | pool mở không có enum để liệt kê hàng phải có; hai hàng bắt buộc là hai vai code biết |
| Kiểm trùng id bằng hai vòng lồng | pool vài hàng, chạy một lần lúc boot; `HashSet` là cấp phát cho một câu hỏi không ai hỏi mỗi frame |

**Nghiệm thu:** compile sạch (`compile-check`). Commit: `feat(segmentation): contract, state, matcher, inheritance, config table`.

---

## Task 2 — `UserSegmentDecider`

**Files (tạo):** `Runtime/Abstractions/Composites/UserSegmentation/UserSegmentDecider.cs`

**Consumes:** `UserSegmentState`, `UserSegmentChangeReason` (Task 1).
**Produces:** `UserSegmentDecider.Change`, `OnBoot`, `OnCampaign`, `OnPurchase` — Task 3 gọi, Task 5 test.

```csharp
using System;

namespace Horcrux.Runtime.Abstractions.Composites.UserSegmentation
{
    /// <summary>Every business rule, as static functions that mutate the given state and return the change. Segment ids are ints; 0 means undecided.</summary>
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

            if (state.CurrSegmentId == 0)
            {
                state.CurrSegmentId = defaultSegmentId;
                change = new Change(0, defaultSegmentId, UserSegmentChangeReason.Initial, null, false);
            }

            int maxSessions = Math.Max(1, resegmentMaxSessions);

            if (!state.IsCampaignLocked && state.SessionCount >= maxSessions)
                state.IsCampaignLocked = true;

            state.SessionCount++;
            return change;
        }

        /// <summary>Attribution answered. <paramref name="matchedSegmentId"/> is 0 when no row matched.</summary>
        public static Change OnCampaign(UserSegmentState state, string campaign, int matchedSegmentId, string matchedKeyword,
            bool isAmbiguous, int defaultSegmentId, int purchasedSegmentId)
        {
            state.LastCampaign = campaign;

            if (state.IsCampaignLocked)
                return Change.None;

            state.IsCampaignLocked = true;

            if (state.CurrSegmentId == purchasedSegmentId)
                return Change.None;

            // A keyword row pointing at the purchased segment is a data error (Validate reports it): a campaign never grants that segment.
            if (matchedSegmentId == purchasedSegmentId)
                matchedSegmentId = 0;

            int target = matchedSegmentId != 0 ? matchedSegmentId : defaultSegmentId;

            if (target == state.CurrSegmentId)
                return Change.None;

            int prev = state.CurrSegmentId;
            state.CurrSegmentId = target;
            return new Change(prev, target, UserSegmentChangeReason.CampaignResolved, matchedKeyword, isAmbiguous);
        }

        /// <summary>A real purchase (not restore, not refund, not a remove-ads-only pack: the project filters those).</summary>
        public static Change OnPurchase(UserSegmentState state, bool isRuleEnabled, int defaultSegmentId, int purchasedSegmentId)
        {
            if (!isRuleEnabled)
                return Change.None;

            if (state.CurrSegmentId == purchasedSegmentId)
                return Change.None;

            int prev = state.CurrSegmentId != 0 ? state.CurrSegmentId : defaultSegmentId;
            state.PrevSegmentId = prev;
            state.CurrSegmentId = purchasedSegmentId;
            state.IsCampaignLocked = true;
            return new Change(prev, purchasedSegmentId, UserSegmentChangeReason.Purchase, null, false);
        }
    }
}
```

| Quyết định | Vì sao |
|---|---|
| Decider nhận kết quả khớp đã tính, không nhận bảng keyword | giữ Decider không generic; khớp là việc của `UserSegmentKeywordMatcher`, host nối hai thứ |
| `LastCampaign` ghi cả khi đã khoá | tracking cần camp thật của user; Zego không cache nó ở đâu |
| Kiểm cửa sổ **trước** `SessionCount++` | phiên 1..N còn phân lại được, khoá ở đầu phiên N+1; với N = 3 khớp "hết 3 phiên" |
| Khớp vào purchased hạ về 0 ngay trong Decider, không chỉ ở `Validate` | `Validate` chỉ log; luật "campaign không bao giờ cấp purchased" phải đứng ở nơi quyết định, và test được không cần host |

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
**Produces:** `AUserSegmentationHost` (kiểu kéo thả cho bước Adjust và tool Editor; giữ bảng keyword và khớp camp) ·
`AUserSegmentationHost<TConfig>` (dự án kế thừa; giữ pool và `CurrConfig`).

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
using System.Collections.Generic;
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
    /// Segment ids are ints from the project's data; the base only knows the default and the purchased roles.
    /// </summary>
    public abstract class AUserSegmentationHost : ABootStep
    {
        #region Properties

        protected const string LogPrefix = "[UserSegmentation]: ";

        [Splitter("References")]
        [SerializeField] private BasePersistenceDataCollection saveCollection;

        private PersistenceDataEntry<UserSegmentState> _entry;
        private UserSegmentState _state;

        /// <summary>False until <see cref="InitializeAsync"/> ran with a valid collection. While false, derived config is null: a missing setup must surface, not run at a default.</summary>
        public bool IsReady => _state != null;
        public int CurrSegmentId => _state != null ? _state.CurrSegmentId : 0;
        public int PrevSegmentId => _state != null ? _state.PrevSegmentId : 0;
        public bool IsCampaignLocked => _state != null && _state.IsCampaignLocked;
        public bool IsFirstSession => _state != null && _state.SessionCount == 1;
        public string LastCampaign => _state?.LastCampaign;

        /// <summary>Segment a user lands in without a matching campaign. Must have a pool row.</summary>
        protected abstract int DefaultSegmentId { get; }
        /// <summary>Segment entered on purchase only; its pool row may inherit with -1. Must have a pool row.</summary>
        protected abstract int PurchasedSegmentId { get; }
        /// <summary>Row order is priority. Read at boot and on each campaign report, never cached here.</summary>
        protected abstract IReadOnlyList<IUserSegmentKeywordRule> KeywordRules { get; }
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

            bool wasLocked = _state.IsCampaignLocked;
            UserSegmentKeywordMatcher.TryMatch(campaign, KeywordRules, out int segmentId, out string keyword, out bool isAmbiguous);
            UserSegmentDecider.Change change = UserSegmentDecider.OnCampaign(_state, campaign, segmentId,
                keyword, isAmbiguous, DefaultSegmentId, PurchasedSegmentId);

            if (wasLocked)
                Debug.Log(LogPrefix + $"campaign '{campaign}' arrived after lock, segment stays {_state.CurrSegmentId}", this);

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

        /// <summary>Typed side: LogError every invalid row once at boot (<see cref="UserSegmentConfigTable.Validate{TConfig}"/>). Never fix the data.</summary>
        protected abstract void ValidateConfigs();

        /// <summary>Called after boot and after every change: rebuild the resolved config.</summary>
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

            if (!change.HasChange)
                return;

            Debug.Log(LogPrefix + $"{change.Reason}: segment {change.PrevSegmentId} -> {change.CurrSegmentId}"
                + (change.MatchedKeyword != null ? $" by keyword '{change.MatchedKeyword}'" : string.Empty)
                + (change.IsAmbiguous ? ", another segment's keyword also matched" : string.Empty), this);

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
    /// The project subclasses this with its pool row DTO and hands over the typed pool it reads from its own remote config.
    /// </summary>
    public abstract class AUserSegmentationHost<TConfig> : AUserSegmentationHost
        where TConfig : class, IUserSegmentConfig, new()
    {
        #region Properties

        private TConfig _currConfig;

        /// <summary>Resolved row of the current segment; for the purchased segment, -1 fields are taken from the previous segment's row. Null only while <see cref="AUserSegmentationHost.IsReady"/> is false.</summary>
        public TConfig CurrConfig => _currConfig;

        /// <summary>Segment pool: one row per segment id, ids from 1. Read at boot and on each change, never cached here.</summary>
        protected abstract IReadOnlyList<TConfig> Configs { get; }

        #endregion

        #region Class Methods

        protected sealed override void OnStateApplied()
        {
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
            List<string> errors = new();
            UserSegmentConfigTable.Validate(Configs, KeywordRules, DefaultSegmentId, PurchasedSegmentId, errors);

            for (int i = 0; i < errors.Count; i++)
                Debug.LogError(LogPrefix + errors[i], this);
        }

        private TConfig FindConfigOrDefault(int id)
        {
            TConfig row = UserSegmentConfigTable.Find(Configs, id);

            if (row != null)
                return row;

            Debug.LogError(LogPrefix + $"no pool row for segment {id}, using the default segment's row", this);
            row = UserSegmentConfigTable.Find(Configs, DefaultSegmentId);

            if (row != null)
                return row;

            // An empty row would be a zero economy (interstitial from level 1, free boosters) running silently: an authoring error must explode.
            throw new InvalidOperationException(LogPrefix
                + $"no pool row for segment {id} nor for the default segment {DefaultSegmentId}; the segment pool on the remote config asset is empty or wrong");
        }

        #endregion
    }
}
```

| Quyết định | Vì sao |
|---|---|
| `_entry.Value = _state` ở mọi `Commit`, kể cả không đổi | gán qua setter bắn `OnValueChanged`; Persistence so payload nên cùng reference vẫn ghi đĩa khi `SessionCount` hay `LastCampaign` đổi |
| Hàng trùng `id`: hàng cuối thắng | tất định, và `Validate` đã báo; khớp với cách `UserSegmentConfigTable.Find` duyệt hết |
| `FindConfigOrDefault` hai bậc rồi ném | hàng của tập, rồi hàng default; hết hai bậc là bảng rỗng — lỗi authoring, nổ ở lần Play đầu thay vì kinh tế bằng 0 |
| `CurrConfig` là field cache, không tính trong getter | consumer có thể đọc nhiều lần trong một level; `Resolve` dùng reflection |
| `saveCollection` trống: `LogError` rồi thoát, `CurrConfig` để null | consumer nổ ở lần chạm đầu — đúng ý: setup thiếu phải lộ, không chạy cả phiên ở tập mặc định như thể đã dựng đúng |
| `LogPrefix` là `protected const` ở base | lớp generic và lớp con của dự án log cùng một tiền tố, không khai lại |
| `KeywordRules` và khớp camp ở base, không ở lớp generic | bảng keyword không mang kiểu dự án; Simulator đọc nó qua kiểu `AUserSegmentationHost` không cần reflection generic |
| `DefaultSegmentId`, `PurchasedSegmentId` là abstract dự án trả hằng, không phải ô Inspector | hai id là wire format đã chốt cùng bảng, đổi là đổi dữ liệu đã lưu; ô số mặc định `0` là ô trống hợp lệ không ai thấy |

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

`Tests/EditMode/UserSegmentKeywordMatcherTests.cs` · `UserSegmentConfigInheritanceTests.cs` · `UserSegmentConfigTableTests.cs` ·
`UserSegmentDeciderTests.cs`. Test dùng DTO giả (hàng pool, hàng keyword) khai trong file test. Chỉ danh sách case; code test viết theo chữ ký thật.

| Đơn vị | Case |
|---|---|
| Matcher | camp `null` → false · rỗng → false · `rules == null` → false · hàng rule `null` bị bỏ · `iap_us` với keyword `IAP` → khớp (hoa thường) · hàng `[BLD, Blended, Hybrid]` với camp `Hybrid_01` → khớp, keyword báo `Hybrid` · camp `Blended_BLD` cùng hàng → keyword báo `BLD` (thứ tự trong mảng, không thứ tự trong camp), không ambiguous · hai hàng cùng tập cùng khớp → không ambiguous · `IAP_Ads` với hàng IAP trước Ads → IAP, ambiguous · `Ads_IAP` cùng bảng → vẫn IAP (thứ tự bảng) · không khớp → false · hàng `Keywords == null` hay rỗng bị bỏ · keyword rỗng trong mảng bị bỏ, keyword sau nó vẫn khớp · `CategoryJam_Android_US` với keyword `Ads` → false · với keyword `Ad` → true (chứng minh vì sao đổi) |
| Inheritance | mọi field `-1` → lấy hết từ prev · không field nào `-1` → giống target · prev cùng `-1` → giữ `-1` · `prev == null` → bản sao của target nguyên `-1` · field không phải `int` (`name`) chép nguyên từ target · trả object khác tham chiếu, target không đổi · `HasInheritValue` đúng/sai |
| ConfigTable `Find` | có hàng → trả hàng · trùng id → hàng cuối · không có → null · `configs == null` → null · hàng `null` trong list bị bỏ |
| ConfigTable `Validate` | pool đủ bốn hàng, rule hợp lệ → true, errors rỗng · pool `null` hay rỗng → ba dòng (rỗng, thiếu default, thiếu purchased), không ném · hàng `null` → một dòng · id `0` hay âm → một dòng · hai hàng cùng id → một dòng · hàng không purchased có `-1` → một dòng · thiếu hàng default → một dòng · thiếu hàng purchased → một dòng · rule `null` → một dòng · rule không keyword (`null` hay rỗng) → một dòng · keyword rỗng trong mảng → một dòng nêu vị trí · rule trỏ purchased → một dòng · rule trỏ id không có hàng pool → một dòng · `rules == null` → không ném, không dòng nào về keyword |
| Decider `OnBoot` | state rỗng → curr = default, `Initial`, `SessionCount` 1 · phiên 2, 3 chưa khoá · phiên 4 với max 3 → khoá, không có change · đã khoá từ trước → giữ · max 0 → kẹp 1: phiên 2 mới khoá, phiên 1 chưa |
| Decider `OnCampaign` | chưa khoá, khớp IAP → curr IAP, `CampaignResolved`, khoá · chưa khoá, không khớp, curr đã default → không change nhưng khoá · camp rỗng → như không khớp · **khớp vào purchased → như không khớp, curr = default, vẫn khoá** · đã khoá → không change, `LastCampaign` vẫn ghi · curr purchased chưa khoá → khoá, không change · ambiguous truyền tới Change |
| Decider `OnPurchase` | từ BLD → prev BLD, curr purchased, khoá, `Purchase` · cờ tắt → không change, state nguyên · mua lần hai → không change · curr 0 → prev = default · mua rồi camp về → không đổi |

Chạy bằng `compile-check` (test EditMode một lệnh). Commit: `test(segmentation): matcher, inheritance, config table, decider`.

---

## Task 6 — Tài liệu hệ (agent)

`Runtime/Abstractions/Composites/UserSegmentation/UserSegmentation.md` theo `docs` › `.md`: luồng dữ liệu
camp → Decider → save → event → config; bảng luật; "Trước khi chạy" (dưới); bẫy đã biết. Commit cùng Task 5 hoặc riêng.

---

## Editor setup — mục "Trước khi chạy" của hệ

Hệ chỉ chạy khi dự án đã dựng đủ; thiếu bước nào thì host `LogError` rồi thoát, `IsReady` ở lại false và `CurrConfig` là null:
consumer đầu tiên đọc config sẽ ném. Đó là chủ ý — setup thiếu phải lộ ngay lần Play đầu, không chạy cả phiên ở tập mặc định.

1. Dự án có `GamePersistenceDataCollection : BasePersistenceDataCollection` và asset của nó. Trên asset, mục **User Segmentation**:
   `Key` = `user_segment_state`, `Default Value` để nguyên số 0 và chuỗi rỗng. Thiếu key: Persistence `LogError` lúc `Initialize`.
2. Dự án có host kế thừa `AUserSegmentationHost<TConfig>`, trả hai id vai và bốn model từ remote config của nó.
3. Scene boot: `BootstrapService` với ba bước con theo thứ tự `order`: `SaveBootstep` (1) → host (2) → `AdjustCampaignBootStep` (3).
   Kéo asset collection vào `saveCollection` của host và vào `SaveBootstepInitializer`; kéo host vào `segmentation` của bước Adjust.
   Sai thứ tự: host hoặc bước Adjust `LogError` "order must be lower".
4. Remote config của dự án đã fetch xong (có thời hạn) **trước** `bootstrap.InitializeAsync()`.
5. Test trong Editor: điền `editorCampaign` trên bước Adjust rồi Play; đổi camp thì xoá key `persistence_user_segment_state`
   (tool PlayerPrefsEditor của Horcrux) để boot lại từ chưa quyết.

---

## Cheat

Chưa có: dự án chưa có SRDebugger. Khi có, một category gồm hàng đọc `CurrSegmentId`, `IsCampaignLocked`, `LastCampaign`,
`SessionCount`, và hai lệnh `ReportPurchase()`, `ReportCampaign(camp)`.
