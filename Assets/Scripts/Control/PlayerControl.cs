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

        /// <summary>E — 불릿타임 진입 요청.</summary>
        public bool BulletTimePressed { get; private set; }
        /// <summary>Spacebar — 콤보 실행 요청.</summary>
        public bool ExecutePressed { get; private set; }
        /// <summary>Y U I O P — 동료 고유기(실시간 전투). 없으면 -1.</summary>
        public int SelfSkillPressed { get; private set; } = -1;

        /// <summary>A / D — 손패 커서 좌우 이동(불릿타임 중). -1, 0, +1.</summary>
        public int HandCursorDelta { get; private set; }
        /// <summary>J — 손패 커서 위치의 카드를 결정(불릿타임 중).</summary>
        public bool ConfirmPressed { get; private set; }

        public override void Tick(float dt)
        {
            Clear();
            BulletTimePressed = false;
            ExecutePressed = false;
            SelfSkillPressed = -1;
            HandCursorDelta = 0;
            ConfirmPressed = false;

            if (dashTimer > 0f) dashTimer -= dt;

            HandleInput();
        }

        private void HandleInput()
        {
            Keyboard kb = Keyboard.current;
            if (kb == null) return;

            // 지휘 입력은 시간 정지 중에도 받아야 하므로 상태와 무관하게 먼저 읽는다.
            BulletTimePressed = kb.eKey.wasPressedThisFrame;
            ExecutePressed = kb.spaceKey.wasPressedThisFrame;

            // 시간이 멈춰 있는 동안에는 같은 키가 손패 조작으로 바뀐다.
            // A/D는 이동, J는 평타와 겹치므로 여기서 갈라 놓는다.
            if (TimeControl.IsFrozen)
            {
                HandCursorDelta = (kb.dKey.wasPressedThisFrame ? 1 : 0) - (kb.aKey.wasPressedThisFrame ? 1 : 0);
                ConfirmPressed = kb.jKey.wasPressedThisFrame;
                return;
            }

            if (Owner != null && Owner.IsBusy) return;
            if (Owner != null && CombatStateRules.IsStunned(Owner.Combat.CombatState)) return;

            // 벨트스크롤 — 좌우는 X축, 위아래는 Z축(깊이).
            float x = (kb.dKey.isPressed ? 1f : 0f) - (kb.aKey.isPressed ? 1f : 0f);
            float z = (kb.wKey.isPressed ? 1f : 0f) - (kb.sKey.isPressed ? 1f : 0f);
            MoveDirection = new Vector3(x, 0f, z);

            SelfSkillPressed = ReadSelfSkillKey(kb);

            if (kb.jKey.wasPressedThisFrame)
            {
                Command = Command.Attack;
            }
            else if (kb.kKey.wasPressedThisFrame)
            {
                Command = Command.Jump;
            }
            else if (kb.leftShiftKey.wasPressedThisFrame && dashTimer <= 0f)
            {
                dashTimer = dashCooldown;
                Command = Command.Dash;
            }
            else if (SelfSkillPressed >= 0)
            {
                Command = Command.Skill;
                SkillIndex = SelfSkillPressed;
            }
            else if (MoveDirection.sqrMagnitude > 0.0001f)
            {
                Command = Command.Move;
            }
        }

        /// <summary>
        /// 실시간 전투 동료 고유기.
        /// 기획서는 ASDF를 지정했지만 WASD 이동과 충돌하므로 YUIOP로 옮겼다.
        /// </summary>
        private static int ReadSelfSkillKey(Keyboard kb)
        {
            if (kb.yKey.wasPressedThisFrame) return 0;
            if (kb.uKey.wasPressedThisFrame) return 1;
            if (kb.iKey.wasPressedThisFrame) return 2;
            if (kb.oKey.wasPressedThisFrame) return 3;
            if (kb.pKey.wasPressedThisFrame) return 4;
            return -1;
        }
    }
}
