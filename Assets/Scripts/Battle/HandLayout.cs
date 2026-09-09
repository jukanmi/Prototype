// 손패 배치 — 부채꼴 기하 · 판의 세로 쌓기 · 게이지 위젯 · 카드 포즈 보간.
// 넷이 서로를 부른다: HandBoardLayout이 HandFanLayout과 게이지 높이를 읽고,
// 게이지가 CardPoseAnimator.Step을 쓴다. 전부 순수 함수라 씬 없이 검증된다.

using UnityEngine.UI;
using UnityEngine;

namespace Prototype
{
    // ══ HandFanLayout ═══════════════════════════════════════════

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

    // ══ HandBoardLayout ═══════════════════════════════════════════

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

    // ══ BulletTimeGaugeWidget ═══════════════════════════════════════════

    /// <summary>
    /// 불릿타임 게이지 막대. 손패 바로 아래에 붙는다 —
    /// 무엇을 써서 카드를 쏟아내는지가 카드와 같은 눈길 안에 들어와야 한다.
    ///
    /// MonoBehaviour가 아니다. <see cref="ComboBoardUI"/>가 만들고 매 프레임 먹인다.
    /// 그래야 게이지가 손패 판과 <b>같은 캔버스·같은 좌표계</b>에 놓인다.
    /// </summary>
    public class BulletTimeGaugeWidget
    {
        public const float Width = 420f;
        public const float Height = 22f;

        /// <summary>트랙 안쪽 여백. 이만큼이 테두리로 남는다.</summary>
        public const float Inset = 2f;

        public const float TickWidth = 2f;

        /// <summary>채움이 실제로 그려지는 폭. 테두리를 뺀 값이다.</summary>
        public static float InnerWidth => Width - Inset * 2f;

        private static readonly Color TrackColor = new Color(0.07f, 0.08f, 0.10f, 0.92f);
        private static readonly Color BorderColor = new Color(0.35f, 0.40f, 0.46f, 0.9f);
        private static readonly Color ChargingColor = new Color(0.16f, 0.45f, 0.55f);
        private static readonly Color ReadyLowColor = new Color(0.10f, 0.62f, 0.75f);
        private static readonly Color ReadyHighColor = new Color(0.45f, 0.95f, 1f);
        private static readonly Color ActiveColor = new Color(0.30f, 0.85f, 1f);
        private static readonly Color CooldownColor = new Color(0.62f, 0.34f, 0.14f);
        private static readonly Color TickColor = new Color(1f, 0.82f, 0.4f, 0.85f);
        private static readonly Color LabelColor = new Color(0.92f, 0.95f, 1f);

        private RectTransform _root;
        private RectTransform _fillRect;
        private Image _fill;
        private RectTransform _tickRect;
        private Text _label;

        /// <summary>화면에 그려지는 비율. 실제 비율을 뒤쫓는다 — 소모가 "쭉 빠지는" 연출이 된다.</summary>
        private float _shown;

        public RectTransform Root => _root;

        /// <summary>테스트가 읽는 표시 비율.</summary>
        public float ShownRatio => _shown;

        // ── 순수 계산 ────────────────────────────────────

        /// <summary>비율만큼 찬 채움 폭.</summary>
        public static float FillWidth(float ratio, float fullWidth)
            => Mathf.Clamp01(ratio) * Mathf.Max(0f, fullWidth);

        /// <summary>진입 임계 눈금이 설 자리. 채움 영역 <b>왼쪽 끝</b>에서의 거리다.</summary>
        public static float TickX(float requiredRatio, float fullWidth)
            => Mathf.Clamp01(requiredRatio) * Mathf.Max(0f, fullWidth);

        /// <summary>임계선을 그릴 만한 값인지. 100%면 막대 끝과 겹쳐 눈금이 의미가 없다.</summary>
        public static bool ShowsTick(float requiredRatio)
            => requiredRatio > 0.02f && requiredRatio < 0.99f;

        /// <summary>0~1을 오가는 맥박. 준비 완료를 눈에 띄게 만든다.</summary>
        public static float Pulse(float unscaledTime)
            => 0.5f + 0.5f * Mathf.Sin(unscaledTime * 6f);

        // ── 빌드 ─────────────────────────────────────────

        public void Build(Transform parent)
        {
            Image border = UiKit.NewImage(parent, "GaugeBar", BorderColor);
            _root = border.rectTransform;
            _root.sizeDelta = new Vector2(Width, Height);

            Image track = UiKit.NewImage(_root, "Track", TrackColor);
            UiKit.Stretch(track.rectTransform, 1f);

            _fill = UiKit.NewImage(_root, "Fill", ChargingColor);
            _fillRect = _fill.rectTransform;
            _fillRect.anchorMin = new Vector2(0f, 0f);
            _fillRect.anchorMax = new Vector2(0f, 1f);
            _fillRect.pivot = new Vector2(0f, 0.5f);
            _fillRect.anchoredPosition = new Vector2(Inset, 0f);
            _fillRect.sizeDelta = new Vector2(0f, -Inset * 2f);

            Image tick = UiKit.NewImage(_root, "Tick", TickColor);
            _tickRect = tick.rectTransform;
            _tickRect.anchorMin = new Vector2(0f, 0f);
            _tickRect.anchorMax = new Vector2(0f, 1f);
            _tickRect.pivot = new Vector2(0.5f, 0.5f);
            _tickRect.sizeDelta = new Vector2(TickWidth, -Inset * 2f);

            _label = UiKit.NewText(_root, "Label", 13, LabelColor, FontStyle.Bold);
            UiKit.Stretch(_label.rectTransform);
        }

