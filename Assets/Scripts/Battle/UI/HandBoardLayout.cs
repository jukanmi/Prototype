using UnityEngine;

namespace Prototype
{
    /// <summary>
    /// 손패 판 전체의 세로 쌓기. 화면 아래에서 위로 쌓는다 —
    /// <b>힌트 · 제목 · 게이지 · 부채꼴</b> 순서다.
    ///
    /// 게이지가 카드 바로 아래에 오는 게 요점이다. 그리고 카드 <b>위쪽은 비워 둔다</b> —
    /// 집은 카드가 <see cref="HandFanLayout.GrabbedLift"/>만큼 솟아오를 자리이자
    /// 선택 화살표가 설 자리라서, 여기에 다른 UI를 두면 곧바로 가려진다.
    ///
    /// 좌표 원점은 판의 <b>바닥 중앙</b>이다(pivot 0.5, 0).
    /// 판은 <see cref="Hand.Size"/> 기준으로 한 번 크기가 정해지고 그 뒤로 흔들리지 않는다 —
    /// 카드를 쓸 때마다 판이 오르내리면 눈이 따라가지 못한다.
    /// </summary>
    public static class HandBoardLayout
    {
        public const float Gap = 10f;
        public const float HintHeight = 22f;
        public const float TitleHeight = 26f;
        public const float SidePadding = 24f;

        /// <summary>화면 하단에서 판을 띄우는 거리.</summary>
        public const float ScreenMargin = 28f;

        /// <summary>
        /// 제목 · 힌트 줄의 폭. 판보다 넓다 — 조작 안내가 한 줄에 안 들어가면 잘려 나간다.
        /// 클리핑 마스크가 없으므로 판 밖으로 나가도 그대로 그려진다.
        /// </summary>
        public const float TextWidth = 820f;

        public static float HintCenterY => HintHeight * 0.5f;

        public static float TitleCenterY => HintHeight + Gap + TitleHeight * 0.5f;

        public static float GaugeCenterY
            => HintHeight + Gap + TitleHeight + Gap + BulletTimeGaugeWidget.Height * 0.5f;

        public static float GaugeTopY => GaugeCenterY + BulletTimeGaugeWidget.Height * 0.5f;

        /// <summary>가장 낮은 카드의 아래 모서리가 놓이는 높이. 게이지 위로 한 칸 띄운다.</summary>
        public static float RowBottomY => GaugeTopY + Gap;

        /// <summary>부채꼴 중심(HandRow의 원점) 높이.</summary>
        public static float RowCenterY => RowBottomY - HandFanLayout.FanBottom(Hand.Size);

        /// <summary>
        /// 부채꼴 좌표계의 크기. 원점이 <b>부채꼴 중심</b>이므로 위아래 중 먼 쪽에 맞춰 대칭으로 잡는다 —
        /// 그래야 카드의 anchoredPosition이 곧 <see cref="CardPose.pos"/> 그대로가 된다.
        /// </summary>
        public static Vector2 RowSize
            => new Vector2(
                HandFanLayout.FanWidth(Hand.Size),
                2f * Mathf.Max(HandFanLayout.FanTop(Hand.Size), -HandFanLayout.FanBottom(Hand.Size)));

        public static float PanelHeight => RowCenterY + HandFanLayout.FanTop(Hand.Size);

        public static float PanelWidth
            => Mathf.Max(HandFanLayout.FanWidth(Hand.Size), BulletTimeGaugeWidget.Width) + SidePadding * 2f;

        public static Vector2 PanelSize => new Vector2(PanelWidth, PanelHeight);
    }
}
