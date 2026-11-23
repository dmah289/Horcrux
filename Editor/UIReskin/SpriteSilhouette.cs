using System;
using System.IO;
using System.Text;
using Horcrux.Editor.SpriteAtlasBuilder;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Horcrux.Editor.UIReskin
{
    /// <summary>
    /// Bản trắng của một art: RGB trắng, alpha giữ nguyên từng pixel. Shader UI mặc định nhân màu Image với texture, nên Image dùng
    /// bản trắng ra đúng màu đặt trên Image theo đúng hình art: lớp phủ khoá, chớp trắng, mặt nạ viền chọn. Art đổi thì gọi lại
    /// với cùng đích: file ghi đè, GUID giữ, reference không phải kéo lại.
    /// </summary>
    public static class SpriteSilhouette
    {
        // Platform có thể mang override nén; platform không override thì bản trắng cũng không override, theo mặc định.
        private static readonly string[] Platforms = { "Standalone", "Android", "iPhone", "WebGL" };

        /// <summary>
        /// Ghi bản trắng của sourceTexturePath ra destinationPath (PNG trong Assets), chép cài đặt import của nguồn (PPU, mipmap,
        /// filter, nén mặc định và override từng platform, border 9-slice, pivot) rồi đặt Single. Nguồn là texture Sprite một
        /// mảnh phủ cả ảnh (Single, hay Multiple một mảnh); nguồn không có pixel trong suốt thì ném vì bản trắng chỉ là ô vuông.
        /// Trả <c>đích: tên WxH border (l,b,r,t) | atlas: ...</c>; chưa atlas nào pack thì <c>atlas: none</c>.
        /// </summary>
        public static string Create(string sourceTexturePath, string destinationPath)
        {
            if (!(AssetImporter.GetAtPath(sourceTexturePath) is TextureImporter source) || source.textureType != TextureImporterType.Sprite)
                throw new ArgumentException("nguồn không phải texture Sprite: " + sourceTexturePath);
            if (!destinationPath.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("đích phải là .png: " + destinationPath);
            var slice = SingleSlice(sourceTexturePath);

            WriteWhite(sourceTexturePath, destinationPath);
            AssetDatabase.ImportAsset(destinationPath, ImportAssetOptions.ForceSynchronousImport);
            var destination = (TextureImporter)AssetImporter.GetAtPath(destinationPath);

            var settings = new TextureImporterSettings();
            source.ReadTextureSettings(settings);
            if (source.spriteImportMode != SpriteImportMode.Single)
            {
                // Multiple một mảnh giữ border, pivot trên mảnh, không trên settings của texture.
                var pivot = new Vector2(slice.pivot.x / slice.rect.width, slice.pivot.y / slice.rect.height);
                settings.spriteBorder = slice.border;
                settings.spriteAlignment = pivot == new Vector2(0.5f, 0.5f) ? (int)SpriteAlignment.Center : (int)SpriteAlignment.Custom;
                settings.spritePivot = pivot;
            }
            settings.spriteMode = (int)SpriteImportMode.Single;
            destination.SetTextureSettings(settings);
            destination.SetPlatformTextureSettings(source.GetDefaultPlatformTextureSettings());
            foreach (var platform in Platforms)
            {
                var platformSettings = source.GetPlatformTextureSettings(platform);
                platformSettings.name = platform;
                destination.SetPlatformTextureSettings(platformSettings);
            }
            destination.SaveAndReimport();

            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(destinationPath);
            var b = sprite.border;
            var sb = new StringBuilder(destinationPath).Append(": ").Append(sprite.name).Append(' ')
                .Append(sprite.rect.width).Append('x').Append(sprite.rect.height)
                .Append($" border ({b.x:0},{b.y:0},{b.z:0},{b.w:0}) | atlas: ");
            sb.Append(AtlasAudit.TextureAtlasMap().TryGetValue(destinationPath, out var atlases) ? string.Join(", ", atlases) : "none");
            return sb.ToString();
        }

        // Mảnh duy nhất và phải phủ cả texture: Single của bản trắng luôn lấy cả ảnh, mảnh nhỏ hơn ảnh thì hai sprite lệch khung.
        private static Sprite SingleSlice(string texturePath)
        {
            Sprite slice = null;
            var count = 0;
            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(texturePath))
                if (asset is Sprite sprite)
                {
                    slice = sprite;
                    count++;
                }
            if (count != 1)
                throw new ArgumentException($"nguồn có {count} mảnh, cần đúng một: {texturePath}");
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
            if ((int)slice.rect.width != texture.width || (int)slice.rect.height != texture.height)
                throw new ArgumentException($"mảnh {slice.name} {slice.rect.width}x{slice.rect.height} không phủ cả texture {texture.width}x{texture.height}: {texturePath}");
            return slice;
        }

        // Đọc file art gốc, không đọc texture đã import: texture import có thể đã nén hay thu theo Max Size.
        private static void WriteWhite(string sourcePath, string destinationPath)
        {
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                if (!texture.LoadImage(File.ReadAllBytes(sourcePath)))
                    throw new ArgumentException("không đọc được ảnh " + sourcePath);
                var pixels = texture.GetPixels32();
                var transparent = false;
                for (var i = 0; i < pixels.Length; i++)
                {
                    transparent |= pixels[i].a < 255;
                    // Cả pixel trong suốt cũng trắng: lọc song tuyến ở mép không kéo màu tối vào viền.
                    pixels[i] = new Color32(255, 255, 255, pixels[i].a);
                }
                if (!transparent)
                    throw new ArgumentException("nguồn không có pixel trong suốt, bản trắng chỉ là ô vuông: " + sourcePath);
                // Art không đổi thì giữ file cũ: bộ mã PNG khác nhau ra byte khác nhau, ghi lại là diff rác.
                if (SamePixels(destinationPath, pixels))
                    return;
                texture.SetPixels32(pixels);
                texture.Apply();
                File.WriteAllBytes(destinationPath, texture.EncodeToPNG());
            }
            finally
            {
                Object.DestroyImmediate(texture);
            }
        }

        private static bool SamePixels(string path, Color32[] pixels)
        {
            if (!File.Exists(path))
                return false;
            var existing = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                if (!existing.LoadImage(File.ReadAllBytes(path)))
                    return false;
                var old = existing.GetPixels32();
                if (old.Length != pixels.Length)
                    return false;
                for (var i = 0; i < old.Length; i++)
                    if (old[i].r != pixels[i].r || old[i].g != pixels[i].g || old[i].b != pixels[i].b || old[i].a != pixels[i].a)
                        return false;
                return true;
            }
            finally
            {
                Object.DestroyImmediate(existing);
            }
        }
    }
}
