using System;
using Horcrux.Runtime.Abstractions.LiveOps;
using Horcrux.Runtime.Utilities;
using Horcrux.Runtime.Utilities.EventBus;
using UnityEngine;

namespace Horcrux.Runtime.Implementations.LiveOps
{
    public class LiveOpsHomeFlowHost : MonoBehaviour
    {
        [Splitter("References")]
        [SerializeField] private LiveOpsHost liveOpsHost;

        private LiveOpsHomeFlowRunner _runner;
        private Subscription<LiveOpsHomeFlowRequested> _requestedSub;
        

        #region Unity Callbacks

        private void Awake()
        {
            _runner = new LiveOpsHomeFlowRunner(liveOpsHost.Modules, OnHomeFlowFailed);
        }

        private void OnEnable()
        {
            _requestedSub = EventBus<LiveOpsHomeFlowRequested>.Subscribe(OnFlowRequested);
        }

        private void OnDisable()
        {
            _requestedSub.Dispose();
        }

        private void OnDestroy()
        {
            _runner.OnHomeExit();
        }

        #endregion

        #region API

        public void OnHomeEnter()
        {
            _runner.OnHomeEnter();
        }

        public void OnHomeExit()
        {
            _runner.OnHomeExit();
        }

        #endregion

        #region Class Methods

        private void OnFlowRequested(LiveOpsHomeFlowRequested _)
        {
            _runner.Request();
        }

        private void OnHomeFlowFailed(Exception e)
        {
            Debug.LogException(e, this);
        }

        #endregion
    }
}