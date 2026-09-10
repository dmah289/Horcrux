using System;
using Sirenix.OdinInspector;
using Newtonsoft.Json;
using UnityEngine;

namespace Horcrux.Runtime.Abstractions.Persistence
{
    /// <summary>
    /// All types are stored in JSON string.
    /// </summary>
    /// <remarks>
    /// <typeparamref name="T"/> must be plain data — no UnityEngine type. See Persistence.md.
    /// </remarks>
    [Serializable]
    public partial class PersistenceDataEntry<T> : IPersistenceDataEntry
    {
        private IPersistenceDataCollection _owner;
        [SerializeField] private string key;

        [SerializeField, Tooltip("Plain data only — no UnityEngine type. Press Validate keys before Play.")]
        private T defaultValue;

        [ShowInInspector, ReadOnly] private T value;
        [ShowInInspector, ReadOnly] private bool isDirty;
        
        public event Action<T> OnValueChanged;


        #region Properties
        public static implicit operator T(PersistenceDataEntry<T> entry)
            => entry != null ? entry.Value : default;

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
        #endregion

        #region API
        public void MarkDirty()
        {
            isDirty = true;
            RaiseChanged();
        }
        
        public void Setup(IPersistenceDataCollection owner)
        {
            _owner = owner;
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

        void IPersistenceDataEntry.ClearDirty()
            => isDirty = false;

        public void FlushNow()
        {
            if (_owner == null)
            {
                Debug.LogError($"[PersistenceDataCollection]: No collection owns {key}]");
                return;
            }
            
            _owner?.Flush(this);
        }

        #endregion

        #region Class Methods
        /// <summary>
        ///  Avoid reference issues by cloning the default value.
        /// </summary>
        /// <returns>
        /// Return a new object with the same value as the default value.
        /// </returns>
        private T CloneDefault()
            => JsonConvert.DeserializeObject<T>(JsonConvert.SerializeObject(defaultValue));
        
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
                    Debug.LogError($"[PersistenceDataCollection]: Error invoking OnValueChanged for key {key}: {e}");
                }
            }
        }
        #endregion
    }
}