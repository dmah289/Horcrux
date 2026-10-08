# Plan — bỏ CharmTween, dùng PrimeTween trực tiếp

Đối tượng đọc: developer tự gõ code vào Horcrux. Plan này **phá API cũ** (xoá hàm `Charm*`), nên làm theo thứ tự Task 1 → 6 để mỗi mốc compile sạch.

## Ngữ cảnh đã chốt

| Mục | Nội dung |
|---|---|
| Mục tiêu | Không còn runner tween tự viết. Việc PrimeTween làm được thì gọi thẳng `Tween.*`; chỉ giữ hàm `Charm*` cho việc PrimeTween **không có** |
| Cách tư duy mới | Một tween do **owner của nó** dừng (`Stop()`, `SetCancellationToken`). Huỷ thì **dừng tại chỗ**, không chốt giá trị đích, không `onComplete` thay thế. Không `try/catch/finally` |
| Giữ `Charm*` | `CharmFlyArc` (chỉ bay bezier, spec 3 field) · `CharmSquashStretch` (chỉ scale giữ thể tích) · `CharmPointAndBob` (đường bay + ease tự chọn) · `CharmPunchScale` (sóng sin biên độ cố định, khác `Tween.PunchScale` tắt dần) · `CharmCount` (đếm số vào `TextMeshPro`) |
| Xoá | `CharmTween.cs` · `CharmMove` · `CharmScale` ×2 · `CharmAlpha` ×2 · `CharmSizeDelta` · `CharmAnchoredPosition` · `CharmFillAmount` |
| `EaseType` | Giữ. **Hàm `Charm*` còn giữ nhận `EaseType`** và áp bằng `Easer.Evaluate` trên tween `Ease.Linear` — **trừ `CharmFlyArc`**: `ArcFlightSpec.progressEase` là `Ease`, vì chỉ có một ease trên `t` nên PrimeTween áp thẳng. **Gọi `Tween.*` trực tiếp thì dùng `PrimeTween.Ease`**: field `[SerializeField]` và tham số của chỗ đó đổi kiểu `EaseType` → `Ease` |
| Thời gian | Mọi tween UI đặt `useUnscaledTime: true` (chạy khi `timeScale = 0`) |
| Cố ý KHÔNG làm | Không map `EaseType` ↔ `Ease` · không giữ bất kỳ hàm bọc nào quanh `Tween.Custom` (mỗi chỗ gọi thẳng) · không chốt giá trị đích khi huỷ |
| Quyết định trái trực giác | `UniTask.Delay` cho `delaySeconds` biến mất: PrimeTween có `startDelay` và đã tính unscaled |
| Giá của việc phá hợp đồng cũ | Huỷ giữa chừng thì object **đứng nguyên ở giá trị đang dở** (trước đây bị ép về đích). Chỗ nào cần đích đúng thì owner tự đặt (xem Task 5, 6) |
| Asset bị ảnh hưởng | Đã kiểm: `MButton`, `ProgressBar`, `FlyRewardItem` **chưa có trong prefab/scene nào** → đổi kiểu field không mất dữ liệu serialize |

## Đã khảo sát

| Nguồn | Lấy gì | Không lấy, vì sao |
|---|---|---|
| `Tween.Position/Scale/Alpha/UISizeDelta/UIAnchoredPosition/UIFillAmount` | Thay các `Charm*` tương đương | — |
| `Tween.Custom(float, float, duration, Action<float>, ease, …, useUnscaledTime)` | Thân của bốn hàm `Charm*` giữ lại, `ProgressBar`, `FlyRewardItem` | — |
| `Tween.SetCancellationToken(ct)` | Huỷ theo token. Huỷ lúc đang `await` thì `await` ném `OperationCanceledException`; token **đã huỷ sẵn** thì tween dừng ngay và `await` **không ném** | Không bọc thêm `ThrowIfCancellationRequested` — caller đều là `.Forget()` hoặc `await` chuỗi, sau huỷ không còn việc để làm |
| `Tween.Scale(target, TweenSettings<Vector3>)` + `Sequence` | Hai pha của `MButton`; bản `Scale(start, end, TweenSettings)` đã Obsolete trong 1.4.12 nên dùng `TweenSettings<Vector3>` | `Tween.PunchScale`: tự về rest, không giữ được `dip` lúc đè |
| `Tween.PunchScale` | — | Không đủ chắc là cùng hành vi: `strength` là độ lệch tuyệt đối cộng vào scale hiện tại (Charm nhân với `restScale`), tần số tính theo số lần lắc mỗi giây (Charm tính theo số `cycles` trên cả thời lượng), và giữa hai điểm lắc là `easeBetweenShakes` (mặc định `OutQuad`), không phải sin thuần. `enableFalloff: false` cho biên độ không tắt, nhưng khớp sóng sin chỉ kiểm được bằng chơi thử. Muốn thay thì thử và chơi thử trước (NT6) |

