using NUnit.Framework;
using UnityEngine;

namespace Prototype.Tests
{
    /// <summary>
    /// 컷인 슬라이드 진행률. 코루틴 없이 계산만 검증한다.
    ///
    /// 곡선 모양은 타이밍을 인자로 받는 static 오버로드로 본다 — 인스펙터 기본값이 바뀌어도
    /// 이 테스트들은 흔들리지 않아야 한다. 기본값 자체는 마지막 두 테스트가 따로 지킨다.
    /// </summary>
    public class SkillCutinTimelineTests
    {
        private const float Tol = 0.001f;

        // 곡선 검증용 고정 타이밍. 실제 기본값과 무관하게 계산만 본다.
        private const float FixedIn = 0.2f;
        private const float FixedHold = 0.5f;
        private const float FixedOut = 0.2f;

        private static float Amount(float elapsed, float delay = 0f)
            => SkillCutinUI.SlideAmount(elapsed, delay, FixedIn, FixedHold, FixedOut);

        [Test]
        public void AtStart_IsFullyOffscreen()
        {
            Assert.That(Amount(0f), Is.EqualTo(0f).Within(Tol));
        }

        [Test]
        public void AfterSlideIn_IsFullyShown()
        {
            Assert.That(Amount(FixedIn), Is.EqualTo(1f).Within(Tol));
        }

        [Test]
        public void DuringHold_StaysFullyShown()
        {
            Assert.That(Amount(FixedIn + FixedHold * 0.5f), Is.EqualTo(1f).Within(Tol));
        }

        [Test]
        public void AtEnd_IsFullyOffscreenAgain()
        {
            float end = FixedIn + FixedHold + FixedOut;

            Assert.That(Amount(end), Is.EqualTo(0f).Within(Tol));
            Assert.That(Amount(end + 5f), Is.EqualTo(0f).Within(Tol));
        }

        [Test]
        public void SlideIn_IsMonotonicallyIncreasing()
        {
            float quarter = Amount(FixedIn * 0.25f);
            float half = Amount(FixedIn * 0.5f);
            float threeQuarter = Amount(FixedIn * 0.75f);

            Assert.That(quarter, Is.LessThan(half));
            Assert.That(half, Is.LessThan(threeQuarter));
            Assert.That(threeQuarter, Is.LessThan(1f));
        }

        [Test]
        public void SlideIn_EasesOut_FastThenSlow()
        {
            // ease-out은 절반 시점에 절반보다 많이 진행해 있다.
            Assert.That(Amount(FixedIn * 0.5f), Is.GreaterThan(0.5f));
        }

        [Test]
        public void Delay_ShiftsTheWholeCurve()
        {
            const float d = 0.1f;

            Assert.That(Amount(d, d), Is.EqualTo(0f).Within(Tol));
            Assert.That(Amount(d * 0.5f, d), Is.EqualTo(0f).Within(Tol), "지연 구간에는 화면 밖에 머문다");
            Assert.That(Amount(FixedIn + d, d), Is.EqualTo(1f).Within(Tol));
        }

        [Test]
        public void ZeroLengthSegments_DoNotDivideByZero()
        {
            // 인스펙터에서 구간을 0으로 만들어도 NaN이 나오면 안 된다.
            float atZeroIn = SkillCutinUI.SlideAmount(0.01f, 0f, 0f, FixedHold, FixedOut);
            float atZeroOut = SkillCutinUI.SlideAmount(FixedIn + FixedHold + 0.01f, 0f, FixedIn, FixedHold, 0f);

            Assert.That(float.IsNaN(atZeroIn), Is.False);
            Assert.That(atZeroIn, Is.EqualTo(1f).Within(Tol), "선딜이 0이면 곧바로 제자리다");
            Assert.That(float.IsNaN(atZeroOut), Is.False);
            Assert.That(atZeroOut, Is.EqualTo(0f).Within(Tol), "후딜이 0이면 곧바로 사라진다");
        }

        // ── 인스펙터 기본값 ───────────────────────────────

        [Test]
        public void Duration_CoversTheDelayedLabel()
        {
            float expected = cutin.SlideIn + cutin.Hold + cutin.SlideOut + cutin.LabelDelay;

            Assert.That(cutin.Duration, Is.EqualTo(expected).Within(Tol));
        }

        [Test]
        public void DefaultHold_IsLongEnoughToReadTheSkillName()
        {
            // 슬롯마다 뜨는 연출이라 짧게 잡고 싶은 유혹이 있지만,
            // 0.25s로 잡았더니 "읽기도 전에 사라진다"는 피드백이 나왔다. 그 아래로 내리지 않는다.
            Assert.That(cutin.Hold, Is.GreaterThanOrEqualTo(0.5f));
        }

        // ── 픽스처 ───────────────────────────────────────

        private SkillCutinUI cutin;

        [SetUp]
        public void SetUp()
        {
            cutin = new GameObject("SkillCutinUI").AddComponent<SkillCutinUI>();
        }

        [TearDown]
        public void TearDown()
        {
            if (cutin != null) Object.DestroyImmediate(cutin.gameObject);
            cutin = null;
        }
    }
}
