using NUnit.Framework;
using UnityEngine;

namespace Prototype.Tests
{
    /// <summary>
    /// 게이지 막대 자체의 눈금과, 그 막대가 손패 어디에 놓이는지.
    ///
    /// "게이지를 잘 보이도록 불릿타임 아래로"가 요구사항이었다.
    /// 아래라는 건 눈으로만 확인할 게 아니라 좌표로 고정해 둘 수 있다.
    /// </summary>
    public class BulletTimeGaugeUITests
    {
        private const float Eps = 0.0001f;

        // ── 막대 채움 ────────────────────────────────────

        [Test]
        public void Fill_EmptyAndFull()
        {
            Assert.That(BulletTimeGaugeWidget.FillWidth(0f, 400f), Is.EqualTo(0f).Within(Eps));
            Assert.That(BulletTimeGaugeWidget.FillWidth(1f, 400f), Is.EqualTo(400f).Within(Eps));
        }

        [Test]
        public void Fill_IsProportional()
        {
            Assert.That(BulletTimeGaugeWidget.FillWidth(0.25f, 400f), Is.EqualTo(100f).Within(Eps));
        }

        /// <summary>비율이 범위를 벗어나도 막대가 트랙 밖으로 삐져나가면 안 된다.</summary>
        [Test]
        public void Fill_Clamps()
        {
            Assert.That(BulletTimeGaugeWidget.FillWidth(-0.5f, 400f), Is.EqualTo(0f).Within(Eps));
            Assert.That(BulletTimeGaugeWidget.FillWidth(1.5f, 400f), Is.EqualTo(400f).Within(Eps));
        }

        /// <summary>채움 폭은 테두리를 뺀 안쪽이다. 트랙 전체 폭을 쓰면 테두리를 덮는다.</summary>
        [Test]
        public void InnerWidth_LeavesTheBorderShowing()
        {
            Assert.That(BulletTimeGaugeWidget.InnerWidth, Is.LessThan(BulletTimeGaugeWidget.Width));
            Assert.That(BulletTimeGaugeWidget.InnerWidth, Is.GreaterThan(0f));
        }

        // ── 진입 임계 눈금 ───────────────────────────────

        [Test]
        public void Tick_SitsAtTheRequiredRatio()
        {
            Assert.That(BulletTimeGaugeWidget.TickX(0.5f, 400f), Is.EqualTo(200f).Within(Eps));
            Assert.That(BulletTimeGaugeWidget.TickX(0f, 400f), Is.EqualTo(0f).Within(Eps));
        }

        /// <summary>눈금이 선 자리가 곧 "여기까지 차면 쓸 수 있다"는 곳이어야 한다.</summary>
        [Test]
        public void Tick_MeetsTheFillAtTheRequiredRatio()
        {
            const float w = 400f;
            const float required = 0.6f;

            Assert.That(BulletTimeGaugeWidget.FillWidth(required, w),
                Is.EqualTo(BulletTimeGaugeWidget.TickX(required, w)).Within(Eps));
        }

        /// <summary>100%가 필요하면 눈금은 막대 끝과 겹쳐 아무것도 안 알려 준다. 안 그린다.</summary>
        [Test]
        public void Tick_HiddenWhenTheWholeBarIsRequired()
        {
            Assert.That(BulletTimeGaugeWidget.ShowsTick(1f), Is.False);
            Assert.That(BulletTimeGaugeWidget.ShowsTick(0f), Is.False);
            Assert.That(BulletTimeGaugeWidget.ShowsTick(0.6f), Is.True);
        }

        [Test]
        public void Pulse_StaysInsideZeroToOne()
        {
            for (float t = 0f; t < 3f; t += 0.05f)
                Assert.That(BulletTimeGaugeWidget.Pulse(t), Is.InRange(0f, 1f));
        }

        // ── 판 안에서의 자리 ─────────────────────────────

