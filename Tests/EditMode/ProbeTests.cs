using System;
using System.Collections.Generic;
using Horcrux.Editor.Probe;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Horcrux.Tests
{
    /// <summary>Probe helpers: reflection reaches private members of a base, sheet layout puts shot 0 top-left, dump reports bounds in root space.</summary>
    public sealed class ProbeTests
    {
        private readonly List<Object> created = new();

        private class Base
        {
            private int secret = 7;
            private int Twice(int value) => value * 2 + secret - 7;
        }

        private sealed class Derived : Base { }

        [TearDown]
        public void TearDown()
        {
            foreach (var o in created)
                Object.DestroyImmediate(o);
            created.Clear();
        }

        [Test]
        public void Field_ReadsPrivateFieldDeclaredOnBase() => Assert.AreEqual(7, PlayProbe.Field<int>(new Derived(), "secret"));

        [Test]
        public void Call_InvokesPrivateMethodDeclaredOnBase() => Assert.AreEqual(10, PlayProbe.Call(new Derived(), "Twice", 5));

        [Test]
        public void Field_MissingName_Throws() =>
            Assert.Throws<MissingFieldException>(() => PlayProbe.Field<int>(new Derived(), "nothing"));

        [Test]
        public void Shrink_AveragesEachBlockInsideCrop()
        {
            var source = Solid(4, 2, new Color32(0, 0, 0, 255));
            source.SetPixel(2, 0, new Color32(200, 0, 0, 255));
            source.SetPixel(3, 1, new Color32(200, 0, 0, 255));
            source.Apply();

            var shrunk = Track(FrameSheet.Shrink(source, new RectInt(2, 0, 2, 2), 2));

            Assert.AreEqual(1, shrunk.width);
            Assert.AreEqual(1, shrunk.height);
            Assert.AreEqual(100, shrunk.GetPixels32()[0].r);
        }

        [Test]
        public void Compose_FirstShotTopLeft_RowMajor()
        {
            var red = Solid(2, 2, new Color32(255, 0, 0, 255));
            var green = Solid(2, 2, new Color32(0, 255, 0, 255));
            var blue = Solid(2, 2, new Color32(0, 0, 255, 255));

            var sheet = Track(FrameSheet.Compose(new[] { red, green, blue }, 2));

            // 2 cột, 2 hàng, khe 4 px: 2*2 + 3*4 = 16 mỗi chiều. Hàng 0 ở dải y cao.
            Assert.AreEqual(16, sheet.width);
            Assert.AreEqual(16, sheet.height);
            Assert.AreEqual(255, ((Color32)sheet.GetPixel(4, 10)).r);
            Assert.AreEqual(255, ((Color32)sheet.GetPixel(10, 10)).g);
            Assert.AreEqual(255, ((Color32)sheet.GetPixel(4, 4)).b);
            Assert.AreEqual(40, ((Color32)sheet.GetPixel(10, 4)).r);
        }

        [Test]
        public void Dump_SpriteBoundsInRootSpace_AndInactiveMarked()
        {
            var root = Track(new GameObject("root"));
            var child = new GameObject("child");
            child.transform.SetParent(root.transform, false);
            child.transform.localPosition = new Vector3(1f, 0f, 0f);
            child.transform.localScale = new Vector3(2f, 2f, 1f);
            var texture = Solid(100, 100, new Color32(255, 255, 255, 255));
            child.AddComponent<SpriteRenderer>().sprite =
                Track(Sprite.Create(texture, new Rect(0, 0, 100, 100), new Vector2(0.5f, 0.5f), 100f));
            var hidden = new GameObject("hidden");
            hidden.transform.SetParent(root.transform, false);
            hidden.SetActive(false);

            var dump = PrefabDump.Dump(root);

            StringAssert.Contains("child on p=1,0,0 s=2,2,1", dump);
            StringAssert.Contains("root c=1,0 size=2,2", dump);
            StringAssert.Contains("hidden OFF", dump);
            StringAssert.DoesNotContain("hidden", PrefabDump.Dump(root, "child"));
        }

        private Texture2D Solid(int width, int height, Color32 color)
        {
            var texture = Track(new Texture2D(width, height, TextureFormat.RGBA32, false));
            var pixels = new Color32[width * height];
            Array.Fill(pixels, color);
            texture.SetPixels32(pixels);
            texture.Apply();
            return texture;
        }

        private T Track<T>(T o) where T : Object
        {
            created.Add(o);
            return o;
        }
    }
}
