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

        protected virtual void Awake()
        {
            Owner = GetComponent<Entity>();
        }

        /// <summary>Entity가 스케일된 dt로 호출한다.</summary>
        public virtual void Tick(float dt)
        {
            Clear();
        }

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
        public void ClearIntent() => Clear();
    }
}
