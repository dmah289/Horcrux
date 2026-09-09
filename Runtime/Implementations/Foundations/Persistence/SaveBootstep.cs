using System.Threading;
using Cysharp.Threading.Tasks;
using Horcrux.Runtime.Abstractions.Bootstrap;
using Horcrux.Runtime.Abstractions.Persistence;
using Sisus.Init;

namespace Horcrux.Runtime.Implementations.Persistence
{
    public class SaveBootstep : BaseBootStep, IInitializable<BaseSaveCollection>
    {
        public override UniTask InitializeAsync(CancellationToken ct)
        {
            saveCollection.Initialize();
            return UniTask.CompletedTask;
        }

        public override void OnGoToBackground(bool inBackground)
        {
            base.OnGoToBackground(inBackground);
            
            if(inBackground)
                saveCollection.FlushAll();
        }

        private BaseSaveCollection saveCollection;
        public void Init(BaseSaveCollection argument)
        {
            saveCollection = argument;
        }
    }
}