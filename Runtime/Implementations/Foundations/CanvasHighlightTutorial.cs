using Horcrux.Runtime.Abstractions.LiveOps;
using Horcrux.Runtime.Utilities;
using Horcrux.Runtime.Utilities.Common;
using Horcrux.Runtime.Utilities.ExtensionMethods;
using Sisus.Init;
using UnityEngine;

namespace Horcrux.Runtime.Implementations.LiveOps
{
    [Service(typeof(IHighlightTutorial), FindFromScene = true)]
    public class HighlightTutorial : MonoBehaviour, IHighlightTutorial
    {
        [Splitter("References")]
        [SerializeField] private Transform selfTransform;
        [SerializeField] private Transform hand;
        
        private Transform currTarget;
        private Transform originalTargetParent;
        
        public void HighlightTarget(Transform target, Direction pointDirection)
        {
            gameObject.SetActive(true);
            
            currTarget = target;
            originalTargetParent = target.parent;
            currTarget.SetParent(selfTransform);
            hand.eulerAngles = hand.eulerAngles.With(z: pointDirection.GetEulerAngleZ());
            

        }

        public void Hide()
        {
            currTarget.SetParent(originalTargetParent);
            gameObject.SetActive(false);
        }
    }
}