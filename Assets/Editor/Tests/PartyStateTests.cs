using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Prototype.Tests
{
    /// <summary>
    /// 스테이지를 넘어가는 파티의 몸 상태. <b>동료의 사망은 런 끝까지 영구</b>라는
    /// 규칙이 여기 전부 들어 있다.
    ///
    /// 죽은 동료를 실제 <see cref="Combat"/>로 만들 수는 없다 — 에디트모드에서는
    /// <c>Awake</c>가 안 돌아 <c>Die()</c>가 물리에서 터진다. 그래서
    /// <see cref="PartyState.Record"/>로 직접 적는다.
    /// </summary>
    public class PartyStateTests
    {
        private readonly List<Object> spawned = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (Object o in spawned) Object.DestroyImmediate(o);
            spawned.Clear();
        }

        private PartyMemberData Member(string id)
        {
            var m = ScriptableObject.CreateInstance<PartyMemberData>();
            m.memberId = id;
            m.displayName = id;
            spawned.Add(m);
            return m;
        }

        // ── 기본값 ──────────────────────────────────────

        /// <summary>
        /// 기록이 없으면 살아 있고 만피다. 첫 스테이지가 이 상태이므로
        /// 여기서 null이나 0이 나오면 게임이 시작하자마자 이상해진다.
        /// </summary>
        [Test]
        public void NoRecord_IsAliveAndFullHealth()
        {
            var state = new PartyState();
            PartyMemberData tan = Member("Tan");

            Assert.That(state.IsDead(tan), Is.False);
            Assert.That(state.HpRatioOf(tan), Is.EqualTo(1f));
            Assert.That(state.HasSnapshot, Is.False);
        }

        [Test]
        public void NullMember_IsHandled()
        {
            var state = new PartyState();

            Assert.That(state.IsDead(null), Is.False);
            Assert.That(state.HpRatioOf(null), Is.EqualTo(1f));
            Assert.DoesNotThrow(() => state.Record(null, 0.5f, true));
        }

        // ── 체력 ────────────────────────────────────────

        [Test]
        public void Record_CarriesHealthRatio()
        {
            var state = new PartyState();
            PartyMemberData war = Member("War");

            state.Record(war, 0.42f, dead: false);

            Assert.That(state.HpRatioOf(war), Is.EqualTo(0.42f).Within(0.0001f));
            Assert.That(state.IsDead(war), Is.False);
        }

        /// <summary>
        /// 산 동료를 0으로 복원하면 체력은 비었는데 <see cref="CombatState"/>는 살아 있는,
        /// 아무도 못 죽이는 몸이 된다. 최소값으로 물린다.
        /// </summary>
        [Test]
        public void Record_LivingMemberNeverDropsToZero()
        {
            var state = new PartyState();
            PartyMemberData arc = Member("Arc");

            state.Record(arc, 0f, dead: false);

            Assert.That(state.HpRatioOf(arc), Is.GreaterThan(0f));
        }

        [Test]
        public void Record_ClampsAboveOne()
        {
            var state = new PartyState();
            PartyMemberData wiz = Member("Wiz");

            state.Record(wiz, 5f, dead: false);

            Assert.That(state.HpRatioOf(wiz), Is.EqualTo(1f));
        }

        // ── 영구 사망 ───────────────────────────────────

        [Test]
        public void Record_DeadMember_IsMarked()
        {
            var state = new PartyState();
            PartyMemberData tan = Member("Tan");

            state.Record(tan, 0.5f, dead: true);

            Assert.That(state.IsDead(tan), Is.True);
            Assert.That(state.HpRatioOf(tan), Is.EqualTo(0f));
            Assert.That(state.DeadCount, Is.EqualTo(1));
        }

        /// <summary>
        /// <b>A안(영구 사망)의 핵심 불변식.</b> 한 번 죽은 동료는 이후 어떤 기록으로도
        /// 되살아나지 않는다. 이게 뚫리면 다음 스테이지에서 시체가 만피로 걸어 나온다.
        /// </summary>
        [Test]
        public void Death_IsIrreversible()
        {
            var state = new PartyState();
            PartyMemberData war = Member("War");

            state.Record(war, 0.5f, dead: true);
            state.Record(war, 1f, dead: false);   // 되살리려는 시도

            Assert.That(state.IsDead(war), Is.True, "죽은 동료가 되살아났다 — 영구 사망이 아니다.");
            Assert.That(state.HpRatioOf(war), Is.EqualTo(0f));
        }

        [Test]
        public void DeadCount_CountsOnlyDead()
        {
            var state = new PartyState();

            state.Record(Member("Tan"), 0.3f, dead: true);
            state.Record(Member("War"), 0.9f, dead: false);
            state.Record(Member("Arc"), 0.1f, dead: true);

            Assert.That(state.DeadCount, Is.EqualTo(2));
        }

        // ── 새 런 ───────────────────────────────────────

        /// <summary>
        /// <c>RunProgression.Reset</c>이 이걸 안 부르면 새 런이 지난 런의 시체를 물고 시작한다.
        /// 증상은 "새로 시작했는데 동료가 두 명뿐"이고, 원인이 전혀 안 보인다.
        /// </summary>
        [Test]
        public void Clear_WipesEverything()
        {
            var state = new PartyState();
            PartyMemberData tan = Member("Tan");

            state.Record(tan, 0.2f, dead: true);
            state.Clear();

            Assert.That(state.IsDead(tan), Is.False);
            Assert.That(state.HpRatioOf(tan), Is.EqualTo(1f));
            Assert.That(state.DeadCount, Is.EqualTo(0));
            Assert.That(state.HeroHpRatio, Is.EqualTo(1f));
            Assert.That(state.HasSnapshot, Is.False);
        }

        [Test]
        public void RunProgression_Reset_ClearsPartyState()
        {
            var run = new RunProgression();
            PartyMemberData tan = Member("Tan");

            run.Party.Record(tan, 0.2f, dead: true);
            run.Reset();

            Assert.That(run.Party.IsDead(tan), Is.False,
                "RunProgression.Reset 이 파티 상태를 안 비운다.");
        }

        // ── 런 덱 정리 ──────────────────────────────────

        /// <summary>
        /// 동료가 죽으면 그 직업 카드는 <b>런 덱에서도</b> 빠져야 한다.
        /// 이 판의 덱만 비우면 다음 스테이지 <c>BuildDeck</c>이 런 덱을 다시 읽어 되살린다.
        /// </summary>
        [Test]
        public void PurgeRole_RemovesOnlyThatRoleFromRunDeck()
        {
            var run = new RunProgression();

            run.SeedDeck(new[]
            {
                Card(Role.Tanker), Card(Role.Tanker), Card(Role.Archer), Card(Role.Wizard),
            });

            int removed = run.PurgeRole(Role.Tanker);

            Assert.That(removed, Is.EqualTo(2));
            Assert.That(run.Cards.Count, Is.EqualTo(2));

            foreach (ComboCard c in run.Cards)
                Assert.That(c.Data.role, Is.Not.EqualTo(Role.Tanker));
        }

        private ComboCard Card(Role role)
        {
            var s = ScriptableObject.CreateInstance<SkillData>();
            s.role = role;
            s.skillName = role + "_테스트";
            spawned.Add(s);

            return new ComboCard(s);
        }
    }
}
