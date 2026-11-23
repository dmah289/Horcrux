using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Horcrux.Editor.PlayerPrefsEditor;
using UnityEngine;

namespace Horcrux.Editor.Probe
{
    /// <summary>
    /// Sao lưu và trả PlayerPrefs của Editor quanh một lượt Play do công cụ chạy: lượt đo đi trên save thật của developer, thắng
    /// level, cộng tiền, mở tính năng là đổi tiến độ của họ. Gọi ngoài Play — game flush save lúc thoát Play, trả trước đó thì bị
    /// ghi đè. Chỉ PlayerPrefs; file trong <c>persistentDataPath</c> và dữ liệu đã đẩy lên server không lấy lại được.
    /// </summary>
    public static class PrefsSnapshot
    {
        [Serializable]
        private class Entry
        {
            public string key;
            public string type;
            public string value;
        }

        [Serializable]
        private class Snapshot
        {
            public List<Entry> entries = new();
        }

        /// <summary>Ghi mọi key PlayerPrefs ra <paramref name="path"/> (JSON), trả số key.</summary>
        public static string Take(string path)
        {
            if (Application.isPlaying) throw new InvalidOperationException("PrefsSnapshot.Take gọi ngoài Play.");
            var snapshot = new Snapshot { entries = Read() };
            File.WriteAllText(path, JsonUtility.ToJson(snapshot));
            return $"prefs: chụp {snapshot.entries.Count} key";
        }

        /// <summary>
        /// Đưa PlayerPrefs về đúng bản ở <paramref name="path"/>: ghi lại key đã đổi hay đã mất, xoá key sinh ra sau lúc chụp, rồi
        /// đọc lại để so. Trả số key đã trả và số key còn lệch (khác 0 là có thứ ghi đè sau lệnh này).
        /// </summary>
        public static string Restore(string path)
        {
            if (Application.isPlaying) throw new InvalidOperationException("PrefsSnapshot.Restore gọi ngoài Play.");
            var saved = JsonUtility.FromJson<Snapshot>(File.ReadAllText(path)).entries;
            var savedByKey = new Dictionary<string, Entry>(saved.Count);
            foreach (var entry in saved) savedByKey[entry.key] = entry;

            int deleted = 0, written = 0;
            foreach (var entry in Read())
            {
                if (!savedByKey.Remove(entry.key, out var old))
                {
                    PlayerPrefs.DeleteKey(entry.key);
                    deleted++;
                }
                else if (old.type != entry.type || old.value != entry.value)
                {
                    Write(old);
                    written++;
                }
            }
            foreach (var missing in savedByKey.Values)
            {
                Write(missing);
                written++;
            }
            PlayerPrefs.Save();

            return $"prefs: trả {written} key, xoá {deleted} key mới, còn lệch {CountDifferences(saved)}";
        }

        private static int CountDifferences(List<Entry> saved)
        {
            var now = new Dictionary<string, Entry>();
            foreach (var entry in Read()) now[entry.key] = entry;
            int differences = 0;
            foreach (var entry in saved)
            {
                if (!now.Remove(entry.key, out var current) || current.type != entry.type || current.value != entry.value)
                    differences++;
            }
            return differences + now.Count;
        }

        // Đọc qua provider của Player Prefs Editor: PlayerPrefs không có API liệt kê key. Registry trả float dạng long (QWORD)
        // mà provider không đổi: lấy lại giá trị qua PlayerPrefs.GetFloat.
        private static List<Entry> Read()
        {
            var entries = new List<Entry>();
            foreach (var pair in new WindowsPlayerPrefsProvider().PlayerPrefsPairs)
            {
                var entry = pair.Value switch
                {
                    int i => new Entry { type = "int", value = i.ToString(CultureInfo.InvariantCulture) },
                    float f => new Entry { type = "float", value = f.ToString("R", CultureInfo.InvariantCulture) },
                    long => new Entry
                    {
                        type = "float", value = PlayerPrefs.GetFloat(pair.Key).ToString("R", CultureInfo.InvariantCulture),
                    },
                    string s => new Entry { type = "string", value = s },
                    _ => null,
                };
                if (entry == null) continue;
                entry.key = pair.Key;
                entries.Add(entry);
            }
            return entries;
        }

        private static void Write(Entry entry)
        {
            switch (entry.type)
            {
                case "int": PlayerPrefs.SetInt(entry.key, int.Parse(entry.value, CultureInfo.InvariantCulture)); break;
                case "float": PlayerPrefs.SetFloat(entry.key, float.Parse(entry.value, CultureInfo.InvariantCulture)); break;
                default: PlayerPrefs.SetString(entry.key, entry.value); break;
            }
        }
    }
}
