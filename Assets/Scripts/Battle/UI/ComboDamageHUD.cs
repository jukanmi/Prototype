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
    ///
    /// <b>화면은 프리팹이 쥔다.</b> 캔버스 · 패널 · 글자 넷은 인스펙터에서 배선한다 —
    /// 자리와 크기를 바꾸려면 코드가 아니라 프리팹을 연다.
    /// </summary>
    public class ComboDamageHUD : MonoBehaviour
    {
        [Header("판정")]
        [Tooltip("마지막 타격 뒤 이 시간 동안 아무것도 안 맞으면 콤보가 끝난다(초).")]
        [SerializeField] private float comboWindow = 2.5f;

        [Tooltip("콤보가 끝난 뒤 숫자를 남겨 두는 시간(초). 마지막 값을 읽을 여유.")]
        [SerializeField] private float lingerDuration = 1.5f;

        [Header("배선")]
        [Tooltip("켜고 끄는 대상. 이 오브젝트에 CanvasGroup이 붙어 있어야 페이드가 먹는다.")]
        [SerializeField] private GameObject panel;

        [Tooltip("panel의 CanvasGroup. 콤보가 끝난 뒤 알파로 사라진다.")]
        [SerializeField] private CanvasGroup group;

        [Tooltip("타수 숫자.")]
        [SerializeField] private Text hitsLabel;

        [Tooltip("누적 피해.")]
        [SerializeField] private Text damageLabel;

        [Tooltip("경과 시간 · DPS.")]
        [SerializeField] private Text detailLabel;

        [Tooltip("끄면 패널을 아예 안 켠다. 시연 녹화 때 화면을 비우는 용도.")]
        [SerializeField] private bool show = true;

        private readonly ComboMeter meter = new ComboMeter();

        /// <summary>배선이 빈 채로 돌 때 경고를 한 번만 낸다. 매 프레임 찍으면 콘솔이 잠긴다.</summary>
        private bool warned;

        /// <summary>지금 세고 있는 값. 테스트와 다른 HUD가 같은 수치를 읽는다.</summary>
        public ComboMeter Meter => meter;

        /// <summary>패널이 화면에 켜져 있는지(<see cref="RecentHitEnemyHUD.IsVisible"/>과 같은 선례).</summary>
        public bool IsVisible => panel != null && panel.activeSelf;

        /// <summary>지금 그려지고 있는 타수. 테스트가 UI까지 갔는지 확인할 때 읽는다.</summary>
        public string HitsText => hitsLabel != null ? hitsLabel.text : string.Empty;

        /// <summary>지금 그려지고 있는 누적 피해.</summary>
        public string DamageText => damageLabel != null ? damageLabel.text : string.Empty;

        private void Awake()
        {
            meter.Window = comboWindow;
            meter.LingerDuration = lingerDuration;

            // 프리팹은 글자가 보이는 채로 저장돼 있다(그래야 에디터에서 배치를 본다).
            // 켜지는 건 첫 타격부터다.
            if (panel != null) panel.SetActive(false);
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
            if (!show)
            {
                if (panel != null && panel.activeSelf) panel.SetActive(false);
                return;
            }

            if (panel == null || group == null || hitsLabel == null
                || damageLabel == null || detailLabel == null)
            {
                if (warned) return;

                warned = true;
                Debug.LogWarning(
                    "[ComboDamageHUD] 배선이 비어 있다 — 콤보 숫자가 안 뜬다. " +
                    "CombatManager 프리팹의 ComboDamageCanvas 배선을 확인할 것.", this);
                return;
            }

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
