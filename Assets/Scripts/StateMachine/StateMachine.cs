// 엔티티 상태머신 — 규약 · 기계 · 상태 구현 모음.
// 전술 층(TacticStateMachine)과는 다른 층이다. 인터페이스를 공유하지 않는다.

using System;
using UnityEngine;

namespace Prototype
{
    // ══ IState ═══════════════════════════════════════════

    /// <summary>
    /// 상태 하나. 자체 dt를 받는다 — Time.timeScale을 쓰지 않기 때문(결정 로그 ⑥).
    /// </summary>
    public interface IState
    {
        /// <summary>
        /// false면 <see cref="StateMachine.TryChangeState"/>가 전이를 거부한다.
        /// SkillState는 false — 슈퍼아머(결정 로그 ③).
        /// </summary>
        bool CanBeInterrupted { get; }

        void Enter();
        void Tick(float dt);
        void Exit();
    }

    // ══ StateMachine ═══════════════════════════════════════════

    /// <summary>
    /// 상태 전이 관문. 슈퍼아머와 콤보 지휘가 모두 여기를 통과한다.
    /// </summary>
    public class StateMachine
    {
        public IState CurState { get; private set; }

        /// <summary>로그 식별용. Entity가 자기 이름을 넣어 준다.</summary>
        public string OwnerName { get; set; } = "?";

        public event Action<IState, IState> OnStateChanged; // (prev, next)

        /// <summary>
        /// 일반 전이. 현재 상태가 중단 불가면 <b>거부</b>한다(결정 로그 ③).
        /// </summary>
        /// <returns>전이가 실제로 일어났으면 true.</returns>
        public bool TryChangeState(IState next)
        {
            if (next == null || next == CurState)
                return false;

            if (CurState != null && !CurState.CanBeInterrupted)
            {
                BattleLog.Log(LogCategory.State,
                    $"{OwnerName}: {BattleLog.StateName(CurState)} → {BattleLog.StateName(next)} <color=#FF6B6B>거부</color> (중단 불가)");
                return false;
            }

            Change(next);
            return true;
        }

        /// <summary>
        /// 무조건 전이. <b>사망 처리</b>와 <b>콤보 지휘</b> 전용(결정 로그 ②③).
        /// ComboExecutor가 동료의 상태머신을 강탈할 때 쓴다.
        /// </summary>
        public void ForceChangeState(IState next)
        {
            if (next == null || next == CurState)
                return;

            if (CurState != null && !CurState.CanBeInterrupted)
                BattleLog.Log(LogCategory.State,
                    $"{OwnerName}: {BattleLog.StateName(CurState)} <color=#FFD166>관통</color> → {BattleLog.StateName(next)}");

            Change(next);
        }

        public void Tick(float dt)
        {
            CurState?.Tick(dt);
        }

        private void Change(IState next)
        {
            IState prev = CurState;
            prev?.Exit();
            CurState = next;
            next.Enter();

            BattleLog.Log(LogCategory.State,
                $"{OwnerName}: {BattleLog.StateName(prev)} → <b>{BattleLog.StateName(next)}</b>");

            OnStateChanged?.Invoke(prev, next);
        }
    }

    // ══ EntityStates ═══════════════════════════════════════════

    /// <summary>Entity가 소유하는 상태의 공통 뼈대.</summary>
    public abstract class EntityState : IState
    {
        protected readonly Entity Entity;
        protected Physics Physics => Entity.Physics;
        protected Combat Combat => Entity.Combat;

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
            switch (Entity.Command)
            {
                case Command.Attack:
                    Entity.Consume();
                    Entity.StateMachine.TryChangeState(
                        Physics.PhysicsState == PhysicsState.Aerial ? Entity.AerialAttackState : (IState)Entity.AttackState);
                    return true;

                case Command.Jump:
                    Entity.Consume();
                    Entity.StateMachine.TryChangeState(Entity.JumpState);
                    return true;

                case Command.Dash:
                    Entity.Consume();
                    Physics.Dash(Entity.MoveDirection, Entity.Stats.GetValue(StatType.DashSpeed, 18f));
                    // 패링은 유저가 직접 민 대시에만 열린다. AI 대시까지 무적을 주면 난이도가 통째로 흔들린다.
                    if (Entity.IsPiloted) Combat.BeginParryWindow();
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

            Vector3 dir = Entity.MoveDirection;
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
            if (Entity.MoveDirection.sqrMagnitude > 0.0001f)
                Physics.Move(Entity.MoveDirection, Entity.Stats.GetValue(StatType.MoveSpeed, 6f) * 0.7f);

            if (Entity.Command == Command.Attack)
            {
                Entity.Consume();
                Entity.StateMachine.TryChangeState(Entity.AerialAttackState);
                return;
            }

            if (Physics.PhysicsState == PhysicsState.Ground)
                Entity.StateMachine.TryChangeState(Entity.IdleState);
        }
    }

