using UnityEngine;
using UnityEngine.UI;

namespace Prototype
{
    /// <summary>
    /// 화면 <b>오른쪽</b>에 "지금 콤보 몇 타 · 누적 몇 딜"을 띄운다.
    ///
    /// 훈련장은 콘솔에 싸이클 합계를 찍지만(<see cref="TrainingDummy"/>) 그건 다 끝난 뒤에
    /// 뒤돌아보는 숫자다. 콤보를 <b>굴리는 동안</b> 몇 대째인지 안 보이면
    /// 어디서 끊겼는지 눈으로 못 잡는다. 그래서 실시간으로 같은 값을 화면에 올린다.
    ///
    /// 계산은 <see cref="ComboMeter"/>가 전부 한다 — 여기는 그리기만 한다.
    /// 아군이 적에게 넣은 피해만 센다.
    /// </summary>
    public class ComboDamageHUD : MonoBehaviour
    {
        [Header("판정")]
        [Tooltip("마지막 타격 뒤 이 시간 동안 아무것도 안 맞으면 콤보가 끝난다(초).")]
        [SerializeField] private float comboWindow = 2.5f;

        [Tooltip("콤보가 끝난 뒤 숫자를 남겨 두는 시간(초). 마지막 값을 읽을 여유.")]
        [SerializeField] private float lingerDuration = 1.5f;

        [Header("배치")]
        [Tooltip("화면 오른쪽 끝에서 띄우는 거리(1920×1080 기준).")]
        [SerializeField] private float rightMargin = 56f;

        [Tooltip("화면 세로 중앙에서 올리는 거리. 양수면 위로.")]
        [SerializeField] private float verticalOffset = 90f;

        [Tooltip("끄면 패널을 아예 만들지 않는다. 시연 녹화 때 화면을 비우는 용도.")]
        [SerializeField] private bool show = true;

        private readonly ComboMeter meter = new ComboMeter();

        private GameObject panel;
        private CanvasGroup group;
        private Text hitsLabel;
        private Text hitsSuffix;
        private Text damageLabel;
        private Text detailLabel;

        /// <summary>지금 세고 있는 값. 테스트와 다른 HUD가 같은 수치를 읽는다.</summary>
        public ComboMeter Meter => meter;

        /// <summary>패널이 화면에 켜져 있는지(<see cref="RecentHitEnemyHUD.IsVisible"/>과 같은 선례).</summary>
        public bool IsVisible => panel != null && panel.activeSelf;

        /// <summary>지금 그려지고 있는 타수. 테스트가 UI까지 갔는지 확인할 때 읽는다.</summary>
        public string HitsText => hitsLabel != null ? hitsLabel.text : string.Empty;

        /// <summary>지금 그려지고 있는 누적 피해.</summary>
        public string DamageText => damageLabel != null ? damageLabel.text : string.Empty;

        // ── 색 ───────────────────────────────────────────
        // 타수는 흰색으로 두고 <b>피해만</b> 색을 준다. 둘 다 물들이면 어느 쪽이 중요한지 안 읽힌다.
        private static readonly Color HitsColor = new Color(1f, 1f, 1f, 1f);
        private static readonly Color DamageColor = new Color32(0xFF, 0xB0, 0x4D, 0xFF);
        private static readonly Color DetailColor = new Color(1f, 1f, 1f, 0.62f);
        private static readonly Color SuffixColor = new Color(1f, 1f, 1f, 0.75f);

        private void Awake()
        {
            meter.Window = comboWindow;
            meter.LingerDuration = lingerDuration;

            if (show) EnsureBuilt();
        }

        private void OnEnable() => Combat.OnAnyDamageDealt += HandleDamage;

        private void OnDisable() => Combat.OnAnyDamageDealt -= HandleDamage;

        /// <summary>
        /// 스케일된 dt로 굴린다. 불릿타임에 머문 시간이 콤보 길이에 들어가면
        /// DPS가 거짓이 되고, 카드를 정렬하는 동안 콤보가 혼자 끊긴다.
        /// </summary>
        private void Update()
        {
            meter.Tick(TimeControl.DeltaTime);
            Redraw();
        }

        /// <summary>
        /// 아군이 적에게 넣은 피해만 센다. 적이 아군을 때린 것까지 세면
        /// "내 콤보"가 아니라 그냥 난전 카운터가 된다.
        /// </summary>
        private void HandleDamage(Combat attacker, Combat victim, float amount)
        {
            if (attacker?.Owner?.Faction != Faction.Ally) return;
            if (victim?.Owner?.Faction != Faction.Enemy) return;

            meter.AddHit(amount);

            if (show) EnsureBuilt();
            Redraw();
        }

        /// <summary>바깥에서 통째로 지운다. 스테이지 종료 · 재시작이 부를 자리.</summary>
        public void ResetCombo()
        {
            meter.Reset();
            Redraw();
        }

