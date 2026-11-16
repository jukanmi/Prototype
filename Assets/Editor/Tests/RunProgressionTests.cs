using System.Collections.Generic;
using NUnit.Framework;
using Prototype;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Prototype.Tests
{
    /// <summary><see cref="RunProgression"/> — 시작 덱 씨뿌리기와 새 런 초기화.</summary>
    public class RunProgressionTests
    {
        private readonly List<Object> assets = new List<Object>();

        private SkillData skill;

        [SetUp]
        public void SetUp()
        {
            skill = ScriptableObject.CreateInstance<SkillData>();
            skill.skillName = "회전강타";
            assets.Add(skill);
        }

        [TearDown]
        public void TearDown()
        {
            for (int i = 0; i < assets.Count; i++) Object.DestroyImmediate(assets[i]);
            assets.Clear();
        }

        [Test]
        public void NewRun_HasNoDeck()
        {
            Assert.That(new RunProgression().HasDeck, Is.False);
        }

        [Test]
        public void Reset_ClearsDeckAndGold()
        {
            var run = new RunProgression();
            run.AddGold(50);
            run.SeedDeck(new List<ComboCard> { Card() });

            run.Reset();

            Assert.That(run.Gold, Is.Zero);
            Assert.That(run.Cards, Is.Empty);
            Assert.That(run.Seeded, Is.False, "새 런은 시작 덱을 다시 정해야 한다");
        }

        /// <summary>씨는 런에 한 번만 뿌린다. 두 번째 스테이지에서 또 뿌리면 보상 카드가 통째로 덮인다.</summary>
        [Test]
        public void SeedDeck_HappensOnlyOncePerRun()
        {
            var run = new RunProgression();
            var seed = new List<ComboCard> { Card(), Card() };

            Assert.That(run.SeedDeck(seed), Is.True);
            Assert.That(run.Seeded, Is.True);

            Assert.That(run.SeedDeck(seed), Is.False, "두 번째 씨뿌리기는 거부된다");
            Assert.That(run.Cards.Count, Is.EqualTo(2), "덱이 두 배로 불어나면 안 된다");
        }

        /// <summary>테스트 모드의 0장 덱. 장수로 "아직 안 짰다"를 판단하면 파티 카드가 도로 부어진다.</summary>
        [Test]
        public void SeedDeck_WithNothing_StillCountsAsSeeded()
        {
            var run = new RunProgression();

            run.SeedDeck(null);

            Assert.That(run.Seeded, Is.True);
            Assert.That(run.Cards, Is.Empty);
        }

        /// <summary>디버그 모드가 짠 덱은 이미 정해진 덱도 덮는다.</summary>
        [Test]
        public void SetDeck_ReplacesWhatWasThere()
        {
            var run = new RunProgression();
            run.SeedDeck(new List<ComboCard> { Card(), Card(), Card() });

            run.SetDeck(new List<ComboCard> { Card() });

            Assert.That(run.Cards.Count, Is.EqualTo(1), "3장이 1장으로 갈아 끼워진다");

            run.SetDeck(new List<ComboCard>());
            Assert.That(run.Cards, Is.Empty, "빈 목록으로 덮으면 0장이어야 한다");
        }

        /// <summary>SkillData가 없는 카드는 영영 발동할 수 없다. 런 덱에 들이지 않는다.</summary>
        [Test]
        public void SeedDeck_DropsCardsWithNoSkillData()
        {
            var run = new RunProgression();

            run.SeedDeck(new List<ComboCard> { new ComboCard(null), null, Card() });

            Assert.That(run.Cards.Count, Is.EqualTo(1), "쓸 수 있는 한 장만 남는다");
        }

        private ComboCard Card() => new ComboCard(skill);
    }
}
