# MButton — press feedback cho Button

> **Loại tài liệu:** hướng dẫn dùng + phương án dự phòng. Bản đang chạy là `MButton.cs` (đứng cạnh `Button`).
> Mục §4 giữ bản **độc lập** (thay hẳn `Button`) để dùng khi muốn bỏ `Button` của Unity.

## 1. Việc nó làm

Nhấn vào thì co về `restScale * pressedScale` (< 1). Thả tay thì về `restScale`; bật `overshoot` thì nảy lên
`restScale * overshootScale` (> 1) rồi mới về. Chạy bằng `CharmScale` (thời gian unscaled, nên vẫn chạy khi pause).
Rest scale lấy từ `localScale` lúc `Awake` — không nhập tay.

## 2. Hai dạng

| | Dạng A — đứng cạnh `Button` (đang dùng) | Dạng B — độc lập (§4) |
|---|---|---|
| Kế thừa | `MonoBehaviour` + `[RequireComponent(typeof(Button))]` | `MonoBehaviour`, không cần `Button` |
| Click, `interactable`, transition màu | do `Button` lo | tự lo (`OnClick`, `Interactable`) |
| Thêm vào prefab cũ | thêm component, không phá gì | phải đổi type mọi chỗ gọi `Button` |
| Cắt click khi trượt ra / kéo scroll | `Button` lo | tự lo (`OnPointerClick` + `Update`) |
| Chọn khi | thay dần, ít rủi ro | muốn bỏ hẳn `Button`, ít component hơn |

Hai dạng **cùng tên class** `MButton` — không để chung một assembly.

## 3. Setup dạng A

1. Object đã có `Button` + `Image` (Raycast Target bật): **Add Component → MButton**.
2. Điền *Pressed Scale* (vd 0.92), *Press Duration* (0.08), *Overshoot* + *Overshoot Scale* (1.08) + *Overshoot Duration* (0.1), *Settle Duration* (0.12).
3. *Target* để trống = scale chính object; điền khi chỉ muốn scale phần hình, không scale vùng bấm.

## 4. Dạng B — bản độc lập

