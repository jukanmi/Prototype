// 상태머신 — 규약(IState)과 기계(StateMachine)만. 누구의 상태인지 모른다.
// 엔티티 상태 구현은 Entities/Support/EntityStates.cs.

using System;
using UnityEngine;

namespace Prototype
{
    // ══ IState ═══════════════════════════════════════════

    /// <summary>
    /// 상태 하나. 자체 dt를 받는다 — Time.timeScale을 쓰지 않기 때문(결정 로그 ⑥).
    /// </summary>
    public interface IState
    {
        /// <summary>
        /// false면 <see cref="StateMachine.TryChangeState"/>가 전이를 거부한다.
        /// SkillState는 false — 슈퍼아머(결정 로그 ③).
        /// </summary>
        bool CanBeInterrupted { get; }

        void Enter();
        void Tick(float dt);
        void Exit();
    }

    // ══ StateMachine ═══════════════════════════════════════════

    /// <summary>
    /// 상태 전이 관문. 슈퍼아머와 콤보 지휘가 모두 여기를 통과한다.
    /// </summary>
    public class StateMachine
    {
        public IState CurState { get; private set; }

        /// <summary>로그 식별용. Entity가 자기 이름을 넣어 준다.</summary>
        public string OwnerName { get; set; } = "?";

        public event Action<IState, IState> OnStateChanged; // (prev, next)

        /// <summary>
        /// 일반 전이. 현재 상태가 중단 불가면 <b>거부</b>한다(결정 로그 ③).
        /// </summary>
        /// <returns>전이가 실제로 일어났으면 true.</returns>
        public bool TryChangeState(IState next)
        {
            if (next == null || next == CurState)
                return false;

            if (CurState != null && !CurState.CanBeInterrupted)
            {
                BattleLog.Log(LogCategory.State,
                    $"{OwnerName}: {BattleLog.StateName(CurState)} → {BattleLog.StateName(next)} <color=#FF6B6B>거부</color> (중단 불가)");
                return false;
            }

            Change(next);
            return true;
        }

        /// <summary>
        /// 무조건 전이. <b>사망 처리</b>와 <b>스킬 지휘</b> 전용(결정 로그 ②③).
        /// 직접 부르지 말고 Entity의 창구(ForceDead · BeginSkill · EndSkill · ForceIdle)를 탈 것.
        /// </summary>
        public void ForceChangeState(IState next)
        {
            if (next == null || next == CurState)
                return;

            if (CurState != null && !CurState.CanBeInterrupted)
                BattleLog.Log(LogCategory.State,
                    $"{OwnerName}: {BattleLog.StateName(CurState)} <color=#FFD166>관통</color> → {BattleLog.StateName(next)}");

            Change(next);
        }

        public void Tick(float dt)
        {
            CurState?.Tick(dt);
        }

        private void Change(IState next)
        {
            IState prev = CurState;
            prev?.Exit();
            CurState = next;
            next.Enter();

            BattleLog.Log(LogCategory.State,
                $"{OwnerName}: {BattleLog.StateName(prev)} → <b>{BattleLog.StateName(next)}</b>");

            OnStateChanged?.Invoke(prev, next);
        }
    }
}
