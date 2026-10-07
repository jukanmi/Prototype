using System.Collections.Generic;
using UnityEngine;

namespace Prototype
{
    public enum AssistPhase { Hidden, Arriving, Ready, Casting, Departing }

    /// <summary>A pooled companion's orders. The entity's state machine runs movement and skills.</summary>
    public sealed class AssistControlModule : Control
    {
        private readonly Queue<CastTicket> automatic = new Queue<CastTicket>();
        private CastTicket current;
        private readonly Queue<CastTicket> bullets = new Queue<CastTicket>();
        private SkillState skill;
        private AssistMoveState movement;
        private AssistManager manager;
        private float castElapsed;
        private float readyElapsed;
        private Vector3 formation;
        private bool moveToFormation;
        private SkillData preparedSkill;
        private Entity preparedTarget;

        public AssistPhase Phase { get; private set; } = AssistPhase.Hidden;
        public bool FormationHeld { get; private set; }
        public bool SelectionHeld { get; set; }
        public bool InFormation => FormationHeld && !moveToFormation &&
                                   Phase == AssistPhase.Ready;
        public bool HasOrders => current != null || bullets.Count > 0 || automatic.Count > 0;

        public void Configure(AssistManager owner)
        {
            manager = owner;
            Owner.TimeDomain = TimeDomain.Cast;
            Owner.SetControl(this);
            Owner.IsTargetable = false;
            Owner.Combat.AssistInvulnerable = true;
        }

        public void Enqueue(CastTicket ticket)
        {
            if (ticket.Request.BulletTime) bullets.Enqueue(ticket);
            else automatic.Enqueue(ticket);
            if (Phase == AssistPhase.Departing)
                BeginMove(Owner.Physics.GroundPosition, AssistPhase.Arriving);
        }

        public void DeferAutomatic(Queue<CastTicket> deferred)
        {
            while (automatic.Count > 0) deferred.Enqueue(automatic.Dequeue());
        }

        public void HoldSelection()
        {
            SelectionHeld = true;
            // Keep an already-visible caster available while the player chooses cards.
            if (Phase == AssistPhase.Departing)
                BeginMove(Owner.Physics.GroundPosition, AssistPhase.Arriving);
        }

        public void Appear(Vector3 spawn, Vector3 destination, Vector3 facing)
        {
            Owner.Physics.Teleport(spawn);
            Owner.Physics.Face(facing);
            BeginMove(destination, AssistPhase.Arriving);
        }

        public void HoldFormation(Vector3 point)
        {
            FormationHeld = true;
            SelectionHeld = false;
            formation = point;
            moveToFormation = true;
            // An automatic cast already in progress finishes before walking into formation.
            if (Phase != AssistPhase.Casting) BeginMove(point, AssistPhase.Arriving);
        }

        public void PrepareSkill(SkillData data, Entity target)
        {
            preparedSkill = data;
            preparedTarget = target;
            if (Phase == AssistPhase.Ready) Ready();
        }

        public void ReleaseFormation()
        {
            FormationHeld = false;
            preparedSkill = null;
            preparedTarget = null;
            SelectionHeld = false;
            moveToFormation = false;
            if (Phase == AssistPhase.Ready && !HasOrders) Depart();
        }

        public override void Tick(float dt)
        {
            Clear();
            if (dt <= 0f || Phase == AssistPhase.Hidden) return;
            if (Phase == AssistPhase.Arriving || Phase == AssistPhase.Departing)
            {
                if (!movement.IsFinished) return;
                if (Phase == AssistPhase.Departing) { manager.Recycle(this); return; }
                moveToFormation = false;
                Ready();
            }

            if (Phase == AssistPhase.Casting)
            {
                castElapsed += dt;
                if (skill is ChargeSkillState charge && charge.IsFullyCharged) charge.Release();
                float timeout = Mathf.Max(5f, current.Request.Skill.TotalDuration + 1f) +
                                (current.Request.Skill.IsCharge ? current.Request.Skill.maxChargeTime : 0f);
                bool interrupted = Owner.StateMachine.CurState != skill;
                if (!skill.IsFinished && !interrupted && castElapsed < timeout) return;
                bool cancelled = interrupted || !skill.IsFinished;
                if (!interrupted && !skill.IsFinished)
                    Debug.LogWarning($"Assist skill timed out: {current.Request.Skill.skillName}", Owner);
                CastTicket done = current;
                current = null;
                skill = null;
                Ready();
                if (moveToFormation) BeginMove(formation, AssistPhase.Arriving);
                done.Complete(cancelled);
                return;
            }

            if (Phase != AssistPhase.Ready) return;
            if (preparedTarget != null) Owner.Physics.Face(preparedTarget.transform.position - Owner.transform.position);
            if (moveToFormation) { BeginMove(formation, AssistPhase.Arriving); return; }
            if (bullets.Count > 0)
            {
                CastTicket next = bullets.Dequeue();
                StartCast(next);
            }
            else if (!FormationHeld && !SelectionHeld && automatic.Count > 0)
                StartCast(automatic.Dequeue());
            else if (!FormationHeld && !SelectionHeld && automatic.Count == 0)
            {
                readyElapsed += dt;
                if (readyElapsed >= manager.LingerSeconds) Depart();
            }
        }

