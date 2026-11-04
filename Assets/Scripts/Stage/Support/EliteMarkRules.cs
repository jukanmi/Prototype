using System;
using System.Collections.Generic;
using UnityEngine;

namespace Prototype
{
    // ══ EliteMarkRules ═══════════════════════════════════════════

    /// <summary>
    /// 강화 개체 표시의 판단 · 수치 전부. 순수 함수라 씬 없이 검증한다.
    ///
    /// <b>왜 발밑 고리인가.</b> 적에게는 이미 신호가 빽빽하다 — 몸 색은 전투 상태 · 예고 · 아머 · 디버프가,
    /// 머리 위는 상태 글자(1.9) · 차지 게이지(1.6) · 상태 막대(2.25~)가 쓴다. 강화를 그중 하나에 얹으면
    /// 둘 다 안 읽힌다(경직 흰색 위에 강화색이 섞이면 "맞았나?"가 흐려진다). <b>바닥만 비어 있다.</b>
    ///
    /// 색은 보라다. 바닥에 이미 깔리는 색 — 사거리 원(하늘 · 빨강), 넉백 예고(주황 · 연두),
    /// 적 공격 범위(빨강), 소환 예고(빨강) — 과 겹치지 않는다. 몸의 바인드 보라와 색상은 가깝지만 자리가 달라 섞이지 않는다.
    /// 고리 <b>모양</b> 자체가 신호라 색약자도 "발밑에 뭔가 있다"는 읽는다. 글자는 적 체력 HUD가 맡는다.
    ///
    /// 몸집은 안 키운다. <see cref="BeltScrollView.MultiplyBodyScale"/>로 키울 수는 있지만 판정(루트 콜라이더)은
    /// 그대로라, 커 보이는 가장자리를 때렸는데 안 맞는 일이 생긴다.
    /// </summary>
    public static class EliteMarkRules
    {
        /// <summary>강화 개체 게임오브젝트 이름 뒤에 붙는 표시. 소환 창구와 HUD가 같은 값을 읽는다.</summary>
        public const string NameSuffix = "_강화";

        /// <summary>적 체력 HUD 이름 앞에 붙는 글자.</summary>
        public const string HudPrefix = "강화 · ";

        /// <summary>발밑 고리 · HUD 이름 색. #B359FF</summary>
        public static readonly Color AuraColor = new Color(0.702f, 0.349f, 1f);

        /// <summary>고리 지름(월드 단위, 깊이 배율 전). 그림자(0.9)보다 넉넉히 커야 그림자에 안 묻힌다.</summary>
        public const float Diameter = 1.7f;

        /// <summary>
        /// 초당 맥동 횟수. 느리게 둔다 — 빨리 깜빡이면 소환 예고(빨간 깜빡임)처럼 "곧 뭔가 온다"로 읽힌다.
        /// </summary>
        public const float PulseHz = 1.1f;

        public const float MinAlpha = 0.55f;
        public const float MaxAlpha = 0.95f;

        /// <summary>맥동할 때 커지는 비율의 최대치.</summary>
        public const float PulseGrow = 0.06f;

        /// <summary>
        /// 고리를 그리는가. 죽었거나 꺼졌거나 <b>벽 뒤에 숨어 있으면</b> 안 그린다 —
        /// 벽에서 걸어 나오는 적은 정렬로 벽 뒤에 가려 두는데(<see cref="EnemySpawnGuard.HiddenSortingOffset"/>),
        /// 바닥 고리만 먼저 보이면 경계 밖 허공에 보라 원이 뜬다.
        /// </summary>
        public static bool ShouldDraw(bool elite, bool dead, bool active, bool hiddenBehindWall)
            => elite && !dead && active && !hiddenBehindWall;

        /// <summary>0~1로 오가는 맥동 위상. 시간이 멈춘 불릿타임에도 돌도록 unscaled 시간을 받는다.</summary>
        public static float Pulse(float unscaledTime)
            => 0.5f + 0.5f * Mathf.Sin(unscaledTime * PulseHz * Mathf.PI * 2f);

        public static float PulseAlpha(float unscaledTime) => Mathf.Lerp(MinAlpha, MaxAlpha, Pulse(unscaledTime));

        public static float PulseScale(float unscaledTime) => 1f + PulseGrow * Pulse(unscaledTime);

        /// <summary>소환된 적의 게임오브젝트 이름.</summary>
        public static string ObjectName(string enemyId, bool elite)
            => elite ? enemyId + NameSuffix : enemyId;

        /// <summary>
        /// HUD에 적을 이름. 오브젝트 이름 뒤의 <see cref="NameSuffix"/>는 떼고, 강화면 앞에 <see cref="HudPrefix"/>를 붙인다 —
        /// "Warrior_강화"를 그대로 보여 주면 개발용 이름으로 읽힌다.
        /// </summary>
        public static string HudName(string objectName, bool elite)
        {
            string n = objectName ?? "";
            if (n.EndsWith(NameSuffix, StringComparison.Ordinal)) n = n.Substring(0, n.Length - NameSuffix.Length);

            return elite ? HudPrefix + n : n;
        }
    }
}
