using NUnit.Framework;
using UnityEngine;

namespace Prototype.Tests
{
    /// <summary>
    /// 손패는 실제로 쥔 카드처럼 보여야 한다 — 한 점에서 펼쳐지고, 모퉁이가 겹치고,
    /// 고른 장이 앞으로 나온다. 그 "보여야 한다"를 숫자로 못 박는다.
    /// </summary>
    public class HandFanLayoutTests
    {
        private const float Eps = 0.0001f;

        // ── 부채꼴 모양 ──────────────────────────────────

        /// <summary>한쪽으로 쏠린 부채는 손에 쥔 것으로 안 보인다.</summary>
        [Test]
        public void Fan_IsSymmetric()
        {
            CardPose left = HandFanLayout.FanPose(0, 4);
            CardPose right = HandFanLayout.FanPose(3, 4);

            Assert.That(right.pos.x, Is.EqualTo(-left.pos.x).Within(Eps));
            Assert.That(right.pos.y, Is.EqualTo(left.pos.y).Within(Eps));
            Assert.That(right.angle, Is.EqualTo(-left.angle).Within(Eps));
        }

        [Test]
        public void SingleCard_SitsDeadCenter()
        {
            CardPose p = HandFanLayout.FanPose(0, 1);

            Assert.That(p.pos.x, Is.EqualTo(0f).Within(Eps));
            Assert.That(p.pos.y, Is.EqualTo(0f).Within(Eps));
            Assert.That(p.angle, Is.EqualTo(0f).Within(Eps));
            Assert.That(p.scale, Is.EqualTo(1f).Within(Eps));
        }

        /// <summary>홀수 장이면 가운데 한 장은 똑바로 선다.</summary>
        [Test]
        public void OddCount_MiddleCardIsUpright()
        {
            CardPose p = HandFanLayout.FanPose(1, 3);

            Assert.That(p.pos.x, Is.EqualTo(0f).Within(Eps));
            Assert.That(p.angle, Is.EqualTo(0f).Within(Eps));
        }

        [Test]
        public void CardsRunLeftToRight()
        {
            for (int i = 1; i < 4; i++)
            {
                float prev = HandFanLayout.FanPose(i - 1, 4).pos.x;
                float cur = HandFanLayout.FanPose(i, 4).pos.x;

                Assert.That(cur, Is.GreaterThan(prev), $"{i - 1}번보다 {i}번이 오른쪽에 있어야 한다");
            }
        }

        /// <summary>요구사항의 핵심 — 모퉁이끼리 겹쳐야 한다. 간격이 카드 폭 이상이면 그냥 일렬이다.</summary>
        [Test]
        public void AdjacentCards_Overlap()
        {
            for (int i = 1; i < 4; i++)
            {
                float gap = HandFanLayout.FanPose(i, 4).pos.x - HandFanLayout.FanPose(i - 1, 4).pos.x;

                Assert.That(gap, Is.GreaterThan(0f));
                Assert.That(gap, Is.LessThan(HandFanLayout.CardWidth),
                    $"간격 {gap:0.#}이 카드 폭 이상이라 안 겹친다");
            }
        }

        /// <summary>아치 — 부채꼴 중심이 아래에 있으니 가운데가 가장 높다.</summary>
        [Test]
        public void MiddleCards_RideHigher()
        {
            Assert.That(HandFanLayout.FanPose(1, 4).pos.y,
                Is.GreaterThan(HandFanLayout.FanPose(0, 4).pos.y));
            Assert.That(HandFanLayout.FanPose(2, 4).pos.y,
                Is.GreaterThan(HandFanLayout.FanPose(3, 4).pos.y));
        }

        /// <summary>왼쪽 카드는 반시계, 오른쪽은 시계로 눕는다.</summary>
        [Test]
        public void OuterCards_TiltOutward()
        {
            Assert.That(HandFanLayout.FanPose(0, 4).angle, Is.GreaterThan(0f));
            Assert.That(HandFanLayout.FanPose(3, 4).angle, Is.LessThan(0f));
        }

        // ── 상태별 들어올림 ──────────────────────────────

        [Test]
        public void Cursor_RisesAboveIdle()
        {
            float idle = HandFanLayout.Pose(1, 4, CardVisualState.Idle).pos.y;
            float cursor = HandFanLayout.Pose(1, 4, CardVisualState.Cursor).pos.y;

            Assert.That(cursor, Is.GreaterThan(idle));
        }

        /// <summary>집기는 선택보다 확실히 더 솟고 더 커야 한다 — 둘이 구분돼야 한다.</summary>
        [Test]
        public void Grabbed_RisesHigherAndGrowsBeyondCursor()
        {
            CardPose cursor = HandFanLayout.Pose(1, 4, CardVisualState.Cursor);
            CardPose grabbed = HandFanLayout.Pose(1, 4, CardVisualState.Grabbed);

            Assert.That(grabbed.pos.y, Is.GreaterThan(cursor.pos.y));
            Assert.That(grabbed.scale, Is.GreaterThan(cursor.scale));
            Assert.That(grabbed.scale, Is.GreaterThan(1f), "집은 카드는 잘 보이도록 커져야 한다");
        }

        /// <summary>집은 카드는 똑바로 선다. 기울어진 채 크게 뜨면 오히려 읽기 어렵다.</summary>
        [Test]
        public void Grabbed_StandsUpright()
        {
            Assert.That(HandFanLayout.Pose(0, 4, CardVisualState.Grabbed).angle,
                Is.EqualTo(0f).Within(Eps));
        }

