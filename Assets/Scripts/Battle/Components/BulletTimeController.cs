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
    /// <item>페이즈(RealTime → Freeze → Order → Resolve) — TacticStates.cs 의 private 중첩 클래스</item>
    /// <item>여기 — 인스펙터 값 · 씬 배선 · 이벤트 구독, 그리고 셋을 잇는 결정</item>
    /// </list>
    ///
    /// 손패는 <b>불릿타임과 무관하게 항상 4장이 유지된다</b>. 큐처럼 굴러가서
    /// 실시간에는 U키로 왼쪽 한 장씩 소모하고, 불릿타임을 걸었다 풀면 왼쪽부터 전부 발동한다.
    ///
    /// <b>Time.timeScale은 쓰지 않는다</b> — UI와 애니메이션까지 멈추면
    /// 정작 손패를 조작할 수 없기 때문(결정 로그 ⑥).
    /// </summary>
    public partial class BulletTimeController : MonoBehaviour
    {
        [Header("참조")]
        [SerializeField] private Player player;
        [SerializeField] private ComboExecutor executor;
        [SerializeField] private TargetSelector targetSelector;

        [Tooltip("비워도 된다. 없으면 U키가 벤치에 앉은 동료를 불러오지 못하고 예전처럼 거부한다.")]
        [SerializeField] private TagSwapController swap;

        [Header("게이지")]
        [SerializeField] private float maxGauge = 100f;
        [Tooltip("초당 자연 충전량.")]
        [SerializeField] private float gaugeRegen = 8f;
        [Tooltip("진입에 필요한 게이지 비율.")]
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
                 "· Empty — 테스트 모드. 0장으로 시작해 레벨업으로만 카드가 들어온다.\n" +
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

        [Header("전술 페이즈")]
        [Tooltip("Freeze 체류 시간(비배율 초). 0이면 다음 프레임에 곧바로 Order로 넘어간다. UI 확대 연출을 넣을 자리.")]
        [SerializeField] private float freezeDuration = 0f;

        private TacticStateMachine tactic;
        private TacticGauge budget;
        private CardSupply cards;

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

        public TacticPhase Phase => tactic != null ? tactic.Phase : TacticPhase.RealTime;

        /// <summary>시간이 멈춰 있는 구간(Freeze · Order).</summary>
        public bool IsActive => Phase == TacticPhase.Freeze || Phase == TacticPhase.Order;

        /// <summary>손패 순서 변경 · 조준이 열려 있는지. UI가 이 값으로 조작을 켜고 끈다.</summary>
        public bool AllowsCardEdit => tactic != null && tactic.AllowsCardEdit;

        /// <summary>페이즈가 바뀌었다. 인자는 (prev, next).</summary>
        public event Action<TacticPhase, TacticPhase> OnPhaseChanged;

        /// <summary>불릿타임에 들어갔다(Freeze 진입).</summary>
        public event Action OnEnter;

        /// <summary>불릿타임이 풀리고 실행이 시작됐다(Resolve 진입).</summary>
        public event Action OnExit;

        // ── UI가 읽는 값 ─────────────────────────────────
        // 게이지 막대가 "얼마나 찼나"만으로는 못 그린다. 어디까지 차야 쓸 수 있는지,
        // 지금 쿨타임인지까지 보여야 유저가 E를 눌러도 되는 때를 안다.

        public Energy Gauge => budget?.Gauge;

        /// <summary>진입에 필요한 게이지 비율. 막대 위 임계 눈금이 여기 선다.</summary>
        public float RequiredRatio => requiredRatio;

        /// <summary>남은 쿨타임(초). 0이면 쿨타임 아님.</summary>
        public float CooldownRemaining => budget != null ? budget.CooldownRemaining : 0f;

        /// <summary>게이지만 놓고 봤을 때 진입선을 넘었는지. 쿨타임 · 마나는 안 본다.</summary>
        public bool IsGaugeReady => budget != null && budget.IsReady;

        public Deck Deck => Cards.Deck;
        public Hand Hand => Cards.Hand;
        public Discard Discard => Cards.Discard;
        public ComboExecutor Executor => executor;

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
            if (targetSelector == null) targetSelector = GetComponentInChildren<TargetSelector>();
            if (swap == null) swap = FindAnyObjectByType<TagSwapController>();

            tactic = new TacticStateMachine(this);
        }

        private void Start()
        {
            // 견본 손패는 덱을 아예 거치지 않는다. 여기서 덱을 지으면
            // "16장이 아니다" 경고만 뜨고 아무도 그 카드를 뽑지 않는다.
            if (buildDeckOnStart && !Cards.UsesFixedHand)
            {
                if (WantsDeckPicker)
                {
                    // 화면에서 다 짜면 그쪽이 RebuildDeckAndHand를 부른다.
                    // 그때까지는 덱도 손패도 비어 있다 — 어차피 조작이 잠겨 있다.
                    DeckBuilderUI.RequestOpen(this, AvailableSkillPool());
                    tactic.Begin();
                    return;
                }

                BuildDeck();
            }

            // 덱이 0장인 채로 시작하므로 첫 Refill이 Discard 회수 → 셔플을 자동으로 부른다.
            RefillHand();

            tactic.Begin();
        }

        private void OnEnable()
        {
            if (executor != null)
            {
                executor.OnSlotConsumed += HandleSlotConsumed;
                executor.OnExecuteFinished += RefillHand;
            }

            CombatEvents.OnParried += HandleParried;
            CombatEvents.OnAnyBasicHitLanded += HandleBasicHitLanded;
            SubscribeAllyDeaths();
        }

        private void OnDisable()
        {
            if (executor != null)
            {
                executor.OnSlotConsumed -= HandleSlotConsumed;
                executor.OnExecuteFinished -= RefillHand;
            }

            CombatEvents.OnParried -= HandleParried;
            CombatEvents.OnAnyBasicHitLanded -= HandleBasicHitLanded;
            UnsubscribeAllyDeaths();
        }

        private void Update()
        {
            // 게이지와 쿨타임은 실제 시간으로 흐른다. 정지 중 자가 충전을 막는다.
            float dt = TimeControl.UnscaledDeltaTime;

            budget.Tick(dt, frozen: IsActive);
            tactic.Tick(dt);
        }

        private void HandleParried(Combat defender, Combat attacker) => budget?.RewardParry(defender);

        private void HandleBasicHitLanded(Combat attacker, Combat victim, int swing)
            => budget?.RewardBasicHit(attacker, victim, swing);

        // ── 진입 ─────────────────────────────────────────

        /// <summary>E키. 실제 판정은 현재 전술 페이즈가 내린다 — Order에서는 이 키가 곧 실행이다.</summary>
        public bool Enter() => tactic != null && tactic.OnBulletTimeKey();

        public bool CanEnter
            => !IsActive && executor != null && !executor.IsRunning
               && budget != null && budget.Shortfall(player) == null;

        /// <summary>진입이 막힌 이유. 로그 전용.</summary>
        public string BlockReason()
        {
            if (IsActive) return "이미 진입 중";
            if (executor == null) return "ComboExecutor 미연결";
            if (executor.IsRunning) return "이전 콤보 실행 중";
            return budget?.Shortfall(player) ?? "알 수 없음";
        }

        /// <summary>
        /// 페이즈를 곧바로 옮긴다. <b>테스트 전용</b> — 게임 경로는 반드시 <see cref="Enter"/>를 탄다.
        /// 코스트 · 게이지 검사를 건너뛰므로 게임 코드에서 부르면 규칙이 깨진다.
        /// </summary>
        internal void ForcePhase(TacticPhase phase) => tactic?.ChangeTo(tactic.StateOf(phase));

        /// <summary>Freeze 진입. 시간은 <see cref="TimeControl"/>로만 멈춘다(결정 로그 ⑥).</summary>
        private void FreezeTime()
        {
            TimeControl.Scale = 0f;

            BattleLog.Log(LogCategory.Bullet,
                $"<b>불릿타임 진입</b> — TimeControl.Scale = 0 (Time.timeScale 미사용) | 마나 {manaCost:0.#} 소모", this);
        }

        // ── 손패 조작 (불릿타임 중 UI가 호출) ─────────────

        /// <summary>손패 두 칸의 순서를 바꾼다. 실행 순서가 그대로 바뀐다.</summary>
        public bool SwapHand(int a, int b) => AllowsCardEdit && Hand.Swap(a, b);

        /// <summary>조준을 확정해 해당 칸에 박제한다.</summary>
        public bool SetHandTarget(int idx, in TargetInfo target) => AllowsCardEdit && Hand.SetTarget(idx, in target);

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
            // 이미 걷어낸 직업의 카드는 되돌리지 않는다. 실행 큐는 손패를 통째로 굳혀 두므로
            // 콤보 도중에 시전자가 죽어도 남은 슬롯이 여기까지 흘러온다 —
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
            => GameManager.Instance != null
                ? GameManager.Instance.DeckStartupMode
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
        /// 레벨업 선택지와 디버그 편집기가 뽑을 수 있는 스킬 전부.
        ///
        /// <b>덱이 아니라 파티가 기준이다.</b> 지금 든 카드에서 모집단을 긁으면
        /// 0장으로 시작하는 테스트 모드에서 뽑을 것이 하나도 없어 레벨업이 헛돈다.
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
        /// 레벨업으로 받은 카드를 지금 판에 들인다.
        ///
        /// <b>곧바로 손패에 꽂는다.</b> 버린 더미에 넣으면 덱 열여섯 장을 다 돌 때까지 —
        /// 사실상 다음 스테이지까지 — 손에 들어오지 않아서, 방금 고른 보상이 아무 일도
        /// 일으키지 않은 것처럼 보인다. 보상은 고른 그 자리에서 손에 잡혀야 한다.
        /// </summary>
        /// <param name="card">받은 카드.</param>
        /// <param name="fusedFrom">합성이면 재료가 된 스킬. 아니면 null.</param>
        /// <param name="fuseCount">걷어낼 재료 장수.</param>
        public void GrantCard(ComboCard card, SkillData fusedFrom = null, int fuseCount = 0)
        {
            if (card == null || card.Data == null) return;

            Cards.Grant(card, fusedFrom, fuseCount);

            // 합성으로 손패가 빘을 수 있다. 남은 칸은 평소대로 덱에서 채운다.
            RefillHand();

            BattleLog.Log(LogCategory.Deck,
                $"카드 획득 — {card.Data.skillName}{(card.Golden ? " <color=#FFD166>(황금)</color>" : "")} " +
                $"→ 손패 | 덱 {Deck.Count} · 손패 {Hand.Count} · Discard {Discard.Count}", this);
        }
    }
}