        // ── 그리기 ───────────────────────────────────────

        private void Redraw()
        {
            if (panel == null) return;

            bool visible = meter.IsVisible;
            if (panel.activeSelf != visible) panel.SetActive(visible);
            if (!visible) return;

            group.alpha = meter.Alpha;

            hitsLabel.text = meter.Hits.ToString();
            damageLabel.text = Mathf.RoundToInt(meter.Damage).ToString();

            // 이어지는 중에는 DPS가 매 프레임 요동친다. 끝난 뒤에만 확정값으로 보여 준다.
            detailLabel.text = meter.IsRunning
                ? $"{meter.Duration:0.0}s"
                : $"{meter.Duration:0.00}s · DPS {meter.Dps:0}";
        }

        // ── UI 조립 ──────────────────────────────────────

        /// <summary>
        /// 캔버스가 아직 없으면 짓는다. Awake에 의존하지 않는 이유는
        /// 에디트모드 테스트가 <see cref="HandleDamage"/>를 곧바로 부르는 경로가 있기 때문이다
        /// (<see cref="SkillCutinUI.EnsureBuilt"/>와 같은 선례).
        /// </summary>
        private void EnsureBuilt()
        {
            if (panel != null) return;

            BuildUI();
            panel.SetActive(false);
        }

        private void BuildUI()
        {
            var canvasGo = new GameObject("ComboDamageCanvas",
                                          typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            canvasGo.transform.SetParent(transform, false);

            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            // RecentHitEnemyHUD(2) 위, SkillCutinUI(10) 아래 — 컷인이 콤보 숫자를 덮어야 한다.
            canvas.sortingOrder = 5;

            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 1f;

            panel = new GameObject("ComboDamagePanel", typeof(RectTransform), typeof(CanvasGroup));
            panel.transform.SetParent(canvasGo.transform, false);

            group = panel.GetComponent<CanvasGroup>();
            group.interactable = false;
            group.blocksRaycasts = false;

            // 오른쪽 세로 중앙에 붙인다. 손패(하단)와 적 체력바(하단)를 피하는 유일한 빈 자리다.
            var rect = (RectTransform)panel.transform;
            rect.anchorMin = new Vector2(1f, 0.5f);
            rect.anchorMax = new Vector2(1f, 0.5f);
            rect.pivot = new Vector2(1f, 0.5f);
            rect.sizeDelta = new Vector2(420f, 220f);
            rect.anchoredPosition = new Vector2(-rightMargin, verticalOffset);

            // ── 타수: 숫자와 단위를 따로 둔다. 붙여 쓰면 자릿수가 늘 때 단위가 밀린다.
            hitsLabel = Label("Hits", 118, HitsColor, FontStyle.Bold, TextAnchor.LowerRight);
            Place(hitsLabel, new Vector2(-92f, 62f), new Vector2(320f, 130f));

            hitsSuffix = Label("HitsSuffix", 34, SuffixColor, FontStyle.Bold, TextAnchor.LowerRight);
            hitsSuffix.text = "HIT";
            Place(hitsSuffix, new Vector2(0f, 74f), new Vector2(88f, 44f));

            // ── 누적 피해: 이 화면에서 유일하게 색을 쓰는 자리.
            damageLabel = Label("Damage", 56, DamageColor, FontStyle.Bold, TextAnchor.UpperRight);
            Place(damageLabel, new Vector2(0f, 8f), new Vector2(420f, 66f));

            // ── 시간 · DPS
            detailLabel = Label("Detail", 26, DetailColor, FontStyle.Normal, TextAnchor.UpperRight);
            Place(detailLabel, new Vector2(0f, -52f), new Vector2(420f, 36f));
        }

        private Text Label(string name, int size, Color color, FontStyle style, TextAnchor anchor)
        {
            Text t = UiFactory.NewText(panel.transform, name, size, color, style);
            t.alignment = anchor;

            // 밝은 배경 위에서도 읽히도록 검은 외곽선을 깐다(SkillCutinUI와 같은 처리).
            var outline = t.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.85f);
            outline.effectDistance = new Vector2(2f, -2f);

            return t;
        }

        private static void Place(Text text, Vector2 position, Vector2 size)
        {
            var rect = (RectTransform)text.transform;
            rect.anchorMin = new Vector2(1f, 0.5f);
            rect.anchorMax = new Vector2(1f, 0.5f);
            rect.pivot = new Vector2(1f, 0.5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
        }

        /// <summary>인스펙터에서 음수를 넣어도 판정이 뒤집히지 않게 막는다.</summary>
        private void OnValidate()
        {
            comboWindow = Mathf.Max(0.1f, comboWindow);
            lingerDuration = Mathf.Max(0f, lingerDuration);

            meter.Window = comboWindow;
            meter.LingerDuration = lingerDuration;
        }
    }
}
