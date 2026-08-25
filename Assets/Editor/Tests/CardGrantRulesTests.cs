using System.Collections.Generic;
using NUnit.Framework;
using Prototype;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Prototype.Tests
{
    /// <summary>
    /// 고른 카드를 덱에 넣는 규칙, 그리고 황금 카드의 데미지 배율.
    ///
    /// <b>합성이 덱을 줄인다</b>는 것이 여기서 가장 중요한 항목이다. 늘어나기만 하면
    /// 덱이 불어나 원하는 카드가 손에 안 잡히고, 성장할수록 손패가 무작위해진다 —
    /// 같은 카드가 겹치는 것에 출구를 주는 게 합성의 존재 이유다.
    /// </summary>
    public class CardGrantRulesTests
    {
        private readonly List<Object> assets = new List<Object>();

        private SkillData strike;
        private SkillData launcher;

        [SetUp]
        public void SetUp()
        {
            strike = NewSkill("회전강타", AttackType.Strike);
            launcher = NewSkill("올려베기", AttackType.Launcher);
        }

        [TearDown]
        public void TearDown()
        {
            for (int i = 0; i < assets.Count; i++) Object.DestroyImmediate(assets[i]);
            assets.Clear();
        }

        // ── 일반 획득 ───────────────────────────────────

        [Test]
        public void NormalPick_AddsOneCard()
        {
            var deck = Deck(Card(strike));

            Assert.That(CardGrantRules.Apply(deck, Offer(strike)), Is.Not.Null);

            Assert.That(deck.Count, Is.EqualTo(2));
            Assert.That(deck[1].Data, Is.EqualTo(strike));
            Assert.That(deck[1].Golden, Is.False);
        }

        [Test]
        public void GoldenPick_AddsAGoldenCard()
        {
            var deck = Deck();

            CardGrantRules.Apply(deck, Offer(strike, golden: true));

            Assert.That(deck[0].Golden, Is.True);
            Assert.That(deck[0].DamageScale, Is.EqualTo(ExpRules.GoldenDamageMul));
        }

        /// <summary>
        /// 돌려주는 카드가 <b>덱에 들어간 바로 그 인스턴스</b>여야 한다.
        /// 새로 만들어 손패에 꽂으면 런 덱과 손패에 같은 카드의 몸이 하나씩 따로 생긴다.
        /// </summary>
        [Test]
        public void Apply_ReturnsTheVeryCardItAdded()
        {
            var deck = Deck();

            ComboCard added = CardGrantRules.Apply(deck, Offer(strike, golden: true));

            Assert.That(added, Is.Not.Null);
            Assert.That(deck[deck.Count - 1], Is.SameAs(added));
        }

        [Test]
        public void EmptyOffer_ChangesNothing()
        {
            var deck = Deck(Card(strike));

            Assert.That(CardGrantRules.Apply(deck, default), Is.Null);
            Assert.That(CardGrantRules.Apply(null, Offer(strike)), Is.Null);
            Assert.That(deck.Count, Is.EqualTo(1));
        }

        // ── 합성 ────────────────────────────────────────

        /// <summary>들고 있던 두 장이 사라지고 황금 한 장이 온다 — 덱은 <b>한 장 줄어든다</b>.</summary>
        [Test]
        public void FusingPick_ConsumesTwoCopies_AndLeavesOneGolden()
        {
            var deck = Deck(Card(strike), Card(strike), Card(launcher));

            CardGrantRules.Apply(deck, Offer(strike, golden: true, fuses: true));

            Assert.That(deck.Count, Is.EqualTo(2), "3장 → 2장");

            Assert.That(CountOf(deck, strike, golden: false), Is.Zero, "일반 회전강타는 전부 사라진다");
            Assert.That(CountOf(deck, strike, golden: true), Is.EqualTo(1));
            Assert.That(CountOf(deck, launcher, golden: false), Is.EqualTo(1), "다른 카드는 안 건드린다");
        }

        /// <summary>이미 들고 있던 황금은 재료가 아니다. 합성해도 그대로 남아야 한다.</summary>
        [Test]
        public void Fusing_NeverEatsGoldenCopies()
        {
            var deck = Deck(Golden(strike), Card(strike), Card(strike));

            CardGrantRules.Apply(deck, Offer(strike, golden: true, fuses: true));

            Assert.That(CountOf(deck, strike, golden: true), Is.EqualTo(2), "원래 황금 1 + 합성 결과 1");
            Assert.That(CountOf(deck, strike, golden: false), Is.Zero);
        }

        /// <summary>
        /// 선택지를 짠 뒤 덱이 바뀌어 재료가 모자란 경우(동료 사망으로 카드가 걷힌 등).
        /// 조용히 어긋나느니 카드 한 장이라도 손에 쥐여 주는 편이 낫다.
        /// </summary>
        [Test]
        public void Fusing_WithoutEnoughCopies_StillGrantsTheCard()
        {
            var deck = Deck(Card(strike));

            Assert.That(CardGrantRules.Apply(deck, Offer(strike, golden: true, fuses: true)), Is.Not.Null);

            Assert.That(CountOf(deck, strike, golden: true), Is.EqualTo(1));
        }

        [Test]
        public void RemoveNormalCopies_StopsAtTheAskedCount()
        {
            var deck = Deck(Card(strike), Card(strike), Card(strike));

            Assert.That(CardGrantRules.RemoveNormalCopies(deck, strike, 2), Is.EqualTo(2));
            Assert.That(deck.Count, Is.EqualTo(1));

            Assert.That(CardGrantRules.RemoveNormalCopies(deck, strike, 0), Is.Zero);
            Assert.That(CardGrantRules.RemoveNormalCopies(null, strike, 2), Is.Zero);
        }

        // ── 드로우 가중치 ───────────────────────────────

        /// <summary>레벨업으로 얻은 시동기가 시작 덱의 시동기와 다르게 굴면 안 된다.</summary>
        [Test]
        public void NewCards_KeepTheSameStarterWeightAsTheStartingDeck()
        {
            Assert.That(CardGrantRules.WeightOf(launcher), Is.EqualTo(2f), "시동기");
            Assert.That(CardGrantRules.WeightOf(strike), Is.EqualTo(1f));
            Assert.That(CardGrantRules.WeightOf(null), Is.EqualTo(1f));
        }

        // ── 황금 배율 ───────────────────────────────────

        [Test]
        public void GoldenCard_CarriesTheDamageMultiplier()
        {
            Assert.That(new ComboCard(strike).DamageScale, Is.EqualTo(1f));
            Assert.That(new ComboCard(strike, 1f, true).DamageScale, Is.EqualTo(1.5f));
            Assert.That(ExpRules.GoldenDamageMul, Is.EqualTo(1.5f));
        }

        /// <summary>구조체 기본값 0이 그대로 새어 나가면 모든 스킬의 데미지가 0이 된다.</summary>
        [Test]
        public void SkillContext_DefaultDamageScale_IsOne()
        {
            Assert.That(default(SkillContext).DamageScale, Is.EqualTo(1f));
            Assert.That(new SkillContext { damageScale = 1.5f }.DamageScale, Is.EqualTo(1.5f));
        }

        [Test]
        public void Clone_And_AsGolden_CarryTheGrade()
        {
            var normal = new ComboCard(launcher, 2f);
            var golden = normal.AsGolden();

            Assert.That(normal.Clone().Golden, Is.False);
            Assert.That(golden.Golden, Is.True);
            Assert.That(golden.DrawWeight, Is.EqualTo(2f), "가중치는 그대로 따라간다");
            Assert.That(golden.Clone().Golden, Is.True);
        }

        // ── 도구 ────────────────────────────────────────

        private static List<ComboCard> Deck(params ComboCard[] cards) => new List<ComboCard>(cards);

        private static ComboCard Card(SkillData data) => new ComboCard(data);
        private static ComboCard Golden(SkillData data) => new ComboCard(data, 1f, true);

        private static CardOffer Offer(SkillData data, bool golden = false, bool fuses = false)
            => new CardOffer { data = data, golden = golden, fuses = fuses };

        private static int CountOf(List<ComboCard> deck, SkillData data, bool golden)
        {
            int n = 0;
            foreach (ComboCard c in deck)
                if (c != null && c.Data == data && c.Golden == golden) n++;

            return n;
        }

        private SkillData NewSkill(string name, AttackType type)
        {
            var data = ScriptableObject.CreateInstance<SkillData>();
            assets.Add(data);

            data.skillName = name;
            data.role = Role.Warrior;
            data.attackType = type;
            return data;
        }
    }
}