        /// <summary>실시간의 "다음 카드" 표시는 선택보다 조용해야 한다.</summary>
        [Test]
        public void Next_LiftsLessThanCursor()
        {
            float idle = HandFanLayout.Pose(0, 4, CardVisualState.Idle).pos.y;
            float next = HandFanLayout.Pose(0, 4, CardVisualState.Next).pos.y;
            float cursor = HandFanLayout.Pose(0, 4, CardVisualState.Cursor).pos.y;

            Assert.That(next, Is.GreaterThan(idle));
            Assert.That(next, Is.LessThan(cursor));
        }

        /// <summary>들어올림은 가로로 밀지 않는다. 옆 카드와 자리가 바뀌면 순서가 헷갈린다.</summary>
        [Test]
        public void Lifting_DoesNotShiftSideways()
        {
            float idle = HandFanLayout.Pose(0, 4, CardVisualState.Idle).pos.x;

            Assert.That(HandFanLayout.Pose(0, 4, CardVisualState.Cursor).pos.x,
                Is.EqualTo(idle).Within(Eps));
            Assert.That(HandFanLayout.Pose(0, 4, CardVisualState.Grabbed).pos.x,
                Is.EqualTo(idle).Within(Eps));
        }

        // ── 화살표 ───────────────────────────────────────

        /// <summary>화살표가 카드를 덮으면 아트를 가려 표시가 아니라 방해가 된다.</summary>
        [Test]
        public void Arrow_ClearsTheCardTop()
        {
            CardPose p = HandFanLayout.Pose(0, 4, CardVisualState.Cursor);
            Vector2 arrow = HandFanLayout.ArrowPos(p);

            Assert.That(arrow.y, Is.GreaterThan(p.pos.y + HandFanLayout.HalfHeight(p)));
            Assert.That(arrow.x, Is.EqualTo(p.pos.x).Within(Eps), "가리키는 카드와 같은 세로선 위여야 한다");
        }

        /// <summary>카드가 솟으면 화살표도 같이 솟아야 한 덩어리로 읽힌다.</summary>
        [Test]
        public void Arrow_RisesWithTheCard()
        {
            float idle = HandFanLayout.ArrowPos(HandFanLayout.Pose(1, 4, CardVisualState.Idle)).y;
            float grabbed = HandFanLayout.ArrowPos(HandFanLayout.Pose(1, 4, CardVisualState.Grabbed)).y;

            Assert.That(grabbed, Is.GreaterThan(idle));
        }

        // ── 겹침 순서 ────────────────────────────────────

        /// <summary>uGUI는 형제 번호가 클수록 위에 그린다. 다음에 나갈 왼쪽 카드가 안 가려야 한다.</summary>
        [Test]
        public void SiblingOrder_PutsLeftmostOnTop()
        {
            Assert.That(HandFanLayout.SiblingIndex(0, 4), Is.EqualTo(3));
            Assert.That(HandFanLayout.SiblingIndex(3, 4), Is.EqualTo(0));
        }

        [Test]
        public void SiblingOrder_IsAPermutation()
        {
            var seen = new bool[4];
            for (int i = 0; i < 4; i++)
            {
                int s = HandFanLayout.SiblingIndex(i, 4);

                Assert.That(s, Is.InRange(0, 3));
                Assert.That(seen[s], Is.False, $"형제 번호 {s}이 두 번 나왔다");
                seen[s] = true;
            }
        }

        // ── 기울어진 카드가 차지하는 크기 ────────────────

        /// <summary>기울면 세로로 더 차지한다. 이걸 무시하면 화살표가 카드에 파묻힌다.</summary>
        [Test]
        public void TiltedCard_TakesMoreVerticalRoom()
        {
            var upright = new CardPose(Vector2.zero, 0f, 1f);
            var tilted = new CardPose(Vector2.zero, 12f, 1f);

            Assert.That(HandFanLayout.HalfHeight(tilted), Is.GreaterThan(HandFanLayout.HalfHeight(upright)));
        }

        [Test]
        public void ScaledCard_TakesProportionallyMoreRoom()
        {
            var one = new CardPose(Vector2.zero, 0f, 1f);
            var big = new CardPose(Vector2.zero, 0f, 2f);

            Assert.That(HandFanLayout.HalfHeight(big),
                Is.EqualTo(HandFanLayout.HalfHeight(one) * 2f).Within(Eps));
        }

        [Test]
        public void FanBottom_IsTheLowestCardEdge()
        {
            float bottom = HandFanLayout.FanBottom(4);

            for (int i = 0; i < 4; i++)
            {
                CardPose p = HandFanLayout.FanPose(i, 4);
                Assert.That(p.pos.y - HandFanLayout.HalfHeight(p), Is.GreaterThanOrEqualTo(bottom - Eps));
            }

            Assert.That(bottom, Is.LessThan(HandFanLayout.FanTop(4)));
        }

        /// <summary>부채꼴이 일렬보다 좁아야 화면 아래를 덜 먹는다. 겹침의 실익이다.</summary>
        [Test]
        public void Fan_IsNarrowerThanARow()
        {
            float row = 4 * HandFanLayout.CardWidth;

            Assert.That(HandFanLayout.FanWidth(4), Is.LessThan(row));
        }
    }
}
