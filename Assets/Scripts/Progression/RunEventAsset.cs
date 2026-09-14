// 이벤트 칸 하나 = 애셋 하나. 글 한 토막과 선택지 몇 개.
//
// 선택지의 결과는 <b>숫자 칸 셋</b>(골드 · 체력 · 카드)으로만 표현한다. 결과를 코드로 짜게 두면
// 이벤트마다 스크립트가 하나씩 생긴다 — 지금 필요한 결과는 전부 이 셋의 조합이다.
// 계획서: docs/Run_Map_Plan.md (2.7)

using System;
using System.Collections.Generic;
using UnityEngine;

namespace Prototype
{
    // ══ RunEventChoice ═══════════════════════════════════════════

    /// <summary>선택지 한 줄. 고르면 칸 셋이 한꺼번에 적용된다.</summary>
    [Serializable]
    public struct RunEventChoice
    {
        [Tooltip("버튼에 적히는 글.")]
        public string label;

        [Tooltip("골드 증감. 음수면 그만큼 가지고 있어야 고를 수 있다.")]
        public int goldDelta;

        [Tooltip("파티 전원의 체력 비율 증감. 0.2면 20% 회복, -0.1이면 10% 피해. 산 몸은 0으로 안 떨어진다.")]
        [Range(-1f, 1f)] public float hpDelta;

        [Tooltip("켜면 이 파티가 쓸 수 있는 카드 중 한 장을 무작위로 받는다.")]
        public bool grantCard;

        [Tooltip("고른 뒤 보여 줄 글. 비우면 결과 수치만 적는다.")]
        [TextArea] public string outcome;
    }

    // ══ RunEventAsset ═══════════════════════════════════════════

    [CreateAssetMenu(fileName = "Event_", menuName = "Prototype/이벤트", order = 30)]
    public class RunEventAsset : ScriptableObject
    {
        public string title = "";

        [TextArea(3, 8)] public string body = "";

        public RunEventChoice[] choices = new RunEventChoice[0];

        public int ChoiceCount => choices != null ? choices.Length : 0;
    }

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
