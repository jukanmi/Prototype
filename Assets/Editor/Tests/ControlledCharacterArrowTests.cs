using NUnit.Framework;
using UnityEngine;

namespace Prototype.Tests
{
    /// <summary>
    /// 조작 캐릭터 머리 위 화살표 위치 및 부유 계산 검증.
    /// <see cref="EnemyStateLabelTests"/>와 같은 규격이다.
    /// </summary>
    public class ControlledCharacterArrowTests
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
        public void ArrowNeverShiftsSideways()
        {
            // 깊이는 카메라가 보여 준다 — 좌표를 옆으로 밀지 않는다.
            // 머리 오프셋은 몸이 줄어든 만큼(z=2 → 0.88배) 같이 내려오고, 화면 위로 올라간다.
            Vector3 p = ControlledCharacterArrow.ArrowPosition(new Vector3(4f, 0f, 2f), height: 0f, headOffset: 2.4f);
            float lift = 2.4f * 0.88f;

            Assert.That(p.x, Is.EqualTo(4f).Within(0.001f), "화살표가 옆으로 밀리면 안 된다");
            Assert.That(p.y, Is.EqualTo(lift * BeltScrollTestCamera.Cos).Within(0.001f));
            Assert.That(p.z, Is.EqualTo(2f + lift * BeltScrollTestCamera.Sin).Within(0.001f));
        }

        [Test]
        public void ArrowSharesTheBodyColumn()
        {
            var ground = new Vector3(-3f, 0f, 1.5f);

            Vector3 body = BeltScroll.ToView(ground, 0f);
            Vector3 arrow = ControlledCharacterArrow.ArrowPosition(ground, height: 0f, headOffset: 2.4f);

            Assert.That(arrow.x, Is.EqualTo(body.x).Within(0.0001f));
        }

        [Test]
        public void ArrowRisesWithJumpHeight()
        {
            Vector3 ground = Vector3.zero;

            Vector3 low = ControlledCharacterArrow.ArrowPosition(ground, height: 0f, headOffset: 2.4f);
            Vector3 high = ControlledCharacterArrow.ArrowPosition(ground, height: 2.5f, headOffset: 2.4f);

            Assert.That(high.y - low.y, Is.EqualTo(2.5f).Within(0.0001f),
                        "점프한 캐릭터의 화살표는 같은 높이만큼 상승해야 한다");
        }

        [Test]
        public void ArrowSitsAboveHead()
        {
            Vector3 ground = new Vector3(0f, 0f, 3f);

            Vector3 body = BeltScroll.ToView(ground, 0f);
            Vector3 arrow = ControlledCharacterArrow.ArrowPosition(ground, height: 0f, headOffset: 2.4f);

            Assert.That(arrow.y, Is.GreaterThan(body.y));
        }

        [Test]
        public void ArrowClearsStatusEffectBar()
        {
            // StatusEffectBar는 2.25에 뜬다. 화살표(2.4)는 그보다 위에 위치해야 겹치지 않는다.
            const float StatusBarOffset = 2.25f;

            Vector3 ground = Vector3.zero;
            Vector3 status = BeltScroll.ToView(ground, StatusBarOffset);
            Vector3 arrow = ControlledCharacterArrow.ArrowPosition(ground, height: 0f, headOffset: 2.4f);

            Assert.That(arrow.y, Is.GreaterThan(status.y));
        }

        [Test]
        public void BobOffsetOscillatesWithinBounds()
        {
            const float Speed = 5f;
            const float Height = 0.12f;

            for (float t = 0f; t < 2f; t += 0.05f)
            {
                float bob = ControlledCharacterArrow.BobOffset(t, Speed, Height);
                Assert.That(bob, Is.GreaterThanOrEqualTo(-Height - 0.0001f));
                Assert.That(bob, Is.LessThanOrEqualTo(Height + 0.0001f));
            }
        }

        [Test]
        public void BobOffsetIsZeroAtZeroTime()
        {
            float bob = ControlledCharacterArrow.BobOffset(0f, speed: 5f, height: 0.12f);
            Assert.That(bob, Is.EqualTo(0f).Within(0.0001f));
        }
    }
}
