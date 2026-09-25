# CanvasHighlightTutorial — tối màn, nâng một canvas lên trên, tay chỉ

Hệ **độc lập** (NT3): chỉ phụ thuộc Unity UI và InitArgs, không gọi tên type nào của dự án. Consumer đầu tiên:
Collection LiveOps (`CollectionHomeFlow.RunTutorialAsync`). Gameplay 3D **không** thuộc hệ này, tách riêng sau.

Chuỗi sự thật: `ICanvasHighlightTutorial.cs` · `CanvasHighlightTutorial.cs` · `Direction.cs` → file này → `CanvasHighlightTutorial.html`
(dựng ở mốc có code chạy).

## 0. Một câu

Khi diễn tutorial, một canvas phủ tối toàn màn ở sorting **cao nhất**, và **target là một `Canvas`** được nâng
`sortingOrder` lên trên lớp tối trong lúc đó, nên nó vừa sáng vừa nhận tap; mọi thứ khác bị lớp tối nuốt.
Thả ra thì target về đúng sorting cũ.

## 1. Hợp đồng target: một `Canvas` + một `GraphicRaycaster`, dựng sẵn trên prefab

| Phần | Vì |
|---|---|
| Object được highlight mang **`Canvas`** riêng (nested), `overrideSorting` **tắt** lúc nghỉ | nâng lên trên lớp tối là đổi `sortingOrder` của chính nó; không có Canvas thì không có gì để đổi. Chữ ký `Focus(Canvas target, …)` ép hợp đồng này **bằng kiểu**, không bằng `GetComponent` lúc chạy (§3.4, §3.6) |
| Cùng object mang **`GraphicRaycaster`** | `Graphic` đăng ký với Canvas **gần nhất**; canvas nested không có raycaster riêng thì không nhận tap dù đang vẽ trên cùng. Thiếu → `LogError` ở `Focus`, không `AddComponent` (Editor-first) |
| Root canvas của target là **ScreenSpaceOverlay** | canvas nested kế thừa render mode của root; Overlay so `sortingOrder` với nhau, còn Overlay luôn vẽ trên mọi canvas Camera. Target dưới canvas Camera **không nâng được** lên trên lớp tối Overlay — đó là lý do gameplay tách riêng |

Cái giá của hợp đồng: object có Canvas riêng là một batch riêng **vĩnh viễn**, không chỉ lúc tutorial. Người dựng
prefab chọn có ý thức, đặt Canvas ở đúng cụm cần sáng (cả widget, hay chỉ một nút).

## 2. Cơ chế

```
CanvasHighlightTutorial.prefab            Overlay · sortingOrder = highlightOrder (mặc định 5000)
├─ Canvas + GraphicRaycaster              tắt khi nghỉ → không draw, không raycast
├─ dim        Image toàn màn              Raycast Target BẬT → nuốt mọi tap ngoài target
├─ frame      Image 9-slice, viền sáng    Raycast Target TẮT · bám rect target + padding
└─ hand       Image                       Raycast Target TẮT · bám một góc/mép target theo Direction, xoay theo hướng, nhấp theo sin

Focus(target, style):
  1. đang Focus target khác → Release() trước (khôi phục sorting của nó)
  2. nhớ (target.overrideSorting, target.sortingOrder)
  3. target.overrideSorting = true · target.sortingOrder = highlightOrder + 1
  4. bật canvas mình · frame/hand theo style
LateUpdate (chỉ khi đang Focus):
  rect target → 4 góc world → màn hình → local của canvas mình · frame = rect + padding · hand = điểm neo theo Direction + sin
Release():
  khôi phục (overrideSorting, sortingOrder) · tắt canvas mình · quên target
```

- **Toạ độ**: cả hai đều Overlay nên `GetWorldCorners` của target đã là toạ độ màn hình;
  `RectTransformUtility.ScreenPointToLocalPointInRectangle(selfRect, p, null)` đưa về local của mình. Bốn góc
  lấy min/max để không phụ thuộc pivot/scale của target.
