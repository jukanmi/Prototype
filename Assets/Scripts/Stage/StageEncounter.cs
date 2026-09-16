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
        /// 칸이 거는 보정. 정예면 강화, 방 크기 보정을 받는 칸(<see cref="RunMapRules.GetsRoomModifier"/>)이면 그 칸의 방 크기.
        /// 칸이 없으면(씬 단독 실행 · 런 밖) <see cref="EncounterModifier.None"/>.
        ///
        /// <b>칸 종류만 받는 서명은 지웠다.</b> 남겨 두면 그걸 부르는 자리가 방 크기를 빠뜨린 채 조용히 100%로 돈다.
        /// 보스 칸에 방 크기가 박혀 있어도(손으로 만든 지도) 여기서 한 번 더 막는다 — 보스 패턴은 방 크기를 전제로 저작한다.
        /// </summary>
        public static EncounterModifier For(MapNode node)
        {
            if (node == null) return EncounterModifier.None;

            int elite = node.Kind == MapNodeKind.Elite ? EliteNodeEvery : 0;
            int room = RunMapRules.GetsRoomModifier(node.Kind) ? node.RoomPercent : 0;

            return new EncounterModifier(elite, room);
        }

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
}
