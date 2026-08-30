using System;
using System.Collections.Generic;
using UnityEngine;

namespace Prototype
{
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
}
