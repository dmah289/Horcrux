using Horcrux.Runtime.Utilities.ExtensionMethods;
using LightSide;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools.Constraints;
using Is = UnityEngine.TestTools.Constraints.Is;

namespace Horcrux.Tests
{
    /// <summary>
    /// SetNumber ghi đúng các ký tự của ToString() và không cấp phát khi số đổi: bằng chứng để số đếm (tiền, lượt, đồng hồ)
    /// ghi mỗi lần đổi mà không tạo rác. Không cần font: chỉ đọc chữ nguồn.
    /// </summary>
    public sealed class UniTextSetNumberTests
    {
        private GameObject _host;
        private UniText _text;

        [SetUp]
        public void SetUp()
        {
            _host = new GameObject("unitext_setnumber_test", typeof(RectTransform));
            _text = _host.AddComponent<UniText>();
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_host);

        [TestCase(0)]
        [TestCase(7)]
        [TestCase(12)]
        [TestCase(-45)]
        [TestCase(int.MaxValue)]
        [TestCase(int.MinValue)]
        public void SetNumber_WritesSameCharactersAsToString(int value)
        {
            _text.SetNumber(value);
            Assert.AreEqual(value.ToString(), new string(_text.RawText.Span));
        }

        [Test]
        public void SetNumber_ChangingValue_DoesNotAllocate()
        {
            _text.SetNumber(10);
            _text.SetNumber(11);
            var next = 12;
            Assert.That(() => _text.SetNumber(next++), Is.Not.AllocatingGCMemory());
        }

        [Test]
        public void AllocationProbe_DetectsStringFormatting()
        {
            var next = 12;
            Assert.That(() => _text.SetText((next++).ToString()), Is.AllocatingGCMemory(),
                "đối chứng: cùng phép đo phải bắt được một chuỗi tạo mới");
        }
    }
}
