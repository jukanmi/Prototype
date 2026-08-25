using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Prototype.Tests
{
    /// <summary>
    /// ComboExecutor가 슬롯마다 시전자를 무대에 올렸다 내리는지 검증한다.
    ///
    /// <see cref="ComboExecutorCutinTests"/>와 같은 방식으로 Run 코루틴을 손으로 펌프한다.
    /// 중첩 열거자(yield return IEnumerator)는 대신 돌려주지 않으므로 RunSlot 본문은
    /// 실행되지 않는다 — StateMachine · SkillState · VFX를 세우지 않고도 무대 호출 순서만
    /// 깨끗이 관측된다.
    ///
    /// 차징 슬롯은 여기서 다루지 않는다. <c>StartCharge</c>가 곧바로
    /// <c>ForceChangeState</c>를 걸어 Physics · Rigidbody가 붙은 진짜 몸을 요구하기 때문이다.
    /// 무대에 둘 이상이 설 때의 자리 계산은 순수 함수로 떼어 내
    /// <see cref="TagSwapRulesTests"/>에서 검증한다.
    /// </summary>
    public class ComboExecutorStageTests
    {
        private readonly List<GameObject> spawned = new List<GameObject>();
        private readonly List<Object> assets = new List<Object>();

        /// <summary>무대와 컷인 호출이 한 줄에 섞여 기록된다. 순서를 보려면 한 벌이어야 한다.</summary>
        private readonly List<string> log = new List<string>();

        private ComboExecutor executor;
        private FakeStage stage;

        [SetUp]
        public void SetUp()
        {
            BattleRegistry.Clear();
            log.Clear();

            executor = NewObject("ComboExecutor").AddComponent<ComboExecutor>();
            stage = new FakeStage(log);
            executor.Stage = stage;
            executor.Cutin = new FakeCutin(log);
        }

        [TearDown]
        public void TearDown()
        {
            BattleRegistry.Clear();

            for (int i = 0; i < spawned.Count; i++) Object.DestroyImmediate(spawned[i]);
            for (int i = 0; i < assets.Count; i++) Object.DestroyImmediate(assets[i]);
            spawned.Clear();
            assets.Clear();
            executor = null;
            stage = null;
        }

        // ── 등장 ────────────────────────────────────────

        /// <summary>
        /// <b>순서가 이 기능의 전부다.</b> 컷인에 시전자가 보여야 하고, 무엇보다
        /// 꺼진 몸에 상태를 걸면 Update가 안 돌아 슬롯이 타임아웃까지 멎는다.
        /// </summary>
        [Test]
        public void EachSlot_EntersItsCasterBeforeTheCutin()
        {
            Ally warrior = NewAlly("Warrior");
            Ally wizard = NewAlly("Wizard");

            var queue = new Queue<ComboSlot>();
            queue.Enqueue(NewSlot(NewSkill("베어내기", Role.Warrior), warrior));
            queue.Enqueue(NewSlot(NewSkill("화염구", Role.Wizard), wizard));

            Drain(executor.Run(queue));

            Assert.That(log, Is.EqualTo(new[]
            {
                "Enter:Warrior", "Cutin:Warrior", "Exit:Warrior",
                "Enter:Wizard",  "Cutin:Wizard",  "Exit:Wizard",
                "Clear",
            }));
        }

        /// <summary>
        /// 같은 시전자가 이어져도 Exit가 다음 Enter보다 먼저다.
        ///
        /// 그래서 <b>무대 쪽이 실제 퇴장을 다음 Enter까지 미뤄야</b> 한 프레임 깜빡임이 없다 —
        /// 그 한 프레임에 <c>OnDisable → ReleaseBody</c>가 돌면 다음 슬롯이 통째로 날아간다.
        /// 이 테스트는 그 계약을 문서로 박아 둔다.
        /// </summary>
        [Test]
        public void SameCaster_TwoSlots_ExitPrecedesTheNextEnter()
        {
            Ally warrior = NewAlly("Warrior");

            var queue = new Queue<ComboSlot>();
            queue.Enqueue(NewSlot(NewSkill("베어내기", Role.Warrior), warrior));
            queue.Enqueue(NewSlot(NewSkill("올려베기", Role.Warrior), warrior));

            Drain(executor.Run(queue));

            Assert.That(log, Is.EqualTo(new[]
            {
                "Enter:Warrior", "Cutin:Warrior", "Exit:Warrior",
                "Enter:Warrior", "Cutin:Warrior", "Exit:Warrior",
                "Clear",
            }));
        }

        // ── 건너뛴 슬롯 ─────────────────────────────────

        /// <summary>발동하지 않을 슬롯에 시전자만 튀어나오면 유령이 된다.</summary>
        [Test]
        public void SlotWithoutCaster_NeverTouchesTheStage()
        {
            var queue = new Queue<ComboSlot>();
            queue.Enqueue(NewSlot(NewSkill("베어내기", Role.Warrior), null));

            Drain(executor.Run(queue));

            Assert.That(log, Is.EqualTo(new[] { "Clear" }), "건너뛴 슬롯은 무대를 안 쓴다");
        }

        [Test]
        public void SlotWithoutSkillData_NeverTouchesTheStage()
        {
            var queue = new Queue<ComboSlot>();
            queue.Enqueue(new ComboSlot { card = new ComboCard(null), caster = NewAlly("Warrior") });

            Drain(executor.Run(queue));

            Assert.That(log, Is.EqualTo(new[] { "Clear" }));
        }

        // ── 정리 ────────────────────────────────────────

        /// <summary>
        /// 무대를 안 비우면 불려 나온 시전자가 실시간 전투에 그대로 남고
        /// 조작 캐릭터는 숨은 채로 굳는다.
        /// </summary>
        [Test]
        public void RunFinished_ClearsTheStageExactlyOnce()
        {
            var queue = new Queue<ComboSlot>();
            queue.Enqueue(NewSlot(NewSkill("베어내기", Role.Warrior), NewAlly("Warrior")));

            Drain(executor.Run(queue));

            Assert.That(stage.ClearCount, Is.EqualTo(1));
            Assert.That(log[log.Count - 1], Is.EqualTo("Clear"), "정리가 마지막이어야 한다");
        }

        /// <summary>
        /// StopCoroutine으로 잘린 Run은 뒷정리가 안 돈다. Abort가 대신 비워야 한다 —
        /// <c>Cutin.Cancel</c>이 TimeControl을 되돌리는 것과 같은 이유다.
        /// </summary>
        [Test]
        public void Abort_ClearsTheStage()
        {
            executor.Abort();

            Assert.That(stage.ClearCount, Is.EqualTo(1));
        }

        /// <summary>무대를 안 꽂은 씬(테스트 씬 등)도 예전처럼 돌아야 한다.</summary>
        [Test]
        public void NoStageWired_RunsWithoutError()
        {
            executor.Stage = null;

            var queue = new Queue<ComboSlot>();
            queue.Enqueue(NewSlot(NewSkill("베어내기", Role.Warrior), NewAlly("Warrior")));

            Assert.DoesNotThrow(() => Drain(executor.Run(queue)));
            Assert.That(stage.ClearCount, Is.Zero);
        }

        // ── 헬퍼 ─────────────────────────────────────────

        /// <summary>호출을 순서대로 한 줄에 적는 스텁.</summary>
        private class FakeStage : ICasterStage
        {
            private readonly List<string> log;

            public int ClearCount;

            public FakeStage(List<string> log) => this.log = log;

            public void Enter(Ally caster) => log.Add("Enter:" + Name(caster));
            public void Exit(Ally caster) => log.Add("Exit:" + Name(caster));

            public void Clear()
            {
                ClearCount++;
                log.Add("Clear");
            }

            private static string Name(Ally a) => a != null ? a.name : "(null)";
        }

        /// <summary>같은 줄에 섞어 적어야 무대와의 순서가 보인다.</summary>
        private class FakeCutin : ISkillCutin
        {
            private readonly List<string> log;

            public FakeCutin(List<string> log) => this.log = log;

            public IEnumerator Play(Ally caster, SkillData data)
            {
                log.Add("Cutin:" + (caster != null ? caster.name : "(null)"));
                return Empty();
            }

            public void Cancel() { }

            private static IEnumerator Empty() { yield break; }
        }

        /// <summary>중첩 열거자는 펌프하지 않는다 — Run 본문의 진행만 본다.</summary>
        private static void Drain(IEnumerator routine)
        {
            int guard = 0;
            while (routine.MoveNext())
            {
                if (++guard > 1000)
                    Assert.Fail("Run 코루틴이 끝나지 않는다 — 무한 루프");
            }
        }

        private ComboSlot NewSlot(SkillData data, Ally caster)
            => new ComboSlot { card = new ComboCard(data), caster = caster, target = TargetInfo.None };

        private SkillData NewSkill(string skillName, Role role)
        {
            var data = ScriptableObject.CreateInstance<SkillData>();
            data.skillName = skillName;
            data.role = role;
            assets.Add(data);
            return data;
        }

        private Ally NewAlly(string name) => NewObject(name).AddComponent<Ally>();

        private GameObject NewObject(string name)
        {
            var go = new GameObject(name);
            spawned.Add(go);
            return go;
        }
    }
}
