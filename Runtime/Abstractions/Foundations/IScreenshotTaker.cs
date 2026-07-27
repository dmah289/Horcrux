using Cysharp.Threading.Tasks;

namespace Horcrux.Runtime.Abstractions
{
    public interface IScreenshotTaker : IService<IScreenshotTaker>
    {
        public bool IsTakingScreenshot { get; }
        public UniTask StartTakingScreenshots(int delayInterval = 1500);
    }
}