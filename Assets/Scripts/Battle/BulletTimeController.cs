using System;
using System.Collections.Generic;
using Prototype.YG;
using UnityEngine;

namespace Prototype
{
    /// <summary>
    /// 지휘 모드의 관제탑. 덱 · 손패 · 실행을 이어 붙인다.
    ///
    /// 손패는 <b>불릿타임과 무관하게 항상 4장이 유지된다</b>. 큐처럼 굴러가서
    /// 실시간에는 U키로 왼쪽 한 장씩 소모하고, 불릿타임을 걸었다 풀면 왼쪽부터 전부 발동한다.
    ///
    /// <b>Time.timeScale은 쓰지 않는다</b> — UI와 애니메이션까지 멈추면
    /// 정작 손패를 조작할 수 없기 때문(결정 로그 ⑥).
    /// </summary>
    public class BulletTimeController : MonoBehaviour
    {
        [Header("참조")]
        [SerializeField] private Player player;
        [SerializeField] private ComboExecutor executor;
        [SerializeField] private ComboPredictor predictor;
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

        [Header("코스트 — 둘 다 구현해 두고 실험 후 결정(결정 로그 ⑤)")]
        [SerializeField] private float manaCost = 0f;
        [SerializeField] private float cooldown = 0f;

        [Header("실시간 카드 쿨타임")]
        [Tooltip("U키 단발 사용에 SkillData.cooldown을 적용한다. 끄면 예전처럼 손패만 있으면 계속 나간다.")]
        [SerializeField] private bool realtimeCooldown = true;

        [Tooltip("SkillData.cooldown에 곱하는 값. 에셋 27개를 건드리지 않고 전체 템포만 조절한다.")]
        [SerializeField] private float realtimeCooldownMul = 1f;

        [Tooltip("카드 한 장을 쓴 뒤 U키 전체가 잠기는 시간(초). 0이면 끈다.\n" +
                 "스킬별 쿨만으로는 연타를 못 막는다 — 손패 4장이 서로 다른 스킬이라 " +
                 "각자 쿨이 따로 돌기 때문이다. 연사 속도를 정하는 건 이 값이다.")]
        [SerializeField] private float realtimeGlobalCooldown = 1.5f;

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

        /// <summary>고정 손패에서 다음에 낼 카드의 인덱스. 순환한다.</summary>
        private int fixedCursor;

        /// <summary>고정 손패가 켜져 있고 실제로 낼 카드가 있는지.</summary>
        public bool UsesFixedHand => useFixedHand && fixedHand != null && fixedHand.Count > 0;

        [Header("전술 페이즈")]
        [Tooltip("Freeze 체류 시간(비배율 초). 0이면 다음 프레임에 곧바로 Order로 넘어간다. UI 확대 연출을 넣을 자리.")]
        [SerializeField] private float freezeDuration = 0f;

        private readonly Deck deck = new Deck();
        private readonly Hand hand = new Hand();
        private readonly Discard discard = new Discard();

        private TacticStateMachine tactic;
        private float cooldownTimer;

        /// <summary>
        /// U키로 쓴 스킬의 남은 쿨타임. <b>불릿타임 실행은 여기 걸리지 않는다</b> —
        /// 게이지를 통째로 태워서 얻는 한 방이므로 실시간 연사 제한과 별개로 둔다.
        /// </summary>
        private readonly SkillCooldownTracker skillCooldowns = new SkillCooldownTracker();

        /// <summary>
        /// 동료 사망 구독. <see cref="Combat.OnDead"/>가 인자를 주지 않아 동료마다 클로저를 하나씩 만든다 —
        /// 해제하려면 그때 넘긴 델리게이트 인스턴스를 그대로 들고 있어야 한다.
        /// </summary>
        private readonly List<(Combat combat, Action handler)> deathHooks = new List<(Combat, Action)>();

        public Energy Gauge { get; private set; }

        /// <summary>전술 상태머신. 입력은 전부 여기로 넣는다.</summary>
        public TacticStateMachine Tactic => tactic;

        public TacticPhase Phase => tactic != null ? tactic.Phase : TacticPhase.RealTime;

        /// <summary>시간이 멈춰 있는 구간(Freeze · Order).</summary>
        public bool IsActive => Phase == TacticPhase.Freeze || Phase == TacticPhase.Order;

