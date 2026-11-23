# Plan — `Tutorial`: một lớp tối, tay chỉ, hộp thoại, tiến độ cho mọi lượt dạy; gộp `CanvasSpotlight`

Bản tạm: category-jam `Assets/_CategoryJam/Scripts/Tutorial/Core/` (namespace đã là Horcrux, nằm nhờ `CategoryJam.Gameplay`), tài liệu `Scripts/Tutorial/Tutorial.md`.
**Phụ thuộc:** không — gõ lúc nào cũng được; GĐ3 chờ dự án consumer `ICanvasSpotlight` — category-jam không gọi (grep ra 0), developer chỉ tên dự án.
Kiểm (từ gốc dự án): `bash "Assets/Horcrux/Editor/CompileCheck/Tools~/verify.sh" --csproj com.horcrux.runtime.csproj,CategoryJam.Gameplay.csproj,Assembly-CSharp.csproj --tests Horcrux.EditModeTests,CategoryJam.EditModeTests`.

## Kịch bản

| Tình huống | Hệ làm gì | Caller/người chơi thấy |
|---|---|---|
| kịch bản chạy qua `PlayAsync` | bật canvas lớp phủ, chạy `RunAsync`, xong ghi `Id` vào tiến độ | lượt dạy một lần; mở lại app không dạy lại |
| kịch bản `Id` trống | chạy, không ghi tiến độ | lượt dạy lặp lại mỗi lần gọi |
| kịch bản bị huỷ, người chơi thoát level giữa lượt | `finally` đưa lỗ, canvas nâng, lớp tối, tay, hộp thoại về nghỉ trong một frame | không kẹt tối, không nút kẹt trên lớp tối |
| đích world (tile, box) | `Open`: shader khoét lỗ trên lớp tối | đích thấy qua lỗ |
| đích UI Overlay (nút HUD, widget) | `Lift(canvas, passesInput, tier)`: canvas lồng vẽ đè lớp tối | nút như chính nó, không viền lỗ; bấm được khi `passesInput` |
| hai canvas nâng phải đè nhau | canvas `tier` cao vẽ trên | thứ tự cố định mọi instance |
| `Lift` lần 5 | `LogError`, trả null | canvas thứ 5 chìm dưới lớp tối |
| `Lift` canvas đang tắt, hay thiếu `GraphicRaycaster` | `LogError` nêu tên canvas, vẫn nâng | lỗi setup lộ trong Console |
| `DimAsync(0)`, `ResetNow` | thả mọi canvas đã nâng, bỏ qua target đã huỷ | ba giá trị canvas về như trước `Lift` |
| hai chủ cùng giữ cửa input (`TutorialGate`) | item qua khi mọi luật nhận; `Drop` gỡ đúng luật của chủ gọi | nhả của bên này không mở cửa của bên kia |
| cửa không còn luật | qua, không bắn `Passed`, `Rejected` | input như ngoài lượt dạy |
| hộp thoại, phía xin vừa | đặt hộp phía xin, mũi tên trên tâm đích | hộp và mũi tên cạnh đích |
| phía xin không vừa, phía kia vừa | đổi phía | hộp phía kia |
| cả hai phía không vừa | kẹp hộp vào vùng, mũi tên đi theo hộp | mũi tên dính hộp, đầu có thể đè đích (đổi so với bản tạm: mũi tên đứng ở mép đích) |
| đích sát mép trái | kẹp mũi tên cách mép `arrowCornerInset` | mũi tên không rơi vào góc bo hộp |
| hộp rộng hơn vùng | đặt giữa vùng | tràn đều hai bên (đổi so với bản tạm: sát trái, tràn phải) |
| key `tutorial_progress` trống trên asset collection | tiến độ không lưu, không log | lượt dạy Level 1 lặp lại mỗi lần mở app |

## Flow

