# Plan — `Tutorial`: một lớp tối, tay chỉ, hộp thoại, tiến độ cho mọi lượt dạy; gộp `CanvasSpotlight`

Ngày 2026-11-19. Nguồn: category-jam `Assets/_CategoryJam/Scripts/Tutorial/Core/` (bản tạm; namespace đã là
`Horcrux.Runtime.Abstractions.Tutorial`, `Horcrux.Runtime.Implementations.Tutorial`, nằm nhờ assembly `CategoryJam.Gameplay`), tài liệu
dự án `Scripts/Tutorial/Tutorial.md`; đang chạy trong dự án với hai kịch bản: Level 1 (khoét lỗ trên tile, box) và mở khoá booster (nâng
nút HUD lên trên lớp tối). Service chạy một kịch bản mỗi lần, dọn lớp phủ ở mọi đường ra, lưu id kịch bản đã xong; đích nổi lên bằng
khoét lỗ (world và UI) hoặc nâng canvas lồng (UI Overlay); hộp thoại đặt cạnh đích bằng một hàm thuần.

**Phân công:** GĐ1–3 developer làm trong Horcrux (dời file, gõ phần mới, xoá `CanvasSpotlight`), agent viết test theo bảng case rồi
compile · GĐ4 (tài liệu, dự án trỏ về bản Horcrux, xoá Plan) agent làm.
**Chạy được, thấy được:** GĐ1 compile sạch + bảng case xanh trong `Tests/EditMode/TutorialLiftTests.cs`,
`Tests/EditMode/TutorialGateTests.cs`, chơi lượt dạy Level 1 như trước · GĐ2 bảng case xanh trong
`Tests/EditMode/CalloutPlacementTests.cs`, hộp thoại đứng như trước · thấy toàn bộ trong game ở GĐ4.
**Lệnh kiểm** (chạy từ gốc dự án): `bash "Assets/Horcrux/Editor/CompileCheck/Tools~/verify.sh" --csproj
com.horcrux.runtime.csproj,CategoryJam.Gameplay.csproj,Assembly-CSharp.csproj --tests Horcrux.EditModeTests,CategoryJam.EditModeTests`.
**Commit:** GĐ1 trong `Assets/Horcrux`, kèm commit dự án xoá bản tạm và con trỏ submodule · GĐ2 trong `Assets/Horcrux` · GĐ3 ở dự án
consumer rồi `Assets/Horcrux` · GĐ4 trong `Assets/Horcrux` (tài liệu) và dự án (`Tutorial.md`, con trỏ submodule).
**Phụ thuộc:** không.
**Đã kiểm trước khi ghi:** csc (Roslyn của Rider, `-langversion:9`, `netstandard.dll` 2.1 và module `UnityEngine` của Unity 6000.0.73f1,
DLL trong `Library/ScriptAssemblies`: `com.horcrux.runtime`, `InitArgs`, `UniTask`, `PrimeTween.Runtime`, `LightSide.UniText`) compile
sạch: GĐ1 — cả lõi bản tạm với `TutorialService.ProgressEntry` mới và partial `BasePersistenceDataCollection.Tutorial.cs` dựng trên
nguồn `BasePersistenceDataCollection*.cs` của Horcrux; GĐ2 — `CalloutPlacement.cs` và `TutorialBubble.cs` bản tạm đã áp GĐ2. Bản mẫu
C# chạy trên mono của Unity với `UnityEngine.CoreModule`: 9 ca của bảng GĐ2, 24 giá trị khớp. `§0`: không — phép đặt hộp là kẹp đoạn
một chiều, ghi ở GĐ2.

## Ngữ cảnh đã chốt

- **Người dùng:** mọi lượt dạy của game — đích world (vật qua camera gameplay) lẫn đích UI (nút HUD, widget Home) — chung một lớp tối,
  một tay chỉ, một hộp thoại, một chỗ lưu tiến độ · khung nổi cạnh một đích (hộp thoại, tooltip, gợi ý cạnh nút) cần chỗ đặt trong
  safe area (`CalloutPlacement`).
- **Mục tiêu:** một hệ duy nhất trong Horcrux; dự án chỉ viết kịch bản (`ATutorialScript`) và đặt neo. `CanvasSpotlight` thôi là hệ
  thứ hai. Hai cách làm nổi đích, cùng lớp tối: *khoét lỗ* (`Open`) — shader cắt cửa sổ, đích thấy qua lỗ, dùng được cho world; *nâng
  canvas* (`Lift`) — canvas lồng của đích vẽ đè lên lớp tối như chính nó, không viền lỗ, chỉ cho UI Overlay. `Lift` là cơ chế của
  `CanvasSpotlight` đưa vào service chung.
- **Ranh giới:** API `ITutorialService` và các interface con giữ đúng như bản tạm; field `tutorialProgress` giữ tên (key YAML của asset
  collection); API công khai mới chỉ có `CalloutPlacement`, `CalloutLayout`, `CalloutSide`; hộp thoại đổi hành vi ở hai ca cạnh màn,
  ghi ở GĐ2.
