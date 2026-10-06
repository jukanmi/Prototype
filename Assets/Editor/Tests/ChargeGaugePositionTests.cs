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
        private GameObject camRig;

        [SetUp]
        public void SetUp()
        {
            // 머리 위 오프셋이 화면 위(0, cosθ, sinθ) 방향으로 올라간다. 카메라를 명시적으로
            // 물려 주지 않으면 Camera.main을 집어 열려 있던 씬에 따라 값이 달라진다.
            BeltScroll.DepthScalePerUnit = 0.06f;
            camRig = BeltScrollTestCamera.Attach();
        }

        [TearDown]
        public void TearDown()
        {
            BeltScrollTestCamera.Detach(camRig);
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

        /// <summary>오프셋은 화면 위로 올린다 — 화면에서 재면 1.6이지만 월드 Y로는 cosθ배다.</summary>
        [Test]
        public void AtOrigin_OffsetRidesScreenUp()
        {
            Vector3 p = ChargeGauge.GaugePosition(Vector3.zero, height: 0f, headOffset: 1.6f);

            Assert.That(p.y, Is.EqualTo(1.6f * BeltScrollTestCamera.Cos).Within(0.001f));
            Assert.That(p.z, Is.EqualTo(1.6f * BeltScrollTestCamera.Sin).Within(0.001f));
        }

        /// <summary>
        /// z=3에서 배율 0.82. 깊이는 카메라가 보여 주므로 <b>좌표에 접히지 않는다</b> —
        /// z는 z에 그대로 남고, 줄어드는 것은 머리 오프셋뿐이다.
        /// </summary>
        [Test]
        public void AtBack_OffsetShrinks_AndDepthStaysInZ()
        {
            var ground = new Vector3(0f, 0f, 3f);

            Vector3 p = ChargeGauge.GaugePosition(ground, height: 0f, headOffset: 1.6f);
            float lift = 1.6f * 0.82f;

            Assert.That(p.y, Is.EqualTo(lift * BeltScrollTestCamera.Cos).Within(0.001f));
            Assert.That(p.z, Is.EqualTo(3f + lift * BeltScrollTestCamera.Sin).Within(0.001f));
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
