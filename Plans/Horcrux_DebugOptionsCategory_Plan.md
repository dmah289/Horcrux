# Plan — `DebugOptionsCategory`: category SRDebugger dựng bằng code, vào Horcrux `Vendors/SRDebugger`

Bản tạm: category-jam `Assets/_Game/Scripts/Cheat/{Tracking,EconomyCheat,LevelCheat}DebugOptions.cs`, `Assets/_Game/Scripts/Segmentation/UserSegmentationDebugOptions.cs` (`Register`, `Unregister`, `SelectBooster`, `TryTakePending`). Không phụ thuộc Plan nào.
Kiểm (từ gốc dự án): `bash "<horcrux>/Editor/CompileCheck/Tools~/verify.sh" --csproj com.horcrux.srdebugger.csproj,Assembly-CSharp.csproj --tests Horcrux.Vendors.SRDebugger.Tests`.

## Kịch bản

| Tình huống | Hệ làm gì | QA thấy |
|---|---|---|
| class cheat `Awake`: `new` → `Add*` → `Attach` | dựng `OptionDefinition` theo thứ tự gọi, gắn container | category trong tab Options, hàng đúng thứ tự `Add*` |
| SRDebugger tắt trong Settings (`SRDebug.Instance == null`) | container vẫn dựng, `Attach` không làm gì | không category, không lỗi |
| dự án không cài SRDebugger | assembly không compile (thiếu define) | không lỗi |
| QA bấm lệnh | chạy lệnh của dự án, rồi báo lại mọi hàng và ô nhập | số mới ngay, không phải đóng mở panel |
| setter một ô làm đổi hàng khác | caller gọi `RefreshRows` | hàng phụ thuộc đổi theo; ô đang gõ không nhảy số |
| ô số chưa gõ | đọc số dư thật | số dư sống |
| QA gõ ô số | giữ số đã gõ | ô hiện số vừa gõ |
| Set khi chưa gõ | không làm gì | — |
| Set số ngoài `0..max` | `LogError` `[{category}] {label} must be in 0..{max}, got {value}`, giữ số đã gõ, không ghi | Console báo, số giữ nguyên |
| Set số hợp lệ | xoá số đã gõ, ghi, báo lại | ô về số mới |
| bấm mũi tên ô chọn enum | bỏ qua giá trị không được phép theo chiều bấm, ghi, bỏ số đã gõ ở **mọi** ô số, báo lại | không dừng ở giá trị canh (`None`, `Count`) hay giá trị không có; các ô số về số dư thật |
| `allowed` rỗng hay chỉ có giá trị hiện tại | giữ giá trị hiện tại | ô chọn đứng yên |
| nút mở panel SDK | ẩn panel SRDebugger rồi mở panel SDK | panel SDK nhận chạm |
| `OnDestroy` → `Detach` | chưa attach: dừng · `Instance` null (thoát app): dừng · còn: gỡ container | không lỗi khi dừng Play |

## Flow

```
  class cheat dự án (MonoBehaviour)                                 Vendors/SRDebugger/DebugOptionsCategory.cs          SRDebugger
  ├─ Awake: new DebugOptionsCategory("<category>")
  │         AddRow / AddInput / AddAmountSetter / AddChoice / AddCommand / AddPanelOpener ──► OptionDefinition, order tăng dần ──► DynamicOptionContainer
  │         Attach ──► SRDebug.Instance null? ──có──► dừng · không ──► AddOptionContainer ──────────────────────────────────► IDebugService
  ├─ setter ô: RefreshRows (ô này đổi hàng khác)
  └─ OnDestroy: Detach ──► chưa attach / Instance null? dừng · còn: RemoveOptionContainer

  QA bấm lệnh ──► run() ──► RefreshRows: _valueRows[i].Property.NotifyValueChanged ──► PropertyReference (SRF) ──► panel vẽ số mới
  QA gõ ô số ──► TypedAmount.Value ──► Set: null? dừng · ngoài 0..max? LogError · còn: Value = null, write() ──► RefreshRows
  QA bấm mũi tên ô chọn ──► SkipToAllowed(order, allowed, current, requested) ──► write() ──► mọi TypedAmount = null ──► RefreshRows
  QA bấm nút mở panel ──► SRDebug.Instance.HideDebugPanel ──► openPanel()
  test: Horcrux.Vendors.SRDebugger.Tests ──► SkipToAllowed (internal, InternalsVisibleTo)
```

