using System.Threading;
using Cysharp.Threading.Tasks;
using Horcrux.Runtime.Abstractions.Bootstrap;
using Horcrux.Runtime.Abstractions.Persistence;
using Sisus.Init;

namespace Horcrux.Runtime.Implementations.Persistence
{
    public class SaveBootstep : BaseBootStep, IInitializable<IPersistenceDataCollection>
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
        }

        private IPersistenceDataCollection saveCollection;
        public void Init(IPersistenceDataCollection argument)
        {
            saveCollection = argument;
        }
    }
}