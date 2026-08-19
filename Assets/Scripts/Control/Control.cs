using UnityEngine;

namespace Prototype
{
    /// <summary>
    /// 이번 프레임의 의도를 담는 얇은 계층. 실제 실행은 상태머신이 한다.
    /// </summary>
    public abstract class Control : MonoBehaviour
    {
        public Command Command { get; protected set; } = Command.None;
        public Vector3 MoveDirection { get; protected set; }

        protected Entity Owner { get; private set; }

        /// <summary>
        /// 평타 선입력. <b>여기 두는 이유</b>: Control이 "이번 프레임의 의도" 계층이고
        /// 버퍼는 그 의도의 유효기간을 늘린 것뿐이라 같은 층에 있어야 한다.
        ///
        /// 덤으로 <b>AI는 연타를 칠 수단 자체가 없어진다</b> — <see cref="AllyControl"/>·
        /// <see cref="EnemyControl"/>은 이 버퍼를 채우지 않는다. bool 플래그로 막는 것보다 강한 보장이다.
        /// </summary>
        private AttackInputBuffer attackBuffer;

        protected virtual void Awake()
        {
            Owner = GetComponent<Entity>();
        }

        /// <summary>Entity가 스케일된 dt로 호출한다.</summary>
        public virtual void Tick(float dt)
        {
            Clear();
        }

        /// <summary>선입력을 채운다. 유저 입력을 읽는 Control만 부른다.</summary>
        protected void BufferAttack(float window) => attackBuffer.Press(window);

        /// <summary>선입력 창을 흘린다. <c>Clear()</c>와 달리 프레임마다 지워지지 않는다.</summary>
        protected void TickAttackBuffer(float dt) => attackBuffer.Tick(dt);

        /// <summary>선입력이 살아 있는지 들여다본다. 비우지 않는다.</summary>
        public bool HasAttackBuffer => attackBuffer.HasInput;

        /// <summary>남아 있으면 true를 내고 비운다.</summary>
        public bool TryConsumeAttackBuffer() => attackBuffer.TryConsume();

        /// <summary>선입력을 버린다. 공격에 들어가는 순간, 그 입력을 두 번 쓰지 않으려고 부른다.</summary>
        public void ClearAttackBuffer() => attackBuffer.Clear();

        protected void Clear()
        {
            Command = Command.None;
            MoveDirection = Vector3.zero;
        }

        public void Consume() => Command = Command.None;

        /// <summary>
        /// 이 Control이 몸에서 손을 뗄 때. 남은 의도를 통째로 비운다.
        /// <see cref="Consume"/>는 명령만 지우고 이동 방향은 남기므로 여기서는 못 쓴다 —
        /// 태그로 내려간 몸이 마지막 이동 방향을 물고 있으면 다시 섰을 때 혼자 걸어간다.
        /// </summary>
        public void ClearIntent()
        {
            Clear();
            // 선입력도 같이 버린다. 안 그러면 내려간 몸이 물고 있던 입력이
            // 다시 섰을 때 터져 아무도 안 누른 평타가 나간다.
            attackBuffer.Clear();
        }
    }
}
