# Plan — `DebugOptionsCategory`: category SRDebugger dựng bằng code lên Horcrux

Ngày 2026-11-23. Nguồn: category-jam `Assets/_Game/Scripts/Cheat/{Tracking,EconomyCheat,LevelCheat}DebugOptions.cs`,
`Assets/_Game/Scripts/Segmentation/UserSegmentationDebugOptions.cs`. Bốn class chép cùng một khối: kiểm `SRDebug.Instance`, dựng
`DynamicOptionContainer`, tự đếm `order`, gỡ container lúc huỷ, giữ tay danh sách hàng phải báo lại sau lệnh. Lõi gói khối đó; từ
vựng (category, hàng, lệnh) và lệnh thật vẫn ở dự án.

**Phân công:** GĐ1 developer gõ · GĐ2 (dự án chuyển sang bản Horcrux, tài liệu, Sổ tay skill) agent làm. Không có test tự động:
class chỉ nối vào service của SRDebugger, ngoài Play `SRDebug.Instance` là `null` nên mọi nhánh có ích đều cần panel thật.
**Chạy được, thấy được:** GĐ1 compile sạch (`bash "<horcrux>/Editor/CompileCheck/Tools~/verify.sh" --csproj
com.horcrux.srdebugger.csproj,Assembly-CSharp.csproj` chạy từ gốc dự án) · GĐ2 bốn category hiện đủ trong panel, lệnh xong hàng đọc đổi ngay. GĐ1 commit trong
`Assets/Horcrux`, GĐ2 commit ở dự án kèm con trỏ submodule. Không phụ thuộc Plan nào, không có lõi toán.