    /// <summary>
    /// 평타. 선딜 → 히트박스 → 후딜.
    ///
    /// 단계(<see cref="BasicAttackStage"/>)를 저작한 몸은 여기서 <b>연타</b>가 된다.
    /// 상태를 새로 만들지 않고 안에서 인덱스를 올리는 이유는 두 가지다 —
    /// <see cref="StateMachine.TryChangeState"/>가 같은 상태로의 재진입을 거부하고,
    /// <see cref="Exit"/>이 히트박스를 닫아 버리기 때문이다.
    /// </summary>
    public class AttackState : EntityState
    {
        private float timer;
        private int stage;

        public AttackState(Entity entity) : base(entity) { }

        // 평타는 중단 가능. 슈퍼아머는 SkillState 전용.
        public override bool CanBeInterrupted => true;

        /// <summary>지금 몇 타째인가(0-base). 애니메이터가 클립을 고를 때 읽는다.</summary>
        public int Stage => stage;

        public override void Enter()
        {
            timer = 0f;
            stage = 0;
            Entity.SetActiveAttackStage(0, Entity.GetBasicStageTiming(0).total);

            Physics.Move(Vector3.zero, 0f);
            if (Entity.MoveDirection.sqrMagnitude > 0.0001f)
                Physics.Face(Entity.MoveDirection);

            // 이 상태로 들어온 그 입력을 버린다. 안 버리면 같은 한 번의 입력이
            // 여기 들어오게 만들고 곧바로 2타 예약까지 해서, 한 번 눌렀는데 두 대가 나간다.
            Entity.ClearAttackBuffer();

            // 예고는 여기서 켜지 않는다 — 켤 시점은 Tick이 "타격까지 남은 시간"으로 판단한다.
            // 쿨 구간에서 이미 켜 뒀으면 그대로 이어진다.
        }

        public override void Tick(float dt)
        {
            BasicAttackTiming t = Entity.GetBasicStageTiming(stage);

            float prev = timer;
            timer += dt;

            bool windup = prev < t.windup && timer >= t.windup;

            // 선딜 동안 번쩍인다. 쿨 끝자락에서 이미 켜져 있고 여기서 이어받는다.
            // 히트박스가 켜지는 순간 꺼진다 — 그 뒤는 피할 수 없는 구간이라 신호를 주면 거짓말이 된다.
            Entity.SetTelegraph(timer < t.windup);

            // 원거리 평타는 선딜 끝에 투사체를 하나 쏘고 끝. 켜고 끌 히트박스가 없다.
            // 연타 대상도 아니다 — 쏘는 손맛은 캔슬이 아니라 사거리에서 온다.
            if (Entity.BasicIsRanged)
            {
                if (windup) Entity.FireBasicProjectile();

                if (timer >= t.total)
                    Entity.StateMachine.TryChangeState(Entity.IdleState);
                return;
            }

            Attack box = Entity.BasicAttack;

            if (box != null)
            {
                if (windup) box.Begin(Entity.BuildBasicHit(stage));
                if (prev < t.activeEnd && timer >= t.activeEnd) box.End();
            }

            // 판정은 전부 순수 규칙에 맡긴다 — 우선순위를 여기 흩어 두면
            // "마지막 프레임에 Finish가 Advance를 이겨 콤보가 한 타에서 멈추는" 실수를 눈으로 잡아야 한다.
            // 들여다보기만 한다. 매 틱 소비하면 캔슬 시점 전에 누른 입력이 그 자리에서 증발한다 —
            // 실제로 쓰는 순간에만 비운다.
            bool buffered = Entity.HasAttackBuffer;

            switch (BasicComboRules.Decide(timer, in t, stage, Entity.BasicComboStageCount, buffered))
            {
                case BasicComboStep.Advance:
                    Entity.ClearAttackBuffer();
                    GoToStage(stage + 1);
                    break;

                case BasicComboStep.Finish:
                    Entity.StateMachine.TryChangeState(Entity.IdleState);
                    break;
            }
        }

