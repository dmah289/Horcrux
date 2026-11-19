# Plan — hệ Tutorial lên Horcrux, gộp `CanvasSpotlight`

Ngày 2026-11-19. Nguồn: category-jam, bản tạm `Assets/_CategoryJam/Scripts/Tutorial/Core/` (namespace đã là
`Horcrux.Runtime.Abstractions.Tutorial`, `Horcrux.Runtime.Implementations.Tutorial`, nằm nhờ assembly `CategoryJam.Gameplay`), tài liệu
dự án `Scripts/Tutorial/Tutorial.md`. Hai consumer đang chạy: kịch bản Level 1 (khoét lỗ trên tile, box) và kịch bản mở khoá booster
(nâng nút HUD lên trên lớp tối). Không phụ thuộc Plan nào.

**Phân công:** GĐ1–3 developer làm trong Horcrux (dời file, gõ phần mới), agent viết test theo bảng case rồi compile · GĐ4 (tài liệu),
GĐ5 (dự án chuyển sang bản Horcrux) agent làm. Kiểm mỗi giai đoạn: `bash Assets/Horcrux/Editor/CompileCheck/Tools~/verify.sh --csproj
com.horcrux.runtime.csproj,CategoryJam.Gameplay.csproj,Assembly-CSharp.csproj --tests Horcrux.EditModeTests,CategoryJam.EditModeTests`.

## Ngữ cảnh đã chốt

- **Người dùng:** mọi tutorial của game — đích world (tile, box qua camera gameplay) lẫn đích UI (nút HUD, widget Home) — chung một lớp
  tối, một tay chỉ, một hộp thoại, một chỗ lưu tiến độ.
- **Mục tiêu:** một hệ duy nhất trong Horcrux; dự án chỉ viết kịch bản (`ATutorialScript`) và đặt neo. `CanvasSpotlight` thôi là hệ thứ hai.
- **Hai cách làm nổi đích, cùng lớp tối:** *khoét lỗ* (`Open`) — shader cắt cửa sổ, đích thấy qua lỗ, dùng được cho world; *nâng canvas*
  (`Lift`) — canvas lồng của đích vẽ đè lên lớp tối như chính nó, không viền lỗ, chỉ cho UI Overlay. `Lift` là cơ chế của `CanvasSpotlight`
  đưa vào service chung.
- **Chỗ đặt:** `Runtime/Abstractions/Composites/Tutorial/`, `Runtime/Implementations/Composites/Tutorial/` — dựng trên Persistence của
  Horcrux, không mang domain. Shader sang `Shader/`.
- **Không làm:** hàng đợi nhiều kịch bản · tracking · bật tắt từ remote config — giữ như tài liệu dự án đã chốt · tay chỉ theo hướng
  (`HighlightConfig.HandDirection` của `CanvasSpotlight`): tay của hệ chung bám neo, chưa có consumer cần hướng khác (mục Mở rộng sau).
- **Quyết định trái trực giác:** canvas lồng của đích **để raycaster bật lúc nghỉ** (khác hợp đồng `CanvasSpotlight` §3.2). Graphic thuộc
  canvas gần nhất đang bật, và raycaster của canvas cha không quét graphic của canvas lồng — tắt raycaster lúc nghỉ là nút không bấm được
  ngoài tutorial. `Lift` nhớ trạng thái raycaster, đặt theo `passesInput`, `Drop` trả lại. (Cần kiểm trên consumer `CanvasSpotlight` hiện
  có: nếu widget của nó vẫn bấm được lúc nghỉ với raycaster tắt thì ghi lại lý do ở đây.)

## Đã khảo sát