## Trước khi chạy (Editor)

1. Mở `Assets/Horcrux/Runtime/com.horcrux.runtime.asmdef` trong Inspector → **Assembly Definition References** → **+** → kéo `PrimeTween.Runtime` (trong `Packages/com.kyrylokuzyk.primetween/Runtime`). Thiếu thì `using PrimeTween;` báo `CS0246`.
2. Không bước nào khác.

Lưu ý tên: `PrimeTween.Easing` (struct) và namespace `Horcrux.Runtime.Tweening.Easing` cùng tên — file nào `using` cả hai mà viết `Easing` trần sẽ mơ hồ; các đoạn dưới chỉ dùng `Ease` và `Easer`, không viết `Easing`.

## Task 0 — Chuyển động theo tốc độ: setup chung và helper độ dài cung

Quy tắc: quãng đường không cố định thì núm là **tốc độ** (MY_SKILL §4.1). Áp cho `MButton` (Task 5), `CharmFlyArc` (Task 3). `ProgressBar` đã dùng `speed`. Mỗi chỗ tự tính `duration = quãng đường / tốc độ` — không có helper chung, vì thân hàm chỉ là một phép chia.

**Tắt cảnh báo thời lượng 0, một lần ở lúc boot của dự án:** `PrimeTweenConfig.warnZeroDuration = false;`. Quãng đường 0 (nhấn rồi thả cùng frame khi scale còn ở `rest`; `CharmFlyArc` có `from == target`) ra `duration = 0`; PrimeTween chỉ `LogWarning` khi cờ này bật (mặc định bật), tween vẫn được tạo. Chỗ đặt dòng này thuộc dự án, không thuộc Horcrux (đọc source: `PrimeTweenManager.CheckDuration`; chưa chạy thử).

`speed` phải > 0 — chia 0 ra thời lượng vô hạn và `await` treo. Chỗ người tune nhập tốc độ khai `[Min(0.01f)]`.

**Thêm vào `PhysXHelper/BezierCurveHelper.cs`** (xấp xỉ độ dài cung bậc hai: `L ≈ (2·|từ→đến| + |từ→điều khiển| + |điều khiển→đến|) / 3`; chính xác khi điểm điều khiển nằm trên đường thẳng):

```csharp
public static float ApproximateQuadraticBezierLength(Vector3 from, Vector3 target, Vector3 controlPoint)
{
    float chordLength = (target - from).magnitude;
    float controlPolygonLength = (controlPoint - from).magnitude + (target - controlPoint).magnitude;
    return (2f * chordLength + controlPolygonLength) / 3f;
}
```

| Quyết định | Vì |
|---|---|
| Xấp xỉ, không tích phân độ dài cung | tốc độ là núm chọn bằng mắt; sai số vài phần trăm không thấy được, tích phân tốn thêm mỗi lần gọi (NT1, §4.1) |
| Đo cả `controlOffset` vào độ dài | khoảng bay ngắn mà `controlOffset` lớn thì cung dài hơn nhiều so với đường thẳng; chỉ đo đường thẳng làm cung ngắn trông vội |
| Tốc độ là **trung bình** trên cả đường | `progressEase` bóp méo nhịp bên trong tween (`InQuad` chậm đầu nhanh cuối) nhưng tổng thời gian = độ dài / tốc độ |

## Task 1 — `Transform` gọi thẳng PrimeTween, xoá `CharmMove` và hai `CharmScale`

File: `ExtensionMethods/Transform/TransformExtensions.CharmTween.cs`, `TransformExtensions.CharmScale.cs`.

- Xoá `CharmMove`, cả hai `CharmScale`. Xoá enum `PunchStart` **không** — `CharmPunchScale` còn dùng.
- Caller `CharmMove`/`CharmScale`: chỉ `MButton` (Task 5). Không caller nào khác.

Mỗi chỗ cần di chuyển/scale thì gọi thẳng, ví dụ:

```csharp
Tween.Position(transform, target, duration, Ease.OutQuad, useUnscaledTime: true);
Tween.Scale(transform, transform.localScale, targetScale, duration, Ease.OutQuad, useUnscaledTime: true);
```

## Việc độc lập — Xoá `ScrollRectExtensions`

Đã quyết định: xoá `ExtensionMethods/ScrollRect/ScrollRectExtensions.cs` (`SnapVertical`, `ScrollToTarget` ×3), dùng cuộn có sẵn của EnhancedScroller (`JumpToDataIndex`, `Velocity`, `InterruptTween`). Đã grep: không còn caller trong Horcrux hay `_Kelsey` (`_Kelsey/Modules/Extension/Runtime/ScrollRectExtensions.cs` là class **cùng tên khác** namespace, không liên quan). Hệ quả:
- `RectTransformExtensions.GetLocalPosYIn` (`RectTransform/RectTransformExtensions.cs`) chỉ có `SnapVertical` gọi → **không còn caller**. Xoá cùng lúc hay giữ làm helper dùng chung là quyết định của bạn.
- Plan này không còn việc nào ở cuộn: `UIVerticalNormalizedPosition` và `ScrollVerticalAsync` bỏ khỏi plan.

## Task 2 — Xoá các `Charm*` UI có sẵn trong PrimeTween

| Xoá | Thay bằng khi cần gọi |
|---|---|
| `CanvasGroupExtensions.CharmTween.cs` (`CharmAlpha`) — xoá cả file | `Tween.Alpha(canvasGroup, to, duration, useUnscaledTime: true)` |
| `GraphicExtensions.CharmAlpha` — giữ `SetAlpha` | `Tween.Alpha(graphic, from, to, duration, ease, useUnscaledTime: true)` |
| `RectTransformExtensions.CharmTween.cs` (`CharmSizeDelta`, `CharmAnchoredPosition`) — xoá cả file | `Tween.UISizeDelta(rect, to, duration, useUnscaledTime: true)` · `Tween.UIAnchoredPosition(rect, to, duration, ease, useUnscaledTime: true)` |
| `ImageExtensions.CharmTween.cs` (`CharmFillAmount`) — xoá cả file | `Tween.UIFillAmount(image, from, to, duration, ease, useUnscaledTime: true)` |

Các hàm này **không có caller nào** trong dự án ngoài chính Horcrux (đã grep). Dọn `using` thừa ở `GraphicExtensions.cs`.

## Task 3 — `CharmFlyArc`, `CharmSquashStretch`, `CharmPointAndBob`, `CharmPunchScale`, `CharmCount` (giữ, viết lại thân)

Mỗi hàm là **một lời gọi `Tween.Custom` duy nhất**, không vòng lặp, không `try`. Thêm `using PrimeTween;`, bỏ `using ...Utilities.Tweening` nếu hết dùng `CharmTween` (giữ nếu còn dùng `ArcFlightSpec`).

**`CharmFlyArc`** — chỉ bay, không đụng scale. `ArcFlightSpec` (file `ExtensionMethods/Transform/ArcFlightSpec.cs`) đã có bốn field; `SquashStretchFlightSpec` đã gỡ khỏi file này:

```csharp
[Serializable]
public struct ArcFlightSpec
{
    public float controlOffset;
    [Min(0.01f)] public float speed;
    [Min(0.1f)] public float controlAtRatio;
    public Ease progressEase;
}
```

`speed` là **đơn vị thế giới mỗi giây**, tính trên độ dài cung (Task 0). `progressEase` là `PrimeTween.Ease`: chỉ có **một** ease trên `t`, nên PrimeTween áp thẳng, không cần `Easer`.

```csharp
public static UniTask CharmFlyArc(this Transform self, Vector3 target, ArcFlightSpec spec,
    CancellationToken ct)
{
    Vector3 from = self.position;
    Vector3 controlPoint = BezierCurveHelper.ComputeControlPoint(from, target,
        spec.controlOffset, spec.controlAtRatio);

    float arcLength = BezierCurveHelper.ApproximateQuadraticBezierLength(from, target, controlPoint);
    float duration = arcLength / spec.speed;

    return Tween.Custom(0f, 1f, duration, spell, spec.progressEase, useUnscaledTime: true)
        .SetCancellationToken(ct);

    void spell(float t) => self.position = BezierCurveHelper.EvaluateQuadraticBezier(
        t, from, target, controlPoint);
}
```