- **Chỗ đặt, tên:** `Runtime/Abstractions/Composites/Tutorial/`, `Runtime/Implementations/Composites/Tutorial/` — dựng trên Persistence
  của Horcrux, không mang domain. Shader sang `Shader/`. `CalloutPlacement` ở `Runtime/Utilities/Layout/`, namespace
  `Horcrux.Runtime.Utilities.Layout`. Tên phần mới (`CalloutPlacement`, `CalloutLayout`, `CalloutSide`, `arrowCornerInset`) chốt khi gõ.
- **Cố ý không làm:** hàng đợi nhiều kịch bản · tracking · bật tắt từ remote config — giữ như tài liệu dự án đã chốt · tay chỉ theo
  hướng (`HighlightConfig.HandDirection` của `CanvasSpotlight`): tay của hệ chung bám neo, chưa có consumer cần hướng khác · đổi namespace
  `…Implementations.Time` (lý do GĐ1).
- **Quyết định trái trực giác:** canvas lồng của đích **để raycaster bật lúc nghỉ** (khác hợp đồng `CanvasSpotlight` §3.2): graphic
  thuộc canvas lồng gần nhất đang bật, raycaster của canvas cha không quét nó — tắt raycaster lúc nghỉ là nút không bấm được ngoài lượt
  dạy (bằng chứng ở GĐ1) · `TutorialGate` **nhiều chủ**: `Restrict` trả `Restriction` của chủ gọi (`Change` đổi luật tại chỗ, `Drop` gỡ
  đúng luật đó), item qua khi mọi luật đang giữ đều nhận. Cửa nhặt có hai chủ thật — kịch bản dạy nhặt và lớp khoá của bàn dựng sẵn cho
  một lượt dạy; một khe chung thì chủ ghi sau đè luật chủ trước, và nhả của bên này gỡ luôn luật bên kia.
- **Mở rộng sau:** tay chỉ theo hướng (ngón trỏ chéo, tay đứng một phía) khi có widget mà tay đè lên nhãn · `DimAsync` nhận thời lượng
  khi một lễ cần tối dần theo nhịp riêng · phần không dính Spine của `SpineHandTapBeat` (dự án) — mốc trong anim lặp đã đi qua chưa giữa
  hai frame, tính cả lúc quấn về đầu (`Crossed(prev, now, mark)`) — lên `Runtime/Utilities` khi có người gọi thứ hai (anim Animator theo
  `normalizedTime`, tay chỉ của dự án khác).

## Đã khảo sát

| Nguồn | Lấy gì | Không lấy, vì sao |
|---|---|---|
| bản tạm category-jam `Scripts/Tutorial/Core/` | toàn bộ: `TutorialService`, `TutorialSpotlight` (lỗ + `Lift`), `TutorialHand`, `TutorialBubble`, `TutorialGate`, `TutorialAnchor`, `TutorialProgress`, hai shader | entry lưu khai ở collection của dự án (GĐ1 dời); phép đặt hộp thoại lẫn trong `TutorialBubble.PlaceNextToTarget` (GĐ2 tách) |
| `Runtime/…/Foundations/Spotlight/CanvasSpotlight.cs`, `docs/Runtime/CanvasSpotlight/` | cơ chế nâng: bật raycaster rồi mới `overrideSorting`, nhớ ba giá trị để trả; nhiều canvas một lượt | lớp tối riêng ở sorting cố định 3103 (hai lớp tối chồng nhau khi cả hai hệ chạy); tay sprite `CharmPointAndBob` không bám đích; không hộp thoại, không lưu tiến độ |
| `Abstractions/Composites/UserSegmentation/BasePersistenceDataCollection.UserSegmentation.cs` | khuôn partial khai entry lưu của hệ Composite | — |
| `Horcrux_ScreenSize_Plan.md` (`ScreenWidth`) | — | hộp thoại đổi canvas → pixel bằng `lossyScale.x` của root; đúng vì canvas hộp thoại bắt buộc Overlay (Editor setup GĐ2) |

## Luồng

```
Dự án: kịch bản (ATutorialScript) ──► ITutorialService.PlayAsync(script, ct)
                                         ├─ bật canvas lớp phủ, lấy Camera.main
                                         ├─ script.RunAsync: Spotlight.DimAsync · Open (lỗ) · Lift (nâng) · Hand
                                         │                   · Bubble.Show ──mỗi frame──► CalloutPlacement.Place     GĐ2
                                         │                   · Gate.Restrict (cửa input của game) → Restriction.Drop
                                         ├─ xong: ghi Id vào TutorialProgress (Id trống: không ghi)
                                         └─ finally: lỗ, canvas đã nâng, lớp tối, tay, hộp thoại về nghỉ trong một frame
GĐ1: cả luồng dời vào Horcrux · GĐ3: consumer CanvasSpotlight chạy qua luồng này · GĐ4: tài liệu
```

## Bản đồ hệ cuối

```
Horcrux Abstractions/Composites/Tutorial          Horcrux Implementations/Composites/Tutorial
  ITutorialService, ITutorialSpotlight,             TutorialService ([Service], MonoBehaviour<BasePersistenceDataCollection>)
  ISpotlightHole, ILiftedCanvas, ITutorialHand,       ├─ TutorialSpotlight (lỗ: shader ui_spotlight_dim · nâng: sorting + raycaster)
  ITutorialBubble, ATutorialScript, TutorialAnchor,   ├─ TutorialHand (shader ui_tap_ripple)
  TutorialGate<T> (+ Restriction), TutorialProgress   └─ TutorialBubble (UniText) ──► CalloutPlacement.Place
  BasePersistenceDataCollection.Tutorial (entry)
Horcrux Utilities/Layout
  CalloutPlacement, CalloutLayout, CalloutSide
Dự án: kịch bản, SpineHandTapBeat, prefab lớp phủ (art), neo trên prefab
```