## Thuật toán

`SkipToAllowed`: SRDebugger trao giá trị enum kề giá trị hiện tại; `n` = số giá trị enum.

| Bước | Công thức |
|---|---|
| chiều | `step = +1` khi `requested == order[(idx(current) + 1) % n]` (tiến), không thì `n − 1` (lùi; `%` của số âm trong C# ra âm) |
| tìm | từ `idx(requested)`, tối đa `n` bước `index = (index + step) % n`, trả `order[index]` đầu tiên thuộc `allowed` |
| không có | trả `current` |

Bản mẫu: vét cạn enum 1–6 giá trị × mọi tập `allowed` × mọi `current` × hai chiều = 1284 lượt, 0 lệch; bỏ chiều (luôn tiến) 230 lệch, lùi bằng `- 1` ném `IndexOutOfRangeException`.

---

## GĐ1 — xong thì class cheat nào cũng dựng được category đủ sáu loại hàng mà không chạm `SRDebug.Instance`, `DynamicOptionContainer`, `order`

```
+ com.horcrux.srdebugger: DebugOptionsCategory ──► DynamicOptionContainer, IDebugService, PropertyReference
+ Horcrux.Vendors.SRDebugger.Tests ──► SkipToAllowed
```

| Member | Caller (sau Chuyển dự án) |
|---|---|
| `AddRow` | Tracking, Level, UserSegmentation |
| `AddInput` | Level (int), UserSegmentation (string) |
| `AddAmountSetter` | Economy (Booster, Coin, Live) |
| `AddChoice` | Economy (Booster Type) |
| `AddCommand` | cả bốn |
| `AddPanelOpener` | Tracking |
| `RefreshRows` | setter ô Level |
| `SkipToAllowed` | `AddChoice`, test |

`Vendors/SRDebugger/com.horcrux.srdebugger.asmdef` — file mới:

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

**Editor setup** — chọn một theo cách dự án cài SRDebugger:

1. Package `com.zego.srdebugger` (bản đang dùng, `version` `0.0.0`; biểu thức `0.0.0` nghĩa là ≥ 0.0.0): không làm gì, assembly tự bật.
2. Package tên khác: thêm một entry `versionDefines` cùng `define` `HORCRUX_SRDEBUGGER`.
3. Nhập từ Asset Store (không phải package): Player Settings › Scripting Define Symbols thêm `HORCRUX_SRDEBUGGER`.

`Vendors/SRDebugger/DebugOptionsCategory.cs` — file mới:

```csharp
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
// Resolved from global; inside the class an "SRDebugger." prefix hits this namespace (CS0234). SRDebug itself is global.
using SRDebugger;
using SRDebugger.Services;
using UnityEngine;

// Tests are SkipToAllowed's second caller; projects never need it.
[assembly: InternalsVisibleTo("Horcrux.Vendors.SRDebugger.Tests")]

namespace Horcrux.Vendors.SRDebugger
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
        // Only Detach reads it: the owner calls Attach once in Awake.
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
        /// before typing does nothing; a value outside 0..max is refused with an error log, not clamped, so QA sees the typo.
        /// </summary>
        // max is a delegate: the cap changes during a session.
        public void AddAmountSetter(string label, Func<int> read, Action<int> write, Func<int> max = null)
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

                if (value < 0 || value > upper)
                {
                    Debug.LogError($"[{_category}] {label} must be in 0..{upper}, got {value}");
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

                // Every field, not just dependents: an amount typed for the old choice must not land on the new one.
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
        internal static T SkipToAllowed<T>(T[] order, IReadOnlyList<T> allowed, T current, T requested) where T : struct, Enum
        {
            EqualityComparer<T> equal = EqualityComparer<T>.Default;
            int count = order.Length;
            // Back is + count - 1, not - 1: C# % of a negative number stays negative.
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

        // Class, not struct: the field's and the button's lambdas must share one typed value.
        private sealed class TypedAmount
        {
            public int? Value;
        }
    }
}
```

`Tests/EditMode/Vendors/SRDebugger/` (agent; asmdef `Horcrux.Vendors.SRDebugger.Tests`: references `com.horcrux.srdebugger`, `UnityEngine.TestRunner`, `UnityEditor.TestRunner`, `nunit.framework.dll`, `includePlatforms: Editor`, `defineConstraints` `UNITY_INCLUDE_TESTS`, `HORCRUX_SRDEBUGGER` kèm `versionDefines` như trên):

| Case | current → requested (enum `{ None, A, B, C, D, E }`, allowed `A, B, D`, trừ khi ghi khác; tiến = kế sau current) | Kỳ vọng |
|---|---|---|
| tiến qua giá trị không được phép | B → C | D |
| tiến vòng qua cuối và giá trị canh | D → E | A |
| lùi vòng qua giá trị canh | A → None | D. Đối chứng: lùi bằng `- 1` ném |
| lùi qua giá trị không được phép | D → C | B. Đối chứng: bỏ chiều (luôn tiến) ra D |
| allowed chỉ có current | allowed `{ B }`, B → C | B |
| allowed rỗng | B → C | B |
| enum hai giá trị `{ None, A }`, allowed `{ A }` | A → None | A |

---

## Chuyển dự án (agent)

| Chỗ | Đổi |
|---|---|
| `docs/Runtime/DebugOptionsCategory/DebugOptionsCategory.md`, `.html` | tạo, `Code: Vendors/SRDebugger/`: Flow, bảng sáu loại hàng (gọi gì, QA thấy gì), "Trước khi chạy" (package SRDebugger có mặt hay define `HORCRUX_SRDEBUGGER`; class cheat trong scene service, `Attach` ở `Awake`, `Detach` ở `OnDestroy`), dòng `bỏ` dưới; chữ ký khớp GĐ1 |
| `Agent/Rules/workflow/reference.md` › Cheat | vế cuối dòng "**Lệnh, cửa đọc `private` và suy luận thuộc hệ**, … lớp nối với panel thuộc dự án." → cơ chế dựng category ở Horcrux (`DebugOptionsCategory`), lệnh và từ vựng ở dự án · Sổ tay: vế "`DynamicOptionContainer` + `OptionDefinition.Create`/`FromMethod`, báo bằng `Property.NotifyValueChanged()`" → trỏ `DebugOptionsCategory` khi dự án dùng SRDebugger · `Agent/Tools~/check_agent.py all` sạch |
| bốn class cheat | field `_container`, `OptionDefinition _…Row`, `_stateRows` → một `DebugOptionsCategory _options` · `OnAwake`/`Awake`: `new` → `Add*` đúng thứ tự cũ → `Attach()` · `OnDestroy`: `_options?.Detach()` · bỏ mọi `…Row.Property.NotifyValueChanged()` sau lệnh |
| `LevelCheatDebugOptions.SetLevelInput` | gọi `_options.RefreshRows()` |
| `EconomyCheatDebugOptions` | bỏ `_pending…Amount`, `TryTakePending`, `SelectBooster`, `Wrap`, `EnumOrder`, `LogPrefix`; `Awake` như đoạn dưới · `SetBooster(int target)` cộng phần chênh như cũ · `SetLive(int target)` giữ bước bắt đồng hồ hồi khi rời mức đầy · `_coins.GetCoin` là method group: bắt `_coins` lúc `Awake`, sau `Init` của InitArgs |
| `TrackingDebugOptions` | `OpenSdkDebugger` → `AddPanelOpener("Open SDK Debugger", Zego.SdkDebugger.Show)` |
| `UserSegmentationDebugOptions` | guard `host == null` giữ trước khi dựng category |
| hành vi đổi | đổi Booster Type bỏ cả số đã gõ ở ô Coin, Live (bản tạm chỉ bỏ ô Booster) · log `[EconomyCheatDebugOptions]: amount must be in 0..{max}, got {target}` → `[Cheat Economy] Live must be in 0..{max}, got {value}` |
| grep | dự án không còn `DynamicOptionContainer`, `AddOptionContainer`; `verify.sh` xanh với `Assembly-CSharp.csproj` |
| Plan này | xoá cùng `.meta` |
| chơi thử | Play, SRDebugger › Options: Tracking, Cheat Economy, Cheat Level, User Segmentation đủ hàng, thứ tự cũ · Economy: Set Booster, Coin, Live cập nhật ngay; mũi tên Booster Type không dừng ở `None`; gõ số rồi đổi loại thì ô về số dư thật; Set Live 99 báo `[Cheat Economy] Live must be in 0..<trần>, got 99` · Level: gõ ô Level thì "Level → file" đổi theo, ô không nhảy số · Open SDK Debugger: panel SDK bấm được · dừng Play không lỗi `Detach`; hỏng: thiếu category (chưa `Attach`, assembly chưa bật) |

`EconomyCheatDebugOptions` › `Awake`:

```csharp
  _options = new DebugOptionsCategory(Category);
  _options.AddChoice("Booster Type", CategoryBoosterConfig.AllBoosterType, () => _selectedBooster, value => _selectedBooster = value);
  _options.AddAmountSetter("Booster", () => _boosters.GetBoosterAmount(_selectedBooster), SetBooster);
  _options.AddCommand("Replay Unlock Tut", ReplayUnlockTutorial);
  _options.AddAmountSetter("Coin", _coins.GetCoin, _coins.SetCoin);
  _options.AddAmountSetter("Live", () => _lives.LiveLeft, SetLive, max: () => _maxLive.GetMaxLive);
  _options.Attach();
```

## Không làm · Mở rộng sau

| | Phương án | Vì sao |
|---|---|---|
| bỏ | asmdef chỉ reference `StompyRobot.SRDebugger` | `PropertyReference` (cho `NotifyValueChanged`) nằm ở `StompyRobot.SRF` |
| bỏ | `versionDefines` và `defineConstraints` ở hai asmdef khác nhau | `versionDefines` chỉ đặt define trong chính assembly khai nó |
| bỏ | `autoReferenced: false` | `Assembly-CSharp` phải thấy class mà không sửa asmdef nào của dự án |
| bỏ | thoát sớm trước khi dựng khi SRDebugger tắt (bản tạm) | vài `OptionDefinition` lúc boot rẻ hơn hai đường code trong mọi class cheat |
| bỏ | `MonoBehaviour` base thay plain class do owner sở hữu | owner đã là `MonoBehaviour<…>` của InitArgs, base generic đã tiêu khe `Init` |
| bỏ | cờ "mở panel" trên `AddCommand` thay `AddPanelOpener` | luật "ẩn SRDebugger trước" do class giữ, caller không cần biết |
| bỏ | chỉ báo lại hàng chỉ đọc sau lệnh | ô số hiện số dư thật tới khi QA gõ; Set xong phải về số mới |
| bỏ | kẹp số ngoài `0..max` | kẹp im lặng làm QA tưởng đã đặt đúng số vừa gõ |
| bỏ | đổi lựa chọn chỉ bỏ số ở ô số phụ thuộc | số gõ cho lựa chọn cũ giữ lại là đặt nhầm sang lựa chọn mới; category có hai ô chọn độc lập thì tách category |
| bỏ | ô số `AddInput` + nút `AddCommand` caller tự nối | số đang gõ dở là trạng thái của cặp ô; caller giữ thì mỗi cặp thêm một `int?` và một hàm kiểm miền |
| bỏ | `Attach` trả `bool` | không caller nào rẽ nhánh theo nó |
| bỏ | `Add*` trả `OptionDefinition` | caller giữ tham chiếu chỉ để báo lại hàng, giờ class tự báo |
| bỏ | cờ "không refresh" trên lệnh | báo vài hàng mỗi lần bấm rẻ hơn một ô phải nhớ |
| bỏ | gỡ từng hàng sau `Attach` (`RemoveOption`) | chưa caller nào cần |
| bỏ | hỗ trợ `SROptions` partial | cơ chế khác hẳn, không phải người dùng của class này |
| sau | tham số `min` cho `AddAmountSetter` (mặc định 0) | khi một ô số nhận được số âm |