```csharp
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Horcrux.Runtime.Tweening.Easing;
using Horcrux.Runtime.Utilities.ExtensionMethods;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;

namespace Horcrux.Runtime.Utilities.UI
{
    // Minimal Button replacement: press feedback + click. Needs a Graphic with Raycast Target on the same object.
    // Click fires only when press AND release happen on the button without a drag in between.
    public class MButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerEnterHandler,
        IPointerExitHandler, IPointerClickHandler
    {
        private static readonly List<CanvasGroup> GroupBuffer = new();

        [Splitter("References")]
        [SerializeField] private Transform target;
        [SerializeField] private bool interactable = true;
        [SerializeField] private UnityEvent onClick = new();

        [Splitter("Press")]
        [SerializeField, Range(0.5f, 1f)] private float pressedScale = 0.92f;
        [SerializeField] private EaseType pressEase = EaseType.OutQuad;
        [SerializeField, Min(0.01f)] private float pressDuration = 0.08f;

        [Splitter("Release")]
        [SerializeField] private bool overshoot = true;
        [SerializeField, Min(1f)] private float overshootScale = 1.08f;
        [SerializeField] private EaseType overshootEase = EaseType.OutQuad;
        [SerializeField, Min(0.01f)] private float overshootDuration = 0.1f;
        [SerializeField] private EaseType settleEase = EaseType.OutQuad;
        [SerializeField, Min(0.01f)] private float settleDuration = 0.12f;

        private Transform _tweened;
        private Vector3 _restScale;
        private CancellationTokenSource _cts;
        private PointerEventData _pointer;
        private bool _held;            // finger is down (may be outside the button)
        private bool _pressedLook;     // button currently shown shrunk
        private bool _groupAllows = true;

        #region Unity Callbacks

        private void Awake()
        {
            _tweened = target != null ? target : transform;
            _restScale = _tweened.localScale;
        }

        private void OnEnable()
        {
            _held = false;
            _pressedLook = false;
            _pointer = null;
            _tweened.localScale = _restScale;
            RefreshGroup();
        }

        private void OnDisable()
        {
            Cancel();
        }

        // A scroll view took the gesture: let go without a click.
        private void Update()
        {
            if (_held && _pointer.dragging)
                LetGo();
        }

        private void OnCanvasGroupChanged()
        {
            RefreshGroup();
            if (!IsInteractable)
                LetGo();
        }

        #endregion

        #region Properties

        public UnityEvent OnClick => onClick;

        public bool Interactable
        {
            get => interactable;
            set
            {
                interactable = value;
                if (!IsInteractable)
                    LetGo();
            }
        }

        public bool IsInteractable => interactable && _groupAllows;

        #endregion

        #region API

        public void OnPointerDown(PointerEventData eventData)
        {
            if (_held || !IsInteractable || eventData.button != PointerEventData.InputButton.Left)
                return;

            _held = true;
            _pointer = eventData;
            Play(true);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (!IsOwnPointer(eventData))
                return;

            LetGo();
        }

        // Slid back onto the button while still held: shrink again.
        public void OnPointerEnter(PointerEventData eventData)
        {
            if (_held && IsOwnPointer(eventData) && IsInteractable)
                Play(true);
        }

        // Slid off while held: grow back; releasing outside gives no click (Unity checks the target).
        public void OnPointerExit(PointerEventData eventData)
        {
            if (_held && IsOwnPointer(eventData))
                Play(false);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (IsInteractable && eventData.button == PointerEventData.InputButton.Left)
                onClick.Invoke();
        }

        // Completes on the next click; for tutorial-style "wait for the player to tap this".
        public async UniTask OnClickAsync(CancellationToken ct)
        {
            UniTaskCompletionSource clicked = new();
            UnityAction handler = () => clicked.TrySetResult();
            onClick.AddListener(handler);

            try { await clicked.Task.AttachExternalCancellation(ct); }
            finally { onClick.RemoveListener(handler); }
        }

        #endregion

        #region Class Methods

        private bool IsOwnPointer(PointerEventData eventData) => _pointer != null && eventData.pointerId == _pointer.pointerId;

        private void LetGo()
        {
            _held = false;
            _pointer = null;

            if (_pressedLook)
                Play(false);
        }

        private void Play(bool pressed)
        {
            _pressedLook = pressed;
            Cancel();
            _cts = new CancellationTokenSource();
            TweenAsync(pressed, _cts.Token).Forget();
        }

        private async UniTask TweenAsync(bool pressed, CancellationToken ct)
        {
            if (pressed)
            {
                await _tweened.CharmScale(_restScale * pressedScale, pressEase, pressDuration, ct: ct);
                return;
            }

            if (overshoot)
                await _tweened.CharmScale(_restScale * overshootScale, overshootEase, overshootDuration, ct: ct);

            await _tweened.CharmScale(_restScale, settleEase, settleDuration, ct: ct);
        }

        // Same rule as Selectable: any parent CanvasGroup with interactable off blocks, up to ignoreParentGroups.
        private void RefreshGroup()
        {
            bool allows = true;
            Transform node = transform;

            while (allows && node != null)
            {
                node.GetComponents(GroupBuffer);
                bool stop = false;

                for (int i = 0; i < GroupBuffer.Count; i++)
                {
                    if (!GroupBuffer[i].interactable)
                    {
                        allows = false;
                        break;
                    }

                    stop |= GroupBuffer[i].ignoreParentGroups;
                }

                if (stop)
                    break;
                node = node.parent;
            }

            GroupBuffer.Clear();
            _groupAllows = allows;
        }

        private void Cancel()
        {
            if (_cts == null)
                return;

            _cts.Cancel();
            _cts.Dispose();
            _cts = null;
        }

        #endregion
    }
}
```

Dạng B đã xử lý: nhấn giữ không thả không bắn click · trượt ra ngoài rồi thả không click, trượt ngược vào thì co lại ·
kéo ScrollRect thì nhả hiệu ứng và Unity tự huỷ click · chỉ nhận một ngón và chuột trái ·
`Interactable = false` / CanvasGroup cha tắt `interactable` thì không co, không click.
Cố ý bỏ: Enter/gamepad, hover, tint màu, chống bấm đúp. Đổi sang dạng B: gọi `OnClick.AddListener` thay `onClick.AddListener`,
`OnClickAsync(ct)` thay extension của UniTask trên `Button`; listener gán trong Inspector phải gán lại.

## 5. Bẫy

| Chỗ | Sự thật |
|---|---|
| `CharmScale` bị huỷ | `finally` của nó ép scale về target của tween bị huỷ ở frame kế. Tween mới ghi đè ngay trong frame đó nên không giật; riêng tắt rồi bật object **cùng một frame** có thể kẹt scale nhấn |
| Field không có giá trị mặc định | `pressedScale = 0` hoặc duration `0` làm nút co về 0 / nhảy tức thì — luôn gán default trong code |
| Thiếu Graphic | không có Raycast Target thì không nhận `OnPointerDown` — nút im lặng, không báo lỗi |
| Dạng A + `Button` transition | tint màu của `Button` vẫn chạy song song với scale; muốn tắt thì đặt Transition = None |
