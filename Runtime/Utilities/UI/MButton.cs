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
        [SerializeField] private float dipScale = 0.95f;
        [SerializeField] private Ease dipEase = Ease.OutQuad;
        [SerializeField, Min(0.05f)] private float dipSpeed = 1f;
        
        [Splitter("Release")]
        [SerializeField] private bool enableOvershoot;
        [SerializeField, Min(0.05f)] private float overshootSpeed = 1f;
        [SerializeField] private float overshootScale = 1.05f;
        [SerializeField] private Ease overshootEase = Ease.OutQuad;

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
        
        public void OnPointerDown(PointerEventData eventData)
        {
            if (!_selfBtn.interactable)
                return;
            
            _isPressed = true;
            _motion.Stop();
            _motion = Sequence.Create(ScaleFromTo(_targetToTween.localScale, _restScale * dipScale, dipSpeed, dipEase));
        }
        
        public void OnPointerUp(PointerEventData eventData)
        {
            if (!_isPressed)
                return;
            
            _isPressed = false;
            _motion.Stop();
            Vector3 currScale = _targetToTween.localScale;
            
            if (!enableOvershoot)
            {
                _motion = Sequence.Create(ScaleFromTo(currScale, _restScale, dipSpeed, dipEase));
                return;
            }
            
            Vector3 overshootVector = _restScale * overshootScale;
            _motion = Sequence.Create(ScaleFromTo(currScale, overshootVector, overshootSpeed, overshootEase))
                .Chain(ScaleFromTo(overshootVector, _restScale, dipSpeed, dipEase));
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