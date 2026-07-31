using UnityEngine;

namespace Prototype
{
    /// <summary>
    /// 모든 캐릭터의 뼈대. 상속이 아닌 <b>컴포지션</b>으로 기능을 조립한다.
    /// </summary>
    [RequireComponent(typeof(Physics))]
    [RequireComponent(typeof(Combat))]
    public class Entity : MonoBehaviour
    {
        [SerializeField] private Stats stats = new Stats();
        [SerializeField] private Energies energies = new Energies();

        [Header("평타")]
        [SerializeField] private Attack basicAttack;
        [Tooltip("선딜. 이 시점에 히트박스가 켜진다.")]
        [SerializeField] private float basicAttackWindup = 0.12f;
        [Tooltip("히트박스가 꺼지는 시점.")]
        [SerializeField] private float basicAttackActiveEnd = 0.24f;
        [Tooltip("후딜 포함 전체 길이.")]
        [SerializeField] private float basicAttackTotal = 0.45f;
        [SerializeField] private HitData basicHit = new HitData
        {
            damageData = new DamageData(10f),
            targetState = CombatState.Neutral,
            nextState = CombatState.LightHit,
            mode = KnockbackMode.Fixed,
            fixedDir = Vector3.forward,
            knockbackForce = 3f,
            hitStunDuration = 0.3f,
        };

        [Tooltip("스킬 전용 히트박스. 비우면 평타 히트박스를 재사용한다.")]
        [SerializeField] private Attack skillAttack;

        [Header("평타 — 원거리")]
        [Tooltip("넣으면 평타가 투사체가 된다. 비우면 앞에 히트박스를 켜는 근접 평타.")]
        [SerializeField] private Projectile basicProjectile;
        [SerializeField] private float basicProjectileSpeed = 16f;
        [SerializeField] private float basicProjectileRange = 9f;
        [SerializeField] private int basicProjectilePierce = 0;

        /// <summary>평타가 날아가는지. AttackState가 이걸로 갈린다.</summary>
        public bool BasicIsRanged => basicProjectile != null;

        /// <summary>
        /// 원거리 평타의 유효 사거리. AI가 이 거리에서 멈춰 선다.
        /// 최대 사거리보다 짧게 잡아 가장자리에서 헛쏘지 않게 한다.
        /// </summary>
        public float BasicAttackReach => basicProjectileRange * 0.8f;

        [Header("사망")]
        [Tooltip("쓰러진 채로 남아 있는 시간. 이 뒤에 서서히 사라진다.")]
        [SerializeField] private float despawnDelay = 1f;
        [Tooltip("페이드아웃에 걸리는 시간. 0이면 즉시 사라진다.")]
        [SerializeField] private float despawnFade = 0.5f;

        public float DespawnDelay => despawnDelay;
        public float DespawnFade => despawnFade;

        public Attack BasicAttack => basicAttack;
        public Attack SkillAttack => skillAttack != null ? skillAttack : basicAttack;
        public float BasicAttackWindup => basicAttackWindup;
        public float BasicAttackActiveEnd => basicAttackActiveEnd;
        public float BasicAttackTotal => basicAttackTotal;

        /// <summary>평타 HitData에 현재 공격력을 실어 새로 만든다. 원본은 건드리지 않는다.</summary>
        public HitData BuildBasicHit()
        {
            HitData h = basicHit;
            h.damageData.damage = stats.GetValue(StatType.AttackPower, h.damageData.damage);
            return h;
        }

        /// <summary>
        /// 원거리 평타 발사. 가장 가까운 상대를 스스로 겨눈다 —
        /// 근접과 달리 바라보는 방향만으로는 맞히기 어렵다.
        /// </summary>
        public void FireBasicProjectile()
        {
            if (basicProjectile == null || Physics == null) return;

            Vector3 from = Physics.GroundPosition;
            Entity target = BattleRegistry.NearestOpponent(this);

            Vector3 dir = target != null
                ? target.Physics.GroundPosition - from
                : Physics.Facing;

            // 히트박스 레이어를 물려받아야 충돌 매트릭스가 맞는다.
            int layer = basicAttack != null ? basicAttack.gameObject.layer : gameObject.layer;

            Projectile shot = Instantiate(basicProjectile);
            HitData hit = BuildBasicHit();

            shot.Launch(Combat, in hit, from, dir,
                        basicProjectileSpeed, basicProjectileRange, basicProjectilePierce,
                        Physics.WallMask, layer);

            // 쏘는 순간 방향을 맞춰 준다. 히트박스 자식과 스프라이트가 따라 돈다.
            Physics.Face(dir);
        }

        /// <summary>히트박스가 아군을 때리지 않게 거르는 기준. Enemy만 덮어쓴다.</summary>
        public virtual Faction Faction => Faction.Ally;

        public Physics Physics { get; private set; }
        public Combat Combat { get; private set; }
        public Control Control { get; private set; }
        public StateMachine StateMachine { get; private set; }
        public Stats Stats => stats;
        public Energies Energies => energies;

