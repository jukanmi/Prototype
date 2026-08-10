using NUnit.Framework;
using UnityEngine;

namespace Prototype.Tests
{
    /// <summary>
    /// 머리 위 표시는 몸이 줄어든 만큼 같이 내려와야 한다.
    /// 안 그러면 뒤쪽 적의 게이지만 허공에 뜬다.
    /// </summary>
    public class ChargeGaugePositionTests
    {
        [SetUp]
        public void SetUp()
        {
            BeltScroll.DepthToScreen = 0.9f;
            BeltScroll.DepthToScreenX = 0.45f;
            BeltScroll.DepthScalePerUnit = 0.06f;
        }

        [TearDown]
        public void TearDown()
        {
            BeltScroll.DepthToScreen = 0.5f;
            BeltScroll.DepthToScreenX = 0.45f;
            BeltScroll.DepthScalePerUnit = 0.06f;
        }

        /// <summary>게이지가 몸과 다른 x에 뜨면 누구 게이지인지 알 수 없다.</summary>
        [Test]
        public void GaugeSharesTheBodyColumn()
        {
            var ground = new Vector3(1f, 0f, -2.5f);

            Vector3 body = BeltScroll.ToView(ground, 0f);
            Vector3 gauge = ChargeGauge.GaugePosition(ground, height: 0f, headOffset: 1.6f);

            Assert.That(gauge.x, Is.EqualTo(body.x).Within(0.0001f));
        }

        [Test]
        public void AtOrigin_OffsetIsUnscaled()
        {
            Vector3 p = ChargeGauge.GaugePosition(Vector3.zero, height: 0f, headOffset: 1.6f);

            Assert.That(p.y, Is.EqualTo(1.6f).Within(0.0001f));
        }

        /// <summary>z=3에서 배율 0.82. 바닥은 2.7로 접히고 오프셋만 줄어야 한다.</summary>
        [Test]
        public void AtBack_OffsetShrinksButFloorFoldDoesNot()
        {
            var ground = new Vector3(0f, 0f, 3f);

            Vector3 p = ChargeGauge.GaugePosition(ground, height: 0f, headOffset: 1.6f);

            Assert.That(p.y, Is.EqualTo(2.7f + 1.6f * 0.82f).Within(0.001f));
        }

        /// <summary>점프 높이는 배율을 안 받는다 — 논리적인 높이 그대로다.</summary>
        [Test]
        public void JumpHeight_IsNotScaled()
        {
            var ground = new Vector3(0f, 0f, 3f);

            Vector3 low = ChargeGauge.GaugePosition(ground, height: 0f, headOffset: 1.6f);
            Vector3 high = ChargeGauge.GaugePosition(ground, height: 2f, headOffset: 1.6f);

            Assert.That(high.y - low.y, Is.EqualTo(2f).Within(0.0001f));
        }

        /// <summary>라벨은 게이지보다 위에 있어야 겹치지 않는다 — 배율이 붙은 뒤에도 그렇다.</summary>
        [Test]
        public void LabelStillClearsTheGauge_AtBack()
        {
            var ground = new Vector3(0f, 0f, 3f);

            Vector3 gauge = ChargeGauge.GaugePosition(ground, height: 0f, headOffset: 1.6f);
            Vector3 label = EnemyStateLabel.LabelPosition(ground, height: 0f, headOffset: 1.9f);

            Assert.That(label.y, Is.GreaterThan(gauge.y));
        }
    }
}
