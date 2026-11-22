using System;
using System.Collections.Generic;
using UnityEngine;

namespace Prototype
{
    /// <summary>
    /// 지휘 모드의 관제탑. 덱 · 손패 · 실행을 이어 붙인다.
    ///
    /// <b>무엇이 어디 있나.</b>
    /// <list type="bullet">
    /// <item>자원(게이지 · 쿨 · 마나 · 보상) — <see cref="TacticGauge"/></item>
    /// <item>카드 더미(덱 · 손패 · 버린 더미 · 덱 짓기 · 걷어내기) — <see cref="CardSupply"/></item>
    /// <item>페이즈(RealTime → Resolve) — TacticStates.cs 의 private 중첩 클래스</item>
    /// <item>여기 — 인스펙터 값 · 씬 배선 · 이벤트 구독, 그리고 셋을 잇는 결정</item>
    /// </list>
    ///
    /// 손패는 <b>항상 4장이 유지된다</b>. 평타 적중 추첨이 여기와 덱에서 카드를 <b>한 장</b> 뽑아 쓴다
    /// (<see cref="BasicAttackDrawRules"/>) — 동료 하나가 나와 스킬 하나를 쓰는 것이고, 총공격이 아니다.
    ///
    /// <b>총공격은 게이지가 차 있을 때 총공격키(기본 Space)를 눌러야만 나간다</b>(<see cref="TryFireAssault"/>).
    /// 런의 총공격 슬롯 4칸이 왼쪽부터 차례로 발동한다. 게이지가 차거나 적이 나타나는 것만으로는 안 나간다.
    /// 편성(<see cref="OpenAssaultEditor"/>)은 칸 안에서 싸움이 없을 때만 바꾼다(<see cref="AssaultEditGate"/>).
    ///
    /// <b>Time.timeScale은 쓰지 않는다</b> — 시간 배율은 <see cref="TimeControl"/>만 만진다(결정 로그 ⑥).
    /// </summary>
    public partial class BulletTimeController : MonoBehaviour
    {
        [Header("참조")]
        [SerializeField] private Player player;
        [SerializeField] private ComboExecutor executor;

        [Header("게이지")]
        [SerializeField] private float maxGauge = 100f;
        [Tooltip("초당 자연 충전량.")]
        [SerializeField] private float gaugeRegen = 8f;
        [Tooltip("총공격에 필요한 게이지 비율.")]
        [Range(0f, 1f)][SerializeField] private float requiredRatio = 1f;
        [Tooltip("대시 패링 한 번의 게이지 보상. 위험을 감수한 대가를 전술 자원으로 돌려준다.")]
        [SerializeField] private float parryGaugeReward = 12f;
        [Tooltip("동료의 평타 한 대가 적에게 적중했을 때의 게이지 보상. 0이면 끈다.\n" +
                 "빗맞거나 패링 · 무적에 흘리면 없다. 한 대가 적 여럿을 맞혀도 한 번만 준다 — " +
                 "몰려 있는 적을 긁는 것만으로 게이지가 폭주하지 않게. 연타는 타마다 따로 센다.")]
        [SerializeField] private float basicAttackGaugeReward = 2f;

        [Header("코스트 — 둘 다 구현해 두고 실험 후 결정(결정 로그 ⑤)")]
        [SerializeField] private float manaCost = 0f;
        [SerializeField] private float cooldown = 0f;

        [Header("덱")]
        [Tooltip("Start에서 파티 장착 카드로 덱을 짠다. 포스트 배틀 흐름이 붙기 전까지만.")]
        [SerializeField] private bool buildDeckOnStart = true;

        [Tooltip("이 씬만 단독으로 Play할 때 쓰는 시작 덱 규칙.\n\n" +
                 "Boot 씬을 거쳐 들어오면 GameManager(런의 주인)의 설정이 이긴다 — " +
                 "여기 값은 무시된다. 시작 덱은 런 단위 결정이라 스테이지마다 다를 수 없다.\n\n" +
                 "· Party — 파티 장착 카드 16장. 게임의 실제 시작이다.\n" +
                 "· Empty — 테스트 모드. 0장으로 시작해 보상 상자로만 카드가 들어온다.\n" +
                 "· Pick  — 디버그 모드. 시작할 때 화면에서 카드를 직접 골라 짠다.")]
        [SerializeField] private DeckStartupMode standaloneStartupMode = DeckStartupMode.Party;

