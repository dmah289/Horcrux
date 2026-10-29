using System;

namespace Horcrux.Runtime.Abstractions.Haptics
{
    public interface IHapticService<THaptic> : IHapticSetting, IService<IHapticService<THaptic>>
        where THaptic : struct, Enum
    {
        void Play(THaptic haptic);
    }
}