---

## GĐ1 — Dời lõi sang Horcrux, không đổi hành vi; bảng case cho `Lift` và `TutorialGate`

**Mục tiêu:** cùng file, cùng GUID, cùng namespace, nằm trong `com.horcrux.runtime`; dự án compile và chơi y như trước. Hai phần bản tạm
đã có mà chưa test — `Lift` (nâng canvas) và `TutorialGate` nhiều chủ — có bảng case.

**Vì sao bây giờ:** namespace đã là Horcrux, phụ thuộc duy nhất vào dự án là entry lưu; dời trước để mọi thay đổi sau diễn ra một chỗ.

**Sơ đồ sau GĐ1** (`+` là phần mới)

```
  dự án: kịch bản ──► Horcrux Composites/Tutorial+ (lõi dời từ dự án)
                         TutorialService.ProgressEntry ──► BasePersistenceDataCollection.TutorialProgress+ (partial mới)
  dự án: Scripts/Tutorial/Core/ ── xoá
```

| Từ (`Assets/_CategoryJam/Scripts/Tutorial/Core/`) | Tới (`Assets/Horcrux/`) |
|---|---|
| `Abstractions/*.cs` (5 file) | `Runtime/Abstractions/Composites/Tutorial/` |
| `Implementations/*.cs` (4 file) | `Runtime/Implementations/Composites/Tutorial/` |
| `Shader/ui_spotlight_dim.shader`, `ui_tap_ripple.shader` | `Shader/` |
| `GamePersistenceDataCollection.Tutorial.cs` | thay bằng `Runtime/Abstractions/Composites/Tutorial/BasePersistenceDataCollection.Tutorial.cs` (dưới) |

Dời trong Project window (hoặc `git mv` kèm `.meta`): GUID giữ, prefab `tutorial_overlay`, scene, material trỏ đúng. Hai repo khác nhau
(dự án và submodule Horcrux): `git mv` không qua ranh giới — chép file **kèm `.meta`** sang Horcrux rồi xoá bản dự án trong cùng lần
refresh, không để Unity thấy hai bản cùng GUID.

### `TutorialService.cs` › `Properties` — đoạn cũ → đoạn mới

Cũ:

```csharp
        // Temporary home of the entry: moves to a BasePersistenceDataCollection partial once the system is in Horcrux.
        private PersistenceDataEntry<TutorialProgress> ProgressEntry
            => ((GamePersistenceDataCollection)_saveCollection).TutorialProgress;
```

Mới:

```csharp
        private PersistenceDataEntry<TutorialProgress> ProgressEntry => _saveCollection.TutorialProgress;
```

và bỏ `using CategoryJam.Gameplay.Config.Persistence;`.

### `Runtime/Abstractions/Composites/Tutorial/BasePersistenceDataCollection.Tutorial.cs` — file mới

```csharp
using Horcrux.Runtime.Abstractions.Tutorial;
using Horcrux.Runtime.Utilities;
using UnityEngine;

namespace Horcrux.Runtime.Abstractions.Persistence
{
    // Lives in the system folder, not Foundations/Persistence: the foundation layer must not reference a Composites type.
    public abstract partial class BasePersistenceDataCollection
    {
        [Splitter("Tutorial")]
        [SerializeField, MarkedPersistence]
        private PersistenceDataEntry<TutorialProgress> tutorialProgress;

        public PersistenceDataEntry<TutorialProgress> TutorialProgress => tutorialProgress;
    }
}
```

Tên field `tutorialProgress` giữ nguyên: là key YAML trong asset `GamePersistenceDataCollection` — đổi tên là mất ô `Key` đã điền
(`tutorial_progress`), tiến độ tutorial không lưu nữa, không log.

**Giữ `UnityEngine.Time` viết đủ** (`TutorialHand.cs` trong vòng tay lướt, `TutorialService.cs` trong vòng đếm giây rảnh): namespace mới
`Horcrux.Runtime.Implementations.Tutorial` là anh em của `Horcrux.Runtime.Implementations.Time` (`TimeService.cs`), nên `Time.` rút gọn
trỏ vào namespace đó, lỗi CS0234. Gốc là tên namespace trùng type Unity; đổi `…Implementations.Time` thành `…Implementations.TimeSystem`
(khớp `docs/Runtime/TimeSystem`) là đổi API công khai — việc riêng nếu developer chốt, không gộp vào lần dời này.

### Đọc lại, không gõ mới — `Lift` và `TutorialGate` (đã có trong file vừa dời)