        /// <summary>다음 타로 넘어간다. 상태를 벗어나지 않으므로 히트박스를 손으로 닫아야 한다.</summary>
        private void GoToStage(int next)
        {
            // 아직 열려 있으면 닫는다. Attack이 Begin/End 양쪽에서 alreadyHit을 비우므로
            // 같은 적이 1·2·3타를 전부 맞는다.
            Entity.BasicAttack?.End();

            stage = next;
            timer = 0f;

            BasicAttackTiming t = Entity.GetBasicStageTiming(stage);
            Entity.SetActiveAttackStage(stage, t.total);

            // 타마다 다시 조준할 수 있게 한다. 벨트스크롤에서 1타 뒤에 옆 적으로 못 돌면
            // 콤보가 보상이 아니라 벌이 된다.
            Physics.Move(Vector3.zero, 0f);
            if (Entity.MoveDirection.sqrMagnitude > 0.0001f)
                Physics.Face(Entity.MoveDirection);

            Entity.Animator?.PlayBasicAttackStage(stage, t.total);

            BattleLog.Log(LogCategory.Combat,
                $"{Entity.name} 평타 {stage + 1}/{Entity.BasicComboStageCount}타", Entity);
        }

        public override void Exit()
        {
            // 선딜 도중 경직으로 끊기면 예고가 켜진 채로 굳는다.
            Entity.SetTelegraph(false);
            Entity.BasicAttack?.End();

            // 상태를 벗어나면 콤보는 끊긴다. 별도의 콤보 타임아웃이 필요 없는 이유가 이것이다 —
            // 피격 · 이동 · 대시로 빠지면 다음 Enter가 1타부터 다시 시작한다.
            stage = 0;
        }
    }

    /// <summary>
    /// 공중 공격. 착지하면 즉시 종료한다.
    ///
    /// <b>지상 평타와 같은 단계 표를 쓴다</b>(<see cref="BasicAttackStage"/>) — 즉 공중에서도 연타가 된다.
    /// 예전에는 한 대 때리고 <c>BasicAttackTotal</c>이 지나기만 기다렸다. 그래서 띄운 적을 쫓아
    /// 올라가도 한 대밖에 못 넣었고, 공중 콤보의 마무리를 전부 스킬에 기대야 했다.
    ///
    /// 지상과 다른 점은 <b>끝나는 조건</b> 하나다 — 착지하면 몇 타째든 그 자리에서 끝난다.
    /// 마지막 타를 다 쓰면 <see cref="BasicComboStep.Finish"/>로 Idle에 나가는 것도 같다.
    /// </summary>
    public class AerialAttackState : EntityState
    {
        private float timer;
        private int stage;

        public AerialAttackState(Entity entity) : base(entity) { }

        /// <summary>지금 몇 타째인가(0-base). 애니메이터가 클립을 고를 때 읽는다.</summary>
        public int Stage => stage;

        public override void Enter()
        {
            timer = 0f;
            stage = 0;

            Entity.SetActiveAttackStage(0, Entity.GetBasicStageTiming(0).total);

            // 지상 평타와 같은 이유로 이 상태에 들어오게 만든 입력을 버린다.
            // 안 버리면 한 번의 점프 공격이 곧바로 2타까지 예약해 버린다.
            Entity.ClearAttackBuffer();

            FireStage();
        }

        public override void Tick(float dt)
        {
            // 착지가 최우선이다. 몇 타째든, 히트박스가 열려 있든 여기서 끝난다.
            if (Physics.PhysicsState == PhysicsState.Ground)
            {
                Entity.StateMachine.TryChangeState(Entity.IdleState);
                return;
            }

            BasicAttackTiming t = Entity.GetBasicStageTiming(stage);

            float prev = timer;
            timer += dt;

            // 원거리는 선딜 끝에 한 발 쏘고 끝이라 켜고 끌 히트박스가 없다.
            // 연타 대상도 아니다 — 지상 평타(AttackState)와 같은 규약이다.
            if (Entity.BasicIsRanged)
            {
                if (timer >= t.total)
                    Entity.StateMachine.TryChangeState(Entity.IdleState);
                return;
            }

            if (prev < t.activeEnd && timer >= t.activeEnd)
                Entity.BasicAttack?.End();

            bool buffered = Entity.HasAttackBuffer;

            switch (BasicComboRules.Decide(timer, in t, stage, Entity.BasicComboStageCount, buffered))
            {
                case BasicComboStep.Advance:
                    Entity.ClearAttackBuffer();
                    GoToStage(stage + 1);
                    break;

                case BasicComboStep.Finish:
                    Entity.StateMachine.TryChangeState(Entity.IdleState);
                    break;
            }
        }

