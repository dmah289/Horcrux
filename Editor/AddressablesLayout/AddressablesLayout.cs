using System.Collections.Generic;
using UnityEditor;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEngine;

namespace Horcrux.Editor.AddressablesLayout
{
    /// <summary>
    /// Lõi idempotent để một dự án khai bố trí Addressables bằng bảng tĩnh rồi áp lại bao nhiêu lần cũng ra cùng kết quả:
    /// group tồn tại với đúng packing, entry ở đúng group với đúng address và label, address chết được gỡ.
    /// </summary>
    public static class AddressablesLayout
    {
        /// <summary>Group có sẵn thì chỉnh schema, chưa có thì tạo; build path và load path luôn là local.</summary>
        public static AddressableAssetGroup EnsureGroup(AddressableAssetSettings settings, string name,
            BundledAssetGroupSchema.BundlePackingMode packing,
            BundledAssetGroupSchema.BundleCompressionMode compression = BundledAssetGroupSchema.BundleCompressionMode.LZ4)
        {
            var group = settings.FindGroup(name);
            if (group == null)
            {
                group = settings.CreateGroup(name, false, false, true, null, typeof(BundledAssetGroupSchema), typeof(ContentUpdateGroupSchema));
                Debug.Log($"[AddressablesLayout]: Created group '{name}'.");
            }

            var schema = group.GetSchema<BundledAssetGroupSchema>();
            if (schema == null)
                schema = group.AddSchema<BundledAssetGroupSchema>();

            schema.BundleMode = packing;
            schema.Compression = compression;
            schema.IncludeInBuild = true;
            schema.BuildPath.SetVariableByName(settings, AddressableAssetSettings.kLocalBuildPath);
            schema.LoadPath.SetVariableByName(settings, AddressableAssetSettings.kLocalLoadPath);
            EditorUtility.SetDirty(schema);
            EditorUtility.SetDirty(group);
            return group;
        }

        /// <summary>Entry của asset ở đúng group và address; label giữ nguyên. Asset không có thì ghi vào errors và trả false.</summary>
        public static bool EnsureEntry(AddressableAssetSettings settings, AddressableAssetGroup group, string assetPath, string address,
            List<string> errors)
        {
            var entry = MoveEntry(settings, group, assetPath, address, errors);
            return entry != null;
        }

        /// <summary>Như trên, và tập label của entry thành đúng <paramref name="labels"/>: label thừa gỡ, label thiếu thêm.</summary>
        public static bool EnsureEntry(AddressableAssetSettings settings, AddressableAssetGroup group, string assetPath, string address,
            string[] labels, List<string> errors)
        {
            var entry = MoveEntry(settings, group, assetPath, address, errors);
            if (entry == null)
                return false;

            // entry.labels là HashSet đang bị sửa trong vòng lặp: chép ra trước.
            var currLabels = new List<string>(entry.labels);
            foreach (var label in currLabels)
            {
                if (System.Array.IndexOf(labels, label) < 0)
                    entry.SetLabel(label, false, false, false);
            }

            foreach (var label in labels)
            {
                settings.AddLabel(label, false);
                entry.SetLabel(label, true, false, false);
            }

            return true;
        }

        /// <summary>Gỡ mọi entry mang address này ở mọi group; trả số entry đã gỡ.</summary>
        public static int RemoveEntriesByAddress(AddressableAssetSettings settings, string address)
        {
            var removed = new List<AddressableAssetEntry>();
            foreach (var group in settings.groups)
            {
                if (group == null) continue;
                foreach (var entry in group.entries)
                {
                    if (entry.address == address)
                        removed.Add(entry);
                }
            }

            foreach (var entry in removed)
            {
                settings.RemoveAssetEntry(entry.guid, false);
                Debug.Log($"[AddressablesLayout]: Removed entry '{address}' ({entry.AssetPath}).");
            }

            return removed.Count;
        }

        /// <summary>Một lần ghi cho cả đợt: gọi sau khi mọi EnsureGroup, EnsureEntry, RemoveEntriesByAddress đã xong.</summary>
        public static void Save(AddressableAssetSettings settings)
        {
            settings.SetDirty(AddressableAssetSettings.ModificationEvent.BatchModification, null, true, true);
            AssetDatabase.SaveAssets();
        }

        private static AddressableAssetEntry MoveEntry(AddressableAssetSettings settings, AddressableAssetGroup group, string assetPath,
            string address, List<string> errors)
        {
            var guid = AssetDatabase.AssetPathToGUID(assetPath);
            if (string.IsNullOrEmpty(guid) || string.IsNullOrEmpty(AssetDatabase.GUIDToAssetPath(guid)))
            {
                errors.Add($"asset not found: {assetPath}");
                return null;
            }

            var entry = settings.CreateOrMoveEntry(guid, group, false, false);
            entry.SetAddress(address, false);
            return entry;
        }
    }
}
