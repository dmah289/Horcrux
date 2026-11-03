using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Horcrux.Editor.UIReskin
{
    /// <summary>
    /// Dựng node UGUI trong <see cref="PrefabEdit.Edit"/>: object mới neo giữa, pivot giữa, layer UI. Image và chữ trang trí
    /// không nhận raycast; nút thật bật lại trên chính Graphic của nút.
    /// </summary>
    public static class UIBuild
    {
        private const int UILayer = 5;

        public static RectTransform Node(Transform parent, string name, Vector2 size = default, Vector2 anchoredPosition = default)
        {
            var go = new GameObject(name, typeof(RectTransform)) { layer = UILayer };
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.sizeDelta = size;
            rect.anchoredPosition = anchoredPosition;
            return rect;
        }

        /// <summary>Sprite có border thì Sliced, không thì Simple. size zero lấy cỡ gốc của sprite.</summary>
        public static Image Image(Transform parent, string name, Sprite sprite, Vector2 size = default, Vector2 anchoredPosition = default)
        {
            var image = Node(parent, name, size, anchoredPosition).gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.raycastTarget = false;
            image.type = sprite != null && sprite.border != Vector4.zero ? UnityEngine.UI.Image.Type.Sliced : UnityEngine.UI.Image.Type.Simple;
            if (size == default && sprite != null)
                image.SetNativeSize();
            return image;
        }

        /// <summary>Chữ một dòng, căn giữa, không xuống dòng. material null giữ material mặc định của font.</summary>
        public static TextMeshProUGUI Text(Transform parent, string name, string text, float fontSize, TMP_FontAsset font,
            Material material = null, Vector2 size = default, Vector2 anchoredPosition = default)
        {
            var label = Node(parent, name, size, anchoredPosition).gameObject.AddComponent<TextMeshProUGUI>();
            label.font = font;
            if (material != null)
                label.fontSharedMaterial = material;
            label.text = text;
            label.fontSize = fontSize;
            label.alignment = TextAlignmentOptions.Center;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.raycastTarget = false;
            return label;
        }

        /// <summary>Phủ kín cha (anchor 0–1, offset 0).</summary>
        public static RectTransform Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            return rect;
        }
    }
}