        /// <summary>손패 순서 변경 · 조준이 열려 있는지. UI가 이 값으로 조작을 켜고 끈다.</summary>
        public bool AllowsCardEdit => tactic != null && tactic.AllowsCardEdit;

        public float FreezeDuration => freezeDuration;

        // ── UI가 읽는 값 ─────────────────────────────────
        // 게이지 막대가 "얼마나 찼나"만으로는 못 그린다. 어디까지 차야 쓸 수 있는지,
        // 지금 쿨타임인지까지 보여야 유저가 E를 눌러도 되는 때를 안다.

        /// <summary>진입에 필요한 게이지 비율. 막대 위 임계 눈금이 여기 선다.</summary>
        public float RequiredRatio => requiredRatio;

        /// <summary>남은 쿨타임(초). 0이면 쿨타임 아님.</summary>
        public float CooldownRemaining => Mathf.Max(0f, cooldownTimer);

        /// <summary>게이지만 놓고 봤을 때 진입선을 넘었는지. 쿨타임 · 마나는 안 본다.</summary>
        public bool IsGaugeReady => Gauge != null && Gauge.Ratio >= requiredRatio;

        /// <summary>실시간(U키) 쿨타임이 켜져 있는지. UI가 표시 여부를 여기서 가른다.</summary>
        public bool RealtimeCooldownEnabled => realtimeCooldown;

        /// <summary>카드 종류와 무관하게 U키가 잠겨 있는 남은 시간(초).</summary>
        public float GlobalCardCooldownRemaining
            => realtimeCooldown ? skillCooldowns.GlobalRemaining : 0f;

        /// <summary>
        /// 그 스킬을 지금 쓸 수 있기까지 남은 시간(초). 0이면 쓸 수 있다.
        /// 공용 쿨과 스킬 쿨 중 <b>긴 쪽</b>이다 — 둘 다 풀려야 나간다.
        /// </summary>
        public float SkillCooldownRemaining(SkillData data)
            => realtimeCooldown ? skillCooldowns.Remaining(data) : 0f;

        /// <summary>U키가 지금 눌리면 나갈 카드의 남은 쿨타임. 0이면 나간다.</summary>
        public float TopCardCooldownRemaining => SkillCooldownRemaining(hand.Get(0).Data);

        public Deck Deck => deck;
        public Hand Hand => hand;
        public Discard Discard => discard;
        public ComboPredictor Predictor => predictor;
        public ComboExecutor Executor => executor;

        public event Action OnEnter;
        public event Action OnExit;

        private void Awake()
        {
            Gauge = new Energy(EnergyType.BulletTimeGauge, maxGauge);
            if (player == null) player = FindAnyObjectByType<Player>();
            if (executor == null) executor = GetComponentInChildren<ComboExecutor>();
            if (predictor == null) predictor = GetComponentInChildren<ComboPredictor>();
            if (targetSelector == null) targetSelector = GetComponentInChildren<TargetSelector>();
            if (swap == null) swap = FindAnyObjectByType<TagSwapController>();

            tactic = new TacticStateMachine(this);
        }

        private void Start()
        {
            // 견본 손패는 덱을 아예 거치지 않는다. 여기서 덱을 지으면
            // "16장이 아니다" 경고만 뜨고 아무도 그 카드를 뽑지 않는다.
            if (buildDeckOnStart && !UsesFixedHand)
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
                executor.OnExecuteFinished += HandleExecuteFinished;
            }

            Combat.OnParried += HandleParried;
            SubscribeAllyDeaths();
        }

        private void OnDisable()
        {
            if (executor != null)
            {
                executor.OnSlotConsumed -= HandleSlotConsumed;
                executor.OnExecuteFinished -= HandleExecuteFinished;
            }

            Combat.OnParried -= HandleParried;
            UnsubscribeAllyDeaths();
        }

        private void Update()
        {
            // 게이지와 쿨타임은 실제 시간으로 흐른다. 정지 중 자가 충전을 막는다.
            float dt = TimeControl.UnscaledDeltaTime;

            if (cooldownTimer > 0f) cooldownTimer -= dt;

            // 스킬 쿨타임도 게이지와 같은 규칙을 따른다 — 정지 중에는 흐르지 않는다.
            // 불릿타임에 오래 머물러 있다고 실시간 쿨이 공짜로 돌아오면 안 된다.
            if (!IsActive)
            {
                Gauge.Recover(gaugeRegen * dt);
                skillCooldowns.Tick(dt);
            }

            tactic.Tick(dt);
        }