Code GĐ1 đã compile thử bằng csc trên DLL của dự án (SRDebugger `com.zego.srdebugger`, Unity 6000.0.73f1, C# 9), kèm một stub gọi
đúng các lệnh GĐ2 của Economy. `SkipToAllowed` đã chạy qua reflection trên enum `None, Fan, ExtraSlot, Pack, Magnet, AddBox` với
allowed `Fan, ExtraSlot, Magnet`: tới từ ExtraSlot nhận Pack → Magnet · tới từ Magnet nhận AddBox → Fan (vòng qua None) · lùi từ
Fan nhận None → Magnet · lùi từ Magnet nhận Pack → ExtraSlot.

---

## Ngữ cảnh đã chốt

- **Người dùng:** class cheat của dự án (một `MonoBehaviour` mỗi hệ, đặt trong scene service) dựng một category trong tab Options
  của SRDebugger. Từ bốn class hiện có: hàng chỉ đọc, ô nhập (int, enum, string), nút lệnh, nút mở panel debug của SDK, ô số kèm
  nút Set (Economy: booster, coin, mạng), ô chọn enum bỏ qua giá trị canh (Economy: loại booster).
- **Chỗ đặt:** `Vendors/SRDebugger/`, assembly riêng `com.horcrux.srdebugger`, namespace `Horcrux.Vendors.SRDebuggerOptions`.
  Bật bằng `versionDefines` như `Vendors/Adjust`: dự án không cài SRDebugger thì assembly không compile, không lỗi. Tên class,
  namespace đổi tuỳ ý trước khi gõ.
- **Ranh giới:** đây là **lớp nối với panel**, không phải chỗ đặt lệnh. Lệnh, cửa đọc `private` và suy luận vẫn thuộc hệ (`I<Hệ>Debug`
  trong hệ Horcrux, `ILevelCheater` ở dự án); dự án dùng panel khác thì bỏ assembly này, hệ không đổi.
- **Hành vi:** mỗi lệnh chạy xong báo **mọi** hàng và ô nhập đọc lại. SRDebugger chỉ đọc lại getter khi bind, mở panel, đổi tab hoặc
  khi được báo; bốn class hiện tự giữ danh sách hàng phải báo, sót một hàng là hàng đứng số cũ tới lần mở panel sau.
- **Thứ tự hàng** là thứ tự gọi `Add*`. Category trống hay SRDebugger tắt trong Settings (`SRDebug.Instance == null`) thì `Attach`
  không làm gì.
- **Không làm:**
  - `Attach` trả `bool`: không caller nào rẽ nhánh theo nó.
  - `Add*` trả `OptionDefinition`: caller cũ giữ tham chiếu chỉ để báo lại hàng, giờ class tự báo.
  - Cờ "không refresh" trên lệnh: báo vài hàng mỗi lần bấm rẻ hơn một ô phải nhớ.
  - Gỡ từng hàng sau khi `Attach`: chưa caller nào cần.
  - Hỗ trợ `SROptions` partial: foods_jam dùng kiểu đó, cơ chế khác hẳn, không phải người dùng của class này.
- **Quyết định trái trực giác:**
  - `AddPanelOpener` tách khỏi `AddCommand`, không phải một cờ trên `AddCommand`. Panel SDK (Zego `CanvasSdkDebugger` sort 400)
    nằm dưới panel SRDebugger: hiện ra nhưng không nhận chạm. Luật "ẩn SRDebugger trước" do class giữ, caller không cần biết.
  - Ô nhập cũng được báo lại sau lệnh. Economy hiện ô số dư thật cho tới khi QA gõ, Set xong phải về số dư mới.
  - `AddAmountSetter` từ chối số ngoài `min..max` kèm log, không kẹp: kẹp im lặng làm QA tưởng mình đã đặt đúng số vừa gõ. `max`
    là delegate vì trần đổi trong phiên (mạng tối đa tăng khi mua gói).
  - `AddChoice` đổi lựa chọn thì bỏ số đã gõ ở **mọi** ô số, không chỉ ô phụ thuộc: số gõ cho lựa chọn cũ, giữ lại là đặt nhầm
    sang lựa chọn mới. Một category có một ô chọn là ca hiện có; category có hai ô chọn độc lập thì tách category.
  - Ô số và nút Set là **một** lệnh `AddAmountSetter`, không phải `AddInput` + `AddCommand` caller tự nối: số đang gõ dở là trạng
    thái của cặp ô đó, caller giữ thì mỗi cặp lại một field `int?` và một hàm kiểm miền.

### Đã khảo sát

| Nguồn | Lấy gì | Không lấy, vì sao |
|---|---|---|
| 4 class `*DebugOptions` dự án | khối `Register`/`Unregister`, comment hai ca `null`, cách đếm `order` | phần lệnh: từ vựng dự án. `TryTakePending` và `SelectBooster` + `Wrap` của Economy không mang tên booster hay coin, nên thành `AddAmountSetter`, `AddChoice` |
| SRDebugger `OptionDefinition` | `Create<T>(name, getter, setter, category, sortPriority)`, `FromMethod(name, Action, category, sortPriority)` | constructor `MethodReference`: `FromMethod` đủ |
| SRDebugger `DynamicOptionContainer` | `AddOption`; thêm sau `AddOptionContainer` vẫn hiện (bắn `OptionAdded`) | `RemoveOption`: chưa caller |
| SRF `PropertyReference.NotifyValueChanged()` | cách báo hàng đọc lại | — |
| `SRDebug.Instance.HideDebugPanel()` | ẩn panel trước khi mở panel khác (đang ở `TrackingDebugOptions`) | — |
| Horcrux `Vendors/Adjust/com.horcrux.adjust.asmdef` | khuôn `versionDefines` + `defineConstraints` | — |
| Skill `workflow` › Cheat (Sổ tay) | khuôn `DynamicOptionContainer` + `OptionDefinition` | plain class nhận hệ qua constructor: bốn class dự án đều là `MonoBehaviour` |

## Luồng mô tả — một category từ lúc boot tới lúc thoát

```
Awake của class cheat dự án: new DebugOptionsCategory("Cheat Economy")
        ▼
AddRow / AddInput / AddAmountSetter / AddChoice / AddCommand / AddPanelOpener ──► OptionDefinition vào container, order tăng dần
        ▼
Attach ──► SRDebug.Instance null? ──có──► dừng (SRDebugger tắt)
        │ không
        ▼ AddOptionContainer: category hiện trong tab Options
QA bấm lệnh ──► run() của dự án ──► RefreshRows: mọi hàng, ô nhập NotifyValueChanged ──► panel vẽ số mới
QA gõ ô số ──► giữ số đã gõ, ô hiện số đó ──► bấm Set: chưa gõ? dừng · ngoài min..max? log lỗi, giữ số · còn: write() ──► RefreshRows
QA bấm mũi tên ô chọn ──► SkipToAllowed theo chiều bấm ──► write() ──► bỏ mọi số đã gõ ──► RefreshRows
QA bấm nút mở panel SDK ──► HideDebugPanel ──► openPanel() của dự án (Zego.SdkDebugger.Show)
        ▼
OnDestroy: Detach ──► chưa attach? dừng · Instance null (thoát app)? dừng · còn: RemoveOptionContainer
```

## Bản đồ hệ cuối

```
  class cheat dự án (MonoBehaviour, scene service)                 SRDebugger
  ├─ Awake:    new · Add* · Attach ──────────► DebugOptionsCategory ──► DynamicOptionContainer ──► IDebugService
  ├─ setter:   RefreshRows (ô này đổi hàng khác)          │ _valueRows ── NotifyValueChanged ──► PropertyReference
  ├─ lệnh:     gọi hệ (I<Hệ>Debug, ILevelCheater…)        │ AddPanelOpener ── HideDebugPanel
  └─ OnDestroy: Detach ───────────────────────►           ▼
```

---

## Giai đoạn 1 — Assembly và class `DebugOptionsCategory`

**Mục tiêu:** dự án dựng được một category đủ sáu loại hàng mà không chạm `SRDebug.Instance`, `DynamicOptionContainer`, `order`.

**Vì sao bây giờ:** class ngắn (một màn hình) và mỗi member đã có caller thật trong bốn class dự án; chia nhỏ hơn thì giai đoạn đầu
không có category nào chuyển được trọn.

### `Vendors/SRDebugger/com.horcrux.srdebugger.asmdef` — file mới

```json
{
    "name": "com.horcrux.srdebugger",
    "rootNamespace": "",
    "references": [
        "StompyRobot.SRDebugger",
        "StompyRobot.SRF"
    ],
    "includePlatforms": [],
    "excludePlatforms": [],
    "allowUnsafeCode": false,
    "overrideReferences": false,
    "precompiledReferences": [],
    "autoReferenced": true,
    "defineConstraints": [
        "HORCRUX_SRDEBUGGER"
    ],
    "versionDefines": [
        {
            "name": "com.zego.srdebugger",
            "expression": "0.0.0",
            "define": "HORCRUX_SRDEBUGGER"
        }
    ],
    "noEngineReferences": false
}
```

- `StompyRobot.SRF` cần cho `Property.NotifyValueChanged()` (`PropertyReference` nằm ở SRF).
- `versionDefines` chỉ đặt define **trong chính assembly khai nó**, nên cặp `versionDefines` + `defineConstraints` phải nằm cùng file.
  Bản Zego mang `version` `0.0.0`, nên biểu thức `0.0.0` (nghĩa là ≥ 0.0.0). Dự án cài SRDebugger dưới tên package khác thì thêm
  một entry cùng `define`; nhập từ Asset Store (không phải package) thì thêm `HORCRUX_SRDEBUGGER` vào Scripting Define Symbols.
- `autoReferenced: true`: `Assembly-CSharp` thấy class mà không phải sửa asmdef nào của dự án.

### `Vendors/SRDebugger/DebugOptionsCategory.cs` — file mới

Ai gọi từng member (sau GĐ2): `AddRow` Tracking, Level, UserSegmentation · `AddInput` Level (int), UserSegmentation (string) ·
`AddAmountSetter` Economy (Booster, Coin, Live) · `AddChoice` Economy (Booster Type) · `AddCommand` cả bốn · `AddPanelOpener`
Tracking · `RefreshRows` setter của Level (`SetLevelInput`).

```csharp
using System;
using System.Collections.Generic;
using SRDebugger;
using SRDebugger.Services;
using UnityEngine;

namespace Horcrux.Vendors.SRDebuggerOptions
{
    /// <summary>
    /// One SRDebugger Options category built in code: rows read the system, commands call it, every command refreshes the
    /// rows. The owner adds options, then calls Attach in Awake and Detach in OnDestroy.
    /// </summary>
    public sealed class DebugOptionsCategory
    {
        #region Properties

        private readonly string _category;
        private readonly DynamicOptionContainer _container = new();

        // SRDebugger re-reads a getter only on bind, panel open, tab change or when told: RefreshRows tells every row here.
        private readonly List<OptionDefinition> _valueRows = new();

        // What QA typed into each amount field and has not set yet; null shows the live value.
        private readonly List<TypedAmount> _typedAmounts = new();

        private int _nextOrder;
        private bool _isAttached;

        #endregion

        public DebugOptionsCategory(string category) => _category = category;

        #region API

        /// <summary>Read-only row; refreshed after every command and on RefreshRows.</summary>
        public void AddRow<T>(string name, Func<T> read) =>
            AddValueRow(OptionDefinition.Create(name, read, null, _category, _nextOrder++));

        /// <summary>Editable field QA types into; refreshed like a row, so it can show the live value until edited.</summary>
        public void AddInput<T>(string name, Func<T> read, Action<T> write) =>
            AddValueRow(OptionDefinition.Create(name, read, write, _category, _nextOrder++));

        /// <summary>
        /// Field "{label} Amount" showing the live value until QA types, and button "Set {label}" writing the typed value. Set
        /// before typing does nothing; a value outside min..max is refused with an error log, not clamped, so QA sees the typo.
        /// </summary>
        public void AddAmountSetter(string label, Func<int> read, Action<int> write, int min = 0, Func<int> max = null)
        {
            var typed = new TypedAmount();
            _typedAmounts.Add(typed);
            AddValueRow(OptionDefinition.Create(label + " Amount", () => typed.Value ?? read(), value => typed.Value = value,
                _category, _nextOrder++));
            AddCommand("Set " + label, () =>
            {
                // Nothing typed: the field already shows the live value.
                if (typed.Value is not int value)
                    return;

                int upper = max?.Invoke() ?? int.MaxValue;

                if (value < min || value > upper)
                {
                    Debug.LogError($"[{_category}] {label} must be in {min}..{upper}, got {value}");
                    return;
                }

                typed.Value = null;
                write(value);
            });
        }

        /// <summary>
        /// Enum field stopping only at allowed values. SRDebugger's enum field steps through every value, sentinels (None, Count)
        /// included: values outside allowed are skipped in the direction QA pressed. A new choice drops amounts typed for the old
        /// one and refreshes every row.
        /// </summary>
        public void AddChoice<T>(string name, IReadOnlyList<T> allowed, Func<T> read, Action<T> write) where T : struct, Enum
        {
            var order = (T[])Enum.GetValues(typeof(T));
            AddValueRow(OptionDefinition.Create(name, read, value =>
            {
                write(SkipToAllowed(order, allowed, read(), value));

                for (int i = 0; i < _typedAmounts.Count; i++)
                    _typedAmounts[i].Value = null;

                RefreshRows();
            }, _category, _nextOrder++));
        }

        /// <summary>Button; after it runs, every row and input re-reads its value.</summary>
        public void AddCommand(string name, Action run)
        {
            _container.AddOption(OptionDefinition.FromMethod(name, () =>
            {
                run();
                RefreshRows();
            }, _category, _nextOrder++));
        }

        /// <summary>Button that opens another debug panel (an SDK's own); hides SRDebugger's panel first.</summary>
        public void AddPanelOpener(string name, Action openPanel)
        {
            // A panel sorted under SRDebugger's would show but not take touches.
            _container.AddOption(OptionDefinition.FromMethod(name, () =>
            {
                SRDebug.Instance.HideDebugPanel();
                openPanel();
            }, _category, _nextOrder++));
        }

        /// <summary>Tells every row and input to re-read; call from an input's setter when other rows depend on it.</summary>
        public void RefreshRows()
        {
            for (int i = 0; i < _valueRows.Count; i++)
                _valueRows[i].Property.NotifyValueChanged();
        }

        public void Attach()
        {
            // Null when SRDebugger is disabled in its Settings: no panel, nothing to attach to.
            IDebugService debug = SRDebug.Instance;

            if (debug == null)
                return;

            debug.AddOptionContainer(_container);
            _isAttached = true;
        }

        public void Detach()
        {
            if (!_isAttached)
                return;

            _isAttached = false;

            // On app quit SRDebugger drops its services before the owner's OnDestroy: nothing left to remove from.
            IDebugService debug = SRDebug.Instance;

            if (debug != null)
                debug.RemoveOptionContainer(_container);
        }

        #endregion

        #region Class Methods

        private void AddValueRow(OptionDefinition row)
        {
            _valueRows.Add(row);
            _container.AddOption(row);
        }

        // SRDebugger hands the enum value next to the current one: the next in enum order means QA pressed forward.
        private static T SkipToAllowed<T>(T[] order, IReadOnlyList<T> allowed, T current, T requested) where T : struct, Enum
        {
            EqualityComparer<T> equal = EqualityComparer<T>.Default;
            int count = order.Length;
            int step = equal.Equals(order[(Array.IndexOf(order, current) + 1) % count], requested) ? 1 : count - 1;
            int index = Array.IndexOf(order, requested);

            for (int i = 0; i < count; i++, index = (index + step) % count)
            {
                for (int j = 0; j < allowed.Count; j++)
                {
                    if (equal.Equals(allowed[j], order[index]))
                        return order[index];
                }
            }

            return current;
        }

        #endregion

        private sealed class TypedAmount
        {
            public int? Value;
        }
    }
}
```

Chỗ dễ gõ sai:
- `SRDebug` là class **global** (không namespace), không nằm trong `using SRDebugger`.
- Không đặt namespace `Horcrux.Vendors.SRDebugger`: trong namespace đó, chữ `SRDebugger.` trỏ vào chính nó thay vì namespace của
  vendor, mọi type SRDebugger phải gõ `global::`.
- Hai lambda trong `AddCommand`, `AddPanelOpener` cấp phát một lần lúc dựng category, không ở hot path.
- `_isAttached` chỉ phục vụ `Detach`: owner gọi `Attach` đúng một lần ở `Awake`.
- `typed.Value is not int value` là pattern C# 9: `value` có giá trị sau nhánh `return`. Viết `typed.Value == null` rồi dùng
  `typed.Value.Value` cũng được.
- `SkipToAllowed` lùi bằng `+ count - 1` chứ không `- 1`: `%` của số âm trong C# ra số âm.
- `TypedAmount` là class, không phải struct: lambda của ô và của nút phải trỏ cùng một chỗ giữ số đã gõ.

**Quyết định, phương án bỏ:**
- Dựng container ngay cả khi SRDebugger tắt (bản dự án hiện thoát sớm trước khi dựng): vài `OptionDefinition` lúc boot rẻ hơn hai
  đường code trong mọi class cheat.
- Plain class do owner sở hữu, không phải `MonoBehaviour` base: owner đã là `MonoBehaviour<…>` của InitArgs, base generic đã tiêu khe
  `Init`; kế thừa thêm một base sẽ đụng khe đó.

**Editor setup:** không có. Assembly tự bật khi package SRDebugger có mặt.

**Kiểm:** compile sạch, kể cả `Assembly-CSharp` (chưa ai gọi, nhưng phải thấy namespace mới). Mốc commit:
`feat(srdebugger): DebugOptionsCategory dựng category SRDebugger bằng code`.

---

## Giai đoạn 2 — Dự án dùng bản Horcrux (agent)

**Mục tiêu:** bốn class cheat bỏ khối `Register`/`Unregister` riêng, dựng category qua `DebugOptionsCategory`. Hành vi QA thấy giữ
nguyên: cùng tên category, cùng tên và thứ tự hàng, cùng lệnh.

Mỗi class:
- field `_container`, `OptionDefinition _…Row`, `_stateRows` → một field `DebugOptionsCategory _options`;
- `OnAwake`/`Awake`: `new` → `Add*` theo đúng thứ tự cũ → `Attach()`; `OnDestroy`: `_options?.Detach()`;
- bỏ mọi `…Row.Property.NotifyValueChanged()` sau lệnh; trong setter làm đổi hàng khác (`SetLevelInput`) gọi
  `_options.RefreshRows()`;
- `EconomyCheatDebugOptions`: bỏ `_pending…Amount`, `TryTakePending`, `SelectBooster`, `Wrap`, `EnumOrder`, `LogPrefix`; lệnh Set
  nhận số đích, phần kiểm miền và giữ số đã gõ do class làm:
  ```csharp
  _options = new DebugOptionsCategory(Category);
  _options.AddChoice("Booster Type", CategoryBoosterConfig.AllBoosterType, () => _selectedBooster, value => _selectedBooster = value);
  _options.AddAmountSetter("Booster", () => _boosters.GetBoosterAmount(_selectedBooster), SetBooster);
  _options.AddCommand("Replay Unlock Tut", ReplayUnlockTutorial);
  _options.AddAmountSetter("Coin", _coins.GetCoin, _coins.SetCoin);
  _options.AddAmountSetter("Live", () => _lives.LiveLeft, SetLive, max: () => _maxLive.GetMaxLive);
  _options.Attach();
  ```
  `SetBooster(int target)` cộng phần chênh như cũ, `SetLive(int target)` giữ bước bắt đồng hồ hồi khi rời mức đầy. Log lỗi đổi
  từ `[EconomyCheatDebugOptions]: amount must be in 0..max` sang `[Cheat Economy] Live must be in 0..5, got 9`;
- `TrackingDebugOptions`: `OpenSdkDebugger` → `AddPanelOpener("Open SDK Debugger", Zego.SdkDebugger.Show)`;
- `UserSegmentationDebugOptions`: guard `host == null` giữ trước khi dựng category.

Sau đó:
- tài liệu `docs/Runtime/SRDebuggerOptions/SRDebuggerOptions.md` và `.html`;
- skill `workflow` › Cheat: Sổ tay trỏ sang class này thay cho khuôn `DynamicOptionContainer` viết tay;
- xoá Plan này.

**Kiểm:**
- grep dự án không còn `DynamicOptionContainer`, `AddOptionContainer`;
- `verify.sh` xanh với `Assembly-CSharp.csproj`;
- **developer chơi thử** trên Editor:
  1. Bật Play, mở SRDebugger, vào tab Options.
  2. Bốn category Tracking, Cheat Economy, Cheat Level, User Segmentation có đủ hàng, đúng thứ tự cũ.
  3. Economy: chọn booster, Set Booster, Set Coin, Set Live. Số dư cập nhật ngay, không phải đóng mở panel. Bấm mũi tên Booster
     Type qua lại: không bao giờ dừng ở `None` hay loại không có trong game; gõ số Booster rồi đổi loại thì ô về số dư của loại
     mới. Set Live 99: Console báo lỗi, số mạng giữ nguyên.
  4. Level: đổi ô Level thì hàng "Level → file" đổi theo.
  5. Tracking › Open SDK Debugger: panel SRDebugger ẩn, panel Zego bấm được.
  6. Dừng Play: Console không có lỗi từ `Detach`.

Mốc commit dự án: `refactor(cheat): category SRDebugger dựng qua DebugOptionsCategory của Horcrux`, kèm con trỏ submodule.

---

## File → giai đoạn

| File | GĐ |
|---|---|
| `Vendors/SRDebugger/com.horcrux.srdebugger.asmdef` | 1 |
| `Vendors/SRDebugger/DebugOptionsCategory.cs` | 1 |
| dự án `Assets/_Game/Scripts/Cheat/{Tracking,EconomyCheat,LevelCheat}DebugOptions.cs` | 2 |
| dự án `Assets/_Game/Scripts/Segmentation/UserSegmentationDebugOptions.cs` | 2 |
| `docs/Runtime/SRDebuggerOptions/SRDebuggerOptions.{md,html}` | 2 |
| `Agent/Rules/workflow/RULE.md` (Sổ tay Cheat) | 2 |