| Nguồn | Lấy gì | Không lấy, vì sao |
|---|---|---|
| `Runtime/…/Foundations/Spotlight/CanvasSpotlight.cs`, `docs/Runtime/CanvasSpotlight/` | cơ chế nâng: bật raycaster rồi mới `overrideSorting`, nhớ ba giá trị để trả; tối đa nhiều canvas một lượt | lớp tối riêng ở sorting cố định 3103 (hai lớp tối chồng nhau khi cả hai hệ chạy); tay sprite `CharmPointAndBob` không bám đích; không hộp thoại, không lưu tiến độ |
| bản tạm `Scripts/Tutorial/Core/` | toàn bộ: `TutorialService`, `TutorialSpotlight` (lỗ + `Lift`), `TutorialHand`, `TutorialBubble`, `TutorialGate`, `TutorialAnchor`, `TutorialProgress` | entry lưu khai ở collection của dự án (GĐ1 dời) |
| `Abstractions/Composites/UserSegmentation/BasePersistenceDataCollection.UserSegmentation.cs` | khuôn partial khai entry lưu của hệ Composite | — |

**Hai bẫy thấy khi đọc `CanvasSpotlight.cs`** (sửa hay xoá theo GĐ3): `FocusCore` còn `Debug.LogError($"[CanvasSpotlight] : …")` — một
dòng đỏ mỗi lần `Focus`; `Capture` log thiếu raycaster rồi vẫn gọi `raycaster.enabled` → NRE.

## Luồng mô tả

```
Dự án: kịch bản (ATutorialScript) ──► ITutorialService.PlayAsync(script, ct)
                                         ├─ bật canvas lớp phủ, lấy Camera.main
                                         ├─ script.RunAsync: Spotlight.DimAsync · Open (lỗ) · Lift (nâng) · Hand · Bubble
                                         ├─ xong: ghi Id vào TutorialProgress (Id trống: không ghi)
                                         └─ finally: lỗ, canvas đã nâng, lớp tối, tay, hộp thoại về nghỉ trong một frame
```

## Bản đồ hệ cuối

```
Horcrux Abstractions/Composites/Tutorial          Horcrux Implementations/Composites/Tutorial
  ITutorialService, ITutorialSpotlight,             TutorialService ([Service], MonoBehaviour<BasePersistenceDataCollection>)
  ISpotlightHole, ILiftedCanvas, ITutorialHand,       ├─ TutorialSpotlight (lỗ: shader ui_spotlight_dim · nâng: sorting + raycaster)
  ITutorialBubble, ATutorialScript, TutorialAnchor,   ├─ TutorialHand (shader ui_tap_ripple)
  TutorialGate<T>, TutorialProgress                   └─ TutorialBubble (UniText)
  BasePersistenceDataCollection.Tutorial (entry)
Dự án: kịch bản, SpineHandTapBeat, prefab lớp phủ (art), neo trên prefab
```

---

## Giai đoạn 1 — Dời lõi sang Horcrux, không đổi hành vi

**Mục tiêu:** cùng file, cùng GUID, cùng namespace, nằm trong `com.horcrux.runtime`; dự án compile và chơi y như trước.

**Vì sao bây giờ:** namespace đã là Horcrux, phụ thuộc duy nhất vào dự án là entry lưu; dời trước để mọi thay đổi sau diễn ra một chỗ.

| Từ (`Assets/_CategoryJam/Scripts/Tutorial/Core/`) | Tới (`Assets/Horcrux/`) |
|---|---|
| `Abstractions/*.cs` (5 file) | `Runtime/Abstractions/Composites/Tutorial/` |
| `Implementations/*.cs` (4 file) | `Runtime/Implementations/Composites/Tutorial/` |
| `Shader/ui_spotlight_dim.shader`, `ui_tap_ripple.shader` | `Shader/` |
| `GamePersistenceDataCollection.Tutorial.cs` | thay bằng `Runtime/Abstractions/Composites/Tutorial/BasePersistenceDataCollection.Tutorial.cs` (dưới) |

Dời trong Project window (hoặc `git mv` kèm `.meta`): GUID giữ, prefab `tutorial_overlay`, scene, material trỏ đúng. Hai repo khác nhau
(dự án và submodule Horcrux): `git mv` không qua ranh giới — chép file **kèm `.meta`** sang Horcrux rồi xoá bản dự án trong cùng lần
refresh, không để Unity thấy hai bản cùng GUID.

### `Runtime/Abstractions/Composites/Tutorial/BasePersistenceDataCollection.Tutorial.cs` — file mới

