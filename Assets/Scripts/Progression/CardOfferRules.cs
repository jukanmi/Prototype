using System;
using System.Collections.Generic;

namespace Prototype
{
    /// <summary>레벨업 선택지 한 칸.</summary>
    public struct CardOffer
    {
        public SkillData data;

        /// <summary>황금으로 나왔는지. 합성이 서면 확률과 무관하게 참이다.</summary>
        public bool golden;

        /// <summary>
        /// 고르면 <b>합성</b>이 되는 자리. 이미 같은 카드 일반판을 두 장 들고 있다는 뜻이다.
        /// UI가 카드 우측 상단에 ★를 다는 근거이고, 고르면 그 두 장이 사라지고 황금 한 장이 된다.
        /// </summary>
        public bool fuses;

        public bool IsEmpty => data == null;
    }

    /// <summary>
    /// 레벨업 선택지를 짜는 규칙. 순수 함수다.
    ///
    /// <b>난수를 주입받는다.</b> <c>UnityEngine.Random</c>을 직접 부르면 20% · 50% 경계를
    /// 검증할 방법이 없어진다. 실사용은 <c>() =&gt; UnityEngine.Random.value</c>를 넘긴다.
    ///
    /// 소비 순서는 <b>고르기 먼저, 황금 판정 나중</b>이다 —
    /// 3장을 다 뽑은 뒤 그 순서대로 황금을 굴린다. 테스트가 이 순서에 기댄다.
    /// </summary>
    public static class CardOfferRules
    {
        /// <summary>한 번에 보여 주는 카드 수.</summary>
        public const int OfferCount = 3;

        /// <summary>몇 장이 모이면 합성인가. 두 장을 들고 있을 때 <b>세 장째</b>가 선택지에 뜨면 성립한다.</summary>
        public const int FuseCopies = 3;

        /// <summary>
        /// 선택지를 짠다. 모집단이 <see cref="OfferCount"/>보다 적으면 있는 만큼만 낸다.
        /// </summary>
        /// <param name="pool">뽑을 수 있는 스킬 전부. null·중복은 걸러진다.</param>
        /// <param name="owned">지금 덱에 든 카드. 합성 판정에 쓴다.</param>
        /// <param name="tier">0 · 1 · 2. 황금 확률을 정한다.</param>
        /// <param name="roll">0 이상 1 미만을 돌려주는 난수원.</param>
        public static List<CardOffer> Build(IReadOnlyList<SkillData> pool,
                                            IReadOnlyList<ComboCard> owned,
                                            int tier,
                                            Func<float> roll)
        {
            var offers = new List<CardOffer>(OfferCount);
            if (pool == null || roll == null) return offers;

            List<SkillData> bag = Distinct(pool);
            if (bag.Count == 0) return offers;

            int take = Math.Min(OfferCount, bag.Count);

            for (int i = 0; i < take; i++)
            {
                int index = Index(roll(), bag.Count);
                offers.Add(new CardOffer { data = bag[index] });
                bag.RemoveAt(index);
            }

            // 황금은 고른 순서 그대로 한 장씩 따로 굴린다. 선택지 전체에 한 번만 굴리면
            // "세 장 다 황금 아니면 세 장 다 일반"이 되어 고를 이유가 사라진다.
            float chance = ExpRules.GoldenChanceOf(tier);

            for (int i = 0; i < offers.Count; i++)
            {
                CardOffer offer = offers[i];

                bool fuses = CountNormalCopies(owned, offer.data) == FuseCopies - 1;

                // 합성은 확률을 이긴다. 어차피 황금이 되는 자리라 굴려 봐야 결과가 같다.
                // 다만 난수 소비 횟수는 유지한다 — 안 그러면 뒤 카드의 결과가 밀린다.
                bool rolled = roll() < chance;

                offer.fuses = fuses;
                offer.golden = fuses || rolled;

                offers[i] = offer;
            }

            return offers;
        }

        /// <summary>
        /// 이 스킬의 <b>일반</b> 카드를 몇 장 들고 있는지. 황금은 세지 않는다 —
        /// 황금 위의 등급이 없어서 합성해 봐야 갈 곳이 없다.
        /// </summary>
        public static int CountNormalCopies(IReadOnlyList<ComboCard> owned, SkillData data)
        {
            if (owned == null || data == null) return 0;

            int n = 0;
            for (int i = 0; i < owned.Count; i++)
            {
                ComboCard c = owned[i];
                if (c != null && !c.Golden && c.Data == data) n++;
            }

            return n;
        }

        /// <summary>0 이상 1 미만을 칸 번호로. 1이 들어와도 범위를 넘지 않게 물린다.</summary>
        private static int Index(float value, int count)
        {
            int i = (int)(value * count);
            if (i < 0) return 0;
            return i >= count ? count - 1 : i;
        }

        private static List<SkillData> Distinct(IReadOnlyList<SkillData> pool)
        {
            var bag = new List<SkillData>(pool.Count);

            for (int i = 0; i < pool.Count; i++)
            {
                SkillData d = pool[i];
                if (d != null && !bag.Contains(d)) bag.Add(d);
            }

            return bag;
        }
    }
}
