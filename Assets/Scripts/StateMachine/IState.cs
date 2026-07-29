namespace Prototype
{
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
}
