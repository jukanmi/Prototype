using NUnit.Framework;

namespace Prototype.Tests
{
    /// <summary>체력 수 표기. 올림이되 float 오차로 1이 더 붙지 않아야 한다.</summary>
    public class PartyHealthNumbersTests
    {
        [Test]
        public void FloatNoise_DoesNotRoundUp()
            => Assert.That(PartyHealthHUD.ShownHp(60.000004f), Is.EqualTo(60));

        [Test]
        public void Fraction_RoundsUp_SoAliveShowsAtLeastOne()
            => Assert.That(PartyHealthHUD.ShownHp(0.3f), Is.EqualTo(1));

        [Test]
        public void Zero_IsZero()
            => Assert.That(PartyHealthHUD.ShownHp(0f), Is.EqualTo(0));
    }
}