- **Bám mỗi frame** vì widget có animation và Main Screen fade: bốn phép chiếu, không cấp phát (§3.3).
- **Tay đặt theo `Direction`** (`Utilities/Common/Direction.cs`, sáu vị trí Top/Bottom × Left/Center/Right): điểm neo là góc hoặc
  trung điểm mép tương ứng của rect target, đẩy ra `handDistance`; `eulerAngles.z = direction.GetEulerAngleZ()` để ngón
  tay trỏ vào target. **Nhấp bằng `Mathf.Sin(Time.unscaledTime * freq) * amp`** dọc hướng trỏ, trong cùng `LateUpdate`, không tween: nhịp lặp vô hạn
  không phải việc của `Charm*` (một property về đích), và một dòng sin không cần huỷ.
- **Không đụng gì khác của target**: không thêm component, không đổi cha, không đổi vị trí. Khôi phục chỉ là hai
  field.

## 3. API

```csharp
namespace Horcrux.Runtime.Abstractions.Highlight
{
    // Direction (Utilities/Common): TopLeft · TopRight · TopCenter · BottomLeft · BottomRight · BottomCenter.
    public readonly struct HighlightStyle
    {
        public readonly bool ShowHand;
        public readonly Direction HandAt;   // góc/mép của target mà tay đứng; tay xoay theo GetEulerAngleZ()
        public readonly float Padding;      // frame lớn hơn rect target bấy nhiêu, đơn vị canvas

        public static readonly HighlightStyle Default = new(Direction.BottomCenter, 12f);
        public static readonly HighlightStyle NoHand = new(false, Direction.BottomCenter, 12f);

        public HighlightStyle(Direction handAt, float padding) : this(true, handAt, padding) { }

        public HighlightStyle(bool showHand, Direction handAt, float padding)
        {
            ShowHand = showHand;
            HandAt = handAt;
            Padding = padding;
        }
    }

    public interface ICanvasHighlightTutorial : IService<ICanvasHighlightTutorial>
    {
        bool IsFocusing { get; }

        // Dim everything, lift target above the dim, point at it. Focusing again replaces the target.
        void Focus(Canvas target, in HighlightStyle style);

        // Restore target sorting and hide. No-op when idle.
        void Release();
    }
}
```

`Direction` dùng chung với phần khác của Horcrux nên không có enum riêng cho tay; `ShowHand` là cờ riêng vì `Direction`
không có giá trị "không" và không nên thêm một phần tử không phải hướng vào nó.

Service **không biết click**: tap đi thẳng vào nút thật dưới target, caller tự `await button.OnClickAsync(ct)` rồi
`Release()`. Một trách nhiệm, không ép caller vào một kiểu chờ. Cần nhãn chữ sau thì thêm field vào
`HighlightStyle`, API không đổi.

## 4. Code

Ba file. `Direction` đã có ở `Utilities/Common`, chỉ thêm thân hai extension.

```csharp
// Utilities/Common/Direction.cs — thêm vào DirectionExtensions
using UnityEngine;

namespace Horcrux.Runtime.Utilities.Common
{
    public enum Direction
    {
        TopLeft,
        TopRight,
        TopCenter,
        BottomLeft,
        BottomRight,
        BottomCenter,
    }

    public static class DirectionExtensions
    {
        private static readonly float Diagonal = 1f / Mathf.Sqrt(2f);

        // Unit vector from a hand standing at this side toward the target center.
        public static Vector2 ToTargetVector(this Direction direction)
        {
            switch (direction)
            {
                case Direction.TopLeft:      return new Vector2(Diagonal, -Diagonal);
                case Direction.TopRight:     return new Vector2(-Diagonal, -Diagonal);
                case Direction.TopCenter:    return Vector2.down;
                case Direction.BottomLeft:   return new Vector2(Diagonal, Diagonal);
                case Direction.BottomRight:  return new Vector2(-Diagonal, Diagonal);
                default:                     return Vector2.up;            // BottomCenter
            }
        }

        // Sprite drawn pointing up (finger up, z = 0). Rotation that makes it point along ToTargetVector.
        public static float GetEulerAngleZ(this Direction direction)
        {
            Vector2 v = direction.ToTargetVector();
            return Mathf.Atan2(v.y, v.x) * Mathf.Rad2Deg - 90f;
        }
    }
}
```

| Direction | Tay đứng ở | Trỏ về | `GetEulerAngleZ` |
|---|---|---|---|
| BottomCenter | dưới, giữa | lên | 0 |
| TopCenter | trên, giữa | xuống | −180 |
| BottomLeft | dưới trái | lên phải | −45 |
| BottomRight | dưới phải | lên trái | 45 |
| TopLeft | trên trái | xuống phải | −135 |
| TopRight | trên phải | xuống trái | −225 |

