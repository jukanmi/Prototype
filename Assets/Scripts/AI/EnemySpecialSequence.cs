using UnityEngine;

namespace Prototype
{
    /// <summary>특수 행동의 진행 단계.</summary>
    public enum EnemySpecialPhase
    {
        Idle,
        /// <summary>
        /// 차징. 제자리에서 힘을 모은다 — 머리 위 게이지가 그 양을 보여 준다.
        /// 예고와 달리 <b>길고, 밀 수 있다</b>(<see cref="EnemySpecialSequence.Delay"/>).
        /// 차징 길이가 0인 패턴은 이 단계를 아예 건너뛴다.
        /// </summary>
        Charge,
        /// <summary>예고. 제자리에서 타겟을 계속 노려본다 — 플레이어가 피할 창.</summary>
        Telegraph,
        /// <summary>발동. 예고가 끝난 순간의 방향으로 판정이 나간다.</summary>
        Active,
        /// <summary>후딜. 반격당하는 구간.</summary>
        Recovery,
    }

    /// <summary>
    /// 특수 행동의 <b>시간 축</b>만 담당하는 순수 객체. MonoBehaviour도 물리도 모른다.
    /// 실행(회전·이동·히트박스)은 <see cref="IEnemySpecialAction"/> 구현체가 이 단계를 보고 한다.
    ///
    /// 돌진 · 내려찍기 · 연타가 전부 같은 3단(예고→발동→후딜) 골격이라 한 벌만 둔다.
    /// 길이는 <see cref="Begin(float,float,float)"/>로 시작할 때마다 갈아끼울 수 있어
    /// 패턴마다 다른 타이밍을 쓰면서도 타이머 구현이 늘지 않는다.
    /// </summary>
    public class EnemySpecialSequence
    {
        private float chargeDuration;
        private float telegraphDuration;
        private float activeDuration;
        private float recoveryDuration;

        private float timer;

        /// <summary>길이를 시작할 때마다 넘길 경우.</summary>
        public EnemySpecialSequence() { }

        public EnemySpecialSequence(float telegraph, float active, float recovery)
        {
            SetDurations(0f, telegraph, active, recovery);
        }

        public EnemySpecialPhase Phase { get; private set; } = EnemySpecialPhase.Idle;

        /// <summary>Active 진입 순간에 한 번만 고정되는 진행 방향. 정규화되어 있다.</summary>
        public Vector3 LockedDirection { get; private set; }

        /// <summary>현재 단계에서 흐른 시간. 연출 보간과 다단히트 간격에 쓴다.</summary>
        public float PhaseTime => timer;

        /// <summary>
        /// 예고 표시(!)를 켤 구간인가. 특수 행동은 예고 단계가 곧 예고다.
        ///
        /// <b>차징 단계는 켜지 않는다.</b> "!"는 "지금 피해라"라는 뜻으로 고정해 둬야
        /// <see cref="CombatStateRules.TelegraphLead"/>에 맞춘 패링 타이밍을 익힐 수 있다.
        /// 차징을 알리는 건 머리 위 게이지다.
        /// </summary>
        public bool ShouldShowTelegraph => Phase == EnemySpecialPhase.Telegraph;

        /// <summary>
        /// 차징 진행도 0~1. 차징 중이 아니면 0이다 —
        /// 게이지가 이 값을 보고 그릴지 말지를 정하므로 예고로 넘어간 뒤엔 사라져야 한다.
        /// </summary>
        public float ChargeProgress => Phase == EnemySpecialPhase.Charge && chargeDuration > 0f
            ? Mathf.Clamp01(timer / chargeDuration)
            : 0f;

        public float ChargeDuration => chargeDuration;
        public float TelegraphDuration => telegraphDuration;
        public float ActiveDuration => activeDuration;
        public float RecoveryDuration => recoveryDuration;

        public bool IsRunning => Phase != EnemySpecialPhase.Idle;

        /// <summary>현재 단계의 진행도 0~1. 길이가 0인 단계는 항상 1이다.</summary>
        public float PhaseProgress
        {
            get
            {
                float len = CurrentDuration();
                return len <= 0f ? 1f : Mathf.Clamp01(timer / len);
            }
        }