        /// <summary>
        /// 대시 패링 보상. 막은 쪽이 아군일 때만 준다 —
        /// 적이 패링했는데 플레이어 게이지가 차면 안 된다.
        /// </summary>
        private void HandleParried(Combat defender, Combat attacker)
        {
            if (defender == null || defender.Owner == null) return;
            if (defender.Owner.Faction != Faction.Ally) return;

            Gauge.Recover(parryGaugeReward);

            BattleLog.Log(LogCategory.Combo,
                $"{BattleLog.Name(defender.Owner)} 패링 보상 — 게이지 +{parryGaugeReward:0.#} " +
                $"({Gauge.Ratio * 100f:0}%)", this);
        }

        public bool CanEnter
        {
            get
            {
                if (IsActive || executor == null || executor.IsRunning) return false;
                if (cooldownTimer > 0f) return false;
                if (Gauge.Ratio < requiredRatio) return false;
                if (manaCost > 0f && player != null)
                {
                    Energy mana = player.Energies.GetEnergy(EnergyType.Mana);
                    if (mana == null || mana.CurValue < manaCost) return false;
                }

                return true;
            }
        }

        /// <summary>진입이 막힌 이유. 로그 전용.</summary>
        public string BlockReason()
        {
            if (IsActive) return "이미 진입 중";
            if (executor == null) return "ComboExecutor 미연결";
            if (executor.IsRunning) return "이전 콤보 실행 중";
            if (cooldownTimer > 0f) return $"쿨타임 {cooldownTimer:0.##}s 남음";
            if (Gauge.Ratio < requiredRatio) return $"게이지 {Gauge.Ratio * 100f:0}% / 필요 {requiredRatio * 100f:0}%";
            return "마나 부족";
        }

        /// <summary>E키 요청. 실제 판정은 현재 전술 페이즈가 내린다.</summary>
        public bool Enter() => tactic != null && tactic.OnBulletTimeKey();

        /// <summary>Spacebar 요청. Order 페이즈에서만 Resolve로 넘어간다.</summary>
        public void Exit() => tactic?.OnExecuteKey();

        // ── 실시간 단발 사용 (U키) ───────────────────────

        /// <summary>
        /// 손패 맨 왼쪽 카드를 즉시 발동한다. 실시간 전투 전용.
        /// 조준은 자동 — 시전 동료 기준 가장 가까운 적을 잡는다.
        ///
        /// <see cref="ComboExecutor"/>를 거치지 않는다. Executor가 돌면
        /// <see cref="CanEnter"/>가 막혀 그동안 불릿타임 진입이 불가능해지기 때문이다.
        /// </summary>
        public bool UseTopCard()
        {
            if (Phase != TacticPhase.RealTime) return false;

            ComboSlot slot = hand.Get(0);

            if (slot.IsEmpty)
            {
                BattleLog.Warn(LogCategory.Deck, "손패가 비어 있다 — 덱과 Discard를 확인할 것", this);
                return false;
            }

            SkillData data = slot.Data;
            if (data == null)
            {
                // 발동할 수 없는 카드는 붙잡아 두지 않는다. 방치하면 손패 맨 앞이 영구히 막힌다.
                BattleLog.Warn(LogCategory.Deck, "SkillData가 비어 있는 카드 — 버리고 다음 장을 당긴다", this);
                hand.Dequeue();
                Recycle(slot.card);
                RefillHand();
                return false;
            }

            // 쿨타임은 카드를 소모하지 않는다 — 손패 맨 앞에 그대로 두고 시간이 지나면 다시 눌린다.
            float cool = SkillCooldownRemaining(data);
            if (cool > 0f)
            {
                string kind = skillCooldowns.OwnRemaining(data) >= cool ? "스킬 쿨" : "카드 공용 쿨";

                BattleLog.Log(LogCategory.Combo,
                    $"{data.skillName} 사용 대기 — {kind} {cool:0.0}s 남음", this);
                return false;
            }

            Ally caster = ResolveCaster(data.role);
            if (caster == null)
            {
                BattleLog.Warn(LogCategory.Combo,
                    $"{data.skillName} 사용 실패 — {data.role} 동료가 파티에 없거나 사망. 카드는 손패에 남는다", this);
                return false;
            }

            // 시전자가 벤치에 있으면 끌어올린다. 안 그러면 그 직업 카드가 손패 맨 앞에 있는 동안
            // U키가 통째로 먹통이 된다 — 유저는 왜 안 나가는지 알 길이 없다.
            // 태그 시스템이라 끌어올린다는 건 곧 조작 대상이 그 동료로 바뀐다는 뜻이다.
            if (swap != null && !swap.EnsureActive(caster))
            {
                BattleLog.Warn(LogCategory.Combo,
                    $"{data.skillName} 사용 실패 — {BattleLog.Name(caster)}를 필드에 세우지 못했다", this);
                return false;
            }

            if (!caster.CanCastCard)
            {
                BattleLog.Log(LogCategory.Combo,
                    $"{data.skillName} 사용 보류 — {BattleLog.Name(caster)}가 아직 이전 동작 중", this);
                return false;
            }

            hand.Dequeue();

            TargetInfo info = slot.aimed && slot.target.IsValid ? slot.target : caster.AutoTarget(data);
            caster.CastCard(data, in info, slot.card != null ? slot.card.DamageScale : 1f);

            StartSkillCooldown(data);

            BattleLog.Log(LogCategory.Combo,
                $"<b>즉시 사용</b> {data.skillName} | {BattleLog.Name(caster)} | 조준 {info.type}" +
                (SkillCooldownRemaining(data) > 0f ? $" | 쿨 {SkillCooldownRemaining(data):0.#}s" : ""), this);

            Recycle(slot.card);
            RefillHand();
            return true;
        }