```csharp
using Horcrux.Runtime.Abstractions.Tutorial;
using Horcrux.Runtime.Utilities;
using UnityEngine;

namespace Horcrux.Runtime.Abstractions.Persistence
{
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

**Editor setup:** mở `Resources/Config/GamePersistenceDataCollection` › mục Tutorial › `Key` vẫn là `tutorial_progress`. Trống thì tiến độ
không lưu, tutorial Level 1 dạy lại mỗi lần mở app.

**Kiểm:** compile sạch · grep `CategoryJam` trong `Runtime/**/Tutorial/` ra 0 · `git diff` asset collection không đổi key · chơi level 1
một lượt, mở lại app không dạy lại.

**Mốc commit:** Horcrux `feat(tutorial): lõi Tutorial vào Composites` · dự án: xoá bản tạm + con trỏ submodule.

---

## Giai đoạn 2 — `Lift` trong `ITutorialSpotlight` (đã có trong bản tạm)

**Mục tiêu:** nút UI nổi nguyên hình trên lớp tối, bấm được hoặc không theo `passesInput`, trả về như cũ ở mọi đường ra.

**Vì sao bây giờ:** là phần `CanvasSpotlight` có mà lõi chưa có; GĐ3 cần nó để gộp.

Code đã nằm trong file dời ở GĐ1 — developer đọc lại đúng ba chỗ, không gõ mới:

| Chỗ | Nội dung |
|---|---|
| `ITutorialService.cs` › `ITutorialSpotlight.Lift(Canvas target, bool passesInput, int tier = 0)` (bậc: cùng sorting order thì Unity vẽ không cố định thứ tự), `ILiftedCanvas.Drop()` | hợp đồng; doc của `DimAsync`: alpha 0 thả mọi canvas đã nâng |
| `TutorialSpotlight.cs` › `LiftedCanvas`, `Lift`, `DropLift`, `DropAllLifts` | tối đa 4 canvas; sorting = sorting canvas lớp phủ + 1; raycaster đặt theo `passesInput` **trước** `overrideSorting`; target đã huỷ thì bỏ qua lúc trả |
| `TutorialSpotlight.cs` › `DimAsync(0)`, `ResetNow` | gọi `DropAllLifts` |

**Quyết định** — sorting suy từ canvas lớp phủ (`rootCanvas.sortingOrder + 1`), không phải số cấu hình: nút nâng luôn ngay trên lớp tối và
dưới popup (lớp phủ 8, popup 10 ở dự án nguồn); một số cố định như 3103 của `CanvasSpotlight` vượt cả popup. — Trả về trong `finally` của
service: kịch bản bị huỷ giữa chừng không để nút kẹt sorting cao.

| Case (agent viết, EditMode) | Input | Kỳ vọng |
|---|---|---|
| nâng bấm được | canvas lồng sorting 0, raycaster tắt; `Lift(c, true)` | `overrideSorting` true, sorting = lớp phủ + 1, raycaster bật |
| nâng không bấm | raycaster bật; `Lift(c, false)` | raycaster tắt |
| thả | sau `Drop()` | ba giá trị về như trước `Lift` |
| thả hai lần | `Drop()` lần hai | không đổi, không ném |
| lớp tối về 0 | `DimAsync(0)` khi đang nâng | ba giá trị về như trước |
| target đã huỷ | `DestroyImmediate` target rồi `ResetNow` | không ném, slot dùng lại được |
| quá 4 | `Lift` lần 5 | `LogError`, trả null |
| thiếu raycaster | `Lift(c, true)` trên canvas không raycaster | `LogError`, vẫn nâng |

**Mốc commit:** Horcrux `test(tutorial): case Lift`.

---

## Giai đoạn 3 — Consumer `CanvasSpotlight` chuyển sang `ITutorialService`, xoá `CanvasSpotlight`

**Mục tiêu:** không còn lớp tối thứ hai; widget Home dạy bằng cùng tay, cùng hộp thoại với gameplay.

**Vì sao bây giờ:** sau GĐ2 mọi thao tác của `CanvasSpotlight` có bản tương đương.

| `ICanvasSpotlight` | `ITutorialService` (trong `ATutorialScript.RunAsync`) |
|---|---|
| `Focus(target, config)` | `Spotlight.DimAsync(alpha, ct)` + `Spotlight.Lift(target, true)` + `Hand.PointAt(anchor)` — đích cần thêm `TutorialAnchor` (`Space` ScreenOverlay) |
| `Focus(target, extra, config)` | thêm `Lift(extra, …)` lần hai |
| `HighlightConfig.NoHand` | không gọi `Hand` |
| `FadeDimAsync(to, duration, ct)` | `Spotlight.DimAsync(to, ct)`: thời lượng là `transitionSeconds` của service (thiếu tham số thời lượng — mục Mở rộng sau nếu lễ cần nhịp riêng) |
| `Release()` | `Drop()` từng handle, hoặc để `PlayAsync` dọn khi kịch bản xong |
| chờ tap nút thật | giữ nguyên cách của consumer (`OnClickAsync`), nút đã nâng với `passesInput` true |

Mỗi luồng consumer thành một class con `ATutorialScript` (Id = khoá lưu nếu chỉ dạy một lần). Dự án nào chưa chuyển xong thì giữ
`CanvasSpotlight` tới lúc đó; xoá `Abstractions/Foundations/CanvasSpotlight/`, `Implementations/Foundations/Spotlight/`,
`docs/Runtime/CanvasSpotlight/` khi grep `ICanvasSpotlight` trên mọi dự án dùng Horcrux ra 0.

**Editor setup (dự án consumer):** gỡ instance `canvas_spotlight` khỏi `Services.unity`; thêm một instance prefab lớp phủ tutorial (Canvas
Overlay, sort order trên HUD, dưới popup); mỗi widget được dạy: `Canvas` + `Graphic Raycaster` (bật) + `Tutorial Anchor`.

**Kiểm:** chơi luồng dạy widget: màn tối một lớp, widget sáng và bấm được, tay bám widget; thoát giữa chừng không kẹt tối.

**Mốc commit:** dự án consumer `refactor(tutorial): dạy widget bằng ITutorialService` · Horcrux `chore: xoá CanvasSpotlight`.

---

## Giai đoạn 4 — Tài liệu (agent)

`docs/Runtime/Tutorial/Tutorial.md` và `.html` từ `Scripts/Tutorial/Tutorial.md` của dự án nguồn, bỏ kịch bản Level 1, tên dự án, số
preset; mục "Trước khi chạy" cho prefab lớp phủ, neo, entry lưu. Xoá Plan này sau khi tài liệu xong.

## Giai đoạn 5 — Dự án nguồn dùng bản Horcrux (agent)

Đã xảy ra ở GĐ1 (file dời, không bản song song). Còn: `Scripts/Tutorial/Tutorial.md` thu về kịch bản của dự án và trỏ tài liệu Horcrux;
grep `Tutorial/Core` trong comment, tài liệu dự án ra 0.

## Mở rộng sau

- Tay chỉ theo hướng (ngón trỏ chéo, tay đứng một phía như `HighlightConfig.HandDirection`) khi có widget mà tay đè lên nhãn.
- `DimAsync` nhận thời lượng khi một lễ cần tối dần theo nhịp riêng.

## File → giai đoạn

| File | GĐ1 | GĐ2 | GĐ3 | GĐ4 |
|---|---|---|---|---|
| `Runtime/Abstractions/Composites/Tutorial/*` | dời vào; `BasePersistenceDataCollection.Tutorial.cs` mới | đọc | | |
| `Runtime/Implementations/Composites/Tutorial/*` | dời vào; `TutorialService.ProgressEntry` | đọc | | |
| `Shader/ui_spotlight_dim.shader`, `ui_tap_ripple.shader` | dời vào | | | |
| `Tests/EditMode/TutorialLiftTests.cs` (agent) | | tạo | | |
| `CanvasSpotlight` (+ tài liệu) | | | xoá | |
| `docs/Runtime/Tutorial/*` (agent) | | | | tạo |
| dự án: `Scripts/Tutorial/Core/` | xoá | | | |
