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
  ngoài tutorial. `Lift` nhớ trạng thái raycaster, đặt theo `passesInput`, `Drop` trả lại. Bằng chứng ở source UGUI:
  `Graphic.CacheCanvas` gán graphic vào canvas tổ tiên **đầu tiên đang bật**, `GraphicRaycaster.Raycast` chỉ duyệt
  `GraphicRegistry.GetRaycastableGraphicsForCanvas(canvas)` của chính nó. Widget của `CanvasSpotlight` bấm được lúc nghỉ với raycaster
  tắt chỉ khi component `Canvas` lồng của nó đang tắt — graphic khi đó thuộc canvas cha.
- **`TutorialGate` nhiều chủ:** `Restrict` trả `Restriction` của chủ gọi (`Change` đổi luật tại chỗ, `Drop` gỡ đúng luật đó), item
  qua khi mọi luật đang giữ đều nhận. Cửa nhặt có hai chủ thật — kịch bản dạy nhặt và lớp khoá của bàn dựng sẵn cho một lượt dạy —
  một khe chung thì chủ ghi sau đè luật chủ trước, và nhả của bên này gỡ luôn luật bên kia.

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

**Giữ `UnityEngine.Time` viết đủ** (`TutorialHand.cs` trong vòng tay lướt, `TutorialService.cs` trong vòng đếm giây rảnh): namespace mới
`Horcrux.Runtime.Implementations.Tutorial` là anh em của `Horcrux.Runtime.Implementations.Time` (`TimeService.cs`), nên `Time.` rút gọn
trỏ vào namespace đó, lỗi CS0234. Gốc là tên namespace trùng type Unity (`code-design` › Naming); đổi `…Implementations.Time` thành
`…Implementations.TimeSystem` (khớp `docs/Runtime/TimeSystem`) là đổi API công khai — làm thành việc riêng nếu developer chốt, không gộp
vào lần dời này.

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
| `ITutorialService.cs` › `ITutorialSpotlight.Lift(Canvas target, bool passesInput, int tier = 0)`, `ILiftedCanvas.Drop()` | hợp đồng; doc của `DimAsync`: alpha 0 thả mọi canvas đã nâng. Target **đang bật** lúc `Lift` và lúc `Drop`: consumer bật rồi mới nâng, thả rồi mới tắt |
| `TutorialSpotlight.cs` › `LiftedCanvas`, `Lift`, `DropLift`, `DropAllLifts` | tối đa 4 canvas; sorting = sorting canvas lớp phủ + 1 + `tier`; target tắt lúc `Lift` thì `LogError` rồi vẫn nâng; raycaster đặt theo `passesInput` **trước** `overrideSorting`; target đã huỷ thì bỏ qua lúc trả |
| `TutorialSpotlight.cs` › `DimAsync(0)`, `ResetNow` | gọi `DropAllLifts` |

**Quyết định** — sorting suy từ canvas lớp phủ (`rootCanvas.sortingOrder + 1`), không phải số cấu hình: nút nâng luôn ngay trên lớp tối và
dưới popup (lớp phủ 8, popup 20 ở dự án nguồn); một số cố định như 3103 của `CanvasSpotlight` vượt cả popup. — Trả về trong `finally` của
service: kịch bản bị huỷ giữa chừng không để nút kẹt sorting cao.

**Quyết định** — `tier` thay vì để Unity chọn: hai canvas Overlay cùng sorting order vẽ theo thứ tự engine tự chọn, đổi theo instance
(vật bay vào nút đè ba nút, chui dưới nút thứ tư). Thứ phải đè một canvas nâng khác lấy bậc cao hơn. **Giới hạn:** lớp phủ + 1 + `tier`
phải nhỏ hơn sorting của popup, không thì hoà với popup và lại không cố định thứ tự: chừa khe giữa lớp phủ và popup (dự án nguồn
chừa 9…19, bậc tới 10). Ghi tầng canvas của dự án trong tài liệu dự án để người sau thêm canvas không lấp khe.

