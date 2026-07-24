using Cysharp.Threading.Tasks;

namespace Horcrux.Runtime.Abstractions
{
    public interface IScreenshotTaker
    {
        public bool IsTakingScreenshot { get; }
        public UniTask StartTakingScreenshots(int delayInterval = 1000);
    }
}