`Tween` → `UniTask` có conversion ngầm (`UNITASK_INSTALLED`), nên trả `Tween` ở chỗ kiểu `UniTask` là hợp lệ. Struct không có giá trị mặc định: `controlAtRatio` để 0 thì điểm điều khiển dồn về `from` mà không báo lỗi — tune trong Inspector (giá trị thường dùng `0.5`).

**`CharmSquashStretch`** — thay `CharmFlyArc(SquashStretchFlightSpec)`. Chỉ đổi scale; không bay, không hồi phục. File mới `ExtensionMethods/Transform/SquashStretchSpec.cs` (đã có):

```csharp
[Serializable]
public struct SquashStretchSpec
{
    public float startScale;
    public float endScale;
    public float duration;
    public Ease ease;
}
```

Hàm đặt cạnh `CharmFlyArc` trong `TransformExtensions.CharmTween.cs`:

```csharp
public static UniTask CharmSquashStretch(this Transform self, SquashStretchSpec spec, CancellationToken ct)
{
    return Tween.Custom(0f, 1f, spec.duration, spell, spec.ease, useUnscaledTime: true)
        .SetCancellationToken(ct);

    void spell(float t) => self.localScale = SquashStretch.GetSquashStretch(t,
        spec.startScale, spec.endScale, AxisType.Y, CoordinateSystem.XY);
}
```

| Quyết định | Vì |
|---|---|
| Hai hàm không biết nhau | ghép là việc của caller; không cờ chế độ, không spec gộp (§3.1 "biến thể là cửa riêng") |
| Không có pha hồi phục | hồi về `1` là một lần gọi nữa với `startScale = endScale, endScale = 1` (`GetVolumePreservingScale(1)` ra `Vector3.one`) |
| Trục `Y` + `XY` cố định | mọi caller hiện tại là 2D, nâng trục thành field spec khi có caller cần khác (thêm field vào struct là việc rẻ) |
| `ease` là `Ease`, PrimeTween áp | `GetSquashStretch` nhận `t` đã ease, không còn tham số ease — một tầng ease duy nhất. Ease vọt quá 1 (`OutBack`, `OutElastic`) là ca dùng chính của hàm; `Tween.Custom` không kẹp giá trị |

Ghép bay + squash (ví dụ caller tự viết sau):

```csharp
await UniTask.WhenAll(
    transform.CharmFlyArc(target, flySpec, ct),
    transform.CharmSquashStretch(squashSpec, ct));
```

**`CharmPointAndBob`** — một lời gọi `Tween.Position`, không `Tween.Custom`, không `Easer`. `ease` đổi thành `Ease` (caller `CanvasSpotlight.Focus` đổi `EaseType.OutQuad` → `Ease.OutQuad`):

```csharp
public static UniTask CharmPointAndBob(this Transform self, Vector3 target, Vector3 direction,
    float backupDistance, float targetPadding, Ease ease, float oneWayDuration, CancellationToken ct)
{
    Vector3 normalizedDirection = direction.normalized;
    Vector3 to = target - normalizedDirection * targetPadding;
    Vector3 from = to - normalizedDirection * backupDistance;

    return Tween.Position(self, from, to, oneWayDuration, ease,
            cycles: -1, cycleMode: CycleMode.Rewind, useUnscaledTime: true)
        .SetCancellationToken(ct);
}
```

| Quyết định | Vì |
|---|---|
| `CycleMode.Rewind`, không `Yoyo` | `Yoyo` giữ **cùng ease** ở chiều về (với `OutQuad`: rời `to` nhanh, tới `from` chậm — đường đi bất đối xứng). `Rewind` đảo thời gian nên chiều về đi lại **đúng đường đã đi**, y hệt bản cũ (`Easer(ease, ratio)` với `ratio` giảm dần). Dùng `Yoyo` sẽ đổi hành vi nhìn thấy |
| Giữ hàm, không inline vào `CanvasSpotlight` | hàm mã hoá một luật hình học (lùi `backupDistance`, đệm `targetPadding` theo hướng chỉ); chỉ có một caller nhưng luật không nên nằm trong code spotlight |

**`CharmPunchScale`** (tween chạy `Linear`, sóng sin giữ nguyên):