```csharp
// Abstractions/Foundations/Highlight/ICanvasHighlightTutorial.cs
using Horcrux.Runtime.Utilities.Common;
using Sisus.Init;
using UnityEngine;

namespace Horcrux.Runtime.Abstractions.Highlight
{
    public readonly struct HighlightStyle
    {
        public readonly bool ShowHand;
        public readonly Direction HandAt;   // side of the target the hand stands on; it rotates to point at the target
        public readonly float Padding;      // frame grows this far past the target rect, canvas units

        public static readonly HighlightStyle Default = new(Direction.BottomCenter, 12f);
        public static readonly HighlightStyle NoHand = new(false, Direction.BottomCenter, 12f);

        public HighlightStyle(Direction handAt, float padding) : this(true, handAt, padding) { }

        public HighlightStyle(bool showHand, Direction handAt, float padding)
        {
            ShowHand = showHand;
            HandAt = handAt;
            Padding = padding;
        }
    }

    public interface ICanvasHighlightTutorial : IService<ICanvasHighlightTutorial>
    {
        bool IsFocusing { get; }

        // Dim everything, lift target above the dim, point at it. Focusing again replaces the target.
        void Focus(Canvas target, in HighlightStyle style);

        // Restore target sorting and hide. No-op when idle.
        void Release();
    }
}
```

```csharp
// Implementations/Foundations/Highlight/CanvasHighlightTutorial.cs
using Horcrux.Runtime.Abstractions.Highlight;
using Horcrux.Runtime.Utilities;
using Horcrux.Runtime.Utilities.Common;
using Sisus.Init;
using UnityEngine;
using UnityEngine.UI;

namespace Horcrux.Runtime.Implementations.Highlight
{
    // Targets are nested Canvases (+ GraphicRaycaster) authored on their prefabs. Overlay roots only.
    [Service(typeof(ICanvasHighlightTutorial), FindFromScene = true)]
    public sealed class CanvasHighlightTutorial : MonoBehaviour, ICanvasHighlightTutorial
    {
        [Splitter("References")]
        [SerializeField] private Canvas selfCanvas;
        [SerializeField] private RectTransform selfRect;
        [SerializeField] private RectTransform frame;
        [SerializeField] private RectTransform hand;      // pivot at the fingertip

        [Splitter("Configs")]
        [SerializeField, Min(1)] private int highlightOrder = 5000;
        [SerializeField, Min(0f)] private float handDistance = 24f;
        [SerializeField, Min(0f)] private float handBobAmplitude = 14f;
        [SerializeField, Min(0.1f)] private float handBobFrequency = 4f;

        private Canvas _target;
        private bool _targetOverrideSorting;
        private int _targetSortingOrder;
        private HighlightStyle _style;
        private Vector2 _handToTarget;
        private readonly Vector3[] _corners = new Vector3[4];

        #region Properties

        public bool IsFocusing => _target != null;

        #endregion

        #region Unity Callbacks

        private void Awake()
        {
            selfCanvas.sortingOrder = highlightOrder;
            selfCanvas.enabled = false;
        }

        private void LateUpdate()
        {
            if (_target == null)
                return;

            Follow();
        }

        #endregion

        #region API

        public void Focus(Canvas target, in HighlightStyle style)
        {
            if (_target != null)
                Release();

            if (target.GetComponent<GraphicRaycaster>() == null)
                Debug.LogError($"[CanvasHighlightTutorial]: {target.name} has no GraphicRaycaster; it draws above the dim but never receives taps.", target);

            _target = target;
            _style = style;
            _handToTarget = style.HandAt.ToTargetVector();
            _targetOverrideSorting = target.overrideSorting;
            _targetSortingOrder = target.sortingOrder;

            target.overrideSorting = true;
            target.sortingOrder = highlightOrder + 1;

            hand.gameObject.SetActive(style.ShowHand);
            hand.localEulerAngles = new Vector3(0f, 0f, style.HandAt.GetEulerAngleZ());
            selfCanvas.enabled = true;

            // Same frame as Focus: no one-frame pop at the previous position.
            Follow();
        }

        public void Release()
        {
            if (_target == null)
                return;

            _target.overrideSorting = _targetOverrideSorting;
            _target.sortingOrder = _targetSortingOrder;
            _target = null;
            selfCanvas.enabled = false;
        }

        #endregion

        #region Class Methods

        private void Follow()
        {
            Rect r = TargetRectInSelf();

            frame.anchoredPosition = r.center;
            frame.sizeDelta = r.size + Vector2.one * (_style.Padding * 2f);

            if (!_style.ShowHand)
                return;

            float bob = Mathf.Sin(Time.unscaledTime * handBobFrequency) * handBobAmplitude;
            hand.anchoredPosition = AnchorOn(r, _style.HandAt) - _handToTarget * (handDistance + bob);
        }

        // Both canvases are Overlay, so world corners are already screen points.
        private Rect TargetRectInSelf()
        {
            ((RectTransform)_target.transform).GetWorldCorners(_corners);
            Vector2 min = new(float.MaxValue, float.MaxValue);
            Vector2 max = new(float.MinValue, float.MinValue);

            for (int i = 0; i < 4; i++)
            {
                RectTransformUtility.ScreenPointToLocalPointInRectangle(selfRect, _corners[i], null, out Vector2 p);
                min = Vector2.Min(min, p);
                max = Vector2.Max(max, p);
            }

            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }

        private static Vector2 AnchorOn(in Rect r, Direction side)
        {
            switch (side)
            {
                case Direction.TopLeft:      return new Vector2(r.xMin, r.yMax);
                case Direction.TopRight:     return new Vector2(r.xMax, r.yMax);
                case Direction.TopCenter:    return new Vector2(r.center.x, r.yMax);
                case Direction.BottomLeft:   return new Vector2(r.xMin, r.yMin);
                case Direction.BottomRight:  return new Vector2(r.xMax, r.yMin);
                default:                     return new Vector2(r.center.x, r.yMin);   // BottomCenter
            }
        }

        #endregion
    }
}
```

