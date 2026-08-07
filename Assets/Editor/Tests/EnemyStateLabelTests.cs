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
        private float saved;
        private float savedShear;
        private float savedScale;

        [SetUp]
        public void SetUp()
        {
            // 전부 static이고 BeltScrollView가 매 프레임 덮어쓴다.
            // 테스트가 씬 빌더와 같은 값으로 고정했다가 원래대로 돌려놓는다.
            saved = BeltScroll.DepthToScreen;
            savedShear = BeltScroll.DepthToScreenX;
            savedScale = BeltScroll.DepthScalePerUnit;
            BeltScroll.DepthToScreen = 0.9f;
            BeltScroll.DepthToScreenX = 0.45f;
            BeltScroll.DepthScalePerUnit = 0.06f;
        }

        [TearDown]
        public void TearDown()
        {
            BeltScroll.DepthToScreen = saved;
            BeltScroll.DepthToScreenX = savedShear;
            BeltScroll.DepthScalePerUnit = savedScale;
        }

        [Test]
        public void LabelFoldsDepthIntoScreenHeight()
        {
            // 깊이 z=2는 화면 세로 1.8로 접히고(0.9 배율) 가로로 0.9 밀린다(0.45 배율).
            // 머리 오프셋은 몸이 줄어든 만큼(z=2 → 0.88배) 같이 내려온다.
            Vector3 p = EnemyStateLabel.LabelPosition(new Vector3(5f, 0f, 2f), height: 0f, headOffset: 1.9f);

            Assert.That(p.x, Is.EqualTo(5f + 0.9f).Within(0.001f), "라벨도 몸과 같이 밀려야 한다");
            Assert.That(p.y, Is.EqualTo(1.8f + 1.9f * 0.88f).Within(0.001f));
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
