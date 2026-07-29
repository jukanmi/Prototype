using System;
using System.Collections.Generic;
using UnityEngine;

namespace Prototype
{
    /// <summary>슬롯 한 칸. 카드와 유저가 찍은 조준값을 함께 보관한다.</summary>
    [Serializable]
    public struct ComboSlot
    {
        public ComboCard card;
        public TargetInfo target;
        /// <summary>이 카드를 실행할 동료. 카드의 직업으로 결정된다.</summary>
        public Ally caster;

        public bool IsEmpty => card == null;
        public SkillData Data => card != null ? card.Data : null;
    }

    /// <summary>
    /// 3~4칸짜리 콤보 슬롯. 배치할 때마다 예측을 갱신한다.
    /// </summary>
    public class ComboSlotBoard : MonoBehaviour
    {
        [SerializeField, Range(3, 4)] private int slotCount = 4;

        private ComboSlot[] slots;

        public int SlotCount => slotCount;
        public IReadOnlyList<ComboSlot> Slots => slots;

        /// <summary>배치 · 회수 · 순서 변경 후. UI와 Predictor가 구독한다.</summary>
        public event Action OnBoardChanged;

        private void Awake()
        {
            slots = new ComboSlot[slotCount];
        }

        public ComboSlot Get(int idx)
            => idx >= 0 && idx < slots.Length ? slots[idx] : default;

        /// <summary>카드를 슬롯에 놓는다. 조준값은 TargetSelector가 확정해 넘긴다.</summary>
        public bool Place(ComboCard card, int idx, in TargetInfo target, Ally caster)
        {
            if (card == null || idx < 0 || idx >= slots.Length) return false;
            if (!slots[idx].IsEmpty) return false;

            slots[idx] = new ComboSlot { card = card, target = target, caster = caster };

            BattleLog.Log(LogCategory.Combo,
                $"슬롯 {idx} ← {(card.Data != null ? card.Data.skillName : "?")} " +
                $"| 시전자 {BattleLog.Name(caster)} | 조준 {target.type}");

            OnBoardChanged?.Invoke();
            return true;
        }

        /// <summary>슬롯을 비우고 카드를 돌려준다.</summary>
        public ComboCard Remove(int idx)
        {
            if (idx < 0 || idx >= slots.Length || slots[idx].IsEmpty) return null;

            ComboCard card = slots[idx].card;
            slots[idx] = default;

            BattleLog.Log(LogCategory.Combo, $"슬롯 {idx} 회수 → 손패");

            OnBoardChanged?.Invoke();
            return card;
        }

        public void Reorder(int from, int to)
        {
            if (from < 0 || from >= slots.Length) return;
            if (to < 0 || to >= slots.Length || from == to) return;

            (slots[from], slots[to]) = (slots[to], slots[from]);

            BattleLog.Log(LogCategory.Combo, $"슬롯 순서 변경 {from} ↔ {to}");

            OnBoardChanged?.Invoke();
        }

        /// <summary>빈 칸을 건너뛰고 앞에서부터 실행 큐를 만든다.</summary>
        public Queue<ComboSlot> BuildQueue()
        {
            var q = new Queue<ComboSlot>(slots.Length);
            for (int i = 0; i < slots.Length; i++)
                if (!slots[i].IsEmpty) q.Enqueue(slots[i]);

            BattleLog.Log(LogCategory.Combo,
                $"실행 큐 생성 {q.Count}장 | " +
                string.Join(" → ", System.Array.ConvertAll(q.ToArray(), s => s.Data != null ? s.Data.skillName : "?")));

            return q;
        }

        /// <summary>실행 후 정리. 배치돼 있던 카드를 전부 돌려준다.</summary>
        public List<ComboCard> ClearAll()
        {
            var placed = new List<ComboCard>();
            for (int i = 0; i < slots.Length; i++)
            {
                if (slots[i].IsEmpty) continue;
                placed.Add(slots[i].card);
                slots[i] = default;
            }

            OnBoardChanged?.Invoke();
            return placed;
        }

        public int FirstEmptyIndex()
        {
            for (int i = 0; i < slots.Length; i++)
                if (slots[i].IsEmpty) return i;
            return -1;
        }
    }
}
