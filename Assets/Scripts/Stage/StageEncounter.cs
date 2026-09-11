// 스테이지는 <b>조우의 목록</b>이다.
//
// 웨이브와 아레나 라운드의 차이는 스테이지 종류가 아니라 조우 하나의 속성이다.
// 둘 다 하는 일이 같다 — 적 한 무리를 내보내고, 다 잡히면 다음으로 넘어간다.
// 갈리는 것은 셋뿐이고, 셋 다 여기 칸으로 들어와 있다.
//   무엇이 시작시키나 · 카메라가 어디에 잠기나 · 문이 있나
//
// 계획서: docs/Stage_Encounter_Unification_Plan.md (1항 · 2항)

using System;
using UnityEngine;

namespace Prototype
{
    // ══ EncounterTrigger ═══════════════════════════════════════════

    /// <summary>무엇이 이 조우를 여는가.</summary>
    public enum EncounterTrigger
    {
        /// <summary>스테이지가 시작되면 바로. 목록의 첫 조우가 보통 이것이다.</summary>
        Immediate = 0,

        /// <summary>앞 조우가 끝나고 숨 돌리는 틈 뒤에. 웨이브 방의 기본 동작이다.</summary>
        AfterPrevious = 1,

        /// <summary>
        /// 플레이어가 자리의 진입선을 넘을 때. 아레나가 이것이다.
        /// <b>자리가 없으면 성립하지 않는다</b> — 넘을 선이 없기 때문이다.
        /// </summary>
        CrossLine = 2,
    }

    // ══ StageEncounter ═══════════════════════════════════════════

    /// <summary>
    /// 목록 한 칸. <b>무엇이 · 언제 · 어디서 · 끝나고 얼마나</b>.
    ///
    /// 개수를 정수로 따로 두지 않는다 — 목록을 들면 개수는 이미 그 길이고,
    /// 정수 칸을 따로 두면 진실이 두 군데가 되어 반드시 어긋난다(계획서 2.2).
    /// </summary>
    [Serializable]
    public struct StageEncounter
    {
        [Tooltip("이 조우에 나오는 적. 비면 그 칸은 건너뛴다.")]
        public WaveAsset content;

        [Tooltip("무엇이 이 조우를 여는가.")]
        public EncounterTrigger trigger;

        [Tooltip("카메라가 잠기는 자리와 문. 비우면 방 전체가 자리다(웨이브 방).")]
        public EncounterSite site;

        [Tooltip("이 조우가 끝나고 다음이 열리기까지의 숨 돌릴 틈.")]
        [Min(0f)] public float gapSeconds;

        /// <summary>자리에 묶인 조우인가. 거짓이면 방 하나가 통째로 자리다.</summary>
        public bool HasSite => site != null;

        /// <summary>
        /// 실제로 적용할 시작 조건.
        ///
        /// 자리 없는 조우에 <see cref="EncounterTrigger.CrossLine"/>이 박혀 있으면
        /// <b>넘을 선이 없어 영영 안 열린다.</b> 그런 조우는 앞 조우 뒤로 접는다 —
        /// 스테이지가 그 자리에서 멈추는 것보다 낫고, 저작 검증이 따로 말해 준다.
        /// </summary>
        public EncounterTrigger Trigger
            => trigger == EncounterTrigger.CrossLine && !HasSite
                ? EncounterTrigger.AfterPrevious
                : trigger;

        /// <summary>앞 조우가 끝나기를 기다리는가.</summary>
        public bool WaitsForPrevious => Trigger == EncounterTrigger.AfterPrevious;

        /// <summary>음수로 저작된 틈은 0으로 본다.</summary>
        public float GapSeconds => Mathf.Max(0f, gapSeconds);
    }
}
