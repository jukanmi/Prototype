using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Prototype.Tests
{
    /// <summary>
    /// 골드 · 비전투 칸 · 이벤트 선택지의 규칙. 씬 없이 도는 순수 판단만 본다.
    /// 창이 제대로 그려지는지는 씬을 재생해서 눈으로 본다.
    /// </summary>
    public class NodeRulesTests
    {
        private readonly List<Object> spawned = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (Object o in spawned) Object.DestroyImmediate(o);
            spawned.Clear();
        }

        private PartyMemberData Member(string id, Role role)
        {
            var m = ScriptableObject.CreateInstance<PartyMemberData>();
            m.memberId = id;
            m.role = role;
            spawned.Add(m);
            return m;
        }

        private SkillData Skill(Role role)
        {
            var s = ScriptableObject.CreateInstance<SkillData>();
            s.role = role;
            s.skillName = role + "_테스트";
            spawned.Add(s);
            return s;
        }

        // ── 골드 수치 ───────────────────────────────────

        private const MapNodeKind Battle = MapNodeKind.Battle;
        private const MapNodeKind Elite = MapNodeKind.Elite;

        [Test]
        public void StageClearReward_GrowsTenPerFloor()
        {
            Assert.That(GoldRules.StageClearReward(1, Battle), Is.EqualTo(40));
            Assert.That(GoldRules.StageClearReward(2, Battle), Is.EqualTo(50));
            Assert.That(GoldRules.StageClearReward(5, Battle), Is.EqualTo(80));
        }

        [Test]
        public void StageClearReward_BelowOne_IsFloorOne()
        {
            Assert.That(GoldRules.StageClearReward(0, Battle), Is.EqualTo(GoldRules.StageClearReward(1, Battle)));
            Assert.That(GoldRules.StageClearReward(-3, Battle), Is.EqualTo(GoldRules.StageClearReward(1, Battle)));
        }

        /// <summary>1층 보상만으로는 카드 한 장을 못 산다 — 규칙 주석이 약속한 값이다.</summary>
        [Test]
        public void FirstClearReward_CannotBuyACard()
        {
            Assert.That(GoldRules.StageClearReward(1, Battle), Is.LessThan(GoldRules.CardPrice));
        }

        // ── 정예 배율 ───────────────────────────────────

        [Test]
        public void EliteReward_IsOneAndAHalfTimes()
        {
            Assert.That(GoldRules.StageClearReward(2, Elite), Is.EqualTo(75));
            Assert.That(GoldRules.StageClearReward(3, Elite), Is.EqualTo(90));
            Assert.That(GoldRules.StageClearReward(4, Elite), Is.EqualTo(105));
        }

        /// <summary>정예만 배율이 붙는다. 보스 · 비전투 칸은 일반과 같다.</summary>
        [TestCase(MapNodeKind.Battle)]
        [TestCase(MapNodeKind.Boss)]
        [TestCase(MapNodeKind.Rest)]
        [TestCase(MapNodeKind.Shop)]
        [TestCase(MapNodeKind.Event)]
        public void NonEliteKinds_HaveNoBonus(MapNodeKind kind)
        {
            Assert.That(GoldRules.ClearRewardPercent(kind), Is.EqualTo(100));
            Assert.That(GoldRules.StageClearReward(3, kind), Is.EqualTo(60));
        }

        /// <summary>
        /// 정예의 대가가 의미가 있는가 — 같은 층 일반 전투보다 카드 반 장 값 이상을 더 줘야 "골라 볼 만한 칸"이다.
        /// 배율을 낮추면 이 테스트가 먼저 말해 준다.
        /// </summary>
        [Test]
        public void EliteBonus_IsWorthAtLeastHalfACard()
        {
            for (int floor = 2; floor <= 4; floor++)
            {
                int bonus = GoldRules.StageClearReward(floor, Elite) - GoldRules.StageClearReward(floor, Battle);
                Assert.That(bonus, Is.GreaterThanOrEqualTo(GoldRules.CardPrice / 2), $"{floor}층");
            }
        }

        /// <summary>기본 보상이 10 단위라 150%가 소수 없이 떨어진다. 배율을 바꿔 잘림이 생기면 여기서 드러난다.</summary>
        [Test]
        public void EliteReward_HasNoTruncation()
        {
            for (int floor = 1; floor <= 9; floor++)
            {
                int baseReward = GoldRules.StageClearReward(floor, Battle);
                Assert.That(baseReward * GoldRules.EliteClearRewardPercent % 100, Is.EqualTo(0), $"{floor}층");
            }
        }

        [Test]
        public void PriceOf_GoldenCostsMore()
        {
            var normal = new CardOffer { data = Skill(Role.Warrior) };
            var golden = new CardOffer { data = normal.data, golden = true };

            Assert.That(GoldRules.PriceOf(normal), Is.EqualTo(GoldRules.CardPrice));
            Assert.That(GoldRules.PriceOf(golden), Is.EqualTo(GoldRules.GoldenCardPrice));
        }

        // ── 런 골드 ─────────────────────────────────────

        [Test]
        public void TrySpendGold_Short_ChangesNothing()
        {
            var run = new RunProgression();
            run.AddGold(30);

            Assert.That(run.TrySpendGold(50), Is.False);
            Assert.That(run.Gold, Is.EqualTo(30));
        }

        [Test]
        public void TrySpendGold_Exact_EmptiesPurse()
        {
            var run = new RunProgression();
            run.AddGold(50);

            Assert.That(run.TrySpendGold(50), Is.True);
            Assert.That(run.Gold, Is.EqualTo(0));
        }

        [Test]
        public void AddGold_IgnoresNonPositive_AndSpendRejectsNegative()
        {
            var run = new RunProgression();
            run.AddGold(-10);
            run.AddGold(0);

            Assert.That(run.Gold, Is.EqualTo(0));
            Assert.That(run.TrySpendGold(-5), Is.False);
            Assert.That(run.Gold, Is.EqualTo(0));
        }

        [Test]
        public void Reset_ClearsGold()
        {
            var run = new RunProgression();
            run.AddGold(120);
            run.Reset();

            Assert.That(run.Gold, Is.EqualTo(0));
        }

        // ── 상점이 거르는 직업 ──────────────────────────

        [Test]
        public void AvailableRoles_SkipsDeadAndDuplicates()
        {
            PartyMemberData tank = Member("Tan", Role.Tanker);
            PartyMemberData war1 = Member("War1", Role.Warrior);
            PartyMemberData war2 = Member("War2", Role.Warrior);
            PartyMemberData wiz = Member("Wiz", Role.Wizard);

            var state = new PartyState();
            state.Record(wiz, 0f, dead: true);

            List<Role> roles = NodeRules.AvailableRoles(new[] { tank, null, war1, war2, wiz }, state);

            Assert.That(roles, Is.EquivalentTo(new[] { Role.Tanker, Role.Warrior }));
        }

        [Test]
        public void AvailableRoles_NullRoster_IsEmpty()
        {
            Assert.That(NodeRules.AvailableRoles(null, new PartyState()), Is.Empty);
        }

        // ── 몸 없는 체력 증감 ───────────────────────────

        [Test]
        public void ChangeHp_ReachesUnrecordedMembers()
        {
            PartyMemberData war = Member("War", Role.Warrior);
            var state = new PartyState();

            state.ChangeHp(-0.25f, new[] { war });

            Assert.That(state.HpRatioOf(war), Is.EqualTo(0.75f).Within(1e-4f));
            Assert.That(state.HeroHpRatio, Is.EqualTo(0.75f).Within(1e-4f));
            Assert.That(state.HasSnapshot, Is.True, "안 켜면 다음 전투의 PartyAssembler가 기록을 무시한다");
        }

        [Test]
        public void ChangeHp_CapsAtFull_AndNeverKills()
        {
            PartyMemberData war = Member("War", Role.Warrior);
            var state = new PartyState();
            state.Record(war, 0.9f, dead: false);

            state.ChangeHp(0.3f, new[] { war });
            Assert.That(state.HpRatioOf(war), Is.EqualTo(1f));

            state.ChangeHp(-5f, new[] { war });
            Assert.That(state.IsDead(war), Is.False);
            Assert.That(state.HpRatioOf(war), Is.GreaterThan(0f));
            Assert.That(state.HeroHpRatio, Is.GreaterThan(0f));
        }

        [Test]
        public void ChangeHp_DoesNotReviveTheDead()
        {
            PartyMemberData wiz = Member("Wiz", Role.Wizard);
            var state = new PartyState();
            state.Record(wiz, 0f, dead: true);

            state.ChangeHp(NodeRules.RestHealRatio, new[] { wiz });

            Assert.That(state.IsDead(wiz), Is.True);
            Assert.That(state.HpRatioOf(wiz), Is.EqualTo(0f));
        }

        // ── 이벤트 선택지 ───────────────────────────────

        [Test]
        public void CanChoose_CostNeedsGold_GainNeedsNothing()
        {
            var pay = new RunEventChoice { goldDelta = -50 };
            var gain = new RunEventChoice { goldDelta = 40 };

            Assert.That(RunEventRules.CanChoose(in pay, 49), Is.False);
            Assert.That(RunEventRules.CanChoose(in pay, 50), Is.True);
            Assert.That(RunEventRules.CanChoose(in gain, 0), Is.True);
        }

        [Test]
        public void Apply_Refused_ChangesNothing()
        {
            var run = new RunProgression();
            run.AddGold(10);
            var choice = new RunEventChoice { goldDelta = -30, hpDelta = 0.5f, grantCard = true };

            bool ok = RunEventRules.Apply(in choice, run, null, new[] { Skill(Role.Warrior) }, () => 0f,
                                          out ComboCard card);

            Assert.That(ok, Is.False);
            Assert.That(card, Is.Null);
            Assert.That(run.Gold, Is.EqualTo(10));
            Assert.That(run.Cards, Is.Empty);
            Assert.That(run.Party.HasSnapshot, Is.False);
        }

        [Test]
        public void Apply_PaysHurtsAndGrantsTogether()
        {
            var run = new RunProgression();
            run.AddGold(80);
            SkillData skill = Skill(Role.Warrior);
            var choice = new RunEventChoice { goldDelta = -30, hpDelta = -0.1f, grantCard = true };

            bool ok = RunEventRules.Apply(in choice, run, null, new[] { skill }, () => 0f, out ComboCard card);

            Assert.That(ok, Is.True);
            Assert.That(run.Gold, Is.EqualTo(50));
            Assert.That(run.Party.HeroHpRatio, Is.EqualTo(0.9f).Within(1e-4f));
            Assert.That(card, Is.Not.Null);
            Assert.That(card.Data, Is.SameAs(skill));
            Assert.That(run.Cards, Has.Member(card));
        }

        /// <summary>표 배선이 빠져 뽑을 카드가 없어도 골드 · 체력은 적용된다.</summary>
        [Test]
        public void Apply_EmptyPool_StillAppliesTheRest()
        {
            var run = new RunProgression();
            var choice = new RunEventChoice { goldDelta = 25, grantCard = true };

            bool ok = RunEventRules.Apply(in choice, run, null, new SkillData[0], () => 0f, out ComboCard card);

            Assert.That(ok, Is.True);
            Assert.That(card, Is.Null);
            Assert.That(run.Gold, Is.EqualTo(25));
        }

        [Test]
        public void Summary_ListsOnlyWhatChanges()
        {
            Assert.That(RunEventRules.Summary(new RunEventChoice()), Is.EqualTo("아무 일도 없었다"));
            Assert.That(RunEventRules.Summary(new RunEventChoice { goldDelta = 40 }), Is.EqualTo("골드 +40"));
            Assert.That(RunEventRules.Summary(new RunEventChoice { goldDelta = -20, grantCard = true }),
                        Is.EqualTo("골드 -20 · 카드 1장"));
        }
    }
}