        // 자주 쓰는 상태는 캐싱해서 매 전이마다 할당하지 않는다.
        public IdleState IdleState { get; private set; }
        public MoveState MoveState { get; private set; }
        public JumpState JumpState { get; private set; }
        public AttackState AttackState { get; private set; }
        public AerialAttackState AerialAttackState { get; private set; }
        public HitState HitState { get; private set; }
        public AerialHitState AerialHitState { get; private set; }
        public DownState DownState { get; private set; }
        public GetupState GetupState { get; private set; }
        public DeadState DeadState { get; private set; }

        protected virtual void Awake()
        {
            Physics = GetComponent<Physics>();
            Combat = GetComponent<Combat>();
            Control = GetComponent<Control>();
            StateMachine = new StateMachine { OwnerName = name };

            if (basicAttack == null) basicAttack = GetComponentInChildren<Attack>(true);
            if (basicAttack != null && basicAttack.Attacker == null) basicAttack.Attacker = Combat;

            EnsureDefaults();
            BuildStates();
        }

        protected virtual void Start()
        {
            BattleLog.Log(LogCategory.State,
                $"{name} 준비 완료 | {GetType().Name} | Control {(Control != null ? Control.GetType().Name : "없음")} | " +
                $"HP {Combat.Health.MaxValue:0.#} | 평타 히트박스 {(basicAttack != null ? "O" : "X")} | 스킬 히트박스 {(skillAttack != null ? "O" : "X")}",
                this);

            StateMachine.ForceChangeState(IdleState);
        }

        /// <summary>
        /// true면 시간이 멈춰도 Control이 계속 돈다.
        /// 불릿타임 중에도 입력을 받아야 하는 플레이어만 해당된다.
        /// </summary>
        protected virtual bool ControlUsesUnscaledTime => false;

        protected virtual void Update()
        {
            float dt = TimeControl.DeltaTime;

            Control?.Tick(ControlUsesUnscaledTime ? TimeControl.UnscaledDeltaTime : dt);

            if (dt <= 0f) return;

            StateMachine.Tick(dt);
            Combat.Tick(dt);
        }

        private void EnsureDefaults()
        {
            if (stats.GetStat(StatType.MoveSpeed) == null) stats.Set(StatType.MoveSpeed, 6f);
            if (stats.GetStat(StatType.DashSpeed) == null) stats.Set(StatType.DashSpeed, 18f);
            if (stats.GetStat(StatType.JumpHeight) == null) stats.Set(StatType.JumpHeight, 2.5f);
            if (stats.GetStat(StatType.JumpTime) == null) stats.Set(StatType.JumpTime, 0.35f);
            if (stats.GetStat(StatType.AttackPower) == null) stats.Set(StatType.AttackPower, 10f);
            if (stats.GetStat(StatType.Defense) == null) stats.Set(StatType.Defense, 0f);

            energies.Ensure(EnergyType.Mana, 100f);
        }

        private void BuildStates()
        {
            IdleState = new IdleState(this);
            MoveState = new MoveState(this);
            JumpState = new JumpState(this);
            AttackState = new AttackState(this);
            AerialAttackState = new AerialAttackState(this);
            HitState = new HitState(this);
            AerialHitState = new AerialHitState(this);
            DownState = new DownState(this);
            GetupState = new GetupState(this);
            DeadState = new DeadState(this);
        }

        /// <summary>
        /// Combat이 피격 반응을 요청한다. 슈퍼아머 중이면 상태머신이 거부하고 false를 돌려준다.
        /// </summary>
        public bool RequestHitReaction(CombatState next)
        {
            IState target;
            switch (next)
            {
                case CombatState.LightHit: target = HitState; break;
                case CombatState.AerialHit:
                case CombatState.Knockback:
                case CombatState.WallBound: target = AerialHitState; break;
                case CombatState.Down: target = DownState; break;
                case CombatState.Getup: target = GetupState; break;
                case CombatState.Dead: ForceDead(); return true;
                default: return true;
            }

            // 이미 그 상태면 전이할 게 없을 뿐, 거부된 게 아니다.
            // TryChangeState는 같은 상태에도 false를 주므로 여기서 갈라 준다.
            // 안 그러면 공중에 뜬 대상의 추가타 넉백이 슈퍼아머로 오인돼 전부 무시된다.
            if (target == StateMachine.CurState)
                return true;

            return StateMachine.TryChangeState(target);
        }

        /// <summary>사망은 슈퍼아머를 관통한다(결정 로그 ②③).</summary>
        public void ForceDead()
        {
            StateMachine.ForceChangeState(DeadState);
        }

        /// <summary>상태머신이 지금 중단 가능한지. AI · UI 판단용.</summary>
        public bool IsBusy => StateMachine.CurState != null && !StateMachine.CurState.CanBeInterrupted;
    }
}
