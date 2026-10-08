namespace Horcrux.Runtime.Abstractions.Audio
{
    public interface IAudioSetting
    {
        bool IsSfxOn { get; set; }
        bool IsMusicOn { get; set; }
    }
}