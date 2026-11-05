using System;
using Horcrux.Editor.Utilities;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore;

namespace Horcrux.Editor.UIReskin
{
    /// <summary>
    /// Tạo TMP sprite asset từ một texture Sprite (Single hay Multiple), mỗi mảnh một glyph theo thứ tự mảnh: <c>&lt;sprite=i&gt;</c>.
    /// Đặt sẵn face info và metric: asset có face info bằng 0 thì TMP không vẽ glyph nào, không báo lỗi.
    /// Sprite đứng như chữ hoa cao bằng mảnh cao nhất: 80% trên baseline, cách hai bên 3.5% chiều cao.
    /// </summary>
    public static class TmpSpriteAssetBuilder
    {
        private const float AscentRatio = 0.8f;
        private const float SideBearingRatio = 0.035f;

        // Asset mang version rỗng bị TMP coi là định dạng cũ lúc nạp và xoá bảng glyph; bản tạo bằng menu của TMP ghi 1.1.0.
        private const string AssetVersion = "1.1.0";

        public static TMP_SpriteAsset Create(string texturePath, string assetPath)
        {
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
            var sprites = Array.FindAll(AssetDatabase.LoadAllAssetsAtPath(texturePath), a => a is Sprite);
            if (texture == null || sprites.Length == 0)
                throw new ArgumentException(texturePath + " không phải texture Sprite");
            if (AssetDatabase.LoadMainAssetAtPath(assetPath) != null)
                throw new InvalidOperationException("đã có " + assetPath);

            var pointSize = 0f;
            foreach (Sprite sprite in sprites)
                pointSize = Mathf.Max(pointSize, sprite.rect.height);
            var ascent = Mathf.Round(pointSize * AscentRatio);
            var side = Mathf.Round(pointSize * SideBearingRatio);

            var asset = ScriptableObject.CreateInstance<TMP_SpriteAsset>();
            asset.spriteSheet = texture;
            for (var i = 0; i < sprites.Length; i++)
            {
                var sprite = (Sprite)sprites[i];
                var r = sprite.rect;
                // mảnh thấp hơn mảnh cao nhất thì căn giữa theo chiều dọc
                var bearingY = ascent - (pointSize - r.height) * 0.5f;
                var metrics = new GlyphMetrics(r.width, r.height, side, bearingY, r.width + 2f * side);
                var glyph = new TMP_SpriteGlyph((uint)i, metrics, new GlyphRect(r), 1f, 0, sprite);
                asset.spriteGlyphTable.Add(glyph);
                asset.spriteCharacterTable.Add(new TMP_SpriteCharacter(0xFFFE, asset, glyph) { name = sprite.name });
            }
            asset.faceInfo = new FaceInfo
            {
                pointSize = pointSize,
                scale = 1f,
                lineHeight = pointSize,
                ascentLine = ascent,
                baseline = 0f,
                descentLine = ascent - pointSize,
            };

            var shader = Shader.Find("TextMeshPro/Sprite");
            if (shader == null)
                throw new InvalidOperationException("Shader TextMeshPro/Sprite missing: import TMP Essential Resources first.");
            var material = new Material(shader) { name = texture.name + " Material" };
            material.mainTexture = texture;
            asset.material = material;

            FolderUtility.CreateFolderRecursively(System.IO.Path.GetDirectoryName(assetPath));
            AssetDatabase.CreateAsset(asset, assetPath);
            AssetDatabase.AddObjectToAsset(material, asset);
            var so = new SerializedObject(asset);
            so.FindProperty("m_Version").stringValue = AssetVersion;
            so.ApplyModifiedPropertiesWithoutUndo();
            asset.UpdateLookupTables();
            AssetDatabase.SaveAssets();
            return asset;
        }
    }
}