| Chỗ | Nội dung |
|---|---|
| `ITutorialService.cs` › `ITutorialSpotlight.Lift(Canvas target, bool passesInput, int tier = 0)`, `ILiftedCanvas.Drop()` | hợp đồng; doc của `DimAsync`: alpha 0 thả mọi canvas đã nâng. Target **đang bật** lúc `Lift` và lúc `Drop`: consumer bật rồi mới nâng, thả rồi mới tắt |
| `TutorialSpotlight.cs` › `LiftedCanvas`, `Lift`, `DropLift`, `DropAllLifts` | tối đa 4 canvas (`MaxLiftAmount`); sorting = sorting canvas lớp phủ + 1 + `max(0, tier)`; target tắt lúc `Lift` thì `LogError` rồi vẫn nâng; raycaster đặt theo `passesInput` **trước** `overrideSorting`; target đã huỷ thì bỏ qua lúc trả |
| `TutorialSpotlight.cs` › `DimAsync(0)`, `ResetNow` | gọi `DropAllLifts` |
| `TutorialGate.cs` › `Restrict`, `Accepts`, `Allows`, `Restriction.Change`, `Restriction.Drop` | mỗi chủ giữ một `Restriction`; `Allows` qua khi mọi luật nhận, bắn `Passed` hoặc `Rejected`; không còn luật thì qua và không bắn gì; `Accepts` im lặng |

**Bằng chứng cho "raycaster bật lúc nghỉ"** (source UGUI): `Graphic.CacheCanvas` gán graphic vào canvas tổ tiên **đầu tiên đang bật**;
`GraphicRaycaster.Raycast` chỉ duyệt `GraphicRegistry.GetRaycastableGraphicsForCanvas(canvas)` của chính nó. Widget của
`CanvasSpotlight` bấm được lúc nghỉ với raycaster tắt chỉ khi component `Canvas` lồng của nó đang tắt — graphic khi đó thuộc canvas cha.
`Lift` nhớ trạng thái raycaster, đặt theo `passesInput`, `Drop` trả lại.

| Consumes | Produces |
|---|---|
| Horcrux Persistence (`BasePersistenceDataCollection`, `PersistenceDataEntry`), `InitArgs`, `UniTask`, `PrimeTween`, `UniText` | cả hệ Tutorial trong `com.horcrux.runtime` · `BasePersistenceDataCollection.TutorialProgress` |

**Quyết định** — sorting của canvas nâng suy từ canvas lớp phủ (`rootCanvas.sortingOrder + 1`), không phải số cấu hình: nút nâng luôn
ngay trên lớp tối và dưới popup; một số cố định như 3103 của `CanvasSpotlight` vượt cả popup. — Trả về trong `finally` của service:
kịch bản bị huỷ giữa chừng không để nút kẹt sorting cao. — `tier` thay vì để Unity chọn: hai canvas Overlay cùng sorting order vẽ theo
thứ tự engine tự chọn, đổi theo instance. Thứ phải đè một canvas nâng khác lấy bậc cao hơn. **Giới hạn:** lớp phủ + 1 + `tier` phải nhỏ
hơn sorting của popup, không thì hoà với popup và lại không cố định thứ tự: dự án chừa khe giữa lớp phủ và popup và ghi tầng canvas
trong tài liệu dự án. — `Lift` nhận target tắt chỉ `LogError`, không tự bật: sorting đặt lên canvas lồng đang tắt không ăn, nhưng bật
hộ là đổi hành vi quan sát được của consumer. `Drop` không log: `DimAsync(0)`, `ResetNow` dọn cả target đã bị chủ tắt hợp lệ.

**Editor setup:** mở `Resources/Config/GamePersistenceDataCollection` › mục Tutorial › `Key` vẫn là `tutorial_progress`. Trống thì tiến độ
không lưu, lượt dạy Level 1 lặp lại mỗi lần mở app.

**Kiểm:** compile sạch · grep `CategoryJam` trong `Runtime/**/Tutorial/` ra 0 · `git diff` asset collection không đổi key · chơi lượt dạy
Level 1, mở lại app không dạy lại.

`Tests/EditMode/TutorialLiftTests.cs` (agent):

| Case | Input | Kỳ vọng |
|---|---|---|
| nâng bấm được | canvas lồng sorting 0, raycaster tắt; `Lift(c, true)` | `overrideSorting` true, sorting = lớp phủ + 1, raycaster bật |
| nâng không bấm | raycaster bật; `Lift(c, false)` | raycaster tắt |
| thả | sau `Drop()` | ba giá trị về như trước `Lift` |
| thả hai lần | `Drop()` lần hai | không đổi, không ném |
| lớp tối về 0 | `DimAsync(0)` khi đang nâng | ba giá trị về như trước |
| target đã huỷ | `DestroyImmediate` target rồi `ResetNow` | không ném, slot dùng lại được |
| quá 4 | `Lift` lần 5 | `LogError`, trả null |
| thiếu raycaster | `Lift(c, true)` trên canvas không raycaster | `LogError`, vẫn nâng |
| bậc | `Lift(a, false)`, `Lift(b, false, tier: 1)` | sorting `b` = sorting `a` + 1 |
| bậc âm | `Lift(c, false, tier: -3)` | sorting = lớp phủ + 1 |
| target tắt | `SetActive(false)` rồi `Lift(c, false)` | `LogError` nêu tên canvas; slot vẫn chiếm, `Drop` trả được |

`Tests/EditMode/TutorialGateTests.cs` (agent; `T` là `int`):