**Quyết định** — `Lift` nhận target tắt chỉ `LogError`, không tự bật: sorting đặt lên canvas lồng đang tắt không ăn (bật lên vẫn vẽ dưới
lớp tối), nhưng bật hộ là đổi hành vi quan sát được của consumer. `Drop` không log: `DimAsync(0)`, `ResetNow` dọn cả target đã bị chủ tắt
hợp lệ.

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
| bậc | `Lift(a, false)`, `Lift(b, false, tier: 1)` | sorting `b` = sorting `a` + 1 |
| bậc âm | `Lift(c, false, tier: -3)` | sorting = lớp phủ + 1 |
| target tắt | `SetActive(false)` rồi `Lift(c, false)` | `LogError` nêu tên canvas; slot vẫn chiếm, `Drop` trả được |

**Mốc commit:** Horcrux `test(tutorial): case Lift`.

---

## Giai đoạn 2b — `CalloutPlacement`: đặt hộp thoại thành hàm thuần

**Mục tiêu:** phép đặt hộp thoại và mũi tên cạnh đích tách khỏi `RectTransform`, có bảng case; hộp thoại trong game y như trước trừ hai
chỗ ghi ở "Đổi hành vi".

**Vì sao bây giờ:** là phần sửa nhiều lượt nhất ở dự án nguồn (mũi tên rời đuôi, giữ trong safe area, mũi tên rời tâm hộp khi thiếu chỗ)
mà bảng case GĐ2 không phủ; phép tính lẫn với đổi đơn vị canvas → pixel trong `TutorialBubble.PlaceNextToTarget`, chỉ kiểm được bằng mắt.
Hàm không mang gì của tutorial: tooltip, khung gợi ý cạnh nút dùng lại được.

**Chỗ đặt:** `Runtime/Utilities/Layout/CalloutPlacement.cs`, namespace `Horcrux.Runtime.Utilities.Layout` (cùng chỗ
`Horcrux_ViewportAnchor_Plan.md`). Tên đổi tuỳ ý trước khi gõ.

**Quyết định** — đầu mũi tên tính **từ hộp**, không từ đích: khi cả trên lẫn dưới đều không đủ chỗ, hộp bị kẹp vào vùng; tính từ đích thì
mũi tên đứng ở mép đích, tách khỏi hộp. Ràng buộc cứng (hộp trong vùng, mũi tên dính hộp) đi trước thẩm mỹ (đầu mũi tên chạm mép đích).
— Hàm làm việc bằng pixel màn hình; đổi đơn vị là việc của component, vì chỉ nó biết canvas.

### `Runtime/Utilities/Layout/CalloutPlacement.cs` — file mới

