using System;
using System.Collections.Generic;
using System.Reflection;
using Horcrux.Runtime.Abstractions.RemoteConfigSystem;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Horcrux.Runtime.Implementations.RemoteConfigSystem
{
    [AttributeUsage(AttributeTargets.Field)]
    public sealed class MarkedRemoteConfig : Attribute { }
    
    /// <summary>
    /// A game project derives one sealed class from this and declares the variables there.
    /// </summary>
    /// <remarks>
    /// It doesn't pick up private field declared on this class.<br/>
    /// A variable added here would have to be protected.
    /// </remarks>>
    public abstract class BaseRemoteConfigCollection : ScriptableObject, IRemoteConfigCollection
    {
        private List<IRemoteConfig> remoteConfigs = new();
        private List<FieldInfo> cachedRemoteConfigFields;
        private IRemoteConfigProvider remoteConfigProvider;
        private bool allRemoteConfigsApplied;
        
        public IEnumerable<IRemoteConfig> RemoteConfigs => remoteConfigs;
        public IRemoteConfigProvider RemoteConfigProvider => remoteConfigProvider;
        public bool AllRemoteConfigsApplied =>  allRemoteConfigsApplied;
        

        #region Unity Callbacks
        protected virtual void OnDestroy()
        {
            if (remoteConfigProvider != null)
                remoteConfigProvider.OnFetched -= OnRemoteConfigFetched;
        }
        #endregion

        private List<FieldInfo> GetRemoteConfigFields()
        {
            if (cachedRemoteConfigFields != null)
                return cachedRemoteConfigFields;
            
            FieldInfo[] allFields = GetType().GetFields(BindingFlags.NonPublic | BindingFlags.Instance);
            
            List<FieldInfo> rcFields = new List<FieldInfo>();
            for (int i = 0; i < allFields.Length; i++)
            {
                if(allFields[i].IsDefined(typeof(MarkedRemoteConfig), false))
                    rcFields.Add(allFields[i]);
            }
            
            return cachedRemoteConfigFields = rcFields;
        }

        public void Initialize()
        {
            allRemoteConfigsApplied = false;
            remoteConfigs.Clear();
            
            List<FieldInfo> rcFields = GetRemoteConfigFields();
            for (int i = 0; i < rcFields.Count; i++)
            {
                if (rcFields[i].GetValue(this) is not IRemoteConfig variable)
                {
                    Debug.LogError($"[RemoteConfig] {rcFields[i].Name} is marked {nameof(MarkedRemoteConfig)} " +
                                   $"but holds no {nameof(IRemoteConfig)}");
                    continue;
                }

                variable.ResetFetchedState();
                remoteConfigs.Add(variable);
            }
            
            if (remoteConfigProvider != null)
                remoteConfigProvider.OnFetched -= OnRemoteConfigFetched;

            if (IRemoteConfigProvider.TryGet(out remoteConfigProvider))
            {
                remoteConfigProvider.OnFetched += OnRemoteConfigFetched;

                if (remoteConfigProvider.IsFetched)
                    OnRemoteConfigFetched();
            }
            else 
                Debug.LogError("No IRemoteConfigProvider found in the scene. Please make sure to have one in order to fetch remote config values.");
        }

        private void OnRemoteConfigFetched()
        {
            for(int i = 0; i < remoteConfigs.Count; i++)
                remoteConfigs[i].ApplyRemoteValue(remoteConfigProvider);

            OnRemoteConfigsApplied();
        }
        
        protected virtual void OnRemoteConfigsApplied()
        {
            allRemoteConfigsApplied = true;
        }

#if UNITY_EDITOR
        [Button]
        [GUIColor("cyan")]
        public void SearchFirebaseKeyUsage(string firebaseKey)
        {
            List<FieldInfo> rcFields = GetRemoteConfigFields();

            for(int i = 0; i < rcFields.Count; i++)
            {
                IRemoteConfig variable = (IRemoteConfig)rcFields[i].GetValue(this);
                if (variable.FirebaseKey == firebaseKey)
                {
                    Debug.Log($"Found usage of firebase key {firebaseKey} in field {rcFields[i].Name}");
                    break;
                }
            }
        }

        [Button]
        [GUIColor("green")]
        private void EnableAllFetching()
        {
            List<FieldInfo> rcFields = GetRemoteConfigFields();

            for (int i = 0; i < rcFields.Count; i++)
            {
                IRemoteConfig variable = (IRemoteConfig)rcFields[i].GetValue(this);
                variable.AllowFetching = true;
            }

            Debug.Log($"Enabled allowFetching on {rcFields.Count} remote configs");
        }
#endif
    }
}