| Case | Input | Kỳ vọng |
|---|---|---|
| không luật | `Allows(5)` | true; không bắn `Passed`, `Rejected` |
| một luật nhận | `Restrict(x => x > 0)`; `Allows(5)` | true; `Passed` một lần với 5 |
| một luật từ chối | như trên; `Allows(-1)` | false; `Rejected` một lần với -1 |
| hai chủ | A `x => x > 0`, B `x => x % 2 == 0`; `Allows(3)`, `Allows(4)` | false, true |
| nhả không gỡ chủ kia | như trên; A `Drop()`; `Allows(3)` | false (B còn giữ). Đối chứng: `Drop` gỡ mọi luật (`_restrictions.Clear()`) làm case đỏ |
| nhả hai lần | A `Drop()` hai lần; B còn giữ | không ném; B vẫn chặn `Allows(3)` |
| đổi luật tại chỗ | A `Change(x => x > 10)`; `Allows(5)` | false; A vẫn trong cửa (nhả A rồi `Allows(5)` true) |
| kiểm im lặng | một luật từ chối; `Accepts(-1)` | false; không bắn `Rejected` |
| luật null | `Restrict(null)`, `Change(null)` | `ArgumentNullException` |

**Mốc commit:** Horcrux `feat(tutorial): lõi Tutorial vào Composites; case Lift, TutorialGate` · dự án: xoá bản tạm + con trỏ submodule.

---

## GĐ2 — `CalloutPlacement`: đặt hộp thoại thành hàm thuần

**Mục tiêu:** phép đặt hộp thoại và mũi tên cạnh đích tách khỏi `RectTransform`, có bảng case; hộp thoại trong game y như trước trừ hai
chỗ ghi ở "Đổi hành vi".

**Vì sao bây giờ:** là phần sửa nhiều lượt nhất ở dự án nguồn (mũi tên rời đuôi, giữ trong safe area, mũi tên rời tâm hộp khi thiếu chỗ)
mà bảng case GĐ1 không phủ; phép tính lẫn với đổi đơn vị canvas → pixel trong `TutorialBubble.PlaceNextToTarget`, chỉ kiểm được bằng
mắt. Hàm không mang gì của tutorial: tooltip, khung gợi ý cạnh nút dùng lại được.

**Sơ đồ sau GĐ2**

```
  TutorialBubble.LateUpdate ──► PlaceNextToTarget ──px──► CalloutPlacement.Place+ ──► CalloutLayout+ ──► selfRect, arrow
                                (đổi canvas → pixel, safe area)          (CalloutSide+)
```

### `TutorialBubble.cs` — field mới, `PlaceNextToTarget` cũ → mới

Thêm `using Horcrux.Runtime.Utilities.Layout;` sau `using Horcrux.Runtime.Utilities;`.

Field mới, dưới `arrowGap`:

```csharp
        [Tooltip("Canvas units between the arrow's tail and the box's edge.")]
        [SerializeField, Min(0f)] private float arrowGap = 8f;
        [Tooltip("Canvas units kept between the arrow's sides and the safe area's edges, so the arrow never sits on the box's rounded corner.")]
        [SerializeField, Min(0f)] private float arrowCornerInset;
```

Cũ: cả thân `PlaceNextToTarget` (từ `Rect frame = …` tới `arrow.localEulerAngles = …`).

Mới:

```csharp
        private void PlaceNextToTarget()
        {
            Rect frame = _target.GetScreenFrame(_worldCamera).Rect;
            // Root is never scaled by the pop, so its lossy scale is the canvas scale: canvas units to screen pixels.
            float toPixels = selfRect.lossyScale.x;
            float margin = screenMargin * toPixels;
            Rect safe = Screen.safeArea;
            Rect bounds = Rect.MinMaxRect(safe.xMin + margin, safe.yMin + margin, safe.xMax - margin, safe.yMax - margin);

            // Named: three gaps and two rects of one type, easy to pass in the wrong order.
            CalloutLayout layout = CalloutPlacement.Place(
                target: frame,
                boxSize: selfRect.rect.size * toPixels,
                arrowSize: _hasArrow ? arrow.rect.size * toPixels : Vector2.zero,
                targetGap: targetGap * toPixels,
                arrowGap: arrowGap * toPixels,
                arrowCornerInset: arrowCornerInset * toPixels,
                bounds: bounds,
                preferredSide: _side == TutorialBubbleSide.Above ? CalloutSide.Above : CalloutSide.Below);

            selfRect.position = layout.BoxCenter;
            if (!_hasArrow) return;
            arrow.position = layout.ArrowTip;
            arrow.localEulerAngles = new Vector3(0f, 0f, layout.Side == CalloutSide.Above ? 0f : 180f);
        }
```

### `Runtime/Utilities/Layout/CalloutPlacement.cs` — file mới

