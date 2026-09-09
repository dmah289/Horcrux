using System;
using Sirenix.OdinInspector;
using Newtonsoft.Json;
using UnityEngine;

namespace Horcrux.Runtime.Abstractions.Persistence
{
    /// <summary>
    /// All types are stored in string.
    /// </summary>
    [Serializable]
    public partial class SaveEntry<T> : ISaveEntry
    {
        [SerializeField] private string key;
        [SerializeField] private T defaultValue;

        [ShowInInspector, ReadOnly] private T value;
        [ShowInInspector, ReadOnly] private bool isDirty;
        
        public event Action<T> OnValueChanged;
        
        
        public static implicit operator T(SaveEntry<T> entry) => entry != null ? entry.Value : default;

        public string Key => key;
        
        public bool IsDirty => isDirty;

        public T Value
        {
            get => value;
            set
            {
                this.value = value;
                MarkDirty();
            }
        }

        public void MarkDirty()
        {
            isDirty = true;
            RaiseChanged();
        }

        private void RaiseChanged()
        {
            Action<T> handlers = OnValueChanged;

            if (handlers == null)
                return;
            
            Delegate[] delegates = handlers.GetInvocationList();
            for(int i = 0; i < delegates.Length; i++)
            {
                try
                {
                    Action<T> action = (Action<T>)delegates[i];
                    action(value);
                }
                catch (Exception e)
                {
                    Debug.LogError($"[SaveCollection]: Error invoking OnValueChanged for key {key}: {e}");
                }
            }
        }
        
        public void ResetRuntimeState()
        {
            OnValueChanged = null;
            isDirty = false;
            value = CloneDefault();
        }

        public void ReadPayload(string payload)
        {
            T loaded = JsonConvert.DeserializeObject<T>(payload);
            value = loaded == null ? CloneDefault() : loaded;
            isDirty = false;
            RaiseChanged();
        }

        public string WritePayload()
            => JsonConvert.SerializeObject(value);

        public void ClearDirty()
            => isDirty = false;

        /// <summary>
        ///  Avoid reference issues by cloning the default value.
        /// </summary>
        /// <returns>
        /// Return a new object with the same value as the default value.
        /// </returns>
        private T CloneDefault()
            => JsonConvert.DeserializeObject<T>(JsonConvert.SerializeObject(defaultValue));
    }
}