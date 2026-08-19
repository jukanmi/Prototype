using System;
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

        public float BasicProjectileSpeed => basicProjectileSpeed;
        public float BasicProjectileRange => basicProjectileRange;
        public int BasicProjectilePierce => basicProjectilePierce;

        /// <summary>
        /// 평타를 투사체로 바꾼다. EnemyData 주입과 에디터 생성기가 같은 경로를 쓰도록 API로 연다.
        /// 0 이하 값은 조용히 최소값으로 올린다 — 저작 실수로 제자리에 서는 투사체를 만들지 않는다.
        /// </summary>
        public void ConfigureBasicProjectile(Projectile prefab, float speed, float range, int pierce)
        {
            basicProjectile = prefab;
            basicProjectileSpeed = Mathf.Max(0.1f, speed);
            basicProjectileRange = Mathf.Max(0.5f, range);
            basicProjectilePierce = Mathf.Max(0, pierce);
        }

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
                        Physics.WallMask, layer, height: BasicProjectileHeight(shot, target));

            // 쏘는 순간 방향을 맞춰 준다. 히트박스 자식과 스프라이트가 따라 돈다.
            Physics.Face(dir);
        }

        /// <summary>
        /// 원거리 평타가 날아갈 높이. 음수면 <see cref="Projectile.FlightHeight"/>를 그대로 쓴다.
        ///
        /// <b>이 값을 안 넘기면 투사체가 늘 고정 높이로 나간다</b> — 점프해서 쏴도 화살이
        /// 발밑 바닥에서 튀어나오는 게 그 증상이다. 스킬 쪽은 예전부터
        /// <c>SkillState.AimHeight</c>로 이 값을 채우고 있었고, 평타 경로만 빠져 있었다.
        ///
        /// 쏘는 쪽과 대상 중 <b>높은 쪽</b>을 따른다. 공중에 띄운 적을 지상에서 쏠 때
        /// 바닥을 긁고 지나가면 공중 콤보 마무리가 통째로 빗나가기 때문이다.
        /// 거기에 총구 높이를 더해 가슴께에서 나가게 맞춘다.
        /// </summary>
        private float BasicProjectileHeight(Projectile shot, Entity target)
        {
            float self = Physics.Height;
            float aim = target != null && target.Physics != null ? target.Physics.Height : 0f;

            float h = Mathf.Max(self, aim);

            // 둘 다 지상이면 프리팹 기본 높이가 정답이다.
            return h > 0.1f ? h + shot.FlightHeight : -1f;
        }

        /// <summary>히트박스가 아군을 때리지 않게 거르는 기준. Enemy만 덮어쓴다.</summary>
        public virtual Faction Faction => Faction.Ally;

        private Physics cachedPhysics;
        private Combat cachedCombat;

        /// <summary>
        /// Awake 없이 접근하는 경로(에디터 테스트 · 생성기)가 있어 지연 해석한다.
        /// RequireComponent가 존재를 보장하므로 GetComponent는 반드시 성공한다.
        /// </summary>
        public Physics Physics => cachedPhysics != null ? cachedPhysics : cachedPhysics = GetComponent<Physics>();
        public Combat Combat => cachedCombat != null ? cachedCombat : cachedCombat = GetComponent<Combat>();

        /// <summary>
        /// 지금 이 몸을 모는 것. <see cref="UseControl{T}"/>가 갈아 끼운다.
        /// 아무도 안 몰면 null이다 — 태그로 내려간 몸과, 불릿타임에 불려 나왔지만
        /// 조작 대상이 아닌 몸이 그렇다.
        /// </summary>
        public Control Control { get; private set; }

        /// <summary>
        /// true면 <b>어떤 Control도 명령을 내지 않는다</b>. <see cref="ComboExecutor"/>가
        /// 상태머신을 강탈하는 동안 켠다(결정 로그 ②).
        ///
        /// Control이 아니라 여기 있는 이유는 태그 교대 때문이다. 몸을 모는 주체가
        /// PlayerControl ↔ AllyControl 로 바뀌는데, 지휘 플래그가 Control에 붙어 있으면
        /// 갈아타는 순간 값이 통째로 사라진다.
        /// </summary>
        public bool IsCommanded { get; set; }

        /// <summary>
        /// 공격 예고(선딜) 중인가. <b>맞기 전에 읽을 수 있는 유일한 신호</b>다.
        ///
        /// 평타는 선딜(<see cref="BasicAttackWindup"/>), 보스 · 돌진은
        /// <see cref="EnemySpecialPhase.Telegraph"/> 구간이 여기 해당한다.
        /// 화면 표현(<see cref="EnemyStateTint"/>)이 이 값을 보고 몸을 하얗게 번쩍인다 —
        /// 대시 패링은 이 신호를 보고 누르는 것이라, 신호가 없으면 패링은 운이 된다.
        /// </summary>
        public bool IsTelegraphing { get; private set; }

        /// <summary>예고가 켜지고 꺼질 때. 표현 쪽이 매 프레임 폴링하지 않게 이벤트로 알린다.</summary>
        public event Action<bool> OnTelegraphChanged;

        /// <summary>예고 표시를 켜고 끈다. 같은 값이면 아무 일도 하지 않는다.</summary>
        public void SetTelegraph(bool on)
        {
            if (IsTelegraphing == on) return;

            IsTelegraphing = on;
            OnTelegraphChanged?.Invoke(on);
        }

        public StateMachine StateMachine { get; private set; }

        /// <summary>
        /// 지금 무언가를 모으고 있으면 그 주체. 없으면 null.
        ///
        /// 모으는 경로가 둘이라 한 창구로 합친다 —
        /// 동료 스킬은 <see cref="ChargeSkillState"/>로 <b>상태머신</b> 위에서 돌고,
        /// 보스 패턴은 <see cref="BossPatternAction"/>으로 <b>컴포넌트</b> 위에서 돈다.
        /// 머리 위 게이지(<see cref="ChargeGauge"/>)는 그 차이를 알 이유가 없다.
        /// </summary>
        public IChargeState ChargeState
        {
            get
            {
                if (StateMachine?.CurState is IChargeState fromState) return fromState;

                // 컴포넌트 쪽은 한 번만 찾는다. 매 프레임 GetComponent를 도는 자리다.
                if (!chargeComponentResolved)
                {
                    chargeComponentResolved = true;
                    chargeComponent = GetComponent<IChargeState>();
                }

                return chargeComponent;
            }
        }

        private IChargeState chargeComponent;
        private bool chargeComponentResolved;

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

        /// <summary>이 몸에 붙은 모든 Control. 태그 교대가 이 중 하나를 고른다.</summary>
        private Control[] controls;

        protected virtual void Awake()
        {
            cachedPhysics = GetComponent<Physics>();
            cachedCombat = GetComponent<Combat>();

            controls = GetComponents<Control>();
            // 인스펙터에서 켜 둔 것을 그대로 존중한다. 씬을 그냥 돌렸을 때
            // 태그 컨트롤러 없이도 예전처럼 움직이게 하기 위한 기본값이다.
            Control = FirstEnabledControl();

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

            // 선딜이 전체 길이보다 길면 AttackState가 히트박스를 켜기 전에 끝난다 —
            // 공격이 조용히 사라지고 모션만 남는다. 예고를 길게 잡다가 밟기 쉬운 함정이라 경고한다.
            if (basicAttackWindup >= basicAttackTotal)
                BattleLog.Warn(LogCategory.Combat,
                    $"{name}: 평타 선딜({basicAttackWindup:0.##}s)이 전체 길이({basicAttackTotal:0.##}s) 이상이다. " +
                    "히트박스가 켜지지 않는다 — windup < activeEnd < total 순서를 지킬 것.", this);
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
            SetTelegraph(false);
            StateMachine.ForceChangeState(DeadState);
        }

        /// <summary>상태머신이 지금 중단 가능한지. AI · UI 판단용.</summary>
        public bool IsBusy => StateMachine.CurState != null && !StateMachine.CurState.CanBeInterrupted;

        // ── 빙의 ────────────────────────────────────────────

        /// <summary>
        /// 이 몸을 <typeparamref name="T"/>가 몰게 한다. 나머지 Control은 꺼지고,
        /// 꺼지는 쪽은 <see cref="Control.Consume"/>로 남은 명령을 비운다 —
        /// 안 그러면 갈아탄 첫 프레임에 직전 주인이 남긴 평타가 한 번 더 나간다.
        ///
        /// 해당 Control이 없으면 <b>아무도 안 모는 상태</b>가 되고 null을 돌려준다.
        /// 플레이어 몸에 <see cref="AllyControl"/>이 없는 게 정상이라 이 경로가 필요하다 —
        /// 불릿타임에 불려 나온 플레이어 몸은 서 있기만 해야 한다.
        /// </summary>
        public T UseControl<T>() where T : Control
        {
            if (controls == null) controls = GetComponents<Control>();

            T picked = null;

            for (int i = 0; i < controls.Length; i++)
            {
                Control c = controls[i];
                if (c == null) continue;

                if (c is T match)
                {
                    picked = match;
                    c.enabled = true;
                    continue;
                }

                c.ClearIntent();
                c.enabled = false;
            }

            Control = picked;
            return picked;
        }

        /// <summary>
        /// 태그로 내려갈 때의 공통 뒷정리. <b>두 가지를 반드시 되돌려야 한다.</b>
        ///
        /// <list type="number">
        /// <item><see cref="IsCommanded"/> 해제 — Executor가 켠 채로 잘리면 다시 섰을 때
        /// 어떤 Control도 명령을 못 낸다.</item>
        /// <item>상태머신을 Idle로 — 코루틴은 <c>SetActive(false)</c>에 죽는다.
        /// 스킬 상태 뒷정리가 중간에 잘리면 <c>CanBeInterrupted == false</c>인 채로 굳어,
        /// 다시 섰을 때 이 몸이 영구히 <see cref="IsBusy"/>가 된다.</item>
        /// <item>경직 해제(<see cref="Combat.ClearHitStun"/>) — 꺼진 몸은 <c>Combat.Tick</c>도
        /// 멈춘다. 맞고 뜬 채로 내려가면 그 상태가 얼어붙고, 다시 설 때
        /// <c>Physics.Teleport</c>가 착지 이벤트 없이 지면에 세우므로 경직이 안 풀린 채 남는다.</item>
        /// </list>
        ///
        /// 사망 상태는 건드리지 않는다 — 시체를 Idle로 되돌리면 다시 섰을 때 되살아난다.
        /// </summary>
        protected void ReleaseBody()
        {
            IsCommanded = false;
            SetTelegraph(false);

            if (Combat.IsDead) return;

            Combat.ClearHitStun();
            StateMachine?.ForceChangeState(IdleState);
        }

        private Control FirstEnabledControl()
        {
            for (int i = 0; i < controls.Length; i++)
                if (controls[i] != null && controls[i].enabled) return controls[i];

            return null;
        }
    }
}
