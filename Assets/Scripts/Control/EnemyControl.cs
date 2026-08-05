using UnityEngine;

namespace Prototype
{
    /// <summary>
    /// 적 제어. 감지 · 타이머 · 게이트를 맡고 <b>판단은 브레인에 위임</b>한다.
    /// 종류별 행동 차이는 브레인 애셋을 갈아끼워 만든다.
    /// 스테이지 종료 시 <see cref="SetActive"/>(false)로 정지시킨다.
    /// </summary>
    public class EnemyControl : Control
    {
        [Tooltip("EnemyData.brain이 비었을 때 쓰는 폴백.")]
        [SerializeField] private EnemyBrainAsset brain;

        [Header("타이머")]
        [SerializeField] private float attackInterval = 1.5f;
        [SerializeField] private float retargetInterval = 0.5f;
        [Tooltip("특수 행동(돌진) 쿨. 평타 쿨과 따로 돈다.")]
        [SerializeField] private float specialInterval = 4f;

        [Header("브레인 파라미터")]
        [SerializeField] private EnemyBrainParams parameters = new EnemyBrainParams
        {
            attackRange = 1.8f,
            leashRange = 0f,
        };

        private Entity target;
        private float attackTimer;
        private float retargetTimer;
        private float specialTimer;
        private bool active = true;

        private EnemyChargeAction chargeAction;

        public Entity Target => target;

        /// <summary>폴백 브레인. 프리팹 배선 검사용 읽기 전용 창구.</summary>
        public EnemyBrainAsset Brain => brain;

        /// <summary>지금 특수 행동을 실행 중인지. 디버그 HUD가 읽는다.</summary>
        public bool IsRunningSpecial => chargeAction != null && chargeAction.IsRunning;

        protected override void Awake()
        {
            base.Awake();
            chargeAction = GetComponent<EnemyChargeAction>();
        }

        /// <summary>도발 등으로 타겟을 강제 지정한다.</summary>
        public void SetTarget(Entity forced)
        {
            target = forced;
            retargetTimer = retargetInterval;
        }

        public void SetActive(bool value)
        {
            active = value;
            if (!active)
            {
                Clear();
                if (chargeAction != null) chargeAction.Cancel();
            }
        }

        /// <summary>
        /// EnemyData의 행동 수치를 주입한다. Enemy.Awake가 호출한다.
        /// data가 null이면 인스펙터 값을 그대로 쓴다 — 데이터를 안 꽂은 프리팹도 동작해야 한다.
        /// </summary>
        public void ApplyData(EnemyData data)
        {
            if (data == null) return;

            if (data.brain != null) brain = data.brain;

            attackInterval = data.attackInterval;
            retargetInterval = data.retargetInterval;
            specialInterval = data.specialInterval;
            parameters.attackRange = data.attackRange;
            parameters.leashRange = data.leashRange;
            parameters.preferredMinRange = data.preferredMinRange;
            parameters.specialRange = data.specialRange;

            // 0이면 사거리 판정에 영영 들어가지 않는다. 저작 실수를 조용히 넘기지 않는다.
            // leashRange는 0이 "무제한"이라는 정상값이므로 검사하지 않는다.
            if (parameters.attackRange <= 0f)
                BattleLog.Warn(LogCategory.State,
                    $"{name}: EnemyData '{data.name}'의 attackRange가 0이다. 이 적은 공격하지 않는다.", this);
        }

        // 배선 누락을 조용히 넘기지 않는다. ApplyData는 Awake에서 끝나므로 Start에서 판단한다.
        private void Start()
        {
            if (brain == null)
                BattleLog.Warn(LogCategory.State, $"{name}: 브레인이 없다. 이 적은 움직이지 않는다.", this);
        }

        public override void Tick(float dt)
        {
            Clear();
            if (!active) return;
            if (brain == null) return;

            attackTimer -= dt;
            retargetTimer -= dt;
            specialTimer -= dt;

            // 경직·사망 중에는 돌진이 이어지면 안 된다. 무적 관통처럼 보인다.
            if (Owner != null && CombatStateRules.IsStunned(Owner.Combat.CombatState))
            {
                if (chargeAction != null) chargeAction.Cancel();
                return;
            }

            // 실행 중인 특수 행동이 우선. 새 판단을 받으면 돌진이 매 프레임 다시 시작된다.
            if (chargeAction != null && chargeAction.IsRunning)
            {
                chargeAction.Tick(dt);
                return;
            }

            if (Owner != null && Owner.IsBusy) return;

            Retarget();

            EnemyIntent intent = brain.Decide(BuildContext(dt));

            if (intent.kind == EnemyActionKind.Charge)
            {
                // 쿨은 실제로 시작됐을 때만 태운다.
                if (chargeAction != null && chargeAction.TryStart(target))
                {
                    specialTimer = specialInterval;
                    chargeAction.Tick(dt);
                }
                return;
            }

            Command = intent.command;
            MoveDirection = intent.moveDirection;

            // 쿨 소모는 여기서 판단한다. 브레인이 별도 플래그를 돌려주면 항상 이 조건과 같은 값이 되어 중복이다.
            if (intent.command == Command.Attack)
                attackTimer = attackInterval;
        }

        private void Retarget()
        {
            if (retargetTimer > 0f && target != null && !target.Combat.IsDead) return;

            retargetTimer = retargetInterval;
            target = FindNearestAlly();
        }

        private EnemyBrainContext BuildContext(float dt)
        {
            Vector3 toTarget = Vector3.zero;
            float distance = 0f;

            if (target != null)
            {
                toTarget = target.transform.position - transform.position;
                toTarget.y = 0f;                 // 거리 판정은 XZ 평면만. 높이는 무시한다.
                distance = toTarget.magnitude;
            }

            // struct 복사 — 인스펙터/EnemyData 원본값은 건드리지 않는다.
            EnemyBrainParams p = parameters;

            // 원거리 평타를 든 적은 근접 사거리 대신 투사체 사거리를 따른다.
            if (Owner != null && Owner.BasicIsRanged) p.attackRange = Owner.BasicAttackReach;

            return new EnemyBrainContext
            {
                self = Owner,
                target = target,
                toTarget = toTarget,
                distance = distance,
                attackReady = attackTimer <= 0f,
                specialReady = specialTimer <= 0f && chargeAction != null,
                p = p,
                dt = dt,
            };
        }

        private Entity FindNearestAlly()
        {
            Entity best = null;
            float bestSqr = float.MaxValue;

            foreach (Entity e in BattleRegistry.Allies)
            {
                if (e == null || e.Combat.IsDead) continue;

                float sqr = (e.transform.position - transform.position).sqrMagnitude;
                if (sqr >= bestSqr) continue;

                bestSqr = sqr;
                best = e;
            }

            return best;
        }
    }
}