        // ── 갱신 ─────────────────────────────────────────

        public void Refresh(BulletTimeController bt, float dt)
        {
            if (_root == null || bt == null) return;

            float ratio = bt.Gauge != null ? bt.Gauge.Ratio : 0f;
            _shown = CardPoseAnimator.Step(_shown, ratio, dt, CardPoseAnimator.GaugeSpeed);

            // 다 찼는데 막대 끝이 미세하게 비어 보이면 준비된 줄 모른다. 근처면 붙여 준다.
            if (Mathf.Abs(_shown - ratio) < 0.002f) _shown = ratio;

            _fillRect.sizeDelta = new Vector2(FillWidth(_shown, InnerWidth), -Inset * 2f);

            float required = bt.RequiredRatio;
            bool showTick = ShowsTick(required);
            _tickRect.gameObject.SetActive(showTick);
            if (showTick)
                _tickRect.anchoredPosition = new Vector2(Inset + TickX(required, InnerWidth), 0f);

            float cooldown = bt.CooldownRemaining;

            if (bt.IsActive)
            {
                _fill.color = ActiveColor;
                _label.text = "불릿타임 진행 중";
            }
            else if (cooldown > 0f)
            {
                _fill.color = CooldownColor;
                _label.text = $"재사용까지 {cooldown:0.0}s";
            }
            else if (bt.IsGaugeReady)
            {
                _fill.color = Color.Lerp(ReadyLowColor, ReadyHighColor, Pulse(Time.unscaledTime));
                _label.text = "불릿타임 준비 — E";
            }
            else
            {
                _fill.color = ChargingColor;
                _label.text = $"불릿타임 게이지 {ratio * 100f:0}%";
            }
        }
    }

    // ══ CardPoseAnimator ═══════════════════════════════════════════

    /// <summary>
    /// 목표 포즈를 향해 미끄러지는 지수 감쇠. 집기와 놓기가 <b>같은 코드 한 줄</b>이 된다 —
    /// 목표만 바꿔 주면 올라갈 때와 내려올 때의 감이 저절로 대칭이 된다.
    ///
    /// <c>Lerp(cur, target, speed * dt)</c>가 아니라 <c>1 - e^(-speed·dt)</c>를 쓴다.
    /// 프레임레이트가 흔들려도 같은 시간이 지나면 같은 자리에 온다 — 반 스텝 두 번이
    /// 한 스텝과 정확히 같다. 그리고 계수가 항상 1 미만이라 목표를 <b>넘어가지 않는다</b>.
    ///
    /// dt는 반드시 <see cref="TimeControl.UnscaledDeltaTime"/>을 넘긴다.
    /// 불릿타임 중에는 <see cref="TimeControl.Scale"/>이 0이라 배율 시간으로는 UI가 얼어붙는다.
    /// </summary>
    public static class CardPoseAnimator
    {
        /// <summary>클수록 빠르게 붙는다. 14면 체감상 0.2초쯤에 자리를 잡는다.</summary>
        public const float DefaultSpeed = 14f;

        /// <summary>게이지 채움용. 카드보다 느리게 흘러야 "빠져나간다"로 읽힌다.</summary>
        public const float GaugeSpeed = 7f;

        /// <summary>이번 프레임에 목표 쪽으로 얼마나 갈지. 0~1.</summary>
        public static float Weight(float dt, float speed)
        {
            if (dt <= 0f || speed <= 0f) return 0f;
            return 1f - Mathf.Exp(-speed * dt);
        }

        public static float Step(float cur, float target, float dt, float speed)
            => Mathf.Lerp(cur, target, Weight(dt, speed));

        public static CardPose Step(in CardPose cur, in CardPose target, float dt, float speed)
        {
            float t = Weight(dt, speed);

            return new CardPose(
                Vector2.Lerp(cur.pos, target.pos, t),
                Mathf.Lerp(cur.angle, target.angle, t),
                Mathf.Lerp(cur.scale, target.scale, t));
        }

        public static CardPose Step(in CardPose cur, in CardPose target, float dt)
            => Step(in cur, in target, dt, DefaultSpeed);

        /// <summary>목표에 사실상 닿았는지. 남은 미동을 끊어 정지 상태를 확정한다.</summary>
        public static bool IsSettled(in CardPose cur, in CardPose target)
            => Vector2.SqrMagnitude(cur.pos - target.pos) < 0.01f &&
               Mathf.Abs(cur.angle - target.angle) < 0.01f &&
               Mathf.Abs(cur.scale - target.scale) < 0.0005f;
    }
}