```csharp
using UnityEngine;

namespace Horcrux.Runtime.Utilities.Layout
{
    /// <summary>Phía của đích mà khung nổi đứng.</summary>
    public enum CalloutSide
    {
        Above = 1,
        Below = 2,
    }

    /// <summary>Kết quả đặt một khung nổi. Mọi số là pixel màn hình.</summary>
    public readonly struct CalloutLayout
    {
        public readonly Vector2 BoxCenter;
        public readonly Vector2 ArrowTip;
        public readonly CalloutSide Side;

        public CalloutLayout(Vector2 boxCenter, Vector2 arrowTip, CalloutSide side)
        {
            BoxCenter = boxCenter;
            ArrowTip = arrowTip;
            Side = side;
        }
    }

    /// <summary>
    /// Đặt khung nổi (hộp thoại, tooltip) cạnh một đích, trong một vùng chữ nhật (safe area trừ lề). Ràng buộc cứng trước: hộp nằm
    /// trong vùng, mũi tên dính hộp. Rồi thẩm mỹ: mũi tên trên tâm đích, hộp căn giữa mũi tên, chỉ lệch khi chạm mép vùng.
    /// </summary>
    public static class CalloutPlacement
    {
        /// <param name="arrowSize">(0, 0) khi không có mũi tên; <see cref="CalloutLayout.ArrowTip"/> khi đó không dùng.</param>
        /// <param name="arrowCornerInset">Khoảng tối thiểu từ mép vùng tới mép mũi tên, để mũi tên không rơi vào góc bo của hộp.</param>
        /// <param name="preferredSide">Phía được xin; đổi sang phía kia chỉ khi phía này không vừa mà phía kia vừa.</param>
        public static CalloutLayout Place(Rect target, Vector2 boxSize, Vector2 arrowSize, float targetGap, float arrowGap,
            float arrowCornerInset, Rect bounds, CalloutSide preferredSide)
        {
            Vector2 halfBox = boxSize * 0.5f;
            bool hasArrow = arrowSize.y > 0f;
            // Đầu mũi tên tới mép gần của hộp.
            float span = hasArrow ? arrowSize.y + arrowGap : 0f;

            float aboveY = target.yMax + targetGap + span + halfBox.y;
            float belowY = target.yMin - targetGap - span - halfBox.y;
            bool aboveFits = aboveY + halfBox.y <= bounds.yMax;
            bool belowFits = belowY - halfBox.y >= bounds.yMin;
            bool above = preferredSide == CalloutSide.Above ? aboveFits || !belowFits : !belowFits && aboveFits;
            float boxY = ClampCentered(above ? aboveY : belowY, halfBox.y, bounds.yMin, bounds.yMax);

            float halfArrow = hasArrow ? arrowSize.x * 0.5f + arrowCornerInset : 0f;
            float arrowX = ClampCentered(target.center.x, halfArrow, bounds.xMin, bounds.xMax);
            float boxX = ClampCentered(arrowX, halfBox.x, bounds.xMin, bounds.xMax);

            // Từ hộp, không từ đích: hộp bị kẹp vào vùng thì mũi tên đi theo hộp.
            float tipY = above ? boxY - halfBox.y - span : boxY + halfBox.y + span;
            return new CalloutLayout(new Vector2(boxX, boxY), new Vector2(arrowX, tipY), above ? CalloutSide.Above : CalloutSide.Below);
        }

        /// <summary>Kẹp tâm một đoạn dài 2×half vào [min, max]; đoạn dài hơn vùng thì đặt giữa vùng.</summary>
        private static float ClampCentered(float center, float half, float min, float max)
            => max - min < 2f * half ? (min + max) * 0.5f : Mathf.Clamp(center, min + half, max - half);
    }
}
```

| Consumes | Produces |
|---|---|
| `TutorialAnchor.GetScreenFrame`, `Screen.safeArea` (ở `TutorialBubble`) | `CalloutPlacement.Place`, `CalloutLayout`, `CalloutSide` · field `TutorialBubble.arrowCornerInset` |

**Quyết định** — đầu mũi tên tính **từ hộp**, không từ đích: khi cả trên lẫn dưới đều không đủ chỗ, hộp bị kẹp vào vùng; tính từ đích thì
mũi tên đứng ở mép đích, tách khỏi hộp. Ràng buộc cứng (hộp trong vùng, mũi tên dính hộp) đi trước thẩm mỹ (đầu mũi tên chạm mép đích).
— Hàm làm việc bằng pixel màn hình; đổi đơn vị là việc của component, vì chỉ nó biết canvas. — Phía là `enum CalloutSide` trong
`Layout`, cả ở tham số lẫn kết quả; `TutorialBubble` ánh xạ từ `TutorialBubbleSide`. Phương án bỏ: `bool preferAbove` — hai phía loại trừ
đọc như cờ ở call site; dùng thẳng `TutorialBubbleSide` — `Utilities` không phụ thuộc hệ Tutorial. — Call site gọi bằng **named
argument**: ba khoảng `float` và hai `Rect` liền nhau dễ truyền lộn. Phương án bỏ: struct spec gom ba khoảng — constructor ba `float`
vẫn lộn được, thêm một type công khai cho cùng tác dụng. — `arrowCornerInset` mặc định 0: mũi tên vẫn có thể sát mép như trước; đặt bằng
bán kính bo của hộp trên prefab.

**Đổi hành vi** (hai chỗ, đều ở cạnh màn; ca thường y như trước):

| Ca | Trước | Sau |
|---|---|---|
| cả trên lẫn dưới không đủ chỗ | hộp kẹp vào vùng, mũi tên đứng ở mép đích, tách khỏi hộp | mũi tên dính hộp, đầu mũi tên có thể đè lên đích |
| hộp rộng hơn vùng | sát mép trái, tràn phải | giữa vùng, tràn đều hai bên |

