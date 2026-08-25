using System.Collections.Generic;

namespace Prototype
{
    /// <summary>
    /// 고른 선택지를 덱에 반영하는 규칙. 순수 함수라 리스트만 넘기면 된다.
    ///
    /// <b>합성은 덱을 늘리지 않는다</b> — 들고 있던 일반 두 장이 사라지고 황금 한 장이 들어오므로
    /// 장수는 오히려 하나 줄고 질이 오른다. 이게 "같은 카드만 계속 나오는" 답답함의 출구다.
    /// </summary>
    public static class CardGrantRules
    {
        /// <summary>
        /// 고른 카드를 덱에 넣는다. <b>실제로 들어간 카드</b>를 돌려준다(실패하면 null) —
        /// 부르는 쪽이 같은 인스턴스를 손패에도 꽂으므로, 새로 만들게 두면 런 덱과 손패에
        /// 서로 다른 몸이 하나씩 생긴다.
        ///
        /// <paramref name="pick"/>이 합성인데 보유분이 모자라면 <b>합성 없이 한 장만</b> 들어온다.
        /// 선택지를 짠 뒤 덱이 바뀌었을 때(동료 사망으로 카드가 걷힌 경우 등) 조용히 어긋나느니
        /// 카드 한 장이라도 손에 쥐여 주는 편이 낫다.
        /// </summary>
        public static ComboCard Apply(List<ComboCard> deck, in CardOffer pick)
        {
            if (deck == null || pick.IsEmpty) return null;

            if (pick.fuses)
                RemoveNormalCopies(deck, pick.data, CardOfferRules.FuseCopies - 1);

            var card = new ComboCard(pick.data, WeightOf(pick.data), pick.golden);
            deck.Add(card);

            return card;
        }

        /// <summary>
        /// 일반 카드를 최대 <paramref name="count"/>장까지 걷어낸다. 실제로 걷어낸 장수를 돌려준다.
        /// 황금은 건드리지 않는다.
        /// </summary>
        public static int RemoveNormalCopies(List<ComboCard> deck, SkillData data, int count)
        {
            if (deck == null || data == null || count <= 0) return 0;

            int removed = 0;

            for (int i = deck.Count - 1; i >= 0 && removed < count; i--)
            {
                ComboCard c = deck[i];
                if (c == null || c.Golden || c.Data != data) continue;

                deck.RemoveAt(i);
                removed++;
            }

            return removed;
        }

        /// <summary>
        /// 새 카드의 드로우 가중치. 시작 덱을 굽는 SkillTableBuilder가 시동기에 2를 주는 것과
        /// 같은 규칙이라, 레벨업으로 얻은 카드가 시작 덱과 다르게 굴지 않는다.
        /// </summary>
        public static float WeightOf(SkillData data)
            => data != null && data.IsStarterType ? 2f : 1f;
    }
}
