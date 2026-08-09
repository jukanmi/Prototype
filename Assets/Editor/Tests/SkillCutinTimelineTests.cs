using NUnit.Framework;

namespace Prototype.Tests
{
    /// <summary>컷인 슬라이드 진행률. 코루틴 없이 계산만 검증한다.</summary>
    public class SkillCutinTimelineTests
    {
        private const float Tol = 0.001f;

        [Test]
        public void AtStart_IsFullyOffscreen()
        {
            Assert.That(SkillCutinUI.SlideAmount(0f, 0f), Is.EqualTo(0f).Within(Tol));
        }

        [Test]
        public void AfterSlideIn_IsFullyShown()
        {
            Assert.That(SkillCutinUI.SlideAmount(SkillCutinUI.SlideIn, 0f), Is.EqualTo(1f).Within(Tol));
        }

        [Test]
        public void DuringHold_StaysFullyShown()
        {
            float mid = SkillCutinUI.SlideIn + SkillCutinUI.Hold * 0.5f;

            Assert.That(SkillCutinUI.SlideAmount(mid, 0f), Is.EqualTo(1f).Within(Tol));
        }

        [Test]
        public void AtEnd_IsFullyOffscreenAgain()
        {
            float end = SkillCutinUI.SlideIn + SkillCutinUI.Hold + SkillCutinUI.SlideOut;

            Assert.That(SkillCutinUI.SlideAmount(end, 0f), Is.EqualTo(0f).Within(Tol));
            Assert.That(SkillCutinUI.SlideAmount(end + 5f, 0f), Is.EqualTo(0f).Within(Tol));
        }

        [Test]
        public void SlideIn_IsMonotonicallyIncreasing()
        {
            float quarter = SkillCutinUI.SlideAmount(SkillCutinUI.SlideIn * 0.25f, 0f);
            float half = SkillCutinUI.SlideAmount(SkillCutinUI.SlideIn * 0.5f, 0f);
            float threeQuarter = SkillCutinUI.SlideAmount(SkillCutinUI.SlideIn * 0.75f, 0f);

            Assert.That(quarter, Is.LessThan(half));
            Assert.That(half, Is.LessThan(threeQuarter));
            Assert.That(threeQuarter, Is.LessThan(1f));
        }

        [Test]
        public void SlideIn_EasesOut_FastThenSlow()
        {
            // ease-out은 절반 시점에 절반보다 많이 진행해 있다.
            Assert.That(SkillCutinUI.SlideAmount(SkillCutinUI.SlideIn * 0.5f, 0f), Is.GreaterThan(0.5f));
        }

        [Test]
        public void Delay_ShiftsTheWholeCurve()
        {
            float d = SkillCutinUI.LabelDelay;

            Assert.That(SkillCutinUI.SlideAmount(d, d), Is.EqualTo(0f).Within(Tol));
            Assert.That(SkillCutinUI.SlideAmount(d * 0.5f, d), Is.EqualTo(0f).Within(Tol),
                        "지연 구간에는 화면 밖에 머문다");
            Assert.That(SkillCutinUI.SlideAmount(SkillCutinUI.SlideIn + d, d), Is.EqualTo(1f).Within(Tol));
        }

        [Test]
        public void Duration_CoversTheDelayedLabel()
        {
            float expected = SkillCutinUI.SlideIn + SkillCutinUI.Hold + SkillCutinUI.SlideOut + SkillCutinUI.LabelDelay;

            Assert.That(SkillCutinUI.Duration, Is.EqualTo(expected).Within(Tol));
            Assert.That(SkillCutinUI.Duration, Is.EqualTo(0.55f).Within(Tol));
        }
    }
}
