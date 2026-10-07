using System;

namespace Prototype
{
    /// <summary>Card systems supply only a caster, skill and targeting context.</summary>
    public readonly struct CastRequest
    {
        public readonly Ally Caster;
        public readonly SkillData Skill;
        public readonly TargetInfo Target;
        public readonly float DamageScale;
        public readonly bool BulletTime;
        public readonly Entity TargetEntity;

        public CastRequest(Ally caster, SkillData skill, TargetInfo target,
                           float damageScale = 1f, bool bulletTime = false, Entity targetEntity = null)
        {
            Caster = caster;
            Skill = skill;
            Target = target;
            DamageScale = damageScale;
            BulletTime = bulletTime;
            TargetEntity = targetEntity;
        }
        public bool IsValid => Caster != null && Skill != null &&
                               Caster.Combat != null && !Caster.Combat.IsDead;
    }

    public sealed class CastTicket
    {
        public CastRequest Request { get; }
        public bool IsDone { get; private set; }
        public bool WasCancelled { get; private set; }
        private readonly Action<CastTicket> completed;

        public CastTicket(CastRequest request, Action<CastTicket> completed = null)
        {
            Request = request;
            this.completed = completed;
        }
        public void Complete(bool cancelled = false)
        {
            if (IsDone) return;
            IsDone = true;
            WasCancelled = cancelled;
            completed?.Invoke(this);
        }
    }
}
