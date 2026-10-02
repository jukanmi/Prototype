using UnityEngine;

namespace Prototype
{
    /// <summary>
    /// 몸에 붙어 그 몸의 의도를 채우는 <b>AI 드라이버</b>. 실제 실행은 상태머신이 한다.
    ///
    /// <b>유저 조작은 여기 없다.</b> 조종사는 씬에 하나뿐인 <see cref="PlayerPilot"/>이고,
    /// 몸에는 안 붙는다. 이 계층은 <b>몸마다 자기 AI가 필요한 적</b>(<see cref="EnemyControl"/>)만 쓴다.
    ///
    /// 의도 자체는 <see cref="Entity"/>가 들고 있다 — 여기는 거기에 써 넣기만 하는 얇은 층이다.
    /// 몸이 바뀌어도 의도가 사라지지 않아야 하기 때문이다(<see cref="Entity.Command"/> 주석 참고).
    /// </summary>
    public abstract class Control : MonoBehaviour
    {
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

        /// <summary>이번 프레임 의도를 비운다. 선입력은 건드리지 않는다.</summary>
        protected void Clear() => Owner?.ClearCommand();

        /// <summary>이번 프레임 의도를 채운다.</summary>
        protected void Drive(Command command, Vector3 moveDirection)
            => Owner?.Drive(command, moveDirection);
    }
}