`LogError` rồi **vẫn** highlight: nhìn thấy nút sáng mà không tap được là dấu hiệu rõ hơn một tutorial không hiện gì;
lỗi lộ ở lần Play đầu, không im lặng.

### Prefab `CanvasHighlightTutorial.prefab`

| Object | Component | Thiết lập |
|---|---|---|
| root | `Canvas` · `CanvasScaler` · `GraphicRaycaster` · `CanvasHighlightTutorial` | Overlay; `sortingOrder` do `Awake` gán từ `highlightOrder`; Scaler cùng tham chiếu với canvas UI của dự án (1080×1920, match 0.5); root **active** |
| `dim` | `Image` stretch toàn màn | màu đen alpha 0.7; **Raycast Target bật** |
| `frame` | `Image` 9-slice viền sáng | anchor giữa, pivot (0.5, 0.5); **Raycast Target tắt** |
| `hand` | `Image` sprite tay trỏ **lên** | pivot ở **đầu ngón**; **Raycast Target tắt**; mặc định tắt |

Kéo bốn ô References; `selfRect` là `RectTransform` của root. Prefab kéo vào scene Services của dự án, một instance.

## 5. Bảng quyết định

| Quyết định | Vì |
|---|---|
| Nâng sorting của target, không khoét lỗ trên lớp tối | developer chốt: target và lớp tối cùng là Canvas, đổi sorting là đúng cơ chế của uGUI, không cần shader, không cần `ICanvasRaycastFilter`. Đổi lại phải chấp nhận hợp đồng §1 và giới hạn Overlay |
| Chữ ký nhận `Canvas`, không `RectTransform` hay `GameObject` | hợp đồng "target là canvas" hiện ở kiểu tham số; caller không có Canvas thì **không compile**, thay vì `GetComponent` null lúc chạy (§3.4 bảng guard) |
| Không `AddComponent<Canvas>` / `<GraphicRaycaster>` lúc chạy | Editor-first (§3.6): thứ cần có trên prefab thì dựng trên prefab; thêm lúc chạy là dựng hierarchy bằng code, phải nhớ gỡ, và `TutorialManager` cũ của dự án đang trả giá đúng chỗ đó |
| `Focus` khi đang `Focus` → thay target | tutorial luôn tuần tự (nút này rồi nút kia); hàng đợi là một hệ không ai cần (NT1). Thay thì `Release` cái cũ trước để không để lại canvas nào bị nâng |
| `Release` idle là no-op | caller đặt `Release()` trong `finally` mà không cần biết đã `Focus` chưa |
| Canvas mình `enabled = false` khi nghỉ, không `SetActive(false)` root | root luôn active để `[Service(FindFromScene)]` và `LateUpdate` ổn định; tắt Canvas là đủ để không draw, không raycast |
| `highlightOrder` mặc định 5000, serialize `[Min(1)]` | trên Toast (102) và mọi canvas UI của dự án; canvas tutorial gameplay cũ của dự án (32766) vẫn cao hơn nhưng chỉ sống trong level. Dự án khác chỉnh trong Inspector |
| Service `MonoBehaviour` + prefab kéo vào scene Services, `FindFromScene = true` | cần `LateUpdate` và một mốc tắt tin được (§3.1 bảng "là gì"); cùng cách đăng ký với `RewardService`, `TimeService` |
| Consumer nhận qua `IInitializable<…, ICanvasHighlightTutorial>` | phụ thuộc nằm ở chữ ký; Collection đã có Initializer, thêm một tham số |

