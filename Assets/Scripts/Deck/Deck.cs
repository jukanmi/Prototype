// 덱 한 벌 — 더미 · 카드 한 장 · 뽑기 규칙.
// ComboCard와 DeckRules는 Deck 밖에서 쓸 일이 없다.

using System.Collections.Generic;
using System;
using UnityEngine;

namespace Prototype
{
    // ══ Deck ═══════════════════════════════════════════

    /// <summary>
    /// <b>맨 위에서 순서대로</b> 뽑는다 — 그래서 <see cref="Shuffle"/>이 실제 순서를 정한다.
    /// 덱이 비면 Discard를 회수해 재셔플한다.
    /// </summary>
    [Serializable]
    public class Deck
    {
        /// <summary>
        /// <b>만석 기준값</b> — 동료 4명 × 장착 4장. 스킬 표를 짤 때의 기준이다
        /// (<c>SkillTableBuilder</c>).
        ///
        /// <b>런타임 검사에 이 상수를 쓰지 말 것.</b> 3인 파티나 동료가 영구 사망한 뒤에는
        /// 목표가 12장·8장으로 줄고, 그때도 덱은 정상으로 굴러간다
        /// (<see cref="Draw"/>가 버린 더미를 회수한다). 지금 인원 기준의 목표는
        /// <see cref="DeckRules.TargetSize(int)"/>가 준다.
        /// </summary>
        public const int Size = 16;

        [SerializeField] private List<ComboCard> cards = new List<ComboCard>();

        public int Count => cards.Count;
        public IReadOnlyList<ComboCard> Cards => cards;

        public event Action OnChanged;

        public Deck() { }

        public Deck(IEnumerable<ComboCard> source)
        {
            cards.AddRange(source);
        }

        public void Add(ComboCard card)
        {
            if (card == null) return;
            cards.Add(card);
            OnChanged?.Invoke();
        }

        public void Shuffle()
        {
            for (int i = cards.Count - 1; i > 0; i--)
            {
                int j = UnityEngine.Random.Range(0, i + 1);
                (cards[i], cards[j]) = (cards[j], cards[i]);
            }

            OnChanged?.Invoke();
        }

        /// <summary>
        /// 맨 위에서 n장 뽑는다. 덱이 모자라면 Discard를 회수해 섞은 뒤 계속 채운다.
        /// </summary>
        public List<ComboCard> Draw(int n, Discard discard = null)
        {
            var drawn = new List<ComboCard>(n);

            for (int i = 0; i < n; i++)
            {
                if (cards.Count == 0)
                {
                    if (discard == null || discard.Count == 0) break;
                    Rebuild(discard);
                    if (cards.Count == 0) break;
                }

                drawn.Add(cards[0]);
                cards.RemoveAt(0);
            }

            BattleLog.Log(LogCategory.Deck,
                $"드로우 {drawn.Count}/{n}장 | 덱 잔여 {cards.Count} | " +
                string.Join(", ", drawn.ConvertAll(c => c.Data != null ? c.Data.skillName : "?")));

            OnChanged?.Invoke();
            return drawn;
        }

        /// <summary>Discard를 전부 회수해 덱으로 되돌리고 섞는다.</summary>
        public void Rebuild(Discard discard)
        {
            if (discard == null) return;

            int recovered = discard.Count;
            cards.AddRange(discard.TakeAll());
            Shuffle();

            BattleLog.Log(LogCategory.Deck, $"덱 소진 → Discard {recovered}장 회수 후 재셔플 (덱 {cards.Count})");
        }

        /// <summary>조건에 맞는 카드를 덱에서 걷어낸다. 지운 장수를 돌려준다.</summary>
        public int RemoveAll(Predicate<ComboCard> match)
        {
            if (match == null) return 0;

            int removed = cards.RemoveAll(match);
            if (removed > 0) OnChanged?.Invoke();
            return removed;
        }

        /// <summary>포스트 배틀에서만 호출한다. 전투 중 덱 수정은 막는다.</summary>
        public void Replace(IEnumerable<ComboCard> newCards)
        {
            cards.Clear();
            if (newCards != null) cards.AddRange(newCards);
            OnChanged?.Invoke();
        }

        public void Clear()
        {
            cards.Clear();
            OnChanged?.Invoke();
        }
    }

