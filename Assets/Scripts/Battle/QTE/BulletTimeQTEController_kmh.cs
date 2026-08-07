using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Prototype
{
    /// <summary>
    /// 콤보 큐의 슬롯 하나가 발동되기 <b>직전</b>에 끼어드는 타이밍 QTE.
    ///
    /// 흐름: 스킬 발동 전 → QTE(슬로우) → 판정 → 실시간 전투(정배속) → 스킬 발동.
    /// 실시간 전투에 슬로우를 걸어 반응할 시간을 벌어 주고, 제한 시간 안에 E를 누르면
    /// 커서가 존에 들어온 정도로 Miss / Good / Perfect를 가른다.
    ///
    /// 실패해도 페널티는 없다 — 판정은 데미지 배율에만 영향을 준다(<see cref="BulletTimeQTEResult"/>).
    /// <see cref="ComboExecutor"/>가 슬롯을 실행하기 전에 <see cref="Run"/>을 yield해서 부른다.
    /// </summary>
    [RequireComponent(typeof(ComboExecutor))]
    public class BulletTimeQTEController : MonoBehaviour
    {
        [Header("타이밍")]
        [Tooltip("QTE 전체 제한 시간(비배율 초). 커서가 왕복하는 동안 이 시간 안에만 누르면 된다 — 못 누르면 Miss.")]
        [SerializeField] private float timeLimit = 1.6f;
        [Tooltip("커서가 0→1 한 방향으로 이동하는 데 걸리는 시간(비배율 초). 끝에 닿으면 되돌아온다.")]
        [SerializeField] private float sweepDuration = 0.5f;
        [Tooltip("QTE 진행 중 실시간 전투에 걸리는 슬로우 배율. 0에 가까울수록 거의 정지.")]
        [Range(0.01f, 1f)][SerializeField] private float slowScale = 0.15f;
        [Tooltip("판정 직후 결과를 띄워 두는 시간(비배율 초, 정배속 복귀 이후).")]
        [SerializeField] private float resultHoldTime = 0.2f;

        [Header("존")]
        [Tooltip("Perfect 판정 폭(0~1 비율).")]
        [Range(0.01f, 0.3f)][SerializeField] private float perfectWidth = 0.08f;
        [Tooltip("Good 판정 폭(0~1 비율). Perfect보다 커야 한다.")]
        [Range(0.05f, 0.6f)][SerializeField] private float goodWidth = 0.24f;
        [Tooltip("존 중심이 나올 수 있는 범위. 커서가 지나가자마자 걸리는 양 끝은 피한다.")]
        [SerializeField] private Vector2 zoneCenterRange = new Vector2(0.3f, 0.85f);

        [Header("배율")]
        [SerializeField] private float perfectMultiplier = 1.35f;
        [SerializeField] private float goodMultiplier = 1.15f;
        [SerializeField] private float missMultiplier = 1f;

        [Header("참조")]
        [Tooltip("비워두면 자동으로 찾거나 새로 붙인다. 코드로만 짓는 UI라 씬 연결이 필요 없다.")]
        [SerializeField] private BulletTimeQTEUI ui;

        /// <summary>가장 최근 <see cref="Run"/> 호출의 결과.</summary>
        public BulletTimeQTEResult LastResult { get; private set; }

        private void Awake()
        {
            if (ui == null) ui = GetComponentInChildren<BulletTimeQTEUI>();
            if (ui == null) ui = gameObject.AddComponent<BulletTimeQTEUI>();
        }

        /// <summary>
        /// 스킬 한 장이 발동하기 전에 호출. 끝나면 <see cref="LastResult"/>에 판정이 담긴다.
        /// 진행 중에는 <see cref="TimeControl.Scale"/>을 낮춰 실시간 전투에 슬로우를 건다.
        /// </summary>
        public IEnumerator Run(SkillData data, Ally caster)
        {
            float zoneCenter = Random.Range(zoneCenterRange.x, zoneCenterRange.y);

            ui.Show(data, caster, zoneCenter, perfectWidth, goodWidth);

            float prevScale = TimeControl.Scale;
            TimeControl.Scale = slowScale;

            // 콤보를 실행시킨 E 입력이 같은 프레임 안에서 그대로 넘어와 첫 슬롯의 QTE를
            // 경과 0초에 즉시 통과시켜 버리는 것을 막는다 — 시작 프레임의 입력은 무시한다.
            // (Order → Resolve 진입과 Executor.Execute → StartCoroutine이 전부 한 프레임 안에서
            //  동기적으로 이어지기 때문에 첫 슬롯만 이 문제가 생긴다.)
            int startFrame = Time.frameCount;

            float sweep = Mathf.Max(0.01f, sweepDuration);

            BulletTimeQTETier tier = BulletTimeQTETier.Miss;
            float elapsed = 0f;

            while (elapsed < timeLimit)
            {
                // 0→1→0→1... 왕복. 한 번 존을 지나쳐도 시간이 남아 있으면 되돌아오며 다시 노릴 수 있다.
                float t = Mathf.PingPong(elapsed / sweep, 1f);
                ui.SetCursor(t);

                bool pressed = Time.frameCount != startFrame
                    && Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame;

                if (pressed)
                {
                    tier = Evaluate(t, zoneCenter);
                    break;
                }

                elapsed += TimeControl.UnscaledDeltaTime;
                yield return null;
            }

            // 슬로우는 판정이 나는 즉시 풀어 준다 — "QTE 끝 → 정배속 → 스킬 발동" 순서.
            TimeControl.Scale = prevScale;

            LastResult = new BulletTimeQTEResult(tier, MultiplierOf(tier));

            BattleLog.Log(LogCategory.Qte,
                $"<b>QTE</b> {(data != null ? data.skillName : "?")} | {BattleLog.Name(caster)} → " +
                $"{tier} (x{LastResult.damageMultiplier:0.##})", this);

            ui.ShowResult(tier);

            float hold = 0f;
            while (hold < resultHoldTime)
            {
                hold += TimeControl.UnscaledDeltaTime;
                yield return null;
            }

            ui.Hide();
        }

        private BulletTimeQTETier Evaluate(float t, float zoneCenter)
        {
            float d = Mathf.Abs(t - zoneCenter);
            if (d <= perfectWidth * 0.5f) return BulletTimeQTETier.Perfect;
            if (d <= goodWidth * 0.5f) return BulletTimeQTETier.Good;
            return BulletTimeQTETier.Miss;
        }

        private float MultiplierOf(BulletTimeQTETier tier)
        {
            switch (tier)
            {
                case BulletTimeQTETier.Perfect: return perfectMultiplier;
                case BulletTimeQTETier.Good: return goodMultiplier;
                default: return missMultiplier;
            }
        }
    }
}