        /// <summary>
        /// 다 쓴 카드를 버린 더미로. 견본 손패는 카드를 그 자리에서 찍어 내므로 회수하지 않는다 —
        /// 넣어 두면 아무도 뽑지 않는 더미만 무한히 커진다.
        /// </summary>
        private void Recycle(ComboCard card)
        {
            if (UsesFixedHand) return;
            discard.Add(card);
        }

        /// <summary>
        /// 카드 한 장을 쓴 대가. 공용 쿨과 그 스킬 쿨을 함께 건다.
        /// 배율은 둘 다에 먹인다 — 하나만 줄이면 템포가 어긋난다.
        /// </summary>
        public void StartSkillCooldown(SkillData data)
        {
            if (!realtimeCooldown) return;

            float mul = Mathf.Max(0f, realtimeCooldownMul);

            skillCooldowns.StartGlobal(realtimeGlobalCooldown * mul);

            if (data != null)
                skillCooldowns.Start(data, data.cooldown * mul);
        }

        // ── 손패 조작 (불릿타임 중 UI가 호출) ─────────────

        /// <summary>손패 두 칸의 순서를 바꾼다. 실행 순서가 그대로 바뀐다.</summary>
        public bool SwapHand(int a, int b)
        {
            if (!AllowsCardEdit) return false;
            if (!hand.Swap(a, b)) return false;

            PredictHand();
            return true;
        }

        /// <summary>조준을 확정해 해당 칸에 박제한다.</summary>
        public bool SetHandTarget(int idx, in TargetInfo target)
        {
            if (!AllowsCardEdit) return false;
            if (!hand.SetTarget(idx, in target)) return false;

            PredictHand();
            return true;
        }

        /// <summary>손패 순서대로 상태 사슬을 시뮬레이션한다. 체인 성립 표시용.</summary>
        public void PredictHand() => predictor?.Simulate(hand.Slots);

        // ── TacticState가 호출하는 실제 동작 ─────────────
        // 페이즈가 "언제"를 결정하고, 여기가 "무엇을"을 담당한다.

        /// <summary>진입 코스트 지불. Freeze 진입 시 1회.</summary>
        public void PayEntryCost()
        {
            if (manaCost > 0f && player != null)
                player.Energies.GetEnergy(EnergyType.Mana)?.Lose(manaCost);
        }

        public void FreezeTime()
        {
            TimeControl.Scale = 0f;

            BattleLog.Log(LogCategory.Bullet,
                $"<b>불릿타임 진입</b> — TimeControl.Scale = 0 (Time.timeScale 미사용) | 마나 {manaCost:0.#} 소모", this);
        }

        public void ResumeTime() => TimeControl.Scale = 1f;

        public void ConsumeGauge() => Gauge.Lose(Gauge.MaxValue);

        public void StartCooldown() => cooldownTimer = cooldown;

        public void CancelTargeting() => targetSelector?.Cancel();

