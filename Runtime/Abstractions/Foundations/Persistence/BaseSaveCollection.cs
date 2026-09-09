using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace Horcrux.Runtime.Abstractions.Persistence
{
    [AttributeUsage(AttributeTargets.Field)]
    public sealed class MarkedPersistence : Attribute { }
    
    /// <summary>
    /// Holding the value as JSON. Lose at most 1 autosave cycle
    /// </summary>
    public abstract partial class BaseSaveCollection : ScriptableObject, ISaveCollection
    {
        public const string KeyPrefix = "save.";
        
        public IReadOnlyList<ISaveEntry> Entries => entries;
        public bool IsInitialized => isInitialized;
        
        private readonly List<ISaveEntry> entries = new();
        private bool isInitialized;
        private List<FieldInfo> cachedSaveEntryFields;


        protected virtual void ResetDerivedState() { }
        protected virtual void OnEntriesLoaded() { }
        
        public void Initialize()
        {
            ScanEntries(entries);

            for (int i = 0; i < entries.Count; i++)
                entries[i].ResetRuntimeState();
            ResetDerivedState();

            LoadAll();
            OnEntriesLoaded();
            
            isInitialized = true;
        }

        private void LoadAll()
        {
            for (int i = 0; i < entries.Count; i++)
            {
                ISaveEntry entry =  entries[i];
                string storageKey = KeyPrefix + entry.Key;
                
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
                    Debug.Log($"[SaveCollection]: Reading entry '{entry.Key}' failed — using its default", this);
                }
            }
        }

        private int ScanEntries(List<ISaveEntry> into)
        {
            into.Clear();
            int rejectedCount = 0;
            Dictionary<string, string> keyOwners = new();
            
            List<FieldInfo> saveEntryFields = GetSaveEntryFields();
            for (int i = 0; i < saveEntryFields.Count; i++)
            {
                FieldInfo field = saveEntryFields[i];
                ISaveEntry saveEntry = field.GetValue(this) as ISaveEntry;

                if (saveEntry == null)
                {
                    Debug.LogError($"[SaveCollection]: Field {field.Name} is marked " +
                                   $"{nameof(MarkedPersistence)} but not SaveEntry type.");
                    rejectedCount++;
                    continue;
                }

                if (string.IsNullOrEmpty(saveEntry.Key))
                {
                    Debug.LogError($"[SaveCollection]: Field {field.Name} has an empty key.");
                    rejectedCount++;
                    continue;
                }

                if (keyOwners.TryGetValue(saveEntry.Key, out string owner))
                {
                    Debug.LogError($"[SaveCollection]: Key {saveEntry.Key} is both on {owner} and {field.Name}. Keeping '{owner}'", this);
                    rejectedCount++;
                    continue;
                }
                
                keyOwners.Add(saveEntry.Key, field.Name);
                into.Add(saveEntry);
            }

            return rejectedCount;
        }

        private List<FieldInfo> GetSaveEntryFields()
        {
            if (cachedSaveEntryFields != null)
                return cachedSaveEntryFields;
            
            FieldInfo[] allFields = GetType().GetFields(BindingFlags.NonPublic | BindingFlags.Instance);
            List<FieldInfo> saveEntryFields = new List<FieldInfo>(allFields.Length);

            for (int i = 0; i < allFields.Length; i++)
            {
                if(allFields[i].IsDefined(typeof(MarkedPersistence), false))
                    saveEntryFields.Add(allFields[i]);
            }
            return cachedSaveEntryFields = saveEntryFields;
            
        }

        public void FlushAll()
        {
            for (int i = 0; i < entries.Count; i++)
            {
                ISaveEntry saveEntry = entries[i];
                
                if (!saveEntry.IsDirty)
                    continue;
                
                PlayerPrefs.SetString(KeyPrefix + saveEntry.Key, saveEntry.WritePayload());
                saveEntry.ClearDirty();
            }
            
            PlayerPrefs.Save();
        }
    }
}