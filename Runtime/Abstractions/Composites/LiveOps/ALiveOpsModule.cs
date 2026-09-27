using UnityEngine;

namespace Horcrux.Runtime.Abstractions.Composites.LiveOps
{
    public abstract class ALiveOpsModule : MonoBehaviour, ILiveOpsModule
    {
        #region Properties
        
        protected abstract ILiveOpsHost LiveOpsHost { get; }
        
        public abstract string ModuleId { get; }
        
        public LiveOpsModuleState State { get; private set; }
        
        public long SecondsLeft => State == LiveOpsModuleState.Running
            ? Window.SecondsLeft(LastUnix) : 0;
        
        protected bool IsInitialized { get; private set; }
        
        protected LiveOpsWindow Window { get;  private set; }
        
        protected long LastUnix { get; private set; }
        
        #endregion

        #region Unity Callbacks

        protected virtual void Start()
        {
            LiveOpsHost.Register(this);
        }

        protected virtual void OnDestroy()
        {
            LiveOpsHost.Unregister(this);
        }

        #endregion

        #region API
        
        public void Initialize(long nowUnix)
        {
            IsInitialized = true;
            Evaluate(nowUnix);
        }

        /// <summary>
        ///  first evaluation and every tick
        /// </summary>
        public void Tick(long nowUnix)
        {
            Evaluate(nowUnix);
        }
        
        #endregion

        #region Class Methods

        protected abstract void Refresh(long nowUnix);
        
        protected abstract LiveOpsWindow ResolveWindow(long nowUnix);

        protected virtual void OnStateChanged(LiveOpsModuleState oldState, 
            LiveOpsModuleState newState) {}
        
        protected void SetState(LiveOpsModuleState nextState)
        {
            if (nextState == State)
                return;
            
            LiveOpsModuleState oldState = State;
            State = nextState;
            OnStateChanged(oldState, nextState);
        }
        
        private void Evaluate(long nowUnix)
        {
            LastUnix = nowUnix;
            Window = ResolveWindow(nowUnix);
            Refresh(nowUnix);
        }

        #endregion
    }
}