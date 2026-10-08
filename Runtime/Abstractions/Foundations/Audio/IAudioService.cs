namespace Horcrux.Runtime.Abstractions.Audio
{
    public interface IAudioService : IService<IAudioService>
    {
        bool IsSfxOn { get; set; }
        bool IsMusicOn { get; set; }
        
        void PlaySfx(AudioId audioId);
        void PlayMusic(AudioId audioId);
        void StopMusic();
    }
}