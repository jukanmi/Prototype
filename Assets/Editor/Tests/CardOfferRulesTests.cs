using System.Collections.Generic;
using NUnit.Framework;
using Prototype;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Prototype.Tests
{
    /// <summary>
    /// 레벨업 선택지를 짜는 규칙.
    ///
    /// <b>난수를 주입받는 것 자체가 설계다.</b> 20% · 50%는 플레이로는 절대 검증할 수 없는 값이고,
    /// 경계(0.199 / 0.200)가 뒤집혀 있어도 몇 십 판을 돌려서는 알아채지 못한다.
    ///
    /// 난수 소비 순서는 <b>고르기 3번 → 황금 3번</b>이다. 아래 테스트가 전부 이 순서에 기댄다.
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
            List<CardOffer> offers = Build(Pool(), Owned(), tier: 0, Rolls(0f));

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

            List<CardOffer> offers = Build(pool, Owned(), tier: 0, Rolls(0f));

            Assert.That(offers.Count, Is.EqualTo(2));
            Assert.That(offers[0].data, Is.Not.EqualTo(offers[1].data));
        }

        [Test]
        public void Build_WithEmptyPool_GivesNothing()
        {
            Assert.That(Build(new List<SkillData>(), Owned(), 0, Rolls(0f)), Is.Empty);
            Assert.That(CardOfferRules.Build(null, Owned(), 0, Rolls(0f)), Is.Empty);
        }

        /// <summary>null과 중복이 섞인 표를 그대로 쓰면 빈 카드가 선택지에 뜬다.</summary>
        [Test]
        public void Build_IgnoresNullsAndDuplicatesInPool()
        {
            var pool = new List<SkillData> { tank, null, tank, warrior, null };

            List<CardOffer> offers = Build(pool, Owned(), 0, Rolls(0f));

            Assert.That(offers.Count, Is.EqualTo(2));
            foreach (CardOffer o in offers) Assert.That(o.data, Is.Not.Null);
        }

        // ── 황금 판정 ───────────────────────────────────

        [Test]
        public void Tier1_NeverRollsGolden()
        {
            // 황금 굴림이 전부 0이어도(가장 잘 나오는 값) 1단계는 특전이 없다.
            List<CardOffer> offers = Build(Pool(), Owned(), tier: 0, Rolls(0f));

            foreach (CardOffer o in offers)
                Assert.That(o.golden, Is.False, $"{o.data.skillName}");
        }

        /// <summary>
        /// 20% 경계. 0.199는 황금이고 0.200은 아니다 —
        /// <c>&lt;=</c>로 잘못 쓰면 확률이 조용히 커진다.
        /// </summary>
        [Test]
        public void Tier2_GoldenBoundaryIsTwentyPercent()
        {
            // 앞 3번은 카드 고르기, 뒤 3번이 황금 판정이다.
            var rolls = new Queue<float>(new[] { 0f, 0f, 0f, 0.199f, 0.200f, 0.999f });

            List<CardOffer> offers = Build(Pool(), Owned(), tier: 1, () => rolls.Dequeue());

            Assert.That(offers[0].golden, Is.True, "0.199 < 0.20");
            Assert.That(offers[1].golden, Is.False, "0.200은 경계 밖");
            Assert.That(offers[2].golden, Is.False);
        }

        [Test]
        public void Tier3_GoldenBoundaryIsFiftyPercent()
        {
            var rolls = new Queue<float>(new[] { 0f, 0f, 0f, 0.499f, 0.500f, 0.1f });

            List<CardOffer> offers = Build(Pool(), Owned(), tier: 2, () => rolls.Dequeue());

            Assert.That(offers[0].golden, Is.True);
            Assert.That(offers[1].golden, Is.False);
            Assert.That(offers[2].golden, Is.True);
        }

        /// <summary>
        /// 세 장이 <b>따로</b> 굴러야 한다. 선택지 전체에 한 번만 굴리면
        /// "셋 다 황금이거나 셋 다 일반"이 되어 고를 이유가 사라진다.
        /// </summary>
        [Test]
        public void GoldenIsRolledPerCard_NotOncePerOffer()
        {
            var rolls = new Queue<float>(new[] { 0f, 0f, 0f, 0.1f, 0.9f, 0.1f });

            List<CardOffer> offers = Build(Pool(), Owned(), tier: 1, () => rolls.Dequeue());

            Assert.That(offers[0].golden, Is.True);
            Assert.That(offers[1].golden, Is.False);
            Assert.That(offers[2].golden, Is.True);
        }

        // ── 합성 ────────────────────────────────────────

        /// <summary>일반 두 장을 들고 있으면 세 장째가 합성 자리로 뜬다.</summary>
        [Test]
        public void HoldingTwoNormalCopies_MarksThatOfferAsFusing()
        {
            var owned = Owned(Card(tank), Card(tank));

            List<CardOffer> offers = Build(new List<SkillData> { tank }, owned, tier: 0, Rolls(0.99f));

            Assert.That(offers[0].fuses, Is.True);
        }

        /// <summary>합성은 확률을 이긴다 — 1단계(황금 0%)에서도 결과는 황금이다.</summary>
        [Test]
        public void Fusing_ForcesGolden_EvenAtTier1()
        {
            var owned = Owned(Card(tank), Card(tank));

            List<CardOffer> offers = Build(new List<SkillData> { tank }, owned, tier: 0, Rolls(0.99f));

            Assert.That(offers[0].golden, Is.True);
        }

        [Test]
        public void HoldingOneOrThreeCopies_DoesNotFuse()
        {
            var pool = new List<SkillData> { tank };

            Assert.That(Build(pool, Owned(Card(tank)), 0, Rolls(0.99f))[0].fuses,
                Is.False, "한 장으로는 아직");

            Assert.That(Build(pool, Owned(Card(tank), Card(tank), Card(tank)), 0, Rolls(0.99f))[0].fuses,
                Is.False, "이미 세 장이면 합성 자리가 아니다");
        }

        /// <summary>황금 위의 등급이 없다. 황금 두 장은 합성 재료가 되지 않는다.</summary>
        [Test]
        public void GoldenCopies_DoNotCountTowardFusing()
        {
            var owned = Owned(Golden(tank), Golden(tank));

            List<CardOffer> offers = Build(new List<SkillData> { tank }, owned, tier: 0, Rolls(0.99f));

            Assert.That(offers[0].fuses, Is.False);
            Assert.That(CardOfferRules.CountNormalCopies(owned, tank), Is.Zero);
        }

        [Test]
        public void CountNormalCopies_CountsOnlyMatchingNonGolden()
        {
            var owned = Owned(Card(tank), Golden(tank), Card(warrior), Card(tank));

            Assert.That(CardOfferRules.CountNormalCopies(owned, tank), Is.EqualTo(2));
            Assert.That(CardOfferRules.CountNormalCopies(owned, warrior), Is.EqualTo(1));
            Assert.That(CardOfferRules.CountNormalCopies(owned, archer), Is.Zero);
            Assert.That(CardOfferRules.CountNormalCopies(null, tank), Is.Zero);
            Assert.That(CardOfferRules.CountNormalCopies(owned, null), Is.Zero);
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
            List<CardOffer> offers = Build(Pool(), Owned(), tier: 2, Rolls(0f));

            Assert.That(offers.Count, Is.EqualTo(CardOfferRules.OfferCount));
            foreach (CardOffer o in offers) Assert.That(o.fuses, Is.False, "빈 덱에서는 합성이 설 수 없다");
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

        private static List<CardOffer> Build(IReadOnlyList<SkillData> pool, IReadOnlyList<ComboCard> owned,
                                             int tier, System.Func<float> roll)
            => CardOfferRules.Build(pool, owned, tier, roll);

        /// <summary>항상 같은 값을 주는 난수원.</summary>
        private static System.Func<float> Rolls(float value) => () => value;

        private List<SkillData> Pool() => new List<SkillData> { tank, warrior, archer, wizard };

        private static List<ComboCard> Owned(params ComboCard[] cards) => new List<ComboCard>(cards);

        private static ComboCard Card(SkillData data) => new ComboCard(data);
        private static ComboCard Golden(SkillData data) => new ComboCard(data, 1f, true);

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