**Editor setup:** prefab lớp phủ › `TutorialBubble` › `Arrow Corner Inset` = bán kính góc bo của ảnh hộp (đơn vị canvas). Canvas của hộp
thoại phải là **Screen Space – Overlay**: `selfRect.position` được gán bằng pixel màn hình, chỉ đúng trên Overlay; canvas khác thì hộp
lệch khỏi đích mà không log.

**Kiểm:** compile sạch, bảng case xanh · chơi lượt dạy Level 1: hộp thoại và mũi tên đứng như trước ở mọi bước.

| Case (agent viết, `Tests/EditMode/CalloutPlacementTests.cs`) | Input (vùng `(0,0,1000,2000)`, hộp 400×100, mũi tên 40×30, `targetGap` 10, `arrowGap` 8, inset 0, trừ khi ghi khác) | Kỳ vọng |
|---|---|---|
| giữa màn | đích `(450,900,100,100)`, xin `Above` | `Side` `Above`, hộp `(500,1098)`, mũi tên `(500,1010)` |
| xin dưới, dưới vừa | như trên, xin `Below` | `Side` `Below`, hộp y 802, mũi tên y 890 |
| sát mép trái | đích `(0,900,40,100)`, xin `Above` | mũi tên x 20, hộp x 200 |
| sát mép trái, có inset | như trên, inset 24 | mũi tên x 44, hộp x 200 |
| trên không đủ chỗ | đích `(450,1900,100,80)`, xin `Above` | `Side` `Below`, hộp `(500,1802)`, mũi tên `(500,1890)` |
| dưới không đủ chỗ | đích `(450,20,100,80)`, xin `Below` | `Side` `Above`, hộp y 198, mũi tên y 110 |
| cả hai không đủ | vùng `(0,0,1000,300)`, đích `(450,100,100,100)`, xin `Above` | `Side` `Above`, hộp `(500,250)`, mũi tên `(500,162)`. Đối chứng: tính đầu mũi tên từ đích ra y 210, case đỏ |
| không mũi tên | `arrowSize` (0,0), đích như ca đầu | hộp `(500,1060)` |
| hộp rộng hơn vùng | hộp 1200×100, đích như ca đầu | hộp x 500 |

**Mốc commit:** Horcrux `feat(layout): CalloutPlacement; TutorialBubble đặt hộp qua nó`.

---

## GĐ3 — Consumer `CanvasSpotlight` chuyển sang `ITutorialService`, xoá `CanvasSpotlight`

**Giả định (cần xác nhận):** consumer `ICanvasSpotlight` ở dự án khác — category-jam không có lời gọi nào (grep ra 0); developer chỉ tên
dự án đó.

**Mục tiêu:** không còn lớp tối thứ hai; widget Home dạy bằng cùng tay, cùng hộp thoại với gameplay.

**Vì sao bây giờ:** sau GĐ1 mọi thao tác của `CanvasSpotlight` có bản tương đương trong service chung.

**Sơ đồ sau GĐ3**

```
  kịch bản dạy widget+ (ATutorialScript) ──► ITutorialService: DimAsync · Lift(widget, true) · Hand.PointAt · Bubble
  CanvasSpotlight ── xoá
```

| `ICanvasSpotlight` | `ITutorialService` (trong `ATutorialScript.RunAsync`) |
|---|---|
| `Focus(target, config)` | `Spotlight.DimAsync(alpha, ct)` + `Spotlight.Lift(target, true)` + `Hand.PointAt(anchor)` — đích cần thêm `TutorialAnchor` (`Space` ScreenOverlay) |
| `Focus(target, extra, config)` | thêm `Lift(extra, …)` lần hai |
| `HighlightConfig.NoHand` | không gọi `Hand` |
| `FadeDimAsync(to, duration, ct)` | `Spotlight.DimAsync(to, ct)`: thời lượng là `transitionSeconds` của service (thiếu tham số thời lượng — Mở rộng sau nếu lễ cần nhịp riêng) |
| `Release()` | `Drop()` từng handle, hoặc để `PlayAsync` dọn khi kịch bản xong |
| chờ tap nút thật | giữ nguyên cách của consumer (`OnClickAsync`), nút đã nâng với `passesInput` true |

Mỗi luồng consumer thành một class con `ATutorialScript` (Id = khoá lưu nếu chỉ dạy một lần). Dự án nào chưa chuyển xong thì giữ
`CanvasSpotlight` tới lúc đó; xoá `Runtime/Abstractions/Foundations/CanvasSpotlight/`, `Runtime/Implementations/Foundations/Spotlight/`,
`docs/Runtime/CanvasSpotlight/` khi grep `ICanvasSpotlight` trên mọi dự án dùng Horcrux ra 0. Hai bẫy đang có trong `CanvasSpotlight.cs`
đi theo file bị xoá, không sửa riêng: `FocusCore` còn `Debug.LogError($"[CanvasSpotlight] : …")` (một dòng đỏ mỗi lần `Focus`);
`Capture` log thiếu raycaster rồi vẫn gọi `raycaster.enabled` (NRE).

| Consumes | Produces |
|---|---|
| `ITutorialService` (GĐ1) | kịch bản dạy widget ở dự án consumer · xoá `ICanvasSpotlight`, `CanvasSpotlight` |

