using NUnit.Framework;
using UnityEngine;

namespace Prototype.Tests
{
    /// <summary>
    /// "부드럽게 올라가고 부드럽게 내려온다"를 검증한다.
    ///
    /// 눈으로만 보면 프레임이 튈 때 감이 달라지는 걸 못 잡는다.
    /// 지수 감쇠를 고른 이유가 바로 그것이므로, 그 성질 자체를 못 박아 둔다.
    /// </summary>
    public class CardPoseAnimationTests
    {
        private const float Eps = 0.0001f;

        private static readonly CardPose Home = HandFanLayout.Pose(1, 4, CardVisualState.Idle);
        private static readonly CardPose Lifted = HandFanLayout.Pose(1, 4, CardVisualState.Grabbed);

        private static CardPose Run(CardPose from, CardPose to, float seconds, float step = 1f / 60f)
        {
            CardPose cur = from;
            for (float t = 0f; t < seconds; t += step)
                cur = CardPoseAnimator.Step(cur, to, step);
            return cur;
        }

        [Test]
        public void ZeroDelta_DoesNotMove()
        {
            CardPose next = CardPoseAnimator.Step(Home, Lifted, 0f);

            Assert.That(next.pos.y, Is.EqualTo(Home.pos.y).Within(Eps));
            Assert.That(next.scale, Is.EqualTo(Home.scale).Within(Eps));
        }

        [Test]
        public void Weight_StaysInsideZeroToOne()
        {
            Assert.That(CardPoseAnimator.Weight(0f, 14f), Is.EqualTo(0f).Within(Eps));
            Assert.That(CardPoseAnimator.Weight(1f / 60f, 14f), Is.InRange(0f, 1f));
            Assert.That(CardPoseAnimator.Weight(10f, 14f), Is.InRange(0f, 1f));
        }

        /// <summary>한 프레임이 통째로 밀려도 목표를 지나치면 안 된다. 튕겨 보인다.</summary>
        [Test]
        public void HugeDelta_NeverOvershoots()
        {
            CardPose next = CardPoseAnimator.Step(Home, Lifted, 5f);

            Assert.That(next.pos.y, Is.LessThanOrEqualTo(Lifted.pos.y + Eps));
            Assert.That(next.scale, Is.LessThanOrEqualTo(Lifted.scale + Eps));
        }

        [Test]
        public void Converges_OnTheTarget()
        {
            CardPose end = Run(Home, Lifted, seconds: 1.5f);

            Assert.That(CardPoseAnimator.IsSettled(end, Lifted), Is.True,
                $"1.5초가 지나도 목표에 못 닿았다 (y {end.pos.y:0.##} vs {Lifted.pos.y:0.##})");
        }

        /// <summary>
        /// 프레임레이트 독립. 반 스텝 두 번이 한 스텝과 같아야
        /// 프레임이 흔들려도 같은 시간에 같은 자리에 온다.
        /// </summary>
        [Test]
        public void TwoHalfSteps_EqualOneFullStep()
        {
            const float dt = 1f / 30f;

            CardPose once = CardPoseAnimator.Step(Home, Lifted, dt);

            CardPose twice = CardPoseAnimator.Step(Home, Lifted, dt * 0.5f);
            twice = CardPoseAnimator.Step(twice, Lifted, dt * 0.5f);

            Assert.That(twice.pos.y, Is.EqualTo(once.pos.y).Within(0.001f));
            Assert.That(twice.angle, Is.EqualTo(once.angle).Within(0.001f));
            Assert.That(twice.scale, Is.EqualTo(once.scale).Within(0.001f));
        }

        /// <summary>올라갈 때와 내려올 때가 같은 코드다. 왕복하면 정확히 제자리여야 한다.</summary>
        [Test]
        public void GrabThenRelease_ReturnsToTheSamePose()
        {
            CardPose up = Run(Home, Lifted, seconds: 1.5f);
            CardPose back = Run(up, Home, seconds: 1.5f);

            Assert.That(back.pos.x, Is.EqualTo(Home.pos.x).Within(0.01f));
            Assert.That(back.pos.y, Is.EqualTo(Home.pos.y).Within(0.01f));
            Assert.That(back.angle, Is.EqualTo(Home.angle).Within(0.01f), "기울기가 안 돌아왔다");
            Assert.That(back.scale, Is.EqualTo(Home.scale).Within(0.001f), "크기가 안 돌아왔다");
        }

        /// <summary>중간에 목표가 바뀌어도(집었다 곧바로 놓아도) 튀지 않고 이어져야 한다.</summary>
        [Test]
        public void ReversingMidFlight_StaysBetweenTheTwoPoses()
        {
            CardPose mid = Run(Home, Lifted, seconds: 0.08f);
            Assert.That(mid.pos.y, Is.InRange(Home.pos.y, Lifted.pos.y));

            CardPose back = CardPoseAnimator.Step(mid, Home, 1f / 60f);
            Assert.That(back.pos.y, Is.LessThan(mid.pos.y), "되돌아가기 시작해야 한다");
            Assert.That(back.pos.y, Is.GreaterThanOrEqualTo(Home.pos.y - Eps));
        }

        /// <summary>움직임은 단조롭게 붙는다. 오르내리면 떨림으로 보인다.</summary>
        [Test]
        public void Approach_IsMonotonic()
        {
            CardPose cur = Home;
            for (int i = 0; i < 40; i++)
            {
                CardPose next = CardPoseAnimator.Step(cur, Lifted, 1f / 60f);

                Assert.That(next.pos.y, Is.GreaterThanOrEqualTo(cur.pos.y - Eps));
                Assert.That(next.pos.y, Is.LessThanOrEqualTo(Lifted.pos.y + Eps));
                cur = next;
            }
        }

        [Test]
        public void IsSettled_OnlyNearTheTarget()
        {
            Assert.That(CardPoseAnimator.IsSettled(Home, Home), Is.True);
            Assert.That(CardPoseAnimator.IsSettled(Home, Lifted), Is.False);
        }

        /// <summary>게이지는 카드보다 느리게 흘러야 소모가 "빠져나가는" 것으로 읽힌다.</summary>
        [Test]
        public void GaugeDrains_SlowerThanCardsMove()
        {
            Assert.That(CardPoseAnimator.GaugeSpeed, Is.LessThan(CardPoseAnimator.DefaultSpeed));

            float gauge = CardPoseAnimator.Step(1f, 0f, 1f / 60f, CardPoseAnimator.GaugeSpeed);
            float card = CardPoseAnimator.Step(1f, 0f, 1f / 60f, CardPoseAnimator.DefaultSpeed);

            Assert.That(gauge, Is.GreaterThan(card), "게이지가 카드보다 빨리 비었다");
        }

        /// <summary>스칼라 감쇠도 목표를 넘지 않는다 — 게이지가 음수 폭이 되면 안 된다.</summary>
        [Test]
        public void ScalarStep_StaysBetweenEndpoints()
        {
            Assert.That(CardPoseAnimator.Step(1f, 0f, 10f, CardPoseAnimator.GaugeSpeed),
                Is.InRange(0f, 1f));
            Assert.That(CardPoseAnimator.Step(0f, 1f, 10f, CardPoseAnimator.GaugeSpeed),
                Is.InRange(0f, 1f));
        }
    }
}
