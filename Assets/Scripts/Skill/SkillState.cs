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
        private readonly SkillContext ctx;

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
        public bool CanBeInterrupted => false;

        /// <summary>후딜까지 끝났는지. ComboExecutor가 다음 슬롯으로 넘어갈 시점 판단에 쓴다.</summary>
        public bool IsFinished => finished;

        public void Enter()
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

        public void Tick(float dt)
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

        public void Exit()
        {
            ctx.caster?.SkillAttack?.End();
            finished = true;
        }

        private float EndTime
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

            HitData hit = data.hitDataList[nextHitIndex];
            Attack box = ctx.caster != null ? ctx.caster.SkillAttack : null;

            if (box != null)
            {
                if (box.Attacker == null && ctx.caster != null)
                    box.Attacker = ctx.caster.Combat;
                box.Begin(in hit);
            }

            BattleLog.Log(LogCategory.Skill,
                $"  └ {data.skillName} {nextHitIndex + 1}/{data.hitDataList.Count}타 발동 (t={timer:0.##}s)", ctx.caster);

            nextHitIndex++;
            nextHitTime = data.castTime + data.hitInterval * nextHitIndex;
        }
    }
}
