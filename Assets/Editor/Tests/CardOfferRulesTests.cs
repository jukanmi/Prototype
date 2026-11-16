using System.Collections.Generic;
using NUnit.Framework;
using Prototype;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Prototype.Tests
{
    /// <summary>
    /// 카드 선택지를 짜는 규칙. 난수를 주입받는다.
    /// </summary>
    public class CardOfferRulesTests
    {
        private readonly List<Object> assets = new List<Object>();

        private SkillData tank, warrior, archer, wizard;

        [SetUp]
        public void SetUp()
        {
            tank = NewSkill("방패연타", Role.Tanker);
            warrior = NewSkill("회전강타", Role.Warrior);
            archer = NewSkill("연속사격", Role.Archer);
            wizard = NewSkill("충격파", Role.Wizard);
        }

        [TearDown]
        public void TearDown()
        {
            for (int i = 0; i < assets.Count; i++) Object.DestroyImmediate(assets[i]);
            assets.Clear();
        }

        // ── 형태 ────────────────────────────────────────

        [Test]
        public void Build_GivesThreeDistinctCards()
        {
            List<CardOffer> offers = Build(Pool(), Rolls(0f));

            Assert.That(offers.Count, Is.EqualTo(CardOfferRules.OfferCount));

            var seen = new HashSet<SkillData>();
            foreach (CardOffer o in offers)
                Assert.That(seen.Add(o.data), Is.True, $"{o.data.skillName}가 두 번 나왔다");
        }

        /// <summary>모집단이 3종보다 적으면 있는 만큼만 낸다. 같은 카드로 채우면 안 된다.</summary>
        [Test]
        public void Build_WithSmallPool_GivesWhatItHas()
        {
            var pool = new List<SkillData> { tank, warrior };

            List<CardOffer> offers = Build(pool, Rolls(0f));

            Assert.That(offers.Count, Is.EqualTo(2));
            Assert.That(offers[0].data, Is.Not.EqualTo(offers[1].data));
        }

        [Test]
        public void Build_WithEmptyPool_GivesNothing()
        {
            Assert.That(Build(new List<SkillData>(), Rolls(0f)), Is.Empty);
            Assert.That(CardOfferRules.Build(null, Rolls(0f)), Is.Empty);
        }

        /// <summary>null과 중복이 섞인 표를 그대로 쓰면 빈 카드가 선택지에 뜬다.</summary>
        [Test]
        public void Build_IgnoresNullsAndDuplicatesInPool()
        {
            var pool = new List<SkillData> { tank, null, tank, warrior, null };

            List<CardOffer> offers = Build(pool, Rolls(0f));

            Assert.That(offers.Count, Is.EqualTo(2));
            foreach (CardOffer o in offers) Assert.That(o.data, Is.Not.Null);
        }

        // ── 모집단 ──────────────────────────────────────

        /// <summary>
        /// <b>테스트 모드(0장 시작)의 핵심 계약.</b> 모집단이 지금 든 카드가 아니라 파티에서
        /// 오므로, 덱이 비어 있어도 선택지가 정상으로 나와야 한다.
        /// 여기가 무너지면 "카드가 없어서 레벨업하는데 카드가 없어서 못 받는" 상태가 된다.
        /// </summary>
        [Test]
        public void EmptyDeck_StillGetsAFullOffer()
        {
            List<CardOffer> offers = Build(Pool(), Rolls(0f));

            Assert.That(offers.Count, Is.EqualTo(CardOfferRules.OfferCount));
        }

        /// <summary>
        /// 모집단은 <see cref="SkillCatalog.Pool"/>이 직업으로 거른다. 시전할 동료가 없는 카드는
        /// 뽑아 줘도 손패 맨 앞을 막을 뿐이다.
        /// </summary>
        [Test]
        public void Pool_KeepsOnlySkillsThePartyCanCast()
        {
            var everything = new List<ComboCard> { Card(tank), Card(warrior), Card(archer), Card(wizard) };
            var roles = new List<Role> { Role.Tanker, Role.Archer };

            List<SkillData> pool = SkillCatalog.Pool(roles, everything);

            Assert.That(pool, Has.Member(tank));
            Assert.That(pool, Has.Member(archer));
            Assert.That(pool, Has.No.Member(warrior), "전사 동료가 없으면 전사 카드도 없다");
            Assert.That(pool, Has.No.Member(wizard));
        }

        /// <summary>
        /// 직업 목록이 비면 <b>거르지 않는다</b>. 조용히 0장을 돌려주면
        /// 레벨업 화면이 이유 없이 비어 원인을 찾을 길이 없다.
        /// </summary>
        [Test]
        public void Pool_WithNoRoles_FiltersNothing()
        {
            var everything = new List<ComboCard> { Card(tank), Card(wizard) };

            Assert.That(SkillCatalog.Pool(null, everything).Count, Is.EqualTo(2));
            Assert.That(SkillCatalog.Pool(new List<Role>(), everything).Count, Is.EqualTo(2));
        }

        // ── 도구 ────────────────────────────────────────

        private static List<CardOffer> Build(IReadOnlyList<SkillData> pool, System.Func<float> roll)
            => CardOfferRules.Build(pool, roll);

        /// <summary>항상 같은 값을 주는 난수원.</summary>
        private static System.Func<float> Rolls(float value) => () => value;

        private List<SkillData> Pool() => new List<SkillData> { tank, warrior, archer, wizard };

        private static ComboCard Card(SkillData data) => new ComboCard(data);

        private SkillData NewSkill(string name, Role role)
        {
            var data = ScriptableObject.CreateInstance<SkillData>();
            assets.Add(data);

            data.skillName = name;
            data.role = role;
            data.attackType = AttackType.Strike;
            return data;
        }
    }
}
