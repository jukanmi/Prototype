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

        /// <summary>시작 스킬은 처음부터 총공격 슬롯에 앞에서부터 들어간다. 같은 스킬은 한 칸, 다섯째부터는 안 들어간다.</summary>
        [Test]
        public void SeedDeck_SlotsTheStartingSkills()
        {
            var run = new RunProgression();
            var names = new[] { "가", "나", "다", "라", "마" };
            var seed = new List<ComboCard> { Card() };   // "회전강타" 둘 — 한 칸만
            foreach (string n in names)
            {
                var s = ScriptableObject.CreateInstance<SkillData>();
                s.skillName = n;
                assets.Add(s);
                seed.Add(new ComboCard(s));
            }
            seed.Add(Card());

            run.SeedDeck(seed);

            Assert.That(run.AssaultSlots, Is.EqualTo(new[] { skill.skillName, "가", "나", "다" }));
        }

        /// <summary>
        /// 마을에서 동료를 바꿨다 — 뺀 동료의 스킬은 보유 스킬 · 총공격 칸에서 사라지고, 남은 스킬은 바꿔 둔 칸을 지키고,
        /// 새 동료의 스킬은 앞쪽 빈 칸에 들어간다.
        /// </summary>
        [Test]
        public void ReseedDeck_DropsRemovedSkills_KeepsOrder_AddsNewOnes()
        {
            var run = new RunProgression();
            SkillData a = Skill("가"), b = Skill("나"), c = Skill("다");

            run.SeedDeck(new List<ComboCard> { new ComboCard(a), new ComboCard(b) });
            run.SwapAssaultSlots(0, 1);   // 마을에서 순서를 바꿨다 — 나, 가

            run.ReseedDeck(new List<ComboCard> { new ComboCard(b), new ComboCard(c) });   // 가를 빼고 다를 넣었다

            Assert.That(run.AssaultSlots, Is.EqualTo(new[] { "나", "다", null, null }));
            Assert.That(run.CountOf("가"), Is.Zero, "뺀 동료의 스킬이 남았다");
            Assert.That(run.CountOf("다"), Is.EqualTo(1));
        }

        /// <summary>씨를 아직 안 뿌렸으면 다시 뿌릴 것도 없다 — 첫 판이 정식으로 뿌린다.</summary>
        [Test]
        public void ReseedDeck_BeforeSeeding_DoesNothing()
        {
            var run = new RunProgression();

            run.ReseedDeck(new List<ComboCard> { Card() });

            Assert.That(run.Seeded, Is.False);
            Assert.That(run.Cards, Is.Empty);
        }

        private SkillData Skill(string name)
        {
            var s = ScriptableObject.CreateInstance<SkillData>();
            s.skillName = name;
            assets.Add(s);
            return s;
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
