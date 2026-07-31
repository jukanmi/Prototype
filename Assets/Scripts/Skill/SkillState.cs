using UnityEngine;

namespace Prototype
{
    /// <summary>
    /// 스킬의 런타임 인스턴스. 실제로 동료가 움직이고 때리는 건 전부 여기서 일어난다.
    /// 피격당해도 중단되지 않는다 — 슈퍼아머(결정 로그 ③).
    /// 사망만 <see cref="StateMachine.ForceChangeState"/>로 관통한다.
    /// </summary>
    public class SkillState : IState
    {
        private readonly SkillData data;
        private SkillContext ctx;

        private float timer;
        private int nextHitIndex;
        private float nextHitTime;
        private bool finished;

        public SkillState(SkillData data, in SkillContext ctx)
        {
            this.data = data;
            this.ctx = ctx;
        }

        /// <summary>슈퍼아머. TryChangeState는 전부 거부된다.</summary>
        public virtual bool CanBeInterrupted => false;

        /// <summary>시전 중인 스킬. EntityAnimator가 클립을 갈아끼울 때 읽는다.</summary>
        public SkillData Data => data;

        /// <summary>후딜까지 끝났는지. ComboExecutor가 다음 슬롯으로 넘어갈 시점 판단에 쓴다.</summary>
        public virtual bool IsFinished => finished;

        /// <summary>파생 상태(차징)가 시전 맥락을 읽고 고칠 수 있게 연다.</summary>
        protected ref SkillContext Context => ref ctx;

        /// <summary>
        /// 타격 직전에 HitData를 손볼 마지막 기회. 차징이 배율을 여기서 태운다.
        /// 원본 에셋은 건드리지 않는다 — 값 복사본만 바뀐다.
        /// </summary>
        protected virtual HitData ModifyHit(HitData hit) => hit;

        public virtual void Enter()
        {
            timer = 0f;
            nextHitIndex = 0;
            finished = false;
            nextHitTime = data.castTime;

            BattleLog.Log(LogCategory.Skill,
                $"<b>{BattleLog.Name(ctx.caster)}</b> 시전: {data.skillName} | {data.attackType} | 선행 {data.requireState} → 결과 {data.resultState} | " +
                $"조준 {ctx.targetInfo.type} | {(ctx.isBulletTime ? "불릿타임" : "라이브")} | 히트 {data.hitDataList.Count}단",
                ctx.caster);

            PlaceCaster();
            ApplyEffects();

            // 선딜이 없으면 즉시 첫 타를 낸다.
            if (data.castTime <= 0f)
                FireNextHit();
        }

        public virtual void Tick(float dt)
        {
            if (finished) return;

            timer += dt;

            while (nextHitIndex < data.hitDataList.Count && timer >= nextHitTime)
                FireNextHit();

            if (nextHitIndex >= data.hitDataList.Count && timer >= EndTime)
            {
                ctx.caster?.SkillAttack?.End();
                finished = true;

                BattleLog.Log(LogCategory.Skill,
                    $"{BattleLog.Name(ctx.caster)} 종료: {data.skillName} (후딜 {data.recoveryTime:0.##}s 포함 {EndTime:0.##}s)", ctx.caster);
            }
        }

        public virtual void Exit()
        {
            ctx.caster?.SkillAttack?.End();
            finished = true;
        }

        protected float EndTime
        {
            get
            {
                int hits = Mathf.Max(1, data.hitDataList.Count);
                return data.castTime + data.hitInterval * (hits - 1) + data.recoveryTime;
            }
        }

        /// <summary>
        /// 결정론적 가이드. 불릿타임으로 발동한 스킬은 유저가 찍은 좌표로 순간이동한다.
        /// 라이브 페이즈에서는 방향만 맞춘다.
        /// </summary>
        private void PlaceCaster()
        {
            Physics phys = ctx.CasterPhysics;
            if (phys == null) return;

            switch (ctx.targetInfo.type)
            {
                case TargetingType.GroundPoint:
                    if (ctx.isBulletTime)
                        phys.Teleport(ctx.targetInfo.point);
                    else
                        phys.Face(ctx.targetInfo.point - phys.Transform.position);
                    break;

                case TargetingType.EnemyUnit:
                    if (ctx.targetInfo.unit != null)
                        phys.Face(ctx.targetInfo.unit.transform.position - phys.Transform.position);
                    break;

                case TargetingType.Direction:
                    phys.Face(ctx.targetInfo.direction);
                    break;

                default:
                    if (ctx.target != null)
                        phys.Face(ctx.target.transform.position - phys.Transform.position);
                    break;
            }

            // 시전 중 관성은 전부 끊는다. 연계가 밀리는 오차를 차단.
            phys.ResetInertia();
        }

        private void ApplyEffects()
        {
            if (data.effects == null) return;

            for (int i = 0; i < data.effects.Count; i++)
            {
                if (data.effects[i] == null) continue;

                BattleLog.Log(LogCategory.Skill,
                    $"  └ 효과 적용: {data.effects[i].GetType().Name}", ctx.caster);
                data.effects[i].Apply(in ctx);
            }
        }

        private void FireNextHit()
        {
            if (nextHitIndex >= data.hitDataList.Count) return;

            HitData hit = ModifyHit(data.hitDataList[nextHitIndex]);

            if (data.IsRanged) LaunchProjectile(in hit);
            else FireMelee(in hit);

            BattleLog.Log(LogCategory.Skill,
                $"  └ {data.skillName} {nextHitIndex + 1}/{data.hitDataList.Count}타 발동 (t={timer:0.##}s)", ctx.caster);

            nextHitIndex++;
            nextHitTime = data.castTime + data.hitInterval * nextHitIndex;
        }

        /// <summary>근접 — 시전자에게 붙은 히트박스를 켠다. 기존 동작.</summary>
        private void FireMelee(in HitData hit)
        {
            Attack box = ctx.caster != null ? ctx.caster.SkillAttack : null;
            if (box == null) return;

            if (box.Attacker == null && ctx.caster != null)
                box.Attacker = ctx.caster.Combat;

            box.Begin(in hit);
        }

        /// <summary>원거리 — 타격마다 투사체를 하나씩 쏜다.</summary>
        private void LaunchProjectile(in HitData hit)
        {
            Physics phys = ctx.CasterPhysics;
            if (phys == null || ctx.caster == null) return;

            Projectile shot = Object.Instantiate(data.projectile);

            // 히트박스 레이어를 그대로 물려받아야 충돌 매트릭스가 맞는다.
            int layer = ctx.caster.BasicAttack != null
                ? ctx.caster.BasicAttack.gameObject.layer
                : ctx.caster.gameObject.layer;

            shot.Launch(ctx.caster.Combat, in hit,
                        phys.GroundPosition, AimDirection(phys),
                        data.projectileSpeed, data.projectileRange, data.projectilePierce,
                        phys.WallMask, layer);
        }

        /// <summary>조준값에서 발사 방향을 뽑는다. 없으면 바라보는 쪽.</summary>
        private Vector3 AimDirection(Physics phys)
        {
            Vector3 from = phys.GroundPosition;

            switch (ctx.targetInfo.type)
            {
                case TargetingType.Direction:
                    return ctx.targetInfo.direction;

                case TargetingType.GroundPoint:
                    return ctx.targetInfo.point - from;

                case TargetingType.EnemyUnit:
                    if (ctx.targetInfo.unit != null)
                        return ctx.targetInfo.unit.Physics.GroundPosition - from;
                    break;
            }

            return ctx.target != null
                ? ctx.target.Physics.GroundPosition - from
                : phys.Facing;
        }
    }
}
