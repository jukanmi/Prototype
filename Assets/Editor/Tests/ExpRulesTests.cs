using System.Collections.Generic;
using NUnit.Framework;
using Prototype;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Prototype.Tests
{
    /// <summary>
    /// 경험치 · 레벨업 단계의 수치.
    ///
    /// 가장 중요한 항목은 <b>2레벨 1단계가 11인가</b>이다. <c>10 × 1.1</c>은 부동소수점에서
    /// 정확히 11이 아니라 11.000000000000002라서, 올림을 그냥 하면 12가 되어 표 전체가
    /// 한 칸씩 밀린다. 플레이해서는 "레벨업이 좀 비싸네" 정도로만 느껴져 알아채기가 어렵다.
    /// </summary>
    public class ExpRulesTests
    {
        // ── 1레벨: 요구사항이 못 박은 값 ─────────────────

        /// <summary>"적 한 명(10)이면 1레벨 1단계 레벨업"이 이 시스템의 기준점이다.</summary>
        [Test]
        public void FirstLevel_Tier1_CostsExactlyOneEnemyKill()
        {
            Assert.That(ExpRules.CostOf(1, 0), Is.EqualTo(10));
            Assert.That(ExpRules.CanAfford(10, 1, 0), Is.True);
            Assert.That(ExpRules.CanAfford(9, 1, 0), Is.False);
        }

        [Test]
        public void FirstLevel_TiersAre_10_15_20()
        {
            Assert.That(ExpRules.CostOf(1, 0), Is.EqualTo(10), "1단계");
            Assert.That(ExpRules.CostOf(1, 1), Is.EqualTo(15), "2단계 — 1.5배");
            Assert.That(ExpRules.CostOf(1, 2), Is.EqualTo(20), "3단계 — 2배");
        }

        // ── 성장: 레벨업마다 1.1배 ───────────────────────

        [Test]
        public void SecondLevel_Tier1_Is11_NotFloatingPointGarbage()
        {
            Assert.That(ExpRules.CostOf(2, 0), Is.EqualTo(11));
        }

        [Test]
        public void CostGrows_ByOnePointOne_EachLevel()
        {
            // 표에 적어 둔 값 그대로. 여기가 어긋나면 밸런스 논의의 기준이 사라진다.
            Assert.That(ExpRules.CostOf(2, 1), Is.EqualTo(17), "Lv2 2단계 = ceil(16.5)");
            Assert.That(ExpRules.CostOf(2, 2), Is.EqualTo(22), "Lv2 3단계");
            Assert.That(ExpRules.CostOf(3, 0), Is.EqualTo(13), "Lv3 1단계 = ceil(12.1)");
            Assert.That(ExpRules.CostOf(3, 2), Is.EqualTo(25), "Lv3 3단계 = ceil(24.2)");
        }

        [Test]
        public void HigherLevel_IsNeverCheaper()
        {
            for (int tier = 0; tier < ExpRules.TierCount; tier++)
                for (int level = 1; level < 20; level++)
                    Assert.That(ExpRules.CostOf(level + 1, tier),
                        Is.GreaterThanOrEqualTo(ExpRules.CostOf(level, tier)),
                        $"Lv{level} → Lv{level + 1}, {tier}단계");
        }

        // ── 황금 확률 ───────────────────────────────────

        [Test]
        public void GoldenChance_RisesWithTier()
        {
            Assert.That(ExpRules.GoldenChanceOf(0), Is.EqualTo(0f), "1단계는 특전 없음");
            Assert.That(ExpRules.GoldenChanceOf(1), Is.EqualTo(0.20f).Within(0.0001f));
            Assert.That(ExpRules.GoldenChanceOf(2), Is.EqualTo(0.50f).Within(0.0001f));
        }

        [Test]
        public void InvalidTier_CannotBeBought()
        {
            Assert.That(ExpRules.IsValidTier(-1), Is.False);
            Assert.That(ExpRules.IsValidTier(ExpRules.TierCount), Is.False);
            Assert.That(ExpRules.CanAfford(999999, 1, 3), Is.False);
            Assert.That(ExpRules.GoldenChanceOf(3), Is.EqualTo(0f));
        }

        // ── 지금 살 수 있는 것 ───────────────────────────

        [Test]
        public void HighestAffordableTier_PicksTheMostExpensiveOneYouCanPay()
        {
            Assert.That(ExpRules.HighestAffordableTier(9, 1), Is.EqualTo(-1), "하나도 못 산다");
            Assert.That(ExpRules.HighestAffordableTier(10, 1), Is.EqualTo(0));
            Assert.That(ExpRules.HighestAffordableTier(19, 1), Is.EqualTo(1));
            Assert.That(ExpRules.HighestAffordableTier(20, 1), Is.EqualTo(2));
            Assert.That(ExpRules.HighestAffordableTier(500, 1), Is.EqualTo(2), "3단계 위는 없다");
        }

        [Test]
        public void TierNumber_IsOneBased()
        {
            Assert.That(ExpRules.TierNumber(0), Is.EqualTo(1));
            Assert.That(ExpRules.TierNumber(2), Is.EqualTo(3));
        }
    }

    /// <summary>
    /// <see cref="RunProgression"/> — 소모와 이월.
    ///
    /// <b>경험치를 전부 태우지 않는다</b>는 것이 이 시스템의 핵심 규칙이다. 남은 경험치가
    /// 사라지면 "적을 더 잡고 오면 손해"가 되어 라운드마다 레벨업을 미룰 이유가 생긴다.
    /// </summary>
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
        public void StartsAtLevelOne_WithNoExp()
        {
            var run = new RunProgression();

            Assert.That(run.Level, Is.EqualTo(1));
            Assert.That(run.Exp, Is.Zero);
            Assert.That(run.HasDeck, Is.False);
        }

        [Test]
        public void Spend_TakesOnlyThatTiersCost_AndCarriesTheRest()
        {
            var run = new RunProgression();
            run.AddExp(34);

            Assert.That(run.Spend(0), Is.True);

            Assert.That(run.Exp, Is.EqualTo(24), "34 - 10 = 24가 남아야 한다");
            Assert.That(run.Level, Is.EqualTo(2));
        }

        /// <summary>단계는 가격과 황금 확률만 바꾼다. 레벨은 어느 단계든 +1이다.</summary>
        [Test]
        public void EveryTier_RaisesLevelByExactlyOne()
        {
            for (int tier = 0; tier < ExpRules.TierCount; tier++)
            {
                var run = new RunProgression();
                run.AddExp(100);

                Assert.That(run.Spend(tier), Is.True);
                Assert.That(run.Level, Is.EqualTo(2), $"{tier}단계");
            }
        }

        [Test]
        public void Spend_Fails_WhenExpIsShort()
        {
            var run = new RunProgression();
            run.AddExp(9);

            Assert.That(run.Spend(0), Is.False);
            Assert.That(run.Exp, Is.EqualTo(9), "실패한 소모는 경험치를 건드리지 않는다");
            Assert.That(run.Level, Is.EqualTo(1));
        }

        [Test]
        public void RepeatedSpending_GetsProgressivelyMoreExpensive()
        {
            var run = new RunProgression();
            run.AddExp(1000);

            Assert.That(run.CostOf(0), Is.EqualTo(10));
            run.Spend(0);
            Assert.That(run.CostOf(0), Is.EqualTo(11));
            run.Spend(0);
            Assert.That(run.CostOf(0), Is.EqualTo(13));
        }

        [Test]
        public void AddExp_IgnoresZeroAndNegative()
        {
            var run = new RunProgression();
            run.AddExp(0);
            run.AddExp(-50);

            Assert.That(run.Exp, Is.Zero);
        }

        [Test]
        public void Reset_ClearsEverything()
        {
            var run = new RunProgression();
            run.AddExp(100);
            run.Spend(2);
            run.SeedDeck(null);

            run.Reset();

            Assert.That(run.Exp, Is.Zero);
            Assert.That(run.Level, Is.EqualTo(1));
            Assert.That(run.Cards, Is.Empty);
            Assert.That(run.Seeded, Is.False, "새 런은 시작 덱을 다시 정해야 한다");
        }

        // ── 시작 덱 ─────────────────────────────────────

        /// <summary>
        /// 씨는 런에 한 번만 뿌린다. 두 번째 스테이지에서 또 뿌리면
        /// 레벨업으로 얻은 카드가 파티 16장에 통째로 덮인다.
        /// </summary>
        [Test]
        public void SeedDeck_HappensOnlyOncePerRun()
        {
            var run = new RunProgression();
            var seed = new List<ComboCard> { Card(), Card() };

            Assert.That(run.Seeded, Is.False);

            Assert.That(run.SeedDeck(seed), Is.True);
            Assert.That(run.Seeded, Is.True);
            Assert.That(run.Cards.Count, Is.EqualTo(2));

            Assert.That(run.SeedDeck(seed), Is.False, "두 번째 씨뿌리기는 거부된다");
            Assert.That(run.Cards.Count, Is.EqualTo(2), "덱이 두 배로 불어나면 안 된다");
        }

        /// <summary>
        /// <b>테스트 모드</b>의 0장 덱. 장수로 "아직 안 짰다"를 판단하면 여기가 무너져,
        /// 다음 씬에서 파티 카드 16장이 도로 부어진다 — 모드가 조용히 꺼진 것처럼 보인다.
        /// </summary>
        [Test]
        public void SeedDeck_WithNothing_StillCountsAsSeeded()
        {
            var run = new RunProgression();

            run.SeedDeck(null);

            Assert.That(run.Seeded, Is.True);
            Assert.That(run.HasDeck, Is.False);
            Assert.That(run.Cards, Is.Empty);
        }

        /// <summary>디버그 모드가 짠 덱은 이미 정해진 덱도 덮는다.</summary>
        [Test]
        public void SetDeck_ReplacesWhatWasThere()
        {
            var run = new RunProgression();
            run.SeedDeck(new List<ComboCard> { Card(), Card(), Card() });

            run.SetDeck(new List<ComboCard> { Golden() });

            Assert.That(run.Seeded, Is.True);
            Assert.That(run.Cards.Count, Is.EqualTo(1), "3장이 1장으로 갈아 끼워진다");
            Assert.That(run.Cards[0].Golden, Is.True);

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

        // ── 도구 ────────────────────────────────────────

        private ComboCard Card() => new ComboCard(skill);
        private ComboCard Golden() => new ComboCard(skill, 1f, true);
    }
}