        [Tooltip("동료가 죽으면 그 직업 카드를 덱 · 손패 · 버린 더미에서 전부 걷어낸다. " +
                 "같은 직업 동료가 아직 살아 있으면 남긴다.")]
        [SerializeField] private bool purgeCardsOnAllyDeath = true;

        [Header("견본 콤보 — 훈련장 · 시연용")]
        [Tooltip("켜면 덱·셔플을 건너뛰고 아래 스킬만 그 순서대로 손패에 채운다.\n\n" +
                 "콤보 한 싸이클을 매번 똑같이 굴려야 값을 비교할 수 있다. " +
                 "무작위 드로우로는 같은 체인이 두 번 나오지 않는다.")]
        [SerializeField] private bool useFixedHand = false;

        [Tooltip("고정 손패에 채울 순서. 정석 체인은 모으기 → 띄우기 → 공격기 → 밀치기다.\n" +
                 "4장을 넘겨도 되며, 손패 4칸이 비는 대로 이 순서를 순환한다.")]
        [SerializeField] private List<SkillData> fixedHand = new List<SkillData>();

        /// <summary>전술 페이즈 기계. 엔티티와 같은 <see cref="StateMachine"/>, 인스턴스만 따로.</summary>
        private StateMachine tactic;
        private RealTimeState realTimeState;
        private ResolveState resolveState;

        private TacticState CurTactic => tactic?.CurState as TacticState;
        private TacticGauge budget;
        private CardSupply cards;

        /// <summary>평타 카드 추첨에 당첨돼 다음 LateUpdate에 발동을 기다리는 중.</summary>
        private bool pendingBasicSkill;
        private readonly BasicAttackProcRoller basicProcRoller = new BasicAttackProcRoller();

        /// <summary>
        /// 동료 사망 구독. <see cref="Combat.OnDead"/>가 인자를 주지 않아 동료마다 클로저를 하나씩 만든다 —
        /// 해제하려면 그때 넘긴 델리게이트 인스턴스를 그대로 들고 있어야 한다.
        /// </summary>
        private readonly List<(Combat combat, Action handler)> deathHooks = new List<(Combat, Action)>();

        /// <summary>자원. 인스펙터 값이 필요해서 Awake에서 만든다.</summary>
        private TacticGauge Budget => budget;

        /// <summary>
        /// 카드 더미. 첫 접근에 만든다 — Awake 없이 손패를 구독하는 경로(에디터 테스트 · UI)가 있다.
        /// </summary>
        private CardSupply Cards => cards ?? (cards = new CardSupply(this));

        // ── 페이즈 ───────────────────────────────────────

        public TacticPhase Phase => CurTactic?.Phase ?? TacticPhase.RealTime;

        // ── UI가 읽는 값 ─────────────────────────────────
        // 게이지 막대가 "얼마나 찼나"만으로는 못 그린다. 어디까지 차야 쓸 수 있는지,
        // 지금 쿨타임인지까지 보여야 총공격키를 언제 눌러도 되는지 안다.

        public Energy Gauge => budget?.Gauge;

        /// <summary>총공격에 필요한 게이지 비율. 막대 위 임계 눈금이 여기 선다.</summary>
        public float RequiredRatio => requiredRatio;

        /// <summary>남은 쿨타임(초). 0이면 쿨타임 아님.</summary>
        public float CooldownRemaining => budget != null ? budget.CooldownRemaining : 0f;

        /// <summary>게이지만 놓고 봤을 때 발동선을 넘었는지. 쿨타임 · 마나는 안 본다.</summary>
        public bool IsGaugeReady => budget != null && budget.IsReady;

        public Deck Deck => Cards.Deck;
        public Hand Hand => Cards.Hand;
        public Discard Discard => Cards.Discard;
        public ComboExecutor Executor => executor;
        public CastDirector Director { get; private set; }

        public void SetCastDirector(CastDirector director)
        {
            Director = director;
            if (executor != null) executor.Director = director;
        }

