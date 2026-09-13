using System;
using Sisus.Init;
using UnityEngine;

namespace Horcrux.Runtime.Abstractions.Composites.LiveOps
{
    public abstract class ALiveOpsModule : MonoBehaviour<ILiveOpsHost>, ILiveOpsModule
    {
        protected LiveOpsWindow Window { get; set; }
        protected long LastNowUnix { get; private set; }
        
        #region Properties
        
        public abstract string ModuleId { get; }
        
        public LiveOpsModuleState State { get; private set; }
        
        public long SecondsLeft => State == LiveOpsModuleState.Running 
            ? Window.SecondsLeft(LastNowUnix) : 0;
        
        #endregion

        #region Unity Callbacks

        protected virtual void Start()
            => liveOpsHost.Register(this);

        protected virtual void OnDestroy()
            => liveOpsHost.Unregister(this);

        #endregion

        #region API
        
        public void Initialize(long nowUnix)
        {
            LastNowUnix = nowUnix;
            Refresh(nowUnix);
        }

        /// <summary>
        ///  first evaluation and every tick
        /// </summary>
        public void Tick(long nowUnix)
        {
            LastNowUnix = nowUnix;
            Refresh(nowUnix);
        }
        
        #endregion

        #region Class Methods

        protected abstract void Refresh(long nowUnix);

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

        #endregion

        #region DI

        ILiveOpsHost liveOpsHost;
        
        protected override void Init(ILiveOpsHost argument)
        {
            liveOpsHost = argument;
        }

        #endregion
    }
}