        private void StartCast(CastTicket ticket)
        {
            if (!ticket.Request.IsValid) { ticket.Complete(true); return; }
            current = ticket;
            CastRequest request = ticket.Request;
            var context = new SkillContext
            {
                data = request.Skill, caster = request.Caster, targetInfo = request.Target, target = request.TargetEntity,
                damageScale = request.DamageScale, isBulletTime = request.BulletTime, comboIndex = 0,
            };
            skill = request.Skill.CreateState(in context) as SkillState;
            if (skill == null) { current = null; ticket.Complete(true); return; }
            Phase = AssistPhase.Casting;
            castElapsed = 0f;
            Owner.IsCommanded = true;
            Owner.StateMachine.ForceChangeState(skill);
        }

        private void Ready()
        {
            Phase = AssistPhase.Ready;
            readyElapsed = 0f;
            Owner.IsCommanded = true;
            Owner.StateMachine.ForceChangeState(new AssistReadyState(Owner, FormationHeld ? preparedSkill : null));
        }

        private void BeginMove(Vector3 point, AssistPhase phase)
        {
            Phase = phase;
            Owner.IsCommanded = true;
            movement = new AssistMoveState(Owner, point, manager.MoveSpeed);
            Owner.StateMachine.ForceChangeState(movement);
        }

        private void Depart() => BeginMove(manager.ExitPosition(Owner), AssistPhase.Departing);

        public void CancelAll()
        {
            var cancelled = new List<CastTicket>();
            if (current != null) cancelled.Add(current);
            while (bullets.Count > 0) cancelled.Add(bullets.Dequeue());
            while (automatic.Count > 0) cancelled.Add(automatic.Dequeue());
            current = null;
            skill = null;
            Phase = AssistPhase.Hidden;
            FormationHeld = SelectionHeld = moveToFormation = false;
            preparedSkill = null;
            preparedTarget = null;
            Owner.ClearIntent();
            Owner.Physics.Move(Vector3.zero, 0f);
            Owner.Physics.ResetInertia();
            Owner.Physics.Suspended = false;
            Owner.IsCommanded = false;
            Owner.StateMachine.ForceChangeState(Owner.IdleState);
            foreach (CastTicket ticket in cancelled) ticket.Complete(true);
        }
    }

    public sealed class AssistReadyState : EntityState
    {
        public SkillData PreparedSkill { get; }
        public AssistReadyState(Entity entity, SkillData preparedSkill = null) : base(entity) { PreparedSkill = preparedSkill; }
        public override bool CanBeInterrupted => false;
        public override void Enter() { Physics.Move(Vector3.zero, 0f); Physics.ResetInertia(); }
    }

    public sealed class AssistMoveState : EntityState
    {
        private readonly Vector3 target;
        private readonly float speed;
        private float elapsed;
        public bool IsFinished { get; private set; }
        public AssistMoveState(Entity entity, Vector3 target, float speed) : base(entity)
        { this.target = target; this.speed = speed; }
        public override bool CanBeInterrupted => false;
        public override void Enter() { Physics.ResetInertia(); Physics.Suspended = false; }
        public override void Tick(float dt)
        {
            elapsed += dt;
            Vector3 delta = target - Physics.GroundPosition;
            delta.y = 0f;
            if (delta.magnitude <= 0.08f || elapsed >= 3f)
            {
                // Bounded recovery if a wall or a narrow platform blocks the approach.
                Physics.Teleport(target);
                Physics.Move(Vector3.zero, 0f);
                IsFinished = true;
                return;
            }
            Physics.Face(delta);
            Physics.Move(delta.normalized, Mathf.Min(speed, delta.magnitude / Mathf.Max(dt, 0.001f)));
        }
        public override void Exit() => Physics.Move(Vector3.zero, 0f);
    }
}
