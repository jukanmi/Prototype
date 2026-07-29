using System;
using System.Collections.Generic;
using UnityEngine;

namespace Prototype
{
    /// <summary>
    /// 지휘 모드의 관제탑. 게이지 · 드로우 · 슬롯 · 실행을 이어 붙인다.
    /// <b>Time.timeScale은 쓰지 않는다</b> — UI와 애니메이션까지 멈추면
    /// 정작 손패를 조작할 수 없기 때문(결정 로그 ⑥).
    /// </summary>
    public class BulletTimeController : MonoBehaviour
    {
        [Header("참조")]
        [SerializeField] private Player player;
        [SerializeField] private ComboSlotBoard board;
        [SerializeField] private ComboExecutor executor;
        [SerializeField] private ComboPredictor predictor;
        [SerializeField] private TargetSelector targetSelector;

        [Header("게이지")]
        [SerializeField] private float maxGauge = 100f;
        [Tooltip("초당 자연 충전량.")]
        [SerializeField] private float gaugeRegen = 8f;
        [Tooltip("진입에 필요한 게이지 비율.")]
        [Range(0f, 1f)][SerializeField] private float requiredRatio = 1f;

        [Header("코스트 — 둘 다 구현해 두고 실험 후 결정(결정 로그 ⑤)")]
        [SerializeField] private float manaCost = 0f;
        [SerializeField] private float cooldown = 0f;

        [Header("드로우")]
        [SerializeField] private int drawCount = Prototype.Hand.Size;
        [Tooltip("Start에서 파티 장착 카드로 덱을 짠다. 포스트 배틀 흐름이 붙기 전까지만.")]
        [SerializeField] private bool buildDeckOnStart = true;

        [Header("전술 페이즈")]
        [Tooltip("Freeze 체류 시간(비배율 초). 0이면 다음 프레임에 곧바로 Order로 넘어간다. 셔플 · UI 확대 연출을 넣을 자리.")]
        [SerializeField] private float freezeDuration = 0f;

        private readonly Deck deck = new Deck();
        private readonly Hand hand = new Hand();
        private readonly Discard discard = new Discard();

        private TacticStateMachine tactic;
        private float cooldownTimer;

        public Energy Gauge { get; private set; }

        /// <summary>전술 상태머신. 입력은 전부 여기로 넣는다.</summary>
        public TacticStateMachine Tactic => tactic;

        public TacticPhase Phase => tactic != null ? tactic.Phase : TacticPhase.RealTime;

        /// <summary>손패가 펼쳐져 있는 구간(Freeze · Order). 기존 코드 호환용.</summary>
        public bool IsActive => Phase == TacticPhase.Freeze || Phase == TacticPhase.Order;

        /// <summary>불릿타임 중 0. 게임플레이에만 적용되는 자체 배율.</summary>
        public float TimeScale { get; private set; } = 1f;

        public float FreezeDuration => freezeDuration;

        public Deck Deck => deck;
        public Hand Hand => hand;
        public Discard Discard => discard;
        public ComboSlotBoard Board => board;
        public ComboPredictor Predictor => predictor;
        public ComboExecutor Executor => executor;

        public event Action OnEnter;
        public event Action OnExit;

        private void Awake()
        {
            Gauge = new Energy(EnergyType.BulletTimeGauge, maxGauge);
            if (player == null) player = FindAnyObjectByType<Player>();
            if (board == null) board = GetComponentInChildren<ComboSlotBoard>();
            if (executor == null) executor = GetComponentInChildren<ComboExecutor>();
            if (predictor == null) predictor = GetComponentInChildren<ComboPredictor>();
            if (targetSelector == null) targetSelector = GetComponentInChildren<TargetSelector>();

            tactic = new TacticStateMachine(this);
        }

        private void Start()
        {
            if (buildDeckOnStart)
                BuildDeckFromParty();

            tactic.Begin();
        }

        private void OnEnable()
        {
            if (executor != null)
            {
                executor.OnSlotConsumed += HandleSlotConsumed;
                executor.OnExecuteFinished += HandleExecuteFinished;
            }
        }

        private void OnDisable()
        {
            if (executor != null)
            {
                executor.OnSlotConsumed -= HandleSlotConsumed;
                executor.OnExecuteFinished -= HandleExecuteFinished;
            }
        }

