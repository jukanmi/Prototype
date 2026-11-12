// 스테이지는 <b>조우의 목록</b>이다.
//
// 웨이브와 아레나 라운드의 차이는 스테이지 종류가 아니라 조우 하나의 속성이다.
// 둘 다 하는 일이 같다 — 적 한 무리를 내보내고, 다 잡히면 다음으로 넘어간다.
// 갈리는 것은 셋뿐이고, 셋 다 여기 칸으로 들어와 있다.
//   무엇이 시작시키나 · 카메라가 어디에 잠기나 · 문이 있나
//
// 계획서: docs/Stage_Encounter_Unification_Plan.md (1항 · 2항)
using System.Collections.Generic;
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

    // ══ EncounterModifier ═══════════════════════════════════════════

    /// <summary>
    /// 지도 칸이 조우에 거는 보정. <b>애셋을 복제해 고치지 않고, 소환 순간에 한 칸을 뒤집는다</b>(계획서 1.1).
    ///
    /// 웨이브 애셋은 씬 보드의 지점 이름에 묶여 있고 디렉터는 그걸 읽기만 한다 — 사본을 끼워 넣으면
    /// "보드 목록이냐 주입 목록이냐"라는 두 번째 진실이 생긴다. 강화 배율은 이미 소환 창구에 있으므로
    /// "이 한 기가 강화인가"만 정하면 충분하다.
    ///
    /// <b>방 크기도 같은 통로로 온다</b>(docs/Room_Size_Plan.md 6단계). 그 값을 쓰는 곳은 디렉터가 아니라 씬의
    /// <see cref="StageRoom"/>이다 — 벽을 옮기는 일은 소환보다 먼저, 파티가 서기 전에 끝나야 한다.
    /// </summary>
    public readonly struct EncounterModifier
    {
        /// <summary>몇 번째마다 강화하는가. 0이면 강화 보정 없음.</summary>
        public readonly int EliteEvery;

        /// <summary>
        /// 저장값. <b>0이 "보정 없음"</b>이다 — <see cref="None"/>이 <c>default</c>라 새 칸의 기본값이 0이기 때문이다.
        /// 읽을 때는 <see cref="RoomPercent"/>로 100으로 펴서 읽는다.
        /// </summary>
        private readonly int roomPercent;

        /// <summary>
        /// 두 값을 다 받는다. 한쪽만 받는 생성자를 두지 않는다 — 정예 간격만 넘기는 호출이 남으면
        /// 그 경로만 방 크기를 조용히 100%로 돈다(docs/Room_Size_Plan.md 6단계).
        /// </summary>
        public EncounterModifier(int eliteEvery, int roomPercent)
        {
            EliteEvery = Mathf.Max(0, eliteEvery);
            this.roomPercent = Mathf.Max(0, roomPercent);
        }

        /// <summary>저작 그대로. 씬 단독 실행과 보정 없는 칸이 이것이다.</summary>
        public static readonly EncounterModifier None = default;

        /// <summary>방 크기 비율(%). 보정이 없으면 100.</summary>
        public int RoomPercent => RoomRules.Normalize(roomPercent);

        /// <summary>강화 보정이 있는가. <see cref="EncounterModifierRules.IsElite"/>가 나눗셈 전에 이것을 본다.</summary>
        public bool HasElite => EliteEvery > 0;

        /// <summary>방 크기 보정이 있는가.</summary>
        public bool HasRoom => RoomPercent != RoomRules.FullPercent;

        public bool IsNone => !HasElite && !HasRoom;

        public override string ToString()
        {
            if (IsNone) return "보정 없음";
            if (!HasRoom) return $"{EliteEvery}기마다 강화";
            if (!HasElite) return $"방 {RoomPercent}%";

            return $"{EliteEvery}기마다 강화 · 방 {RoomPercent}%";
        }
    }

    /// <summary>보정의 판단. 순수 함수라 씬 없이 검증한다.</summary>
    public static class EncounterModifierRules
    {
        /// <summary>정예 칸에서 강화가 붙는 간격 — 대략 세 기 중 한 기.</summary>
        public const int EliteNodeEvery = 3;

        /// <summary>
        /// 이 한 기가 강화로 나오는가.
        ///
        /// <b>보스는 보정으로 강화하지 않는다.</b> 강화 배율은 역할을 가리지 않아서, 규칙이 안 막으면
        /// 레시피에 보스 씬을 정예로 넣는 순간 체력 2.5배 보스가 나온다. 저작에서 직접 켠 강화는 그대로 둔다.
        /// </summary>
        /// <param name="ordinal">조우 안에서 몇 번째 줄인가(증원은 증원 번호). 0부터.</param>
        public static bool IsElite(bool authored, EnemyRole role, int ordinal, in EncounterModifier modifier)
        {
            if (authored) return true;

            // IsNone 이 아니라 HasElite 를 본다. 방 크기 보정만 있는 칸은 IsNone 이 거짓인데 EliteEvery 가 0이라,
            // IsNone 으로 거르면 아래 나머지 연산이 0으로 나눈다.
            if (role == EnemyRole.Boss || !modifier.HasElite || ordinal < 0) return false;

            return ordinal % modifier.EliteEvery == 0;
        }
    }

    // ══ IStageContext ═══════════════════════════════════════════════
    // 스테이지를 연 쪽(런 · GameManager)이 넘겨주는 이번 판의 조건.
    // Stage는 이것만 알고 지도 칸(MapNode · Run 층)이나 GameManager(App 층)를 모른다.
    public interface IStageContext
    {
        /// <summary>들어와 있는 지도 칸이 있는가. 없으면 씬 단독 재생 · 런 밖.</summary>
        bool HasNode { get; }

        /// <summary>그 칸이 거는 보정. 칸이 없으면 <see cref="EncounterModifier.None"/>.</summary>
        EncounterModifier NodeModifier { get; }
    }

    /// <summary>구현이 꽂히는 자리. GameManager가 Awake에서 꽂고 OnDestroy에서 뺀다. 비어 있으면 저작 그대로 돈다.</summary>
    public static class StageContext
    {
        public static IStageContext Current { get; set; }
    }

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
