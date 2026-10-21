using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Firebase;
using Firebase.RemoteConfig;
using Horcrux.Runtime.Abstractions.RemoteConfigSystem;
using Sisus.Init;
using UnityEngine;

namespace Horcrux.Vendors.Firebase
{
    [Service(typeof(IRemoteConfigProvider))]
    public class FirebaseRemoteConfigProvider : IRemoteConfigProvider
    {
        private FirebaseRemoteConfig _rc;
        private bool _isFetchStarted;
        
        public event Action OnFetched;
        public bool IsFetched { get; private set; }

        #region API

        public bool TryGetRemoteValue(string firebaseKey, out string value)
        {
            value = null;
            if (_rc == null) 
                return false;
            
            var configValue = _rc.GetValue(firebaseKey);
            if(configValue.Source != ValueSource.RemoteValue)
                return false;

            value = configValue.StringValue;
            return true;
        }

        public async UniTask FetchAndActivateAsync(TimeSpan timeout, CancellationToken ct)
        {
            if (_isFetchStarted)
                return;

            _isFetchStarted = true;
            try
            {
                bool isTimeOut = await PrepareAndFetchAsync(ct).TimeoutWithoutException(timeout, DelayType.Realtime);
                if (isTimeOut)
                    Debug.LogWarning("[FirebaseRemoteConfigProvider]: Fetch timed out. Using last session version.");

                if (_rc != null)
                    await _rc.ActivateAsync().AsUniTask().AttachExternalCancellation(ct);
            }
            finally
            {
                IsFetched = true;
                OnFetched?.Invoke();
            }
        }

        #endregion

        #region Class Methods

        private async UniTask PrepareAndFetchAsync(CancellationToken ct)
        {
            var status = await FirebaseApp.CheckAndFixDependenciesAsync().AsUniTask().AttachExternalCancellation(ct);
            if (status != DependencyStatus.Available)
            {
                Debug.LogError($"[FirebaseRemoteConfigProvider]: Firebase can't be used ({status}). Using default");
                return;
            }

            _rc = FirebaseRemoteConfig.DefaultInstance;
            try
            {
                await _rc.FetchAsync().AsUniTask().AttachExternalCancellation(ct);
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                Debug.LogWarning($"[FirebaseRemoteConfigProvider]: Fetch failed - {e.Message}. Using last session version.");
            }
        }

        #endregion
    }
}