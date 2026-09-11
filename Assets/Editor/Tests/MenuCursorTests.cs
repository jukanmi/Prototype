using System;
using NUnit.Framework;

namespace Prototype.Tests
{
    /// <summary>
    /// 모달 커서가 잠긴 칸을 어떻게 다루는가. 레벨업 화면은 경험치가 모자란 단계를
    /// 잠가 두는데, 커서가 거기 얹히면 Enter를 눌러도 아무 일이 없어 입력이 죽은 것처럼 보인다.
    /// </summary>
    public class MenuCursorTests
    {
        private static Func<int, bool> Only(params int[] enabled)
            => i => Array.IndexOf(enabled, i) >= 0;

        // ── First ───────────────────────────────────────────

        [Test]
        public void First_TakesTheLowestEnabled()
        {
            Assert.That(MenuCursor.First(3, Only(1, 2)), Is.EqualTo(1));
        }

        [Test]
        public void First_WithNothingEnabled_IsNone()
        {
            Assert.That(MenuCursor.First(3, Only()), Is.EqualTo(MenuCursor.None));
        }

        [Test]
        public void First_WithNoPredicate_IsZero()
        {
            Assert.That(MenuCursor.First(3, null), Is.EqualTo(0));
        }

        [Test]
        public void First_OnEmptyRow_IsNone()
        {
            Assert.That(MenuCursor.First(0, null), Is.EqualTo(MenuCursor.None));
        }

        // ── Step ────────────────────────────────────────────

        [Test]
        public void Step_MovesOneSlot()
        {
            Assert.That(MenuCursor.Step(0, 3, 1, null), Is.EqualTo(1));
            Assert.That(MenuCursor.Step(2, 3, -1, null), Is.EqualTo(1));
        }

        [Test]
        public void Step_SkipsLockedSlots()
        {
            // 0과 2만 열려 있으면 오른쪽 한 번에 1을 건너뛰고 2로 간다.
            Assert.That(MenuCursor.Step(0, 3, 1, Only(0, 2)), Is.EqualTo(2));
        }

        [Test]
        public void Step_AtTheEnd_StaysPut()
        {
            // 감싸지 않는다. 칸이 서너 개뿐인 줄에서 반대쪽으로 튀면 어디까지 갔는지 손이 못 센다.
            Assert.That(MenuCursor.Step(2, 3, 1, null), Is.EqualTo(2));
            Assert.That(MenuCursor.Step(0, 3, -1, null), Is.EqualTo(0));
        }

        [Test]
        public void Step_WithOnlyLockedSlotsAhead_StaysPut()
        {
            Assert.That(MenuCursor.Step(0, 3, 1, Only(0)), Is.EqualTo(0));
        }

        [Test]
        public void Step_WithoutDirection_StaysPut()
        {
            Assert.That(MenuCursor.Step(1, 3, 0, null), Is.EqualTo(1));
        }

        [Test]
        public void Step_FromOutsideTheRow_LandsOnTheFirstEnabled()
        {
            Assert.That(MenuCursor.Step(MenuCursor.None, 3, 1, Only(2)), Is.EqualTo(2));
        }

        // ── Clamp ───────────────────────────────────────────

        [Test]
        public void Clamp_KeepsAValidSlot()
        {
            Assert.That(MenuCursor.Clamp(2, 3, Only(0, 2)), Is.EqualTo(2));
        }

        [Test]
        public void Clamp_LeavesASlotThatJustLocked()
        {
            // 경험치를 쓰고 단계 판으로 돌아오면 방금 고른 단계가 잠겨 있을 수 있다.
            Assert.That(MenuCursor.Clamp(2, 3, Only(0)), Is.EqualTo(0));
        }

        [Test]
        public void Clamp_WithNothingEnabled_IsNone()
        {
            // 쓸 수 있는 단계가 하나도 남지 않았다. 레벨업 화면은 이때 커서를 [나가기]로 내린다.
            Assert.That(MenuCursor.Clamp(1, 3, Only()), Is.EqualTo(MenuCursor.None));
        }
    }
}
