using NUnit.Framework;

namespace Prototype.Tests
{
    /// <summary>
    /// 다단히트가 발동 구간 안에 어떻게 흩어지는지 검증한다.
    ///
    /// 세 타격이 한 프레임에 몰려도 데미지 합은 같아서 로그로는 정상으로 보인다 —
    /// 화면에서만 "한 번 맞은 것 같은데 체력이 왕창 줄었다"로 나타나는 종류의 버그라
    /// 순수 함수로 떼어 놓고 여기서 잡는다.
    /// </summary>
    public class BossPatternScheduleTests
    {
        private const float Active = 0.6f;

        [Test]
        public void SingleHit_FiresAtStartOfActive()
        {
            Assert.That(BossPatternAction.HitTime(Active, 0, 1), Is.EqualTo(0f));
        }

        [Test]
        public void TripleHit_SplitsActiveEvenly()
        {
            Assert.That(BossPatternAction.HitTime(Active, 0, 3), Is.EqualTo(0f).Within(0.0001f));
            Assert.That(BossPatternAction.HitTime(Active, 1, 3), Is.EqualTo(0.2f).Within(0.0001f));
            Assert.That(BossPatternAction.HitTime(Active, 2, 3), Is.EqualTo(0.4f).Within(0.0001f));
        }

        /// <summary>마지막 타격이 발동 구간 끝에 걸리면 판정이 켜지기 전에 후딜로 넘어간다.</summary>
        [Test]
        public void LastHit_LandsBeforeActiveEnds()
        {
            float last = BossPatternAction.HitTime(Active, 2, 3);

            Assert.That(last, Is.LessThan(Active));
        }

        [Test]
        public void Hits_AreStrictlyIncreasing()
        {
            float prev = -1f;

            for (int i = 0; i < 5; i++)
            {
                float t = BossPatternAction.HitTime(Active, i, 5);
                Assert.That(t, Is.GreaterThan(prev), $"{i}번째 타격이 앞 타격보다 늦지 않다");
                prev = t;
            }
        }

        /// <summary>길이가 음수인 패턴을 저작해도 타격 시각이 과거로 가면 안 된다.</summary>
        [Test]
        public void NegativeDuration_ClampsToZero()
        {
            Assert.That(BossPatternAction.HitTime(-1f, 2, 3), Is.EqualTo(0f));
        }

        [Test]
        public void ZeroOrNegativeTotal_FallsBackToImmediate()
        {
            Assert.That(BossPatternAction.HitTime(Active, 0, 0), Is.EqualTo(0f));
        }
    }
}
