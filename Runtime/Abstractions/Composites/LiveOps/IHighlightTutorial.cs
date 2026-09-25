using Horcrux.Runtime.Utilities.Common;
using UnityEngine;

namespace Horcrux.Runtime.Abstractions.Composites.LiveOps
{
    public interface IHighlightTutorial
    {
        public void HighlightTarget(Transform target, Direction pointDirection);
        public void Hide();
    }
}