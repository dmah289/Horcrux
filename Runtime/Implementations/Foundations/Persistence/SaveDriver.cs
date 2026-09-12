using Cysharp.Threading.Tasks;
using Horcrux.Runtime.Abstractions.Persistence;
using Sisus.Init;
using UnityEngine;

namespace Horcrux.Runtime.Implementations.Persistence
{
    public class SaveDriver : MonoBehaviour<BasePersistenceDataCollection>
    {
        private bool inBackground;

        #region Unity Callbacks

        protected override void OnAwake()
        {
            base.OnAwake();
            DontDestroyOnLoad(gameObject);
        }

        private void Start()
        {
            saveCollection.Initialize();
            saveCollection.RunAutosaveAsync(destroyCancellationToken).Forget();
        }

        private void OnApplicationFocus(bool hasFocus)
            => HandleOnGoToBackground(!hasFocus);

        private void OnApplicationPause(bool pauseStatus)
            => HandleOnGoToBackground(pauseStatus);

        private void OnApplicationQuit()
        {
            saveCollection.FlushAll();
            saveCollection.IsInitialized = false;
        }

        #endregion

        #region Class Methods
        private void HandleOnGoToBackground(bool isInBg)
        {
            if (isInBg == inBackground)
                return;
            
            inBackground = isInBg;
            if(inBackground)
                saveCollection.FlushAll();
        }
        #endregion
        
        #region DI
        private BasePersistenceDataCollection saveCollection;
        
        protected override void Init(BasePersistenceDataCollection argument)
        {
            saveCollection = argument;
        }
        #endregion
    }
}