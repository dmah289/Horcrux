using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.U2D;
using UnityEngine;
using UnityEngine.U2D;

namespace Horcrux.Editor.SpriteAtlasBuilder
{
    /// <summary>
    /// Soát atlas cho UI: màn nào dùng texture nào, texture nằm atlas nào hay lẻ, texture nào nhiều màn cùng dùng, atlas pack ra
    /// bao nhiêu trang và lấp được bao nhiêu. Chỉ đọc; pack xem trước ghi vào cache Library, không đổi asset.
    /// </summary>
    public static class AtlasAudit
    {
        /// <summary>Sprite chiếm quá phần này của trang thì nên để lẻ: ăn chỗ cả trang và kéo cả trang vào bộ nhớ theo nó.</summary>
        public const float BigSpriteShare = 0.25f;

        /// <summary>
        /// Mỗi đối số là một đơn vị (prefab, scene, hay thư mục gom mọi prefab bên trong). Với mỗi đơn vị: group Addressables, mỗi
        /// atlas nó chạm một dòng <c>atlas N tex (on a, off b, dep c)</c>, rồi mỗi texture lẻ một dòng <c>- | texture WxH | trạng thái</c>
        /// (<paramref name="verbose"/> liệt kê cả texture trong atlas). Trạng thái: <c>on</c> có component đang bật giữ nó, <c>off</c>
        /// chỉ object tắt hay component tắt giữ nó (vẫn nạp theo prefab), <c>dep</c> đến qua asset khác (config, material, sprite asset
        /// TMP) hoặc từ scene. Cuối là mục <c>shared</c>: atlas hay texture lẻ mà hai đơn vị trở lên cùng nạp, cờ <c>DUP</c> khi các
        /// đơn vị ở khác group mà thứ được dùng chung không addressable — build chép nó vào từng bundle.
        /// </summary>
        public static string Usage(bool verbose, params string[] units)
        {
            var atlasOf = TextureAtlasMap();
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            var usedBy = new Dictionary<string, List<string>>();
            var groupOf = new Dictionary<string, string>();
            var sb = new StringBuilder();

            foreach (var unit in units)
            {
                var states = new Dictionary<string, string>();
                foreach (var root in RootsOf(unit))
                    Collect(root, states);
                groupOf[unit] = GroupOf(settings, unit);
                sb.Append("== ").Append(unit).Append(" [").Append(groupOf[unit]).Append("]\n");

                // Holder: atlas chứa texture, hay chính texture khi lẻ. Một texture nằm nhiều atlas thì Unity chọn một, tính atlas đầu.
                var byHolder = states.GroupBy(p => atlasOf.TryGetValue(p.Key, out var list) ? list[0] : p.Key).OrderBy(g => g.Key);
                var atlasCount = 0;
                foreach (var holder in byHolder)
                {
                    if (!usedBy.TryGetValue(holder.Key, out var users)) usedBy[holder.Key] = users = new List<string>();
                    users.Add(unit);
                    if (!atlasOf.ContainsKey(holder.First().Key)) continue;

                    atlasCount++;
                    sb.Append("  ").Append(holder.Key).Append(' ').Append(holder.Count()).Append(" tex (")
                        .Append(string.Join(", ", holder.GroupBy(p => p.Value).OrderByDescending(g => Rank(g.Key)).Select(g => g.Key + " " + g.Count())))
                        .Append(")\n");
                    if (verbose)
                        foreach (var pair in holder.OrderBy(p => p.Key))
                            sb.Append("    ").Append(pair.Key).Append(' ').Append(SourceSize(pair.Key)).Append(" | ").Append(pair.Value).Append('\n');
                }

                var loose = 0;
                foreach (var pair in states.Where(p => !atlasOf.ContainsKey(p.Key)).OrderBy(p => p.Key))
                {
                    loose++;
                    sb.Append("  - | ").Append(pair.Key).Append(' ').Append(SourceSize(pair.Key)).Append(" | ").Append(pair.Value).Append('\n');
                }

                sb.Append("  = ").Append(atlasCount).Append(" atlas, ").Append(loose).Append(" lẻ\n");
            }

            sb.Append("== shared\n");
            foreach (var pair in usedBy.Where(p => p.Value.Count > 1).OrderBy(p => p.Key))
            {
                var groups = pair.Value.Select(u => groupOf[u]).Where(g => g != "-").Distinct().Count();
                var dup = groups > 1 && GroupOf(settings, pair.Key) == "-";
                sb.Append("  ").Append(pair.Key).Append(" | ").Append(string.Join(", ", pair.Value.Select(System.IO.Path.GetFileNameWithoutExtension)))
                    .Append(dup ? " | DUP" : "").Append('\n');
            }

            return sb.ToString();
        }

