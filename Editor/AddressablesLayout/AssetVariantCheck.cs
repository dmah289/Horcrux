using System.Collections.Generic;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using Object = UnityEngine.Object;

namespace Horcrux.Editor.AddressablesLayout
{
    /// <summary>
    /// Kiểm một asset chọn được theo tên từ ngoài build (remote config, save) không: address = tên asset, mang label của tập,
    /// mọi bản cùng label chung một group Pack Together có GUID trong catalog. Hàm thuần, không log, không sửa settings.
    /// </summary>
    public static class AssetVariantCheck
    {
        #region API

        /// <summary>Mỗi lỗi một dòng vào <paramref name="errors"/>.</summary>
        public static void Validate(Object variant, string label, List<string> errors)
        {
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null)
            {
                errors.Add("Project chưa có Addressables settings.");
                return;
            }

            var guid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(variant));
            var entry = settings.FindAssetEntry(guid);
            if (entry == null)
            {
                errors.Add($"Chưa addressable: game không nạp được bản này. Thêm vào group chung của label {label}, address {variant.name}, label {label}.");
                return;
            }

            if (entry.address != variant.name)
                errors.Add($"Address \"{entry.address}\" khác tên asset \"{variant.name}\": giá trị gõ theo tên asset sẽ không tìm thấy bản này.");
            if (!entry.labels.Contains(label))
                errors.Add($"Thiếu label {label}: tên đúng vẫn bị host coi là lạ, cheat không liệt kê bản này.");

            if (entry.parentGroup == null)
                errors.Add("Entry không nằm trong group nào.");
            else
                ValidateGroupSchema(entry.parentGroup, errors);

            foreach (var other in AllEntries(settings))
            {
                if (other.guid == guid) continue;
                if (other.address == entry.address)
                    errors.Add($"Trùng address \"{entry.address}\" với {other.AssetPath}: không biết nạp bản nào.");
                // Khác group là khác bundle: phụ thuộc chung (level, sprite) bị nhân bản, đổi bản giữa phiên unload thứ đang dùng.
                if (other.labels.Contains(label) && other.parentGroup != entry.parentGroup)
                    errors.Add($"{other.AssetPath} cùng label {label} mà ở group {other.parentGroup?.Name}, khác group {entry.parentGroup?.Name}: mọi bản cùng label phải chung một group.");
            }
        }

        /// <summary>Có entry mang <paramref name="address"/> và <paramref name="label"/> không — giá trị remote, save có trỏ vào bản chọn được không.</summary>
        public static bool IsSelectable(string address, string label)
        {
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null || string.IsNullOrEmpty(address)) return false;

            foreach (var entry in AllEntries(settings))
                if (entry.address == address && entry.labels.Contains(label)) return true;
            return false;
        }

        /// <summary>Mọi <typeparamref name="T"/> trong <paramref name="folder"/>, sắp theo tên. Thư mục là quy ước của dự án: tìm theo kiểu toàn project nhặt cả nháp, fixture.</summary>
        public static List<T> FindAll<T>(string folder) where T : Object
        {
            var all = new List<T>();
            foreach (var guid in AssetDatabase.FindAssets("t:" + typeof(T).Name, new[] { folder }))
            {
                var asset = AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guid));
                if (asset != null) all.Add(asset);
            }

            all.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
            return all;
        }

        #endregion

        #region Class Methods

        private static void ValidateGroupSchema(AddressableAssetGroup group, List<string> errors)
        {
            var schema = group.GetSchema<BundledAssetGroupSchema>();
            if (schema == null)
            {
                errors.Add($"Group {group.Name} thiếu BundledAssetGroupSchema: bản này không vào bundle nào.");
                return;
            }

            if (schema.BundleMode != BundledAssetGroupSchema.BundlePackingMode.PackTogether)
                errors.Add($"Group {group.Name} không Pack Together: đổi bản giữa phiên unload thứ đang dùng, phụ thuộc chung bị nhân bản.");
            if (!schema.IncludeGUIDInCatalog)
                errors.Add($"Group {group.Name} tắt Include GUID in Catalog: bản mặc định kéo bằng AssetReference (nạp theo GUID) không nạp được.");
        }

        private static IEnumerable<AddressableAssetEntry> AllEntries(AddressableAssetSettings settings)
        {
            foreach (var group in settings.groups)
            {
                if (group == null) continue;
                foreach (var entry in group.entries) yield return entry;
            }
        }

        #endregion
    }
}
