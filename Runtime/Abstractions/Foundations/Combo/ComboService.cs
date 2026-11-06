using System;
using PrimeTween;
using UnityEngine;

namespace Horcrux.Runtime.Abstractions.Combo
{
    public abstract class ComboService : MonoBehaviour, IComboService
    {
        private Tween _window;
        
        public event Action<int, float> OnCounterChanged;
        public int Count { get; private set; }
        protected abstract float CalculateWindowSeconds();

        public void AddPoint()
        {
            Count++;
            float windowSeconds = CalculateWindowSeconds();
            
            _window.Stop();
            if (!float.IsNegativeInfinity(windowSeconds))
                _window = Tween.Delay(this, windowSeconds, OnWindowElapsed);
            
            OnCounterChanged?.Invoke(Count, windowSeconds);
        }

        private void OnWindowElapsed()
        {
            Count = 0;
            OnCounterChanged?.Invoke(0, 0f);
        }

        private void OnDestroy()
        {
            _window.Stop();
        }
    }
}