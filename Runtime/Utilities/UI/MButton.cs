using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Horcrux.Runtime.Tweening.Easing;
using Horcrux.Runtime.Utilities.ExtensionMethods;
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
        [SerializeField] private float pressedScale = 0.9f;
        [SerializeField] private EaseType pressEase = EaseType.OutQuad;
        [SerializeField] private float pressDuration = 0.3f;
        
        [Splitter("Release")]
        [SerializeField] private bool overshoot;
        [SerializeField] private float overshootDuration = 0.2f;
        [SerializeField] private float overshootScale = 1.1f;
        [SerializeField] private EaseType overshootEase = EaseType.OutQuad;
        [SerializeField] private float settleDuration = 0.05f;

        private Button _selfBtn;
        private Transform _targetToTween;
        private Vector3 _restScale;
        
        private CancellationTokenSource _cts;
        private bool _isPressed;

        #region Unity Callbacks

        private void Awake()
        {
            _selfBtn = GetComponent<Button>();
            _targetToTween = target != null ? target : transform;
            _restScale = _targetToTween.localScale;
        }

        private void OnEnable()
        {
            _isPressed = false;
            _targetToTween.localScale = _restScale;
        }

        private void OnDisable()
        {
            Cancel();
        }
        
        public void OnPointerDown(PointerEventData eventData)
        {
            if (!_selfBtn.interactable)
                return;

            _isPressed = true;
            Restart(PressAsync);
        }
        
        public void OnPointerUp(PointerEventData eventData)
        {
            Release();
        }

        #endregion

        #region Class Methods

        private async UniTask PressAsync(CancellationToken ct)
        {
            await _targetToTween.CharmScale(_restScale * pressedScale, pressEase, pressDuration, ct: ct);
        }

        private void Restart(Func<CancellationToken, UniTask> tween)
        {
            Cancel();
            _cts = new CancellationTokenSource();
            tween(_cts.Token).Forget();
        }

        private void Release()
        {
            if (!_isPressed)
                return;

            _isPressed = false;
            Restart(ReleaseAsync);
        }

        private async UniTask ReleaseAsync(CancellationToken ct)
        {
            if (overshoot)
                await _targetToTween.CharmScale(_restScale * overshootScale, overshootEase, overshootDuration, ct: ct);
            
            await _targetToTween.CharmScale(_restScale, EaseType.Linear, settleDuration, ct: ct);
        }

        private void Cancel()
        {
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;
        }

        #endregion
    }
}