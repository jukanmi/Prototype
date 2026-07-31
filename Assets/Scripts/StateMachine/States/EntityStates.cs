using UnityEngine;

namespace Prototype
{
    /// <summary>Entity가 소유하는 상태의 공통 뼈대.</summary>
    public abstract class EntityState : IState
    {
        protected readonly Entity Entity;
        protected Physics Physics => Entity.Physics;
        protected Combat Combat => Entity.Combat;
        protected Control Control => Entity.Control;

        protected EntityState(Entity entity)
        {
            Entity = entity;
        }

        public virtual bool CanBeInterrupted => true;

        public virtual void Enter() { }
        public virtual void Tick(float dt) { }
        public virtual void Exit() { }

        /// <summary>이동 · 점프 · 대쉬 · 평타 · 스킬 명령을 공통 처리한다.</summary>
        protected bool HandleCommonCommands()
        {
            if (Control == null) return false;

            switch (Control.Command)
            {
                case Command.Attack:
                    Control.Consume();
                    Entity.StateMachine.TryChangeState(
                        Physics.PhysicsState == PhysicsState.Aerial ? Entity.AerialAttackState : (IState)Entity.AttackState);
                    return true;

                case Command.Jump:
                    Control.Consume();
                    Entity.StateMachine.TryChangeState(Entity.JumpState);
                    return true;

                case Command.Dash:
                    Control.Consume();
                    Physics.Dash(Control.MoveDirection, Entity.Stats.GetValue(StatType.DashSpeed, 18f));
                    return true;

                case Command.Move:
                    if (Entity.StateMachine.CurState != Entity.MoveState)
                    {
                        Entity.StateMachine.TryChangeState(Entity.MoveState);
                        return true;
                    }
                    return false;

                default:
                    return false;
            }
        }
    }

    /// <summary>기본. 아무것도 하지 않고 명령을 기다린다.</summary>
    public class IdleState : EntityState
    {
        public IdleState(Entity entity) : base(entity) { }

        public override void Enter()
        {
            Physics.Move(Vector3.zero, 0f);
        }

        public override void Tick(float dt)
        {
            HandleCommonCommands();
        }
    }

    /// <summary>이동. XZ축 가/감속.</summary>
    public class MoveState : EntityState
    {
        public MoveState(Entity entity) : base(entity) { }

        public override void Tick(float dt)
        {
            if (HandleCommonCommands()) return;

            Vector3 dir = Control != null ? Control.MoveDirection : Vector3.zero;
            if (dir.sqrMagnitude <= 0.0001f)
            {
                Physics.Move(Vector3.zero, 0f);
                Entity.StateMachine.TryChangeState(Entity.IdleState);
                return;
            }

            Physics.Move(dir, Entity.Stats.GetValue(StatType.MoveSpeed, 6f));
        }

        public override void Exit()
        {
            Physics.Move(Vector3.zero, 0f);
        }
    }

    /// <summary>점프. 목표 높이 · 도달 시간으로 중력을 역산한다.</summary>
    public class JumpState : EntityState
    {
        public JumpState(Entity entity) : base(entity) { }

        public override void Enter()
        {
            Physics.Jump(
                Entity.Stats.GetValue(StatType.JumpHeight, 2.5f),
                Entity.Stats.GetValue(StatType.JumpTime, 0.35f));
        }

        public override void Tick(float dt)
        {
            // 공중에서도 이동은 허용한다.
            if (Control != null && Control.MoveDirection.sqrMagnitude > 0.0001f)
                Physics.Move(Control.MoveDirection, Entity.Stats.GetValue(StatType.MoveSpeed, 6f) * 0.7f);

            if (Control != null && Control.Command == Command.Attack)
            {
                Control.Consume();
                Entity.StateMachine.TryChangeState(Entity.AerialAttackState);
                return;
            }

            if (Physics.PhysicsState == PhysicsState.Ground)
                Entity.StateMachine.TryChangeState(Entity.IdleState);
        }
    }

    /// <summary>평타. 선딜 → 히트박스 → 후딜.</summary>
    public class AttackState : EntityState
    {
        private float timer;

        public AttackState(Entity entity) : base(entity) { }

        // 평타는 중단 가능. 슈퍼아머는 SkillState 전용.
        public override bool CanBeInterrupted => true;

        public override void Enter()
        {
            timer = 0f;
            Physics.Move(Vector3.zero, 0f);
            if (Control != null && Control.MoveDirection.sqrMagnitude > 0.0001f)
                Physics.Face(Control.MoveDirection);
        }

