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
}
