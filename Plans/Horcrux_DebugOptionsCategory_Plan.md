# Plan — `DebugOptionsCategory`: category SRDebugger dựng bằng code, vào Horcrux `Vendors/SRDebugger`

Ngày 2026-11-23. Nguồn: category-jam `Assets/_Game/Scripts/Cheat/{Tracking,EconomyCheat,LevelCheat}DebugOptions.cs`,
`Assets/_Game/Scripts/Segmentation/UserSegmentationDebugOptions.cs` (`Register`, `Unregister`, `SelectBooster`, `TryTakePending`), đang
chạy ở dự án. Bốn class chép cùng một khối: kiểm `SRDebug.Instance`, dựng `DynamicOptionContainer`, tự đếm `order`, gỡ
container lúc huỷ, giữ tay danh sách hàng phải báo lại sau lệnh. Lõi gói khối đó; từ vựng (category, hàng, lệnh) và lệnh thật ở dự án.

**Phân công:** GĐ1 developer gõ, agent viết test theo bảng case rồi compile · GĐ2 (tài liệu, sửa luật Cheat) và GĐ3 (dự án chuyển
sang bản Horcrux, xoá Plan) agent làm.
**Chạy được, thấy được:** GĐ1 compile sạch (kể cả `Assembly-CSharp`) + bảng case `SkipToAllowed` xanh trong
`Tests/EditMode/Vendors/SRDebugger/`; thấy bốn category trong panel ở GĐ3.
**Lệnh kiểm** (chạy từ gốc dự án): `bash "<horcrux>/Editor/CompileCheck/Tools~/verify.sh" --csproj
com.horcrux.srdebugger.csproj,Assembly-CSharp.csproj --tests Horcrux.Vendors.SRDebugger.Tests`.
**Commit:** GĐ1, GĐ2 trong `Assets/Horcrux`; GĐ3 ở dự án kèm con trỏ submodule.
**Phụ thuộc:** không.
**Đã kiểm trước khi ghi:** file GĐ1 compile bằng csc (C# 9, `-warn:4`, 0 lỗi 0 cảnh báo) trên `StompyRobot.SRDebugger.dll`,
`StompyRobot.SRF.dll` của dự án và `UnityEngine.CoreModule` 6000.0.73f1, kèm một caller gọi đủ các `Add*` theo đúng lời gọi GĐ3 của
Economy, Level, UserSegmentation. Namespace `Horcrux.Vendors.SRDebugger` compile; đối chứng: gõ `SRDebugger.DynamicOptionContainer`
trong namespace đó báo CS0234. Assembly test chỉ reference `com.horcrux.srdebugger` compile được lời gọi `SkipToAllowed`.
`SkipToAllowed` chạy vét cạn so với bản tham chiếu (giá trị được phép kế tiếp theo chiều bấm, tính vòng từ sau giá trị hiện tại):
enum 1–6 giá trị × mọi tập allowed × mọi giá trị hiện tại × hai chiều = 1284 lượt, 0 lệch. Đối chứng: bỏ chiều (luôn tiến) cho 230
lệch; lùi bằng `- 1` thay `+ count - 1` ném `IndexOutOfRangeException`. `§0`: không, không có toán.

---

## Ngữ cảnh đã chốt

- **Người dùng:** class cheat của dự án (một `MonoBehaviour` mỗi hệ, đặt trong scene service) dựng một category trong tab Options của
  SRDebugger: hàng chỉ đọc, ô nhập (int, enum, string), nút lệnh, nút mở panel debug của SDK, ô số kèm nút Set, ô chọn enum bỏ qua
  giá trị canh.
- **Mục tiêu:** dựng một category mà không chạm `SRDebug.Instance`, `DynamicOptionContainer`, `order`; mỗi lệnh chạy xong mọi hàng và
  ô nhập đọc lại.
- **Ranh giới:** Horcrux giữ cơ chế dựng category (thứ tự, báo lại hàng, gắn và gỡ container, ô số, ô chọn). Lệnh, cửa đọc `private`,
  suy luận và từ vựng ở hệ và dự án; dự án dùng panel khác thì bỏ assembly này, hệ không đổi.
- **Chỗ đặt, tên:** `Vendors/SRDebugger/`, assembly riêng `com.horcrux.srdebugger`, namespace `Horcrux.Vendors.SRDebugger`. Bật bằng
  `versionDefines`: dự án không cài SRDebugger thì assembly không compile, không lỗi. Tài liệu `docs/Runtime/DebugOptionsCategory/`.
- **Hành vi:**
  - SRDebugger chỉ đọc lại getter khi bind, mở panel, đổi tab hoặc khi được báo; sót một hàng là hàng đứng số cũ tới lần mở panel sau.
    Class báo **mọi** hàng và ô nhập sau mỗi lệnh.
  - Thứ tự hàng là thứ tự gọi `Add*`. SRDebugger tắt trong Settings (`SRDebug.Instance == null`) thì `Attach` không làm gì.
- **Cố ý không làm:**
  - `Attach` trả `bool`: không caller nào rẽ nhánh theo nó.
  - `Add*` trả `OptionDefinition`: caller giữ tham chiếu chỉ để báo lại hàng, giờ class tự báo.
  - Cờ "không refresh" trên lệnh: báo vài hàng mỗi lần bấm rẻ hơn một ô phải nhớ.
  - Gỡ từng hàng sau khi `Attach`: chưa caller nào cần.
  - Hỗ trợ `SROptions` partial: cơ chế khác hẳn, không phải người dùng của class này.
- **Quyết định trái trực giác:**
  - `AddPanelOpener` tách khỏi `AddCommand`, không phải một cờ: panel của SDK xếp dưới panel SRDebugger thì hiện ra mà không nhận
    chạm. Luật "ẩn SRDebugger trước" do class giữ, caller không cần biết.
  - Ô nhập cũng được báo lại sau lệnh: ô số hiện số dư thật cho tới khi QA gõ, Set xong phải về số mới.
  - `AddAmountSetter` từ chối số ngoài `0..max` kèm log, không kẹp: kẹp im lặng làm QA tưởng đã đặt đúng số vừa gõ. `max` là delegate
    vì trần đổi trong phiên.
  - `AddChoice` đổi lựa chọn thì bỏ số đã gõ ở **mọi** ô số, không chỉ ô phụ thuộc: số gõ cho lựa chọn cũ, giữ lại là đặt nhầm sang
    lựa chọn mới. Category có hai ô chọn độc lập thì tách category.
  - Ô số và nút Set là **một** lệnh `AddAmountSetter`, không phải `AddInput` + `AddCommand` caller tự nối: số đang gõ dở là trạng thái
    của cặp ô đó; caller giữ thì mỗi cặp lại một field `int?` và một hàm kiểm miền.
- **Mở rộng sau:** tham số `min` cho `AddAmountSetter` (mặc định 0) khi một ô số nhận được số âm.

## Đã khảo sát

| Nguồn | Lấy gì | Không lấy, vì sao |
|---|---|---|
| category-jam 4 class `*DebugOptions` (bản đang chạy) | khối `Register`/`Unregister`, comment hai ca `null`, cách đếm `order`; `TryTakePending`, `SelectBooster` + `Wrap` của Economy không mang tên booster hay coin, nên thành `AddAmountSetter`, `AddChoice` | phần lệnh: từ vựng dự án |
| SRDebugger `OptionDefinition` | `Create<T>(name, getter, setter, category, sortPriority)`, `FromMethod(name, Action, category, sortPriority)` | constructor `MethodReference`: `FromMethod` đủ |
| SRDebugger `DynamicOptionContainer` | `AddOption`; thêm sau `AddOptionContainer` vẫn hiện (bắn `OptionAdded`) | `RemoveOption`: chưa caller |
| SRF `PropertyReference.NotifyValueChanged()` | cách báo hàng đọc lại | — |
| `IDebugService.HideDebugPanel()` | ẩn panel trước khi mở panel khác | — |
| Horcrux `Vendors/Adjust/com.horcrux.adjust.asmdef` | khuôn `versionDefines` + `defineConstraints` | — |
| `Agent/Rules/workflow/reference.md` › Cheat (Sổ tay) | khuôn `DynamicOptionContainer` + `OptionDefinition`; báo hàng sau lệnh; từ chối không kẹp | plain class nhận hệ qua constructor: bốn class dự án đều là `MonoBehaviour` |

## Luồng

```
Awake của class cheat: new DebugOptionsCategory("<category>")
        ▼
AddRow / AddInput / AddAmountSetter / AddChoice / AddCommand / AddPanelOpener ──► OptionDefinition vào container, order tăng dần
        ▼
Attach ──► SRDebug.Instance null? ──có──► dừng (SRDebugger tắt)
        │ không
        ▼ AddOptionContainer: category hiện trong tab Options
QA bấm lệnh ──► run() của dự án ──► RefreshRows: mọi hàng, ô nhập NotifyValueChanged ──► panel vẽ số mới
QA gõ ô số ──► giữ số đã gõ, ô hiện số đó ──► bấm Set: chưa gõ? dừng · ngoài 0..max? log lỗi, giữ số · còn: write() ──► RefreshRows
QA bấm mũi tên ô chọn ──► SkipToAllowed theo chiều bấm ──► write() ──► bỏ mọi số đã gõ ──► RefreshRows
QA bấm nút mở panel SDK ──► HideDebugPanel ──► openPanel() của dự án
        ▼
OnDestroy: Detach ──► chưa attach? dừng · Instance null (thoát app)? dừng · còn: RemoveOptionContainer
```

## Bản đồ hệ cuối

```
  class cheat dự án (MonoBehaviour)                                SRDebugger
  ├─ Awake:     new · Add* · Attach ─────────► DebugOptionsCategory ──► DynamicOptionContainer ──► IDebugService
  ├─ setter:    RefreshRows (ô này đổi hàng khác)        │ _valueRows ── NotifyValueChanged ──► PropertyReference
  ├─ lệnh:      gọi hệ                                   │ AddPanelOpener ── HideDebugPanel
  └─ OnDestroy: Detach ──────────────────────►           ▼
  test: Horcrux.Vendors.SRDebugger.Tests ──► SkipToAllowed (internal, InternalsVisibleTo)
```

---

## GĐ1 — Assembly, `DebugOptionsCategory`, test `SkipToAllowed`

**Mục tiêu:** dự án dựng được một category đủ sáu loại hàng; thuật toán ô chọn có bảng case xanh. Xong thì bốn class cheat chuyển
được trọn ở GĐ3.

**Vì sao bây giờ:** class một màn hình, mỗi member đã có caller thật trong bốn class dự án (bảng dưới); chia nhỏ hơn thì GĐ đầu không
có category nào chuyển được trọn.

**Sơ đồ sau GĐ** (mới: cả ba khối):

```
[mới] com.horcrux.srdebugger: DebugOptionsCategory ──► DynamicOptionContainer, IDebugService, PropertyReference
[mới] Horcrux.Vendors.SRDebugger.Tests ──► SkipToAllowed
```

**Consumes:** `OptionDefinition`, `DynamicOptionContainer`, `SRDebug.Instance` (SRDebugger), `PropertyReference` (SRF).
**Produces:** `DebugOptionsCategory` với `AddRow`, `AddInput`, `AddAmountSetter`, `AddChoice`, `AddCommand`, `AddPanelOpener`,
`RefreshRows`, `Attach`, `Detach`.

Ai gọi từng member (sau GĐ3): `AddRow` Tracking, Level, UserSegmentation · `AddInput` Level (int), UserSegmentation (string) ·
`AddAmountSetter` Economy (Booster, Coin, Live) · `AddChoice` Economy (Booster Type) · `AddCommand` cả bốn · `AddPanelOpener` Tracking ·
`RefreshRows` setter ô Level · `SkipToAllowed` `AddChoice` và test.

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
- `versionDefines` chỉ đặt define **trong chính assembly khai nó**, nên cặp `versionDefines` + `defineConstraints` nằm cùng file. Gói
  SRDebugger đang dùng mang tên `com.zego.srdebugger`, `version` `0.0.0`, nên biểu thức `0.0.0` (nghĩa là ≥ 0.0.0). Dự án cài
  SRDebugger dưới tên package khác thì thêm một entry cùng `define`; nhập từ Asset Store (không phải package) thì thêm
  `HORCRUX_SRDEBUGGER` vào Scripting Define Symbols.
- `autoReferenced: true`: `Assembly-CSharp` thấy class mà không phải sửa asmdef nào của dự án.

### `Vendors/SRDebugger/DebugOptionsCategory.cs` — file mới

```csharp
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using SRDebugger;
using SRDebugger.Services;
using UnityEngine;

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
- Trong namespace `Horcrux.Vendors.SRDebugger`, tên có tiền tố `SRDebugger.` trỏ vào chính namespace này: không gõ
  `SRDebugger.DynamicOptionContainer` trong thân class (CS0234). Các `using SRDebugger;` ở đầu file phân giải từ global nên vẫn đúng.
- Hai lambda trong `AddCommand`, `AddPanelOpener` cấp phát một lần lúc dựng category, không ở hot path.
- `_isAttached` chỉ phục vụ `Detach`: owner gọi `Attach` đúng một lần ở `Awake`.
- `typed.Value is not int value` là pattern C# 9: `value` có giá trị sau nhánh `return`.
- `SkipToAllowed` lùi bằng `+ count - 1` chứ không `- 1`: `%` của số âm trong C# ra số âm (đối chứng ở đầu Plan ném
  `IndexOutOfRangeException`).
- `TypedAmount` là class, không phải struct: lambda của ô và của nút phải trỏ cùng một chỗ giữ số đã gõ.

**Quyết định, phương án bỏ:**
- Dựng container ngay cả khi SRDebugger tắt (bản dự án thoát sớm trước khi dựng): vài `OptionDefinition` lúc boot rẻ hơn hai đường
  code trong mọi class cheat.
- Plain class do owner sở hữu, không phải `MonoBehaviour` base: owner đã là `MonoBehaviour<…>` của InitArgs, base generic đã tiêu khe
  `Init`; kế thừa thêm một base sẽ đụng khe đó.
- `SkipToAllowed` để `internal` + `InternalsVisibleTo`: test là caller thứ hai; dự án không cần hàm này.

**Editor setup:** không. Assembly tự bật khi package SRDebugger có mặt.

### Assembly test — agent viết

Thư mục `Tests/EditMode/Vendors/SRDebugger/`, asmdef `Horcrux.Vendors.SRDebugger.Tests`:
- references: `com.horcrux.srdebugger`, `UnityEngine.TestRunner`, `UnityEditor.TestRunner` (không cần StompyRobot: chữ ký
  `SkipToAllowed` chỉ có kiểu BCL);
- `nunit.framework.dll`, `includePlatforms: Editor`;
- `defineConstraints`: `UNITY_INCLUDE_TESTS`, `HORCRUX_SRDEBUGGER`, kèm `versionDefines` như trên.

Enum test `{ None, A, B, C, D, E }` (giá trị canh ở đầu), allowed `A, B, D`. "Tiến" là `requested` = giá trị kế sau `current` theo thứ
tự enum.

| Case | current → requested | Kỳ vọng |
|---|---|---|
| tiến qua giá trị không được phép | B → C | D |
| tiến vòng qua cuối và giá trị canh | D → E | A |
| lùi vòng qua giá trị canh | A → None | D |
| lùi qua giá trị không được phép | D → C | B |
| allowed chỉ có current | allowed `{ B }`, B → C | B |
| allowed rỗng | B → C | B |
| enum hai giá trị `{ None, A }`, allowed `{ A }` | A → None | A |

Case đỏ được: bỏ chiều (luôn tiến) thì case "lùi qua giá trị không được phép" ra D; lùi bằng `- 1` thì case "lùi vòng" ném.

**Kiểm:** compile sạch, kể cả `Assembly-CSharp` (chưa ai gọi, nhưng phải thấy namespace mới); bảng case xanh. Mốc commit:
`feat(srdebugger): DebugOptionsCategory dựng category SRDebugger bằng code`.

---

## GĐ2 — Tài liệu và luật Cheat (agent)

**Mục tiêu:** dự án sau dựng category mà không mở code; luật Cheat trỏ cơ chế này.

**Vì sao bây giờ:** API đã chốt ở GĐ1; GĐ3 xoá Plan.

**Sơ đồ sau GĐ:** không đổi code.

- `docs/Runtime/DebugOptionsCategory/DebugOptionsCategory.md` và `.html`, dòng `Code: Vendors/SRDebugger/`. Gồm: luồng, bảng sáu loại
  hàng (gọi gì, QA thấy gì), mục "Trước khi chạy" (package SRDebugger có mặt hay define `HORCRUX_SRDEBUGGER`; class cheat đặt trong
  scene service, `Attach` ở `Awake`, `Detach` ở `OnDestroy`), các quyết định trái trực giác.
- `Agent/Rules/workflow/reference.md` › Cheat:
  - dòng "**Lệnh, cửa đọc `private` và suy luận thuộc hệ**, … lớp nối với panel thuộc dự án." → vế cuối thành: cơ chế dựng category ở
    Horcrux (`DebugOptionsCategory`), lệnh và từ vựng ở dự án;
  - Sổ tay: vế "hàng là delegate đọc hệ thì `DynamicOptionContainer` + `OptionDefinition.Create`/`FromMethod`, báo bằng
    `Property.NotifyValueChanged()`" → trỏ sang `DebugOptionsCategory` của Horcrux khi dự án dùng SRDebugger.

**Editor setup:** không.

**Kiểm:** chữ ký trong `.md` khớp GĐ1; `Agent/Tools~/check_agent.py all` sạch. Mốc commit:
`docs(srdebugger): tài liệu DebugOptionsCategory, luật Cheat trỏ cơ chế Horcrux`.

---

## GĐ3 — Dự án dùng bản Horcrux (agent)

**Mục tiêu:** bốn class cheat bỏ khối `Register`/`Unregister` riêng, dựng category qua `DebugOptionsCategory`; xoá Plan này. Cùng tên
category, cùng tên và thứ tự hàng, cùng lệnh. Khác trước hai chỗ: đổi Booster Type bỏ cả số đã gõ ở ô Coin, Live (bản cũ chỉ bỏ ô
Booster); câu log khi số ngoài miền.

**Sơ đồ sau GĐ:**

```
4 class cheat ──► [mới] DebugOptionsCategory (Horcrux) ──► SRDebugger
[xoá] _container, …Row, _stateRows, TryTakePending, SelectBooster, Wrap, EnumOrder ở dự án
```

Mỗi class:
- field `_container`, `OptionDefinition _…Row`, `_stateRows` → một field `DebugOptionsCategory _options`;
- `OnAwake`/`Awake`: `new` → `Add*` theo đúng thứ tự cũ → `Attach()`; `OnDestroy`: `_options?.Detach()`;
- bỏ mọi `…Row.Property.NotifyValueChanged()` sau lệnh; trong setter làm đổi hàng khác (`LevelCheatDebugOptions.SetLevelInput`) gọi
  `_options.RefreshRows()`;
- `EconomyCheatDebugOptions`: bỏ `_pending…Amount`, `TryTakePending`, `SelectBooster`, `Wrap`, `EnumOrder`, `LogPrefix`; lệnh Set nhận
  số đích, phần kiểm miền và giữ số đã gõ do class làm:
  ```csharp
  _options = new DebugOptionsCategory(Category);
  _options.AddChoice("Booster Type", CategoryBoosterConfig.AllBoosterType, () => _selectedBooster, value => _selectedBooster = value);
  _options.AddAmountSetter("Booster", () => _boosters.GetBoosterAmount(_selectedBooster), SetBooster);
  _options.AddCommand("Replay Unlock Tut", ReplayUnlockTutorial);
  _options.AddAmountSetter("Coin", _coins.GetCoin, _coins.SetCoin);
  _options.AddAmountSetter("Live", () => _lives.LiveLeft, SetLive, max: () => _maxLive.GetMaxLive);
  _options.Attach();
  ```
  `SetBooster(int target)` cộng phần chênh như cũ, `SetLive(int target)` giữ bước bắt đồng hồ hồi khi rời mức đầy. Log lỗi đổi từ
  `[EconomyCheatDebugOptions]: amount must be in 0..{max}, got {target}` sang `[Cheat Economy] Live must be in 0..{max}, got {value}`.
  `_coins.GetCoin` là method group: bắt `_coins` lúc `Awake`, sau `Init` của InitArgs;
- `TrackingDebugOptions`: `OpenSdkDebugger` → `AddPanelOpener("Open SDK Debugger", Zego.SdkDebugger.Show)`;
- `UserSegmentationDebugOptions`: guard `host == null` giữ trước khi dựng category.

Sau đó xoá `Plans/Horcrux_DebugOptionsCategory_Plan.md` và `.meta`.

**Editor setup:** không.

**Kiểm:** grep dự án không còn `DynamicOptionContainer`, `AddOptionContainer`; `verify.sh` xanh với `Assembly-CSharp.csproj`.

**Kịch bản chơi thử** (developer, Editor):
1. Bật Play, mở SRDebugger, vào tab Options: bốn category Tracking, Cheat Economy, Cheat Level, User Segmentation có đủ hàng, đúng
   thứ tự cũ. Hỏng: thiếu category (chưa `Attach`, hay assembly chưa bật).
2. Economy: chọn booster, Set Booster, Set Coin, Set Live. Số dư cập nhật ngay, không phải đóng mở panel. Bấm mũi tên Booster Type
   qua lại: không dừng ở `None` hay loại không có trong game. Gõ số Booster (và số Coin) rồi đổi loại: hai ô về số dư thật. Set Live
   99: Console báo `[Cheat Economy] Live must be in 0..<trần>, got 99`, số mạng giữ nguyên.
3. Level: gõ số vào ô Level thì hàng "Level → file" đổi theo; ô Level không nhảy số trong lúc gõ (ô được báo lại cùng các hàng khác).
4. Tracking › Open SDK Debugger: panel SRDebugger ẩn, panel SDK bấm được.
5. Dừng Play: Console không có lỗi từ `Detach`.

Mốc commit dự án: `refactor(cheat): category SRDebugger dựng qua DebugOptionsCategory của Horcrux`, kèm con trỏ submodule.

---

## File → GĐ

| File | GĐ |
|---|---|
| `Vendors/SRDebugger/com.horcrux.srdebugger.asmdef` | 1 |
| `Vendors/SRDebugger/DebugOptionsCategory.cs` | 1 |
| `Tests/EditMode/Vendors/SRDebugger/` (asmdef, test) | 1 (agent) |
| `docs/Runtime/DebugOptionsCategory/DebugOptionsCategory.{md,html}` | 2 |
| `Agent/Rules/workflow/reference.md` (mục Cheat) | 2 |
| dự án `Assets/_Game/Scripts/Cheat/{Tracking,EconomyCheat,LevelCheat}DebugOptions.cs` | 3 |
| dự án `Assets/_Game/Scripts/Segmentation/UserSegmentationDebugOptions.cs` | 3 |
| xoá `Plans/Horcrux_DebugOptionsCategory_Plan.md` | 3 |
