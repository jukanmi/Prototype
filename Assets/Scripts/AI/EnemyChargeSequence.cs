using UnityEngine;

namespace Prototype
{
    /// <summary>돌진의 진행 단계.</summary>
    public enum EnemyChargePhase
    {
        Idle,
        /// <summary>예고. 제자리에서 타겟을 계속 노려본다 — 플레이어가 피할 창.</summary>
        Telegraph,
        /// <summary>돌진. 예고가 끝난 순간의 방향으로 직진한다.</summary>
        Charging,
        /// <summary>후딜. 반격당하는 구간.</summary>
        Recovery,
    }

    /// <summary>
    /// 돌진의 <b>시간 축</b>만 담당하는 순수 객체. MonoBehaviour도 물리도 모른다.
    /// 실행(회전·대쉬·히트박스)은 <see cref="EnemyChargeAction"/>이 이 단계를 보고 한다.
    /// </summary>
    public class EnemyChargeSequence
    {
        private readonly float telegraphDuration;
        private readonly float chargeDuration;
        private readonly float recoveryDuration;

        private float timer;

        public EnemyChargeSequence(float telegraph, float charge, float recovery)
        {
            telegraphDuration = Mathf.Max(0f, telegraph);
            chargeDuration = Mathf.Max(0f, charge);
            recoveryDuration = Mathf.Max(0f, recovery);
        }

        public EnemyChargePhase Phase { get; private set; } = EnemyChargePhase.Idle;

        /// <summary>Charging 진입 순간에 한 번만 고정되는 진행 방향. 정규화되어 있다.</summary>
        public Vector3 LockedDirection { get; private set; }

        /// <summary>현재 단계에서 흐른 시간. 연출 보간에 쓴다.</summary>
        public float PhaseTime => timer;

        public bool IsRunning => Phase != EnemyChargePhase.Idle;

        public void Begin()
        {
            Phase = EnemyChargePhase.Telegraph;
            timer = 0f;
            LockedDirection = Vector3.zero;
        }

        /// <summary>
        /// liveDirection은 예고 중에만 쓴다 — 돌진에 들어가면 무시한다.
        /// 유도되는 돌진은 피할 방법이 없어진다.
        /// </summary>
        public void Tick(float dt, Vector3 liveDirection)
        {
            if (Phase == EnemyChargePhase.Idle) return;

            timer += dt;

            switch (Phase)
            {
                case EnemyChargePhase.Telegraph:
                    if (timer < telegraphDuration) return;

                    LockedDirection = Normalize(liveDirection);
                    Enter(EnemyChargePhase.Charging);
                    return;

                case EnemyChargePhase.Charging:
                    if (timer >= chargeDuration) Enter(EnemyChargePhase.Recovery);
                    return;

                case EnemyChargePhase.Recovery:
                    if (timer >= recoveryDuration) Enter(EnemyChargePhase.Idle);
                    return;
            }
        }

        /// <summary>몸통이 무언가에 닿았다. 돌진 중일 때만 후딜로 끊는다.</summary>
        public void HitOrWall()
        {
            if (Phase != EnemyChargePhase.Charging) return;
            Enter(EnemyChargePhase.Recovery);
        }

        /// <summary>피격·사망·AI 정지. 어느 단계든 즉시 끝낸다.</summary>
        public void Cancel()
        {
            Enter(EnemyChargePhase.Idle);
        }

        private void Enter(EnemyChargePhase next)
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
