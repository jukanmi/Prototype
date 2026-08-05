using System;

namespace Prototype
{
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
        /// 무조건 전이. <b>사망 처리</b>와 <b>콤보 지휘</b> 전용(결정 로그 ②③).
        /// ComboExecutor가 동료의 상태머신을 강탈할 때 쓴다.
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