```csharp
public static UniTask CharmPunchScale(this Transform self, Vector3 restScale, float amplitude,
    float duration, CancellationToken ct, int cycles = 2, PunchStart punchStart = PunchStart.Dip)
{
    float frequency = cycles * 0.5f;
    float signedAmplitude = punchStart == PunchStart.Dip ? -amplitude : amplitude;

    return Tween.Custom(0f, 1f, duration, spell, Ease.Linear, useUnscaledTime: true)
        .SetCancellationToken(ct);

    void spell(float t)
    {
        float wave = HarmonicOscillator.GetHarmonicDisplacement(WaveStyle.Sin, frequency, t);
        self.localScale = restScale * (1f + signedAmplitude * wave);
    }
}
```

Đầu sóng sin kết thúc ở 0 tại `t = 1` với `cycles` nguyên, nên hết tween thì scale đã về `restScale` mà không cần ép.

**`CharmCount`** — `delaySeconds` thành `startDelay`; đích cuối do tween chạm `t = 1`:

```csharp
public static UniTask CharmCount(this TextMeshProUGUI self, int start, int end, string format,
    EaseType easeType, float duration, float delaySeconds = 0f, CancellationToken ct = default)
{
    return Tween.Custom(0f, 1f, duration, spell, Ease.Linear, startDelay: delaySeconds, useUnscaledTime: true)
        .SetCancellationToken(ct);

    void spell(float t) =>
        self.SetText(format, Mathf.RoundToInt(Mathf.Lerp(start, end, Easer.Evaluate(easeType, t))));
}
```

Lưu ý: bản cũ nội suy `Lerp(start, end, t)` với `t` **đã ease** từ `CastAsync`; bản mới áp `Easer` rõ ràng ở đúng một chỗ, cùng kết quả.

## Task 4 — `ProgressBar`, `CanvasSpotlight.FadeDimAsync`

**`ProgressBar.PlayAsync`** (+ field `ease`):

```csharp
[SerializeField] private Ease ease;

public UniTask PlayAsync(int from, int to, int goal, CancellationToken ct)
{
    float duration = Mathf.Abs(to - from) / Mathf.Max(0.0001f, speed);
    return Tween.Custom(from, to, duration, value => Write(value, goal), ease, useUnscaledTime: true)
        .SetCancellationToken(ct);
}
```

`from`/`to` là `int`, `Tween.Custom` nhận `float`: ép ngầm đúng. Huỷ giữa chừng thì thanh đứng ở giá trị dở — owner (view cha) gọi `Set(curr, goal)` nếu cần số đúng.

**`CanvasSpotlight.FadeDimAsync`** — đích chốt ở owner, không ở tween (`ICanvasSpotlight.FadeDimAsync` giữ chữ ký):

```csharp
public UniTask FadeDimAsync(float toAlpha, float duration, CancellationToken ct)
{
    return Tween.Alpha(dim, toAlpha, duration, Ease.Linear, useUnscaledTime: true)
        .SetCancellationToken(ct);
}
```

Dòng "huỷ thì đặt alpha đích ngay" trong `CanvasSpotlight.md` bị bỏ — xem mục cuối.

## Task 5 — `MButton` hai pha, chỉ dùng `Tween.Scale` + `Sequence`

Kịch bản: **pha 1 (pointer down)** `hiện tại → dip`, giữ ở `dip` suốt lúc đè. **Pha 2 (pointer up)** `allowOvershoot` bật: `hiện tại → overshoot → rest`; tắt: `hiện tại → rest`. Không `async`, không CTS, không `EaseType`. **Mỗi đoạn chạy theo tốc độ, không theo thời lượng**: thời gian của đoạn = `quãng đường / tốc độ`, nên đoạn ngắn (đè lại khi gần `dip`, thả tay lúc mới nhún) đi ngắn, đoạn dài đi dài, cùng một nhịp cảm nhận.

**Mọi tween bắt đầu từ scale hiện tại của object, không từ `dip` hay `rest` lý thuyết** — đó là điều làm các điểm giao giữa hai pha liền mạch (xem bảng dưới).

Viết lại cả class `Utilities/UI/MButton.cs`:

```csharp
using PrimeTween;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Horcrux.Runtime.Utilities.UI
{
    [RequireComponent(typeof(Button))]
    public class MButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        [Splitter("References")]
        [SerializeField] private Transform target;

        [Splitter("Press")]
        [SerializeField] private float dipScale = 0.9f;
        [SerializeField, Min(0.01f)] private float dipSpeed = 1.25f;
        [SerializeField] private Ease dipEase = Ease.OutQuad;

        [Splitter("Release")]
        [SerializeField] private bool allowOvershoot;
        [SerializeField] private float overshootScale = 1.1f;
        [SerializeField, Min(0.01f)] private float overshootSpeed = 2f;
        [SerializeField] private Ease overshootEase = Ease.OutQuad;
        [SerializeField, Min(0.01f)] private float releaseSpeed = 0.8f;
        [SerializeField] private Ease releaseEase = Ease.OutQuad;

        private Button _selfBtn;
        private Transform _targetToTween;
        private Vector3 _restScale;
        private float _restScaleMagnitude;
        private Sequence _motion;
        private bool _isPressed;

        #region Unity Callbacks

        private void Awake()
        {
            _selfBtn = GetComponent<Button>();
            _targetToTween = target != null ? target : transform;
            _restScale = _targetToTween.localScale;
            _restScaleMagnitude = _restScale.magnitude;
        }

        private void OnEnable()
        {
            _isPressed = false;
            _targetToTween.localScale = _restScale;
        }

        private void OnDisable()
        {
            _motion.Stop();
        }

        #endregion

        #region API

        public void OnPointerDown(PointerEventData eventData)
        {
            if (!_selfBtn.interactable)
                return;

            _isPressed = true;
            _motion.Stop();
            _motion = Sequence.Create(ScaleFromTo(_targetToTween.localScale, _restScale * dipScale,
                dipSpeed, dipEase));
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (!_isPressed)
                return;

            _isPressed = false;
            _motion.Stop();
            Vector3 currScale = _targetToTween.localScale;

            if (!allowOvershoot)
            {
                _motion = Sequence.Create(ScaleFromTo(currScale, _restScale, releaseSpeed, releaseEase));
                return;
            }

            Vector3 overshootVector = _restScale * overshootScale;
            _motion = Sequence.Create(ScaleFromTo(currScale, overshootVector, overshootSpeed, overshootEase))
                .Chain(ScaleFromTo(overshootVector, _restScale, releaseSpeed, releaseEase));
        }

        #endregion

        #region Class Methods

        private Tween ScaleFromTo(Vector3 start, Vector3 end, float speed, Ease ease)
        {
            float scaleRatioDist = (end - start).magnitude / _restScaleMagnitude;
            float duration = scaleRatioDist / speed;
            return Tween.Scale(_targetToTween, start, end, duration, ease, useUnscaledTime: true);
        }

        #endregion
    }
}
```

**Xử lý các ca**

| Ca | Cách xử lý | Vì sao mượt |
|---|---|---|
| Đè giữ rồi kéo ra ngoài vùng raycast | Không nghe `OnPointerExit`/`Enter`; scale giữ ở `dip` | Nút không đổi trạng thái khi con trỏ rời |
| Thả tay **ngoài** nút | `OnPointerUp` vẫn chạy → sang pha 2 | Unity gửi `PointerUp` tới object đã nhận `PointerDown`, kể cả khi con trỏ đã ra ngoài |
| Chạm nhanh: thả tay khi pha 1 còn chạy giữa chừng | pha 2 bắt đầu từ `localScale` lúc đó, thời gian co theo quãng đường còn lại | không nhảy về `dip` rồi mới đi; nhún dở dang chuyển thẳng sang bật lên, không nhanh hay chậm bất thường |
| Đè lại khi pha 2 còn chạy (đang overshoot hoặc đang về rest) | `_motion.Stop()` rồi pha 1 bắt đầu từ `localScale` lúc đó, thời gian co theo quãng đường còn lại | không giật về `rest`; gần `dip` thì đi ngắn, không phải chờ đủ thời lượng cố định |
| Đoạn nối `overshoot → rest` | tween thứ hai khai `start = overshootVector` tường minh | tween tạo sẵn trong `Chain` không được đọc `localScale` lúc dựng — lúc đó scale chưa tới `overshoot`, đọc sẽ ra điểm xuất phát sai và nhảy ở đoạn nối |
| `OnPointerUp` khi `OnPointerDown` bị `interactable = false` chặn | `_isPressed` chặn | không overshoot dù chưa nhún |
| `interactable` thành `false` giữa lúc đè | vẫn chạy pha 2 khi thả | nút không kẹt ở `dip` |
| Object bị tắt khi đang đè | `OnDisable` dừng motion, `OnEnable` đặt `rest` | không có `PointerUp` để chờ |