        /// <summary>
        /// 다음 타로 넘어간다. <see cref="AttackState.GoToStage"/>와 같은 일을 하되
        /// 방향 재조준(<c>Physics.Move</c>)은 하지 않는다 — 공중에서는 관성이 이동을 이미 쥐고 있다.
        /// </summary>
        private void GoToStage(int next)
        {
            Entity.BasicAttack?.End();

            stage = next;
            timer = 0f;

            BasicAttackTiming t = Entity.GetBasicStageTiming(stage);
            Entity.SetActiveAttackStage(stage, t.total);
            Entity.Animator?.PlayBasicAttackStage(stage, t.total);

            FireStage();

            BattleLog.Log(LogCategory.Combat,
                $"{Entity.name} 공중 평타 {stage + 1}/{Entity.BasicComboStageCount}타", Entity);
        }

        /// <summary>
        /// 이 단계의 타격을 낸다. 공중 공격은 선딜을 두지 않는다 —
        /// 체공 시간이 짧아 선딜을 기다리면 착지가 먼저 오고 한 대도 못 넣는다.
        /// </summary>
        private void FireStage()
        {
            if (Entity.BasicIsRanged) Entity.FireBasicProjectile();
            else Entity.BasicAttack?.Begin(Entity.BuildBasicHit(stage));
        }

        public override void Exit()
        {
            Entity.BasicAttack?.End();
            stage = 0;
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

    /// <summary>
    /// 행동 불능(스턴). <b>시간은 <see cref="StatusEffects"/>가 재고 여기는 몸만 붙잡는다</b> —
    /// 타이머를 상태에도 두면 화면과 게이지가 서로 다른 숫자를 부른다.
    ///
    /// 게이팅이 이 클래스의 존재 이유다. <see cref="CanBeInterrupted"/>가 false면
    /// <see cref="Entity.IsBusy"/>가 true가 되어 입력(PlayerPilot) · AI(EnemyControl) ·
    /// 카드 시전(Ally.CanCastCard)이 <b>이미 있는 게이트</b>로 전부 막힌다. 그리고
    /// 여기가 <c>HandleCommonCommands</c>를 부르지 않으므로 이동 · 점프 · 대시 · 평타도 같이 죽는다.
    ///
    /// 들어오고 나가는 판정은 <see cref="Combat"/>의 정합 루프 한 곳에만 있다.
    /// 여기서도 재면 규칙이 두 벌이 된다.
    /// </summary>
    public class StunState : EntityState
    {
        public StunState(Entity entity) : base(entity) { }

        public override bool CanBeInterrupted => false;

        public override void Enter()
        {
            // Move(zero)지 ResetInertia가 아니다. 관성을 지우면 넉백까지 먹어
            // 굳은 적을 벽으로 밀어붙이는 그림이 사라진다(HitState.Enter와 같은 처리).
            Physics.Move(Vector3.zero, 0f);
        }

        public override void Tick(float dt) { }
    }

    /// <summary>
    /// 빙결. <see cref="StunState"/>와 규칙이 같고 지속시간 · 연출만 다르다.
    ///
    /// 코드가 같은데도 클래스를 나눈 이유: 둘은 같은 것의 두 이름이 아니라 서로 다른 저작 단위다.
    /// "빙결 대상은 다음 타격에 추가 피해" 같은 게 붙을 자리를 미리 갈라 둔 것이고,
    /// 그때 <see cref="StunState"/>를 건드리지 않아야 한다.
    /// </summary>
    public class FrozenState : EntityState
    {
        public FrozenState(Entity entity) : base(entity) { }

        public override bool CanBeInterrupted => false;

        public override void Enter()
        {
            Physics.Move(Vector3.zero, 0f);
        }

        public override void Tick(float dt) { }
    }

    /// <summary>
    /// 공중 바인드. 빙결과 같은 붙잡기다 — 몸을 붙잡는 일만 한다.
    ///
    /// <b>물리 정지(<see cref="Physics.Suspended"/>)는 여기서 켜지 않는다.</b> 바인드는 띄워 둔 적에게
    /// 거는 게 본래 용도인데, 이 상태 진입은 피격 경직이 끝나길 기다리고 공중 경직은 착지로만 끝난다.
    /// 여기에 묶으면 떨어진 뒤에야 붙잡는다. 스위치는 <c>Combat.SyncDebuffState</c>가 시간(Statuses)을 보고
    /// 직접 켜고 끈다 — 소유자가 하나여야 어긋나지 않는다.
    /// </summary>
    public class AirBoundState : EntityState
    {
        public AirBoundState(Entity entity) : base(entity) { }

        public override bool CanBeInterrupted => false;

        public override void Enter()
        {
            Physics.Move(Vector3.zero, 0f);
        }

        public override void Tick(float dt) { }
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