        /// <summary>덱에서 뽑아 손패를 4장까지 채운다. 덱이 비면 Discard가 섞여 되돌아온다.</summary>
        public void RefillHand()
        {
            if (UsesFixedHand)
            {
                RefillFixedHand();
                return;
            }

            int drawn = hand.Refill(deck, discard);
            if (drawn > 0)
                BattleLog.Log(LogCategory.Deck,
                    $"손패 보충 {drawn}장 → {hand.Count}장 | 덱 {deck.Count} | Discard {discard.Count}", this);
        }

        /// <summary>
        /// 견본 손패 보충. 덱·Discard를 아예 건드리지 않는다 —
        /// 여기서 나간 카드는 회수할 필요가 없다(<see cref="HandleSlotConsumed"/>가 넣는 Discard는
        /// 고정 손패가 쓰지 않으므로 그냥 쌓였다가 무시된다).
        /// </summary>
        private void RefillFixedHand()
        {
            int added = 0;

            // 리스트가 전부 null이면 아래 continue가 영원히 돌 수 있다.
            // 손패를 다 채우거나 리스트를 한 바퀴 다 훑으면 그만둔다.
            int tries = Hand.Size + fixedHand.Count;

            while (hand.Count < Hand.Size && tries-- > 0)
            {
                SkillData data = fixedHand[fixedCursor];
                fixedCursor = (fixedCursor + 1) % fixedHand.Count;

                if (data == null) continue;
                if (!hand.Add(new ComboCard(data))) break;

                added++;
            }

            if (added > 0)
                BattleLog.Log(LogCategory.Deck,
                    $"<b>견본 손패</b> 보충 {added}장 → {hand.Count}장 | {DescribeHand()}", this);
        }

        /// <summary>손패를 "A → B → C" 한 줄로. 로그에서 체인 순서를 눈으로 확인하는 용도.</summary>
        private string DescribeHand()
        {
            var names = new string[hand.Count];
            for (int i = 0; i < hand.Count; i++)
            {
                SkillData d = hand.Get(i).Data;
                names[i] = d != null ? d.skillName : "?";
            }

            return string.Join(" → ", names);
        }

        /// <summary>
        /// 손패를 통째로 실행 큐로 굳힌다. 왼쪽부터 순서대로 들어간다.
        /// 시전자를 못 찾은 카드는 발동하지 못하므로 그대로 Discard로 보낸다.
        /// </summary>
        public Queue<ComboSlot> BuildQueueFromHand()
        {
            BattleLog.Log(LogCategory.Bullet, "<b>불릿타임 해제</b> — TimeControl.Scale = 1", this);

            var q = new Queue<ComboSlot>(Hand.Size);
            List<ComboSlot> all = hand.TakeAll();

            for (int i = 0; i < all.Count; i++)
            {
                ComboSlot s = all[i];
                SkillData data = s.Data;
                if (data == null)
                {
                    // 그냥 continue하면 카드가 덱에서 증발한다. Discard로 돌려보낸다.
                    Recycle(s.card);
                    continue;
                }

                Ally caster = ResolveCaster(data.role);
                if (caster == null)
                {
                    BattleLog.Warn(LogCategory.Combo,
                        $"{data.skillName} 건너뜀 — {data.role} 동료가 파티에 없거나 사망", this);
                    Recycle(s.card);
                    continue;
                }

                s.caster = caster;
                if (!s.aimed || !s.target.IsValid)
                    s.target = caster.AutoTarget(data);

                q.Enqueue(s);
            }

            BattleLog.Log(LogCategory.Combo,
                $"실행 큐 생성 {q.Count}장 | " +
                string.Join(" → ", Array.ConvertAll(q.ToArray(), s => s.Data != null ? s.Data.skillName : "?")), this);

            return q;
        }

        public void RaiseEnter() => OnEnter?.Invoke();

        public void RaiseExit() => OnExit?.Invoke();

        /// <summary>카드의 직업에 맞는 동료를 찾는다. 스킬은 직업 전용이다.</summary>
        public Ally ResolveCaster(Role role)
        {
            if (player == null) return null;

            foreach (Ally a in player.Party)
                if (a != null && a.Role == role && !a.Combat.IsDead)
                    return a;

            return null;
        }