    /// <summary>
    /// 손패 4장. <b>큐처럼</b> 굴러간다 — 왼쪽(0번)이 다음에 나갈 카드이고,
    /// 한 장이 빠지면 나머지가 왼쪽으로 당겨지고 오른쪽 끝이 덱에서 채워진다.
    ///
    /// 카드만이 아니라 조준값까지 함께 들고 있으므로(<see cref="ComboSlot"/>)
    /// 손패 자체가 곧 콤보 실행 순서다. 별도 슬롯 보드가 필요 없다.
    /// </summary>
    [Serializable]
    public class Hand
    {
        public const int Size = 4;

        [SerializeField] private List<ComboSlot> slots = new List<ComboSlot>();

        public int Count => slots.Count;
        public bool IsFull => slots.Count >= Size;

        /// <summary>ComboBoardUI · ComboExecutor가 그대로 받는다.</summary>
        public IReadOnlyList<ComboSlot> Slots => slots;

        public event Action OnChanged;

        public ComboSlot Get(int idx)
            => idx >= 0 && idx < slots.Count ? slots[idx] : default;

        public ComboCard GetCard(int idx) => Get(idx).card;

        public SkillData GetData(int idx) => Get(idx).Data;

        /// <summary>
        /// 덱에서 뽑아 <see cref="Size"/>까지 채운다.
        /// 덱이 비어 있으면 <see cref="Deck.Draw"/>가 Discard를 회수해 섞는다.
        /// </summary>
        public int Refill(Deck deck, Discard discard)
        {
            if (deck == null) return 0;

            int need = Size - slots.Count;
            if (need <= 0) return 0;

            List<ComboCard> drawn = deck.Draw(need, discard);
            for (int i = 0; i < drawn.Count; i++)
                slots.Add(new ComboSlot { card = drawn[i] });

            if (drawn.Count > 0) OnChanged?.Invoke();
            return drawn.Count;
        }

        /// <summary>
        /// 카드 한 장을 오른쪽 끝에 놓는다. 손패가 이미 <see cref="Size"/>면 거부한다.
        ///
        /// 덱을 거치지 않는 유일한 통로다 — <b>견본 콤보</b>(고정 손패)가 쓴다.
        /// 시연·튜닝에서 매번 같은 4장이 같은 순서로 와야 콤보 한 싸이클을 비교할 수 있는데,
        /// 셔플을 거치면 그게 불가능하다.
        /// </summary>
        public bool Add(ComboCard card)
        {
            if (card == null || slots.Count >= Size) return false;

            slots.Add(new ComboSlot { card = card });
            OnChanged?.Invoke();
            return true;
        }

        /// <summary>
        /// 한 칸을 지목해 빼낸다. 오른쪽 칸들이 왼쪽으로 당겨지므로 <b>인덱스가 밀린다</b>.
        /// 레벨업으로 받은 카드를 손패에 바로 꽂을 때, 밀려날 칸을 빼내는 데 쓴다.
        /// </summary>
        public ComboSlot RemoveAt(int idx)
        {
            if (idx < 0 || idx >= slots.Count) return default;

            ComboSlot s = slots[idx];
            slots.RemoveAt(idx);

            OnChanged?.Invoke();
            return s;
        }

        /// <summary>맨 왼쪽 카드를 꺼낸다. 나머지는 왼쪽으로 당겨진다.</summary>
        public ComboSlot Dequeue()
        {
            if (slots.Count == 0) return default;

            ComboSlot s = slots[0];
            slots.RemoveAt(0);
            OnChanged?.Invoke();
            return s;
        }

        /// <summary>
        /// 조건에 맞는 카드가 든 칸을 손패에서 걷어낸다. 지운 장수를 돌려준다.
        /// 남은 칸은 왼쪽으로 당겨지므로 <b>인덱스가 밀린다</b> —
        /// 조준 대기 중인 UI가 있으면 갱신을 받아야 한다.
        /// </summary>
        public int RemoveAll(Predicate<ComboCard> match)
        {
            if (match == null) return 0;

            int removed = slots.RemoveAll(s => match(s.card));
            if (removed > 0) OnChanged?.Invoke();
            return removed;
        }

        /// <summary>두 칸의 순서를 바꾼다. 조준값도 카드를 따라 같이 이동한다.</summary>
        public bool Swap(int a, int b)
        {
            if (a < 0 || a >= slots.Count) return false;
            if (b < 0 || b >= slots.Count || a == b) return false;

            (slots[a], slots[b]) = (slots[b], slots[a]);

            BattleLog.Log(LogCategory.Combo, $"손패 순서 변경 {a} ↔ {b}");

            OnChanged?.Invoke();
            return true;
        }