## 6. Cái sai lộ ra lúc nào

| Sai | Lộ |
|---|---|
| Target không có `Canvas` | không compile ở call site |
| Target có `Canvas` không có `GraphicRaycaster` | `LogError` ở `Focus`; nút sáng nhưng tap không vào, tutorial kẹt ở `OnClickAsync` — thấy ngay lần Play đầu |
| Root canvas của target là Camera | target không lên trên lớp tối, nhìn thấy tối toàn màn không có gì sáng — lần Play đầu |
| Quên `Release()` (huỷ giữa tutorial) | canvas target kẹt sorting cao, vẽ trên popup khác. Chặn bằng `finally { Release(); }` ở caller |
| Prefab service không có trong scene | InitArgs `ThrowIfMissing` ở `Awake` của Initializer (DEBUG); release là null im lặng → NRE ở `Focus` |
| Hai service cùng highlight | không có, chỉ một instance `FindFromScene` |

## 7. Giới hạn và đường mở

- **Chỉ UI Overlay.** Gameplay 3D (box trên lưới) cần hệ khác: lớp tối là Camera canvas hoặc post-effect, target là
  `Renderer`, không chung cơ chế sorting. Tách riêng, đặt tên riêng, không ép vào interface này.
- **Một target một lúc.** Cần sáng hai chỗ cùng lúc thì là yêu cầu mới, thêm overload nhận mảng, đường cũ không đổi.
- **Không có chữ.** Hướng dẫn bằng chữ là màn riêng của consumer (Collection: `CollectionTutorialScreen`).
- **`Direction.GetEulerAngleZ()`** là bảng sáu góc cố định của sprite tay (vẽ trỏ lên, góc 0 = trỏ lên); sprite khác hướng gốc thì
  chỉnh ở một chỗ đó.

## 8. Consumer đầu tiên — Collection

| Chỗ | Việc |
|---|---|
| `CollectionHomeWidget.prefab` root | thêm `Canvas` (override tắt) + `GraphicRaycaster`; field `highlightCanvas`, property `HighlightCanvas` |
| `CollectionMainScreen.prefab` → object `infoBtn` | thêm `Canvas` + `GraphicRaycaster`; field `infoHighlightCanvas`, property `InfoHighlightCanvas`; property `InfoButton` |
| `CollectionModule` | `IInitializable<IRewardService, ITimeService, ICanvasHighlightTutorial>`, `internal HighlightTutorial` cho flow |
| `CollectionModuleInitializer` | `Initializer<CollectionModule, IRewardService, ITimeService, ICanvasHighlightTutorial>` |
| `CollectionHomeFlow.RunTutorialAsync` | `Focus(widget.HighlightCanvas)` → tap → `Focus(mainScreen.InfoHighlightCanvas)` → tap → `finally Release()` → `TutorialScreen.ShowAsync` |
| `CollectionTutorialScreen` | chỉ còn màn chữ + `continueButton`; bỏ `PointAt`/`HidePointer` |
| `Services.unity` | kéo `CanvasHighlightTutorial.prefab`; ô thứ ba của Initializer để **Service** |
