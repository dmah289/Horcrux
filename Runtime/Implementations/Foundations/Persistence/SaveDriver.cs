using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Horcrux.Runtime.Abstractions.Persistence;
using Sisus.Init;
using UnityEngine;

namespace Horcrux.Runtime.Implementations.Persistence
{
    public class SaveDriver : MonoBehaviour<BaseSaveCollection>
    {
        private bool inBackground;
        
        [SerializeField] private float intervalSeconds;

#region Unity Callbacks
        private void Start()
            => AutoSaveLoopAsync(destroyCancellationToken).Forget();

        private void OnApplicationFocus(bool hasFocus)
            => HandleOnGoToBackground(!hasFocus);

        private void OnApplicationPause(bool pauseStatus) 
            => HandleOnGoToBackground(pauseStatus);
#endregion

        private async UniTask AutoSaveLoopAsync(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                await UniTask.Delay(TimeSpan.FromSeconds(intervalSeconds), 
                    DelayType.Realtime, cancellationToken: ct);
                
                if (ct.IsCancellationRequested)
                    return;
                
                saveCollection.FlushAll();
            }
        }

        private void HandleOnGoToBackground(bool isInBg)
        {
            if (isInBg == inBackground) 
                return;
            
            inBackground = isInBg;
            saveCollection.FlushAll();
        }

        private BaseSaveCollection saveCollection;
        protected override void Init(BaseSaveCollection argument)
        {
            saveCollection = argument;
        }
    }
}