| Quyết định | Vì |
|---|---|
| Núm là **tốc độ** (`scaleRatioDist` mỗi giây), không phải thời lượng | quãng đường mỗi đoạn đổi theo điểm ngắt (thả tay lúc mới nhún, đè lại gần `dip`); tốc độ giữ nhịp thống nhất ở mọi quãng đường (MY_SKILL §4.1). PrimeTween có `PositionAtSpeed`/`RotationAtSpeed` nhưng **không có `ScaleAtSpeed`**, nên tự tính `duration = quãng đường / tốc độ` rồi gọi `Tween.Scale` |
| Quãng đường đo bằng **tỉ lệ so với `restScale`** (`(end − start).magnitude / _restScaleMagnitude`) | cùng một tốc độ cho nút to và nhỏ, scale gốc khác 1 vẫn đúng; `0.9 → 1` ra đúng `0.1` khi `restScale = (1,1,1)`. `_restScaleMagnitude` tính một lần ở `Awake` vì `restScale` không đổi trong phiên (NT2) |
| Không có sàn thời lượng | quãng đường 0 ra `duration = 0` chỉ gây cảnh báo, đã tắt ở Task 0 |
| `Ease` + `speed` là field riêng cho từng pha, `[Min(0.01f)]` trên tốc độ | `speed = 0` chia 0 ra thời lượng vô hạn, nút kẹt không báo; `[Min]` chặn từ Inspector (§3.4 default hợp lệ im lặng) |
| `useUnscaledTime: true` ngay trong lời gọi | nút trong popup pause (`timeScale = 0`) vẫn phản hồi, không dựa vào ô tick dễ quên (§3.1) |
| Một field `Sequence _motion` cho cả hai pha | một chủ giữ trạng thái chuyển động, một cửa `Stop()`; pha 1 cũng bọc trong `Sequence` để cùng kiểu |
| `releaseSpeed` + `releaseEase` dùng chung cho `→ rest` ở cả hai nhánh | cùng nghĩa "về rest"; hai nhánh khác nhau ở **có điểm dừng `overshoot` hay không**, không ở cách về rest |
| `overshootScale` là con số rõ ràng, không `Ease.OutBack` | `OutBack` vọt khoảng 10% **quãng đường** (`0.9 → 1` chỉ vọt ~`1.01`), mức vọt đổi theo `dipScale`; `overshootScale` là con số người tune nhìn thấy (§4.1) |

Quyết định trái trực giác: `overshoot` và `rest` là hai tween **tách riêng** (không gộp thành một tween `Ease.OutBack` từ `hiện tại → rest`), lý do ở hàng `overshootScale` ở trên.

**Trước khi chạy (Editor)**

1. Thêm `MButton` vào object có `Button` + `Image` (Raycast Target bật). Không bước nào khác cho nút mới — giá trị mặc định đã đủ để chạy.
2. *Dip Scale* ví dụ `0.92` (tỉ lệ so với scale lúc `Awake`). *Dip Speed* là tỉ lệ scale mỗi giây (`1.25` ≈ nhún `0.1` trong `0.08s`), *Dip Ease* `OutQuad`.
3. Bật *Allow Overshoot* thì chỉnh *Overshoot Scale* (> 1, ví dụ `1.08`), *Overshoot Speed*, *Overshoot Ease*; *Release Speed* và *Release Ease* áp cho đoạn về `rest` ở cả hai nhánh.
4. *Target* để trống = scale chính object; điền khi chỉ muốn scale phần hình, không scale vùng bấm (như cũ).
5. Các giá trị trên là điểm khởi đầu, chưa kiểm; cảm giác chốt bằng chơi thử (NT8). Rest scale lấy từ `localScale` lúc `Awake` — không nhập tay.

Xoá khỏi file: `PressAsync`, `ReleaseAsync`, `Restart`, `Cancel`, `_cts`, `pressedScale`, `pressDuration`, `pressEase`, `overshootDuration`, `overshootEase`, `settleDuration`, và các `using` `System`, `System.Threading`, `Cysharp.Threading.Tasks`, `Horcrux.Runtime.Tweening.Easing`, `...ExtensionMethods`.

## Task 6 — `FlyRewardItem`, xoá `CharmTween.cs`

`FlyRewardItem` cần `t` **thô** cho fade và burst, `t` đã ease cho vị trí, nên tween chạy `Linear` và `moveEase` **giữ `EaseType`**:

