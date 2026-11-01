using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using Horcrux.Editor.Utilities;
using UnityEditor;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.U2D;
using UnityEngine;
using UnityEngine.U2D;

namespace Horcrux.Editor.SpriteAtlasBuilder
{
    /// <summary>Một thư mục icon thành một atlas; dự án quyết thư mục nào, atlas đặt đâu.</summary>
    public readonly struct FolderAtlas
    {
        public readonly string FolderPath;
        public readonly string AtlasPath;

        public FolderAtlas(string folderPath, string atlasPath)
        {
            FolderPath = folderPath;
            AtlasPath = atlasPath;
        }
    }

    /// <summary>Cài đặt importer áp cho mọi atlas của một đợt; mặc định là bộ đã dùng cho icon mobile.</summary>
    [Serializable]
    public struct AtlasImportSpec
    {
        public int padding;
        public int maxTextureSize;
        public TextureImporterFormat format;
        public string[] platforms;

        public static AtlasImportSpec MobileIcons => new()
        {
            padding = 4,
            maxTextureSize = 2048,
            format = TextureImporterFormat.ASTC_6x6,
            platforms = new[] { "Android", "iPhone" },
        };
    }

    /// <summary>
    /// Dựng atlas v2 theo thư mục: chuẩn hoá importer của từng ảnh, ghi atlas, áp cài đặt importer atlas, xoá atlas mồ côi.
    /// Mỗi bước chạy lại ra cùng kết quả; bước nào không đổi gì thì không reimport.
    /// </summary>
    public static class SpriteAtlasBuilder
    {
        /// <summary>Đường dẫn ảnh trong thư mục, sắp theo thứ tự tự nhiên ("1 (2)" trước "1 (10)").</summary>
        public static List<string> FindTexturePaths(string folderPath, string extension = ".png")
        {
            var paths = new List<string>();
            foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { folderPath }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.EndsWith(extension, StringComparison.OrdinalIgnoreCase) && !paths.Contains(path))
                    paths.Add(path);
            }