```
Dự án: kịch bản (ATutorialScript, Id) ──► ITutorialService.PlayAsync(script, ct)                   Implementations/…/TutorialService.cs
  ├─ bật canvas lớp phủ, lấy Camera.main
  ├─ script.RunAsync
  │    ├─ Spotlight.DimAsync · Open (lỗ, shader ui_spotlight_dim)                                   TutorialSpotlight.cs
  │    │    └─ Lift ──► LiftedCanvas ──► ILiftedCanvas.Drop · DimAsync(0)/ResetNow ──► DropAllLifts   TutorialSpotlight.cs › Lift, DropLift
  │    ├─ Hand.PointAt(TutorialAnchor) (shader ui_tap_ripple)                                       TutorialHand.cs
  │    ├─ Bubble.Show ──LateUpdate──► PlaceNextToTarget ──px──► CalloutPlacement.Place ──► CalloutLayout
  │    │     TutorialBubble.cs (TutorialAnchor.GetScreenFrame, Screen.safeArea)      Utilities/Layout/CalloutPlacement.cs
  │    └─ Gate.Restrict(rule) ──► Restriction.Change/Drop ; cửa input game ──► Allows/Accepts        Abstractions/…/TutorialGate.cs
  ├─ xong: Id ──► ProgressEntry ──► BasePersistenceDataCollection.TutorialProgress    Abstractions/…/BasePersistenceDataCollection.Tutorial.cs
  └─ finally: lỗ, canvas nâng, lớp tối, tay, hộp thoại về nghỉ

Abstractions/… = Runtime/Abstractions/Composites/Tutorial/ (ITutorialService, ATutorialScript, TutorialAnchor, TutorialGate, TutorialProgress)
Implementations/… = Runtime/Implementations/Composites/Tutorial/ · shader ở Shader/ · dự án giữ kịch bản, SpineHandTapBeat, prefab lớp phủ, neo
```

## Thuật toán

| Lõi | Công thức, bất biến |
|---|---|
| sorting canvas nâng | `overlay.sortingOrder + 1 + max(0, tier)`; bất biến `overlay + 1 + tier < sorting popup` — dự án chừa khe, ghi tầng canvas trong tài liệu dự án |
| `Lift` / `Drop` | nhớ `overrideSorting`, `sortingOrder`, `raycaster.enabled`; raycaster đặt theo `passesInput` **trước** `overrideSorting`; `Drop` trả đủ ba; tối đa 4 (`MaxLiftAmount`) |
| `TutorialGate.Allows(x)` | `∀ r ∈ restrictions: r(x)`; rỗng → true, không bắn sự kiện; `Accepts` cùng phép, im lặng |
| `CalloutPlacement` khoảng đứng | `span = arrow.h + arrowGap` (0 khi không mũi tên) · `aboveY = target.yMax + targetGap + span + box.h/2` · `belowY = target.yMin − targetGap − span − box.h/2` |
| chọn phía | xin `Above`: `aboveFits ∨ ¬belowFits` · xin `Below`: `¬belowFits ∧ aboveFits` — cả hai không vừa thì giữ phía xin |
| kẹp | `ClampCentered(c, h, min, max)` = giữa vùng khi `max − min < 2h`, không thì `clamp(c, min + h, max − h)`; thứ tự: `boxY` → `arrowX` (h = `arrow.w/2 + inset`) → `boxX` quanh `arrowX` → đầu mũi tên tính từ hộp |

Bản mẫu: C# trên mono của Unity với `UnityEngine.CoreModule`, 9 ca bảng GĐ2, 24 giá trị khớp; tính đầu mũi tên từ đích làm ca "cả hai không đủ" đỏ.

---

## GĐ1 — xong thì mọi dự án dùng Horcrux chạy được lượt dạy, tiến độ lưu qua collection gốc

```
  dự án: kịch bản ──► Horcrux Composites/Tutorial+ (lõi dời từ dự án, cùng GUID, cùng namespace)
                         TutorialService.ProgressEntry ──► BasePersistenceDataCollection.TutorialProgress+ (partial mới)
  dự án: Scripts/Tutorial/Core/ ── xoá
```

