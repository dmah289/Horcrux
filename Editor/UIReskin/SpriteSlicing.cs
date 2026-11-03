using System;
using System.Text;
using UnityEditor;
using UnityEngine;
#if HORCRUX_SPRITE_EDITOR
using UnityEditor.U2D.Sprites;
#endif

namespace Horcrux.Editor.UIReskin
{
    /// <summary>
    /// Đọc và đặt 9-slice của sprite. Border tính bằng pixel của texture đã import, thứ tự Unity (trái, dưới, phải, trên).
    /// Texture nguồn lớn hơn Max Size bị thu khi import: rect và border co theo, số đo pixel từ art gốc không còn khớp.
    /// </summary>
    public static class SpriteSlicing
    {
        /// <summary>
        /// Mỗi texture Sprite trong folder một dòng: cỡ nguồn, Max Size (<c>SHRUNK</c> khi nguồn vượt), mode, PPU, rồi từng mảnh
        /// với rect và border.
        /// </summary>
        public static string Report(string folder)
        {
            var sb = new StringBuilder();
            foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { folder }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (!(AssetImporter.GetAtPath(path) is TextureImporter importer) || importer.textureType != TextureImporterType.Sprite)
                    continue;
                importer.GetSourceTextureWidthAndHeight(out var width, out var height);
                sb.Append(path).Append(" src ").Append(width).Append('x').Append(height).Append(" max ").Append(importer.maxTextureSize);
                if (Mathf.Max(width, height) > importer.maxTextureSize)
                    sb.Append(" SHRUNK");
                sb.Append(' ').Append(importer.spriteImportMode).Append(" ppu ").Append(importer.spritePixelsPerUnit).Append('\n');
                foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(path))
                    if (asset is Sprite sprite)
                        sb.Append("  ").Append(sprite.name).Append(" rect ").Append(Rect(sprite.rect)).Append(" border ")
                            .Append(Border(sprite.border)).Append('\n');
            }
            return sb.ToString();
        }

        /// <summary>Mảnh có diện tích lớn nhất. LoadAssetAtPath&lt;Sprite&gt; trả mảnh đầu tiên, không phải mảnh chính.</summary>
        public static Sprite LargestSlice(string texturePath)
        {
            Sprite best = null;
            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(texturePath))
                if (asset is Sprite sprite && (best == null || sprite.rect.width * sprite.rect.height > best.rect.width * best.rect.height))
                    best = sprite;
            return best != null ? best : throw new ArgumentException("không có sprite trong " + texturePath);
        }

#if HORCRUX_SPRITE_EDITOR
        /// <summary>
        /// Đặt border cho một mảnh (Single hay Multiple), reimport, trả <c>tên: cũ -> mới</c>. spriteName null chỉ được khi
        /// texture có đúng một mảnh. Đọc dòng trả về trước khi ghi tiếp: border cũ là của art, ghi đè thì mất.
        /// </summary>
        public static string SetBorder(string texturePath, Vector4 border, string spriteName = null)
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(texturePath);
            var factories = new SpriteDataProviderFactories();
            factories.Init();
            var provider = factories.GetSpriteEditorDataProviderFromObject(importer);
            provider.InitSpriteEditorDataProvider();
            var rects = provider.GetSpriteRects();

            SpriteRect target = null;
            var names = "";
            foreach (var rect in rects)
            {
                names += " " + rect.name;
                if (spriteName == null ? rects.Length == 1 : rect.name == spriteName)
                    target = rect;
            }
            if (target == null)
                throw new ArgumentException(texturePath + ": chọn mảnh theo tên trong" + names);

            var old = target.border;
            if (old == border)
                return target.name + ": " + Border(old) + " (giữ)";
            target.border = border;
            provider.SetSpriteRects(rects);
            provider.Apply();
            importer.SaveAndReimport();
            return target.name + ": " + Border(old) + " -> " + Border(border);
        }
#endif

        private static string Rect(Rect r) => $"{r.x:0},{r.y:0} {r.width:0}x{r.height:0}";

        private static string Border(Vector4 b) => $"({b.x:0},{b.y:0},{b.z:0},{b.w:0})";
    }
}