        /// <summary>
        /// Pack xem trước từng atlas (đường dẫn atlas hay thư mục chứa atlas) cho build target hiện tại, mỗi atlas: group Addressables,
        /// cài đặt, packable, rồi từng trang <c>WxH format fill %</c>. Dòng <c>BIG</c> là sprite chiếm quá
        /// <see cref="BigSpriteShare"/> trang; dòng <c>ALSO</c> là texture đã nằm ở atlas khác.
        /// </summary>
        public static string Atlases(params string[] paths)
        {
            var atlasOf = TextureAtlasMap();
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            var target = EditorUserBuildSettings.activeBuildTarget;
            var sb = new StringBuilder();
            foreach (var path in AtlasPaths(paths))
            {
                var atlas = AssetDatabase.LoadAssetAtPath<SpriteAtlas>(path);
                var importer = (SpriteAtlasImporter)AssetImporter.GetAtPath(path);
                SpriteAtlasUtility.PackAtlases(new[] { atlas }, target, false);

                var packing = importer.packingSettings;
                var platform = importer.GetPlatformSettings(BuildPipeline.GetBuildTargetGroup(target).ToString());
                if (!platform.overridden)
                    platform = importer.GetPlatformSettings("DefaultTexturePlatform");
                sb.Append("== ").Append(path).Append(" [").Append(GroupOf(settings, path)).Append("] inBuild ").Append(importer.includeInBuild)
                    .Append(" pad ").Append(packing.padding).Append(" rot ").Append(packing.enableRotation).Append(" tight ")
                    .Append(packing.enableTightPacking).Append(" mip ").Append(importer.textureSettings.generateMipMaps).Append(' ')
                    .Append(target).Append(" max ").Append(platform.maxTextureSize).Append(' ').Append(platform.format).Append('\n');
                foreach (var packable in atlas.GetPackables())
                    sb.Append("  pack ").Append(AssetDatabase.GetAssetPath(packable)).Append('\n');

                var sprites = new Sprite[atlas.spriteCount];
                atlas.GetSprites(sprites);
                try
                {
                    var used = new Dictionary<Texture2D, float>();
                    foreach (var sprite in sprites)
                    {
                        var area = sprite.textureRect.width * sprite.textureRect.height;
                        used[sprite.texture] = (used.TryGetValue(sprite.texture, out var sum) ? sum : 0) + area;
                    }

                    foreach (var page in used)
                        sb.Append("  page ").Append(page.Key.width).Append('x').Append(page.Key.height).Append(' ').Append(page.Key.format)
                            .Append(" fill ").Append(Mathf.RoundToInt(100 * page.Value / (page.Key.width * page.Key.height))).Append("%\n");

                    foreach (var sprite in sprites)
                    {
                        var page = sprite.texture.width * sprite.texture.height;
                        var area = sprite.textureRect.width * sprite.textureRect.height;
                        if (area > BigSpriteShare * page)
                            sb.Append("  BIG ").Append(sprite.name.Replace("(Clone)", "")).Append(' ').Append((int)sprite.textureRect.width)
                                .Append('x').Append((int)sprite.textureRect.height).Append(' ').Append(Mathf.RoundToInt(100 * area / page)).Append("%\n");
                    }
                }
                finally
                {
                    foreach (var sprite in sprites)
                        UnityEngine.Object.DestroyImmediate(sprite);
                }

                foreach (var pair in atlasOf.Where(p => p.Value.Contains(path) && p.Value.Count > 1).OrderBy(p => p.Key))
                    sb.Append("  ALSO ").Append(pair.Key).Append(" | ").Append(string.Join(", ", pair.Value.Where(a => a != path))).Append('\n');
            }

            return sb.ToString();
        }

        /// <summary>
        /// Mỗi texture trong thư mục một dòng <c>texture WxH | atlas | mip | platform max format</c> theo build target hiện tại
        /// (platform không override thì đọc Default). Texture trong atlas thì cài đặt của atlas quyết trong build; texture lẻ thì dòng
        /// này là thứ vào build: UI không cần mipmap, cỡ nguồn vượt max thì bị thu.
        /// </summary>
        public static string Importers(string folder)
        {
            var atlasOf = TextureAtlasMap();
            var platformName = BuildPipeline.GetBuildTargetGroup(EditorUserBuildSettings.activeBuildTarget).ToString();
            var sb = new StringBuilder();
            foreach (var path in AssetDatabase.FindAssets("t:Texture2D", new[] { folder }).Select(AssetDatabase.GUIDToAssetPath).Distinct().OrderBy(p => p))
            {
                if (AssetImporter.GetAtPath(path) is not TextureImporter importer) continue;
                var platform = importer.GetPlatformTextureSettings(platformName);
                if (!platform.overridden)
                    platform = importer.GetDefaultPlatformTextureSettings();
                sb.Append(path).Append(' ').Append(SourceSize(path)).Append(" | ")
                    .Append(atlasOf.TryGetValue(path, out var list) ? string.Join("+", list) : "-").Append(" | mip ").Append(importer.mipmapEnabled)
                    .Append(" | ").Append(platform.overridden ? platformName : "Default").Append(' ').Append(platform.maxTextureSize).Append(' ')
                    .Append(platform.overridden ? platform.format.ToString() : platform.textureCompression.ToString()).Append('\n');
            }

            return sb.ToString();
        }

