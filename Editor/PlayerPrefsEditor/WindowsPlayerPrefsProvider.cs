using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Win32;
using UnityEditor;
using UnityEngine;

namespace Horcrux.Editor.PlayerPrefsEditor
{
    public class WindowsPlayerPrefsProvider : IPlayerPrefsProvider
    {
        private static Encoding encoding = new UTF8Encoding();
        private static List<PlayerPrefsPair> cachedPlayerPrefsPairs = new();
        private string playerPrefsPath;
        
        private string PlayerPrefsPath
        {
            get
            {
                playerPrefsPath ??=
                        $@"Software\Unity\UnityEditor\{Application.companyName}\{Application.productName}";
                return playerPrefsPath;
            }
        }

        public List<PlayerPrefsPair> PlayerPrefsPairs
        {
            get
            {
                cachedPlayerPrefsPairs.Clear();

                using (RegistryKey registryKey = Registry.CurrentUser.OpenSubKey(PlayerPrefsPath))
                {
                    if (registryKey == null) return cachedPlayerPrefsPairs;

                    string[] fileNames = registryKey.GetValueNames();
                    for (int i = 0; i < fileNames.Length; i++)
                    {
                        object fileContent = registryKey.GetValue(fileNames[i]);
                        if (fileContent == null) continue;

                        int idx = fileNames[i].LastIndexOf("_", StringComparison.Ordinal);
                        string standardFileName = idx > 0 ? fileNames[i].Substring(0, idx) : fileNames[i];

                        // Floats come back as int from registry because the float is stored as
                        // 64 bit but marked as 32 bit - which confuses GetValue() greatly.
                        if (fileContent is int)
                        {
                            if (PlayerPrefs.GetInt(standardFileName, -1) == -1 &&
                                PlayerPrefs.GetInt(standardFileName, 0) == 0)
                                fileContent = PlayerPrefs.GetFloat(standardFileName, 0f);
                        }
                        else if (fileContent is byte[])
                        {
                            fileContent = encoding.GetString((byte[])fileContent).TrimEnd('\0');
                        }

                        cachedPlayerPrefsPairs.Add(new PlayerPrefsPair { Key = standardFileName, Value = fileContent });
                    }
                }

                return cachedPlayerPrefsPairs;
            }
        }
    }
}