        public void CancelAssistSequence()
        {
            pendingBasicSkill = false;
            assaultQueue = null;
            assaultCast = false;
            executor?.Abort();
            if (tactic != null) ChangePhase(realTimeState);
        }

        /// <summary>
        /// 파티 명단의 주인을 꽂는다. <see cref="PartyAssembler"/>가 <c>Awake</c>(-200)에서
        /// 부르므로 이 컴포넌트의 <see cref="Awake"/>(0)보다 먼저 도착한다.
        ///
        /// 이 참조로 덱을 짓는다 — 비면 손패가 통째로 빈다.
        /// </summary>
        public void SetHero(Player hero)
        {
            if (hero != null) player = hero;
        }

        private void Awake()
        {
            budget = new TacticGauge(new TacticGauge.Config(
                maxGauge, gaugeRegen, requiredRatio, parryGaugeReward,
                basicAttackGaugeReward, manaCost, cooldown), this);

            Cards.UseFixedHand(useFixedHand ? fixedHand : null);

            if (player == null) player = FindAnyObjectByType<Player>();
            if (executor == null) executor = GetComponentInChildren<ComboExecutor>();

            tactic = new StateMachine { OwnerName = "전술" };
            realTimeState = new RealTimeState(this);
            resolveState = new ResolveState(this);
        }

        private void Start()
        {
            // 견본 손패는 덱을 아예 거치지 않는다. 여기서 덱을 지으면
            // "16장이 아니다" 경고만 뜨고 아무도 그 카드를 뽑지 않는다.
            // 총공격: 런 보유 스킬(이름:개수)에 파티 장착 카드를 씨 뿌리는 용도로만 쓴다.
            if (buildDeckOnStart && !Cards.UsesFixedHand)
                BuildDeck();

            // 덱이 0장인 채로 시작하므로 첫 Refill이 Discard 회수 → 셔플을 자동으로 부른다.
            RefillHand();

            BeginTactic();
        }

        private void OnEnable()
        {
            basicProcRoller.Reset();
            if (executor != null)
            {
                executor.OnSlotConsumed += HandleSlotConsumed;
                executor.OnExecuteFinished += RefillHand;
            }

            CombatEvents.OnParried += HandleParried;
            CombatEvents.OnAnyBasicHitLanded += HandleBasicHitLanded;
            CombatEvents.OnAnyHitLanded += TrackRecentTarget;
            SubscribeAllyDeaths();
        }

        private void OnDisable()
        {
            Director?.Abort();
            pendingBasicSkill = false;
            if (executor != null)
            {
                executor.OnSlotConsumed -= HandleSlotConsumed;
                executor.OnExecuteFinished -= RefillHand;
            }

            CombatEvents.OnParried -= HandleParried;
            CombatEvents.OnAnyBasicHitLanded -= HandleBasicHitLanded;
            CombatEvents.OnAnyHitLanded -= TrackRecentTarget;
            recentTarget = null;
            UnsubscribeAllyDeaths();
        }

        private void Update()
        {
            // 게이지와 쿨타임은 전투 시간으로 흐른다 — TimeControl이 멈추면 같이 멈춘다.
            // 총공격은 여기서 내지 않는다. 키를 눌렀을 때만 나간다(TryFireAssault).
            budget.Tick(TimeControl.DeltaTime);
            tactic.Tick(TimeControl.UnscaledDeltaTime);
        }

        private void HandleParried(Combat defender, Combat attacker) => budget?.RewardParry(defender);

        /// <summary>
        /// 평타 적중. 게이지 보상을 주고, 추첨에 당첨되면 손패 · 덱의 카드 한 장을 실제 스킬로 실행한다.
        /// </summary>
        private void HandleBasicHitLanded(Combat attacker, Combat victim, int swing)
        {
            if (budget == null || !budget.RewardBasicHit(attacker, victim, swing)) return;

            if (Phase != TacticPhase.RealTime || assaultCast || Director == null || pendingBasicSkill || TimeControl.Scale <= 0f) return;
            if (!HasAutomaticCardCandidate()) return;
            if (!basicProcRoller.TryRoll(UnityEngine.Random.value)) return;

            // 적중 콜백 안에서 상태를 바꾸면 진행 중인 평타의 히트박스 처리가 깨질 수 있다.
            pendingBasicSkill = true;
        }

