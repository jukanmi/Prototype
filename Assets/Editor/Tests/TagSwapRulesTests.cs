using System;
using NUnit.Framework;
using UnityEngine;

namespace Prototype.Tests
{
    /// <summary>
    /// 태그 교대 순환 규칙. <see cref="Entity"/>를 세우지 않고 술어만으로 돌린다 —
    /// 에디트모드에서는 Awake가 안 돌아 "죽은 몸"을 만들 방법이 없다.
    ///
    /// 여기서 보는 건 순서뿐이다. 실제 활성/비활성 · 배치 · 빙의 전환은
    /// <see cref="TagSwapController"/>가 하고 그건 씬을 재생해서 확인한다.
    ///
    /// 로스터는 플레이어가 0번, 동료가 1~4번인 5칸이다.
    /// </summary>
    public class TagSwapRulesTests
    {
        /// <summary>alive[i] 가 true면 그 칸을 세울 수 있다.</summary>
        private static Func<int, bool> From(params bool[] alive)
            => i => i >= 0 && i < alive.Length && alive[i];

        // ── 순환 ────────────────────────────────────────────

        [Test]
        public void Next_MovesToFollowingSlot()
        {
            Assert.That(TagSwapRules.Next(4, From(true, true, true, true), 1), Is.EqualTo(2));
        }

        [Test]
        public void Next_WrapsFromLastToFirst()
        {
            Assert.That(TagSwapRules.Next(4, From(true, true, true, true), 3), Is.EqualTo(0));
        }

        [Test]
        public void Next_FromNegative_StartsAtZero()
        {
            // 시작 직후에는 세운 동료가 없다. -1에서 부르면 0번부터 봐야 한다.
            Assert.That(TagSwapRules.Next(4, From(true, true, true, true), -1), Is.EqualTo(0));
        }

        [Test]
        public void Next_OutOfRangeCurrent_StartsAtZero()
        {
            Assert.That(TagSwapRules.Next(4, From(true, true, true, true), 99), Is.EqualTo(0));
        }

        // ── 건너뛰기 ────────────────────────────────────────

        [Test]
        public void Next_SkipsUnselectableSlots()
        {
            // 1·2번이 죽었거나 비었다. 0에서 누르면 3번으로 건너뛴다.
            Assert.That(TagSwapRules.Next(4, From(true, false, false, true), 0), Is.EqualTo(3));
        }

        [Test]
        public void Next_SkipsWhileWrapping()
        {
            // 3에서 감기면서 0·1을 건너뛰고 2로 간다.
            Assert.That(TagSwapRules.Next(4, From(false, false, true, true), 3), Is.EqualTo(2));
        }

        // ── 경계 ────────────────────────────────────────────

        [Test]
        public void Next_LastSurvivorReturnsItself()
        {
            // 혼자 남았을 때 -1을 주면 부르는 쪽이 "전멸"과 구별하지 못한다.
            // 교대는 실패해야 하지만 그 동료는 계속 서 있어야 한다.
            Assert.That(TagSwapRules.Next(4, From(false, true, false, false), 1), Is.EqualTo(1));
        }

        [Test]
        public void Next_AllDeadReturnsMinusOne()
        {
            Assert.That(TagSwapRules.Next(4, From(false, false, false, false), 2), Is.EqualTo(-1));
        }

        [Test]
        public void Next_EmptyPartyReturnsMinusOne()
        {
            Assert.That(TagSwapRules.Next(0, From(), -1), Is.EqualTo(-1));
        }

        [Test]
        public void Next_NullPredicateReturnsMinusOne()
        {
            Assert.That(TagSwapRules.Next(4, null, 0), Is.EqualTo(-1));
        }

        [Test]
        public void Next_CurrentSlotDead_StillFindsSurvivor()
        {
            // 교대자가 죽어 자동 교대가 도는 경로. 자기 칸이 이미 못 쓰는 상태다.
            Assert.That(TagSwapRules.Next(4, From(false, false, true, false), 0), Is.EqualTo(2));
        }

        // ── 5칸 로스터 ──────────────────────────────────────

        [Test]
        public void Next_FiveSlotRoster_WrapsBackToPlayer()
        {
            // 플레이어(0) + 동료 4. 마지막 동료에서 누르면 플레이어로 돌아온다.
            Assert.That(TagSwapRules.Next(5, From(true, true, true, true, true), 4), Is.EqualTo(0));
        }

        [Test]
        public void Next_FiveSlotRoster_SkipsDeadPlayer()
        {
            // 플레이어가 죽어도 동료 순환은 계속돈다.
            Assert.That(TagSwapRules.Next(5, From(false, true, true, true, true), 4), Is.EqualTo(1));
        }

        // ── 첫 배치 ─────────────────────────────────────────

        [Test]
        public void First_PicksLowestSelectableSlot()
        {
            Assert.That(TagSwapRules.First(4, From(false, true, true, false)), Is.EqualTo(1));
        }

        [Test]
        public void First_NoneSelectableReturnsMinusOne()
        {
            Assert.That(TagSwapRules.First(4, From(false, false, false, false)), Is.EqualTo(-1));
        }

        // ── 로스터 어댑터 ───────────────────────────────────

        [Test]
        public void IsSelectable_RejectsNullSlot()
        {
            var roster = new Entity[] { null, null };

            Assert.That(TagSwapRules.IsSelectable(roster, 0), Is.False);
            Assert.That(TagSwapRules.IsSelectable(roster, 1), Is.False);
        }

        [Test]
        public void IsSelectable_RejectsOutOfRange()
        {
            var roster = new Entity[] { null };

            Assert.That(TagSwapRules.IsSelectable(roster, -1), Is.False);
            Assert.That(TagSwapRules.IsSelectable(roster, 5), Is.False);
        }

        [Test]
        public void NextAlive_NullRosterReturnsMinusOne()
        {
            Assert.That(TagSwapRules.NextAlive(null, 0), Is.EqualTo(-1));
            Assert.That(TagSwapRules.FirstAlive(null), Is.EqualTo(-1));
        }

        [Test]
        public void NextAlive_AllSlotsEmptyReturnsMinusOne()
        {
            // 로스터는 플레이어 1 + 동료 4 = 5칸이다. 전부 비면 세울 몸이 없다.
            Assert.That(TagSwapRules.NextAlive(new Entity[5], 0), Is.EqualTo(-1));
        }
    }
}
