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

        /// <summary>E — 불릿타임 진입 · 해제 요청.</summary>
        public bool BulletTimePressed { get; private set; }
        /// <summary>Spacebar — 콤보 실행 요청.</summary>
        public bool ExecutePressed { get; private set; }
        /// <summary>U — 손패 맨 왼쪽 카드 즉시 사용(실시간 전투).</summary>
        public bool CardUsePressed { get; private set; }
        /// <summary>Z X C V — 동료 고유기(실시간 전투). 없으면 -1.</summary>
        public int SelfSkillPressed { get; private set; } = -1;

        public override void Tick(float dt)
        {
            Clear();
            BulletTimePressed = false;
            ExecutePressed = false;
            CardUsePressed = false;
            SelfSkillPressed = -1;

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
            CardUsePressed = kb.uKey.wasPressedThisFrame;

            // 정지 중에는 이동 · 평타 입력을 받지 않는다.
            // WASD는 TargetSelector가 조준용으로 직접 읽어 간다.
            if (TimeControl.IsFrozen) return;

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
        /// 기획서는 ASDF를 지정했지만 WASD 이동과 충돌한다.
        /// YUIOP를 쓰다가 U를 카드 사용에 내주면서 ZXCV로 옮겼다.
        /// </summary>
        private static int ReadSelfSkillKey(Keyboard kb)
        {
            if (kb.zKey.wasPressedThisFrame) return 0;
            if (kb.xKey.wasPressedThisFrame) return 1;
            if (kb.cKey.wasPressedThisFrame) return 2;
            if (kb.vKey.wasPressedThisFrame) return 3;
            return -1;
        }
    }
}