```csharp
using UnityEngine;

namespace Horcrux.Runtime.Utilities.Layout
{
    /// <summary>Kết quả đặt một khung nổi. Mọi số là pixel màn hình.</summary>
    public readonly struct CalloutLayout
    {
        public readonly Vector2 BoxCenter;
        public readonly Vector2 ArrowTip;
        public readonly bool Above;

        public CalloutLayout(Vector2 boxCenter, Vector2 arrowTip, bool above)
        {
            BoxCenter = boxCenter;
            ArrowTip = arrowTip;
            Above = above;
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
        public static CalloutLayout Place(Rect target, Vector2 boxSize, Vector2 arrowSize, float targetGap, float arrowGap,
            float arrowCornerInset, Rect bounds, bool preferAbove)
        {
            Vector2 halfBox = boxSize * 0.5f;
            bool hasArrow = arrowSize.y > 0f;
            // Đầu mũi tên tới mép gần của hộp.
            float span = hasArrow ? arrowSize.y + arrowGap : 0f;

            float aboveY = target.yMax + targetGap + span + halfBox.y;
            float belowY = target.yMin - targetGap - span - halfBox.y;
            bool aboveFits = aboveY + halfBox.y <= bounds.yMax;
            bool belowFits = belowY - halfBox.y >= bounds.yMin;
            // Phía được xin, trừ khi chỉ phía kia vừa.
            bool above = preferAbove ? aboveFits || !belowFits : !belowFits && aboveFits;
            float boxY = ClampCentered(above ? aboveY : belowY, halfBox.y, bounds.yMin, bounds.yMax);

            float halfArrow = hasArrow ? arrowSize.x * 0.5f + arrowCornerInset : 0f;
            float arrowX = ClampCentered(target.center.x, halfArrow, bounds.xMin, bounds.xMax);
            float boxX = ClampCentered(arrowX, halfBox.x, bounds.xMin, bounds.xMax);

            // Từ hộp, không từ đích: hộp bị kẹp vào vùng thì mũi tên đi theo hộp.
            float tipY = above ? boxY - halfBox.y - span : boxY + halfBox.y + span;
            return new CalloutLayout(new Vector2(boxX, boxY), new Vector2(arrowX, tipY), above);
        }

        /// <summary>Kẹp tâm một đoạn dài 2×half vào [min, max]; đoạn dài hơn vùng thì đặt giữa vùng.</summary>
        private static float ClampCentered(float center, float half, float min, float max)
            => max - min < 2f * half ? (min + max) * 0.5f : Mathf.Clamp(center, min + half, max - half);
    }
}
```

### `TutorialBubble.cs` — field mới, `PlaceNextToTarget` cũ → mới

Field mới, dưới `arrowGap`:

```csharp
        [Tooltip("Canvas units kept between the arrow's sides and the safe area's edges, so the arrow never sits on the box's rounded corner.")]
        [SerializeField, Min(0f)] private float arrowCornerInset;
```

Cũ: cả thân `PlaceNextToTarget` (khoảng 40 dòng, từ `Rect frame = …` tới `arrow.localEulerAngles = …`).

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

            CalloutLayout layout = CalloutPlacement.Place(frame, selfRect.rect.size * toPixels,
                _hasArrow ? arrow.rect.size * toPixels : Vector2.zero, targetGap * toPixels, arrowGap * toPixels,
                arrowCornerInset * toPixels, bounds, _side == TutorialBubbleSide.Above);

            selfRect.position = layout.BoxCenter;
            if (!_hasArrow) return;
            arrow.position = layout.ArrowTip;
            arrow.localEulerAngles = new Vector3(0f, 0f, layout.Above ? 0f : 180f);
        }
