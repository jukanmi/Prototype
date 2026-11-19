using NUnit.Framework;
using UnityEngine;

namespace Prototype.Tests
{
    /// <summary>
    /// 총공격 게이지 막대의 순수 계산 — 채움 · 발동 임계 눈금 · 표시 비율.
    /// 막대를 실제로 그리는 건 씬이 있어야 하므로 여기서는 숫자만 본다.
    /// </summary>
    public class BulletTimeGaugeUITests
    {
        private const float Eps = 0.0001f;

        // ── 막대 채움 ────────────────────────────────────

        [Test]
        public void Fill_EmptyAndFull()
        {
            Assert.That(BulletTimeGaugeWidget.FillWidth(0f, 400f), Is.EqualTo(0f).Within(Eps));
            Assert.That(BulletTimeGaugeWidget.FillWidth(1f, 400f), Is.EqualTo(400f).Within(Eps));
        }

        [Test]
        public void Fill_IsProportional()
        {
            Assert.That(BulletTimeGaugeWidget.FillWidth(0.25f, 400f), Is.EqualTo(100f).Within(Eps));
        }

        /// <summary>비율이 범위를 벗어나도 막대가 트랙 밖으로 삐져나가면 안 된다.</summary>
        [Test]
        public void Fill_Clamps()
        {
            Assert.That(BulletTimeGaugeWidget.FillWidth(-0.5f, 400f), Is.EqualTo(0f).Within(Eps));
            Assert.That(BulletTimeGaugeWidget.FillWidth(1.5f, 400f), Is.EqualTo(400f).Within(Eps));
        }

        /// <summary>채움 폭은 테두리를 뺀 안쪽이다. 트랙 전체 폭을 쓰면 테두리를 덮는다.</summary>
        [Test]
        public void InnerWidth_LeavesTheBorderShowing()
        {
            Assert.That(BulletTimeGaugeWidget.InnerWidth, Is.LessThan(BulletTimeGaugeWidget.Width));
            Assert.That(BulletTimeGaugeWidget.InnerWidth, Is.GreaterThan(0f));
        }

        // ── 발동 임계 눈금 ───────────────────────────────

        [Test]
        public void Tick_SitsAtTheRequiredRatio()
        {
            Assert.That(BulletTimeGaugeWidget.TickX(0.5f, 400f), Is.EqualTo(200f).Within(Eps));
            Assert.That(BulletTimeGaugeWidget.TickX(0f, 400f), Is.EqualTo(0f).Within(Eps));
        }

        /// <summary>눈금이 선 자리가 곧 "여기까지 차면 쓸 수 있다"는 곳이어야 한다.</summary>
        [Test]
        public void Tick_MeetsTheFillAtTheRequiredRatio()
        {
            const float w = 400f;
            const float required = 0.6f;

            Assert.That(BulletTimeGaugeWidget.FillWidth(required, w),
                Is.EqualTo(BulletTimeGaugeWidget.TickX(required, w)).Within(Eps));
        }

        /// <summary>100%가 필요하면 눈금은 막대 끝과 겹쳐 아무것도 안 알려 준다. 안 그린다.</summary>
        [Test]
        public void Tick_HiddenWhenTheWholeBarIsRequired()
        {
            Assert.That(BulletTimeGaugeWidget.ShowsTick(1f), Is.False);
            Assert.That(BulletTimeGaugeWidget.ShowsTick(0f), Is.False);
            Assert.That(BulletTimeGaugeWidget.ShowsTick(0.6f), Is.True);
        }

        [Test]
        public void Pulse_StaysInsideZeroToOne()
        {
            for (float t = 0f; t < 3f; t += 0.05f)
                Assert.That(BulletTimeGaugeWidget.Pulse(t), Is.InRange(0f, 1f));
        }

        // ── 표시 비율 ────────────────────────────────────

        [Test]
        public void Chase_ZeroDelta_DoesNotMove()
        {
            Assert.That(BulletTimeGaugeWidget.Chase(0.3f, 1f, 0f), Is.EqualTo(0.3f).Within(Eps));
        }

        /// <summary>프레임이 아무리 길어도 목표를 넘어가지 않는다.</summary>
        [Test]
        public void Chase_StaysBetweenEndpoints()
        {
            Assert.That(BulletTimeGaugeWidget.Chase(1f, 0f, 10f), Is.InRange(0f, 1f));
            Assert.That(BulletTimeGaugeWidget.Chase(0f, 1f, 10f), Is.InRange(0f, 1f));
        }

        /// <summary>반 프레임 두 번이 한 프레임과 같다 — 프레임레이트가 흔들려도 같은 시간이면 같은 자리다.</summary>
        [Test]
        public void Chase_TwoHalfSteps_EqualOneFullStep()
        {
            const float dt = 1f / 30f;

            float once = BulletTimeGaugeWidget.Chase(1f, 0f, dt);
            float twice = BulletTimeGaugeWidget.Chase(BulletTimeGaugeWidget.Chase(1f, 0f, dt * 0.5f), 0f, dt * 0.5f);

            Assert.That(twice, Is.EqualTo(once).Within(Eps));
        }
    }
}
