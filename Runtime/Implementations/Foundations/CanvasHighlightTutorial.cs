using System.Threading;
using Cysharp.Threading.Tasks;
using Horcrux.Runtime.Abstractions.LiveOps;
using Horcrux.Runtime.Tweening.Easing;
using Horcrux.Runtime.Utilities;
using Horcrux.Runtime.Utilities.Common;
using Horcrux.Runtime.Utilities.ExtensionMethods;
using Sisus.Init;
using UnityEngine;
using UnityEngine.UI;

namespace Horcrux.Runtime.Implementations.LiveOps
{
    [Service(typeof(ICanvasHighlightTutorial), FindFromScene = true)]
    public class CanvasHighlightTutorial : MonoBehaviour, ICanvasHighlightTutorial
    {
        [Splitter("References")]
        [SerializeField] private Canvas selfCanvas;
        [SerializeField] private RectTransform selfRect;
        [SerializeField] private RectTransform handGroup;
        
        [Splitter("Configs")]
        [SerializeField] private int highlightSortingOrder = 3103;
        
        private Canvas _target;
        private GraphicRaycaster _targetRaycaster;
        private bool _cacheTargetCanvasEnabled;
        private bool _cacheTargetRaycasterEnabled;
        private bool _cacheTargetOverrideSorting;
        private int _cacheTargetSortingOrder;
        
        private HighlightConfig _highlightConfig;
        private Vector2 _handToTargetDir;

        private CancellationTokenSource _bobCts;

        [SerializeField] private Canvas tmp;

        #region Properties

        public bool IsFocusing => _target != null;

        #endregion

        #region Unity Callbacks

        private void Awake()
        {
            selfCanvas.sortingOrder = highlightSortingOrder;
            gameObject.SetActive(false);
        }

        private async UniTask tmp1()
        {
            await UniTask.Delay(3000);
            Focus(tmp, new HighlightConfig(Direction.TopLeft, 100, 100));
            
            await UniTask.Delay(3000);
            Focus(tmp, new HighlightConfig(Direction.BottomRight, 100, 100));
            
            await UniTask.Delay(3000);
            Release();
        }

        #endregion

        #region API

        public void Focus(Canvas target, in HighlightConfig config)
        {
            if(_target != null)
                Release();

            GraphicRaycaster raycaster = target.GetComponent<GraphicRaycaster>();
            if(raycaster == null)
                Debug.LogError($"[CanvasHighlightTutorial]: {target.name} has no GraphicRaycaster, never receives taps.", target);
            
            gameObject.SetActive(true);
            
            _target = target;
            _targetRaycaster = raycaster;
            _highlightConfig = config;
            _handToTargetDir = _highlightConfig.HandDirection.GetDirectionVector();
            
            // cache original sorting values
            _cacheTargetCanvasEnabled = _target.enabled;
            _cacheTargetRaycasterEnabled = _targetRaycaster.enabled;
            _cacheTargetOverrideSorting = _target.overrideSorting;
            _cacheTargetSortingOrder = _target.sortingOrder;

            // override sorting values to make sure highlight is on top of target
            _target.enabled = true;
            _targetRaycaster.enabled = true;
            _target.overrideSorting = true;
            _target.sortingOrder = highlightSortingOrder + 1;
            
            handGroup.gameObject.SetActive(_highlightConfig.ShowHand);
            handGroup.localEulerAngles = new Vector3(0f, 0f, _highlightConfig.HandDirection.GetEulerAngleZ());

            _bobCts = new CancellationTokenSource();
            handGroup.CharmPointAndBob(_target.transform.position, _handToTargetDir, 
                _highlightConfig.HandTargetOffset, _highlightConfig.TargetPadding,
                EaseType.OutQuad, 1f, _bobCts.Token).Forget();
        }

        public void Release()
        {
            if (_target == null)
                return;
            
            if(_bobCts != null)
            {
                _bobCts.Cancel();
                _bobCts.Dispose();
                _bobCts = null;
            }
            
            _target.overrideSorting = _cacheTargetOverrideSorting;
            _target.sortingOrder = _cacheTargetSortingOrder;
            _target.enabled = _cacheTargetCanvasEnabled;
            _targetRaycaster.enabled = _cacheTargetRaycasterEnabled;
            _target = null;
            _targetRaycaster = null;
    
            gameObject.SetActive(false);
        }
        
        #endregion
    }
}