        // ── 총공격 ───────────────────────────────────────

        /// <summary>가장 최근에 아군이 때린 적. 총공격은 이 대상만 노린다. 죽거나 사라지면 비운다.</summary>
        private Entity recentTarget;

        private void TrackRecentTarget(Combat attacker, Combat victim)
        {
            if (attacker?.Owner?.Faction != Faction.Ally ||
                victim?.Owner?.Faction != Faction.Enemy || victim.IsDead) return;

            recentTarget = victim.Owner;
        }

        /// <summary>쓸 수 있는 최근 대상. 죽었거나 타게팅 불가면 null.</summary>
        private Entity RecentTargetOrNull()
        {
            Entity t = recentTarget;
            if (t == null || t.Combat == null || t.Combat.IsDead || !t.IsTargetable
                || !t.gameObject.activeInHierarchy)
            {
                recentTarget = null;
                return null;
            }

            return t;
        }

        /// <summary>총공격 큐를 굳혀 둔 채 ResolveState 진입을 기다리는 중. 진입하면 비운다.</summary>
        private Queue<ComboSlot> assaultQueue;

        /// <summary>총공격이 실행 중이다(Resolve 동안). 소비된 슬롯을 버린 더미로 안 보내는 데 쓴다.</summary>
        private bool assaultCast;

        /// <summary>
        /// 총공격키(기본 Space)를 눌렀다. 낼 수 있으면 런의 총공격 슬롯 4칸을 왼쪽부터 차례로 발동하고,
        /// 못 내면 그 이유를 돌려준다(<see cref="AssaultRules"/>). 못 냈을 때 게이지는 그대로다.
        ///
        /// <b>총공격이 나가는 길은 이것 하나다.</b> 예전에는 매 프레임 스스로 검사해서(<c>TryAutoAssault</c>)
        /// 게이지가 차는 순간 · 다음 방의 적이 나타나는 순간 · 편집창을 닫는 순간에 저절로 나갔다.
        /// </summary>
        public AssaultBlock TryFireAssault()
        {
            string shortfall = budget != null ? budget.Shortfall(player) : "자원 없음";

            AssaultBlock block = AssaultRules.CheckFire(
                running: Phase != TacticPhase.RealTime || assaultCast || executor == null || executor.IsRunning,
                heroDown: player != null && player.Combat.IsDead,
                resourcesReady: shortfall == null,
                aliveEnemies: BattleRegistry.AliveEnemyCount());

            // 슬롯은 나머지가 다 될 때만 지어 본다 — 시전자를 못 찾은 슬롯이 누를 때마다 경고를 남긴다.
            Queue<ComboSlot> queue = null;
            if (block == AssaultBlock.None)
            {
                queue = BuildAssaultQueue();
                block = AssaultRules.CheckQueue(queue.Count);
            }

            if (block != AssaultBlock.None)
            {
                string detail = block == AssaultBlock.Charging ? $" ({shortfall})" : "";
                BattleLog.Log(LogCategory.Bullet, $"총공격 안 나감 — {AssaultRules.Describe(block)}{detail}", this);
                return block;
            }

            assaultQueue = queue;
            assaultCast = true;
            BattleLog.Log(LogCategory.Bullet, $"<b>총공격 발동</b> — {queue.Count}슬롯", this);
            ChangePhase(resolveState);
            return AssaultBlock.None;
        }

        /// <summary>
        /// 런의 총공격 슬롯을 실행 큐로. 레벨(<see cref="SkillFusion"/>)은 데미지 배율로 실린다.
        /// 시전자가 없는 슬롯은 건너뛴다.
        /// </summary>
        public Queue<ComboSlot> BuildAssaultQueue()
        {
            var q = new Queue<ComboSlot>(RunProgression.AssaultSlotCount);
            RunProgression run = RunProgression.Current;
            Entity focus = RecentTargetOrNull();   // 없으면 스킬 자동 조준으로 폴백

            for (int i = 0; i < RunProgression.AssaultSlotCount; i++)
            {
                string name = run.AssaultSlots[i];
                if (name == null) continue;

                SkillData data = run.Resolve(name);
                Ally caster = data != null ? ResolveCaster(data.role) : null;
                if (caster == null)
                {
                    BattleLog.Warn(LogCategory.Combo, $"총공격 슬롯 {i} 건너뜀 — {name}: 에셋 또는 시전자 없음", this);
                    continue;
                }

                q.Enqueue(new ComboSlot
                {
                    card = new ComboCard(data),
                    caster = caster,
                    target = caster.AutoTarget(data),
                    damageScale = SkillFusion.DamageMulFromCount(run.CountOf(name)),
                    targetEntity = focus,
                });
            }

            return q;
        }

