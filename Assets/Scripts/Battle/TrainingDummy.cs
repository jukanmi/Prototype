using UnityEngine;

namespace Prototype
{
    /// <summary>
    /// 훈련장 허수아비. <b>죽지 않고, 제자리로 돌아오고, 맞은 값을 센다.</b>
    ///
    /// 콤보 한 싸이클을 몇 번이고 같은 조건에서 굴리기 위한 장치다. 그냥 적을 세워 두면
    /// 세 가지가 매번 어긋난다 — 중간에 죽고, 밀려나 자리가 달라지고, 얼마나 들어갔는지 알 수 없다.
    ///
    /// <b>수치는 손대지 않는다.</b> 무게(<see cref="Physics"/>) · 가드 · 피격 반응은 전부
    /// 프리팹이 들고 오는 값 그대로다 — 보스 프리팹에서 떠 온 몸이라 여기서 잰 넉백 거리가
    /// 보스전에서 그대로 재현된다. 여기서 값을 덮으면 훈련장이 거짓말을 한다.
    /// </summary>
    [RequireComponent(typeof(Combat))]
    public class TrainingDummy : MonoBehaviour
    {
        [Header("불사")]
        [Tooltip("체력을 이 비율 아래로 떨어지지 않게 계속 되돌린다. 0이면 불사를 끈다(죽는 걸 보고 싶을 때).")]
        [Range(0f, 1f)][SerializeField] private float healthFloor = 0.5f;

        [Header("제자리 복귀")]
        [Tooltip("마지막으로 맞은 뒤 이 시간이 지나면 처음 자리로 돌아온다. 0이면 돌아오지 않는다.")]
        [SerializeField] private float returnDelay = 2.5f;

        [Header("계측")]
        [Tooltip("맞기 시작한 뒤 이 시간 동안 새 타격이 없으면 한 싸이클이 끝난 것으로 보고 합계를 찍는다.")]
        [SerializeField] private float cycleGap = 2f;

        private Combat combat;
        private Physics physics;
        private Vector3 homeSpot;

        /// <summary>마지막 피격 이후 흐른 시간. 복귀와 싸이클 마감이 같은 시계를 본다.</summary>
        private float sinceLastHit;

        /// <summary>이번 싸이클에 아직 안 찍은 기록이 남아 있는지.</summary>
        private bool cycleOpen;

        private bool returned;

        // ── 이번 싸이클 계측 ─────────────────────────────

        /// <summary>이번 싸이클에 받은 총 피해.</summary>
        public float CycleDamage { get; private set; }

        /// <summary>이번 싸이클에 맞은 횟수.</summary>
        public int CycleHits { get; private set; }

        /// <summary>첫 타부터 마지막 타까지 걸린 시간(초).</summary>
        public float CycleDuration { get; private set; }

        /// <summary>초당 피해. 싸이클 길이가 0이면 0이다(한 대만 맞은 경우).</summary>
        public float CycleDps => CycleDuration > 0.0001f ? CycleDamage / CycleDuration : 0f;

        /// <summary>직전에 마감된 싸이클의 요약. HUD가 그대로 그려도 되는 한 줄.</summary>
        public string LastCycleSummary { get; private set; } = "대기 중";

        private void Awake()
        {
            combat = GetComponent<Combat>();
            physics = GetComponent<Physics>();
            homeSpot = physics != null ? physics.GroundPosition : transform.position;
        }

        private void OnEnable() => Combat.OnAnyHitLanded += HandleAnyHit;

        private void OnDisable() => Combat.OnAnyHitLanded -= HandleAnyHit;

