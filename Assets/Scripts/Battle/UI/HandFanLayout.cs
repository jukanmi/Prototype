using UnityEngine;

namespace Prototype
{
    /// <summary>
    /// 카드 한 장의 화면 포즈. 애니메이터가 이 값을 향해 미끄러진다.
    /// RectTransform이 아니라 값 타입이라서 씬 없이 검증할 수 있다.
    /// </summary>
    public struct CardPose
    {
        /// <summary>손패 판(HandRow) 안에서의 anchoredPosition.</summary>
        public Vector2 pos;

        /// <summary>z축 회전(도). 양수가 반시계.</summary>
        public float angle;

        public float scale;

        public CardPose(Vector2 pos, float angle, float scale)
        {
            this.pos = pos;
            this.angle = angle;
            this.scale = scale;
        }

        public static CardPose Identity => new CardPose(Vector2.zero, 0f, 1f);
    }

    /// <summary>
    /// 카드가 지금 어떤 대접을 받고 있는지. 포즈는 여기서만 갈린다 —
    /// 색과 텍스트는 <see cref="ComboBoardUI"/>가 따로 정한다.
    /// </summary>
    public enum CardVisualState
    {
        /// <summary>부채꼴 위 제자리.</summary>
        Idle,

        /// <summary>실시간에서 다음에 나갈 맨 왼쪽 카드. 살짝만 띄운다.</summary>
        Next,

        /// <summary>커서가 짚은 카드. 떠오르고 앞으로 나온다.</summary>
        Cursor,

        /// <summary>집은 카드. 크게 떠올라 똑바로 선다. 조준 중에도 이 포즈를 유지한다.</summary>
        Grabbed,
    }

    /// <summary>
    /// 손패를 실제 카드처럼 부채꼴로 편다.
    ///
    /// 화면 아래 <see cref="PivotRadius"/>만큼 떨어진 <b>가상의 한 점</b>을 쥔 손으로 보고,
    /// 카드를 그 반경 위에 각도만 달리해 얹는다. 그래서 회전과 아치가 따로 놀지 않는다 —
    /// 손목 하나가 돌아가는 모양이 그대로 나온다.
    ///
    /// 모든 함수가 순수 함수다. MonoBehaviour도 RectTransform도 모른다.
    /// </summary>
    public static class HandFanLayout
    {
        // 카드 아트가 세로형(약 3:4)이라 위젯도 세로로 잡는다.
        public const float CardWidth = 140f;
        public const float CardHeight = 208f;

        /// <summary>부채꼴 중심점까지의 거리. 클수록 완만하게 펴진다.</summary>
        public const float PivotRadius = 780f;

        /// <summary>카드 한 장당 벌어지는 각도.</summary>
        public const float StepAngle = 7.5f;

        // ── 상태별 들어올림 ──────────────────────────────

        public const float NextLift = 12f;
        public const float CursorLift = 34f;
        public const float GrabbedLift = 165f;

        public const float CursorScale = 1.06f;
        public const float GrabbedScale = 1.4f;

        /// <summary>커서 카드는 기울기를 절반만 남긴다. 완전히 세우면 집기와 구분이 안 된다.</summary>
        public const float CursorAngleFactor = 0.5f;

        /// <summary>화살표를 카드 위쪽 모서리에서 얼마나 더 띄울지.</summary>
        public const float ArrowGap = 22f;

        /// <summary>부채꼴 중심에서 index번째 카드가 벌어진 각도. 왼쪽이 음수.</summary>
        public static float AngleAt(int index, int count)
        {
            if (count <= 1) return 0f;
            return StepAngle * (index - (count - 1) * 0.5f);
        }

