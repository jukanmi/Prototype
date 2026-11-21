using NUnit.Framework;

namespace Prototype.Tests
{
    public class MainMenuIntroTests
    {
        [TestCase(0f, 0f)]
        [TestCase(1f, 1f)]
        public void EaseOutCubic_StartsAtZero_EndsAtOne(float x, float expected)
        {
            Assert.AreEqual(expected, MainMenuIntro.EaseOutCubic(x), 1e-4f);
        }

        [Test]
        public void EaseOutCubic_RisesMonotonically_WithoutOvershoot()
        {
            float prev = 0f;
            for (int i = 1; i <= 100; i++)
            {
                float v = MainMenuIntro.EaseOutCubic(i / 100f);
                Assert.GreaterOrEqual(v, prev);
                Assert.LessOrEqual(v, 1f + 1e-4f);
                prev = v;
            }
        }
    }
}