        public override void Tick(float dt)
        {
            float prev = timer;
            timer += dt;

            bool windup = prev < Entity.BasicAttackWindup && timer >= Entity.BasicAttackWindup;

            // 원거리 평타는 선딜 끝에 투사체를 하나 쏘고 끝. 켜고 끌 히트박스가 없다.
            if (Entity.BasicIsRanged)
            {
                if (windup) Entity.FireBasicProjectile();

                if (timer >= Entity.BasicAttackTotal)
                    Entity.StateMachine.TryChangeState(Entity.IdleState);
                return;
            }

            Attack box = Entity.BasicAttack;
            if (box == null)
            {
                if (timer >= Entity.BasicAttackTotal)
                    Entity.StateMachine.TryChangeState(Entity.IdleState);
                return;
            }

            if (windup)
                box.Begin(Entity.BuildBasicHit());

            if (prev < Entity.BasicAttackActiveEnd && timer >= Entity.BasicAttackActiveEnd)
                box.End();

            if (timer >= Entity.BasicAttackTotal)
                Entity.StateMachine.TryChangeState(Entity.IdleState);
        }

        public override void Exit()
        {
            Entity.BasicAttack?.End();
        }
    }

    /// <summary>공중 공격. 착지하면 즉시 종료한다.</summary>
    public class AerialAttackState : EntityState
    {
        private float timer;

        public AerialAttackState(Entity entity) : base(entity) { }

        public override void Enter()
        {
            timer = 0f;

            if (Entity.BasicIsRanged) Entity.FireBasicProjectile();
            else Entity.BasicAttack?.Begin(Entity.BuildBasicHit());
        }

        public override void Tick(float dt)
        {
            timer += dt;

            if (Physics.PhysicsState == PhysicsState.Ground || timer >= Entity.BasicAttackTotal)
                Entity.StateMachine.TryChangeState(Entity.IdleState);
        }

        public override void Exit()
        {
            Entity.BasicAttack?.End();
        }
    }

    /// <summary>약경직. Combat이 경직 타이머를 관리하므로 여기서는 입력만 막는다.</summary>
    public class HitState : EntityState
    {
        public HitState(Entity entity) : base(entity) { }

        public override void Enter()
        {
            Physics.Move(Vector3.zero, 0f);
        }

        public override void Tick(float dt)
        {
            if (!CombatStateRules.IsStunned(Combat.CombatState))
                Entity.StateMachine.TryChangeState(Entity.IdleState);
        }
    }

    /// <summary>공중 피격 · 넉백 · 벽 바운드. 착지 판정은 Physics가 알린다.</summary>
    public class AerialHitState : EntityState
    {
        public AerialHitState(Entity entity) : base(entity) { }

        public override void Enter()
        {
            Physics.Move(Vector3.zero, 0f);
        }

        public override void Tick(float dt)
        {
            if (!CombatStateRules.IsStunned(Combat.CombatState))
                Entity.StateMachine.TryChangeState(Entity.IdleState);
        }
    }

    /// <summary>눕기. 무적이지만 바닥쓸기(OTG)에는 맞는다.</summary>
    public class DownState : EntityState
    {
        public DownState(Entity entity) : base(entity) { }

        public override void Enter()
        {
            Physics.ResetInertia();
        }
    }

    /// <summary>기상. 완전 무적 구간. Exit 시 공중 콤보 카운트가 리셋된다(결정 로그 ⑦).</summary>
    public class GetupState : EntityState
    {
        public GetupState(Entity entity) : base(entity) { }

        public override void Enter()
        {
            Physics.ResetInertia();
        }
    }

    /// <summary>
    /// 사망. ForceChangeState로만 진입하며 다시 나가지 않는다.
    /// 잠깐 쓰러져 있다가 페이드아웃 후 씬에서 빠진다.
    /// </summary>
    public class DeadState : EntityState
    {
        private float timer;
        private SpriteRenderer[] renderers;
        private Color[] baseColors;

        public DeadState(Entity entity) : base(entity) { }

        // 사망 이후로는 어떤 전이도 받지 않는다.
        public override bool CanBeInterrupted => false;

        public override void Enter()
        {
            timer = 0f;

            Physics.ResetInertia();
            Physics.Move(Vector3.zero, 0f);
            Entity.BasicAttack?.End();
            BattleRegistry.Unregister(Entity);

            // 시체가 산 캐릭터를 밀거나 맞지 않도록 판정을 전부 끈다.
            foreach (Collider c in Entity.GetComponentsInChildren<Collider>(true))
                c.enabled = false;

            CacheRenderers();
        }

        public override void Tick(float dt)
        {
            timer += dt;

            float delay = Entity.DespawnDelay;
            if (timer < delay) return;

            float fade = Entity.DespawnFade;
            float t = fade <= 0f ? 1f : Mathf.Clamp01((timer - delay) / fade);

            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] == null) continue;

                Color c = baseColors[i];
                c.a = baseColors[i].a * (1f - t);
                renderers[i].color = c;
            }

            if (t >= 1f)
                Entity.gameObject.SetActive(false);
        }

        /// <summary>원본 색은 최초 1회만 잡는다. 두 번 죽어도 알파가 0으로 굳지 않게.</summary>
        private void CacheRenderers()
        {
            renderers = Entity.GetComponentsInChildren<SpriteRenderer>(true);

            if (baseColors != null && baseColors.Length == renderers.Length) return;

            baseColors = new Color[renderers.Length];
            for (int i = 0; i < renderers.Length; i++)
                baseColors[i] = renderers[i] != null ? renderers[i].color : Color.white;
        }
    }
}
