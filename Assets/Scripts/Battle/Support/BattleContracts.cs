// Battle 층의 계약 모음.
// 스킬 컷인(ISkillCutin)은 SkillCutinUI가, 재시작(IStageRestarter)은 BattleSceneController가,
// 총공격 편성 문(IAssaultEditGate)은 칸 지휘자(BattleSceneController · NodeSceneController)가 구현한다.
using System.Collections;
using UnityEngine;

namespace Prototype
{
    // ══ ISkillCutin ═══════════════════════════════════════════

    /// <summary>
    /// 스킬이 나가기 직전에 끼어드는 인트로 연출.
    /// <see cref="ComboExecutor"/>는 이 계약만 알고 UI 구현은 모른다 —
    /// 미배선(null)이면 컷인 없이 곧바로 스킬로 간다.
    /// </summary>
    public interface ISkillCutin
    {
        /// <summary>
        /// 컷인을 한 번 재생한다. Executor가 반환된 열거자를 소진할 때까지 슬롯이 대기한다.
        /// <b>호출 즉시</b> 연출을 시작해야 한다(시간 정지 · 패널 표시) —
        /// 반환값을 펌프하지 않는 호출자도 상태 변화를 관측할 수 있어야 하기 때문.
        /// </summary>
        IEnumerator Play(Ally caster, SkillData data);

        /// <summary>
        /// 재생을 즉시 중단하고 정지시킨 시간을 되돌린다.
        /// <see cref="ComboExecutor.Abort"/>가 부른다 — StopCoroutine으로 잘린 코루틴은
        /// finally가 돌지 않아 <see cref="TimeControl.Scale"/>이 0에 묶이기 때문.
        /// </summary>
        void Cancel();
    }

    // ══ IStageRestarter ═════════════════════════════════════════════
    // 스테이지를 처음부터 다시 거는 쪽. [처음부터] 버튼(PauseMenu 안)은 이것만 알고,
    // 구현(BattleSceneController · App 층)을 모른다.
    public interface IStageRestarter
    {
        void RestartStage();
    }

    // ══ IAssaultEditGate ════════════════════════════════════════════
    // 총공격 편성을 지금 바꿔도 되는가 — 칸(노드) 씬의 지휘자가 답한다.
    // 편집창(AssaultEditorUI)과 런 바는 이 계약만 알고, 구현(FlowScene 층)을 모른다.

    /// <summary>
    /// 칸 하나의 편성 문. 판단 규칙은 <see cref="AssaultRules"/>에 있고,
    /// 구현은 "지금 이 칸의 상태"(싸우는 중 · 이겼다 · 떠나는 중)를 그 규칙에 넣어 답하기만 한다.
    /// </summary>
    public interface IAssaultEditGate
    {
        /// <summary>이 칸이 지금 싸움 밖인가 — 편성을 바꿔도 되는가.</summary>
        bool AssaultEditable { get; }
    }

    /// <summary>
    /// 지금 떠 있는 씬의 편성 문. 칸 · 마을의 지휘자가 켜질 때 걸고 꺼질 때 뗀다.
    ///
    /// <b>걸린 문이 없으면 닫혀 있다.</b> 지도 · 메뉴는 문이 없어 편성할 수 없다 —
    /// "지도 말고 걸어 다니는 곳에서, 싸움 밖에서만"이 규칙이다.
    /// </summary>
    public static class AssaultEditGate
    {
        private static IAssaultEditGate current;

        /// <summary>지금 편성을 바꿔도 되는가. 문이 없거나 그 칸이 싸우는 중이면 거짓.</summary>
        public static bool CanEdit
        {
            get
            {
                // 지휘자가 떼지 못하고 파괴됐으면(씬이 예외로 내려갔다 등) 닫힌 것으로 본다.
                if (current is Object owner && owner == null) current = null;
                return current != null && current.AssaultEditable;
            }
        }

        public static void Register(IAssaultEditGate gate)
        {
            if (gate != null) current = gate;
        }

        /// <summary>
        /// 자기 문일 때만 뗀다. 다음 칸이 먼저 걸었으면 그대로 둔다 —
        /// 씬을 겹쳐 올린 뒤 내리므로 새 칸의 OnEnable이 옛 칸의 OnDisable보다 먼저 올 수 있다.
        /// </summary>
        public static void Unregister(IAssaultEditGate gate)
        {
            if (ReferenceEquals(current, gate)) current = null;
        }

        /// <summary>Domain Reload가 꺼져 있으면 지난 플레이의 문이 static에 남는다.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => current = null;
    }
}
