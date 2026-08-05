using System;
using System.Collections.Generic;
using UnityEngine;

namespace Prototype
{
    /// <summary>
    /// 16장 덱. 직업별 6종 중 4장씩 골라 4직업 = 16장.
    /// 덱이 비면 Discard를 회수해 재셔플한다.
    /// </summary>
    [Serializable]
    public class Deck
    {
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
        /// n장 드로우. 드로우 가중치가 높은 카드(시동기)가 먼저 뽑히도록 보정한다.
        /// 덱이 모자라면 Discard를 회수해 채운다.
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

                int index = PickWeightedIndex();
                drawn.Add(cards[index]);
                cards.RemoveAt(index);
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

        /// <summary>포스트 배틀에서만 호출한다. 전투 중 덱 수정은 막는다.</summary>
        public void Replace(IEnumerable<ComboCard> newCards)
        {
            cards.Clear();
            cards.AddRange(newCards);
            OnChanged?.Invoke();
        }

        private int PickWeightedIndex()
        {
            float total = 0f;
            for (int i = 0; i < cards.Count; i++)
                total += cards[i].DrawWeight;

            float roll = UnityEngine.Random.Range(0f, total);
            for (int i = 0; i < cards.Count; i++)
            {
                roll -= cards[i].DrawWeight;
                if (roll <= 0f) return i;
            }

            return cards.Count - 1;
        }
    }

    /// <summary>손패 5장. ASDFG 단축키에 순서대로 매핑된다.</summary>
    [Serializable]
    public class Hand
    {
        public const int Size = 5;

        [SerializeField] private List<ComboCard> cards = new List<ComboCard>();

        public int Count => cards.Count;
        public IReadOnlyList<ComboCard> Cards => cards;

        public event Action OnChanged;

        public ComboCard Get(int idx)
            => idx >= 0 && idx < cards.Count ? cards[idx] : null;

        public void Fill(IEnumerable<ComboCard> drawn)
        {
            cards.AddRange(drawn);
            if (cards.Count > Size)
                cards.RemoveRange(Size, cards.Count - Size);

            OnChanged?.Invoke();
        }

        /// <summary>슬롯에 배치하며 손패에서 빼낸다.</summary>
        public ComboCard Take(int idx)
        {
            ComboCard c = Get(idx);
            if (c == null) return null;

            cards.RemoveAt(idx);
            OnChanged?.Invoke();
            return c;
        }

        /// <summary>슬롯에서 회수한 카드를 손패로 되돌린다.</summary>
        public void Return(ComboCard card)
        {
            if (card == null || cards.Count >= Size) return;

            cards.Add(card);
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
            cards.AddRange(range);
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