```

và thêm `using Horcrux.Runtime.Utilities.Layout;`.

**Đổi hành vi** (hai chỗ, đều ở cạnh màn; ca thường y như trước):

| Ca | Trước | Sau |
|---|---|---|
| cả trên lẫn dưới không đủ chỗ | hộp kẹp vào vùng, mũi tên đứng ở mép đích, tách khỏi hộp | mũi tên dính hộp, đầu mũi tên có thể đè lên đích |
| hộp rộng hơn vùng | sát mép trái, tràn phải | giữa vùng, tràn đều hai bên |

`arrowCornerInset` mặc định 0: mũi tên vẫn có thể sát mép như trước; đặt bằng bán kính bo của hộp trên prefab.

**Editor setup:** prefab lớp phủ › `TutorialBubble` › `Arrow Corner Inset` = bán kính góc bo của ảnh hộp (đơn vị canvas). Canvas của hộp
thoại phải là **Screen Space – Overlay**: `selfRect.position` được gán bằng pixel màn hình, chỉ đúng trên Overlay.

| Case (agent viết, EditMode, `Tests/EditMode/CalloutPlacementTests.cs`) | Input (vùng `(0,0,1000,2000)`, hộp 400×100, mũi tên 40×30, `targetGap` 10, `arrowGap` 8, inset 0, trừ khi ghi khác) | Kỳ vọng |
|---|---|---|
| giữa màn | đích `(450,900,100,100)`, xin trên | `Above` true, hộp `(500,1098)`, mũi tên `(500,1010)` |
| sát mép trái | đích `(0,900,40,100)` | mũi tên x 20, hộp x 200 |
| sát mép trái, có inset | như trên, inset 24 | mũi tên x 44, hộp x 200 |
| trên không đủ chỗ | đích `(450,1900,100,80)`, xin trên | `Above` false, hộp `(500,1802)`, mũi tên `(500,1890)` |
| dưới không đủ chỗ | đích `(450,20,100,80)`, xin dưới | `Above` true, hộp y 198, mũi tên y 110 |
| cả hai không đủ | vùng `(0,0,1000,300)`, đích `(450,100,100,100)`, xin trên | `Above` true, hộp `(500,250)`, mũi tên `(500,162)` |
| không mũi tên | `arrowSize` (0,0), đích như ca đầu | hộp `(500,1060)` |
| hộp rộng hơn vùng | hộp 1200×100, đích như ca đầu | hộp x 500 |

**Kiểm:** compile sạch, bảng case xanh · chơi lượt dạy level 1: hộp thoại và mũi tên đứng như trước ở mọi bước.

**Mốc commit:** Horcrux `feat(layout): CalloutPlacement; TutorialBubble đặt hộp qua nó`.

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
preset; mục "Trước khi chạy" cho prefab lớp phủ (canvas hộp thoại là Screen Space – Overlay), neo, entry lưu; `CalloutPlacement` một
mục trong tài liệu Layout của Horcrux. Xoá Plan này sau khi tài liệu xong.

## Giai đoạn 5 — Dự án nguồn dùng bản Horcrux (agent)

Đã xảy ra ở GĐ1 (file dời, không bản song song). Còn: `Scripts/Tutorial/Tutorial.md` thu về kịch bản của dự án và trỏ tài liệu Horcrux;
grep `Tutorial/Core` trong comment, tài liệu dự án ra 0.

## Mở rộng sau

- Tay chỉ theo hướng (ngón trỏ chéo, tay đứng một phía như `HighlightConfig.HandDirection`) khi có widget mà tay đè lên nhãn.
- `DimAsync` nhận thời lượng khi một lễ cần tối dần theo nhịp riêng.
- `SpineHandTapBeat` ở lại dự án (phụ thuộc Spine). Phần không dính Spine — mốc trong anim lặp đã đi qua chưa giữa hai frame, tính cả lúc
  quấn về đầu (`Crossed(prev, now, mark)`) — lên `Runtime/Utilities` khi có người gọi thứ hai (anim Animator theo `normalizedTime`, tay
  chỉ của dự án khác).

## File → giai đoạn

| File | GĐ1 | GĐ2 | GĐ3 | GĐ4 |
|---|---|---|---|---|
| `Runtime/Abstractions/Composites/Tutorial/*` | dời vào; `BasePersistenceDataCollection.Tutorial.cs` mới | đọc | | |
| `Runtime/Implementations/Composites/Tutorial/*` | dời vào; `TutorialService.ProgressEntry` | đọc | | |
| `Shader/ui_spotlight_dim.shader`, `ui_tap_ripple.shader` | dời vào | | | |
| `Tests/EditMode/TutorialLiftTests.cs` (agent) | | tạo | | |
| `Runtime/Utilities/Layout/CalloutPlacement.cs`; `TutorialBubble.cs` › `PlaceNextToTarget` | | GĐ2b | | |
| `Tests/EditMode/CalloutPlacementTests.cs` (agent) | | GĐ2b | | |
| `CanvasSpotlight` (+ tài liệu) | | | xoá | |
| `docs/Runtime/Tutorial/*` (agent) | | | | tạo |
| dự án: `Scripts/Tutorial/Core/` | xoá | | | |
