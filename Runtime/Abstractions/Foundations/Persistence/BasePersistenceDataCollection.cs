using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Horcrux.Runtime.Abstractions.Persistence
{
    [AttributeUsage(AttributeTargets.Field)]
    public sealed class MarkedPersistence : Attribute { }
    
    public abstract partial class BasePersistenceDataCollection : ScriptableObject, IPersistenceDataCollection
    {
        private const string KeyPrefix = "persistence_";

        [SerializeField, Min(5f)]
        private float autosaveIntervalSeconds = 10f;

        private readonly List<IPersistenceDataEntry> entries = new();
        private readonly List<IPersistenceDataEntry> pendingClearDirtyEntries = new();
        private bool isInitialized;
        private List<FieldInfo> cachedMarkedPersistenceFields;

        #region Properties
        public IReadOnlyList<IPersistenceDataEntry> Entries => entries;
        
        public bool IsInitialized => isInitialized;
        #endregion

        #region API
        public void Initialize()
        {
            if (isInitialized)
                return;
            
            ScanEntries(entries);
            
            for(int i = 0; i < entries.Count; i++)
                entries[i].Setup(this);
            isInitialized = true;
            ResetDerivedState();

            LoadAll();
            OnEntriesLoaded();
        }
        
        public void FlushAll()
            => FlushInternal(null);
        
        public async UniTask RunAutosaveAsync(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                // Realtime: autosave keeps ticking while the game sits at timeScale = 0.
                await UniTask.Delay((int)(autosaveIntervalSeconds * 1000f),
                    DelayType.Realtime, cancellationToken: ct);

                if (ct.IsCancellationRequested)
                    return;

                FlushInternal(null);
            }
        }

        private void LoadAll()
        {
            for (int i = 0; i < entries.Count; i++)
            {
                IPersistenceDataEntry entry =  entries[i];
                string storageKey = GetFinalKey(entry.Key);
                
                // keeps default value
                if (!PlayerPrefs.HasKey(storageKey))
                    continue;
                
                string payload = PlayerPrefs.GetString(storageKey);
                
                if(string.IsNullOrEmpty(payload))
                    continue;

                try
                {
                    entry.ReadPayload(payload);
                }
                catch (Exception e)
                {
                    Debug.LogError($"[PersistenceDataCollection]: Reading entry '{entry.Key}' failed — using its default", this);
                    Debug.LogException(e, this);
                }
            }
        }
        
        public void Flush(IPersistenceDataEntry entry)
        {
            if (entry == null)
            {
                Debug.LogError("[PersistenceDataCollection]: Flush null entry.");
                return;
            }

            if (!entries.Contains(entry))
            {
                Debug.LogError("[PersistenceDataCollection]: Entry not found in persistence data collection.", this);
                return;
            }

            FlushInternal(entry);
        }
        #endregion

        #region Class Methods
        /// <summary>
        /// Hook to reset subclass state
        /// </summary>
        protected virtual void ResetDerivedState() { }
        
        protected virtual void OnEntriesLoaded() { }

        /// <summary>The one flush body. A null <paramref name="only"/> flushes every dirty entry, otherwise just that one.</summary>
        private void FlushInternal(IPersistenceDataEntry only)
        {
            pendingClearDirtyEntries.Clear();

            // Phase one: hand the payloads to PlayerPrefs. Dirty stays on — this is still memory.
            for (int i = 0; i < entries.Count; i++)
            {
                IPersistenceDataEntry entry = entries[i];

                if (only != null && !ReferenceEquals(entry, only))
                    continue;

                if (!entry.IsDirty)
                    continue;

                try
                {
                    PlayerPrefs.SetString(GetFinalKey(entry.Key), entry.WritePayload());
                    pendingClearDirtyEntries.Add(entry);
                }
                catch (Exception e)
                {
                    Debug.LogError($"[PersistenceDataCollection]: Writing entry '{entry.Key}' failed" +
                                   " — it stays dirty and retries next flush.", this);
                    Debug.LogException(e, this);
                }
            }

            if (pendingClearDirtyEntries.Count == 0)
                return;

            // Phase two: the only call that reaches storage. Dirty clears after it lands, never before.
            PlayerPrefs.Save();

            for (int i = 0; i < pendingClearDirtyEntries.Count; i++)
                pendingClearDirtyEntries[i].ClearDirty();
        }

        private string GetFinalKey(string entryKey)
            => KeyPrefix + entryKey;
        
        private int ScanEntries(List<IPersistenceDataEntry> into)
        {
            into.Clear();
            int rejectedCount = 0;
            Dictionary<string, string> keyOwners = new();
            List<FieldInfo> fields = GetMarkedPersistenceFields();
            
            for (int i = 0; i < fields.Count; i++)
            {
                FieldInfo field = fields[i];
                IPersistenceDataEntry entry = field.GetValue(this) as IPersistenceDataEntry;

                if (entry == null)
                {
                    Debug.LogError($"[PersistenceDataCollection]: Field '{field.Name}' is marked " +
                                   "MarkedPersistence but not PersistenceDataEntry type.");
                    rejectedCount++;
                    continue;
                }

                if (string.IsNullOrEmpty(entry.Key))
                {
                    Debug.LogError($"[PersistenceDataCollection]: Field '{field.Name}' has an empty key.");
                    rejectedCount++;
                    continue;
                }

                if (keyOwners.TryGetValue(entry.Key, out string owner))
                {
                    Debug.LogError($"[PersistenceDataCollection]: Key {entry.Key} is on both field '{owner}'" +
                                   $" and field '{field.Name}'. Keeping '{owner}'", this);
                    rejectedCount++;
                    continue;
                }
                
                keyOwners.Add(entry.Key, field.Name);
                into.Add(entry);
            }

            return rejectedCount;
        }

        private List<FieldInfo> GetMarkedPersistenceFields()
        {
            if (cachedMarkedPersistenceFields != null)
                return cachedMarkedPersistenceFields;
            
            FieldInfo[] allFields = GetType().GetFields(BindingFlags.NonPublic | BindingFlags.Instance);
            List<FieldInfo> markedPersistenceFields = new List<FieldInfo>(allFields.Length);

            for (int i = 0; i < allFields.Length; i++)
            {
                if(allFields[i].IsDefined(typeof(MarkedPersistence), false))
                    markedPersistenceFields.Add(allFields[i]);
            }
            return cachedMarkedPersistenceFields = markedPersistenceFields;
            
        }
        #endregion
    }
}