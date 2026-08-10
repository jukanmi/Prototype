using UnityEngine;

namespace Prototype
{
    /// <summary>
    /// 커맨더 조작. 직접 콤보를 치지 않고 이동 · 평타 · 불릿타임 지휘를 담당한다.
    ///
    /// 입력을 직접 읽지 않는다 — <see cref="PlayerInputController"/>가 읽어 온 것을
    /// 상태머신이 아는 <see cref="Command"/>로 옮기는 것만 한다.
    /// 지휘키(E · Space · U)는 아무 조건 없이 통과하므로 여기를 거치지 않고
    /// <see cref="Player"/>가 컨트롤러에서 곧바로 읽는다.
    /// </summary>
    public class PlayerControl : Control
    {
        [SerializeField] private float dashCooldown = 0.6f;

        private float dashTimer;
        private PlayerInputController input;

        /// <summary>
        /// 동료 고유기 슬롯 0~3, 없으면 -1.
        /// <b>게이트를 통과한 값</b>이다 — 정지 · 경직 · 행동 중에는 눌러도 -1이다.
        /// <see cref="Player"/>가 동료에게 시전을 넘길 때도 이 값을 봐야 지휘가 겹치지 않는다.
        /// </summary>
        public int SelfSkillPressed { get; private set; } = -1;

        protected override void Awake()
        {
            base.Awake();
            input = GetComponent<PlayerInputController>();
        }

        public override void Tick(float dt)
        {
            Clear();
            SelfSkillPressed = -1;

            if (dashTimer > 0f) dashTimer -= dt;

            if (input == null) return;
            HandleInput();
        }

        private void HandleInput()
        {
            // 정지 중에는 이동 · 평타 입력을 받지 않는다.
            // 조준은 BulletTime 맵의 Aim이 따로 받는다.
            if (TimeControl.IsFrozen) return;

            if (Owner != null && Owner.IsBusy) return;
            if (Owner != null && CombatStateRules.IsStunned(Owner.Combat.CombatState)) return;

            // 벨트스크롤 — 좌우는 X축, 위아래는 Z축(깊이).
            Vector2 move = input.Move;
            MoveDirection = new Vector3(move.x, 0f, move.y);

            SelfSkillPressed = input.SkillPressed;

            if (input.AttackPressed)
            {
                Command = Command.Attack;
            }
            else if (input.JumpPressed)
            {
                Command = Command.Jump;
            }
            else if (input.DashPressed && dashTimer <= 0f)
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
    }
}