        private void Update()
        {
            // 게이지와 쿨타임은 실제 시간으로 흐른다. 정지 중 자가 충전을 막는다.
            float dt = TimeControl.UnscaledDeltaTime;

            if (cooldownTimer > 0f) cooldownTimer -= dt;

            if (!IsActive)
                Gauge.Recover(gaugeRegen * dt);

            tactic.Tick(dt);
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
            TimeScale = 0f;
            TimeControl.Scale = 0f;

            BattleLog.Log(LogCategory.Bullet,
                $"<b>불릿타임 진입</b> — TimeControl.Scale = 0 (Time.timeScale 미사용) | 마나 {manaCost:0.#} 소모", this);
        }

        public void ResumeTime()
        {
            TimeScale = 1f;
            TimeControl.Scale = 1f;
        }

        /// <summary>덱 셔플 루틴 포함. 덱이 비면 Deck.Draw가 Discard를 되돌려 섞는다.</summary>
        public void DrawHand()
        {
            hand.Fill(deck.Draw(drawCount, discard));
            BattleLog.Log(LogCategory.Bullet, $"손패 {hand.Count}장 | 덱 {deck.Count} | Discard {discard.Count}", this);
        }

        public void ConsumeGauge() => Gauge.Lose(Gauge.MaxValue);

        public void StartCooldown() => cooldownTimer = cooldown;

        public void CancelTargeting() => targetSelector?.Cancel();

        /// <summary>슬롯을 큐로 굳히고 보드를 비운다. 미사용 손패는 그대로 소멸.</summary>
        public Queue<ComboSlot> CollectQueue()
        {
            BattleLog.Log(LogCategory.Bullet, "<b>불릿타임 해제</b> — TimeControl.Scale = 1", this);

            Queue<ComboSlot> queue = board != null ? board.BuildQueue() : null;
            board?.ClearAll();

            // 배치하지 않고 남은 손패는 그대로 버린다 — 사용 즉시 소멸 규칙.
            int dumped = hand.Count;
            discard.AddRange(hand.TakeAll());

            if (dumped > 0)
                BattleLog.Log(LogCategory.Deck, $"미사용 손패 {dumped}장 소멸 → Discard {discard.Count}", this);

            return queue;
        }

        public void RaiseEnter() => OnEnter?.Invoke();

        public void RaiseExit() => OnExit?.Invoke();

        // ── 손패 → 슬롯 ─────────────────────────────────

        /// <summary>
        /// 손패 카드를 슬롯에 놓는다. 조준이 필요한 스킬이면 TargetSelector가 먼저 값을 확정한다.
        /// </summary>
        public bool PlaceFromHand(int handIndex, int slotIndex, in TargetInfo target)
        {
            if (board == null || tactic == null || !tactic.AllowsCardEdit) return false;

            ComboCard card = hand.Get(handIndex);
            if (card == null || card.Data == null) return false;

            Ally caster = ResolveCaster(card.Data.role);
            if (caster == null)
            {
                BattleLog.Warn(LogCategory.Combo,
                    $"{card.Data.skillName} 배치 실패 — {card.Data.role} 동료가 파티에 없거나 사망", this);
                return false;
            }

            if (!board.Place(card, slotIndex, in target, caster)) return false;

            hand.Take(handIndex);
            predictor?.Simulate(board.Slots);
            return true;
        }

        /// <summary>슬롯에서 카드를 회수해 손패로 되돌린다.</summary>
        public void RecallToHand(int slotIndex)
        {
            if (board == null || tactic == null || !tactic.AllowsCardEdit) return;

            ComboCard card = board.Remove(slotIndex);
            if (card == null) return;

            hand.Return(card);
            predictor?.Simulate(board.Slots);
        }

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
            // 사용한 카드는 즉시 소멸 이동.
            discard.Add(card);
        }

        private void HandleExecuteFinished()
        {
            // 실행이 끝나야 다음 진입을 허용한다.
            cooldownTimer = Mathf.Max(cooldownTimer, 0f);
        }

        /// <summary>포스트 배틀에서만 호출. 전투 중 덱 수정은 막는다.</summary>
        public void BuildDeckFromParty()
        {
            if (player == null) return;

            var cards = new List<ComboCard>(Prototype.Deck.Size);
            foreach (Ally a in player.Party)
            {
                if (a == null) continue;
                foreach (ComboCard c in a.Equipped)
                    if (c != null) cards.Add(c);
            }

            deck.Replace(cards);
            deck.Shuffle();

            BattleLog.Log(LogCategory.Deck, $"덱 구성 완료 — {cards.Count}장 (목표 {Prototype.Deck.Size})", this);
            if (cards.Count != Prototype.Deck.Size)
                BattleLog.Warn(LogCategory.Deck, "덱 장수가 16장이 아니다. 동료 4명 × 장착 4장을 확인할 것.", this);
        }
    }
}