**Quyết định** — không giữ `CanvasSpotlight` làm lớp mỏng gọi sang service: hai API cho một việc, consumer mới không biết chọn bên nào;
bảng ánh xạ trên đủ để chuyển tay.

**Editor setup (dự án consumer):** gỡ instance `canvas_spotlight` khỏi `Services.unity`; thêm một instance prefab lớp phủ tutorial (Canvas
Overlay, sort order trên HUD, dưới popup); mỗi widget được dạy: `Canvas` + `Graphic Raycaster` (bật) + `Tutorial Anchor`. Thiếu
`Graphic Raycaster` bật thì widget sáng mà không bấm được (Console có `LogError` của `Lift`).

**Kiểm:** chơi luồng dạy widget: màn tối một lớp, widget sáng và bấm được, tay bám widget; thoát giữa chừng không kẹt tối · grep
`ICanvasSpotlight` ra 0 trước khi xoá.

**Mốc commit:** dự án consumer `refactor(tutorial): dạy widget bằng ITutorialService` · Horcrux `chore: xoá CanvasSpotlight`.

---

## GĐ4 — Tài liệu, dự án trỏ về bản Horcrux, xoá Plan (agent)

| File | Đổi |
|---|---|
| `docs/Runtime/Tutorial/Tutorial.md`, `.html` | từ `Scripts/Tutorial/Tutorial.md` của dự án, bỏ kịch bản Level 1, tên dự án, số preset; mục "Trước khi chạy" cho prefab lớp phủ (canvas hộp thoại là Screen Space – Overlay), neo, entry lưu; `TutorialGate` nhiều chủ; tầng sorting của `Lift` và khe trước popup |
| `docs/Runtime/CalloutPlacement/CalloutPlacement.md`, `.html` | hệ Runtime thuần, `Code: Runtime/Utilities/Layout`: ràng buộc cứng trước thẩm mỹ, bảng case GĐ2 làm ví dụ, lưu ý pixel màn hình và canvas Overlay |
| dự án `Assets/_CategoryJam/Scripts/Tutorial/Tutorial.md` | thu về kịch bản của dự án và tầng canvas, trỏ `docs/Runtime/Tutorial/`; grep `Tutorial/Core` trong comment, tài liệu dự án ra 0 |
| Plan này | xoá (kèm `.meta`) khi GĐ1–3 xong; GĐ3 còn chờ dự án consumer thì chuyển GĐ3 sang task riêng rồi xoá |

**Kịch bản chơi thử** (developer):

| Vào đâu | Làm gì | Nhìn gì | Khác trước | Dấu hiệu hỏng |
|---|---|---|---|---|
| Level 1, save mới | đi hết lượt dạy | lỗ khoét trên tile, box; tay bám đích; hộp thoại và mũi tên cạnh đích mỗi bước | không | hộp thoại lệch khỏi đích; lớp tối không có lỗ |
| level mở khoá booster | tới bước dùng thử | nút booster nổi trên lớp tối, bấm được; bấm chỗ khác bị nuốt | không | nút chìm dưới lớp tối; nút không bấm được |
| bước có đích sát mép trên hoặc trái | nhìn hộp thoại | hộp trong safe area, mũi tên dính hộp | ca cả trên lẫn dưới không đủ chỗ: mũi tên dính hộp thay vì đứng ở mép đích | mũi tên tách khỏi hộp; hộp tràn khỏi safe area |
| giữa lượt dạy | thoát level | lớp tối, tay, hộp thoại tắt ngay | không | màn kẹt tối; nút kẹt trên lớp tối |
| sau khi xong Level 1 | tắt hẳn app, mở lại | không dạy lại | không | lượt dạy Level 1 lặp lại (key `tutorial_progress` trống) |

**Mốc commit:** Horcrux `docs(tutorial): Tutorial, CalloutPlacement; xoá Plan` · dự án `docs(tutorial): Tutorial.md trỏ Horcrux` kèm con
trỏ submodule.

## File → GĐ

| File | GĐ1 | GĐ2 | GĐ3 | GĐ4 |
|---|---|---|---|---|
| `Runtime/Abstractions/Composites/Tutorial/*` | dời vào; `BasePersistenceDataCollection.Tutorial.cs` mới | | | |
| `Runtime/Implementations/Composites/Tutorial/*` | dời vào; `TutorialService.ProgressEntry` | `TutorialBubble`: `arrowCornerInset`, `PlaceNextToTarget` | | |
| `Shader/ui_spotlight_dim.shader`, `ui_tap_ripple.shader` | dời vào | | | |
| `Runtime/Utilities/Layout/CalloutPlacement.cs` | | tạo | | |
| `Tests/EditMode/TutorialLiftTests.cs`, `TutorialGateTests.cs` (agent) | tạo | | | |
| `Tests/EditMode/CalloutPlacementTests.cs` (agent) | | tạo | | |
| `CanvasSpotlight` (code, tài liệu); dự án consumer | | | xoá; kịch bản dạy widget | |
| `docs/Runtime/Tutorial/*`, `docs/Runtime/CalloutPlacement/*` (agent) | | | | tạo |
| dự án: `Scripts/Tutorial/Core/` | xoá | | | |
| dự án: `Scripts/Tutorial/Tutorial.md`; Plan này | | | | sửa; xoá Plan |
