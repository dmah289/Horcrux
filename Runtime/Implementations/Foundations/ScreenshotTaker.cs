#if UNITY_EDITOR
using UnityEngine;
using System.IO;
using Cysharp.Threading.Tasks;
using Horcrux.Runtime.Abstractions;
using Horcrux.Runtime.Abstractions.Composites;
using Horcrux.Runtime.Abstractions.Singleton;
using Sirenix.OdinInspector;
using Sisus.Init;

namespace Horcrux.Runtime.Implementations
{
    [Service(typeof(IScreenshotTaker), FindFromScene = true)]
    public class ScreenshotTaker : MonoBehaviour<ILevelManager, ILevelCheater>, IScreenshotTaker
    {
        [SerializeField] private Camera myCamera;
        [SerializeField] private int resolutionWidth = 1080;
        [SerializeField] private int resolutionHeight = 1920;
        [SerializeField] private int scale = 1;
        [SerializeField] private bool alphaIncluded;
        [SerializeField] private int counter;

        [SerializeField] private string saveFolderName = "Screenshots";
        [SerializeField] private string savePath = "";

        private RenderTexture m_CachedRT;
        private Texture2D m_CachedTex;
        private bool isInTakingScreenshotProcess;
        

        public bool IsTakingScreenshot => isInTakingScreenshotProcess;

        protected override void OnAwake()
        {
            base.OnAwake();
            myCamera ??= Camera.main;

            savePath = Path.Combine(Application.dataPath, "..", saveFolderName);
            if (!Directory.Exists(savePath))
                Directory.CreateDirectory(savePath);
        }

        private void TakeScreenshot()
        {
            int finalWidth = resolutionWidth * scale;
            int finalHeight = resolutionHeight * scale;

            m_CachedRT ??= new RenderTexture(finalWidth, finalHeight, 24);

            myCamera.targetTexture = m_CachedRT;
            myCamera.Render();

            if (m_CachedTex == null)
            {
                TextureFormat format = alphaIncluded ? TextureFormat.ARGB32 : TextureFormat.RGB24;
                m_CachedTex = new Texture2D(finalWidth, finalHeight, format, false);
            }
            RenderTexture.active = m_CachedRT;
            m_CachedTex.ReadPixels(new Rect(0, 0, finalWidth, finalHeight), 0, 0);

            myCamera.targetTexture = null;
            RenderTexture.active = null;

            byte[] ssBytes = m_CachedTex.EncodeToPNG();
            string fileName = $"{savePath}/ss_{counter++:D3}_{finalWidth}x{finalHeight}.png";
            File.WriteAllBytes(fileName, ssBytes);
        }

        public async UniTask StartTakingScreenshots(int delayInterval = 1000)
        {
            if (isInTakingScreenshotProcess)
                return;
            
            isInTakingScreenshotProcess = true;
            myCamera ??= Camera.main;
            
            while(levelMng.CurrLevelDataIndex < levelMng.LevelDataAmount)
            {
                await UniTask.Yield();
                TakeScreenshot();
                await UniTask.Delay(delayInterval);
                levelCheater.NextLevel();
            }
            
            isInTakingScreenshotProcess = false;
        }

        private void OnDestroy()
        {
            if (m_CachedRT != null)
                Destroy(m_CachedRT);
            if (m_CachedTex != null)
                Destroy(m_CachedTex);
        }

        [Button("Open Save Folder")]
        private void OpenSaveFolder()
        {
            Application.OpenURL(Path.Combine(Application.dataPath, "..", saveFolderName));
        }

        private ILevelManager levelMng;
        private ILevelCheater levelCheater;
        protected override void Init(ILevelManager firstArgument, ILevelCheater secondArgument)
        {
            levelMng = firstArgument;
            levelCheater = secondArgument;
        }
    }
}
#endif
