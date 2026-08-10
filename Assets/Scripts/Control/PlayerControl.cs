using UnityEngine;
using UnityEngine.InputSystem;

namespace Prototype
{
    /// <summary>
    /// 커맨더 조작. 직접 콤보를 치지 않고 이동 · 평타 · 불릿타임 지휘를 담당한다.
    /// </summary>
    public class PlayerControl : Control
    {
        [SerializeField] private float dashCooldown = 0.6f;

        private float dashTimer;

        /// <summary>Spacebar — 불릿타임 진입 · 해제 요청.</summary>
        public bool BulletTimePressed { get; private set; }
        /// <summary>Spacebar — 콤보 실행 요청.</summary>
        public bool ExecutePressed { get; private set; }
        /// <summary>Z — 손패 맨 왼쪽 카드 즉시 사용(실시간 전투).</summary>
        public bool CardUsePressed { get; private set; }

        public override void Tick(float dt)
        {
            Clear();
            BulletTimePressed = false;
            ExecutePressed = false;
            CardUsePressed = false;

            if (dashTimer > 0f) dashTimer -= dt;

            HandleInput();
        }

        private void HandleInput()
        {
            Keyboard kb = Keyboard.current;
            if (kb == null) return;

            // 지휘 입력은 시간 정지 중에도 받아야 하므로 상태와 무관하게 먼저 읽는다.
            // Space 한 키가 진입 · 해제 · 실행을 전부 맡는다.
            // 페이즈별 판정은 TacticState가 하므로 두 요청을 같은 키로 올려도 충돌하지 않는다.
            BulletTimePressed = kb.spaceKey.wasPressedThisFrame;
            ExecutePressed = kb.spaceKey.wasPressedThisFrame;
            CardUsePressed = kb.zKey.wasPressedThisFrame;

            // 정지 중에는 이동 · 평타 입력을 받지 않는다.
            // 방향키는 TargetSelector가 조준용으로 직접 읽어 간다.
            if (TimeControl.IsFrozen) return;

            if (Owner != null && Owner.IsBusy) return;
            if (Owner != null && CombatStateRules.IsStunned(Owner.Combat.CombatState)) return;

            // 벨트스크롤 — 좌우는 X축, 위아래는 Z축(깊이).
            float x = (kb.rightArrowKey.isPressed ? 1f : 0f) - (kb.leftArrowKey.isPressed ? 1f : 0f);
            float z = (kb.upArrowKey.isPressed ? 1f : 0f) - (kb.downArrowKey.isPressed ? 1f : 0f);
            MoveDirection = new Vector3(x, 0f, z);

            if (kb.xKey.wasPressedThisFrame)
            {
                Command = Command.Attack;
            }
            else if (kb.cKey.wasPressedThisFrame)
            {
                Command = Command.Jump;
            }
            else if (kb.leftShiftKey.wasPressedThisFrame && dashTimer <= 0f)
            {
                dashTimer = dashCooldown;
                Command = Command.Dash;
            }
            else if (MoveDirection.sqrMagnitude > 0.0001f)
            {
                Command = Command.Move;
            }
        }
    }
}