| Từ (`Assets/_CategoryJam/Scripts/Tutorial/Core/`) | Tới (`Assets/Horcrux/`) |
|---|---|
| `Abstractions/*.cs` (5 file) | `Runtime/Abstractions/Composites/Tutorial/` |
| `Implementations/*.cs` (4 file) | `Runtime/Implementations/Composites/Tutorial/` |
| `Shader/ui_spotlight_dim.shader`, `ui_tap_ripple.shader` | `Shader/` |
| `GamePersistenceDataCollection.Tutorial.cs` | thay bằng `Runtime/Abstractions/Composites/Tutorial/BasePersistenceDataCollection.Tutorial.cs` (dưới) |

**Editor setup**

1. Chép từng file **kèm `.meta`** sang Horcrux theo bảng, xoá bản dự án trong cùng lần refresh — hai repo, `git mv` không qua ranh giới; Unity thấy hai bản cùng GUID thì prefab `tutorial_overlay`, scene, material trỏ lạc.
2. `Resources/Config/GamePersistenceDataCollection` › mục Tutorial › `Key` vẫn là `tutorial_progress`; `git diff` asset không đổi key.

`TutorialService.cs` › `Properties` — cũ:

```csharp
        // Temporary home of the entry: moves to a BasePersistenceDataCollection partial once the system is in Horcrux.
        private PersistenceDataEntry<TutorialProgress> ProgressEntry
            => ((GamePersistenceDataCollection)_saveCollection).TutorialProgress;
```

Mới, và bỏ `using CategoryJam.Gameplay.Config.Persistence;`:

```csharp
        private PersistenceDataEntry<TutorialProgress> ProgressEntry => _saveCollection.TutorialProgress;
```

`Runtime/Abstractions/Composites/Tutorial/BasePersistenceDataCollection.Tutorial.cs` — file mới:

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
        // Name is the asset's YAML key: renaming drops the filled Key and progress silently stops saving.
        private PersistenceDataEntry<TutorialProgress> tutorialProgress;

        public PersistenceDataEntry<TutorialProgress> TutorialProgress => tutorialProgress;
    }
}
```

`Lift` và `TutorialGate` đã có trong file vừa dời, không gõ mới; đọc theo Thuật toán. `Tests/EditMode/TutorialLiftTests.cs` (agent):

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

---

## GĐ2 — xong thì khung nổi nào (hộp thoại, tooltip, gợi ý cạnh nút) cũng đặt cạnh đích trong safe area bằng một hàm thuần

```
  TutorialBubble.LateUpdate ──► PlaceNextToTarget ──px──► CalloutPlacement.Place+ ──► CalloutLayout+ ──► selfRect, arrow
                                (đổi canvas → pixel, safe area)          (CalloutSide+)
```

`TutorialBubble.cs` — thêm `using Horcrux.Runtime.Utilities.Layout;` sau `using Horcrux.Runtime.Utilities;`; field mới dưới `arrowGap`:

```csharp
        [Tooltip("Canvas units between the arrow's tail and the box's edge.")]
        [SerializeField, Min(0f)] private float arrowGap = 8f;
        [Tooltip("Canvas units kept between the arrow's sides and the safe area's edges, so the arrow never sits on the box's rounded corner.")]
        [SerializeField, Min(0f)] private float arrowCornerInset;
```

`TutorialBubble.cs` › `PlaceNextToTarget` — thay cả thân (từ `Rect frame = …` tới `arrow.localEulerAngles = …`):

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

`Runtime/Utilities/Layout/CalloutPlacement.cs` — file mới:

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

**Editor setup**

1. Prefab lớp phủ › `TutorialBubble` › `Arrow Corner Inset` = bán kính góc bo của ảnh hộp (đơn vị canvas); để 0 thì mũi tên vẫn sát mép như bản tạm.
2. Canvas của hộp thoại là **Screen Space – Overlay** — `selfRect.position` gán bằng pixel màn hình; canvas khác thì hộp lệch khỏi đích, không log.

`Tests/EditMode/CalloutPlacementTests.cs` (agent):

| Case | Input (vùng `(0,0,1000,2000)`, hộp 400×100, mũi tên 40×30, `targetGap` 10, `arrowGap` 8, inset 0, trừ khi ghi khác) | Kỳ vọng |
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

---

## GĐ3 — xong thì widget Home dạy bằng cùng lớp tối, tay, hộp thoại với gameplay; `CanvasSpotlight` xoá

```
  kịch bản dạy widget+ (ATutorialScript) ──► ITutorialService: DimAsync · Lift(widget, true) · Hand.PointAt · Bubble
  CanvasSpotlight ── xoá
