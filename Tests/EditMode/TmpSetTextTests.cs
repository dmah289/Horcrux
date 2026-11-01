using NUnit.Framework;
using TMPro;
using UnityEngine;

namespace Horcrux.Tests
{
    /// <summary>
    /// SetText("{0}", số nguyên) cho đúng các ký tự của ToString(), không dấu thập phân: bằng chứng để số đếm ghi qua SetText
    /// không tạo chuỗi. Dùng font mặc định của TMP Essential Resources, không phụ thuộc font dự án.
    /// </summary>
    public sealed class TmpSetTextTests
    {
        [Test]
        public void SetText_WholeNumber_RendersSameGlyphsAsToString()
        {
            var font = TMP_Settings.defaultFontAsset;
            if (font == null)
                font = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
            Assert.IsNotNull(font, "TMP Essential Resources chưa import: Window > TextMeshPro > Import TMP Essential Resources");

            var go = new GameObject("tmp_settext_test");
            try
            {
                var text = go.AddComponent<TextMeshPro>();
                text.font = font;

                foreach (var value in new[] { 0, 1, 4, 12, 100 })
                {
                    text.SetText("{0}", value);
                    text.ForceMeshUpdate(true);

                    var expected = value.ToString();
                    var info = text.textInfo;
                    Assert.AreEqual(expected.Length, info.characterCount, $"value {value}: character count");
                    for (var i = 0; i < expected.Length; i++)
                        Assert.AreEqual(expected[i], info.characterInfo[i].character, $"value {value}: char {i}");
                }
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }
    }
}
