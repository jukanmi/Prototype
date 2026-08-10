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

        /// <summary>스킬 명령일 때 몇 번 슬롯인지. 현재는 AI 경로에서만 쓴다.</summary>
        public int SkillIndex { get; protected set; } = -1;

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
            SkillIndex = -1;
        }

        public void Consume() => Command = Command.None;
    }
}
