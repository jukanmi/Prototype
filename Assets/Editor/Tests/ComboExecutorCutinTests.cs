using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Prototype.Tests
{
    /// <summary>
    /// ComboExecutor가 슬롯마다 컷인을 앞세우는지 검증한다.
    ///
    /// Run 코루틴을 Unity 스케줄러 없이 손으로 펌프한다. 손 펌프는 중첩 열거자
    /// (yield return IEnumerator)를 대신 돌려주지 않으므로 RunSlot 본문은 실행되지 않는다 —
    /// 덕분에 StateMachine · SkillState · VFX를 세우지 않고도 컷인 호출만 깨끗이 관측된다.
    /// 그래서 ISkillCutin.Play는 "호출 즉시 기록"되어야 한다(FakeCutin이 그렇게 만들어져 있다).
    /// </summary>
    public class ComboExecutorCutinTests
    {
        private readonly List<GameObject> spawned = new List<GameObject>();
        private readonly List<Object> assets = new List<Object>();

        private ComboExecutor executor;
        private FakeCutin cutin;

        [SetUp]
        public void SetUp()
        {
            // BattleRegistry는 static이라 이전 테스트(파일)가 등록만 하고 안 지운 적이 있으면
            // 재타겟 결과가 오염된다. 여기서 비운 상태로 시작한다.
            BattleRegistry.Clear();

            executor = NewObject("ComboExecutor").AddComponent<ComboExecutor>();
            cutin = new FakeCutin();
            executor.Cutin = cutin;
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
            cutin = null;
        }

        [Test]
        public void EverySlot_PlaysOneCutin_InQueueOrder()
        {
            SkillData a = NewSkill("베어내기", Role.Warrior);
            SkillData b = NewSkill("화염구", Role.Wizard);
            Ally warrior = NewAlly("Warrior");
            Ally wizard = NewAlly("Wizard");

            var queue = new Queue<ComboSlot>();
            queue.Enqueue(NewSlot(a, warrior));
            queue.Enqueue(NewSlot(b, wizard));

            Drain(executor.Run(queue));

            Assert.That(cutin.Calls.Count, Is.EqualTo(2));
            Assert.That(cutin.Calls[0].caster, Is.SameAs(warrior));
            Assert.That(cutin.Calls[0].data, Is.SameAs(a));
            Assert.That(cutin.Calls[1].caster, Is.SameAs(wizard));
            Assert.That(cutin.Calls[1].data, Is.SameAs(b));
        }

        [Test]
        public void Cutin_PlaysBeforeCasterEntersSkillState()
        {
            SkillData skill = NewSkill("베어내기", Role.Warrior);
            Ally warrior = NewAlly("Warrior");

            var queue = new Queue<ComboSlot>();
            queue.Enqueue(NewSlot(skill, warrior));

            IEnumerator run = executor.Run(queue);

            // 첫 양보 지점이 곧 컷인이다. 아직 스킬 상태로 넘어가지 않았어야 한다.
            // AddComponent<Ally>()는 대상이 활성 오브젝트면 Awake를 즉시(동기로) 돌린다
            // (EnemyStateTintTests의 NewRootRendererEnemy 주석 참고 — Enemy.Awake도 같은 방식으로 확인됨).
            // Entity.Awake가 StateMachine을 그 자리에서 만들어 넣으므로 이 시점에 StateMachine은
            // 항상 non-null이다 — null 단락 없이 곧바로 단언한다.
            Assert.That(run.MoveNext(), Is.True);
            Assert.That(cutin.Calls.Count, Is.EqualTo(1));
            Assert.That(warrior.StateMachine.CurState is SkillState, Is.False,
                        "컷인이 끝나기 전에 스킬 상태로 들어가면 안 된다");

            Drain(run);
        }

        [Test]
        public void NoCutinWired_RunsWithoutError()
        {
            executor.Cutin = null;

            SkillData skill = NewSkill("베어내기", Role.Warrior);
            var queue = new Queue<ComboSlot>();
            queue.Enqueue(NewSlot(skill, NewAlly("Warrior")));

            Assert.DoesNotThrow(() => Drain(executor.Run(queue)));
            Assert.That(cutin.Calls, Is.Empty);
        }

        [Test]
        public void SlotWithoutSkillData_SkipsCutin()
        {
            var queue = new Queue<ComboSlot>();
            queue.Enqueue(new ComboSlot { card = new ComboCard(null), caster = NewAlly("Warrior") });

            Drain(executor.Run(queue));

            Assert.That(cutin.Calls, Is.Empty);
        }

        [Test]
        public void SlotWithoutCaster_SkipsCutin()
        {
            var queue = new Queue<ComboSlot>();
            queue.Enqueue(NewSlot(NewSkill("베어내기", Role.Warrior), null));

            Drain(executor.Run(queue));

            Assert.That(cutin.Calls, Is.Empty);
        }

        [Test]
        public void SkippedSlot_StillGoesToDiscard()
        {
            var consumed = new List<ComboCard>();
            executor.OnSlotConsumed += c => consumed.Add(c);

            ComboSlot slot = NewSlot(NewSkill("베어내기", Role.Warrior), null);
            var queue = new Queue<ComboSlot>();
            queue.Enqueue(slot);

            Drain(executor.Run(queue));

            Assert.That(consumed, Has.Count.EqualTo(1), "발동 못 한 카드도 버린 더미로 가야 덱이 마르지 않는다");
            Assert.That(consumed[0], Is.SameAs(slot.card));
        }

        // 재타겟 실패 → 유령 컷인 방지 테스트는 여기 있었다.
        // 조준이 좌표만 남기도록 바뀌면서(TargetInfo.Unit 제거) 재타겟 경로 자체가 사라져 함께 걷어냈다.
        // 죽은 대상은 이제 시전 순간 SkillState.ResolveTarget이 좌표에서 다시 고른다.

        [Test]
        public void Abort_CancelsCutin()
        {
            executor.Abort();

            Assert.That(cutin.CancelCount, Is.EqualTo(1));
        }

        [Test]
        public void CanRunSlot_RejectsMissingPieces()
        {
            SkillData skill = NewSkill("베어내기", Role.Warrior);
            Ally warrior = NewAlly("Warrior");

            Assert.That(ComboExecutor.CanRunSlot(NewSlot(skill, warrior)), Is.True);
            Assert.That(ComboExecutor.CanRunSlot(NewSlot(skill, null)), Is.False);
            Assert.That(ComboExecutor.CanRunSlot(new ComboSlot { card = new ComboCard(null), caster = warrior }),
                        Is.False);
        }

        // ── 헬퍼 ─────────────────────────────────────────

        /// <summary>호출만 기록하는 스텁. Play가 이터레이터가 아니라서 호출 즉시 기록된다.</summary>
        private class FakeCutin : ISkillCutin
        {
            public readonly List<(Ally caster, SkillData data)> Calls = new List<(Ally, SkillData)>();
            public int CancelCount;

            public IEnumerator Play(Ally caster, SkillData data)
            {
                Calls.Add((caster, data));
                return Empty();
            }

            public void Cancel() => CancelCount++;

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