        /// <summary>
        /// 정지 중에는 아무것도 세지 않아야 한다 — 불릿타임에 머문 시간이 싸이클 길이에 들어가면
        /// DPS가 통째로 거짓이 된다. 그래서 <see cref="TimeControl.DeltaTime"/>을 쓴다.
        /// </summary>
        private void Update()
        {
            float dt = TimeControl.DeltaTime;
            if (dt <= 0f) return;

            // 순서가 중요하다 — 재는 게 먼저, 채우는 게 나중이다.
            // 뒤집으면 이번 프레임에 들어간 피해를 회복이 지워 버려 합계가 0이 된다.
            MeasureDamage();
            KeepAlive();
            lastHealth = combat != null ? combat.Health.CurValue : lastHealth;

            if (!cycleOpen && returned) return;

            sinceLastHit += dt;

            if (cycleOpen && sinceLastHit >= cycleGap) CloseCycle();
            if (!returned && returnDelay > 0f && sinceLastHit >= returnDelay) ReturnHome();
        }

        /// <summary>
        /// 체력을 바닥선 위로 되돌린다. 최대치로 채우지 않는 이유는 체력바가 움직이는 걸
        /// 눈으로 봐야 "지금 들어갔다"가 읽히기 때문이다 — 가득 찬 바는 아무 말도 하지 않는다.
        /// </summary>
        private void KeepAlive()
        {
            if (healthFloor <= 0f || combat == null || combat.IsDead) return;

            Energy hp = combat.Health;
            float floor = hp.MaxValue * healthFloor;

            if (hp.CurValue < floor) hp.Recover(floor - hp.CurValue);
        }

        private void HandleAnyHit(Combat attacker, Combat victim)
        {
            if (victim != combat) return;

            if (!cycleOpen)
            {
                cycleOpen = true;
                CycleDamage = 0f;
                CycleHits = 0;
                CycleDuration = 0f;
            }
            else
            {
                // 첫 타 시점부터의 누적. 마지막 타 이후의 공백은 길이에 넣지 않는다.
                CycleDuration += sinceLastHit;
            }

            CycleHits++;
            sinceLastHit = 0f;
            returned = false;
        }

        /// <summary>직전 프레임 끝의 체력. 피해를 체력 변화로 재기 위한 기준.</summary>
        private float lastHealth = -1f;

        private void Start()
        {
            if (combat != null) lastHealth = combat.Health.CurValue;
        }

        /// <summary>
        /// 이번 프레임에 실제로 들어간 피해. <see cref="Combat.OnAnyHitLanded"/>는 수치를 주지 않으므로
        /// 체력 변화로 잰다 — 방어력 · 보호막 · 피해감소가 <b>다 적용된 뒤</b>의 값이라
        /// 오히려 이쪽이 알고 싶은 값이다.
        /// </summary>
        private void MeasureDamage()
        {
            if (!cycleOpen || combat == null) return;

            float now = combat.Health.CurValue;
            if (now < lastHealth) CycleDamage += lastHealth - now;
        }

        private void CloseCycle()
        {
            cycleOpen = false;

            LastCycleSummary =
                $"{CycleHits}타 · {CycleDamage:0} 피해 · {CycleDuration:0.00}s · DPS {CycleDps:0.0}";

            BattleLog.Log(LogCategory.Combat,
                $"<b>허수아비 싸이클 종료</b> — {LastCycleSummary}", this);
        }

        /// <summary>
        /// 처음 자리로 되돌린다. <see cref="Combat.ClearHitStun"/>을 같이 부르는 이유는
        /// <see cref="Physics.Teleport"/>가 착지 이벤트 없이 지면에 세우기 때문이다 —
        /// 넉백 · 공중피격처럼 착지로만 풀리는 상태가 그대로 굳는다.
        /// </summary>
        private void ReturnHome()
        {
            returned = true;

            if (combat != null) combat.ClearHitStun();
            if (physics != null) physics.Teleport(homeSpot);

            BattleLog.Log(LogCategory.Combat, $"{name} 제자리 복귀 {homeSpot:F1}", this);
        }

        /// <summary>씬에서 허수아비를 옮겼을 때의 새 기준점. 에디터 도구가 부른다.</summary>
        public void SetHome(Vector3 groundSpot) => homeSpot = groundSpot;

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.7f, 0.2f, 0.9f);
            Gizmos.DrawWireSphere(Application.isPlaying ? homeSpot : transform.position, 0.5f);
        }
    }
}