            paths.Sort(NaturalCompare);
            return paths;
        }

        /// <summary>
        /// Mỗi ảnh thành Sprite Single ở <paramref name="pixelsPerUnit"/>, không mipmap, alpha là trong suốt, không rect Multiple cũ,
        /// không physics shape dự phòng. Trả số ảnh phải reimport.
        /// </summary>
        public static int NormalizeSpriteImporters(IReadOnlyList<string> texturePaths, float pixelsPerUnit)
        {
            var changed = 0;
            AssetDatabase.StartAssetEditing();
            try
            {
                foreach (var path in texturePaths)
                {
                    if (AssetImporter.GetAtPath(path) is not TextureImporter importer) continue;
                    if (NormalizeSpriteImporter(importer, pixelsPerUnit))
                        changed++;
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }

            return changed;
        }

        /// <summary>Ghi file .spriteatlasv2 có đúng một packable là thư mục; atlas đã có thì thay packable, giữ GUID.</summary>
        public static void WriteFolderAtlases(IReadOnlyList<FolderAtlas> atlases)
        {
            AssetDatabase.StartAssetEditing();
            try
            {
                foreach (var atlas in atlases)
                {
                    var folder = AssetDatabase.LoadAssetAtPath<DefaultAsset>(atlas.FolderPath);
                    if (folder == null)
                    {
                        Debug.LogError($"[SpriteAtlasBuilder]: Folder not found: {atlas.FolderPath}");
                        continue;
                    }

                    FolderUtility.CreateFolderRecursively(Path.GetDirectoryName(atlas.AtlasPath));

                    SpriteAtlasAsset atlasAsset;
                    if (File.Exists(atlas.AtlasPath))
                    {
                        atlasAsset = SpriteAtlasAsset.Load(atlas.AtlasPath);
                        if (atlasAsset == null)
                            atlasAsset = new SpriteAtlasAsset();
                        var imported = AssetDatabase.LoadAssetAtPath<SpriteAtlas>(atlas.AtlasPath);
                        var existing = imported != null ? imported.GetPackables() : null;
                        if (existing != null && existing.Length > 0)
                            atlasAsset.Remove(existing);
                    }
                    else
                    {
                        atlasAsset = new SpriteAtlasAsset();
                    }

                    atlasAsset.Add(new UnityEngine.Object[] { folder });
                    SpriteAtlasAsset.Save(atlasAsset, atlas.AtlasPath);
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }

            AssetDatabase.Refresh();
        }

        /// <summary>Include in build, packing không xoay không tight, không mipmap, Bilinear, sRGB, và cỡ với format theo spec cho từng platform.</summary>
        public static int ApplyImportSpec(IReadOnlyList<string> atlasPaths, in AtlasImportSpec spec)
        {
            var changed = 0;
            AssetDatabase.StartAssetEditing();
            try
            {
                foreach (var path in atlasPaths)
                {
                    if (AssetImporter.GetAtPath(path) is not SpriteAtlasImporter importer) continue;
                    if (ApplyImportSpec(importer, spec))
                        changed++;
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }

            AssetDatabase.Refresh();
            return changed;
        }

        /// <summary>Xoá atlas trong thư mục không nằm trong <paramref name="keepAtlasPaths"/>, kèm entry Addressables của nó. Trả số đã xoá.</summary>
        public static int RemoveOrphanAtlases(string atlasDir, ICollection<string> keepAtlasPaths, AddressableAssetSettings settings)
        {
            var orphans = new List<string>();
            foreach (var guid in AssetDatabase.FindAssets("t:SpriteAtlas", new[] { atlasDir }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.EndsWith(".spriteatlasv2", StringComparison.OrdinalIgnoreCase) && !keepAtlasPaths.Contains(path) && !orphans.Contains(path))
                    orphans.Add(path);
            }

            foreach (var path in orphans)
            {
                var guid = AssetDatabase.AssetPathToGUID(path);
                if (settings != null && !string.IsNullOrEmpty(guid) && settings.FindAssetEntry(guid) != null)
                    settings.RemoveAssetEntry(guid, false);
                AssetDatabase.DeleteAsset(path);
                Debug.Log($"[SpriteAtlasBuilder]: Removed orphan atlas {path}.");
            }

            if (orphans.Count > 0 && settings != null)
                settings.SetDirty(AddressableAssetSettings.ModificationEvent.EntryRemoved, null, true, true);
            return orphans.Count;
        }

        /// <summary>So tên file không đuôi theo từng cụm số: "6a" trước "6b", "1 (2)" trước "1 (10)".</summary>
        public static int NaturalCompare(string a, string b)
        {
            var ta = Regex.Split(Path.GetFileNameWithoutExtension(a), @"(\d+)");
            var tb = Regex.Split(Path.GetFileNameWithoutExtension(b), @"(\d+)");
            var n = Math.Min(ta.Length, tb.Length);
            for (var i = 0; i < n; i++)
            {
                if (ta[i] == tb[i]) continue;
                if (long.TryParse(ta[i], out var na) && long.TryParse(tb[i], out var nb))
                    return na.CompareTo(nb);
                return string.Compare(ta[i], tb[i], StringComparison.OrdinalIgnoreCase);
            }

            return ta.Length.CompareTo(tb.Length);
        }

        private static bool NormalizeSpriteImporter(TextureImporter importer, float pixelsPerUnit)
        {
            var dirty = false;
            if (importer.textureType != TextureImporterType.Sprite) { importer.textureType = TextureImporterType.Sprite; dirty = true; }
            if (importer.spriteImportMode != SpriteImportMode.Single) { importer.spriteImportMode = SpriteImportMode.Single; dirty = true; }
            if (Mathf.Abs(importer.spritePixelsPerUnit - pixelsPerUnit) > 0.001f) { importer.spritePixelsPerUnit = pixelsPerUnit; dirty = true; }
            if (importer.mipmapEnabled) { importer.mipmapEnabled = false; dirty = true; }
            if (!importer.alphaIsTransparency) { importer.alphaIsTransparency = true; dirty = true; }

            // Ảnh từng là Sprite Mode Multiple giữ rect con cũ trong .meta sau khi về Single; rect đó vẽ sai viền.
            var serialized = new SerializedObject(importer);
            var staleSprites = serialized.FindProperty("m_SpriteSheet.m_Sprites");
            if (staleSprites != null && staleSprites.isArray && staleSprites.arraySize > 0)
            {
                staleSprites.ClearArray();
                serialized.ApplyModifiedPropertiesWithoutUndo();
                dirty = true;
            }

            var textureSettings = new TextureImporterSettings();
            importer.ReadTextureSettings(textureSettings);
            if (textureSettings.spriteGenerateFallbackPhysicsShape)
            {
                textureSettings.spriteGenerateFallbackPhysicsShape = false;
                importer.SetTextureSettings(textureSettings);
                dirty = true;
            }

            if (dirty)
                importer.SaveAndReimport();
            return dirty;
        }

        private static bool ApplyImportSpec(SpriteAtlasImporter importer, in AtlasImportSpec spec)
        {
            var dirty = false;
            if (!importer.includeInBuild) { importer.includeInBuild = true; dirty = true; }

            var packing = importer.packingSettings;
            if (packing.padding != spec.padding || packing.enableRotation || packing.enableTightPacking || packing.enableAlphaDilation)
            {
                packing.padding = spec.padding;
                packing.enableRotation = false;
                packing.enableTightPacking = false;
                packing.enableAlphaDilation = false;
                importer.packingSettings = packing;
                dirty = true;
            }

            var texture = importer.textureSettings;
            if (texture.generateMipMaps || texture.filterMode != FilterMode.Bilinear || !texture.sRGB)
            {
                texture.generateMipMaps = false;
                texture.filterMode = FilterMode.Bilinear;
                texture.sRGB = true;
                importer.textureSettings = texture;
                dirty = true;
            }

            foreach (var platform in spec.platforms)
            {
                var platformSettings = importer.GetPlatformSettings(platform);
                if (platformSettings.overridden && platformSettings.maxTextureSize == spec.maxTextureSize && platformSettings.format == spec.format)
                    continue;
                platformSettings.overridden = true;
                platformSettings.maxTextureSize = spec.maxTextureSize;
                platformSettings.format = spec.format;
                importer.SetPlatformSettings(platformSettings);
                dirty = true;
            }

            if (dirty)
                importer.SaveAndReimport();
            return dirty;
        }
    }
}
