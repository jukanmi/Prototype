using UnityEngine.UI;
using UnityEngine;

namespace Prototype
{
    // ══ BulletTimeGaugeWidget ═══════════════════════════════════════════

    /// <summary>
    /// 총공격 게이지 막대.
    ///
    /// MonoBehaviour가 아니다. <see cref="BulletTimeGaugeUI"/>가 만들고 매 프레임 먹인다.
    /// 계산 부분(<see cref="FillWidth"/> · <see cref="TickX"/> · <see cref="Chase"/>)은 순수 함수라 씬 없이 검증된다.
    /// </summary>
    public class BulletTimeGaugeWidget
    {
        public const float Width = 420f;
        public const float Height = 22f;

        /// <summary>트랙 안쪽 여백. 이만큼이 테두리로 남는다.</summary>
        public const float Inset = 2f;

        public const float TickWidth = 2f;

        /// <summary>표시 비율이 실제 비율을 뒤쫓는 빠르기(1/s). 느려야 소모가 "쭉 빠지는" 연출로 읽힌다.</summary>
        public const float ChaseSpeed = 7f;

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

        /// <summary>발동 임계 눈금이 설 자리. 채움 영역 <b>왼쪽 끝</b>에서의 거리다.</summary>
        public static float TickX(float requiredRatio, float fullWidth)
            => Mathf.Clamp01(requiredRatio) * Mathf.Max(0f, fullWidth);

        /// <summary>임계선을 그릴 만한 값인지. 100%면 막대 끝과 겹쳐 눈금이 의미가 없다.</summary>
        public static bool ShowsTick(float requiredRatio)
            => requiredRatio > 0.02f && requiredRatio < 0.99f;

        /// <summary>0~1을 오가는 맥박. 준비 완료를 눈에 띄게 만든다.</summary>
        public static float Pulse(float unscaledTime)
            => 0.5f + 0.5f * Mathf.Sin(unscaledTime * 6f);

        /// <summary>
        /// <paramref name="cur"/>에서 <paramref name="target"/> 쪽으로 이번 프레임만큼 다가간 값.
        ///
        /// <c>Lerp(cur, target, speed * dt)</c>가 아니라 <c>1 - e^(-speed·dt)</c>를 쓴다 —
        /// 프레임레이트가 흔들려도 같은 시간이 지나면 같은 자리에 오고, 계수가 항상 1 미만이라
        /// 목표를 <b>넘어가지 않는다</b>.
        /// </summary>
        public static float Chase(float cur, float target, float dt)
        {
            if (dt <= 0f) return cur;
            return Mathf.Lerp(cur, target, 1f - Mathf.Exp(-ChaseSpeed * dt));
        }

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
            _shown = Chase(_shown, ratio, dt);

            // 다 찼는데 막대 끝이 미세하게 비어 보이면 준비된 줄 모른다. 근처면 붙여 준다.
            if (Mathf.Abs(_shown - ratio) < 0.002f) _shown = ratio;

            _fillRect.sizeDelta = new Vector2(FillWidth(_shown, InnerWidth), -Inset * 2f);

            float required = bt.RequiredRatio;
            bool showTick = ShowsTick(required);
            _tickRect.gameObject.SetActive(showTick);
            if (showTick)
                _tickRect.anchoredPosition = new Vector2(Inset + TickX(required, InnerWidth), 0f);

            float cooldown = bt.CooldownRemaining;

            if (bt.Phase == TacticPhase.Resolve)
            {
                _fill.color = ActiveColor;
                _label.text = "총공격 진행 중";
            }
            else if (cooldown > 0f)
            {
                _fill.color = CooldownColor;
                _label.text = $"재사용까지 {cooldown:0.0}s";
            }
            else if (bt.IsGaugeReady)
            {
                _fill.color = Color.Lerp(ReadyLowColor, ReadyHighColor, Pulse(Time.unscaledTime));
                _label.text = "총공격 준비 — 적 등장 시 자동 발동";
            }
            else
            {
                _fill.color = ChargingColor;
                _label.text = $"총공격 게이지 {ratio * 100f:0}%";
            }
        }
    }
}
