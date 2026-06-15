using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Horcrux.Runtime.Abstractions.RemoteConfigSystem;
using Horcrux.Runtime.Abstractions.Singleton;
using Sirenix.OdinInspector;
using Sisus.Init;
using UnityEngine;

namespace Horcrux.Runtime.Implementations.RemoteConfigSystem
{
    [AttributeUsage(AttributeTargets.Field)]
    public sealed class RegisteredRCVar : Attribute { }
    
    [Service(typeof(IRCVariableCollection))]
    [CreateAssetMenu(menuName = "manhnd_sdk/RCVariableCollection", fileName = "RCVariableCollection")]
    public partial class RCVariableCollection : SingletonSO<RCVariableCollection>, IRCVariableCollection
    {
        private List<IRCVariable> rcVariables = new();
        private List<FieldInfo> cachedRCFields;
        private IRemoteConfigProvider provider;
        
        public IEnumerable<IRCVariable> RCVariables => rcVariables;
        public IRemoteConfigProvider RemoteConfigProvider => provider;

        private List<FieldInfo> GetRCFields()
        {
            return cachedRCFields ??= GetType().GetFields(BindingFlags.NonPublic | BindingFlags.Instance)
                .Where(field => field.IsDefined(typeof(RegisteredRCVar), false)).ToList();
        }

        public void Initialize()
        {
            rcVariables.Clear();
            List<FieldInfo> rcFields = GetRCFields();

            for (int i = 0; i < rcFields.Count; i++)
                rcVariables.Add((IRCVariable)rcFields[i].GetValue(this));

            if (IRemoteConfigProvider.TryGet(out provider))
            {
                provider.OnFetched -= OnRemoteConfigFetched;
                provider.OnFetched += OnRemoteConfigFetched;

                if (provider.IsFetched)
                    OnRemoteConfigFetched();
            }
            else Debug.LogError("No IRemoteConfigProvider found in the scene. Please make sure to have one in order to fetch remote config values.");
        }

        private void OnRemoteConfigFetched()
        {
            for(int i = 0; i < rcVariables.Count; i++)
                rcVariables[i].ApplyRemoteValue(provider);
        }

        private void OnDestroy()
        {
            if (provider != null)
                provider.OnFetched -= OnRemoteConfigFetched;
        }

#if UNITY_EDITOR
        [Button]
        [GUIColor("cyan")]
        public void SearchFirebaseKeyUsage(string firebaseKey)
        {
            List<FieldInfo> rcFields = GetRCFields();

            for(int i = 0; i < rcFields.Count; i++)
            {
                IRCVariable variable = (IRCVariable)rcFields[i].GetValue(this);
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
            List<FieldInfo> rcFields = GetRCFields();

            for (int i = 0; i < rcFields.Count; i++)
            {
                IRCVariable variable = (IRCVariable)rcFields[i].GetValue(this);
                variable.AllowFetching = true;
            }

            Debug.Log($"Enabled allowFetching on {rcFields.Count} RCVariables");
        }
#endif
    }
}