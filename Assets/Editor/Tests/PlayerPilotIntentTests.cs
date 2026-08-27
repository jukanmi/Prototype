using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Prototype.Tests
{
    /// <summary>
    /// 조종사가 입력을 몸의 <see cref="Command"/>로 옮기는 계층으로만 남았는지 본다.
    ///
    /// 키를 실제로 누르는 경로는 못 만든다 — <c>InputTestFixture</c>가 별도 어셈블리라
    /// asmdef 없이는 참조를 못 건다. 그래서 여기서 보는 건 <b>게이트와 인계</b>다:
    /// 입력이 없거나 막혀 있을 때 명령이 새지 않는가, 몸을 잡고 놓을 때 뒷정리가 되는가.
    /// </summary>
    public class PlayerPilotIntentTests
    {
        private readonly List<GameObject> spawned = new List<GameObject>();

        private PlayerPilot pilot;
        private Ally body;

        [SetUp]
        public void SetUp()
        {
            BattleRegistry.Clear();

            // 입력 호스트 없이 세운다. Tick이 그걸 견디는지가 검사 대상 중 하나다 —
            // 조종사는 같은 오브젝트가 아니라 PlayerInputController.Instance를 본다.
            pilot = NewObject("PlayerPilot").AddComponent<PlayerPilot>();
            body = NewObject("Body").AddComponent<Ally>();

            TimeControl.Reset();
        }

        [TearDown]
        public void TearDown()
        {
            BattleRegistry.Clear();

            for (int i = 0; i < spawned.Count; i++)
                if (spawned[i] != null) Object.DestroyImmediate(spawned[i]);
            spawned.Clear();

            TimeControl.Reset();
        }

        // ── 게이트 ──────────────────────────────────────

        [Test]
        public void WithoutBody_TickDoesNotThrow()
        {
            Assert.That(pilot.Body, Is.Null);
            Assert.DoesNotThrow(() => pilot.Tick(0.016f));
        }

        [Test]
        public void WithoutInputController_TickDoesNotThrow()
        {
            // 씬에 입력 호스트가 없으면 Instance가 null이다. 예외 대신 조용히
            // 아무 명령도 안 내는 쪽이 맞다 — 배선 누락은 BattleInputPrefabTests가 잡는다.
            pilot.Take(body);

            Assert.DoesNotThrow(() => pilot.Tick(0.016f));
        }

        [Test]
        public void WithoutInputController_IssuesNoCommand()
        {
            pilot.Take(body);
            pilot.Tick(0.016f);

            Assert.That(body.Command, Is.EqualTo(Command.None));
            Assert.That(body.MoveDirection, Is.EqualTo(Vector3.zero));
        }

        [Test]
        public void WhileFrozen_IssuesNoCommand()
        {
            TimeControl.Scale = 0f;
            Assert.That(TimeControl.IsFrozen, Is.True, "전제가 깨졌다 — IsFrozen이 안 켜졌다.");

            pilot.Take(body);
            pilot.Tick(0.016f);

            // 정지 중 이동 · 평타가 새면 불릿타임에 카드를 놓는 동안 캐릭터가 움직인다.
            Assert.That(body.Command, Is.EqualTo(Command.None));
            Assert.That(body.MoveDirection, Is.EqualTo(Vector3.zero));
        }

        [Test]
        public void Tick_ClearsPreviousFrameCommand()
        {
            pilot.Take(body);

            pilot.Tick(0.016f);
            Assert.That(body.Command, Is.EqualTo(Command.None));

            // 명령이 프레임을 넘어 남으면 한 번 누른 평타가 두 번 나간다.
            pilot.Tick(0.016f);
            Assert.That(body.Command, Is.EqualTo(Command.None));
        }

        /// <summary>
        /// 지휘 중에는 조종사가 손을 뗀다. 콤보가 도는 동안 상태머신은 Executor 소유다 —
        /// 여기서 명령이 새면 입력과 시전이 겹친다.
        /// </summary>
        [Test]
        public void WhileCommanded_IssuesNoCommand()
        {
            pilot.Take(body);
            body.Drive(Command.Move, Vector3.right);
            body.IsCommanded = true;

            pilot.Tick(0.016f);

            Assert.That(body.Command, Is.EqualTo(Command.None), "지휘 중 명령이 샜다");
            Assert.That(body.MoveDirection, Is.EqualTo(Vector3.zero));
        }

        /// <summary>
        /// 벤치에 내려간 몸은 아예 안 건드린다. 불릿타임에 조작 캐릭터가 잠깐 숨는 구간이 그렇다 —
        /// 거기서 의도를 비우면 다시 섰을 때 걷던 관성이 끊긴다.
        /// </summary>
        [Test]
        public void BenchedBody_IsNotDriven()
        {
            pilot.Take(body);
            body.Drive(Command.Move, Vector3.right);

            body.gameObject.SetActive(false);
            pilot.Tick(0.016f);

            Assert.That(body.MoveDirection, Is.EqualTo(Vector3.right), "벤치 몸을 건드렸다");
        }

        // ── 인계 ────────────────────────────────────────

        [Test]
        public void Take_MarksTheBodyAsPiloted()
        {
            pilot.Take(body);

            Assert.That(pilot.Body, Is.SameAs(body));
            Assert.That(body.IsPiloted, Is.True);
        }

        /// <summary>
        /// 놓을 때 의도를 안 비우면, 마지막 이동 방향을 물고 내려간 몸이
        /// 다시 섰을 때 혼자 걸어간다.
        /// </summary>
        [Test]
        public void Release_ClearsIntentAndFlag()
        {
            pilot.Take(body);
            body.Drive(Command.Move, Vector3.right);
            body.BufferAttack(0.25f);

            pilot.Release();

            Assert.That(pilot.Body, Is.Null);
            Assert.That(body.IsPiloted, Is.False);
            Assert.That(body.Command, Is.EqualTo(Command.None));
            Assert.That(body.MoveDirection, Is.EqualTo(Vector3.zero));
            Assert.That(body.HasAttackBuffer, Is.False, "선입력이 남으면 다시 섰을 때 안 누른 평타가 나간다");
        }

        /// <summary>몸을 갈아탈 때 직전 몸은 자동으로 놓아야 한다 — 두 몸이 동시에 조작되면 안 된다.</summary>
        [Test]
        public void Take_ReleasesThePreviousBody()
        {
            Ally other = NewObject("Other").AddComponent<Ally>();

            pilot.Take(body);
            pilot.Take(other);

            Assert.That(body.IsPiloted, Is.False, "직전 몸이 조작 대상으로 남았다");
            Assert.That(other.IsPiloted, Is.True);
            Assert.That(pilot.Body, Is.SameAs(other));
        }

        [Test]
        public void Take_SameBodyTwice_KeepsIt()
        {
            pilot.Take(body);
            pilot.Take(body);

            Assert.That(pilot.Body, Is.SameAs(body));
            Assert.That(body.IsPiloted, Is.True);
        }

        private GameObject NewObject(string name)
        {
            var go = new GameObject(name);
            spawned.Add(go);
            return go;
        }
    }
}