        /// <summary>요구사항 그 자체 — 게이지는 카드 <b>아래</b>에 있다.</summary>
        [Test]
        public void Gauge_SitsBelowEveryCard()
        {
            float lowestCardBottom = HandBoardLayout.RowCenterY + HandFanLayout.FanBottom(Hand.Size);

            Assert.That(lowestCardBottom, Is.GreaterThan(HandBoardLayout.GaugeTopY),
                "게이지가 카드에 물린다");
        }

        /// <summary>붙어 있어도 안 된다. 겹치지 않되 한눈에 들어오는 거리여야 한다.</summary>
        [Test]
        public void Gauge_KeepsAGapFromTheCards()
        {
            float lowestCardBottom = HandBoardLayout.RowCenterY + HandFanLayout.FanBottom(Hand.Size);

            Assert.That(lowestCardBottom - HandBoardLayout.GaugeTopY,
                Is.EqualTo(HandBoardLayout.Gap).Within(0.01f));
        }

        /// <summary>아래에서 위로 힌트 · 제목 · 게이지 · 카드 순으로 쌓인다.</summary>
        [Test]
        public void Board_StacksBottomUp()
        {
            Assert.That(HandBoardLayout.HintCenterY, Is.LessThan(HandBoardLayout.TitleCenterY));
            Assert.That(HandBoardLayout.TitleCenterY, Is.LessThan(HandBoardLayout.GaugeCenterY));
            Assert.That(HandBoardLayout.GaugeCenterY, Is.LessThan(HandBoardLayout.RowCenterY));
        }

        /// <summary>글자와 게이지가 화면 밖으로 안 밀려나야 한다.</summary>
        [Test]
        public void Board_StartsAboveTheScreenEdge()
        {
            Assert.That(HandBoardLayout.HintCenterY - HandBoardLayout.HintHeight * 0.5f,
                Is.GreaterThanOrEqualTo(0f));
            Assert.That(HandBoardLayout.ScreenMargin, Is.GreaterThan(0f));
        }

        /// <summary>
        /// 집은 카드는 판 <b>위로</b> 솟는다. 판이 그만큼 커지면 안 된다 —
        /// 커지면 평소에도 화면 아래 절반을 먹는다. 위쪽 여백은 화면이 대신 내준다.
        /// </summary>
        [Test]
        public void GrabbedCard_RisesAboveTheBoard()
        {
            CardPose grabbed = HandFanLayout.Pose(0, Hand.Size, CardVisualState.Grabbed);
            float top = HandBoardLayout.RowCenterY + grabbed.pos.y + HandFanLayout.HalfHeight(grabbed);

            Assert.That(top, Is.GreaterThan(HandBoardLayout.PanelHeight));
        }

        /// <summary>판 높이는 평소(안 집은) 부채꼴을 정확히 감싼다.</summary>
        [Test]
        public void Board_WrapsTheRestingFan()
        {
            float fanTop = HandBoardLayout.RowCenterY + HandFanLayout.FanTop(Hand.Size);

            Assert.That(HandBoardLayout.PanelHeight, Is.EqualTo(fanTop).Within(0.01f));
        }

        /// <summary>판은 부채꼴과 게이지 중 넓은 쪽을 담아야 한다. 좁으면 드래그 판정이 잘린다.</summary>
        [Test]
        public void Board_IsWideEnoughForBoth()
        {
            Assert.That(HandBoardLayout.PanelWidth,
                Is.GreaterThan(HandFanLayout.FanWidth(Hand.Size)));
            Assert.That(HandBoardLayout.PanelWidth,
                Is.GreaterThan(BulletTimeGaugeWidget.Width));
        }

        /// <summary>부채꼴 좌표계는 원점이 한가운데다 — 위아래 어느 쪽도 잘리면 안 된다.</summary>
        [Test]
        public void RowFrame_CoversTheFanBothWays()
        {
            float half = HandBoardLayout.RowSize.y * 0.5f;

            Assert.That(half, Is.GreaterThanOrEqualTo(HandFanLayout.FanTop(Hand.Size)));
            Assert.That(half, Is.GreaterThanOrEqualTo(-HandFanLayout.FanBottom(Hand.Size)));
        }
    }
}