```

Dự án consumer — mỗi luồng gọi `ICanvasSpotlight` thành một class con `ATutorialScript` (`Id` = khoá lưu khi chỉ dạy một lần), gọi trong `RunAsync`:

| `ICanvasSpotlight` | `ITutorialService` |
|---|---|
| `Focus(target, config)` | `Spotlight.DimAsync(alpha, ct)` + `Spotlight.Lift(target, true)` + `Hand.PointAt(anchor)` — đích cần thêm `TutorialAnchor` (`Space` ScreenOverlay) |
| `Focus(target, extra, config)` | thêm `Lift(extra, …)` lần hai |
| `HighlightConfig.NoHand` | không gọi `Hand` |
| `FadeDimAsync(to, duration, ct)` | `Spotlight.DimAsync(to, ct)`: thời lượng là `transitionSeconds` của service |
| `Release()` | `Drop()` từng handle, hoặc để `PlayAsync` dọn khi kịch bản xong |
| chờ tap nút thật | giữ cách của consumer (`OnClickAsync`), nút đã nâng với `passesInput` true |

**Editor setup (dự án consumer)**

1. Gỡ instance `canvas_spotlight` khỏi `Services.unity`.
2. Thêm một instance prefab lớp phủ tutorial: Canvas Overlay, sort order trên HUD, dưới popup.
3. Mỗi widget được dạy: `Canvas` + `Graphic Raycaster` (bật) + `Tutorial Anchor`. Thiếu `Graphic Raycaster` bật thì widget sáng mà không bấm được (Console có `LogError` của `Lift`).
4. Grep `ICanvasSpotlight` trên mọi dự án dùng Horcrux ra 0 thì xoá `Runtime/Abstractions/Foundations/CanvasSpotlight/`, `Runtime/Implementations/Foundations/Spotlight/`, `docs/Runtime/CanvasSpotlight/`; chưa ra 0 thì giữ tới lúc đó.

| Case (chơi luồng dạy widget) | Input | Kỳ vọng |
|---|---|---|
| vào lượt dạy | tới bước dạy widget | màn tối một lớp, widget sáng, bấm được, tay bám widget |
| thoát giữa chừng | rời màn giữa lượt | không kẹt tối, widget về sorting cũ |
| trước khi xoá | grep `ICanvasSpotlight` | 0 |

---

## Chuyển dự án (agent)

| Chỗ | Đổi |
|---|---|
| `docs/Runtime/Tutorial/Tutorial.md`, `.html` | tạo từ `Scripts/Tutorial/Tutorial.md` của dự án, bỏ kịch bản Level 1, tên dự án, số preset · "Trước khi chạy": prefab lớp phủ (canvas hộp thoại Overlay), neo, entry lưu · `TutorialGate` nhiều chủ · tầng sorting của `Lift` và khe trước popup |
| `docs/Runtime/CalloutPlacement/CalloutPlacement.md`, `.html` | tạo, `Code: Runtime/Utilities/Layout`: ràng buộc cứng trước thẩm mỹ, bảng case GĐ2 làm ví dụ, pixel màn hình và canvas Overlay |
| dự án `Assets/_CategoryJam/Scripts/Tutorial/Tutorial.md` | thu về kịch bản của dự án và tầng canvas, trỏ `docs/Runtime/Tutorial/` |
| grep | `CategoryJam` trong `Runtime/**/Tutorial/` ra 0 · `Tutorial/Core` trong comment, tài liệu dự án ra 0 |
| Plan này | xoá cùng `.meta` khi GĐ1–3 xong; GĐ3 còn chờ dự án consumer thì chuyển GĐ3 sang task riêng rồi xoá |
| chơi thử | theo Kịch bản: Level 1 save mới (lỗ trên tile, box, tay, hộp thoại mỗi bước) · level mở khoá booster (nút nổi bấm được, chỗ khác bị nuốt) · đích sát mép · thoát giữa lượt · mở lại app không dạy lại; hỏng: hộp lệch đích, nút chìm, màn kẹt tối |

## Không làm · Mở rộng sau

| | Phương án | Vì sao |
|---|---|---|
| bỏ | hàng đợi nhiều kịch bản · tracking · bật tắt từ remote config | giữ như tài liệu dự án đã chốt |
| bỏ | rút `UnityEngine.Time` thành `Time.` trong `TutorialHand`, `TutorialService` | namespace anh em `Horcrux.Runtime.Implementations.Time` chiếm tên, lỗi CS0234 |
| bỏ | đổi namespace `…Implementations.Time` thành `…TimeSystem` trong lần dời | đổi API công khai — việc riêng nếu developer chốt |
| bỏ | sorting canvas nâng là số cấu hình (3103 như `CanvasSpotlight`) | số cố định vượt cả popup; suy từ lớp phủ thì luôn ngay trên lớp tối, dưới popup |
| bỏ | trả canvas nâng ở cuối kịch bản, không ở `finally` của service | kịch bản huỷ giữa chừng để nút kẹt sorting cao |
| bỏ | để Unity xếp hai canvas nâng cùng sorting (không `tier`) | hai canvas Overlay cùng sorting vẽ theo thứ tự engine chọn, đổi theo instance |
| bỏ | tắt raycaster canvas lồng lúc nghỉ (hợp đồng `CanvasSpotlight`) | graphic thuộc canvas lồng gần nhất đang bật, raycaster cha không quét nó: nút chết ngoài lượt dạy |
| bỏ | `Lift` tự bật target đang tắt | bật hộ đổi hành vi quan sát được của consumer; sorting trên canvas tắt không ăn nên `LogError` |
| bỏ | `Drop` log khi target tắt | `DimAsync(0)`, `ResetNow` dọn cả target chủ đã tắt hợp lệ |
| bỏ | `TutorialGate` một khe luật chung | chủ ghi sau đè luật chủ trước, nhả của bên này gỡ luôn luật bên kia |
| bỏ | `CalloutPlacement` nhận đơn vị canvas | chỉ component biết canvas; hàm làm việc bằng pixel màn hình |
| bỏ | `bool preferAbove` | hai phía loại trừ đọc như cờ ở call site |
| bỏ | dùng thẳng `TutorialBubbleSide` trong `CalloutPlacement` | `Utilities` không phụ thuộc hệ Tutorial |
| bỏ | struct spec gom ba khoảng | constructor ba `float` vẫn lộn được, thêm type công khai cho cùng tác dụng |
| bỏ | `ScreenWidth` (`Horcrux_ScreenSize_Plan.md`) đổi canvas → pixel cho hộp thoại | canvas hộp thoại bắt buộc Overlay, `lossyScale.x` của root đủ |
| bỏ | giữ `CanvasSpotlight` làm lớp mỏng gọi sang service | hai API cho một việc, consumer mới không biết chọn bên nào |
| bỏ | sửa `LogError` thừa ở `FocusCore`, NRE ở `Capture` của `CanvasSpotlight` | đi theo file bị xoá |
| sau | tay chỉ theo hướng (`HighlightConfig.HandDirection`: ngón trỏ chéo, tay đứng một phía) | khi có widget mà tay bám neo đè lên nhãn |
| sau | `DimAsync` nhận thời lượng | khi một lễ cần tối dần theo nhịp riêng |
| sau | phần không dính Spine của `SpineHandTapBeat` (dự án): `Crossed(prev, now, mark)` — mốc anim lặp đã qua giữa hai frame, tính cả lúc quấn về đầu — lên `Runtime/Utilities` | khi có người gọi thứ hai (Animator theo `normalizedTime`, tay chỉ của dự án khác) |
