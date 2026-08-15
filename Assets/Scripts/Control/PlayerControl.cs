using UnityEngine;

namespace Prototype
{
    /// <summary>
    /// 유저가 직접 모는 몸. 태그로 필드에 선 <b>한 명</b>이 이걸로 움직인다 —
    /// 플레이어 캐릭터든 동료든 같은 컴포넌트를 쓴다.
    ///
    /// 입력을 직접 읽지 않는다. <see cref="PlayerInputController"/>가 읽어 온 것을
    /// 상태머신이 아는 <see cref="Command"/>로 옮기는 것만 한다.
    ///
    /// 그 컨트롤러를 <c>GetComponent</c>가 아니라 <see cref="PlayerInputController.Instance"/>로
    /// 잡는 이유는 <b>입력이 몸에서 떨어져 나갔기 때문</b>이다. 태그로 내려간 몸은
    /// <c>SetActive(false)</c>가 되는데, 입력이 거기 붙어 있으면 교대하는 순간
    /// 되돌아올 키까지 함께 죽는다.
    ///
    /// 지휘키(E · U · F)는 몸과 무관하므로 <see cref="BattleCommander"/>가 따로 읽는다.
    /// </summary>
    public class PlayerControl : Control
    {
        [SerializeField] private float dashCooldown = 0.6f;

        private float dashTimer;

        /// <summary>
        /// 매번 <c>Instance</c>를 본다. 캐싱하면 씬을 다시 열거나 입력 호스트가 교체됐을 때
        /// 파괴된 인스턴스를 붙들고 조용히 입력이 죽는다.
        /// </summary>
        private static PlayerInputController Input
            => PlayerInputController.Instance != null ? PlayerInputController.Instance : null;

        public override void Tick(float dt)
        {
            Clear();

            if (dashTimer > 0f) dashTimer -= dt;

            // 지휘 중에는 어떤 명령도 내지 않는다. 상태머신은 Executor 소유다 —
            // 콤보가 도는 동안 조작 대상이 그 몸이면 입력과 시전이 겹친다.
            if (Owner != null && Owner.IsCommanded) return;

            if (Input == null) return;
            HandleInput();
        }

        private void HandleInput()
        {
            // 정지 중에는 이동 · 평타 입력을 받지 않는다.
            // 조준은 BulletTime 맵의 Aim이 따로 받는다.
            if (TimeControl.IsFrozen) return;

            PlayerInputController input = Input;

            // 차징 중도 해제는 IsBusy 게이트보다 먼저 본다 — 모으는 중에는 슈퍼아머라
            // 아래 입력이 전부 막히기 때문이다.
            if (TryReleaseCharge(input)) return;

            if (Owner != null && Owner.IsBusy) return;
            if (Owner != null && CombatStateRules.IsStunned(Owner.Combat.CombatState)) return;

            // 벨트스크롤 — 좌우는 X축, 위아래는 Z축(깊이).
            Vector2 move = input.Move;
            MoveDirection = new Vector3(move.x, 0f, move.y);

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
            else if (MoveDirection.sqrMagnitude > 0.0001f)
            {
                Command = Command.Move;
            }
        }

        /// <summary>
        /// 점프키로 차징을 중도 해제한다. <b>모은 만큼</b> 그 자리에서 터진다 —
        /// 게이지가 다 차기를 기다리면 최대 위력이고, 일찍 끊으면 그만큼 약하다.
        /// 위력과 타이밍을 유저가 직접 저울질하게 만드는 것이 이 키의 목적이다.
        ///
        /// 점프를 고른 이유는 모으는 동안 유일하게 놀고 있는 키라서다 —
        /// 이동은 제자리 고정, 평타 · 대시는 슈퍼아머에 막힌다.
        ///
        /// 불릿타임 콤보(<see cref="Entity.IsCommanded"/>)는 건드리지 않는다.
        /// 그쪽 해제는 큐를 다 비운 <see cref="ComboExecutor"/>의 몫이고,
        /// 애초에 <see cref="Tick"/>이 지휘 중에 여기까지 오지 않는다.
        /// </summary>
        private bool TryReleaseCharge(PlayerInputController input)
        {
            if (!input.JumpPressed || Owner == null) return false;

            if (!(Owner.StateMachine?.CurState is IChargeState charge) || !charge.IsCharging)
                return false;

            BattleLog.Log(LogCategory.Skill,
                $"{BattleLog.Name(Owner)} 차징 중도 해제 — 모은 양 {charge.ChargeRatio * 100f:0}%", this);

            charge.Release();
            return true;
        }
    }
}