        /// <summary>
        /// 총공격 슬롯 편집창을 연다(E키). 총공격이 도는 중이면 무시한다.
        /// "칸 안 · 싸움 밖"은 편집창이 직접 본다(<see cref="AssaultEditorUI.CanOpen"/>) — 런 바의 슬롯 버튼도 같은 문을 지난다.
        /// </summary>
        public void OpenAssaultEditor()
        {
            if (Phase != TacticPhase.RealTime || assaultCast) return;
            AssaultEditorUI.RequestOpen();
        }

        private bool HasAutomaticCardCandidate()
        {
            bool CanCast(ComboCard card) => card != null && card.Data != null && ResolveCaster(card.Data.role) != null;
            for (int i = 0; i < Cards.Hand.Count; i++)
                if (CanCast(Cards.Hand.GetCard(i))) return true;
            for (int i = 0; i < Cards.Deck.Count; i++)
                if (CanCast(Cards.Deck.Cards[i])) return true;
            return false;
        }

        private void LateUpdate()
        {
            if (!pendingBasicSkill) return;
            pendingBasicSkill = false;
            TryExecuteBasicAttackSkill(UnityEngine.Random.value);
        }

        /// <summary>현재 손패와 덱에서 한 장을 소비하고, 기존 시전자 등장 · 스킬 실행 경로로 보낸다.</summary>
        public bool TryExecuteBasicAttackSkill(float roll)
        {
            if (Phase != TacticPhase.RealTime || Director == null || player == null || player.Combat.IsDead) return false;

            bool CanCast(ComboCard c) => ResolveCaster(c.Data.role) != null;
            ComboCard card = BasicAttackDrawRules.TakeCard(Cards.Hand, Cards.Deck, roll, CanCast);
            if (card == null) return false;

            Ally caster = ResolveCaster(card.Data.role);
            var slot = new ComboSlot
            {
                card = card,
                caster = caster,
                target = caster.AutoTarget(card.Data),
            };
            CastTicket ticket = Director.Cast(new CastRequest(caster, card.Data, slot.target), _ =>
            {
                HandleSlotConsumed(card);
                if (Phase == TacticPhase.RealTime && isActiveAndEnabled) RefillHand();
            });
            if (ticket == null)
            {
                HandleSlotConsumed(card);
                RefillHand();
                return false;
            }
            BattleLog.Log(LogCategory.Combo,
                $"평타 적중 스킬 발동 — {card.Data.skillName}", this);
            return true;
        }

        // ── 페이즈 ───────────────────────────────────────

        /// <summary>전술 페이즈 전이. 막는 페이즈가 없으므로 무조건 전이다.</summary>
        private void ChangePhase(TacticState next) => tactic.ForceChangeState(next);

        /// <summary>Start에서 한 번. 실시간 전투로 시작한다.</summary>
        private void BeginTactic() => ChangePhase(realTimeState);

        // ── 손패 ─────────────────────────────────────────

        /// <summary>덱에서 뽑아 손패를 4장까지 채운다. 덱이 비면 Discard가 섞여 되돌아온다.</summary>
        private void RefillHand() => Cards.Refill();

        /// <summary>카드의 직업에 맞는 동료를 찾는다. 스킬은 직업 전용이다.</summary>
        public Ally ResolveCaster(Role role)
        {
            if (player == null) return null;

            foreach (Ally a in player.Party)
                if (a != null && a.Role == role && !a.Combat.IsDead)
                    return a;

            return null;
        }

