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
        [SerializeField] private float intervalSeconds;

        private void Start()
            => AutoSaveLoopAsync(destroyCancellationToken).Forget();

        private void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus)
                saveCollection.FlushAll();
        }

        private void OnApplicationPause(bool pauseStatus)
        {
            if(pauseStatus)
                saveCollection.FlushAll();
        }

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

        private BaseSaveCollection saveCollection;
        protected override void Init(BaseSaveCollection argument)
        {
            saveCollection = argument;
        }
    }
}