        private void HandleSlotConsumed(ComboCard card)
        {
            // 견본 손패는 매번 새 ComboCard를 찍어 낸다. 버린 더미에 넣으면 회수되지 않은 채 계속 쌓인다.
            if (UsesFixedHand) return;

            // 이미 걷어낸 직업의 카드는 되돌리지 않는다. 실행 큐는 손패를 통째로 굳혀 두므로
            // 콤보 도중에 시전자가 죽어도 남은 슬롯이 여기까지 흘러온다 —
            // 그대로 넣으면 덱이 소진될 때 Discard가 회수되면서 죽은 동료 카드가 되살아난다.
            if (purgeCardsOnAllyDeath && IsOrphanCard(card))
            {
                BattleLog.Log(LogCategory.Deck,
                    $"{(card.Data != null ? card.Data.skillName : "(빈 카드)")} — " +
                    "시전 직업이 전멸해 버린 더미로 보내지 않고 소멸", this);
                return;
            }

            // 사용한 카드는 즉시 소멸 이동.
            discard.Add(card);
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
        }

        /// <summary>
        /// 한 직업의 카드를 덱 · 손패 · 버린 더미에서 통째로 걷어낸다.
        /// <b>버린 더미까지 지우는 게 핵심</b> — 남겨 두면 덱이 소진될 때 회수돼 되살아난다.
        /// </summary>
        public int PurgeRole(Role role)
        {
            bool Match(ComboCard c) => c != null && c.Data != null && c.Data.role == role;

            int fromDeck = deck.RemoveAll(Match);
            int fromHand = hand.RemoveAll(Match);
            int fromDiscard = discard.RemoveAll(Match);
            int total = fromDeck + fromHand + fromDiscard;

            BattleLog.Log(LogCategory.Deck,
                $"<b>{role} 카드 {total}장 제거</b> — 덱 {fromDeck} · 손패 {fromHand} · 버린 더미 {fromDiscard} → " +
                $"덱 {deck.Count} · 손패 {hand.Count} · 버린 더미 {discard.Count}", this);

            if (total == 0) return 0;

            // 손패에 구멍이 났으면 메운다. 실행 중에는 건드리지 않는다 —
            // 아직 Discard로 안 간 카드가 다시 뽑히기 때문(HandleExecuteFinished가 대신 채운다).
            if (executor == null || !executor.IsRunning)
                RefillHand();

            PredictHand();
            return total;
        }

        private void HandleExecuteFinished()
        {
            // 콤보가 전부 끝난 뒤에 다시 4장을 채운다.
            // 실행 도중에 채우면 아직 Discard로 안 간 카드가 다시 뽑힐 수 있다.
            RefillHand();
        }

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
            List<ComboCard> partyCards = player != null ? CollectPartyCards(ref ignored) : null;