        /// <summary>
        /// 실행이 끝난 카드. 콤보가 전부 끝난 뒤의 보충은 <c>OnExecuteFinished</c>가 한다 —
        /// 실행 도중에 채우면 아직 Discard로 안 간 카드가 다시 뽑힐 수 있다.
        /// </summary>
        private void HandleSlotConsumed(ComboCard card)
        {
            // 총공격 카드는 그 자리에서 만든 것이라 버린 더미로 돌릴 곳이 없다.
            if (assaultCast) return;

            // 이미 걷어낸 직업의 카드는 되돌리지 않는다. 평타 적중으로 나간 카드는 시전이 끝나야
            // 여기로 오므로, 그사이 시전자가 죽으면 걷어낸 직업의 카드가 흘러온다 —
            // 그대로 넣으면 덱이 소진될 때 Discard가 회수되면서 죽은 동료 카드가 되살아난다.
            if (purgeCardsOnAllyDeath && !Cards.UsesFixedHand && IsOrphanCard(card))
            {
                BattleLog.Log(LogCategory.Deck,
                    $"{(card.Data != null ? card.Data.skillName : "(빈 카드)")} — " +
                    "시전 직업이 전멸해 버린 더미로 보내지 않고 소멸", this);
                return;
            }

            Cards.Recycle(card);
        }

        /// <summary>살아 있는 시전자가 없어 영영 쓸 수 없게 된 카드인지.</summary>
        private bool IsOrphanCard(ComboCard card)
            => card != null && card.Data != null && ResolveCaster(card.Data.role) == null;

        // ── 동료 사망 → 카드 회수 ─────────────────────────

        private void SubscribeAllyDeaths()
        {
            if (player == null) return;

            foreach (Ally a in player.Party)
            {
                if (a == null || a.Combat == null) continue;

                Ally dead = a;
                Action handler = () => HandleAllyDied(dead);

                dead.Combat.OnDead += handler;
                deathHooks.Add((dead.Combat, handler));
            }
        }

        private void UnsubscribeAllyDeaths()
        {
            for (int i = 0; i < deathHooks.Count; i++)
                if (deathHooks[i].combat != null)
                    deathHooks[i].combat.OnDead -= deathHooks[i].handler;

            deathHooks.Clear();
        }

        private void HandleAllyDied(Ally dead)
        {
            if (!purgeCardsOnAllyDeath || dead == null) return;

            // 같은 직업 동료가 아직 살아 있으면 그 카드는 여전히 발동할 수 있다.
            if (ResolveCaster(dead.Role) != null)
            {
                BattleLog.Log(LogCategory.Deck,
                    $"{BattleLog.Name(dead)} 사망 — 같은 직업({dead.Role}) 동료가 살아 있어 카드는 남긴다", this);
                return;
            }

            BattleLog.Log(LogCategory.Deck, $"<b>{BattleLog.Name(dead)} 사망</b> — {dead.Role} 카드를 걷어낸다", this);
            PurgeRole(dead.Role);

            // 런 덱에서도 걷는다. 여기가 빠지면 이 판에서만 사라지고, 다음 스테이지의
            // BuildDeck이 RunProgression.Cards를 다시 읽는 순간 통째로 되살아난다.
            int fromRun = RunProgression.Current.PurgeRole(dead.Role);
            if (fromRun > 0)
                BattleLog.Log(LogCategory.Deck,
                    $"런 덱에서도 {dead.Role} 카드 {fromRun}장 제거 — 다음 스테이지로 안 넘어간다", this);
        }

        /// <summary>
        /// 한 직업의 카드를 덱 · 손패 · 버린 더미에서 통째로 걷어낸다(<see cref="CardSupply.PurgeRole"/>).
        /// </summary>
        public int PurgeRole(Role role)
        {
            int total = Cards.PurgeRole(role);
            if (total == 0) return 0;

            // 손패에 구멍이 났으면 메운다. 실행 중에는 건드리지 않는다 —
            // 아직 Discard로 안 간 카드가 다시 뽑히기 때문(OnExecuteFinished가 대신 채운다).
            if (executor == null || !executor.IsRunning)
                RefillHand();

            return total;
        }

        // ── 덱 짓기 · 보상 ───────────────────────────────

