using System;

namespace Horcrux.Runtime.Abstractions.Combo
{
    public interface IComboService
    {
        event Action<int, float> OnCounterChanged;
        int Count { get; }
        
        void AddPoint();
    }
}