            return SkillCatalog.Pool(AvailableRoles(), partyCards);
        }

        /// <summary>덱을 다시 짓고 손패를 채운다. 시작 덱을 화면에서 짠 뒤 그쪽이 부른다.</summary>
        public void RebuildDeckAndHand()
        {
            BuildDeck();
            RefillHand();
        }

        /// <summary>
        /// 포스트 배틀에서만 호출. 전투 중 덱 수정은 막는다.
        /// 카드는 <b>Discard에 적재</b>하고 덱은 0장으로 둔다 — 첫 드로우가 회수 · 셔플을 겸한다.
        ///
        /// <b>런 덱이 있으면 그것이 이긴다.</b> 파티 장착 카드는 런의 첫 씨앗일 뿐이고,
        /// 그 뒤로는 레벨업으로 얻은 카드까지 든 <see cref="RunProgression.Cards"/>가 진짜 덱이다.
        /// 여기서 매번 파티로 새로 짜면 스테이지를 넘어가는 순간 얻은 카드가 전부 사라진다.
        /// </summary>
        public void BuildDeck()
        {
            if (player == null) return;

            int empty = 0;
            List<ComboCard> partyCards = CollectPartyCards(ref empty);

            // 첫 씬에서 한 번만 씨를 뿌리고, 그 뒤로는 런 덱을 그대로 읽는다.
            // 테스트 모드는 그 씨앗을 <b>비운다</b> — 0장으로 시작해 레벨업으로만 카드가 들어온다.
            RunProgression run = RunProgression.Current;
            run.SeedDeck(StartupMode == DeckStartupMode.Empty ? null : partyCards);

            // 장수가 아니라 Seeded로 가른다. 테스트 모드의 0장 덱을 "아직 안 짰다"로 읽으면
            // 파티 카드 16장이 도로 부어진다.
            List<ComboCard> cards = run.Seeded ? new List<ComboCard>(run.Cards) : partyCards;

            deck.Clear();
            discard.Clear();
            discard.AddRange(cards);

            BattleLog.Log(LogCategory.Deck,
                $"덱 구성 완료 — {cards.Count}장을 Discard에 적재 (덱 0장에서 시작) | 런 Lv.{run.Level}", this);

            if (empty > 0)
                BattleLog.Warn(LogCategory.Deck,
                    $"<b>SkillData가 비어 있는 카드 {empty}장을 덱에서 제외했다.</b> " +
                    "Ally 인스펙터의 Equipped 항목에 스킬 에셋을 지정할 것.", this);

            // 장수는 <b>파티 장착분</b>으로 따진다. 런 덱은 레벨업으로 늘고 합성으로 줄어드는 게
            // 정상이라 여기서 세면 성장할 때마다 거짓 경고가 뜬다.
            if (partyCards.Count != Prototype.Deck.Size)
                BattleLog.Warn(LogCategory.Deck,
                    $"파티 장착 카드가 {partyCards.Count}장이다(목표 {Prototype.Deck.Size}). " +
                    "동료 4명 × 장착 4장을 확인할 것.", this);
        }

        /// <summary>파티 4명이 장착한 카드를 걷는다. SkillData가 빈 카드는 세어서 제외한다.</summary>
        private List<ComboCard> CollectPartyCards(ref int empty)
        {
            var cards = new List<ComboCard>(Prototype.Deck.Size);

            foreach (Ally a in player.Party)
            {
                if (a == null) continue;

                foreach (ComboCard c in a.Equipped)
                {
                    if (c == null) continue;

                    // SkillData가 없는 카드는 영영 발동할 수 없다.
                    // 덱에 들이면 손패 맨 앞을 막아 U키가 먹통이 되므로 여기서 잘라 낸다.
                    if (c.Data == null)
                    {
                        empty++;
                        continue;
                    }

                    cards.Add(c);
                }
            }

            return cards;
        }

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

            if (fusedFrom != null && fuseCount > 0)
            {
                int eaten = ConsumeNormalCopies(fusedFrom, fuseCount);

                BattleLog.Log(LogCategory.Deck,
                    $"합성 재료 회수 — {fusedFrom.skillName} {eaten}/{fuseCount}장", this);
            }

            PlaceInHand(card);

            // 합성으로 손패가 빘을 수 있다. 남은 칸은 평소대로 덱에서 채운다.
            RefillHand();

            BattleLog.Log(LogCategory.Deck,
                $"카드 획득 — {card.Data.skillName}{(card.Golden ? " <color=#FFD166>(황금)</color>" : "")} " +
                $"→ 손패 | 덱 {deck.Count} · 손패 {hand.Count} · Discard {discard.Count}", this);
        }

        /// <summary>
        /// 카드를 손패에 바로 꽂는다. 손패가 차 있으면 <b>맨 오른쪽</b> 칸을 덱으로 돌려보내고
        /// 그 자리를 내준다 — 왼쪽부터 발동하므로 오른쪽 끝이 유저의 다음 몇 수에 가장 영향이 적다.
        /// </summary>
        private void PlaceInHand(ComboCard card)
        {
            if (hand.IsFull)
            {
                ComboSlot pushed = hand.RemoveAt(hand.Count - 1);

                // 밀려난 카드는 잃지 않는다. 덱 아래로 돌아가 제 차례에 다시 나온다.
                if (pushed.card != null) deck.Add(pushed.card);
            }

            // 손패가 꽉 찬 채로 여기 오는 경우는 없지만, 실패해도 카드를 잃지는 않는다.
            if (!hand.Add(card)) discard.Add(card);
        }

        /// <summary>
        /// 합성 재료를 지금 판에서 걷어낸다. <b>버린 더미 → 덱 → 손패</b> 순서다 —
        /// 손패는 유저가 눈으로 짜 둔 순서라 마지막까지 아낀다.
        /// 황금은 재료가 아니므로 건드리지 않는다.
        /// </summary>
        private int ConsumeNormalCopies(SkillData data, int count)
        {
            int budget = count;

            bool Match(ComboCard c)
            {
                if (budget <= 0) return false;
                if (c == null || c.Golden || c.Data != data) return false;

                budget--;
                return true;
            }

            int removed = discard.RemoveAll(Match);
            removed += deck.RemoveAll(Match);
            removed += hand.RemoveAll(Match);

            return removed;
        }
    }
}