        /// <summary>Texture → các atlas (không tính variant) có packable phủ nó: thư mục (cả thư mục con), texture, hay sprite.</summary>
        public static Dictionary<string, List<string>> TextureAtlasMap()
        {
            var map = new Dictionary<string, List<string>>();
            foreach (var atlasPath in AtlasPaths(new[] { "Assets" }))
            {
                var atlas = AssetDatabase.LoadAssetAtPath<SpriteAtlas>(atlasPath);
                if (atlas == null || atlas.isVariant) continue;
                foreach (var packable in atlas.GetPackables())
                {
                    var packablePath = AssetDatabase.GetAssetPath(packable);
                    var textures = AssetDatabase.IsValidFolder(packablePath)
                        ? AssetDatabase.FindAssets("t:Texture2D", new[] { packablePath }).Select(AssetDatabase.GUIDToAssetPath)
                        : new[] { packablePath };
                    foreach (var texture in textures)
                    {
                        if (!map.TryGetValue(texture, out var list)) map[texture] = list = new List<string>();
                        if (!list.Contains(atlasPath)) list.Add(atlasPath);
                    }
                }
            }

            return map;
        }

        private static IEnumerable<string> AtlasPaths(IEnumerable<string> paths)
        {
            foreach (var path in paths)
            {
                if (!AssetDatabase.IsValidFolder(path))
                {
                    yield return path;
                    continue;
                }

                foreach (var guid in AssetDatabase.FindAssets("t:SpriteAtlas", new[] { path }).Distinct())
                {
                    var atlasPath = AssetDatabase.GUIDToAssetPath(guid);
                    if (atlasPath.EndsWith(".spriteatlasv2", StringComparison.OrdinalIgnoreCase))
                        yield return atlasPath;
                }
            }
        }

        private static IEnumerable<string> RootsOf(string unit)
        {
            if (!AssetDatabase.IsValidFolder(unit))
                return new[] { unit };
            return AssetDatabase.FindAssets("t:Prefab", new[] { unit }).Select(AssetDatabase.GUIDToAssetPath).Distinct();
        }

        /// <summary>Gộp trạng thái mạnh nhất: on thắng off thắng dep.</summary>
        private static void Collect(string root, Dictionary<string, string> states)
        {
            if (root.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(root);
                foreach (var component in prefab.GetComponentsInChildren<Component>(true))
                {
                    if (component == null) continue;
                    var drawn = IsActive(component.transform, prefab.transform) && (component is not Behaviour b || b.enabled);
                    var property = new SerializedObject(component).GetIterator();
                    while (property.Next(true))
                    {
                        // Path của sprite là texture nguồn; sprite.texture của sprite đã pack là trang atlas.
                        if (property.propertyType == SerializedPropertyType.ObjectReference && property.objectReferenceValue is Sprite or Texture2D)
                            Merge(states, AssetDatabase.GetAssetPath(property.objectReferenceValue), drawn ? "on" : "off");
                    }
                }
            }

            foreach (var dependency in AssetDatabase.GetDependencies(root, true))
                if (AssetDatabase.GetMainAssetTypeAtPath(dependency) == typeof(Texture2D))
                    Merge(states, dependency, "dep");
        }

        private static void Merge(Dictionary<string, string> states, string texture, string state)
        {
            if (string.IsNullOrEmpty(texture) || !texture.StartsWith("Assets/", StringComparison.Ordinal)) return;
            if (!states.TryGetValue(texture, out var current) || Rank(state) > Rank(current))
                states[texture] = state;
        }

        private static int Rank(string state) => state switch { "on" => 2, "off" => 1, _ => 0 };

        private static bool IsActive(Transform node, Transform root)
        {
            for (var t = node; t != null; t = t == root ? null : t.parent)
                if (!t.gameObject.activeSelf) return false;
            return true;
        }

        private static string SourceSize(string texture)
        {
            if (AssetImporter.GetAtPath(texture) is not TextureImporter importer) return "?";
            importer.GetSourceTextureWidthAndHeight(out var width, out var height);
            return width + "x" + height;
        }

        /// <summary>Group chứa asset, kể cả entry ngầm qua thư mục; <c>-</c> khi không addressable. Thư mục: group của prefab đầu có group.</summary>
        private static string GroupOf(AddressableAssetSettings settings, string path)
        {
            if (settings == null) return "-";
            foreach (var root in RootsOf(path))
            {
                var entry = settings.FindAssetEntry(AssetDatabase.AssetPathToGUID(root), true);
                if (entry != null) return entry.parentGroup.Name;
            }

            return "-";
        }
    }
}
