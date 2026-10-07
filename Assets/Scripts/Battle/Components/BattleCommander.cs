using UnityEngine;

namespace Prototype
{
    /// <summary>
    /// 주인공 몸과 독립된 불릿타임 진입·실행 입력.
    ///
    /// <b>절대 꺼지지 않는 오브젝트에 붙어야 한다</b>. 태그로 내려가는 몸에 붙이면
    /// 교대하는 순간 되돌아올 키까지 함께 죽는다. 같은 이유로
    /// <c>PlayerInput</c> · <see cref="PlayerInputController"/> · <see cref="InputMapSwitcher"/>도
    /// 여기 함께 둔다.
    ///
    /// 몸을 실제로 움직이는 건 <see cref="PlayerPilot"/>이다. 여기는 몸과 무관한 키만 본다.
    /// </summary>
    [DefaultExecutionOrder(-150)]
    public class BattleCommander : MonoBehaviour
    {
        [SerializeField] private BulletTimeController bulletTime;

        private void Awake()
        {
            if (bulletTime == null) bulletTime = FindAnyObjectByType<BulletTimeController>();
        }

        /// <summary>
        /// 지휘 입력은 시간이 멈춰 있어도 받아야 한다. <c>TimeControl.Scale</c>은 자체 배율이라
        /// <c>Time.timeScale</c>을 건드리지 않으므로 Update는 정지 중에도 그대로 돈다
        /// (결정 로그 ⑥).
        /// </summary>
        private void Update()
        {
            PlayerInputController input = PlayerInputController.Instance;
            if (input == null || input.GameplaySuspended) return;

            if (bulletTime != null)
            {
                if (input.JumpPressed && bulletTime.IsActive)
                {
                    input.ConsumeJumpPress();
                    bulletTime.Tactic.OnCancelKey();
                    return;
                }
                if (input.BulletTimePressed)
                    bulletTime.Enter();
            }

        }
    }
}
