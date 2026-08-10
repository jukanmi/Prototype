using NUnit.Framework;
using UnityEngine;

namespace Prototype.Tests
{
    /// <summary>
    /// PlayerControl이 입력을 <see cref="Command"/>로 옮기는 계층으로만 남았는지 본다.
    ///
    /// 키를 실제로 누르는 경로는 못 만든다 — <c>InputTestFixture</c>가 별도 어셈블리라
    /// asmdef 없이는 참조를 못 건다. 그래서 여기서 보는 건 <b>게이트</b>다:
    /// 입력이 없거나 막혀 있을 때 명령이 새지 않는가.
    /// </summary>
    public class PlayerControlIntentTests
    {
        private GameObject go;
        private PlayerControl control;

        [SetUp]
        public void SetUp()
        {
            // PlayerInputController 없이 세운다. Tick이 그걸 견디는지가 검사 대상 중 하나다.
            go = new GameObject("PlayerControlRig");
            control = go.AddComponent<PlayerControl>();

            TimeControl.Reset();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(go);
            TimeControl.Reset();
        }

        [Test]
        public void WithoutInputController_TickDoesNotThrow()
        {
            // 프리팹에 PlayerInputController를 안 붙인 채로 씬을 돌리면 여기서 터진다.
            // 예외 대신 조용히 아무 명령도 안 내는 쪽이 맞다 — 배선 누락은 테스트가 잡는다.
            Assert.DoesNotThrow(() => control.Tick(0.016f));
        }

        [Test]
        public void WithoutInputController_IssuesNoCommand()
        {
            control.Tick(0.016f);

            Assert.That(control.Command, Is.EqualTo(Command.None));
            Assert.That(control.MoveDirection, Is.EqualTo(Vector3.zero));
            Assert.That(control.SkillIndex, Is.EqualTo(-1));
            Assert.That(control.SelfSkillPressed, Is.EqualTo(-1));
        }

        [Test]
        public void WhileFrozen_IssuesNoCommand()
        {
            TimeControl.Scale = 0f;
            Assert.That(TimeControl.IsFrozen, Is.True, "전제가 깨졌다 — IsFrozen이 안 켜졌다.");

            control.Tick(0.016f);

            // 정지 중 이동 · 평타가 새면 불릿타임에 카드를 놓는 동안 캐릭터가 움직인다.
            Assert.That(control.Command, Is.EqualTo(Command.None));
            Assert.That(control.MoveDirection, Is.EqualTo(Vector3.zero));
        }

        [Test]
        public void WhileFrozen_SelfSkillIsBlocked()
        {
            TimeControl.Scale = 0f;
            control.Tick(0.016f);

            // Player가 동료 고유기를 넘길 때 이 값을 본다. 여기서 새면
            // 정지 중에도 동료가 시전해 지휘 입력과 겹친다.
            Assert.That(control.SelfSkillPressed, Is.EqualTo(-1));
        }

        [Test]
        public void Tick_ClearsPreviousFrameCommand()
        {
            control.Tick(0.016f);
            Assert.That(control.Command, Is.EqualTo(Command.None));

            // 명령이 프레임을 넘어 남으면 한 번 누른 평타가 두 번 나간다.
            control.Tick(0.016f);
            Assert.That(control.Command, Is.EqualTo(Command.None));
        }
    }
}
