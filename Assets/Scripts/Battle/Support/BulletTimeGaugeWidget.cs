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
        // 초상화 옆, 체력바 바로 밑에 테두리를 맞대고 붙는다. 체력보다 얇고 짧다(HudLayout).
        public const float Width = HudLayout.GaugeWidth;
        public const float Height = HudLayout.GaugeHeight;

        /// <summary>트랙 안쪽 여백. 이만큼이 테두리로 남는다 — 체력바 테두리와 같은 두께.</summary>
        public const float Inset = HudLayout.Border;

        public const float TickWidth = 2f;

        /// <summary>표시 비율이 실제 비율을 뒤쫓는 빠르기(1/s). 느려야 소모가 "쭉 빠지는" 연출로 읽힌다.</summary>
        public const float ChaseSpeed = 7f;

        /// <summary>채움이 실제로 그려지는 폭. 테두리를 뺀 값이다.</summary>
        public static float InnerWidth => Width - Inset * 2f;

        private static readonly Color TrackColor = UiTheme.InkDeep;
        // 위의 체력 막대와 같은 검은 테두리 — 한 묶음으로 읽히게.
        private static readonly Color BorderColor = UiTheme.OutlineColor.WithAlpha(0.95f);
        private static readonly Color ChargingColor = UiTheme.Gauge.Shade(0.55f);
        private static readonly Color ReadyLowColor = UiTheme.Gauge;
        private static readonly Color ReadyHighColor = UiTheme.Gauge.Shade(1.35f);
        private static readonly Color ActiveColor = UiTheme.Gauge.Shade(1.5f);
        private static readonly Color CooldownColor = UiTheme.TextOff;
        private static readonly Color TickColor = UiTheme.Gold.WithAlpha(0.85f);
        private static readonly Color LabelColor = UiTheme.TextMain;

        private RectTransform _root;
        private SlantedBar _fill;
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

        /// <summary>
        /// 테두리 · 홈 · 채움이 전부 같은 각으로 꺾인 <see cref="SlantedBar"/>다 — 위의 체력바와 한 모양이다.
        /// 채움은 폭을 줄이지 않고 <see cref="SlantedBar.Fill"/>로 자른다. 폭을 줄이면 꺾인 끝이 함께 움직인다.
        /// </summary>
        public void Build(Transform parent)
        {
            SlantedBar border = NewBar(parent, "GaugeBar", BorderColor);
            _root = border.rectTransform;
            _root.sizeDelta = new Vector2(Width, Height);

            SlantedBar track = NewBar(_root, "Track", TrackColor);
            UiKit.Stretch(track.rectTransform, Inset);

            _fill = NewBar(_root, "Fill", ChargingColor);
            UiKit.Stretch(_fill.rectTransform, Inset);
            _fill.Fill = 0f;

            Image tick = UiKit.NewImage(_root, "Tick", TickColor);
            _tickRect = tick.rectTransform;
            _tickRect.anchorMin = new Vector2(0f, 0f);
            _tickRect.anchorMax = new Vector2(0f, 1f);
            _tickRect.pivot = new Vector2(0.5f, 0.5f);
            _tickRect.sizeDelta = new Vector2(TickWidth, -Inset * 2f);

            _label = UiKit.NewText(_root, "Label", 13, LabelColor, FontStyle.Bold);
            UiKit.Stretch(_label.rectTransform);
            // 꺾인 끝만큼 오른쪽을 비워 글자가 곧은 구간 가운데 오게 한다.
            _label.rectTransform.offsetMax = new Vector2(-Height * HudLayout.SlantRatio, 0f);
            // 외곽선은 NewText 가 기본으로 붙인다 — 준비 완료 때 밝게 맥동하는 채움 위에서도 읽힌다.
        }

        private static SlantedBar NewBar(Transform parent, string name, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(SlantedBar));
            go.transform.SetParent(parent, false);

            SlantedBar bar = go.GetComponent<SlantedBar>();
            bar.color = color;
            bar.raycastTarget = false;
            bar.SlantRatio = HudLayout.SlantRatio;
            return bar;
        }

        // ── 갱신 ─────────────────────────────────────────

        public void Refresh(BulletTimeController bt, float dt)
        {
            if (_root == null || bt == null) return;

            float ratio = bt.Gauge != null ? bt.Gauge.Ratio : 0f;
            _shown = Chase(_shown, ratio, dt);

            // 다 찼는데 막대 끝이 미세하게 비어 보이면 준비된 줄 모른다. 근처면 붙여 준다.
            if (Mathf.Abs(_shown - ratio) < 0.002f) _shown = ratio;

            _fill.Fill = _shown;

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
