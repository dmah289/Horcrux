using System.Threading;
using Cysharp.Threading.Tasks;
using Horcrux.Runtime.Abstractions.Bootstrap;
using Horcrux.Runtime.Abstractions.Persistence;
using Sisus.Init;
using UnityEngine;

namespace Horcrux.Runtime.Implementations.Persistence
{
    public class SaveBootstep : BaseBootStep, IInitializable<BasePersistenceDataCollection>
    {
        public override UniTask InitializeAsync(CancellationToken ct)
        {
            saveCollection.Initialize();
            // Own lifetime, not the phase token: the runner cancels that one at every level load.
            saveCollection.RunAutosaveAsync(destroyCancellationToken).Forget();

            return UniTask.CompletedTask;
        }

        public override void OnGoToBackground(bool inBackground)
        {
            base.OnGoToBackground(inBackground);
            
            if(inBackground)
                saveCollection.FlushAll();
        }

        public override void OnAppQuit()
        {
            base.OnAppQuit();
            saveCollection.FlushAll();
            saveCollection.IsInitialized = false;
        }

        private BasePersistenceDataCollection saveCollection;
        public void Init(BasePersistenceDataCollection argument)
        {
            saveCollection = argument;
        }
    }
}