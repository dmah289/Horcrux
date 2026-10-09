using System;

namespace Horcrux.Runtime.Abstractions.Audio
{
    /// <summary>
    /// SFX: many at once, each with its own pitch, bursts of one sound throttled. Music: one track at a time.
    /// Enum must starts at 1: <c>0</c> reads as unassigned. Underlying type must be <c>int</c>.
    /// </summary>
    public interface IAudioService<TSfx, TMusic> : IAudioSetting, IService<IAudioService<TSfx, TMusic>>
        where TSfx : struct, Enum
        where TMusic : struct, Enum
    {
        void PlaySfx(TSfx sfx);
        void PlaySfx(TSfx sfx, float pitchScale);
        void PlayMusic(TMusic music);
        void StopMusic();
    }
}