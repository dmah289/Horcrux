using System;
using System.Globalization;
using Horcrux.Runtime.Abstractions.RemoteConfigSystem;
using Newtonsoft.Json;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Horcrux.Runtime.Implementations.RemoteConfigSystem
{
    [Serializable]
    public class RCVariable<T> : IRCVariable
    {
        [SerializeField] private string firebaseKey;
        [SerializeField] private bool allowFetching = true;
        [SerializeField] private T value;
        [NonSerialized, ShowInInspector] private bool fetched;

        public string FirebaseKey => firebaseKey;
        public bool AllowFetching
        {
            get => allowFetching;
            set => allowFetching = value;
        }
        public T Value => value;
        
        public static implicit operator T(RCVariable<T> variable) => variable != null ? variable.Value : default;

        public void ResetFetchedState()
        {
            fetched = false;
        }

        public void ApplyRemoteValue(IRemoteConfigProvider provider)
        {
            if (!allowFetching)
                return;

            bool exists = provider.TryGetRemoteValue(firebaseKey, out string fetchedVal);
            if (exists && !string.IsNullOrEmpty(fetchedVal) && TryParseValueFromString(fetchedVal, out T parsed))
            {
                value = parsed;
                PlayerPrefs.SetString(firebaseKey, fetchedVal);
                fetched = true;

                return;
            }

            if(!exists)
                Debug.LogError($"Can't find remote config value for key {firebaseKey}");
            else if(string.IsNullOrEmpty(fetchedVal))
                Debug.LogError($"Remote config value for key {firebaseKey} is empty");
            else
                Debug.LogError($"Can't parse remote config value for key {firebaseKey} with value {fetchedVal} to type {typeof(T)} -> Using cached value in PlayerPrefs");

            string prefsVal = PlayerPrefs.GetString(firebaseKey, string.Empty);
            if(!string.IsNullOrEmpty(prefsVal) && TryParseValueFromString(prefsVal, out T cachedParsed))
                value = cachedParsed;
            else
                Debug.LogError($"Can't find remote config prefs value for key {firebaseKey} -> Using default value set on Editor");
        }

        private bool TryParseValueFromString(string serializedVal, out T result)
        {
            try
            {
                if (typeof(T) == typeof(string))
                {
                    result = (T)(object)serializedVal;
                    return true;
                }

                if (typeof(T).IsEnum)
                {
                    result = (T)Enum.Parse(typeof(T), serializedVal);
                    return true;
                }

                result = Type.GetTypeCode(typeof(T)) switch
                {
                    TypeCode.Int32 => (T)(object)int.Parse(serializedVal),
                    TypeCode.Boolean => (T)(object)bool.Parse(serializedVal),
                    // BẮT BUỘC dùng InvariantCulture để tránh lỗi dấu chấm/phẩy ở các quốc gia khác
                    TypeCode.Single => (T)(object)float.Parse(serializedVal, CultureInfo.InvariantCulture),
                    TypeCode.Double => (T)(object)double.Parse(serializedVal, CultureInfo.InvariantCulture),
                    TypeCode.Int64 => (T)(object)long.Parse(serializedVal),
                    _ => JsonConvert.DeserializeObject<T>(serializedVal),
                };
                return true;
            }
            catch
            {
                Debug.LogError($"Can't parse remote config value for key {firebaseKey} with value {serializedVal} to type {typeof(T)}");
                result = default;
                return false;
            }
        }
        
#if UNITY_EDITOR
        // NonSerialized + ShowInInspector: hiện trong Inspector nhưng KHÔNG serialize.
        // Trước đây dùng [SerializeField] bọc trong #if UNITY_EDITOR → Editor serialize field này
        // vào asset, nhưng build strip field → layout mismatch → crash native:
        // "Read 19208 bytes but expected 19364 bytes"
        [NonSerialized, ShowInInspector, MultiLineProperty]
        private string valueToImport;

        [Button]
        private void CopyJsonToClipboard()
        {
            string json = JsonConvert.SerializeObject(value, Formatting.Indented);
            GUIUtility.systemCopyBuffer = json;
            Debug.Log($"Copied RCVariable with key {firebaseKey} to clipboard:\n{json}");
        }

        [Button]
        private void ImportJson()
        {
            if(!string.IsNullOrEmpty(valueToImport) && TryParseValueFromString(valueToImport, out T parsed))
                value = parsed;
        }

        /// <summary>Copies the value out as CSV, so an edited sheet can come straight back in.</summary>
        [Button]
        private void CopyCsvToClipboard()
        {
            if (!RCVariableCsv.TryFormat(typeof(T), value, out string csv, out string error))
            {
                Debug.LogError($"RCVariable {firebaseKey} has no CSV form: {error}");
                return;
            }
            GUIUtility.systemCopyBuffer = csv;
            Debug.Log($"Copied RCVariable with key {firebaseKey} to clipboard as CSV:\n{csv}");
        }

        /// <summary>Reads the import box as CSV instead of JSON.</summary>
        /// <remarks>All-or-nothing: a rejected sheet leaves the current value alone.</remarks>
        [Button]
        private void ImportCsv()
        {
            if (!RCVariableCsv.TryParse(typeof(T), valueToImport, out object parsed, out string report))
            {
                Debug.LogError($"RCVariable {firebaseKey} CSV import aborted, nothing written: {report}");
                return;
            }
            value = (T)parsed;
            Debug.Log($"Imported {report} into RCVariable with key {firebaseKey} from CSV");
        }
#endif
    }
}