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
        ///
        /// 기본 구현은 콤보 QTE 배율(<see cref="SkillContext.QteMultiplier"/>)을 적용한다 —
        /// 차징 스킬을 포함해 모든 스킬이 공통으로 받는 보너스이기 때문.
        /// </summary>
        protected virtual HitData ModifyHit(HitData hit)
        {
            float mul = ctx.QteMultiplier;
            if (mul == 1f) return hit;

            hit.damageData.damage *= mul;
            hit.knockbackForce *= mul;
            hit.launchForce *= mul;
            return hit;
        }

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

            // 순간이동 뒤라야 조준했던 좌표에 정확히 뜬다.
            PlaceCaster();
            EmitCastVfx();
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
        /// 결정론적 가이드. <b>근거리 직업은 시전 위치로 순간이동한 뒤에 시작한다</b> —
        /// 제자리에서 휘두르면 조준한 곳에 판정이 안 닿아 콤보가 통째로 헛돈다.
        /// 원거리 직업은 사거리가 있으니 방향만 맞춘다.
        /// </summary>
        private void PlaceCaster()
        {
            Physics phys = ctx.CasterPhysics;
            if (phys == null) return;

            if (TryGetCastSpot(phys, out Vector3 spot))
                phys.Teleport(spot);

            phys.Face(FaceDirection(phys));

            // 시전 중 관성은 전부 끊는다. 연계가 밀리는 오차를 차단.
            phys.ResetInertia();
        }

        /// <summary>
        /// 근거리 직업인지. 스킬 카드는 직업에 묶여 있으므로(<c>Ally.EquipSkill</c>)
        /// 에셋의 role이 곧 시전자의 직업이다.
        ///
        /// 투사체 유무로 가르지 않는 이유: 위저드 장판은 투사체가 없지만 근거리가 아니다.
        /// 그걸 근접으로 보면 마법사가 적진 한가운데로 순간이동한다.
        /// </summary>
        private bool IsMeleeCaster => data.role == Role.Tanker || data.role == Role.Warrior;

        /// <summary>
        /// 시전을 시작할 자리. 옮길 필요가 없으면 false.
        /// </summary>
        private bool TryGetCastSpot(Physics phys, out Vector3 spot)
        {
            spot = default;
            Vector3 from = phys.GroundPosition;

            switch (ctx.targetInfo.type)
            {
                case TargetingType.GroundPoint:
                    // 찍은 좌표가 곧 시전 위치다.
                    // 원거리 직업은 불릿타임에 한해 옮긴다 — 기존 동작(위저드 장판 텔포)을 유지한다.
                    if (!IsMeleeCaster && !ctx.isBulletTime) return false;

                    spot = new Vector3(ctx.targetInfo.point.x, from.y, ctx.targetInfo.point.z);
                    return true;

                case TargetingType.EnemyUnit:
                    return IsMeleeCaster && TryApproach(from, ctx.targetInfo.unit, out spot);

                default:
                    // 방향 지정 · 조준 없음에는 찍은 좌표가 없다.
                    // 그래도 근거리면 붙어야 하므로 콤보가 잡아 둔 대상으로 간다.
                    return IsMeleeCaster && TryApproach(from, ctx.target, out spot);
            }
        }

        /// <summary>대상 옆에 서는 자리. 오던 쪽에 붙는다 — 대상을 관통해 넘어가지 않게.</summary>
        private bool TryApproach(Vector3 from, Entity target, out Vector3 spot)
        {
            spot = default;
            if (target == null || target.Physics == null) return false;
            if (target.Combat != null && target.Combat.IsDead) return false;

            Vector3 t = target.Physics.GroundPosition;
            float side = from.x >= t.x ? 1f : -1f;

            // 벨트스크롤에서 Z가 어긋나면 후속타가 전부 빗나간다(Physics.SnapZ와 같은 이유).
            // X만 띄우고 깊이는 대상과 같은 레인에 맞춘다.
            spot = new Vector3(t.x + side * data.ApproachDistance, from.y, t.z);

            // 이미 그 자리면 옮기지 않는다. 매 타격마다 미세하게 튀는 걸 막는다.
            Vector3 d = spot - from;
            d.y = 0f;
            return d.sqrMagnitude > 0.04f;
        }

        /// <summary>시전 후 바라볼 방향.</summary>
        private Vector3 FaceDirection(Physics phys)
        {
            Vector3 from = phys.GroundPosition;

            switch (ctx.targetInfo.type)
            {
                case TargetingType.Direction:
                    return ctx.targetInfo.direction;

                case TargetingType.EnemyUnit:
                    if (ctx.targetInfo.unit != null)
                        return ctx.targetInfo.unit.Physics.GroundPosition - from;
                    break;

                case TargetingType.GroundPoint:
                    // 찍은 자리로 이미 옮겼다면 그 지점을 봐도 방향이 안 나온다. 대상 쪽을 본다.
                    if (ctx.target != null)
                        return ctx.target.Physics.GroundPosition - from;

                    Vector3 toPoint = ctx.targetInfo.point - from;
                    toPoint.y = 0f;
                    if (toPoint.sqrMagnitude > 0.0001f) return toPoint;
                    break;
            }

            if (ctx.target != null)
                return ctx.target.Physics.GroundPosition - from;

            return phys.Facing;
        }

        /// <summary>
        /// 스킬이 터지는 자리를 한 번 크게 알린다.
        /// 반경은 판정과 같은 값을 쓴다 — 차징으로 커진 배율까지 그대로 따라간다.
        /// </summary>
        private void EmitCastVfx()
        {
            SkillVfx style = data.vfx.AsSkill();
            BattleVfx.Cast(ctx.Origin, data.radius * ctx.RadiusScale, in style);
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

            // 히트박스는 스킬끼리 공유된다(SkillAttack). 켤 때마다 이번 스킬 색을 다시 실어 준다.
            // AsSkill()로 스킬 표시를 같이 넘겨야 평타보다 크게 그려진다.
            SkillVfx style = data.vfx.AsSkill();
            box.Begin(in hit, in style);
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

            SkillVfx style = data.vfx.AsSkill();

            shot.Launch(ctx.caster.Combat, in hit,
                        phys.GroundPosition, AimDirection(phys),
                        data.projectileSpeed, data.projectileRange, data.projectilePierce,
                        phys.WallMask, layer, in style);
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
