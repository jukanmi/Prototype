using NUnit.Framework;
using UnityEngine;

namespace Prototype.Tests
{
    /// <summary>
    /// 라벨 위치 계산만 검증한다. 순회 · 풀링은 BattleRegistry에 의존하는데
    /// 그 목록은 Enemy.Start에서 채워지고 EditMode는 Start를 돌리지 않는다.
    /// </summary>
    public class EnemyStateLabelTests
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

        [Test]
        public void LabelNeverShiftsSideways()
        {
            // 깊이는 카메라가 보여 준다 — 좌표를 옆으로 밀지 않는다.
            // 머리 오프셋은 몸이 줄어든 만큼(z=2 → 0.88배) 같이 내려오고, 화면 위로 올라간다.
            Vector3 p = EnemyStateLabel.LabelPosition(new Vector3(5f, 0f, 2f), height: 0f, headOffset: 1.9f);
            float lift = 1.9f * 0.88f;

            Assert.That(p.x, Is.EqualTo(5f).Within(0.001f), "라벨이 옆으로 밀리면 안 된다");
            Assert.That(p.y, Is.EqualTo(lift * BeltScrollTestCamera.Cos).Within(0.001f));
            Assert.That(p.z, Is.EqualTo(2f + lift * BeltScrollTestCamera.Sin).Within(0.001f));
        }

        /// <summary>라벨이 몸과 다른 x에 뜨면 누구 라벨인지 알 수 없다.</summary>
        [Test]
        public void LabelSharesTheBodyColumn()
        {
            var ground = new Vector3(-2f, 0f, 2.5f);

            Vector3 body = BeltScroll.ToView(ground, 0f);
            Vector3 label = EnemyStateLabel.LabelPosition(ground, height: 0f, headOffset: 1.9f);

            Assert.That(label.x, Is.EqualTo(body.x).Within(0.0001f));
        }

        [Test]
        public void LabelRisesWithJumpHeight()
        {
            Vector3 ground = new Vector3(0f, 0f, 0f);

            Vector3 low = EnemyStateLabel.LabelPosition(ground, height: 0f, headOffset: 1.9f);
            Vector3 high = EnemyStateLabel.LabelPosition(ground, height: 3f, headOffset: 1.9f);

            Assert.That(high.y - low.y, Is.EqualTo(3f).Within(0.0001f),
                        "띄워진 적의 라벨은 같이 올라가야 한다");
        }

        [Test]
        public void LabelSitsAboveTheHead()
        {
            Vector3 ground = new Vector3(0f, 0f, 4f);

            Vector3 body = BeltScroll.ToView(ground, 0f);
            Vector3 label = EnemyStateLabel.LabelPosition(ground, height: 0f, headOffset: 1.9f);

            Assert.That(label.y, Is.GreaterThan(body.y));
        }

        [Test]
        public void LabelClearsTheChargeGauge()
        {
            // ChargeGauge는 1.6에 뜬다. 라벨이 그보다 낮으면 겹친다.
            const float ChargeGaugeOffset = 1.6f;

            Vector3 ground = Vector3.zero;
            Vector3 gauge = BeltScroll.ToView(ground, ChargeGaugeOffset);
            Vector3 label = EnemyStateLabel.LabelPosition(ground, height: 0f, headOffset: 1.9f);

            Assert.That(label.y, Is.GreaterThan(gauge.y));
        }
    }
}