        /// <summary>
        /// 차징 길이가 0이면 예고부터 시작한다 — 차징을 안 쓰는 패턴은
        /// 이 클래스가 생기기 전과 똑같은 경로를 탄다.
        /// </summary>
        public void Begin()
        {
            Phase = chargeDuration > 0f ? EnemySpecialPhase.Charge : EnemySpecialPhase.Telegraph;
            timer = 0f;
            LockedDirection = Vector3.zero;
        }

        /// <summary>이번에 쓸 길이를 실어 시작한다. 패턴마다 타이밍이 다를 때.</summary>
        public void Begin(float telegraph, float active, float recovery)
        {
            Begin(0f, telegraph, active, recovery);
        }

        /// <summary>차징까지 실어 시작한다.</summary>
        public void Begin(float charge, float telegraph, float active, float recovery)
        {
            SetDurations(charge, telegraph, active, recovery);
            Begin();
        }

        /// <summary>
        /// liveDirection은 예고 중에만 쓴다 — 발동에 들어가면 무시한다.
        /// 유도되는 돌진은 피할 방법이 없어진다.
        /// </summary>
        public void Tick(float dt, Vector3 liveDirection)
        {
            if (Phase == EnemySpecialPhase.Idle) return;

            timer += dt;

            switch (Phase)
            {
                case EnemySpecialPhase.Charge:
                    if (timer >= chargeDuration) Enter(EnemySpecialPhase.Telegraph);
                    return;

                case EnemySpecialPhase.Telegraph:
                    if (timer < telegraphDuration) return;

                    LockedDirection = Normalize(liveDirection);
                    Enter(EnemySpecialPhase.Active);
                    return;

                case EnemySpecialPhase.Active:
                    if (timer >= activeDuration) Enter(EnemySpecialPhase.Recovery);
                    return;

                case EnemySpecialPhase.Recovery:
                    if (timer >= recoveryDuration) Enter(EnemySpecialPhase.Idle);
                    return;
            }
        }

        /// <summary>
        /// 몸통이 무언가에 닿았다. 발동 중일 때만 후딜로 끊는다.
        /// 제자리 패턴은 이걸 구독하지 않는다 — 첫 타 적중이 자기 공격을 끊어 버린다.
        /// </summary>
        public void HitOrWall()
        {
            if (Phase != EnemySpecialPhase.Active) return;
            Enter(EnemySpecialPhase.Recovery);
        }

        /// <summary>차징을 즉시 끝내고 예고로 넘긴다. 외부에서 강제로 터뜨릴 때.</summary>
        public void ReleaseCharge()
        {
            if (Phase != EnemySpecialPhase.Charge) return;

            Enter(EnemySpecialPhase.Telegraph);
        }

        /// <summary>피격·사망·AI 정지. 어느 단계든 즉시 끝낸다.</summary>
        public void Cancel()
        {
            Enter(EnemySpecialPhase.Idle);
        }

        private void SetDurations(float charge, float telegraph, float active, float recovery)
        {
            chargeDuration = Mathf.Max(0f, charge);
            telegraphDuration = Mathf.Max(0f, telegraph);
            activeDuration = Mathf.Max(0f, active);
            recoveryDuration = Mathf.Max(0f, recovery);
        }

        private float CurrentDuration()
        {
            switch (Phase)
            {
                case EnemySpecialPhase.Charge: return chargeDuration;
                case EnemySpecialPhase.Telegraph: return telegraphDuration;
                case EnemySpecialPhase.Active: return activeDuration;
                case EnemySpecialPhase.Recovery: return recoveryDuration;
                default: return 0f;
            }
        }

        private void Enter(EnemySpecialPhase next)
        {
            Phase = next;
            timer = 0f;
        }

        private static Vector3 Normalize(Vector3 dir)
        {
            dir.y = 0f;
            return dir.sqrMagnitude > 0.0001f ? dir.normalized : Vector3.forward;
        }
    }
}