        /// <summary>상태를 빼고 부채꼴 자리만. 손이 쥐고 있는 기본 자세다.</summary>
        public static CardPose FanPose(int index, int count)
        {
            float deg = AngleAt(index, count);
            float rad = deg * Mathf.Deg2Rad;

            // 중심점이 화면 아래에 있으므로 y는 원호를 따라 내려온다 — 가운데가 가장 높다.
            var pos = new Vector2(
                PivotRadius * Mathf.Sin(rad),
                PivotRadius * Mathf.Cos(rad) - PivotRadius);

            // 오른쪽 카드는 시계방향으로 눕는다. 회전 부호가 각도와 반대다.
            return new CardPose(pos, -deg, 1f);
        }

        /// <summary>부채꼴 자리 위에 상태별 들어올림을 얹은 최종 포즈.</summary>
        public static CardPose Pose(int index, int count, CardVisualState state)
        {
            CardPose p = FanPose(index, count);

            switch (state)
            {
                case CardVisualState.Next:
                    p.pos.y += NextLift;
                    break;

                case CardVisualState.Cursor:
                    p.pos.y += CursorLift;
                    p.angle *= CursorAngleFactor;
                    p.scale = CursorScale;
                    break;

                case CardVisualState.Grabbed:
                    p.pos.y += GrabbedLift;
                    p.angle = 0f;
                    p.scale = GrabbedScale;
                    break;
            }

            return p;
        }

        // ── 회전한 카드가 차지하는 크기 ──────────────────
        // 기울어진 사각형의 축 정렬 경계다. 화살표 높이와 판 크기를 여기서 뽑는다.

        public static float HalfHeight(in CardPose pose)
        {
            float rad = pose.angle * Mathf.Deg2Rad;
            return 0.5f * pose.scale *
                   (CardWidth * Mathf.Abs(Mathf.Sin(rad)) + CardHeight * Mathf.Abs(Mathf.Cos(rad)));
        }

        public static float HalfWidth(in CardPose pose)
        {
            float rad = pose.angle * Mathf.Deg2Rad;
            return 0.5f * pose.scale *
                   (CardWidth * Mathf.Abs(Mathf.Cos(rad)) + CardHeight * Mathf.Abs(Mathf.Sin(rad)));
        }

        /// <summary>선택 표시 화살표 자리. 카드 바로 위 중앙 — 아트를 가리지 않는다.</summary>
        public static Vector2 ArrowPos(in CardPose pose)
            => new Vector2(pose.pos.x, pose.pos.y + HalfHeight(in pose) + ArrowGap);

        // ── 판 크기 ──────────────────────────────────────
        // 들어올림은 안 친다. 집은 카드는 판 밖으로 솟는 게 정상이다.

        /// <summary>가장 낮은 카드의 아래 모서리. 부채꼴 중심 기준이라 음수다.</summary>
        public static float FanBottom(int count)
        {
            float lowest = 0f;
            for (int i = 0; i < count; i++)
            {
                CardPose p = FanPose(i, count);
                float bottom = p.pos.y - HalfHeight(in p);
                if (i == 0 || bottom < lowest) lowest = bottom;
            }
            return lowest;
        }

        /// <summary>가장 높은 카드의 위 모서리.</summary>
        public static float FanTop(int count)
        {
            float highest = 0f;
            for (int i = 0; i < count; i++)
            {
                CardPose p = FanPose(i, count);
                float top = p.pos.y + HalfHeight(in p);
                if (i == 0 || top > highest) highest = top;
            }
            return highest;
        }

        public static float FanWidth(int count)
        {
            float widest = 0f;
            for (int i = 0; i < count; i++)
            {
                CardPose p = FanPose(i, count);
                float edge = Mathf.Abs(p.pos.x) + HalfWidth(in p);
                if (edge > widest) widest = edge;
            }
            return widest * 2f;
        }

        public static float FanHeight(int count) => FanTop(count) - FanBottom(count);

        /// <summary>
        /// 겹침 순서. <b>왼쪽이 위</b>다 — 다음에 나갈 카드가 안 가려야 한다.
        /// uGUI는 형제 순서가 곧 그리는 순서이자 레이캐스트 우선순위다.
        /// </summary>
        public static int SiblingIndex(int index, int count) => count - 1 - index;
    }
}
