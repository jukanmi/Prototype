using System.Collections.Generic;
using NUnit.Framework;
using Prototype;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Prototype.Tests
{
    /// <summary>고른 카드를 덱에 넣는 규칙과 드로우 가중치.</summary>
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

        [Test]
        public void Pick_AddsOneCard()
        {
            var deck = new List<ComboCard> { new ComboCard(strike) };

            Assert.That(CardGrantRules.Apply(deck, new CardOffer { data = strike }), Is.Not.Null);

            Assert.That(deck.Count, Is.EqualTo(2));
            Assert.That(deck[1].Data, Is.EqualTo(strike));
        }

        [Test]
        public void EmptyPick_AddsNothing()
        {
            var deck = new List<ComboCard>();

            Assert.That(CardGrantRules.Apply(deck, default), Is.Null);
            Assert.That(deck, Is.Empty);
        }

        /// <summary>보상으로 얻은 시동기가 시작 덱의 시동기와 다르게 굴면 안 된다.</summary>
        [Test]
        public void NewCards_KeepTheSameStarterWeightAsTheStartingDeck()
        {
            Assert.That(CardGrantRules.WeightOf(launcher), Is.EqualTo(2f), "시동기");
            Assert.That(CardGrantRules.WeightOf(strike), Is.EqualTo(1f));
            Assert.That(CardGrantRules.WeightOf(null), Is.EqualTo(1f));
        }

        /// <summary>구조체 기본값 0이 그대로 새어 나가면 모든 스킬의 데미지가 0이 된다.</summary>
        [Test]
        public void SkillContext_DefaultDamageScale_IsOne()
        {
            Assert.That(default(SkillContext).DamageScale, Is.EqualTo(1f));
            Assert.That(new SkillContext { damageScale = 1.5f }.DamageScale, Is.EqualTo(1.5f));
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
