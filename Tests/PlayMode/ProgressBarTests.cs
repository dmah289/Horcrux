using System;
using System.Collections;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;
using Cysharp.Threading.Tasks;
using Horcrux.Runtime.Utilities.UI;
using NUnit.Framework;
using PrimeTween;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Horcrux.Tests
{
    /// <summary>Case table of docs/plans/Gameplay_Hud_Plan.md §2: fill width or fill amount, initial fill, label written once, time mode, cancel.</summary>
    public sealed class ProgressBarTests
    {
        private const float FullWidth = 300f;
        private const float Tolerance = 1e-3f;

        private GameObject _root;
        private ProgressBar _bar;
        private RectTransform _fill;
        private TextMeshProUGUI _label;

        [SetUp]
        public void SetUp()
        {
            _root = new GameObject("progress_bar_test", typeof(RectTransform));
            _fill = new GameObject("fill", typeof(RectTransform)).GetComponent<RectTransform>();
            _fill.SetParent(_root.transform, false);
            _fill.anchorMin = _fill.anchorMax = new Vector2(0f, 0.5f);

            _label = new GameObject("label", typeof(RectTransform)).AddComponent<TextMeshProUGUI>();
            _label.transform.SetParent(_root.transform, false);

            _bar = _root.AddComponent<ProgressBar>();
            SetField("fill", _fill);
            SetField("label", _label);
            SetField("fullWidth", FullWidth);
            SetField("ease", Ease.Linear);
            SetField("speed", 1f);
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Time.timeScale = 1f;
            Tween.StopAll();
            Object.Destroy(_root);
            yield return null;
        }

        [Test]
        public void Set_HalfGoal_WritesHalfWidthAndLabel()
        {
            _bar.Set(1.5f, 3f, "Combo x3");

            Assert.That(_fill.rect.width, Is.EqualTo(150f).Within(Tolerance));
            Assert.That(_label.text, Is.EqualTo("Combo x3"));
        }

        [TestCase(0f, 0f)]
        [TestCase(3f, FullWidth)]
        public void Set_Bounds_WritesEmptyOrFullWidth(float curr, float expectedWidth)
        {
            _bar.Set(curr, 3f, "x");

            Assert.That(_fill.rect.width, Is.EqualTo(expectedWidth).Within(Tolerance));
        }

        [Test]
        public void Set_NoLabelAssigned_WritesFillOnly()
        {
            SetField("label", null);

            Assert.DoesNotThrow(() => _bar.Set(1f, 2f, "ignored"));
            Assert.That(_fill.rect.width, Is.EqualTo(150f).Within(Tolerance));
        }

        [Test]
        public void Set_NullLabelContent_WritesFillAndKeepsLabel()
        {
            _bar.Set(1f, 2f, "before");

            _bar.Set(3f, 3f, null);

            Assert.That(_fill.rect.width, Is.EqualTo(FullWidth).Within(Tolerance));
            Assert.That(_label.text, Is.EqualTo("before"));
        }

        [UnityTest]
        public IEnumerator PlayAsync_NullLabelContent_KeepsLabel() => UniTask.ToCoroutine(async () =>
        {
            SetField("speed", 10f);
            _bar.Set(1f, 1f, "before");

            await _bar.PlayAsync(1f, 0f, 1f, null, CancellationToken.None);

            Assert.That(_fill.rect.width, Is.EqualTo(0f).Within(Tolerance));
            Assert.That(_label.text, Is.EqualTo("before"));
        });

        [Test]
        public void Set_GoalZero_LogsErrorAndKeepsFillAndLabel()
        {
            _bar.Set(1f, 2f, "before");
            LogAssert.Expect(LogType.Error, new Regex(@"\[ProgressBar\]: Goal of .* <= 0"));

            _bar.Set(1f, 0f, "after");

            Assert.That(_fill.rect.width, Is.EqualTo(150f).Within(Tolerance));
            Assert.That(_label.text, Is.EqualTo("before"));
        }

        [UnityTest]
        public IEnumerator PlayAsync_Drain_EndsEmptyWithLabel() => UniTask.ToCoroutine(async () =>
        {
            SetField("speed", 10f);

            await _bar.PlayAsync(1f, 0f, 1f, "Combo x2", CancellationToken.None);

            Assert.That(_fill.rect.width, Is.EqualTo(0f).Within(Tolerance));
            Assert.That(_label.text, Is.EqualTo("Combo x2"));
        });

        [Test]
        public void PlayAsync_SameFrame_WritesStartWidth()
        {
            _bar.Set(0f, 1f, "x");

            _bar.PlayAsync(1f, 0f, 1f, "x", CancellationToken.None).Forget();

            Assert.That(_fill.rect.width, Is.EqualTo(FullWidth).Within(Tolerance));
        }

        [UnityTest]
        public IEnumerator PlayAsync_ScaledTime_StandsStillWhileTimeScaleIsZero()
        {
            SetField("useUnscaledTime", false);
            Time.timeScale = 0f;

            _bar.PlayAsync(1f, 0f, 1f, "x", CancellationToken.None).Forget();
            yield return new WaitForSecondsRealtime(0.2f);

            Assert.That(_fill.rect.width, Is.EqualTo(FullWidth).Within(Tolerance));
        }

        [UnityTest]
        public IEnumerator PlayAsync_UnscaledTime_RunsWhileTimeScaleIsZero()
        {
            SetField("useUnscaledTime", true);
            Time.timeScale = 0f;

            _bar.PlayAsync(1f, 0f, 1f, "x", CancellationToken.None).Forget();
            yield return new WaitForSecondsRealtime(0.2f);

            Assert.That(_fill.rect.width, Is.LessThan(FullWidth - 1f));
        }

        [UnityTest]
        public IEnumerator PlayAsync_CancelMidway_ThrowsAndStopsFill() => UniTask.ToCoroutine(async () =>
        {
            using var cts = new CancellationTokenSource();
            var play = _bar.PlayAsync(1f, 0f, 1f, "x", cts.Token);
            await UniTask.Delay(TimeSpan.FromSeconds(0.2f));
            cts.Cancel();

            var thrown = false;
            try { await play; }
            catch (OperationCanceledException) { thrown = true; }

            var widthAtCancel = _fill.rect.width;
            await UniTask.Delay(TimeSpan.FromSeconds(0.2f));

            Assert.That(thrown, Is.True);
            Assert.That(widthAtCancel, Is.InRange(1f, FullWidth - 1f));
            Assert.That(_fill.rect.width, Is.EqualTo(widthAtCancel).Within(Tolerance));
        });

        [Test]
        public void Set_FillMode_WritesFillAmountAndKeepsWidth()
        {
            var image = UseFillMode();
            var widthBefore = _fill.rect.width;

            _bar.Set(1f, 4f, "x");

            Assert.That(image.fillAmount, Is.EqualTo(0.25f).Within(Tolerance));
            Assert.That(_fill.rect.width, Is.EqualTo(widthBefore).Within(Tolerance));
        }

        [UnityTest]
        public IEnumerator PlayAsync_FillMode_DrainEndsAtZeroFillAmount() => UniTask.ToCoroutine(async () =>
        {
            var image = UseFillMode();
            SetField("speed", 10f);

            var play = _bar.PlayAsync(1f, 0f, 1f, "x", CancellationToken.None);
            Assert.That(image.fillAmount, Is.EqualTo(1f).Within(Tolerance));
            await play;

            Assert.That(image.fillAmount, Is.EqualTo(0f).Within(Tolerance));
        });

        [Test]
        public void Set_FillModeWithoutImage_LogsError()
        {
            SetField("fillMode", ProgressBar.FillMode.SetFill);
            LogAssert.Expect(LogType.Error, new Regex(@"\[ProgressBar\]: Fill of .* has no Image for SetFill"));

            _bar.Set(1f, 2f, "x");
        }

        [TestCase(ProgressBar.FillMode.SetWidth)]
        [TestCase(ProgressBar.FillMode.SetFill)]
        public void Awake_WritesInitialFill(ProgressBar.FillMode mode)
        {
            var host = new GameObject("initial_fill", typeof(RectTransform));
            host.SetActive(false);
            host.transform.SetParent(_root.transform, false);
            _fill.SetParent(host.transform, false);
            var image = mode == ProgressBar.FillMode.SetFill ? UseFillMode() : null;
            var bar = host.AddComponent<ProgressBar>();
            SetField(bar, "fill", _fill);
            SetField(bar, "fillMode", mode);
            SetField(bar, "fullWidth", FullWidth);
            SetField(bar, "initialFill", 0.4f);

            host.SetActive(true);

            if (image != null)
                Assert.That(image.fillAmount, Is.EqualTo(0.4f).Within(Tolerance));
            else
                Assert.That(_fill.rect.width, Is.EqualTo(120f).Within(Tolerance));
        }

        [Test]
        public void SetLabel_WritesLabelAndKeepsFill()
        {
            _bar.Set(1f, 2f, "before");

            _bar.SetLabel("after");

            Assert.That(_label.text, Is.EqualTo("after"));
            Assert.That(_fill.rect.width, Is.EqualTo(150f).Within(Tolerance));
        }

        [Test]
        public void SetLabelActive_TogglesLabelAndKeepsTextAndFill()
        {
            _bar.Set(1f, 2f, "before");

            _bar.SetLabelActive(false);

            Assert.That(_label.gameObject.activeSelf, Is.False);
            Assert.That(_label.text, Is.EqualTo("before"));
            Assert.That(_fill.rect.width, Is.EqualTo(150f).Within(Tolerance));

            _bar.SetLabelActive(true);

            Assert.That(_label.gameObject.activeSelf, Is.True);
        }

        private Image UseFillMode()
        {
            var image = _fill.gameObject.AddComponent<Image>();
            image.type = Image.Type.Filled;
            image.fillMethod = Image.FillMethod.Horizontal;
            SetField("fillMode", ProgressBar.FillMode.SetFill);
            return image;
        }

        private void SetField(string name, object value) => SetField(_bar, name, value);

        private static void SetField(ProgressBar bar, string name, object value) =>
            typeof(ProgressBar).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(bar, value);
    }
}
