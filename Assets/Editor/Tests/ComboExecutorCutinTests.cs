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

        /// <summary>
        /// Important 1 — 재타겟 실패 시 유령 컷인 방지. 대상이 죽었고 BattleRegistry에 살아 있는
        /// 적이 하나도 없으면, 재타겟은 컷인보다 먼저(Run 루프에서) 실패해야 한다.
        /// 컷인이 뜬 뒤에 취소되면 화면만 0.55초 얼렸다가 아무 일도 없이 넘어가는
        /// "유령 연출"이 된다 — 그게 이 테스트가 막으려는 것이다.
        /// </summary>
        [Test]
        public void Retarget_FailsWithNoLivingEnemies_SkipsCutinButConsumesCard()
        {
            SkillData skill = NewSkill("베어내기", Role.Warrior);
            Ally warrior = NewAlly("Warrior");

            // Enemy.Start가 채우는 BattleRegistry 등록은 EditMode에서 돌지 않는다
            // (EnemyStateLabelTests 참고) — 즉 아무것도 등록하지 않아도 이미 "적 없음" 상태다.
            // 그래도 죽은 적을 대상으로 세워 재타겟 분기를 실제로 타게 한다.
            GameObject deadEnemyGo = NewObject("DeadEnemy");
            Enemy deadEnemy = deadEnemyGo.AddComponent<Enemy>();
            deadEnemy.Combat.TakeDamage(new DamageData(9999f));
            Assert.That(deadEnemy.Combat.IsDead, Is.True, "선행 조건: 대상이 죽어 있어야 재타겟이 시작된다");

            var consumed = new List<ComboCard>();
            executor.OnSlotConsumed += c => consumed.Add(c);

            ComboSlot slot = new ComboSlot
            {
                card = new ComboCard(skill),
                caster = warrior,
                target = TargetInfo.Unit(deadEnemy),
            };
            var queue = new Queue<ComboSlot>();
            queue.Enqueue(slot);

            Drain(executor.Run(queue));

            Assert.That(cutin.Calls, Is.Empty, "재타겟에 실패한 슬롯은 컷인을 띄우면 안 된다");
            Assert.That(consumed, Has.Count.EqualTo(1), "발동 못 한 카드도 버린 더미로 가야 덱이 마르지 않는다");
            Assert.That(consumed[0], Is.SameAs(slot.card));
        }

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
