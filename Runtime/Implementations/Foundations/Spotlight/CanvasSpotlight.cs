using System.Threading;
using Cysharp.Threading.Tasks;
using Horcrux.Runtime.Abstractions.LiveOps;
using Horcrux.Runtime.Tweening.Easing;
using Horcrux.Runtime.Utilities;
using Horcrux.Runtime.Utilities.Common;
using Horcrux.Runtime.Utilities.ExtensionMethods;
using Horcrux.Runtime.Utilities.Tweening;
using Sisus.Init;
using UnityEngine;
using UnityEngine.UI;

namespace Horcrux.Runtime.Implementations.LiveOps
{
    [Service(typeof(ICanvasSpotlight), FindFromScene = true)]
    public class CanvasSpotlight : MonoBehaviour, ICanvasSpotlight
    {
        private struct TargetState
        {
            public Canvas Canvas;
            public GraphicRaycaster Raycaster;
            public bool RaycasterEnabled;
            public bool OverrideSorting;
            public int SortingOrder;
        }
        
        [Splitter("References")]
        [SerializeField] private Canvas selfCanvas;
        [SerializeField] private RectTransform selfRect;
        [SerializeField] private RectTransform handGroup;
        [SerializeField] private Image dim;
        
        [Splitter("Configs")]
        [SerializeField] private int highlightSortingOrder = 3103;
        [SerializeField, Range(0f, 1f)] private float dimAlpha = 0.8f;
        [SerializeField] private float handOneWayDuration = 0.75f;

        private readonly TargetState[] _targets = new TargetState[2];
        private int _targetCount;
        
        private HighlightConfig _highlightConfig;
        private Vector2 _handToTargetDir;

        private CancellationTokenSource _bobCts;
        

        #region Properties

        public bool IsFocusing => _targetCount > 0;

        #endregion

        #region Unity Callbacks

        private void Awake()
        {
            selfCanvas.sortingOrder = highlightSortingOrder;
            gameObject.SetActive(false);
        }

        #endregion

        #region API

        public void Focus(Canvas target, in HighlightConfig config)
        {
            FocusCore(target, null, in config);
        }
        
        public void Focus(Canvas target, Canvas extra, in HighlightConfig config)
        {
            FocusCore(target, extra, in config);
        }
        
        public UniTask FadeDimAsync(float toAlpha, float duration, CancellationToken ct)
        {
            float fromAlpha = dim.color.a;
            return CharmTween.CastAsync(duration, EaseType.Linear,spell, ct, onComplete);

            void spell(float t) => dim.SetAlpha(Mathf.Lerp(fromAlpha, toAlpha, t));
            void onComplete() => dim.SetAlpha(toAlpha);
        }
        
        public void Release()
        {
            if (_targetCount == 0)
                return;
            
            if(_bobCts != null)
            {
                _bobCts.Cancel();
                _bobCts.Dispose();
                _bobCts = null;
            }
            
            for(int i = 0; i < _targetCount; i++)
            {
                ref TargetState state = ref _targets[i];
                state.Raycaster.enabled = state.RaycasterEnabled;
                state.Canvas.overrideSorting = state.OverrideSorting;
                state.Canvas.sortingOrder = state.SortingOrder;
                state = default;
            }

            _targetCount = 0;
            gameObject.SetActive(false);
        }
        
        #endregion

        #region Class Methods

        private void FocusCore(Canvas target, Canvas extra, in HighlightConfig config)
        {
            if(_targetCount > 0)
                Release();
            
            gameObject.SetActive(true);
            dim.SetAlpha(dimAlpha);
            
            _highlightConfig = config;
            _handToTargetDir = _highlightConfig.HandDirection.GetDirectionVector();
            
            Capture(target);
            if(extra != null) 
                Capture(extra);
            
            handGroup.gameObject.SetActive(_highlightConfig.ShowHand);
            handGroup.localEulerAngles = new Vector3(0f, 0f, _highlightConfig.HandDirection.GetEulerAngleZ());

            _bobCts = new CancellationTokenSource();
            Debug.LogError($"[CanvasSpotlight] : {target.transform.position}");
            handGroup.CharmPointAndBob(target.transform.position, _handToTargetDir, 
                _highlightConfig.HandTargetOffset, _highlightConfig.TargetPadding,
                EaseType.OutQuad, handOneWayDuration, _bobCts.Token).Forget();
            
        }

        private void Capture(Canvas target)
        {
            GraphicRaycaster raycaster = target.GetComponent<GraphicRaycaster>();
            if(raycaster == null)
                Debug.LogError($"[CanvasSpotlight]: {target.name} has no GraphicRaycaster, never receives taps.", target);
            
            ref TargetState state = ref _targets[_targetCount];
            state.Canvas = target;
            state.Raycaster = raycaster;
            state.RaycasterEnabled = raycaster.enabled;
            state.OverrideSorting = target.overrideSorting;
            state.SortingOrder = target.sortingOrder;
            _targetCount++;
            
            // enable first, then override: a Graphic binds to the nearest ENABLED canvas
            raycaster.enabled = true;
            target.overrideSorting = true;
            target.sortingOrder = highlightSortingOrder + 1;
        }
        
        #endregion
    }
}