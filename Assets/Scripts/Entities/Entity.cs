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
            pushDistance = 0.375f,
            hitStunDuration = 0.3f,
        };

        [Tooltip("평타 연타 단계. 비워 두면 지금까지의 단발 평타 그대로다 — 적 · 자율 동료가 그렇다.\n\n" +
                 "채우면 유저가 모는 몸만 버튼을 반복해 이어 칠 수 있다.\n" +
                 "각 칸의 타이밍이 0이면 위의 기본 평타 값으로 떨어진다.")]
        [SerializeField] private BasicAttackStage[] basicComboStages = new BasicAttackStage[0];

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
        public HitData BuildBasicHit() => BuildBasicHit(0);

        /// <summary>
        /// 연타 <paramref name="stage"/>타째의 타격. 단계가 없으면 기본 평타와 같다 —
        /// 무인자 버전이 여기로 위임하므로 원거리 · 공중 평타 호출부는 손댈 필요가 없다.
        /// </summary>
        public HitData BuildBasicHit(int stage)
        {
            HitData h = basicHit;
            h.damageData.damage = stats.GetValue(StatType.AttackPower, h.damageData.damage);

            if (!HasComboStage(stage)) return h;

            return BasicComboRules.BuildStageHit(in h, in basicComboStages[stage]);
        }

        // ── 평타 연타 ────────────────────────────────────

        /// <summary>연타 단계 수. 저작하지 않았으면 1 — 단발이라는 뜻이다.</summary>
        public int BasicComboStageCount => basicComboStages != null && basicComboStages.Length > 0
            ? basicComboStages.Length
            : 1;

        /// <summary>연타를 저작한 몸인지.</summary>
        public bool HasBasicCombo => BasicComboStageCount > 1;

        private bool HasComboStage(int stage)
            => basicComboStages != null && stage >= 0 && stage < basicComboStages.Length;

        /// <summary>이 단계에 재생할 클립. 비워 뒀으면 null — 애니메이터가 기본 평타 클립으로 떨어진다.</summary>
        public AnimationClip GetBasicStageClip(int stage)
            => HasComboStage(stage) ? basicComboStages[stage].clip : null;

        /// <summary>단계 타이밍. 0으로 비워 둔 값은 기본 평타 값으로 접어서 돌려준다.</summary>
        public BasicAttackTiming GetBasicStageTiming(int stage)
        {
            BasicAttackStage s = HasComboStage(stage) ? basicComboStages[stage] : default;
            return BasicComboRules.ResolveTiming(in s, basicAttackWindup, basicAttackActiveEnd, basicAttackTotal);
        }

        /// <summary>
        /// 지금 도는 평타 한 타의 길이. <see cref="EntityAnimator"/>가 클립 속도를 여기에 맞춘다 —
        /// 고정 <see cref="BasicAttackTotal"/>을 보면 10프레임짜리 마무리가 1타 길이에 우겨넣어져 배속으로 보인다.
        /// </summary>
        public float ActiveAttackTotal { get; private set; }

        /// <summary>지금 도는 단계. 애니메이터가 클립을 고를 때 본다.</summary>
        public int ActiveAttackStage { get; private set; }

        /// <summary><see cref="AttackState"/>만 부른다. 단계에 들어갈 때 한 번.</summary>
        public void SetActiveAttackStage(int stage, float total)
        {
            ActiveAttackStage = stage;
            ActiveAttackTotal = total > 0f ? total : basicAttackTotal;
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

        private Vector3 cachedHurtboxSize;

        /// <summary>
        /// 이 몸의 <b>피격 범위</b> — 루트에 붙은 트리거 아닌 콜라이더의 크기.
        /// 기획서가 스킬 범위를 "플레이어 피격 가로 × 2"처럼 <b>배수로</b> 적으므로,
        /// 그 배수를 실제 유닛으로 바꾸려면 기준이 되는 이 값이 필요하다
        /// (<see cref="SkillData.castRangeScale"/>).
        ///
        /// 몸마다 다르다 — Player · Ally는 (1, 1, 1)이고 Enemy_Dummy는 (1.4, 2.28, 1.4)다.
        /// 상수로 박지 않고 콜라이더에서 읽는 이유가 그것이다.
        ///
        /// <see cref="Physics"/>와 같은 이유로 지연 해석한다. 콜라이더가 없으면 (1, 1, 1) —
        /// 배수를 그대로 유닛으로 쓰는 셈이라 저작 의도에서 가장 덜 벗어난다.
        /// </summary>
        public Vector3 HurtboxSize
        {
            get
            {
                if (cachedHurtboxSize != Vector3.zero) return cachedHurtboxSize;

                // bounds가 아니라 콜라이더 치수를 직접 읽는다 — bounds는 월드 AABB라
                // 씬에 서 있지 않은 몸(EditMode 테스트 · 프리팹 편집)에서는 0으로 나온다.
                cachedHurtboxSize = Vector3.one;

                switch (GetComponent<Collider>())
                {
                    case CapsuleCollider cap when !cap.isTrigger:
                        cachedHurtboxSize = new Vector3(cap.radius * 2f, cap.height, cap.radius * 2f);
                        break;
                    case BoxCollider box when !box.isTrigger:
                        cachedHurtboxSize = box.size;
                        break;
                }

                return cachedHurtboxSize;
            }
        }

        private EntityAnimator cachedAnimator;

        /// <summary>
        /// 스프라이트 애니메이터. 없는 몸도 있으므로(생성기 · 테스트) 항상 null 검사를 하고 쓴다.
        /// <see cref="Physics"/>와 같은 이유로 지연 해석한다.
        /// </summary>
        public EntityAnimator Animator => cachedAnimator != null
            ? cachedAnimator
            : cachedAnimator = GetComponentInChildren<EntityAnimator>(true);

        /// <summary>
        /// 이 몸에 붙은 AI 드라이버. 이제 <see cref="EnemyControl"/>뿐이다.
        /// 유저가 모는 몸은 null이다 — 조종사(<see cref="PlayerPilot"/>)가 밖에서 몬다.
        /// </summary>
        public Control Control { get; private set; }

        /// <summary>
        /// true면 <b>어떤 Control도 명령을 내지 않는다</b>. <see cref="ComboExecutor"/>가
        /// 상태머신을 강탈하는 동안 켠다(결정 로그 ②).
        ///
        /// 조종사가 아니라 여기 있는 이유는 태그 교대 때문이다. 조종사는 몸을 갈아타는데,
        /// 지휘 플래그가 조종사에 붙어 있으면 갈아타는 순간 값이 통째로 사라진다.
        /// </summary>
        public bool IsCommanded { get; set; }

        /// <summary>
        /// 유저가 지금 이 몸을 몰고 있는가. <see cref="PlayerPilot"/>이 켜고 끈다.
        ///
        /// <see cref="IsCommanded"/>와 같은 이유로 여기 있다 — 조종사가 몸 밖에 있으므로
        /// "이 몸이 조종 대상인가"는 몸이 들고 있어야 한다. 대시 패리처럼
        /// <b>유저가 몰 때만 열리는 규칙</b>이 이 값을 본다.
        /// </summary>
        public bool IsPiloted { get; set; }

        // ── 의도 ────────────────────────────────────────────

        /// <summary>
        /// 이번 프레임의 명령. 조종사(<see cref="PlayerPilot"/>)나 AI(<see cref="EnemyControl"/>)가
        /// 채우고 상태머신이 읽어 소비한다.
        ///
        /// <b>Control이 아니라 몸이 들고 있다.</b> 조종사가 몸 밖으로 나갔으므로,
        /// 의도를 조종사가 들고 있으면 몸이 바뀌는 순간 값이 통째로 사라진다 —
        /// <see cref="IsCommanded"/>를 여기로 올린 것과 같은 이유다.
        /// </summary>
        public Command Command { get; private set; } = Command.None;

        /// <summary>이번 프레임의 이동 방향. 벨트스크롤이라 XZ 평면이다.</summary>
        public Vector3 MoveDirection { get; private set; }

        /// <summary>
        /// 평타 선입력. 의도의 유효기간을 늘린 것뿐이라 의도와 같은 층에 둔다.
        ///
        /// 덤으로 <b>AI는 연타를 칠 수단 자체가 없어진다</b> — <see cref="EnemyControl"/>은
        /// <see cref="BufferAttack"/>을 부르지 않는다. bool 플래그로 막는 것보다 강한 보장이다.
        /// </summary>
        private AttackInputBuffer attackBuffer;

        /// <summary>이번 프레임의 의도를 통째로 갈아 끼운다. 조종사와 AI가 부른다.</summary>
        public void Drive(Command command, Vector3 moveDirection)
        {
            Command = command;
            MoveDirection = moveDirection;
        }

        /// <summary>명령만 지운다. <b>이동 방향은 남긴다</b> — 걷는 도중에 명령만 소비되는 경우가 있다.</summary>
        public void Consume() => Command = Command.None;

        /// <summary>이번 프레임 의도를 비운다. 선입력은 건드리지 않는다.</summary>
        public void ClearCommand()
        {
            Command = Command.None;
            MoveDirection = Vector3.zero;
        }

        /// <summary>
        /// 남은 의도를 통째로 비운다. 몸에서 손을 뗄 때.
        /// <see cref="Consume"/>는 이동 방향을 남기므로 여기서는 못 쓴다 —
        /// 태그로 내려간 몸이 마지막 이동 방향을 물고 있으면 다시 섰을 때 혼자 걸어간다.
        ///
        /// 선입력도 같이 버린다. 안 그러면 내려간 몸이 물고 있던 입력이
        /// 다시 섰을 때 터져 아무도 안 누른 평타가 나간다.
        /// </summary>
        public void ClearIntent()
        {
            ClearCommand();
            attackBuffer.Clear();
        }

        /// <summary>선입력을 채운다. 유저 입력을 읽는 쪽만 부른다.</summary>
        public void BufferAttack(float window) => attackBuffer.Press(window);

        /// <summary>선입력 창을 흘린다. <see cref="ClearCommand"/>와 달리 프레임마다 지워지지 않는다.</summary>
        public void TickAttackBuffer(float dt) => attackBuffer.Tick(dt);

        /// <summary>선입력이 살아 있는지 들여다본다. 비우지 않는다.</summary>
        public bool HasAttackBuffer => attackBuffer.HasInput;

        /// <summary>남아 있으면 true를 내고 비운다.</summary>
        public bool TryConsumeAttackBuffer() => attackBuffer.TryConsume();

        /// <summary>선입력을 버린다. 공격에 들어가는 순간, 그 입력을 두 번 쓰지 않으려고 부른다.</summary>
        public void ClearAttackBuffer() => attackBuffer.Clear();

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

        protected virtual void Awake()
        {
            cachedPhysics = GetComponent<Physics>();
            cachedCombat = GetComponent<Combat>();

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

            // 연타는 단계마다 같은 함정을 밟을 수 있다. 폴백을 먹인 뒤의 값으로 본다.
            for (int i = 0; i < BasicComboStageCount && HasBasicCombo; i++)
            {
                BasicAttackTiming t = GetBasicStageTiming(i);
                if (t.windup < t.activeEnd && t.activeEnd <= t.total) continue;

                BattleLog.Warn(LogCategory.Combat,
                    $"{name}: 평타 {i + 1}타 타이밍이 어긋났다 " +
                    $"(선딜 {t.windup:0.##} / 판정끝 {t.activeEnd:0.##} / 전체 {t.total:0.##}). " +
                    "windup < activeEnd <= total 순서를 지킬 것.", this);
            }
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

        /// <summary>
        /// 자동 조준이 이 몸을 후보로 삼아도 되는가. 기본 true.
        ///
        /// 벽에서 걸어 나오는 중인 적이 false를 건다(<see cref="EnemySpawnGuard"/>).
        /// 판정을 끄는 것만으로는 모자라다 — 조준은 여전히 그쪽을 향하므로,
        /// 플레이어의 스킬이 <b>때릴 수 없는 적</b>을 향해 나가 헛돈다.
        /// </summary>
        public bool IsTargetable { get; set; } = true;

        // ── 빙의 ────────────────────────────────────────────

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

        /// <summary>
        /// 이 몸을 모는 드라이버. 이제 <see cref="EnemyControl"/>뿐이라 사실상 적만 잡힌다.
        ///
        /// <c>enabled</c>를 보는 이유는 인스펙터에서 꺼 둔 AI를 존중하기 위해서다 —
        /// 훈련용 허수아비처럼 서 있기만 해야 하는 몸이 있다.
        /// </summary>
        private Control FirstEnabledControl()
        {
            foreach (Control c in GetComponents<Control>())
                if (c != null && c.enabled) return c;

            return null;
        }
    }
}