        /// <summary>
        /// 지금 적용되는 시작 덱 규칙.
        ///
        /// <b>Boot 씬의 <c>GameManager</c>가 이긴다.</b> 시작 덱은 런 단위 결정이라
        /// 스테이지마다 다를 수 없고, 스테이지 씬 아홉 개에 같은 스위치를 하나씩 켜 두면
        /// 한 곳만 어긋나도 "2스테이지부터 갑자기 덱이 달라지는" 식으로만 드러난다.
        ///
        /// 인스펙터 값은 <c>GameManager</c>가 없을 때 — 스테이지 씬 단독 실행에서만 쓰인다.
        /// </summary>
        private DeckStartupMode StartupMode
            => RunHost.Current != null
                ? RunHost.Current.DeckStartupMode
                : standaloneStartupMode;

        /// <summary>
        /// 시작할 때 화면에서 덱을 짜야 하는가. 디버그 모드이고, 이 런이 아직 시작 덱을
        /// 정하지 않았을 때만 — 두 번째 스테이지에서 또 물으면 진행이 끊긴다.
        /// </summary>
        private bool WantsDeckPicker
            => StartupMode == DeckStartupMode.Pick && !RunProgression.Current.Seeded;

        /// <summary>
        /// 지금 카드를 시전할 수 있는 직업들. <see cref="ResolveCaster"/>와 같은 규칙이라
        /// (살아 있는 동료가 있는 직업) 사망 시 카드를 걷어내는 규칙과 어긋나지 않는다.
        /// </summary>
        public List<Role> AvailableRoles()
        {
            var roles = new List<Role>(Ally.EquipSlots);
            if (player == null) return roles;

            foreach (Ally a in player.Party)
            {
                if (a == null || a.Combat == null || a.Combat.IsDead) continue;
                if (!roles.Contains(a.Role)) roles.Add(a.Role);
            }

            return roles;
        }

        /// <summary>
        /// 보상 상자와 디버그 편집기가 뽑을 수 있는 스킬 전부.
        ///
        /// <b>덱이 아니라 파티가 기준이다.</b> 지금 든 카드에서 모집단을 긁으면
        /// 0장으로 시작하는 테스트 모드에서 뽑을 것이 하나도 없어 상자가 빈다.
        /// "가진 동료가 쓸 수 있는 카드 전부"가 언제나 후보다.
        ///
        /// 직업으로 거르는 이유는 시전자가 없는 카드가 손패 맨 앞을 막기 때문이다 —
        /// 마법사가 없는데 마법사 카드를 주면 그 카드는 영영 안 나간다.
        /// </summary>
        public List<SkillData> AvailableSkillPool()
        {
            int ignored = 0;
            List<ComboCard> partyCards = player != null ? CardSupply.CollectPartyCards(player, ref ignored) : null;

            return SkillCatalog.Pool(AvailableRoles(), partyCards);
        }

        /// <summary>덱을 다시 짓고 손패를 채운다. 시작 덱을 화면에서 짠 뒤 그쪽이 부른다.</summary>
        public void RebuildDeckAndHand()
        {
            BuildDeck();
            RefillHand();
        }

        /// <summary>포스트 배틀에서만 호출. 전투 중 덱 수정은 막는다(<see cref="CardSupply.Build"/>).</summary>
        public void BuildDeck() => Cards.Build(player, StartupMode);

        /// <summary>
        /// 보상으로 받은 카드를 지금 판에 들인다.
        ///
        /// <b>곧바로 손패에 꽂는다.</b> 버린 더미에 넣으면 덱 열여섯 장을 다 돌 때까지 —
        /// 사실상 다음 스테이지까지 — 손에 들어오지 않아서, 방금 고른 보상이 아무 일도
        /// 일으키지 않은 것처럼 보인다. 보상은 고른 그 자리에서 손에 잡혀야 한다.
        /// </summary>
        /// <param name="card">받은 카드.</param>
        public void GrantCard(ComboCard card)
        {
            if (card == null || card.Data == null) return;

            Cards.Grant(card);

            // 손패가 비어 있을 수 있다. 남은 칸은 평소대로 덱에서 채운다.
            RefillHand();

            BattleLog.Log(LogCategory.Deck,
                $"카드 획득 — {card.Data.skillName} " +
                $"→ 손패 | 덱 {Deck.Count} · 손패 {Hand.Count} · Discard {Discard.Count}", this);
        }
    }
}