        /// <summary>유저가 확정한 조준값을 해당 칸에 박제한다.</summary>
        public bool SetTarget(int idx, in TargetInfo target)
        {
            if (idx < 0 || idx >= slots.Count) return false;

            ComboSlot s = slots[idx];
            s.target = target;
            s.aimed = true;
            slots[idx] = s;

            OnChanged?.Invoke();
            return true;
        }

        public void ClearTarget(int idx)
        {
            if (idx < 0 || idx >= slots.Count) return;

            ComboSlot s = slots[idx];
            s.target = TargetInfo.None;
            s.aimed = false;
            slots[idx] = s;

            OnChanged?.Invoke();
        }

        public List<ComboSlot> TakeAll()
        {
            var all = new List<ComboSlot>(slots);
            slots.Clear();
            OnChanged?.Invoke();
            return all;
        }
    }

    /// <summary>사용한 카드가 모이는 곳. 덱이 비면 통째로 회수된다.</summary>
    [Serializable]
    public class Discard
    {
        [SerializeField] private List<ComboCard> cards = new List<ComboCard>();

        public int Count => cards.Count;
        public IReadOnlyList<ComboCard> Cards => cards;

        public event Action OnChanged;

        public void Add(ComboCard card)
        {
            if (card == null) return;

            cards.Add(card);
            OnChanged?.Invoke();
        }

        public void AddRange(IEnumerable<ComboCard> range)
        {
            if (range == null) return;

            cards.AddRange(range);
            OnChanged?.Invoke();
        }

        /// <summary>조건에 맞는 카드를 버린 더미에서 걷어낸다. 지운 장수를 돌려준다.</summary>
        public int RemoveAll(Predicate<ComboCard> match)
        {
            if (match == null) return 0;

            int removed = cards.RemoveAll(match);
            if (removed > 0) OnChanged?.Invoke();
            return removed;
        }

        public void Clear()
        {
            cards.Clear();
            OnChanged?.Invoke();
        }

        public List<ComboCard> TakeAll()
        {
            var all = new List<ComboCard>(cards);
            cards.Clear();
            OnChanged?.Invoke();
            return all;
        }
    }

    // ══ ComboCard ═══════════════════════════════════════════

    /// <summary>
    /// 덱에 들어가는 카드 한 장. SkillData 에셋을 가리키고 드로우 보정만 따로 들고 있다.
    /// </summary>
    [Serializable]
    public class ComboCard
    {
        [SerializeField] private SkillData data;
        [Tooltip("드로우 가중치. 시동기가 안 나오는 패 꼬임을 막는 인챈트(선발투수 기믹).\n" +
                 "현재 미사용 — 드로우가 덱 맨 위에서 순서대로 가져가므로 셔플 단계에 편향을 주는 식으로 되살려야 한다.")]
        [SerializeField] private float drawWeight = 1f;

        [Tooltip("황금 카드. 데미지가 ExpRules.GoldenDamageMul배가 되고 테두리가 금색으로 뜬다.\n" +
                 "레벨업 2 · 3단계에서 뽑히거나, 같은 카드 세 장째를 합성하면 붙는다.")]
        [SerializeField] private bool golden;

        public SkillData Data => data;
        public float DrawWeight => Mathf.Max(0.01f, drawWeight);
        public bool Golden => golden;

        /// <summary>
        /// 이 카드로 낸 스킬의 데미지 배율. <see cref="SkillContext.damageScale"/>로 실려 나간다.
        /// 차징 배율과는 <b>곱해진다</b> — 둘은 서로 다른 층에서 붙는 값이다.
        /// </summary>
        public float DamageScale => golden ? ExpRules.GoldenDamageMul : 1f;

        /// <summary>시동기 여부. 지정하지 않으면 스킬의 공격 유형으로 판단한다.</summary>
        public bool IsStarter => data != null && data.IsStarterType;

        public Role Role => data != null ? data.role : Role.Tanker;

        public ComboCard(SkillData data, float drawWeight = 1f, bool golden = false)
        {
            this.data = data;
            this.drawWeight = drawWeight;
            this.golden = golden;
        }

        public ComboCard Clone() => new ComboCard(data, drawWeight, golden);

        /// <summary>같은 스킬의 황금판. 합성 결과를 만들 때 쓴다.</summary>
        public ComboCard AsGolden() => new ComboCard(data, drawWeight, true);
    }

    // ══ DeckRules ═══════════════════════════════════════════