```csharp
public async UniTask FlyAsync(Sprite sprite, Vector3 from, Vector3 to, CancellationToken ct)
{
    icon.sprite = sprite;
    icon.SetAlpha(1);
    selfRect.position = from;
    selfRect.localScale = Vector3.one;
    gameObject.SetActive(true);
    bool burstFired = false;

    await UniTask.WhenAll(
        selfRect.CharmPunchScale(Vector3.one, 0.2f, duration, ct: ct),
        Tween.Custom(0f, 1f, duration, FlySequence, Ease.Linear, useUnscaledTime: true)
            .SetCancellationToken(ct));

    gameObject.SetActive(false);

    void FlySequence(float t) { /* thân giữ nguyên */ }
}
```

`Land()` biến mất; `SetActive(false)` đứng sau `await`. Huỷ giữa chừng thì `await` ném và item **không tự tắt** — owner (nơi đang pool item) tắt/trả pool.

Cuối cùng: xoá `Tweening/CharmTween.cs` (+ `.meta`), rồi grep `CharmTween` toàn Horcrux phải ra 0.

## Bảng case kiểm thử (agent viết sau khi đọc code thật)

| Input | Kỳ vọng |
|---|---|
| `CharmFlyArc` hết thời lượng | `position == target`, `scale` không đổi |
| `CharmFlyArc` cùng `speed`, quãng bay 100 và 400 | thời gian ≈ tỉ lệ độ dài cung (4×), không bằng nhau |
| `CharmFlyArc` `from == target` | không ném, `await` xong ngay, không cảnh báo (cờ đã tắt) |
| `ApproximateQuadraticBezierLength` điểm điều khiển trên đường thẳng | đúng bằng khoảng cách `from → target` |
| `CharmSquashStretch` hết thời lượng | `localScale == GetVolumePreservingScale(endScale, Y, XY)`, `position` không đổi |
| `CharmSquashStretch` `startScale = endScale`, `endScale = 1` | kết thúc `Vector3.one` |
| `CharmFlyArc` + `CharmSquashStretch` chạy song song qua `WhenAll` | vị trí và scale độc lập, không ghi đè nhau |
| `CharmPunchScale`, `cycles` nguyên | hết tween `localScale == restScale` |
| `CharmCount` | số cuối đúng `end`, dùng `startDelay` |
| `CharmPointAndBob` huỷ | dừng trong ≤ 1 frame, không log |
| `CharmPointAndBob` `ease = OutQuad` | chiều về đi đúng đường chiều đi (ease đảo), không bất đối xứng |
| `timeScale = 0` | mọi tween vẫn chạy |
| Huỷ giữa chừng | object đứng nguyên giá trị đang dở, `await` ném `OperationCanceledException` |
| `MButton` đè giữ, kéo con trỏ ra ngoài raycast | scale giữ ở `dip` |
| `MButton` thả tay ngoài nút | pha 2 chạy (`OnPointerUp` vẫn tới) |
| `MButton` thả tay khi pha 1 mới chạy nửa chừng | pha 2 bắt đầu từ scale lúc đó, không nhảy |
| `MButton` đè lại khi pha 2 còn chạy | pha 1 bắt đầu từ scale lúc đó, không giật về `rest` |
| `MButton` `allowOvershoot` bật | scale đạt `rest * overshootScale` rồi về `rest`, không nhảy ở đoạn nối |
| `MButton` `allowOvershoot` tắt | `hiện tại → rest`, không vượt `rest` |
| `MButton` bấm nhanh 20 lần | `localScale` cuối đúng `restScale` |
| `MButton` `interactable = false` lúc nhấn rồi thả | không overshoot |
| `MButton` trong popup `timeScale = 0` | vẫn chạy |
| Chơi thử (developer) | nút nhấn nhanh, popup pause, tutorial bàn tay bob, thưởng bay |

## Đồng bộ tài liệu sau khi gõ xong

Báo agent: `CharmFlyArc.md` (viết lại §1 thành hai hàm tách: `CharmFlyArc` và `CharmSquashStretch`; bỏ "dựng trên `CharmTween.CastAsync`", `CharmScale`), `CharmPunchScale.html`, `MButton.md` (viết lại §1, §3 theo hai pha dip → rest/overshoot; §4 "Dạng B" cũng gọi `CharmScale` đã bị xoá nên cần quyết định riêng: sửa theo kịch bản mới hay bỏ dạng B), `CanvasSpotlight.md` (bỏ dòng "huỷ thì đặt alpha đích ngay" và "nhất quán với `CharmTween`"), rồi `graphify update .`.
