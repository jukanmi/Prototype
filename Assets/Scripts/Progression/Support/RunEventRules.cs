using System;
using System.Collections.Generic;
using UnityEngine;

namespace Prototype
{
    // ══ RunEventRules ═══════════════════════════════════════════

    /// <summary>이벤트 선택지의 판단. 순수 함수라 씬 없이 검증한다.</summary>
    public static class RunEventRules
    {
        /// <summary>
        /// 고를 수 있는가. <b>골드가 모자라면 막는다</b> — 반만 내고 결과를 다 받는 것도,
        /// 결과만 받고 한 푼도 안 내는 것도 둘 다 공짜가 된다.
        /// </summary>
        public static bool CanChoose(in RunEventChoice choice, int gold)
            => choice.goldDelta >= 0 || gold >= -choice.goldDelta;

        /// <summary>
        /// 선택지를 적용한다. 고를 수 없으면 <b>아무것도 안 바꾸고</b> 거짓이다.
        ///
        /// 카드는 <paramref name="pool"/>에서 레벨업 1단계 규칙으로 한 장을 뽑는다(황금 없음).
        /// 모집단이 비어 있으면 카드만 빠지고 나머지는 적용된다 — 표 배선이 빠진 것이지
        /// 플레이어가 고른 것을 되돌릴 이유는 아니다.
        /// </summary>
        public static bool Apply(in RunEventChoice choice, RunProgression run,
                                 IReadOnlyList<PartyMemberData> roster,
                                 IReadOnlyList<SkillData> pool, Func<float> roll,
                                 out ComboCard granted)
        {
            granted = null;
            if (run == null || !CanChoose(in choice, run.Gold)) return false;

            if (choice.goldDelta > 0) run.AddGold(choice.goldDelta);
            else if (choice.goldDelta < 0) run.TrySpendGold(-choice.goldDelta);

            run.Party.ChangeHp(choice.hpDelta, roster);

            if (choice.grantCard && roll != null)
            {
                List<CardOffer> offers = CardOfferRules.Build(pool, run.Cards, 0, roll);
                if (offers.Count > 0) granted = run.Grant(offers[0]);
            }

            return true;
        }

        /// <summary>결과 글이 비었을 때 대신 적는 수치 요약. 아무 변화도 없으면 "아무 일도 없었다".</summary>
        public static string Summary(in RunEventChoice choice)
        {
            var parts = new List<string>(3);

            if (choice.goldDelta != 0) parts.Add($"골드 {choice.goldDelta:+#;-#}");
            if (!Mathf.Approximately(choice.hpDelta, 0f)) parts.Add($"체력 {choice.hpDelta:+0%;-0%}");
            if (choice.grantCard) parts.Add("카드 1장");

            return parts.Count > 0 ? string.Join(" · ", parts) : "아무 일도 없었다";
        }
    }
}