    /// <summary>
    /// 이 파티가 짜야 할 덱의 크기. <b>순수 함수</b>다.
    ///
    /// <b>왜 상수가 아닌가.</b> <see cref="Deck.Size"/>는 16이고, 그건 "동료 4명 × 장착 4장"이
    /// 만석일 때의 값이다. 3인 파티는 12장이 <b>정상</b>인데 상수와 비교하면 매 스테이지
    /// 거짓 경고가 뜬다 — 그러면 경고를 무시하는 습관이 붙고, 진짜 저작 실수
    /// (장착이 3장인 동료)까지 같이 묻힌다.
    ///
    /// <b>덱이 작아도 기능은 멀쩡하다.</b> <see cref="Deck.Draw"/>가 덱이 비면 버린 더미를
    /// 회수해 다시 섞으므로 12장 덱도 손패 4장을 끊김 없이 유지한다. 달라지는 것은
    /// 순환 속도뿐이고(<see cref="CycleHands"/>), 그건 알려 줄 정보이지 경고할 오류가 아니다.
    /// </summary>
    public static class DeckRules
    {
        /// <summary>동료 <paramref name="memberCount"/>명이 짜는 덱의 목표 장수.</summary>
        public static int TargetSize(int memberCount)
            => memberCount <= 0 ? 0 : memberCount * Ally.EquipSlots;

        /// <summary>편성 화면이 쓴다. 빈 칸은 세지 않는다.</summary>
        public static int TargetSize(IReadOnlyList<PartyMemberData> party)
            => TargetSize(CountFilled(party));

        /// <summary>
        /// 런타임이 쓴다. <see cref="Player.Party"/>는 빈 칸에 <c>null</c>이 들어 있고,
        /// 영구 사망한 동료의 슬롯도 <see cref="PartyAssembler"/>가 지워 <c>null</c>이 된다.
        /// 그래서 이 값은 <b>지금 실제로 카드를 낼 수 있는 인원</b>과 같다.
        /// </summary>
        public static int TargetSize(IReadOnlyList<Ally> party)
            => TargetSize(CountFilled(party));

        public static int CountFilled(IReadOnlyList<PartyMemberData> party)
        {
            if (party == null) return 0;

            int n = 0;
            for (int i = 0; i < party.Count; i++)
                if (party[i] != null) n++;

            return n;
        }

        public static int CountFilled(IReadOnlyList<Ally> party)
        {
            if (party == null) return 0;

            int n = 0;
            for (int i = 0; i < party.Count; i++)
                if (party[i] != null) n++;

            return n;
        }

        /// <summary>
        /// 덱 한 바퀴가 몇 번의 손패인가. 4인이면 4핸드, 3인이면 3핸드다.
        ///
        /// 인원이 줄면 <b>같은 카드가 더 자주 돌아온다</b>. 편성 화면이 이 숫자를 보여 줘야
        /// "동료 하나가 빠지면 콤보 다양성이 준다"는 사실이 고르는 자리에서 읽힌다 —
        /// 전투에 들어가서야 체감하는 값이 아니다.
        /// </summary>
        public static int CycleHands(int deckSize)
            => deckSize <= 0 ? 0 : deckSize / Hand.Size;

        /// <summary>
        /// 장수가 목표와 맞는가. <b>목표보다 많아도 어긋난 것이다</b> —
        /// 인원 × 4를 넘는다는 건 어떤 동료가 5장을 들고 있다는 뜻이다.
        /// </summary>
        public static bool Matches(int actual, int target) => actual == target;

        /// <summary>
        /// 저작 실수의 설명. 맞으면 <c>null</c>.
        ///
        /// 인원이 적어서 덱이 작은 것은 <b>실수가 아니다</b> — 그건 목표 자체가 줄어드는
        /// 경우라 여기 걸리지 않는다. 여기 걸리는 것은 "4인인데 15장" 같은,
        /// 장착 칸이 빈 동료가 있다는 신호뿐이다.
        /// </summary>
        public static string Explain(int actual, int memberCount)
        {
            int target = TargetSize(memberCount);
            if (Matches(actual, target)) return null;

            return actual < target
                ? $"장착 카드가 {actual}장이다(동료 {memberCount}명 × {Ally.EquipSlots}장 = {target}장). " +
                  "빈 장착 칸이 있는 동료를 확인할 것."
                : $"장착 카드가 {actual}장이다(동료 {memberCount}명 기준 {target}장). " +
                  $"{Ally.EquipSlots}장을 넘게 든 동료가 있다.";
        }
    }
}
